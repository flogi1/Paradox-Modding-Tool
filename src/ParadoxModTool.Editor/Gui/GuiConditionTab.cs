using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Tab "Bedingung" im GUI-Daten-Helfer (siehe docs/superpowers/specs/2026-09-26-gui-condition-helper-design.md): links der Bedingungsbaum mit Knöpfen,
/// rechts der Editor des markierten Knotens, unten Klartext und Hinweise. Der Ausdruck selbst steht in der Vorschau des Fensters.
/// </summary>
public sealed class GuiConditionTab
{
    private readonly DataTypeDatabase _db;
    private readonly string? _start;
    private readonly ComparisonCatalog _catalog;
    private readonly ParsedExpression? _loaded;
    private readonly List<DataTypeEntry> _checks;
    private readonly TreeView _tree = new() { MinWidth = 220 };
    private readonly ContentControl _detail = new() { Margin = new Thickness(10, 0, 0, 0) };
    private readonly TextBox _plain = new() { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), Height = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ListBox _hints = new() { Height = 64 };
    private readonly Border _banner = new() { Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
    private readonly Dictionary<string, Button> _buttons = new();
    private bool _refreshing;
    private bool _selecting;
    private int _detailVersion;
    private CondPath? _selected;

    public GuiConditionTab(DataTypeDatabase db, string? start, string? condition)
    {
        _db = db;
        _start = start;
        _catalog = ComparisonCatalog.From(db);
        _checks = db.Globals
            .Where(g => g.Kind == DataTypeKind.GlobalFunction && g.ReturnType == "bool" && !IsLogicOrComparison(g.Name))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _loaded = condition is null ? null : DataExpressionParser.Parse(condition);
        LoadFailed = _loaded is { Errors.Count: > 0 };
        Editor = new ConditionEditor(LoadFailed || _loaded is null ? null : ConditionTree.From(_loaded, _catalog));
        Editor.Changed += (_, _) => Refresh();
        View = Build();
        Refresh();
    }

    public event EventHandler? ExpressionChanged;

    /// <summary>"Kette bauen": aktueller Kettentext und der Rückweg, der die fertige Kette (ohne Klammern) an diese Stelle setzt.</summary>
    public event Action<string, Action<string>>? ChainRequested;

    public FrameworkElement View { get; }

    public ConditionEditor Editor { get; }

    /// <summary>Markierter Knoten; setzen markiert ihn auch im Baum und zeigt seinen Editor.</summary>
    public CondPath? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            if (!_refreshing && !_selecting)
            {
                _selecting = true;
                SelectInTree();
                _selecting = false;
                UpdateButtons();
                ShowDetail();
            }
        }
    }

    public bool LoadFailed { get; private set; }

    public string Expression => ConditionWriter.WriteFull(_loaded, Editor.Root);

    public string PlainText => _plain.Text;

    public IReadOnlyList<ConditionHint> Hints { get; private set; } = Array.Empty<ConditionHint>();

    public int TreeItemCount => Items(_tree.Items).Count();

    /// <summary>Fokus auf den markierten Knoten (sonst den Baum), damit Entf und Strg+Z gleich wirken.</summary>
    public void FocusTree()
    {
        if (_tree.SelectedItem is TreeViewItem item)
        {
            item.Focus();
        }
        else
        {
            _tree.Focus();
        }
    }

    /// <summary>Kaputter Ausdruck: mit leerem Baum weiter.</summary>
    public void StartEmpty()
    {
        LoadFailed = false;
        if (Editor.Root is not null)
        {
            Editor.Remove(CondPath.Root);
        }
        else
        {
            Refresh();
        }
    }

    /// <summary>Kaputter Ausdruck: den ganzen Innentext als Rohtext übernehmen.</summary>
    public void TakeAsRaw()
    {
        LoadFailed = false;
        var text = _loaded?.Root is RawNode raw ? raw.Text : string.Empty;
        Selected = Editor.Add(null, new RawCondNode(text, null));
    }

    /// <summary>Seite eines Vergleichs, Argument einer Prüfung oder Inhalt eines Werts aus eingetipptem Text (Kette, Aufruf oder Rohtext).</summary>
    public void SetSide(CondPath at, int side, string text) => ApplySide(at, side, DataExpressionParser.ParseDetached(text) ?? new RawNode(string.Empty, null));

    /// <summary>Konstante; ohne Typ nimmt ein Vergleich seine Variante, sonst wird es Text.</summary>
    public void SetConstant(CondPath at, int side, string value, string? type)
    {
        if (type is null && Editor.NodeAt(at) is CompareNode { Variant: { Length: > 0 } variant })
        {
            type = variant;
        }

        ApplySide(at, side, new LiteralNode(type, value, null));
    }

    public void SetVariant(CondPath at, string variant)
    {
        if (Editor.NodeAt(at) is CompareNode compare)
        {
            Editor.Replace(at, Retype(compare with { Variant = variant, Source = null }));
        }
    }

    public void SetKind(CondPath at, CompareKind kind)
    {
        if (Editor.NodeAt(at) is not CompareNode compare)
        {
            return;
        }

        var arity = ComparisonCatalog.Arity(kind);
        var empty = new RawNode(string.Empty, null);
        var variant = compare.Variant is { } v && _catalog.Variants(kind).Contains(v) ? v : AutoVariant(kind, compare.Left);
        Editor.Replace(at, Retype(new CompareNode(
            kind,
            variant,
            compare.Left,
            arity > 1 ? compare.Right ?? empty : null,
            arity > 2 ? compare.Right2 ?? empty : null,
            null)));
    }

    public void SetCheck(CondPath at, string name)
    {
        if (Editor.NodeAt(at) is not CheckNode check)
        {
            return;
        }

        var count = ExpressionArgs(name) ?? check.Args.Count;
        var args = Enumerable.Range(0, count).Select(i => i < check.Args.Count ? check.Args[i] : new RawNode(string.Empty, null)).ToArray();
        Editor.Replace(at, new CheckNode(name, args, null));
    }

    /// <summary>Rohtext übernehmen; ist er gültig, entstehen daraus echte Knoten.</summary>
    public void SetRaw(CondPath at, string text) =>
        Editor.Replace(at, ConditionTree.FromText(text, _catalog) ?? new RawCondNode(text, null));

    private static bool IsLogicOrComparison(string name) =>
        name == "Not" || System.Text.RegularExpressions.Regex.IsMatch(name, "^(And|Or)[2-8]?$") || ComparisonCatalog.TryKind(name, out _);

    private int? ExpressionArgs(string name) => _db.Globals.FirstOrDefault(g => g.Name == name)?.Arguments.Count;

    /// <summary>Variante nach dem Typ der linken Seite; Text heißt im Dump <c>CString</c>, die Variante <c>string</c>.</summary>
    private string? AutoVariant(CompareKind kind, ExprNode left) =>
        ExpressionTypes.TypeOf(left, _db, _start) is { } type && (type == "CString" ? "string" : type) is var variant && _catalog.Variants(kind).Contains(variant)
            ? variant
            : null;

    /// <summary>Konstanten mit Typ folgen der Variante des Vergleichs.</summary>
    private static CompareNode Retype(CompareNode compare)
    {
        // Zahlen ohne Typ (eingetippt, bevor es eine Variante gab) bekommen den Typ ebenfalls; Text bleibt Text.
        ExprNode? Fix(ExprNode? side) =>
            side is LiteralNode literal
            && compare.Variant is { Length: > 0 } variant
            && variant != "string"
            && literal.Type != variant
            && (literal.Type is not null || double.TryParse(literal.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                ? new LiteralNode(variant, literal.Value, null)
                : side;
        return compare with { Left = Fix(compare.Left)!, Right = Fix(compare.Right), Right2 = Fix(compare.Right2) };
    }

    private void ApplySide(CondPath at, int side, ExprNode expr)
    {
        switch (Editor.NodeAt(at))
        {
            case CompareNode compare:
                var changed = side switch
                {
                    0 => compare with { Left = expr, Source = null },
                    1 => compare with { Right = expr, Source = null },
                    _ => compare with { Right2 = expr, Source = null },
                };
                if (side == 0 && AutoVariant(changed.Kind, expr) is { } variant)
                {
                    changed = changed with { Variant = variant };
                }

                Editor.Replace(at, Retype(changed));
                break;
            case CheckNode check:
                var args = check.Args.ToList();
                while (args.Count <= side)
                {
                    args.Add(new RawNode(string.Empty, null));
                }

                args[side] = expr;
                Editor.Replace(at, new CheckNode(check.Name, args, null));
                break;
            case ValueNode:
                Editor.Replace(at, new ValueNode(expr, null));
                break;
        }
    }

    private FrameworkElement Build()
    {
        var root = new DockPanel { Margin = new Thickness(8) };

        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        var add = ToolButton("add", GuiDataTexts.CondAdd, GuiDataTexts.CondAddTip, () => { });
        var menu = new ContextMenu();
        menu.Items.Add(MenuEntry(GuiDataTexts.CondAddValue, () => new ValueNode(new RawNode(string.Empty, null), null)));
        menu.Items.Add(MenuEntry(GuiDataTexts.CondAddCompare, () => new CompareNode(CompareKind.GreaterThan, null, new RawNode(string.Empty, null), new RawNode(string.Empty, null), null, null)));
        menu.Items.Add(MenuEntry(GuiDataTexts.CondAddCheck, () =>
        {
            var name = _checks.FirstOrDefault(c => c.Name == "IsDataModelEmpty")?.Name ?? _checks.FirstOrDefault()?.Name ?? "IsDataModelEmpty";
            return new CheckNode(name, Enumerable.Range(0, ExpressionArgs(name) ?? 1).Select(_ => (ExprNode)new RawNode(string.Empty, null)).ToArray(), null);
        }));
        menu.Items.Add(MenuEntry(GuiDataTexts.CondAddAnd, () => new LogicNode(LogicOp.And, Array.Empty<CondNode>(), null)));
        menu.Items.Add(MenuEntry(GuiDataTexts.CondAddOr, () => new LogicNode(LogicOp.Or, Array.Empty<CondNode>(), null)));
        menu.Items.Add(MenuEntry(GuiDataTexts.CondAddRaw, () => new RawCondNode(string.Empty, null)));
        add.ContextMenu = menu;
        add.Click += (_, _) =>
        {
            menu.PlacementTarget = add;
            menu.IsOpen = true;
        };
        toolbar.Children.Add(add);
        toolbar.Children.Add(ToolButton("wrapAnd", GuiDataTexts.CondWrapAnd, null, () => Selected = Editor.Wrap(Selected!, LogicOp.And)));
        toolbar.Children.Add(ToolButton("wrapOr", GuiDataTexts.CondWrapOr, null, () => Selected = Editor.Wrap(Selected!, LogicOp.Or)));
        toolbar.Children.Add(ToolButton("not", GuiDataTexts.CondToggleNot, null, () => Selected = Editor.ToggleNot(Selected!)));
        toolbar.Children.Add(ToolButton("up", GuiDataTexts.CondMoveUp, null, () => Selected = Editor.Move(Selected!, -1) ?? Selected));
        toolbar.Children.Add(ToolButton("down", GuiDataTexts.CondMoveDown, null, () => Selected = Editor.Move(Selected!, 1) ?? Selected));
        toolbar.Children.Add(ToolButton("remove", GuiDataTexts.CondRemove, null, RemoveSelected));
        toolbar.Children.Add(ToolButton("merge", GuiDataTexts.CondMerge, GuiDataTexts.CondMergeTip, () => Selected = Editor.Merge(Selected!)));
        toolbar.Children.Add(ToolButton("undo", GuiDataTexts.CondUndo, null, Editor.Undo));
        toolbar.Children.Add(ToolButton("redo", GuiDataTexts.CondRedo, null, Editor.Redo));
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        _banner.BorderThickness = new Thickness(1);
        _banner.SetResourceReference(Border.BorderBrushProperty, "Theme.Warning");
        var bannerPanel = new DockPanel();
        var bannerButtons = new StackPanel { Orientation = Orientation.Horizontal };
        var startEmpty = new Button { Content = GuiDataTexts.CondStartEmpty, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(8, 0, 0, 0) };
        startEmpty.Click += (_, _) => StartEmpty();
        var takeRaw = new Button { Content = GuiDataTexts.CondTakeAsRaw, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
        takeRaw.Click += (_, _) => TakeAsRaw();
        bannerButtons.Children.Add(startEmpty);
        bannerButtons.Children.Add(takeRaw);
        DockPanel.SetDock(bannerButtons, Dock.Right);
        bannerPanel.Children.Add(bannerButtons);
        bannerPanel.Children.Add(new TextBlock { Text = BrokenText(), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        _banner.Child = bannerPanel;
        DockPanel.SetDock(_banner, Dock.Top);
        root.Children.Add(_banner);

        var bottom = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        bottom.Children.Add(new TextBlock { Text = GuiDataTexts.CondPlain, Margin = new Thickness(0, 0, 0, 2) });
        bottom.Children.Add(_plain);
        bottom.Children.Add(new TextBlock { Text = GuiDataTexts.CondHints, Margin = new Thickness(0, 6, 0, 2) });
        bottom.Children.Add(_hints);
        _hints.SelectionChanged += (_, _) =>
        {
            if (!_refreshing && _hints.SelectedItem is ListBoxItem { Tag: CondPath path })
            {
                Selected = path;
                SelectInTree();
            }
        };
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        var main = new Grid();
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 200 });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 240 });
        main.Children.Add(_tree);
        var splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent };
        Grid.SetColumn(splitter, 1);
        main.Children.Add(splitter);
        var detailScroll = new ScrollViewer { Content = _detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(detailScroll, 2);
        main.Children.Add(detailScroll);
        root.Children.Add(main);

        _tree.SelectedItemChanged += (_, _) =>
        {
            if (!_refreshing && !_selecting)
            {
                Selected = (_tree.SelectedItem as TreeViewItem)?.Tag as CondPath;
            }
        };
        root.PreviewKeyDown += OnKey;
        return root;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox)
        {
            return;
        }

        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        if (ctrl && e.Key == Key.Z)
        {
            Editor.Undo();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.Y)
        {
            Editor.Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && Selected is not null && IsInTree(e.OriginalSource as DependencyObject))
        {
            RemoveSelected();
            e.Handled = true;
        }
    }

    /// <summary>Entf wirkt nur im Baum, nicht in der Prüfungs- oder Hinweisliste.</summary>
    private bool IsInTree(DependencyObject? element)
    {
        for (var current = element; current is not null; current = LogicalTreeHelper.GetParent(current) ?? (current is Visual ? VisualTreeHelper.GetParent(current) : null))
        {
            if (ReferenceEquals(current, _tree))
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveSelected()
    {
        if (Selected is { } path)
        {
            Selected = Editor.Remove(path);
        }
    }

    private string BrokenText()
    {
        if (_loaded is not { Errors.Count: > 0 } loaded)
        {
            return string.Empty;
        }

        var error = loaded.Errors[0];
        var what = error.Kind switch
        {
            ExprErrorKind.MissingClose => GuiDataTexts.CondErrMissingClose,
            ExprErrorKind.UnclosedQuote => GuiDataTexts.CondErrUnclosedQuote,
            _ => GuiDataTexts.CondErrUnexpected,
        };
        return GuiDataTexts.CondBroken(error.Position + 1, what);
    }

    private void Refresh()
    {
        _refreshing = true;
        _banner.Visibility = LoadFailed ? Visibility.Visible : Visibility.Collapsed;
        if (Selected is not null && Editor.NodeAt(Selected) is null)
        {
            Selected = Selected.Parent is { } parent && Editor.NodeAt(parent) is not null ? parent : null;
        }

        if (Selected is null && Editor.Root is not null)
        {
            Selected = CondPath.Root;
        }

        Hints = ConditionChecker.Check(Editor.Root, _db, _start);
        _tree.Items.Clear();
        if (Editor.Root is { } node)
        {
            _tree.Items.Add(TreeItem(node, CondPath.Root));
        }

        _plain.Text = Editor.Root is null ? GuiDataTexts.CondEmptyTree : ConditionPhrases.Describe(Editor.Root);
        _hints.Items.Clear();
        foreach (var hint in Hints)
        {
            _hints.Items.Add(new ListBoxItem { Content = ConditionPhrases.Hint(hint), Tag = hint.Path });
        }

        if (Hints.Count == 0)
        {
            _hints.Items.Add(new ListBoxItem { Content = GuiDataTexts.CondNoHints, IsEnabled = false });
        }

        SelectInTree();
        _refreshing = false;
        UpdateButtons();
        ShowDetail();
        ExpressionChanged?.Invoke(this, EventArgs.Empty);
    }

    private TreeViewItem TreeItem(CondNode node, CondPath path)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        var mark = Hints.Where(h => h.Path.Equals(path)).ToList();
        if (mark.Count > 0)
        {
            var warning = mark.Any(h => h.Kind != ConditionHintKind.RawText);
            header.Children.Add(new TextBlock
            {
                Text = warning ? "⚠ " : "• ",
                Foreground = warning ? Brushes.Goldenrod : Brushes.Gray,
                ToolTip = string.Join(Environment.NewLine, mark.Select(ConditionPhrases.Hint)),
            });
        }

        header.Children.Add(new TextBlock { Text = ConditionPhrases.Label(node), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 6, 0) });
        header.Children.Add(new TextBlock { Text = ConditionPhrases.Short(node), FontFamily = new FontFamily("Consolas") });
        var item = new TreeViewItem { Header = header, Tag = path, IsExpanded = true };
        switch (node)
        {
            case LogicNode logic:
                for (var i = 0; i < logic.Items.Count; i++)
                {
                    item.Items.Add(TreeItem(logic.Items[i], path.Child(i)));
                }

                break;
            case NotNode not:
                item.Items.Add(TreeItem(not.Inner, path.Child(0)));
                break;
        }

        return item;
    }

    private void SelectInTree()
    {
        if (Selected is not null && Items(_tree.Items).FirstOrDefault(i => Selected.Equals(i.Tag)) is { } item)
        {
            item.IsSelected = true;
        }
    }

    private static IEnumerable<TreeViewItem> Items(ItemCollection items)
    {
        foreach (var item in items.OfType<TreeViewItem>())
        {
            yield return item;
            foreach (var child in Items(item.Items))
            {
                yield return child;
            }
        }
    }

    private void UpdateButtons()
    {
        var node = Selected is null ? null : Editor.NodeAt(Selected);
        var inGroup = Selected?.Parent is { } parent && Editor.NodeAt(parent) is LogicNode group ? group : null;
        Enable("wrapAnd", node is not null);
        Enable("wrapOr", node is not null);
        Enable("not", node is not null);
        Enable("up", inGroup is not null && Selected!.Last > 0);
        Enable("down", inGroup is not null && Selected!.Last < inGroup.Items.Count - 1);
        Enable("remove", node is not null);
        Enable("merge", node is not null && !ReferenceEquals(ConditionWriter.Merge(node), node));
        Enable("undo", Editor.CanUndo);
        Enable("redo", Editor.CanRedo);
    }

    private void Enable(string key, bool enabled) => _buttons[key].IsEnabled = enabled;

    private void ShowDetail()
    {
        _detailVersion++;
        if (Selected is not { } path || Editor.NodeAt(path) is not { } node)
        {
            _detail.Content = Muted(Editor.Root is null ? GuiDataTexts.CondEmptyTree : string.Empty);
            return;
        }

        var panel = new StackPanel();
        switch (node)
        {
            case LogicNode logic:
                panel.Children.Add(Muted(GuiDataTexts.CondGroupInfo(ConditionPhrases.Label(logic), logic.Items.Count)));
                break;
            case NotNode:
                panel.Children.Add(Muted(GuiDataTexts.CondNotInfo));
                break;
            case CompareNode compare:
                BuildCompare(panel, path, compare);
                break;
            case CheckNode check:
                BuildCheck(panel, path, check);
                break;
            case ValueNode value:
                panel.Children.Add(Muted(GuiDataTexts.CondValueInfo));
                panel.Children.Add(SideEditor(path, 0, value.Expr, string.Empty));
                break;
            case RawCondNode raw:
                var box = new TextBox { Text = raw.Text, FontFamily = new FontFamily("Consolas"), AcceptsReturn = false, MinWidth = 200 };
                var apply = new Button { Content = GuiDataTexts.CondApply, ToolTip = GuiDataTexts.CondApplyTip, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
                apply.Click += (_, _) => SetRaw(path, box.Text);
                box.KeyDown += (_, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        SetRaw(path, box.Text);
                    }
                };
                var version = _detailVersion;
                box.LostKeyboardFocus += (_, _) =>
                {
                    if (version == _detailVersion && !_refreshing && box.Text != raw.Text)
                    {
                        SetRaw(path, box.Text);
                    }
                };
                panel.Children.Add(new TextBlock { Text = GuiDataTexts.LabelRaw, Margin = new Thickness(0, 0, 0, 2) });
                panel.Children.Add(box);
                panel.Children.Add(apply);
                break;
        }

        _detail.Content = panel;
    }

    private void BuildCompare(StackPanel panel, CondPath path, CompareNode compare)
    {
        var kinds = new ComboBox { MinWidth = 200, Margin = new Thickness(0, 0, 0, 6) };
        foreach (var kind in Enum.GetValues<CompareKind>())
        {
            kinds.Items.Add(new ComboBoxItem { Content = ConditionPhrases.KindText(kind), Tag = kind, IsSelected = kind == compare.Kind });
        }

        kinds.SelectionChanged += (_, _) =>
        {
            if (kinds.SelectedItem is ComboBoxItem { Tag: CompareKind kind } && kind != compare.Kind)
            {
                SetKind(path, kind);
            }
        };
        panel.Children.Add(new TextBlock { Text = GuiDataTexts.CondKind });
        panel.Children.Add(kinds);

        var variants = new ComboBox { MinWidth = 200, Margin = new Thickness(0, 0, 0, 6), ToolTip = compare.Variant is null ? GuiDataTexts.CondChooseVariant : null };
        foreach (var variant in _catalog.Variants(compare.Kind))
        {
            variants.Items.Add(variant);
        }

        variants.SelectedItem = compare.Variant;
        variants.SelectionChanged += (_, _) =>
        {
            if (variants.SelectedItem is string variant && variant != compare.Variant)
            {
                SetVariant(path, variant);
            }
        };
        panel.Children.Add(new TextBlock { Text = GuiDataTexts.CondVariant });
        panel.Children.Add(variants);

        panel.Children.Add(SideEditor(path, 0, compare.Left, GuiDataTexts.CondLeft));
        if (compare.Right is { } right)
        {
            panel.Children.Add(SideEditor(path, 1, right, GuiDataTexts.CondRight));
        }

        if (compare.Right2 is { } right2)
        {
            panel.Children.Add(SideEditor(path, 2, right2, GuiDataTexts.CondRight2));
        }
    }

    private void BuildCheck(StackPanel panel, CondPath path, CheckNode check)
    {
        var search = new TextBox { MinWidth = 200, Margin = new Thickness(0, 0, 0, 4) };
        var list = new ListBox { Height = 110, Margin = new Thickness(0, 0, 0, 6) };
        void Fill()
        {
            var filter = search.Text.Trim();
            list.Items.Clear();
            foreach (var entry in _checks.Where(c => filter.Length == 0 || c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(300))
            {
                list.Items.Add(new ListBoxItem
                {
                    Content = $"{entry.Name}( {string.Join(", ", entry.Arguments)} )",
                    Tag = entry.Name,
                    ToolTip = entry.Description.Length == 0 ? null : entry.Description,
                    IsSelected = entry.Name == check.Name,
                });
            }
        }

        Fill();
        search.TextChanged += (_, _) => Fill();
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is ListBoxItem { Tag: string name } && name != check.Name)
            {
                SetCheck(path, name);
            }
        };
        list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && list.SelectedItem is ListBoxItem { Tag: string name })
            {
                e.Handled = true;
                SetCheck(path, name);
            }
        };
        panel.Children.Add(new TextBlock { Text = check.Name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(new TextBlock { Text = GuiDataTexts.CondCheckSearch });
        panel.Children.Add(search);
        panel.Children.Add(list);
        for (var i = 0; i < check.Args.Count; i++)
        {
            panel.Children.Add(SideEditor(path, i, check.Args[i], GuiDataTexts.CondArgument(i + 1)));
        }
    }

    /// <summary>Eine Seite bzw. ein Argument: Kette (Textfeld, Typ, "Kette bauen") oder Konstante (Wert und Typ).</summary>
    private FrameworkElement SideEditor(CondPath path, int side, ExprNode expr, string label)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
        if (label.Length > 0)
        {
            panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
        }

        var isConstant = expr is LiteralNode;
        var modes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        var group = $"side{side}_{Guid.NewGuid():N}";
        var chainMode = new RadioButton { Content = GuiDataTexts.CondChainMode, GroupName = group, IsChecked = !isConstant, Margin = new Thickness(0, 0, 12, 0) };
        var constantMode = new RadioButton { Content = GuiDataTexts.CondConstantMode, GroupName = group, IsChecked = isConstant };
        modes.Children.Add(chainMode);
        modes.Children.Add(constantMode);
        panel.Children.Add(modes);

        var text = new TextBox
        {
            Text = expr is LiteralNode literal ? literal.Value : DataExpressionWriter.Write(expr, _loaded?.Original ?? string.Empty),
            FontFamily = new FontFamily("Consolas"),
            MinWidth = 200,
            ToolTip = GuiDataTexts.CondApplyTip,
        };
        panel.Children.Add(text);

        var chainRow = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
        var build = new Button { Content = GuiDataTexts.CondBuildChain, ToolTip = GuiDataTexts.CondBuildChainTip, Padding = new Thickness(8, 1, 8, 1) };
        build.Click += (_, _) => ChainRequested?.Invoke(text.Text, result => SetSide(path, side, result));
        DockPanel.SetDock(build, Dock.Right);
        chainRow.Children.Add(build);
        var type = ExpressionTypes.TypeOf(expr, _db, _start);
        chainRow.Children.Add(Muted(type is null ? GuiDataTexts.CondTypeUnknown : GuiDataTexts.CondType(type)));

        var types = new ComboBox { MinWidth = 140, Margin = new Thickness(0, 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        types.Items.Add(GuiDataTexts.CondTextType);
        foreach (var variant in Enum.GetValues<CompareKind>().SelectMany(_catalog.Variants).Distinct().Where(v => v != "string"))
        {
            types.Items.Add(variant);
        }

        types.SelectedItem = expr is LiteralNode { Type: { } literalType } ? literalType : GuiDataTexts.CondTextType;

        void ShowMode()
        {
            var constant = constantMode.IsChecked == true;
            chainRow.Visibility = constant ? Visibility.Collapsed : Visibility.Visible;
            types.Visibility = constant ? Visibility.Visible : Visibility.Collapsed;
        }

        void Apply()
        {
            if (constantMode.IsChecked == true)
            {
                var chosen = types.SelectedItem as string;
                SetConstant(path, side, text.Text, chosen is null || chosen == GuiDataTexts.CondTextType ? null : chosen);
            }
            else
            {
                SetSide(path, side, text.Text);
            }
        }

        // Nach jeder Übernahme baut Refresh den Editor neu; ein altes Feld, das danach noch den Fokus verliert, darf nicht ein zweites Mal übernehmen.
        var version = _detailVersion;
        bool Live() => version == _detailVersion && !_refreshing;

        void ModeChanged()
        {
            ShowMode();
            if (Live() && text.Text.Trim().Length > 0 && (constantMode.IsChecked == true) != isConstant)
            {
                Apply();
            }
        }

        chainMode.Checked += (_, _) => ModeChanged();
        constantMode.Checked += (_, _) => ModeChanged();
        text.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Apply();
            }
        };
        text.LostKeyboardFocus += (_, _) =>
        {
            if (Live() && text.Text != (expr is LiteralNode l ? l.Value : DataExpressionWriter.Write(expr, _loaded?.Original ?? string.Empty)))
            {
                Apply();
            }
        };
        types.SelectionChanged += (_, _) =>
        {
            if (Live() && constantMode.IsChecked == true && text.Text.Length > 0)
            {
                Apply();
            }
        };
        panel.Children.Add(chainRow);
        panel.Children.Add(types);
        ShowMode();
        return panel;
    }

    private Button ToolButton(string key, string text, string? tip, Action action)
    {
        var button = new Button { Content = text, ToolTip = tip, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 4, 4) };
        button.Click += (_, _) =>
        {
            if (key == "add" || Selected is not null || key is "undo" or "redo")
            {
                action();
            }
        };
        _buttons[key] = button;
        return button;
    }

    private MenuItem MenuEntry(string text, Func<CondNode> create)
    {
        var item = new MenuItem { Header = text };
        item.Click += (_, _) => Selected = Editor.Add(Selected, create());
        return item;
    }

    private static TextBlock Muted(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return block;
    }
}
