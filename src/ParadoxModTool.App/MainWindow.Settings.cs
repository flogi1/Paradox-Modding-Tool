using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ParadoxModTool.Editor;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.Settings;
using ParadoxModTool.Theming;

namespace ParadoxModTool.App;

/// <summary>
/// Einstellungs-Modell des Hauptfensters (<see cref="AppSettings"/>): Werkzeugleisten-Schalter und Einstellungsfenster schreiben nur ins
/// Modell; jede Änderung wird hier angewendet.
/// </summary>
public partial class MainWindow
{
    // Interface-Rework 2: alte Sitzungen ohne WindowLayout blenden die vier Kachel-Werkzeuge aus der kleinen Leiste aus (sie sind Kacheln).
    private readonly AppSettings _settings = new(migrateTileTools: true);

    /// <summary>Reihenfolge beim Anwenden aller Werte nach dem Laden (Panel-Sichtbarkeit hängt von zwei Werten ab, daher zuletzt).</summary>
    private static readonly string[] ApplyOrder =
    {
        nameof(AppSettings.WindowLayout),
        nameof(AppSettings.SplitViewEnabled),
        nameof(AppSettings.ExplorerVisible),
        nameof(AppSettings.ResultsPanelOnTop),
        nameof(AppSettings.ColorizeWholeTab),
        nameof(AppSettings.MultiRowTabs),
        nameof(AppSettings.PathBarVisible),
        nameof(AppSettings.DocumentBarVisible),
        nameof(AppSettings.WorkspaceFilterButtonVisible),
        nameof(AppSettings.NotesPanelVisible),
        nameof(AppSettings.TileBarVisible),
        nameof(AppSettings.WorkspaceCategoryButtonVisible),
        nameof(AppSettings.WorkspaceSortButtonVisible),
        nameof(AppSettings.WorkspaceMarkButtonsVisible),
        nameof(AppSettings.ToolbarPosition),
        nameof(AppSettings.ToolbarOrder),
        nameof(AppSettings.ToolbarLabelsVisible),
        nameof(AppSettings.HiddenToolbarItems),
        nameof(AppSettings.IconColorsByTheme),
        nameof(AppSettings.CompatchResultRight),
        nameof(AppSettings.CompatchKindsSideBySide),
        nameof(AppSettings.ExplorerButtonsOnLeft),
        nameof(AppSettings.ExplorerShowErrorMarks),
        nameof(AppSettings.ExplorerSearchFoldersOnly),
        nameof(AppSettings.ExplorerHideUgcReferences),
    };

    /// <summary>Einmal im Konstruktor: jede Änderung am Modell wird sofort angewendet.</summary>
    private void InitializeSettings()
    {
        _settings.Changed += (_, name) => ApplySetting(name);
        _settings.Reloaded += (_, _) => ApplyAllSettings();
        ExplorerPanelControl.OptionsChanged += (_, _) =>
        {
            if (_applyingExplorerOptions)
            {
                return;
            }

            _settings.ExplorerButtonsOnLeft = ExplorerPanelControl.ButtonsOnLeft;
            _settings.ExplorerShowErrorMarks = ExplorerPanelControl.ShowErrorMarks;
            _settings.ExplorerSearchFoldersOnly = ExplorerPanelControl.SearchFoldersOnly;
        };
        ThemeManager.ThemeChanged += (_, _) => ApplyIconColors();
    }

    /// <summary>Während die Einstellungen in die Übersicht übertragen werden, schreibt die Übersicht nichts zurück (sonst überschrieben halb übernommene Werte die geladenen).</summary>
    private bool _applyingExplorerOptions;

