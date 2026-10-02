using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// GUI-Daten-Helfer: Datentypen des Spiels nachschlagen (Tab "Nachschlagen": Stichwortsuche mit Beschreibung und Vanilla-Beispielen) und vom Datenkontext
/// aus einen Ausdruck bauen (Tab "Kette"). Oben der Start, unten Vorschau und Knöpfe für beide Tabs. "Einfügen" beendet den Dialog mit
/// <see cref="Result"/>; das Einfügen selbst übernimmt der Aufrufer.
/// </summary>
public sealed class GuiDataHelperWindow : Window
{
    private sealed record StartItem(string Name, string Detail)
    {
        public override string ToString() => Detail.Length == 0 ? Name : $"{Name}  ({Detail})";
    }

    private sealed record EntryRow(DataTypeEntry Entry)
    {
        public string Name => Entry.Arguments.Count == 0 ? Entry.Name : $"{Entry.Name}( {string.Join(", ", Entry.Arguments)} )";

        public string Owner => Entry.Owner;

        public string KindText => KindOf(Entry.Kind);

        public string ReturnType => Entry.ReturnType;

        public string Description => Entry.Description;
    }

    private sealed record StepItem(DataTypeEntry Entry)
    {
        public override string ToString() =>
            $"{(Entry.Arguments.Count == 0 ? Entry.Name : $"{Entry.Name}( {string.Join(", ", Entry.Arguments)} )")}  → {(Entry.ReturnType.Length == 0 ? "-" : Entry.ReturnType)}";
    }

    private sealed record LinkRow(int Index, string Number, string Text, string Returns);

