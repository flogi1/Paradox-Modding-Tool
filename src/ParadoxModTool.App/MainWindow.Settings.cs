using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ParadoxModTool.Editor;
using ParadoxModTool.Settings;
using ParadoxModTool.Theming;

namespace ParadoxModTool.App;

/// <summary>
/// Einstellungs-Modell des Hauptfensters (<see cref="AppSettings"/>): Werkzeugleisten-Schalter und Einstellungsfenster schreiben nur ins
/// Modell; jede Änderung wird hier angewendet.
/// </summary>
public partial class MainWindow
{
    private readonly AppSettings _settings = new();

    /// <summary>Reihenfolge beim Anwenden aller Werte nach dem Laden (Panel-Sichtbarkeit hängt von zwei Werten ab, daher zuletzt).</summary>
    private static readonly string[] ApplyOrder =
    {
        nameof(AppSettings.SplitViewEnabled),
        nameof(AppSettings.ExplorerVisible),
        nameof(AppSettings.ResultsPanelOnTop),
        nameof(AppSettings.ColorizeWholeTab),
        nameof(AppSettings.MultiRowTabs),
        nameof(AppSettings.PathBarVisible),
        nameof(AppSettings.DocumentBarVisible),
        nameof(AppSettings.WorkspaceFilterButtonVisible),
        nameof(AppSettings.NotesPanelVisible),
        nameof(AppSettings.HiddenToolbarItems),
        nameof(AppSettings.IconColorsByTheme),
    };

    /// <summary>Einmal im Konstruktor: jede Änderung am Modell wird sofort angewendet.</summary>
    private void InitializeSettings()
    {
        _settings.Changed += (_, name) => ApplySetting(name);
        _settings.Reloaded += (_, _) => ApplyAllSettings();
        ThemeManager.ThemeChanged += (_, _) => ApplyIconColors();
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
            case nameof(AppSettings.WorkspaceFilterButtonVisible):
            case nameof(AppSettings.WorkspaceIconButtonVisible):
                foreach (var pane in AllPanes)
                {
                    pane.SetToolbarButtonsVisible(_settings.WorkspaceFilterButtonVisible, _settings.WorkspaceIconButtonVisible);
                }

                break;
            case nameof(AppSettings.NotesPanelVisible):
            case nameof(AppSettings.NotesPanelAlwaysVisible):
                NotesPanelToggle.IsChecked = _settings.NotesPanelVisible;
                UpdateNotesPanelVisibility();
                break;
            case nameof(AppSettings.HiddenToolbarItems):
                ApplyToolbarVisibility();
                break;
            case nameof(AppSettings.IconColorsByTheme):
                ApplyIconColors();
                break;
        }
    }

    /// <summary>Die Elemente der allgemeinen Werkzeugleiste zu ihren Kennungen (<see cref="ToolbarItemIds"/>).</summary>
    private IReadOnlyDictionary<string, FrameworkElement> ToolbarElements() => new Dictionary<string, FrameworkElement>
    {
        [ToolbarItemIds.Undo] = UndoButton,
        [ToolbarItemIds.Redo] = RedoButton,
        [ToolbarItemIds.Search] = ToolbarSearchButton,
        [ToolbarItemIds.GoToLine] = ToolbarGoToLineButton,
        [ToolbarItemIds.GoToBracket] = ToolbarGoToBracketButton,
        [ToolbarItemIds.ScopeFinder] = ToolbarScopeFinderButton,
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
        [ToolbarItemIds.LaunchOption] = LaunchOptionsCombo,
        [ToolbarItemIds.Playset] = PlaysetsCombo,
        [ToolbarItemIds.LaunchGame] = LaunchGameButton,
    };

    /// <summary>
    /// Blendet die Einträge der allgemeinen Werkzeugleiste nach dem Modell ein oder aus. Eine Trennlinie steht nur zwischen zwei Gruppen mit
    /// je mindestens einem sichtbaren Eintrag (<see cref="ToolbarSeparators"/>).
    /// </summary>
    private void ApplyToolbarVisibility()
    {
        foreach (var (id, element) in ToolbarElements())
        {
            element.Visibility = _settings.IsToolbarItemVisible(id) ? Visibility.Visible : Visibility.Collapsed;
        }

        var separators = ToolbarSeparators.VisibleBefore(_settings.IsToolbarItemVisible);
        ToolbarToolsSeparator.Visibility = separators.Contains(ToolbarGroup.Tools) ? Visibility.Visible : Visibility.Collapsed;
        ToolbarTogglesSeparator.Visibility = separators.Contains(ToolbarGroup.Toggles) ? Visibility.Visible : Visibility.Collapsed;
        ToolbarLaunchSeparator.Visibility = separators.Contains(ToolbarGroup.Launch) ? Visibility.Visible : Visibility.Collapsed;
    }

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
