using ParadoxModTool.Localization;

namespace ParadoxModTool.Compatch;

/// <summary>
/// Alle Oberflächen- und Meldungstexte des Kompatch-Merges. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>;
/// der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>CompatchTextsTests</c>). Jeder Aufruf von
/// <c>T</c>/<c>F</c> steht in einer Zeile. Beschriftungen enden nie mit Auslassungspunkten.
/// </summary>
public static class CompatchTexts
{
    // Stufen, Herkunft, Formen, Entscheidungen.
    public static string Stage(MergeStage stage) => stage switch
    {
        MergeStage.Conflict => T("compatch.stage.conflict", "Konflikt"),
        MergeStage.Review => T("compatch.stage.review", "Prüfen"),
        MergeStage.Automatic => T("compatch.stage.automatic", "Automatisch"),
        MergeStage.FromExistingPatch => T("compatch.stage.fromPatch", "Aus bestehendem Patch"),
        MergeStage.Decided => T("compatch.stage.decided", "Entschieden"),
        MergeStage.Outdated => T("compatch.stage.outdated", "Veraltet"),
        _ => T("compatch.stage.notNeeded", "Nicht nötig"),
    };

    public static string Origin(MergeOrigin? origin) => origin switch
    {
        MergeOrigin.A => T("compatch.origin.a", "aus A"),
        MergeOrigin.B => T("compatch.origin.b", "aus B"),
        MergeOrigin.Both => T("compatch.origin.both", "A und B gleich"),
        _ => T("compatch.origin.conflict", "A und B verschieden"),
    };

    public static string NoteMissingInTarget => T("compatch.note.missingInTarget", "fehlte im Ziel");

    public static string Form(PatchForm form) => form switch
    {
        PatchForm.Inject => T("compatch.form.inject", "INJECT: (nur Ergänzungen)"),
        PatchForm.Replace => T("compatch.form.replace", "REPLACE: (ganzer Schlüssel)"),
        PatchForm.PlainKey => T("compatch.form.plain", "Ohne Präfix (eigene Datei)"),
        _ => T("compatch.form.wholeFile", "Ganze Datei"),
    };

    public static string Decision(DecisionKind kind) => kind switch
    {
        DecisionKind.TakeA => T("compatch.decision.a", "A übernehmen"),
        DecisionKind.TakeB => T("compatch.decision.b", "B übernehmen"),
        DecisionKind.TakeVanilla => T("compatch.decision.vanilla", "Vanilla übernehmen"),
        _ => T("compatch.decision.manual", "Von Hand"),
    };

    public static string TakeMod(string name) => F("compatch.decision.takeMod", "{0} übernehmen", name);

    public static string Skip(SkipReason reason, string kind, int count) => reason switch
    {
        SkipReason.HiddenKind => F("compatch.skip.hidden", "{0} Dateien der Art {1} ausgeblendet", count, kind),
        SkipReason.Locked => F("compatch.skip.locked", "{0} gesperrte Dateien nicht eingelesen", count),
        SkipReason.OwnPatch => F("compatch.skip.ownPatch", "{0} PMT-Patchdateien der Basis-Mod nicht eingelesen", count),
        _ => F("compatch.skip.metadata", "{0} Metadaten-Dateien übersprungen", count),
    };

    public static string RoleText(string targets) => F("compatch.role", "Kompatch für {0}", targets);

    // Schreiben.
    public static string OwnFileHeader => T("compatch.write.header", "Von PMT erzeugt (Kompatch-Merge). PMT schreibt diese Datei beim nächsten Schreiben neu.");

    public static string IssueDuplicate(string key, string files) => F("compatch.issue.duplicate", "\"{0}\" steht mehrfach in Patch-Dateien ({1}). Dort schreibt PMT nichts.", key, files);

    public static string IssueLocked(string path) => F("compatch.issue.locked", "Gesperrt, nicht geschrieben: {0}", path);

    public static string IssueNoSource(string path) => F("compatch.issue.noSource", "Für die ganze Datei gibt es keine Quelle (keine Vanilla-Fassung): {0}", path);

