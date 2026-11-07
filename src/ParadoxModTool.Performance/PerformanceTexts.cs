using System.Globalization;
using ParadoxModTool.Localization;
using ParadoxModTool.Patches.Ai;

namespace ParadoxModTool.Performance;

/// <summary>
/// Texte des Performance-Checkers. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>; der deutsche Text hier ist der
/// Rückfall und muss mit de.json übereinstimmen (Test <c>PerformanceTextsTests</c>). Jeder Aufruf von <c>T</c>/<c>F</c> steht in einer Zeile.
/// Die Einheit der Zeiten steht nur hier (<see cref="TimeUnit"/>); die Zeiten selbst sind Rohwerte des Dumps.
/// </summary>
public static class PerformanceTexts
{
    /// <summary>Einheit hinter jeder Zeit; der Dump liefert Rohwerte, es wird nicht umgerechnet.</summary>
    public static string TimeUnit => T("performance.timeUnit", "s");

    public static string Origin(RowOrigin origin) => origin switch
    {
        RowOrigin.Mod => T("performance.origin.mod", "Mod"),
        RowOrigin.Both => T("performance.origin.both", "Mod und Vanilla"),
        RowOrigin.OtherMod => T("performance.origin.otherMod", "Andere Mod"),
        RowOrigin.Vanilla => T("performance.origin.vanilla", "Vanilla"),
        _ => T("performance.origin.unknown", "Unbekannt"),
    };

    public static string VerdictName(Verdict verdict) => verdict switch
    {
        Verdict.Better => T("performance.verdict.better", "besser"),
        Verdict.Worse => T("performance.verdict.worse", "schlechter"),
        Verdict.Unchanged => T("performance.verdict.unchanged", "unverändert"),
        Verdict.NewInB => T("performance.verdict.newInB", "neu in B"),
        Verdict.MissingInB => T("performance.verdict.missingInB", "fehlt in B"),
        _ => T("performance.verdict.behaviorChanged", "Verhalten geändert"),
    };

    // Zusammenfassung eines Laufs.
    public static string ReportHeader(string runName, int? months) => months is { } m
        ? F("performance.report.header", "Profiler-Lauf {0}: {1} Monate", runName, m.ToString(CultureInfo.CurrentCulture))
        : F("performance.report.headerUnknownMonths", "Profiler-Lauf {0}: Monate unbekannt", runName);

    public static string ReportTotals => T("performance.report.totals", "Eigenzeit nach Herkunft");

    public static string ReportSum => T("performance.report.sum", "Summe");

    /// <param name="selfFormatted">Die Eigenzeit der Mod-Seite, bereits formatiert.</param>
    public static string ReportModSelf(string selfFormatted) => F("performance.report.modSelf", "Mod-Seite (Mod sowie Mod und Vanilla): {0} {1}", selfFormatted, TimeUnit);

    public static string ReportTopModTitle(int top) => F("performance.report.topModTitle", "Top {0} Mod-Zeilen (Mod und beide)", top.ToString(CultureInfo.CurrentCulture));

    public static string ReportTopAllTitle(int top) => F("performance.report.topAllTitle", "Top {0} Zeilen aller Herkünfte", top.ToString(CultureInfo.CurrentCulture));

    public static string ReportHintsTitle(int count) => F("performance.report.hintsTitle", "Hinweise ({0})", count.ToString(CultureInfo.CurrentCulture));

    public static string ReportHintLine(HintSeverity severity, string path, int line, string title) => F("performance.report.hintLine", "[{0}] {1}:{2} — {3}", HintSeverityName(severity), path, line.ToString(CultureInfo.InvariantCulture), title);

    /// <param name="calls">Bereits formatiert, ebenso <paramref name="self"/> und <paramref name="total"/>.</param>
    public static string ReportRowLine(int rank, string key, string origin, string calls, string self, string total) =>
        F("performance.report.rowLine", "{0}. {1} [{2}] Aufrufe: {3}, Eigenzeit: {4} {6}, Gesamtzeit: {5} {6}", rank.ToString(CultureInfo.CurrentCulture), key, origin, calls, self, total, TimeUnit);

    // Vergleich zweier Läufe.
    public static string ComparisonHeader(string a, string b) => F("performance.compare.header", "Vergleich der Profiler-Läufe: A = {0}, B = {1}", a, b);

    public static string MonthsSame(int months) => F("performance.compare.monthsSame", "Beide Läufe: {0} Monate", months.ToString(CultureInfo.CurrentCulture));

    public static string MonthsDiffer(int? a, int? b) => F("performance.compare.monthsDiffer", "Achtung: Die Läufe decken unterschiedlich viele Monate ab (A: {0}, B: {1}). Die Zeiten sind nicht direkt vergleichbar.", Months(a), Months(b));

    public static string MonthsUnknown => T("performance.compare.monthsUnknown", "Achtung: Die Monatszahl mindestens eines Laufs ist unbekannt. Die Zeiten sind nur bedingt vergleichbar.");

    public static string NoiseNote(NoiseSettings noise) =>
        F("performance.compare.noise", "Rauschschwellen: Zeit relativ {0}, absolut {1} {2}, Aufrufe {3}", Percent(noise.RelativeThreshold), noise.AbsoluteFloor.ToString("N3", CultureInfo.CurrentCulture), TimeUnit, Percent(noise.CallsChangeThreshold));

    /// <param name="a">Mod-Eigenzeit in Lauf A, bereits formatiert, ebenso <paramref name="b"/> und <paramref name="delta"/>; <paramref name="verdict"/> ist das Urteilswort.</param>
    public static string ComparisonModSelf(string a, string b, string delta, string verdict) => F("performance.compare.modSelf", "Mod-Eigenzeit: A {0} {3}, B {1} {3}, Änderung {2}, Urteil: {4}", a, b, delta, TimeUnit, verdict);

