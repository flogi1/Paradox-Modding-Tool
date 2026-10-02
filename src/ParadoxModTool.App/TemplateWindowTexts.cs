using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>
/// Texte des Template-Fensters, der Regelfenster, der Überschreib-Regeln und des Lokalisierungs-Profil-Dialogs. Die Keys stehen in
/// <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// Texte der Regel-Logik (Ebenen, Aktionen, Prüfmeldungen) stehen in <see cref="ParadoxModTool.Templates.TemplatesTexts"/>.
/// </summary>
public static class TemplateWindowTexts
{
    // ---- Template-Fenster: Kopfzeile ----

    public static string Title => T("templates.title", "Templates erzeugen");

    public static string TemplateLabel => T("templates.template.label", "Vorlage:");

    public static string TemplateNameTip => T("templates.template.nameTip",
        "Name, unter dem die Vorlage gespeichert oder geladen wird. Eine Vorlage enthält das Snippet, deine Änderungen an den Zählpunkten und die Lokalisierungstabelle.");

    public static string TemplateListTip => T("templates.template.listTip",
        "Gespeicherte Vorlagen. Die Auswahl übernimmt den Namen in das Feld links; danach mit Laden laden.");

    public static string Load => T("templates.template.load", "Laden");

    public static string LoadTip => T("templates.template.loadTip", "Die gewählte Vorlage laden (ersetzt das aktuelle Snippet)");

    public static string SaveTip => T("templates.template.saveTip", "Den aktuellen Stand unter dem Namen im Feld speichern");

    public static string DeleteTip => T("templates.template.deleteTip", "Die gewählte Vorlage löschen");

    public static string ModTip => T("templates.mod.tip",
        "Die Mod, deren Vorlagen angezeigt werden. Vorgabe ist die Mod, die das Hauptfenster erkannt hat (Mod der offenen Datei). Die Wahl hier ändert nicht die erkannte Mod im Hauptfenster.");

    public static string SaveIn => T("templates.saveIn", "Speichern in:");

    public static string SaveInTip => T("templates.saveIn.tip",
        "Spiel: die Vorlage gilt für alle Mods dieses Spiels. Mod: nur für die links gewählte Mod (nur wählbar, wenn eine Mod gewählt ist).");

    public static string Count => T("templates.count", "Anzahl:");

    public static string CountTip => T("templates.count.tip", "Wie viele Durchläufe erzeugt werden (1 bis 1000)");

    public static string Collapse => T("templates.collapse", "Beispiele zusammenfassen");

    public static string CollapseTip => T("templates.collapse.tip",
        "Steht dasselbe Muster mehrfach im Snippet (idea_1, idea_2, idea_3 …), gilt es als Beispiel: das Tool lernt daraus Start und Schritt und macht eine Vorlage daraus. Ausschalten, wenn du das Snippet genau so verwenden willst.");

    public static string Multi => T("templates.multi", "Mehrere Zahlen:");

    public static string MultiTip => T("templates.multi.tip",
        "Enthält ein Bezeichner mehrere Zahlen (z. B. ARA_1_1: Zähler und Stufe), zählt nur die gewählte. Zahlen direkt hinter einem Buchstaben (iv2) zählen dann nie. Einzelne Zahlen schaltest du im Prüfmodus um.");

    public static string MultiFirst => T("templates.multi.first", "erste zählt");

    public static string MultiLast => T("templates.multi.last", "letzte zählt");

    public static string MultiAll => T("templates.multi.all", "alle zählen");

    public static string Rules => T("templates.rules", "Regeln");

    public static string RulesTip => T("templates.rules.tip",
        "Zählregeln: legen fest, welche Zahlen mitzählen (Textur, Button, position, Namenspräfix … und deine eigenen Regeln), je Vorlage, Mod, Spiel oder für alle Spiele");

    public static string Review => T("templates.review", "Prüfmodus");

    public static string ReviewTip => T("templates.review.tip",
        "Zeigt die erkannten Zählpunkte in einer Liste und im Snippet farbig, damit du Start und Schritt ändern oder Punkte abschalten kannst");

    public static string GenerateLoc => T("templates.loc.generate", "Lokalisierung erzeugen");

