using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using DiskCleaner.Core.Models;
using DiskCleaner.Core.Scan;

namespace DiskCleaner.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<DirRow> _rows = new();
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        ResultGrid.ItemsSource = _rows;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                DriveCombo.Items.Add(new DriveItem
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
        if (DriveCombo.Items.Count > 0) DriveCombo.SelectedIndex = 0;
    }

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
