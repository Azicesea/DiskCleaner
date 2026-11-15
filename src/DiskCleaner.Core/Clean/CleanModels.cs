namespace DiskCleaner.Core.Clean;

/// <summary>一条清理规则扫描出来的结果状态。</summary>
public enum CleanItemState
{
    /// <summary>还没扫描。</summary>
    Pending,
    /// <summary>扫描完成，有东西可清。</summary>
    Ready,
    /// <summary>扫描完成，但路径都不存在（这台机器没装这个软件）。</summary>
    NotApplicable,
    /// <summary>扫描出错（无权限等）。</summary>
    Failed,
}

/// <summary>一条清理规则的可清理量。</summary>
public sealed class CleanItem
{
    public required string RuleId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public CleanCategory Category { get; init; }
    public CleanRuleKind Kind { get; init; } = CleanRuleKind.Files;
    public bool UseRecycleBin { get; init; }
    public bool RequiresAdmin { get; init; }

    public CleanItemState State { get; set; } = CleanItemState.Pending;
    public long Size { get; set; }
    public int FileCount { get; set; }
    public string? Problem { get; set; }

    public bool CanClean => State == CleanItemState.Ready && (Kind == CleanRuleKind.RecycleBin || FileCount > 0);
}

/// <summary>扫描进度。</summary>
public sealed class CleanScanProgress
{
    public required string CurrentRule { get; init; }
    public int Done { get; init; }
    public int Total { get; init; }
}

/// <summary>清理执行结果。</summary>
public sealed class CleanOutcome
{
    public int FilesDeleted { get; set; }
    public long BytesFreed { get; set; }
    public int FilesFailed { get; set; }
    public bool RecycleBinEmptied { get; set; }
    public string? Problem { get; set; }
}

/// <summary>清理过程中的进度。</summary>
public sealed class CleanProgress
{
    public required string CurrentRule { get; init; }
    public int FilesDone { get; init; }
    public long BytesDone { get; init; }
    public int RulesDone { get; init; }
    public int RulesTotal { get; init; }
}
