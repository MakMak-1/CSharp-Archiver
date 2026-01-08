namespace Program;

using BLL;
using BLL.Archives;

class Program
{
    public static async Task Main()
    {
        await CreateZip();
    }

    private static async Task CreateZip()
    {
        IArchiveFactory factory = new ArchiveFactory();

        string sourceDirectory = @"C:\archive_test\TestData\pdf_files";
        string destinationArchivePath = @"C:\archive_test\MyArchive.zip";

        // Calculate original size (sum of all files)
        long originalSizeBytes = Directory
            .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);

        // Progress reporting
        var progress = new Progress<ProgressReport>(report =>
        {
        });

        Console.WriteLine("Starting ZIP compression...");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await factory.CreateFromDirectoryAsync(
                sourceDirectory: sourceDirectory,
                destinationArchivePath: destinationArchivePath,
                compressionLevel: ArchiveCompressionLevel.Best,
                progress: progress,
                token: default
            );

            sw.Stop(); // stop timing
            Console.WriteLine("\nDone!");

            // Get compressed file size
            long compressedSizeBytes = new FileInfo(destinationArchivePath).Length;

            // Convert to MB
            double originalMB = originalSizeBytes / (1024.0 * 1024.0);
            double compressedMB = compressedSizeBytes / (1024.0 * 1024.0);

            // Compression ratio (how many times smaller)
            double ratio = compressedMB / originalMB * 100;

            // Speed = MB / second
            double speed = originalMB / sw.Elapsed.TotalSeconds;

