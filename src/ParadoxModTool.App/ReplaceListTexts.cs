using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Texte des Tabs "Ersetzungslisten" im Compare und Update Tool (Spec 2026-09-30-replace-lists). Die Keys stehen in allen
/// <c>locales/*.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class ReplaceListTexts
{
    public static string TabHeader => T("replaceList.tab", "Ersetzungslisten");

    public static string NoMod => T("replaceList.noMod", "Kein Mod-Ordner gewählt. Wähle im Update-Tab einen Mod-Ordner.");

    public static string NoList => T("replaceList.noList", "Noch keine Ersetzungsliste. Lege mit \"Neu\" eine an.");

    public static string ListLabel => T("replaceList.list", "Liste:");

    public static string New => T("replaceList.new", "Neu");

    public static string NewListName => T("replaceList.newListName", "Neue Liste");

    public static string Rename => T("replaceList.rename", "Umbenennen");

    public static string Delete => T("replaceList.delete", "Löschen");

    public static string Export => T("replaceList.export", "Exportieren");

    public static string Import => T("replaceList.import", "Importieren");

    public static string FileFilter => T("replaceList.fileFilter", "Ersetzungsliste (*.json)|*.json");

    public static string TargetFolderLabel => T("replaceList.folder", "Zielordner:");

    public static string ChooseFolder => T("replaceList.chooseFolder", "Ordner wählen");

    public static string FolderOutsideMod => T("replaceList.folderOutsideMod", "Der Zielordner muss im Mod-Ordner liegen.");

    public static string FolderMissing(string folder) => F("replaceList.folderMissing", "Der Zielordner {0} fehlt in der Mod.", folder);

    public static string ColumnEnabled => T("replaceList.col.enabled", "Aktiv");

    public static string ColumnSearch => T("replaceList.col.search", "Suchen");

    public static string ColumnReplace => T("replaceList.col.replace", "Ersetzen durch");

    public static string ColumnMatches => T("replaceList.col.matches", "Treffer");

    public static string ColumnMode => T("replaceList.col.mode", "Art");

    public static string ColumnSafe => T("replaceList.col.safe", "Klammer schützen");

    public static string ModeWord => T("replaceList.mode.word", "Wort");

    public static string ModeText => T("replaceList.mode.text", "Text");

    public static string ModePattern => T("replaceList.mode.pattern", "Platzhalter");

    public static string ModeTip => T("replaceList.mode.tip", "Wort: ganzes Wort. Text: beliebiger Suchtext (auch mit { } = und mehreren Wörtern), ohne Wortgrenzen; Leerraum im Suchtext passt auf jeden Leerraum, auch auf Zeilenumbrüche. Platzhalter: wie Text, dazu steht * für ein Wort oder einen Wert, und $1, $2 … in \"Ersetzen durch\" geben ihn wieder.");

    public static string SafeTip => T("replaceList.safe.tip", "Nur bei \"Ersetzen durch\" mit # am Anfang: Steht nach dem Treffer noch Code in der Zeile (z. B. die schließende Klammer }), kommt der Kommentar auf eine eigene Zeile, damit der Rest aktiv bleibt.");

    public static string GridTip => T("replaceList.grid.tip", "Strg+V fügt Zeilen aus einer Tabelle ein: erste Spalte Suchen, zweite Spalte Ersetzen durch. Ein Suchtext mit Klammern oder mehreren Wörtern bekommt dabei die Art Text, mit * die Art Platzhalter.");

    public static string AddRule => T("replaceList.addRule", "Regel hinzufügen");

    public static string RemoveRule => T("replaceList.removeRule", "Regel entfernen");

    public static string MoveUp => T("replaceList.moveUp", "Nach oben");

    public static string MoveDown => T("replaceList.moveDown", "Nach unten");

    public static string ProblemEmpty => T("replaceList.problem.empty", "Suchen ist leer.");

    public static string ProblemNotAWord => T("replaceList.problem.notAWord", "In der Art Wort muss Suchen ein ganzes Wort sein: Buchstaben, Ziffern, _ und innen auch . : - Für Klammern oder mehrere Wörter wähle die Art Text.");

    public static string ProblemMultiline => T("replaceList.problem.multiline", "Der Suchtext muss einzeilig sein. Leerraum darin passt auch auf Zeilenumbrüche in der Datei.");

    public static string ProblemBadCapture => T("replaceList.problem.badCapture", "\"Ersetzen durch\" nennt ein $n, zu dem es kein passendes * im Suchtext gibt ($1 ist das erste *).");

    public static string ProblemDuplicate => T("replaceList.problem.duplicate", "Dieser Suchtext steht in mehreren aktiven Regeln der gleichen Art; keine davon läuft.");

    public static string InvalidCount(int count) => F("replaceList.invalidCount", "{0} Regeln sind ungültig (rot) und laufen nicht.", count);

    public static string ZeroMatchesTip => T("replaceList.zeroMatches.tip", "Kein Treffer im Zielordner. Hat das Spiel diesen Namen umbenannt oder entfernt?");

    public static string Counting => T("replaceList.counting", "Treffer werden gezählt");

    public static string Run => T("replaceList.run", "Ausführen");

    public static string RunTip => T("replaceList.run.tip", "Wendet alle aktiven Regeln der Reihe nach auf den Zielordner an und zeigt vorher eine Vorschau. Text nach # (Kommentar) bleibt unberührt.");

    public static string PreviewTitle(string list) => F("replaceList.preview.title", "Vorschau: Ersetzungsliste \"{0}\"", list);

    public static string PreviewFile(string path, int count) => F("replaceList.preview.file", "{0} ({1} Ersetzungen)", path, count);

    public static string PreviewBefore => T("replaceList.preview.before", "Vorher");

    public static string PreviewAfter => T("replaceList.preview.after", "Nachher");

    public static string Apply => T("replaceList.apply", "Übernehmen");

    public static string Cancel => T("replaceList.cancel", "Abbrechen");

    public static string NothingToReplace => T("replaceList.nothing", "Nichts zu ersetzen: keine aktive Regel trifft im Zielordner.");

    public static string Stale(string file) => F("replaceList.stale", "{0} wurde seit der Vorschau geändert. Nichts geschrieben; bitte erneut ausführen.", file);

    public static string SnapshotNote(string list) => F("replaceList.snapshotNote", "Ersetzungsliste \"{0}\"", list);

    public static string UndoAction(string list) => F("replaceList.undoAction", "Ersetzungsliste \"{0}\" ausgeführt", list);

    public static string Done(int replacements, int files) => F("replaceList.done", "{0} Ersetzungen in {1} Dateien geschrieben.", replacements, files);

    public static string Skipped(int files) => F("replaceList.skipped", "{0} Dateien übersprungen (kein gültiges UTF-8 oder nicht lesbar).", files);

    public static string Failed(string error) => F("replaceList.failed", "Schreiben fehlgeschlagen, nichts geändert: {0}", error);

    public static string NameTitle => T("replaceList.name.title", "Name der Liste");

    public static string NamePrompt => T("replaceList.name.prompt", "Name der Ersetzungsliste:");

    public static string NameInvalid => T("replaceList.name.invalid", "Der Name ist leer oder wird schon von einer anderen Liste dieser Mod benutzt.");

    public static string DeleteConfirm(string list) => F("replaceList.deleteConfirm", "Ersetzungsliste \"{0}\" löschen?", list);

    public static string ImportFailed(string error) => F("replaceList.importFailed", "Import fehlgeschlagen: {0}", error);

    public static string StoreFailed(string error) => F("replaceList.storeFailed", "Ersetzungslisten konnten nicht gespeichert oder gelesen werden: {0}", error);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
