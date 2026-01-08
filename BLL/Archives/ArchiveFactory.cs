using Ionic.Zip;
using SevenZip;

namespace BLL.Archives;

public class ArchiveFactory : IArchiveFactory
{
    public IArchive GetArchive(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("The specified archive file does not exist.", archivePath);

        string extension = Path.GetExtension(archivePath).ToLowerInvariant();

        return extension switch
        {
            ".zip" => new ZipArchive(archivePath),
            ".7z" => new SevenZArchive(archivePath),
            _ => throw new NotSupportedException($"Archive format '{extension}' is not supported.")
        };
    }

    public Task<IArchive> CreateFromDirectoryAsync(
        string sourceDirectory,
        string destinationArchivePath,
        ArchiveCompressionLevel compressionLevel,
        IProgress<ProgressReport> progress,
        CancellationToken token)
    {
        // Delegate to the main method with null password
        return CreateFromDirectoryAsync(sourceDirectory, destinationArchivePath, null, compressionLevel, progress, token);
    }

    public Task<IArchive> CreateFromDirectoryAsync(
        string sourceDirectory,
        string destinationArchivePath,
        string? password,
        ArchiveCompressionLevel compressionLevel,
        IProgress<ProgressReport> progress,
        CancellationToken token)
    {
        string extension = Path.GetExtension(destinationArchivePath).ToLowerInvariant();

        return extension switch
        {
            ".zip" => CreateZipArchiveAsync(sourceDirectory, destinationArchivePath, password, compressionLevel, progress, token),
            ".7z" => CreateSevenZipArchiveAsync(sourceDirectory, destinationArchivePath, password, compressionLevel, progress, token),
            _ => throw new NotSupportedException($"Cannot create archive with format '{extension}'. Supported formats are.zip and.7z.")
        };
    }

    // --- Internal Creation Logic for ZIP ---
    private Task<IArchive> CreateZipArchiveAsync(
        string sourceDir,
        string destPath,
        string? password,
        ArchiveCompressionLevel level,
        IProgress<ProgressReport> progress,
        CancellationToken token)
    {
        return Task.Run<IArchive>(() =>
        {
            progress?.Report(new ProgressReport(0, "Preparing Zip archive..."));

            using (var zip = new ZipFile())
            {
                // Ensure UTF-8 Encoding for filenames
                zip.AlternateEncoding = System.Text.Encoding.UTF8;
                zip.AlternateEncodingUsage = ZipOption.Always;

                // Map Compression Level
                zip.CompressionLevel = level switch
                {
                    ArchiveCompressionLevel.Store => Ionic.Zlib.CompressionLevel.None,
                    ArchiveCompressionLevel.Fast => Ionic.Zlib.CompressionLevel.BestSpeed,
                    ArchiveCompressionLevel.Best => Ionic.Zlib.CompressionLevel.BestCompression,
                    _ => Ionic.Zlib.CompressionLevel.Default
                };

                // Apply Encryption if password provided
                if (!string.IsNullOrEmpty(password))
                {
                    zip.Password = password;
                    zip.Encryption = EncryptionAlgorithm.WinZipAes256;
                }

                if (sourceDir != null)
                {
                    zip.AddDirectory(sourceDir);
                }

                // Progress Handler
                zip.SaveProgress += (s, e) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        e.Cancel = true;
                        return;
                    }

                    if (e.EventType == ZipProgressEventType.Saving_EntryBytesRead)
                    {
                        // Estimate progress based on entries saved vs total
                        double val = (double)e.EntriesSaved / e.EntriesTotal * 100.0;
                        progress?.Report(new ProgressReport((int)val, $"Archiving {e.CurrentEntry.FileName}..."));
                    }
                };

                try
                {
                    zip.Save(destPath);
                }
                catch (ZipException ex) when (ex.Message.Contains("Cancelled"))
                {
                    throw new OperationCanceledException(token);
                }
            }

            progress?.Report(new ProgressReport(100, "Zip created successfully."));
            return new ZipArchive(destPath);

        }, token);
    }

    // --- Internal Creation Logic for 7-Zip ---
    private Task<IArchive> CreateSevenZipArchiveAsync(
        string sourceDir,
        string destPath,
        string? password,
        ArchiveCompressionLevel level,
        IProgress<ProgressReport> progress,
        CancellationToken token)
    {
        return Task.Run<IArchive>(async () =>
        {
            progress?.Report(new ProgressReport(0, "Preparing 7z archive..."));

            var compressor = new SevenZipCompressor
            {
                ArchiveFormat = OutArchiveFormat.SevenZip,
                DirectoryStructure = true,
                IncludeEmptyDirectories = true,
                PreserveDirectoryRoot = false
            };

            // Map Compression Level
            compressor.CompressionLevel = level switch
            {
                ArchiveCompressionLevel.Store => SevenZip.CompressionLevel.None,
                ArchiveCompressionLevel.Fast => SevenZip.CompressionLevel.Fast,
                ArchiveCompressionLevel.Best => SevenZip.CompressionLevel.Ultra,
                _ => SevenZip.CompressionLevel.Normal
            };

            // Apply Encryption
            if (!string.IsNullOrEmpty(password))
            {
                compressor.ZipEncryptionMethod = ZipEncryptionMethod.Aes256;
            }

            // Progress Handler
            compressor.Compressing += (s, e) =>
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Operation cancelled by user.");
                }
                progress?.Report(new ProgressReport(e.PercentDone, "Compressing..."));
            };

            compressor.FileCompressionStarted += (s, e) =>
            {
                if (token.IsCancellationRequested) e.Cancel = true;
            };

            try
            {
                if (sourceDir != null)
                {
                    if (string.IsNullOrEmpty(password))
                        compressor.CompressDirectory(sourceDir, destPath);
                    else
                        compressor.CompressDirectory(sourceDir, destPath, password);
                }
                else
                {
                    // Create empty archive
                    // Create a temporary directory to serve as the "empty" source
                    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        // Create a dummy hidden file to ensure the compressor has 
                        // 'something' to process, ensuring a valid 7z header is written.
                        // Without this, 7z often throws "System error: The parameter is incorrect".
                        File.WriteAllBytes(Path.Combine(tempDir, ".empty"), Array.Empty<byte>());

                        if (string.IsNullOrEmpty(password))
                        {
                            compressor.CompressDirectory(tempDir, destPath);
                        }
                        else
                        {
                            compressor.CompressDirectory(tempDir, destPath, password);
                        }
                    }
                    finally
                    {
                        // Clean up the temp directory
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException 
                                    || ex.Message.Contains("cancelled") 
                                    || token.IsCancellationRequested)
            {
                // Cleanup partial file on cancel
                if (File.Exists(destPath))
                {
                    try { File.Delete(destPath); } catch { }
                }
                throw new OperationCanceledException(token);
            }

            progress?.Report(new ProgressReport(100, "7z created successfully."));
            IArchive archive = new SevenZArchive(destPath);
            await archive.DeleteEntriesAsync(
                new string[]
                {
                    ".empty",
                },
                password: password,
                progress: progress,
                token: default);
            return archive;

        }, token);
    }
}