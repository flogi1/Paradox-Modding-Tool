using ParadoxModTool.Localization;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Oberflächentexte der Playset-Auswahl (Keys <c>playset.*</c> in <c>locales/*.json</c>). Der deutsche Text hier ist der Rückfall und
/// muss mit de.json übereinstimmen.
/// </summary>
public static class PlaysetTexts
{
    public static string DisplayActive(string name, int enabled, int total) => F("playset.display.active", "{0} ({1}/{2} Mods, aktiv)", name, enabled, total);

    public static string Display(string name, int enabled, int total) => F("playset.display", "{0} ({1}/{2} Mods)", name, enabled, total);

    public static string FileNotFound(string path) => F("playset.fileNotFound", "Playset-Datei nicht gefunden: \"{0}\".", path);

    public static string UnexpectedFormat => T("playset.unexpectedFormat", "Playset-Datei hat nicht das erwartete Format (kein \"playsets\"-Array gefunden).");

    public static string PlaysetNotFound(string name) => F("playset.notFound", "Playset \"{0}\" wurde in der Playset-Datei nicht gefunden.", name);

    public static string UpdateFailed(string message) => F("playset.updateFailed", "Playset-Datei konnte nicht aktualisiert werden: {0}", message);

    public static string LauncherDatabaseReadOnly => T("playset.launcherDatabaseReadOnly", "Die Playsets dieses Spiels liegen in der Datenbank des Paradox-Launchers. PMT liest sie nur; umschalten lässt sich das Playset nur im Paradox-Launcher.");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
