using ParadoxModTool.Localization;

namespace ParadoxModTool.App;

/// <summary>Texte des Fensters "Generator-Pakete" (Keys <c>generatorWindow.*</c> in <c>locales/*.json</c>). Der deutsche Text ist der Rückfall und muss mit de.json übereinstimmen.</summary>
internal static class GeneratorTexts
{
    public static string Title => T("generatorWindow.title", "Generator-Pakete");

    public static string Packages => T("generatorWindow.packages", "Pakete");

    public static string States => T("generatorWindow.states", "Arbeitsstände");

    public static string SearchStatesTip => T("generatorWindow.searchStates.tip", "Arbeitsstände nach Schlüssel filtern");

    public static string NewState => T("generatorWindow.newState", "Neu");

    public static string Guide => T("generatorWindow.guide", "Anleitung");

    public static string GuideTip => T("generatorWindow.guide.tip", "Öffnet die Anleitung zu Generator-Paketen (F1).");

    public static string EditPackage => T("generatorWindow.editPackage", "Paket bearbeiten");

    public static string EditPackageTip => T("generatorWindow.editPackage.tip", "Öffnet das gewählte Paket im Paket-Editor (Formular, Ausgaben, Vorlagen).");

    public static string NewPackage => T("generatorWindow.newPackage", "Neues Paket");

    public static string NewPackageTip => T("generatorWindow.newPackage.tip", "Legt im Paket-Editor ein neues, leeres Paket an.");

    public static string ReadFromMod => T("generatorWindow.readFromMod", "Aus Mod einlesen");

    public static string ReadFromModTip => T("generatorWindow.readFromMod.tip", "Liest den Stand dieses Schlüssels aus der Basis-Mod neu ein und ersetzt das Formular; Basis-Mod und Ziel bleiben.");

    public static string ReadNothing(string key) => F("generatorWindow.readNothing", "Für {0} wurde in der Basis-Mod nichts gefunden.", key);

    public static string TakeOver => T("generatorWindow.takeOver", "Tags aus Mod übernehmen");

    public static string TakeOverTip => T("generatorWindow.takeOver.tip", "Liest Sets einer anderen Mod als Arbeitsstände ein; \"Erzeugen\" schreibt sie danach in die gewählte Basis-Mod.");

    public static string TakeOverTitle => T("generatorWindow.takeOver.title", "Tags aus Mod übernehmen");

    public static string TakeOverSource => T("generatorWindow.takeOver.source", "Quelle");

    public static string TakeOverReplaces => T("generatorWindow.takeOver.replaces", "wird ersetzt");

    public static string TakeOverAll => T("generatorWindow.takeOver.all", "Alle");

    public static string TakeOverNone => T("generatorWindow.takeOver.none", "Keine");

    public static string TakeOverHint => T("generatorWindow.takeOver.hint", "Änderungen in den eigenen Dateien eines Sets, die nicht aus dem Formular kommen, werden beim nächsten \"Erzeugen\" ersetzt; die Vorschau zeigt das vorher.");

    public static string TakeOverOk => T("generatorWindow.takeOver.ok", "Übernehmen");

    public static string TakeOverConfirmReplace(int count) => F("generatorWindow.takeOver.confirmReplace", "{0} vorhandene Arbeitsstände werden ersetzt. Fortfahren?", count);

    public static string TakeOverDone(int count) => F("generatorWindow.takeOver.done", "{0} Arbeitsstände übernommen.", count);

    public static string Check => T("generatorWindow.check", "Mod prüfen");

    public static string CheckTip => T("generatorWindow.check.tip", "Liest alle Sets der Basis-Mod neu ein und zeigt, welche Dateien inhaltlich von den Vorlagen abweichen, welche sich nur in Leerraum oder Kommentaren unterscheiden und welche Blöcke nichts mehr nutzt; geschrieben wird nichts.");

    public static string CheckTitle(string mod) => F("generatorWindow.check.title", "Mod prüfen: {0}", mod);

    public static string CheckColumnTag => T("generatorWindow.check.column.tag", "Tag");

    public static string CheckColumnMissing => T("generatorWindow.check.column.missing", "Fehlt");

    public static string CheckColumnChanged => T("generatorWindow.check.column.changed", "Weicht ab");

    public static string CheckColumnEqual => T("generatorWindow.check.column.equal", "Gleich");

    public static string CheckColumnFormatOnly => T("generatorWindow.check.column.formatOnly", "Nur Format");

