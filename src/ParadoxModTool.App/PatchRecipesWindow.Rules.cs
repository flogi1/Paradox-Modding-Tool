using System.IO;
using System.Windows;
using System.Windows.Markup;
using ParadoxModTool.Patches;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Eine Regel in der Liste links: Haken zum Ein- und Ausschalten, Art, Name, Ziel; ausgegraut, wenn nicht aktiv. Der Haken schreibt sofort in die Regel und meldet
/// das über <see cref="Toggled"/> (Rezept als geändert merken, Regel-Editor nachziehen).
/// </summary>
public sealed class PatchRuleItem : System.ComponentModel.INotifyPropertyChanged
{
    public PatchRuleItem(PatchRule rule, Action<PatchRuleItem>? toggled = null)
    {
        Rule = rule;
        Toggled = toggled;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public Action<PatchRuleItem>? Toggled { get; }

    public PatchRule Rule { get; }

    public bool Enabled
    {
        get => Rule.Enabled;
        set
        {
            if (Rule.Enabled == value)
            {
                return;
            }

            Rule.Enabled = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Enabled)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Opacity)));
            Toggled?.Invoke(this);
        }
    }

    public string Kind => Rule switch
    {
        LineRule => PatchRecipeTexts.KindLines,
        BlockCopyRule => PatchRecipeTexts.KindBlockCopy,
        BlockEditRule => PatchRecipeTexts.KindBlockEdit,
        TemplateRule => PatchRecipeTexts.KindTemplate,
        _ => PatchRecipeTexts.KindList,
    };

    public string Name => Rule.Name.Length > 0 ? Rule.Name : Rule.Id;

    public string Target => Rule switch
    {
        LineRule line => line.Files,
        BlockCopyRule copy => "→ " + copy.Target,
        BlockEditRule edit => edit.Files,
        TemplateRule template => "→ " + template.Target,
        ListEntryRule list => list.Target + " · " + list.ListPath,
        _ => string.Empty,
    };

    public double Opacity => Rule.Enabled ? 1.0 : 0.5;
}

/// <summary>Reiter "Regeln": Liste links (Reihenfolge = Ausführung), Editor der gewählten Regel rechts, Warnungen und Einzel-Vorschau.</summary>
public partial class PatchRecipesWindow
{
    private readonly Controls.ListBox _ruleList = new() { Width = 300 };
    private readonly Controls.CheckBox _wholeRows = new()
    {
        Content = new Controls.TextBlock { Text = PatchRecipeTexts.WholeRows, TextWrapping = TextWrapping.Wrap, Width = 280 },
        ToolTip = PatchRecipeTexts.WholeRowsTip,
        Margin = new Thickness(0, 8, 0, 0),
    };
    private readonly Controls.Button _topUp = new()
    {
        Content = new Controls.TextBlock { Text = PatchRecipeTexts.TopUp, TextWrapping = TextWrapping.Wrap },
        ToolTip = PatchRecipeTexts.TopUpTip,
        Padding = new Thickness(8, 2, 8, 2),
        Margin = new Thickness(0, 8, 0, 0),
        Visibility = Visibility.Collapsed,
    };
    private readonly Controls.ContentControl _ruleEditorHost = new();
    private readonly Controls.TextBlock _ruleWarnings = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TextBlock _rulePlaceholders = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Controls.ComboBox _previewRow = new() { MinWidth = 220, IsEditable = true, IsTextSearchEnabled = true };
    private readonly Controls.TextBlock _noRules = new() { Text = PatchRecipeTexts.NoRules, Margin = new Thickness(0, 8, 0, 0) };
    private bool _refreshingRules;

