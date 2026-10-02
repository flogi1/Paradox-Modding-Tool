using ParadoxModTool.Localization;

namespace ParadoxModTool.Desync;

/// <summary>
/// Alle Oberflächen- und Meldungstexte der Desync-Analyse. Die Schlüssel stehen in allen Dateien unter <c>locales/</c>;
/// der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>DesyncTextsTests</c>). Jeder Aufruf von
/// <c>T</c>/<c>F</c> steht in einer Zeile. Beschriftungen enden nie mit Auslassungspunkten. "OOS", "Desync", "random_count", DLC und Mod
/// bleiben in allen Sprachen als Fachbegriffe stehen.
/// </summary>
public static class DesyncTexts
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    // Menü und Fenster.
    public static string Menu => T("desync.menu", "Desync-Analyse");

    public static string MenuTip => T("desync.menu.tip", "OOS-Dumps aus Mehrspieler-Runden lesen: Übersicht, Logs vor dem OOS und Zustands-Vergleich beider Rechner");

    public static string WindowTitle(string game) => F("desync.window.title", "Desync-Analyse - {0}", game);

    /// <summary>Die Regeldatei des Spiels (Profil <c>desyncRules</c>) ließ sich nicht laden; <paramref name="message"/> ist die Meldung des Systems.</summary>
    public static string ProfileLoadFailed(string path, string message) => F("desync.profileLoadFailed", "Die Desync-Regeln {0} konnten nicht geladen werden: {1}", path, message);

    // Liste links.
    public static string AddDump => T("desync.list.addDump", "Dump hinzufügen");

    public static string AddFolder => T("desync.list.addFolder", "Ordner hinzufügen");

    public static string Refresh => T("desync.list.refresh", "Aktualisieren");

    public static string RemoveAdded => T("desync.list.remove", "Entfernen");

    /// <summary>Eintrag einer Sitzung: Beginn und Zahl der OOS darin.</summary>
    public static string SessionLabel(DateTime start, int count) => F("desync.list.session", "Sitzung vom {0:g} ({1} OOS)", start, count);

    /// <summary>Eintrag eines Dump-Paars: OOS-Kennung und Zeit.</summary>
    public static string PairLabel(string id, DateTime date) => F("desync.list.pair", "OOS {0} ({1:g})", id, date);

    public static string MissingMachine(string machine, string player, string folder) =>
        F("desync.list.missingMachine", "Dump von Maschine {0} ({1}) fehlt: Mitspieler soll Ordner {2} schicken", machine, player, folder);

    public static string DumpError(string path, string message) => F("desync.list.dumpError", "Dump {0} nicht lesbar: {1}", path, message);

    public static string NoOosFolder(string path) => F("desync.list.noOosFolder", "Kein Ordner „oos“ gefunden: {0}", path);

    /// <summary>Filter des Dateidialogs "Dump hinzufügen" (Beschreibung|Muster).</summary>
    public static string ZipFilter => T("desync.list.zipFilter", "Zip-Dateien (*.zip)|*.zip");

    public static string SourcesSaveFailed(string message) => F("desync.list.sourcesSaveFailed", "Die hinzugefügten Pfade konnten nicht gespeichert werden: {0}", message);

    // Reiter.
    public static string TabOverview => T("desync.tab.overview", "Übersicht");

    public static string TabLogs => T("desync.tab.logs", "Logs vor dem OOS");

    public static string TabState => T("desync.tab.state", "Zustandsvergleich");

    public static string TabReport => T("desync.report.tab", "Bericht");

    // Übersicht.
    public static string OverviewOosId => T("desync.overview.oosId", "OOS-ID:");

    public static string OverviewGameDate => T("desync.overview.gameDate", "Spieldatum:");

    public static string OverviewFrames(long? start, long? oos) => F("desync.overview.frames", "Frames: Start {0}, OOS {1}", Unknown(start), Unknown(oos));

    public static string OverviewChecksums => T("desync.overview.checksums", "Abweichende Prüfsummen:");

    public static string ChecksumUnknown(string category) => F("desync.overview.checksumUnknown", "{0} (keine Erklärung bekannt)", category);

    public static string OverviewMachine(string id, string role, string player) => F("desync.overview.machine", "Maschine {0} ({1}): {2}", id, role, player);

    public static string OverviewVersion => T("desync.overview.version", "Version:");

    public static string OverviewDlcs => T("desync.overview.dlcs", "DLCs:");

    public static string OverviewMods => T("desync.overview.mods", "Mods:");

    /// <summary>Ein Mod der Liste "Mods:" mit seiner Herkunft; ohne bekannte Herkunft nur der Name.</summary>
    public static string ModLabel(DumpMod mod) => mod.Kind switch
    {
        ModSourceKind.Workshop => F("desync.overview.modWorkshop", "{0} (Workshop {1})", mod.Name, mod.WorkshopId),
        ModSourceKind.Local => F("desync.overview.modLocal", "{0} (lokaler Ordner)", mod.Name),
        _ => mod.Name,
    };

    public static string OverviewRandomSeed(string value) => F("desync.overview.randomSeed", "random_seed: {0}", value);

    public static string OverviewRandomCount(string value) => F("desync.overview.randomCount", "random_count: {0}", value);

    public static string RandomCountNote => T("desync.overview.randomCountNote", "Gleiche Werte heißen nicht, dass der Zufall gleich lief.");

    public static string OverviewRandomLog(bool present) => present
        ? T("desync.overview.randomLogPresent", "Zufallslog vorhanden")
        : T("desync.overview.randomLogAbsent", "Kein Zufallslog vorhanden");

    public static string OverviewCallstacks(bool present) => present
        ? T("desync.overview.callstacksPresent", "Callstacks vorhanden")
        : T("desync.overview.callstacksAbsent", "Keine Callstacks vorhanden");

    // Warnungen ganz oben in der Übersicht.
    public static string WarnGameCommit => T("desync.warn.gameCommit", "Warnung: Die Maschinen haben unterschiedliche Spielversionen (game_commit). Das ist eine häufige echte Ursache.");

    public static string WarnEngineCommit => T("desync.warn.engineCommit", "Warnung: Die Maschinen haben unterschiedliche Engine-Versionen (engine_commit). Das ist eine häufige echte Ursache.");

    public static string WarnDlcs => T("desync.warn.dlcs", "Warnung: Die Maschinen haben unterschiedliche DLCs. Das ist eine häufige echte Ursache.");

    public static string WarnMods => T("desync.warn.mods", "Warnung: Die Maschinen haben unterschiedliche Mod-Listen. Das ist eine häufige echte Ursache.");

    public static string WarnMissing => T("desync.warn.missing", "Der Dump der anderen Maschine fehlt: Version, DLCs und Mods lassen sich nicht vergleichen.");

    // Logs vor dem OOS.
    public static string LogsWindow => T("desync.logs.window", "Spieltage vor dem OOS:");

    public static string LogsColumnDate => T("desync.logs.column.date", "Datum");

    public static string LogsColumnMachine => T("desync.logs.column.machine", "Maschine");

    public static string LogsColumnSource => T("desync.logs.column.source", "Quelle");

    public static string LogsColumnMessage => T("desync.logs.column.message", "Meldung");

    public static string LogsColumnMod => T("desync.logs.column.mod", "Mod");

    /// <summary>Name des Logs in der Spalte "Quelle"; andere Dateiarten erscheinen mit ihrem Namen.</summary>
    public static string LogSource(DumpFileKind kind) => kind switch
    {
        DumpFileKind.Error => T("desync.logs.source.error", "Fehlerlog"),
        DumpFileKind.Game => T("desync.logs.source.game", "Spiellog"),
        DumpFileKind.Multiplayer => T("desync.logs.source.multiplayer", "Mehrspielerlog"),
        _ => kind.ToString(),
    };

    public static string LeadInvalidCommand => T("desync.logs.leadInvalidCommand", "Spur: Invalid command");

    public static string LeadScriptError => T("desync.logs.leadScriptError", "Spur: Skriptfehler");

    public static string OnlyOnThisMachine => T("desync.logs.onlyHere", "nur auf dieser Maschine");

    public static string NoTicks => T("desync.logs.noTicks", "Keine Tick-Zeilen in den Logs: Die Meldungen lassen sich keinen Spieltagen zuordnen.");

    // Zustandsvergleich.
    public static string StateStart => T("desync.state.start", "Vergleichen");

    public static string StateCancel => T("desync.state.cancel", "Abbrechen");

    public static string StateProgress(string phase, double percent) => F("desync.state.progress", "{0}: {1:0} %", phase, percent);

    public static string StatePhase(DesyncPhase phase) => phase switch
    {
        DesyncPhase.IndexReference => T("desync.state.phase.indexReference", "Referenz-Spielstand wird gelesen"),
        DesyncPhase.IndexDesynced => T("desync.state.phase.indexDesynced", "Desynchroner Spielstand wird gelesen"),
        DesyncPhase.Collect => T("desync.state.phase.collect", "Abweichende Objekte werden geholt"),
        DesyncPhase.Diff => T("desync.state.phase.diff", "Felder werden verglichen"),
        _ => phase.ToString(),
    };

    public static string StateIncomplete(string side) => F("desync.state.incomplete", "Spielstand {0} unvollständig", side);

    /// <summary>Wie <see cref="StateIncomplete"/>, mit der Stelle, an der der Spielstand abbricht (Byte-Versatz, gezeigt in MB).</summary>
    public static string StateIncompleteAt(string side, long offsetBytes) =>
        F("desync.state.incompleteAt", "Spielstand {0} unvollständig, endet nach {1:0.0} MB", side, offsetBytes / BytesPerMegabyte);

    public static string StateBinary => T("desync.state.binary", "Binärer Spielstand (Ironman): kein Zustandsvergleich möglich, Übersicht und Logs funktionieren weiterhin.");

    public static string StateNeedsBoth => T("desync.state.needsBoth", "Für den Zustandsvergleich werden die Dumps beider Maschinen gebraucht.");

    public static string WeightCause => T("desync.state.weight.cause", "Ursache-Kandidaten");

    public static string WeightConsequence => T("desync.state.weight.consequence", "Wahrscheinliche Folgeschäden");

    public static string WeightNoise => T("desync.state.weight.noise", "Rauschen");

    public static string ShowNoise => T("desync.state.showNoise", "Rauschen anzeigen");

    /// <summary>Gruppe der eigenen Skalare der obersten Ebene des Spielstands (Pfad "") im Baum.</summary>
    public static string TopLevel => T("desync.state.topLevel", "Spielstand (oberste Ebene)");

    /// <summary>Zahl der abweichenden Einträge (Objekte und Blöcke) einer Gruppe im Baum.</summary>
    public static string EntryCount(int count) => F("desync.state.entryCount", "Einträge: {0}", count);

    /// <summary>Zahl der abweichenden Felder eines Eintrags im Baum.</summary>
    public static string FieldCount(int count) => F("desync.state.fieldCount", "Felder: {0}", count);

    public static string SlotReused(string refLabel, int? refGen, string desLabel, int? desGen) =>
        F("desync.state.slotReused", "Slot wiederverwendet: Referenz {0} (Gen. {1}), desynchron {2} (Gen. {3})", refLabel, Unknown(refGen), desLabel, Unknown(desGen));

    public static string OnlyReference => T("desync.state.onlyReference", "Nur in der Referenz");

    public static string OnlyDesynced => T("desync.state.onlyDesynced", "Nur im desynchronen Spielstand");

    /// <summary>Objekt zu groß für den Feldvergleich; die Größe kommt in Bytes und wird in MB gezeigt.</summary>
    public static string TooLarge(long sizeBytes) => F("desync.state.tooLarge", "Zu groß für den Feldvergleich ({0:0.0} MB)", sizeBytes / BytesPerMegabyte);

    public static string FieldsTruncated => T("desync.state.fieldsTruncated", "Feldliste gekürzt, weitere Unterschiede werden nicht angezeigt");

    public static string LineFallback => T("desync.state.lineFallback", "Zeilenvergleich statt Feldvergleich");

    public static string Recurring(string manager, int count, int total) => F("desync.state.recurring", "{0} weicht in {1} von {2} OOS ab", manager, count, total);

    public static string ColumnReference => T("desync.state.column.reference", "Referenz");

    public static string ColumnDesynced => T("desync.state.column.desynced", "Desynchron");

    public static string Cached(DateTime time) => F("desync.state.cached", "Aus dem Zwischenspeicher, berechnet am {0:g}", time);

    /// <summary>Hinweis, wenn das Ergebnis nicht in den Zwischenspeicher geschrieben werden konnte; das Ergebnis wird trotzdem gezeigt.</summary>
    public static string CacheSaveFailed(string message) => F("desync.state.cacheSaveFailed", "Ergebnis konnte nicht zwischengespeichert werden: {0}", message);

    // Rollen der Maschinen.
    public static string RoleHost => T("desync.role.host", "Host");

    public static string RoleClient => T("desync.role.client", "Client");

    // Bericht.
    public static string ReportRecipient => T("desync.report.recipient", "Empfänger:");

    public static string ReportParadox => T("desync.report.paradox", "Paradox-Forum");

    public static string ReportModAuthor => T("desync.report.modAuthor", "Mod-Autor");

    public static string ReportBugTracker => T("desync.report.bugTracker", "PMT-Bugtracker");

    public static string ReportDiffIncluded => T("desync.report.diffIncluded", "Mit Ergebnis des Zustandsvergleichs.");

    public static string ReportDiffMissing => T("desync.report.diffMissing", "Ohne Zustandsvergleich. Starte ihn für Ursache-Kandidaten im Bericht.");

    public static string ReportCopy => T("desync.report.copy", "Kopieren");

    public static string ReportCopied => T("desync.report.copied", "Kopiert");

    public static string ReportSaveFile => T("desync.report.saveFile", "Als Datei speichern");

    /// <summary>Filter des Speichern-Dialogs: nur die Beschreibung wird übersetzt, <c>|*.md</c> bleibt.</summary>
    public static string ReportTextFilter => T("desync.report.textFilter", "Textdatei (*.txt)|*.txt");

    public static string ReportShowInExplorer => T("desync.report.showInExplorer", "Im Explorer zeigen");

    public static string ReportSaveZip => T("desync.report.saveZip", "Als Zip speichern");

    /// <summary>Filter des Speichern-Dialogs: nur die Beschreibung wird übersetzt, <c>|*.zip</c> bleibt.</summary>
    public static string ReportZipFilter => T("desync.report.zipFilter", "Zip-Archiv (*.zip)|*.zip");

    /// <summary>Eintrag der Anhang-Liste: der Dump der genannten Maschine liegt nicht vor und kann nicht angehängt werden.</summary>
    public static string ReportDumpMissing(int machine) => F("desync.report.dumpMissing", "Dump von Maschine {0} fehlt", machine);

    /// <summary>Größe eines Dumps im Zip in Megabyte.</summary>
    public static string ReportDumpSize(double megabytes) => F("desync.report.dumpSize", "{0:0.0} MB", megabytes);

    /// <summary>Maschine eines Dumps in der Liste der Anhänge.</summary>
    public static string ReportMachine(int machine) => F("desync.report.machine", "Maschine {0}", machine);

    public static string ReportNoForm => T("desync.report.noForm", "Für dieses Spiel ist kein Paradox-Formular hinterlegt.");

    public static string ReportFormHint => T("desync.report.formHint", "PMT füllt nichts im Browser aus. Text kopieren und ins Formular einfügen.");

    public static string ReportBugTitle => T("desync.report.bug.title", "Titel:");

    public static string ReportBugMod => T("desync.report.bug.mod", "Mod:");

    public static string ReportBugLinks => T("desync.report.bug.links", "Verknüpfte Stellen:");

    public static string ReportBugNoLinks => T("desync.report.bug.noLinks", "Keine Spur in eigenen Mod-Dateien. Der Bug wird ohne Verknüpfung angelegt.");

    public static string ReportBugCreate => T("desync.report.bug.create", "Bug anlegen");

    public static string ReportBugCreated => T("desync.report.bug.created", "Bug angelegt.");

    public static string ReportBugOpen => T("desync.report.bug.open", "Im Bugtracker öffnen");

    public static string ReportBugNoOwnMod => T("desync.report.bug.noOwnMod", "Keine eigene Mod vorhanden. Bugs lassen sich nur für eigene Mods anlegen.");

    /// <summary>Beim Speichern des Berichts (Datei oder Zip) ist ein Fehler aufgetreten; <paramref name="message"/> ist die Meldung des Systems.</summary>
    public static string ReportSaveFailed(string message) => F("desync.report.saveFailed", "Speichern fehlgeschlagen: {0}", message);

    // MP-Prüfung.
    public static string TabSafety => T("desync.tab.safety", "MP-Prüfung");

    public static string SafetyRun => T("desync.safety.run", "Prüfen");

    public static string SafetyCancel => T("desync.safety.cancel", "Abbrechen");

    public static string SafetyWholePlayset => T("desync.safety.wholePlayset", "ganzes Playset");

    /// <summary>Kästchen bei gewähltem Paar: statt der Mods aus dem Dump die eigenen Mods (oder das ganze Playset) prüfen.</summary>
    public static string SafetyPrecheck => T("desync.safety.precheck", "Vorabprüfung statt Dump-Mods");

    public static string SafetyNoRules => T("desync.safety.noRules", "Keine MP-Regeln für dieses Spiel");

    /// <summary>Vorabprüfung ohne OOS: es werden die <paramref name="mods"/> der Mod-Liste geprüft.</summary>
    public static string SafetyModeA(int mods) => F("desync.safety.modeA", "Vorabprüfung: {0} Mods", mods);

    /// <summary>Prüfung zu einem OOS: es werden die <paramref name="mods"/> aus dem Dump des OOS <paramref name="id"/> geprüft.</summary>
    public static string SafetyModeB(string id, int mods) => F("desync.safety.modeB", "OOS {0}: {1} Mods aus dem Dump", id, mods);

    public static string SafetyNotStarted => T("desync.safety.notStarted", "Noch nicht geprüft. „Prüfen“ startet die Prüfung.");

    public static string SafetyDone(int files, int findings) => F("desync.safety.done", "{0} Dateien geprüft, {1} Treffer", files, findings);

    public static string SafetyCancelled => T("desync.safety.cancelled", "Prüfung abgebrochen");

    public static string SafetyNoMods => T("desync.safety.noMods", "Die Mod-Liste ist noch nicht geladen oder leer");

    public static string SafetySuspectsHeader => T("desync.safety.suspects", "Verdächtige Dateien zu diesem OOS");

    public static string SafetyNeedsDiff => T("desync.safety.needsDiff", "Zustandsvergleich starten, um auch nach Begriffen aus den Ursachen zu suchen");

    public static string SafetyFindingsHeader => T("desync.safety.findings", "Regeltreffer");

    public static string SafetyColumnSeverity => T("desync.safety.col.severity", "Stufe");

    public static string SafetyColumnMod => T("desync.safety.col.mod", "Mod");

    public static string SafetyColumnFile => T("desync.safety.col.file", "Datei");

    public static string SafetyColumnLine => T("desync.safety.col.line", "Zeile");

    public static string SafetyColumnRule => T("desync.safety.col.rule", "Regel");

    public static string SafetyColumnToken => T("desync.safety.col.token", "Fundstück");

    public static string SafetyColumnContext => T("desync.safety.col.context", "Kontext");

    public static string SafetyColumnExplain => T("desync.safety.col.explain", "Begründung");

    public static string SafetyColumnScore => T("desync.safety.col.score", "Punkte");

    public static string SafetyColumnReasons => T("desync.safety.col.reasons", "Gründe");

    public static string SafetySeverityName(SafetySeverity severity) => severity switch
    {
        SafetySeverity.Allowed => T("desync.safety.sev.allowed", "erlaubt"),
        SafetySeverity.Check => T("desync.safety.sev.check", "prüfen"),
        _ => T("desync.safety.sev.risk", "riskant"),
    };

    /// <summary>Stufe in der Spalte "Stufe" eines Treffers, zu dem es ein Urteil des Nutzers gibt; <paramref name="severity"/> ist der schon übersetzte Name der Stufe.</summary>
    public static string SafetyByVerdict(string severity) => F("desync.safety.byVerdict", "{0} (Urteil)", severity);

    public static string SafetyContextName(SafetyContext context) => context switch
    {
        SafetyContext.Synced => T("desync.safety.ctx.synced", "synchron"),
        SafetyContext.Local => T("desync.safety.ctx.local", "lokal"),
        _ => T("desync.safety.ctx.unknown", "unbekannt"),
    };

    /// <summary>Die Datei ließ sich nicht parsen; der Fehler liegt in der genannten Zeile.</summary>
    public static string SafetyUnparsed(int line) => F("desync.safety.unparsed", "Datei nicht parsebar, Zeile {0}", line);

    public static string SafetyMarkHarmless => T("desync.safety.markHarmless", "Als unbedenklich markieren");

    public static string SafetyMarkRisky => T("desync.safety.markRisky", "Als riskant bestätigen");

    public static string SafetyResetVerdict => T("desync.safety.resetVerdict", "Urteil zurücksetzen");

    /// <summary>Die gespeicherten Urteile waren beschädigt; <paramref name="path"/> ist die Sicherungskopie.</summary>
    public static string SafetyVerdictsBroken(string path) => F("desync.safety.verdictsBroken", "Die Urteile waren beschädigt und wurden als {0} gesichert", path);

    public static string SafetyVerdictSaveFailed(string message) => F("desync.safety.verdictSaveFailed", "Das Urteil konnte nicht gespeichert werden: {0}", message);

    public static string SafetyNoticeMissing(string mod, string path) => F("desync.safety.notice.missing", "Mod {0}: Ordner {1} fehlt", mod, path);

    public static string SafetyNoticeUnreadable(string mod, string path, string message) => F("desync.safety.notice.unreadable", "Mod {0}: {1} nicht lesbar: {2}", mod, path, message);

    public static string SafetyNoticeNotInstalled(string mod) => F("desync.safety.notice.notInstalled", "Mod {0} ist lokal nicht vorhanden", mod);

    /// <summary>Begründung eines verdächtigen Treffers: Fundstelle im Log und Maschine; <paramref name="machines"/> ist die fertige Aufzählung, eine fehlende Zeile steht als Fragezeichen.</summary>
    public static string SafetyReasonLead(string file, int? line, string machines) => F("desync.safety.reason.lead", "Log: {0}:{1}, Maschine {2}", file, Unknown(line), machines);

    public static string SafetyReasonRule(string rule, string token, int line) => F("desync.safety.reason.rule", "Regel {0} ({1}), Zeile {2}", rule, token, line);

    public static string SafetyReasonTerm(string term, int files) => F("desync.safety.reason.term", "Begriff {0} ({1} Dateien)", term, files);

    /// <summary>Zahl als Text; fehlt sie, steht ein Fragezeichen.</summary>
    private static string Unknown(long? value) => value?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? "?";

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