    public static string CheckColumnLegacy => T("generatorWindow.check.column.legacy", "Alt-Einträge");

    public static string CheckColumnUnused => T("generatorWindow.check.column.unused", "Ungenutzt");

    public static string CheckColumnError => T("generatorWindow.check.column.error", "Fehler");

    public static string CheckOpen => T("generatorWindow.check.open", "Öffnen");

    public static string CheckRemoveUnused => T("generatorWindow.check.removeUnused", "Ungenutzte entfernen");

    public static string CheckMiddleKept(string block) => F("generatorWindow.check.middleKept", "{0} liegt in der Mitte und bleibt", block);

    public static string CheckNone => T("generatorWindow.check.none", "Keine Tags gefunden.");

    public static string CheckConfirmReplace(string key) => F("generatorWindow.check.confirmReplace", "Der Arbeitsstand {0} wird durch den gelesenen Stand ersetzt. Fortfahren?", key);

    public static string NewStatePrompt => T("generatorWindow.newState.prompt", "Schlüssel des neuen Arbeitsstands (bei IV2 der Tag):");

    public static string NewKeyTitle => T("generatorWindow.newKey.title", "Neuer Arbeitsstand");

    public static string NewKeyMessage => T("generatorWindow.newKey.message", "Die Felder, aus denen der Schlüssel des neuen Arbeitsstands besteht:");

    public static string NewKeyInvalid(string field) => F("generatorWindow.newKey.invalid", "\"{0}\" ist leer oder passt nicht zum Muster des Felds.", field);

    public static string Renumbered(string oldKey, string newKey, string collisions) =>
        F("generatorWindow.renumbered",
            "Der neue Arbeitsstand {0} hätte Lokalisierungsschlüssel mit demselben Hash wie vorhandene Schlüssel; das füllt im Spiel das error.log. Er wurde auf {1} verschoben.\n\nKollisionen der übersprungenen Nummern:\n{2}",
            oldKey, newKey, collisions);

    public static string StateExists(string key) => F("generatorWindow.newState.exists", "Den Arbeitsstand {0} gibt es schon.", key);

    public static string FormTab => T("generatorWindow.tab.form", "Formular");

    public static string PreviewTab => T("generatorWindow.tab.preview", "Vorschau");

    public static string PreviewTabCount(int files) => F("generatorWindow.tab.previewCount", "Vorschau ({0})", files);

    public static string Save => T("generatorWindow.save", "Speichern");

    public static string Preview => T("generatorWindow.preview", "Vorschau");

    public static string Generate => T("generatorWindow.generate", "Erzeugen");

    public static string Head => T("generatorWindow.head", "Kopf");

    public static string Target => T("generatorWindow.target", "Ziel");

    public static string TargetModNamed(string mod) => F("generatorWindow.target.modNamed", "In Mod schreiben: {0}", mod);

    public static string TargetNoMod => T("generatorWindow.target.noMod", "In Mod schreiben (keine Mod gewählt)");

    public static string TargetOutput => T("generatorWindow.target.output", "In Ausgabeordner");

    public static string ChooseFolder => T("generatorWindow.chooseFolder", "Ordner wählen");

    public static string ExternalChoose => T("generatorWindow.external.choose", "Datei wählen");

    public static string ExternalNone => T("generatorWindow.external.none", "(keine Datei gewählt)");

    public static string ExternalDialogTitle(string label) => F("generatorWindow.external.dialogTitle", "Datei wählen: {0}", label);

    public static string ExternalSaveFailed(string error) => F("generatorWindow.external.saveFailed", "Der Dateipfad konnte nicht gespeichert werden:\n{0}", error);

    public static string Languages => T("generatorWindow.languages", "Sprachen");

    public static string RuleExtend => T("generatorWindow.rule.extend", "Englisch in alle Sprachen");

    public static string RuleCopy => T("generatorWindow.rule.copy", "Englisch in leere Sprachen kopieren");

    public static string RuleMissing => T("generatorWindow.rule.missing", "Englisch für fehlende Übersetzungen");

    public static string RuleExtendTip => T("generatorWindow.rule.extend.tip", "Schreibt die Lokalisierung für alle Sprachen des Spiels, nicht nur für die gewählten. Texte ohne eigene Übersetzung bekommen den englischen Text.");

