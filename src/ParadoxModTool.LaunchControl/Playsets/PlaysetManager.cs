namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Die Playset-Verwaltung eines Spiels. Jede schreibende Methode läuft gleich ab: Schutz ("Spiel oder Launcher läuft"), Prüfung des Playsets, Sicherung aller
/// berührten Dateien (Datenbank oder playsets.json und Lade-Datei zusammen), dann das Schreiben. Schlägt ein Schritt fehl, ändert sich nichts Weiteres.
/// </summary>
public sealed class PlaysetManager
{
    private readonly IPlaysetBackend _backend;
    private readonly PlaysetGuard _guard;
    private readonly PlaysetBackups _backups;
    private readonly Func<string, bool> _modFolderExists;
    private readonly DlcNameResolver? _dlcNames;

    /// <param name="loadFilePath">Lade-Datei, die das Spiel beim direkten Start liest (nur Launcher-Spiele); sonst null.</param>
    /// <param name="gameModFolder">Documents/Paradox Interactive/&lt;Spiel&gt;/mod.</param>
    public PlaysetManager(IPlaysetBackend backend, PlaysetGuard guard, PlaysetBackups backups, string? loadFilePath, string gameModFolder, Func<string, bool> modFolderExists, DlcNameResolver? dlcNames = null)
    {
        _backend = backend;
        _guard = guard;
        _backups = backups;
        LoadFilePath = loadFilePath;
        GameModFolder = gameModFolder;
        _modFolderExists = modFolderExists;
        _dlcNames = dlcNames;
    }

    public string? LoadFilePath { get; }

    public string GameModFolder { get; }

    public bool UsesLauncherDatabase => _backend.UsesLauncherDatabase;

    public IPlaysetBackend Backend => _backend;

    public IReadOnlyList<PlaysetData> LoadAll() => _backend.LoadAll();

    /// <summary>Anzeigename eines DLCs aus den Spieldateien; ohne sie ein aus der Kennung gebauter Name.</summary>
    public string DlcName(string id) => _dlcNames?.Name(id) ?? DlcNameResolver.Readable(id);

    public PlaysetResult Save(PlaysetData playset)
    {
        var writable = _guard.CheckWritable(_backend.UsesLauncherDatabase);
        if (!writable.Success)
        {
            return writable;
        }

        var valid = PlaysetGuard.Validate(playset, _backend.LoadAll(), _modFolderExists);
        if (!valid.Success)
        {
            return valid;
        }

        return WithBackup(() => _backend.Save(playset));
    }

    public PlaysetResult Delete(string id)
    {
        var writable = _guard.CheckWritable(_backend.UsesLauncherDatabase);
        return !writable.Success ? writable : WithBackup(() => _backend.Delete(id));
    }

    /// <summary>Kopie mit Mods, Ein/Aus-Zuständen, Reihenfolge und DLCs genau wie im Original; die Kopie ist nicht aktiv.</summary>
    public PlaysetResult Copy(string id, string newName)
    {
        var source = _backend.LoadAll().FirstOrDefault(p => p.Id == id);
        return source is null
            ? new PlaysetResult(PlaysetStatus.PlaysetMissing, id)
            : Save(source.CopyAs(NewId(newName), newName));
    }

    public PlaysetResult CreateEmpty(string name) =>
        Save(new PlaysetData { Id = NewId(name), Name = name, Dlcs = _backend.DefaultDlcs().ToList() });

    /// <summary>Neues Playset aus dem gewählten plus <paramref name="mod"/> am Ende, eingeschaltet (steht sie schon darin, wird sie nur eingeschaltet).</summary>
    public PlaysetResult WithExtraMod(string id, PlaysetModEntry mod, string newName)
    {
        var source = _backend.LoadAll().FirstOrDefault(p => p.Id == id);
        if (source is null)
        {
            return new PlaysetResult(PlaysetStatus.PlaysetMissing, id);
        }

        return Save(AddEnabled(source.CopyAs(NewId(newName), newName), mod));
    }

    /// <summary>
    /// Nimmt eine Mod ins Playset <paramref name="playsetName"/> auf (Fehlerprotokoll "Mod ins Playset aufnehmen"): vor die erste eigene Mod, sonst ans Ende; steht sie
    /// ausgeschaltet darin, wird sie an ihrer Stelle eingeschaltet. Gleiche Ergebnisse wie <see cref="PlaysetEdits.AddMod"/> für playsets.json, dazu für Launcher-Spiele;
    /// Schutz, Prüfung und Sicherung wie bei <see cref="Save"/>. Bei anderen Fehlern steht der verständliche Text in <see cref="PlaysetEditResult.Detail"/>.
    /// </summary>
    public PlaysetEditResult AddMod(string playsetName, PlaysetModEntry mod, IEnumerable<string> ownModFolders)
    {
        var writable = _guard.CheckWritable(_backend.UsesLauncherDatabase);
        if (!writable.Success)
        {
            return ToEditResult(writable);
        }

        var playset = _backend.LoadAll().FirstOrDefault(p => p.Name == playsetName);
        if (playset is null)
        {
            return new PlaysetEditResult(PlaysetEditStatus.PlaysetMissing, playsetName);
        }

        var key = ModKey(mod.Path);
        var index = playset.Mods.FindIndex(m => ModKey(m.Path) == key);
        PlaysetEditStatus status;
        if (index >= 0)
        {
            if (playset.Mods[index].Enabled)
            {
                return new PlaysetEditResult(PlaysetEditStatus.AlreadyPresent);
            }

            playset.Mods[index] = playset.Mods[index] with { Enabled = true };
            status = PlaysetEditStatus.Enabled;
        }
        else
        {
            var own = ownModFolders.Select(ModKey).ToHashSet();
            var firstOwn = playset.Mods.FindIndex(m => own.Contains(ModKey(m.Path)));
            playset.Mods.Insert(firstOwn >= 0 ? firstOwn : playset.Mods.Count, mod with { Enabled = true });
            status = PlaysetEditStatus.Added;
        }

        var saved = Save(playset);
        return saved.Success ? new PlaysetEditResult(status) : ToEditResult(saved);
    }