    public static string IssueSameFile(string path) => F("compatch.issue.sameFile", "Die Datei ist schon die Fassung der Basis-Mod und wirkt nur, wenn die Basis-Mod nach der anderen lädt: {0}", path);

    public static string IssueUnreadable(string path, string error) => F("compatch.issue.unreadable", "Patch-Datei nicht lesbar: {0} ({1})", path, error);

    public static string IssueRemovalAmbiguous(string key, string path) => F("compatch.issue.removal", "\"{0}\" ist in {1} nicht eindeutig zu finden und wurde nicht entfernt.", key, path);

    public static string IssueKeptChangedFile(string path) => F("compatch.issue.keptFile", "Früher geschriebene Datei wurde seitdem geändert und bleibt: {0}", path);

    public static string WriteFailed(string error, string snapshot) => F("compatch.write.failed", "Schreiben fehlgeschlagen: {0}. Der Stand vorher liegt im Snapshot {1}.", error, snapshot);

    public static string CautiousBlocked(int open) => F("compatch.write.cautious", "Vorsichtig: {0} Aktionen sind noch nicht entschieden.", open);

    // Neuer Kompatch, Arbeitsstand.
    public static string NewCompatchExists(string folder) => F("compatch.new.exists", "Den Ordner gibt es schon: {0}", folder);

    public static string NewCompatchNameEmpty => T("compatch.new.nameEmpty", "Der Name des Kompatchs darf nicht leer sein.");

    public static string NewCompatchDescription(string names) => F("compatch.new.description", "Kompatibilitätspatch für {0}.", names);

    public static string StateUnreadable => T("compatch.state.unreadable", "Der Arbeitsstand war nicht lesbar; eine Kopie liegt als .bad daneben. Es geht mit einem leeren Stand weiter.");

    // Menü und Einstellungen.
    public static string MenuCautious => T("compatch.menu.cautious", "Kompatch-Merge: vorsichtig schreiben");

    public static string MenuCautiousTip => T("compatch.menu.cautious.tip", "Voreinstellung für neue Läufe: Patch erst schreiben, wenn alle Konflikte und Prüfstellen entschieden sind");

    public static string MenuSkipLocked => T("compatch.menu.skipLocked", "Kompatch-Merge: gesperrte Dateien nicht einlesen");

    public static string MenuSkipLockedTip => T("compatch.menu.skipLocked.tip", "Voreinstellung für neue Läufe: gesperrte Dateien eigener Mods werden nicht verglichen");

    public static string MenuDefaultView => T("compatch.menu.defaultView", "Kompatch-Merge: Standardansicht");

    public static string MenuDefaultViewTip => T("compatch.menu.defaultView.tip", "Ansicht, mit der das Merge-Fenster öffnet. Ein Wechsel im Fenster wird ebenfalls gemerkt.");

    // Ansichten.
    public static string ViewLabel => T("compatch.view.label", "Ansicht");

    public static string View(CompatchView view) => view switch
    {
        CompatchView.Document => T("compatch.view.document", "B: Ergebnisdokument"),
        CompatchView.Cards => T("compatch.view.cards", "C: Review-Karten"),
        _ => T("compatch.view.actions", "A: Aktionen und Spalten"),
    };

    // Startdialog.
    public static string StartTitle => T("compatch.start.title", "Kompatch-Merge starten");

    public static string ModA => T("compatch.start.modA", "Mod A");

    public static string ModB => T("compatch.start.modB", "Mod B");

    public static string FromRegister => T("compatch.start.fromRegister", "Aus Register");

    public static string FromWorkshop => T("compatch.start.fromWorkshop", "Aus Workshop");

    public static string FromFolder => T("compatch.start.fromFolder", "Ordner");

    public static string NotChosen => T("compatch.start.notChosen", "(nicht gewählt)");

    public static string BLoadsAfterA => T("compatch.start.bAfterA", "B lädt nach A");

    public static string TargetHeader => T("compatch.start.target", "Ziel");

    public static string TargetNew => T("compatch.start.targetNew", "Neuer Kompatch");