    public static string RuleCopyTip => T("generatorWindow.rule.copy.tip", "Jede gewählte Sprache ohne eigene Übersetzung bekommt den englischen Text. Aus: diese Texte bleiben leer, zum späteren Übersetzen.");

    public static string RuleMissingTip => T("generatorWindow.rule.missing.tip", "Nur für Texte, die schon Übersetzungen in andere Sprachen haben (z. B. aus der Ideen-Datenbank): Sprachen ohne Übersetzung bekommen Englisch statt leer. Ohne Wirkung, solange Englisch ohnehin in leere oder alle Sprachen geht.");

    public static string Translations => T("generatorWindow.translations", "Übersetzungen");

    public static string TranslationsTitle(string field) => F("generatorWindow.translations.title", "Übersetzungen: {0}", field);

    public static string MissingTranslation(string languages) => F("generatorWindow.translations.missing", "Ohne Übersetzung: {0}", languages);

    public static string Count => T("generatorWindow.count", "Anzahl");

    public static string NoPackages(string folder) => F("generatorWindow.noPackages", "Keine Pakete gefunden. Pakete liegen in {0}.", folder);

    public static string BrokenPackage(string folder) => F("generatorWindow.brokenPackage", "{0} (fehlerhaft)", folder);

    public static string NoMod => T("generatorWindow.noMod", "Keine Mod gewählt oder der Ordner fehlt. Bitte oben im Formular bei \"Mod\" eine Mod wählen.");

    public static string ModLabel => T("generatorWindow.mod", "Basis-Mod");

    public static string ModHint => T("generatorWindow.mod.hint", "liefert die Basisdateien; Ziel bei \"In Mod schreiben\"");

    public static string ModPickerTip => T("generatorWindow.mod.tip", "Die Mod dieses Arbeitsstands: Grundlage der Basisdateien und Ziel bei \"In Mod schreiben\". Sie bleibt beim Stand gespeichert, egal welche Datei im Editor offen ist.");

    public static string NoModChosen => T("generatorWindow.mod.none", "(keine Mod)");

    public static string NoState => T("generatorWindow.noState", "Links einen Arbeitsstand wählen oder mit \"Neu\" einen anlegen.");

    public static string UnsavedAsk(string key) => F("generatorWindow.unsaved", "Änderungen an {0} speichern?", key);

    public static string Saved(string key) => F("generatorWindow.saved", "{0} gespeichert.", key);

    public static string ImageNone => T("generatorWindow.imageNone", "(kein Bild)");

    public static string NoPreview => T("generatorWindow.noPreview", "keine Vorschau");

    public static string ImageLibraryError(string message) => F("generatorWindow.imageLibraryError", "Bilder nicht lesbar: {0}", message);

    public static string AssetMissing(string file) => F("generatorWindow.assetMissing", "{0} fehlt noch; ohne die Datei entfällt nur die Kopie daraus.", file);

    public static string AssetPick => T("generatorWindow.assetPick", "Asset wählen");

    public static string AssetPickTip => T("generatorWindow.assetPick.tip", "Datei wählen; PMT kopiert sie in den Datenordner des Pakets (z. B. die Wappen-Zip coa_library.zip).");

    public static string AssetFilter => T("generatorWindow.assetFilter", "Zip-Dateien (*.zip)|*.zip|Alle Dateien (*.*)|*.*");

    public static string AssetFilterJson => T("generatorWindow.assetFilterJson", "JSON-Dateien (*.json)|*.json|Alle Dateien (*.*)|*.*");

    public static string ErrorsAndWarnings(int errors, int warnings) => F("generatorWindow.issues", "{0} Fehler, {1} Warnungen", errors, warnings);

    public static string NoIssues => T("generatorWindow.noIssues", "Formular in Ordnung.");

    public static string Stale(string files) => F("generatorWindow.stale", "Seit der Vorschau geändert: {0}. Bitte die Vorschau neu berechnen.", files);

    public static string Unsaved(string file) => F("generatorWindow.unsavedDocument", "{0} ist im Editor geöffnet und nicht gespeichert. Bitte erst speichern oder schließen.", file);

    public static string WriteFailed(string message) => F("generatorWindow.writeFailed", "Schreiben fehlgeschlagen, nichts geändert: {0}", message);

    public static string Written(int files) => F("generatorWindow.written", "{0} Dateien geschrieben (ein Schritt, Strg+Z im Hauptfenster nimmt ihn zurück).", files);

