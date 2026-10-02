using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Texte für "Mod an neue Vanilla anpassen" (Update-Reiter, Startfenster, Merge-Fenster im Update-Modus). Die Keys stehen in allen
/// <c>locales/*.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class VanillaUpdateTexts
{
    // Vanilla-Historie einlesen
    public static string ReadVersionButton => T("vanillaUpdate.readVersion", "Vanilla-Version einlesen");

    public static string ReadVersionTip => T("vanillaUpdate.readVersion.tip", "Liest die Vanilla-Dateien, die deine lokalen Mods überschreiben, für die installierte Spielversion in die Vanilla-Historie ein.");

    public static string Reading(int done, int total) => F("vanillaUpdate.reading", "Vanilla-Version wird eingelesen ({0}/{1})", done, total);

    public static string Recorded(string version, int recorded, int present) => F("vanillaUpdate.recorded", "{1} Dateien von Version {0} eingelesen, {2} schon vorhanden.", version, recorded, present);

    public static string Failed(int count) => F("vanillaUpdate.failed", "{0} Dateien konnten nicht gelesen werden.", count);

    public static string VersionUnknown => T("vanillaUpdate.versionUnknown", "Spielversion unbekannt, nichts eingelesen.");

    // Dateiarten ausblenden (Dateilisten und Update-Merge)
    public static string HideKindsLabel => T("vanillaUpdate.hideKinds", "Ausblenden:");

    public static string HideKindsTip => T("vanillaUpdate.hideKinds.tip", "Dateien dieser Arten nicht anzeigen und nicht abgleichen. Dieselbe Auswahl gilt im Kompatch-Start.");

    // Startfenster
    public static string StartTitle => T("vanillaUpdate.start.title", "Mod an neue Vanilla anpassen");

    public static string ModLabel => T("vanillaUpdate.start.mod", "Mod:");

    public static string CurrentVersionLabel => T("vanillaUpdate.start.current", "Installierte Version:");

    public static string OldVersionLabel => T("vanillaUpdate.start.old", "Alte Version:");

    public static string OldVersionAutomatic => T("vanillaUpdate.start.oldAutomatic", "automatisch (je Datei)");

    public static string ScopeThisFile => T("vanillaUpdate.start.thisFile", "Diese Datei");

    public static string ScopeAllChanged => T("vanillaUpdate.start.allChanged", "Alle geänderten Dateien");

    public static string IncludeWithoutOld => T("vanillaUpdate.start.includeWithoutOld", "Auch Dateien ohne alte Version");

    public static string Counts(int changed, int withoutOld) => F("vanillaUpdate.start.counts", "{0} Dateien in Vanilla geändert, {1} ohne alte Version.", changed, withoutOld);

    public static string Removed(string version, string files) => F("vanillaUpdate.start.removed", "Vanilla-Datei entfällt in Version {0}: {1}", version, files);

    public static string NoOldHint => T("vanillaUpdate.start.noOldHint", "Für manche Dateien fehlt eine alte Vanilla-Version. So bekommst du sie: das Spiel in Steam unter Eigenschaften › Betas auf die alte Version stellen, PMT einlesen lassen (automatisch oder mit „Vanilla-Version einlesen“), dann das Spiel wieder aktualisieren.");

    public static string NothingToDo => T("vanillaUpdate.start.nothing", "Keine Datei ist für den Abgleich ausgewählt.");

    public static string StartButton => T("vanillaUpdate.start.button", "Starten");

    public static string VersionUnknownStart => T("vanillaUpdate.start.versionUnknown", "Spielversion unbekannt. Trage sie oben im Update-Reiter ein.");

    // Merge-Fenster im Update-Modus
    public static string WindowTitle(string version) => F("vanillaUpdate.window.title", "Mod an Vanilla {0} anpassen", version);

    public static string OldVanillaColumn => T("vanillaUpdate.window.oldVanilla", "Vanilla alt");

    // Gespeicherte Läufe
    public static string SavedKind(string version) => F("vanillaUpdate.saved.kind", "Vanilla-Update → {0}", version);

    public static string SavedOutdated => T("vanillaUpdate.saved.outdated", "veraltet, das Spiel wurde seither aktualisiert");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