    public static string GenerateLocTip => T("templates.loc.generate.tip", "Erzeugt zusätzlich Einträge für die Lokalisierung (Datei .yml) aus der Tabelle rechts");

    public static string Language => T("templates.language", "Sprache:");

    public static string LanguageTip => T("templates.language.tip", "Sprache im Kopf der Lokalisierungsdatei, z. B. english für l_english:");

    // ---- Template-Fenster: Suchen ----

    public static string FindIn => T("templates.find.in", "Suchen in:");

    public static string FindTargetTip => T("templates.find.target.tip",
        "Snippet: suchen und ersetzen. Code-Vorschau: nur suchen, um das erzeugte Ergebnis zu prüfen. Klickst du in einen der beiden Editoren, wird er hier gewählt.");

    public static string CodePreview => T("templates.find.codePreview", "Code-Vorschau");

    public static string FindTip => T("templates.find.tip", "Alle Treffer werden markiert und gezählt (Enter = weitersuchen)");

    public static string ReplaceLabel => T("templates.find.replaceLabel", "Ersetzen:");

    public static string FindNext => T("templates.find.next", "Weitersuchen");

    public static string Replace => T("templates.find.replace", "Ersetzen");

    public static string ReplaceAll => T("templates.find.replaceAll", "Alle ersetzen");

    public static string MatchCase => T("templates.find.matchCase", "Groß/klein");

    public static string WholeWords => T("templates.find.wholeWords", "Ganze Wörter");

    public static string IgnoreFormatting => T("templates.find.ignoreFormatting", "Formatierung ignorieren");

    public static string IgnoreFormattingTip => T("templates.find.ignoreFormatting.tip",
        "Beim Suchen werden Leerzeichen, Tabs, Einrückung, Zeilenumbrüche und Leerzeilen nicht verglichen. Beispiel: \"name = idea_1\" findet auch \"name=idea_1\" und eine Fundstelle über mehrere Zeilen. Beim Ersetzen wird die Einrückung des neuen Textes an die jeweilige Fundstelle angepasst. Ausschalten für eine genaue Suche, bei der jedes Leerzeichen zählt.");

    public static string NoMatch => T("templates.find.noMatch", "Kein Treffer");

    public static string MoreThanMatches(int max) => F("templates.find.moreThan", "über {0} Treffer", max);

    public static string Matches(int count) => F("templates.find.matches", "{0} Treffer", count);

    public static string PreviewSearchOnly => T("templates.find.previewOnly", "Die Vorschau ist nur zum Suchen. Ersetzen gilt für das Snippet.");

    public static string Replaced(int count) => F("templates.find.replaced", "{0} ersetzt", count);

    // ---- Template-Fenster: Snippet, Ergebnis, Lokalisierung ----

    public static string TogglePoint => T("templates.snippet.togglePoint", "Zählpunkt am Cursor umschalten");

    public static string TogglePointTip => T("templates.snippet.togglePoint.tip",
        "Schaltet den Zählpunkt an der Cursorstelle im Snippet an oder aus (auch über das Rechtsklick-Menü)");

    public static string SnippetHeading => T("templates.snippet.heading", "Beispiel-Snippet");

    public static string PointOnOff => T("templates.snippet.pointOnOff", "Zählpunkt an/aus");

    public static string RuleFromNumber => T("templates.snippet.ruleFromNumber", "Regel aus dieser Zahl erstellen");

    public static string RuleFromNumberTip => T("templates.snippet.ruleFromNumber.tip",
        "Legt aus der Zahl am Cursor eine Zählregel an (Muster aus dem Namen, die Zahl als [#])");

    public static string InsertAtCaret => T("templates.output.insert", "Am Cursor einfügen");

    public static string InsertAtCaretTip => T("templates.output.insert.tip", "Fügt das Ergebnis im aktiven Editor des Hauptfensters an der Cursorstelle ein");

    public static string NewDocument => T("templates.output.newDocument", "In neues Dokument");

    public static string Copy => T("templates.output.copy", "Kopieren");

    public static string AppendToFile => T("templates.output.append", "An Datei anhängen");

    public static string CodeAppendTip => T("templates.output.codeAppend.tip",
        "Hängt das Ergebnis an eine Datei an (bei einer neuen Datei wird sie angelegt). Ist die Datei im Programm offen, wird dort angehängt.");

