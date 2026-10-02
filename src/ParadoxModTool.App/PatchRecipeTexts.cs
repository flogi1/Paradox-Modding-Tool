using ParadoxModTool.Localization;
using ParadoxModTool.Patches;

namespace ParadoxModTool.App;

/// <summary>
/// Texte des Fensters "Patch-Rezepte" (Spec 2026-09-30-patch-recipes, Etappe 2). Die Keys <c>patchUi.*</c> stehen in allen
/// <c>locales/*.json</c>; der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen.
/// </summary>
public static class PatchRecipeTexts
{
    public static string Title => T("patchUi.title", "Patch-Rezepte");

    public static string ModLabel => T("patchUi.mod", "Mod:");

    public static string ChooseMod => T("patchUi.chooseMod", "Mod wählen");

    public static string NoMod => T("patchUi.noMod", "Kein Mod gewählt");

    public static string NoGameFolder => T("patchUi.noGameFolder", "Spielordner nicht gefunden: bitte in den Einstellungen das Spiel (die ausführbare Datei) wählen");

    public static string RecipeLabel => T("patchUi.recipe", "Rezept");

    public static string NewRecipe => T("patchUi.newRecipe", "Neues Rezept");

    public static string Rename => T("patchUi.rename", "Umbenennen");

    public static string Delete => T("patchUi.delete", "Löschen");
    public static string Guide => T("patchUi.guide", "Anleitung");
    public static string GuideTip => T("patchUi.guide.tip", "Öffnet die Anleitung mit Beispielen (F1)");

    public static string NewPrompt => T("patchUi.newPrompt", "Name des neuen Rezepts");

    public static string RenamePrompt => T("patchUi.renamePrompt", "Neuer Name des Rezepts");

    public static string DeleteConfirm(string name) => F("patchUi.deleteConfirm", "Rezept „{0}“ löschen? Das Gedächtnis des Rezepts wird mit gelöscht, erzeugte Bereiche in den Mod-Dateien bleiben stehen.", name);

    public static string LoadErrors(int count) => F("patchUi.loadErrors", "{0} Rezeptdateien nicht lesbar", count);

    public static string GameVersion(string version) => F("patchUi.gameVersion", "Spielversion {0}", version);

    public static string TabTable => T("patchUi.tab.table", "Tabelle");

    public static string TabRules => T("patchUi.tab.rules", "Regeln");

    public static string TabPreview => T("patchUi.tab.preview", "Vorschau");

    public static string NoRecipe => T("patchUi.noRecipe", "Noch kein Rezept. Lege mit „Neues Rezept“ eines an.");

    public static string RecipesMoved(int count, string folder) =>
        F("patchUi.recipesMoved", "{0} Rezeptdateien aus dem Mod-Ordner nach {1} verschoben, damit sie nicht mit hochgeladen werden.", count, folder);

    public static string RecipesKept(string files, string oldFolder) =>
        F("patchUi.recipesKept", "Nicht verschoben, weil es sie am neuen Ort schon gibt: {0}. Sie liegen weiter in {1}; bitte von Hand prüfen und vor dem Hochladen entfernen.", files, oldFolder);

    public static string RecipesMoveFailed(string message) =>
        F("patchUi.recipesMoveFailed", "Rezepte konnten nicht aus dem Mod-Ordner verschoben werden: {0}", message);

    public static string Stale => T("patchUi.stale", "Dateien wurden seit der Vorschau geändert. Bitte die Vorschau neu berechnen.");

    public static string Unsaved(string file) => F("patchUi.unsaved", "{0} ist im Editor geöffnet und nicht gespeichert.", file);

    public static string SnapshotNote(string name) => F("patchUi.snapshotNote", "Vor Patch-Rezept „{0}“", name);

    public static string SnapshotFailed(string message) => F("patchUi.snapshotFailed", "Sicherung der Mod fehlgeschlagen: {0}", message);

    public static string WriteFailed(string message) => F("patchUi.writeFailed", "Schreiben fehlgeschlagen, alle Dateien wurden zurückgesetzt: {0}", message);

    public static string Applied(int count) => F("patchUi.applied", "{0} Dateien geschrieben", count);

    public static string SaveFailed(string message) => F("patchUi.saveFailed", "Rezept konnte nicht gespeichert werden: {0}", message);

    public static string OnlyErrors => T("patchUi.table.onlyErrors", "Nur Einträge mit Fehlern");