    public static string ComparisonRowsTitle(int top) => F("performance.compare.rowsTitle", "Die {0} zeitintensivsten Zeilen der Mod-Seite (voller Schlüssel, nach der größeren Gesamtzeit von A und B)", top.ToString(CultureInfo.CurrentCulture));

    public static string ComparisonFilesTitle(int top) => F("performance.compare.filesTitle", "Die {0} zeitintensivsten Dateien der Mod-Seite (Summe je Datei, unempfindlich gegen verschobene Zeilen)", top.ToString(CultureInfo.CurrentCulture));

    /// <param name="calls">Aufrufe als <c>A -> B</c>, bereits formatiert, ebenso <paramref name="total"/> und <paramref name="delta"/>.</param>
    public static string ComparisonRowLine(int rank, string key, string origin, string calls, string total, string delta, string verdict) =>
        F("performance.compare.rowLine", "{0}. {1} [{2}] Aufrufe: {3}, Gesamtzeit: {4} {7}, Änderung: {5}, Urteil: {6}", rank.ToString(CultureInfo.CurrentCulture), key, origin, calls, total, delta, verdict, TimeUnit);

    // Warnungen der Sitzung beim Vergleich zweier Läufe (die Monats-Warnungen sind MonthsDiffer und MonthsUnknown).
    /// <summary>Suffix hinter dem Urteil, wenn die Läufe verschieden lange gemessen wurden oder die Dauer eines Laufs unbekannt ist.</summary>
    public static string VerdictMonthsDiffer => T("performance.compare.verdictMonthsDiffer", " (Dauer ungleich oder unbekannt)");

    /// <param name="a">Name des Spielstands von Lauf A; <c>null</c> oder leer = unbekannt, ebenso <paramref name="b"/>.</param>
    public static string WarnSaveDiffers(string? a, string? b) => F("performance.warn.saveDiffers", "Achtung: Die Läufe nutzen verschiedene Spielstände (A: {0}, B: {1}). Die Zeiten sind nicht direkt vergleichbar.", Name(a), Name(b));

    public static string WarnPlaysetDiffers(string? a, string? b) => F("performance.warn.playsetDiffers", "Achtung: Die Läufe nutzen verschiedene Playsets (A: {0}, B: {1}). Die Zeiten sind nur bedingt vergleichbar.", Name(a), Name(b));

    public static string WarnModChangedSinceA => T("performance.warn.modChangedSinceA", "Hinweis: Die Moddateien haben sich seit Lauf A geändert.");

    // Messplan und Checkliste.
    public static string StepStartGame => T("performance.step.startGame", "PMT startet das Spiel mit -debug_mode");

    public static string StepLoadSave(string name) => F("performance.step.loadSave", "Spielstand \"{0}\" im Spiel laden", name);

    public static string StepLoadSaveUnknown => T("performance.step.loadSaveUnknown", "Mess-Spielstand im Spiel laden (kein Save gewählt)");

    public static string StepLoadSaveByPmt(string name) => F("performance.step.loadSaveByPmt", "PMT lädt den Spielstand \"{0}\" beim Start selbst", name);

    public static string StepSetContinue(string name) => F("performance.step.setContinue", "PMT trägt \"{0}\" als letzten Stand in continue_game.json ein (Sicherung daneben) und das Spiel lädt ihn beim Start", name);

    public static string StatusContinueWriteFailed(string message) => F("performance.status.continueWriteFailed", "continue_game.json konnte nicht geschrieben werden ({0}); bitte den Spielstand im Spiel von Hand laden.", message);

    public static string StepAutosaveHint => T("performance.step.autosaveHint", "Autosave (Intervall und beim Beenden) in den Spieleinstellungen ausschalten, sonst zeigt der letzte Stand nach dem Lauf auf ein Autosave und PMT kann das Mess-Save beim nächsten Lauf nicht selbst laden");

    public static string StepRestart(string command) => F("performance.step.restart", "In der Konsole {0} eingeben (setzt den Profiler zurück)", command);

    public static string StepPlay(int months) => F("performance.step.play", "{0} Monate mit höchster Geschwindigkeit spielen", months.ToString(CultureInfo.CurrentCulture));

    public static string StepStop(string command) => F("performance.step.stop", "In der Konsole {0} eingeben (beendet die Aufzeichnung; erst danach schreibt der Dump Daten)", command);

    public static string StepDump(string command) => F("performance.step.dump", "In der Konsole {0} eingeben (schreibt den Profiler-Dump)", command);

    public static string StepPickup => T("performance.step.pickup", "PMT holt den Dump ab, sobald die Datei fertig geschrieben ist");

    public static string GameRunningNoDebug => T("performance.plan.runningNoDebug", "Das Spiel läuft bereits, aber nicht von PMT im Debug-Modus gestartet. Beende es und starte es über PMT.");

    public static string GameRunningInDebug => T("performance.plan.runningInDebug", "Spiel läuft schon im Debug-Modus");

    // Menüpunkt, Kachel und Fenster.
    public static string Menu => T("performance.menu", "Performance-Checker");

    public static string MenuTip => T("performance.menu.tip", "Script-Profiler des Spiels messen: Lauf aufzeichnen, teuerste Stellen der Mod finden und zwei Läufe vergleichen");

    public static string NotAvailableNoProfiler => T("performance.notAvailable.noProfiler", "Dieses Spiel hat keinen Script-Profiler mit Dump.");

    public static string NotAvailableLocked => T("performance.notAvailable.locked", "Der Profiler dieses Spiels liefert in der aktuellen Version keinen brauchbaren Dump (siehe Hilfe).");

    public static string WindowTitle => T("performance.window.title", "Performance-Checker");

    public static string TabMeasure => T("performance.tab.measure", "Messung");

    public static string TabAnalyze => T("performance.tab.analyze", "Auswertung");

    public static string TabCompare => T("performance.tab.compare", "Vergleich");

    // Schritt 1 Messung.
    public static string GameAndMod(string game, string mod) => F("performance.measure.gameAndMod", "Spiel: {0}, Mod: {1}", game, mod);

