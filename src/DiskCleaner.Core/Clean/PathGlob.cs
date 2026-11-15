using System.IO;

namespace DiskCleaner.Core.Clean;

/// <summary>
/// 路径模板展开：支持 %ENV% 环境变量和 * 通配。
/// 例：<c>%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2</c>
/// 返回所有实际存在的匹配项；目录或文件都可能是结果。
/// </summary>
public static class PathGlob
{
    /// <summary>展开模板，返回存在的匹配路径（目录或文件）。</summary>
    public static IEnumerable<string> Expand(string template)
    {
        if (string.IsNullOrWhiteSpace(template)) yield break;

        var expanded = Environment.ExpandEnvironmentVariables(template.Trim());
        if (string.IsNullOrWhiteSpace(expanded) || expanded.Contains('%')) yield break;

        // 拆成非空段，第一段可能是盘符 "C:" 或占位
        var parts = expanded.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0) yield break;

        var current = new List<string>();

        if (parts[0].EndsWith(':'))
        {
            // 盘符：C: -> C:\
            current.Add(parts[0] + Path.DirectorySeparatorChar);
            parts = parts.Skip(1).ToArray();
        }
        else
        {
            // 相对或 UNC：整体交给 Path.GetFullPath
            current.Add(string.Empty);
        }

        if (parts.Length == 0)
        {
            foreach (var c in current) yield return c;
            yield break;
        }

        foreach (var part in parts)
        {
            var next = new List<string>();
            foreach (var dir in current)
            {
                if (part == "**")
                {
                    // 简化的 **：展开当前目录下全部子目录（含自身）
                    if (Directory.Exists(dir))
                    {
                        next.Add(dir);
                        try
                        {
                            foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                                next.Add(sub);
                        }
                        catch (Exception) { /* 无权限则跳过 */ }
                    }
                }
                else if (part.Contains('*') || part.Contains('?'))
                {
                    if (!Directory.Exists(dir)) continue;
                    try
                    {
                        foreach (var hit in Directory.EnumerateFileSystemEntries(dir, part))
                            next.Add(hit);
                    }
                    catch (Exception) { /* 无权限则跳过 */ }
                }
                else
                {
                    var candidate = Path.Combine(dir, part);
                    if (Directory.Exists(candidate) || File.Exists(candidate))
                        next.Add(candidate);
                }
            }
            current = next;
            if (current.Count == 0) yield break;
        }

        foreach (var p in current.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(p);
            if (Directory.Exists(full) || File.Exists(full))
                yield return full;
        }
    }

    /// <summary>枚举一个展开后的目标下面全部文件（目录递归；文件直接返回）。</summary>
    public static IEnumerable<FileInfo> EnumerateFiles(string target)
    {
        if (File.Exists(target))
        {
            FileInfo? fi = null;
            try { fi = new FileInfo(target); } catch (Exception) { }
            if (fi is not null) yield return fi;
            yield break;
        }

        if (!Directory.Exists(target)) yield break;

        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
        };

        foreach (var fi in new DirectoryInfo(target).EnumerateFiles("*", opts))
            yield return fi;
    }
}
