using System.Diagnostics;
using DiskCleaner.Core.Clean;
using DiskCleaner.Core.Dup;
using DiskCleaner.Core.Scan;

var mode = args.Length > 0 ? args[0] : "scan";

if (mode == "selftest-recycle")
{
    var dir = Path.Combine(Path.GetTempPath(), "diskcleaner-selftest");
    Directory.CreateDirectory(dir);
    var files = new List<string>();
    for (var i = 0; i < 3; i++)
    {
        var f = Path.Combine(dir, $"t{i}.txt");
        File.WriteAllText(f, new string('x', 100 * 1024));
        files.Add(f);
    }

    var before = DiskCleaner.Core.Interop.ShellInterop.QueryRecycleBin(null);
    Console.WriteLine($"回收站(前): {before.Size:N0} bytes, {before.Items:N0} 项");

    var failed = DiskCleaner.Core.Interop.ShellInterop.DeleteToRecycleBin(files);
    var after = DiskCleaner.Core.Interop.ShellInterop.QueryRecycleBin(null);

    Console.WriteLine($"删除失败数: {failed}");
    Console.WriteLine($"文件还在吗: {files.Count(f => File.Exists(f))}/{files.Count}（应为 0/3）");
    Console.WriteLine($"回收站(后): {after.Size:N0} bytes, {after.Items:N0} 项（应 +3 项）");

    Directory.Delete(dir, true);
    return;
}

if (mode == "clean")
{
    var dryRun = args.Contains("--dry");
    Console.WriteLine(dryRun ? "清理自检（dry-run，不删任何东西）" : "清理自检（真实执行）");
    Console.WriteLine();

    var sw = Stopwatch.StartNew();
    var items = CleanScanner.Scan(DefaultRules.All,
        new Progress<CleanScanProgress>(p =>
        {
            if (!string.IsNullOrEmpty(p.CurrentRule))
                Console.WriteLine($"  扫描中: {p.CurrentRule}");
        }));

    Console.WriteLine($"扫描耗时 {sw.ElapsedMilliseconds} ms");
    Console.WriteLine();
    long totalSize = 0;
    var totalCount = 0;
    foreach (var it in items)
    {
        totalSize += it.Size;
        totalCount += it.FileCount;
        var state = it.State switch
        {
            CleanItemState.Ready => "OK  ",
            CleanItemState.NotApplicable => "N/A ",
            CleanItemState.Failed => "FAIL",
            _ => "    ",
        };
        Console.WriteLine($"{state} {it.Size / 1048576.0,10:F1} MB  {it.FileCount,8:N0} 文件  {it.Name}" +
                          (it.Problem is null ? "" : $"  ({it.Problem})"));
    }
    Console.WriteLine();
    Console.WriteLine($"合计: {totalSize / 1073741824.0:F2} GB, {totalCount:N0} 文件");

    if (args.Contains("--run"))
    {
        Console.WriteLine();
        Console.WriteLine("执行清理（缓存类直接删，回收站项跳过）...");
        var ids = items.Where(i => i.CanClean && i.Kind != CleanRuleKind.RecycleBin).Select(i => i.RuleId).ToArray();
        Console.WriteLine("选中: " + string.Join(", ", ids));
        var outcome = CleanExecutor.Execute(DefaultRules.All, ids, null, default, dryRun: dryRun);
        Console.WriteLine($"删除 {outcome.FilesDeleted:N0} 个文件, 释放 {outcome.BytesFreed / 1073741824.0:F2} GB, 失败 {outcome.FilesFailed:N0}");
        if (outcome.Problem != null) Console.WriteLine("问题: " + outcome.Problem);
    }

    return;
}

if (mode == "treemap")
{
    var treeTarget = args.Length > 1 ? args[1] : @"E:\";
    var res = DiskScanner.Scan(treeTarget);
    const double W = 800, H = 500;
    var sw = Stopwatch.StartNew();
    var blocks = TreemapLayout.Layout(res.Root, W, H);
    sw.Stop();

    Console.WriteLine($"目录: {treeTarget}  根大小 {res.Root.Size / 1073741824.0:F2} GB");
    Console.WriteLine($"布局耗时: {sw.ElapsedMilliseconds} ms, 块数: {blocks.Count}, 子项总数: {res.Root.Children.Count}");

    var area = 0.0;
    var bad = 0;
    var negative = 0;
    foreach (var b in blocks)
    {
        if (b.Width < 0 || b.Height < 0) negative++;
        if (b.X < -0.01 || b.Y < -0.01 || b.X + b.Width > W + 0.01 || b.Y + b.Height > H + 0.01) bad++;
        area += b.Width * b.Height;
    }

    var overlap = 0;
    for (var i = 0; i < blocks.Count; i++)
    for (var j = i + 1; j < blocks.Count; j++)
    {
        var a = blocks[i];
        var b = blocks[j];
        var ox = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        var oy = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        if (ox > 0.01 && oy > 0.01) overlap++;
    }

    Console.WriteLine($"面积覆盖率: {area / (W * H) * 100:F2}%  (应≈100%)");
    Console.WriteLine($"越界块: {bad}  负尺寸块: {negative}  重叠对: {overlap}   (应全为 0)");
    foreach (var b in blocks.Take(8))
        Console.WriteLine($"  {b.Node.Name,-24} {b.Node.Size / 1073741824.0,8:F2} GB  x={b.X,7:F1} y={b.Y,7:F1} w={b.Width,7:F1} h={b.Height,7:F1}");
    return;
}

