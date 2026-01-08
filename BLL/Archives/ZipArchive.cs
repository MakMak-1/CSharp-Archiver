using Ionic.Zip;
using System.Linq;
using System.Reflection.Emit;

namespace BLL.Archives;

public class ZipArchive : IArchive
{
    private readonly string _filePath;

    public ZipArchive(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public bool IsPasswordProtected
    {
        get
        {
            using (var zip = ZipFile.Read(FilePath))
            {
                // Check if any file inside is encrypted
                return zip.Entries.Any(e => e.UsesEncryption);
            }
        }
    }

    public Task AddFilesAsync(IEnumerable<string> sourceFilePaths, string pathInArchive, string? password, ArchiveCompressionLevel level, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            if (!IsPasswordCorrect(password))
            {
                throw new UnauthorizedAccessException("The provided password is incorrect for this archive.");
            }

            progress?.Report(new ProgressReport(0, "Opening archive..."));

            using (var zip = ZipFile.Read(_filePath))
            {
                // Ensure UTF-8 Encoding for filenames
                zip.AlternateEncoding = System.Text.Encoding.UTF8;
                zip.AlternateEncodingUsage = ZipOption.Always;

                if (!string.IsNullOrEmpty(password))
                {
                    zip.Password = password;
                    zip.Encryption = EncryptionAlgorithm.WinZipAes256;
                }

                zip.CompressionLevel = level switch
                {
                    ArchiveCompressionLevel.Store => Ionic.Zlib.CompressionLevel.None,
                    ArchiveCompressionLevel.Fast => Ionic.Zlib.CompressionLevel.BestSpeed,
                    ArchiveCompressionLevel.Best => Ionic.Zlib.CompressionLevel.BestCompression,
                    _ => Ionic.Zlib.CompressionLevel.Default
                };

                foreach (var file in sourceFilePaths)
                {
                    if (!File.Exists(file))
                    {
                        continue; // Skip non-existent files
                    }

                    try
                    {
                        // If pathInArchive is empty, it goes to root.
                        zip.AddFile(file, pathInArchive ?? string.Empty);
                    }
                    catch (ArgumentException ex)
                    {
                        // Handle cases where duplicate files are being added.
                        progress?.Report(new ProgressReport(0, $"Warning: Could not add file '{file}': {ex.Message}"));
                        continue;
                    }
                }

                // Hook up SaveProgress (Add operations trigger a Save)
                zip.SaveProgress += (sender, e) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        e.Cancel = true;
                        return;
                    }

                    if (e.EventType == ZipProgressEventType.Saving_EntryBytesRead)
                    {
                        progress?.Report(new ProgressReport(0, $"Saving: {e.CurrentEntry.FileName}"));
                    }
                };

                progress?.Report(new ProgressReport(50, "Saving changes..."));
                try
                {
                    zip.Save();
                }
                catch (ZipException ex) when (ex.Message.Contains("Cancelled"))
                {
                    throw new OperationCanceledException(token);
                }
            }