    public static string Recompute => T("generatorWindow.recompute", "Neu berechnen");

    public static string Computed(string time) => F("generatorWindow.computed", "Berechnet {0} aus dem Formularstand", time);

    public static string FileFilterTip => T("generatorWindow.fileFilter.tip", "Dateien nach Name filtern");

    public static string Chip(string kind, int count) => kind switch
    {
        "new" => F("generatorWindow.chip.new", "Neu {0}", count),
        "changed" => F("generatorWindow.chip.changed", "Geändert {0}", count),
        "unchanged" => F("generatorWindow.chip.unchanged", "Unverändert {0}", count),
        _ => F("generatorWindow.chip.image", "Bilder {0}", count),
    };

    public static string StatusLabel(string kind) => kind switch
    {
        "new" => T("generatorWindow.status.new", "neu"),
        "changed" => T("generatorWindow.status.changed", "geändert"),
        "unchanged" => T("generatorWindow.status.unchanged", "gleich"),
        _ => T("generatorWindow.status.image", "Bild"),
    };

    public static string BlocksRemovedShort(int count) => F("generatorWindow.blocksRemoved", "{0} Block/Blöcke entfallen", count);

    public static string ResultBefore => T("generatorWindow.before", "Jetzt");

    public static string ResultAfter => T("generatorWindow.after", "Nach dem Erzeugen");

    public static string NoFile => T("generatorWindow.noFile", "(keine Datei)");

    public static string Bytes(int count) => F("generatorWindow.bytes", "{0} Byte", count);

    public static string PreviousDifference => T("generatorWindow.previousDifference", "◀ Vorherige (Umschalt+F7)");

    public static string NextDifference => T("generatorWindow.nextDifference", "Nächste (F7) ▶");

    public static string DiscardHint => T("generatorWindow.discardHint", "Zeilen links markieren und U drücken nimmt eine Änderung zurück; Strg+Z macht das rückgängig. Esc hebt die Markierung auf.");

    public static string ResultModBase => T("generatorWindow.beforeModBase", "Grundlage: Datei der Mod");

    public static string ModBaseHint => T("generatorWindow.modBaseHint",
        "Im Ausgabeordner fehlt diese Datei noch. Links steht die Datei der Mod, auf der sie aufbaut; markiert sind nur die echten Änderungen. Zurücknehmen mit U geht hier nicht.");

    public static string FromModFile => T("generatorWindow.fromModFile", "aus Mod-Datei");

    public static string FromModFileTip => T("generatorWindow.fromModFile.tip", "Neu im Ausgabeordner: die Datei der Mod mit den Änderungen dieses Laufs.");

    public static string ConfirmWarnings(string warnings) => F("generatorWindow.confirmWarnings", "Warnungen:\n{0}\n\nTrotzdem fortfahren?", warnings);

    public static string ConfirmRemoved(string blocks) => F("generatorWindow.confirmRemoved", "Diese Blöcke werden entfernt:\n{0}\n\nTrotzdem erzeugen?", blocks);

    public static string NoOutputFolder => T("generatorWindow.noOutputFolder", "Für das Ziel \"In Ausgabeordner\" bitte einen Ordner wählen.");

    public static string PlanFailed(string message) => F("generatorWindow.planFailed", "Vorschau nicht möglich: {0}", message);

    public static string KeyIndexTitle => T("generatorWindow.keyIndex.title", "Lokalisierungsschlüssel einlesen");

    public static string KeyIndexHint => T("generatorWindow.keyIndex.hint", "Prüft neue Schlüssel auf Hash-Kollisionen. Das passiert einmal je Fenster.");

    public static string PreviewCancelled => T("generatorWindow.previewCancelled", "Vorschau abgebrochen.");

    public static string UndoName(string package, string key) => F("generatorWindow.undoName", "Generator: {0} ({1})", package, key);

    public static string ModifierAdd => T("generatorWindow.modifier.add", "+ Modifier");

    public static string ModifierAddTip => T("generatorWindow.modifier.add.tip", "Modifier suchen und mit Startwert einfügen");

    public static string ModifierSearchTitle => T("generatorWindow.modifier.searchTitle", "Modifier suchen");

    public static string ModifierColumnName => T("generatorWindow.modifier.column.name", "Name");

    public static string ModifierColumnKey => T("generatorWindow.modifier.column.key", "Schlüssel");

