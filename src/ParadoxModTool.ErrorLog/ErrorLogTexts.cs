using ParadoxModTool.Localization;

namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Alle Oberflächen- und Meldungstexte des Fehlerprotokolls. Die Schlüssel stehen in <c>locales/de.json</c> und <c>locales/en.json</c>;
/// der deutsche Text hier ist der Rückfall und muss mit de.json übereinstimmen (Test <c>ErrorLogTextsTests</c>). Jeder Aufruf von
/// <c>T</c>/<c>F</c> steht in einer Zeile. Beschriftungen enden nie mit Auslassungspunkten. Katalogtexte (Namen, Erklärungen, Tipps der
/// Fehlerarten) stehen zweisprachig in der Katalogdatei, nicht hier.
/// </summary>
public static class ErrorLogTexts
{
    // Menü und Fenster.
    public static string Menu => T("errorlog.menu", "Fehlerprotokoll");

    public static string MenuTip => T("errorlog.menu.tip", "error.log des Spiels lesen: Fehler nach Mod gruppiert, mit Erklärung, Sprung zur Stelle und Vergleich mit dem vorigen Lauf");

    public static string WindowTitle(string game) => F("errorlog.window.title", "Fehlerprotokoll - {0}", game);

    // Kopfzeile.
    public static string RunLabel => T("errorlog.run", "Lauf:");

    public static string RunEntry(string fileName, DateTime localTime) => F("errorlog.run.entry", "{0} ({1:g})", fileName, localTime);

    public static string RunEntryParts(string oldestFile, string newestFile, DateTime localTime) =>
        F("errorlog.run.entryParts", "{0} bis {1} ({2:g})", oldestFile, newestFile, localTime);

    public static string RunMaybeIncomplete(string text) => F("errorlog.run.maybeIncomplete", "{0}, möglicherweise unvollständig", text);

    /// <summary>Eintrag der Laufauswahl: Dateibereich des Laufs und Zeit der letzten Änderung, ggf. mit Hinweis auf fehlende ältere Teile.</summary>
    public static string RunText(LogRun run)
    {
        var time = run.Newest.LastWriteUtc.ToLocalTime();
        var text = run.Files.Count == 1
            ? RunEntry(run.Newest.FileName, time)
            : RunEntryParts(run.Files[0].FileName, run.Newest.FileName, time);
        return run.MaybeIncomplete ? RunMaybeIncomplete(text) : text;
    }

    public static string ComparisonLabel => T("errorlog.comparison", "Vergleich:");

    public static string NoComparison => T("errorlog.comparison.none", "Kein Vergleich");

    public static string PlaysetLabel => T("errorlog.playset", "Playset:");

    public static string PlaysetUnknown => T("errorlog.playset.unknown", "Playset unbekannt");

    public static string Reload => T("errorlog.reload", "Neu laden");

    public static string ReloadTip => T("errorlog.reload.tip", "Logdateien neu einlesen und den Symbolindex neu aufbauen");

    public static string SearchLabel => T("errorlog.search", "Suche:");

    public static string Counts(ErrorCounts counts) => F("errorlog.counts", "{0} Meldungen in {1} Gruppen, {2} neu, {3} behoben", counts.Occurrences, counts.Groups, counts.NewGroups, counts.FixedGroups);

    // Filter.
    public static string FilterErrors => T("errorlog.filter.errors", "Fehler");

    public static string FilterWarnings => T("errorlog.filter.warnings", "Warnungen");

    public static string FilterInfos => T("errorlog.filter.infos", "Hinweise");

    public static string FilterNew => T("errorlog.filter.new", "Neu");

    public static string FilterUnchanged => T("errorlog.filter.unchanged", "Unverändert");

    public static string FilterFixed => T("errorlog.filter.fixed", "Behoben");

    public static string FilterKindLabel => T("errorlog.filter.kind", "Fehlerart:");

    public static string AllKinds => T("errorlog.filter.allKinds", "Alle Fehlerarten");

    public static string OwnModsOnly => T("errorlog.filter.ownOnly", "Nur eigene Mods");

    // Export.
    public static string Export => T("errorlog.export", "Exportieren");

    public static string ExportTip => T("errorlog.export.tip", "Die gefilterte Liste als Text- oder CSV-Datei speichern");

    public static string ExportTitle => T("errorlog.export.title", "Fehlerliste exportieren");

    public static string ExportFilter => T("errorlog.export.filter", "Textdatei (*.txt)|*.txt|CSV-Datei (*.csv)|*.csv");