    public static string NoActiveMod => T("performance.measure.noActiveMod", "Keine aktive Mod gewählt. Bitte im Hauptfenster eine Mod wählen, dann lässt sich messen.");

    public static string PlaysetLabel => T("performance.measure.playset", "Playset");

    /// <summary>Eintrag der Playset-Auswahl für den Start ohne Playset-Wechsel; <paramref name="active"/> ist der Name des aktiven Playsets oder null.</summary>
    public static string PlaysetUnchanged(string? active) => F("performance.measure.playsetUnchanged", "Ohne Playset-Wechsel ({0})", Name(active));

    public static string SaveLabel => T("performance.measure.save", "Spielstand");

    public static string SaveUnknown => T("performance.measure.saveUnknown", "unbekannt");

    public static string RunNameLabel => T("performance.measure.runName", "Laufname");

    public static string DefaultRunName(int number) => F("performance.measure.defaultRunName", "Lauf {0}", number.ToString(CultureInfo.CurrentCulture));

    public static string MonthsLabel => T("performance.measure.months", "Monate");

    public static string MonthsInvalid => T("performance.measure.monthsInvalid", "Monate: eine ganze Zahl von 1 bis 120.");

    public static string ContinueTitleLine(string title) => F("performance.measure.continueTitle", "continue_game.json nennt: {0}", title);

    public static string ContinueTitleNone => T("performance.measure.continueTitleNone", "continue_game.json nennt keinen Spielstand.");

    public static string ChecklistTitle => T("performance.measure.checklist", "Checkliste");

    public static string StepByPmt => T("performance.measure.byPmt", "PMT");

    public static string StepByYou => T("performance.measure.byYou", "Du");

    public static string CopyCommand => T("performance.measure.copyCommand", "Kopieren");

    public static string CopyCommandTip => T("performance.measure.copyCommand.tip", "Kopiert den Konsolenbefehl in die Zwischenablage; im Spiel mit Strg+V in die Konsole einfügen");

    public static string CommandCopied(string command) => F("performance.measure.commandCopied", "Befehl {0} in die Zwischenablage kopiert.", command);

    public static string SaveDisplay(string name, string date, string file) => F("performance.measure.saveDisplay", "{0}, {1} ({2})", name, date, file);

    public static string StartButton => T("performance.measure.start", "Spiel starten und auf Dump warten");

    public static string CancelWaitButton => T("performance.measure.cancelWait", "Warten abbrechen");

    public static string ImportManuallyButton => T("performance.measure.importManually", "Dump manuell wählen");

    public static string DumpFilter => T("performance.measure.dumpFilter", "Profiler-Dump (*.csv)|*.csv|Alle Dateien (*.*)|*.*");

    public static string RunsTitle => T("performance.measure.runs", "Bisherige Läufe dieser Mod");

    public static string ColumnName => T("performance.measure.column.name", "Name");

    public static string ColumnDate => T("performance.measure.column.date", "Datum");

    public static string ColumnSave => T("performance.measure.column.save", "Spielstand");

    public static string ColumnMonths => T("performance.measure.column.months", "Monate");

    public static string MenuRename => T("performance.measure.menu.rename", "Umbenennen");

    public static string MenuDelete => T("performance.measure.menu.delete", "Löschen");

    public static string MenuAnalyze => T("performance.measure.menu.analyze", "Auswerten");

    public static string MenuCompareA => T("performance.measure.menu.compareA", "Vergleich A");

    public static string MenuCompareB => T("performance.measure.menu.compareB", "Vergleich B");

    public static string RenameTitle => T("performance.measure.renameTitle", "Lauf umbenennen");

    public static string RenamePrompt => T("performance.measure.renamePrompt", "Neuer Name des Laufs:");

    public static string ConfirmDelete(string name) => F("performance.measure.confirmDelete", "Lauf \"{0}\" samt Dump-Kopie löschen? Der Original-Dump im Logs-Ordner bleibt.", name);

    // Statuszeile der Messung.
    public static string StatusWaiting(string folder, string pattern) => F("performance.status.waiting", "Warte auf den Dump ({1}) in {0}", folder, pattern);

    public static string StatusRunCreated(string name) => F("performance.status.runCreated", "Lauf \"{0}\" angelegt.", name);

    public static string StatusImportError(string message) => F("performance.status.importError", "Dump nicht übernommen: {0}", message);

    public static string StatusImportRetry => T("performance.status.importRetry", "Dump-Datei noch gesperrt, neuer Versuch in einer Sekunde.");

    public static string StatusWatchError(string message) => F("performance.status.watchError", "Dateiwächter angehalten: {0}", message);

    public static string StatusLogFolderMissing(string? folder) => F("performance.status.logFolderMissing", "Logs-Ordner nicht gefunden: {0}", Name(folder));

    public static string StatusStopped => T("performance.status.stopped", "Warten auf den Dump abgebrochen.");

    public static string StatusNotStarted => T("performance.status.notStarted", "Spiel nicht gestartet; PMT wartet nicht auf einen Dump.");

    public static string StatusActionFailed(string message) => F("performance.status.actionFailed", "Aktion fehlgeschlagen: {0}", message);

    public static string StatusErrorLogCopyFailed(string name, string message) => F("performance.status.errorLogCopyFailed", "Lauf \"{0}\" angelegt, das error.log aber nicht kopiert (Fehler-Regeln fehlen): {1}", name, message);

    // Schritt 2 Auswertung.
    /// <param name="selfTotal">Die Eigenzeit aller Zeilen, bereits formatiert.</param>
    public static string AnalyzeHeader(string runName, int? months, string selfTotal) => months is { } m
        ? F("performance.analyze.header", "Lauf {0}: {1} Monate, Eigenzeit gesamt {2} {3}", runName, m.ToString(CultureInfo.CurrentCulture), selfTotal, TimeUnit)
        : F("performance.analyze.headerUnknownMonths", "Lauf {0}: Monate unbekannt, Eigenzeit gesamt {1} {2}", runName, selfTotal, TimeUnit);

