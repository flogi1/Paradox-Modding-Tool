namespace ParadoxModTool.Settings;

/// <summary>Die Kategorien des Einstellungsfensters in Anzeigereihenfolge. Der Name wird gespeichert (<see cref="AppSettings.SettingsWindowCategory"/>).</summary>
public enum SettingsCategory
{
    General,
    Toolbar,
    Workspace,
    WorkspaceToolbar,
    NotesPanel,
    Explorer,
    Compatch,
    GuiTools,
    ErrorLog,
    SaveExplorer,
    Layout,
    Language,
    Themes,
    Hotkeys,
    BugTracker,
    Update,
}

public static class SettingsCategories
{
    public static IReadOnlyList<SettingsCategory> All { get; } = Enum.GetValues<SettingsCategory>();

    /// <summary>Kategorie zum gespeicherten Namen (genau, Groß-/Kleinschreibung zählt); Unbekanntes, Zahlen und leer ergeben <see cref="SettingsCategory.General"/>.</summary>
    public static SettingsCategory Parse(string? id) =>
        All.Where(category => category.ToString() == id).DefaultIfEmpty(SettingsCategory.General).First();
}
