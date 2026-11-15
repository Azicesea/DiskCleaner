namespace DiskCleaner.Core.Find;

/// <summary>一条命中结果。</summary>
public sealed class KeywordHit
{
    public required string Path { get; init; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string? Directory => System.IO.Path.GetDirectoryName(Path);
    public long Size { get; init; }
    public DateTime Modified { get; init; }
}

/// <summary>搜索进度。</summary>
public sealed class KeywordProgress
{
    public int FilesSeen { get; init; }
    public long BytesSeen { get; init; }
    public int Hits { get; init; }
    public string? CurrentDir { get; init; }
}

/// <summary>
/// 按关键词在文件名和完整路径里找文件。
/// 除了字面匹配，还会把常见中文词扩展成对应的英文/后缀，
/// 这样输入「临时文件」也能找到 Temp、tmp 下面的东西。
/// </summary>
public static class KeywordFinder
{
    /// <summary>结果上限，防止扫 C 盘时把内存撑爆。</summary>
    public const int DefaultMaxResults = 5000;

    private const int ProgressInterval = 20000;

    /// <summary>中文关键词 → 额外要匹配的词。key 命中子串就生效。</summary>
    private static readonly (string Key, string[] Extras)[] Synonyms =
    {
        ("临时", new[] { "temp", "tmp" }),
        ("缓存", new[] { "cache" }),
        ("日志", new[] { "log", ".log" }),
        ("备份", new[] { "backup", ".bak" }),
        ("安装", new[] { "setup", "install" }),
        ("下载", new[] { "download" }),
        ("垃圾", new[] { "temp", "tmp", "cache", ".log", ".bak", "~$" }),
        ("缩略图", new[] { "thumb" }),
        ("转储", new[] { "dump", ".dmp" }),
        ("视频", new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" }),
        ("图片", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" }),
        ("音乐", new[] { ".mp3", ".flac", ".wav", ".m4a", ".aac" }),
        ("压缩", new[] { ".zip", ".rar", ".7z", ".tar", ".gz" }),
        ("文档", new[] { ".doc", ".docx", ".pdf", ".txt", ".xls", ".xlsx", ".ppt", ".pptx" }),
    };

    /// <summary>
    /// 在 <paramref name="rootPath"/> 下递归找匹配的文件，按大小降序返回。
    /// </summary>
    public static IReadOnlyList<KeywordHit> Find(
        string rootPath,
        string query,
        int maxResults = DefaultMaxResults,
        IProgress<KeywordProgress>? progress = null,
        CancellationToken ct = default)
    {
        var groups = BuildGroups(query);
        if (groups.Count == 0) return Array.Empty<KeywordHit>();
        if (!System.IO.Directory.Exists(rootPath)) return Array.Empty<KeywordHit>();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };

        var hits = new List<KeywordHit>();
        var files = 0;
        var bytes = 0L;
        var lastReport = 0;

        foreach (var path in System.IO.Directory.EnumerateFiles(rootPath, "*", options))
        {
            ct.ThrowIfCancellationRequested();
            files++;

            if (Matches(path, groups))
            {
                try
                {
                    var info = new FileInfo(path);
                    hits.Add(new KeywordHit
                    {
                        Path = path,
                        Size = info.Length,
                        Modified = info.LastWriteTime,
                    });
                    bytes += info.Length;
                }
                catch
                {
                    // 文件刚好被删掉/无权访问，跳过
                }

                if (hits.Count >= maxResults) break;
            }

            if (progress != null && files - lastReport >= ProgressInterval)
            {
                lastReport = files;
                progress.Report(new KeywordProgress
                {
                    FilesSeen = files,
                    BytesSeen = bytes,
                    Hits = hits.Count,
                });
            }
        }

        progress?.Report(new KeywordProgress
        {
            FilesSeen = files,
            BytesSeen = bytes,
            Hits = hits.Count,
        });

        hits.Sort((a, b) => b.Size.CompareTo(a.Size));
        return hits;
    }

    /// <summary>
    /// 把输入拆成若干词组，每组是若干个可接受的候选词。
    /// 一个文件要同时命中每一组才算匹配。
    /// </summary>
    internal static List<string[]> BuildGroups(string query)
    {
        var groups = new List<string[]>();
        if (string.IsNullOrWhiteSpace(query)) return groups;

        var words = query.Split(new[] { ' ', '\t', ',', '，' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var word in words)
        {
            var set = new List<string> { word };
            foreach (var (key, extras) in Synonyms)
            {
                if (!word.Contains(key, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var extra in extras)
                {
                    if (!set.Contains(extra, StringComparer.OrdinalIgnoreCase)) set.Add(extra);
                }
            }
            groups.Add(set.ToArray());
        }

        return groups;
    }

    private static bool Matches(string path, List<string[]> groups)
    {
        foreach (var group in groups)
        {
            var hit = false;
            foreach (var candidate in group)
            {
                if (candidate.Length == 0) continue;
                if (MatchesCandidate(path, candidate))
                {
                    hit = true;
                    break;
                }
            }
            if (!hit) return false;
        }
        return true;
    }

    private static bool MatchesCandidate(string path, string candidate)
    {
        // 扩展名（.log / .tmp）只比对结尾
        if (candidate[0] == '.')
            return path.EndsWith(candidate, StringComparison.OrdinalIgnoreCase);

        // 纯英文词按整词匹配，否则 "temp" 会命中 "Templates" 这种不相干的东西
        if (IsAsciiWord(candidate))
            return ContainsWord(path, candidate);

        return path.Contains(candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAsciiWord(string s)
    {
        foreach (var c in s)
        {
            if (c > 127 || !char.IsLetterOrDigit(c)) return false;
        }
        return s.Length > 0;
    }

    /// <summary>找 candidate 出现在路径里、且前后都不是字母数字的位置。</summary>
    private static bool ContainsWord(string path, string candidate)
    {
        var from = 0;
        while (from <= path.Length - candidate.Length)
        {
            var idx = path.IndexOf(candidate, from, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            var beforeOk = idx == 0 || !char.IsLetterOrDigit(path[idx - 1]);
            var afterIdx = idx + candidate.Length;
            var afterOk = afterIdx >= path.Length || !char.IsLetterOrDigit(path[afterIdx]);
            if (beforeOk && afterOk) return true;

            from = idx + 1;
        }
        return false;
    }
}