    public static string TargetNewName => T("compatch.start.targetNewName", "Name");

    public static string TargetExisting => T("compatch.start.targetExisting", "Bestehender Kompatch oder eigene Mod");

    public static string TargetBase => T("compatch.start.targetBase", "In eine der beiden Mods schreiben (Addon oder Basis-Mod)");

    public static string TargetBaseTip => T("compatch.start.targetBase.tip", "Die andere Mod (Hauptmod) wird in die gewählte eigene Mod eingearbeitet, zum Beispiel in ein Addon, das eine Mod an eine große Mod anpasst. Die gewählte Mod sollte nach der Hauptmod laden.");

    public static string MainAsBase => T("compatch.start.mainAsBase", "Hauptmod ersetzt Vanilla (nur gegen die Hauptmod vergleichen)");

    public static string MainAsBaseTip => T("compatch.start.mainAsBase.tip", "Für Mods, die Vanilla praktisch komplett ersetzen (Beispiel aus EU IV: Anbennar). PMT vergleicht dann nur gegen die Hauptmod. Alles, was das Ziel anders macht, gilt als gewollt.");

    public static string FillMissing => T("compatch.start.fillMissing", "Fehlende Blöcke der Hauptmod übernehmen");

    public static string FillMissingTip => T("compatch.start.fillMissing.tip", "Blöcke und Einträge der Hauptmod, die im Ziel ganz fehlen, werden automatisch übernommen. Andere Werte im Ziel bleiben. Jede Übernahme lässt sich mit ↶ zurücknehmen.");

    public static string WriteModeHeader => T("compatch.start.writeMode", "Schreibmodus");

    public static string WriteStandard => T("compatch.start.writeStandard", "Standard: jederzeit schreibbar, offene Konflikte werden markiert");

    public static string WriteCautious => T("compatch.start.writeCautious", "Vorsichtig: erst schreiben, wenn alles entschieden ist");

    public static string FilterHeader => T("compatch.start.filter", "Dateiarten ausblenden");

    public static string RememberDefault => T("compatch.start.remember", "Als Voreinstellung merken");

    public static string SkipLocked => T("compatch.start.skipLocked", "Gesperrte Dateien nicht einlesen");

    public static string Start => T("compatch.start.start", "Starten");

    public static string Cancel => T("compatch.common.cancel", "Abbrechen");

    public static string Ok => T("compatch.common.ok", "OK");

    public static string Close => T("compatch.common.close", "Schließen");

    public static string ErrorPickBoth => T("compatch.error.pickBoth", "Wähle Mod A und Mod B.");

    public static string ErrorSame => T("compatch.error.same", "Mod A und Mod B sind derselbe Ordner.");

    public static string ErrorMissingFolder(string folder) => F("compatch.error.missingFolder", "Der Ordner fehlt: {0}", folder);

    public static string ErrorNoTarget => T("compatch.error.noTarget", "Wähle ein Ziel.");

    public static string ErrorTargetIsSide => T("compatch.error.targetIsSide", "Ein bestehender Kompatch darf nicht Mod A oder Mod B sein.");

    public static string ErrorBaseNotOwn => T("compatch.error.baseNotOwn", "Die Basis-Mod muss Mod A oder Mod B sein und im Register stehen.");

    public static string ErrorNoModRoot => T("compatch.error.noModRoot", "Der Mod-Ordner des Spiels ist unbekannt.");

    public static string ErrorNoVanilla => T("compatch.error.noVanilla", "Der Spielordner ist unbekannt. Stelle die Spiel-Exe in PMT ein.");

    public static string ErrorWorkshopTarget => T("compatch.error.workshopTarget", "In Workshop-Mods schreibt PMT nicht.");

    public static string AskMakeCompatch(string name) => F("compatch.ask.makeCompatch", "\"{0}\" als Kompatch für Mod A und Mod B festlegen?", name);

    public static string AskReplaceState => T("compatch.ask.replaceState", "Der gespeicherte Arbeitsstand dieses Ziels gehört zu einem anderen Mod-Paar. Neu beginnen?");

