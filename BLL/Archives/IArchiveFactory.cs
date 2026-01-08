namespace BLL.Archives;

/// <summary>
/// An abstract factory for creating and retrieving IArchive instances.
/// </summary>
public interface IArchiveFactory
{
    /// <summary>
    /// Gets an IArchive implementaion for an existing file.
    /// </summary>
    /// <param name="archivePath">The path to the existing archive file.</param>
    /// <returns>An IArchive instance.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown if the file extension is not supported.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the file at archivePath does not exist.
    /// </exception>
    IArchive GetArchive(string archivePath);

    /// <summary>
    /// Creates a new archive file from a source directory.
    /// </summary>
    /// <param name="sourceDirectory">The directory to compress.</param>
    /// <param name="destinationArchivePath">The full path of the new 
    /// archive file to be created (e.g., "C:\MyDocs\new.zip").</param>
    /// <param name="password">The password for encryption. 
    /// Pass null or string.Empty for no encryption.</param>
    /// <param name="progress">A provider to report progress to the UI.</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <returns>An IArchive instance representing the newly created file.</returns>
    Task<IArchive> CreateFromDirectoryAsync(
        string sourceDirectory,
        string destinationArchivePath,
        string? password,
        ArchiveCompressionLevel compressionLevel,
        IProgress<ProgressReport> progress,
        CancellationToken token);

    /// <summary>
    /// Creates a new archive file from a source directory without encryption.
    /// </summary>
    /// <param name="sourceDirectory">The directory to compress.</param>
    /// <param name="destinationArchivePath">The full path of the new 
    /// archive file to be created (e.g., "C:\MyDocs\new.zip").</param>
    /// <param name="progress">A provider to report progress to the UI.</param>
    /// <param name="token">A token to cancel the operation.</param>
    /// <returns>An IArchive instance representing the newly created file.</returns>
    Task<IArchive> CreateFromDirectoryAsync(
        string sourceDirectory,
        string destinationArchivePath,
        ArchiveCompressionLevel compressionLevel,
        IProgress<ProgressReport> progress,
        CancellationToken token);
}
