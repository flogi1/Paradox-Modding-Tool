using System.IO;
using System.Windows.Threading;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.SaveGames;

namespace ParadoxModTool.App;

/// <summary>
/// Was der Spielstand-Explorer vom Hauptfenster braucht. <see cref="Index"/> liefert den Symbolindex des Fehlerprotokolls oder null, solange er
/// nicht fertig ist bzw. das Spiel keinen Fehlerkatalog hat; <see cref="Jump"/> öffnet eine Datei an einer Zeile. <see cref="ErrorIcons"/>
/// liefert je Name mit Fehlerprotokoll-Meldungen das Symbol der höchsten Schwere, <see cref="ShowInErrorLog"/> öffnet das Fehlerprotokoll
/// auf diesem Namen (Plan 2b, Gegenrichtung).
/// </summary>
public sealed record SaveExplorerHost(
    string GameId,
    string GameName,
    SaveGameSettings Settings,
    string? Folder,
    Func<SymbolIndex?> Index,
    Action WantIndex,
    Action<string, int> Jump,
    Func<IReadOnlyDictionary<string, string>> ErrorIcons,
    Action<string> ShowInErrorLog);

/// <summary>
/// Dienst des Spielstand-Explorers (Plan 2a): hält die <see cref="SaveExplorerSession"/> über das Schließen des Fensters hinaus und beobachtet
/// den Ordner der Spielstände. Signale des Dateisystems werden gesammelt; nach 2 s Ruhe liest der Dienst die Liste neu und meldet einen
/// neueren Stand (Spec: nur Hinweis, kein automatisches Laden).
/// </summary>
public sealed class SaveExplorerService : IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _settle;
    private FileSystemWatcher? _watcher;

    public SaveExplorerService(SaveExplorerHost host)
    {
        Host = host;
        Session = new SaveExplorerSession(host.Settings, host.Folder);
        _settle = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => OnSettled(), _dispatcher);
        _settle.Stop();
    }

    public event EventHandler? NewerSaveAvailable;

    public SaveExplorerHost Host { get; }

    public SaveExplorerSession Session { get; }

    public void Start()
    {
        Session.RefreshSaves();
        if (Host.Folder is null || !Directory.Exists(Host.Folder))
        {
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(Host.Folder, "*" + Host.Settings.Extension) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
            _watcher.Created += OnSignal;
            _watcher.Changed += OnSignal;
            _watcher.Renamed += OnSignal;
            _watcher.Deleted += OnSignal;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    public void Dispose()
    {
        _settle.Stop();
        _watcher?.Dispose();
        _watcher = null;
        Session.Cancel();
    }

    private void OnSignal(object sender, FileSystemEventArgs e) =>
        _dispatcher.BeginInvoke(() =>
        {
            _settle.Stop();
            _settle.Start();
        });

    private void OnSettled()
    {
        _settle.Stop();
        Session.RefreshSaves();
        if (Session.NewerSave() is not null)
        {
            NewerSaveAvailable?.Invoke(this, EventArgs.Empty);
        }
    }
}