    public static string AddRow => T("patchUi.table.addRow", "Zeile hinzufügen");

    public static string RemoveRow => T("patchUi.table.removeRow", "Zeile entfernen");

    public static string Columns => T("patchUi.table.columns", "Spalten");

    public static string Hint => T("patchUi.table.hint", "Hinweis");

    public static string TableStatus(int total, int errors, string defaults) => F("patchUi.table.status", "{0} Einträge · {1} mit Fehlern · Standardwerte: {2}", total, errors, defaults);

    public static string NoDefaults => T("patchUi.table.noDefaults", "keine");

    public static string ColumnsTitle => T("patchUi.columns.title", "Spalten");

    public static string ColName => T("patchUi.columns.name", "Name");

    public static string ColType => T("patchUi.columns.type", "Art");

    public static string TypeText => T("patchUi.columns.typeText", "Text");

    public static string TypeNumber => T("patchUi.columns.typeNumber", "Zahl");

    public static string TypeList => T("patchUi.columns.typeList", "Liste");

    public static string ColDefault => T("patchUi.columns.default", "Standardwert");

    public static string ColTitleKey => T("patchUi.columns.titleKey", "Titel prüfen");

    public static string ColDerived => T("patchUi.columns.derived", "Abgeleitet (aus Spieldateien nachschlagen)");

    public static string ColDerivedShort => T("patchUi.columns.derivedShort", "abgeleitet");

    public static string LookupFiles => T("patchUi.columns.files", "Dateien");

    public static string LookupSource => T("patchUi.columns.source", "Quelle");

    public static string SourceVanilla => T("patchUi.columns.sourceVanilla", "Vanilla");

    public static string SourceMod => T("patchUi.columns.sourceMod", "Mod");

    public static string SourceModOverVanilla => T("patchUi.columns.sourceModOverVanilla", "Mod vor Vanilla");

    public static string LookupMatch => T("patchUi.columns.match", "Suche");

    public static string LookupTake => T("patchUi.columns.take", "Nehmen");

    public static string TakeTop => T("patchUi.columns.takeTop", "äußerster Block");

    public static string TakeInner => T("patchUi.columns.takeInner", "innerster Block");

    public static string LookupOptional => T("patchUi.columns.optional", "Optional (nicht gefunden = leer)");

    public static string ColAdd => T("patchUi.columns.add", "Hinzufügen");

    public static string ColRemove => T("patchUi.columns.remove", "Entfernen");

    public static string PlanProgressTitle => T("patchUi.plan.title", "Vorschau wird berechnet");

    public static string PlanProgressHint => T("patchUi.plan.hint", "Die Regeln laufen auf einer Kopie im Speicher, geschrieben wird nichts.");

    public static string RuleProgress(int done, int total) => F("patchUi.plan.progress", "Regel {0} von {1}", done, total);

    public static string PlanCancelled => T("patchUi.plan.cancelled", "Berechnung abgebrochen");

    public static string KindLines => T("patchUi.kind.lines", "Zeilen");

    public static string KindBlockCopy => T("patchUi.kind.blockCopy", "Block-Kopie");

    public static string KindTemplate => T("patchUi.kind.template", "Vorlage");

    public static string KindList => T("patchUi.kind.list", "Liste");

    public static string RuleToggleTip => T("patchUi.rule.toggle.tip", "Regel ein- oder ausschalten");

    public static string KindBlockEdit => T("patchUi.kind.blockEdit", "Block bearbeiten");

    public static string BlockEditCopyTip => T("patchUi.blockEdit.copy.tip", "Steht der Block nur in Vanilla, legt PMT zuerst die Mod-Kopie dieser Vanilla-Datei an und bearbeitet den Block dort. Steht er schon in einer Mod-Datei, wird er dort bearbeitet.");

    public static string AddRule => T("patchUi.rules.add", "+ Regel");

    public static string RemoveRule => T("patchUi.rules.remove", "Entfernen");

    public static string RemoveRuleConfirm(string name) => F("patchUi.rules.removeConfirm", "Regel „{0}“ entfernen?", name);

    public static string NoRules => T("patchUi.rules.none", "Noch keine Regeln");

    public static string Placeholders(string list) => F("patchUi.rules.placeholders", "Platzhalter: {0}", list);

    public static string PreviewFor => T("patchUi.rules.previewFor", "Vorschau für");

    public static string ShowResult => T("patchUi.rules.showResult", "Ergebnis zeigen");

