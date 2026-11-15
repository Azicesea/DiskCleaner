namespace DiskCleaner.Core.Clean;

/// <summary>清理项分类，用于界面分组。</summary>
public enum CleanCategory
{
    Temp,
    Browser,
    System,
    RecycleBin,
}

/// <summary>规则类型：普通文件集合，还是回收站整体清空。</summary>
public enum CleanRuleKind
{
    Files,
    RecycleBin,
}

/// <summary>
/// 一条清理规则：描述「什么样的东西可以清理」，本身不执行任何删除。
/// 所有路径模板支持 %ENV% 环境变量和 * 通配，展开后才去统计。
/// 安全约定：只会枚举并删除 <see cref="Paths"/> 展开后落在其中的文件，目录结构本身保留。
/// </summary>
public sealed class CleanRule
{
    /// <summary>稳定的内部标识，用于配置/日志，不用于显示。</summary>
    public required string Id { get; init; }

    /// <summary>界面显示名。</summary>
    public required string Name { get; init; }

    /// <summary>界面说明：这东西是什么、删了有什么影响。</summary>
    public required string Description { get; init; }

    public CleanCategory Category { get; init; }
    public CleanRuleKind Kind { get; init; } = CleanRuleKind.Files;

    /// <summary>路径模板（文件或目录）。目录会递归删除其中所有文件，但目录本身保留。</summary>
    public string[] Paths { get; init; } = [];

    /// <summary>是否需要管理员权限才能读取。</summary>
    public bool RequiresAdmin { get; init; }

    /// <summary>删除时是否走回收站（可还原）。</summary>
    public bool UseRecycleBin { get; init; } = true;
}