    public static string ExportFailed(string reason) => F("errorlog.export.failed", "Export fehlgeschlagen: {0}", reason);

    public static string ExportHeading(string run) => F("errorlog.export.heading", "Fehlerprotokoll {0}", run);

    public static string ExportColumns => T("errorlog.export.columns", "Bereich;Mod;Code;Fehlerart;Schweregrad;Status;Datei;Zeile;Name;Meldung");

    // Hinweisleiste.
    public static string NoLogFolder => T("errorlog.notice.noFolder", "Der Logordner des Spiels ist unbekannt.");

    public static string NoLogFound(string folder) => F("errorlog.notice.noLog", "Kein error.log gefunden in {0}", folder);

    public static string ChooseFolder => T("errorlog.notice.chooseFolder", "Ordner wählen");

    public static string ChooseFolderTitle => T("errorlog.notice.chooseFolder.title", "Logordner des Spiels wählen (enthält error.log)");

    public static string LoadFailed(string reason) => F("errorlog.notice.loadFailed", "Log konnte nicht gelesen werden: {0}", reason);

    public static string PlaysetUnknownNotice => T("errorlog.notice.playsetUnknown", "Playset unbekannt: Zuordnung nur über eigene Mods und Vanilla.");

    public static string GameFolderUnknown => T("errorlog.notice.gameUnknown", "Spielordner unbekannt: Vanilla-Dateien werden nicht gefunden.");

    public static string NoCatalog => T("errorlog.catalog.none", "Für dieses Spiel gibt es keinen Fehlerkatalog. Alle Zeilen erscheinen als nicht zugeordnet.");

    public static string CatalogProblemText(CatalogProblem problem) => problem.Kind switch
    {
        CatalogProblemKind.FileMissing => F("errorlog.catalog.missing", "Fehlerkatalog fehlt: {0}. Alle Zeilen erscheinen als nicht zugeordnet.", problem.Detail),
        CatalogProblemKind.InvalidJson => F("errorlog.catalog.invalidJson", "Fehlerkatalog ist kein gültiges JSON: {0}", problem.Detail),
        CatalogProblemKind.EntryWithoutCodeOrPattern => F("errorlog.catalog.incomplete", "Katalogeintrag ohne Code oder Muster übersprungen: {0}", problem.Detail),
        CatalogProblemKind.InvalidPattern => F("errorlog.catalog.invalidPattern", "Muster von {0} ungültig, Eintrag übersprungen: {1}", problem.Code, problem.Detail),
        _ => F("errorlog.catalog.unknownSeverity", "Unbekannter Schweregrad bei {0}: {1}", problem.Code, problem.Detail),
    };

    // Symbolindex.
    public static string IndexStatus(int done, int total) => total == 0 ? T("errorlog.index.searching", "Symbolindex: Dateien werden gesucht") : F("errorlog.index.progress", "Symbolindex: {0} von {1} Dateien", done, total);

    public static string IndexCancel => T("errorlog.index.cancel", "Abbrechen");

    public static string IndexCancelled => T("errorlog.index.cancelled", "Symbolindex abgebrochen: Hinweise zu Definitionen und Verwendungen fehlen. „Neu laden“ startet ihn erneut.");

    public static string IndexFailed(string reason) => F("errorlog.index.failed", "Symbolindex fehlgeschlagen: {0}", reason);

    // Bereiche und Gruppen.
    public static string SectionName(ErrorSection section) => section.Kind switch
    {
        ErrorSectionKind.OwnMod => F("errorlog.section.own", "Eigene Mod: {0}", section.Mod?.Name),
        ErrorSectionKind.OtherMod => F("errorlog.section.other", "Andere Mod: {0}", section.Mod?.Name),
        ErrorSectionKind.Vanilla => T("errorlog.section.vanilla", "Vanilla"),
        ErrorSectionKind.Engine => T("errorlog.section.engine", "Engine-Meldungen ohne Datei"),
        ErrorSectionKind.Unassigned => T("errorlog.section.unassigned", "Ohne Zuordnung"),
        ErrorSectionKind.Unmatched => T("errorlog.section.unmatched", "Nicht zugeordnet"),
        ErrorSectionKind.Fixed => T("errorlog.section.fixed", "Behoben"),
        _ => T("errorlog.section.suppressed", "Unterdrückte"),
    };

    public static string Section(ErrorSection section) => F("errorlog.section.withCount", "{0} ({1})", SectionName(section), section.Total);

    public static string Severity(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Error => T("errorlog.severity.error", "Fehler"),
        ErrorSeverity.Warning => T("errorlog.severity.warning", "Warnung"),
        _ => T("errorlog.severity.info", "Hinweis"),
    };

