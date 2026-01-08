using BLL.Archives;

namespace GUI.Models;

public class VirtualArchiveEntry : IArchiveEntry
{
    public string Name { get; }
    public string FullName { get; }
    public long Size => 0; // Folders have no size
    public DateTimeOffset LastWriteTime { get; } = DateTimeOffset.Now;
    public bool IsDirectory => true;

    public VirtualArchiveEntry(string fullPath)
    {
        // Normalize path to use forward slashes for internal consistency
        FullName = fullPath.Replace('\\', '/').TrimEnd('/') + "/";

        // The name is just the last segment (e.g. "myfolder" from "data/myfolder/")
        var segments = FullName.TrimEnd('/').Split('/');
        Name = segments.Last();
    }
}