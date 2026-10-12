using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>Texte des Fensters "Paket mit KI erstellen" (Keys <c>aiPackage.*</c>); der deutsche Text ist der Rückfall.</summary>
internal static class AiPackageTexts
{
    public static string Title => T("aiPackage.title", "Paket mit KI erstellen");

    public static string Open => T("aiPackage.open", "Mit KI erstellen");

    public static string OpenTip => T("aiPackage.open.tip",
        "Stellt einen Auftrag für eine KI deiner Wahl zusammen (Anleitung, Vorbild-Dateien aus deiner Mod, Beschreibung) und übernimmt ihre Antwort als Paket.");

    public static string TaskTab => T("aiPackage.taskTab", "Auftrag");

    public static string ResultTab => T("aiPackage.resultTab", "Ergebnis");

    public static string TaskIntro => T("aiPackage.taskIntro",
        "1. Mod wählen und die Dateien eines fertigen Satzes anhaken (z. B. alles zu einem Tag) oder unter \"Codeblöcke\" nur die passenden Stellen einfügen. 2. Beschreiben, was das Formular abfragen soll. 3. Auftrag kopieren und in eine KI einfügen, oder als Ordner für eine KI mit Dateizugriff speichern. 4. Die Antwort unter \"Ergebnis\" einfügen.");

    public static string Mod => T("aiPackage.mod", "Mod");

    public static string ModTip => T("aiPackage.mod.tip", "Mod mit dem Vorbild; die aktive Mod ist vorgewählt.");

    public static string NoMod => T("aiPackage.noMod", "Keine Mod gewählt.");

    public static string ChooseMod => T("aiPackage.chooseMod", "Mod wählen");

    public static string ChooseModMessage => T("aiPackage.chooseMod.message", "Mod mit dem Vorbild wählen. Die aktive Mod steht oben.");

    public static string ActiveMod(string name) => F("aiPackage.activeMod", "{0} (aktiv)", name);

    public static string Files => T("aiPackage.files", "Vorbild-Dateien");

    public static string FilesTip => T("aiPackage.files.tip",
        "Alle Dateien eines fertigen Satzes anhaken, auch Lokalisierung und Einträge in gemeinsamen Dateien. Bilder werden nur genannt, nicht mitgeschickt.");

    public static string FileLine(string path, long bytes) => F("aiPackage.fileLine", "{0}  ({1} KB)", path, Math.Max(1, (bytes + 1023) / 1024));

    public static string Chosen => T("aiPackage.chosen", "Angehakte Dateien");

    public static string ChosenTip => T("aiPackage.chosen.tip", "Häkchen entfernen nimmt die Datei wieder aus dem Auftrag.");

    public static string ChosenNone => T("aiPackage.chosen.none", "Noch keine Datei angehakt.");

    public static string ClearChosen => T("aiPackage.clearChosen", "Alle abwählen");

    public static string ClearChosenTip => T("aiPackage.clearChosen.tip", "Nimmt alle angehakten Dateien aus dem Auftrag.");

    public static string Snippets => T("aiPackage.snippets", "Codeblöcke");

    public static string SnippetsTip => T("aiPackage.snippets.tip", "Nur die passenden Stellen einfügen statt ganzer Dateien. Das hält den Auftrag klein, auch für kostenlose KIs.");

    public static string SnippetsCount(int count) => F("aiPackage.snippetsCount", "Codeblöcke ({0})", count);

    public static string SnippetsIntro => T("aiPackage.snippetsIntro",
        "Je Block eine Stelle aus der Mod einfügen und angeben, in welche Datei sie gehört. \"Ausschnitt\" heißt: Der Rest der Datei bleibt, das Paket fügt nur diesen Teil ein.");

    public static string AddSnippet => T("aiPackage.addSnippet", "Codeblock hinzufügen");

    public static string AddSnippetTip => T("aiPackage.addSnippet.tip", "Legt einen leeren Codeblock an.");

    public static string SnippetPath => T("aiPackage.snippetPath", "Mod-Pfad");

    public static string SnippetPathTip => T("aiPackage.snippetPath.tip",
        "Pfad der Datei in der Mod, z. B. in_game/common/static_modifiers/meine_datei.txt. Leer lassen, wenn er unbekannt ist; die KI fragt dann nach.");

    public static string PickSnippetPath => T("aiPackage.pickSnippetPath", "Aus Mod wählen");

    public static string PickSnippetPathTip => T("aiPackage.pickSnippetPath.tip", "Pfad aus den Dateien der gewählten Mod übernehmen.");

    public static string PickSnippetPathMessage => T("aiPackage.pickSnippetPath.message", "Datei der Mod wählen, zu der der Codeblock gehört.");

    public static string RemoveSnippet => T("aiPackage.removeSnippet", "Entfernen");

    public static string RemoveSnippetTip => T("aiPackage.removeSnippet.tip", "Nimmt diesen Codeblock aus dem Auftrag.");

    public static string SnippetWholeFile => T("aiPackage.snippetWholeFile", "Ganze Datei");

    public static string SnippetWholeFileTip => T("aiPackage.snippetWholeFile.tip", "Der Block ist die ganze Datei; das Paket schreibt sie komplett.");

