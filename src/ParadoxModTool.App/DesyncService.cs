using System.IO;
using ParadoxModTool.Desync;
using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.App;

/// <summary>Ein vom Nutzer hinzugefügter Pfad. <paramref name="Problem"/>: Anzeigetext, wenn der Pfad vorhanden ist, aber keinen Dump liefert.</summary>
public sealed record DesyncAddedSource(string Path, bool Exists, string? Problem);

/// <summary>Anzeigedaten eines Paares: Schreibzeit der Metadaten (Ortszeit) und Ordnername des Dumps (<c>&lt;id&gt;_0</c>).</summary>
public sealed record DesyncPairInfo(DateTime? Written, string? FolderName);

/// <summary>Wie ein bekanntes Ergebnis zustande kam: aus dem Zwischenspeicher (mit dessen Schreibzeit) und ob das Speichern scheiterte.</summary>
public sealed record DesyncResultNote(DateTime? CachedAt, string? CacheSaveError);

/// <summary>
/// Die Desync-Analyse eines Spiels über das Fenster hinaus (Spec, App): Dump-Katalog aus dem <c>oos</c>-Ordner und den hinzugefügten Pfaden,
/// Zwischenspeicher der Vergleiche unter <c>&lt;GameDirectory&gt;/desync/</c> und die in dieser Sitzung bekannten Ergebnisse. Alle öffentlichen
/// Mitglieder laufen im UI-Thread; Lesen der Dumps und Vergleiche laufen im Hintergrund und kommen per <c>await</c> dorthin zurück.
/// Bekannte Ergebnisse gelten je OOS-Id und Stempel der beiden Spielstände: Ein ausgetauschter Dump mit derselben Id zählt als neues Paar.
/// </summary>
public sealed class DesyncService : IDisposable
{
    private readonly ErrorCatalog _catalog;
    private readonly string _oosFolder;
    private readonly string _cacheDirectory;
    private readonly DesyncSourcesStore _store;
    private readonly DesyncCache _cache;
    private readonly Dictionary<string, DesyncPairInfo> _pairInfos = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Id, string Stamp), (DesyncDiffResult Result, DesyncResultNote Note)> _results = new();
    // Zuletzt gesehener Stempel je OOS-Id; ein neuer Katalog verwirft ihn, bis LoadCachedAsync oder CompareAsync ihn neu bestimmt.
    private readonly Dictionary<string, string> _currentStamps = new(StringComparer.Ordinal);
    // Laufende Vergleiche je OOS-Id: ein zweiter Vergleich desselben Paares wartet, bis der erste beendet ist (Speicherrahmen).
    private readonly Dictionary<string, Task<DesyncDiffResult>> _comparisons = new(StringComparer.Ordinal);
    private int _refreshGeneration;
    private bool _disposed;

    /// <param name="oosFolder">Der <c>oos</c>-Ordner des Spiels (<see cref="OosLocations.FolderFor"/>); leer, wenn er unbekannt ist.</param>
    /// <param name="gameDirectory">PMT-Datenordner des Spiels (<c>GameContext.Repository.GameDirectory</c>).</param>
    public DesyncService(DesyncProfile profile, ErrorCatalog catalog, string oosFolder, string gameDirectory)
    {
        Profile = profile;
        _catalog = catalog;
        _oosFolder = oosFolder;
        var desyncDirectory = Path.Combine(gameDirectory, "desync");
        _store = new DesyncSourcesStore(Path.Combine(desyncDirectory, "sources.json"));
        _cacheDirectory = Path.Combine(desyncDirectory, "cache");
        _cache = new DesyncCache(_cacheDirectory);
        VerdictsPath = Path.Combine(desyncDirectory, "safety-verdicts.json");
        Verdicts = new SafetyVerdicts(VerdictsPath);
        LoadVerdicts();
    }

    /// <summary>Die Urteile des Nutzers zur MP-Prüfung dieses Spiels (<c>&lt;GameDirectory&gt;/desync/safety-verdicts.json</c>).</summary>
    public SafetyVerdicts Verdicts { get; }

    /// <summary>Pfad der Datei der Urteile; eine beschädigte Datei liegt danach als <c>&lt;Pfad&gt;.bak</c> daneben.</summary>
    public string VerdictsPath { get; }

    /// <summary>Die Datei der Urteile war beschädigt und wurde als <c>.bak</c> gesichert; es gibt keine Urteile.</summary>
    public bool VerdictsBroken { get; private set; }

    /// <summary>
    /// Meldung des Systems, wenn sich die Datei der Urteile nicht lesen (oder nicht als <c>.bak</c> sichern) ließ; der Stand ist dann leer
    /// und darf die Datei nicht überschreiben. <c>null</c>, wenn das Lesen klappte.
    /// </summary>
    public string? VerdictsLoadError { get; private set; }

    /// <summary>
    /// Urteile wurden im Speicher geändert, aber wegen <see cref="VerdictsLoadError"/> nicht gespeichert; ein erneutes <see cref="LoadVerdicts"/>
    /// würde sie verwerfen und unterbleibt dann.
    /// </summary>
    public bool VerdictsUnsaved { get; set; }

    /// <summary>Liest die Urteile (neu); Lesefehler werden zu <see cref="VerdictsLoadError"/>, das Öffnen des Fensters scheitert nie daran.</summary>
    public void LoadVerdicts()
    {
        try
        {
            VerdictsBroken = !Verdicts.Load();
            VerdictsLoadError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            VerdictsLoadError = ex.Message;
        }
    }

    /// <summary>Katalog, Hinweise oder hinzugefügte Pfade haben sich geändert.</summary>
    public event EventHandler? Changed;

    public DesyncProfile Profile { get; }

    /// <summary>Die Sitzungen mit ihren Paaren, neueste zuerst.</summary>
    public IReadOnlyList<OosSession> Sessions { get; private set; } = Array.Empty<OosSession>();

    /// <summary>Die hinzugefügten Pfade in der Reihenfolge des Hinzufügens.</summary>
    public IReadOnlyList<DesyncAddedSource> AddedSources { get; private set; } = Array.Empty<DesyncAddedSource>();

    /// <summary>Anzeigetexte zu nicht lesbaren Dumps und Pfaden und zum fehlenden <c>oos</c>-Ordner.</summary>
    public IReadOnlyList<string> Problems { get; private set; } = Array.Empty<string>();

    /// <summary>Ob gerade ein Katalog im Hintergrund gelesen wird.</summary>
    public bool IsRefreshing { get; private set; }

    /// <summary>Liest den Katalog im Hintergrund neu und meldet <see cref="Changed"/>; ein späterer Aufruf verwirft das Ergebnis eines früheren.</summary>
    public void Refresh() => _ = RefreshAsync();

    /// <summary>Endet immer: auch ein unerwarteter Lesefehler setzt <see cref="IsRefreshing"/> zurück und erscheint als Hinweis.</summary>
    private async Task RefreshAsync()
    {
        var generation = ++_refreshGeneration;
        IsRefreshing = true;
        RaiseChanged();
        ScanResult scan;
        try
        {
            scan = await Task.Run(ScanAll);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            // ScanAll fängt Fehler je Pfad ab; das hier ist nur das Netz darunter. Der bisherige Katalog bleibt stehen.
            scan = new ScanResult(
                Sessions,
                _store.Load().Select(path => new DesyncAddedSource(path, Exists(path), null)).ToList(),
                new[] { DesyncTexts.DumpError(_oosFolder.Length > 0 ? _oosFolder : _store.Load().FirstOrDefault() ?? "oos", ex.Message) },
                _pairInfos.Select(pair => (pair.Key, pair.Value)).ToList());
        }

        if (_disposed || generation != _refreshGeneration)
        {
            return;
        }

        Sessions = scan.Sessions;
        AddedSources = scan.Added;
        Problems = scan.Problems;
        _pairInfos.Clear();
        foreach (var (id, info) in scan.PairInfos)
        {
            _pairInfos[id] = info;
        }

        // Dateien können sich geändert haben: bekannte Ergebnisse gelten erst wieder, wenn ihr Stempel neu bestätigt ist.
        _currentStamps.Clear();
        IsRefreshing = false;
        RaiseChanged();
    }

    /// <summary>Merkt den Pfad in <c>sources.json</c> und liest den Katalog neu.</summary>
    /// <exception cref="IOException">Die Datei lässt sich nicht lesen oder schreiben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Lese- oder Schreibrecht.</exception>
    public void AddPath(string path)
    {
        _store.Add(path);
        Refresh();
    }

    /// <summary>Entfernt den Pfad aus <c>sources.json</c> und liest den Katalog neu.</summary>
    /// <exception cref="IOException">Die Datei lässt sich nicht lesen oder schreiben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Lese- oder Schreibrecht.</exception>
    public void RemovePath(string path)
    {
        _store.Remove(path);
        Refresh();
    }

    /// <summary>Schreibzeit und Ordnername des Paares aus dem letzten Katalog.</summary>
    public DesyncPairInfo InfoOf(OosPair pair) => _pairInfos.GetValueOrDefault(pair.OosId16) ?? new DesyncPairInfo(null, null);

    /// <summary>Übersichtskarte des Paares. Liest Metadaten, Kopf der Spielstände und Logs; im Hintergrund aufrufen.</summary>
    public OosSummary SummaryOf(OosPair pair)
    {
        var dumps = OpenAll(pair);
        try
        {
            return OosSummaryBuilder.Build(pair, dumps);
        }
        finally
        {
            DisposeAll(dumps);
        }
    }

    /// <summary>Zeitleiste der Logs vor dem OOS über alle Maschinen des Paares; im Hintergrund aufrufen.</summary>
    /// <exception cref="OosDumpException">Ein Log ist nicht lesbar.</exception>
    public IReadOnlyList<TimelineEntry> TimelineOf(OosPair pair, int gameDays)
    {
        var dumps = OpenAll(pair);
        try
        {
            return OosTimeline.Build(dumps, _catalog, gameDays);
        }
        finally
        {
            DisposeAll(dumps);
        }
    }

    /// <summary>
    /// Zustandsvergleich des Paares. Prüft zuerst die bekannten Ergebnisse und den Zwischenspeicher; sonst läuft der Vergleich im Hintergrund
    /// und das Ergebnis wird gespeichert. Scheitert das Speichern, steht der Fehler in <see cref="NoteOf"/>; das Ergebnis gilt trotzdem.
    /// Läuft für dasselbe Paar schon ein Vergleich (etwa aus einem inzwischen geschlossenen Fenster), wartet dieser Aufruf auf dessen Ende.
    /// </summary>
    /// <exception cref="InvalidOperationException">Dem Paar fehlt eine Seite.</exception>
    /// <exception cref="OperationCanceledException">Abgebrochen.</exception>
    /// <exception cref="OosDumpException">Ein Spielstand fehlt oder ist nicht lesbar.</exception>
    public async Task<DesyncDiffResult> CompareAsync(OosPair pair, IProgress<DesyncProgress> progress, CancellationToken cancellation)
    {
        var (reference, desynced) = SidesOf(pair);
        // Ein beendeter Lauf kann noch im Wörterbuch stehen, bis sein Aufrufer weiterläuft; auf ihn wird nicht gewartet (sonst kreist die Schleife).
        while (_comparisons.TryGetValue(pair.OosId16, out var previous) && !previous.IsCompleted)
        {
            try
            {
                await previous.WaitAsync(cancellation);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
            {
                // Ausgang des früheren Laufs gehört seinem Aufrufer; hier zählt nur, dass er beendet ist.
            }
        }

        var run = Task.Run(() => Compare(pair, reference, desynced, progress, cancellation), cancellation);
        var task = Finish(run);
        _comparisons[pair.OosId16] = task;
        try
        {
            return await task;
        }
        finally
        {
            if (_comparisons.TryGetValue(pair.OosId16, out var current) && ReferenceEquals(current, task))
            {
                _comparisons.Remove(pair.OosId16);
            }
        }

        // Ergebnis im UI-Thread merken, bevor ein wartender zweiter Aufruf weiterläuft.
        async Task<DesyncDiffResult> Finish(Task<CompareOutcome> work)
        {
            var outcome = await work;
            Remember(pair.OosId16, outcome.Stamp, outcome.Result!, outcome.Note!);
            return outcome.Result!;
        }
    }

    private CompareOutcome Compare(OosPair pair, OosDumpInfo reference, OosDumpInfo desynced, IProgress<DesyncProgress> progress,
        CancellationToken cancellation)
    {
        using var referenceDump = reference.Open();
        using var desyncedDump = desynced.Open();
        var stamp = DesyncCache.StampOf(referenceDump, desyncedDump, Profile);
        if (Known(pair.OosId16, stamp) is { } known)
        {
            return new CompareOutcome(stamp, known.Result, known.Note);
        }

        if (_cache.TryLoad(pair.OosId16, stamp) is { } cached)
        {
            return new CompareOutcome(stamp, cached, new DesyncResultNote(CacheFileTime(pair.OosId16), null));
        }

        var computed = DesyncComparison.Run(referenceDump, desyncedDump, Profile, progress, cancellation);
        try
        {
            _cache.Save(computed, stamp);
            return new CompareOutcome(stamp, computed, new DesyncResultNote(null, null));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new CompareOutcome(stamp, computed, new DesyncResultNote(null, ex.Message));
        }
    }

    /// <summary>
    /// Das bekannte oder gespeicherte Ergebnis des Paares, ohne zu vergleichen; null ohne passenden Eintrag, ohne beide Seiten oder wenn ein
    /// Dump nicht lesbar ist (das zeigen Katalog und Vergleich).
    /// </summary>
    public async Task<DesyncDiffResult?> LoadCachedAsync(OosPair pair)
    {
        if (pair.Reference is not { } reference || pair.Desynced is not { } desynced)
        {
            return null;
        }

        var loaded = await Task.Run(() =>
        {
            try
            {
                using var referenceDump = reference.Open();
                using var desyncedDump = desynced.Open();
                var stamp = DesyncCache.StampOf(referenceDump, desyncedDump, Profile);
                if (Known(pair.OosId16, stamp) is { } known)
                {
                    return new CompareOutcome(stamp, known.Result, known.Note);
                }

                return _cache.TryLoad(pair.OosId16, stamp) is { } cached
                    ? new CompareOutcome(stamp, cached, new DesyncResultNote(CacheFileTime(pair.OosId16), null))
                    : new CompareOutcome(stamp, null, null);
            }
            catch (Exception ex) when (ex is OosDumpException || IsReadFailure(ex))
            {
                return null;
            }
        });

        if (loaded is null || _disposed)
        {
            return null;
        }

        _currentStamps[pair.OosId16] = loaded.Stamp;
        if (loaded.Result is null)
        {
            return null;
        }

        Remember(pair.OosId16, loaded.Stamp, loaded.Result, loaded.Note!);
        return loaded.Result;
    }

    /// <summary>Das bekannte Ergebnis des Paares zum zuletzt bestätigten Stempel (verglichen oder aus dem Zwischenspeicher geladen).</summary>
    public DesyncDiffResult? ResultOf(OosPair pair) => Current(pair)?.Result;

    public DesyncResultNote? NoteOf(OosPair pair) => Current(pair)?.Note;

    public void Dispose() => _disposed = true;

    private (DesyncDiffResult Result, DesyncResultNote Note)? Current(OosPair pair) =>
        _currentStamps.TryGetValue(pair.OosId16, out var stamp) && _results.TryGetValue((pair.OosId16, stamp), out var entry) ? entry : null;

    /// <summary>Bekanntes Ergebnis zu Id und Stempel; liest das Wörterbuch auch aus dem Hintergrund (geschrieben wird nur im UI-Thread).</summary>
    private (DesyncDiffResult Result, DesyncResultNote Note)? Known(string oosId16, string stamp)
    {
        lock (_results)
        {
            return _results.TryGetValue((oosId16, stamp), out var entry) ? entry : null;
        }
    }

    private void Remember(string oosId16, string stamp, DesyncDiffResult result, DesyncResultNote note)
    {
        lock (_results)
        {
            _results[(oosId16, stamp)] = (result, note);
        }

        _currentStamps[oosId16] = stamp;
    }

    private static (OosDumpInfo Reference, OosDumpInfo Desynced) SidesOf(OosPair pair) =>
        pair.Reference is { } reference && pair.Desynced is { } desynced
            ? (reference, desynced)
            : throw new InvalidOperationException("Für den Zustandsvergleich fehlt eine Seite des Paares " + pair.OosId16 + ".");

    /// <summary>Schreibzeit der Zwischenspeicher-Datei (Ortszeit), Name wie in <see cref="DesyncCache"/>.</summary>
    private DateTime? CacheFileTime(string oosId16)
    {
        var path = Path.Combine(_cacheDirectory, oosId16 + ".json");
        return File.Exists(path) ? File.GetLastWriteTime(path) : null;
    }

    private void RaiseChanged()
    {
        if (!_disposed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Lesefehler des Dateisystems oder eines Zip-Eintrags.</summary>
    private static bool IsReadFailure(Exception ex) =>
        ex is OosDumpException or IOException or UnauthorizedAccessException or InvalidDataException;

    /// <summary>
    /// Öffnet die Dumps des Paares; nicht lesbare werden übersprungen (der Katalog zeigt ihren Fehler). Wirft trotzdem etwas Unerwartetes,
    /// werden die schon geöffneten wieder geschlossen.
    /// </summary>
    private static List<OosDump> OpenAll(OosPair pair)
    {
        var dumps = new List<OosDump>();
        try
        {
            foreach (var info in pair.Dumps)
            {
                try
                {
                    dumps.Add(info.Open());
                }
                catch (Exception ex) when (IsReadFailure(ex))
                {
                }
            }
        }
        catch
        {
            DisposeAll(dumps);
            throw;
        }

        return dumps;
    }

    private static void DisposeAll(IEnumerable<OosDump> dumps)
    {
        foreach (var dump in dumps)
        {
            dump.Dispose();
        }
    }

    /// <summary>Ergebnis eines Laufs im Hintergrund; <paramref name="Result"/> null heißt "nichts gespeichert" (nur beim Laden).</summary>
    private sealed record CompareOutcome(string Stamp, DesyncDiffResult? Result, DesyncResultNote? Note);

    private sealed record ScanResult(
        IReadOnlyList<OosSession> Sessions,
        IReadOnlyList<DesyncAddedSource> Added,
        IReadOnlyList<string> Problems,
        IReadOnlyList<(string Id, DesyncPairInfo Info)> PairInfos);

    /// <summary>Hintergrundteil von <see cref="Refresh"/>: Katalog, Paare, Sitzungen, Anzeigedaten und Hinweise.</summary>
    private ScanResult ScanAll()
    {
        var addedPaths = _store.Load();
        var roots = _oosFolder.Length > 0 ? new[] { _oosFolder } : Array.Empty<string>();
        var problems = new List<string>();
        if (_oosFolder.Length > 0 && !Directory.Exists(_oosFolder))
        {
            problems.Add(DesyncTexts.NoOosFolder(_oosFolder));
        }

        var dumps = ScanSkippingBrokenPaths(roots, addedPaths, problems);
        var pairs = OosDumpCatalog.Pair(dumps);
        var sessions = OosDumpCatalog.Group(pairs);
        problems.AddRange(dumps.Where(d => d.Error is not null).Select(d => DesyncTexts.DumpError(d.Path, d.Error!)));

        var added = addedPaths
            .Select(path => Exists(path) ? new DesyncAddedSource(path, true, ProblemOf(path, dumps)) : new DesyncAddedSource(path, false, null))
            .ToList();

        var infos = pairs.Select(pair => (pair.OosId16, InfoFor(pair))).ToList();
        return new ScanResult(sessions, added, problems, infos);
    }

    /// <summary>
    /// Durchsucht alle Pfade. Bricht ein Pfad den Katalog mit einem Lesefehler ab (Ordner verschwindet, gesperrt), wird jeder Pfad einzeln
    /// geprüft: die fehlerhaften erscheinen mit ihrem Fehler unter den Hinweisen, die übrigen werden ohne sie neu durchsucht.
    /// </summary>
    private static IReadOnlyList<OosDumpInfo> ScanSkippingBrokenPaths(IReadOnlyList<string> roots, IReadOnlyList<string> added, List<string> problems)
    {
        try
        {
            return OosDumpCatalog.Scan(roots, added);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            var broken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in roots.Concat(added))
            {
                try
                {
                    OosDumpCatalog.Scan([path], []);
                }
                catch (Exception pathError) when (IsReadFailure(pathError))
                {
                    if (broken.Add(path))
                    {
                        problems.Add(DesyncTexts.DumpError(path, pathError.Message));
                    }
                }
            }

            try
            {
                return OosDumpCatalog.Scan(roots.Where(p => !broken.Contains(p)), added.Where(p => !broken.Contains(p)));
            }
            catch (Exception again) when (IsReadFailure(again))
            {
                // Der Fehler hängt an keinem einzelnen Pfad (oder kam erst jetzt): ohne Katalog, mit der Meldung.
                problems.Add(DesyncTexts.DumpError(roots.Concat(added).FirstOrDefault() ?? "oos", again.Message));
                return Array.Empty<OosDumpInfo>();
            }
        }
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>
    /// Hinweis zu einem vorhandenen Pfad, aus dem der Katalog nichts übernommen hat. Liefert der Pfad allein Dumps (die dann schon über einen
    /// anderen Pfad bekannt sind) oder nur Fehlereinträge (die schon unter <see cref="Problems"/> stehen), gibt es keinen eigenen Hinweis;
    /// sonst nennt er den Grund, aus dem der Pfad kein Dump ist.
    /// </summary>
    private static string? ProblemOf(string path, IReadOnlyList<OosDumpInfo> dumps)
    {
        try
        {
            if (dumps.Any(d => IsUnder(d.Path, path)) || OosDumpCatalog.Scan([], [path]).Count > 0)
            {
                return null;
            }

            OosDump.Open(path).Dispose();
            return null;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return DesyncTexts.DumpError(path, ex.Message);
        }
    }

    private static bool IsUnder(string candidate, string root)
    {
        try
        {
            var full = Path.GetFullPath(candidate).TrimEnd('\\', '/');
            var rootFull = Path.GetFullPath(root).TrimEnd('\\', '/');
            return string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>Schreibzeit der Metadaten und Ordnername aus dem Referenz-Dump (sonst einem anderen des Paares).</summary>
    private static DesyncPairInfo InfoFor(OosPair pair)
    {
        var info = pair.Reference ?? pair.Desynced ?? pair.Dumps.FirstOrDefault();
        if (info is null)
        {
            return new DesyncPairInfo(null, null);
        }

        try
        {
            using var dump = info.Open();
            return new DesyncPairInfo(dump.StampOf(DumpFileKind.Metadata).LastWriteUtc.ToLocalTime(), dump.Metadata.FolderName);
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            return new DesyncPairInfo(null, null);
        }
    }
}