    public static string LocHeading => T("templates.loc.heading", "Lokalisierung");

    public static string LocAppendTip => T("templates.output.locAppend.tip",
        "Hängt die Einträge an eine .yml-Datei an. Schlüssel, die es dort schon gibt, kannst du überspringen.");

    public static string ResetOverrides => T("templates.loc.resetOverrides", "Einzeländerungen zurücksetzen");

    public static string ResetOverridesTip => T("templates.loc.resetOverrides.tip", "Verwirft alle Texte, die du unten einzeln geändert hast");

    public static string FileType => T("templates.loc.fileType", "Dateityp:");

    public static string ProfileBoxTip => T("templates.loc.profileBox.tip",
        "Welche Stellen eine Lokalisierung brauchen, hängt vom Dateityp ab (Gebäude, GUI …). „automatisch“ nimmt den Ordner der zuletzt geöffneten Datei. Die Profile werden aus den Spieldateien gelernt.");

    public static string Profile => T("templates.loc.profile", "Profil");

    public static string ProfileTip => T("templates.loc.profile.tip",
        "Zeigt, welche Endungen und Felder das Profil vorschlägt, und lässt dich einzelne ein- oder ausschalten (gilt nur für diese Vorlage)");

    public static string DefaultText => T("templates.loc.defaultText", "Standardtext:");

    public static string DefaultTextTip => T("templates.loc.defaultText.tip", "Text neuer Zeilen. In Textmustern steht {n} für den Zähler und {key} für den Schlüssel.");

    public static string TextPatternTip => T("templates.loc.textPattern.tip", "Textmuster: {n} = Zähler, {key} = Schlüssel");

    public static string ProfileChoice(string path, int objects, int fields) =>
        F("templates.loc.profileChoice", "{0} ({1} Objekte, {2} Felder)", path, objects, fields);

    public static string ProfileNotLearned(string path) => F("templates.loc.profileNotLearned", "{0} (nicht gelernt)", path);

    // ---- Template-Fenster: Prüfmodus ----

    public static string PointsLegend => T("templates.points.legend",
        "Gelb = zählt, markiert im Snippet und im Ergebnis, aber nur für Stellen, die die Liste gerade zeigt · ▶ rot = ausgewählte Stelle · ● = von dir geändert");

    public static string MarkNumbers => T("templates.points.mark", "Eingesetzte Zahlen im Ergebnis markieren");

    public static string MarkNumbersTip => T("templates.points.mark.tip",
        "Färbt im Code-Ergebnis jede Zahl ein, die ein Zählpunkt eingesetzt hat. Ein in der Liste angeklickter Punkt wird kräftiger gefärbt.");

    public static string PointFilterTip => T("templates.points.filter.tip", "Nur Stellen zeigen, deren Name, Werte oder Hinweis diesen Text enthalten");

    public static string PointShowTip => T("templates.points.show.tip", "Welche Stellen die Liste zeigt. Die Auswahl wirkt nur auf die Anzeige, nicht auf das Ergebnis.");

    public static string ShowAll => T("templates.points.show.all", "alle");

    public static string ShowActive => T("templates.points.show.active", "nur aktive");

    public static string ShowDisabled => T("templates.points.show.disabled", "nur ausgeschaltete");

    public static string ShowChanged => T("templates.points.show.changed", "nur geänderte");

    public static string Hide => T("templates.points.hide", "Ausblenden ▾");

    public static string HideTip => T("templates.points.hide.tip",
        "Arten von Stellen aus der Liste ausblenden. Sie zählen weiter wie eingestellt und bleiben im Snippet markiert.");

    public static string HideIgnored => T("templates.points.hide.ignored", "Ignorierte Stellen (texture, position …)");

    public static string HideNumbers => T("templates.points.hide.numbers", "Einzelne Zahlen (cost = 10)");

    public static string HideTexts => T("templates.points.hide.texts", "Zahlen in Texten");

    public static string HideRule => T("templates.points.hide.rule", "Durch die Regel abgeschaltete (Stufen, iv2)");

    public static string ResetPoints => T("templates.points.reset", "Änderungen zurücksetzen");

    public static string ResetPointsTip => T("templates.points.reset.tip",
        "Verwirft alle deine Änderungen an den Zählpunkten und stellt die automatische Erkennung wieder her");