    /// <param name="self">Eigenzeit der Herkunft, bereits formatiert, ebenso <paramref name="share"/> (Anteil an der gesamten Eigenzeit).</param>
    public static string AnalyzeOriginShare(RowOrigin origin, string self, string share) => F("performance.analyze.originShare", "{0}: {1} {2} ({3})", Origin(origin), self, TimeUnit, share);

    public static string AnalyzeNoRun => T("performance.analyze.noRun", "Kein Lauf gewählt. Wähle in der Messung einen Lauf und klicke Auswerten.");

    public static string AnalyzeDumpUnreadable(string runName, string reason) => F("performance.analyze.dumpUnreadable", "Der Dump von Lauf {0} ist nicht lesbar: {1}", runName, reason);

    public static string AnalyzeOriginFilter => T("performance.analyze.originFilter", "Herkunft:");

    public static string AnalyzeFilterModSide => T("performance.analyze.filterModSide", "Mod und beide");

    public static string AnalyzeFilterAll => T("performance.analyze.filterAll", "alle");

    public static string AnalyzeFilterVanilla => T("performance.analyze.filterVanilla", "Vanilla");

    public static string AnalyzeSearchPlaceholder => T("performance.analyze.searchPlaceholder", "Stelle suchen");

    public static string AnalyzeByLocation => T("performance.analyze.byLocation", "je Datei:Zeile zusammenfassen");

    public static string AnalyzeTop => T("performance.analyze.top", "Top");

    public static string AnalyzeCopyReport => T("performance.analyze.copyReport", "Bericht kopieren");

    public static string AnalyzeReportCopied => T("performance.analyze.reportCopied", "Bericht in die Zwischenablage kopiert.");

    public static string AnalyzeOpenInEditor => T("performance.analyze.openInEditor", "Zeile im Editor öffnen");

    public static string AnalyzeNotInMod(string path) => F("performance.analyze.notInMod", "Datei liegt nicht in der Mod: {0}", path);

    public static string AnalyzeReaderWarnings(int count) => F("performance.analyze.readerWarnings", "Warnungen beim Lesen des Dumps: {0}", count.ToString(CultureInfo.CurrentCulture));

    public static string AnalyzeReaderWarningsTitle => T("performance.analyze.readerWarningsTitle", "Warnungen des Dump-Lesers");

    public static string AnalyzeHintsTitle => T("performance.analyze.hintsTitle", "Hinweise");

    public static string AnalyzeHintsTip => T("performance.analyze.hintsTip", "Hinweise aus festen Regeln zu teuren Mustern im gewählten Lauf. Der Tooltip einer Zeile erklärt den Hinweis, Doppelklick oder Enter öffnet die Stelle im Editor. Mehr in der Hilfe (F1), Kapitel „Hinweise“.");

    public static string AnalyzeHintsColumnSeverity => T("performance.analyze.hintsColumn.severity", "Schwere");

    public static string AnalyzeHintsColumnTitle => T("performance.analyze.hintsColumn.title", "Titel");

    public static string AnalyzeHintsCount(int count) => count == 0
        ? T("performance.analyze.hintsNone", "Keine Hinweise zu diesem Lauf.")
        : F("performance.analyze.hintsCount", "Hinweise: {0}", count.ToString(CultureInfo.CurrentCulture));

    public static string AnalyzeColumnRank => T("performance.analyze.column.rank", "Rang");

    public static string AnalyzeColumnLocation => T("performance.analyze.column.location", "Stelle");

    public static string AnalyzeColumnOrigin => T("performance.analyze.column.origin", "Herkunft");

    public static string AnalyzeColumnCalls => T("performance.analyze.column.calls", "Calls");

    public static string AnalyzeColumnSelf => T("performance.analyze.column.self", "Self");

    public static string AnalyzeColumnTotal => T("performance.analyze.column.total", "Total");

    // Schritt 3 Vergleich (Stelle und Herkunft teilen sich die Spaltenköpfe mit der Auswertung).
    public static string CompareLabelA => T("performance.compare.labelA", "A (vorher):");

    public static string CompareLabelB => T("performance.compare.labelB", "B (nachher):");

    /// <param name="date">Anlagedatum des Laufs, bereits formatiert.</param>
    public static string CompareRunChoice(string name, string date) => F("performance.compare.runChoice", "{0} ({1})", name, date);

    public static string CompareEmpty => T("performance.compare.empty", "Wähle zwei Läufe (A = vorher, B = nachher).");

    // Prüfzeile des Vergleichs: nur, was wirklich verglichen wurde, bekommt ein Häkchen; Unbekanntes heißt "nicht geprüft".
    public static string CheckSaveSame => T("performance.compare.checkSaveSame", "Save gleich ✓");

    public static string CheckSaveUnknown => T("performance.compare.checkSaveUnknown", "Save unbekannt (nicht geprüft)");

    public static string CheckMonthsSame => T("performance.compare.checkMonthsSame", "Dauer gleich ✓");

    public static string CheckPlaysetSame => T("performance.compare.checkPlaysetSame", "Playset gleich ✓");

    public static string CheckPlaysetUnknown => T("performance.compare.checkPlaysetUnknown", "Playset unbekannt (nicht geprüft)");

    public static string CheckModSame => T("performance.compare.checkModSame", "Mod unverändert ✓");

    public static string CheckModUnknown => T("performance.compare.checkModUnknown", "Mod-Stand nicht geprüft");

    public static string CompareNoiseRelative => T("performance.compare.noiseRelative", "Rauschen relativ %");

    public static string CompareNoiseFloor => F("performance.compare.noiseFloor", "absoluter Boden ({0})", TimeUnit);