    /// <summary>Symbol vor jeder Gruppe; kein übersetzbarer Text.</summary>
    public static string SeverityIcon(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Error => "⛔",
        ErrorSeverity.Warning => "⚠",
        _ => "ℹ",
    };

    public static string Status(RunStatus status) => status switch
    {
        RunStatus.New => T("errorlog.status.new", "neu"),
        RunStatus.Unchanged => T("errorlog.status.unchanged", "unverändert"),
        _ => T("errorlog.status.fixed", "behoben"),
    };

    public static string KindName(ErrorKind? kind, string language) => kind?.NameIn(language) ?? T("errorlog.kind.unmatched", "Nicht zugeordnete Meldung");

    public static string GroupTitle(string kindName, string groupName) => groupName.Length == 0 ? kindName : F("errorlog.group", "{0}: {1}", kindName, groupName);

    public static string GroupCount(int count) => F("errorlog.group.count", "{0}×", count);

    public static string GroupChange(int previous, int current) => F("errorlog.group.change", "{0} → {1}", previous, current);

    // Einzelheiten.
    public static string NoSelection => T("errorlog.detail.none", "Links eine Gruppe wählen, um Einzelheiten zu sehen.");

    public static string DetailHeading(string kindName, string code) => F("errorlog.detail.heading", "{0} ({1})", kindName, code);

    public static string DetailMessage => T("errorlog.detail.message", "Meldung");

    public static string DetailExplain => T("errorlog.detail.explain", "Erklärung");

    public static string DetailTips => T("errorlog.detail.tips", "Tipps");

    public static string DetailFile => T("errorlog.detail.file", "Datei");

    public static string DetailExcerpt => T("errorlog.detail.excerpt", "Codeausschnitt");

    public static string DetailHints => T("errorlog.detail.hints", "Hinweise");

    public static string DetailFixes => T("errorlog.detail.fixes", "Korrekturen");

    public static string DetailOccurrences(int count) => F("errorlog.detail.occurrences", "Stellen ({0})", count);

    public static string Jump => T("errorlog.jump", "Zur Stelle springen");

    public static string MaybeMoved => T("errorlog.jump.moved", "Die Datei wurde nach dem Spiellauf geändert, die Zeile hat sich möglicherweise verschoben.");

    public static string FileNotFound(string logPath) => F("errorlog.file.notFound", "Datei nicht gefunden: {0} (gesucht in den Mods des Playsets und in Vanilla)", logPath);

    public static string LogLineText(int number) => F("errorlog.logLine", "Log-Zeile {0}", number);

    public static string At(string path, int line) => F("errorlog.at", "{0}:{1}", path, line);

    public static string OccurrenceEntry(string path, int line, int logLine) => F("errorlog.occurrence.entry", "{0}:{1}  (Log-Zeile {2})", path, line, logLine);

    public static string OccurrenceEntryIn(string path, int line, string logFile, int logLine) =>
        F("errorlog.occurrence.entryIn", "{0}:{1}  ({2}, Zeile {3})", path, line, logFile, logLine);

    public static string LogLineIn(string logFile, int number) => F("errorlog.logLineIn", "{0}, Zeile {1}", logFile, number);

    /// <summary>Fundstelle einer Meldung; bei Läufen über mehrere Dateien mit dem Namen der Logdatei (Zeilennummern zählen pro Datei).</summary>
    public static string LogPosition(ErrorEntry entry, IReadOnlyList<string> runFiles)
    {
        var file = runFiles.Count > 1 && entry.Line.Part < runFiles.Count && runFiles[entry.Line.Part].Length > 0 ? runFiles[entry.Line.Part] : null;
        if (entry.FilePath is not null && entry.FileLine is int line)
        {
            return file is null ? OccurrenceEntry(entry.FilePath, line, entry.Line.LineNumber) : OccurrenceEntryIn(entry.FilePath, line, file, entry.Line.LineNumber);
        }

        return file is null ? LogLineText(entry.Line.LineNumber) : LogLineIn(file, entry.Line.LineNumber);
    }

    public static string More(int count) => F("errorlog.more", "und {0} weitere", count);

    // Hinweise aus dem Symbolindex.
    public static string IndexLoading => T("errorlog.hint.loading", "Symbolindex wird noch aufgebaut, Hinweise folgen.");