    public static string SnippetExcerpt => T("aiPackage.snippetExcerpt", "Ausschnitt (Rest der Datei bleibt)");

    public static string SnippetExcerptTip => T("aiPackage.snippetExcerpt.tip", "Der Block ist nur ein Teil der Datei; das Paket fügt nur diesen Teil ein und lässt den Rest stehen.");

    public static string SnippetCodeTip => T("aiPackage.snippetCode.tip", "Den Code aus der Mod hier einfügen.");

    public static string SnippetNote => T("aiPackage.snippetNote", "Erläuterung");

    public static string SnippetNoteTip => T("aiPackage.snippetNote.tip",
        "Optional: was die KI über diesen Block wissen muss, z. B. \"wiederholt sich je Idee\" oder \"potential nicht ändern\".");

    public static string SnippetFromGameFile => T("aiPackage.snippetFromGameFile", "Datei des Spiels");

    public static string SnippetFromGameFileTip => T("aiPackage.snippetFromGameFile.tip",
        "Der Block stammt aus dem Spiel, nicht aus der Mod; der Pfad ist relativ zum Spielordner. Die KI nimmt ihn als Vorbild.");

    public static string SnippetToggleTip => T("aiPackage.snippetToggle.tip", "Klicken klappt den Block auf oder zu.");

    public static string SnippetSummary(string path, string scope, int lines) => F("aiPackage.snippetSummary", "{0} – {1}, {2} Zeilen", path, scope, lines);

    public static string SnippetNoPath => T("aiPackage.snippetNoPath", "(ohne Pfad)");

    public static string SnippetExcerptShort => T("aiPackage.snippetExcerptShort", "Ausschnitt");

    public static string SnippetGamePrefix => T("aiPackage.snippetGamePrefix", "Spiel: ");

    public static string CollapseAll => T("aiPackage.collapseAll", "Alle zuklappen");

    public static string CollapseAllTip => T("aiPackage.collapseAll.tip", "Zeigt von jedem Block nur die Kopfzeile.");

    public static string ExpandAll => T("aiPackage.expandAll", "Alle aufklappen");

    public static string ExpandAllTip => T("aiPackage.expandAll.tip", "Zeigt alle Blöcke vollständig.");

    public static string SnippetFromMod => T("aiPackage.snippetFromMod", "Aus Mod-Datei übernehmen");

    public static string SnippetFromModTip => T("aiPackage.snippetFromMod.tip", "Datei der gewählten Mod öffnen, Code markieren und als Block übernehmen.");

    public static string SnippetFromModMessage => T("aiPackage.snippetFromMod.message", "Datei der Mod wählen, aus der Code übernommen werden soll.");

    public static string SnippetFromGame => T("aiPackage.snippetFromGame", "Aus Spieldatei übernehmen");

    public static string SnippetFromGameTip => T("aiPackage.snippetFromGame.tip",
        "Datei des Spiels öffnen, Code markieren und als Block übernehmen, z. B. als Vorbild dafür, wie das Spiel etwas aufbaut.");

    public static string SnippetFromGameMessage => T("aiPackage.snippetFromGame.message", "Datei des Spiels wählen (nur Textdateien).");

    public static string NoGameFolder => T("aiPackage.noGameFolder", "Kein Spielordner bekannt. Bitte zuerst die Spiel-Exe festlegen.");

    public static string SourceTitle(string path) => F("aiPackage.sourceTitle", "Code wählen: {0}", path);

    public static string SourceIntro => T("aiPackage.sourceIntro",
        "Code markieren und übernehmen. Die Markierung wird auf ganze Zeilen erweitert. Das Fenster bleibt für weitere Stellen offen.");

    public static string SourceIntroGame => T("aiPackage.sourceIntroGame",
        "Datei des Spiels, nur zum Lesen. Code markieren und übernehmen; die Markierung wird auf ganze Zeilen erweitert. Das Fenster bleibt für weitere Stellen offen.");

    public static string SourceSearch => T("aiPackage.sourceSearch", "Suchen");

    public static string SourceSearchTip => T("aiPackage.sourceSearch.tip", "Enter oder \"Weitersuchen\" springt zur nächsten Fundstelle. Strg+F setzt den Cursor hierher.");

    public static string SourceFindNext => T("aiPackage.sourceFindNext", "Weitersuchen");

    public static string SourceNotFound => T("aiPackage.sourceNotFound", "Nicht gefunden.");

    public static string SourceTakeSelection => T("aiPackage.sourceTakeSelection", "Auswahl übernehmen");

    public static string SourceTakeSelectionTip => T("aiPackage.sourceTakeSelection.tip", "Legt einen Block mit den markierten Zeilen an, Umfang \"Ausschnitt\".");

    public static string SourceTakeWhole => T("aiPackage.sourceTakeWhole", "Ganze Datei übernehmen");

    public static string SourceTakeWholeTip => T("aiPackage.sourceTakeWhole.tip", "Legt einen Block mit der ganzen Datei an, Umfang \"Ganze Datei\".");

    public static string SourceClose => T("aiPackage.sourceClose", "Schließen");