    /// <param name="a">Mod-Eigenzeit in Lauf A, bereits formatiert, ebenso <paramref name="b"/> und <paramref name="delta"/>; <paramref name="verdict"/> ist das Urteil samt Zusatz.</param>
    public static string CompareModSelfLine(string a, string b, string delta, string verdict) => F("performance.compare.modSelfLine", "Mod-Self {0} → {1} {2} ({3}, {4})", a, b, TimeUnit, delta, verdict);

    public static string CompareViewRows => T("performance.compare.viewRows", "je Zeile");

    public static string CompareViewFiles => T("performance.compare.viewFiles", "je Datei");

    public static string FileTotalsInclusive => T("performance.compare.fileTotalsInclusive", "Gesamtzeit je Datei ist inklusiv, nicht additiv");

    public static string CompareColumnCallsA => T("performance.compare.column.callsA", "Calls A");

    public static string CompareColumnCallsB => T("performance.compare.column.callsB", "Calls B");

    public static string CompareColumnTotalA => T("performance.compare.column.totalA", "Total A");

    public static string CompareColumnTotalB => T("performance.compare.column.totalB", "Total B");

    public static string CompareColumnDelta => T("performance.compare.column.delta", "Delta");

    public static string CompareColumnVerdict => T("performance.compare.column.verdict", "Bewertung");

    public static string CompareCopy => T("performance.compare.copy", "Vergleich kopieren");

    public static string CompareCopied => T("performance.compare.copied", "Vergleich in die Zwischenablage kopiert.");

    public static string CompareNewFromB => T("performance.compare.newFromB", "Neue Messung mit B als Basis");

    // Hinweise der Regeln.
    public static string HintSeverityName(HintSeverity severity) => severity == HintSeverity.Warning
        ? T("performance.hint.severity.warning", "Warnung")
        : T("performance.hint.severity.info", "Hinweis");

    public static string HintNoErrorLog => T("performance.hint.noErrorLog", "Kein error.log zu diesem Lauf: Fehler-Regeln ausgelassen.");

    /// <summary>Warnung zum Lauf, wenn das Berechnen der Hinweise mit einem Fehler abbricht; <paramref name="message"/> ist dessen Meldung.</summary>
    public static string HintComputeFailed(string message) => F("performance.hint.computeFailed", "Hinweise: {0}", message);

    public static string HintErrorSpamTitle => T("performance.hint.errorSpam.title", "Fehler-Spam an dieser Stelle");

    /// <param name="perMinute">Einträge pro Minute Spielsitzung; <c>null</c>, wenn unbekannt.</param>
    /// <param name="message">Erste Zeile der häufigsten Meldung; wird als Argument eingesetzt, geschweifte Klammern darin sind erlaubt.</param>
    public static string HintErrorSpamDetail(int count, double? perMinute, string message) => perMinute is { } rate
        ? F("performance.hint.errorSpam.detailPerMinute", "{0} Einträge im error.log ({1} pro Minute). Jeder Eintrag kostet beim Schreiben Zeit. Häufigste Meldung: {2}", count.ToString("N0", CultureInfo.CurrentCulture), rate.ToString("N1", CultureInfo.CurrentCulture), message)
        : F("performance.hint.errorSpam.detail", "{0} Einträge im error.log. Jeder Eintrag kostet beim Schreiben Zeit. Häufigste Meldung: {1}", count.ToString("N0", CultureInfo.CurrentCulture), message);

    /// <summary>Zusatz zu <see cref="HintErrorSpamDetail"/>, wenn die Stelle auch eine Hotspot-Zeile des Dumps ist.</summary>
    public static string HintErrorSpamRowTotal(double total) => F("performance.hint.errorSpam.rowTotal", "Gesamtzeit der Zeile im Profiler: {0} {1}.", total.ToString("N3", CultureInfo.CurrentCulture), TimeUnit);

    public static string HintVanillaCopyTitle => T("performance.hint.vanillaCopy.title", "Hotspot in einer Mod-Kopie einer Vanilla-Datei");

    public static string HintVanillaCopyDetail(double total) => F("performance.hint.vanillaCopy.detail", "Gesamtzeit {0} {1} in einer Datei, die die Mod aus Vanilla überschreibt. Prüfe mit dem Compare und Update Tool, ob die Zeit im eigenen Teil oder im übernommenen Vanilla-Teil steckt.", total.ToString("N3", CultureInfo.CurrentCulture), TimeUnit);

    public static string HintManyCallsTitle => T("performance.hint.manyCalls.title", "Script Value mit sehr vielen Aufrufen");

    public static string HintManyCallsDetail(double callsPerYear) => F("performance.hint.manyCalls.detail", "Etwa {0} Aufrufe pro Spieljahr. Ändert sich das Ergebnis selten, berechne es einmal (etwa monatlich) und speichere es als globale Variable; die Aufrufer lesen dann nur die Variable.", callsPerYear.ToString("N0", CultureInfo.CurrentCulture));

    public static string HintUnguardedTitle => T("performance.hint.unguarded.title", "Variablenzugriff ohne Guard");

    /// <param name="names">Die Namen der ungeschützten Variablen, bereits sortiert und mit Komma verbunden.</param>
    /// <param name="errorCount">Einträge des error.log im ganzen obersten Block; <c>null</c>, wenn der Lauf kein error.log hat oder der Block keine Einträge.</param>
    public static string HintUnguardedDetail(string names, int? errorCount) => errorCount is { } count
        ? F("performance.hint.unguarded.detailErrors", "Gelesen ohne vorherige Prüfung im selben Block: {0}. Fehlt die Variable, schreibt jeder Aufruf einen Fehler ins error.log. Prüfe vorher mit has_variable = NAME oder exists = var:NAME, oder lies sie mit ?=. In diesem Block stehen {1} Einträge im error.log.", names, count.ToString("N0", CultureInfo.CurrentCulture))
        : F("performance.hint.unguarded.detail", "Gelesen ohne vorherige Prüfung im selben Block: {0}. Fehlt die Variable, schreibt jeder Aufruf einen Fehler ins error.log. Prüfe vorher mit has_variable = NAME oder exists = var:NAME, oder lies sie mit ?=.", names);

