using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
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
    /// <summary>Erlaubte UI-Skalierung im Eingabefeld (0 hieße: Teilen durch 0 in der Engine).</summary>
    private const double MinUiScale = GuiPreviewScreen.MinUiScale;

    private const double MaxUiScale = GuiPreviewScreen.MaxUiScale;

    // Bildschirm-Werkzeuge der Vorschau (Auflösung, UI-Skalierung, Rahmen, Überstand) in beiden Formaten.
    private readonly StackPanel _screenTools = new() { Orientation = Orientation.Horizontal };
    private readonly ComboBox _resolution = new() { Margin = new Thickness(0, 2, 4, 2), MinWidth = 110, ToolTip = GuiTexts.DesignerResolutionTip };
    private readonly TextBox _customResolution = new()
    {
        Width = 80,
        Margin = new Thickness(0, 2, 8, 2),
        VerticalContentAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
        ToolTip = GuiTexts.DesignerResolutionCustomTip,
    };
    private readonly TextBox _uiScale = new()
    {
        Width = 44,
        Margin = new Thickness(0, 2, 8, 2),
        VerticalContentAlignment = VerticalAlignment.Center,
        Text = "1",
        ToolTip = GuiTexts.DesignerUiScaleTip,
    };
    // Sprache der Jomini-Vorschautexte: "Spiel (…)" und die Sprachen der Installation; nur im Jomini-Format sichtbar.
    private readonly TextBlock _languageLabel = new() { Text = GuiTexts.DesignerLanguage, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
    private readonly ComboBox _language = new() { Margin = new Thickness(0, 2, 8, 2), MinWidth = 100, ToolTip = GuiTexts.DesignerLanguageTip };
    private readonly CheckBox _showBounds = new()
    {
        Content = GuiTexts.DesignerShowBounds,
        Margin = new Thickness(0, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        ToolTip = GuiTexts.DesignerShowBoundsTip,
    };
    private readonly CheckBox _markOverflow = new()
    {
        Content = GuiTexts.DesignerMarkOverflow,
        Margin = new Thickness(0, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        ToolTip = GuiTexts.DesignerMarkOverflowTip,
    };
    // Bedingte Elemente: Banner über der Vorschau ("n bedingte Elemente ausgeblendet", "Alle einblenden") und derselbe Knopf in der Leiste.
    private readonly TextBlock _conditionalBannerText = new();
    private readonly Button _conditionalBannerButton = new();
    private readonly Border _conditionalBanner;
    private readonly Button _showAllConditionals = new()
    {
        Content = GuiTexts.DesignerShowAllConditionals,
        ToolTip = GuiTexts.DesignerShowAllConditionalsTip,
        Padding = new Thickness(6, 1, 6, 1),
        Margin = new Thickness(0, 2, 8, 2),
        IsEnabled = false,
    };

    // Angesehene Datei der letzten Vorschau und die Schlüssel bedingter Elemente je Syntaxknoten (für Baummarken und Baummenü).
    private string? _viewedFile;
    private Dictionary<GuiNode, string> _conditionalKeys = new();

    // Gezeigte Marke je Baumeintrag: false = bedingt, true = ausgeblendet; fehlt = nicht bedingt.
    private readonly Dictionary<GuiNode, bool> _treeMarks = new();

    // Stand der ausgeblendeten Schlüssel beim Entladen: weicht er beim nächsten Laden ab, rechnet die Vorschau neu.
    private HashSet<string>? _hiddenAtUnload;

    // Wirksame Vorschausprache beim Entladen: weicht sie beim nächsten Laden ab, rechnet die Vorschau neu.
    private string? _languageAtUnload;
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

    // Klassisch: das Sprite im Kasten des gewählten Elements (Frame, Ränder, Textur) für die Skizzen im Erklärungsfeld.
    private GuiSpriteVisual? _selectedSprite;
    private Point? _dragStart;
    private GuiTreeItem? _dragItem;

    // Vorschau: gewählte Auflösung und Skalierung, die eigene Auflösung und der zuletzt gelesene Stand der Spieleinstellungen (klassisch settings.txt,
    // Jomini pdx_settings im Benutzerordner) samt Format und Quelle, aus denen er stammt.
    private (int Width, int Height) _customSize = (1920, 1080);
    private double _uiScaleValue = 1;
    private string? _settingsSource;
    private bool _settingsClassic;
    private bool _settingsRead;
    private (int Width, int Height, double UiScale)? _gameScreen;
    private bool _syncingScreenTools;
    private bool _syncingLanguage;
    private string? _languageItems;

    /// <summary>Leiste mit Auflösung, UI-Skalierung, "Rahmen zeigen" und "Überstand markieren" (für Tests).</summary>
    internal StackPanel ScreenTools => _screenTools;

    /// <summary>Sprache der Vorschautexte: die gewählte, sonst die des Spiels.</summary>
    private static string EffectiveLanguage => GuiSettings.PreviewLanguage ?? GuiPreviewScreen.GameLanguage();

    /// <summary>Umschalter der Vorschausprache (für Tests).</summary>
    internal ComboBox LanguageBox => _language;

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
        _showAllConditionals.Click += (_, _) => ShowAllConditionals();
        _conditionalBannerButton.Click += (_, _) => ShowAllConditionals();
        _conditionalBanner = ConditionalBanner(_conditionalBannerText, _conditionalBannerButton);
        toolbar.Children.Add(_showAllConditionals);
        toolbar.Children.Add(new TextBlock { Text = GuiTexts.DesignerListCount, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        toolbar.Children.Add(_listCount);
        BuildScreenTools();
        toolbar.Children.Add(_screenTools);
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
        var previewPanel = new DockPanel();
        DockPanel.SetDock(_conditionalBanner, Dock.Top);
        previewPanel.Children.Add(_conditionalBanner);
        previewPanel.Children.Add(previewArea);

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
                if (GuiSettings.Layout.IsClassic)
                {
                    _propertyEditor.SetRow(row);
                }
                else
                {
                    _propertyEditor.SetProperty(row.Source);
                }
            }
        };
        _propertyEditor.ActionRequested += (_, action) => Request(action);
        _propertyEditor.ShowInCodeRequested += (_, node) => ShowInCodeRequested?.Invoke(this, node);
        _propertyEditor.ExampleActivated += (_, origin) => OriginActivated?.Invoke(this, origin);
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
        Grid.SetColumn(previewPanel, 2);
        main.Children.Add(previewPanel);
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

        // Ausgeblendete bedingte Elemente gelten für alle offenen Designer und Inspektoren: nur angemeldet, solange die Ansicht geladen ist.
        Loaded += (_, _) =>
        {
            GuiSettings.HiddenConditionalsChanged -= OnHiddenConditionalsChanged;
            GuiSettings.HiddenConditionalsChanged += OnHiddenConditionalsChanged;
            GuiSettings.Changed -= OnSettingsChanged;
            GuiSettings.Changed += OnSettingsChanged;
            GuiSettings.PreviewLanguagesChanged -= OnSettingsChanged;
            GuiSettings.PreviewLanguagesChanged += OnSettingsChanged;
            GuiSettings.PreviewLanguageChanged -= OnPreviewLanguageChanged;
            GuiSettings.PreviewLanguageChanged += OnPreviewLanguageChanged;
            // Während der Ansicht entladen war, kann der Arbeitsbereich oder die Vorschausprache gewechselt haben.
            SyncLanguageTools();
            var changed = _hiddenAtUnload is { } before && !before.SetEquals(GuiSettings.HiddenConditionals);
            _hiddenAtUnload = null;
            // Die Vorschausprache kann sich geändert haben, solange die Ansicht entladen war (Abmeldung vom Ereignis).
            changed |= _languageAtUnload is { } languageBefore && languageBefore != EffectiveLanguage;
            _languageAtUnload = null;
            UpdateConditionalState();
            if (changed)
            {
                PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
            }
        };
        Unloaded += (_, _) =>
        {
            GuiSettings.HiddenConditionalsChanged -= OnHiddenConditionalsChanged;
            GuiSettings.Changed -= OnSettingsChanged;
            GuiSettings.PreviewLanguagesChanged -= OnSettingsChanged;
            GuiSettings.PreviewLanguageChanged -= OnPreviewLanguageChanged;
            _languageAtUnload = EffectiveLanguage;
            _hiddenAtUnload = new HashSet<string>(GuiSettings.HiddenConditionals, StringComparer.Ordinal);
        };
        UpdateConditionalState();

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
                var insert = new GuiDesignerAction(GuiDesignerActionKind.Insert, null)
                {
                    Placement = targetNode is null ? GuiPlacement.AtSelection : GuiPlacement.Drop,
                    Target = targetNode,
                    DropY = y,
                    Entry = entry,
                };

                // Ein Baustein öffnet ein modales Fenster: nicht innerhalb der Ziehen-Schleife von DoDragDrop (Teil-2-Ruling 2).
                if (entry.Kind == GuiPaletteKind.Block)
                {
                    Dispatcher.BeginInvoke(() => Request(insert));
                }
                else
                {
                    Request(insert);
                }
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

    /// <summary>"Als Baustein speichern" im Baum.</summary>
    public event EventHandler<GuiNode>? SaveAsBlockRequested;

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

    /// <summary>Vorschau-Einstellungen mit der gewählten Auflösung und UI-Skalierung (beide Formate).</summary>
    public LayoutOptions PreviewOptions
    {
        get
        {
            var (width, height) = SelectedResolution;
            return new LayoutOptions
            {
                ShowConditional = _showConditional.IsChecked == true,
                ShowHidden = _showHidden.IsChecked == true,
                ListSampleCount = _listCount.SelectedItem is int count ? count : 3,
                ScreenWidth = width,
                ScreenHeight = height,
                UiScale = _uiScaleValue,
                // Kopie: das Layout läuft im Hintergrund, die Menge kann sich währenddessen ändern.
                HiddenConditionals = new HashSet<string>(GuiSettings.HiddenConditionals, StringComparer.Ordinal),
                Language = EffectiveLanguage,
            };
        }
    }

    /// <summary>Text des Banners für ausgeblendete bedingte Elemente; null, solange es nicht zu sehen ist (für Tests).</summary>
    internal string? ConditionalBannerText => _conditionalBanner.Visibility == Visibility.Visible ? _conditionalBannerText.Text : null;

    /// <summary>"Alle einblenden" im Banner (für Tests).</summary>
    internal Button ConditionalBannerButton => _conditionalBannerButton;

    /// <summary>"Alle einblenden" in der Leiste (für Tests).</summary>
    internal Button ShowAllConditionalsButton => _showAllConditionals;

    /// <summary>Der Baumeintrag dieses Knotens trägt die Marke "bedingt" (gezeigt oder ausgeblendet; für Tests).</summary>
    internal bool TreeItemConditional(GuiNode node) => _treeMarks.ContainsKey(node);

    /// <summary>Der Baumeintrag dieses Knotens ist als ausgeblendet markiert (für Tests).</summary>
    internal bool TreeItemHidden(GuiNode node) => _treeMarks.TryGetValue(node, out var hidden) && hidden;

    /// <summary>Namensfeld in der Überschrift des Baumeintrags dieses Knotens (für Tests).</summary>
    internal TextBlock? TreeLabel(GuiNode node) =>
        _treeItems.TryGetValue(node, out var item) && item.Header is Panel header ? header.Children.OfType<TextBlock>().FirstOrDefault() : null;

    /// <summary>Baut das Kontextmenü des Baumeintrags dieses Knotens wie beim Rechtsklick (für Tests).</summary>
    internal ContextMenu TreeMenuFor(GuiNode node)
    {
        _treeMenu.Items.Clear();
        if (_treeItems.TryGetValue(node, out var item) && item.Tag is GuiTreeItem tag)
        {
            BuildTreeMenu(tag);
        }

        return _treeMenu;
    }

    /// <summary>
    /// Spieleinstellungen neu lesen (<see cref="GuiPreviewScreen.FromGame"/>), wenn sich Format oder Quelle geändert haben: klassisch die settings.txt,
    /// Jomini der Benutzerordner mit pdx_settings. Dann gelten wieder "Spiel" und dessen Skalierung; ohne lesbare Datei klassisch 1920×1080, Jomini "Spiel"
    /// mit der Auflösung des Hauptbildschirms (<see cref="GuiPreviewScreen.Fallback"/>), jeweils mit Skalierung 1. Ruft der Controller
    /// vor jeder Vorschau und beim Wechsel der Datei auf.
    /// </summary>
    public void SyncScreenTools()
    {
        var classic = GuiSettings.Layout.IsClassic;
        _showAllConditionals.Visibility = classic ? Visibility.Collapsed : Visibility.Visible;
        SyncLanguageTools();
        var source = classic ? GuiSettings.GameSettingsPath : GuiSettings.GameUserFolder;
        if (_settingsRead && classic == _settingsClassic && string.Equals(source, _settingsSource, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settingsRead = true;
        _settingsClassic = classic;
        _settingsSource = source;
        // Jomini ohne lesbare Einstellungen: "Spiel" ist der Hauptbildschirm mit Skalierung 1 (Spec "Fehlerbehandlung"); klassisch fehlt "Spiel" dann.
        _gameScreen = GuiPreviewScreen.FromGame() ?? (classic ? null : GuiPreviewScreen.Fallback());
        _syncingScreenTools = true;
        try
        {
            FillResolutions();
            _uiScaleValue = _gameScreen?.UiScale ?? 1;
            _uiScale.Text = _uiScaleValue.ToString("0.###", CultureInfo.CurrentCulture);
        }
        finally
        {
            _syncingScreenTools = false;
        }
    }

    /// <summary>
    /// Umschalter der Vorschausprache zum aktuellen Stand: nur im Jomini-Format sichtbar; erster Eintrag "Spiel (Sprache)" (Tag null), dann eine Sprache je
    /// Eintrag des Textdienstes (ohne "l_"). Aufgebaut wird nur bei geänderter Liste oder Spielsprache; gewählt ist die Vorschausprache der Sitzung.
    /// </summary>
    private void SyncLanguageTools()
    {
        var visibility = GuiSettings.Layout.IsClassic ? Visibility.Collapsed : Visibility.Visible;
        _languageLabel.Visibility = visibility;
        _language.Visibility = visibility;
        if (visibility != Visibility.Visible)
        {
            return;
        }

        var game = GuiPreviewScreen.GameLanguage();
        // Nur die im Hintergrund gesuchte Liste: der UI-Thread löst die Ordnersuche des Textdienstes nie aus.
        var languages = GuiSettings.PreviewLanguages;
        var signature = game + "|" + string.Join(',', languages);
        _syncingLanguage = true;
        try
        {
            if (signature != _languageItems)
            {
                _languageItems = signature;
                _language.Items.Clear();
                _language.Items.Add(new ComboBoxItem { Content = GuiTexts.DesignerLanguageGame(GuiPreviewScreen.LanguageName(game)), Tag = null });
                foreach (var language in languages)
                {
                    _language.Items.Add(new ComboBoxItem { Content = GuiPreviewScreen.LanguageName(language), Tag = language });
                }
            }

            var chosen = GuiSettings.PreviewLanguage;
            _language.SelectedItem = _language.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, chosen))
                ?? _language.Items.OfType<ComboBoxItem>().First();
        }
        finally
        {
            _syncingLanguage = false;
        }
    }

    /// <summary>Neuer Arbeitsbereich (Sprachen der Installation) oder anderes Spiel: Umschalter neu füllen.</summary>
    private void OnSettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(SyncLanguageTools);

    /// <summary>Ein anderer Designer hat die Sprache gewählt: Umschalter angleichen und die Vorschau neu rechnen.</summary>
    private void OnPreviewLanguageChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            var before = _language.SelectedItem;
            SyncLanguageTools();
            if (!ReferenceEquals(before, _language.SelectedItem))
            {
                PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
            }
        });

    private (int Width, int Height) SelectedResolution => (_resolution.SelectedItem as ComboBoxItem)?.Tag switch
    {
        ValueTuple<int, int> size => size,
        ResolutionChoice.Game when _gameScreen is { } game => (game.Width, game.Height),
        ResolutionChoice.Custom => _customSize,
        _ => FallbackSize(),
    };

    private static (int Width, int Height) FallbackSize()
    {
        var (width, height, _) = GuiPreviewScreen.Fallback();
        return (width, height);
    }

    private enum ResolutionChoice
    {
        Game,
        Custom,
    }

    private void BuildScreenTools()
    {
        _screenTools.Children.Add(new TextBlock { Text = GuiTexts.DesignerResolution, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _screenTools.Children.Add(_resolution);
        _screenTools.Children.Add(_customResolution);
        _screenTools.Children.Add(new TextBlock { Text = GuiTexts.DesignerUiScale, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        _screenTools.Children.Add(_uiScale);
        _screenTools.Children.Add(_languageLabel);
        _screenTools.Children.Add(_language);
        _screenTools.Children.Add(_showBounds);
        _screenTools.Children.Add(_markOverflow);
        _markOverflow.IsChecked = GuiSettings.MarkAllOverflow;
        _canvas.MarkAllOverflow = GuiSettings.MarkAllOverflow;
        FillResolutions();

        _resolution.SelectionChanged += (_, _) =>
        {
            var custom = (_resolution.SelectedItem as ComboBoxItem)?.Tag is ResolutionChoice.Custom;
            _customResolution.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
            if (_syncingScreenTools)
            {
                return;
            }

            if (custom)
            {
                _customResolution.Text = FormatSize(_customSize);
                _customResolution.Focus();
                _customResolution.SelectAll();
            }

            PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
        };

        _customResolution.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyCustomResolution();
                e.Handled = true;
            }
        };
        _customResolution.LostKeyboardFocus += (_, _) => ApplyCustomResolution();
        _uiScale.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyUiScale();
                e.Handled = true;
            }
        };
        _uiScale.LostKeyboardFocus += (_, _) => ApplyUiScale();
        _language.SelectionChanged += (_, _) =>
        {
            if (_syncingLanguage || _language.SelectedItem is not ComboBoxItem { } item)
            {
                return;
            }

            var language = item.Tag as string;
            GuiSettings.PreviewLanguage = language;
            // Die gewählte Sprache vorladen, damit die neu gerechnete Vorschau nicht darauf wartet.
            GuiSettings.WarmText(GuiSettings.Workspace, language ?? GuiPreviewScreen.GameLanguage());
            PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
        };
        _showBounds.Click += (_, _) => _canvas.ShowBounds = _showBounds.IsChecked == true;
        _markOverflow.Click += (_, _) => _canvas.MarkAllOverflow = GuiSettings.MarkAllOverflow = _markOverflow.IsChecked == true;
    }

    /// <summary>Die festen Auflösungen, "Spiel" (klassisch nur mit lesbaren Spieleinstellungen, Jomini immer; dann vorgewählt) und "Eigene…".</summary>
    private void FillResolutions()
    {
        _resolution.Items.Clear();
        foreach (var size in new[] { (1920, 1080), (2560, 1440), (3840, 2160), (3440, 1440) })
        {
            _resolution.Items.Add(new ComboBoxItem { Content = FormatSize(size), Tag = size });
        }

        if (_gameScreen is { } game)
        {
            _resolution.Items.Add(new ComboBoxItem { Content = GuiTexts.DesignerResolutionGame(game.Width, game.Height), Tag = ResolutionChoice.Game });
        }

        _resolution.Items.Add(new ComboBoxItem { Content = GuiTexts.DesignerResolutionCustom, Tag = ResolutionChoice.Custom });
        _resolution.SelectedIndex = _gameScreen is null ? 0 : 4;
    }

    private void ApplyCustomResolution()
    {
        if (ParseSize(_customResolution.Text) is { } size && size != _customSize)
        {
            _customSize = size;
            PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
        }

        _customResolution.Text = FormatSize(_customSize);
    }

    private void ApplyUiScale()
    {
        var text = _uiScale.Text.Trim().Replace(',', '.');
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) && double.IsFinite(scale) && scale is >= MinUiScale and <= MaxUiScale
            && scale != _uiScaleValue)
        {
            _uiScaleValue = scale;
            PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
        }

        _uiScale.Text = _uiScaleValue.ToString("0.###", CultureInfo.CurrentCulture);
    }

    /// <summary>"2048×1152", "2048x1152" oder "2048 1152"; Seiten zwischen 320 und 16384 Pixeln.</summary>
    internal static (int Width, int Height)? ParseSize(string text)
    {
        var parts = text.Split(new[] { '×', 'x', 'X', '*', ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) && width is >= 320 and <= 16384
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) && height is >= 320 and <= 16384
            ? (width, height)
            : null;
    }

    private static string FormatSize((int Width, int Height) size) => string.Create(CultureInfo.InvariantCulture, $"{size.Width}×{size.Height}");

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
            _treeMarks.Clear();
            foreach (var item in items)
            {
                _tree.Items.Add(CreateItem(item, string.Empty, 0, expanded));
            }

            // Der Baum kann nach der Vorschau neu entstehen: Marken aus der letzten Vorschau gleich wieder setzen.
            ApplyTreeMarks();
        }
        finally
        {
            _suppressTreeEvents = false;
        }
    }

    /// <param name="fonts">Bitmap-Schriften des Arbeitsbereichs, der die Vorschau gelegt hat (klassisch).</param>
    /// <param name="viewedFile">Angesehene Datei, mit der die Vorschau gelegt wurde (Schlüssel bedingter Elemente); null ohne Dateipfad.</param>
    public void SetPreview(GuiPreview? preview, bool keepView, GuiDesign.Classic.ClassicFontLibrary? fonts = null, string? viewedFile = null)
    {
        _canvas.DarkBackground = !GuiSettings.Layout.IsClassic;
        GuiSchematicCanvas.ApplyMessageForeground(_message, _canvas.DarkBackground);
        _canvas.SetScene(preview?.Scene, keepView, fonts);
        _message.Text = preview?.Scene is null
            ? preview?.Message ?? GuiTexts.PreviewNone
            : string.Empty;
        _viewedFile = viewedFile;
        _conditionalKeys = ConditionalKeysByNode(preview);
        UpdateConditionalState();
    }

    /// <summary>
    /// Schlüssel bedingter Elemente je Syntaxknoten der Vorschau, wie <see cref="GuiPreview.ElementFor"/>: je Herkunftsknoten das erste Element in
    /// Vorordnung, das Ziel steht für das Wurzelelement. Die Schlüssel berechnet die Engine (<see cref="LayoutBox.ConditionalKeys"/>), auch für
    /// ausgeblendete Elemente ohne Kasten.
    /// </summary>
    private static Dictionary<GuiNode, string> ConditionalKeysByNode(GuiPreview? preview)
    {
        var result = new Dictionary<GuiNode, string>();
        if (preview?.Element is not { } root || preview.Scene?.Root.ConditionalKeys is not { Count: > 0 } keys)
        {
            return result;
        }

        var seen = new HashSet<GuiNode> { preview.Target };
        if (keys.TryGetValue(root, out var rootKey))
        {
            result[preview.Target] = rootKey;
        }

        var stack = new Stack<ResolvedElement>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            if (seen.Add(element.Origin.Node) && keys.TryGetValue(element, out var key))
            {
                result[element.Origin.Node] = key;
            }

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(element.Children[i]);
            }
        }

        return result;
    }

    /// <summary>Ein Schlüssel kam hinzu oder fiel weg (auch aus einem anderen Designer oder dem Inspektor): Marken, Banner, Vorschau neu.</summary>
    private void OnHiddenConditionalsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            UpdateConditionalState();
            PreviewOptionsChanged?.Invoke(this, EventArgs.Empty);
        });

    /// <summary>Blendet ein bedingtes Element aus oder wieder ein; Marken und Banner sofort, die Vorschau über <see cref="OnHiddenConditionalsChanged"/>.</summary>
    private void SetConditionalHidden(string key, bool hidden)
    {
        GuiSettings.SetConditionalHidden(key, hidden);
        UpdateConditionalState();
    }

    /// <summary>"Alle einblenden": nur die Schlüssel der angesehenen Datei.</summary>
    private void ShowAllConditionals()
    {
        if (_viewedFile is { } file)
        {
            GuiSettings.ShowAllConditionals(file);
            UpdateConditionalState();
        }
    }

    /// <summary>Baummarken, Banner und Leistenknopf zum aktuellen Stand der ausgeblendeten Schlüssel.</summary>
    private void UpdateConditionalState()
    {
        ApplyTreeMarks();
        var count = _viewedFile is { } file ? GuiSettings.HiddenCount(file) : 0;
        _conditionalBannerText.Text = count > 0 ? GuiTexts.DesignerHiddenConditionals(count) : string.Empty;
        _conditionalBanner.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _showAllConditionals.IsEnabled = count > 0;

        // Klassische .gui-Dateien (HOI IV, EU IV) kennen kein visible = "[…]": der Knopf bliebe dort immer gesperrt.
        _showAllConditionals.Visibility = GuiSettings.Layout.IsClassic ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Setzt die Marke "bedingt" bzw. "ausgeblendet" an den Baumeinträgen; Überschriften entstehen nur bei geändertem Zustand neu.</summary>
    private void ApplyTreeMarks()
    {
        foreach (var (node, treeItem) in _treeItems)
        {
            bool? mark = _conditionalKeys.TryGetValue(node, out var key) ? GuiSettings.HiddenConditionals.Contains(key) : null;
            bool? shown = _treeMarks.TryGetValue(node, out var current) ? current : null;
            if (mark == shown || treeItem.Tag is not GuiTreeItem item)
            {
                continue;
            }

            treeItem.Header = HeaderFor(item, mark);
            if (mark is { } hidden)
            {
                _treeMarks[node] = hidden;
            }
            else
            {
                _treeMarks.Remove(node);
            }
        }
    }

    /// <summary>Markiert das Element in Baum und Vorschau und zeigt seine Eigenschaften (aus der Vorschau, sofern das Element darin vorkommt).</summary>
    public void Select(GuiNode? widget, GuiPreview? preview)
    {
        _selectedNode = widget;
        _selectedSprite = widget is null ? null : preview?.Scene?.BoxesFor(widget).Select(box => box.Visual).OfType<GuiSpriteVisual>().FirstOrDefault();
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
        _propertyEditor.SetElement(element is null ? null : _selectedNode, element, IsEditable, treeItem?.Kind == GuiTreeItemKind.Widget, treeItem?.IsTypeInstance == true, _selectedSprite);
        if (element is null)
        {
            _chain.Visibility = Visibility.Visible;
            _chain.Text = GuiTexts.NoProperties;
            _properties.ItemsSource = null;
            return;
        }

        ShowChain(element);
        var list = BuildRows(element);
        var rows = new ListCollectionView(list);
        rows.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GuiPropertyRow.Group)));
        _properties.ItemsSource = rows;

        // Nach einer Änderung dieselbe Eigenschaft wieder wählen, damit man an ihr weiterarbeiten kann. Klassisch ohne Rücksicht auf Groß-/Kleinschreibung
        // (der Name kommt aus dem Code und kann sich beim Bearbeiten ändern), die genaue Schreibweise zuerst.
        if (_selectedPropertyName is { } name && RowNamed(list, name) is { } again)
        {
            _properties.SelectedItem = again;
        }
    }

    /// <summary>Zeilen des Eigenschaftenfelds; klassisch mit den Werten aus dem <c>if_resolution</c>-Block, der zur gewählten Auflösung und UI-Skalierung passt.</summary>
    private List<GuiPropertyRow> BuildRows(ResolvedElement element)
    {
        if (!GuiSettings.Layout.IsClassic)
        {
            return GuiPropertyRows.Build(element).ToList();
        }

        var options = PreviewOptions;
        var scale = options.UiScale > 0 ? options.UiScale : 1;
        return ClassicPropertyRows.Build(element, options.ScreenWidth / scale, options.ScreenHeight / scale).ToList();
    }

    internal static GuiPropertyRow? RowNamed(IReadOnlyList<GuiPropertyRow> rows, string name)
    {
        var exact = rows.FirstOrDefault(r => r.Name == name);
        return exact ?? (GuiSettings.Layout.IsClassic ? rows.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) : null);
    }

    /// <summary>Eigenschaften eines Elements aus dem Inneren eines Typs: nur lesen, Anpassen über blockoverride (Plan-Ruling 14).</summary>
    private void ShowInherited(GuiNode instance, ResolvedElement inner)
    {
        _shownElement = null;
        ShowChain(inner);
        var rows = new ListCollectionView(GuiPropertyRows.Build(inner).ToList());
        rows.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GuiPropertyRow.Group)));
        _properties.ItemsSource = rows;
        _propertyEditor.SetInherited(instance, inner, IsEditable);
    }

    /// <summary>Typkette über den Eigenschaften; klassische .gui-Dateien (HOI IV, EU IV) kennen keine Typen, dort bleibt die Zeile leer und ausgeblendet.</summary>
    private void ShowChain(ResolvedElement element)
    {
        var classic = GuiSettings.Layout.IsClassic;
        _chain.Visibility = classic ? Visibility.Collapsed : Visibility.Visible;
        _chain.Text = classic ? string.Empty : GuiTexts.TypeChain(string.Join(" → ", element.TypeChain));
    }

    /// <summary>"In hbox/vbox einpacken" im Baummenü: nur Jomini, denn klassische .gui-Dateien (HOI IV, EU IV) kennen hbox und vbox nicht.</summary>
    internal static bool OffersWrapIn(GuiTreeItemKind kind) =>
        kind is GuiTreeItemKind.Widget or GuiTreeItemKind.Holder && !GuiSettings.Layout.IsClassic;

    /// <summary>"Neuer Typ", "Als neuen Typ herausziehen", "Widget-Schlüssel umstellen": Typen gibt es nur im Jomini-Format, nicht klassisch (HOI IV, EU IV).</summary>
    internal static bool OffersTypeEdits(GuiTreeItemKind kind) =>
        kind == GuiTreeItemKind.Widget && !GuiSettings.Layout.IsClassic;

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

            // Auch schreibgeschützt (Vanilla): Speichern ändert die Datei nicht (Teil-2-Ruling 3).
            AddMenuItem(GuiTexts.ContextSaveBlock, () => SaveAsBlockRequested?.Invoke(this, node));

            // Ausblenden ändert nur die Vorschau, nie die Datei: auch schreibgeschützt. Ohne Dateipfad gibt es keinen Schlüssel zum Merken.
            if (_viewedFile is not null && _conditionalKeys.TryGetValue(node, out var conditionalKey))
            {
                var hidden = GuiSettings.HiddenConditionals.Contains(conditionalKey);
                AddMenuItem(
                    hidden ? GuiTexts.DesignerUnhideConditional : GuiTexts.DesignerHideConditional,
                    () => SetConditionalHidden(conditionalKey, !hidden));
            }
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

            // Bausteine (Teil-2-Ruling 1) werden wie Widgets eingefügt, nie als using.
            if (entry.Kind == GuiPaletteKind.Template)
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

        if (OffersWrapIn(item.Kind))
        {
            foreach (var box in new[] { "hbox", "vbox" })
            {
                AddMenuItem(GuiTexts.TreeWrapIn(box), () => Request(new GuiDesignerAction(GuiDesignerActionKind.WrapIn, node) { Name = box }), editable);
            }
        }

        if (OffersTypeEdits(item.Kind))
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
        var treeItem = new TreeViewItem { Header = HeaderFor(item, null), Tag = item, IsExpanded = depth == 0 || expanded.Contains(key) };
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

    internal static TreeViewItem? FindTreeItem(DependencyObject? source)
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

    /// <param name="conditional">null: nicht bedingt; false: bedingt und gezeigt; true: ausgeblendet.</param>
    private static FrameworkElement HeaderFor(GuiTreeItem item, bool? conditional)
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

        MarkConditional(panel, label, conditional);
        return panel;
    }

    /// <summary>
    /// Marke eines bedingten Elements hinter dem Namen (Dreieck in der Warnfarbe wie in der Vorschau); ausgeblendet hohl, Name durchgestrichen und
    /// gedämpft. Auch im Inspektor.
    /// </summary>
    internal static void MarkConditional(Panel header, TextBlock label, bool? hidden)
    {
        if (hidden is not { } isHidden)
        {
            return;
        }

        var triangle = new System.Windows.Shapes.Polygon
        {
            Points = new PointCollection { new Point(0, 0), new Point(7, 0), new Point(7, 7) },
            StrokeThickness = 1,
            Margin = new Thickness(5, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = GuiTexts.DesignerConditionalTip,
        };
        triangle.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Theme.Warning");
        if (isHidden)
        {
            label.TextDecorations = TextDecorations.Strikethrough;

            // Eine schon gesetzte Farbe bleibt (z.B. Theme.Danger bei unbekanntem Typ): ausgeblendet zeigen Strich und hohle Marke.
            if (label.ReadLocalValue(TextBlock.ForegroundProperty) == DependencyProperty.UnsetValue)
            {
                label.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            }
        }
        else
        {
            triangle.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Theme.Warning");
        }

        header.ToolTip = GuiTexts.DesignerConditionalTip;
        header.Children.Add(triangle);
    }

    /// <summary>
    /// Banner über der Vorschau, solange bedingte Elemente der angesehenen Datei ausgeblendet sind: fetter Text und "Alle einblenden" (auch im
    /// Inspektor). Hinweisleisten-Farben des Themes mit einem Rand in der Warnfarbe, damit es nicht übersehen wird.
    /// </summary>
    internal static Border ConditionalBanner(TextBlock text, Button showAll)
    {
        text.FontWeight = FontWeights.Bold;
        text.TextWrapping = TextWrapping.Wrap;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.NoticeText");
        showAll.Content = GuiTexts.DesignerShowAllConditionals;
        showAll.Padding = new Thickness(6, 1, 6, 1);
        showAll.Margin = new Thickness(8, 0, 0, 0);
        var row = new DockPanel();
        DockPanel.SetDock(showAll, Dock.Right);
        row.Children.Add(showAll);
        row.Children.Add(text);
        var banner = new Border
        {
            Child = row,
            Padding = new Thickness(6, 3, 6, 3),
            BorderThickness = new Thickness(4, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        banner.SetResourceReference(Border.BackgroundProperty, "Theme.NoticeBackground");
        banner.SetResourceReference(Border.BorderBrushProperty, "Theme.Warning");
        return banner;
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
