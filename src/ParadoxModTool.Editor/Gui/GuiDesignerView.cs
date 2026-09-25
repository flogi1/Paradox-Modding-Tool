using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Designer-Ansicht: schematische Vorschau links, Elementbaum und Eigenschaften rechts. Kennt keinen Editor und schreibt nie selbst:
/// Bearbeiten meldet sie als <see cref="GuiDesignerAction"/>, <see cref="GuiDesignerController"/> führt aus.
/// </summary>
public sealed class GuiDesignerView : UserControl
{
    /// <summary>Datenformat beim Ziehen eines Baumeintrags (Wert: <see cref="GuiTreeItem"/>).</summary>
    internal const string TreeDragFormat = "ParadoxModTool.GuiTreeItem";

    /// <summary>Datenformat beim Ziehen aus der Palette (Wert: <see cref="GuiPaletteEntry"/>).</summary>
    internal const string PaletteDragFormat = "ParadoxModTool.GuiPaletteEntry";

    private readonly GuiSchematicCanvas _canvas = new();
    private readonly TreeView _tree = new() { AllowDrop = true };
    private readonly ListView _properties = new();
    private readonly TextBlock _chain = new() { Margin = new Thickness(4, 2, 4, 2), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _message = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(12),
        IsHitTestVisible = false,
    };
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Border _banner = new() { Padding = new Thickness(6, 3, 6, 3), Visibility = Visibility.Collapsed };
    private readonly TextBlock _bannerText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Border _readOnlyBanner = new() { Padding = new Thickness(6, 3, 6, 3), Visibility = Visibility.Collapsed };
    private readonly TextBlock _readOnlyText = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _draftButton = new() { Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0), Visibility = Visibility.Collapsed };
    private readonly CheckBox _showConditional = new()
    {
        Content = GuiTexts.DesignerShowConditional,
        IsChecked = true,
        Margin = new Thickness(4, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        ToolTip = GuiTexts.DesignerShowConditionalTip,
    };
    private readonly CheckBox _showHidden = new()
    {
        Content = GuiTexts.DesignerShowHidden,
        Margin = new Thickness(0, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        ToolTip = GuiTexts.DesignerShowHiddenTip,
    };
    private readonly ComboBox _listCount = new()
    {
        Width = 50,
        Margin = new Thickness(0, 2, 8, 2),
        ToolTip = GuiTexts.DesignerListCountTip,
    };
    private readonly ColumnDefinition _treeColumn = new() { Width = new GridLength(320) };
    private readonly RowDefinition _propertiesRow = new() { Height = new GridLength(320) };
    private readonly Dictionary<GuiNode, TreeViewItem> _treeItems = new();
    private readonly ContextMenu _treeMenu = new();
    private readonly GuiPropertyEditor _propertyEditor = new();
    private readonly GuiPaletteView _palette = new();
    private readonly ColumnDefinition _paletteColumn = new() { Width = new GridLength(220) };
    private ResolvedElement? _shownElement;
    private string? _selectedPropertyName;
    private ResolvedElement? _clickedInner;
    private bool _suppressTreeEvents;
    private bool _stale;
    private string? _readOnlyReason;
    private GuiNode? _selectedNode;
    private Point? _dragStart;
    private GuiTreeItem? _dragItem;

    public GuiDesignerView()
    {
        for (var i = 1; i <= 10; i++)
        {
            _listCount.Items.Add(i);
        }

        _listCount.SelectedItem = 3;
        _showConditional.Click += (_, _) => PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
        _showHidden.Click += (_, _) => PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
        _listCount.SelectionChanged += (_, _) => PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);

        var fit = new Button
        {
            Content = GuiTexts.DesignerFit,
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 2, 4, 2),
            ToolTip = GuiTexts.DesignerFitTip,
        };
        fit.Click += (_, _) =>
        {
            _canvas.FitToView();
            _canvas.InvalidateVisual();
        };
        var undo = new Button { Content = GuiTexts.DesignerUndo, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 2, 4, 2), ToolTip = GuiTexts.DesignerUndoTip };
        undo.Click += (_, _) => UndoRequested?.Invoke(this, false);
        var redo = new Button { Content = GuiTexts.DesignerRedo, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 2, 8, 2), ToolTip = GuiTexts.DesignerRedoTip };
        redo.Click += (_, _) => UndoRequested?.Invoke(this, true);

        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
        toolbar.Children.Add(undo);
        toolbar.Children.Add(redo);
        toolbar.Children.Add(_showConditional);
        toolbar.Children.Add(_showHidden);
        toolbar.Children.Add(new TextBlock { Text = GuiTexts.DesignerListCount, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        toolbar.Children.Add(_listCount);
        toolbar.Children.Add(fit);
        var viewInGame = new Button { Content = GuiTexts.ViewInGame, Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 2, 4, 2), ToolTip = GuiTexts.ViewInGameTip };
        viewInGame.Click += (_, _) => ViewInGameRequested?.Invoke(this, EventArgs.Empty);
        toolbar.Children.Add(viewInGame);
        toolbar.Children.Add(_status);
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _message.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _banner.Child = _bannerText;
        _banner.SetResourceReference(Border.BackgroundProperty, "Theme.NoticeBackground");
        _draftButton.Content = GuiTexts.DraftEdit;
        _draftButton.ToolTip = GuiTexts.DraftEditTip;
        _draftButton.Click += (_, _) => DraftRequested?.Invoke(this, EventArgs.Empty);
        var readOnlyRow = new DockPanel();
        DockPanel.SetDock(_draftButton, Dock.Right);
        readOnlyRow.Children.Add(_draftButton);
        readOnlyRow.Children.Add(_readOnlyText);
        _readOnlyBanner.Child = readOnlyRow;
        _readOnlyBanner.SetResourceReference(Border.BackgroundProperty, "Theme.NoticeBackground");

        var previewArea = new Grid();
        previewArea.Children.Add(_canvas);
        previewArea.Children.Add(_message);

        _properties.View = CreatePropertyColumns();
        _properties.ItemContainerStyle = PropertyRowStyle();
        _properties.GroupStyle.Add(new GroupStyle { HeaderTemplate = GroupHeaderTemplate() });
        _properties.ToolTip = GuiTexts.PropertiesTip;
        _properties.MouseDoubleClick += (_, _) =>
        {
            if (_properties.SelectedItem is GuiPropertyRow row)
            {
                OriginActivated?.Invoke(this, row.Origin);
            }
        };

        _properties.SelectionChanged += (_, _) =>
        {
            if (_properties.SelectedItem is GuiPropertyRow row)
            {
                _selectedPropertyName = row.Name;
                _propertyEditor.SetProperty(row.Source);
            }
        };
        _propertyEditor.ActionRequested += (_, action) => Request(action);
        _propertyEditor.ShowInCodeRequested += (_, node) => ShowInCodeRequested?.Invoke(this, node);
        _propertyEditor.StatusChanged += (_, text) => SetStatus(text);
        _propertyEditor.NewTypeRequested += (_, node) => RequestFor(NewTypeRequested, node);
        _propertyEditor.TypeEverywhereRequested += (_, node) => TypeEverywhereRequested?.Invoke(this, node);

        var propertiesPanel = new DockPanel();
        DockPanel.SetDock(_chain, Dock.Top);
        propertiesPanel.Children.Add(_chain);
        DockPanel.SetDock(_propertyEditor, Dock.Bottom);
        propertiesPanel.Children.Add(_propertyEditor);
        propertiesPanel.Children.Add(_properties);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(_propertiesRow);
        Grid.SetRow(_tree, 0);
        right.Children.Add(_tree);
        var rowSplitter = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows };
        rowSplitter.SetResourceReference(BackgroundProperty, "Theme.Separator");
        rowSplitter.DragCompleted += (_, _) => LayoutChanged?.Invoke(this, EventArgs.Empty);
        Grid.SetRow(rowSplitter, 1);
        right.Children.Add(rowSplitter);
        Grid.SetRow(propertiesPanel, 2);
        right.Children.Add(propertiesPanel);

        GridSplitter ColumnSplitter(int column)
        {
            var splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns };
            splitter.SetResourceReference(BackgroundProperty, "Theme.Separator");
            splitter.DragCompleted += (_, _) => LayoutChanged?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(splitter, column);
            return splitter;
        }

        // Palette | Vorschau | Baum und Eigenschaften
        var main = new Grid();
        main.ColumnDefinitions.Add(_paletteColumn);
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        main.ColumnDefinitions.Add(_treeColumn);
        Grid.SetColumn(_palette, 0);
        main.Children.Add(_palette);
        main.Children.Add(ColumnSplitter(1));
        Grid.SetColumn(previewArea, 2);
        main.Children.Add(previewArea);
        main.Children.Add(ColumnSplitter(3));
        Grid.SetColumn(right, 4);
        main.Children.Add(right);

        _palette.EntryActivated += (_, entry) => Request(new GuiDesignerAction(GuiDesignerActionKind.Insert, null)
        {
            Entry = entry,
            Target = _selectedNode,
            Placement = GuiPlacement.AtSelection,
            FromSelection = true,
        });
        _palette.InspectRequested += (_, definition) => InspectDefinitionRequested?.Invoke(this, definition);

        var root = new DockPanel();
        DockPanel.SetDock(_readOnlyBanner, Dock.Top);
        root.Children.Add(_readOnlyBanner);
        DockPanel.SetDock(_banner, Dock.Top);
        root.Children.Add(_banner);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(main);
        Content = root;

        _tree.SelectedItemChanged += (_, e) =>
        {
            // Nur eine Auswahl durch den Nutzer verwirft den Klick in der Vorschau; Select wählt den Eintrag selbst (unterdrückt) aus.
            if (!_suppressTreeEvents && e.NewValue is TreeViewItem { Tag: GuiTreeItem item })
            {
                _clickedInner = null;
                NodeActivated?.Invoke(this, item.Node);
            }
        };
        // BoxClicked kommt vor NodeClicked: das angeklickte Element selbst merken, auch wenn es aus dem Inneren eines Typs stammt (Plan-Ruling 14).
        _canvas.BoxClicked += (_, box) => _clickedInner = box.Element;
        _canvas.NodeClicked += (_, node) => NodeActivated?.Invoke(this, node);

        // Rechtsklick im Baum: Menü für den Eintrag unter der Maus (TreeView wählt beim Rechtsklick nicht selbst aus).
        _tree.ContextMenu = _treeMenu;
        _tree.ContextMenuOpening += (_, e) =>
        {
            _treeMenu.Items.Clear();
            if (FindTreeItem(e.OriginalSource as DependencyObject)?.Tag is GuiTreeItem item)
            {
                BuildTreeMenu(item);
            }

            if (_treeMenu.Items.Count == 0)
            {
                e.Handled = true;
            }
        };

        // Tasten im Baum: Entf, Strg+D, Alt+Pfeil hoch/runter (Alt-Kombinationen kommen als Key.System).
        _tree.PreviewKeyDown += (_, e) =>
        {
            if (_tree.SelectedItem is not TreeViewItem { Tag: GuiTreeItem item } || item.Kind == GuiTreeItemKind.OfferedBlock)
            {
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            GuiDesignerActionKind? kind = (key, Keyboard.Modifiers) switch
            {
                (Key.Delete, ModifierKeys.None) => GuiDesignerActionKind.Delete,
                (Key.D, ModifierKeys.Control) => GuiDesignerActionKind.Duplicate,
                (Key.Up, ModifierKeys.Alt) => GuiDesignerActionKind.MoveUp,
                (Key.Down, ModifierKeys.Alt) => GuiDesignerActionKind.MoveDown,
                _ => null,
            };
            if (kind is { } action)
            {
                Request(new GuiDesignerAction(action, item.Node) { FromSelection = true });
                e.Handled = true;
            }
        };

        // Rückgängig im ganzen Designer, außer in Eingabefeldern (die haben ihr eigenes Rückgängig; Plan-Ruling 9).
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.FocusedElement is TextBoxBase)
            {
                return;
            }

            var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if (control && e.Key == Key.Z)
            {
                UndoRequested?.Invoke(this, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                e.Handled = true;
            }
            else if (control && e.Key == Key.Y)
            {
                UndoRequested?.Invoke(this, true);
                e.Handled = true;
            }
        };

        // Ziehen und Ablegen: Baumeinträge verschieben, Palette-Einträge einfügen (Plan-Ruling 8).
        _tree.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _dragStart = e.GetPosition(_tree);
            _dragItem = FindTreeItem(e.OriginalSource as DependencyObject)?.Tag as GuiTreeItem;
        };
        _tree.PreviewMouseLeftButtonUp += (_, _) => _dragItem = null;
        _tree.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragStart is not { } start || _dragItem is not { } item
                || item.Kind == GuiTreeItemKind.OfferedBlock || !IsEditable)
            {
                return;
            }

            var delta = e.GetPosition(_tree) - start;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _dragStart = null;
            _dragItem = null;
            DragDrop.DoDragDrop(_tree, new DataObject(TreeDragFormat, item), DragDropEffects.Move);
        };
        _tree.DragOver += (_, e) =>
        {
            e.Effects = !IsEditable ? DragDropEffects.None
                : e.Data.GetDataPresent(TreeDragFormat) ? DragDropEffects.Move
                : e.Data.GetDataPresent(PaletteDragFormat) ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        };
        _tree.Drop += (_, e) =>
        {
            var target = FindTreeItem(e.OriginalSource as DependencyObject);
            var y = target?.Header is FrameworkElement header && header.ActualHeight > 0 ? e.GetPosition(header).Y / header.ActualHeight : 0.5;
            var targetNode = (target?.Tag as GuiTreeItem)?.Node;
            if (e.Data.GetData(TreeDragFormat) is GuiTreeItem dragged && targetNode is not null)
            {
                Request(new GuiDesignerAction(GuiDesignerActionKind.Move, dragged.Node) { Placement = GuiPlacement.Drop, Target = targetNode, DropY = y });
            }
            else if (e.Data.GetData(PaletteDragFormat) is GuiPaletteEntry entry)
            {
                // Ohne Eintrag unter der Maus (z.B. leere Datei): ans Dateiende.
                Request(new GuiDesignerAction(GuiDesignerActionKind.Insert, null)
                {
                    Placement = targetNode is null ? GuiPlacement.AtSelection : GuiPlacement.Drop,
                    Target = targetNode,
                    DropY = y,
                    Entry = entry,
                });
            }

            e.Handled = true;
        };
    }

    public event EventHandler<GuiNode>? NodeActivated;

    /// <summary>"Inspizieren" im Kontextmenü des Elementbaums.</summary>
    public event EventHandler<GuiNode>? InspectRequested;

    /// <summary>Bearbeiten (Baum, Palette, Eigenschaften); der Controller führt aus.</summary>
    public event EventHandler<GuiDesignerAction>? ActionRequested;

    /// <summary>"Im Code zeigen": Stelle im Code-Editor markieren (bei ausgeblendetem Code auf "Geteilt" umschalten).</summary>
    public event EventHandler<GuiNode>? ShowInCodeRequested;

    /// <summary>"Definition öffnen" für eine Typ-Instanz.</summary>
    public event EventHandler<GuiNode>? OpenDefinitionRequested;

    /// <summary>Rückgängig (false) bzw. Wiederholen (true).</summary>
    public event EventHandler<bool>? UndoRequested;

    /// <summary>"Für Mod bearbeiten" im Banner einer Vanilla-Datei.</summary>
    public event EventHandler? DraftRequested;

    /// <summary>"Im Spiel ansehen" (Plan 4): das Hauptfenster speichert und startet das Spiel.</summary>
    public event EventHandler? ViewInGameRequested;

    /// <summary>"Neuer Typ" (Eigenschaftenfeld oder Baum, Plan 3c Ruling 1).</summary>
    public event EventHandler<GuiNode>? NewTypeRequested;

    /// <summary>"Als neuen Typ herausziehen" im Baum.</summary>
    public event EventHandler<GuiNode>? ExtractTypeRequested;

    /// <summary>"Widget-Schlüssel umstellen" im Baum.</summary>
    public event EventHandler<GuiNode>? RetypeRequested;

    /// <summary>"Typ überall" (Eigenschaftenfeld oder Baum).</summary>
    public event EventHandler<GuiNode>? TypeEverywhereRequested;

    /// <summary>"Inspizieren" im Kontextmenü der Palette (Typ oder Template).</summary>
    public event EventHandler<GuiDefinition>? InspectDefinitionRequested;

    public event EventHandler<GuiOrigin>? OriginActivated;

    public event EventHandler? PreviewOptionsChanged;

    public event EventHandler? LayoutChanged;

    /// <summary>Der Designer darf ändern: nicht schreibgeschützt (Plan-Ruling 2) und kein veralteter Stand.</summary>
    public bool IsEditable => _readOnlyReason is null && !_stale;

    /// <summary>Gewähltes Element (Baum, Vorschau oder Cursor); null ohne Auswahl.</summary>
    public GuiNode? SelectedNode => _selectedNode;

    public LayoutOptions PreviewOptions => new()
    {
        ShowConditional = _showConditional.IsChecked == true,
        ShowHidden = _showHidden.IsChecked == true,
        ListSampleCount = _listCount.SelectedItem is int count ? count : 3,
    };

    /// <summary>"Baumbreite;Eigenschaftenhöhe;Palettenbreite" in Pixeln (invariante Zahlen); die ältere Form ohne Palettenbreite bleibt lesbar.</summary>
    public string LayoutState
    {
        get
        {
            // Nach dem Ziehen kann der GridSplitter Nachbar-Definitionen auf Stern-Längen umstellen; dann gilt die gemessene Größe.
            var tree = _treeColumn.ActualWidth > 0 ? _treeColumn.ActualWidth : _treeColumn.Width.Value;
            var properties = _propertiesRow.ActualHeight > 0 ? _propertiesRow.ActualHeight : _propertiesRow.Height.Value;
            var palette = _paletteColumn.ActualWidth > 0 ? _paletteColumn.ActualWidth : _paletteColumn.Width.Value;
            return string.Create(CultureInfo.InvariantCulture, $"{tree:0};{properties:0};{palette:0}");
        }
        set
        {
            var parts = (value ?? string.Empty).Split(';');
            if (parts.Length >= 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var tree) && tree >= 120
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var properties) && properties >= 60)
            {
                _treeColumn.Width = new GridLength(tree);
                _propertiesRow.Height = new GridLength(properties);
            }

            if (parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var palette) && palette >= 120)
            {
                _paletteColumn.Width = new GridLength(palette);
            }
        }
    }

    public void SetStatus(string text) => _status.Text = text;

    public void SetStale(bool stale, string? problem)
    {
        _stale = stale;
        _banner.Visibility = stale ? Visibility.Visible : Visibility.Collapsed;
        _bannerText.Text = problem ?? GuiTexts.Stale;
        _canvas.Dimmed = stale;
        _tree.Opacity = stale ? 0.6 : 1;
        _tree.IsEnabled = !stale;
        _propertyEditor.SetEditable(IsEditable);
    }

    /// <summary>Schreibschutz mit Grund (Banner) bzw. null = der Designer darf die Datei ändern.</summary>
    public void SetReadOnly(string? reason, bool offerDraft = false)
    {
        _readOnlyReason = reason;
        _readOnlyText.Text = reason ?? string.Empty;
        _readOnlyBanner.Visibility = reason is null ? Visibility.Collapsed : Visibility.Visible;
        _draftButton.Visibility = reason is not null && offerDraft ? Visibility.Visible : Visibility.Collapsed;
        _propertyEditor.SetEditable(IsEditable);
    }

    /// <summary>
    /// Die Datei ist ein Vanilla-Entwurf: Der Geltungsbereich sagt bei Widgets auf oberster Ebene, dass die ganze Datei kopiert wird. Der Pfad lässt
    /// den Hinweis die Verwendungen im angezeigten Text live zählen.
    /// </summary>
    public void SetDraft(bool isDraft, string? filePath)
    {
        _propertyEditor.FilePath = filePath;
        _propertyEditor.IsDraft = isDraft;
    }

    public void SetTree(IReadOnlyList<GuiTreeItem> items)
    {
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        foreach (TreeViewItem item in _tree.Items)
        {
            CollectExpanded(item, string.Empty, expanded);
        }

        _suppressTreeEvents = true;
        try
        {
            _treeItems.Clear();
            _tree.Items.Clear();
            foreach (var item in items)
            {
                _tree.Items.Add(CreateItem(item, string.Empty, 0, expanded));
            }
        }
        finally
        {
            _suppressTreeEvents = false;
        }
    }

    public void SetPreview(GuiPreview? preview, bool keepView)
    {
        _canvas.SetScene(preview?.Scene, keepView);
        _message.Text = preview?.Scene is null
            ? preview?.Message ?? GuiTexts.PreviewNone
            : string.Empty;
    }

    /// <summary>Markiert das Element in Baum und Vorschau und zeigt seine Eigenschaften (aus der Vorschau, sofern das Element darin vorkommt).</summary>
    public void Select(GuiNode? widget, GuiPreview? preview)
    {
        _selectedNode = widget;
        _canvas.SetSelection(widget);
        _suppressTreeEvents = true;
        try
        {
            for (var node = widget; node is not null; node = node.Parent)
            {
                if (_treeItems.TryGetValue(node, out var item))
                {
                    for (var parent = item.Parent as TreeViewItem; parent is not null; parent = parent.Parent as TreeViewItem)
                    {
                        parent.IsExpanded = true;
                    }

                    item.IsSelected = true;
                    item.BringIntoView();
                    break;
                }
            }
        }
        finally
        {
            _suppressTreeEvents = false;
        }

        var element = widget is null ? null : preview?.ElementFor(widget);
        var inner = _clickedInner;
        _clickedInner = null;
        if (widget is not null && element is not null && inner is not null && !ReferenceEquals(inner, element) && GuiBlockOverrides.IsInherited(inner, widget))
        {
            ShowInherited(widget, inner);
        }
        else
        {
            ShowProperties(element);
        }
    }

    /// <summary>Meldet eine Aktion; ist die Datei schreibgeschützt oder veraltet, erklärt die Statuszeile warum (der Controller prüft ebenfalls).</summary>
    internal void Request(GuiDesignerAction action)
    {
        if (_readOnlyReason is { } reason)
        {
            SetStatus(reason);
            return;
        }

        if (_stale)
        {
            SetStatus(GuiTexts.EditBlockedByErrors);
            return;
        }

        ActionRequested?.Invoke(this, action);
    }

    /// <summary>Wie <see cref="Request"/>, für Aktionen, die erst einen Dialog zeigen (der Controller fragt nach Name bzw. Schlüssel).</summary>
    private void RequestFor(EventHandler<GuiNode>? handler, GuiNode node)
    {
        if (_readOnlyReason is { } reason)
        {
            SetStatus(reason);
            return;
        }

        if (_stale)
        {
            SetStatus(GuiTexts.EditBlockedByErrors);
            return;
        }

        handler?.Invoke(this, node);
    }

    private void ShowProperties(ResolvedElement? element)
    {
        _shownElement = element;
        var treeItem = _selectedNode is not null && _treeItems.TryGetValue(_selectedNode, out var shownItem) ? shownItem.Tag as GuiTreeItem : null;
        _propertyEditor.SetElement(element is null ? null : _selectedNode, element, IsEditable, treeItem?.Kind == GuiTreeItemKind.Widget, treeItem?.IsTypeInstance == true);
        if (element is null)
        {
            _chain.Text = GuiTexts.NoProperties;
            _properties.ItemsSource = null;
            return;
        }

        _chain.Text = GuiTexts.TypeChain(string.Join(" → ", element.TypeChain));
        var list = GuiPropertyRows.Build(element).ToList();
        var rows = new ListCollectionView(list);
        rows.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GuiPropertyRow.Group)));
        _properties.ItemsSource = rows;

        // Nach einer Änderung dieselbe Eigenschaft wieder wählen, damit man an ihr weiterarbeiten kann.
        if (_selectedPropertyName is { } name && list.FirstOrDefault(r => r.Name == name) is { } again)
        {
            _properties.SelectedItem = again;
        }
    }

    /// <summary>Eigenschaften eines Elements aus dem Inneren eines Typs: nur lesen, Anpassen über blockoverride (Plan-Ruling 14).</summary>
    private void ShowInherited(GuiNode instance, ResolvedElement inner)
    {
        _shownElement = null;
        _chain.Text = GuiTexts.TypeChain(string.Join(" → ", inner.TypeChain));
        var rows = new ListCollectionView(GuiPropertyRows.Build(inner).ToList());
        rows.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GuiPropertyRow.Group)));
        _properties.ItemsSource = rows;
        _propertyEditor.SetInherited(instance, inner, IsEditable);
    }

    /// <summary>Kontextmenü eines Baumeintrags: Anzeigen (immer) und Bearbeiten (nur, wenn der Designer ändern darf).</summary>
    private void BuildTreeMenu(GuiTreeItem item)
    {
        var node = item.Node;
        var editable = IsEditable;
        var isElement = item.Kind != GuiTreeItemKind.OfferedBlock;
        if (item.Kind is GuiTreeItemKind.Type or GuiTreeItemKind.Template or GuiTreeItemKind.Widget or GuiTreeItemKind.Holder)
        {
            AddMenuItem(GuiTexts.ContextInspect, () => InspectRequested?.Invoke(this, node));
        }

        if (isElement)
        {
            AddMenuItem(GuiTexts.TreeShowInCode, () => ShowInCodeRequested?.Invoke(this, node));
        }

        if (item.IsTypeInstance)
        {
            AddMenuItem(GuiTexts.TreeOpenDefinition, () => OpenDefinitionRequested?.Invoke(this, node));
            AddMenuItem(GuiTexts.ScopeTypeEverywhere, () => TypeEverywhereRequested?.Invoke(this, node));
        }

        // Bearbeiten
        _treeMenu.Items.Add(new Separator());
        if (isElement && _palette.SelectedEntry is { } entry)
        {
            GuiDesignerAction Insert(GuiPlacement placement) =>
                new(GuiDesignerActionKind.Insert, null) { Entry = entry, Target = node, Placement = placement };

            if (entry.Snippet is null)
            {
                AddMenuItem(GuiTexts.TreeAddUsing(entry.Name), () => Request(Insert(GuiPlacement.Child)), editable);
            }
            else
            {
                AddMenuItem(GuiTexts.TreeInsertChild(entry.Name), () => Request(Insert(GuiPlacement.Child)), editable);
                AddMenuItem(GuiTexts.TreeInsertBefore(entry.Name), () => Request(Insert(GuiPlacement.Before)), editable);
                AddMenuItem(GuiTexts.TreeInsertAfter(entry.Name), () => Request(Insert(GuiPlacement.After)), editable);
            }
        }
        if (item.Kind == GuiTreeItemKind.OfferedBlock && item.BlockName is { } block)
        {
            AddMenuItem(GuiTexts.TreeOverrideBlock, () => Request(new GuiDesignerAction(GuiDesignerActionKind.OverrideBlock, node) { Name = block, KeepContent = true }), editable);
            AddMenuItem(GuiTexts.TreeClearBlock, () => Request(new GuiDesignerAction(GuiDesignerActionKind.OverrideBlock, node) { Name = block }), editable);
        }

        if (isElement)
        {
            AddMenuItem(GuiTexts.TreeDuplicate, () => Request(new GuiDesignerAction(GuiDesignerActionKind.Duplicate, node)), editable, GuiTexts.KeyDuplicate);
            AddMenuItem(GuiTexts.TreeDelete, () => Request(new GuiDesignerAction(GuiDesignerActionKind.Delete, node)), editable, GuiTexts.KeyDelete);
            AddMenuItem(GuiTexts.TreeMoveUp, () => Request(new GuiDesignerAction(GuiDesignerActionKind.MoveUp, node)), editable, GuiTexts.KeyMoveUp);
            AddMenuItem(GuiTexts.TreeMoveDown, () => Request(new GuiDesignerAction(GuiDesignerActionKind.MoveDown, node)), editable, GuiTexts.KeyMoveDown);
        }

        if (item.Kind is GuiTreeItemKind.Widget or GuiTreeItemKind.Holder)
        {
            foreach (var box in new[] { "hbox", "vbox" })
            {
                AddMenuItem(GuiTexts.TreeWrapIn(box), () => Request(new GuiDesignerAction(GuiDesignerActionKind.WrapIn, node) { Name = box }), editable);
            }
        }

        if (item.Kind == GuiTreeItemKind.Widget)
        {
            AddMenuItem(GuiTexts.ScopeNewType, () => RequestFor(NewTypeRequested, node), editable);
            AddMenuItem(GuiTexts.TreeExtractType, () => RequestFor(ExtractTypeRequested, node), editable);
            AddMenuItem(GuiTexts.TreeRetype, () => RequestFor(RetypeRequested, node), editable);
        }

        if (_treeMenu.Items.Count > 0 && _treeMenu.Items[^1] is Separator)
        {
            _treeMenu.Items.RemoveAt(_treeMenu.Items.Count - 1);
        }
    }

    /// <summary>Menüeintrag; die Überschrift als TextBlock, weil sie Namen mit "_" enthalten kann (WPF läse sonst eine Zugriffstaste).</summary>
    private void AddMenuItem(string header, Action click, bool enabled = true, string? gesture = null)
    {
        var item = new MenuItem { Header = new TextBlock { Text = header }, IsEnabled = enabled, InputGestureText = gesture ?? string.Empty };
        item.Click += (_, _) => click();
        _treeMenu.Items.Add(item);
    }

    private TreeViewItem CreateItem(GuiTreeItem item, string parentKey, int depth, HashSet<string> expanded)
    {
        var key = parentKey + "/" + item.Label;
        var treeItem = new TreeViewItem { Header = HeaderFor(item), Tag = item, IsExpanded = depth == 0 || expanded.Contains(key) };
        if (item.Kind != GuiTreeItemKind.OfferedBlock)
        {
            _treeItems.TryAdd(item.Node, treeItem);
        }

        foreach (var child in item.Children)
        {
            treeItem.Items.Add(CreateItem(child, key, depth + 1, expanded));
        }

        return treeItem;
    }

    private static void CollectExpanded(TreeViewItem item, string parentKey, HashSet<string> expanded)
    {
        if (item.Tag is not GuiTreeItem tag)
        {
            return;
        }

        var key = parentKey + "/" + tag.Label;
        if (item.IsExpanded)
        {
            expanded.Add(key);
        }

        foreach (TreeViewItem child in item.Items)
        {
            CollectExpanded(child, key, expanded);
        }
    }

    private static TreeViewItem? FindTreeItem(DependencyObject? source)
    {
        for (var current = source; current is not null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (current is TreeViewItem item)
            {
                return item;
            }
        }

        return null;
    }

    private static FrameworkElement HeaderFor(GuiTreeItem item)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Border
        {
            Width = 8,
            Height = 8,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(GuiSchematicCanvas.ColorOf(item.Category)),
        });
        var label = new TextBlock { Text = item.Label };
        if (item.Kind == GuiTreeItemKind.OfferedBlock)
        {
            label.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        }

        if (item.Unknown)
        {
            label.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
        }

        panel.Children.Add(label);
        if (item.Detail is { } detail)
        {
            var detailText = new TextBlock { Text = "  " + detail, FontStyle = FontStyles.Italic };
            detailText.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            panel.Children.Add(detailText);
        }

        return panel;
    }

    /// <summary>Spalten Eigenschaft, Wert, Herkunft (auch im GUI-Inspektor).</summary>
    internal static GridView CreatePropertyColumns()
    {
        var columns = new GridView();
        columns.Columns.Add(new GridViewColumn { Header = GuiTexts.ColumnProperty, DisplayMemberBinding = new Binding(nameof(GuiPropertyRow.Name)), Width = 140 });
        columns.Columns.Add(new GridViewColumn { Header = GuiTexts.ColumnValue, DisplayMemberBinding = new Binding(nameof(GuiPropertyRow.Value)), Width = 160 });
        columns.Columns.Add(new GridViewColumn { Header = GuiTexts.ColumnOrigin, DisplayMemberBinding = new Binding(nameof(GuiPropertyRow.OriginText)), Width = 240 });
        return columns;
    }

    internal static Style PropertyRowStyle()
    {
        var style = new Style(typeof(ListViewItem), Application.Current?.TryFindResource(typeof(ListViewItem)) as Style);
        var here = new DataTrigger { Binding = new Binding(nameof(GuiPropertyRow.IsHere)), Value = true };
        here.Setters.Add(new Setter(FontWeightProperty, FontWeights.Bold));
        var inherited = new DataTrigger { Binding = new Binding(nameof(GuiPropertyRow.IsHere)), Value = false };
        inherited.Setters.Add(new Setter(ForegroundProperty, new DynamicResourceExtension("Theme.TextMuted")));
        var unresolved = new DataTrigger { Binding = new Binding(nameof(GuiPropertyRow.Unresolved)), Value = true };
        unresolved.Setters.Add(new Setter(FontStyleProperty, FontStyles.Italic));
        style.Triggers.Add(here);
        style.Triggers.Add(inherited);
        style.Triggers.Add(unresolved);
        return style;
    }

    internal static DataTemplate GroupHeaderTemplate()
    {
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding("Name") { Converter = new GroupNameConverter() });
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.MarginProperty, new Thickness(2, 6, 0, 2));
        return new DataTemplate { VisualTree = text };
    }

    private sealed class GroupNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
        {
            GuiPropertyGroup.Data => GuiTexts.GroupData,
            GuiPropertyGroup.Layout => GuiTexts.GroupLayout,
            GuiPropertyGroup.Appearance => GuiTexts.GroupAppearance,
            GuiPropertyGroup.Text => GuiTexts.GroupText,
            GuiPropertyGroup.Behavior => GuiTexts.GroupBehavior,
            GuiPropertyGroup.Other => GuiTexts.GroupOther,
            _ => value?.ToString(),
        };

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