    public static string CreateRule => T("templates.points.createRule", "Regel erstellen");

    public static string CreateRuleTip => T("templates.points.createRule.tip",
        "Legt aus der in der Liste gewählten Zahl eine Zählregel an (Muster aus dem Namen, die Zahl als [#])");

    public static string PointsHeading => T("templates.points.heading", "Zählpunkte (Prüfmodus)");

    public static string ColumnSpot => T("templates.points.column.spot", "Stelle");

    public static string ColumnOccurrences => T("templates.points.column.occurrences", "Anz.");

    public static string ColumnOccurrencesTip => T("templates.points.column.occurrences.tip", "Wie oft die Stelle im Vorlagentext vorkommt");

    public static string ColumnStep => T("templates.points.column.step", "Schritt");

    public static string ColumnStepTip => T("templates.points.column.step.tip",
        "Um wie viel der Wert von einem Durchlauf (Anzahl oben) zum nächsten steigt. Der erste Durchlauf bleibt beim Startwert.");

    public static string ColumnInnerStep => T("templates.points.column.innerStep", "Wdh.-Schritt");

    public static string ColumnInnerStepTip => T("templates.points.column.innerStep.tip",
        "Nur für Zahlen in einem wiederholten Unterblock (siehe \"Wiederholte Unterblöcke\" unten in dieser Spalte). Der Wert sagt, um wie viel die Zahl von einer Wiederholung des Blocks zur nächsten steigt. Beispiel: Ein Event hat drei gleiche option-Blöcke mit opt_1, opt_2 und opt_3. Mit Wdh.-Schritt 1 erscheint in jedem erzeugten Event wieder opt_1, opt_2, opt_3. Der Schritt links davon gilt dagegen von einem erzeugten Event zum nächsten.");

    public static string SelectedSpotTip => T("templates.points.selected.tip", "Ausgewählte Stelle (markiert im Snippet und im Ergebnis)");

    public static string ChangedTip => T("templates.points.changed.tip", "Gegenüber der automatischen Erkennung geändert");

    public static string NumbersInName => T("templates.points.numbersInName", "Zahlen im Namen:");

    public static string ResetShapeTip => T("templates.points.resetShape.tip", "Zurück zur allgemeinen Regel \"Mehrere Zahlen\" für diese Form");

    public static string InnerStepTip => T("templates.points.innerStep.tip",
        "Wdh.-Schritt: um wie viel die Zahl von einer Wiederholung des Unterblocks zur nächsten steigt (innerhalb eines erzeugten Durchlaufs)");

    public static string Groups => T("templates.groups", "Wiederholte Unterblöcke");

    public static string GroupsTip => T("templates.groups.tip",
        "Gleichförmige Blöcke im Snippet, die zu einem zusammengefasst wurden. Die Anzahl bestimmt, wie oft er je Durchlauf erscheint.");

    public static string GroupCountTip => T("templates.groups.count.tip", "Wie oft der Block wiederholt wird");

    public static string SpotCount(int count) => F("templates.points.count", "{0} Stellen", count);

    public static string SpotCountFiltered(int visible, int total) => F("templates.points.countFiltered", "{0} von {1} Stellen angezeigt", visible, total);

    public static string RelaxTextFilter => T("templates.points.relax.textFilter", "Textfilter");

    public static string RelaxShowAll => T("templates.points.relax.showAll", "Auswahl \"alle\"");

    public static string RelaxIgnored => T("templates.points.relax.ignored", "Ignorierte Stellen");

    public static string RelaxNumbers => T("templates.points.relax.numbers", "Einzelne Zahlen");

    public static string RelaxRule => T("templates.points.relax.rule", "Durch die Regel abgeschaltete");

    public static string RelaxedBy(string list) => F("templates.points.relaxedBy", ". In der Liste eingeblendet durch Lockern von: {0}", list);

    public static string PointsReset => T("templates.points.resetDone", "Alle Änderungen an den Zählpunkten zurückgesetzt.");

    public static string NoPointHere => T("templates.points.noPointHere", "Hier steht keine Zahl, die als Zählpunkt in Frage kommt.");

    // ---- Template-Fenster: Zeilen der Prüfliste ----