    public static string Hint(ErrorHint hint) => hint.Kind switch
    {
        HintKind.EngineCommand => F("errorlog.hint.engine", "{0} ist ein {1} der Engine (Script-Datenbank).", hint.Name, CommandKind(hint.CommandKind)),
        HintKind.WrongCommandKind => F("errorlog.hint.wrongKind", "{0} ist ein {1} der Engine und passt an dieser Stelle nicht.", hint.Name, CommandKind(hint.CommandKind)),
        HintKind.DefinedInPlayset => F("errorlog.hint.defined", "{0} ist definiert in:", hint.Name),
        HintKind.DefinedOutsidePlayset => F("errorlog.hint.outsidePlayset", "{0} ist in {1} definiert, diese Mod fehlt im Playset.", hint.Name, hint.Mod?.Name ?? VanillaName),
        HintKind.NotDefined => F("errorlog.hint.notDefined", "{0} ist nirgends definiert.", hint.Name),
        HintKind.Usages => F("errorlog.hint.usages", "Stellen, an denen {0} gesetzt oder benutzt wird:", hint.Name),
        HintKind.SaveSetAtRuntime => F("errorlog.hint.saveSetAtRuntime", "{0} ist im Spielstand bei {1} Objekten gesetzt, entsteht also zur Laufzeit (z.B. über einen zusammengesetzten Namen, Vanilla oder eine andere Mod). Die Meldung ist vermutlich harmlos.", hint.Name, hint.Save?.ObjectCount ?? 0),
        HintKind.SaveLikelyTypo => F("errorlog.hint.saveLikelyTypo", "Vermutlich Tippfehler: {0} ist im Spielstand bei {1} Objekten gesetzt.", hint.Save?.SimilarName ?? string.Empty, hint.Save?.SimilarCount ?? 0),
        HintKind.SaveNowhere => F("errorlog.hint.saveNowhere", "{0} ist im Spielstand nirgends gesetzt: Die Lesestelle liest immer einen leeren Wert.", hint.Name),
        HintKind.UsedInGui => F("errorlog.hint.usedInGui", "Vermutlich harmlos: {0} wird in GUI-Dateien genutzt (das Spiel zählt das nicht mit).", hint.Name),
        HintKind.NotFoundInFiles => F("errorlog.hint.notFound", "{0} steht in keiner Datei wörtlich, vermutlich aus einem Parameter zusammengesetzt.", hint.Name),
        _ => F("errorlog.hint.localization", "{0} wird in der Lokalisierung benutzt. Das Spiel zählt das nicht mit, die Meldung ist vermutlich harmlos.", hint.Name),
    };

    public static string CommandKind(EngineCommandKind? kind) => kind == EngineCommandKind.Trigger ? T("errorlog.command.trigger", "Trigger") : T("errorlog.command.effect", "Effekt");

    public static string Candidates(IEnumerable<string> names) => F("errorlog.hint.candidates", "Ähnliche Namen: {0}", string.Join(", ", names));

    public static string Role(SymbolRole role) => role switch
    {
        SymbolRole.Definition => T("errorlog.role.definition", "definiert"),
        SymbolRole.Set => T("errorlog.role.set", "vermutlich gesetzt"),
        SymbolRole.Reference => T("errorlog.role.reference", "benutzt"),
        _ => T("errorlog.role.localization", "Lokalisierung"),
    };

    public static string VanillaName => T("errorlog.vanilla", "Vanilla");

    public static string Occurrence(SymbolOccurrence occurrence) => F("errorlog.hint.occurrence", "{0}, {1}: {2}:{3}", occurrence.Mod?.Name ?? VanillaName, Role(occurrence.Role), occurrence.FilePath, occurrence.Line);

    // Korrekturen (Plan 3) und Unterdrücken.
    /// <summary>Knopftext einer Korrektur; "auskommentieren" nennt den ersten Effekt aus dem Katalog wörtlich (z.B. set_variable).</summary>
    public static string Fix(string name, ErrorKind? kind) =>
        name == "commentOutSet" ? F("errorlog.fix.commentOutSet", "{0} auskommentieren", kind?.SetEffects.FirstOrDefault() ?? "set_variable") : Fix(name);

    public static string SaveHeading => T("errorlog.save.heading", "Spielstand");

    public static string SaveCheck => T("errorlog.save.check", "Mit Spielstand prüfen");

    public static string SaveShow => T("errorlog.save.show", "Im Spielstand-Explorer zeigen");

    public static string SaveLoading => T("errorlog.save.loading", "Spielstand wird gelesen");

    public static string CommentOutTitle(string effect, string name) => F("errorlog.commentOut.title", "{0} für {1} auskommentieren", effect, name);

