using System.IO;
using System.Windows.Threading;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.LaunchControl;
using ParadoxModTool.Performance;
using ErrorCatalog = ParadoxModTool.ErrorLog.ErrorCatalog;
using SourceResolver = ParadoxModTool.ErrorLog.SourceResolver;

namespace ParadoxModTool.App;

/// <summary>
/// Dienst hinter Schritt 1 "Messung" des Performance-Checkers: hält die <see cref="PerformanceSession"/> der aktiven Mod, startet das Spiel für eine
/// Messung und beobachtet den Logs-Ordner, bis der Profiler-Dump fertig geschrieben ist (2 s Ruhe, <see cref="DumpSettleTracker"/>). Der erste fertige
/// Dump wird als Lauf übernommen (Kopie im PMT-Datenordner, das Original bleibt in <c>logs/</c>; das error.log der Sitzung wird mit in den Lauf
/// kopiert, <see cref="RunErrorLog"/>), danach endet das Warten. Alles läuft im UI-Thread:
/// Ereignisse des Dateiwächters kommen über den Dispatcher herein, ein Timer fragt jede Sekunde nach fertigen Dateien. Mit <see cref="Dispose"/>
/// (Schließen des Fensters) endet der Wächter.
/// </summary>
internal sealed class PerformanceService : IDisposable
{
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(2);

    private readonly PerformanceWindowHost _host;
    private readonly Dispatcher _dispatcher;
    private readonly PerformanceSettings _settings;
    private readonly PerformanceRunStore _store;
    private readonly DispatcherTimer _timer;
    private readonly IReadOnlyList<string> _vanillaRoots;
    private bool _modRead;
    private string? _modId;
    private string? _modKey;
    private FileSystemWatcher? _watcher;
    private DumpSettleTracker? _tracker;
    private PendingRun? _pending;
    private string? _retryPath;
    private string _status = string.Empty;
    private ErrorCatalog? _catalog;