    public static string ShapeTip(string shape) => F("templates.point.shapeTip",
        "Die Zahlen in diesem Namen (Form {0}; # steht für eine Zahl). Angehakte Zahlen zählen hoch, die anderen bleiben gleich. "
        + "Die Auswahl gilt für alle Bezeichner dieser Form und ersetzt dafür die allgemeine Regel \"Mehrere Zahlen\". Die fett gezeigte Zahl ist die dieser Zeile.", shape);

    public static string StaysUnchanged => T("templates.point.staysUnchanged", "bleibt unverändert");

    public static string RepeatValues(string values) => F("templates.point.repeatValues", "Wdh.: {0}", values);

    public static string SecondPassValues(string values) => F("templates.point.secondPass", " · 2. Durchlauf: {0}", values);

    public static string Becomes(string values) => F("templates.point.becomes", "wird {0}", values);

    public static string TurnedOnByYou => T("templates.point.onByYou", "von dir eingeschaltet");

    public static string TurnedOffByYou => T("templates.point.offByYou", "von dir ausgeschaltet");

    public static string Otherwise(string reason, string byDefault) => F("templates.point.otherwise", "{0} (sonst: {1})", reason, byDefault);

    public static string Counts => T("templates.point.counts", "zählt");

    public static string Off => T("templates.point.off", "aus");

    public static string InRepetition => T("templates.point.inRepetition", " · in Wiederholung");

    public static string CountsLearned => T("templates.point.countsLearned", "zählt: in den Beispielen erkannt");

    public static string CountsCustom => T("templates.point.countsCustom", "zählt: bei der eigenen Auswahl für diese Form angehakt");

    public static string CountsByRule(string rule) => F("templates.point.countsByRule", "zählt: {0}", rule);

    public static string CountsGeneralRule(string rule) => F("templates.point.countsGeneralRule", "zählt: allgemeine Regel „{0}“", rule);

    public static string CountsInText => T("templates.point.countsInText", "zählt: Zahl im Text");

    public static string CountsInName => T("templates.point.countsInName", "zählt: Zahl im Bezeichner");

    public static string OffIrregular => T("templates.point.offIrregular", "aus: in den Beispielen unregelmäßig");

    public static string OffReason(string reason) => F("templates.point.offReason", "aus: {0}", reason);

    public static string OffCustom => T("templates.point.offCustom", "aus: bei der eigenen Auswahl für diese Form nicht angehakt");

    public static string OffGeneralRule(string rule) =>
        F("templates.point.offGeneralRule", "aus: weitere Zahl oder Namenszahl, allgemeine Regel „{0}“", rule);

    public static string OffConstant => T("templates.point.offConstant", "aus: in den Beispielen immer gleich");

    public static string OffSingleNumber => T("templates.point.offSingleNumber", "aus: einzelne Zahl (standardmäßig aus)");

    public static string NoMod => T("templates.mod.none", "keine Mod");

    public static string ModDetected(string name) => F("templates.mod.detected", "{0} (erkannt)", name);

    // ---- Template-Fenster: Statuszeile und Meldungen ----

    public static string ExamplesMerged(int count) => F("templates.status.examplesMerged", "{0} Beispiele zu einer Vorlage zusammengefasst", count);

    public static string RepeatedGroups(int count) => F("templates.status.repeatedGroups", "{0} wiederholte Unterblöcke", count);

    public static string PointsActive(int active, int total) => F("templates.status.pointsActive", "{0} von {1} Zählpunkten aktiv", active, total);

    public static string Passes(int count) => F("templates.status.passes", "{0} Durchläufe", count);

    public static string IrregularWarning => T("templates.status.irregular", "Achtung: Zählung in den Beispielen unregelmäßig (Prüfmodus)");

    public static string EnterName => T("templates.template.enterName", "Gib im Feld neben \"Vorlage\" einen Namen ein.");

    public static string ConfirmOverwrite(string name, string where) =>
        F("templates.template.confirmOverwrite", "Die Vorlage \"{0}\"{1} gibt es schon. Überschreiben?", name, where);

    public static string SaveFailed(string message) => F("templates.template.saveFailed", "Die Vorlage konnte nicht gespeichert werden:\n{0}", message);

