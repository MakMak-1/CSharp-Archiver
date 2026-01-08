namespace BLL.Archives;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides a unified, asynchronous interface for interacting with
/// an archive file.
/// </summary>
public interface IArchive
{
    #region Properties
    /// <summary>
    /// Gets the full path to the archive file on disk.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets a value indicating whether the archive is
    /// encrypted and requires a password for reading or modification.
    /// </summary>
    bool IsPasswordProtected { get; }
    #endregion

    #region Methods
    /// <summary>
    /// Extracts the entire contents of the archive using a password.
    /// </summary>
    /// <param name="destinationDirectory">The folder to extract files to.</param>
    /// <param name="password">The password for decryption.</param>
    /// <param name="progress">A provider to report progress to the UI.</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="DirectoryNotFoundException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    Task ExtractAsync(
        string destinationDirectory,
        string password,
        IProgress<ProgressReport> progress,
        CancellationToken token);

    /// <summary>
    /// Extracts the entire contents of a non-encrypted archive.
    /// </summary>
    /// <param name="destinationDirectory">The folder to extract files to.</param>
    /// <param name="progress">A provider to report progress to the UI.</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="DirectoryNotFoundException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    Task ExtractAsync(
        string destinationDirectory,
        IProgress<ProgressReport> progress,
        CancellationToken token);

    /// <summary>
    /// Retrieves a list of all entries (files and directories) from encrypted archive.
    /// </summary>
    /// <param name="password">The password for decryption.</param>
    /// <param name="progress">A provider to report progress (needed for.tar.gz.gpg).</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <returns>A collection of archive entry DTOs.</returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    Task<IEnumerable<IArchiveEntry>> GetEntriesAsync(
        string password,
        IProgress<ProgressReport> progress,
        CancellationToken token);

    /// <summary>
    /// Retrieves a list of all entries from a non-encrypted archive.
    /// </summary>
    /// <param name="progress">A provider to report progress (needed for.tar.gz.gpg).</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    Task<IEnumerable<IArchiveEntry>> GetEntriesAsync(
        IProgress<ProgressReport> progress,
        CancellationToken token);

    /// <summary>
    /// Adds one or more files to the archive.
    /// </summary>
    /// <param name="sourceFilePaths">A collection of file paths on disk to add.</param>
    /// <param name="pathInArchive">The relative path inside the archive to add the files to (e.g., "data/logs/").</param>
    /// <param name="password">The password for encryption (if any).</param>
    /// <param name="level">Compression level of files in the archive.</param>
    /// <param name="progress">A provider to report progress to the UI.</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    Task AddFilesAsync(
        IEnumerable<string> sourceFilePaths,
        string pathInArchive,
        string password,
        ArchiveCompressionLevel level,
        IProgress<ProgressReport> progress,
        CancellationToken token);

    /// <summary>
    /// Adds one or more files to a non-encrypted archive.
    /// </summary>
    /// <param name="sourceFilePaths">A collection of file paths on disk to add.</param>
    /// <param name="pathInArchive">The relative path inside the archive to add the files to (e.g., "data/logs/").</param>
    /// <param name="level">Compression level of files in the archive.</param>
    /// <param name="progress">A provider to report progress to the UI.</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    Task AddFilesAsync(
        IEnumerable<string> sourceFilePaths,
        string pathInArchive,
        ArchiveCompressionLevel level,
        IProgress<ProgressReport> progress,
        CancellationToken token);

    /// <summary>
    /// Deletes one or more entries from the archive.
    /// </summary>
    /// <param name="entryPaths">A collection of full entry paths to delete.</param>
    /// <param name="password">The password for encryption (if any).</param>
    /// <param name="progress">A provider to report progress to the UI.</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    Task DeleteEntriesAsync(
        IEnumerable<string> entryPaths,
        string password,
        IProgress<ProgressReport> progress,
        CancellationToken token);

    /// <summary>
    /// Deletes one or more entries from a non-encrypted archive.
    /// </summary>
    /// <param name="entryPaths">A collection of full entry paths to delete.</param>
    /// <param name="progress">A provider to report progress to the UI.</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    Task DeleteEntriesAsync(
        IEnumerable<string> entryPaths,
        IProgress<ProgressReport> progress,
        CancellationToken token);
    #endregion
}