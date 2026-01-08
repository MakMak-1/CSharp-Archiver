using SevenZip;

namespace BLL.Archives;

public class SevenZArchive : IArchive
{
    private readonly string _filePath;

    // Static constructor to set up the library path once
    // '7z.dll' must be in the output directory
    static SevenZArchive()
    {
        string dllName = Environment.Is64BitProcess ? "7z64.dll" : "7z.dll";
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dllName);

        if (File.Exists(path))
        {
            SevenZipBase.SetLibraryPath(path);
        }
        else
        {
            throw new FileNotFoundException($"Could not find {dllName}. Please copy it to the output directory.");
        }
    }

    public SevenZArchive(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public bool IsPasswordProtected
    {
        get
        {
            if (!File.Exists(_filePath)) return false;

            try
            {
                // Try to read the archive structure without a password.
                using (var extractor = new SevenZipExtractor(_filePath))
                {
                    // If headers are encrypted, accessing ArchiveFileData will likely throw.
                    _ = extractor.ArchiveFileData.Count;

                    // If we can read the list, check if individual files are encrypted
                    return extractor.ArchiveFileData.Any(x => x.Encrypted);
                }
            }
            catch (SevenZipArchiveException)
            {
                // Exception usually means encrypted headers prevented reading
                return true;
            }
        }
    }

    public Task AddFilesAsync(IEnumerable<string> sourceFilePaths, string pathInArchive, ArchiveCompressionLevel level, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return AddFilesAsync(sourceFilePaths, pathInArchive, null, level, progress, token);
    }

    public Task AddFilesAsync(IEnumerable<string> sourceFilePaths, string pathInArchive, string? password, ArchiveCompressionLevel level, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            if (!IsPasswordCorrect(password))
            {
                throw new UnauthorizedAccessException("The provided password is incorrect.");
            }

            // Create a set to store file names that are ALREADY in the archive.
            var existingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_filePath))
            {
                try
                {
                    // We must open the archive to see what's inside.
                    // Note: If the archive is encrypted, we need the password to read headers (sometimes) 
                    // or just to list files depending on encryption settings.
                    using (var extractor = string.IsNullOrEmpty(password)
                            ? new SevenZipExtractor(_filePath)
                            : new SevenZipExtractor(_filePath, password))
                    {
                        foreach (var fileData in extractor.ArchiveFileData)
                        {
                            // 7-zip usually stores paths with forward slashes internally.
                            // We normalize to be sure.
                            existingFiles.Add(fileData.FileName.Replace("\\", "/"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Handle cases where the archive might be corrupted or locked.
                    // For now, we might rethrow or log. 
                    throw new IOException($"Could not read existing archive: {ex.Message}", ex);
                }
            }

            // Use SevenZipCompressor in Append mode
            var compressor = new SevenZipCompressor
            {
                CompressionMode = CompressionMode.Append,
                ArchiveFormat = OutArchiveFormat.SevenZip,
                // If we want to preserve specific paths, we handle directory structure manually
                DirectoryStructure = false
            };

            // Map Compression Level
            compressor.CompressionLevel = level switch
            {
                ArchiveCompressionLevel.Store => SevenZip.CompressionLevel.None,
                ArchiveCompressionLevel.Fast => SevenZip.CompressionLevel.Fast,
                ArchiveCompressionLevel.Best => SevenZip.CompressionLevel.Ultra,
                _ => SevenZip.CompressionLevel.Normal
            };

            if (!string.IsNullOrEmpty(password))
            {
                compressor.ZipEncryptionMethod = ZipEncryptionMethod.Aes256;
            }

            // Hook up events
            compressor.Compressing += (s, e) =>
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Operation cancelled by user.");
                }
                progress?.Report(new ProgressReport(e.PercentDone, "Adding files..."));
            };

            // Also hook into File Started for cleaner cancellation where supported
            compressor.FileCompressionStarted += (s, e) =>
            {
                if (token.IsCancellationRequested) e.Cancel = true;
            };

            // 7-Zip library requires a Dictionary<filename, fullpath> to handle paths correctly
            // when DirectoryStructure = false. This is how we map "C:\temp\file.txt" to "Folder\file.txt" inside archive.
            var fileMap = new Dictionary<string, string>();

            foreach (var file in sourceFilePaths)
            {
                if (!File.Exists(file)) continue;

                string fileName = Path.GetFileName(file);
                // Combine the virtual path (e.g. "Docs/") with the filename
                string entryName = string.IsNullOrEmpty(pathInArchive)
                   ? fileName
                    : Path.Combine(pathInArchive, fileName).Replace("\\", "/"); // 7z uses forward slashes

                // Ensure uniqueness in dictionary
                if (!fileMap.ContainsKey(entryName) && !existingFiles.Contains(entryName))
                {
                    fileMap.Add(entryName, file);
                }
            }

            try
            {
                // Execute compression
                if (string.IsNullOrEmpty(password))
                    compressor.CompressFileDictionary(fileMap, _filePath);
                else
                    compressor.CompressFileDictionary(fileMap, _filePath, password);

                progress?.Report(new ProgressReport(100, "Files added."));
            }
            catch (Exception ex) when (ex is OperationCanceledException 
                                    || ex.Message.Contains("cancelled") 
                                    || token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }

        }, token);
    }

    public Task DeleteEntriesAsync(IEnumerable<string> entryPaths, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return DeleteEntriesAsync(entryPaths, null, progress, token);
    }

    public Task DeleteEntriesAsync(IEnumerable<string> entryPaths, string? password, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            if (!IsPasswordCorrect(password))
            {
                throw new UnauthorizedAccessException("The provided password is incorrect.");
            }

            progress?.Report(new ProgressReport(0, "Scanning archive..."));

            // To delete from 7z, we must map Paths to Indices (Integers)
            List<int> indicesToDelete = new List<int>();
            var pathsSet = new HashSet<string>(entryPaths.Select(p => p.Replace("/", "\\")));

            using (var extractor = new SevenZipExtractor(_filePath, password))
            {
                foreach (var entry in extractor.ArchiveFileData)
                {
                    if (pathsSet.Contains(entry.FileName))
                    {
                        indicesToDelete.Add(entry.Index);
                    }
                }
            }

            if (indicesToDelete.Count == 0)
            {
                progress?.Report(new ProgressReport(100, "No matching entries found."));
                return;
            }

            // Configure Compressor for Modification
            var compressor = new SevenZipCompressor
            {
                CompressionMode = CompressionMode.Append,
                ArchiveFormat = OutArchiveFormat.SevenZip
            };

            // ModifyArchive uses a Dictionary<Index, NewName>. 
            // Passing NULL as the value means "Delete this index".
            var modifyMap = new Dictionary<int, string?>();
            foreach (int index in indicesToDelete)
            {
                modifyMap.Add(index, null);
            }

            // Hook up events (ModifyArchive usually reports via Compressing event)
            compressor.Compressing += (s, e) =>
            {
                token.ThrowIfCancellationRequested();
                progress?.Report(new ProgressReport(e.PercentDone, "Applying deletions..."));
            };

            try
            {
                // This method rebuilds the archive without the specified indices
                compressor.ModifyArchive(_filePath, modifyMap, password);
                progress?.Report(new ProgressReport(100, "Entries deleted."));
            }
            catch (Exception ex) when (ex is OperationCanceledException
                                    || ex.Message.Contains("cancelled")
                                    || token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }

        }, token);
    }

    public Task ExtractAsync(string destinationDirectory, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return ExtractAsync(destinationDirectory, null, progress, token);
    }

    public Task ExtractAsync(string destinationDirectory, string? password, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            if (!IsPasswordCorrect(password))
            {
                throw new UnauthorizedAccessException("The provided password is incorrect.");
            }

            using (var extractor = new SevenZipExtractor(_filePath, password))
            {
                // Hook up events
                extractor.Extracting += (s, e) =>
                {
                    token.ThrowIfCancellationRequested();
                    progress?.Report(new ProgressReport(e.PercentDone, "Extracting..."));
                };

                extractor.FileExtractionStarted += (s, e) =>
                {
                    if (token.IsCancellationRequested) e.Cancel = true;
                };

                try
                {
                    extractor.ExtractArchive(destinationDirectory);
                }
                catch (Exception ex) when (ex is OperationCanceledException
                                    || ex.Message.Contains("cancelled")
                                    || token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
            }

            progress?.Report(new ProgressReport(100, "Extraction complete."));
        }, token);
    }

    public Task<IEnumerable<IArchiveEntry>> GetEntriesAsync(IProgress<ProgressReport> progress, CancellationToken token)
    {
        return GetEntriesAsync(null, progress, token);
    }

    public Task<IEnumerable<IArchiveEntry>> GetEntriesAsync(string? password, IProgress<ProgressReport> progress, CancellationToken token)
    {
        return Task.Run<IEnumerable<IArchiveEntry>>(() =>
        {
            if (!IsPasswordCorrect(password))
            {
                throw new UnauthorizedAccessException("The provided password is incorrect.");
            }

            var entriesList = new List<IArchiveEntry>();
            progress?.Report(new ProgressReport(0, "Reading archive directory..."));

            using (var extractor = new SevenZipExtractor(_filePath, password))
            {
                int total = (int)extractor.FilesCount;
                int current = 0;

                foreach (var item in extractor.ArchiveFileData)
                {
                    token.ThrowIfCancellationRequested();

                    // Map to DTO
                    var dto = new ArchiveEntry
                    {
                        // 7z usually stores paths like "folder\file.txt". 
                        Name = Path.GetFileName(item.FileName),
                        FullName = item.FileName,
                        Size = (long)item.Size,
                        LastWriteTime = item.LastWriteTime != DateTime.MinValue ? new DateTimeOffset(item.LastWriteTime) : DateTimeOffset.Now,
                        IsDirectory = item.IsDirectory
                    };

                    entriesList.Add(dto);
                    current++;

                    if (total > 0 && current % 5 == 0)
                    {
                        progress?.Report(new ProgressReport((int)((double)current / total * 100), $"Reading {item.FileName}..."));
                    }
                }
            }

            progress?.Report(new ProgressReport(100, "Ready."));
            return entriesList;

        }, token);
    }

    private bool IsPasswordCorrect(string? password)
    {
        // 1. If not protected, logic is trivial
        if (!IsPasswordProtected) return true;
        if (string.IsNullOrEmpty(password)) return false;

        try
        {
            using (var extractor = new SevenZipExtractor(_filePath, password))
            {
                // --- CHECK HEADER ENCRYPTION ---
                // If headers are encrypted, accessing ArchiveFileData will throw 
                // an exception immediately if the password is wrong.
                var files = extractor.ArchiveFileData;

                // If we are here, the headers are readable.
                if (files.Count == 0) return true;

                // --- FIND SMALLEST FILE ---
                int testIndex = -1;
                ulong minSize = long.MaxValue;

                for (int i = 0; i < files.Count; i++)
                {
                    if (!files[i].IsDirectory)
                    {
                        if (files[i].Size < minSize)
                        {
                            minSize = files[i].Size;
                            testIndex = i;
                        }
                    }
                }

                // If only folders exist
                if (testIndex == -1) return true;

                // --- SIZE THRESHOLD ---
                // If the SMALLEST file is larger than this, do not attempt to verify.
                // It is better to let the user in than to freeze the app for minutes.
                ulong sizeLimitBytes = 50 * 1024 * 1024; // 50 MB

                if (minSize > sizeLimitBytes)
                {
                    // OPTIMISTIC SUCCESS: 
                    // We assume the password is correct because verifying it 
                    // would take too long. If it's wrong, the extraction/add 
                    // operation will fail later, which is acceptable.
                    return true;
                }

                // --- EXTRACT TEST (Fast for small files) ---
                extractor.ExtractFile(testIndex, Stream.Null);

                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }
}