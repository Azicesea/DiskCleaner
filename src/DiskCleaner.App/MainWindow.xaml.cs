using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DiskCleaner.Core.Clean;
using DiskCleaner.Core.Dup;
using DiskCleaner.Core.Models;
using DiskCleaner.Core.Scan;

namespace DiskCleaner.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DirRow> _rows = new();
    private readonly ObservableCollection<CleanRow> _cleanRows = new();
    private readonly ObservableCollection<BigFileRow> _bigRows = new();
    private readonly ObservableCollection<DupRow> _dupRows = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _dupCts;
    private List<CleanRule> _customRules = CustomRuleStore.Load();

    private DirNode? _treemapRoot;
    private readonly List<DirNode> _treemapStack = new();

    public MainWindow()
    {
        InitializeComponent();
        ResultGrid.ItemsSource = _rows;
        CleanList.ItemsSource = _cleanRows;
        BigFileGrid.ItemsSource = _bigRows;
        DupGrid.ItemsSource = _dupRows;
        DupMinSizeCombo.Items.Add(new SizeOption { Label = "1 MB", Bytes = 1024L * 1024 });
        DupMinSizeCombo.Items.Add(new SizeOption { Label = "10 MB", Bytes = 10L * 1024 * 1024 });
        DupMinSizeCombo.Items.Add(new SizeOption { Label = "100 MB", Bytes = 100L * 1024 * 1024 });
        DupMinSizeCombo.SelectedIndex = 0;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        FillDriveCombo(DriveCombo);
        FillDriveCombo(DupDriveCombo);
    }

    /// <summary>把本机的固定磁盘填进下拉框，默认选第一个。</summary>
    private static void FillDriveCombo(System.Windows.Controls.ComboBox combo)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                combo.Items.Add(new DriveItem
                {
                    Path = drive.RootDirectory.FullName,
                    Label = $"{drive.Name.TrimEnd('\\')}   可用 {FormatSize(drive.AvailableFreeSpace)} / 共 {FormatSize(drive.TotalSize)}",
                });
            }
            catch
            {
                // 光驱/不可用盘符直接跳过
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    // ================= 磁盘占用 =================

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (DriveCombo.SelectedItem is not DriveItem drive) return;

        _cts = new CancellationTokenSource();
        SetBusy(true);
        _rows.Clear();
        StatusText.Text = "";
        ProgressText.Text = "扫描中…";

        var progress = new Progress<ScanProgress>(p =>
        {
            ProgressText.Text =
                $"扫描中… 已读 {p.FilesSeen:N0} 个文件 / {FormatSize(p.BytesSeen)}，已用 {p.ElapsedMs / 1000.0:F0} 秒";
        });

        try
        {
            var result = await Task.Run(
                () => DiskScanner.Scan(drive.Path, progress, _cts.Token));

            FillRows(result);
            FillBigFiles(result);
            _treemapRoot = result.Root;
            _treemapStack.Clear();
            if (ViewMapRadio.IsChecked == true) RenderTreemap();
            StatusText.Text =
                $"扫描完成：{result.FilesSeen:N0} 个文件，共 {FormatSize(result.BytesSeen)}，" +
                $"耗时 {(result.ElapsedMs / 1000.0):F1} 秒" +
                (result.InaccessibleDirs > 0 ? $"，跳过 {result.InaccessibleDirs} 个无权限目录" : "");
            ProgressText.Text = "扫描完成";
        }
        catch (OperationCanceledException)
        {
            ProgressText.Text = "已取消";
        }
        catch (Exception ex)
        {
            ProgressText.Text = "扫描失败";
            MessageBox.Show(this, ex.Message, "扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void FillRows(ScanResult result)
    {
        var root = result.Root;
        var total = Math.Max(1, root.Size);

        var items = new List<DirRow>();
        if (root.DirectFileCount > 0)
        {
            items.Add(new DirRow
            {
                Name = "（根目录直属文件）",
                Size = root.DirectSize,
                FileCount = root.DirectFileCount,
                Percent = root.DirectSize * 100.0 / total,
            });
        }
        foreach (var child in root.Children)
        {
            items.Add(new DirRow
            {
                Name = child.Name,
                Size = child.Size,
                FileCount = child.FileCount,
                Percent = child.Size * 100.0 / total,
            });
        }
        items.Sort((a, b) => b.Size.CompareTo(a.Size));

        foreach (var row in items) _rows.Add(row);
    }

    private void SetBusy(bool busy)
    {
        ScanButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        DriveCombo.IsEnabled = !busy;
        ScanProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ScanProgressBar.IsIndeterminate = busy;
    }

    // ================= 大文件 =================

    private void FillBigFiles(ScanResult result)
    {
        _bigRows.Clear();
        foreach (var f in result.TopFiles)
        {
            _bigRows.Add(new BigFileRow
            {
                Name = f.Name,
                Size = f.Size,
                Folder = f.Directory ?? "",
                FullPath = f.Path,
            });
        }

        BigFileStatus.Text = _bigRows.Count == 0
            ? "没有找到文件"
            : $"最大的 {_bigRows.Count} 个文件，合计 {FormatSize(_bigRows.Sum(r => r.Size))}。双击可在资源管理器中定位。";
    }

    private void BigFileGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (BigFileGrid.SelectedItem is not BigFileRow row) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{row.FullPath}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "无法打开所在文件夹",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ================= 重复文件 =================

    private async void DupScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (DupDriveCombo.SelectedItem is not DriveItem drive)
        {
            MessageBox.Show(this, "先选一个要扫描的磁盘。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _dupCts?.Cancel();
        _dupCts = new CancellationTokenSource();
        var ct = _dupCts.Token;
        var minLabel = (DupMinSizeCombo.SelectedItem as SizeOption)?.Label ?? "1 MB";
        var minSize = (DupMinSizeCombo.SelectedItem as SizeOption)?.Bytes ?? 1024L * 1024;

        _dupRows.Clear();
        DupStatus.Text = "";
        SetDupBusy(true);

        var progress = new Progress<DupProgress>(p =>
        {
            if (p.FilesTotal > 0)
            {
                if (DupProgressBar.IsIndeterminate)
                {
                    DupProgressBar.IsIndeterminate = false;
                    DupProgressBar.Minimum = 0;
                    DupProgressBar.Maximum = p.FilesTotal;
                }
                DupProgressBar.Value = p.FilesDone;
                DupProgressText.Text = $"{p.Stage}  {p.FilesDone:N0} / {p.FilesTotal:N0}";
            }
            else
            {
                DupProgressText.Text = p.Stage;
            }
        });

        try
        {
            var sw = Stopwatch.StartNew();
            var groups = await Task.Run(() => DuplicateFinder.Find(drive.Path, progress, ct, minSize), ct);
            sw.Stop();

            var groupNo = 0;
            var wasted = 0L;
            var totalFiles = 0;
            foreach (var group in groups)
            {
                groupNo++;
                wasted += group.WastedBytes;
                foreach (var file in group.Files)
                {
                    totalFiles++;
                    _dupRows.Add(new DupRow
                    {
                        GroupText = $"#{groupNo} · {group.Files.Count} 个",
                        Name = file.Name,
                        Folder = file.Directory ?? "",
                        FullPath = file.Path,
                        Size = file.Size,
                        Note = IsSystemPath(file.Path) ? "系统文件" : "",
                    });
                }
            }

            if (groups.Count == 0)
            {
                DupStatus.Text = $"{drive.Path} 里没找到重复文件（只看大于 {minLabel} 的）。";
                DupProgressText.Text = "没有发现重复";
            }
            else
            {
                DupStatus.Text =
                    $"{drive.Path} 找到 {groups.Count:N0} 组重复、共 {totalFiles:N0} 个文件，" +
                    $"每组只留 1 个能省 {FormatSize(wasted)}，检查耗时 {sw.Elapsed.TotalSeconds:F1} 秒。" +
                    "标着「系统文件」的是程序自带的多份拷贝，不建议删。";
                DupProgressText.Text = "双击一行可以在资源管理器中定位。";
            }
        }
        catch (OperationCanceledException)
        {
            DupStatus.Text = "已取消。";
        }
        catch (Exception ex)
        {
            DupStatus.Text = "查找失败：" + ex.Message;
        }
        finally
        {
            SetDupBusy(false);
        }
    }

    private void DupCancelButton_Click(object sender, RoutedEventArgs e) => _dupCts?.Cancel();

    private void DupGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DupGrid.SelectedItem is not DupRow row) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{row.FullPath}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "无法打开所在文件夹",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetDupBusy(bool busy)
    {
        DupScanButton.IsEnabled = !busy;
        DupCancelButton.IsEnabled = busy;
        DupMinSizeCombo.IsEnabled = !busy;
        DupDriveCombo.IsEnabled = !busy;
        DupProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            DupProgressBar.IsIndeterminate = true;
            DupProgressText.Text = "正在查找…";
        }
    }

    /// <summary>系统目录里的重复文件大多是程序自带的多份拷贝，标出来提醒别乱删。</summary>
    private static bool IsSystemPath(string path)
    {
        static bool Under(string dir, string p) =>
            !string.IsNullOrEmpty(dir) && p.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase);

        return Under(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path)
            || Under(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), path)
            || Under(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), path);
    }

    // ================= 树图 =================

    private void ViewMode_Changed(object sender, RoutedEventArgs e)
    {
        // XAML 解析阶段 IsChecked="True" 就会触发一次，那时控件字段还没赋值。
        if (ResultGrid is null || TreemapHost is null) return;

        var showMap = ViewMapRadio.IsChecked == true;
        ResultGrid.Visibility = showMap ? Visibility.Collapsed : Visibility.Visible;
        TreemapHost.Visibility = showMap ? Visibility.Visible : Visibility.Collapsed;
        if (showMap) RenderTreemap();
    }

    private void TreemapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewMapRadio is null || TreemapCanvas is null) return;
        if (ViewMapRadio.IsChecked == true) RenderTreemap();
    }

    private void TreemapUp_Click(object sender, RoutedEventArgs e)
    {
        if (_treemapStack.Count == 0) return;
        _treemapRoot = _treemapStack[^1];
        _treemapStack.RemoveAt(_treemapStack.Count - 1);
        RenderTreemap();
    }

    private void RenderTreemap()
    {
        TreemapCanvas.Children.Clear();
        var node = _treemapRoot;
        if (node is null)
        {
            TreemapPathText.Text = "还没有扫描";
            TreemapUpButton.IsEnabled = false;
            return;
        }

        TreemapPathText.Text = node.FullPath;
        TreemapUpButton.IsEnabled = _treemapStack.Count > 0;

        var w = TreemapCanvas.ActualWidth;
        var h = TreemapCanvas.ActualHeight;
        if (w < 20 || h < 20) return;

        var blocks = TreemapLayout.Layout(node, w, h);
        var total = Math.Max(1, node.Size);

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var child = block.Node;
            var share = child.Size * 100.0 / total;
            var canDrill = child.Children.Count > 0 || child.DirectSize > 0;

            var rect = new Rectangle
            {
                Width = Math.Max(0, block.Width - 1),
                Height = Math.Max(0, block.Height - 1),
                Fill = BlockBrush(i),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = canDrill ? Cursors.Hand : Cursors.Arrow,
                ToolTip = $"{child.Name}\n{FormatSize(child.Size)}  {child.FileCount:N0} 个文件  {share:F1}%",
            };
            var target = child;
            var drillable = canDrill;
            rect.MouseLeftButtonUp += (_, _) => DrillInto(target, drillable);
            Canvas.SetLeft(rect, block.X);
            Canvas.SetTop(rect, block.Y);
            TreemapCanvas.Children.Add(rect);

            if (block.Width >= 68 && block.Height >= 30)
            {
                var label = new TextBlock
                {
                    Text = block.Height >= 46
                        ? $"{child.Name}\n{FormatSize(child.Size)}  {share:F0}%"
                        : $"{child.Name}  {FormatSize(child.Size)}",
                    Foreground = Brushes.White,
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    IsHitTestVisible = false,
                    Margin = new Thickness(4, 2, 0, 0),
                };
                Canvas.SetLeft(label, block.X);
                Canvas.SetTop(label, block.Y);
                TreemapCanvas.Children.Add(label);
            }
        }
    }

    private void DrillInto(DirNode node, bool canDrill)
    {
        if (!canDrill || _treemapRoot is null) return;
        _treemapStack.Add(_treemapRoot);
        _treemapRoot = node;
        RenderTreemap();
    }

    private static readonly Brush[] BlockBrushes =
    {
        MakeBrush(0x4C, 0x8B, 0xF5), MakeBrush(0x5E, 0x9A, 0xE8), MakeBrush(0x6F, 0xA6, 0xDC),
        MakeBrush(0x7E, 0xB0, 0xD2), MakeBrush(0x6A, 0xB3, 0xB0), MakeBrush(0x7B, 0xBE, 0xA4),
        MakeBrush(0x8E, 0xC6, 0x95), MakeBrush(0xA3, 0xCB, 0x86), MakeBrush(0xBE, 0xC4, 0x76),
        MakeBrush(0xCF, 0xB5, 0x6C), MakeBrush(0xD9, 0xA2, 0x6C), MakeBrush(0xD3, 0x8E, 0x7E),
    };

    private static Brush MakeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Brush BlockBrush(int index) => BlockBrushes[index % BlockBrushes.Length];

    // ================= 清理 =================

    private async void CleanScanButton_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetCleanBusy(true);
        _cleanRows.Clear();
        CleanStatusText.Text = "正在扫描可清理的内容…";

        try
        {
            await ReloadCleanItemsAsync(_cts.Token);
            var ready = _cleanRows.Where(r => r.Item.CanClean).ToList();
            var total = ready.Sum(r => r.Size);
            CleanStatusText.Text = ready.Count == 0
                ? "没有找到可清理的内容"
                : $"扫描完成：{ready.Count} 项可清理，共 {FormatSize(total)}";
        }
        catch (OperationCanceledException)
        {
            CleanStatusText.Text = "已取消";
        }
        catch (Exception ex)
        {
            CleanStatusText.Text = "扫描失败";
            MessageBox.Show(this, ex.Message, "扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetCleanBusy(false);
            UpdateCleanSummary();
        }
    }

    private async void CustomRulesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var win = new CustomRulesWindow(_customRules) { Owner = this };
            win.ShowDialog();
            _customRules = win.Rules.ToList();
        }
        catch (Exception)
        {
            return;
        }

        // 规则改了，直接重扫一遍，免得用户看着旧列表
        _cts = new CancellationTokenSource();
        SetCleanBusy(true);
        _cleanRows.Clear();
        CleanStatusText.Text = "规则变了，正在重新扫描…";
        try
        {
            await ReloadCleanItemsAsync(_cts.Token);
            var ready = _cleanRows.Where(r => r.Item.CanClean).ToList();
            CleanStatusText.Text = ready.Count == 0
                ? "没有找到可清理的内容"
                : $"扫描完成：{ready.Count} 项可清理，共 {FormatSize(ready.Sum(r => r.Size))}";
        }
        catch (OperationCanceledException)
        {
            CleanStatusText.Text = "已取消";
        }
        catch (Exception ex)
        {
            CleanStatusText.Text = "扫描失败";
            MessageBox.Show(this, ex.Message, "扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetCleanBusy(false);
            UpdateCleanSummary();
        }
    }

    private async Task ReloadCleanItemsAsync(CancellationToken ct)
    {
        var progress = new Progress<CleanScanProgress>(p =>
        {
            if (!string.IsNullOrEmpty(p.CurrentRule))
                CleanStatusText.Text = $"正在扫描：{p.CurrentRule}（{p.Done + 1}/{p.Total}）";
        });

        var rules = AllCleanRules();
        var items = await Task.Run(() => CleanScanner.Scan(rules, progress, ct), ct);
        var previouslyChecked = _cleanRows
            .Where(r => r.IsChecked)
            .Select(r => r.Item.RuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hadRows = _cleanRows.Count > 0;

        _cleanRows.Clear();
        foreach (var item in items)
        {
            var rule = rules.FirstOrDefault(r => r.Id == item.RuleId);
            if (rule is null) continue;
            var row = new CleanRow
            {
                Item = item,
                Rule = rule,
                IsChecked = hadRows
                    ? previouslyChecked.Contains(item.RuleId)
                    : DefaultCheckedFor(rule),
            };
            row.PropertyChanged += (_, _) => UpdateCleanSummary();
            _cleanRows.Add(row);
        }
    }

    /// <summary>内置规则 + 用户自定义规则。扫描和清理都必须用同一份，否则 Id 对不上。</summary>
    private IReadOnlyList<CleanRule> AllCleanRules()
    {
        if (_customRules.Count == 0) return DefaultRules.All;
        var list = new List<CleanRule>(DefaultRules.All);
        list.AddRange(_customRules);
        return list;
    }

    /// <summary>自定义规则是用户自己加的，默认勾上；回收站依旧不默认勾。</summary>
    private static bool DefaultCheckedFor(CleanRule rule) =>
        rule.Id.StartsWith("custom-", StringComparison.Ordinal) || DefaultRules.DefaultChecked(rule);

    private async void CleanRunButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _cleanRows.Where(r => r.IsChecked && r.Item.CanClean).ToList();
        if (selected.Count == 0) return;

        var total = selected.Sum(r => r.Size);
        var toRecycle = RecycleCheck.IsChecked == true;
        var hasRecycleBinItem = selected.Any(r => r.Item.Kind == CleanRuleKind.RecycleBin);

        var sb = new StringBuilder();
        sb.AppendLine($"即将清理 {selected.Count} 项，共约 {FormatSize(total)}：");
        sb.AppendLine();
        foreach (var r in selected) sb.AppendLine($"  · {r.Name}    {r.SizeText}");
        sb.AppendLine();
        sb.AppendLine(toRecycle
            ? "删掉的文件会放进回收站，随时可以还原。"
            : "这些都是缓存/临时文件，会被直接删除（用的时候自动重建）。");
        if (hasRecycleBinItem)
            sb.AppendLine("注意：勾选的「回收站」会彻底清空，这部分不可还原。");

        var confirm = MessageBox.Show(this, sb.ToString(), "确认清理",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        _cts = new CancellationTokenSource();
        SetCleanBusy(true);
        CleanProgressBar.IsIndeterminate = false;
        CleanProgressBar.Value = 0;
        CleanProgressBar.Visibility = Visibility.Visible;

        var ids = selected.Select(r => r.Item.RuleId).ToArray();
        var progress = new Progress<CleanProgress>(p =>
        {
            CleanStatusText.Text =
                $"正在清理：{p.CurrentRule}（{p.RulesDone}/{p.RulesTotal}）已处理 {p.FilesDone:N0} 个文件，{FormatSize(p.BytesDone)}";
            CleanProgressBar.Value = p.RulesTotal == 0 ? 0 : p.RulesDone * 100.0 / p.RulesTotal;
        });

        try
        {
            var rules = AllCleanRules();
            var result = await Task.Run(() => CleanExecutor.Execute(
                rules, ids, progress, _cts.Token, forceRecycleBin: toRecycle));

            var summary = $"清理完成：删除 {result.FilesDeleted:N0} 个文件，释放 {FormatSize(result.BytesFreed)}";
            if (result.FilesFailed > 0) summary += $"，{result.FilesFailed:N0} 个文件被占用跳过";
            if (result.Problem != null) summary += $"（{result.Problem}）";

            CleanStatusText.Text = summary;

            // 重新扫描，数字刷新
            await ReloadCleanItemsAsync(_cts.Token);
            UpdateCleanSummary();
        }
        catch (OperationCanceledException)
        {
            CleanStatusText.Text = "已取消";
        }
        catch (Exception ex)
        {
            CleanStatusText.Text = "清理失败";
            MessageBox.Show(this, ex.Message, "清理失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetCleanBusy(false);
        }
    }

    private void UpdateCleanSummary()
    {
        var ready = _cleanRows.Where(r => r.Item.CanClean).ToList();
        var selected = ready.Where(r => r.IsChecked).ToList();
        var allSize = ready.Sum(r => r.Size);
        var selSize = selected.Sum(r => r.Size);

        CleanHint.Text = ready.Count == 0
            ? ""
            : $"共可清理 {FormatSize(allSize)} · 已选 {selected.Count} 项 / {FormatSize(selSize)}";

        CleanRunButton.IsEnabled = selected.Count > 0;
    }

    private void SetCleanBusy(bool busy)
    {
        CleanScanButton.IsEnabled = !busy;
        CleanRunButton.IsEnabled = !busy && _cleanRows.Any(r => r.IsChecked && r.Item.CanClean);
        RecycleCheck.IsEnabled = !busy;
        CleanList.IsEnabled = !busy;
        CleanProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CleanProgressBar.IsIndeterminate = true;
        CleanProgressBar.Value = 0;
    }

    internal static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double v = bytes;
        string[] units = { "KB", "MB", "GB", "TB" };
        int i = -1;
        do { v /= 1024; i++; } while (v >= 1024 && i < units.Length - 1);
        return $"{v:F1} {units[i]}";
    }
}

