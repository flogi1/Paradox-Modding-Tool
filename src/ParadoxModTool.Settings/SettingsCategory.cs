namespace ParadoxModTool.Settings;

/// <summary>Die Kategorien des Einstellungsfensters in Anzeigereihenfolge. Der Name wird gespeichert (<see cref="AppSettings.SettingsWindowCategory"/>).</summary>
public enum SettingsCategory
{
    General,
    Layout,
    Tiles,
    Toolbars,
    Workspace,
    NotesPanel,
    Explorer,
    Compatch,
    GuiTools,
    ErrorLog,
    SaveExplorer,
    BugTracker,
    Language,
    Themes,
    Hotkeys,
    Update,
}

public static class SettingsCategories
{
    public static IReadOnlyList<SettingsCategory> All { get; } = Enum.GetValues<SettingsCategory>();

    /// <summary>
    /// Kategorie zum gespeicherten Namen (genau, Groß-/Kleinschreibung zählt); Unbekanntes, Zahlen und leer ergeben
    /// <see cref="SettingsCategory.General"/>. Die alten Namen <c>Toolbar</c> und <c>WorkspaceToolbar</c> ergeben <see cref="SettingsCategory.Toolbars"/>.
    /// </summary>
    public static SettingsCategory Parse(string? id) => id switch
    {
        "Toolbar" or "WorkspaceToolbar" => SettingsCategory.Toolbars,
        _ => All.Where(category => category.ToString() == id).DefaultIfEmpty(SettingsCategory.General).First(),
    };
}
