namespace ParadoxModTool.ErrorLog;

/// <summary>Bereiche der Fehlerliste. Die ersten sechs entsprechen <see cref="ErrorBucket"/>.</summary>
public enum ErrorSectionKind
{
    OwnMod,
    OtherMod,
    Vanilla,
    Engine,
    Unassigned,
    Unmatched,
    Fixed,
    Suppressed,
}

/// <summary>
/// Eine Zeile der Fehlerliste: Fehler gleicher Zuordnung, Fehlerart und gleichen Namens. <c>Count</c> und <c>PreviousCount</c> zählen
/// Log-Zeilen wie <see cref="RunComparison"/> (Plan-Ruling 8). Behobene Gruppen haben <c>Count</c> 0; <c>Entries</c> enthält dann das
/// Beispiel aus dem Vergleichslauf.
/// </summary>
public sealed record ErrorGroupView(
    string Key,
    string Code,
    string GroupName,
    ErrorKind? Kind,
    ErrorSeverity Severity,
    RunStatus Status,
    int Count,
    int PreviousCount,
    IReadOnlyList<ErrorEntry> Entries,
    ErrorAttribution Attribution);

/// <summary>Ein Bereich der Liste (je eigene oder fremde Mod einer, sonst einer je Art).</summary>
public sealed record ErrorSection(ErrorSectionKind Kind, ModSource? Mod, IReadOnlyList<ErrorGroupView> Groups)
{
    public string Key => $"{Kind}|{Mod?.Directory}";

    public int Total => Kind == ErrorSectionKind.Fixed ? Groups.Sum(g => g.PreviousCount) : Groups.Sum(g => g.Count);
}

/// <summary>Filter des Fensters. Standard: alles sichtbar.</summary>
public sealed record ErrorFilter
{
    public static ErrorFilter All { get; } = new();

    public bool Errors { get; init; } = true;

    public bool Warnings { get; init; } = true;

    public bool Infos { get; init; } = true;

    public bool New { get; init; } = true;

    public bool Unchanged { get; init; } = true;

    public bool Fixed { get; init; } = true;

    /// <summary>Nur diese Fehlerart; <see langword="null"/> = alle.</summary>
    public string? Code { get; init; }

    public bool OwnModsOnly { get; init; }

    public string Search { get; init; } = string.Empty;

    /// <summary>Nur Fehler in dieser Datei oder unter diesem Ordner (voller Pfad); <see langword="null"/> = überall.</summary>
    public string? Path { get; init; }
}

/// <summary>Markierung einer Datei oder eines Ordners (Tabs, Explorer): Log-Zeilen und schwerster Schweregrad.</summary>
public sealed record ErrorFileMark(int Count, ErrorSeverity Severity);

/// <summary>Zähler der Kopfzeile: Log-Zeilen und Gruppen der sichtbaren Bereiche (ohne "Behoben" und "Unterdrückte"), neue und behobene Gruppen.</summary>
public sealed record ErrorCounts(int Occurrences, int Groups, int NewGroups, int FixedGroups);

/// <summary>Sprungziel eines Fehlers; <c>MaybeMoved</c>: Die Datei wurde nach dem Lauf geändert, die Zeile ist geschätzt.</summary>
public sealed record JumpTarget(string FilePath, int Line, bool MaybeMoved);

/// <summary>
/// Der geladene Lauf mit Vergleichslauf, Zuordnung, Unterdrückungen und Symbolindex; Grundlage für Fenster, Notizbereich und Symbole.
/// Nicht threadsicher: Die App ändert die Sitzung nur im UI-Thread. <see cref="Changed"/> meldet jede Änderung.
/// </summary>
public sealed class ErrorLogSession
{
    private readonly string? _suppressionsPath;
    private readonly Func<string, EngineCommandKind?> _engineCommandKind;
    private readonly Dictionary<string, ErrorAttribution> _attributions = new(StringComparer.Ordinal);
    private Dictionary<string, ErrorFileMark>? _fileMarks;
    private Dictionary<string, ErrorFileMark>? _folderMarks;
    private List<ErrorEntry> _entries = new();
    private List<ErrorEntry> _comparison = new();
    private List<LogLine> _lines = new();
    private long? _liveFrom;
    private long _seen = long.MaxValue;
    private IReadOnlyList<ActiveTrace> _traces = Array.Empty<ActiveTrace>();
    private List<TraceHit> _traceHits = new();
    private SourceResolver _resolver;
    private RunDiff _diff;

