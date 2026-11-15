using System.IO;
using DiskCleaner.Core.Interop;

namespace DiskCleaner.Core.Clean;

/// <summary>执行清理。只删除规则命中路径下的文件，目录结构保留。</summary>
public static class CleanExecutor
{
    /// <summary>
    /// 清理选中的项。<paramref name="dryRun"/> 为 true 时只统计不删除（给预览用）。
    /// </summary>
    public static CleanOutcome Execute(
        IReadOnlyList<CleanRule> rules,
        IReadOnlyCollection<string> selectedRuleIds,
        IProgress<CleanProgress>? progress = null,
        CancellationToken ct = default,
        bool dryRun = false,
        bool forceRecycleBin = false)
    {
        var outcome = new CleanOutcome();
        var selected = rules.Where(r => selectedRuleIds.Contains(r.Id, StringComparer.OrdinalIgnoreCase)).ToList();

        for (var i = 0; i < selected.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var rule = selected[i];

            try
            {
                if (rule.Kind == CleanRuleKind.RecycleBin)
                {
                    if (dryRun)
                    {
                        var (size, items) = ShellInterop.QueryRecycleBin(null);
                        outcome.BytesFreed += size;
                        outcome.FilesDeleted += (int)Math.Min(items, int.MaxValue);
                    }
                    else if (ShellInterop.EmptyRecycleBin(null))
                    {
                        outcome.RecycleBinEmptied = true;
                    }
                    else
                    {
                        outcome.Problem = "清空回收站失败";
                    }

                    progress?.Report(new CleanProgress
                    {
                        CurrentRule = rule.Name,
                        FilesDone = outcome.FilesDeleted,
                        BytesDone = outcome.BytesFreed,
                        RulesDone = i + 1,
                        RulesTotal = selected.Count,
                    });
                    continue;
                }

                // 先收集全部待删文件（路径 + 大小）
                var files = new List<string>();
                long bytes = 0;
                foreach (var template in rule.Paths)
                {
                    foreach (var target in PathGlob.Expand(template))
                    {
                        foreach (var fi in PathGlob.EnumerateFiles(target))
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                files.Add(fi.FullName);
                                bytes += fi.Length;
                            }
                            catch (Exception)
                            {
                                // 枚举到一半文件没了，忽略
                            }
                        }
                    }
                }

                if (dryRun)
                {
                    outcome.FilesDeleted += files.Count;
                    outcome.BytesFreed += bytes;
                }
                else if (rule.UseRecycleBin || forceRecycleBin)
                {
                    var failed = ShellInterop.DeleteToRecycleBin(files);
                    outcome.FilesFailed += failed;
                    outcome.FilesDeleted += files.Count - failed;
                    outcome.BytesFreed += bytes;
                }
                else
                {
                    var ok = 0;
                    var failed = 0;
                    foreach (var f in files)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            File.Delete(f);
                            ok++;
                        }
                        catch (Exception)
                        {
                            failed++;
                        }
                    }
                    outcome.FilesDeleted += ok;
                    outcome.FilesFailed += failed;
                    outcome.BytesFreed += bytes;
                }

                progress?.Report(new CleanProgress
                {
                    CurrentRule = rule.Name,
                    FilesDone = outcome.FilesDeleted,
                    BytesDone = outcome.BytesFreed,
                    RulesDone = i + 1,
                    RulesTotal = selected.Count,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                outcome.Problem = $"{rule.Name}: {ex.Message}";
            }
        }

        return outcome;
    }
}