            token.ThrowIfCancellationRequested();
            progress?.Report(new ProgressReport(100, "Files added successfully."));
        }, token);
    }

    public Task AddFilesAsync(IEnumerable<string> sourceFilePaths, string pathInArchive, ArchiveCompressionLevel level, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return AddFilesAsync(sourceFilePaths, pathInArchive, null, level, progress, token);
    }

    public Task DeleteEntriesAsync(IEnumerable<string> entryPaths, string? password, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            if (!IsPasswordCorrect(password))
            {
                throw new UnauthorizedAccessException("The provided password is incorrect for this archive.");
            }

            progress?.Report(new ProgressReport(0, "Opening archive..."));

            using (var zip = ZipFile.Read(_filePath))
            {
                // Ensure UTF-8 Encoding for filenames
                zip.AlternateEncoding = System.Text.Encoding.UTF8;
                zip.AlternateEncodingUsage = ZipOption.Always;

                // DotNetZip cannot delete while iterating the Entries collection directly.
                // We must first find the entries, then remove them.

                // We use a HashSet for fast lookups of the paths we want to delete.
                var pathsToDelete = new HashSet<string>(entryPaths);

                var entriesToRemove = new List<ZipEntry>();

                foreach (var entry in zip.Entries)
                {
                    if (pathsToDelete.Contains(entry.FileName))
                    {
                        entriesToRemove.Add(entry);
                    }
                }

                if (entriesToRemove.Count == 0)
                {
                    progress?.Report(new ProgressReport(100, "No matching entries found to delete."));
                    return;
                }

                // Perform removal
                foreach (var entry in entriesToRemove)
                {
                    zip.RemoveEntry(entry);
                }

                // Hook up SaveProgress
                zip.SaveProgress += (sender, e) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        e.Cancel = true;
                        return;
                    }
                };

                progress?.Report(new ProgressReport(50, "Saving changes..."));
                try
                {
                    zip.Save();
                }
                catch (ZipException ex) when (ex.Message.Contains("Cancelled"))
                {
                    throw new OperationCanceledException(token);
                }
            }

            token.ThrowIfCancellationRequested();
            progress?.Report(new ProgressReport(100, "Entries deleted."));

        }, token);
    }

    public Task DeleteEntriesAsync(IEnumerable<string> entryPaths, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return DeleteEntriesAsync(entryPaths, null, progress, token);
    }

    public Task ExtractAsync(string destinationDirectory, string? password, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            if (!IsPasswordCorrect(password))
            {
                throw new UnauthorizedAccessException("The provided password is incorrect for this archive.");
            }

            if (!Directory.Exists(destinationDirectory))
            {
                throw new DirectoryNotFoundException($"The destination directory '{destinationDirectory}' does not exist.");
            }

            progress?.Report(new ProgressReport(0, "Preparing to extract..."));

            using (var zip = ZipFile.Read(FilePath))
            {
                // Ensure UTF-8 Encoding for filenames
                zip.AlternateEncoding = System.Text.Encoding.UTF8;
                zip.AlternateEncodingUsage = ZipOption.Always;

                zip.Password = password;

                // Hook up ExtractProgress
                zip.ExtractProgress += (sender, e) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        e.Cancel = true;
                        return;
                    }

                    if (e.EventType == ZipProgressEventType.Extracting_EntryBytesWritten)
                    {
                        if (e.TotalBytesToTransfer > 0)
                        {
                            int percent = (int)((double)e.BytesTransferred / e.TotalBytesToTransfer * 100);
                            progress?.Report(new ProgressReport(percent, $"Extracting: {e.CurrentEntry.FileName}"));
                        }
                    }
                };

                try
                {
                    zip.ExtractAll(destinationDirectory, ExtractExistingFileAction.OverwriteSilently);
                }
                catch (ZipException ex) when (ex.Message.Contains("Cancelled"))
                {
                    throw new OperationCanceledException(token);
                }
            }

            token.ThrowIfCancellationRequested();
            progress?.Report(new ProgressReport(100, "Extraction complete."));

        }, token);
    }

    public Task ExtractAsync(string destinationDirectory, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return ExtractAsync(destinationDirectory, null, progress, token);
    }

    public Task<IEnumerable<IArchiveEntry>> GetEntriesAsync(string? password, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return Task.Run<IEnumerable<IArchiveEntry>>(() =>
        {
            if (!IsPasswordCorrect(password))
            {
                throw new UnauthorizedAccessException("The provided password is incorrect for this archive.");
            }

            var entriesList = new List<IArchiveEntry>();

            progress?.Report(new ProgressReport(0, "Opening archive..."));

            // Check for cancellation before doing heavy I/O
            token.ThrowIfCancellationRequested();

            using (ZipFile zip = ZipFile.Read(FilePath))
            {
                // Ensure UTF-8 Encoding for filenames
                zip.AlternateEncoding = System.Text.Encoding.UTF8;
                zip.AlternateEncodingUsage = ZipOption.Always;

                zip.Password = password;
                int totalCount = zip.Count;
                int current = 0;

                foreach (var entry in zip)
                {
                    token.ThrowIfCancellationRequested();

                    // Map the DotNetZip 'ZipEntry' to 'IArchiveEntry' DTO.
                    var dto = new ArchiveEntry
                    {
                        Name = Path.GetFileName(entry.FileName.TrimEnd('/')),
                        FullName = entry.FileName,
                        Size = entry.UncompressedSize,
                        LastWriteTime = new DateTimeOffset(entry.LastModified),
                        IsDirectory = entry.IsDirectory
                    };

                    entriesList.Add(dto);
                    current++;

                    // Report progress periodically
                    if (totalCount > 0 && current % 10 == 0)
                    {
                        int percent = (int)((double)current / totalCount * 100);
                        progress?.Report(new ProgressReport(percent, $"Reading entry {current} of {totalCount}..."));
                    }
                }
            }

            progress?.Report(new ProgressReport(100, "Archive loaded."));

            return entriesList;
        }, token);
    }

    public Task<IEnumerable<IArchiveEntry>> GetEntriesAsync(IProgress<ProgressReport> progress, CancellationToken token)
    {
        return GetEntriesAsync(null, progress, token);
    }

    private bool IsPasswordCorrect(string? password)
    {
        if (!IsPasswordProtected)
        {
            return true;
        }

        if (String.IsNullOrEmpty(password))
        {
            return false;
        }

        return ZipFile.CheckZipPassword(FilePath, password);
    }
}