    public static string ModifierColumnCategory => T("generatorWindow.modifier.column.category", "Kategorie");

    public static string ModifierInsert => T("generatorWindow.modifier.insert", "Einfügen");

    public static string ModifierNoneFound => T("generatorWindow.modifier.none", "Keine Modifier gefunden. Lädt PMT sie gerade noch, erscheinen sie hier von selbst.");

    public static string ModifierInserted(string name) => F("generatorWindow.modifier.inserted", "{0} eingefügt.", name);

    public static string ModifierAlreadyThere(string key) => F("generatorWindow.modifier.alreadyThere", "{0} steht schon im Feld.", key);

    public static string ModifierRemoveTip => T("generatorWindow.modifier.remove.tip", "Modifier entfernen");

    public static string SwitchCopy => T("generatorWindow.switch.copy", "Auf höhere Stufen kopieren");

    public static string SwitchCopyTip => T("generatorWindow.switch.copy.tip", "Ein über \"+ Modifier\" eingefügter Modifier landet auch in allen höheren Stufen derselben Idee. Stufen, die ihn schon haben, bleiben unverändert.");

    public static string SwitchScale => T("generatorWindow.switch.scale", "Werte skalieren");

    public static string SwitchScaleTip => T("generatorWindow.switch.scale.tip", "Kopierte Werte wachsen mit der Stufe: Wert × Stufe / Einfügestufe, z. B. 0.05 → 0.1 → 0.15 → 0.2. Ja/Nein-Werte werden nur kopiert.");

    public static string SwitchRemoveAll => T("generatorWindow.switch.removeAll", "Löschen übertragen");

    public static string LookupButton => T("generatorWindow.lookup.button", "Aus Datenbank");

    public static string LookupButtonTip => T("generatorWindow.lookup.button.tip", "Name und Beschreibung aus der Ideen-Datenbank übernehmen (Advances, Reformen und Gesetze aus dem Spiel, Ideen aus der Datenbank-Datei)");

    public static string LookupTitle => T("generatorWindow.lookup.title", "Ideen-Datenbank");

    public static string LookupArt => T("generatorWindow.lookup.art", "Art");

    public static string LookupAll => T("generatorWindow.lookup.all", "Alle");

    public static string LookupTag => T("generatorWindow.lookup.tag", "Tag");

    public static string LookupColumnName => T("generatorWindow.lookup.column.name", "Name");

    public static string LookupColumnNation => T("generatorWindow.lookup.column.nation", "Nation");

    public static string LookupColumnTag => T("generatorWindow.lookup.column.tag", "Tag");

    public static string LookupColumnArt => T("generatorWindow.lookup.column.art", "Art");

    public static string LookupGeneric => T("generatorWindow.lookup.generic", "allgemein");

    public static string LookupLoading => T("generatorWindow.lookup.loading", "Wird geladen");

    public static string LookupTake => T("generatorWindow.lookup.take", "Übernehmen");

    public static string LookupChooseGameFolder => T("generatorWindow.lookup.chooseGameFolder", "Spielordner einstellen");

    public static string LookupEmpty => T("generatorWindow.lookup.empty", "Keine Einträge");

    public static string LookupCount(int shown, int total) => F("generatorWindow.lookup.count", "{0} von {1} Einträgen", shown, total);

    public static string ImageGridButton => T("generatorWindow.imageGrid.button", "Wappen wählen");

    public static string ImageGridTitle => T("generatorWindow.imageGrid.title", "Wappen wählen");

    public static string ImageGridTake => T("generatorWindow.imageGrid.take", "Übernehmen");

    public static string SwitchRemoveAllTip => T("generatorWindow.switch.removeAll.tip", "✕ an einem Modifier entfernt ihn auch aus den anderen Stufen derselben Idee. Löschen von Hand im Feld bleibt im Feld.");

