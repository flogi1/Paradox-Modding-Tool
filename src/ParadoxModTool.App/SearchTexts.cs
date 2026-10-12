using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Texte des Suchen-&amp;-Ersetzen-Fensters. Die Keys stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche
/// Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class SearchTexts
{
    public static string Title => T("searchWindow.title", "Suchen & Ersetzen");

    public static string ModeCurrentDocument => T("searchWindow.mode.document", "Aktuelles Dokument");

    public static string ModeFiles => T("searchWindow.mode.files", "Ordner oder Datei");

    public static string ResultCopy => T("searchWindow.result.copy", "Treffer kopieren");

    public static string ResultCopyAll => T("searchWindow.result.copyAll", "Alle Treffer kopieren");

    public static string ResultDetailTip => T("searchWindow.result.detail.tip", "Code der markierten Treffer. Hier lassen sich Teile markieren und mit Strg+C kopieren. Mehrere Treffer markierst du in der Liste mit Strg+Klick oder Umschalt+Klick.");

    public static string ResultDetailMore(int count) => F("searchWindow.result.detail.more", "+ {0} weitere markierte Treffer (Strg+C in der Liste kopiert alle)", count);

    public static string BlockWarningTitle => T("searchWindow.blockWarning.title", "Block mit zu wenig Einträgen");

    public static string BlockWarningIntro(int count) =>
        F("searchWindow.blockWarning.intro", "Nach dem Ersetzen enthalten {0} OR-, NOR- oder AND-Blöcke nur noch einen oder keinen Eintrag. Vermutlich lassen sie sich vereinfachen oder entfernen.", count);

    public static string BlockWarningOpenHint => T("searchWindow.blockWarning.openHint", "Doppelklick öffnet die Stelle.");

    public static string BlockWarningItem(string file, int line, string keyword, string entries) =>
        F("searchWindow.blockWarning.item", "{0} — Zeile {1}: {2} ({3})", file, line, keyword, entries);

    public static string BlockWarningPanelHeading(int count) => F("searchWindow.blockWarning.panelHeading", "⚠ Nach dem Ersetzen ({0})", count);

    public static string BlockWarningPanelRefreshTip => T("searchWindow.blockWarning.refreshTip", "Liste aktualisieren: erledigte Stellen entfernen und die Zeilen nachführen");

    public static string BlockWarningPanelClearTip => T("searchWindow.blockWarning.clearTip", "Liste der Warnungen leeren");

    /// <summary>Eine Zeile der Warnliste: "Datei — Zeile n: OR (1 Eintrag)".</summary>
    public static string BlockWarningText(BlockWarningHit hit) =>
        BlockWarningItem(hit.File, hit.Block.Line, hit.Block.Keyword, hit.Block.Entries == 0 ? BlockWarningEntriesNone : BlockWarningEntriesOne);

    public static string BlockWarningEntriesNone => T("searchWindow.blockWarning.entries.none", "kein Eintrag");

    public static string BlockWarningEntriesOne => T("searchWindow.blockWarning.entries.one", "1 Eintrag");

    public static string BlockWarningMore(int count) => F("searchWindow.blockWarning.more", "… und {0} weitere", count);

    public static string ProgressFind => T("searchWindow.progress.find", "Suche in Dateien");

    public static string ProgressReplace => T("searchWindow.progress.replace", "Ersetzungen berechnen");

    public static string Browse => T("searchWindow.browse", "Durchsuchen");

    public static string PathTip => T("searchWindow.path.tip", "Zu durchsuchender Ordner oder einzelne Datei (z. B. der lokale Mod-Ordner)");

    public static string FavoriteRemove => T("searchWindow.favorite.remove", "Entfernen");

    public static string FavoriteRemoveTip => T("searchWindow.favorite.remove.tip", "Ausgewählten Favoriten entfernen");

    public static string FavoriteSave => T("searchWindow.favorite.save", "★ Speichern");

    public static string FavoriteSaveTip => T("searchWindow.favorite.save.tip", "Aktuellen Ordner als Favorit speichern");

    public static string FavoritesTip => T("searchWindow.favorites.tip", "Favorisierte Suchordner");

    public static string QuickPathHide => T("searchWindow.quickPath.hide", "▾ Ausblenden");

    public static string QuickPathShow => T("searchWindow.quickPath.show", "▸ Einblenden");

    public static string QuickPathToggleTip => T("searchWindow.quickPath.toggle.tip", "Schnellzugriff-Buttons ein- oder ausblenden");

    public static string QuickPathHeading => T("searchWindow.quickPath.heading", "Schnellzugriff:");

    public static string QuickPathModFolder => T("searchWindow.quickPath.modFolder", "Mod-Ordner");

    public static string QuickPathModFolderTip => T("searchWindow.quickPath.modFolder.tip", "Der lokale Mod-Ordner aus dem aktiven Spielprofil.");

    public static string QuickPathUnavailable => T("searchWindow.quickPath.unavailable", "(Derzeit nicht ermittelbar - Mod-Ordner nicht gefunden.)");

    public static string QuickPathAddTip => T("searchWindow.quickPath.add.tip", "Neuen Schnellzugriff anlegen: Name und Ordner");

    public static string QuickPathMenuEdit => T("searchWindow.quickPath.menu.edit", "Bearbeiten");

    public static string QuickPathMenuLeft => T("searchWindow.quickPath.menu.left", "Nach links");

    public static string QuickPathMenuRight => T("searchWindow.quickPath.menu.right", "Nach rechts");

    public static string QuickPathMenuGames => T("searchWindow.quickPath.menu.games", "Spiele festlegen…");

    public static string QuickPathGamesTitle => T("searchWindow.quickPath.games.title", "Schnellzugriff: Spiele");

    public static string QuickPathGamesPrompt(string label) => F("searchWindow.quickPath.games.prompt", "In welchen Spielen soll \"{0}\" angezeigt werden?", label);

    public static string QuickPathGamesAll => T("searchWindow.quickPath.games.all", "Alle Spiele");

    public static string QuickPathMenuRemove => T("searchWindow.quickPath.menu.remove", "Entfernen");

    public static string QuickPathDragHint => T("searchWindow.quickPath.dragHint", "Klick: diesen Ordner verwenden. Mit gedrückter linker Maustaste auf ein Pfadfeld ziehen: Ordner dort einfügen. Rechtsklick auf eigene Knöpfe: bearbeiten, verschieben, entfernen.");

    public static string QuickPathNamePrompt => T("searchWindow.quickPath.namePrompt", "Name für diesen Schnellzugriff-Button:");

    public static string QuickPathConfigureTitle => T("searchWindow.quickPath.configureTitle", "Schnellzugriff-Button konfigurieren");

    public static string QuickPathFolderPrompt(string label) =>
        F("searchWindow.quickPath.folderPrompt", "Ordner für \"{0}\" auswählen (Abbrechen behält den bisherigen Pfad bei)", label);

    public static string SearchLabel => T("searchWindow.search.label", "Suchbegriff (auch mehrzeilige Codeblöcke möglich):");

    public static string ReplaceLabel => T("searchWindow.replace.label", "Ersetzen durch:");

    public static string SwapTip => T("searchWindow.swap.tip", "Suchen- und Ersetzen-Text tauschen");

    public static string MatchCase => T("searchWindow.matchCase", "Groß-/Kleinschreibung beachten");

    public static string WholeWord => T("searchWindow.wholeWord", "Nur ganzes Wort");

    public static string IgnoreFormatting => T("searchWindow.ignoreFormatting", "Formatierung ignorieren");

    public static string IgnoreFormattingTip => T("searchWindow.ignoreFormatting.tip",
        "Findet Codeblöcke unabhängig von Einrückung, Zeilenumbrüchen und Leerzeichen. Ersetzt wird trotzdem jeweils exakt die tatsächlich gefundene, ggf. unterschiedlich formatierte Stelle.");

    public static string FindAll => T("searchWindow.findAll", "Alle finden");

    public static string ReplaceAll => T("searchWindow.replaceAll", "Alle ersetzen");

    public static string HideDescription => T("searchWindow.hideDescription", "Nur gefundenen Code anzeigen");

    public static string HideDescriptionTip => T("searchWindow.hideDescription.tip",
        "Blendet die beschreibende Zeile (Datei/Zeilennummer) aus, damit nur der gefundene Code selbst zu sehen ist (zum Kopieren)");

    public static string ResultsTip => T("searchWindow.results.tip", "Doppelklick springt zum Treffer");

    public static string BrowseDialogTitle => T("searchWindow.browse.dialog", "Suchordner auswählen");

    public static string EnterQuery => T("searchWindow.status.enterQuery", "Bitte einen Suchbegriff eingeben.");

    public static string PathNotFound => T("searchWindow.status.pathNotFound", "Der angegebene Ordner oder die Datei wurde nicht gefunden.");

    public static string FilesFound(int matches, int files) =>
        F("searchWindow.status.filesFound", "{0} Treffer in {1} Datei(en).", matches, files);

    public static string DocumentFound(int matches) =>
        F("searchWindow.status.documentFound", "{0} Treffer im aktuellen Dokument.", matches);

    public static string NothingReplaced => T("searchWindow.status.nothingReplaced", "Keine Treffer - es wurde nichts ersetzt.");

    public static string ReplaceHistoryDescription(string query, string replacement, int files) =>
        F("searchWindow.history.replace", "Suchen & Ersetzen in Dateien: \"{0}\" → \"{1}\" ({2} Datei(en))", query, replacement, files);

    public static string FilesReplaced(int replacements, int files) =>
        F("searchWindow.status.filesReplaced", "{0} Ersetzung(en) in {1} Datei(en) durchgeführt.", replacements, files);

    public static string DocumentNothingReplaced =>
        T("searchWindow.status.documentNothingReplaced", "Keine Treffer im aktuellen Dokument - es wurde nichts ersetzt.");

    public static string DocumentReplaced(int count) =>
        F("searchWindow.status.documentReplaced",
            "{0} Ersetzung(en) im aktuellen Dokument durchgeführt (Strg+Z im Textbereich macht dies rückgängig, solange noch nicht gespeichert).", count);

    public static string ResultLine(int line, string text) => F("searchWindow.result.line", "Zeile {0}: {1}", line, text);

    public static string ResultFileLine(string file, int line, string text) =>
        F("searchWindow.result.fileLine", "{0} — Zeile {1}: {2}", file, line, text);

    public static string ResultReplacements(int count) => F("searchWindow.result.replacements", "{0} Ersetzung(en)", count);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