    private readonly DataTypeDatabase _db;
    private readonly GuiPropertyCatalog _catalog;
    private readonly List<StartItem> _starts;
    private readonly TextBox _startSearch = new() { Width = 170, Margin = new Thickness(0, 0, 8, 0) };
    private readonly ComboBox _startBox = new() { MinWidth = 260 };
    private readonly TextBox _query = new() { Margin = new Thickness(0, 0, 0, 6) };
    private readonly ListView _results = new();
    private readonly TextBox _examples = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), Height = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button _append = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _chainStart = new() { FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center };
    private readonly ItemsControl _links = new();
    private readonly TextBox _linkSearch = new() { Width = 170, Margin = new Thickness(0, 0, 8, 0) };
    private readonly ComboBox _linkBox = new() { IsTextSearchEnabled = true };
    private readonly TextBox _preview = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), Height = 48, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button _insert = new() { MinWidth = 90, Margin = new Thickness(0, 0, 6, 0), IsEnabled = false };
    private readonly DispatcherTimer _queryTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private IReadOnlyList<StepItem> _linkItems = Array.Empty<StepItem>();
    private DataChain _chain = DataChain.Empty(string.Empty);
    private readonly TabControl _tabs = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly TabItem _chainItem = new() { Header = GuiDataTexts.TabChain, ToolTip = GuiDataTexts.TabChainTip };
    private readonly TabItem _conditionItem = new() { Header = GuiDataTexts.TabCondition, ToolTip = GuiDataTexts.TabConditionTip };
    private readonly GuiConditionTab _conditionTab;
    private readonly Button _takeChain = new() { Padding = new Thickness(8, 1, 8, 1), Visibility = Visibility.Collapsed };
    private Action<string>? _chainTarget;
    private bool _updating;

    /// <param name="condition">Wert einer Bedingungs-Eigenschaft (<c>visible</c>, <c>enabled</c>, <c>down</c>, <c>checked</c>); dann öffnet der Tab "Bedingung" damit.</param>
    public GuiDataHelperWindow(DataTypeLoad load, GuiPropertyCatalog catalog, string? start, string? condition = null)
    {
        _db = load.Database;
        _catalog = catalog;
        _conditionTab = new GuiConditionTab(_db, start, condition);
        _conditionTab.ExpressionChanged += (_, _) => UpdatePreview();
        _conditionTab.ChainRequested += OpenChainFor;
        Title = GuiDataTexts.Title;
        Width = 900;
        Height = 700;
        MinWidth = 620;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _starts = _db.Types.Select(t => new StartItem(t, GuiDataTexts.KindType))
            .Concat(_db.Globals.Where(g => g.Kind == DataTypeKind.GlobalPromote && g.HasRegisteredReturn).Select(g => new StartItem(g.Name, g.ReturnType)))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var root = new DockPanel { Margin = new Thickness(12) };
        var top = BuildTop(load);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        var bottom = BuildBottom();
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        _tabs.Items.Add(new TabItem { Header = GuiDataTexts.TabLookup, ToolTip = GuiDataTexts.TabLookupTip, Content = BuildLookup() });
        _chainItem.Content = BuildChain();
        _tabs.Items.Add(_chainItem);
        _conditionItem.Content = _conditionTab.View;
        _tabs.Items.Add(_conditionItem);
        if (condition is not null)
        {
            _tabs.SelectedItem = _conditionItem;
        }

        // Nur Wechsel der Tabs selbst; SelectionChanged von Listen und Auswahlfeldern in den Tabs steigt bis hierher auf.
        _tabs.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, _tabs))
            {
                // Zurück zum Tab Bedingung ohne "In Bedingung übernehmen": der Rückweg zeigt vielleicht auf einen inzwischen geänderten Knoten.
                if (ConditionTabActive && _chainTarget is not null)
                {
                    _chainTarget = null;
                    _takeChain.Visibility = Visibility.Collapsed;
                }

                UpdatePreview();
            }
        };
        root.Children.Add(_tabs);
        Content = root;

        _startBox.ItemsSource = _starts;
        if (start is not null && _starts.FirstOrDefault(s => s.Name == start) is { } initial)
        {
            _updating = true;
            _startBox.SelectedItem = initial;
            _updating = false;
            _chain = DataChain.Empty(initial.Name);
        }

        Loaded += (_, _) =>
        {
            if (ConditionTabActive)
            {
                _conditionTab.FocusTree();
            }
            else
            {
                _query.Focus();
            }
        };
        UpdateChain();
    }

    /// <summary>Der Ausdruck (<c>[Country.GetCapital.GetName]</c>), wenn der Dialog mit "Einfügen" endet.</summary>
    public string? Result { get; private set; }

    private FrameworkElement BuildTop(DataTypeLoad load)
    {
        var panel = new StackPanel();
        var startRow = new DockPanel();
        startRow.Children.Add(Label(GuiDataTexts.Start, bold: true));
        startRow.Children.Add(Muted(GuiDataTexts.Search));
        startRow.Children.Add(_startSearch);
        startRow.Children.Add(_startBox);
        _startSearch.ToolTip = GuiDataTexts.SearchStartTip;
        _startBox.ToolTip = GuiDataTexts.StartTip;
        _startSearch.TextChanged += (_, _) => FilterStarts();
        _startSearch.PreviewKeyDown += SwallowEnter;
        _startBox.SelectionChanged += (_, _) =>
        {
            if (!_updating && _startBox.SelectedItem is StartItem item && item.Name != _chain.Start)
            {
                _chain = DataChain.Empty(item.Name);
                UpdateChain();
            }
        };
        panel.Children.Add(startRow);

        if (load.FilesUtc is { } filesUtc)
        {
            panel.Children.Add(new TextBlock { Text = GuiDataTexts.FilesDate(filesUtc.ToLocalTime().ToString("dd.MM.yyyy")), Margin = new Thickness(0, 4, 0, 0), Foreground = MutedBrush() });
        }

        var problems = new List<string>();
        if (load.Status == DataTypeStatus.Missing)
        {
            problems.Add(GuiDataTexts.Missing);
        }
        else if (load.Status == DataTypeStatus.Stale)
        {
            problems.Add(GuiDataTexts.Stale);
        }

        problems.AddRange(load.Unreadable.Select(GuiDataTexts.Unreadable));
        if (problems.Count > 0)
        {
            if (load.Status != DataTypeStatus.Current)
            {
                problems.Add(GuiDataTexts.HowTo);
            }

            var banner = new Border { Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 6, 0, 0), BorderThickness = new Thickness(1) };
            banner.SetResourceReference(Border.BorderBrushProperty, "Theme.Warning");
            banner.Child = new TextBlock { Text = string.Join(" ", problems), TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(banner);
        }

        return panel;
    }

    private FrameworkElement BuildLookup()
    {
        _query.ToolTip = GuiDataTexts.QueryTip;
        _query.TextChanged += (_, _) =>
        {
            _queryTimer.Stop();
            _queryTimer.Start();
        };
        _queryTimer.Tick += (_, _) =>
        {
            _queryTimer.Stop();
            _results.ItemsSource = _db.Search(_query.Text).Select(e => new EntryRow(e)).ToList();
        };
        _query.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && _results.Items.Count > 0)
            {
                _results.SelectedIndex = Math.Max(0, _results.SelectedIndex);
                _results.Focus();
                e.Handled = true;
            }
            else
            {
                SwallowEnter(_, e);
            }
        };

        var columns = new GridView();
        columns.Columns.Add(Column(GuiDataTexts.ColumnName, nameof(EntryRow.Name), 220));
        columns.Columns.Add(Column(GuiDataTexts.ColumnOwner, nameof(EntryRow.Owner), 150));
        columns.Columns.Add(Column(GuiDataTexts.ColumnKind, nameof(EntryRow.KindText), 110));
        columns.Columns.Add(Column(GuiDataTexts.ColumnReturn, nameof(EntryRow.ReturnType), 120));
        columns.Columns.Add(Column(string.Empty, nameof(EntryRow.Description), 260));
        _results.View = columns;
        _results.SelectionChanged += (_, _) => UpdateSelection();
        _results.MouseDoubleClick += (_, _) => AppendSelected();

        _append.Content = GuiDataTexts.Append;
        _append.Click += (_, _) => AppendSelected();
        ToolTipService.SetShowOnDisabled(_append, true);

        var grid = new Grid { Margin = new Thickness(8) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var queryRow = new DockPanel();
        queryRow.Children.Add(Label(GuiDataTexts.Query));
        queryRow.Children.Add(_query);
        Grid.SetRow(queryRow, 0);
        grid.Children.Add(queryRow);
        Grid.SetRow(_results, 1);
        grid.Children.Add(_results);
        var examplesLabel = new TextBlock { Text = GuiDataTexts.Examples, Margin = new Thickness(0, 6, 0, 2) };
        Grid.SetRow(examplesLabel, 2);
        grid.Children.Add(examplesLabel);
        Grid.SetRow(_examples, 3);
        grid.Children.Add(_examples);
        Grid.SetRow(_append, 4);
        grid.Children.Add(_append);
        UpdateSelection();
        return grid;
    }

    private FrameworkElement BuildChain()
    {
        var panel = new DockPanel { Margin = new Thickness(8) };
        var startLine = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        startLine.Children.Add(Label(GuiDataTexts.Start, bold: true));
        _takeChain.Content = GuiDataTexts.CondTakeChain;
        _takeChain.Click += (_, _) => TakeChain();
        DockPanel.SetDock(_takeChain, Dock.Right);
        startLine.Children.Add(_takeChain);
        startLine.Children.Add(_chainStart);
        DockPanel.SetDock(startLine, Dock.Top);
        panel.Children.Add(startLine);

        var appendRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        appendRow.Children.Add(Label(GuiDataTexts.AppendLink));
        appendRow.Children.Add(Muted(GuiDataTexts.Search));
        appendRow.Children.Add(_linkSearch);
        appendRow.Children.Add(_linkBox);
        _linkSearch.ToolTip = GuiDataTexts.AppendLinkTip;
        _linkBox.ToolTip = GuiDataTexts.AppendLinkTip;
        _linkSearch.TextChanged += (_, _) => FilterLinks();
        _linkSearch.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            if (_linkBox.Items.Count > 0 && _linkBox.Items[0] is StepItem first)
            {
                AppendLink(first.Entry);
                _linkSearch.Focus();
            }
        };
        _linkBox.DropDownClosed += (_, _) =>
        {
            if (!_updating && _linkBox.SelectedItem is StepItem item)
            {
                AppendLink(item.Entry);
            }
        };
        DockPanel.SetDock(appendRow, Dock.Bottom);
        panel.Children.Add(appendRow);

        panel.Children.Add(new ScrollViewer { Content = _links, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return panel;
    }

    private FrameworkElement BuildBottom()
    {
        var panel = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        _insert.Content = GuiDataTexts.Insert;
        _insert.ToolTip = GuiDataTexts.InsertTip;
        _insert.Click += (_, _) =>
        {
            if (CurrentExpression.Length > 0)
            {
                Result = CurrentExpression;
                DialogResult = true;
            }
        };
        var close = new Button { Content = GuiDataTexts.Close, MinWidth = 90, IsCancel = true };
        buttons.Children.Add(_insert);
        buttons.Children.Add(close);
        DockPanel.SetDock(buttons, Dock.Bottom);
        panel.Children.Add(buttons);
        var label = new TextBlock { Text = GuiDataTexts.Preview, Margin = new Thickness(0, 0, 0, 2) };
        DockPanel.SetDock(label, Dock.Top);
        panel.Children.Add(label);
        panel.Children.Add(_preview);
        return panel;
    }

    private void FilterStarts()
    {
        var filter = _startSearch.Text.Trim();
        var items = filter.Length == 0 ? _starts : _starts.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        var before = _startBox.SelectedItem as StartItem;
        _updating = true;
        _startBox.ItemsSource = items;
        _startBox.SelectedItem = before is not null && items.Contains(before) ? before : items.FirstOrDefault();
        _updating = false;
        if (_startBox.SelectedItem is StartItem now && now.Name != _chain.Start)
        {
            _chain = DataChain.Empty(now.Name);
            UpdateChain();
        }
    }

    private void FilterLinks()
    {
        var filter = _linkSearch.Text.Trim();
        _updating = true;
        _linkBox.ItemsSource = filter.Length == 0 ? _linkItems : _linkItems.Where(i => i.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        _linkBox.SelectedIndex = -1;
        _updating = false;
    }

    private void AppendLink(DataTypeEntry entry)
    {
        _chain = _chain.Append(entry);
        _updating = true;
        _linkSearch.Text = string.Empty;
        _updating = false;
        UpdateChain();
    }

    /// <summary>Was "Anhängen" mit dem gewählten Treffer täte: die neue Kette oder den Grund, warum es nicht geht.</summary>
    private (DataChain? Chain, string Reason) Plan(DataTypeEntry entry)
    {
        if (entry.Kind == DataTypeKind.Type || entry.IsGlobal)
        {
            if (_chain.Links.Count > 0)
            {
                return (null, GuiDataTexts.AppendAsStart);
            }

            var start = entry.Arguments.Count == 0 ? entry.Name : $"{entry.Name}( {string.Join(", ", Enumerable.Repeat(DataChain.ArgumentPlaceholder, entry.Arguments.Count))} )";
            return (DataChain.Empty(start), GuiDataTexts.AppendTip);
        }

        if (_chain.IsClosed)
        {
            return (null, GuiDataTexts.AppendClosed);
        }

        if (_chain.EndType(_db) is not { } end)
        {
            return (null, GuiDataTexts.ChainEmpty);
        }

        return _db.PathTo(end, entry.Owner) is { } path
            ? (_chain.AppendRange(path).Append(entry), GuiDataTexts.AppendTip)
            : (null, GuiDataTexts.AppendNoPath);
    }

    private void UpdateSelection()
    {
        if (_results.SelectedItem is not EntryRow row)
        {
            _examples.Text = string.Empty;
            _append.IsEnabled = false;
            _append.ToolTip = GuiDataTexts.AppendTip;
            return;
        }

        var examples = _catalog.ExamplesFor(row.Entry.FullName);
        _examples.Text = examples.Count == 0
            ? GuiDataTexts.NoExamples
            : string.Join(Environment.NewLine, examples.Select(e => $"{e.Expression}    ({e.File})"));
        var (chain, reason) = Plan(row.Entry);
        _append.IsEnabled = chain is not null;
        _append.ToolTip = reason;
    }

    private void AppendSelected()
    {
        if (_results.SelectedItem is EntryRow row && Plan(row.Entry).Chain is { } chain)
        {
            var newStart = chain.Start != _chain.Start;
            _chain = chain;
            if (newStart)
            {
                _updating = true;
                _startBox.ItemsSource = _starts;
                _startBox.SelectedItem = _starts.FirstOrDefault(s => s.Name == chain.Start);
                _updating = false;
            }

            UpdateChain();
            UpdateSelection();
        }
    }

    private void UpdateChain()
    {
        _chainStart.Text = _chain.Start;
        _links.Items.Clear();
        for (var i = 0; i < _chain.Links.Count; i++)
        {
            var index = i;
            var link = _chain.Links[i];
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var remove = new Button { Content = GuiDataTexts.RemoveFrom, Padding = new Thickness(6, 0, 6, 0) };
            remove.Click += (_, _) =>
            {
                _chain = _chain.TruncateAt(index);
                UpdateChain();
                UpdateSelection();
            };
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(new TextBlock { Text = $"{index + 1}.", Width = 40, Foreground = MutedBrush(), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = new StepItem(link).ToString(), FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center });
            _links.Items.Add(row);
        }

        var end = _chain.IsClosed ? null : _chain.EndType(_db);
        _linkItems = end is null ? Array.Empty<StepItem>() : _db.Members(end).Select(e => new StepItem(e)).ToList();
        FilterLinks();

        UpdatePreview();
    }

    /// <summary>Vorschau und "Einfügen" folgen dem aktiven Tab: Bedingung oder Kette.</summary>
    private void UpdatePreview()
    {
        var expression = CurrentExpression;
        _preview.Text = expression.Length > 0 ? expression : ConditionTabActive ? GuiDataTexts.CondEmptyTree : GuiDataTexts.ChainEmpty;
        _insert.IsEnabled = expression.Length > 0;
    }

    /// <summary>"Kette bauen" im Tab Bedingung: Kette laden, Rückweg merken, zum Tab Kette wechseln.</summary>
    private void OpenChainFor(string text, Action<string> apply)
    {
        _chain = ChainFrom(text);
        if (_starts.FirstOrDefault(s => s.Name == _chain.Start) is { } start)
        {
            _updating = true;
            _startBox.ItemsSource = _starts;
            _startBox.SelectedItem = start;
            _updating = false;
        }

        _chainTarget = apply;
        _takeChain.Visibility = Visibility.Visible;
        UpdateChain();
        _tabs.SelectedItem = _chainItem;
    }

    private void TakeChain()
    {
        if (_chainTarget is { } apply && _chain.Text.Length > 0)
        {
            _chainTarget = null;
            _takeChain.Visibility = Visibility.Collapsed;
            _tabs.SelectedItem = _conditionItem;
            apply(DataExpression.Strip(_chain.Text));
        }
    }

    /// <summary>
    /// Kette aus eingetipptem Text (<c>AdvanceItem.GetCost</c>) mit den Einträgen des Dumps; ein unbekanntes Glied beendet sie. Kein Kettentext: leere Kette
    /// am gewählten Start. Argumente von Gliedern werden zu Platzhaltern.
    /// </summary>
    private DataChain ChainFrom(string text)
    {
        if (DataExpressionParser.ParseDetached(text) is not ChainNode node)
        {
            return DataChain.Empty((_startBox.SelectedItem as StartItem)?.Name ?? _chain.Start);
        }

        // Beginnt die Kette mit einem Mitglied des Datenkontexts (GetCost statt AdvanceItem.GetCost), startet sie beim Start des Fensters.
        var first = node.Links[0];
        var isStart = (first.Args is null && _db.IsType(first.Name)) || _db.Globals.Any(g => g.Name == first.Name);
        var windowStart = (_startBox.SelectedItem as StartItem)?.Name;
        var chain = isStart || windowStart is null
            ? DataChain.Empty(DataExpressionWriter.Write(new ChainNode(new[] { first }, null), string.Empty))
            : DataChain.Empty(windowStart);
        foreach (var link in isStart || windowStart is null ? node.Links.Skip(1) : node.Links)
        {
            if (chain.EndType(_db) is not { } type || _db.Members(type).FirstOrDefault(m => m.Name == link.Name) is not { } entry)
            {
                break;
            }

            chain = chain.Append(entry);
        }

        return chain;
    }

    public bool ConditionTabActive => ReferenceEquals(_tabs.SelectedItem, _conditionItem);

    /// <summary>Ausdruck des aktiven Tabs, den "Einfügen" liefern würde; leer, wenn es noch keinen gibt.</summary>
    public string CurrentExpression => ConditionTabActive ? _conditionTab.Expression : _chain.Text;

    private static void SwallowEnter(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
        }
    }

    private static string KindOf(DataTypeKind kind) => kind switch
    {
        DataTypeKind.Type => GuiDataTexts.KindType,
        DataTypeKind.Promote => GuiDataTexts.KindPromote,
        DataTypeKind.Function => GuiDataTexts.KindFunction,
        DataTypeKind.GlobalPromote => GuiDataTexts.KindGlobalPromote,
        DataTypeKind.GlobalFunction => GuiDataTexts.KindGlobalFunction,
        _ => GuiDataTexts.KindGlobalMacro,
    };

    private static GridViewColumn Column(string header, string path, double width) =>
        new() { Header = header, DisplayMemberBinding = new System.Windows.Data.Binding(path), Width = width };

    private static TextBlock Label(string text, bool bold = false) =>
        new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }

    private static Brush MutedBrush() => Application.Current?.TryFindResource("Theme.TextMuted") as Brush ?? Brushes.Gray;
}
