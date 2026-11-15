using System.Security.Cryptography;
using DiskCleaner.Core.Models;

namespace DiskCleaner.Core.Dup;

/// <summary>一组内容相同的文件。</summary>
public sealed class DuplicateGroup
{
    public long Size { get; init; }
    public List<FileEntry> Files { get; init; } = new();

    /// <summary>删掉多余副本能省下的空间。</summary>
    public long WastedBytes => Size * Math.Max(0, Files.Count - 1);
}

public sealed class DupProgress
{
    public string Stage { get; init; } = "";
    public int FilesDone { get; init; }
    public int FilesTotal { get; init; }
    public long BytesDone { get; init; }
}

/// <summary>
/// 重复文件查找：先按大小分组（便宜），再对候选算哈希（贵）。
/// 全程只读，不删不改任何文件。
/// </summary>
public static class DuplicateFinder
{
    /// <summary>只比较前 8KB 的预筛哈希，用来快速排除伪重复。</summary>
    private const int HeadBytes = 8192;

    public static List<DuplicateGroup> Find(
        string rootPath,
        IProgress<DupProgress>? progress = null,
        CancellationToken ct = default,
        long minFileSize = 1024 * 1024)
    {
        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };

        progress?.Report(new DupProgress { Stage = "正在列出文件…" });

        var bySize = new Dictionary<long, List<string>>();
        long scanned = 0;
        foreach (var file in new DirectoryInfo(rootPath).EnumerateFiles("*", opts))
        {
            ct.ThrowIfCancellationRequested();
            scanned++;
            if (scanned % 20_000 == 0)
                progress?.Report(new DupProgress { Stage = $"正在列出文件…（{scanned:N0} 个）" });

            long len;
            try
            {
                if (file.Length < minFileSize) continue;
                len = file.Length;
            }
            catch { continue; }

            if (!bySize.TryGetValue(len, out var list))
            {
                list = new List<string>();
                bySize[len] = list;
            }
            list.Add(file.FullName);
        }

        var candidates = bySize.Where(kv => kv.Value.Count > 1).ToList();
        var totalFiles = candidates.Sum(c => c.Value.Count);

        progress?.Report(new DupProgress
        {
            Stage = $"正在比对 {candidates.Count:N0} 组候选（{totalFiles:N0} 个文件）…",
            FilesTotal = totalFiles,
        });

        var result = new List<DuplicateGroup>();
        var done = 0;
        long bytesDone = 0;

        foreach (var (size, paths) in candidates.OrderByDescending(c => c.Key * (c.Value.Count - 1)))
        {
            ct.ThrowIfCancellationRequested();

            // 第一轮：只比前 8KB，能甩掉绝大多数「大小相同但内容不同」的文件
            var headGroups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                ct.ThrowIfCancellationRequested();
                var h = TryHash(path, HeadBytes, ct);
                if (h is null) { done++; continue; }
                if (!headGroups.TryGetValue(h, out var l))
                {
                    l = new List<string>();
                    headGroups[h] = l;
                }
                l.Add(path);
                done++;
                bytesDone += size;
                if (done % 200 == 0)
                    progress?.Report(new DupProgress
                    {
                        Stage = "正在比对内容…",
                        FilesDone = done,
                        FilesTotal = totalFiles,
                        BytesDone = bytesDone,
                    });
            }

            foreach (var headGroup in headGroups.Values)
            {
                if (headGroup.Count < 2) continue;

                // 第二轮：整文件 SHA-256，确认内容真的一致
                var fullGroups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var path in headGroup)
                {
                    ct.ThrowIfCancellationRequested();
                    var h = TryHash(path, 0, ct);
                    if (h is null) continue;
                    if (!fullGroups.TryGetValue(h, out var l))
                    {
                        l = new List<string>();
                        fullGroups[h] = l;
                    }
                    l.Add(path);
                }

                foreach (var g in fullGroups.Values)
                {
                    if (g.Count < 2) continue;
                    result.Add(new DuplicateGroup
                    {
                        Size = size,
                        Files = g.Select(p => new FileEntry { Path = p, Size = size }).ToList(),
                    });
                }
            }
        }

        result.Sort((a, b) => b.WastedBytes.CompareTo(a.WastedBytes));
        progress?.Report(new DupProgress { Stage = "完成", FilesDone = done, FilesTotal = totalFiles });
        return result;
    }

    private static string? TryHash(string path, int headBytes, CancellationToken ct)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            using var sha = SHA256.Create();
            var buffer = new byte[64 * 1024];
            long remaining = headBytes > 0 ? headBytes : long.MaxValue;
            int read;
            while (remaining > 0 && (read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining))) > 0)
            {
                ct.ThrowIfCancellationRequested();
                sha.TransformBlock(buffer, 0, read, null, 0);
                remaining -= read;
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // 打不开/读不了的直接跳过，不影响其他文件
            return null;
        }
    }
}