    /// <summary>
    /// Nur auf dem UI-Thread und nur aus einer Nutzeraktion (Öffnen des Fensters): liest die Vanilla-Wurzeln einmal, dabei kann PMT nach der
    /// Spiel-Exe fragen. Das Ergebnis (auch "unbekannt") gilt für die Lebensdauer des Dienstes.
    /// </summary>
    public PerformanceService(PerformanceWindowHost host, Dispatcher dispatcher)
    {
        _host = host;
        _dispatcher = dispatcher;
        _settings = host.Profile.Performance ?? throw new ArgumentException("The game profile has no performance settings.", nameof(host));
        _store = new PerformanceRunStore(Path.Combine(host.GameDirectory, "performance"));
        _vanillaRoots = host.GameInstallRoot() is { } installRoot ? SourceResolver.VanillaRootsFor(installRoot) : Array.Empty<string>();
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => OnTick(), dispatcher);
        _timer.Stop();
        RefreshMod();
    }

    /// <summary>Meldet eine neue <see cref="Status"/>-Zeile; auch <see cref="Watching"/> ändert sich nur zusammen mit dem Status.</summary>
    public event EventHandler? StatusChanged;

    /// <summary>Die Sitzung wurde ersetzt (andere aktive Mod) oder hat sich geändert (<see cref="PerformanceSession.Changed"/>): alles neu lesen.</summary>
    public event EventHandler? SessionChanged;

    /// <summary>Ein Dump wurde als Lauf übernommen (Dateiwächter oder "Dump manuell wählen"); das Argument ist die Lauf-Id in <see cref="Session"/>.</summary>
    public event EventHandler<string>? RunImported;

    /// <summary>
    /// Läufe der aktiven Mod; <c>null</c> ohne aktive Mod (abweichend vom Plan nullable: ohne Mod-Ordner gibt es keinen Mod-Schlüssel).
    /// </summary>
    public PerformanceSession? Session { get; private set; }

    /// <summary>Ordner der Mod, zu der <see cref="Session"/> gehört; <c>null</c> ohne aktive Mod.</summary>
    public string? ModRoot { get; private set; }

    /// <summary>Der Dateiwächter wartet auf einen Dump.</summary>
    public bool Watching => _tracker is not null;

    /// <summary>Eine Zeile für das Fenster; leer, solange nichts zu melden ist.</summary>
    public string Status => _status;

    /// <summary>Speicher der Läufe; die KI-Analyse legt dort den Rat zum Lauf ab (<see cref="PerformanceRunStore.AdviceFileName"/>).</summary>
    public PerformanceRunStore Store => _store;

    /// <summary>Titel aus <c>continue_game.json</c>; <c>null</c>, wenn die Datei fehlt oder keinen nennt. Nur auf dem UI-Thread.</summary>
    public string? ContinueTitle() => _host.ContinueGamePath() is { } path ? ContinueGameFile.ReadTitle(path) : null;

    /// <summary>Messplan für diesen Spielstand (<c>null</c> = unbekannt) und diese Monatszahl mit dem aktuellen Zustand des Spiels. Nur auf dem UI-Thread.</summary>
    public MeasurementPlanResult Plan(string? saveName, int months) => PlanFor(saveName, months, _host.GameRunningInDebug());

    /// <summary>
    /// Liest die aktive Mod des Hauptfensters neu und baut die Sitzung neu, wenn es eine andere ist. Während des Wartens auf einen Dump bleibt
    /// alles, wie es ist (der Lauf gehört zur Mod beim Start). Nur auf dem UI-Thread.
    /// </summary>
    public void RefreshMod()
    {
        if (Watching)
        {
            return;
        }

        var root = _host.ActiveModRoot();
        var id = _host.ActiveModId();
        if (_modRead && string.Equals(root, ModRoot, StringComparison.OrdinalIgnoreCase) && string.Equals(id, _modId, StringComparison.Ordinal))
        {
            return;
        }

        _modRead = true;
        if (Session is { } old)
        {
            old.Changed -= OnSessionChanged;
        }

        ModRoot = root;
        _modId = id;
        _modKey = null;
        Session = null;
        if (root is not null)
        {
            try
            {
                var modKey = PerformanceRunStore.ModKeyFor(id, root);
                var others = _host.KnownMods().Select(m => m.Path).Where(p => !SamePath(p, root)).ToList();
                var vanilla = _vanillaRoots;
                Session = new PerformanceSession(_store, _host.GameId, modKey, _settings, () => new OriginClassifier(root, others, vanilla), Noise(), CatalogFactory());
                Session.Changed += OnSessionChanged;
                _modKey = modKey;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Session = null;
                SetStatus(PerformanceTexts.StatusActionFailed(ex.Message));
            }
        }

        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Startet eine Messung: baut den Plan, startet das Spiel über <see cref="PerformanceWindowHost.StartGame"/> (nicht, wenn es schon im
    /// Debug-Modus läuft) und wartet dann auf den Dump. <c>false</c> mit <see cref="Status"/>, wenn der Plan blockiert, der Logs-Ordner fehlt, das
    /// Spiel nicht startet oder der Wächter nicht anläuft. Nur auf dem UI-Thread.
    /// </summary>
    public bool StartMeasurement(string? playsetId, string? saveName, int months, string runName, string? playsetName)
    {
        StopWatchingCore();
        RefreshMod();
        if (Session is not { } session || ModRoot is not { } modRoot || _modKey is not { } modKey)
        {
            SetStatus(PerformanceTexts.NoActiveMod);
            return false;
        }

        var runningInDebug = _host.GameRunningInDebug();
        var plan = PlanFor(saveName, months, runningInDebug);
        if (plan.Blocker is { } blocker)
        {
            SetStatus(blocker);
            return false;
        }

        var folder = _host.LogFolder();
        if (folder is null || !Directory.Exists(folder))
        {
            SetStatus(PerformanceTexts.StatusLogFolderMissing(folder));
            return false;
        }

        // Nur wenn PMT das Spiel startet, lohnt der Eintrag in continue_game.json; ein schon laufendes Spiel liest die Datei nicht mehr.
        if (!runningInDebug)
        {
            plan = ApplyContinueTitle(plan, saveName, months, runningInDebug);
        }

        // Vor dem Start: Berührungen ab jetzt zählen, ältere Dumps im Ordner nicht.
        var tracker = new DumpSettleTracker(DateTime.UtcNow, SettleTime);
        var pending = new PendingRun(PerformanceSession.NewRunId(DateTime.UtcNow), session, modKey, modRoot, runName, saveName, months, playsetName, plan.Arguments, MeasuredNow: true, LogFolder: folder);
        if (!runningInDebug && !_host.StartGame(playsetId, plan.Arguments))
        {
            SetStatus(PerformanceTexts.StatusNotStarted);
            return false;
        }

        return StartWatching(folder, tracker, pending);
    }

    /// <summary>Beendet das Warten auf den Dump ("Warten abbrechen").</summary>
    public void StopWatching()
    {
        if (!Watching)
        {
            return;
        }

        StopWatchingCore();
        SetStatus(PerformanceTexts.StatusStopped);
    }

    /// <summary>
    /// Übernimmt einen vorhandenen Dump als Lauf der aktiven Mod (ohne Startargumente); Erfolg oder Fehler stehen in <see cref="Status"/>, nach
    /// Erfolg meldet <see cref="RunImported"/> den Lauf. Ein manuell gewählter Dump ist meist älter und nicht jetzt gemessen: Playset und Stand der
    /// Moddateien sind darum unbekannt (<see cref="PerformanceRun.PlaysetName"/> und <see cref="PerformanceRun.ModFilesHash"/> bleiben <c>null</c>),
    /// der Vergleich zeigt sie als "nicht geprüft".
    /// </summary>
    public void ImportManually(string dumpPath, string runName, string? saveName, int months)
    {
        RefreshMod();
        if (Session is not { } session || ModRoot is not { } modRoot || _modKey is not { } modKey)
        {
            SetStatus(PerformanceTexts.NoActiveMod);
            return;
        }

        PerformanceRun run;
        try
        {
            run = Import(new PendingRun(PerformanceSession.NewRunId(DateTime.UtcNow), session, modKey, modRoot, runName, saveName, months, null, string.Empty, MeasuredNow: false, LogFolder: null), dumpPath);
        }
        catch (Exception ex) when (IsImportError(ex))
        {
            SetStatus(PerformanceTexts.StatusImportError(ex.Message));
            return;
        }

        SetStatus(PerformanceTexts.StatusRunCreated(run.Name));
        RunImported?.Invoke(this, run.Id);
    }

    public void Dispose()
    {
        StopWatchingCore();
        _timer.Stop();
        if (Session is { } session)
        {
            session.Changed -= OnSessionChanged;
        }
    }

    /// <summary>
    /// Was beim Start der Messung feststand; der Lauf entsteht daraus, auch wenn im Hauptfenster inzwischen eine andere Mod aktiv ist. Die
    /// <paramref name="Id"/> steht vorab fest, damit ein zweiter Versuch nach einem Teilfehler keinen zweiten Lauf anlegt.
    /// <paramref name="MeasuredNow"/>: der Dump entsteht jetzt mit dieser Mod (nur dann wird der Stand der Moddateien vermerkt).
    /// <paramref name="LogFolder"/>: der beobachtete Logs-Ordner einer Messung, aus dem nach dem Import das error.log in den Lauf kopiert wird;
    /// <c>null</c> beim manuellen Import (das error.log gehört dann nicht zum Dump).
    /// </summary>
    private sealed record PendingRun(
        string Id,
        PerformanceSession Session,
        string ModKey,
        string ModRoot,
        string Name,
        string? SaveName,
        int Months,
        string? PlaysetName,
        string LaunchArguments,
        bool MeasuredNow,
        string? LogFolder);

    private static bool IsImportError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException;

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Fingerabdruck der Moddateien; <c>null</c>, wenn sich der Ordner beim Lesen ändert oder nicht lesbar ist. Auch für den KI-Auftrag (Stand jetzt).</summary>
    internal static string? HashOf(string modRoot)
    {
        try
        {
            return ModFilesHash.Compute(modRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private MeasurementPlanResult PlanFor(string? saveName, int months, bool runningInDebug, bool? canSetContinueTitle = null)
    {
        var debugArguments = DebugLaunchTracker.DebugArguments(_host.Profile.LaunchOptions.Select(o => o.Args)) ?? "-debug_mode";
        var canSet = canSetContinueTitle ?? (_host.ContinueGamePath() is { } path && File.Exists(path));
        return MeasurementPlan.Build(new MeasurementInput(_settings, debugArguments, saveName, ContinueTitle(), _host.GameRunning(), runningInDebug, months, canSet));
    }

    /// <summary>
    /// Trägt den gewählten Spielstand in continue_game.json ein (Florian 2026-10-24). Gelingt das nicht, kommt der Plan ohne Save-Laden zurück und
    /// die Statuszeile nennt den Grund; der Modder lädt dann von Hand.
    /// </summary>
    private MeasurementPlanResult ApplyContinueTitle(MeasurementPlanResult plan, string? saveName, int months, bool runningInDebug)
    {
        if (!plan.SetsContinueTitle || saveName is null)
        {
            return plan;
        }

        string? failure = null;
        try
        {
            if (_host.ContinueGamePath() is not { } path || !ContinueGameFile.WriteTitle(path, saveName))
            {
                failure = PerformanceTexts.ContinueTitleNone;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failure = ex.Message;
        }

        if (failure is null)
        {
            return plan;
        }

        SetStatus(PerformanceTexts.StatusContinueWriteFailed(failure));
        return PlanFor(saveName, months, runningInDebug, canSetContinueTitle: false);
    }

    /// <summary>
    /// Fehlerkatalog des Spiels für die Fehler-Regeln der Hinweise; <c>null</c>, wenn das Profil keinen nennt (dann laufen diese Regeln nicht).
    /// Der Katalog wird beim ersten Bedarf geladen und gilt für alle Sitzungen dieses Dienstes; Lesefehler fängt die Sitzung ab.
    /// </summary>
    private Func<ErrorCatalog>? CatalogFactory()
    {
        var path = ErrorCatalog.PathFor(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"), _host.Profile.ErrorLogRules);
        return path is null ? null : () => _catalog ??= ErrorCatalog.Load(path);
    }

    private NoiseSettings Noise()
    {
        var defaults = new NoiseSettings();
        return defaults with
        {
            RelativeThreshold = _host.State.PerformanceNoiseRelative ?? defaults.RelativeThreshold,
            AbsoluteFloor = _host.State.PerformanceNoiseFloor ?? defaults.AbsoluteFloor,
        };
    }

    private bool StartWatching(string folder, DumpSettleTracker tracker, PendingRun pending)
    {
        var pattern = _settings.DumpFilePattern ?? "*.csv";
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(folder, pattern) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };

            // Ereignisse kommen auf einem Thread des Dateisystems; der Tracker ist nicht threadsicher und wird nur im UI-Thread berührt. Ein
            // Ereignis, das nach dem Ende dieses Wartens ankommt (anderer oder kein Tracker), wird verworfen.
            void Touch(object sender, FileSystemEventArgs e)
            {
                var path = e.FullPath;
                var at = DateTime.UtcNow;
                _dispatcher.BeginInvoke(() =>
                {
                    if (ReferenceEquals(_tracker, tracker))
                    {
                        tracker.Touched(path, at);
                    }
                });
            }

            watcher.Created += Touch;
            watcher.Changed += Touch;
            watcher.Renamed += Touch;
            watcher.Error += (_, e) =>
            {
                var message = e.GetException()?.Message ?? string.Empty;
                _dispatcher.BeginInvoke(() =>
                {
                    if (ReferenceEquals(_tracker, tracker))
                    {
                        StopWatchingCore();
                        SetStatus(PerformanceTexts.StatusWatchError(message));
                    }
                });
            };
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            watcher?.Dispose();
            SetStatus(PerformanceTexts.StatusWatchError(ex.Message));
            return false;
        }

        _watcher = watcher;
        _tracker = tracker;
        _pending = pending;
        _retryPath = null;
        _timer.Start();
        SetStatus(PerformanceTexts.StatusWaiting(folder, pattern));
        return true;
    }

    private void StopWatchingCore()
    {
        _timer.Stop();
        _tracker = null;
        _pending = null;
        _retryPath = null;
        if (_watcher is { } watcher)
        {
            _watcher = null;
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }

    /// <summary>
    /// Jede Sekunde: den ersten fertigen Dump übernehmen. Eine <see cref="IOException"/> (Datei noch gesperrt; der Speicher hat schon einmal sofort
    /// wiederholt) gibt dem Pfad einen zweiten Versuch im nächsten Takt; scheitert auch der, steht der Fehler im Status und der Wächter wartet weiter.
    /// Nach dem Übernehmen wird das error.log der Sitzung in den Lauf kopiert (außerhalb des Versuchs: ein Fehler dabei löst keinen zweiten Import
    /// aus, er steht nur im Status), danach meldet <see cref="RunImported"/> den Lauf, damit die Hinweise das error.log schon sehen (ebenfalls außerhalb
    /// des Versuchs: ein Fehler beim Anzeigen löst keinen zweiten Import aus).
    /// </summary>
    private void OnTick()
    {
        if (_tracker is not { } tracker || _pending is not { } pending)
        {
            _timer.Stop();
            return;
        }

        string path;
        var lastAttempt = false;
        if (_retryPath is { } retry)
        {
            path = retry;
            _retryPath = null;
            lastAttempt = true;
        }
        else if (tracker.Settled(DateTime.UtcNow) is { Count: > 0 } settled)
        {
            path = settled[0];
        }
        else
        {
            return;
        }

        PerformanceRun run;
        try
        {
            run = Import(pending, path);
        }
        catch (IOException) when (!lastAttempt)
        {
            _retryPath = path;
            SetStatus(PerformanceTexts.StatusImportRetry);
            return;
        }
        catch (Exception ex) when (IsImportError(ex))
        {
            SetStatus(PerformanceTexts.StatusImportError(ex.Message));
            return;
        }

        var copyFailure = CopyErrorLog(pending);
        StopWatchingCore();
        SetStatus(copyFailure is null ? PerformanceTexts.StatusRunCreated(run.Name) : PerformanceTexts.StatusErrorLogCopyFailed(run.Name, copyFailure));
        RunImported?.Invoke(this, run.Id);
    }

    /// <summary>
    /// Kopiert das error.log der laufenden Spielsitzung in den Ordner des Laufs (nie verschoben: das Original bleibt in <c>logs/</c>). Gibt die
    /// Fehlermeldung zurück, wenn das Kopieren scheitert, sonst <c>null</c>; fehlt ein error.log, bleibt der Lauf still ohne.
    /// </summary>
    private string? CopyErrorLog(PendingRun pending)
    {
        if (pending.LogFolder is not { } logFolder)
        {
            return null;
        }

        try
        {
            RunErrorLog.Copy(logFolder, _store.FolderOf(pending.ModKey, pending.Id));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Legt den Lauf mit der vorab vergebenen Id an. Gibt es seinen Ordner nach einer <see cref="IOException"/> schon (ein früherer Versuch oder
    /// dieser selbst hat ihn angelegt, gescheitert ist erst ein späterer Schritt), gilt der Lauf als übernommen: Die Sitzung liest neu, und der
    /// vorhandene Lauf wird zurückgegeben. Der Speicher räumt bei eigenem Scheitern den Ordner weg; bleibt doch ein Ordner ohne lesbaren Lauf
    /// zurück, fliegt die ursprüngliche Ausnahme weiter.
    /// </summary>
    private PerformanceRun Import(PendingRun pending, string dumpPath)
    {
        var run = new PerformanceRun(
            pending.Id,
            pending.Name,
            _host.GameId,
            pending.ModKey,
            pending.ModRoot,
            pending.PlaysetName,
            pending.SaveName,
            pending.Months,
            null,
            pending.MeasuredNow ? HashOf(pending.ModRoot) : null,
            pending.LaunchArguments,
            DateTime.UtcNow,
            string.Empty);
        try
        {
            return pending.Session.Import(run, dumpPath, pending.MeasuredNow);
        }
        catch (IOException) when (Directory.Exists(_store.FolderOf(pending.ModKey, pending.Id)))
        {
            pending.Session.Refresh();
            if (pending.Session.Runs.FirstOrDefault(r => r.Id == pending.Id) is { } existing)
            {
                return existing;
            }

            throw;
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) => SessionChanged?.Invoke(this, EventArgs.Empty);

    private void SetStatus(string status)
    {
        _status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
