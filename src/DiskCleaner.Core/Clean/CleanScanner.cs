using DiskCleaner.Core.Interop;

namespace DiskCleaner.Core.Clean;

/// <summary>扫描「本机能清多少」，只读，不删除任何东西。</summary>
public static class CleanScanner
{
    /// <summary>按规则列表扫描，返回每一条的可清理量。</summary>
    public static List<CleanItem> Scan(
        IReadOnlyList<CleanRule> rules,
        IProgress<CleanScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<CleanItem>(rules.Count);

        for (var i = 0; i < rules.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var rule = rules[i];

            progress?.Report(new CleanScanProgress
            {
                CurrentRule = rule.Name,
                Done = i,
                Total = rules.Count,
            });

            var item = new CleanItem
            {
                RuleId = rule.Id,
                Name = rule.Name,
                Description = rule.Description,
                Category = rule.Category,
                Kind = rule.Kind,
                UseRecycleBin = rule.UseRecycleBin,
                RequiresAdmin = rule.RequiresAdmin,
            };

            try
            {
                if (rule.Kind == CleanRuleKind.RecycleBin)
                {
                    var (size, items) = ShellInterop.QueryRecycleBin(null);
                    item.Size = size;
                    item.FileCount = (int)Math.Min(items, int.MaxValue);
                    item.State = CleanItemState.Ready;
                }
                else
                {
                    var (size, count, anyPath) = Measure(rule, ct);
                    item.Size = size;
                    item.FileCount = count;
                    item.State = anyPath ? CleanItemState.Ready : CleanItemState.NotApplicable;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                item.State = CleanItemState.Failed;
                item.Problem = ex.Message;
            }

            results.Add(item);
        }

        progress?.Report(new CleanScanProgress
        {
            CurrentRule = "",
            Done = rules.Count,
            Total = rules.Count,
        });

        return results;
    }

    private static (long Size, int Count, bool AnyPath) Measure(CleanRule rule, CancellationToken ct)
    {
        long size = 0;
        var count = 0;
        var anyPath = false;

        foreach (var template in rule.Paths)
        {
            foreach (var target in PathGlob.Expand(template))
            {
                anyPath = true;
                foreach (var fi in PathGlob.EnumerateFiles(target))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        size += fi.Length;
                        count++;
                    }
                    catch (Exception)
                    {
                        // 文件此刻被删/没法读，忽略
                    }
                }
            }
        }

        return (size, count, anyPath);
    }
}