            // Output results
            Console.WriteLine("\n=== ZIP Compression Results ===");
            Console.WriteLine($"Original size:   {originalMB:F2} MB");
            Console.WriteLine($"Compressed size: {compressedMB:F2} MB");
            Console.WriteLine($"Compression ratio: {ratio:F2}% of the original size");
            Console.WriteLine($"Speed: {speed:F2} MB/s");
            Console.WriteLine($"Time: {sw.Elapsed.TotalSeconds:F3} s\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task Create7z()
    {
        IArchiveFactory factory = new ArchiveFactory();

        // This overwrites the current console line
        var progress = new Progress<ProgressReport>(report =>
        {
            // \r moves cursor to start of line
            // PadRight ensures we overwrite previous longer messages
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });

        try
        {
            Console.WriteLine("Starting operation...");

            await factory.CreateFromDirectoryAsync(
                sourceDirectory: @"C:\archive_test\TestData",
                destinationArchivePath: @"C:\archive_test\MyArchive.7z",
                compressionLevel: ArchiveCompressionLevel.Best,
                progress: progress,
                token: default);

            Console.WriteLine("\nDone!"); // Move to next line after finish
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task ExtractZip()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchive.zip");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting extraction...");
            await archive.ExtractAsync(
                destinationDirectory: @"C:\archive_test\Extracted\Zip",
                progress: progress,
                token: default);
            Console.WriteLine("\nExtraction complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task Extract7z()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchive.7z");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting extraction...");
            await archive.ExtractAsync(
                destinationDirectory: @"C:\archive_test\Extracted\7z",
                progress: progress,
                token: default);
            Console.WriteLine("\nExtraction complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task CreateZipPassword()
    {
        IArchiveFactory factory = new ArchiveFactory();

        // This overwrites the current console line
        var progress = new Progress<ProgressReport>(report =>
        {
            // \r moves cursor to start of line
            // PadRight ensures we overwrite previous longer messages
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });

        try
        {
            Console.WriteLine("Starting operation...");

            await factory.CreateFromDirectoryAsync(
                sourceDirectory: @"C:\archive_test\TestData",
                destinationArchivePath: @"C:\archive_test\MyArchiveProtected.zip",
                compressionLevel: ArchiveCompressionLevel.Best,
                password: "password123",
                progress: progress,
                token: default);

            Console.WriteLine("\nDone!"); // Move to next line after finish
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task Create7zPassword()
    {
        IArchiveFactory factory = new ArchiveFactory();

        // This overwrites the current console line
        var progress = new Progress<ProgressReport>(report =>
        {
            // \r moves cursor to start of line
            // PadRight ensures we overwrite previous longer messages
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });

        try
        {
            Console.WriteLine("Starting operation...");

            await factory.CreateFromDirectoryAsync(
                sourceDirectory: @"C:\archive_test\TestData",
                destinationArchivePath: @"C:\archive_test\MyArchiveProtected.7z",
                compressionLevel: ArchiveCompressionLevel.Best,
                password: "password123",
                progress: progress,
                token: default);

            Console.WriteLine("\nDone!"); // Move to next line after finish
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task ExtractZipPasswordCorrect()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchiveProtected.zip");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting extraction...");
            await archive.ExtractAsync(
                destinationDirectory: @"C:\archive_test\Extracted\ZipProtected",
                progress: progress,
                password: "password123",
                token: default);
            Console.WriteLine("\nExtraction complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task Extract7zPasswordCorrect()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchiveProtected.7z");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting extraction...");
            await archive.ExtractAsync(
                destinationDirectory: @"C:\archive_test\Extracted\7zProtected",
                password: "password123",
                progress: progress,
                token: default);
            Console.WriteLine("\nExtraction complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task ExtractZipPasswordIncorrect()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchiveProtected.zip");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting extraction...");
            await archive.ExtractAsync(
                destinationDirectory: @"C:\archive_test\Extracted\ZipProtected",
                progress: progress,
                password: "password1234",
                token: default);
            Console.WriteLine("\nExtraction complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task Extract7zPasswordIncorrect()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchiveProtected.7z");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting extraction...");
            await archive.ExtractAsync(
                destinationDirectory: @"C:\archive_test\Extracted\7zProtected",
                password: null,
                progress: progress,
                token: default);
            Console.WriteLine("\nExtraction complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task AddFilesZip()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchive.zip");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting to add files...");
            await archive.AddFilesAsync(
                new string[]
                {
                    @"C:\archive_test\Additional\book.pdf",
                    @"C:\archive_test\Additional\browser.txt"
                },
                pathInArchive: "additional_files/",
                level: ArchiveCompressionLevel.Best,
                progress: progress,
                token: default);
            Console.WriteLine("\nFiles added successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task AddFiles7z()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchive.7z");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting to add files...");
            await archive.AddFilesAsync(
                new string[]
                {
                    @"C:\archive_test\Additional\book.pdf",
                    @"C:\archive_test\Additional\browser.txt"
                },
                pathInArchive: "additional_files/",
                level: ArchiveCompressionLevel.Best,
                progress: progress,
                token: default);
            Console.WriteLine("\nFiles added successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task ListEntriesZip()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchive.zip");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Retrieving entries...");
            var entries = await archive.GetEntriesAsync(
                progress: progress,
                token: default);
            Console.WriteLine("\nEntries in archive:");
            foreach (var entry in entries)
            {
                Console.WriteLine($"- {entry.FullName} | Size: {entry.Size} bytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task ListEntries7z()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchive.7z");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Retrieving entries...");
            var entries = await archive.GetEntriesAsync(
                progress: progress,
                token: default);
            Console.WriteLine("\nEntries in archive:");
            foreach (var entry in entries)
            {
                Console.WriteLine($"- {entry.FullName} | Size: {entry.Size} bytes");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task DeleteEntriesZip()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchive.zip");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting to delete entries...");
            await archive.DeleteEntriesAsync(
                new string[]
                {
                    "additional_files/book.pdf",
                    "Events.csv"
                },
                progress: progress,
                token: default);
            Console.WriteLine("\nEntries deleted successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private static async Task DeleteEntries7z()
    {
        IArchiveFactory factory = new ArchiveFactory();
        IArchive archive = factory.GetArchive(@"C:\archive_test\MyArchive.7z");
        var progress = new Progress<ProgressReport>(report =>
        {
            Console.Write($"\r{report.Percentage:000}% | {report.Message}".PadRight(Console.WindowWidth - 1));
        });
        try
        {
            Console.WriteLine("Starting to delete entries...");
            await archive.DeleteEntriesAsync(
                new string[]
                {
                    "additional_files/book.pdf",
                    "Events.csv"
                },
                progress: progress,
                token: default);
            Console.WriteLine("\nEntries deleted successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }
}