    public static string ResultTitle(string rule, string row) => F("patchUi.result.title", "Ergebnis von „{0}“ für {1}", rule, row);

    public static string ResultBefore => T("patchUi.result.before", "Aktuell");

    public static string ResultAfter => T("patchUi.result.after", "Nach dem Anwenden");

    public static string ResultNothing => T("patchUi.result.nothing", "Keine Änderungen");

    public static string SkippedLine(string rule, string row, string reason) => F("patchUi.result.skipped", "{0} · {1}: {2}", rule, row, reason);

    public static string FieldName => T("patchUi.field.name", "Name");

    public static string FieldEnabled => T("patchUi.field.enabled", "Aktiv");

    public static string EditEnabledTip => T("patchUi.edit.enabled.tip", "Ausgeschaltet bleibt die Bearbeitung im Rezept stehen, läuft aber nicht.");

    public static string FieldScope => T("patchUi.field.scope", "Geltung");

    public static string ScopePerRow => T("patchUi.scope.perRow", "pro Zeile");

    public static string ScopeGlobal => T("patchUi.scope.global", "einmal (global)");

    public static string FieldFiles => T("patchUi.field.files", "Dateien");

    public static string FieldMatch => T("patchUi.field.match", "Suche");

    public static string FieldMatchKind => T("patchUi.field.matchKind", "Suchart");

    public static string MatchText => T("patchUi.match.text", "Text (ganze Wörter)");

    public static string MatchRegex => T("patchUi.match.regex", "Regulärer Ausdruck");

    public static string FieldAction => T("patchUi.field.action", "Aktion");

    public static string ActionComment => T("patchUi.action.comment", "Auskommentieren");

    public static string ActionDelete => T("patchUi.action.delete", "Löschen");

    public static string ActionReplace => T("patchUi.action.replace", "Ersetzen");

    public static string ActionRemoveBlock => T("patchUi.action.removeBlock", "Block entfernen");

    public static string FieldReplacement => T("patchUi.field.replacement", "Ersatz");

    public static string FollowNext => T("patchUi.field.followNext", "Folgestufen mitnehmen (next_building)");

    public static string CreateCopy => T("patchUi.field.createCopy", "Kopie aus Vanilla anlegen, wenn sie fehlt");

    public static string FieldSource => T("patchUi.field.source", "Quelle");

    public static string FieldBlock => T("patchUi.field.block", "Block");

    public static string FieldTarget => T("patchUi.field.target", "Ziel");

    public static string FieldInside => T("patchUi.field.inside", "Innerhalb (optional)");

    public static string FieldText => T("patchUi.field.text", "Text");

    public static string FieldListPath => T("patchUi.field.listPath", "Liste (Pfad)");

    public static string FieldEntry => T("patchUi.field.entry", "Eintrag");

    public static string FieldKey => T("patchUi.field.key", "Schlüssel");

    public static string FieldValue => T("patchUi.field.value", "Wert");

    public static string Edits => T("patchUi.edits", "Bearbeitungen");

    public static string AddEdit => T("patchUi.edits.add", "+ Bearbeitung");

    public static string EditDelete => T("patchUi.edits.delete", "Entfernen");

    public static string EditRemove => T("patchUi.edit.remove", "Eigenschaft entfernen");

    public static string EditInsertInto => T("patchUi.edit.insertInto", "Einfügen in");

    public static string EditInsertAfter => T("patchUi.edit.insertAfter", "Einfügen nach");

    public static string EditAtStart => T("patchUi.edit.atStart", "Am Anfang einfügen");

    public static string EditAtEnd => T("patchUi.edit.atEnd", "Am Ende einfügen");

    public static string HideUnchanged => T("patchUi.preview.hideUnchanged", "Unveränderte ausblenden");

    public static string ComputePreview => T("patchUi.preview.compute", "Vorschau berechnen");

    public static string ApplyButton(int count) => F("patchUi.preview.apply", "Anwenden ({0} Dateien)", count);

    public static string CountNew(int count) => F("patchUi.preview.countNew", "neu {0}", count);

    public static string CountChanged(int count) => F("patchUi.preview.countChanged", "geändert {0}", count);

    public static string CountConflict(int count) => F("patchUi.preview.countConflict", "Konflikt {0}", count);

    public static string StatusNew => T("patchUi.status.new", "neu");

    public static string StatusChanged => T("patchUi.status.changed", "geändert");

