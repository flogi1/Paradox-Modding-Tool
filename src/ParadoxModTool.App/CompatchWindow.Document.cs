using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;
using MergeAction = ParadoxModTool.Compatch.MergeAction;

namespace ParadoxModTool.App;

/// <summary>Ansicht B: ein Ergebnisdokument je Datei im Ordnerbaum, ◀ ▶ durch alle gefilterten Aktionen, Quellwerte unten (Spec, "Ansicht B", Nachtrag 2).</summary>
public sealed partial class CompatchWindow
{
    private readonly Controls.TreeView _docTree = new();
    private readonly Dictionary<string, Controls.TreeViewItem> _docFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Controls.ListBox _docLines = new() { BorderThickness = new Thickness(0) };
    private readonly Controls.Button _docPrevious = new() { Content = CompatchTexts.DocPrevious, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.Button _docNext = new() { Content = CompatchTexts.DocNext, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 12, 0) };
    private readonly Controls.TextBlock _docPosition = new() { VerticalAlignment = System.Windows.VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Controls.TextBlock _docSourceHeader = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
    private readonly Controls.StackPanel _docSourceRows = new();
    private readonly Controls.WrapPanel _docButtons = new() { Margin = new Thickness(0, 4, 0, 0) };
    private readonly Controls.Button _docEqual = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0), ToolTip = CompatchTexts.ApplyToEqualTip, Visibility = Visibility.Collapsed };
    private readonly Controls.CheckBox _docSeen = new() { Content = new Controls.TextBlock { Text = CompatchTexts.Seen }, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private IReadOnlyList<MergeAction> _docOrder = Array.Empty<MergeAction>();
    private IReadOnlyList<DocLine> _docLineData = Array.Empty<DocLine>();
    private string? _docActionId;
    private string? _docScope;
    private bool _docSyncing;

    internal MergeAction? ActionById(string id) => _session?.Actions.FirstOrDefault(a => a.Id == id);

    private MergeAction? DocAction => _docActionId is null ? null : ActionById(_docActionId);

    partial void InitializeDocument()
    {
        var root = new Controls.DockPanel();

        var nav = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        Controls.DockPanel.SetDock(nav, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_docPrevious, Controls.Dock.Left);
        Controls.DockPanel.SetDock(_docNext, Controls.Dock.Left);
        nav.Children.Add(_docPrevious);
        nav.Children.Add(_docNext);
        var legend = CompatchLegend.Build(_state.ModAName, _state.ModBName);
        legend.Margin = new Thickness(12, 0, 0, 0);
        Controls.DockPanel.SetDock(legend, Controls.Dock.Right);
        nav.Children.Add(legend);
        nav.Children.Add(_docPosition);
        root.Children.Add(nav);

        var sources = new Controls.Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 6, 0, 0), Margin = new Thickness(0, 6, 0, 0) };
        sources.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.Separator");
        Controls.DockPanel.SetDock(sources, Controls.Dock.Bottom);
        var sourcePanel = new Controls.StackPanel();
        sourcePanel.Children.Add(new Controls.TextBlock { Text = CompatchTexts.DocSources, FontWeight = FontWeights.Bold });
        sourcePanel.Children.Add(_docSourceHeader);
        sourcePanel.Children.Add(new Controls.ScrollViewer { Content = _docSourceRows, MaxHeight = 220, HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });
        sourcePanel.Children.Add(_docButtons);
        sources.Child = sourcePanel;
        root.Children.Add(sources);

        var grid = new Controls.Grid();
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 180 });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(6) });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        var listPanel = new Controls.DockPanel();
        var listTitle = new Controls.TextBlock { Text = CompatchTexts.DocDocuments, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) };
        Controls.DockPanel.SetDock(listTitle, Controls.Dock.Top);
        listPanel.Children.Add(listTitle);
        Controls.DockPanel.SetDock(_searchSlotDocument, Controls.Dock.Top);
        listPanel.Children.Add(_searchSlotDocument);
        listPanel.Children.Add(_docTree);
        grid.Children.Add(listPanel);
        var splitter = new Controls.GridSplitter { Width = 6, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        Controls.Grid.SetColumn(splitter, 1);
        grid.Children.Add(splitter);
        _docLines.ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(CompatchDocLineView)) };
        var itemStyle = new Style(typeof(Controls.ListBoxItem));
        itemStyle.Setters.Add(new Setter(Controls.Control.HorizontalContentAlignmentProperty, System.Windows.HorizontalAlignment.Stretch));
        itemStyle.Setters.Add(new Setter(Controls.Control.PaddingProperty, new Thickness(0)));
        _docLines.ItemContainerStyle = itemStyle;
        Controls.VirtualizingPanel.SetIsVirtualizing(_docLines, true);
        Controls.VirtualizingPanel.SetVirtualizationMode(_docLines, Controls.VirtualizationMode.Recycling);
        Controls.Grid.SetColumn(_docLines, 2);
        grid.Children.Add(_docLines);
        root.Children.Add(grid);
        _documentHost.Child = root;

        _docButtons.Children.Add(DocButton(CompatchTexts.UndoLabel, (s, a) => s.Undo(a.Id)));
        _docButtons.Children.Add(DocButton(CompatchTexts.OtherSide, (s, a) => s.TakeOtherSide(a.Id)));
        _docButtons.Children.Add(DocButton(CompatchTexts.Confirm, (s, a) => s.Confirm(a.Id)));
        var edit = new Controls.Button { Content = CompatchTexts.Edit, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        edit.Click += (_, _) =>
        {
            if (DocAction is { } action)
            {
                OpenInList(action.Id, edit: true);
            }
        };
        _docButtons.Children.Add(edit);
        _docEqual.Click += (_, _) =>
        {
            if (DocAction is { } action)
            {
                ChangeAction(action, (s, a) => s.ApplyToEqual(a.Id));
            }
        };
        _docButtons.Children.Add(_docEqual);
        _docButtons.Children.Add(_docSeen);
        _docSeen.Click += (_, _) =>
        {
            if (DocAction is { } action)
            {
                SetActionSeen(action, _docSeen.IsChecked == true);
            }
        };

        _docPrevious.Click += (_, _) => ShowDocAction(ActionOrder.Step(_docOrder, _docActionId, -1)?.Id ?? _docActionId, rebuildLines: false, selectLine: true);
        _docNext.Click += (_, _) => ShowDocAction(ActionOrder.Step(_docOrder, _docActionId, +1)?.Id ?? _docActionId, rebuildLines: false, selectLine: true);
        _docTree.SelectedItemChanged += (_, _) =>
        {
            // Nur Datei-Knoten tragen ihren Pfad als Tag; Ordner werden nur auf-/zugeklappt.
            if (!_docSyncing && _docTree.SelectedItem is Controls.TreeViewItem { Tag: string file })
            {
                var first = _docOrder.FirstOrDefault(a => string.Equals(TreePath.FileOf(a), file, StringComparison.OrdinalIgnoreCase));
                if (first is not null)
                {
                    ShowDocAction(first.Id, rebuildLines: false, selectLine: true);
                }
            }
        };
        _docLines.MouseDoubleClick += (_, _) =>
        {
            if (_docLines.SelectedItem is CompatchDocLineItem item && ActionById(item.Line.ActionId) is { } action)
            {
                OpenCompare(action);
            }
        };
        _docLines.SelectionChanged += (_, _) =>
        {
            if (!_docSyncing && _docLines.SelectedItem is CompatchDocLineItem item)
            {
                // Ruling 9: die Aktion der Zeile wählen, ohne die Zeilenauswahl zu versetzen.
                ShowDocAction(item.Line.ActionId, rebuildLines: false, selectLine: false);
            }
        };
    }

    /// <summary>Ordner-/Dateibaum der Dokumente (Spec Nachtrag 2). Datei-Knoten tragen ihren Pfad als Tag.</summary>
    private void FillDocTree()
    {
        _docTree.Items.Clear();
        _docFiles.Clear();
        var session = _session!;
        Controls.TreeViewItem Build(ActionTreeNode node)
        {
            var item = new Controls.TreeViewItem { Header = CompatchTreeBuilder.Header(node), IsExpanded = true, Tag = node.Kind == TreeNodeKind.File ? node.Path : null };
            if (node.Kind == TreeNodeKind.File)
            {
                _docFiles[node.Path] = item;
                return item;
            }

            foreach (var child in node.Children.Where(c => c.Kind != TreeNodeKind.Action))
            {
                item.Items.Add(Build(child));
            }

            return item;
        }

        foreach (var node in ActionTree.Build(_docOrder, a => IsOpenConflict(session, a), _ => false))
        {
            _docTree.Items.Add(Build(node));
        }
    }

    private Controls.Button DocButton(string text, Action<CompatchSession, MergeAction> change)
    {
        var button = new Controls.Button { Content = text, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        button.Click += (_, _) =>
        {
            if (DocAction is { } action)
            {
                ChangeAction(action, change);
            }
        };
        return button;
    }

    /// <summary>Ruling 4 und 8: Reihenfolge und Dokumentliste nur beim Neuaufbau; das aktuelle Dokument wird nach jeder Änderung neu gebaut.</summary>
    partial void RefreshDocument(bool rebuild)
    {
        if (_session is null)
        {
            _docOrder = Array.Empty<MergeAction>();
            _docTree.Items.Clear();
            _docFiles.Clear();
            _docLines.ItemsSource = null;
            _docActionId = null;
            _docScope = null;
            FillDocSources(null);
            UpdateDocNavigation(null);
            return;
        }

        if (rebuild || _docTree.Items.Count == 0)
        {
            _docOrder = FilteredActions();
            FillDocTree();
            if (_docActionId is null || ActionOrder.IndexOf(_docOrder, _docActionId) < 0)
            {
                _docActionId = _docOrder.FirstOrDefault()?.Id;
            }
        }

        ShowDocAction(_docActionId, rebuildLines: true, selectLine: true);
    }

    /// <summary>Zeigt eine Aktion: passendes Dokument, Zeile markieren (optional), Position, Quellwerte.</summary>
    private void ShowDocAction(string? id, bool rebuildLines, bool selectLine)
    {
        if (_session is null)
        {
            return;
        }

        _docActionId = id;
        var action = id is null ? null : ActionById(id);
        var scope = action is null ? _docScope : TreePath.FileOf(action);
        _docSyncing = true;
        try
        {
            if (rebuildLines || !string.Equals(scope, _docScope, StringComparison.OrdinalIgnoreCase))
            {
                _docScope = scope;
                _docLineData = scope is null ? Array.Empty<DocLine>() : ResultDocument.Build(_session.Run, scope, HideComments);
                _docLines.ItemsSource = _docLineData.Select((l, i) => new CompatchDocLineItem(i, l, this)).ToList();
                selectLine = true;
            }

            if (_docScope is not null && _docFiles.TryGetValue(_docScope, out var fileItem))
            {
                fileItem.IsSelected = true;
                fileItem.BringIntoView();
            }
            if (selectLine && action is not null && ResultDocument.LineOf(_docLineData, action) is var index and >= 0)
            {
                _docLines.SelectedIndex = index;
                _docLines.ScrollIntoView(_docLines.Items[index]);
            }
        }
        finally
        {
            _docSyncing = false;
        }

        FillDocSources(action);
        UpdateDocNavigation(action);
    }

    private void UpdateDocNavigation(MergeAction? action)
    {
        _docPrevious.IsEnabled = ActionOrder.Step(_docOrder, _docActionId, -1) is not null;
        _docNext.IsEnabled = ActionOrder.Step(_docOrder, _docActionId, +1) is not null;
        _docPosition.Text = action is null
            ? CompatchTexts.NoActions
            : CompatchTexts.DocPosition(ActionOrder.IndexOf(_docOrder, action.Id) + 1, _docOrder.Count, action.DisplayPath);
    }

    /// <summary>Unterer Bereich (Ruling 9): Kopf der Aktion und Spalten A | Ergebnis | B.</summary>
    private void FillDocSources(MergeAction? action)
    {
        _docSourceRows.Children.Clear();
        _docButtons.IsEnabled = action is not null;
        if (action is null || _session is null || !_session.Run.Resolved.Addresses.TryGetValue(action.Address, out var info))
        {
            _docSourceHeader.Text = string.Empty;
            _docSeen.IsChecked = false;
            _docEqual.Visibility = Visibility.Collapsed;
            return;
        }

        var equal = _session.EqualPending(action.Id).Count;
        _docEqual.Content = CompatchTexts.ApplyToEqual(equal);
        _docEqual.Visibility = equal > 0 ? Visibility.Visible : Visibility.Collapsed;

        var decision = _session.EffectiveDecision(action);
        _docSourceHeader.Text = $"{action.DisplayPath}   [{TreePath.FileOf(action)}]\n" + Summary(_session, action, _state.ModAName, _state.ModBName);
        _docSeen.IsChecked = _session.IsSeen(action.Id);
        var open = IsOpenConflict(_session, action);
        var rows = action.Address.Kind == AddressKind.WholeFile ? ActionText.WholeFileRows(info, decision, withVanilla: false) : ActionText.Rows(action, decision, withVanilla: false, HideComments);
        foreach (var row in rows.Take(MaxRows))
        {
            _docSourceRows.Children.Add(RowGrid(row, showVanilla: false, open));
        }
    }
}