    public static string Saved(string name, string where) => F("templates.template.saved", "Vorlage \"{0}\"{1} gespeichert.", name, where);

    public static string NotFound => T("templates.template.notFound", "Diese Vorlage gibt es nicht.");

    public static string Loaded(string name, string where) => F("templates.template.loaded", "Vorlage \"{0}\"{1} geladen.", name, where);

    public static string ConfirmDelete(string name, string where) => F("templates.template.confirmDelete", "Vorlage \"{0}\"{1} löschen?", name, where);

    public static string PutCaretOnNumber => T("templates.rule.putCaret", "Setze den Cursor auf eine Zahl im Snippet, um eine Regel daraus zu erstellen.");

    public static string ChooseNumberFirst => T("templates.rule.chooseNumber", "Wähle zuerst in der Liste eine Zahl aus.");

    public static string RuleFor(string label) => F("templates.rule.defaultName", "Regel für {0}", label);

    public static string RuleCreated(string name, string layer) => F("templates.rule.created", "Regel „{0}“ auf der Ebene {1} angelegt.", name, layer);

    public static string RuleSaveFailed(string message) => F("templates.rule.saveFailed", "Die Regel konnte nicht gespeichert werden:\n{0}", message);

    public static string Inserted => T("templates.output.inserted", "Eingefügt.");

    public static string OpenedInNewDocument => T("templates.output.openedNew", "In neuem Dokument geöffnet.");

    public static string Copied => T("templates.output.copied", "In die Zwischenablage kopiert.");

    public static string ClipboardBusy => T("templates.output.clipboardBusy", "Die Zwischenablage ist gerade belegt. Bitte noch einmal versuchen.");

    public static string AppendCodeTitle => T("templates.output.appendCode.title", "Code an Datei anhängen");

    public static string ScriptFileFilter => T("templates.output.scriptFilter", "Skriptdateien (*.txt)|*.txt|Alle Dateien (*.*)|*.*");

    public static string AppendedInOpenDocument => T("templates.output.appendedOpen", "Im offenen Dokument angehängt (noch nicht gespeichert).");

    public static string AppendedTo(string path) => F("templates.output.appendedTo", "An {0} angehängt.", path);

    public static string WriteFailed(string message) => F("templates.output.writeFailed", "Die Datei konnte nicht geschrieben werden:\n{0}", message);

    public static string AppendLocTitle => T("templates.output.appendLoc.title", "Lokalisierung an Datei anhängen");

    public static string LocFileFilter => T("templates.output.locFilter", "Lokalisierung (*.yml)|*.yml|Alle Dateien (*.*)|*.*");

    public static string DuplicateKeys(int count, string sample) => F("templates.output.duplicateKeys",
        "{0} Schlüssel gibt es in der Datei schon ({1}).\n\nJa = diese überspringen\nNein = trotzdem anhängen (doppelte Schlüssel)\nAbbrechen = nichts tun", count, sample);

    public static string LanguageMismatch(string fileLanguage, string setLanguage) =>
        F("templates.output.languageMismatch", "Die Datei ist für die Sprache \"{0}\", eingestellt ist \"{1}\". Trotzdem anhängen?", fileLanguage, setLanguage);

    public static string EntriesAppended(int count) => F("templates.output.entriesAppended", "{0} Einträge angehängt", count);

    public static string EntriesSkipped(int count) => F("templates.output.entriesSkipped", ", {0} übersprungen", count);

    public static string InOpenDocumentNotSaved => T("templates.output.inOpenNotSaved", " (im offenen Dokument, noch nicht gespeichert).");

    // ---- Fenster "Zählregeln" ----

    public static string RulesTitle => T("rules.title", "Zählregeln");

    public static string ChangesOnLayer => T("rules.list.layerLabel", "Änderungen auf Ebene:");

    public static string ChangesOnLayerTip => T("rules.list.layerLabel.tip",
        "Auf dieser Ebene entstehen neue Regeln, und hier wird eine geerbte Regel abgeschaltet: Vorlage (nur diese Vorlage), Mod (alle Vorlagen der Mod), Spiel, Alle Spiele.");

    public static string NewRuleButton => T("rules.list.new", "Neu");

    public static string NewRuleTip => T("rules.list.new.tip", "Eine neue Regel anlegen");