public sealed class DriveItem
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public override string ToString() => Label;
}

public sealed class DirRow
{
    public required string Name { get; init; }
    public long Size { get; init; }
    public long FileCount { get; init; }
    public double Percent { get; init; }

    public string SizeText => MainWindow.FormatSize(Size);
    public string FilesText => FileCount.ToString("N0");
    public string PercentText => Percent <= 0 ? "" : Percent.ToString("F1") + "%";
}

/// <summary>大文件页的一行。</summary>
public sealed class BigFileRow
{
    public required string Name { get; init; }
    public required string Folder { get; init; }
    public required string FullPath { get; init; }
    public long Size { get; init; }

    public string SizeText => MainWindow.FormatSize(Size);
}

/// <summary>重复文件页的一行。</summary>
public sealed class DupRow
{
    public required string GroupText { get; init; }
    public required string Name { get; init; }
    public required string Folder { get; init; }
    public required string FullPath { get; init; }
    public long Size { get; init; }
    public string Note { get; init; } = "";

    public string SizeText => MainWindow.FormatSize(Size);
}

/// <summary>「只看大于」下拉框的选项。</summary>
public sealed class SizeOption
{
    public required string Label { get; init; }
    public long Bytes { get; init; }
    public override string ToString() => Label;
}

/// <summary>清理页的一行，IsChecked 变化时通知父窗口更新合计。</summary>
public sealed class CleanRow : INotifyPropertyChanged
{
    private bool _isChecked;

    public required CleanItem Item { get; init; }
    public required CleanRule Rule { get; init; }

    public string Name => Item.Name;
    public string Description => Item.Description;
    public long Size => Item.Size;

    public string SizeText => Item.State switch
    {
        CleanItemState.NotApplicable => "—",
        CleanItemState.Failed => "无法读取",
        _ => MainWindow.FormatSize(Item.Size),
    };

    public string FilesText => Item.State switch
    {
        CleanItemState.NotApplicable => "本机没有",
        CleanItemState.Failed => "无权限",
        _ => $"{Item.FileCount:N0} 个文件",
    };

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
