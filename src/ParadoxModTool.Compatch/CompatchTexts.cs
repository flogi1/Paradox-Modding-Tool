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

    /// <summary>Blockknoten für den Dateirest (alles außerhalb von Schlüsseln).</summary>
    public static string FileRestBlock => T("compatch.tree.fileRestBlock", "Dateirest");

    public static string NoteAmbiguous => T("compatch.note.ambiguous", "Zuordnung unklar");

    public static string NoteLogicBlock => T("compatch.note.logicBlock", "Logikblock");

    public static string NoteNoOldVersion => T("compatch.note.noOldVersion", "keine alte Vanilla-Version, deine Fassung ist vorausgewählt");

    public static string NoteNotScript => T("compatch.note.notScript", "keine Skriptdatei");

    /// <summary>"nicht lesbar": derselbe Text wie in der Mod-Kompatibilitätsprüfung.</summary>
    public static string NoteUnreadable => ParadoxModTool.ModCompat.ModCompatTexts.Unreadable;

    /// <summary>
    /// Anzeigename einer Dateiart aus <see cref="CompatchFileFilter.DefaultHiddenExtensions"/>. Die deutschen Namen sind zugleich die
    /// Kennungen in <c>state.json</c> (<see cref="CompatchState.HiddenKinds"/>) und bleiben deshalb unübersetzt gespeichert.
    /// </summary>
    public static string FileKind(string kind) => kind switch
    {
        "Grafik" => T("compatch.fileKind.graphics", "Grafik"),
        "Modelle" => T("compatch.fileKind.models", "Modelle"),
        "Audio" => T("compatch.fileKind.audio", "Audio"),
        "Schriften" => T("compatch.fileKind.fonts", "Schriften"),
        "Metadaten" => T("compatch.fileKind.metadata", "Metadaten"),
        "Gesperrt" => T("compatch.fileKind.locked", "Gesperrt"),
        _ => kind,
    };

    public static string ModFolderMissing(string side, string folder) => F("compatch.error.modFolderMissing", "Der Ordner von Mod {0} fehlt: {1}", side, folder);

    public static string PathOutsideTarget(string path) => F("compatch.error.pathOutsideTarget", "Pfad außerhalb des Zielordners: {0}", path);

    public static string Form(PatchForm form) => form switch
    {
        PatchForm.Inject => T("compatch.form.inject", "INJECT: (nur Änderungen und Ergänzungen)"),
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
        SkipReason.HiddenKind => F("compatch.skip.hidden", "{0} Dateien der Art {1} ausgeblendet", count, FileKind(kind)),
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

    public static string MenuResultRightTip => T("compatch.menu.resultRight.tip", "Vergleichsansichten zeigen A | B | Ergebnis: der neue Stand steht ganz rechts, mit einer Trennlinie davor, statt in der Mitte");

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

    /// <summary>Hinweis im Startfenster, wenn eine Mod mit replace_path Vanilla-Ordner ausblendet.</summary>
    public static string HiddenVanilla(string modName, int count) => F("compatch.hiddenVanilla", "{0} blendet {1} Vanilla-Ordner aus", modName, count);

    /// <summary>Tooltip zu <see cref="HiddenVanilla"/>; darunter folgt die Liste der Ordner.</summary>
    public static string HiddenVanillaTip => T("compatch.hiddenVanilla.tip", "Diese Vanilla-Ordner lädt das Spiel nicht (replace_path). PMT vergleicht dort ohne Vanilla:");

    /// <summary>Hinweis im Startfenster (Crusader Kings III), wenn die später ladende Mod mit replace_path Vanilla und die Dateien der anderen Mod ausblendet.</summary>
    public static string HiddenVanillaAndMod(string modName, int count, string otherName) => F("compatch.hiddenVanillaAndMod", "{0} blendet {1} Ordner aus (Vanilla und {2})", modName, count, otherName);

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

    public static string RememberDefaultTip => T("compatch.start.remember.tip", "Speichert Dateiarten, Schreibmodus, gesperrte Dateien und die Hauptmod-Optionen als Standard für neue Arbeitsstände (Einstellungen, Seite Komp-Patch)");

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

    public static string ResumeHeader => T("compatch.start.resumeHeader", "Arbeitsstand fortsetzen");

    public static string ResumeNone => T("compatch.start.resumeNone", "Noch kein gespeicherter Arbeitsstand für dieses Spiel.");

    public static string ResumeTip => T("compatch.start.resumeTip", "Gespeicherte Arbeitsstände (Entscheidungen, übernommene Blöcke, Filter), der zuletzt gespeicherte oben. Doppelklick setzt fort.");

    public static string Resume => T("compatch.start.resume", "Fortsetzen");

    public static string ResumeEntry(string modA, string modB, string target, string saved) => F("compatch.start.resumeEntry", "{0} + {1} → {2}   (gespeichert {3})", modA, modB, target, saved);

    public static string ResumeMissing(string folder) => F("compatch.start.resumeMissing", "Ordner nicht gefunden: {0}\n\nWähle die Mods unten neu und starte, um diesen Arbeitsstand weiterzuführen.", folder);

    public static string ResumeDelete => T("compatch.start.resumeDelete", "Arbeitsstand löschen");

    public static string ResumeDeleteAsk(string entry) => F("compatch.start.resumeDeleteAsk", "Arbeitsstand löschen?\n\n{0}\n\nEntscheidungen, übernommene Blöcke und Filter gehen verloren; die Datei kommt in den Papierkorb. Die Patch-Dateien in der Mod bleiben unverändert.", entry);

    public static string ResumeDeleteFailed(string error) => F("compatch.start.resumeDeleteFailed", "Der Arbeitsstand konnte nicht gelöscht werden: {0}", error);

    public static string NewStartHeader => T("compatch.start.newHeader", "Neu starten");

    public static string AskReplaceState => T("compatch.ask.replaceState", "Der gespeicherte Arbeitsstand dieses Ziels gehört zu einem anderen Mod-Paar. Neu beginnen?");

    public static string NoDependency(string names) => F("compatch.new.noDependency", "Ohne Abhängigkeitseintrag (keine id in den Metadaten): {0}", names);

    public static string DefaultName(string a, string b) => F("compatch.new.defaultName", "{0} + {1} Kompatch", a, b);

    // Merge-Fenster.
    public static string WindowTitle(string a, string b, string target) => F("compatch.window.title", "Kompatch-Merge: {0} + {1} nach {2}", a, b, target);

    public static string FilterStage => T("compatch.window.filterStage", "Stufe");

    public static string FilterAll => T("compatch.window.filterAll", "Alle");

    public static string FilterOpen => T("compatch.window.filterOpen", "Offen");

    public static string FilterOrigin => T("compatch.window.filterOrigin", "Herkunft");

    public static string FilterUncertain => T("compatch.window.filterUncertain", "Nur unsichere");

    public static string FilterUncertainTip => T("compatch.window.filterUncertain.tip", "Zeigt nur Stellen, bei denen PMT den Vorschlag nicht sicher bestimmen konnte");

    public static string FilterHideFilled => T("compatch.window.hideFilled", "Fehlende Blöcke ausblenden");

    public static string FilterHideFilledTip => T("compatch.window.hideFilled.tip", "Blendet die Stellen aus, die \"Fehlende Blöcke der Hauptmod übernehmen\" hinzufügt. Sie werden trotzdem übernommen.");

    public static string FilterHideDecided => T("compatch.window.hideDecided", "Erledigte ausblenden");

    public static string FilterHideEqual => T("compatch.window.hideEqual", "Gleiche ausblenden");

    public static string FilterHideEqualTip => T("compatch.window.hideEqual.tip", "Blendet Stellen aus, an denen A und B dasselbe haben (Herkunft \"A und B gleich\"). Nach einem fertigen Update sind das fast alle; geschrieben werden sie trotzdem richtig.");

    public static string RemovedInBoth(string baseColumn) => F("compatch.detail.removedInBoth", "In A und B entfernt; der Text stand nur in \"{0}\" (Spalte dafür eingeblendet).", baseColumn);

    public static string FilterHideDecidedTip => T("compatch.window.hideDecided.tip", "Blendet Stellen aus, sobald du sie entschieden oder mit \"Passt\" bestätigt hast; danach ist gleich der nächste Eintrag gewählt");

    public static string DoneTip => T("compatch.tree.doneTip", "Erledigt: entschieden oder mit \"Passt\" bestätigt");

    public static string FilterHideReplaced => T("compatch.window.hideReplaced", "Doppelte Dateien ausblenden");

    public static string FilterHideReplacedTip => T("compatch.window.hideReplaced.tip", "Blendet Dateien aus, die in beiden Mods am selben Pfad liegen und nur automatisch ersetzt werden (keine Konflikte, nichts zu prüfen)");

    public static string BlockHeader(string name, int count) => F("compatch.block.header", "{0} ({1} Stellen)", name, count);

    public static string BlockSummary(int count, int open) => F("compatch.block.summary", "Block mit {0} Stellen, davon {1} offene Konflikte", count, open);

    public static string BlockTake(string mod) => F("compatch.block.take", "{0} übernehmen (ganzer Block)", mod);

    public static string BlockConfirm => T("compatch.block.confirm", "Passt (alle Stellen)");

    public static string BlockReset => T("compatch.block.reset", "Zurücksetzen (alle Stellen)");

    public static string OverwriteTitle => T("compatch.overwrite.title", "Entscheidungen überschreiben?");

    public static string OverwriteQuestion(int overwritten, int total) => F("compatch.overwrite.question", "{0} von {1} Stellen sind schon anders entschieden (oder haben übernommene Zeilen). Sollen diese Entscheidungen überschrieben werden?\n\nStrg+Z macht die Sammelentscheidung danach wieder rückgängig.", overwritten, total);

    public static string OverwriteAll => T("compatch.overwrite.all", "Alle überschreiben");

    public static string OverwriteOnlyOpen => T("compatch.overwrite.onlyOpen", "Nur offene");

    public static string NothingOpenToDecide => T("compatch.overwrite.nothingOpen", "Alle Stellen sind schon entschieden, nichts geändert.");

    public static string ConfirmAllTitle => T("compatch.confirmAll.title", "Offene Konflikte als gelöst markieren?");

    public static string ConfirmAllQuestion(int openConflicts, int total) => F("compatch.confirmAll.question", "{0} von {1} Stellen sind offene Konflikte. \"Alle als gelöst markieren\" übernimmt dort den Vorschlag (die Mod, die ohne Patch gewinnt), ohne dass du sie angesehen hast.\n\nStrg+Z macht es danach wieder rückgängig.", openConflicts, total);

    public static string ConfirmAllEverything => T("compatch.confirmAll.everything", "Alle markieren");

    public static string ConfirmAllWithoutConflicts => T("compatch.confirmAll.withoutConflicts", "Ohne offene Konflikte");

    public static string ConfirmAllNothingLeft => T("compatch.confirmAll.nothingLeft", "Darunter sind nur offene Konflikte, nichts markiert.");

    public static string BulkConfirm => T("compatch.bulk.confirm", "Alle als gelöst markieren (Vorschlag übernehmen)");

    public static string BulkTake(string mod) => F("compatch.bulk.take", "Alle: {0} übernehmen", mod);

    public static string BulkReset => T("compatch.bulk.reset", "Alle Stellen zurücksetzen");

    public static string ResetTitle => T("compatch.reset.title", "Stellen zurücksetzen?");

    public static string ResetQuestion(int count, string where) => F("compatch.reset.question", "{0} Stellen in \"{1}\" zurücksetzen? Eigene Entscheidungen und übernommene Zeilen fallen weg, jede Stelle zeigt wieder ihren Vorschlag.\n\nStrg+Z macht es danach wieder rückgängig.", count, where);

    public static string NodeSummary(int count, int open) => F("compatch.node.summary", "{0} Stellen, davon {1} offene Konflikte", count, open);

    public static string ForgottenFiles(int count, string files) => F("compatch.update.forgotten", "{0} Datei(en) seit dem letzten Schreiben verändert, ihre Entscheidungen wurden verworfen: {1}", count, files);

    public static string SearchTip => T("compatch.window.search.tip", "Sucht im Dateipfad (z. B. common/buildings), in der Stelle und im Schlüssel");

    public static string SettingsMenu => T("compatch.window.settingsMenu", "Einstellungen ▾");

    public static string SettingsMenuTip => T("compatch.window.settingsMenu.tip", "Optionen dieses Arbeitsstands. Jeder Arbeitsstand merkt sich seine eigenen Werte");

    public static string OpenSettingsWindow => T("compatch.window.openSettings", "Einstellungsfenster öffnen");

    public static string OpenSettingsWindowTip => T("compatch.window.openSettings.tip", "Öffnet die Seite Komp-Patch im Einstellungsfenster. Dort stehen die Standardwerte für neue Arbeitsstände");

    public static string RecheckAll => T("compatch.window.recheck", "Alles neu prüfen");

    public static string SaveState => T("compatch.window.saveState", "Arbeitsstand speichern");

    public static string WritePatch => T("compatch.window.write", "Patch schreiben");

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

    public static string CardJumpTip => T("compatch.cards.jumpTip", "Klick: in Ansicht A öffnen · Doppelklick oder Rechtsklick: Vergleich an dieser Stelle");

    public static string CardUndoTip => T("compatch.cards.undoTip", "Automatische Übernahme rückgängig machen (andere Seite nehmen); bei einem Konflikt die Entscheidung zurücksetzen");

    public static string CardOtherSideTip => T("compatch.cards.otherSideTip", "Die jeweils andere Mod übernehmen");

    public static string CardConfirmTip => T("compatch.cards.confirmTip", "Die angezeigte Entscheidung bestätigen");

    public static string CardEditTip => T("compatch.cards.editTip", "In Ansicht A öffnen und das Ergebnis von Hand bearbeiten");

    public static string CardCompareTip => T("compatch.cards.compareTip", "Vergleichsfenster mit dem ganzen Block öffnen, gleich an dieser Stelle");

    public static string CardSeenTip => T("compatch.cards.seenTip", "Als gesehen markieren (zählt für den Fortschritt oben)");

    public static string CardBlockTakeTip => T("compatch.cards.blockTakeTip", "Diese Mod für alle Stellen des Blocks übernehmen");

    public static string CardBlockConfirmTip => T("compatch.cards.blockConfirmTip", "Die angezeigte Entscheidung für alle Stellen des Blocks bestätigen");

    public static string MoreLines(int count) => F("compatch.cards.moreLines", "+{0} Zeilen", count);

    public static string SeenProgress(int seen, int total) => F("compatch.cards.seen", "Gesehen {0}/{1}", seen, total);

    public static string NoActions => T("compatch.cards.none", "Keine Aktionen für diesen Filter.");

    public static string Compare => T("compatch.compare.button", "Vergleich");

    public static string PreviousDifference => T("compatch.compare.previous", "Vorige Abweichung");

    public static string PreviousDifferenceTip => T("compatch.compare.previous.tip", "Zur vorigen Abweichung zwischen den Mods springen (Umschalt+F7)");

    public static string NextDifference => T("compatch.compare.next", "Nächste Abweichung");

    public static string NextDifferenceTip => T("compatch.compare.next.tip", "Zur nächsten Abweichung zwischen den Mods springen (F7)");

    public static string NoFurtherDifference => T("compatch.compare.noFurther", "Keine weitere Abweichung in dieser Richtung.");

    public static string DifferenceCount(int count) => F("compatch.compare.count", "{0} Abweichungen in diesem Block", count);

    public static string DifferencePosition(int index, int count) => F("compatch.compare.position", "Abweichung {0} von {1}", index, count);

    public static string TakeRows(int count, string source) => F("compatch.compare.takeRows", "Markierte Zeilen ({0}) aus {1} übernehmen", count, source);

    public static string TakeRowsTip => T("compatch.compare.takeRows.tip", "Genau die markierten Zeilen ersetzen die Zeilen des Ergebnisses; die Stelle bzw. der Block gilt danach als von Hand bearbeitet. Strg+Z nimmt es zurück. Ausgeblendete Kommentarzeilen fehlen im Übernommenen.");

    public static string TakeRowsFromResult => T("compatch.compare.takeRows.result", "In der Ergebnis-Spalte gibt es nichts zu übernehmen");

    public static string NothingMarked => T("compatch.compare.nothingMarked", "Erst Zeilen in Spalte A, B oder Vanilla markieren (mit gedrückter linker Maustaste), dann U drücken.");

    public static string TakeDone =>T("compatch.compare.takeDone", "Übernommen. Strg+Z macht es rückgängig.");

    public static string PreviewTitle => T("compatch.detail.previewTitle", "Vorschau");

    public static string PreviewTitleTip => T("compatch.detail.previewTip", "Hier lässt sich nichts tippen. Zeilen einer Spalte mit gedrückter linker Maustaste markieren, dann U oder Rechtsklick: übernimmt genau diese Zeilen (Strg+Z macht es rückgängig). Sonst ändern über die Knöpfe oben, mit \"Bearbeiten\" oder im Vergleichsfenster (Doppelklick auf die Stelle im Baum).");

    public static string TakeUnchanged => T("compatch.compare.takeUnchanged", "Nichts geändert: Mit den markierten Zeilen ist das Ergebnis schon so. Kommentare, Leerzeilen und Einrückung zählen beim Übernehmen nicht.");

    public static string TakeFailed(string error) => F("compatch.compare.takeFailed", "Nicht übernommen, der Block wäre so nicht lesbar: {0}", error);

    public static string TakeUndone => T("compatch.compare.takeUndone", "Letzte Änderung rückgängig gemacht.");

    public static string NothingToUndo => T("compatch.compare.nothingToUndo", "Nichts rückgängig zu machen.");

    public static string BlockTextNote => T("compatch.block.textNote", "Block von Hand zusammengestellt");

    public static string OverviewTip => T("compatch.compare.overview.tip", "Wo im Block die Mods voneinander abweichen. Klick springt zur Stelle.");

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
