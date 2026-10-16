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

    /// <summary>Verständliche Meldung zu einem Ergebnis der Playset-Verwaltung (Keys <c>playset.status.*</c>).</summary>
    public static string Describe(PlaysetResult result) => result.Status switch
    {
        PlaysetStatus.Ok => string.Empty,
        PlaysetStatus.GameRunning => T("playset.status.gameRunning", "Das Spiel läuft. Bitte zuerst das Spiel schließen; es schreibt seine Playset-Dateien selbst."),
        PlaysetStatus.LauncherRunning => T("playset.status.launcherRunning", "Der Paradox-Launcher läuft. Bitte zuerst den Launcher schließen; er würde die Änderung überschreiben."),
        PlaysetStatus.FileMissing => F("playset.status.fileMissing", "Playset-Datei nicht gefunden: {0}", result.Detail),
        PlaysetStatus.InvalidFile => T("playset.status.invalidFile", "Die Playset-Datei hat nicht das erwartete Format. Nichts wurde geändert."),
        PlaysetStatus.InvalidName => T("playset.status.invalidName", "Das Playset braucht einen Namen."),
        PlaysetStatus.DuplicateName => F("playset.status.duplicateName", "Ein Playset mit dem Namen „{0}“ gibt es schon.", result.Detail),
        PlaysetStatus.ModMissing => F("playset.status.modMissing", "Der Ordner der Mod fehlt: {0}", result.Detail),
        PlaysetStatus.DuplicateMod => F("playset.status.duplicateMod", "Diese Mod steht zweimal im Playset: {0}", result.Detail),
        PlaysetStatus.PlaysetMissing => F("playset.status.playsetMissing", "Das Playset „{0}“ gibt es nicht (mehr).", result.Detail),
        PlaysetStatus.WriteFailed => F("playset.status.writeFailed", "Schreiben fehlgeschlagen, die Datei ist unverändert: {0}", result.Detail),
        PlaysetStatus.LoadFileFailed => F("playset.status.loadFileFailed", "Die Lade-Datei für das Spiel konnte nicht geschrieben werden: {0}", result.Detail),
        PlaysetStatus.BackupMissing => F("playset.status.backupMissing", "Die Sicherung „{0}“ gibt es nicht.", result.Detail),
        _ => result.Status.ToString(),
    };

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