    public static string EditRuleButton => T("rules.list.edit", "Bearbeiten");

    public static string CopyRuleButton => T("rules.list.copy", "Kopie erstellen ▾");

    public static string CopyRuleTip => T("rules.list.copy.tip",
        "Legt eine Kopie der gewählten Regel auf einer Ebene deiner Wahl an. Bei einer mitgelieferten Regel wird das Original dort abgeschaltet.");

    public static string MoveRuleButton => T("rules.list.move", "Verschieben ▾");

    public static string MoveRuleTip => T("rules.list.move.tip", "Verschiebt die gewählte Regel auf eine andere Ebene");

    public static string MoveUp => T("rules.list.up", "Nach oben");

    public static string MoveUpTip => T("rules.list.up.tip", "Die Regel innerhalb ihrer Ebene früher prüfen");

    public static string MoveDown => T("rules.list.down", "Nach unten");

    public static string Guide => T("rules.guide", "Anleitung");

    public static string GuideTip => T("rules.guide.tip", "Öffnet die Anleitung mit Beispielen");

    public static string ColumnLayer => T("rules.column.layer", "Ebene");

    public static string ColumnWhere => T("rules.column.where", "Wo");

    public static string ColumnPatterns => T("rules.column.patterns", "Muster");

    public static string ColumnAction => T("rules.column.action", "Aktion");

    public static string ColumnGames => T("rules.column.games", "Spiele");

    public static string ColumnHits => T("rules.column.hits", "Treffer");

    public static string ColumnHitsTip => T("rules.column.hits.tip", "Über wie viele Zahlen im aktuellen Snippet diese Regel entscheidet");

    public static string ToggleRuleTip => T("rules.list.toggle.tip", "Regel einschalten oder ausschalten");

    public static string NewRuleName => T("rules.list.newRuleName", "Neue Regel");

    public static string ShippedNotEditable => T("rules.list.shippedNotEditable",
        "Mitgelieferte Regeln lassen sich nicht ändern. Mit „Kopie erstellen“ legst du eine eigene Fassung an.");

    public static string CopyTo(string layer) => F("rules.list.copyTo", "Kopie auf {0}", layer);

    public static string MoveTo(string layer) => F("rules.list.moveTo", "Verschieben nach {0}", layer);

    public static string AlreadyOnTarget => T("rules.list.alreadyOnTarget", "Auf der Zielebene gibt es diese Regel schon.");

    public static string ConfirmDeleteRule(string name, string layer) => F("rules.list.confirmDelete", "Die Regel „{0}“ ({1}) löschen?", name, layer);

    // ---- Dialog "Regel bearbeiten" ----

    public static string EditRuleTitle => T("rules.edit.title", "Regel bearbeiten");

    public static string LayerLabel => T("rules.edit.layer", "Ebene:");

    public static string LayerTip => T("rules.edit.layer.tip",
        "Wo die Regel gespeichert wird und für wen sie gilt: Vorlage (nur diese), Mod, Spiel oder Alle Spiele. Spezifischere Ebenen gehen vor.");

    public static string AppliesToGames => T("rules.edit.games", "Gilt für Spiele:");

    public static string AllGamesCheck => T("rules.edit.allGames", "alle Spiele");

    public static string WhereLabel => T("rules.edit.where", "Wo:");

    public static string WhereTip => T("rules.edit.where.tip",
        "Überall: jeder Name im Snippet. Innerhalb von Schlüssel: nur im Eintrag \"Schlüssel = …\" (Wert und alles darin).");

    public static string WhereAnywhere => T("rules.edit.where.anywhere", "Überall");

    public static string WhereInsideKey => T("rules.edit.where.insideKey", "Innerhalb von Schlüssel");

    public static string KeyLabel => T("rules.edit.key", "Schlüssel:");

    public static string KeyTip => T("rules.edit.key.tip", "Zum Beispiel position oder texture; * steht für beliebigen Text (z. B. *_button)");

    public static string PatternsLabel => T("rules.edit.patterns", "Muster (eines pro Zeile):");

    public static string PatternsHint => T("rules.edit.patterns.hint",
        "* = beliebiger Text · # = eine Zahl · [#] = die Zahl, um die es geht (höchstens einmal je Muster). Das Muster muss auf den ganzen Namen passen; mehrere Muster gelten als „oder“.");

