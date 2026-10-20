using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// GUI-Inspektor (eigenes Fenster, nur lesend; Plan-Ruling 1): zeigt ein Element aufgelöst mit Herkunft jeder Eigenschaft, wie geschrieben,
/// als aufgelösten Code und mit seinen Beziehungen, rechts die schematische Vorschau. Verweise (Typkette, abgeleitete Typen, Verwendungen)
/// öffnen das nächste Element im selben Fenster, "Zurück" kehrt zurück. Rechnet im Hintergrund; kommt ein neuer GUI-Index, rechnet es neu.
/// </summary>
public sealed class GuiInspectorWindow : Window
{
    private sealed record UsageRow(GuiUsage Usage, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly Stack<GuiInspectTarget> _history = new();
    private readonly Stack<GuiInspectTarget> _forward = new();
    private readonly Button _back = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 4, 0), IsEnabled = false };
    private readonly Button _forwardButton = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0), IsEnabled = false };
    private readonly Button _pick = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _showInCode = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _openInDesigner = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _takeOverAsType = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
    private readonly TextBlock _title = new()
    {
        FontWeight = FontWeights.SemiBold,
        FontSize = 14,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    private readonly TextBlock _location = new() { Margin = new Thickness(0, 2, 0, 0) };
    private readonly TextBlock _chain = new() { Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly TreeView _tree = new();
    private readonly ListView _properties = new();
    private readonly TextBox _written = CodeBox();
    private readonly TextBlock _references = new() { Margin = new Thickness(2, 6, 2, 2), TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _resolvedCode = CodeBox();
    private readonly StackPanel _relations = new() { Margin = new Thickness(4) };
    private readonly TextBlock _usagesHeader = new() { Margin = new Thickness(4, 8, 4, 2), TextWrapping = TextWrapping.Wrap };
    private readonly ListBox _usages = new();
    private readonly GuiSchematicCanvas _canvas = new();
    private readonly TextBlock _previewMessage = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(12),
        IsHitTestVisible = false,
    };
    private readonly CheckBox _showConditional = new() { IsChecked = true, Margin = new Thickness(4, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox _showHidden = new() { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _listCount = new() { Width = 50, Margin = new Thickness(0, 2, 8, 2) };
    private readonly Dictionary<ResolvedElement, TreeViewItem> _treeItems = new();

    // Bedingte Elemente: Banner über der Vorschau, Schlüssel der gezeigten Vorschau (auch ausgeblendete), gezeigte Marke je Baumeintrag
    // (false = bedingt, true = ausgeblendet; fehlt = nicht bedingt).
    private readonly TextBlock _conditionalBannerText = new();
    private readonly Button _conditionalBannerButton = new();
    private readonly Border _conditionalBanner;
    private readonly ContextMenu _treeMenu = new();
    private IReadOnlyDictionary<ResolvedElement, string> _conditionalKeys = new Dictionary<ResolvedElement, string>();
    private readonly Dictionary<ResolvedElement, bool> _treeMarks = new();
    private IReadOnlyList<UsageRow> _usageRows = Array.Empty<UsageRow>();
    private Dictionary<GuiUsage, UsageRow> _rowByUsage = new(ReferenceEqualityComparer.Instance);
    private GuiInspectTarget? _target;
    private GuiInspection? _inspection;
    private GuiWorkspace? _workspace;
    private GuiIndex _index = GuiIndex.Empty;
    private CancellationTokenSource? _run;
    private CancellationTokenSource? _layoutRun;

    public GuiInspectorWindow()
    {
        Title = GuiTexts.InspectorTitle;
        Width = 1150;
        Height = 740;
        MinWidth = 640;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _back.Content = GuiTexts.InspectorBack;
        _back.ToolTip = GuiTexts.InspectorBackTip;
        _back.Click += (_, _) =>
        {
            if (_history.Count > 0 && _target is { } current)
            {
                _forward.Push(current);
                Load(_history.Pop());
            }
        };
        _forwardButton.Content = GuiTexts.InspectorForward;
        _forwardButton.ToolTip = GuiTexts.InspectorForwardTip;
        _forwardButton.Click += (_, _) =>
        {
            if (_forward.Count > 0 && _target is { } current)
            {
                _history.Push(current);
                Load(_forward.Pop());
            }
        };
        // "Element wählen…": öffnet das Suchfenster (Hauptfenster); seine Auswahl kommt über Inspect hierher zurück.
        _pick.Content = GuiTexts.InspectorPick;
        _pick.ToolTip = GuiTexts.InspectorPickTip;
        _pick.Click += (_, _) => PickElementRequested?.Invoke(this, EventArgs.Empty);
        _showInCode.Content = GuiTexts.InspectorShowInCode;
        _showInCode.ToolTip = GuiTexts.InspectorShowInCodeTip;
        _showInCode.Click += (_, _) => OpenLocation(_target?.FilePath, _target?.Node.Line ?? 1);
        _openInDesigner.Content = GuiTexts.InspectorOpenInDesigner;
        _openInDesigner.ToolTip = GuiTexts.InspectorOpenInDesignerTip;
        _openInDesigner.Click += (_, _) =>
        {
            if (_target is { FilePath: { } path } target)
            {
                OpenInDesignerRequested?.Invoke(this, new GuiLocationEventArgs(path, target.Node.Line));
            }
        };

        _takeOverAsType.Content = GuiTexts.InspectorTakeOverAsType;
        _takeOverAsType.ToolTip = GuiTexts.InspectorTakeOverAsTypeTip;
        _takeOverAsType.Click += (_, _) =>
        {
            if (_target is { } target)
            {
                TakeOverAsTypeRequested?.Invoke(this, target);
            }
        };

        var titleRow = new DockPanel();
        DockPanel.SetDock(_back, Dock.Left);
        titleRow.Children.Add(_back);
        DockPanel.SetDock(_forwardButton, Dock.Left);
        titleRow.Children.Add(_forwardButton);
        DockPanel.SetDock(_pick, Dock.Left);
        titleRow.Children.Add(_pick);
        DockPanel.SetDock(_openInDesigner, Dock.Right);
        titleRow.Children.Add(_openInDesigner);
        DockPanel.SetDock(_takeOverAsType, Dock.Right);
        titleRow.Children.Add(_takeOverAsType);
        DockPanel.SetDock(_showInCode, Dock.Right);
        titleRow.Children.Add(_showInCode);
        titleRow.Children.Add(_title);
        _location.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        var header = new StackPanel { Margin = new Thickness(10, 8, 10, 6) };
        header.Children.Add(titleRow);
        header.Children.Add(_location);
        header.Children.Add(_chain);
        header.Children.Add(_status);

        // Reiter "Aufgelöst": Baum der aufgelösten Elemente, darunter ihre Eigenschaften mit Herkunft.
        _tree.ToolTip = GuiTexts.InspectorResolvedTip;
        _tree.SelectedItemChanged += (_, e) =>
        {
            if (e.NewValue is TreeViewItem { Tag: ResolvedElement element })
            {
                ShowElement(element);
            }
        };
        // Rechtsklick im Baum: bedingte Elemente aus- und wieder einblenden (nur mit Dateipfad, die Schlüssel hängen an der Datei).
        _tree.ContextMenu = _treeMenu;
        _tree.ContextMenuOpening += (_, e) =>
        {
            BuildTreeMenu(GuiDesignerView.FindTreeItem(e.OriginalSource as DependencyObject)?.Tag as ResolvedElement);
            if (_treeMenu.Items.Count == 0)
            {
                e.Handled = true;
            }
        };
        _tree.MouseDoubleClick += (_, e) =>
        {
            if (_tree.SelectedItem is TreeViewItem { Tag: ResolvedElement element })
            {
                OpenLocation(element.Origin.FilePath, element.Origin.Line);
                e.Handled = true;
            }
        };
        _properties.View = GuiDesignerView.CreatePropertyColumns();
        _properties.ItemContainerStyle = GuiDesignerView.PropertyRowStyle();
        _properties.GroupStyle.Add(new GroupStyle { HeaderTemplate = GuiDesignerView.GroupHeaderTemplate() });
        _properties.ToolTip = GuiTexts.PropertiesTip;
        _properties.MouseDoubleClick += (_, _) =>
        {
            if (_properties.SelectedItem is GuiPropertyRow row)
            {
                OpenLocation(row.Origin.FilePath, row.Origin.Line);
            }
        };
        var resolvedGrid = new Grid();
        resolvedGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        resolvedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resolvedGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_tree, 0);
        resolvedGrid.Children.Add(_tree);
        var rowSplitter = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Rows };
        rowSplitter.SetResourceReference(BackgroundProperty, "Theme.Separator");
        Grid.SetRow(rowSplitter, 1);
        resolvedGrid.Children.Add(rowSplitter);
        Grid.SetRow(_properties, 2);
        resolvedGrid.Children.Add(_properties);

        // Reiter "Wie geschrieben": Originaltext, darunter die Verweise darin als Links (Plan-Ruling 5).
        var writtenPanel = new DockPanel();
        var referencesScroll = new ScrollViewer { Content = _references, MaxHeight = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        DockPanel.SetDock(referencesScroll, Dock.Bottom);
        writtenPanel.Children.Add(referencesScroll);
        writtenPanel.Children.Add(_written);

        // Reiter "Aufgelöster Code".
        var copy = new Button
        {
            Content = GuiTexts.InspectorCopy,
            ToolTip = GuiTexts.InspectorCopyTip,
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        copy.Click += (_, _) => CopyResolvedCode();
        var codePanel = new DockPanel();
        DockPanel.SetDock(copy, Dock.Top);
        codePanel.Children.Add(copy);
        codePanel.Children.Add(_resolvedCode);

        // Reiter "Beziehungen": oben Instanz/abgeleitete Typen/Blöcke, darunter die (virtualisierte) Liste der Verwendungen.
        _usages.ToolTip = GuiTexts.RelationsUsagesTip;
        _usages.MouseDoubleClick += (_, _) =>
        {
            if (_usages.SelectedItem is UsageRow row)
            {
                OpenLocation(row.Usage.File.FullPath, row.Usage.Node.Line);
            }
        };
        var inspectUsage = new MenuItem { Header = GuiTexts.ContextInspect };
        inspectUsage.Click += (_, _) =>
        {
            if (_usages.SelectedItem is UsageRow row)
            {
                Inspect(GuiInspection.TargetFor(_index, row.Usage));
            }
        };
        _usages.ContextMenu = new ContextMenu { Items = { inspectUsage } };
        var relationsPanel = new DockPanel();
        var summary = new ScrollViewer { Content = _relations, MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        DockPanel.SetDock(summary, Dock.Top);
        relationsPanel.Children.Add(summary);
        DockPanel.SetDock(_usagesHeader, Dock.Top);
        relationsPanel.Children.Add(_usagesHeader);
        relationsPanel.Children.Add(_usages);

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = GuiTexts.InspectorResolved, Content = resolvedGrid });
        tabs.Items.Add(new TabItem { Header = GuiTexts.InspectorWritten, Content = writtenPanel });
        tabs.Items.Add(new TabItem { Header = GuiTexts.InspectorCode, Content = codePanel });
        tabs.Items.Add(new TabItem { Header = GuiTexts.InspectorRelations, Content = relationsPanel });

        // Vorschau mit denselben Schaltern wie im Designer; Schalter rechnen nur das Layout neu.
        _showConditional.Content = GuiTexts.DesignerShowConditional;
        _showConditional.ToolTip = GuiTexts.DesignerShowConditionalTip;
        _showHidden.Content = GuiTexts.DesignerShowHidden;
        _showHidden.ToolTip = GuiTexts.DesignerShowHiddenTip;
        _listCount.ToolTip = GuiTexts.DesignerListCountTip;
        for (var i = 1; i <= 10; i++)
        {
            _listCount.Items.Add(i);
        }

        _listCount.SelectedItem = 3;
        _showConditional.Click += (_, _) => Relayout();
        _showHidden.Click += (_, _) => Relayout();
        _listCount.SelectionChanged += (_, _) => Relayout();
        var fit = new Button
        {
            Content = GuiTexts.DesignerFit,
            ToolTip = GuiTexts.DesignerFitTip,
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(0, 2, 4, 2),
        };
        fit.Click += (_, _) =>
        {
            _canvas.FitToView();
            _canvas.InvalidateVisual();
        };
        var previewBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
        previewBar.Children.Add(_showConditional);
        previewBar.Children.Add(_showHidden);
        previewBar.Children.Add(new TextBlock { Text = GuiTexts.DesignerListCount, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        previewBar.Children.Add(_listCount);
        // Überstand standardmäßig nur am gewählten Element rot umranden (wie im Designer, gemeinsamer Wert der Sitzung; beide Formate).
        var markOverflow = new CheckBox
        {
            Content = GuiTexts.DesignerMarkOverflow,
            ToolTip = GuiTexts.DesignerMarkOverflowTip,
            IsChecked = GuiSettings.MarkAllOverflow,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        markOverflow.Click += (_, _) => _canvas.MarkAllOverflow = GuiSettings.MarkAllOverflow = markOverflow.IsChecked == true;
        _canvas.MarkAllOverflow = GuiSettings.MarkAllOverflow;
        previewBar.Children.Add(markOverflow);
        previewBar.Children.Add(fit);
        _previewMessage.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _canvas.BoxClicked += (_, box) => SelectElement(box.Element);
        var previewArea = new Grid();
        previewArea.Children.Add(_canvas);
        previewArea.Children.Add(_previewMessage);
        _conditionalBannerButton.Click += (_, _) =>
        {
            if (_target?.FilePath is { } file)
            {
                GuiSettings.ShowAllConditionals(file);
                UpdateConditionalState();
            }
        };
        _conditionalBanner = GuiDesignerView.ConditionalBanner(_conditionalBannerText, _conditionalBannerButton);
        var previewPanel = new DockPanel();
        DockPanel.SetDock(previewBar, Dock.Top);
        previewPanel.Children.Add(previewBar);
        DockPanel.SetDock(_conditionalBanner, Dock.Top);
        previewPanel.Children.Add(_conditionalBanner);
        previewPanel.Children.Add(previewArea);

        var main = new Grid { Margin = new Thickness(6, 0, 6, 6) };
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(tabs, 0);
        main.Children.Add(tabs);
        var columnSplitter = new GridSplitter
        {
            Width = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
        };
        columnSplitter.SetResourceReference(BackgroundProperty, "Theme.Separator");
        Grid.SetColumn(columnSplitter, 1);
        main.Children.Add(columnSplitter);
        Grid.SetColumn(previewPanel, 2);
        main.Children.Add(previewPanel);

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(main);
        Content = root;

        GuiSettings.Changed += OnSettingsChanged;
        GuiSettings.HiddenConditionalsChanged += OnHiddenConditionalsChanged;
        GuiSettings.PreviewLanguageChanged += OnPreviewLanguageChanged;
        Closed += (_, _) =>
        {
            GuiSettings.Changed -= OnSettingsChanged;
            GuiSettings.HiddenConditionalsChanged -= OnHiddenConditionalsChanged;
            GuiSettings.PreviewLanguageChanged -= OnPreviewLanguageChanged;
            _run?.Cancel();
            _layoutRun?.Cancel();
        };
    }

    public event EventHandler<GuiLocationEventArgs>? OpenLocationRequested;

    /// <summary>"Element wählen…": das Suchfenster "GUI-Element inspizieren" öffnen.</summary>
    public event EventHandler? PickElementRequested;

    /// <summary>"Im Designer öffnen": Datei und Zeile der gezeigten Definition (Vanilla wird dabei zum Entwurf).</summary>
    public event EventHandler<GuiLocationEventArgs>? OpenInDesignerRequested;

    /// <summary>"Als neuen Typ in Mod übernehmen" für das gezeigte Element (Plan 3c Ruling 7).</summary>
    public event EventHandler<GuiInspectTarget>? TakeOverAsTypeRequested;

    /// <summary>
    /// Vorschau-Einstellungen mit Auflösung und UI-Skalierung aus den Spieleinstellungen (<see cref="GuiPreviewScreen.FromGame"/>), sonst
    /// <see cref="GuiPreviewScreen.Fallback"/> (klassisch 1920×1080, Jomini der Hauptbildschirm; Skalierung 1).
    /// </summary>
    internal LayoutOptions PreviewOptions
    {
        get
        {
            var (width, height, scale) = GuiPreviewScreen.FromGame() ?? GuiPreviewScreen.Fallback();
            return new LayoutOptions
            {
                ShowConditional = _showConditional.IsChecked == true,
                ShowHidden = _showHidden.IsChecked == true,
                ListSampleCount = _listCount.SelectedItem is int count ? count : 3,
                ScreenWidth = width,
                ScreenHeight = height,
                UiScale = scale,
                // Kopie: das Layout läuft im Hintergrund, die Menge kann sich währenddessen ändern.
                HiddenConditionals = new HashSet<string>(GuiSettings.HiddenConditionals, StringComparer.Ordinal),
                // Der Inspektor hat keinen eigenen Umschalter: Sprache des Spiels oder die im Designer gewählte.
                Language = GuiSettings.PreviewLanguage ?? GuiPreviewScreen.GameLanguage(),
            };
        }
    }

    /// <summary>Zeigt ein Element; das bisher gezeigte kommt in den Verlauf für "Zurück", der Verlauf für "Vorwärts" wird verworfen (wie im Browser).</summary>
    public void Inspect(GuiInspectTarget target)
    {
        if (_target is not null)
        {
            _history.Push(_target);
        }

        _forward.Clear();
        Load(target);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Nur ein neuer Arbeitsbereich (Index geladen, Mod gewechselt, Datei gespeichert) ändert das Ergebnis, nicht das Umschalten von Einstellungen.
        if (_target is { } target && !ReferenceEquals(GuiSettings.Workspace, _workspace))
        {
            Load(target);
        }
    }

    /// <summary>Gezeigte Vorschau-Szene (für Tests).</summary>
    internal GuiSchematicScene? PreviewScene { get; private set; }

    /// <summary>Elemente des aufgelösten Baums (für Tests).</summary>
    internal IEnumerable<ResolvedElement> TreeElements => _treeItems.Keys;

    /// <summary>Text des Banners für ausgeblendete bedingte Elemente; null, solange es nicht zu sehen ist (für Tests).</summary>
    internal string? ConditionalBannerText => _conditionalBanner.Visibility == Visibility.Visible ? _conditionalBannerText.Text : null;

    /// <summary>"Element wählen…" (für Tests).</summary>
    internal Button PickButton => _pick;

    /// <summary>"Alle einblenden" im Banner (für Tests).</summary>
    internal Button ConditionalBannerButton => _conditionalBannerButton;

    /// <summary>Gezeigte Marke des Baumeintrags: null nicht bedingt, false bedingt, true ausgeblendet (für Tests).</summary>
    internal bool? TreeMark(ResolvedElement element) => _treeMarks.TryGetValue(element, out var hidden) ? hidden : null;

    /// <summary>Baut das Kontextmenü des Baumeintrags wie beim Rechtsklick (für Tests).</summary>
    internal ContextMenu TreeMenuFor(ResolvedElement element)
    {
        BuildTreeMenu(element);
        return _treeMenu;
    }

    /// <summary>Kontextmenü eines Baumeintrags: "Bedingtes Element ausblenden" bzw. "Wieder einblenden", nur mit Dateipfad (Schlüssel hängen an der Datei).</summary>
    private void BuildTreeMenu(ResolvedElement? element)
    {
        _treeMenu.Items.Clear();
        if (element is null || _target?.FilePath is null || !_conditionalKeys.TryGetValue(element, out var key))
        {
            return;
        }

        var hidden = GuiSettings.HiddenConditionals.Contains(key);
        var item = new MenuItem { Header = new TextBlock { Text = hidden ? GuiTexts.DesignerUnhideConditional : GuiTexts.DesignerHideConditional } };
        item.Click += (_, _) =>
        {
            GuiSettings.SetConditionalHidden(key, !hidden);
            UpdateConditionalState();
        };
        _treeMenu.Items.Add(item);
    }

    /// <summary>Ein Schlüssel kam hinzu oder fiel weg (auch aus einem Designer): Marken und Banner sofort, das Layout neu.</summary>
    private void OnHiddenConditionalsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            UpdateConditionalState();
            Relayout();
        });

    /// <summary>Die Vorschausprache wurde im Designer gewählt: das Layout neu rechnen (die Texte ändern sich).</summary>
    private void OnPreviewLanguageChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Relayout);

    /// <summary>Baummarken und Banner (Zahl der ausgeblendeten Schlüssel der Datei des gezeigten Elements) zum aktuellen Stand.</summary>
    private void UpdateConditionalState()
    {
        foreach (var (element, treeItem) in _treeItems)
        {
            bool? mark = _conditionalKeys.TryGetValue(element, out var key) ? GuiSettings.HiddenConditionals.Contains(key) : null;
            bool? shown = _treeMarks.TryGetValue(element, out var current) ? current : null;
            if (mark == shown)
            {
                continue;
            }

            treeItem.Header = HeaderFor(element, mark);
            if (mark is { } hidden)
            {
                _treeMarks[element] = hidden;
            }
            else
            {
                _treeMarks.Remove(element);
            }
        }

        var count = _target?.FilePath is { } file ? GuiSettings.HiddenCount(file) : 0;
        _conditionalBannerText.Text = count > 0 ? GuiTexts.DesignerHiddenConditionals(count) : string.Empty;
        _conditionalBanner.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Load(GuiInspectTarget target)
    {
        _target = target;
        _inspection = null;

        // Banner gleich zur neuen Datei: schlägt der Lauf fehl, bliebe sonst das der vorigen Datei stehen (und "Alle einblenden" träfe die neue).
        UpdateConditionalState();
        _back.IsEnabled = _history.Count > 0;
        _forwardButton.IsEnabled = _forward.Count > 0;
        _run?.Cancel();
        _layoutRun?.Cancel();
        var run = new CancellationTokenSource();
        _run = run;
        var workspace = GuiSettings.Workspace;
        _workspace = workspace;
        _index = workspace?.Index ?? GuiIndex.Empty;
        var index = _index;
        var resolver = workspace?.CreateResolver(target.Document, target.FilePath)
            ?? new GuiResolver(GuiIndex.Empty, GuiRules.Empty, target.Document, target.FilePath);
        var createEngine = GuiSettings.LayoutEngineFactory(workspace);
        var options = PreviewOptions;

        _title.Text = GuiElementTree.LabelOf(target.Node);
        _openInDesigner.IsEnabled = target.FilePath is { } file && file.EndsWith(".gui", StringComparison.OrdinalIgnoreCase);
        // Klassisch (HOI IV, EU IV) gibt es keine Typen: gesperrt, der Tooltip sagt warum.
        _takeOverAsType.IsEnabled = GuiSettings.TypeCopyOffered && GuiTypeEdits.CopySource(index, target, workspace?.Rules ?? GuiRules.Empty) is not null;
        _takeOverAsType.ToolTip = GuiSettings.TypeCopyOffered ? GuiTexts.InspectorTakeOverAsTypeTip : GuiTexts.ClassicNoTypes;
        ToolTipService.SetShowOnDisabled(_takeOverAsType, true);
        Title = GuiTexts.InspectorTitleWith(_title.Text);
        FillLocation(target);
        _chain.Inlines.Clear();
        _status.Text = GuiTexts.PreviewRunning;

        Task.Run(
            () =>
            {
                var inspection = GuiInspection.Create(resolver, index, target, run.Token);
                var preview = inspection.Element is null
                    ? null
                    : GuiPreviewBuilder.Layout(inspection.Element, target.Node, target.Document, createEngine, options, run.Token, target.FilePath);

                // Zeilen der Verwendungsliste im Hintergrund: Grundtypen wie hbox oder button haben zehntausende Verwendungen.
                var rows = inspection.Usages.Select(usage => new UsageRow(usage, UsageText(usage))).ToList();
                return (Inspection: inspection, Preview: preview, Rows: rows);
            },
            run.Token).ContinueWith(
            task =>
            {
                if (run.IsCancellationRequested)
                {
                    return;
                }

                if (task.IsFaulted)
                {
                    _status.Text = GuiTexts.PreviewFailed(task.Exception!.GetBaseException().Message);
                    return;
                }

                if (!task.IsCompletedSuccessfully)
                {
                    return;
                }

                Fill(task.Result.Inspection, task.Result.Preview, task.Result.Rows);
                _status.Text = GuiSettings.PreviewStatus;

                // Während des Laufs aus- oder eingeblendet: Relayout kam zu früh (noch kein Element), jetzt mit dem aktuellen Stand nachholen.
                if (!options.HiddenConditionals.SetEquals(GuiSettings.HiddenConditionals))
                {
                    Relayout();
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void Fill(GuiInspection inspection, GuiPreview? preview, IReadOnlyList<UsageRow> rows)
    {
        _inspection = inspection;
        _usageRows = rows;
        _rowByUsage = new Dictionary<GuiUsage, UsageRow>(ReferenceEqualityComparer.Instance);
        foreach (var row in rows)
        {
            _rowByUsage[row.Usage] = row;
        }

        FillChain(inspection);
        FillTree(inspection.Element);
        _written.Text = inspection.WrittenText;
        FillReferences(inspection);
        _resolvedCode.Text = inspection.ResolvedCode ?? GuiTexts.InspectorNoResolved;
        FillRelations(inspection);
        ShowPreview(preview, keepView: false);
    }

    private void FillLocation(GuiInspectTarget target)
    {
        _location.Inlines.Clear();
        var kind = target.Node.Kind switch
        {
            GuiNodeKind.TypeDefinition => GuiTexts.KindOf(GuiDefinitionKind.Type),
            GuiNodeKind.TemplateDefinition => GuiTexts.KindOf(GuiDefinitionKind.Template),
            _ => GuiTexts.KindWidget,
        };
        if (target.FilePath is not { } path)
        {
            _location.Inlines.Add(new Run(kind));
            return;
        }

        var shown = _index.FileOf(target.Node)?.RelativePath ?? System.IO.Path.GetFileName(path);
        _location.Inlines.Add(Link(GuiTexts.InspectorLocation(kind, shown, target.Node.Line), () => OpenLocation(path, target.Node.Line)));
    }

    private void FillChain(GuiInspection inspection)
    {
        _chain.Inlines.Clear();
        if (inspection.TypeChain.Count < 2)
        {
            return;
        }

        _chain.Inlines.Add(new Run(GuiTexts.InspectorChainLabel + " "));
        for (var i = 0; i < inspection.TypeChain.Count; i++)
        {
            if (i > 0)
            {
                _chain.Inlines.Add(new Run(" → "));
            }

            var name = inspection.TypeChain[i];
            _chain.Inlines.Add(i == 0 && inspection.Kind == GuiInspectKind.Type ? new Run(name) : DefinitionLink(name, GuiDefinitionKind.Type));
        }
    }

    private void FillTree(ResolvedElement? root)
    {
        _treeItems.Clear();
        _treeMarks.Clear();
        _tree.Items.Clear();
        _properties.ItemsSource = null;
        if (root is null)
        {
            _tree.Items.Add(new TreeViewItem { Header = GuiTexts.InspectorNoResolved, IsEnabled = false });
            return;
        }

        var item = CreateTreeItem(root, 0);
        _tree.Items.Add(item);
        item.IsSelected = true;
    }

    private TreeViewItem CreateTreeItem(ResolvedElement element, int depth)
    {
        var item = new TreeViewItem { Header = HeaderFor(element, null), Tag = element, IsExpanded = depth < 2 };
        _treeItems.TryAdd(element, item);
        foreach (var child in element.Children)
        {
            item.Items.Add(CreateTreeItem(child, depth + 1));
        }

        return item;
    }

    /// <param name="conditional">null: nicht bedingt; false: bedingt und gezeigt; true: ausgeblendet.</param>
    private static FrameworkElement HeaderFor(ResolvedElement element, bool? conditional)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new Border
        {
            Width = 8,
            Height = 8,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(GuiSchematicCanvas.ColorOf(GuiWidgetKinds.CategoryOf(element.BaseWidget))),
        });
        var label = new TextBlock { Text = element.Name is { Length: > 0 } name ? $"{element.Key} \"{name}\"" : element.Key };
        panel.Children.Add(label);
        if (element.Role == GuiRole.TypeInstance)
        {
            var detail = new TextBlock { Text = "  " + GuiWidgetKinds.Normalize(element.BaseWidget), FontStyle = FontStyles.Italic };
            detail.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            panel.Children.Add(detail);
        }

        GuiDesignerView.MarkConditional(panel, label, conditional);
        return panel;
    }

    private void ShowElement(ResolvedElement element)
    {
        var rows = new ListCollectionView(GuiPropertyRows.Build(element).ToList());
        rows.GroupDescriptions.Add(new PropertyGroupDescription(nameof(GuiPropertyRow.Group)));
        _properties.ItemsSource = rows;
        _canvas.SetSelection(element.Origin.Node);
    }

    /// <summary>Klick in der Vorschau: Baum und Vorschau teilen sich dieselben aufgelösten Elemente (Task 3, <c>GuiPreviewBuilder.Layout</c>).</summary>
    private void SelectElement(ResolvedElement element)
    {
        if (!_treeItems.TryGetValue(element, out var item))
        {
            return;
        }

        for (var parent = item.Parent as TreeViewItem; parent is not null; parent = parent.Parent as TreeViewItem)
        {
            parent.IsExpanded = true;
        }

        item.IsSelected = true;
        item.BringIntoView();
    }

    private void FillReferences(GuiInspection inspection)
    {
        _references.Inlines.Clear();
        if (inspection.References.Count == 0)
        {
            _references.Inlines.Add(new Run(GuiTexts.ReferencesNone));
            return;
        }

        _references.Inlines.Add(new Run(GuiTexts.ReferencesHeader(inspection.References.Count)) { FontWeight = FontWeights.SemiBold });
        foreach (var reference in inspection.References)
        {
            _references.Inlines.Add(new LineBreak());
            _references.Inlines.Add(DefinitionLink(reference.Name, reference.Kind));
            _references.Inlines.Add(new Run(GuiTexts.ReferenceDetail(GuiTexts.KindOf(reference.Kind), reference.At.Line)));
        }
    }

    private void FillRelations(GuiInspection inspection)
    {
        _relations.Children.Clear();
        if (inspection.Subject is not { } subject)
        {
            _relations.Children.Add(Paragraph(new Run(GuiTexts.RelationsEngineWidget)));
            ShowUsages(Array.Empty<GuiUsage>(), null);
            return;
        }

        if (inspection.Kind == GuiInspectKind.Widget)
        {
            _relations.Children.Add(Paragraph(new Run(GuiTexts.RelationsInstanceOf + " "), DefinitionLink(subject, GuiDefinitionKind.Type)));
        }

        if (inspection.SubjectKind == GuiDefinitionKind.Type)
        {
            _relations.Children.Add(Heading(GuiTexts.RelationsDerived(inspection.DerivedTypes.Count)));
            var derived = new List<Inline>();
            foreach (var definition in inspection.DerivedTypes)
            {
                if (derived.Count > 0)
                {
                    derived.Add(new Run(", "));
                }

                var target = GuiInspection.TargetFor(_index, definition);
                derived.Add(Link(definition.Name, () => Inspect(target)));
            }

            _relations.Children.Add(derived.Count > 0 ? Paragraph(derived.ToArray()) : Paragraph(new Run(GuiTexts.RelationsNone)));
        }

        _relations.Children.Add(Heading(GuiTexts.RelationsBlocks(inspection.OfferedBlocks.Count)));
        if (inspection.OfferedBlocks.Count == 0)
        {
            _relations.Children.Add(Paragraph(new Run(GuiTexts.RelationsNone)));
        }

        foreach (var block in inspection.OfferedBlocks)
        {
            var line = new List<Inline> { new Run($"block \"{block.Name}\": {GuiTexts.RelationsOverridden(block.OverriddenAt.Count)}") };
            if (block.OverriddenAt.Count > 0)
            {
                line.Add(new Run("  "));
                line.Add(Link(GuiTexts.RelationsShowOverrides, () => ShowUsages(block.OverriddenAt, block.Name)));
            }

            _relations.Children.Add(Paragraph(line.ToArray()));
        }

        ShowUsages(inspection.Usages, null);
    }

    /// <summary>
    /// Füllt die Verwendungsliste: alle Verwendungen (die im Hintergrund gebauten Zeilen) oder nur die Überschreibungen eines Blocks, dann mit
    /// Link zurück zu allen.
    /// </summary>
    private void ShowUsages(IReadOnlyList<GuiUsage> usages, string? block)
    {
        _usagesHeader.Inlines.Clear();
        _usagesHeader.Inlines.Add(new Run(block is null ? GuiTexts.RelationsUsages(usages.Count) : GuiTexts.RelationsOverridesOf(block, usages.Count))
        {
            FontWeight = FontWeights.SemiBold,
        });
        if (block is not null && _inspection is { } inspection)
        {
            _usagesHeader.Inlines.Add(new Run("  "));
            _usagesHeader.Inlines.Add(Link(GuiTexts.RelationsShowAll, () => ShowUsages(inspection.Usages, null)));
        }

        _usages.ItemsSource = block is null
            ? _usageRows
            : usages.Select(usage => _rowByUsage.TryGetValue(usage, out var row) ? row : new UsageRow(usage, UsageText(usage))).ToList();
    }

    private static string UsageText(GuiUsage usage) =>
        $"{usage.File.RelativePath}:{usage.Node.Line}   {GuiInspection.ContextOf(usage.Node, GuiSettings.Layout)}"
        + (usage.File.Layer == GuiLayer.Vanilla ? string.Empty : $"   [{usage.File.Label}]");

    private void ShowPreview(GuiPreview? preview, bool keepView)
    {
        _canvas.DarkBackground = _workspace is { } workspace && !workspace.Rules.Layout.IsClassic;
        GuiSchematicCanvas.ApplyMessageForeground(_previewMessage, _canvas.DarkBackground);
        _canvas.SetScene(preview?.Scene, keepView, _workspace?.Classic?.Fonts);
        PreviewScene = preview?.Scene;
        _conditionalKeys = preview?.Scene?.Root.ConditionalKeys ?? new Dictionary<ResolvedElement, string>();
        UpdateConditionalState();
        _previewMessage.Text = preview?.Scene is not null
            ? string.Empty
            : _inspection?.Kind == GuiInspectKind.Template ? GuiTexts.PreviewTemplate : GuiTexts.PreviewImpossible;
        if (_tree.SelectedItem is TreeViewItem { Tag: ResolvedElement element })
        {
            _canvas.SetSelection(element.Origin.Node);
        }
    }

    /// <summary>Anzahl der gestarteten Layout-Neuberechnungen (für Tests).</summary>
    internal int LayoutRuns { get; private set; }

    private void Relayout()
    {
        if (_inspection?.Element is not { } element || _target is not { } target)
        {
            return;
        }

        LayoutRuns++;
        _layoutRun?.Cancel();
        var run = new CancellationTokenSource();
        _layoutRun = run;
        var createEngine = GuiSettings.LayoutEngineFactory(_workspace);
        var options = PreviewOptions;
        Task.Run(() => GuiPreviewBuilder.Layout(element, target.Node, target.Document, createEngine, options, run.Token, target.FilePath), run.Token).ContinueWith(
            task =>
            {
                if (!run.IsCancellationRequested && task.IsCompletedSuccessfully)
                {
                    ShowPreview(task.Result, keepView: true);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Link auf eine Typ- oder Template-Definition; reiner Text, wenn es keine gibt (Engine-Widget).</summary>
    private Inline DefinitionLink(string name, GuiDefinitionKind kind) =>
        GuiInspection.TargetFor(_index, name, kind, _target) is { } target ? Link(name, () => Inspect(target)) : new Run(name);

    private static Hyperlink Link(string text, Action onClick)
    {
        var link = new Hyperlink(new Run(text));
        link.SetResourceReference(TextElement.ForegroundProperty, "Theme.Accent");
        link.Click += (_, _) => onClick();
        return link;
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2) };

    private static TextBlock Paragraph(params Inline[] inlines)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        block.Inlines.AddRange(inlines);
        return block;
    }

    private static TextBox CodeBox() => new()
    {
        IsReadOnly = true,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
    };

    private void CopyResolvedCode()
    {
        if (_inspection?.ResolvedCode is not { Length: > 0 } code)
        {
            return;
        }

        try
        {
            Clipboard.SetText(code);
        }
        catch (ExternalException)
        {
            // Zwischenablage kurz von einem anderen Programm belegt: nichts kopiert, der Nutzer kann es erneut versuchen.
        }
    }

    private void OpenLocation(string? path, int line)
    {
        if (path is not null)
        {
            OpenLocationRequested?.Invoke(this, new GuiLocationEventArgs(path, line));
        }
    }
}