    partial void InitRules()
    {
        _ruleList.ItemTemplate = (DataTemplate)XamlReader.Parse(
            "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "<Grid Margin=\"0,2\">" +
            "<Grid.ColumnDefinitions><ColumnDefinition Width=\"Auto\" /><ColumnDefinition Width=\"*\" /></Grid.ColumnDefinitions>" +
            "<CheckBox Grid.Column=\"0\" IsChecked=\"{Binding Enabled, Mode=TwoWay}\" VerticalAlignment=\"Top\" Margin=\"0,3,6,0\" ToolTip=\"" + System.Security.SecurityElement.Escape(PatchRecipeTexts.RuleToggleTip) + "\" />" +
            "<StackPanel Grid.Column=\"1\" Opacity=\"{Binding Opacity}\">" +
            "<StackPanel Orientation=\"Horizontal\">" +
            "<Border CornerRadius=\"3\" Padding=\"4,0\" Margin=\"0,0,5,0\" Background=\"{DynamicResource Theme.ControlHover}\"><TextBlock Text=\"{Binding Kind}\" FontSize=\"10\" /></Border>" +
            "<TextBlock Text=\"{Binding Name}\" FontWeight=\"SemiBold\" />" +
            "</StackPanel>" +
            "<TextBlock Text=\"{Binding Target}\" FontSize=\"11\" Foreground=\"{DynamicResource Theme.TextMuted}\" TextTrimming=\"CharacterEllipsis\" />" +
            "</StackPanel></Grid></DataTemplate>");
        _ruleList.SelectionChanged += (_, _) =>
        {
            if (!_refreshingRules)
            {
                ShowRuleEditor();
            }
        };
        _ruleWarnings.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.Warning");
        _rulePlaceholders.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");

        var add = RuleButton(PatchRecipeTexts.AddRule, (_, _) => { });
        add.ContextMenu = new Controls.ContextMenu();
        foreach (var (kind, label) in new[] { ("lines", PatchRecipeTexts.KindLines), ("blockCopy", PatchRecipeTexts.KindBlockCopy), ("blockEdit", PatchRecipeTexts.KindBlockEdit), ("template", PatchRecipeTexts.KindTemplate), ("listEntry", PatchRecipeTexts.KindList) })
        {
            var item = new Controls.MenuItem { Header = label };
            item.Click += (_, _) => AddRule(kind, label);
            add.ContextMenu.Items.Add(item);
        }

        add.Click += (_, _) =>
        {
            add.ContextMenu.PlacementTarget = add;
            add.ContextMenu.IsOpen = true;
        };

        var listButtons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        listButtons.Children.Add(add);
        listButtons.Children.Add(RuleButton("▲", (_, _) => MoveRule(-1)));
        listButtons.Children.Add(RuleButton("▼", (_, _) => MoveRule(1)));
        listButtons.Children.Add(RuleButton(PatchRecipeTexts.RemoveRule, (_, _) => RemoveRule()));
        var left = new Controls.DockPanel { Margin = new Thickness(0, 0, 10, 0) };
        _wholeRows.Click += (_, _) =>
        {
            if (Recipe is { } recipe)
            {
                recipe.WholeRows = _wholeRows.IsChecked == true;
                MarkRecipeEdited();
            }
        };
        _topUp.Click += (_, _) => TopUpFromPreset();
        Controls.DockPanel.SetDock(_topUp, Controls.Dock.Bottom);
        left.Children.Add(_topUp);
        Controls.DockPanel.SetDock(_wholeRows, Controls.Dock.Bottom);
        left.Children.Add(_wholeRows);
        Controls.DockPanel.SetDock(listButtons, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_noRules, Controls.Dock.Top);
        left.Children.Add(listButtons);
        left.Children.Add(_noRules);
        left.Children.Add(_ruleList);

        var previewBar = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        previewBar.Children.Add(new Controls.TextBlock { Text = PatchRecipeTexts.PreviewFor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        previewBar.Children.Add(_previewRow);
        previewBar.Children.Add(RuleButton(PatchRecipeTexts.ShowResult, async (_, _) => await ShowRuleResultAsync()));
        var right = new Controls.StackPanel();
        right.Children.Add(_ruleEditorHost);
        right.Children.Add(_rulePlaceholders);
        right.Children.Add(_ruleWarnings);
        right.Children.Add(previewBar);

        Controls.DockPanel.SetDock(left, Controls.Dock.Left);
        RulesRoot.Children.Add(left);
        RulesRoot.Children.Add(new Controls.ScrollViewer { Content = right, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });
    }

    partial void RulesRecipeSelected()
    {
        _wholeRows.IsChecked = Recipe?.WholeRows == true;
        _topUp.Visibility = Recipe is not null && PresetTopUp.Template(Recipe) is not null ? Visibility.Visible : Visibility.Collapsed;
        RefreshRuleList(null);
    }

    private Controls.Button RuleButton(string text, RoutedEventHandler click)
    {
        var button = new Controls.Button { Content = new Controls.TextBlock { Text = text }, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0) };
        button.Click += click;
        return button;
    }

    private PatchRule? SelectedRule => (_ruleList.SelectedItem as PatchRuleItem)?.Rule;

    /// <summary>"Fehlendes aus der Vorlage ergänzen": zeigt, was fehlt, und ergänzt es nach Rückfrage; Vorhandenes bleibt unverändert.</summary>
    private void TopUpFromPreset()
    {
        if (Recipe is not { } recipe)
        {
            return;
        }

        var missing = PresetTopUp.Missing(recipe);
        if (missing.Count == 0)
        {
            SetStatus(PatchRecipeTexts.TopUpNone);
            return;
        }

        var lines = new List<string>();
        if (missing.Rules.Count > 0)
        {
            lines.Add(PatchRecipeTexts.TopUpRules(string.Join(", ", missing.Rules)));
        }

        if (missing.Columns.Count > 0)
        {
            lines.Add(PatchRecipeTexts.TopUpColumns(string.Join(", ", missing.Columns)));
        }

        if (missing.Edits.Count > 0)
        {
            lines.Add(PatchRecipeTexts.TopUpEdits(string.Join(", ", missing.Edits)));
        }

        if (missing.Patterns.Count > 0)
        {
            lines.Add(PatchRecipeTexts.TopUpPatterns(string.Join(", ", missing.Patterns)));
        }

        if (MessageBox.Show(this, PatchRecipeTexts.TopUpConfirm(string.Join("\n", lines)), PatchRecipeTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var selected = SelectedRule;
        var result = PresetTopUp.Apply(recipe);
        MarkRecipeEdited();
        RefreshRuleList(selected);
        if (result.Columns.Count > 0)
        {
            RebuildTable();
        }

        SetStatus(PatchRecipeTexts.TopUpDone(result.Count));
    }

    private void RefreshRuleList(PatchRule? select)
    {
        _refreshingRules = true;
        var items = Recipe?.Rules.Select(r => new PatchRuleItem(r, RuleToggled)).ToList() ?? new List<PatchRuleItem>();
        _ruleList.ItemsSource = items;
        _ruleList.SelectedItem = items.FirstOrDefault(i => i.Rule == select) ?? items.FirstOrDefault();
        _noRules.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _previewRow.ItemsSource = Recipe?.Rows.Select(r => r.Key).Where(k => k.Length > 0).Distinct().ToList();
        if (_previewRow.SelectedIndex < 0 && _previewRow.Items.Count > 0)
        {
            _previewRow.SelectedIndex = 0;
        }

        _refreshingRules = false;
        ShowRuleEditor();
    }

    private void ShowRuleEditor()
    {
        if (SelectedRule is not { } rule || Recipe is not { } recipe)
        {
            _ruleEditorHost.Content = null;
            _ruleWarnings.Text = _rulePlaceholders.Text = string.Empty;
            return;
        }

        _ruleEditorHost.Content = PatchRuleEditor.Build(rule, _host.Highlighting(), RuleEdited, () => Dispatcher.BeginInvoke(ShowRuleEditor));
        _rulePlaceholders.Text = PatchRecipeTexts.Placeholders(string.Join(" ", new[] { "{key}" }.Concat(recipe.Columns.Select(c => "{" + c.Name + "}"))));
        UpdateRuleWarnings();
    }

    /// <summary>Jede Eingabe im Editor: speichern (verzögert), Warnungen und Listeneintrag aktualisieren.</summary>
    private void RuleEdited()
    {
        MarkRecipeEdited();
        UpdateRuleWarnings();
        if (_ruleList.SelectedItem is PatchRuleItem item)
        {
            _refreshingRules = true;
            var items = (List<PatchRuleItem>)_ruleList.ItemsSource;
            var index = items.IndexOf(item);
            items[index] = new PatchRuleItem(item.Rule, RuleToggled);
            _ruleList.Items.Refresh();
            _ruleList.SelectedIndex = index;
            _refreshingRules = false;
        }
    }

    /// <summary>Haken in der Regelliste: Rezept als geändert merken; ist es die gewählte Regel, zieht das Feld "Aktiv" im Editor nach.</summary>
    private void RuleToggled(PatchRuleItem item)
    {
        if (_refreshingRules)
        {
            return;
        }

        MarkRecipeEdited();
        if (ReferenceEquals(SelectedRule, item.Rule))
        {
            ShowRuleEditor();
        }
    }

    private void UpdateRuleWarnings()
    {
        _ruleWarnings.Text = SelectedRule is { } rule && Recipe is { } recipe
            ? string.Join(Environment.NewLine, RecipeEditing.RuleWarnings(recipe, rule))
            : string.Empty;
    }

    private void AddRule(string kind, string label)
    {
        if (Recipe is not { } recipe)
        {
            return;
        }

        var rule = RecipeEditing.NewRule(recipe, kind, label);
        recipe.Rules.Add(rule);
        MarkRecipeEdited();
        RefreshRuleList(rule);
    }

    private void MoveRule(int delta)
    {
        if (Recipe is { } recipe && SelectedRule is { } rule)
        {
            RecipeEditing.Move(recipe, rule.Id, delta);
            MarkRecipeEdited();
            RefreshRuleList(rule);
        }
    }

    private void RemoveRule()
    {
        if (Recipe is not { } recipe || SelectedRule is not { } rule
            || MessageBox.Show(this, PatchRecipeTexts.RemoveRuleConfirm(rule.Name), PatchRecipeTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        recipe.Rules.Remove(rule);
        MarkRecipeEdited();
        RefreshRuleList(null);
    }

    /// <summary>"Ergebnis zeigen": nur diese Regel für diese Zeile, Ergebnis als schreibgeschützter Vergleich je Datei.</summary>
    private async Task ShowRuleResultAsync()
    {
        if (Recipe is not { } recipe || SelectedRule is not { } rule || ModRoot is not { } modRoot)
        {
            return;
        }

        var rowKey = _previewRow.Text.Trim();
        var plan = await RunPlanAsync(RecipeEditing.SingleRulePreview(recipe, rule.Id, rowKey));
        if (plan is null)
        {
            return;
        }

        new PatchResultWindow(plan, modRoot, PatchRecipeTexts.ResultTitle(rule.Name, rowKey)) { Owner = this }.Show();
    }
}

/// <summary>Schreibgeschütztes Ergebnis eines Plans: Dateiliste, Vergleich aktuell gegen nachher, übersprungene Zeilen und Warnungen.</summary>
public sealed class PatchResultWindow : Window
{
    public PatchResultWindow(PatchPlan plan, string modRoot, string title)
    {
        Title = title;
        Width = 1100;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var pane = new DiffComparePane();
        pane.Initialize(() => false, _ => { }, _ => false);
        var files = plan.Files.Where(f => f.Status != PlannedFileStatus.Unchanged).ToList();
        var list = new Controls.ListBox { ItemsSource = files, DisplayMemberPath = nameof(PlannedFile.RelativePath), Margin = new Thickness(0, 0, 6, 0) };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is PlannedFile file)
            {
                pane.LoadTexts(file.OriginalText ?? string.Empty, file.NewText, PatchRecipeTexts.ResultBefore, PatchRecipeTexts.ResultAfter, Path.Combine(modRoot, file.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
        };

        var report = new Controls.TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 110,
            VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 6, 0, 0),
            Text = string.Join(Environment.NewLine,
                (files.Count == 0 ? new[] { PatchRecipeTexts.ResultNothing } : Array.Empty<string>())
                    .Concat(plan.Skipped.Select(s => PatchRecipeTexts.SkippedLine(s.RuleId, s.RowKey, s.Reason)))
                    .Concat(plan.Warnings)),
        };

        var grid = new Controls.Grid { Margin = new Thickness(8) };
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(280) });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new Controls.RowDefinition { Height = GridLength.Auto });
        Controls.Grid.SetColumn(pane, 1);
        Controls.Grid.SetRow(report, 1);
        Controls.Grid.SetColumnSpan(report, 2);
        grid.Children.Add(list);
        grid.Children.Add(pane);
        grid.Children.Add(report);
        Content = grid;
        list.SelectedIndex = files.Count > 0 ? 0 : -1;
    }
}
