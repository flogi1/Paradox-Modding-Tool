using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.Notes;
using ParadoxModTool.Settings;
using ParadoxModTool.Shell;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace ParadoxModTool.App;

/// <summary>
/// Fensterlayout (Interface-Rework 2): schaltet Menüleiste, Kachelleiste, kleine Werkzeugleiste und die Leisten der Arbeitsbereiche
/// zwischen dem neuen Layout ("Tiles") und dem klassischen ("Classic") um, ohne Neustart. Jede Änderung am Modell läuft über
/// <see cref="ApplySetting"/> hierher. Die Elemente der kleinen Leiste bleiben die aus <c>ToolbarElementPool</c> (MainWindow.xaml);
/// <see cref="ToolbarBuilder"/> ordnet sie nach <see cref="AppSettings.ToolbarOrder"/> und Position an.
/// </summary>
public partial class MainWindow
{
    private ToolbarPositionGrip? _toolbarGrip;
    private int _bugTrackerUnseen;
    private Button? _launchOptionMenuButton;
    private Button? _playsetMenuButton;

    private bool IsTilesLayout => _settings.WindowLayout == "Tiles";

    /// <summary>Senkrechte Leiste: nur im neuen Layout und nur links oder rechts.</summary>
    private bool ToolbarVertical => IsTilesLayout && _settings.ToolbarPosition is "Left" or "Right";

    /// <summary>Menüpunkte, die es nur im klassischen Layout gibt (alle Werkzeuge, Mod-Projekt, Unterstützen).</summary>
    private IEnumerable<UIElement> ClassicOnlyMenuItems => new UIElement[]
    {
        ProjectMenuItem, CuToolMenuItem, PatchRecipesMenuItem, GeneratorPackagesMenuItem, PackageEditorMenuItem, ModCompatMenuItem, PlaysetsMenuItem,
        CompatchMenuItem, ErrorLogMenuItem, DesyncMenuItem, SaveExplorerMenuItem, ScriptDocsMenuItem, GuiMenuItem, TemplatesMenuItem,
        SupportMenuItem,
    };

    /// <summary>Menüpunkte, die das Spiel nicht unterstützt, bleiben auch im klassischen Layout verborgen (Desync-Analyse nur mit <c>desyncRules</c>).</summary>
    private bool IsMenuItemAvailable(UIElement item) => item != DesyncMenuItem || DesyncAvailable;

    /// <summary>Elemente, die es nur im neuen Layout gibt (senkrechte Trennstriche, Abschnitt "Unterstützen" in Hilfe).</summary>
    private IEnumerable<UIElement> TilesOnlyMenuItems => new UIElement[]
    {
        MenuSeparator1, MenuSeparator1b, PlaysetsBarMenuItem, MenuSeparator2, MenuSeparator3, MenuSeparator4, HelpSupportSeparator, HelpSupportHeader, HelpSponsorMenuItem, HelpKofiMenuItem,
    };

    /// <summary>Wendet das gewählte Fensterlayout auf alle Bereiche an.</summary>
    private void ApplyWindowLayout()
    {
        ApplyMenuLayout();
        ApplyTileBar();
        ApplyToolbarPosition();
        ApplyPaneToolbarLayout();
    }

    /// <summary>Kachelleiste nur im neuen Layout und nur, wenn eingeschaltet.</summary>
    private void ApplyTileBar() =>
        TileBarControl.Visibility = IsTilesLayout && _settings.TileBarVisible ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Leisten beider Arbeitsbereiche: klassisch mit Dateiknöpfen, sonst mit den Schaltern aus den Einstellungen.</summary>
    private void ApplyPaneToolbarLayout()
    {
        foreach (var pane in AllPanes)
        {
            pane.SetToolbarLayout(
                classic: !IsTilesLayout,
                _settings.WorkspaceCategoryButtonVisible,
                _settings.WorkspaceFilterButtonVisible,
                _settings.WorkspaceIconButtonVisible,
                _settings.WorkspaceSortButtonVisible,
                _settings.WorkspaceMarkButtonsVisible);
        }
    }