    private static string ModKey(string path) => PlaysetGuard.NormalizePath(path).Replace('/', '\\').ToLowerInvariant();

    private static PlaysetEditResult ToEditResult(PlaysetResult result) => result.Status switch
    {
        PlaysetStatus.GameRunning => new PlaysetEditResult(PlaysetEditStatus.GameRunning),
        PlaysetStatus.FileMissing => new PlaysetEditResult(PlaysetEditStatus.FileMissing, result.Detail),
        PlaysetStatus.PlaysetMissing => new PlaysetEditResult(PlaysetEditStatus.PlaysetMissing, result.Detail),
        PlaysetStatus.InvalidFile => new PlaysetEditResult(PlaysetEditStatus.InvalidFile),
        _ => new PlaysetEditResult(PlaysetEditStatus.WriteFailed, PlaysetTexts.Describe(result)),
    };

    /// <summary>Setzt das Playset aktiv und schreibt bei Launcher-Spielen die Lade-Datei dazu; die Datenbank ist dann schon geändert, wenn die Lade-Datei scheitert.</summary>
    public PlaysetResult SetActive(string id)
    {
        var writable = _guard.CheckWritable(_backend.UsesLauncherDatabase);
        if (!writable.Success)
        {
            return writable;
        }

        var playset = _backend.LoadAll().FirstOrDefault(p => p.Id == id);
        if (playset is null)
        {
            return new PlaysetResult(PlaysetStatus.PlaysetMissing, id);
        }

        return WithBackup(() =>
        {
            var result = _backend.SetActive(id);
            return result.Success && LoadFilePath is not null ? LoadFileWriter.Write(LoadFilePath, playset, GameModFolder) : result;
        });
    }

    /// <summary>Bereitet den Start mit dem Playset vor (aktiv setzen, Lade-Datei schreiben); bei <see cref="PlaysetStatus.LoadFileFailed"/> darf nicht gestartet werden.</summary>
    public PlaysetResult PrepareStart(string id) => SetActive(id);

    /// <summary>
    /// Start mit dem Playset plus einer Mod, ohne etwas zu speichern: nur die Lade-Datei wird aus einer Kopie im Speicher geschrieben. Gibt es keine Lade-Datei
    /// (EU V), ist das nicht möglich; der Aufrufer nimmt dann <see cref="WithExtraMod"/>.
    /// </summary>
    public PlaysetResult PrepareStartWithExtraMod(string id, PlaysetModEntry mod)
    {
        if (LoadFilePath is null)
        {
            return new PlaysetResult(PlaysetStatus.LoadFileFailed, "no load file");
        }

        var writable = _guard.CheckWritable(_backend.UsesLauncherDatabase);
        if (!writable.Success)
        {
            return writable;
        }

        var source = _backend.LoadAll().FirstOrDefault(p => p.Id == id);
        if (source is null)
        {
            return new PlaysetResult(PlaysetStatus.PlaysetMissing, id);
        }

        var temporary = AddEnabled(source.CopyAs(source.Id, source.Name), mod);
        return WithBackup(() => LoadFileWriter.Write(LoadFilePath, temporary, GameModFolder));
    }

    public IReadOnlyList<string> BackupSets() => _backups.List();

    public PlaysetResult RestoreBackup(string setName)
    {
        var writable = _guard.CheckWritable(_backend.UsesLauncherDatabase);
        return !writable.Success ? writable : _backups.Restore(setName, Path.GetDirectoryName(_backend.FilesToBackUp[0])!);
    }

    private PlaysetResult WithBackup(Func<PlaysetResult> write)
    {
        try
        {
            _backups.Create(LoadFilePath is null ? _backend.FilesToBackUp : _backend.FilesToBackUp.Append(LoadFilePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PlaysetResult(PlaysetStatus.WriteFailed, ex.Message);
        }

        return write();
    }

    private string NewId(string name) => _backend.UsesLauncherDatabase ? Guid.NewGuid().ToString() : name;

    private static PlaysetData AddEnabled(PlaysetData playset, PlaysetModEntry mod)
    {
        var key = PlaysetGuard.NormalizePath(mod.Path);
        var index = playset.Mods.FindIndex(m => string.Equals(PlaysetGuard.NormalizePath(m.Path), key, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            playset.Mods[index] = playset.Mods[index] with { Enabled = true };
        }
        else
        {
            playset.Mods.Add(mod with { Enabled = true });
        }

        return playset;
    }
}
