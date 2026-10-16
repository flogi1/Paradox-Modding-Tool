using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class PlaysetBackupsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-backups-" + Guid.NewGuid().ToString("N"));
    private DateTime _now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Local);

    public PlaysetBackupsTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "game"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string GameFile(string name, string content)
    {
        var path = Path.Combine(_root, "game", name);
        File.WriteAllText(path, content);
        return path;
    }

    private PlaysetBackups Backups() => new(Path.Combine(_root, "backups"), () => _now);

    [Fact]
    public void Create_copies_files_into_timestamped_set()
    {
        var db = GameFile("launcher-v2.sqlite", "db1");
        var load = GameFile("dlc_load.json", "load1");

        var set = Backups().Create(new[] { db, load });

        Assert.Equal("20261016-120000", set);
        Assert.Equal("db1", File.ReadAllText(Path.Combine(_root, "backups", set, "launcher-v2.sqlite")));
        Assert.Equal("load1", File.ReadAllText(Path.Combine(_root, "backups", set, "dlc_load.json")));
    }

    [Fact]
    public void Create_skips_missing_files()
    {
        var db = GameFile("launcher-v2.sqlite", "db1");

        var set = Backups().Create(new[] { db, Path.Combine(_root, "game", "dlc_load.json") });

        Assert.False(File.Exists(Path.Combine(_root, "backups", set, "dlc_load.json")));
        Assert.True(File.Exists(Path.Combine(_root, "backups", set, "launcher-v2.sqlite")));
    }

    [Fact]
    public void Create_in_same_second_gets_distinct_names()
    {
        var db = GameFile("a.json", "x");
        var backups = Backups();

        var first = backups.Create(new[] { db });
        File.WriteAllText(db, "y");
        var second = backups.Create(new[] { db });

        Assert.NotEqual(first, second);
        Assert.Equal(2, backups.List().Count);
    }

    [Fact]
    public void Create_with_the_same_files_as_the_newest_set_reuses_it()
    {
        var db = GameFile("a.json", "x");
        var load = GameFile("dlc_load.json", "l");
        var backups = Backups();

        var first = backups.Create(new[] { db, load });
        _now = _now.AddMinutes(1);
        var second = backups.Create(new[] { db, load });

        Assert.Equal(first, second);
        Assert.Single(backups.List());
    }

    [Fact]
    public void Create_makes_a_new_set_when_one_file_changed_or_a_file_was_added()
    {
        var db = GameFile("a.json", "x");
        var backups = Backups();
        var first = backups.Create(new[] { db });

        _now = _now.AddMinutes(1);
        var load = GameFile("dlc_load.json", "l");
        var withLoad = backups.Create(new[] { db, load });
        _now = _now.AddMinutes(1);
        File.WriteAllText(load, "l2");
        var changed = backups.Create(new[] { db, load });

        Assert.Equal(3, new[] { first, withLoad, changed }.Distinct().Count());
    }

    [Fact]
    public void Create_keeps_only_newest_ten_sets()
    {
        var db = GameFile("a.json", "x");
        var backups = Backups();
        for (var i = 0; i < 12; i++)
        {
            _now = _now.AddMinutes(1);
            File.WriteAllText(db, "x" + i);
            backups.Create(new[] { db });
        }

        var list = backups.List();

        Assert.Equal(10, list.Count);
        Assert.Equal("20261016-121200", list[0]);
        Assert.DoesNotContain("20261016-120100", list);
        Assert.DoesNotContain("20261016-120200", list);
    }

    [Fact]
    public void Restore_puts_files_back_and_keeps_current_state_as_new_set()
    {
        var db = GameFile("launcher-v2.sqlite", "old");
        var backups = Backups();
        var set = backups.Create(new[] { db });
        File.WriteAllText(db, "new");
        _now = _now.AddMinutes(5);

        var result = backups.Restore(set, Path.Combine(_root, "game"));

        Assert.True(result.Success);
        Assert.Equal("old", File.ReadAllText(db));
        var newest = backups.List()[0];
        Assert.NotEqual(set, newest);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_root, "backups", newest, "launcher-v2.sqlite")));
    }

    [Fact]
    public void Restore_removes_a_stale_wal_and_shm_that_the_set_does_not_have_and_keeps_them_in_the_safety_set()
    {
        var db = GameFile("launcher-v2.sqlite", "old");
        var backups = Backups();
        var set = backups.Create(new[] { db });
        File.WriteAllText(db, "new");
        var wal = GameFile("launcher-v2.sqlite-wal", "stale-wal");
        var shm = GameFile("launcher-v2.sqlite-shm", "stale-shm");
        _now = _now.AddMinutes(5);

        var result = backups.Restore(set, Path.Combine(_root, "game"));

        Assert.True(result.Success);
        Assert.Equal("old", File.ReadAllText(db));
        Assert.False(File.Exists(wal));
        Assert.False(File.Exists(shm));
        var safety = backups.List()[0];
        Assert.Equal("stale-wal", File.ReadAllText(Path.Combine(_root, "backups", safety, "launcher-v2.sqlite-wal")));
        Assert.Equal("stale-shm", File.ReadAllText(Path.Combine(_root, "backups", safety, "launcher-v2.sqlite-shm")));
    }

    [Fact]
    public void Restore_keeps_a_wal_that_belongs_to_the_set()
    {
        var db = GameFile("launcher-v2.sqlite", "old");
        var wal = GameFile("launcher-v2.sqlite-wal", "set-wal");
        var backups = Backups();
        var set = backups.Create(new[] { db, wal });
        File.WriteAllText(wal, "changed");

        Assert.True(backups.Restore(set, Path.Combine(_root, "game")).Success);

        Assert.Equal("set-wal", File.ReadAllText(wal));
    }

    [Fact]
    public void Restore_unknown_set_is_reported()
    {
        var result = Backups().Restore("nope", Path.Combine(_root, "game"));

        Assert.Equal(PlaysetStatus.BackupMissing, result.Status);
    }
}
