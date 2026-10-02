using ParadoxModTool.Localization;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Texte der Verzeichnis-Übersicht (Baum, Kontextmenü, Namenssuche) und des Dialogs "Neue Datei anlegen". Die Keys stehen in
/// <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class ExplorerTexts
{
    // Kopf und Werkzeugleiste
    public static string Title => T("explorer.title", "Verzeichnis-Übersicht");

    public static string AddRoot => T("explorer.addRoot", "Ordner hinzufügen");

    public static string AddRootTip => T("explorer.addRoot.tip", "Weitere Ordnerstruktur zur Übersicht hinzufügen (Spieldateien, ein weiterer Mod, o. ä.)");

    public static string RefreshAllTip => T("explorer.refreshAll.tip", "Alle Wurzeln neu einlesen");

    public static string ButtonsLeft => T("explorer.buttonsLeft", "Buttons links");

    public static string ButtonsLeftTip => T("explorer.buttonsLeft.tip", "Lupe und die Knöpfe für den linken und rechten Arbeitsbereich auf die linke Seite der Zeilen setzen");

    public static string ErrorMarks => T("explorer.errorMarks", "Bug-Markierungen");

    public static string ErrorMarksTip => T("explorer.errorMarks.tip", "Zeigt hinter Dateien und Ordnern die Zahl der Meldungen aus dem letzten Spiellauf (Fehlerprotokoll)");

    public static string Favorites => T("explorer.favorites", "Favoriten");

    public static string FavoritesTip => T("explorer.favorites.tip", "Angepinnte Dateien/Ordner (Nutzer-Feedback: 'eine extra Favoritenliste ... wo ich einige Dateien oder Ordner anpinnen kann') - Alt+F pinnt den gerade ausgewählten Eintrag an");

    public static string Loading => T("explorer.loading", "Lädt…");

    // Zeilen-Buttons
    public static string SearchFolderTip => T("explorer.searchFolder.tip", "In diesem Ordner suchen (öffnet das Suchen-Fenster)");

    public static string OpenLeftTip => T("explorer.openLeft.tip", "Im linken Arbeitsbereich öffnen");

    public static string OpenRightTip => T("explorer.openRight.tip", "Im rechten Arbeitsbereich öffnen");

    // Namenssuche
    public static string SearchTip => T("explorer.search.tip", "Datei- und Ordnernamen in allen Wurzeln suchen. Teilstring, oder mit Platzhaltern (* und ?), z. B. *.gui - Enter startet sofort, Esc leert");

    public static string SearchClearTip => T("explorer.search.clear.tip", "Suche leeren (Esc)");

    public static string SearchFoldersOnly => T("explorer.search.foldersOnly", "Nur Ordner");

    public static string SearchFoldersOnlyTip => T("explorer.search.foldersOnly.tip", "Nur nach Ordnern suchen. Ein Klick auf einen Ordner zeigt ihn im Baum mit allen Dateien");

    public static string SearchBack => T("explorer.search.back", "Zurück zu den Treffern");

    public static string SearchBackTip => T("explorer.search.back.tip", "Zeigt die Trefferliste der letzten Suche wieder an");

    public static string SearchResultsTip => T("explorer.search.results.tip", "Doppelklick auf eine Datei: öffnen. Klick auf einen Ordner: im Baum zeigen");

    public static string SearchRunning => T("explorer.search.running", "Suche läuft…");

    public static string SearchNoHits => T("explorer.search.none", "Keine Treffer.");

    public static string SearchHitsTruncated(int count) => F("explorer.search.truncated", "{0} Treffer - weitere nicht angezeigt, bitte Suche eingrenzen.", count);

    public static string SearchHits(int count) => F("explorer.search.count", "{0} Treffer.", count);

    // Kontextmenü
    public static string NewFile => T("explorer.menu.newFile", "Neue Datei anlegen");

    public static string NewFolder => T("explorer.menu.newFolder", "Neuen Ordner anlegen");

    public static string RefreshFolder => T("explorer.menu.refreshFolder", "Ordner aktualisieren");

    public static string AssignColor => T("explorer.menu.assignColor", "Farbe zuweisen");

    public static string ClearColor => T("explorer.menu.clearColor", "Farbe entfernen");

    public static string PinFavorite => T("explorer.menu.pinFavorite", "Als Favorit anpinnen");

    public static string UnpinFavorite => T("explorer.menu.unpinFavorite", "Von Favoriten entfernen");

    public static string RevealInExplorer => T("explorer.menu.reveal", "Ordner öffnen (Explorer)");

    /// <summary>"In Dateien suchen" für Ordner.</summary>
    public static string SearchInFiles => T("explorer.menu.searchInFiles", "In Dateien suchen");

    public static string CreateFolderFilter => T("explorer.menu.createFolderFilter", "Als Filter anlegen");

    /// <summary>"In Datei suchen" für eine einzelne Datei.</summary>
    public static string SearchInFile => T("explorer.menu.searchInFile", "In Datei suchen");

    public static string CopyToMod => T("explorer.menu.copyToMod", "In andere Mod kopieren");

    public static string MoveToMod => T("explorer.menu.moveToMod", "In andere Mod verschieben");

    public static string CompareWithMod => T("explorer.menu.compareWithMod", "Mit anderer Mod vergleichen");

    public static string RemoveRoot => T("explorer.menu.removeRoot", "Aus Übersicht entfernen");

    public static string LockFolder => T("explorer.lock.folder", "Ordner sperren");

    public static string LockFile => T("explorer.lock.file", "Datei sperren");

    public static string UnlockFolder => T("explorer.unlock.folder", "Ordner entsperren");

    public static string UnlockFile => T("explorer.unlock.file", "Datei entsperren");

    public static string LockedByParent(string? folder) => F("explorer.lock.byParent", "Gesperrt durch den Ordner {0}. Entsperre den Ordner.", folder);

    public static string UnlockBelow(int count) => F("explorer.menu.unlockBelow", "Alle Sperren darunter aufheben ({0})", count);

    // Neuer Ordner (InputBox; Titel = NewFolder)
    public static string NewFolderPrompt => T("explorer.newFolder.prompt", "Name des neuen Ordners:");

    public static string NewFolderDefaultName => T("explorer.newFolder.defaultName", "Neuer Ordner");

    // Löschen
    public static string DeleteConfirmFolder(string name) => F(
        "explorer.delete.confirmFolder",
        "Soll den Ordner \"{0}\" wirklich gelöscht werden?\n\nDas Element wird in den Windows-Papierkorb verschoben (von dort bei Bedarf wiederherstellbar).",
        name);

    public static string DeleteConfirmFile(string name) => F(
        "explorer.delete.confirmFile",
        "Soll die Datei \"{0}\" wirklich gelöscht werden?\n\nDas Element wird in den Windows-Papierkorb verschoben (von dort bei Bedarf wiederherstellbar).",
        name);

    public static string DeleteConfirmTitle => T("explorer.delete.title", "Löschen bestätigen");

    // Dialog "Neue Datei anlegen" (Titel = NewFile)
    public static string NewFileTargetFolder => T("explorer.newFile.targetFolder", "Zielordner:");

    public static string NewFileName => T("explorer.newFile.fileName", "Dateiname (ohne Endung):");

    public static string NewFileType => T("explorer.newFile.fileType", "Dateityp: ");

    public static string NewFileCreate => T("explorer.newFile.create", "Anlegen");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