    public static string CommentOutWarning(string name) => F("errorlog.commentOut.warning", "Wird {0} über einen zusammengesetzten Namen gelesen, erkennt PMT das nicht.", name);

    public static string CommentOutReads(string name) => F("errorlog.commentOut.reads", "{0} wird noch gelesen; auskommentieren ist gesperrt. Fundstellen:", name);

    public static string CommentOutSkipped => T("errorlog.commentOut.skipped", "Ausgelassen, weil in derselben Zeile weiterer Code steht (bitte von Hand):");

    public static string CommentOutNothing(string effect, string name) => F("errorlog.commentOut.nothing", "Keine {0}-Stelle für {1} in deinen Mods gefunden.", effect, name);

    public static string CommentOutConfirm => T("errorlog.commentOut.confirm", "Auskommentieren");

    public static string CommentOutFailed(string reason) => F("errorlog.commentOut.failed", "Auskommentieren fehlgeschlagen, alle Dateien sind wieder im alten Stand: {0}", reason);

    public static string CommentOutDone(int count, int files) => F("errorlog.commentOut.done", "{0} Stellen in {1} Dateien auskommentiert.", count, files);

    public static string CommentOutSnapshotNote(string effect) => F("errorlog.commentOut.snapshotNote", "vor {0} auskommentieren", effect);

    public static string CommentOutAction(string effect, string name) => F("errorlog.commentOut.action", "{0} auskommentieren: {1}", effect, name);

    public static string Fix(string name) => name switch
    {
        "replaceWithCandidate" => T("errorlog.fix.replace", "Kandidat einsetzen"),
        "renameEverywhere" => T("errorlog.fix.rename", "Überall umbenennen"),
        "addModToPlayset" => T("errorlog.fix.addMod", "Mod ins Playset aufnehmen"),
        "traceScope" => T("errorlog.fix.traceScope", "Scope verfolgen"),
        _ => name,
    };

    public static string TraceHeading => T("errorlog.trace.heading", "Ausgelöst von");

    public static string TraceCandidatesHeading => T("errorlog.trace.candidates", "Mögliche Auslöser (nicht eindeutig)");

    public static string TraceNoHits => T("errorlog.trace.noHits", "Spur aktiv, noch keine Treffer in debug.log");

    public static string TraceSaved => T("errorlog.trace.saved", "Gespeicherte Event Targets");

    public static string TraceNotOwn => T("errorlog.trace.notOwn", "Nur in eigenen Mods");

    public static string TraceNoEffectContext => T("errorlog.trace.noEffectContext", "An dieser Stelle kann kein Effekt stehen");

    public static string TraceAlready => T("errorlog.trace.already", "Spur ist schon aktiv");

    public static string TraceLineMissing => T("errorlog.trace.lineMissing", "Die Zeile gibt es in der Datei nicht mehr");

    public static string TraceDialogTitle => T("errorlog.trace.title", "Scope verfolgen");

    public static string TraceUnguardedKind => T("errorlog.trace.unguarded", "Bei jedem Aufruf (keine passende Bedingung, Ergebnis evtl. nicht eindeutig)");

    public static string TraceInsert => T("errorlog.trace.insert", "Einfügen");

    public static string TraceGameRunning => T("errorlog.trace.gameRunning", "Das Spiel läuft. Im Debug-Modus lädt es die Datei meist sofort neu; das kann scheitern oder das Spiel abstürzen lassen. Vorher speichern.");

    public static string TraceWhenEffective => T("errorlog.trace.whenEffective", "Die Spur wirkt, sobald die Stelle das nächste Mal läuft (nach Neuladen durch das Spiel oder beim nächsten Spielstart).");

    public static string TraceBigLogHint => T("errorlog.trace.bigLog", "Läuft die Stelle oft, wird debug.log groß.");

    public static string TraceSnapshotNote => T("errorlog.trace.snapshotNote", "vor Scope-Spur");

    public static string TraceFileChanged => T("errorlog.trace.fileChanged", "Die Datei wurde seit der Vorschau geändert; nichts geschrieben.");

    public static string TraceNothing => T("errorlog.trace.nothing", "Keine Spur zu schreiben.");

    public static string ActiveTracesHeading => T("errorlog.trace.active", "Aktive Spuren");

    public static string TraceBroken => T("errorlog.trace.broken", "beschädigt");

    public static string RemoveTrace => T("errorlog.trace.remove", "Spur entfernen");

    public static string RemoveAllTraces => T("errorlog.trace.removeAll", "Alle Spuren entfernen");

