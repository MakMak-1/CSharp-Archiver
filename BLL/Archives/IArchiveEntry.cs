namespace BLL.Archives;

/// <summary>
/// Represents a single entry (file or directory) within an archive.
/// This is a data-transfer object (DTO) safe for UI binding.
/// </summary>
public interface IArchiveEntry
{
    /// <summary>
    /// Gets the file name of the entry (e.g., "file.txt").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the full relative path of the entry (e.g., "folder/file.txt").
    /// </summary>
    string FullName { get; }

    /// <summary>
    /// Gets the uncompressed size of the entry in bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Gets the last modified timestamp of the entry.
    /// </summary>
    DateTimeOffset LastWriteTime { get; }

    /// <summary>
    /// Gets a value indicating whether this entry is a directory.
    /// </summary>
    bool IsDirectory { get; }
}