    public static string StatusConflict => T("patchUi.status.conflict", "Konflikt");

    public static string StatusUnchanged => T("patchUi.status.unchanged", "unverändert");

    public static string NoPlan => T("patchUi.preview.noPlan", "Noch keine Vorschau. „Vorschau berechnen“ lässt die Regeln laufen, ohne etwas zu schreiben.");

    public static string PlanDropped => T("patchUi.preview.dropped", "Rezept geändert, Vorschau neu berechnen");

    public static string PlanReady(int count) => F("patchUi.preview.ready", "Vorschau fertig: {0} Dateien betroffen", count);

    public static string ConflictBarTitle => T("patchUi.preview.conflicts", "Bausteine mit Konflikt: links deine Fassung, rechts neu erzeugt. Übernimm mit U, was bleiben soll.");

    public static string ResolveButton => T("patchUi.preview.resolve", "Als gelöst übernehmen");

    public static string ConflictLeft => T("patchUi.preview.conflictLeft", "Deine Fassung");

    public static string ConflictRight => T("patchUi.preview.conflictRight", "Neu erzeugt (an Konfliktstellen das Neue)");

    public static string SkippedHeader(int count) => F("patchUi.preview.skipped", "Übersprungene Zeilen und Warnungen ({0})", count);

    public static string ConflictsSkippedConfirm(int count) => F("patchUi.preview.conflictsConfirm", "{0} Dateien mit Konflikt werden nicht geschrieben. Trotzdem anwenden?", count);

    public static string ReapplyAfterUpdate => T("patchUi.reapplyAfterUpdate", "Diese Mod hat Patch-Rezepte. Jetzt öffnen, um sie nach dem Update neu anzuwenden?");

    public static string TabWatch => T("patchUi.watch.tab", "Wächter");

    public static string WatchHideDone => T("patchUi.watch.hideDone", "Erledigte ausblenden");

    public static string WatchPatterns => T("patchUi.watch.patterns", "Suchmuster");

    public static string WatchPatternsTip => T("patchUi.watch.patterns.tip", "Muster, nach denen der Wächter sucht; die Schlüssel der Tabellenzeilen sucht er immer mit");

    public static string WatchPatternsPrompt => T("patchUi.watch.patterns.prompt", "Ein Muster pro Zeile, z. B. every_special_building_province. Es zählen nur ganze Wörter, Leerzeichen sind egal.");

    public static string WatchRescan => T("patchUi.watch.rescan", "Neu scannen");

    public static string WatchStatusName(WatchStatus status) => status switch
    {
        WatchStatus.Uncovered => T("patchUi.watch.status.uncovered", "nicht abgedeckt"),
        WatchStatus.Outdated => T("patchUi.watch.status.outdated", "veraltet"),
        WatchStatus.OwnFile => T("patchUi.watch.status.ownFile", "eigene Datei"),
        WatchStatus.Covered => T("patchUi.watch.status.covered", "abgedeckt"),
        _ => T("patchUi.watch.status.ignored", "ignoriert"),
    };

    public static string WatchColStatus => T("patchUi.watch.col.status", "Status");

    public static string WatchColPath => T("patchUi.watch.col.path", "Datei");

    public static string WatchColFindings => T("patchUi.watch.col.findings", "Fundstellen");

    public static string WatchColNote => T("patchUi.watch.col.note", "Notiz");

    public static string WatchFindingCount(int count, string pattern) => F("patchUi.watch.findingCount", "{0}× {1}", count, pattern);

    public static string WatchCopyAndRule => T("patchUi.watch.copyAndRule", "Kopie anlegen + Regel");

    public static string WatchCopyAndRuleTip => T("patchUi.watch.copyAndRule.tip", "Nimmt die Datei in eine Zeilen-Regel auf und schaltet dort „Kopie aus Vanilla anlegen“ ein; die Kopie entsteht beim nächsten Anwenden");

    public static string WatchNewRule => T("patchUi.watch.newRule", "Neue Zeilen-Regel (aus)");

    public static string WatchIgnore => T("patchUi.watch.ignore", "Ignorieren");

    public static string WatchIgnorePrompt(string path) => F("patchUi.watch.ignore.prompt", "Notiz zu {0} (optional):", path);

    public static string WatchUnignore => T("patchUi.watch.unignore", "Nicht mehr ignorieren");

    public static string WatchIgnoreFolder => T("patchUi.watch.ignoreFolder", "Ordner ignorieren");

