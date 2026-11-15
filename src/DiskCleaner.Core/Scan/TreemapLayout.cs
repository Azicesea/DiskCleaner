using DiskCleaner.Core.Models;

namespace DiskCleaner.Core.Scan;

/// <summary>树图里的一块矩形。</summary>
public sealed record TreemapBlock(DirNode Node, double X, double Y, double Width, double Height);

/// <summary>
/// 矩形树图（squarified treemap）布局：把一组子目录按大小铺满给定矩形。
/// 纯计算，不涉及任何界面或文件操作。
/// </summary>
public static class TreemapLayout
{
    /// <summary>
    /// 计算 <paramref name="parent"/> 的子项在 <paramref name="width"/>×<paramref name="height"/> 里的矩形。
    /// 直属文件会作为一个名为「（本级文件）」的虚拟块参与布局。
    /// </summary>
    public static List<TreemapBlock> Layout(DirNode parent, double width, double height, double minArea = 9)
    {
        var result = new List<TreemapBlock>();
        if (width <= 1 || height <= 1) return result;

        var items = new List<(DirNode Node, double Size)>();
        foreach (var child in parent.Children)
            if (child.Size > 0) items.Add((child, child.Size));

        if (parent.DirectSize > 0)
        {
            var direct = new DirNode
            {
                Name = "（本级文件）",
                FullPath = parent.FullPath,
                Size = parent.DirectSize,
                FileCount = parent.DirectFileCount,
            };
            items.Add((direct, parent.DirectSize));
        }

        if (items.Count == 0) return result;

        items.Sort((a, b) => b.Size.CompareTo(a.Size));
        var total = items.Sum(i => i.Size);
        if (total <= 0) return result;

        var scale = width * height / total;
        var areas = items.Select(i => (i.Node, Area: i.Size * scale)).ToList();

        Squarify(areas, new Rect(0, 0, width, height), result, minArea);
        return result;
    }

    private static void Squarify(
        List<(DirNode Node, double Area)> items,
        Rect bounds,
        List<TreemapBlock> output,
        double minArea)
    {
        var start = 0;
        while (start < items.Count)
        {
            var remaining = items.Count - start;
            if (remaining == 1)
            {
                var only = items[start];
                if (only.Area >= minArea) output.Add(new TreemapBlock(only.Node, bounds.X, bounds.Y, bounds.W, bounds.H));
                return;
            }

            var shortSide = Math.Min(bounds.W, bounds.H);
            var row = new List<(DirNode Node, double Area)>();
            double rowArea = 0;

            while (start < items.Count)
            {
                var cand = items[start];
                // 加进来这行会变得更「扁」就收手，否则继续加
                if (row.Count > 0 &&
                    WorstRatio(row, rowArea, cand.Area, shortSide) > WorstRatio(row, rowArea, 0, shortSide))
                    break;
                row.Add(cand);
                rowArea += cand.Area;
                start++;
            }

            if (bounds.W >= bounds.H)
            {
                var rowWidth = rowArea / Math.Max(1e-6, bounds.H);
                var y = bounds.Y;
                foreach (var (node, area) in row)
                {
                    var h = area / rowWidth;
                    if (area >= minArea) output.Add(new TreemapBlock(node, bounds.X, y, rowWidth, h));
                    y += h;
                }
                bounds = new Rect(bounds.X + rowWidth, bounds.Y, Math.Max(0, bounds.W - rowWidth), bounds.H);
            }
            else
            {
                var rowHeight = rowArea / Math.Max(1e-6, bounds.W);
                var x = bounds.X;
                foreach (var (node, area) in row)
                {
                    var w = area / rowHeight;
                    if (area >= minArea) output.Add(new TreemapBlock(node, x, bounds.Y, w, rowHeight));
                    x += w;
                }
                bounds = new Rect(bounds.X, bounds.Y + rowHeight, bounds.W, Math.Max(0, bounds.H - rowHeight));
            }

            if (bounds.W <= 0 || bounds.H <= 0) return;
        }
    }

    private static double WorstRatio(List<(DirNode Node, double Area)> row, double rowArea, double extra, double side)
    {
        double sum = rowArea + extra;
        if (sum <= 0) return double.MaxValue;
        double max = extra, min = extra <= 0 ? double.MaxValue : extra;
        foreach (var r in row)
        {
            if (r.Area > max) max = r.Area;
            if (r.Area < min) min = r.Area;
        }
        if (min <= 0) min = max;
        var s2 = sum * sum;
        var w2 = side * side;
        return Math.Max(w2 * max / s2, s2 / (w2 * min));
    }

    private readonly record struct Rect(double X, double Y, double W, double H);
}
