using System.IO;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Localization;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using ParadoxModTool.SaveGames;
using ParadoxModTool.SteamIntegration;

namespace ParadoxModTool.App;

/// <summary>
/// Was das Fehlerprotokoll vom Hauptfenster braucht (wie <see cref="ModCompatHost"/>). Register und Spielordner als Funktionen: Der Dienst
/// lebt ab dem PMT-Start und liest sie bei jedem Neuaufbau frisch (Plan-2b-Ruling 2).
/// </summary>
public sealed record ErrorLogHost(
    string GameName,
    Func<ModRegistry?> Registry,
    string MetadataFile,
    string? SteamAppId,
    string? ModRootTemplate,
    Func<string?> InstallDirectory,
    Func<string?> ContentRoot,
    Func<string?> PlaysetsFile,
    Func<string, EngineCommandKind?> EngineKindOf,
    Action<string, int> JumpTo,
    Func<string, int, bool> InspectGui,
    Func<string, int, string, string, string?> ReplaceName,
    Func<IReadOnlyList<ModRenamePlan>, string, string, FixOutcome> ApplyRename,
    Func<ModSource, FixOutcome> AddModToPlayset,
    Func<string, string> SnapshotDirectoryFor,
    bool HasSaveGames,
    Func<SaveSnapshot?> Save,
    Func<bool> LoadNewestSave,
    Action<string> ShowInSaveExplorer,
    Func<IReadOnlyList<ModRenamePlan>, string, string, FixOutcome> ApplyCommentOut,
    Func<IReadOnlyList<ModRenamePlan>, ModEditTexts, FixOutcome> ApplyTraceEdits,
    Func<bool> IsGameRunning,
    Func<string, string, bool> ShowSaveObject);

/// <summary>Texte für eine Änderung an Mod-Dateien (Aktionsverlauf, Sicherung, Meldungen), z.B. Scope-Spur einfügen oder entfernen.</summary>
public sealed record ModEditTexts(string Description, string SnapshotNote, string Nothing, Func<string, string> Failed, Func<int, int, string> Done);

/// <summary>Ergebnis einer Korrektur (Fehlerprotokoll Plan 3): Erfolg und die Meldung für den Nutzer.</summary>
public sealed record FixOutcome(bool Success, string Message);

/// <summary>
/// Die Fehlerprotokoll-Sitzung eines Spiels über das Fenster hinaus (Plan-Ruling 3): Logordner und Läufe, gewähltes Playset, Mod-Liste und
/// Symbolindex. Mod-Liste und Index entstehen im Hintergrund, der Fortschritt steht im Fenster (Ruling 4). Beim PMT-Start ohne Index; das
/// Fenster fordert ihn beim ersten Öffnen an (Plan-2b-Ruling 1). Alle öffentlichen Mitglieder laufen im UI-Thread; die Hintergrundarbeit
/// kommt per <c>await</c> dorthin zurück, bevor sie die Sitzung ändert.
/// </summary>
public sealed class ErrorLogService : IDisposable
{
    private CancellationTokenSource? _cancel;
    private int _build;
    private bool _building;
    private bool _indexWanted;
    private int _runIndex;
    private int? _comparisonIndex = 1;

    public ErrorLogService(ErrorLogHost host, ErrorCatalog catalog, bool hasCatalog, string suppressionsPath, string? logFolder)
    {
        Host = host;
        HasCatalog = hasCatalog;
        LogFolder = logFolder;
        Session = new ErrorLogSession(catalog, SuppressionSet.Load(suppressionsPath), suppressionsPath, UiText.Current.Language, host.EngineKindOf);
        Session.Changed += (_, _) => UpdateLiveCount();
    }

    /// <summary>Läufe, Playset, Fortschritt oder Hinweise haben sich geändert. Änderungen der Liste meldet <see cref="ErrorLogSession.Changed"/>.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Der Nutzer hat einen Logordner gewählt; das Hauptfenster merkt ihn sich (<c>AppSessionState.ErrorLogFolder</c>).</summary>
    public event EventHandler<string>? LogFolderChosen;