    public static string NoDependency(string names) => F("compatch.new.noDependency", "Ohne Abhängigkeitseintrag (keine id in den Metadaten): {0}", names);

    public static string DefaultName(string a, string b) => F("compatch.new.defaultName", "{0} + {1} Kompatch", a, b);

    // Merge-Fenster.
    public static string WindowTitle(string a, string b, string target) => F("compatch.window.title", "Kompatch-Merge: {0} + {1} nach {2}", a, b, target);

    public static string FilterStage => T("compatch.window.filterStage", "Stufe");

    public static string FilterAll => T("compatch.window.filterAll", "Alle");

    public static string FilterOpen => T("compatch.window.filterOpen", "Offen");

    public static string FilterOrigin => T("compatch.window.filterOrigin", "Herkunft");

    public static string FilterFolder => T("compatch.window.filterFolder", "Ordner");

    public static string FilterUncertain => T("compatch.window.filterUncertain", "Nur unsichere");

    public static string Search => T("compatch.window.search", "Suche");

    public static string RecheckAll => T("compatch.window.recheck", "Alles neu prüfen");

    public static string SaveState => T("compatch.window.saveState", "Arbeitsstand speichern");

    public static string WritePatch => T("compatch.window.write", "Patch schreiben");

    public static string CautiousToggle => T("compatch.window.cautious", "Vorsichtig");

    public static string Counters(int conflicts, int review, int automatic, int other, int seen, int total) => F("compatch.window.counters", "Konflikte {0} · Prüfen {1} · Automatisch {2} · Sonstige {3} · Gesehen {4}/{5}", conflicts, review, automatic, other, seen, total);

    public static string ShowSkipped => T("compatch.window.showSkipped", "einblenden");

    public static string ChangedNotice(string name) => F("compatch.window.changed", "{0} hat sich geändert.", name);

    public static string Rerun => T("compatch.window.rerun", "Neu prüfen");

    public static string TabActions => T("compatch.window.tabActions", "Aktionen");

    public static string TabPatchFiles => T("compatch.window.tabFiles", "Patch-Dateien");

    public static string Loading => T("compatch.window.loading", "Lese und vergleiche");

    public static string Loaded(int count, double seconds) => F("compatch.window.loaded", "{0} Aktionen in {1:0.0} s", count, seconds);

    public static string RunFailed(string message) => F("compatch.window.failed", "Lauf fehlgeschlagen: {0}", message);

    public static string Cancelled => T("compatch.window.cancelled", "Abgebrochen.");

    public static string StateSaved => T("compatch.window.saved", "Arbeitsstand gespeichert.");

    // Ansicht A.
    public static string NoSelection => T("compatch.detail.none", "Wähle links eine Aktion.");

    public static string ColumnA(string name) => F("compatch.detail.columnA", "A: {0}", name);

    public static string ColumnResult => T("compatch.detail.columnResult", "Ergebnis");

    public static string ColumnB(string name) => F("compatch.detail.columnB", "B: {0}", name);

    public static string ColumnVanilla => T("compatch.detail.columnVanilla", "Vanilla");

    public static string ColumnBase(string name) => F("compatch.detail.columnBase", "Basis: {0}", name);

    public static string WholeKey => T("compatch.detail.wholeKey", "Ganzer Schlüssel");

    public static string VanillaColumn => T("compatch.detail.vanillaColumn", "Vanilla-Spalte");

    public static string HideComments => T("compatch.detail.hideComments", "Kommentarzeilen ausblenden");

    public static string HideCommentsTip => T("compatch.detail.hideComments.tip", "Blendet Zeilen aus, die mit # beginnen (auskommentierter Code). Gilt für alle Ansichten und den Vergleich. Nur Anzeige; Entscheidungen und Patch bleiben unverändert.");

    public static string Edit => T("compatch.detail.edit", "Bearbeiten");

    public static string Confirm => T("compatch.detail.confirm", "Passt");

    public static string UndoLabel => T("compatch.detail.undo", "↶ Rückgängig");

    public static string Reset => T("compatch.detail.reset", "Zurücksetzen");

