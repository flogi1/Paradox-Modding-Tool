using ParadoxModTool.Settings;
using ParadoxModTool.SettingsUi.Pages;

namespace ParadoxModTool.SettingsUi;

/// <summary>Baut die Seite einer Kategorie. Jede Seite wird beim Anzeigen neu gebaut und beim Wechsel wieder freigegeben.</summary>
public static class SettingsPages
{
    public static SettingsPage Create(SettingsCategory category, ISettingsHost host) => category switch
    {
        SettingsCategory.General => new GeneralPage(host),
        SettingsCategory.Tiles => new TilesPage(host),
        SettingsCategory.Toolbars => new ToolbarsPage(host),
        SettingsCategory.Workspace => new WorkspacePage(host),
        SettingsCategory.NotesPanel => new NotesPanelPage(host),
        SettingsCategory.Explorer => new ExplorerPage(host),
        SettingsCategory.Compatch => new CompatchPage(host),
        SettingsCategory.GuiTools => new GuiToolsPage(host),
        SettingsCategory.ErrorLog => new ErrorLogPage(host),
        SettingsCategory.SaveExplorer => new SaveExplorerPage(host),
        SettingsCategory.Layout => new LayoutPage(host),
        SettingsCategory.Language => new LanguagePage(host),
        SettingsCategory.Themes => new ThemesPage(host),
        SettingsCategory.Hotkeys => new HotkeysPage(host),
        SettingsCategory.BugTracker => new BugTrackerPage(host),
        SettingsCategory.Update => new UpdatePage(host),
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
}