    /// <summary>Der Spielstand-Explorer hat einen anderen Stand geladen (Spielstand-Explorer Plan 2b); der Detailbereich baut sich neu auf.</summary>
    public event EventHandler? SaveChanged;

    /// <summary>Zählt jeden neu geladenen Spielstand; Teil des Schlüssels, an dem das Fenster erkennt, ob die Einzelheiten neu gebaut werden müssen.</summary>
    public int SaveVersion { get; private set; }

    public void NotifySaveChanged()
    {
        SaveVersion++;
        SaveChanged?.Invoke(this, EventArgs.Empty);
    }

    public ErrorLogHost Host { get; }

    public ErrorLogSession Session { get; }

    /// <summary>Das Spielprofil nennt einen Katalog (<c>errorLogRules</c>). Fehlt die Datei, meldet der Katalog selbst ein Problem.</summary>
    public bool HasCatalog { get; }

    public string? LogFolder { get; private set; }

    /// <summary>Die Spielläufe, neuester zuerst; ein Lauf kann mehrere Logdateien umfassen (das Spiel wechselt error.log mitten im Lauf).</summary>
    public IReadOnlyList<LogRun> Runs { get; private set; } = Array.Empty<LogRun>();

    /// <summary>Position des gewählten Laufs in <see cref="Runs"/>.</summary>
    public int CurrentRunPosition => _runIndex < Runs.Count ? _runIndex : 0;

    public LogRun? CurrentRun => Runs.Count == 0 ? null : Runs[CurrentRunPosition];

    /// <summary>Position des Vergleichslaufs in <see cref="Runs"/>; nie der gewählte Lauf selbst.</summary>
    public int? ComparisonRunPosition =>
        _comparisonIndex is int index && index != CurrentRunPosition && index < Runs.Count ? index : null;

    public LogRun? ComparisonRun => ComparisonRunPosition is int index ? Runs[index] : null;

    public IReadOnlyList<string> Playsets { get; private set; } = Array.Empty<string>();

    public string? PlaysetName { get; private set; }

    /// <summary>Fortschritt des laufenden Aufbaus von Mod-Liste und Index; <see langword="null"/>, wenn keiner läuft.</summary>
    public IndexProgress? IndexState { get; private set; }

    /// <summary>Hinweis nach abgebrochenem oder fehlgeschlagenem Indexaufbau.</summary>
    public string? IndexNotice { get; private set; }

    public string? LoadError { get; private set; }

    /// <summary>Der Knopf "Ordner wählen" erscheint: Logordner unbekannt oder ohne <c>error.log</c>.</summary>
    public bool NeedsFolder => LogFolder is null || Runs.Count == 0;