    public static string WatchIgnoreFolderTip => T("patchUi.watch.ignoreFolder.tip", "Ignoriert alle Dateien im Ordner der gewählten Datei, auch in Unterordnern.");

    public static string WatchIgnoreFolderPrompt(string folder) => F("patchUi.watch.ignoreFolder.prompt", "Notiz zum Ordner {0} (optional):", folder);

    public static string WatchUnignoreTip => T("patchUi.watch.unignore.tip", "Hebt das Ignorieren auf. Wurde die Datei über einen Ordner ignoriert, entfällt die Ordner-Regel und alle Dateien des Ordners zählen wieder.");

    public static string WatchOpenUpdate => T("patchUi.watch.openUpdate", "Im Update-Tool öffnen");

    public static string WatchOpen => T("patchUi.watch.open", "Öffnen");

    public static string WatchLineVanilla(int line, string text) => F("patchUi.watch.line.vanilla", "Vanilla {0}: {1}", line, text);

    public static string WatchLineMod(int line, string text) => F("patchUi.watch.line.mod", "Mod {0}: {1}", line, text);

    public static string WatchLinePlanned(int line, string text) => F("patchUi.watch.line.planned", "Nach Anwenden {0}: {1}", line, text);

    public static string WatchNotScanned => T("patchUi.watch.notScanned", "Noch nicht gescannt. „Neu scannen“ durchsucht Vanilla und Mod.");

    public static string WatchStale => T("patchUi.watch.stale", "Rezept geändert, neu scannen");

    public static string WatchFilesChanged => T("patchUi.watch.filesChanged", "Dateien seit dem Scan geändert (Anwenden), neu scannen");

    public static string WatchRuleAddedDisabled(string rule) => F("patchUi.watch.ruleAddedDisabled", "Datei zur Regel „{0}“ hinzugefügt. Die Regel ist ausgeschaltet: schalte sie im Reiter Regeln ein, sonst bleibt die Datei nicht abgedeckt.", rule);

    public static string WatchScanCancelled => T("patchUi.watch.cancelled", "Scan abgebrochen, das vorige Ergebnis bleibt");

    public static string WatchScanTitle => T("patchUi.watch.scanTitle", "Wächter: Dateien werden durchsucht");

    public static string WatchScanHint => T("patchUi.watch.scanHint", "Vanilla und Mod werden nach den Suchmustern und Zeilenschlüsseln durchsucht. Geschrieben wird nichts.");

    public static string WatchFileProgress(int done, int total) => F("patchUi.watch.progress", "Datei {0} von {1}", done, total);

    public static string WatchScanned(int files, int open) => F("patchUi.watch.scanned", "{0} Dateien mit Treffern, {1} offen", files, open);

    public static string WatchRuleAdded(string rule) => F("patchUi.watch.ruleAdded", "Datei zur Regel „{0}“ hinzugefügt. Die Kopie entsteht beim nächsten Anwenden.", rule);

    public static string WatchRuleSibling(string rule) => F("patchUi.watch.ruleSibling", "Neue Regel „{0}“ nur für diese Datei angelegt. Sie kopiert die Datei beim nächsten Anwenden aus Vanilla.", rule);

    public static string WatchRuleCreated => T("patchUi.watch.ruleCreated", "Neue Zeilen-Regel angelegt, sie ist noch ausgeschaltet");

    public static string LookupMatchTip => T("patchUi.columns.match.tip", "Mehrere Suchen mit ; trennen, z. B. special_building_slot = {key}; special_building = {key}");

    public static string LookupAll => T("patchUi.columns.all", "Alle Treffer (Liste)");

    public static string LookupAllTip => T("patchUi.columns.all.tip", "Statt des ersten Treffers alle als Liste (a, b, c). In Vorlagen wiederholt {*spalte}…{/spalte} einen Text je Wert.");

    public static string VanillaPathsOnly => T("patchUi.field.vanillaPathsOnly", "Nur Dateien, die es auch in Vanilla gibt");

    public static string VanillaPathsOnlyTip => T("patchUi.field.vanillaPathsOnly.tip", "Wirkt nur auf Mod-Kopien von Vanilla-Dateien. Eigene Dateien der Mod im selben Ordner bleiben unberührt.");

    public static string AdoptExisting => T("patchUi.field.adoptExisting", "Bestehende Blöcke übernehmen");