    public ErrorLogSession(
        ErrorCatalog catalog,
        SuppressionSet suppressions,
        string? suppressionsPath,
        string language,
        Func<string, EngineCommandKind?>? engineCommandKind = null)
    {
        Catalog = catalog;
        Suppressions = suppressions;
        _suppressionsPath = suppressionsPath;
        Language = language;
        _engineCommandKind = engineCommandKind ?? (_ => null);
        _resolver = CreateResolver();
        _diff = RunComparison.Compare(_entries, _comparison);
    }

    public event EventHandler? Changed;

    public ErrorCatalog Catalog { get; }

    public SuppressionSet Suppressions { get; }

    /// <summary>Sprache der Katalogtexte (Namen der Fehlerarten in Suche, Liste und Export).</summary>
    public string Language { get; }

    public IReadOnlyList<ModSource> Mods { get; private set; } = Array.Empty<ModSource>();

    public IReadOnlyList<string> VanillaRoots { get; private set; } = Array.Empty<string>();

    public SymbolIndex Index { get; private set; } = SymbolIndex.Empty;

    /// <summary>Der Index passt zu <see cref="Mods"/>. Solange nicht, zeigen Hinweise "wird geladen" (Spec, Fehlerbehandlung).</summary>
    public bool IndexReady { get; private set; }

    public string? RunPath { get; private set; }

    /// <summary>Dateinamen des geladenen Laufs, älteste zuerst (Index = <see cref="LogLine.Part"/>); leer ohne Lauf.</summary>
    public IReadOnlyList<string> RunFileNames { get; private set; } = Array.Empty<string>();

    /// <summary>Zeitpunkt des Laufs (letzte Änderung der Logdatei). Später geänderte Dateien gelten als "seit dem Lauf geändert".</summary>
    public DateTime RunTimeUtc { get; private set; } = DateTime.MaxValue;

    public IReadOnlyList<ErrorEntry> Entries => _entries;

    /// <summary>Scope-Spuren in den eigenen Mods (aus <see cref="TraceMarkers.Scan"/>).</summary>
    public IReadOnlyList<ActiveTrace> Traces => _traces;