    public static string SourceCloseTip => T("aiPackage.sourceClose.tip", "Schließt das Codefenster; übernommene Blöcke bleiben.");

    public static string SourceNoSelection => T("aiPackage.sourceNoSelection", "Erst Code markieren.");

    public static string SourceTaken(int number) => F("aiPackage.sourceTaken", "Block {0} hinzugefügt.", number);

    public static string SizeWithSnippets(int files, int snippets, long bytes) =>
        F("aiPackage.sizeWithSnippets", "{0} Dateien angehakt, {1} Codeblöcke, {2} KB im Auftrag", files, snippets, (bytes + 1023) / 1024);

    public static string Size(int files, long bytes) => F("aiPackage.size", "{0} Dateien angehakt, {1} KB im Auftrag", files, (bytes + 1023) / 1024);

    public static string SizeWarning => T("aiPackage.sizeWarning",
        "Über 500 KB: Viele Chat-KIs schneiden so lange Texte ab. Weniger Dateien wählen oder den Auftrag als Datei oder Ordner geben.");

    public static string Description => T("aiPackage.description", "Beschreibung");

    public static string DescriptionTip => T("aiPackage.description.tip",
        "Was soll das Paket tun? Beispiel: \"Für jeden Tag bis zu 10 Ideen mit je 4 Stufen; Name und Beschreibung in Englisch und Deutsch; Modifikatoren frei.\"");

    public static string WithPackage => T("aiPackage.withPackage", "Offenes Paket mitgeben (ändern statt neu erstellen)");

    public static string WithPackageTip => T("aiPackage.withPackage.tip", "Gibt das im Paket-Editor offene Paket mit; die KI ändert es und behält Kennung und Markierungs-Kennung.");

    public static string CopyText => T("aiPackage.copyText", "Als Text kopieren");

    public static string CopyTextTip => T("aiPackage.copyText.tip", "Anleitung, Auftrag und Dateien als ein Text in die Zwischenablage, zum Einfügen in eine Chat-KI.");

    public static string SaveFile => T("aiPackage.saveFile", "Als Datei speichern");

    public static string SaveFileTip => T("aiPackage.saveFile.tip", "Derselbe Text als .md-Datei, zum Hochladen in eine KI.");

    public static string SaveFolder => T("aiPackage.saveFolder", "Als Ordner speichern");

    public static string SaveFolderTip => T("aiPackage.saveFolder.tip",
        "Für KIs mit Dateizugriff: Anleitung, task.md mit den Prüfbefehlen, die Vorbild-Dateien in example/ und das offene Paket in package/.");

    public static string Copied => T("aiPackage.copied", "Auftrag in die Zwischenablage kopiert.");

    public static string Saved(string path) => F("aiPackage.saved", "Gespeichert: {0}", path);

    public static string FolderNotEmpty => T("aiPackage.folderNotEmpty", "Bitte einen leeren Ordner wählen.");

    public static string ResultIntro => T("aiPackage.resultIntro",
        "Die komplette Antwort der KI einfügen und \"Übernehmen\" drücken, oder den Ordner wählen, in den eine KI das Paket geschrieben hat. PMT lädt das Paket erst probeweise, fragt bei vergebener Kennung nach und prüft es danach.");

    public static string Answer => T("aiPackage.answer", "Antwort der KI");

    public static string AnswerTip => T("aiPackage.answer.tip", "Die ganze Antwort einfügen; PMT sucht die Blöcke zwischen <<<FILE ...>>> und <<<END>>>.");

    public static string Apply => T("aiPackage.apply", "Übernehmen");

    public static string ApplyTip => T("aiPackage.apply.tip", "Liest die Dateiblöcke und legt das Paket an (mit den gleichen Prüfungen wie beim Import).");

    public static string ImportFolder => T("aiPackage.importFolder", "Aus Ordner importieren");

    public static string ImportFolderTip => T("aiPackage.importFolder.tip", "Ordner mit package.json und templates/ wählen, z. B. den, in den eine KI das Paket geschrieben hat.");

    public static string Messages => T("aiPackage.messages", "Meldungen");

    public static string CopyFeedback => T("aiPackage.copyFeedback", "Fehler für die KI kopieren");

    public static string CopyFeedbackTip => T("aiPackage.copyFeedback.tip", "Kopiert die Meldungen mit einer kurzen Bitte um Korrektur; in die KI einfügen.");

    public static string FeedbackCopied => T("aiPackage.feedbackCopied", "Meldungen kopiert.");

    public static string Imported(string id) => F("aiPackage.imported", "Paket \"{0}\" angelegt und geöffnet.", id);

    public static string CheckOk => T("aiPackage.checkOk", "\"Prüfen\" findet keine Fehler. Teste das Paket jetzt mit \"Im Generator testen\".");

    public static string NotImported => T("aiPackage.notImported", "Nichts angelegt.");

    public static string Help => T("aiPackage.help", "Anleitung");

    public static string HelpTip => T("aiPackage.help.tip", "Öffnet das Kapitel \"Mit KI erstellen\" der Anleitung (F1).");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object[] args) => UiText.Format(key, german, args);
}
