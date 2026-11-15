namespace DiskCleaner.Core.Models;

/// <summary>单个文件的大小信息，用于大文件排行。</summary>
public sealed class FileEntry
{
    public required string Path { get; init; }
    public long Size { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string? Directory => System.IO.Path.GetDirectoryName(Path);
}