    public static string AdoptExistingTip => T("patchUi.field.adoptExisting.tip", "Ist ein Schlüssel schon anderswo in der Mod definiert, holt PMT diesen Block in den erzeugten Bereich, baut ihn mit den Bearbeitungen um und entfernt ihn aus seiner Datei. Deine Änderungen darin bleiben erhalten.");

    public static string AdoptionLost(string keys) => F("patchUi.preview.adoptionLost", "Nicht angewendet: {0} ginge verloren. Der übernommene Baustein ist mit U zurückgenommen, das Entfernen aus der Quelldatei aber nicht. Nimm beides zurück oder keins, oder berechne die Vorschau neu.", keys);

    public static string AdoptButton(int count) => F("patchUi.preview.adopt", "Bestehende Blöcke übernehmen ({0})", count);

    public static string NewEmpty => T("patchUi.new.empty", "Leer");

    public static string NewFromPreset => T("patchUi.new.preset", "Aus Vorlage: Special Buildings in normale Gebäude");

    public static string PresetDefaultName => T("patchUi.new.presetName", "Special Buildings");

    public static string NewFromPresetResources => T("patchUi.new.presetResources", "Aus Vorlage: Ressourcen in Special Buildings");

    public static string PresetResourcesDefaultName => T("patchUi.new.presetResourcesName", "Ressourcen in Specials");

    public static string ImportSpecials => T("patchUi.import.button", "Specials aus Vanilla einlesen");

    public static string ImportSpecialsTip => T("patchUi.import.button.tip", "Listet die Special Buildings aus Vanilla. Gewählte werden Tabellenzeilen, vorhandene Zeilen bleiben.");

    public static string ImportColKey => T("patchUi.import.col.key", "Schlüssel");

    public static string ImportColBaronies => T("patchUi.import.col.baronies", "Baronien");

    public static string ImportColInMod => T("patchUi.import.col.inMod", "In Mod noch Special");

    public static string ImportColIsRow => T("patchUi.import.col.isRow", "Schon Zeile");

    public static string ImportNoPlace => T("patchUi.import.noPlace", "kein Ort");

    public static string ImportNoPlaceTip => T("patchUi.import.noPlace.tip", "Kein Ort in der History: Vanilla vergibt den Slot per Script (Event, Entscheidung oder Großprojekt) und baut das Gebäude gleich mit, z. B. generische Minen, Lustschloss, Verbotene Stadt. Umgewandelt wäre es überall baubar und die Scripts schlügen fehl; es bleibt deshalb Special und ist nicht wählbar.");

    public static string ImportYes => T("patchUi.import.yes", "ja");

    public static string ImportOnlyInMod => T("patchUi.import.onlyInMod", "Nur in Mod noch Special");

    public static string ImportHideRows => T("patchUi.import.hideRows", "Bereits vorhandene ausblenden");

    public static string ImportSelectVisible => T("patchUi.import.selectVisible", "Alle sichtbaren wählen");

    public static string ImportSelectNone => T("patchUi.import.selectNone", "Keine");

    public static string ImportAccept => T("patchUi.import.accept", "Übernehmen");

    public static string ImportLoading => T("patchUi.import.loading", "Specials und Baronien werden gelesen");

    public static string ImportCount(int selected, int total) => F("patchUi.import.count", "{0} gewählt von {1}", selected, total);

    public static string AdoptModButton => T("patchUi.adoptMod.button", "Bestehende Mod-Gebäude übernehmen");

    public static string AdoptModButtonTip => T("patchUi.adoptMod.button.tip", "Benennt Gebäude, die deine Mod unter eigenem Namen als Kopie eines Vanilla-Gebäudes führt, auf den Vanilla-Schlüssel zurück. Danach kommen die Specials als Zeilen in die Tabelle.");

    public static string AdoptModColOld => T("patchUi.adoptMod.col.old", "Mod-Schlüssel");

    public static string AdoptModColNew => T("patchUi.adoptMod.col.new", "Vanilla-Schlüssel");

    public static string AdoptModColStatus => T("patchUi.adoptMod.col.status", "Status");

    public static string AdoptModColFile => T("patchUi.adoptMod.col.file", "Datei");

    public static string AdoptModOnlyProposals => T("patchUi.adoptMod.onlyProposals", "Nur mit Vorschlag");

    public static string AdoptModWasComments => T("patchUi.adoptMod.wasComments", "Kommentar # was: <alter Schlüssel> einfügen");