    /// <summary>Ids aller Treffer im aktuellen debug.log; eine neue Spur meidet sie (<see cref="TraceMarkers.NewId"/>).</summary>
    public IReadOnlyCollection<string> TraceHitIds => _traceHits.Select(h => h.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>Zählt jede Änderung an Spuren oder Treffern (das Fenster baut die Einzelheiten dann neu).</summary>
    public int TraceVersion { get; private set; }

    public void SetTraces(IReadOnlyList<ActiveTrace> traces)
    {
        _traces = traces;
        TraceVersion++;
        Raise();
    }

    /// <summary>Alle Treffer neu (debug.log ganz gelesen oder neu angelegt).</summary>
    public void SetTraceHits(IReadOnlyList<TraceHit> hits)
    {
        _traceHits = hits.ToList();
        TraceVersion++;
        Raise();
    }

    /// <summary>Neue Treffer aus dem Live-Mitlesen von debug.log.</summary>
    public void AddTraceHits(IReadOnlyList<TraceHit> hits)
    {
        if (hits.Count == 0)
        {
            return;
        }

        _traceHits.AddRange(hits);
        TraceVersion++;
        Raise();
    }

    public int HitCountOf(ActiveTrace trace) => _traceHits.Count(h => h.Id == trace.Id);

    /// <summary>Die Spur, in deren Zeilenbereich die Stelle des Fehlers liegt (gleiche aufgelöste Datei).</summary>
    public ActiveTrace? TraceFor(ErrorEntry entry)
    {
        if (_traces.Count == 0 || entry.FileLine is not int line || AttributionOf(entry).Source is not { } source)
        {
            return null;
        }

        var key = PathKey(source.FullPath);
        return _traces.FirstOrDefault(t => line >= t.FirstLine && line <= t.LastLine && string.Equals(PathKey(t.Path), key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Befund der Spur zum Fehler: Treffer gegen alle Meldungen derselben Art aus dem Bereich der Spur.</summary>
    public TraceResult? TraceResultFor(ErrorEntry entry)
    {
        if (TraceFor(entry) is not { } trace)
        {
            return null;
        }

        var errors = _entries.Where(e => e.Kind == entry.Kind && ReferenceEquals(TraceFor(e), trace)).ToList();
        return TraceMatcher.Match(trace, errors, _traceHits);
    }

    public string? ComparisonPath { get; private set; }

    public bool HasComparison => ComparisonPath is not null;

    public RunDiff Diff => _diff;

    public FixAdvisor Advisor => new(Index, _engineCommandKind);

    /// <summary>Neue Mod-Liste (z.B. anderes Playset). Der bisherige Index passt nicht mehr und wird verworfen.</summary>
    public void SetSources(IReadOnlyList<ModSource> mods, IReadOnlyList<string> vanillaRoots)
    {
        Mods = mods;
        VanillaRoots = vanillaRoots;
        Index = SymbolIndex.Empty;
        IndexReady = false;
        ResetAttribution();
        Raise();
    }

    public void SetIndex(SymbolIndex index)
    {
        Index = index;
        IndexReady = true;
        ResetAttribution();
        Raise();
    }

    public void LoadRun(string path) => LoadRun(path, LogReader.ReadFile(path), File.GetLastWriteTimeUtc(path));

    /// <summary>Lädt alle Dateien eines Laufs als ein Protokoll (das Spiel wechselt error.log mitten im Lauf).</summary>
    public void LoadRun(LogRun run) =>
        LoadRun(run.Newest.Path, LogReader.ReadRun(run).Lines, run.Newest.LastWriteUtc, run.Files.Select(f => f.FileName).ToList());

    public void LoadRun(string path, IReadOnlyList<LogLine> lines, DateTime runTimeUtc, IReadOnlyList<string>? fileNames = null)
    {
        RunPath = path;
        RunTimeUtc = runTimeUtc;
        RunFileNames = fileNames ?? new[] { Path.GetFileName(path) };
        _lines = lines.ToList();
        _entries = ErrorClassifier.Classify(_lines, Catalog);
        ResetAttribution();
        Compare();
        Raise();
    }

    public void ClearRun()
    {
        RunPath = null;
        RunFileNames = Array.Empty<string>();
        RunTimeUtc = DateTime.MaxValue;
        _entries = new List<ErrorEntry>();
        _lines = new List<LogLine>();
        _liveFrom = null;
        _seen = long.MaxValue;
        ResetAttribution();
        Compare();
        Raise();
    }

    /// <summary>Vergleichslauf aus allen seinen Dateien; <see langword="null"/> entfernt den Vergleich.</summary>
    public void LoadComparison(LogRun? run) =>
        LoadComparison(run?.Newest.Path, run is null ? Array.Empty<LogLine>() : LogReader.ReadRun(run).Lines);

    /// <summary>
    /// Nach einem Wechsel von error.log mitten im Lauf heißen die bisherigen Dateien eine Nummer höher (error.log wird error.1.log). Die
    /// Namen zählen vom neuesten Teil (error.log) rückwärts; Teile hinter error.5.log hat das Spiel gelöscht und bleiben ohne Namen.
    /// </summary>
    public void RenameRunFilesAfterSwitch()
    {
        var lastPart = _lines.Count == 0 ? 0 : _lines.Max(l => l.Part);
        RunFileNames = Enumerable.Range(0, lastPart + 1)
            .Select(part => lastPart - part is var index && index <= ErrorLogLocations.MaxPreviousRuns ? ErrorLogLocations.RunFileName(index) : string.Empty)
            .ToList();
    }

    public void LoadComparison(string? path, IReadOnlyList<LogLine> lines)
    {
        ComparisonPath = path;
        _comparison = path is null ? new List<ErrorEntry>() : ErrorClassifier.Classify(lines, Catalog);
        Compare();
        Raise();
    }

    /// <summary>Live-Beobachtung läuft: Einträge ab <see cref="StartLive"/> gelten als "neu in diesem Lauf".</summary>
    public bool LiveActive => _liveFrom is not null;

    /// <summary>Live beginnt: Alles ab der nächsten Zeile gilt als neu und zählt für "+N"; nach einem Spielstart (Rotation) alles.</summary>
    public void StartLive(bool everythingIsNew = false)
    {
        _liveFrom = everythingIsNew ? 0 : NextOrder();
        _seen = _liveFrom.Value;
        Raise();
    }

    public void StopLive()
    {
        _liveFrom = null;
        _seen = long.MaxValue;
        Raise();
    }

    /// <summary>Der Nutzer hat hingeschaut: "+N" zählt ab jetzt neu. Die Liste ändert sich nicht, daher kein <see cref="Changed"/>.</summary>
    public void MarkLiveSeen()
    {
        if (_liveFrom is not null)
        {
            _seen = NextOrder();
        }
    }

    /// <summary>Hängt eine Live-Portion an; Folgezeilen am Anfang gehören zum letzten Eintrag (<see cref="LogReader.Append"/>). Klassifiziert neu.</summary>
    public void AppendLines(IReadOnlyList<LogLine> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        LogReader.Append(_lines, lines);
        _entries = ErrorClassifier.Classify(_lines, Catalog);
        Compare();
        Raise();
    }

    public bool IsLiveNew(ErrorEntry entry) => _liveFrom is long from && entry.Line.Order >= from;

    /// <summary>"+N": sichtbare (Filter), nicht unterdrückte Einträge seit dem letzten Ansehen (Plan-2b-Ruling 10).</summary>
    public int LiveNewCount(ErrorFilter filter) =>
        _liveFrom is null
            ? 0
            : Sections(filter)
                .Where(s => s.Kind is not (ErrorSectionKind.Fixed or ErrorSectionKind.Suppressed))
                .SelectMany(s => s.Groups)
                .SelectMany(g => g.Entries)
                .Count(e => e.Line.Order >= _seen);

    /// <summary>Ordnung der nächsten Zeile im Lauf (über alle Dateien, <see cref="LogLine.Order"/>).</summary>
    private long NextOrder() => _lines.Count == 0 ? 0 : _lines[^1].Order + 1;

    /// <summary>Zuordnung mit Zwischenspeicher: Sie hängt nur von der Datei aus dem Log ab, ohne Datei vom Namen.</summary>
    public ErrorAttribution AttributionOf(ErrorEntry entry)
    {
        var key = entry.Kind is null ? "?"
            : entry.FilePath is not null ? "F|" + entry.FilePath
            : entry.Name is not null ? "N|" + entry.Name
            : "E";
        if (!_attributions.TryGetValue(key, out var attribution))
        {
            attribution = ErrorAttributor.Attribute(entry, _resolver, Index);
            _attributions[key] = attribution;
        }

        return attribution;
    }

    public bool IsSuppressed(ErrorEntry entry) => Suppressions.IsSuppressed(entry, AttributionOf(entry));

    /// <summary>
    /// Für den Spielstand-Explorer (Plan 2b): je Name aus sichtbaren Meldungen des aktuellen Laufs, deren Fehlerart im Spielstand sucht
    /// (<c>saveNames</c>), die höchste Schwere.
    /// </summary>
    public IReadOnlyDictionary<string, ErrorSeverity> NameSeverities()
    {
        var result = new Dictionary<string, ErrorSeverity>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            if (entry.Name is not { } name || entry.Kind?.SaveNames is null || IsDefaultSuppressed(entry) || IsSuppressed(entry))
            {
                continue;
            }

            if (!result.TryGetValue(name, out var existing) || entry.Severity < existing)
            {
                result[name] = entry.Severity;
            }
        }

        return result;
    }

    /// <summary>Katalog-Standardunterdrückung: Sie lässt sich nicht einblenden (Plan-1-Ruling 7).</summary>
    public static bool IsDefaultSuppressed(ErrorEntry entry) => entry.Kind?.DefaultSuppressed == true;

    /// <summary>Die möglichen Unterdrückungen für einen Eintrag: Fehlerart, Fehlerart + Name, Mod, Datei (Spec: drei Ebenen).</summary>
    public IReadOnlyList<Suppression> SuppressionOptions(ErrorEntry entry)
    {
        var options = new List<Suppression>();
        if (entry.Kind is null)
        {
            return options;
        }

        options.Add(new Suppression(SuppressionScope.Code, entry.Code));
        if (entry.Name is not null)
        {
            options.Add(new Suppression(SuppressionScope.CodeAndName, entry.Code, entry.Name));
        }

        var attribution = AttributionOf(entry);
        if (attribution.Mod is not null)
        {
            options.Add(new Suppression(SuppressionScope.Mod, attribution.Mod.Directory));
        }

        if (attribution.Source is not null)
        {
            options.Add(new Suppression(SuppressionScope.File, attribution.Source.FullPath));
        }

        return options;
    }

    public void Suppress(Suppression suppression)
    {
        if (!Suppressions.Add(suppression))
        {
            return;
        }

        try
        {
            Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Suppressions.Remove(suppression);
            throw;
        }

        Raise();
    }

    /// <summary>Blendet einen Eintrag wieder ein: entfernt alle gespeicherten Unterdrückungen, die ihn treffen. Rückgabe: Anzahl entfernter.</summary>
    public int Restore(ErrorEntry entry)
    {
        var matching = Suppressions.ItemsMatching(entry, AttributionOf(entry));
        if (matching.Count == 0)
        {
            return 0;
        }

        foreach (var suppression in matching)
        {
            Suppressions.Remove(suppression);
        }

        try
        {
            Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Suppressions.Items.AddRange(matching);
            throw;
        }

        Raise();
        return matching.Count;
    }

    /// <summary>Sprungziel (Spec, <c>jump</c>). Ist die Datei seit dem Lauf geändert, die nächstgelegene Zeile mit dem Namen.</summary>
    public JumpTarget? JumpTargetOf(ErrorEntry entry)
    {
        var source = AttributionOf(entry).Source;
        if (source is null || entry.FileLine is not int line)
        {
            return null;
        }

        if (!source.ChangedSinceRun)
        {
            return new JumpTarget(source.FullPath, line, false);
        }

        int? nearest = null;
        if (entry.Name is { } name)
        {
            try
            {
                nearest = SourceResolver.FindNearestLine(File.ReadAllLines(source.FullPath), line, name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                nearest = null;
            }
        }

        return new JumpTarget(source.FullPath, nearest ?? line, true);
    }

    public IReadOnlyList<ErrorSection> Sections(ErrorFilter filter)
    {
        var visible = new List<ErrorEntry>();
        var suppressed = new List<ErrorEntry>();
        foreach (var entry in _entries.Where(e => Matches(e, filter) && UnderPath(e, filter.Path)))
        {
            (IsSuppressed(entry) ? suppressed : visible).Add(entry);
        }

        var sections = new List<ErrorSection>();
        var groups = ErrorGrouping.Group(visible, AttributionOf)
            .Select(View)
            .Where(g => StatusVisible(g.Status, filter) && (!filter.OwnModsOnly || g.Attribution.Bucket == ErrorBucket.OwnMod));
        foreach (var bucket in groups.GroupBy(g => (g.Attribution.Bucket, Directory: g.Attribution.Mod?.Directory)))
        {
            sections.Add(new ErrorSection(SectionOf(bucket.Key.Bucket), bucket.First().Attribution.Mod, bucket.ToList()));
        }

        if (HasComparison && filter.Fixed)
        {
            var fixedGroups = _diff.Fixed
                .Where(d => Matches(d.Sample, filter) && UnderPath(d.Sample, filter.Path) && !IsSuppressed(d.Sample))
                .Select(d => (Diff: d, Attribution: AttributionOf(d.Sample)))
                .Where(x => !filter.OwnModsOnly || x.Attribution.Bucket == ErrorBucket.OwnMod)
                .Select(x => new ErrorGroupView(
                    "Fixed|" + x.Diff.Key,
                    x.Diff.Sample.Code,
                    GroupNameOf(x.Diff.Sample),
                    x.Diff.Sample.Kind,
                    x.Diff.Sample.Severity,
                    RunStatus.Fixed,
                    0,
                    x.Diff.PreviousCount,
                    new[] { x.Diff.Sample },
                    x.Attribution))
                .OrderBy(g => g.Attribution.Bucket)
                .ThenByDescending(g => g.PreviousCount)
                .ThenBy(g => g.GroupName, StringComparer.Ordinal)
                .ToList();
            if (fixedGroups.Count > 0)
            {
                sections.Add(new ErrorSection(ErrorSectionKind.Fixed, null, fixedGroups));
            }
        }

        var hidden = ErrorGrouping.Group(suppressed, AttributionOf)
            .Select(View)
            .Where(g => !filter.OwnModsOnly || g.Attribution.Bucket == ErrorBucket.OwnMod)
            .ToList();
        if (hidden.Count > 0)
        {
            sections.Add(new ErrorSection(ErrorSectionKind.Suppressed, null, hidden));
        }

        return sections;
    }

    /// <summary>Vergleichsschlüssel für Pfade: voller Pfad ohne Trenner am Ende (Vergleich ohne Groß-/Kleinschreibung).</summary>
    public static string PathKey(string path) => System.IO.Path.GetFullPath(path).TrimEnd('\\', '/');

    /// <summary>Markierung einer Datei: Fehler eigener Mods, nicht unterdrückt (Plan-2b-Ruling 3).</summary>
    public ErrorFileMark? MarkForFile(string path)
    {
        EnsureMarks();
        return _fileMarks!.TryGetValue(PathKey(path), out var mark) ? mark : null;
    }

    /// <summary>Markierung eines Ordners: Summe und schwerster Schweregrad aller markierten Dateien darunter.</summary>
    public ErrorFileMark? MarkForFolder(string folder)
    {
        EnsureMarks();
        return _folderMarks!.TryGetValue(PathKey(folder), out var mark) ? mark : null;
    }

    /// <summary>Alle nicht unterdrückten Einträge einer Datei, jede Zuordnung (Notizbereich, Plan-2b-Ruling 4).</summary>
    public IReadOnlyList<ErrorEntry> EntriesForFile(string path)
    {
        var key = PathKey(path);
        return _entries
            .Where(e => AttributionOf(e).Source is { } source
                && string.Equals(PathKey(source.FullPath), key, StringComparison.OrdinalIgnoreCase)
                && !IsSuppressed(e))
            .ToList();
    }

    public static ErrorCounts CountsOf(IReadOnlyList<ErrorSection> sections)
    {
        var active = sections
            .Where(s => s.Kind is not (ErrorSectionKind.Fixed or ErrorSectionKind.Suppressed))
            .SelectMany(s => s.Groups)
            .ToList();
        return new ErrorCounts(
            active.Sum(g => g.Count),
            active.Count,
            active.Count(g => g.Status == RunStatus.New),
            sections.Where(s => s.Kind == ErrorSectionKind.Fixed).Sum(s => s.Groups.Count));
    }

    private ErrorGroupView View(ErrorGroup group)
    {
        var previous = group.Entries
            .Select(RunComparison.KeyOf)
            .Distinct(StringComparer.Ordinal)
            .Sum(key => _diff.ByKey.TryGetValue(key, out var diff) ? diff.PreviousCount : 0);
        var status = HasComparison && group.Entries.All(e => _diff.StatusOf(e) == RunStatus.New) ? RunStatus.New : RunStatus.Unchanged;
        return new ErrorGroupView(
            group.Key,
            group.Code,
            group.GroupName,
            group.Kind,
            group.Entries.Min(e => e.Severity),
            status,
            group.Entries.Sum(e => e.Lines.Count),
            previous,
            group.Entries,
            AttributionOf(group.Entries[0]));
    }

    private bool Matches(ErrorEntry entry, ErrorFilter filter)
    {
        var severity = entry.Severity switch
        {
            ErrorSeverity.Error => filter.Errors,
            ErrorSeverity.Warning => filter.Warnings,
            _ => filter.Infos,
        };
        if (!severity || (filter.Code is { } code && entry.Code != code))
        {
            return false;
        }

        var search = filter.Search.Trim();
        return search.Length == 0
            || Contains(entry.Message)
            || Contains(entry.Name)
            || Contains(entry.FilePath)
            || Contains(entry.Code)
            || Contains(entry.Kind?.NameIn(Language));

        bool Contains(string? text) => text?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>Pfadfilter: gleiche Datei oder Datei unter dem Ordner; der Vergleich endet an Trennern.</summary>
    private bool UnderPath(ErrorEntry entry, string? path)
    {
        if (path is null)
        {
            return true;
        }

        if (AttributionOf(entry).Source is not { } source)
        {
            return false;
        }

        var key = PathKey(source.FullPath);
        var filter = PathKey(path);
        return key.Equals(filter, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(filter + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureMarks()
    {
        if (_fileMarks is not null && _folderMarks is not null)
        {
            return;
        }

        var files = new Dictionary<string, ErrorFileMark>(StringComparer.OrdinalIgnoreCase);
        var folders = new Dictionary<string, ErrorFileMark>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _entries)
        {
            var attribution = AttributionOf(entry);
            if (attribution.Bucket != ErrorBucket.OwnMod || attribution.Source is null || IsSuppressed(entry))
            {
                continue;
            }

            var key = PathKey(attribution.Source.FullPath);
            AddMark(files, key, entry);
            for (var folder = System.IO.Path.GetDirectoryName(key); !string.IsNullOrEmpty(folder); folder = System.IO.Path.GetDirectoryName(folder))
            {
                AddMark(folders, folder.TrimEnd('\\', '/'), entry);
            }
        }

        _fileMarks = files;
        _folderMarks = folders;
    }

    private static void AddMark(Dictionary<string, ErrorFileMark> marks, string key, ErrorEntry entry) =>
        marks[key] = marks.TryGetValue(key, out var mark)
            ? new ErrorFileMark(mark.Count + entry.Lines.Count, mark.Severity < entry.Severity ? mark.Severity : entry.Severity)
            : new ErrorFileMark(entry.Lines.Count, entry.Severity);

    private static bool StatusVisible(RunStatus status, ErrorFilter filter) => status switch
    {
        RunStatus.New => filter.New,
        RunStatus.Unchanged => filter.Unchanged,
        _ => filter.Fixed,
    };

    private static ErrorSectionKind SectionOf(ErrorBucket bucket) => bucket switch
    {
        ErrorBucket.OwnMod => ErrorSectionKind.OwnMod,
        ErrorBucket.OtherMod => ErrorSectionKind.OtherMod,
        ErrorBucket.Vanilla => ErrorSectionKind.Vanilla,
        ErrorBucket.Engine => ErrorSectionKind.Engine,
        ErrorBucket.Unassigned => ErrorSectionKind.Unassigned,
        _ => ErrorSectionKind.Unmatched,
    };

    /// <summary>Wie <see cref="ErrorGrouping"/>: Name, sonst Datei aus dem Log, bei nicht zugeordneten Zeilen die Meldung.</summary>
    private static string GroupNameOf(ErrorEntry entry) => entry.Name ?? entry.FilePath ?? (entry.Kind is null ? entry.Message : string.Empty);

    private void ResetAttribution()
    {
        _attributions.Clear();
        _resolver = CreateResolver();
    }

    private SourceResolver CreateResolver() => new(Mods, VanillaRoots, RunTimeUtc);

    private void Compare() => _diff = RunComparison.Compare(_entries, _comparison);

    private void Raise()
    {
        _fileMarks = null;
        _folderMarks = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        if (_suppressionsPath is not null)
        {
            Suppressions.Save(_suppressionsPath);
        }
    }
}
