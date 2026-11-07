using ParadoxModTool.Localization;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.GfxAudit;

/// <summary>
/// Texte der GFX-Prüfung. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der
/// Rückfall und muss mit de.json übereinstimmen (Test <c>GfxAuditTextsTests</c>). Jeder Aufruf von <c>T</c>/<c>F</c> steht in einer Zeile.
/// </summary>
public static class GfxAuditTexts
{
    public static string Menu => T("gfxaudit.menu", "GFX-Prüfung");

    public static string MenuTip => T("gfxaudit.menuTip", "Findet ungenutzte Bilder und Sprites, fehlende Texturen und Sprites sowie doppelte Sprite-Namen in der gewählten Mod.");

    public static string WindowTitle => T("gfxaudit.title", "GFX-Prüfung");

    public static string Kind(GfxFindingKind kind) => kind switch
    {
        GfxFindingKind.UnusedImage => T("gfxaudit.kind.unusedImage", "Ungenutztes Bild"),
        GfxFindingKind.UnusedSprite => T("gfxaudit.kind.unusedSprite", "Ungenutztes Sprite"),
        GfxFindingKind.MissingTexture => T("gfxaudit.kind.missingTexture", "Fehlende Textur"),
        GfxFindingKind.MissingSprite => T("gfxaudit.kind.missingSprite", "Fehlendes Sprite"),
        GfxFindingKind.DuplicateSprite => T("gfxaudit.kind.duplicateSprite", "Doppelter Sprite-Name"),
        _ => kind.ToString(),
    };

    public static string Severity(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Error => T("gfxaudit.severity.error", "Fehler"),
        FindingSeverity.Warning => T("gfxaudit.severity.warning", "Warnung"),
        FindingSeverity.Hint => T("gfxaudit.severity.hint", "Hinweis"),
        _ => severity.ToString(),
    };

    public static string ColKind => T("gfxaudit.col.kind", "Art");

    public static string ColName => T("gfxaudit.col.name", "Name");

    public static string ColPlace => T("gfxaudit.col.place", "Datei:Zeile");

    public static string ColRelated => T("gfxaudit.col.related", "Benutzt von / zeigt auf");

    public static string Refresh => T("gfxaudit.refresh", "Aktualisieren");

    public static string Cancel => T("gfxaudit.cancel", "Abbrechen");

    public static string Cancelled => T("gfxaudit.cancelled", "Prüfung abgebrochen, der letzte Stand bleibt");

    public static string Failed(string message) => F("gfxaudit.failed", "Prüfung fehlgeschlagen: {0}", message);

    public static string ModLabel => T("gfxaudit.modLabel", "Mod:");

    public static string SearchTip => T("gfxaudit.searchTip", "Sucht in Name, Datei und Verwendung der Befunde");

    public static string FilterAll => T("gfxaudit.filterAll", "Alle");

    public static string ShowHidden => T("gfxaudit.showHidden", "Ausgeblendete zeigen");

    public static string Hide => T("gfxaudit.hide", "Ausblenden");

    public static string Unhide => T("gfxaudit.unhide", "Wieder einblenden");

    public static string Open => T("gfxaudit.open", "Stelle öffnen");

    public static string ShowInExplorer => T("gfxaudit.showInExplorer", "Im Explorer zeigen");

    public static string CopyName => T("gfxaudit.copyName", "Namen kopieren");

    public static string NobodyUses => T("gfxaudit.nobodyUses", "niemand");

    public static string Coverage => T("gfxaudit.coverage", "Grenzen: nur .dds, .png und .tga; Bilder unterhalb eines Ordners, den eine Datei als Pfad nennt, gelten als benutzt; Bilder, die nur in 3D-Modellen stehen, zählen über immer benutzte Ordner.");

    public static string Status(int images, int files, DateTime builtUtc) => F("gfxaudit.status", "Stand {2:g}, {0} Bilder, {1} Dateien", images, files, builtUtc.ToLocalTime());

    public static string Unread(int count) => F("gfxaudit.unread", "{0} Dateien nicht lesbar", count);

    public static string HideSaveFailed(string message) => F("gfxaudit.hideSaveFailed", "Ausblende-Liste nicht gespeichert: {0}", message);

    public static string NoMods => T("gfxaudit.noMods", "Keine Mod zur Auswahl.");

    public static string NoActiveMod => T("gfxaudit.noActiveMod", "Keine Mod gewählt: Es gibt nichts zu prüfen.");

    public static string NoGameFolder => T("gfxaudit.noGameFolder", "Spielordner fehlt: Vanilla-Bilder und -Sprites sind nicht bekannt, die Prüfung wäre unzuverlässig.");

