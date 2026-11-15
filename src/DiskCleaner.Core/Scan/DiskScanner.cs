using System.Diagnostics;
using DiskCleaner.Core.Models;

namespace DiskCleaner.Core.Scan;

/// <summary>扫描进度快照。</summary>
public sealed class ScanProgress
{
    public long FilesSeen { get; init; }
    public long BytesSeen { get; init; }
    public string CurrentDir { get; init; } = "";
    public long ElapsedMs { get; init; }
}

/// <summary>一次扫描的完整结果。</summary>
public sealed class ScanResult
{
    public required DirNode Root { get; init; }
    public long ElapsedMs { get; init; }
    public long FilesSeen { get; init; }
    public long BytesSeen { get; init; }
    public int InaccessibleDirs { get; init; }
}

/// <summary>
/// 只读目录扫描器：遍历目录树，累计每个节点的大小与文件数。
/// 不写任何文件、不删除任何东西。并联扫描顶层子目录提速。
/// </summary>
public static class DiskScanner
{
    private sealed class Counters
    {
        public long Files;
        public long Bytes;
        public int Inaccessible;
        public long SinceReport;
        public string CurrentDir = "";
    }

    public static ScanResult Scan(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var full = Path.GetFullPath(rootPath);
        var root = new DirNode
        {
            Name = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            FullPath = full,
        };

        var counters = new Counters();
        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };

        // 顶层：先分拣直属文件与子目录，子目录再并行展开。
        var subDirs = new List<DirectoryInfo>();
        var top = new DirectoryInfo(full);
        try
        {
            foreach (var info in top.EnumerateFileSystemInfos("*", opts))
            {
                ct.ThrowIfCancellationRequested();
                if (info is DirectoryInfo d)
                {
                    subDirs.Add(d);
                }
                else if (info is FileInfo f)
                {
                    long len;
                    try { len = f.Length; } catch { continue; }
                    root.Size += len;
                    root.FileCount++;
                    root.DirectSize += len;
                    root.DirectFileCount++;
                    Interlocked.Increment(ref counters.Files);
                    Interlocked.Add(ref counters.Bytes, len);
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            root.InaccessibleDirs++;
            counters.Inaccessible++;
        }

        void Report(bool force)
        {
            if (progress is null) return;
            if (!force && Interlocked.Read(ref counters.SinceReport) < 20_000) return;
            Interlocked.Exchange(ref counters.SinceReport, 0);
            progress.Report(new ScanProgress
            {
                FilesSeen = Interlocked.Read(ref counters.Files),
                BytesSeen = Interlocked.Read(ref counters.Bytes),
                CurrentDir = counters.CurrentDir,
                ElapsedMs = sw.ElapsedMilliseconds,
            });
        }

        var children = new List<DirNode>();
        var parallelOpts = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount),
            CancellationToken = ct,
        };

        Parallel.ForEach(subDirs, parallelOpts, dir =>
        {
            var child = new DirNode { Name = dir.Name, FullPath = dir.FullName };
            ScanDir(child, counters, progress, sw, ct, opts);
            lock (children) { children.Add(child); }
            Report(force: false);
        });

        foreach (var child in children)
        {
            root.Children.Add(child);
            root.Size += child.Size;
            root.FileCount += child.FileCount;
            root.InaccessibleDirs += child.InaccessibleDirs;
        }
        root.Children.Sort((a, b) => b.Size.CompareTo(a.Size));

        sw.Stop();
        progress?.Report(new ScanProgress
        {
            FilesSeen = counters.Files,
            BytesSeen = counters.Bytes,
            CurrentDir = counters.CurrentDir,
            ElapsedMs = sw.ElapsedMilliseconds,
        });

        return new ScanResult
        {
            Root = root,
            ElapsedMs = sw.ElapsedMilliseconds,
            FilesSeen = counters.Files,
            BytesSeen = counters.Bytes,
            InaccessibleDirs = counters.Inaccessible,
        };
    }

    private static void ScanDir(
        DirNode node,
        Counters counters,
        IProgress<ScanProgress>? progress,
        Stopwatch sw,
        CancellationToken ct,
        EnumerationOptions opts)
    {
        DirectoryInfo di;
        try { di = new DirectoryInfo(node.FullPath); }
        catch { return; }

        try
        {
            foreach (var info in di.EnumerateFileSystemInfos("*", opts))
            {
                ct.ThrowIfCancellationRequested();

                if (info is DirectoryInfo d)
                {
                    var child = new DirNode { Name = d.Name, FullPath = d.FullName };
                    ScanDir(child, counters, progress, sw, ct, opts);
                    node.Children.Add(child);
                    node.Size += child.Size;
                    node.FileCount += child.FileCount;
                    node.InaccessibleDirs += child.InaccessibleDirs;
                }
                else if (info is FileInfo f)
                {
                    long len;
                    try { len = f.Length; } catch { continue; }

                    node.Size += len;
                    node.FileCount++;
                    node.DirectSize += len;
                    node.DirectFileCount++;

                    Interlocked.Increment(ref counters.Files);
                    Interlocked.Add(ref counters.Bytes, len);
                    Interlocked.Increment(ref counters.SinceReport);
                    counters.CurrentDir = node.FullPath;

                    if (progress is not null && Interlocked.Read(ref counters.SinceReport) >= 20_000)
                    {
                        Interlocked.Exchange(ref counters.SinceReport, 0);
                        progress.Report(new ScanProgress
                        {
                            FilesSeen = Interlocked.Read(ref counters.Files),
                            BytesSeen = Interlocked.Read(ref counters.Bytes),
                            CurrentDir = counters.CurrentDir,
                            ElapsedMs = sw.ElapsedMilliseconds,
                        });
                    }
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            node.InaccessibleDirs++;
            Interlocked.Increment(ref counters.Inaccessible);
        }

        node.Children.Sort((a, b) => b.Size.CompareTo(a.Size));
    }

    /// <summary>把结果树压平成「路径 → 大小」列表，供报表使用。</summary>
    public static IEnumerable<DirNode> Flatten(DirNode node)
    {
        yield return node;
        foreach (var c in node.Children)
            foreach (var n in Flatten(c))
                yield return n;
    }
}