    /// <summary>Menüleiste: sechs Grundpunkte (mit Playsets hinter der Spielauswahl) mit Trennstrichen im neuen Layout, alle Werkzeuge im klassischen; "Mods" trägt im neuen Layout das Spielkürzel.</summary>
    private void ApplyMenuLayout()
    {
        var tiles = IsTilesLayout;
        foreach (var item in ClassicOnlyMenuItems)
        {
            item.Visibility = tiles || !IsMenuItemAvailable(item) ? Visibility.Collapsed : Visibility.Visible;
        }

        foreach (var item in TilesOnlyMenuItems)
        {
            item.Visibility = tiles ? Visibility.Visible : Visibility.Collapsed;
        }

        ModsMenuItem.Header = tiles ? MenuTexts.MyGameMods(GameContext.Profile?.EffectiveShortName ?? GameContext.GameId) : MenuTexts.Mods;
    }

    // ---- Kleine Werkzeugleiste ----

    /// <summary>Hängt die Leiste an den gewählten Rand (klassisch immer oben) und baut sie neu.</summary>
    private void ApplyToolbarPosition()
    {
        var position = IsTilesLayout ? _settings.ToolbarPosition : "Top";
        DockPanel.SetDock(ToolbarHost, Enum.Parse<Dock>(position));
        // Oben: vor Status- und Ergebnisleiste, damit eine oben angedockte Ergebnisleiste unter der Leiste bleibt (wie bisher). Sonst nach
        // beiden, damit die senkrechte Leiste zwischen Kachel- und Statusleiste steht.
        if (ToolbarHost.Parent is DockPanel dock)
        {
            dock.Children.Remove(ToolbarHost);
            dock.Children.Insert(position == "Top" ? dock.Children.IndexOf(ModStatusBar) : dock.Children.Count - 1, ToolbarHost);
        }

        ToolbarHost.BorderThickness = position switch
        {
            "Bottom" => new Thickness(0, 1, 0, 0),
            "Left" => new Thickness(0, 0, 1, 0),
            "Right" => new Thickness(1, 0, 0, 0),
            _ => new Thickness(0, 0, 0, 1),
        };
        RebuildToolbar();
    }