    /// <summary>Schalter der Verzeichnis-Übersicht aus den Einstellungen (Einstellungsfenster und Werkzeugleiste der Übersicht sind dieselbe Einstellung).</summary>
    private void ApplyExplorerOptions()
    {
        _applyingExplorerOptions = true;
        try
        {
            ExplorerPanelControl.ButtonsOnLeft = _settings.ExplorerButtonsOnLeft;
            ExplorerPanelControl.ShowErrorMarks = _settings.ExplorerShowErrorMarks;
            ExplorerPanelControl.SearchFoldersOnly = _settings.ExplorerSearchFoldersOnly;
            ExplorerPanelControl.HideUgcReferences = _settings.ExplorerHideUgcReferences;
        }
        finally
        {
            _applyingExplorerOptions = false;
        }
    }

    /// <summary>Die beim Start bekannten Themes (für die einmalige Übernahme der alten Notiz-Farbe).</summary>
    private static IReadOnlyCollection<string> KnownThemeIds() =>
        new[] { ThemeDefinition.LightId, ThemeDefinition.DarkId }.Concat(ThemeManager.CustomThemes.Select(t => t.Id)).ToList();

    private void ApplyAllSettings()
    {
        foreach (var name in ApplyOrder)
        {
            ApplySetting(name);
        }
    }

    private void ApplySetting(string name)
    {
        switch (name)
        {
            case nameof(AppSettings.SplitViewEnabled):
                ApplySplitViewState(_settings.SplitViewEnabled);
                SplitViewToggle.IsChecked = _settings.SplitViewEnabled;
                break;
            case nameof(AppSettings.ExplorerVisible):
                ApplyExplorerVisibleState(_settings.ExplorerVisible);
                ExplorerToggle.IsChecked = _settings.ExplorerVisible;
                break;
            case nameof(AppSettings.ResultsPanelOnTop):
                ApplyResultsPanelPosition(_settings.ResultsPanelOnTop);
                break;
            case nameof(AppSettings.ColorizeWholeTab):
                OpenDocument.ColorizeWholeTabEnabled = _settings.ColorizeWholeTab;
                RefreshAllDocumentColors();
                break;
            case nameof(AppSettings.MultiRowTabs):
                ApplyMultiRowTabs(_settings.MultiRowTabs);
                break;
            case nameof(AppSettings.PathBarVisible):
                foreach (var pane in AllPanes)
                {
                    pane.SetPathBarVisible(_settings.PathBarVisible);
                }

                break;
            case nameof(AppSettings.DocumentBarVisible):
                ApplyDocumentBarVisible(_settings.DocumentBarVisible);
                break;
            case nameof(AppSettings.WorkspaceCategoryButtonVisible):
            case nameof(AppSettings.WorkspaceFilterButtonVisible):
            case nameof(AppSettings.WorkspaceIconButtonVisible):
            case nameof(AppSettings.WorkspaceSortButtonVisible):
            case nameof(AppSettings.WorkspaceMarkButtonsVisible):
                ApplyPaneToolbarLayout();
                break;
            case nameof(AppSettings.WindowLayout):
                ApplyWindowLayout();
                break;
            case nameof(AppSettings.TileBarVisible):
                ApplyTileBar();
                break;
            case nameof(AppSettings.NotesPanelVisible):
            case nameof(AppSettings.NotesPanelAlwaysVisible):
                NotesPanelToggle.IsChecked = _settings.NotesPanelVisible;
                UpdateNotesPanelVisibility();
                break;
            case nameof(AppSettings.HiddenToolbarItems):
            case nameof(AppSettings.ToolbarOrder):
            case nameof(AppSettings.ToolbarLabelsVisible):
                RebuildToolbar();
                break;
            case nameof(AppSettings.ToolbarPosition):
                ApplyToolbarPosition();
                break;
            case nameof(AppSettings.ExplorerButtonsOnLeft):
            case nameof(AppSettings.ExplorerShowErrorMarks):
            case nameof(AppSettings.ExplorerSearchFoldersOnly):
            case nameof(AppSettings.ExplorerHideUgcReferences):
                ApplyExplorerOptions();
                break;
            case nameof(AppSettings.CompatchResultRight):
                CompatchWindow.ResultRight = _settings.CompatchResultRight;
                break;
            case nameof(AppSettings.CompatchKindsSideBySide):
                CompatchWindow.KindsSideBySide = _settings.CompatchKindsSideBySide;
                break;
            case nameof(AppSettings.IconColorsByTheme):
                ApplyIconColors();
                break;
        }
    }