    public static string Candidates => T("generatorWindow.candidates.button", "Modifier ohne Idee…");
    public static string CandidatesTip => T("generatorWindow.candidates.button.tip", "Zeigt die Modifier des Spiels, die noch keine dynamische Idee benutzt, nach Gruppen und mit Suche. Daraus lassen sich Arbeitsstände anlegen.");
    public static string CandidatesTitle => T("generatorWindow.candidates.title", "Modifier ohne Idee");
    public static string CandidatesGroupAll => T("generatorWindow.candidates.group.all", "Alle");
    public static string CandidatesGroupGeneral => T("generatorWindow.candidates.group.general", "Allgemein");
    public static string CandidatesGroupCount(string label, int total, int news) => F("generatorWindow.candidates.group.count", "{0} ({1} · {2} neu)", label, total, news);
    public static string CandidatesHide => T("generatorWindow.candidates.group.hide", "ausblenden");
    public static string CandidatesHideTip => T("generatorWindow.candidates.group.hide.tip", "Blendet die Modifier dieser Gruppe aus. Die Gruppe bleibt in der Liste, damit ein neuer Modifier darin nicht unbemerkt bleibt.");
    public static string CandidatesSearchTip => T("generatorWindow.candidates.search.tip", "Sucht im Namen und im Schlüssel, immer in allen Gruppen, auch in ausgeblendeten. Modifier „nicht als Idee“ und von der Regex getroffene erscheinen nur mit „Ausgeblendete zeigen“.");
    public static string CandidatesOnlyNew => T("generatorWindow.candidates.onlyNew", "Nur neue");
    public static string CandidatesOnlyNewTip => T("generatorWindow.candidates.onlyNew.tip", "Nur Modifier, die der jüngste Import der Script-Datenbank neu gebracht hat.");
    public static string CandidatesCustomHide => T("generatorWindow.candidates.customHide", "Ausblenden (Regex)");
    public static string CandidatesCustomHideTip => T("generatorWindow.candidates.customHide.tip", "Regulärer Ausdruck auf den Schlüssel: Treffer sind ausgeblendet, zusätzlich zu den ausgeblendeten Gruppen. Gilt für dieses Paket.");
    public static string CandidatesShowHidden => T("generatorWindow.candidates.showHidden", "Ausgeblendete zeigen");
    public static string CandidatesColumnNew => T("generatorWindow.candidates.column.new", "neu");
    public static string CandidatesColumnGroup => T("generatorWindow.candidates.column.group", "Gruppe");
    public static string CandidatesColumnState => T("generatorWindow.candidates.column.state", "Arbeitsstand");
    public static string CandidatesStateNotAsIdea => T("generatorWindow.candidates.state.notAsIdea", "nicht als Idee");
    public static string CandidatesNotAsIdea => T("generatorWindow.candidates.notAsIdea", "Nicht als Idee");
    public static string CandidatesNotAsIdeaTip => T("generatorWindow.candidates.notAsIdea.tip", "Merkt die gewählten Modifier als „nicht als Idee“ vor; sie sind dann in jeder Ansicht ausgeblendet. Mit „Ausgeblendete zeigen“ siehst du sie grau und kannst die Markierung mit „Wieder anzeigen“ zurücknehmen.");
    public static string CandidatesShowAgain => T("generatorWindow.candidates.notAsIdea.undo", "Wieder anzeigen");
    public static string CandidatesCreate => T("generatorWindow.candidates.create", "Anlegen…");
    public static string CandidatesCreateTip => T("generatorWindow.candidates.create.tip", "Legt für jeden gewählten Modifier einen neuen Arbeitsstand an. Die Werte bleiben leer.");
    public static string CandidatesStatus(int shown, int news, int hidden) => F("generatorWindow.candidates.status", "{0} Kandidaten · {1} neu · {2} ausgeblendet", shown, news, hidden);
    public static string CandidatesAskTitle => T("generatorWindow.candidates.ask.title", "Arbeitsstände anlegen");
    public static string CandidatesAskMessage(int count) => F("generatorWindow.candidates.ask.message", "Diese Angaben gelten für alle {0} neuen Arbeitsstände. Die Nummer vergibt PMT fortlaufend.", count);
    public static string CandidatesDuplicateConfirm(string list) => F("generatorWindow.candidates.duplicate.confirm", "Für diese Modifier gibt es schon Arbeitsstände:\n{0}\n\nTrotzdem neue Arbeitsstände anlegen?", list);
    public static string CandidatesNamesTitle => T("generatorWindow.candidates.names.title", "Namen der Modifier lesen");
    public static string CandidatesNamesHint => T("generatorWindow.candidates.names.hint", "Liest den offiziellen Namen der Modifier in jeder Sprache des Arbeitsstands aus der Lokalisierung von Spiel und Mod.");
    public static string CandidatesCreated(int count, string keys) => F("generatorWindow.candidates.created", "{0} Arbeitsstände angelegt: {1}", count, keys);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object[] args) => UiText.Format(key, german, args);
}
