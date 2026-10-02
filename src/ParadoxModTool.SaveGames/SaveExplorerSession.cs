using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.SaveGames;

/// <summary>Hinweis der Sitzung für die Hinweiszeile bzw. die leere Ansicht.</summary>
public enum SaveNotice
{
    None,
    FolderMissing,
    NoReadableSave,
    StillWriting,
    Incomplete,
    CannotRead,
}

/// <summary>Lädt einen Stand; in Tests austauschbar.</summary>
public delegate SaveSnapshot SaveLoader(string path, SaveGameSettings settings, IProgress<double>? progress, CancellationToken cancellationToken);

/// <summary>
/// Zustand des Spielstand-Explorers ohne WPF: Liste der Stände, aktueller Stand, Vergleichsstand und Hinweis. Ladeaufträge laufen im
/// Hintergrund; ein neuer Auftrag bricht den laufenden ab, und nur das Ergebnis des neuesten wird übernommen. Ein Stand, den das Spiel noch
/// schreibt oder der gesperrt ist, ersetzt den angezeigten Stand nicht. <see cref="Changed"/> kommt auf dem Thread, an dem der Aufrufer
/// fortsetzt (in der App der UI-Thread).
/// </summary>
public sealed class SaveExplorerSession
{
    private readonly SaveGameSettings _settings;
    private readonly SaveLoader _loader;
    private CancellationTokenSource? _loadCancel;
    private CancellationTokenSource? _comparisonCancel;
    private int _loadGeneration;
    private int _comparisonGeneration;
    private bool _loading;
    private bool _loadingComparison;
    private SaveSnapshot? _current;
    private SaveSnapshot? _before;

    public SaveExplorerSession(SaveGameSettings settings, string? folder, SaveLoader? loader = null)
    {
        _settings = settings;
        Folder = folder;
        _loader = loader ?? SaveSnapshot.Load;
    }

    public event EventHandler? Changed;

    public SaveGameSettings Settings => _settings;

    public string? Folder { get; }

    public IReadOnlyList<SaveFileInfo> Saves { get; private set; } = Array.Empty<SaveFileInfo>();

    public SaveFileInfo? CurrentFile { get; private set; }

    public SaveFileInfo? ComparisonFile { get; private set; }

    public SaveView? View { get; private set; }

    public SaveNotice Notice { get; private set; }

    public string? NoticeDetail { get; private set; }

    public bool IsLoading => _loading || _loadingComparison;

    public void RefreshSaves()
    {
        if (Folder is null || !Directory.Exists(Folder))
        {
            Saves = Array.Empty<SaveFileInfo>();
            SetNotice(SaveNotice.FolderMissing, null);
        }
        else
        {
            Saves = SaveFiles.List(Folder, _settings);
            if (!Saves.Any(s => s.Readable))
            {
                SetNotice(SaveNotice.NoReadableSave, null);
            }
            else if (Notice is SaveNotice.FolderMissing or SaveNotice.NoReadableSave)
            {
                SetNotice(SaveNotice.None, null);
            }
        }

        Raise();
    }

    /// <summary>Neuester lesbarer Stand, der später geschrieben wurde als der aktuelle; null ohne aktuellen oder ohne neueren Stand.</summary>
    public SaveFileInfo? NewerSave() =>
        CurrentFile is null
            ? null
            : Saves.Where(s => s.Readable && s.LastWriteUtc > CurrentFile.LastWriteUtc).OrderByDescending(s => s.LastWriteUtc).FirstOrDefault();

    public async Task LoadAsync(SaveFileInfo file, IProgress<double>? progress)
    {
        if (!file.Readable)
        {
            return;
        }

        _loadCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _loadCancel = cancel;
        var generation = ++_loadGeneration;
        _loading = true;
        Raise();

        var snapshot = await Run(file, progress, cancel.Token, () => generation == _loadGeneration, done => _loading = !done);
        if (snapshot is null)
        {
            return;
        }

        _current = snapshot;
        CurrentFile = file;
        if (_before is not null && ComparisonFile is not null && SamePath(ComparisonFile, file))
        {
            ClearComparison();
        }

        RebuildView();
        SetNotice(snapshot.Status == SaveReadStatus.Incomplete ? SaveNotice.Incomplete : SaveNotice.None, null);
        _loading = false;
        Raise();
    }

    /// <summary>Vergleichsstand setzen; null, derselbe Stand wie der aktuelle oder eine nicht mehr vorhandene Datei ergeben "kein Vergleich".</summary>
    public async Task SetComparisonAsync(SaveFileInfo? file, IProgress<double>? progress)
    {
        _comparisonCancel?.Cancel();
        var generation = ++_comparisonGeneration;
        if (file is null || !file.Readable || (CurrentFile is not null && SamePath(file, CurrentFile)) || !File.Exists(file.Path))
        {
            _loadingComparison = false;
            ClearComparison();
            RebuildView();
            Raise();
            return;
        }

        var cancel = new CancellationTokenSource();
        _comparisonCancel = cancel;
        _loadingComparison = true;
        Raise();

        var snapshot = await Run(file, progress, cancel.Token, () => generation == _comparisonGeneration, done => _loadingComparison = !done);
        if (snapshot is null)
        {
            return;
        }

        _before = snapshot;
        ComparisonFile = file;
        RebuildView();
        _loadingComparison = false;
        Raise();
    }

    /// <summary>Bricht laufende Aufträge ab; der angezeigte Zustand bleibt.</summary>
    public void Cancel()
    {
        _loadCancel?.Cancel();
        _comparisonCancel?.Cancel();
        _loadGeneration++;
        _comparisonGeneration++;
        _loading = false;
        _loadingComparison = false;
        Raise();
    }

    /// <summary>Lädt im Hintergrund. Null, wenn der Auftrag überholt, abgebrochen oder fehlgeschlagen ist (Hinweis dann gesetzt).</summary>
    private async Task<SaveSnapshot?> Run(SaveFileInfo file, IProgress<double>? progress, CancellationToken token, Func<bool> isLatest, Action<bool> finished)
    {
        SaveSnapshot snapshot;
        try
        {
            snapshot = await Task.Run(() => _loader(file.Path, _settings, progress, token));
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (isLatest())
            {
                finished(true);
                SetNotice(SaveNotice.CannotRead, ex.Message);
                Raise();
            }

            return null;
        }

        if (!isLatest())
        {
            return null;
        }

        if (snapshot.Status == SaveReadStatus.Changed)
        {
            finished(true);
            SetNotice(SaveNotice.StillWriting, null);
            Raise();
            return null;
        }

        return snapshot;
    }

    private void ClearComparison()
    {
        _before = null;
        ComparisonFile = null;
    }

    private void RebuildView() =>
        View = _current is null ? null
            : _before is null ? new SaveView(_current, null, null)
            : new SaveView(_current, _before, SaveComparison.Compare(_before, _current));

    private void SetNotice(SaveNotice notice, string? detail)
    {
        Notice = notice;
        NoticeDetail = detail;
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    private static bool SamePath(SaveFileInfo a, SaveFileInfo b) => string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
}