    public static string ApplyToEqual(int count) => F("compatch.detail.applyToEqual", "Für alle gleichen übernehmen ({0})", count);

    public static string TreeConflicts(int count) => F("compatch.tree.conflicts", "{0} Konflikte", count);

    public static string ApplyToEqualTip =>T("compatch.detail.applyToEqual.tip", "Überträgt die angezeigte Entscheidung auf alle Stellen mit gleichem Inhalt in A und B, auch in anderen Dateien. Stellen, die du schon selbst entschieden hast, bleiben.");

    public static string Seen => T("compatch.detail.seen", "Gesehen");

    public static string Apply => T("compatch.detail.apply", "Übernehmen");

    public static string FormLabel => T("compatch.detail.form", "Form");

    public static string Uncertain => T("compatch.detail.uncertain", "unsicher");

    public static string Open => T("compatch.detail.open", "offen");

    public static string DecisionLabel(string text) => F("compatch.detail.decision", "Entscheidung: {0}", text);

    public static string ExplicitMark => T("compatch.detail.explicit", "(von dir)");

    public static string WholeFileChoice(string side) => F("compatch.detail.wholeFile", "Ganze Datei aus {0}", side);

    public static string ManualParseError(string message) => F("compatch.detail.parseError", "Eigener Text nicht lesbar: {0}", message);

    public static string ExistingPatchNote => T("compatch.detail.existingPatch", "Vorauswahl aus dem bestehenden Patch");

    public static string NormalizedNote => T("compatch.detail.normalized", "Die Datei wird aus Einträgen neu geschrieben; Leerzeilen zwischen Einträgen gehen verloren.");

    // Ansicht B.
    public static string DocFileRest => T("compatch.doc.fileRest", "(Rest der Datei)");

    public static string DocWholeFile => T("compatch.doc.wholeFile", "(ganze Datei)");

    public static string DocRemoved => T("compatch.doc.removed", "(entfernt)");

    public static string DocPrevious => T("compatch.doc.previous", "◀ Vorherige");

    public static string DocNext => T("compatch.doc.next", "Nächste ▶");

    public static string DocPosition(int index, int count, string path) => F("compatch.doc.position", "Aktion {0}/{1}: {2}", index, count, path);

    public static string DocDocuments => T("compatch.doc.documents", "Dokumente");

    public static string DocSources => T("compatch.doc.sources", "Quellwerte der gewählten Stelle");

    // Ansicht C.
    public static string OtherSide => T("compatch.cards.otherSide", "Andere Seite");

    public static string CardJumpTip => T("compatch.cards.jumpTip", "Klick auf die Karte öffnet die Aktion in Ansicht A");

    public static string MoreLines(int count) => F("compatch.cards.moreLines", "+{0} Zeilen", count);

    public static string SeenProgress(int seen, int total) => F("compatch.cards.seen", "Gesehen {0}/{1}", seen, total);

    public static string NoActions => T("compatch.cards.none", "Keine Aktionen für diesen Filter.");

    public static string Compare => T("compatch.compare.button", "Vergleich");

    public static string CompareTitle(string key, string file) => F("compatch.compare.title", "Vergleich: {0} ({1})", key, file);

    public static string ShowFolders => T("compatch.cards.showFolders", "Ordner anzeigen");

    public static string AllFiles => T("compatch.cards.all", "Alle");

    public static string CardFileTip(string mod) => F("compatch.cards.fileTip", "Datei, die im Spiel ohne Patch gilt, aus {0}", mod);

    // Farblegende (alle Ansichten).
    public static string LegendA(string name) => F("compatch.legend.a", "aus A ({0})", name);

    public static string LegendB(string name) => F("compatch.legend.b", "aus B ({0})", name);

    public static string LegendReview => T("compatch.legend.review", "prüfen oder eigener Text");

    public static string LegendConflict => T("compatch.legend.conflict", "offener Konflikt");

    public static string LegendGap => T("compatch.legend.gap", "fehlt in dieser Spalte");

    public static string LegendTip => T("compatch.legend.tip", "Farben in Spalten, Karten und Dokument. Linker Kartenrand: rot = offener Konflikt, gelb = prüfen, veraltet oder aus bestehendem Patch, grau = automatisch oder nicht nötig.");