    public static string CaseSensitive => T("rules.edit.caseSensitive", "Groß-/Kleinschreibung beachten");

    public static string ActionLabel => T("rules.edit.action", "Aktion:");

    public static string PreviewLegend => T("rules.edit.previewLegend",
        "grün, fett = zählt · rot, durchgestrichen = zählt nicht · grau = die Regel entscheidet hier nicht");

    public static string PreviewNone => T("rules.edit.previewNone", "Diese Regel trifft im aktuellen Snippet keinen Namen.");

    public static string PreviewCount(int count) => F("rules.edit.previewCount", "Diese Regel trifft {0} Namen im aktuellen Snippet:", count);

    public static string ChooseGame => T("rules.edit.chooseGame", "Wähle mindestens ein Spiel oder „alle Spiele“.");

    // ---- Fenster "Überschreib-Regeln" ----

    public static string OverrideTitle => T("rules.override.title", "Überschreib-Regeln");

    public static string OverrideCertain => T("rules.override.certain", "gesichert");

    public static string OverrideUncertain => T("rules.override.uncertain", "unsicher");

    public static string OverrideLayerGameReplaces => T("rules.override.layer.gameReplaces", "Spiel (ersetzt Standard)");

    public static string OverrideLayerGame => T("rules.override.layer.game", "Spiel");

    public static string OverrideLayerDefault => T("rules.override.layer.default", "Standard");

    public static string OverrideCertainCheck => T("rules.override.certainCheck", "Laut Wiki gesichert");

    public static string OverrideHelp => T("rules.override.help",
        "Der Ordner steht relativ zum Wurzelordner der Spielinhalte, zum Beispiel \"*/common/on_action\". Ein Stern steht für genau einen Ordnernamen "
        + "(in_game, main_menu, loading_screen). Der längste passende Ordner gilt. Ein Spiel-Eintrag ersetzt den Standard-Eintrag desselben Ordners; "
        + "wird er entfernt, gilt wieder der Standard. \"Unsicher\" markiert Funde als nicht durch das Wiki gesichert. Änderungen gelten ab der nächsten Prüfung.");

    public static string OverrideFolder => T("rules.override.folder", "Ordner");

    public static string OverrideBehavior => T("rules.override.behavior", "Verhalten");

    public static string OverrideCertainty => T("rules.override.certainty", "Sicherheit");

    public static string OverrideApply => T("rules.override.apply", "Übernehmen");

    public static string OverrideNewEntry => T("rules.override.newEntry", "Neuer Eintrag");

    public static string OverrideMissingInput => T("rules.override.missingInput", "Gib einen Ordner an und wähle ein Verhalten.");

    // ---- Lokalisierungs-Profile ----

    public static string LocProfileTitle(string description) => F("locprofile.title", "Lokalisierungs-Profil: {0}", description);

    public static string LocProfileIntro => T("locprofile.intro",
        "Diese Auswahl gilt nur für diese Vorlage. Die Haken sind wie im Profil vorbelegt (gelernt aus den Spieldateien).");

    public static string LocProfileObjectKeys => T("locprofile.objectKeys", "Schlüssel aus dem Objektnamen (Name = X)");

    public static string LocProfileFields => T("locprofile.fields", "Werte in Feldern (feld = Schlüssel)");

    public static string LocProfileExtraSuffixes => T("locprofile.extraSuffixes", "Weitere Endungen (durch Leerzeichen getrennt)");

    public static string LocProfileExtraSuffixesTip => T("locprofile.extraSuffixes.tip",
        "Endungen, die zusätzlich zu jedem Objektnamen als Schlüssel vorgeschlagen werden, z. B. _tooltip");

    public static string LocProfileShare(double share) => F("locprofile.share", "   ({0:P0} der Objekte)", share);

    public static string LocProfilesTitle => T("locprofile.relearn.title", "Lokalisierungs-Profile");

    public static string LocProfilesLearnFailed => T("locprofile.relearn.failed", "Die Lokalisierungs-Profile konnten nicht gelernt werden.");

    public static string LocProfilesDuration(double seconds) => F("locprofile.relearn.duration", "\n\nDauer: {0:0.0} s", seconds);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
