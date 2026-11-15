namespace DiskCleaner.Core.Models;

/// <summary>
/// 一个目录的扫描结果节点。Size / FileCount 是包含所有子目录的累计值。
/// </summary>
public sealed class DirNode
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }

    /// <summary>含子目录的累计大小（字节）。</summary>
    public long Size { get; set; }

    /// <summary>含子目录的累计文件数。</summary>
    public long FileCount { get; set; }

    /// <summary>该目录直属文件的大小（不含子目录）。</summary>
    public long DirectSize { get; set; }

    /// <summary>该目录直属文件的数量（不含子目录）。</summary>
    public long DirectFileCount { get; set; }

    /// <summary>子目录节点。</summary>
    public List<DirNode> Children { get; } = new();

    /// <summary>扫描该目录时无权限访问、被跳过的子目录数量。</summary>
    public int InaccessibleDirs { get; set; }
}