    /// <summary>
    /// Baut die kleine Leiste aus den Pool-Elementen: im neuen Layout nach <see cref="AppSettings.ToolbarOrder"/> mit Griff und
    /// Drag-and-drop, klassisch nach <see cref="ToolbarCatalog.DefaultOrder"/> ohne beides. Senkrecht steht sie in einem ScrollViewer.
    /// </summary>
    private void RebuildToolbar()
    {
        EnsureToolbarExtras();
        ApplyToolbarContent();

        var tiles = IsTilesLayout;
        var order = tiles ? _settings.ToolbarOrder : ToolbarCatalog.DefaultOrder;
        var panel = ToolbarBuilder.Build(new ToolbarBuild(
            order,
            id => OfferedInToolbar(id) && (ToolbarCatalog.IsSeparator(id) || (tiles ? _settings.IsToolbarItemVisible(id) : _settings.IsToolbarItemVisibleInClassic(id))),
            ToolbarElementFor,
            ToolbarVertical,
            tiles ? _toolbarGrip : null,
            tiles ? MoveToolbarItem : null));

        ToolbarHost.Child = ToolbarVertical
            ? new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }
            : panel;
    }

    /// <summary>Ob ein Eintrag in der Leiste überhaupt angeboten wird: GUI-Datenhelfer nur mit GUI-Werkzeugen, Dateiknöpfe nicht im klassischen Layout.</summary>
    private bool OfferedInToolbar(string id)
    {
        if (ToolbarCatalog.IsSeparator(id))
        {
            return true;
        }

        if (id == ToolbarItemIds.GuiDataHelper && !GuiSettings.ToolsEnabled)
        {
            return false;
        }

        return IsTilesLayout || ToolbarCatalog.All.FirstOrDefault(item => item.Id == id)?.Group != ToolbarGroup.Files;
    }

    /// <summary>Das Element zu einer Kennung; Startoption und Playset sind senkrecht Menü-Knöpfe statt Auswahlfelder.</summary>
    private FrameworkElement? ToolbarElementFor(string id) => id switch
    {
        ToolbarItemIds.LaunchOption => ToolbarVertical ? _launchOptionMenuButton : LaunchOptionsCombo,
        ToolbarItemIds.Playset => ToolbarVertical ? _playsetMenuButton : PlaysetsCombo,
        _ => ToolbarElements().GetValueOrDefault(id),
    };

    private void MoveToolbarItem(string dragged, string target)
    {
        var copy = _settings.ToolbarOrder.ToList();
        if (ReorderList.MoveNextTo(copy, dragged, target))
        {
            _settings.ToolbarOrder = copy;
        }
    }

    /// <summary>Griff und die beiden Menü-Knöpfe für die senkrechte Leiste, einmal angelegt.</summary>
    private void EnsureToolbarExtras()
    {
        if (_toolbarGrip is not null)
        {
            return;
        }

        _toolbarGrip = new ToolbarPositionGrip(() => _settings.ToolbarPosition);
        _toolbarGrip.PositionChosen += (_, position) => _settings.ToolbarPosition = position;
        _launchOptionMenuButton = LaunchMenuButton(ToolbarItemIds.LaunchOption, LaunchOptionsCombo);
        _playsetMenuButton = LaunchMenuButton(ToolbarItemIds.Playset, PlaysetsCombo);
        LaunchOptionsCombo.SelectionChanged += (_, _) => UpdateLaunchMenuButtons();
        PlaysetsCombo.SelectionChanged += (_, _) => UpdateLaunchMenuButtons();
        UpdateLaunchMenuButtons();
    }

    /// <summary>Senkrechte Leiste: Icon-Knopf, der die Einträge des Auswahlfelds als Menü zeigt; der gewählte Wert steht im Tooltip.</summary>
    private static Button LaunchMenuButton(string id, ComboBox combo)
    {
        var button = new Button { Content = ToolbarItemView.Content(id, SettingsTexts.ToolbarItemLabel(id), showLabel: false), Focusable = false, Tag = id };
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu();
            foreach (var item in combo.Items)
            {
                var entry = new MenuItem { Header = ItemLabel(combo, item), IsCheckable = true, IsChecked = ReferenceEquals(combo.SelectedItem, item) };
                var chosen = item;
                entry.Click += (_, _) => combo.SelectedItem = chosen;
                menu.Items.Add(entry);
            }

            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Right;
            menu.IsOpen = true;
        };
        return button;
    }

    private void UpdateLaunchMenuButtons()
    {
        if (_launchOptionMenuButton is { } launch)
        {
            launch.ToolTip = MenuTexts.ToolbarTip(ToolbarItemIds.LaunchOption) + SelectedSuffix(LaunchOptionsCombo);
        }

        if (_playsetMenuButton is { } playset)
        {
            playset.ToolTip = MenuTexts.ToolbarTip(ToolbarItemIds.Playset) + SelectedSuffix(PlaysetsCombo);
        }
    }

    private static string SelectedSuffix(ComboBox combo) => combo.SelectedItem is { } selected ? "\n" + ItemLabel(combo, selected) : string.Empty;

    /// <summary>Anzeigetext eines Combo-Eintrags über dessen <see cref="ItemsControl.DisplayMemberPath"/>.</summary>
    private static string ItemLabel(ComboBox combo, object item) =>
        (string.IsNullOrEmpty(combo.DisplayMemberPath) ? null : TypeDescriptor.GetProperties(item)[combo.DisplayMemberPath]?.GetValue(item)?.ToString())
        ?? item.ToString() ?? string.Empty;

    /// <summary>Beschriftung eines Eintrags; der Bugtracker trägt die Zahl neuer Meldungen und ⚠ bei einem fehlgeschlagenen Abruf.</summary>
    private string ToolbarLabelFor(string id)
    {
        var label = IsTilesLayout ? SettingsTexts.ToolbarItemLabel(id) : MenuTexts.ToolbarLabel(id);
        if (id != ToolbarItemIds.BugTracker)
        {
            return label;
        }

        var count = _bugTrackerUnseen;
        return (count > 0 ? $"{label} ({BugTexts.NewCount(count)})" : label) + (BugRefreshError(ActiveBugProjectId) is not null ? " ⚠" : string.Empty);
    }

    /// <summary>
    /// Inhalt und Tooltip aller Pool-Elemente nach Layout: im neuen Layout Icon plus Beschriftung (waagerecht, wenn eingeschaltet), klassisch
    /// der Text mit Emoji-Präfix. Rückgängig/Wiederholen bekommen danach wieder ihre dynamischen Tooltips.
    /// </summary>
    private void ApplyToolbarContent()
    {
        foreach (var (id, element) in ToolbarElements())
        {
            if (element is ContentControl control)
            {
                control.Content = IsTilesLayout ? ToolbarItemView.Content(id, ToolbarLabelFor(id), ShowToolbarLabels) : ToolbarLabelFor(id);
            }

            element.ToolTip = ToolbarTipFor(id);
        }

        UpdateUndoRedoButtons();
    }

    /// <summary>
    /// Nur der Bugtracker-Knopf nach einer Änderung der Zahl oder des Abruf-Fehlers: Beschriftung und Tooltip an Ort und Stelle statt
    /// alle Inhalte neu zu bauen. Ein noch nicht mit Icon gebauter Inhalt (vor dem ersten <see cref="RebuildToolbar"/>) wird gebaut.
    /// </summary>
    private void UpdateBugTrackerToolbarItem()
    {
        const string id = ToolbarItemIds.BugTracker;
        if (ToolbarElements().GetValueOrDefault(id) is not { } element)
        {
            return;
        }

        if (element is ContentControl control)
        {
            var label = ToolbarLabelFor(id);
            if (!IsTilesLayout)
            {
                control.Content = label;
            }
            else if (control.Content is not FrameworkElement content || !ToolbarItemView.SetLabel(content, label))
            {
                control.Content = ToolbarItemView.Content(id, label, ShowToolbarLabels);
            }
        }

        element.ToolTip = ToolbarTipFor(id);
    }

    /// <summary>Beschriftungen neben den Icons: nur waagerecht und wenn eingeschaltet.</summary>
    private bool ShowToolbarLabels => !ToolbarVertical && _settings.ToolbarLabelsVisible;

    /// <summary>Tooltip eines Eintrags; der Bugtracker bekommt bei einem fehlgeschlagenen Abruf eine Zeile dazu.</summary>
    private string ToolbarTipFor(string id) =>
        id == ToolbarItemIds.BugTracker && BugRefreshError(ActiveBugProjectId) is not null
            ? MenuTexts.ToolbarTip(id) + "\n" + BugTexts.RefreshFailedTip
            : MenuTexts.ToolbarTip(id);

    // ---- Dateiknöpfe der kleinen Leiste: wirken auf den fokussierten Arbeitsbereich ----

    private void ToolbarNew_Click(object sender, RoutedEventArgs e) => ActivePane.NewDocument(_activeHighlighting);

    private void ToolbarOpen_Click(object sender, RoutedEventArgs e) => OpenFileInto(ActivePane);

    private void ToolbarSave_Click(object sender, RoutedEventArgs e) => SaveActiveDocumentOf(ActivePane);

    private void ToolbarSaveAll_Click(object sender, RoutedEventArgs e) => SaveAllDocuments();

    private void ToolbarClose_Click(object sender, RoutedEventArgs e) => ActivePane.CloseActiveDocument();

    private void ToolbarMove_Click(object sender, RoutedEventArgs e)
    {
        var pane = ActivePane;
        if (pane.ActiveDocument is { } document)
        {
            MoveDocumentBetweenPanes(pane, ReferenceEquals(pane, LeftEditor) ? RightEditor : LeftEditor, document, null);
        }
    }
}
