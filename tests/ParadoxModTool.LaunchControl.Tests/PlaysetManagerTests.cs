using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PlaysetManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-manager-" + Guid.NewGuid().ToString("N"));
    private readonly string _gameFolder;
    private readonly string _modFolder;
    private readonly List<string> _log = new();
    private bool _gameRunning;
    private bool _launcherRunning;

    public PlaysetManagerTests()
    {
        _gameFolder = Path.Combine(_root, "game");
        _modFolder = Path.Combine(_gameFolder, "mod");
        Directory.CreateDirectory(_modFolder);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string AddMod(string name)
    {
        var folder = Path.Combine(_modFolder, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(_modFolder, name + ".mod"), "name=\"" + name + "\"\npath=\"" + folder.Replace('\\', '/') + "\"\n");
        return folder;
    }

    private FakeBackend NewBackend(bool launcher = true, params PlaysetData[] playsets)
    {
        var backend = new FakeBackend(Path.Combine(_gameFolder, "launcher-v2.sqlite"), launcher, _log, () => Backups().List().Count);
        backend.Playsets.AddRange(playsets);
        File.WriteAllText(backend.FilesToBackUp[0], "db-v1");
        return backend;
    }

    private PlaysetBackups Backups() => new(Path.Combine(_gameFolder, "pmt-playset-backups"));

    private PlaysetManager Manager(FakeBackend backend, string? loadFile = "dlc_load.json") => new(
        backend,
        new PlaysetGuard(() => { _log.Add("guard"); return _gameRunning; }, () => _launcherRunning),
        Backups(),
        loadFile is null ? null : Path.Combine(_gameFolder, loadFile),
        _modFolder,
        path => { _log.Add("validate"); return Directory.Exists(path); });

    private static PlaysetData Playset(string id, string name, params PlaysetModEntry[] mods) => new()
    {
        Id = id,
        Name = name,
        Mods = mods.ToList(),
        Dlcs = { new PlaysetDlcEntry("dlc_a", true), new PlaysetDlcEntry("dlc_b", false) },
    };

    private static PlaysetModEntry Mod(string path, bool enabled = true) => new(path, null, enabled, null);

    [Fact]
    public void Save_runs_guard_validate_backup_in_that_order()
    {
        var backend = NewBackend(true, Playset("p1", "One"));
        var edited = Playset("p1", "One");

        var result = Manager(backend).Save(edited);

        Assert.True(result.Success);
        Assert.Equal(new[] { "guard", "write:Save:backups=1" }, _log.Where(l => l != "validate").ToList());
        Assert.True(_log.IndexOf("guard") < _log.FindIndex(l => l.StartsWith("write")));
    }

    [Fact]
    public void Game_running_changes_nothing_and_creates_no_backup()
    {
        _gameRunning = true;
        var backend = NewBackend(true, Playset("p1", "One"));
        var manager = Manager(backend);

        Assert.Equal(PlaysetStatus.GameRunning, manager.Save(Playset("p1", "One")).Status);
        Assert.Equal(PlaysetStatus.GameRunning, manager.Delete("p1").Status);
        Assert.Equal(PlaysetStatus.GameRunning, manager.SetActive("p1").Status);

        Assert.Empty(Backups().List());
        Assert.DoesNotContain(_log, l => l.StartsWith("write"));
    }

    [Fact]
    public void Launcher_running_blocks_database_games()
    {
        _launcherRunning = true;

        Assert.Equal(PlaysetStatus.LauncherRunning, Manager(NewBackend(true, Playset("p1", "One"))).Save(Playset("p1", "One")).Status);
        Assert.True(Manager(NewBackend(false, Playset("p1", "One")), loadFile: null).Save(Playset("p1", "One")).Success);
    }

    [Fact]
    public void Validation_failure_creates_no_backup_and_writes_nothing()
    {
        var backend = NewBackend(true, Playset("p1", "One"));
        var bad = Playset("p1", "One", Mod(Path.Combine(_modFolder, "gone")));

        var result = Manager(backend).Save(bad);

        Assert.Equal(PlaysetStatus.ModMissing, result.Status);
        Assert.Empty(Backups().List());
        Assert.DoesNotContain(_log, l => l.StartsWith("write"));
    }

    [Fact]
    public void Duplicate_name_is_refused()
    {
        var backend = NewBackend(true, Playset("p1", "One"), Playset("p2", "Two"));

        var result = Manager(backend).Save(Playset("p2", "one"));

        Assert.Equal(PlaysetStatus.DuplicateName, result.Status);
    }

    [Fact]
    public void Copy_keeps_order_flags_and_dlcs()
    {
        var a = AddMod("a");
        var b = AddMod("b");
        var source = Playset("p1", "One", Mod(b), Mod(a, false));
        source.IsActive = true;
        var backend = NewBackend(true, source);

        var result = Manager(backend).Copy("p1", "Copy of One");

        Assert.True(result.Success);
        var copy = backend.Playsets.Single(p => p.Name == "Copy of One");
        Assert.Equal(new[] { b, a }, copy.Mods.Select(m => m.Path));
        Assert.Equal(new[] { true, false }, copy.Mods.Select(m => m.Enabled));
        Assert.Equal(new[] { ("dlc_a", true), ("dlc_b", false) }, copy.Dlcs.Select(d => (d.Id, d.Enabled)));
        Assert.False(copy.IsActive);
        Assert.NotEqual("p1", copy.Id);
    }

    [Fact]
    public void Copy_on_json_backend_uses_the_name_as_id()
    {
        var backend = NewBackend(false, Playset("One", "One"));

        Assert.True(Manager(backend, loadFile: null).Copy("One", "Two").Success);

        Assert.Equal("Two", backend.Playsets.Single(p => p.Name == "Two").Id);
    }

    [Fact]
    public void CreateEmpty_uses_default_dlcs()
    {
        var backend = NewBackend(true, Playset("p1", "One"));
        backend.Defaults.Add(new PlaysetDlcEntry("dlc_x", true));

        Assert.True(Manager(backend).CreateEmpty("Fresh").Success);

        var created = backend.Playsets.Single(p => p.Name == "Fresh");
        Assert.Empty(created.Mods);
        Assert.Equal(new[] { "dlc_x" }, created.Dlcs.Select(d => d.Id));
    }

    [Fact]
    public void WithExtraMod_appends_enabled_at_end_and_original_unchanged()
    {
        var a = AddMod("a");
        var test = AddMod("test");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a)));

        var result = Manager(backend).WithExtraMod("p1", Mod(test, false), "One + test");

        Assert.True(result.Success);
        Assert.Equal(new[] { a }, backend.Playsets.Single(p => p.Id == "p1").Mods.Select(m => m.Path));
        var created = backend.Playsets.Single(p => p.Name == "One + test");
        Assert.Equal(new[] { a, test }, created.Mods.Select(m => m.Path));
        Assert.True(created.Mods[1].Enabled);
    }

    [Fact]
    public void WithExtraMod_enables_a_mod_that_is_already_in_the_playset_instead_of_duplicating()
    {
        var a = AddMod("a");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a, false)));

        Assert.True(Manager(backend).WithExtraMod("p1", Mod(a), "Copy").Success);

        var created = backend.Playsets.Single(p => p.Name == "Copy");
        Assert.Single(created.Mods);
        Assert.True(created.Mods[0].Enabled);
    }

    [Fact]
    public void AddMod_inserts_before_the_first_own_mod()
    {
        var a = AddMod("a");
        var own = AddMod("own");
        var c = AddMod("c");
        var added = AddMod("added");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a), Mod(own), Mod(c)));

        var result = Manager(backend).AddMod("One", Mod(added), new[] { own });

        Assert.Equal(PlaysetEditStatus.Added, result.Status);
        Assert.Equal(new[] { a, added, own, c }, backend.Playsets.Single().Mods.Select(m => m.Path));
        Assert.True(backend.Playsets.Single().Mods[1].Enabled);
    }

    [Fact]
    public void AddMod_appends_when_the_playset_has_no_own_mod()
    {
        var a = AddMod("a");
        var added = AddMod("added");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a)));

        var result = Manager(backend).AddMod("One", Mod(added), new[] { Path.Combine(_modFolder, "elsewhere") });

        Assert.Equal(PlaysetEditStatus.Added, result.Status);
        Assert.Equal(new[] { a, added }, backend.Playsets.Single().Mods.Select(m => m.Path));
    }

    [Fact]
    public void AddMod_enables_a_switched_off_mod_in_place()
    {
        var a = AddMod("a");
        var b = AddMod("b");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a), Mod(b, false)));

        var result = Manager(backend).AddMod("One", Mod(b), Array.Empty<string>());

        Assert.Equal(PlaysetEditStatus.Enabled, result.Status);
        Assert.Equal(new[] { a, b }, backend.Playsets.Single().Mods.Select(m => m.Path));
        Assert.True(backend.Playsets.Single().Mods[1].Enabled);
    }

    [Fact]
    public void AddMod_reports_a_mod_that_is_already_in_and_on_without_writing()
    {
        var a = AddMod("a");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a)));

        var result = Manager(backend).AddMod("One", Mod(a.ToUpperInvariant() + Path.DirectorySeparatorChar), Array.Empty<string>());

        Assert.Equal(PlaysetEditStatus.AlreadyPresent, result.Status);
        Assert.DoesNotContain(_log, l => l.StartsWith("write"));
    }

    [Fact]
    public void AddMod_unknown_playset_game_running_and_launcher_running_are_reported_without_writing()
    {
        var a = AddMod("a");
        var backend = NewBackend(true, Playset("p1", "One"));
        var manager = Manager(backend);

        Assert.Equal(PlaysetEditStatus.PlaysetMissing, manager.AddMod("Nope", Mod(a), Array.Empty<string>()).Status);
        _gameRunning = true;
        Assert.Equal(PlaysetEditStatus.GameRunning, manager.AddMod("One", Mod(a), Array.Empty<string>()).Status);
        _gameRunning = false;
        _launcherRunning = true;
        var launcher = manager.AddMod("One", Mod(a), Array.Empty<string>());
        Assert.Equal(PlaysetEditStatus.WriteFailed, launcher.Status);
        Assert.Equal(PlaysetTexts.Describe(new PlaysetResult(PlaysetStatus.LauncherRunning)), launcher.Detail);
        Assert.DoesNotContain(_log, l => l.StartsWith("write"));
    }

    [Fact]
    public void PrepareStart_writes_load_file_for_launcher_backend()
    {
        var a = AddMod("a");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a)));

        var result = Manager(backend).PrepareStart("p1");

        Assert.True(result.Success);
        Assert.Contains("write:SetActive:backups=1", _log);
        Assert.Equal("{\"enabled_mods\":[\"mod/a.mod\"],\"disabled_dlcs\":[\"dlc_b\"]}", File.ReadAllText(Path.Combine(_gameFolder, "dlc_load.json")));
    }

    [Fact]
    public void PrepareStart_load_file_failure_is_reported_not_success()
    {
        var folder = Path.Combine(_modFolder, "No Descriptor");
        Directory.CreateDirectory(folder);
        var backend = NewBackend(true, Playset("p1", "One", Mod(folder)));

        var result = Manager(backend).PrepareStart("p1");

        Assert.Equal(PlaysetStatus.LoadFileFailed, result.Status);
        Assert.Equal(folder, result.Detail);
    }

    [Fact]
    public void PrepareStart_on_json_backend_only_sets_active()
    {
        var backend = NewBackend(false, Playset("One", "One"));

        Assert.True(Manager(backend, loadFile: null).PrepareStart("One").Success);

        Assert.Contains("write:SetActive:backups=1", _log);
        Assert.False(File.Exists(Path.Combine(_gameFolder, "dlc_load.json")));
    }

    [Fact]
    public void PrepareStartWithExtraMod_changes_load_file_but_not_the_database()
    {
        var a = AddMod("a");
        var test = AddMod("test");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a)));

        var result = Manager(backend).PrepareStartWithExtraMod("p1", Mod(test));

        Assert.True(result.Success);
        Assert.DoesNotContain(_log, l => l.StartsWith("write"));
        Assert.Equal("{\"enabled_mods\":[\"mod/a.mod\",\"mod/test.mod\"],\"disabled_dlcs\":[\"dlc_b\"]}", File.ReadAllText(Path.Combine(_gameFolder, "dlc_load.json")));
        Assert.Single(backend.Playsets.Single().Mods);
    }

    [Fact]
    public void PrepareStartWithExtraMod_without_load_file_is_refused()
    {
        var backend = NewBackend(false, Playset("One", "One"));

        Assert.Equal(PlaysetStatus.LoadFileFailed, Manager(backend, loadFile: null).PrepareStartWithExtraMod("One", Mod("x")).Status);
    }

    [Fact]
    public void Restore_puts_database_and_load_file_back_together()
    {
        var a = AddMod("a");
        var backend = NewBackend(true, Playset("p1", "One", Mod(a)));
        var loadFile = Path.Combine(_gameFolder, "dlc_load.json");
        File.WriteAllText(loadFile, "load-v1");
        var manager = Manager(backend);
        Assert.True(manager.Save(Playset("p1", "One", Mod(a))).Success);
        File.WriteAllText(backend.FilesToBackUp[0], "db-v2");
        File.WriteAllText(loadFile, "load-v2");

        var result = manager.RestoreBackup(manager.BackupSets()[0]);

        Assert.True(result.Success);
        Assert.Equal("db-v1", File.ReadAllText(backend.FilesToBackUp[0]));
        Assert.Equal("load-v1", File.ReadAllText(loadFile));
    }

    [Fact]
    public void Restore_is_blocked_while_the_game_runs()
    {
        var backend = NewBackend(true, Playset("p1", "One"));
        var manager = Manager(backend);
        Assert.True(manager.Save(Playset("p1", "One")).Success);
        _gameRunning = true;

        Assert.Equal(PlaysetStatus.GameRunning, manager.RestoreBackup(manager.BackupSets()[0]).Status);
    }

    [Fact]
    public void Delete_active_playset_is_allowed_and_leaves_no_active_flag_dangling()
    {
        var path = Path.Combine(_gameFolder, "playsets.json");
        File.WriteAllText(path, "{\"playsets\":[{\"name\":\"One\",\"isActive\":true},{\"name\":\"Two\"}]}");
        var manager = new PlaysetManager(new JsonPlaysetBackend(path), new PlaysetGuard(() => false, () => false), Backups(), null, _modFolder, Directory.Exists);

        var result = manager.Delete("One");

        Assert.True(result.Success);
        var left = manager.LoadAll();
        Assert.Equal(new[] { "Two" }, left.Select(p => p.Name));
        Assert.DoesNotContain(left, p => p.IsActive);
        Assert.Single(manager.BackupSets());
    }

    [Fact]
    public void Factory_returns_null_for_missing_file_and_picks_backend_by_extension()
    {
        Assert.Null(PlaysetManagerFactory.Create(Path.Combine(_gameFolder, "nope.json"), null, null));
        var json = Path.Combine(_gameFolder, "playsets.json");
        File.WriteAllText(json, "{\"playsets\":[]}");
        var sqlite = Path.Combine(_gameFolder, "launcher-v2.sqlite");
        File.WriteAllText(sqlite, "x");

        var jsonManager = PlaysetManagerFactory.Create(json, null, null)!;
        var dbManager = PlaysetManagerFactory.Create(sqlite, "dlc_load.json", null)!;

        Assert.False(jsonManager.UsesLauncherDatabase);
        Assert.Null(jsonManager.LoadFilePath);
        Assert.True(dbManager.UsesLauncherDatabase);
        Assert.Equal(Path.Combine(_gameFolder, "dlc_load.json"), dbManager.LoadFilePath);
        Assert.Equal(_modFolder, dbManager.GameModFolder);
    }

    private sealed class FakeBackend : IPlaysetBackend
    {
        private readonly List<string> _log;
        private readonly Func<int> _backupCount;
        private readonly string _file;

        public FakeBackend(string file, bool launcher, List<string> log, Func<int> backupCount)
        {
            _file = file;
            UsesLauncherDatabase = launcher;
            _log = log;
            _backupCount = backupCount;
        }

        public List<PlaysetData> Playsets { get; } = new();

        public List<PlaysetDlcEntry> Defaults { get; } = new();

        public IReadOnlyList<string> FilesToBackUp => new[] { _file };

        public bool UsesLauncherDatabase { get; }

        public IReadOnlyList<PlaysetData> LoadAll() => Playsets.ToList();

        public PlaysetResult Save(PlaysetData playset)
        {
            _log.Add($"write:Save:backups={_backupCount()}");
            var existing = Playsets.FindIndex(p => p.Id == playset.Id);
            if (existing >= 0)
            {
                Playsets[existing] = playset;
            }
            else
            {
                Playsets.Add(playset);
            }

            return PlaysetResult.Ok;
        }

        public PlaysetResult Delete(string id)
        {
            _log.Add($"write:Delete:backups={_backupCount()}");
            Playsets.RemoveAll(p => p.Id == id);
            return PlaysetResult.Ok;
        }

        public PlaysetResult SetActive(string id)
        {
            _log.Add($"write:SetActive:backups={_backupCount()}");
            foreach (var playset in Playsets)
            {
                playset.IsActive = playset.Id == id;
            }

            return PlaysetResult.Ok;
        }

        public IReadOnlyList<PlaysetDlcEntry> DefaultDlcs() => Defaults;
    }
}
