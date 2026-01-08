namespace BLL.Archives;

public class ArchiveEntry : IArchiveEntry
{
    public required string Name { get; set; }
    public required string FullName { get; set; }
    public long Size { get; set; }
    public DateTimeOffset LastWriteTime { get; set; }
    public bool IsDirectory { get; set; }
}