    // Reiter Patch-Dateien und Schreiben.
    public static string Refresh => T("compatch.files.refresh", "Aktualisieren");

    public static string HideUnchanged => T("compatch.files.hideUnchanged", "Unveränderte ausblenden");

    public static string ChangeKind(FileChangeKind kind) => kind switch
    {
        FileChangeKind.Create => T("compatch.files.create", "neu"),
        FileChangeKind.Modify => T("compatch.files.modify", "geändert"),
        FileChangeKind.Delete => T("compatch.files.delete", "gelöscht"),
        _ => T("compatch.files.unchanged", "unverändert"),
    };

    public static string IssuesHeader => T("compatch.files.issues", "Hinweise");

    public static string TargetLocked => T("compatch.write.targetLocked", "Die Ziel-Mod ist gesperrt. PMT schreibt nichts hinein.");

    public static string NothingToWrite => T("compatch.write.nothing", "Es gibt nichts zu schreiben; der Zielordner ist schon aktuell.");

    public static string WriteConfirm(int create, int modify, int delete, int issues, string folder) => F("compatch.write.confirm", "In {4} schreiben?\n\nNeu: {0}, geändert: {1}, gelöscht: {2}, Hinweise: {3}.\nVorher legt PMT einen Snapshot an.", create, modify, delete, issues, folder);

    public static string ReportTitle => T("compatch.report.title", "Patch geschrieben");

    public static string ReportSummary(int written, int deleted, string snapshot) => F("compatch.report.summary", "{0} Dateien geschrieben, {1} gelöscht. Snapshot: {2}", written, deleted, snapshot);

    public static string OrderDependentHeader => T("compatch.report.order", "Diese Einträge wirken nur, wenn der Patch nach den Mods lädt:");

    public static string NoIssues => T("compatch.report.noIssues", "Keine Hinweise.");

    // Mods-Fenster und Ziel-Dialog.
    public static string ModsSetCompatch => T("compatch.mods.set", "Als Kompatch festlegen");

    public static string ModsClearCompatch => T("compatch.mods.clear", "Kompatch aufheben");

    public static string ModsOpenMerge => T("compatch.mods.open", "Kompatch-Merge öffnen");

    public static string ReferenceRow(string name) => F("compatch.mods.reference", "↳ Kompatch: {0}", name);

    public static string TargetMissing => T("compatch.mods.targetMissing", "Ziel fehlt");

    public static string TargetsTitle(string name) => F("compatch.targets.title", "Ziele von {0}", name);

    public static string TargetsInfo => T("compatch.targets.info", "Die Mods, die dieser Kompatch zusammenführt.");

    public static string Remove => T("compatch.targets.remove", "Entfernen");

    public static string ClearConfirm(string name) => F("compatch.mods.clearConfirm", "\"{0}\" ist dann wieder eine normale Mod. Der Ordner bleibt unverändert.", name);

    // Mod-Kompatibilität.
    public static string CreateCompatch => T("compatch.compat.create", "Kompatch erstellen");

    public static string CreateCompatchTip => T("compatch.compat.create.tip", "Startet den Kompatch-Merge mit den beiden Mods des gewählten Funds");

    public static string NeedTwoMods => T("compatch.compat.needTwo", "Der Fund braucht zwei Mods.");

    // Fortschritt.
    public static string ProgressTitle => T("compatch.progress.title", "Kompatch-Merge: Dateien werden gelesen und verglichen");

    public static string ProgressReadMod(string side) => F("compatch.progress.readMod", "Lese Mod {0}", side);

    public static string ProgressReadVanilla => T("compatch.progress.readVanilla", "Lese Vanilla");

    public static string ProgressCompare => T("compatch.progress.compare", "Vergleiche");

    public static string ProgressCount(string text, int done, int total) => F("compatch.progress.count", "{0} ({1}/{2})", text, done, total);

    public static string ProgressHint => T("compatch.progress.hint", "Das kann bei großen Mods eine Weile dauern.");

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