    public static string RemoveTracesSnapshotNote => T("errorlog.trace.removeSnapshotNote", "vor Entfernen von Scope-Spuren");

    public static string DebugLogMissing => T("errorlog.trace.debugLogMissing", "debug.log nicht gefunden");

    public static string TraceNoSaveObject => T("errorlog.trace.noSaveObject", "Objekt hat im Spielstand keine Variablen");

    public static string TraceGuardedKind(string limit) => F("errorlog.trace.guarded", "Nur bei scheiterndem Aufruf (Bedingung: {0})", limit);

    public static string TraceScopeLine(string scope, string root, int count) => F("errorlog.trace.scopeLine", "{0}, Root {1} ×{2}", scope, root, count);

    public static string TraceScopeOnly(string scope, int count) => F("errorlog.trace.scopeOnly", "{0} ×{1}", scope, count);

    public static string TraceAction(string id) => F("errorlog.trace.action", "Scope-Spur {0} einfügen", id);

    public static string TraceDone(int count, int files) => F("errorlog.trace.done", "Spur eingefügt ({0} Zeile in {1} Datei).", count, files);

    public static string TraceFailed(string reason) => F("errorlog.trace.failed", "Einfügen fehlgeschlagen, die Datei ist wieder im alten Stand: {0}", reason);

    public static string RemoveTracesAction(int count) => F("errorlog.trace.removeAction", "{0} Scope-Spuren entfernen", count);

    public static string RemoveTracesDone(int count, int files) => F("errorlog.trace.removeDone", "{0} Spuren in {1} Dateien entfernt.", count, files);

    public static string RemoveTracesFailed(string reason) => F("errorlog.trace.removeFailed", "Entfernen fehlgeschlagen, alle Dateien sind wieder im alten Stand: {0}", reason);

    public static string RemoveTracesConfirm(int count, string files) => F("errorlog.trace.removeConfirm", "{0} Spuren entfernen?\n\n{1}", count, files);

    public static string ActiveTraceLine(string mod, string file, int line, int hits) => F("errorlog.trace.activeLine", "{0}: {1}:{2}  ({3} Treffer)", mod, file, line, hits);

    public static string PublishTracesWarning(int count) => F("errorlog.trace.publishWarning", "Die Mod enthält noch {0} Scope-Spuren (PMT-TRACE). Entfernen im Fehlerprotokoll unter \"Aktive Spuren\". Trotzdem veröffentlichen?", count);

    public static string FixLater => T("errorlog.fix.later", "Folgt in einer späteren Version von PMT.");

    public static string SuppressHeading => T("errorlog.suppress", "Unterdrücken");

    public static string SuppressOption(Suppression suppression, string kindName, string? modName) => suppression.Scope switch
    {
        SuppressionScope.Code => F("errorlog.suppress.code", "Alle Meldungen der Art {0}", kindName),
        SuppressionScope.CodeAndName => F("errorlog.suppress.name", "Nur {0} bei dieser Art", suppression.Name),
        SuppressionScope.Mod => F("errorlog.suppress.mod", "Alle Meldungen der Mod {0}", modName ?? suppression.Value),
        _ => F("errorlog.suppress.file", "Alle Meldungen der Datei {0}", Path.GetFileName(suppression.Value)),
    };

    public static string SuppressFailed(string reason) => F("errorlog.suppress.failed", "Unterdrückung konnte nicht gespeichert werden: {0}", reason);

    public static string Restore => T("errorlog.restore", "Wieder einblenden");

    public static string DefaultSuppressed => T("errorlog.defaultSuppressed", "Diese Fehlerart ist im Katalog standardmäßig unterdrückt.");

    // Plan 2b: Menü und Titel mit Live-Zähler.
    public static string MenuWithCount(int count) => F("errorlog.menu.count", "Fehlerprotokoll (+{0})", count);

    public static string WindowTitleWithCount(string game, int count) => F("errorlog.window.titleCount", "Fehlerprotokoll - {0} (+{1})", game, count);

    // Plan 2b: Pfadfilter (aus Tab, Explorer oder Notizbereich geöffnet).
    public static string PathFilter(string name) => F("errorlog.pathFilter", "Nur: {0}", name);

    public static string PathFilterClearTip => T("errorlog.pathFilter.clear.tip", "Filter auf diese Datei bzw. diesen Ordner aufheben");

    // Plan 2b: Markierungen an Tabs und im Explorer.
    public static string MarkTooltip(ErrorFileMark mark) => F("errorlog.mark.tip", "{0} Meldungen aus dem letzten Spiellauf, schwerste: {1}. Klick öffnet das Fehlerprotokoll für diese Stelle.", mark.Count, Severity(mark.Severity));