    /// <summary>Die Elemente der allgemeinen Werkzeugleiste zu ihren Kennungen (<see cref="ToolbarItemIds"/>).</summary>
    private IReadOnlyDictionary<string, FrameworkElement> ToolbarElements() => new Dictionary<string, FrameworkElement>
    {
        [ToolbarItemIds.New] = ToolbarNewButton,
        [ToolbarItemIds.Open] = ToolbarOpenButton,
        [ToolbarItemIds.Save] = ToolbarSaveButton,
        [ToolbarItemIds.SaveAll] = ToolbarSaveAllButton,
        [ToolbarItemIds.Close] = ToolbarCloseButton,
        [ToolbarItemIds.Move] = ToolbarMoveButton,
        [ToolbarItemIds.Undo] = UndoButton,
        [ToolbarItemIds.Redo] = RedoButton,
        [ToolbarItemIds.Search] = ToolbarSearchButton,
        [ToolbarItemIds.GoToLine] = ToolbarGoToLineButton,
        [ToolbarItemIds.GoToBracket] = ToolbarGoToBracketButton,
        [ToolbarItemIds.ScopeFinder] = ToolbarScopeFinderButton,
        [ToolbarItemIds.GuiDataHelper] = ToolbarGuiDataHelperButton,
        [ToolbarItemIds.Templates] = ToolbarTemplatesButton,
        [ToolbarItemIds.Notes] = ToolbarNotesButton,
        [ToolbarItemIds.Bookmark] = ToolbarBookmarkButton,
        [ToolbarItemIds.Bug] = ToolbarBugButton,
        [ToolbarItemIds.BugTracker] = ToolbarBugTrackerButton,
        [ToolbarItemIds.Format] = ToolbarFormatButton,
        [ToolbarItemIds.SplitView] = SplitViewToggle,
        [ToolbarItemIds.Explorer] = ExplorerToggle,
        [ToolbarItemIds.NotesPanel] = NotesPanelToggle,
        [ToolbarItemIds.AutoComplete] = ScriptAutoCompleteToggle,
        [ToolbarItemIds.Hover] = ScriptHoverToggle,
        [ToolbarItemIds.ModifierReload] = ToolbarModifierReloadButton,
        [ToolbarItemIds.LaunchOption] = LaunchOptionsCombo,
        [ToolbarItemIds.Playset] = PlaysetsCombo,
        [ToolbarItemIds.LaunchGame] = LaunchGameButton,
    };

    /// <summary>Setzt die Symbolfarben für das aktuelle Theme: eigene Farbe, sonst Standard (Lesezeichen/Bug aus dem Theme).</summary>
    private void ApplyIconColors()
    {
        var theme = ThemeManager.Current.Id;
        TabIconBrushes.Set(TabIconBrushes.NoteKey, IconColorOr(theme, IconIds.Note, TabIconBrushes.DefaultNote));
        TabIconBrushes.Set(TabIconBrushes.BookmarkKey, IconColorOr(theme, IconIds.Bookmark, ThemeManager.GetColor("Bookmark")));
        TabIconBrushes.Set(TabIconBrushes.BugKey, IconColorOr(theme, IconIds.Bug, ThemeManager.GetColor("Danger")));
        TabIconBrushes.Set(TabIconBrushes.VanillaKey, IconColorOr(theme, IconIds.Vanilla, TabIconBrushes.DefaultVanilla));
        TabIconBrushes.Set(TabIconBrushes.ModKey, IconColorOr(theme, IconIds.Mod, Colors.Transparent));
    }

    private Color IconColorOr(string themeId, string iconId, Color fallback) =>
        _settings.GetIconColor(themeId, iconId) is { } hex && ColorConverter.ConvertFromString(hex) is Color color ? color : fallback;
}