    /// <summary>Filter des Fensters; bestimmt, was "+N" zählt (Plan-2b-Ruling 10). Standard: alles.</summary>
    public ErrorFilter Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            UpdateLiveCount();
        }
    }

    /// <summary>Beim PMT-Start: Playsets, Läufe und Mod-Liste, noch ohne Symbolindex (Plan-2b-Ruling 1).</summary>
    public void Start()
    {
        LoadPlaysets();
        LoadRuns();
        _ = RebuildSourcesAsync();
    }

    /// <summary>Das Fenster öffnet: Ab jetzt gehört der Symbolindex zu jedem Neuaufbau der Quellen.</summary>
    public void WantIndex()
    {
        if (_indexWanted)
        {
            return;
        }

        _indexWanted = true;
        if (!Session.IndexReady)
        {
            _ = RebuildSourcesAsync();
        }
    }

    /// <summary>Spielpfad, Playset-Datei oder Mod-Register haben sich geändert (Plan-2b-Ruling 2).</summary>
    public void RefreshSources()
    {
        LoadPlaysets();
        _ = RebuildSourcesAsync();
    }

    public void SetLogFolder(string folder)
    {
        StopLive();
        LogFolder = folder;
        LogFolderChosen?.Invoke(this, folder);
        LoadRuns();
    }

    public void SelectRun(int index)
    {
        if (LiveEnabled && index == 0)
        {
            return;
        }

        // Plan-2b-Ruling 8: Live gilt nur für den Lauf mit error.log (Position 0); ein anderer Lauf beendet es.
        StopLive();
        _runIndex = index;
        LoadRuns();
    }

    public void SelectComparison(int? index)
    {
        _comparisonIndex = index;
        LoadComparison();
        RaiseStateChanged();
    }

    /// <summary>Anderes Playset: Die Ladereihenfolge steckt in Zuordnung und Index, beides wird neu aufgebaut.</summary>
    public void SelectPlayset(string? name)
    {
        if (name == PlaysetName)
        {
            return;
        }

        PlaysetName = name;
        _ = RebuildSourcesAsync();
    }

    /// <summary>Plan-Ruling 9: Logdateien neu lesen, Mod-Liste und Index neu aufbauen.</summary>
    public void Reload()
    {
        if (LiveEnabled)
        {
            SetLive(true);
        }
        else
        {
            LoadRuns();
        }

        _ = RebuildSourcesAsync();
    }

    public void CancelIndex() => _cancel?.Cancel();

    /// <summary>Hinweise für die Leiste im Fenster (Spec, Fehlerbehandlung).</summary>
    public IReadOnlyList<string> Notices()
    {
        var notices = new List<string>();
        if (!HasCatalog)
        {
            notices.Add(ErrorLogTexts.NoCatalog);
        }

        notices.AddRange(Session.Catalog.Problems.Select(ErrorLogTexts.CatalogProblemText));
        if (LogFolder is null)
        {
            notices.Add(ErrorLogTexts.NoLogFolder);
        }
        else if (Runs.Count == 0)
        {
            notices.Add(ErrorLogTexts.NoLogFound(LogFolder));
        }

        if (LoadError is not null)
        {
            notices.Add(ErrorLogTexts.LoadFailed(LoadError));
        }

        if (PlaysetName is null)
        {
            notices.Add(ErrorLogTexts.PlaysetUnknownNotice);
        }

        if (!_building && Session.VanillaRoots.Count == 0)
        {
            notices.Add(ErrorLogTexts.GameFolderUnknown);
        }

        if (IndexNotice is not null)
        {
            notices.Add(IndexNotice);
        }

        if (LiveNotice is not null)
        {
            notices.Add(LiveNotice);
        }

        if (Session.Traces.Count > 0 && (ErrorLogLocations.DebugLogPath(LogFolder) is not { } debugLog || !File.Exists(debugLog)))
        {
            notices.Add(ErrorLogTexts.DebugLogMissing);
        }

        return notices;
    }

    /// <summary>Beim Beenden von PMT: laufenden Aufbau abbrechen.</summary>
    public void Dispose()
    {
        _cancel?.Cancel();
        StopWatcher();
    }

    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private ErrorFilter _filter = ErrorFilter.All;
    private LiveLogWatcher? _watcher;

    /// <summary>"+N" hat sich geändert (Menü, Fenstertitel).</summary>
    public event EventHandler? LiveCountChanged;

    public bool LiveEnabled => _watcher is not null;

    /// <summary>Sichtbare neue Meldungen seit dem letzten Ansehen (Plan-2b-Ruling 10).</summary>
    public int LiveNewCount { get; private set; }

    /// <summary>Hinweis nach einem erkannten Spielstart während Live (Plan-2b-Ruling 11).</summary>
    public string? LiveNotice { get; private set; }

    /// <summary>Live ein: liest error.log neu, merkt die Position und beobachtet ab dort (Plan-2b-Ruling 8). Aus: beendet die Beobachtung.</summary>
    public void SetLive(bool on)
    {
        StopWatcher();
        LiveNotice = null;
        if (!on)
        {
            Session.StopLive();
            UpdateLiveCount();
            RaiseStateChanged();
            return;
        }

        var path = LogFolder is null ? null : Path.Combine(LogFolder, ErrorLogLocations.RunFileName(0));
        if (path is null || !File.Exists(path))
        {
            RaiseStateChanged();
            return;
        }

        LogRun run;
        RunRead start;
        try
        {
            Runs = ErrorLogLocations.Runs(LogFolder);
            run = Runs.Count > 0 && Runs[0].IsCurrent ? Runs[0] : new LogRun(new[] { new LogRunFile(path, 0, File.GetLastWriteTimeUtc(path)) }, null, false);
            start = LogReader.ReadRun(run);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LoadError = ex.Message;
            RaiseStateChanged();
            return;
        }

        _runIndex = 0;
        LoadError = null;
        Session.LoadRun(path, start.Lines, run.Newest.LastWriteUtc, run.Files.Select(f => f.FileName).ToList());
        LoadComparison();
        Session.StartLive();
        LiveLogWatcher? watcher = null;
        watcher = new LiveLogWatcher(
            path,
            start.NewestNext,
            chunk => Post(() =>
            {
                // Portionen eines beendeten oder ersetzten Wächters verwerfen.
                if (ReferenceEquals(_watcher, watcher))
                {
                    OnLiveChunk(chunk);
                }
            }),
            error => Post(() =>
            {
                LoadError = error.Message;
                RaiseStateChanged();
            }),
            part: run.Files.Count - 1,
            runAnchorPath: ErrorLogLocations.DebugLogPath(LogFolder));
        _watcher = watcher;
        RestartDebugWatcher();
        UpdateLiveCount();
        RaiseStateChanged();
    }

    /// <summary>Der Nutzer hat die neuen Meldungen gesehen (Fenster aktiviert oder Klick auf "+N").</summary>
    public void MarkLiveSeen()
    {
        Session.MarkLiveSeen();
        UpdateLiveCount();
    }

    private void StopLive()
    {
        if (_watcher is not null)
        {
            SetLive(false);
        }
    }

    private void StopWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debugWatcher?.Dispose();
        _debugWatcher = null;
    }

    private LiveLogWatcher? _debugWatcher;
    private LogPosition? _debugEnd;
    private int _traceBuild;

    /// <summary>
    /// Scope-Spuren der eigenen Mods neu suchen (Spec Scope-Verfolgung, Teil 3). Gibt es welche, liest der Dienst debug.log (sonst nicht,
    /// das Log kann groß sein) und beobachtet es im Live-Betrieb weiter.
    /// </summary>
    public async Task RefreshTracesAsync()
    {
        var build = ++_traceBuild;
        var own = Session.Mods.Where(m => m.IsOwn).ToList();
        var rules = Session.Catalog.Trace;
        var debugLog = ErrorLogLocations.DebugLogPath(LogFolder);
        IReadOnlyList<ActiveTrace> traces;
        (IReadOnlyList<TraceHit> Hits, LogPosition End)? read;
        try
        {
            (traces, read) = await Task.Run(() =>
            {
                IReadOnlyList<ActiveTrace> found = own.SelectMany(m => TraceMarkers.Scan(m, rules)).ToList();
                (IReadOnlyList<TraceHit>, LogPosition)? hits = found.Count > 0 && debugLog is not null && File.Exists(debugLog)
                    ? DebugLogReader.ReadHits(debugLog, rules.DebugLogSource, rules.DebugScopeSource)
                    : null;
                return (found, hits);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        if (build != _traceBuild)
        {
            return;
        }

        _debugEnd = read?.End;
        Session.SetTraces(traces);
        Session.SetTraceHits(read?.Hits ?? Array.Empty<TraceHit>());
        RestartDebugWatcher();
        RaiseStateChanged();
    }

    /// <summary>Live und Spuren vorhanden: debug.log ab dem gelesenen Ende weiter beobachten; ein Neuanlegen (Spielstart) ersetzt die Treffer.</summary>
    private void RestartDebugWatcher()
    {
        _debugWatcher?.Dispose();
        _debugWatcher = null;
        if (!LiveEnabled || Session.Traces.Count == 0 || _debugEnd is not { } end || ErrorLogLocations.DebugLogPath(LogFolder) is not { } debugLog)
        {
            return;
        }

        LiveLogWatcher? watcher = null;
        watcher = new LiveLogWatcher(
            debugLog,
            end,
            chunk => Post(() =>
            {
                if (!ReferenceEquals(_debugWatcher, watcher))
                {
                    return;
                }

                var hits = DebugLogReader.Hits(chunk.Lines, Session.Catalog.Trace.DebugLogSource, Session.Catalog.Trace.DebugScopeSource);
                if (chunk.Restarted)
                {
                    Session.SetTraceHits(hits);
                }
                else
                {
                    Session.AddTraceHits(hits);
                }
            }));
        _debugWatcher = watcher;
    }

    private void OnLiveChunk(LogChunk chunk)
    {
        if (LogFolder is null)
        {
            return;
        }

        if (chunk.Restarted)
        {
            // Spielstart: Das Spiel hat error.log in error.1.log umbenannt; der bisherige Lauf wird Vergleichslauf (Spec, Live-Beobachtung).
            Runs = ErrorLogLocations.Runs(LogFolder);
            _comparisonIndex = 1;
            Session.LoadRun(Path.Combine(LogFolder, ErrorLogLocations.RunFileName(0)), chunk.Lines, DateTime.UtcNow, new[] { ErrorLogLocations.RunFileName(0) });
            LoadComparison();
            Session.StartLive(everythingIsNew: true);
            LiveNotice = ErrorLogTexts.LiveRestarted;
            RaiseStateChanged();
        }
        else if (chunk.Continued)
        {
            // Wechsel mitten im Lauf: gleicher Lauf, die bisherigen Dateien heißen jetzt eine Nummer höher.
            Session.AppendLines(chunk.Lines);
            Runs = ErrorLogLocations.Runs(LogFolder);
            Session.RenameRunFilesAfterSwitch();

            RaiseStateChanged();
        }
        else
        {
            Session.AppendLines(chunk.Lines);
        }

        UpdateLiveCount();
    }

    private void Post(Action action)
    {
        if (_context is null)
        {
            action();
        }
        else
        {
            _context.Post(_ => action(), null);
        }
    }

    private void UpdateLiveCount()
    {
        var count = Session.LiveNewCount(Filter);
        if (count == LiveNewCount)
        {
            return;
        }

        LiveNewCount = count;
        LiveCountChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Playsets neu lesen; das gewählte bleibt, wenn es noch existiert, sonst das aktive.</summary>
    private void LoadPlaysets()
    {
        var playsets = PlaysetStore.ListPlaysets(Host.PlaysetsFile());
        Playsets = playsets.Select(p => p.Name).ToList();
        if (PlaysetName is null || !Playsets.Contains(PlaysetName))
        {
            PlaysetName = playsets.FirstOrDefault(p => p.IsActive)?.Name ?? Playsets.FirstOrDefault();
        }
    }

    private void LoadRuns()
    {
        Runs = ErrorLogLocations.Runs(LogFolder);
        LoadError = null;
        try
        {
            if (CurrentRun is { } run)
            {
                Session.LoadRun(run);
            }
            else
            {
                Session.ClearRun();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LoadError = ex.Message;
            Session.ClearRun();
        }

        LoadComparison();
        RaiseStateChanged();
    }

    private void LoadComparison()
    {
        try
        {
            Session.LoadComparison(ComparisonRun);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LoadError = ex.Message;
            Session.LoadComparison((LogRun?)null);
        }
    }

    /// <summary>
    /// Baut Mod-Liste und (sobald das Fenster ihn angefordert hat) Symbolindex im Hintergrund. Ein neuer Aufruf (Playset-Wechsel, "Neu
    /// laden") bricht den laufenden ab; Ergebnisse und Fortschritt eines abgelösten Aufbaus werden verworfen (Zähler <c>_build</c>).
    /// </summary>
    private async Task RebuildSourcesAsync()
    {
        _cancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _cancel = cancel;
        var build = ++_build;
        _building = true;
        IndexNotice = null;
        IndexState = new IndexProgress(0, 0);
        RaiseStateChanged();

        var playset = PlaysetName is { } name
            ? PlaysetStore.ListPlaysetMods(Host.PlaysetsFile(), name).Select(m => new PlaysetEntry(m.Path, m.IsEnabled)).ToList()
            : null;
        var own = OwnMods();
        var installDirectory = Host.InstallDirectory();
        var contentRoot = Host.ContentRoot();
        try
        {
            var (mods, vanilla) = await Task.Run(
                () => (ErrorLogSources.BuildMods(own, playset, InstalledMods()), ErrorLogSources.VanillaRoots(installDirectory, contentRoot)),
                cancel.Token);
            if (build != _build)
            {
                return;
            }

            Session.SetSources(mods, vanilla);
            _ = RefreshTracesAsync();
            if (!_indexWanted)
            {
                return;
            }

            var progress = new Progress<IndexProgress>(value =>
            {
                if (build == _build && _building)
                {
                    IndexState = value;
                    RaiseStateChanged();
                }
            });
            var roots = ErrorLogSources.SymbolRoots(mods, vanilla);
            var index = await Task.Run(() => SymbolIndex.Build(roots, cancel.Token, progress), cancel.Token);
            if (build != _build)
            {
                return;
            }

            Session.SetIndex(index);
        }
        catch (OperationCanceledException)
        {
            if (build == _build)
            {
                IndexNotice = ErrorLogTexts.IndexCancelled;
            }
        }
        catch (Exception ex)
        {
            // Hintergrundarbeit: Jeder Fehler (unlesbare Datei, verweigerter Zugriff) erscheint als Hinweis statt unbeobachtet zu verschwinden.
            if (build == _build)
            {
                IndexNotice = ErrorLogTexts.IndexFailed((ex as AggregateException)?.InnerException?.Message ?? ex.Message);
            }
        }
        finally
        {
            if (build == _build)
            {
                _building = false;
                IndexState = null;
                RaiseStateChanged();
            }
        }
    }

    /// <summary>Ordner der eigenen Mods (im UI-Thread gelesen, das Register ist nicht threadsicher).</summary>
    private List<OwnModFolder> OwnMods() =>
        Host.Registry() is not { } registry
            ? new List<OwnModFolder>()
            : registry.Mods.SelectMany(m => m.Paths.Where(Directory.Exists).Select(p => new OwnModFolder(m.Name, p))).ToList();

    /// <summary>Installierte Mods aus dem lokalen Mod-Ordner und dem Workshop, mit Namen aus den Metadaten (im Hintergrund).</summary>
    private List<InstalledModFolder> InstalledMods()
    {
        var result = new List<InstalledModFolder>();
        if (ModRoot() is { } modRoot)
        {
            foreach (var folder in Directory.EnumerateDirectories(modRoot).Where(f => !Path.GetFileName(f).StartsWith('.')))
            {
                result.Add(new InstalledModFolder(NameOf(folder, CompatOrigin.Folder), folder));
            }
        }

        if (Host.SteamAppId is { Length: > 0 } appId && SteamFolders.Find() is { } steam)
        {
            foreach (var folder in CompatMembers.FindWorkshopFolders(appId, SteamFolders.LibraryFolders(steam)))
            {
                result.Add(new InstalledModFolder(NameOf(folder, CompatOrigin.Workshop), folder));
            }
        }

        return result;
    }

    private string? ModRoot()
    {
        if (string.IsNullOrWhiteSpace(Host.ModRootTemplate))
        {
            return null;
        }

        var path = Environment.ExpandEnvironmentVariables(Host.ModRootTemplate);
        return Directory.Exists(path) ? Path.GetFullPath(path) : null;
    }

    private string NameOf(string folder, CompatOrigin origin)
    {
        try
        {
            return CompatMembers.FromFolder(folder, origin, null, Host.MetadataFile).Name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return Path.GetFileName(folder);
        }
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