    public static string AdoptModLoading => T("patchUi.adoptMod.loading", "Mod-Gebäude und Lokalisierung werden gelesen");

    public static string AdoptModNone => T("patchUi.adoptMod.none", "Keine Gebäude mit eigenem Namen gefunden, die nur deine Mod definiert");

    public static string AdoptModNoProposals(int count) => F("patchUi.adoptMod.noProposals", "Keine Vorschläge: alle {0} Gebäude sind eigene ohne Vanilla-Gegenstück", count);

    public static string AdoptModStatusOk => T("patchUi.adoptMod.status.ok", "ok");

    public static string AdoptModStatusNoProposal => T("patchUi.adoptMod.status.noProposal", "kein Vorschlag");

    public static string AdoptModStatusAmbiguous(string candidates) => F("patchUi.adoptMod.status.ambiguous", "mehrdeutig: {0}", candidates);

    public static string AdoptModStatusTargetDefined(string where) => F("patchUi.adoptMod.status.targetDefined", "schon definiert in {0}", where);

    public static string AdoptModStatusTargetInVanilla(string file) => F("patchUi.adoptMod.status.targetInVanilla", "Vanilla definiert es in {0}, die Mod überdeckt die Datei nicht", file);

    public static string AdoptModStatusTargetTwice => T("patchUi.adoptMod.status.targetTwice", "Ziel doppelt vergeben");

    public static string AdoptModStatusInvalid => T("patchUi.adoptMod.status.invalid", "ungültiger Schlüssel");

    public static string AdoptModSaveWarning(int count) => F("patchUi.adoptMod.saveWarning", "Die Umbenennung von {0} Gebäuden macht laufende Spielstände dieser Mod unbrauchbar: bereits gebaute Gebäude verschwinden. Fortfahren?", count);

    public static string AdoptModPlanReady(int count) => F("patchUi.adoptMod.planReady", "Umbenennung von {0} Gebäuden bereit. Anwenden schreibt sie als einen Rückgängig-Schritt; danach kommen die Specials als Zeilen in die Tabelle", count);

    public static string AdoptModDone(int renamed, int rows) => F("patchUi.adoptMod.done", "{0} Gebäude umbenannt, {1} als Zeilen übernommen. Die Vorschau zeigt jetzt den Rezeptlauf", renamed, rows);

    public static string NoRowsHint => T("patchUi.preview.noRows", "Die Tabelle hat keine Zeilen, deshalb gibt es nichts zu tun. Lies zuerst Zeilen ein oder lege welche an (Reiter Tabelle).");

    public static string TopUp => T("patchUi.rules.topUp", "Fehlendes aus der Vorlage ergänzen");

    public static string TopUpTip => T("patchUi.rules.topUp.tip", "Ergänzt, was dem Rezept gegenüber seiner Vorlage fehlt: Regeln, Spalten, Bearbeitungen und Wächter-Suchmuster. Vorhandenes bleibt unverändert.");

    public static string TopUpNone => T("patchUi.rules.topUp.none", "Dem Rezept fehlt nichts aus der Vorlage.");

    public static string TopUpConfirm(string summary) => F("patchUi.rules.topUp.confirm", "Aus der Vorlage fehlen im Rezept:\n{0}\nErgänzen? Vorhandenes bleibt unverändert.", summary);

    public static string TopUpRules(string names) => F("patchUi.rules.topUp.rules", "• Regeln: {0}", names);

    public static string TopUpColumns(string names) => F("patchUi.rules.topUp.columns", "• Spalten: {0}", names);

    public static string TopUpEdits(string names) => F("patchUi.rules.topUp.edits", "• Bearbeitungen: {0}", names);

    public static string TopUpPatterns(string names) => F("patchUi.rules.topUp.patterns", "• Wächter-Suchmuster: {0}", names);

    public static string TopUpDone(int count) => F("patchUi.rules.topUp.done", "{0} Teile aus der Vorlage ergänzt", count);

    public static string WholeRows => T("patchUi.rules.wholeRows", "Zeilen ganz oder gar nicht");

    public static string WholeRowsTip => T("patchUi.rules.wholeRows.tip", "Überspringt eine Regel eine Zeile (z. B. „bereits definiert“), lassen alle Regeln sie aus. So wird keine Zeile halb umgebaut.");

    public static string FilesTip => T("patchUi.field.files.tip", "Pfad ab dem Spielordner; * und ** für Ordner, mehrere Muster mit ; trennen");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