    /// <summary>Zusatz zu <see cref="HintUnguardedDetail"/>, wenn die Stelle eine Hotspot-Zeile des Dumps ist.</summary>
    public static string HintUnguardedRowTotal(double total) => F("performance.hint.unguarded.rowTotal", "Gesamtzeit der Zeile im Profiler: {0} {1}.", total.ToString("N3", CultureInfo.CurrentCulture), TimeUnit);

    public static string HintFrequentTickTitle => T("performance.hint.frequentTick.title", "Schwerer Block auf häufigem Tick");

    /// <param name="marker">Die gefundene Kennzeichnung, z. B. <c>ai_tick = daily</c>.</param>
    /// <param name="key">Schlüssel des obersten Blocks.</param>
    /// <param name="frequency">Wert von <c>ai_tick_frequency</c> (ohne Angabe 1).</param>
    /// <param name="periodDays">Wirksamer Abstand in Tagen: Tage je Tick mal <paramref name="frequency"/>.</param>
    public static string HintFrequentTickDetail(string marker, string key, int frequency, long periodDays, double total) => F("performance.hint.frequentTick.detail", "{1} läuft häufig ({0}, ai_tick_frequency = {2}, Abstand in Tagen: {3}) und kostet insgesamt {4} {5}. ai_tick_frequency erhöhen oder den Trigger enger fassen; ein seltenerer Tick kann die KI passiver machen, engere Trigger sind darum oft besser.", marker, key, frequency.ToString("N0", CultureInfo.CurrentCulture), periodDays.ToString("N0", CultureInfo.CurrentCulture), total.ToString("N3", CultureInfo.CurrentCulture), TimeUnit);

    /// <summary>Wie <see cref="HintFrequentTickDetail"/>, wenn der Abstand nicht bestimmbar ist (z. B. <c>ai_tick_frequency</c> ist ein Script Value).</summary>
    /// <param name="frequency">Wert von <c>ai_tick_frequency</c> wie im Skript.</param>
    /// <param name="maxPeriodDays">Ab diesem Abstand warnt die Regel nicht mehr (<see cref="RuleThresholds.FrequentTickMaxPeriodDays"/>).</param>
    public static string HintFrequentTickUnknownDetail(string marker, string key, string frequency, double total, int maxPeriodDays) => F("performance.hint.frequentTick.detailUnknown", "{1} läuft mit {0} und ai_tick_frequency = {2}; der Abstand in Tagen ist unbekannt, weil der Wert keine feste Zahl ist. Gesamtzeit {3} {4}. Liegt der Abstand unter {5} Tagen: ai_tick_frequency erhöhen oder den Trigger enger fassen.", marker, key, frequency, total.ToString("N3", CultureInfo.CurrentCulture), TimeUnit, maxPeriodDays.ToString(CultureInfo.CurrentCulture));

    /// <param name="pulse">Name des häufigen Pulses (<see cref="ParadoxModTool.GameProfiles.PerformanceSettings.FrequentPulseNames"/>).</param>
    /// <param name="key">Schlüssel des obersten Blocks.</param>
    public static string HintFrequentTickPulseDetail(string pulse, string key, double total) => F("performance.hint.frequentTick.detailPulse", "{1} läuft häufig ({0}) und kostet insgesamt {2} {3}. Den Trigger enger fassen oder die Arbeit auf einen selteneren Puls legen.", pulse, key, total.ToString("N3", CultureInfo.CurrentCulture), TimeUnit);

