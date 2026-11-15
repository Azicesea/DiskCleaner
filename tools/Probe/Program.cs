using System.Diagnostics;
using DiskCleaner.Core.Scan;

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
