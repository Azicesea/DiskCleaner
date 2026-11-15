using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DiskCleaner.Core.Clean;

namespace DiskCleaner.App;

/// <summary>管理用户自定义的清理规则：新增、删除，改动立刻存盘。</summary>
public partial class CustomRulesWindow : Window
{
    private readonly ObservableCollection<RuleRow> _rows = new();
    private readonly List<CleanRule> _rules;

    /// <summary>关闭窗口时，主窗口从这里取最新的规则列表。</summary>
    public IReadOnlyList<CleanRule> Rules => _rules;

    public CustomRulesWindow(IEnumerable<CleanRule> rules)
    {
        InitializeComponent();
        _rules = rules.ToList();
        RuleList.ItemsSource = _rows;
        foreach (var rule in _rules) _rows.Add(new RuleRow { Rule = rule });
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var paths = PathBox.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToArray();

        if (name.Length == 0)
        {
            MessageBox.Show(this, "给这条规则起个名字，比如「我的下载缓存」。", "还差一点",
                MessageBoxButton.OK, MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }
        if (paths.Length == 0)
        {
            MessageBox.Show(this, "至少写一个路径。", "还差一点",
                MessageBoxButton.OK, MessageBoxImage.Information);
            PathBox.Focus();
            return;
        }

        // 先看看这些路径现在能不能找到东西；找不到也允许存，只是提醒一下
        var hits = 0;
        foreach (var p in paths)
        {
            try { hits += PathGlob.Expand(p).Count(); }
            catch { /* 写错了的模板，下面统一提示 */ }
        }
        if (hits == 0)
        {
            var ok = MessageBox.Show(this,
                "这些路径现在一个文件都没找到。\n\n可能是还没下载过东西，也可能路径写错了。\n仍然保存吗？",
                "没找到文件", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes) return;
        }

        var rule = new CleanRule
        {
            Id = "custom-" + Guid.NewGuid().ToString("N"),
            Name = name,
            Description = "自定义清理规则",
            Category = CleanCategory.Temp,
            Kind = CleanRuleKind.Files,
            Paths = paths,
            UseRecycleBin = RecycleBox.IsChecked == true,
        };

        _rules.Add(rule);
        _rows.Add(new RuleRow { Rule = rule });
        Persist();

        NameBox.Clear();
        PathBox.Clear();
        HintText.Text = $"已添加「{name}」。关掉这个窗口后回到清理页，点「扫描可清理项」就能看到它。";
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (RuleList.SelectedItem is not RuleRow row) return;
        _rules.Remove(row.Rule);
        _rows.Remove(row);
        Persist();
        HintText.Text = $"已删除「{row.Name}」。";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Persist()
    {
        try
        {
            CustomRuleStore.Save(_rules);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存规则失败：" + ex.Message, "没法保存",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

public sealed class RuleRow
{
    public required CleanRule Rule { get; init; }
    public string Name => Rule.Name;
    public string PathsText => string.Join("    ", Rule.Paths.Select((p, i) => (i + 1) + ". " + p));
    public string RecycleText => Rule.UseRecycleBin ? "删除时放进回收站" : "直接删除（不进回收站）";
}