if (mode == "dup")
{
    var dupTarget = args.Length > 1 ? args[1] : @"E:\";
    var minSize = args.Length > 2 ? long.Parse(args[2]) : 1024 * 1024;
    Console.WriteLine($"查找重复文件: {dupTarget}  最小 {minSize / 1024.0:F0} KB");
    var sw = Stopwatch.StartNew();
    long lastTick = 0;
    var groups = DuplicateFinder.Find(dupTarget, new Progress<DupProgress>(p =>
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref lastTick) < 500) return;
        Interlocked.Exchange(ref lastTick, now);
        Console.WriteLine($"  {p.Stage}  {p.FilesDone:N0}/{p.FilesTotal:N0}");
    }), minFileSize: minSize);
    sw.Stop();

    var totalWasted = groups.Sum(g => g.WastedBytes);
    Console.WriteLine($"完成: {groups.Count} 组重复, 共 {groups.Sum(g => g.Files.Count)} 个文件, 可省 {totalWasted / 1073741824.0:F2} GB, 耗时 {sw.Elapsed.TotalSeconds:F1} 秒");
    foreach (var g in groups.Take(10))
    {
        Console.WriteLine($"  [{g.Size / 1048576.0:F1} MB x {g.Files.Count}] 可省 {g.WastedBytes / 1048576.0:F1} MB");
        foreach (var f in g.Files.Take(3))
            Console.WriteLine($"      {f.Path}");
    }
    return;
}

if (mode == "find")
{
    var findRoot = args.Length > 1 ? args[1] : @"E:\";
    var query = args.Length > 2 ? args[2] : "临时文件";
    Console.WriteLine($"查找: {query}   位置: {findRoot}");
    Console.WriteLine();

    var sw = Stopwatch.StartNew();
    var hits = DiskCleaner.Core.Find.KeywordFinder.Find(
        findRoot,
        query,
        progress: new Progress<DiskCleaner.Core.Find.KeywordProgress>(p =>
            Console.WriteLine($"  {p.FilesSeen:N0} 文件, 命中 {p.Hits:N0}")));
    sw.Stop();

    var totalBytes = hits.Sum(h => h.Size);
    Console.WriteLine();
    Console.WriteLine($"命中 {hits.Count:N0} 个文件, 合计 {totalBytes / 1073741824.0:F2} GB, 耗时 {sw.Elapsed.TotalSeconds:F1} 秒");
    foreach (var h in hits.Take(15))
        Console.WriteLine($"  {h.Size / 1048576.0,9:F1} MB  {h.Path}");
    return;
}

var target = args.Length > 0 ? args[0] : @"E:\";
Console.WriteLine($"扫描: {target}");

long lastPrint = 0;
var result = DiskScanner.Scan(
    target,
    new Progress<ScanProgress>(p =>
    {
        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref lastPrint) < 2000) return;
        Interlocked.Exchange(ref lastPrint, now);
        Console.WriteLine($"  [{p.ElapsedMs / 1000.0,6:F1}s] {p.FilesSeen,12:N0} 文件  {p.BytesSeen / 1073741824.0,9:F2} GB");
    }));

Console.WriteLine();
Console.WriteLine($"完成: {result.FilesSeen:N0} 文件, {result.BytesSeen / 1073741824.0:F2} GB, {result.ElapsedMs / 1000.0:F1} 秒, 跳过 {result.InaccessibleDirs} 个无权限目录");
Console.WriteLine();
Console.WriteLine("目录占比（按大小降序）:");
foreach (var child in result.Root.Children.Take(20))
{
    var pct = child.Size * 100.0 / Math.Max(1, result.Root.Size);
    Console.WriteLine($"  {child.Size / 1073741824.0,9:F2} GB  {pct,5:F1}%  {child.FileCount,10:N0} 文件  {child.FullPath}");
}

Console.WriteLine();
Console.WriteLine($"最大文件 Top {Math.Min(15, result.TopFiles.Count)}（共收集 {result.TopFiles.Count} 个）:");
foreach (var f in result.TopFiles.Take(15))
{
    Console.WriteLine($"  {f.Size / 1048576.0,9:F1} MB  {f.Path}");
}