    // Plan 2b: Abschnitt im Notizbereich.
    public static string NotesHeading(int count) => F("errorlog.notes.heading", "⚠ Fehler ({0})", count);

    public static string NotesShowInLog => T("errorlog.notes.showInLog", "Im Fehlerprotokoll zeigen");

    public static string NotesShowInLogTip => T("errorlog.notes.showInLog.tip", "Öffnet das Fehlerprotokoll, gefiltert auf diese Datei");

    public static string NotesLine(int line) => F("errorlog.notes.line", "Zeile {0}", line);

    public static string NotesJumpTip => T("errorlog.notes.jump.tip", "Doppelklick springt zur Zeile");

    // Plan 2b: GUI-Inspektor.
    public static string InspectGui => T("errorlog.inspectGui", "Im GUI-Inspektor zeigen");

    public static string InspectGuiTip => T("errorlog.inspectGui.tip", "Zeigt das GUI-Element an dieser Stelle im GUI-Inspektor");

    public static string InspectGuiFailed => T("errorlog.inspectGui.failed", "An dieser Stelle wurde kein GUI-Element gefunden, oder die GUI-Werkzeuge sind ausgeschaltet.");

    // Plan 2b: Live-Beobachtung.
    public static string LiveToggle => T("errorlog.live", "Live");

    public static string LiveTip => T("errorlog.live.tip", "error.log beobachten, während das Spiel läuft: neue Meldungen erscheinen sofort, gefiltert wie die Liste");

    public static string LiveMarker(int count) => F("errorlog.live.marker", "+{0} live", count);

    public static string LiveCountButton(int count) => F("errorlog.live.count", "+{0}", count);

    public static string LiveCountTip => T("errorlog.live.count.tip", "Neue sichtbare Meldungen seit dem letzten Ansehen. Klick setzt den Zähler zurück.");

    public static string LiveRestarted => T("errorlog.live.restarted", "Neuer Spiellauf erkannt: Die Liste zeigt jetzt den neuen Lauf, der bisherige ist der Vergleichslauf.");

    // Plan 3: Korrekturen, die Dateien ändern.
    public static string FixNeedsLocation => T("errorlog.fix.needsLocation", "Nur mit einer Fundstelle im Log möglich. Für Meldungen ohne Datei „Überall umbenennen“ nutzen.");

    public static string FixCandidatesTip => T("errorlog.fix.candidates.tip", "Kandidat wählen: Er ersetzt den Namen an der Fundstelle im Editor. Speichern musst du selbst.");

    public static string FixNameNotOnLine(string name, int line) => F("errorlog.fix.notOnLine", "{0} steht nicht mehr in Zeile {1}. Die Datei wurde vermutlich geändert; „Neu laden“ liest das Log neu.", name, line);

    public static string Cancel => T("errorlog.cancel", "Abbrechen");

    public static string RenameTitle(string name) => F("errorlog.rename.title", "{0} überall umbenennen", name);

    public static string RenameNewName => T("errorlog.rename.newName", "Neuer Name:");

    public static string RenameCandidates => T("errorlog.rename.candidates", "Vorschläge:");

    public static string RenameSummary(int count, int files, int mods) => F("errorlog.rename.summary", "{0} Stellen in {1} Dateien, {2} Mods", count, files, mods);

    public static string RenameInvalid => T("errorlog.rename.invalid", "Nur Buchstaben, Ziffern und _, nicht mit einer Ziffer beginnen und anders als der alte Name.");

    public static string RenameExists(string name) => F("errorlog.rename.exists", "{0} gibt es schon. Umbenennen würde beide Namen zusammenlegen.", name);

    public static string RenameModHeading(string mod, int files) => F("errorlog.rename.mod", "{0} ({1} Dateien)", mod, files);

    public static string RenameFileHeading(string path, int count) => F("errorlog.rename.file", "{0} ({1}×)", path, count);

    public static string RenameLineText(int number, string before, string after) => F("errorlog.rename.line", "{0}: {1}  →  {2}", number, before, after);

    public static string RenameConfirm => T("errorlog.rename.confirm", "Umbenennen");

    public static string RenameSnapshotHint => T("errorlog.rename.snapshotHint", "Vorher legt PMT für jede betroffene Mod eine Sicherung an. Rückgängig machen lässt sich alles in einem Schritt mit „Rückgängig“.");

    public static string RenameNothing(string name) => F("errorlog.rename.nothing", "{0} steht in keiner Datei deiner Mods.", name);