    public static string Phase(ParadoxModTool.FlagTracker.BuildPhase phase, int done, int total) => phase switch
    {
        ParadoxModTool.FlagTracker.BuildPhase.Reading => F("gfxaudit.phase.reading", "Dateien lesen {0}/{1}", done, total),
        ParadoxModTool.FlagTracker.BuildPhase.Calls => F("gfxaudit.phase.calls", "Erwähnungen suchen {0}/{1}", done, total),
        _ => F("gfxaudit.phase.resolving", "Bilder abgleichen {0}/{1}", done, total),
    };

    public static string FileGone(string path) => F("gfxaudit.fileGone", "Datei nicht mehr vorhanden: {0}", path);

    public static string HideListUnreadable => T("gfxaudit.hideListUnreadable", "Die Ausblende-Liste ist nicht lesbar und wird nicht überschrieben.");

    public static string OpenFailed(string message) => F("gfxaudit.openFailed", "Datei konnte nicht geöffnet werden: {0}", message);

    public static string UnreadTip(int count) => F("gfxaudit.unreadTip", "{0} Dateien konnten nicht gelesen werden", count);

    public static string MoveOut => T("gfxaudit.moveOut", "Auslagern…");

    public static string MoveOutTip => T("gfxaudit.moveOutTip", "Verschiebt die gewählten ungenutzten Bilder in den Ordner <Mod>_gfx_archiv neben der Mod. Nichts wird gelöscht.");

    public static string Archive => T("gfxaudit.archive", "Archiv…");

    public static string ArchiveTip => T("gfxaudit.archiveTip", "Zeigt die ausgelagerten Bilder dieser Mod und holt sie zurück.");

    public static string MoveConfirmTitle => T("gfxaudit.move.confirmTitle", "Bilder auslagern");

    public static string MoveConfirm(int count, string archiveDirectory, string list) => F("gfxaudit.move.confirm", "{0} Bilder nach „{1}“ verschieben?\n\n{2}", count, archiveDirectory, list);

    public static string MoveDone(int moved, int failed) => F("gfxaudit.move.done", "{0} Bilder ausgelagert, {1} nicht verschoben.", moved, failed);

    public static string MoveNoImages => T("gfxaudit.move.noImages", "Keine ungenutzten Bilder ausgewählt.");

    public static string MoveBlockedBuilding => T("gfxaudit.move.blocked.building", "Erst nach dem Aufbau möglich.");

    public static string MoveBlockedNotLocal => T("gfxaudit.move.blocked.notLocal", "Nur für Mods im Mod-Ordner des Spiels (nicht Workshop).");

    public static string MoveBlockedNoVanilla => T("gfxaudit.move.blocked.noVanilla", "Ohne Spielordner nicht möglich: Ungenutzt ist dann nicht sicher.");

    public static string MoveBlockedUnread(int count) => F("gfxaudit.move.blocked.unread", "{0} Dateien waren nicht lesbar: Ungenutzt ist dann nicht sicher.", count);

    public static string MoveBlockedStale => T("gfxaudit.move.blocked.stale", "Erst aktualisieren: Die Liste gehört nicht zur gewählten Mod.");

    public static string Failure(GfxArchiveFailureReason reason) => reason switch
    {
        GfxArchiveFailureReason.Missing => T("gfxaudit.failure.missing", "Datei fehlt"),
        GfxArchiveFailureReason.OutsideFolder => T("gfxaudit.failure.outside", "liegt außerhalb des Ordners"),
        GfxArchiveFailureReason.NotAnImage => T("gfxaudit.failure.notImage", "kein Bild"),
        GfxArchiveFailureReason.TargetExists => T("gfxaudit.failure.targetExists", "am Ziel liegt schon eine Datei"),
        _ => T("gfxaudit.failure.io", "Zugriff fehlgeschlagen"),
    };

    public static string ArchiveTitle(string modName) => F("gfxaudit.archive.title", "Archiv: {0}", modName);

    public static string ArchiveEmpty => T("gfxaudit.archive.empty", "Keine ausgelagerten Bilder.");

    public static string ArchiveColFile => T("gfxaudit.archive.colFile", "Datei");

    public static string ArchiveColOriginal => T("gfxaudit.archive.colOriginal", "Alter Ort");

    public static string ArchiveColSize => T("gfxaudit.archive.colSize", "Größe (KB)");

    public static string ArchiveColDate => T("gfxaudit.archive.colDate", "Geändert");

    public static string ArchiveColState => T("gfxaudit.archive.colState", "Hinweis");

    public static string ArchiveOriginalExists => T("gfxaudit.archive.originalExists", "am alten Ort liegt schon eine Datei");

    public static string ArchiveRestore => T("gfxaudit.archive.restore", "Zurückholen");

    public static string ArchiveShowInExplorer => T("gfxaudit.archive.showInExplorer", "Im Explorer zeigen");

    public static string ArchiveClose => T("gfxaudit.archive.close", "Schließen");

    public static string ArchiveRestored(int restored, int failed) => F("gfxaudit.archive.restored", "{0} Bilder zurückgeholt, {1} nicht verschoben.", restored, failed);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