    // KI-Analyse: Lesen der Antwort.
    /// <summary>Meldung zu einem Problem mit einem Dateiblock der KI-Antwort; nur die Arten, die im Performance-Auftrag vorkommen können.</summary>
    public static string AiBlockProblem(AiBlockProblemKind kind, string path) => kind switch
    {
        AiBlockProblemKind.BlockWithoutEnd => F("performance.ai.blockWithoutEnd", "Der Block für \"{0}\" endet nicht mit <<<END>>>.", path),
        AiBlockProblemKind.NotAllowed => F("performance.ai.fileNotAllowed", "Datei \"{0}\" ist nicht erlaubt (nur recipe.json).", path),
        AiBlockProblemKind.Duplicate => F("performance.ai.fileDuplicate", "Datei \"{0}\" kommt doppelt vor.", path),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // KI-Analyse: Auftrag.
    public static string AiHeadingRun => T("performance.ai.task.headingRun", "Messlauf");

    public static string AiHeadingOrigins => T("performance.ai.task.headingOrigins", "Zeiten je Herkunft");

    public static string AiHeadingHotspots => T("performance.ai.task.headingHotspots", "Hotspots (Mod und beide)");

    public static string AiHeadingHints => T("performance.ai.task.headingHints", "Hinweise");

    public static string AiHeadingFiles => T("performance.ai.task.headingFiles", "Betroffene Dateien");

    public static string AiHeadingDeliverables => T("performance.ai.task.headingDeliverables", "Was die KI liefern soll");

    public static string AiRunName(string name) => F("performance.ai.task.runName", "Lauf: {0}", name);

    public static string AiRunGame(string gameId) => F("performance.ai.task.runGame", "Spiel: {0}", gameId);

    public static string AiRunMod(string mod) => F("performance.ai.task.runMod", "Mod: {0}", mod);

    public static string AiRunDate(string date) => F("performance.ai.task.runDate", "Gemessen am: {0}", date);

    public static string AiRunMonths(int? planned, int? counted) => F("performance.ai.task.runMonths", "Monate: geplant {0}, gezählt {1}", Months(planned), Months(counted));

    public static string AiRunSave(string? save) => F("performance.ai.task.runSave", "Spielstand: {0}", string.IsNullOrEmpty(save) ? AiUnknown : save);

    public static string AiRunPlayset(string? playset) => F("performance.ai.task.runPlayset", "Playset: {0}", string.IsNullOrEmpty(playset) ? AiUnknown : playset);

    public static string AiUnknown => T("performance.ai.task.unknown", "unbekannt");

    public static string AiHashChanged(string atRun, string now) => F("performance.ai.task.hashChanged", "Dateistand: seit der Messung geändert (Messung {0}, jetzt {1})", atRun, now);

    public static string AiHashNow => T("performance.ai.task.hashNow", "Dateistand: jetzt. Die Codeblöcke unten zeigen den aktuellen Stand der Dateien, nicht den gemessenen.");

    public static string AiHashSame => T("performance.ai.task.hashSame", "Dateistand: wie gemessen");

    public static string AiHashUnknown => T("performance.ai.task.hashUnknown", "Dateistand: unbekannt");

    public static string AiTimeUnitNote => F("performance.ai.task.timeUnitNote", "Alle Zeiten sind Rohwerte des Profiler-Dumps in {0}, ohne Umrechnung.", TimeUnit);

    public static string AiOriginLine(RowOrigin origin, string self) => F("performance.ai.task.originLine", "{0}: {1} {2}", Origin(origin), self, TimeUnit);

    public static string AiNoModRows => T("performance.ai.task.noModRows", "Die Messung enthält keine Zeilen aus der Mod. Es gibt nichts zu optimieren, was sich in der Mod ändern ließe.");

    public static string AiHotspotHeader => T("performance.ai.task.hotspotHeader", "#  Stelle  Art  Calls  Self  Total");

    public static string AiHotspotRow(int rank, string place, string kind, string calls, string self, string total) => F("performance.ai.task.hotspotRow", "{0}  {1}  {2}  {3}  {4}  {5}", rank.ToString(CultureInfo.CurrentCulture), place, kind, calls, self, total);

    public static string AiSnippetCut(int shown, int total) => F("performance.ai.task.snippetCut", "Block auf {0} von {1} Zeilen gekürzt", shown.ToString(CultureInfo.CurrentCulture), total.ToString(CultureInfo.CurrentCulture));

    public static string AiSnippetNoteHotspot(int rank, double total) => F("performance.ai.task.snippetNoteHotspot", "Hotspot Nr. {0}, Total {1} {2}", rank.ToString(CultureInfo.CurrentCulture), total.ToString("N3", CultureInfo.CurrentCulture), TimeUnit);

    public static string AiSnippetNoteHint(string title) => F("performance.ai.task.snippetNoteHint", "Hinweis: {0}", title);

    public static string AiDeliverableFindings => T("performance.ai.task.deliverableFindings", "Eine Einschätzung je Hotspot: warum die Stelle teuer ist und ob sich eine Änderung lohnt.");

    public static string AiDeliverableProposals => T("performance.ai.task.deliverableProposals", "Konkrete Skriptvorschläge für die lohnenden Stellen, als Code mit Pfad und Block.");

    public static string AiDeliverableTechniques => T("performance.ai.task.deliverableTechniques", "Bevorzugte Mittel: Guards vor teuren Prüfungen, Zwischenspeichern in Variablen, engere Trigger, seltenere Pulse.");

    public static string AiDeliverableRemeasure => T("performance.ai.task.deliverableRemeasure", "Was nach der Änderung neu gemessen werden soll und woran man den Erfolg erkennt.");

    public static string AiRecipeIntro => T("performance.ai.task.recipeIntro", "Ist eine Änderung mechanisch und wiederholbar, darfst du zusätzlich ein Patch-Rezept als Dateiblock liefern: eine Zeile <<<FILE recipe.json>>>, dann das JSON, dann eine Zeile <<<END>>>. Ein Rezept ist freiwillig.");

    public static string AiFeedbackIntro => T("performance.ai.task.feedbackIntro", "PMT hat diese Probleme mit deiner Antwort gefunden. Bitte korrigiere sie und antworte erneut mit der vollständigen Antwort:");

    // KI-Analyse: Fenster und Reiter Auswertung.
    public static string AiAnalyze => T("performance.ai.analyze", "Mit KI analysieren");

    public static string AiAnalyzeTip => T("performance.ai.analyze.tip", "Macht aus dem gewählten Lauf einen Auftrag an eine KI: Messlauf, Hotspots, Hinweise und die Codeblöcke um die Hotspots. PMT ruft keine KI selbst auf.");

    public static string AiShowAdvice => T("performance.ai.showAdvice", "KI-Rat anzeigen");

    public static string AiShowAdviceTip => T("performance.ai.showAdvice.tip", "Zeigt den KI-Rat, der zu diesem Lauf gespeichert ist.");

    public static string AiTitle => T("performance.ai.title", "Performance mit KI analysieren");

    public static string AiTaskIntro => T("performance.ai.taskIntro", "1. Prüfen, ob oben die Mod des Laufs gewählt ist. 2. Optional ganze Dateien anhaken oder unter „Codeblöcke“ weitere Stellen einfügen; Messlauf, Hotspots, Hinweise und die Codeblöcke um die Hotspots legt PMT selbst bei. 3. Beschreiben, was du schon weißt oder vermutest. 4. Auftrag kopieren und in eine KI einfügen, oder als Ordner für eine KI mit Dateizugriff speichern. 5. Die Antwort unter „Ergebnis“ einfügen.");

    public static string AiResultIntro => T("performance.ai.resultIntro", "Die komplette Antwort der KI einfügen und „Übernehmen“ drücken, oder den Ordner wählen, in den eine KI answer.md geschrieben hat. PMT speichert den Rat beim Lauf; im Reiter Auswertung zeigt ihn „KI-Rat anzeigen“ wieder. Enthält die Antwort ein Rezept, fragt PMT, ob es in die Patch-Rezepte übernommen werden soll. In die Mod wird nichts geschrieben.");

    public static string AiDescriptionTip => T("performance.ai.description.tip", "Was du schon weißt oder vermutest; was die KI nicht ändern soll");

    public static string AiWithRecipe => T("performance.ai.withRecipe", "Rezeptformat beilegen (Vorschläge auch als Patch-Rezept)");

    public static string AiWithRecipeTip => T("performance.ai.withRecipe.tip", "Hängt das Format der Patch-Rezepte an den Auftrag. Die KI darf dann eine mechanische, wiederholbare Änderung zusätzlich als Rezept liefern; PMT fragt nach dem Übernehmen, ob es in die Patch-Rezepte importiert werden soll.");

    public static string AiModTip => T("performance.ai.mod.tip", "Mod, deren Dateien du anhaken kannst; ein Rezept aus der Antwort gehört zu ihr. Vorgewählt ist die Mod des Laufs.");

    public static string AiChooseModMessage => T("performance.ai.chooseMod.message", "Mod wählen, deren Dateien du anhaken willst. Die Mod des Laufs steht oben.");

    public static string AiFilesTip => T("performance.ai.files.tip", "Ganze Dateien anhaken, wenn die KI mehr sehen soll als die Codeblöcke um die Hotspots, z. B. eine Datei mit Skriptwerten, die ein Hotspot aufruft. Bilder werden nur genannt, nicht mitgeschickt.");

    public static string AiCopyTextTip => T("performance.ai.copyText.tip", "Anleitung, Beispiel, Auftrag mit Messlauf, Hotspots und Hinweisen, die angehakten Dateien und alle Codeblöcke als ein Text in die Zwischenablage, zum Einfügen in eine Chat-KI.");

    public static string AiSaveFolderTip => T("performance.ai.saveFolder.tip", "Für KIs mit Dateizugriff: Anleitung, task.md, snippets.md mit den Codeblöcken, die angehakten Dateien in mod/ und game/ und auf Wunsch recipe-format.md. Die KI schreibt ihre Antwort nach answer.md.");

    public static string AiAnswerTip => T("performance.ai.answer.tip", "Die ganze Antwort einfügen: Text mit den Überschriften Befund, Vorschläge und Nachmessen, optional ein Block zwischen <<<FILE recipe.json>>> und <<<END>>>.");

    public static string AiApplyTip => T("performance.ai.apply.tip", "Speichert den Rat beim Lauf und zeigt ihn unten an. Ein Rezept aus der Antwort übernimmt PMT nur nach Rückfrage in die Patch-Rezepte. In die Mod wird nichts geschrieben.");

    public static string AiImportFolderTip => T("performance.ai.importFolder.tip", "Ordner mit answer.md wählen, z. B. den Auftragsordner, in den eine KI mit Dateizugriff ihre Antwort geschrieben hat.");

    public static string AiSnippetsIntro => T("performance.ai.snippetsIntro", "Je Block eine weitere Stelle aus der Mod oder dem Spiel einfügen und angeben, aus welcher Datei sie stammt. Die Codeblöcke um die Hotspots legt PMT selbst bei.");

    public static string AiSnippetWholeFileTip => T("performance.ai.snippetWholeFile.tip", "Der Block ist die ganze Datei.");

    public static string AiSnippetExcerptTip => T("performance.ai.snippetExcerpt.tip", "Der Block ist nur ein Teil der Datei; der Rest bleibt, die KI darf ihn nicht ersetzen.");

    public static string AiSnippetNoteTip => T("performance.ai.snippetNote.tip", "Optional: was die KI über diesen Block wissen muss, z. B. „wird von Hotspot Nr. 2 aufgerufen“.");

    public static string AiNoRun => T("performance.ai.noRun", "Kein ausgewerteter Lauf gewählt. Im Reiter Messung einen Lauf auswerten und dann „Mit KI analysieren“ drücken.");

    public static string AiEmptyAnswer => T("performance.ai.emptyAnswer", "Die Antwort enthält weder Text noch ein Rezept.");

    public static string AiAdviceSaved(string run) => F("performance.ai.adviceSaved", "KI-Rat beim Lauf „{0}“ gespeichert.", run);

    public static string AiAdviceSaveFailed(string message) => F("performance.ai.adviceSaveFailed", "KI-Rat nicht gespeichert: {0}", message);

    public static string AiAdviceMore(int lines) => F("performance.ai.adviceMore", "Weitere {0} Zeilen stehen im gespeicherten KI-Rat.", lines.ToString(CultureInfo.CurrentCulture));

    public static string AiNoAdvice => T("performance.ai.noAdvice", "Die Antwort enthält nur ein Rezept und keinen Text; beim Lauf wurde nichts gespeichert.");

    public static string AiRecipeOffer => T("performance.ai.recipeOffer", "Die Antwort enthält ein Patch-Rezept. Als Rezept importieren?\n\nPMT öffnet dann die Patch-Rezepte für die Mod und zeigt Erklärung und Vorschau. In die Mod geschrieben wird erst mit „Anwenden“.");

    public static string AiRecipeImported => T("performance.ai.recipeImported", "Import in die Patch-Rezepte:");

    public static string AiRecipeNotImported => T("performance.ai.recipeNotImported", "Das Rezept aus der Antwort wurde nicht importiert. Ein neues „Übernehmen“ fragt wieder.");

    public static string AiNoAnswerFile(string folder) => F("performance.ai.noAnswerFile", "Keine Antwort im Ordner {0}: answer.md fehlt.", folder);

    public static string AiAdviceTitle(string run) => F("performance.ai.adviceTitle", "KI-Rat: {0}", run);

    public static string AiAdviceMissing => T("performance.ai.adviceMissing", "Zu diesem Lauf ist kein KI-Rat gespeichert.");

    private static string Months(int? months) => months?.ToString(CultureInfo.CurrentCulture) ?? "?";

    private static string Name(string? name) => string.IsNullOrEmpty(name) ? "?" : name;

    private static string Percent(double ratio) => ratio.ToString("P0", CultureInfo.CurrentCulture);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