    public static string RenameUnreadable(string files) => F("errorlog.rename.unreadable", "Diese Dateien enthalten den Namen, sind aber kein gültiges UTF-8. PMT ändert sie nicht, deshalb ist Umbenennen gesperrt: {0}", files);

    public static string RenameUnsaved(string file) => F("errorlog.rename.unsaved", "{0} hat ungespeicherte Änderungen. Bitte zuerst speichern oder verwerfen.", file);

    public static string RenameSnapshotFailed(string reason) => F("errorlog.rename.snapshotFailed", "Sicherung fehlgeschlagen, nichts wurde geändert: {0}", reason);

    public static string RenameFailed(string reason) => F("errorlog.rename.failed", "Umbenennen fehlgeschlagen, alle Dateien sind wieder im alten Stand: {0}", reason);

    public static string RenameDone(int count, int files) => F("errorlog.rename.done", "{0} Stellen in {1} Dateien umbenannt.", count, files);

    public static string RenameAction(string oldName, string newName) => F("errorlog.rename.action", "Umbenennen {0} → {1}", oldName, newName);

    public static string RenameSnapshotNote => T("errorlog.rename.snapshotNote", "vor-umbenennen");

    public static string AddModTitle => T("errorlog.addMod.title", "Mod ins Playset aufnehmen");

    public static string AddModConfirm(string mod, string playset) => F("errorlog.addMod.confirm", "{0} ins Playset „{1}“ aufnehmen? PMT setzt die Mod vor deine erste eigene Mod und sichert playsets.json vorher als playsets.json.pmt-backup. Das Spiel muss dafür geschlossen sein.", mod, playset);

    public static string AddModConfirmLauncher(string mod, string playset) => F("errorlog.addMod.confirmLauncher", "{0} ins Playset „{1}“ aufnehmen? PMT setzt die Mod vor deine erste eigene Mod und sichert die Datenbank des Paradox-Launchers vorher (Sicherungen im Playset-Fenster). Spiel und Launcher müssen dafür geschlossen sein.", mod, playset);

    public static string AddModNoPlayset => T("errorlog.addMod.noPlayset", "Kein Playset gewählt. Bitte oben im Fenster ein Playset wählen.");

    public static string AddModAdded(string mod, string playset) => F("errorlog.addMod.added", "{0} steht jetzt im Playset „{1}“. Die Meldungen verschwinden nach dem nächsten Spielstart.", mod, playset);

    public static string AddModEnabled(string mod, string playset) => F("errorlog.addMod.enabled", "{0} war im Playset „{1}“ ausgeschaltet und ist jetzt eingeschaltet.", mod, playset);

    public static string AddModAlreadyPresent(string mod, string playset) => F("errorlog.addMod.present", "{0} ist schon eingeschaltet im Playset „{1}“.", mod, playset);

    public static string AddModGameRunning => T("errorlog.addMod.gameRunning", "Bitte zuerst das Spiel schließen. Es schreibt playsets.json selbst und würde die Änderung überschreiben.");

    public static string AddModFileMissing => T("errorlog.addMod.fileMissing", "Keine playsets.json eingestellt oder gefunden (Einstellungen, Allgemein, Playset-Datei).");

    public static string AddModPlaysetMissing(string playset) => F("errorlog.addMod.playsetMissing", "Playset „{0}“ steht nicht in playsets.json.", playset);

    public static string AddModInvalidFile => T("errorlog.addMod.invalid", "playsets.json hat nicht das erwartete Format. Nichts wurde geändert.");

    public static string AddModWriteFailed(string reason) => F("errorlog.addMod.writeFailed", "playsets.json konnte nicht geschrieben werden, sie ist unverändert: {0}", reason);

    /// <summary>Befund-Zeile im Abschnitt "Spielstand": gesetzt bei N Objekten mit Beispielen, oder nirgends gesetzt.</summary>
    public static string EvidenceLine(SaveEvidence evidence) =>
        evidence.ObjectCount > 0
            ? F("errorlog.save.evidence", "Im Spielstand {0}: bei {1} Objekten gesetzt ({2})", evidence.SaveLabel, evidence.ObjectCount, string.Join(", ", evidence.Examples.Select(e => F("errorlog.save.example", "{0} {1}", e.Kind.Label, e.DisplayName))))
            : F("errorlog.save.nowhere", "Im Spielstand {0} nirgends gesetzt", evidence.SaveLabel);

    private static string T(string key, string german) => UiText.Get(key, german);

    private static string F(string key, string german, params object?[] args) => UiText.Format(key, german, args);
}
