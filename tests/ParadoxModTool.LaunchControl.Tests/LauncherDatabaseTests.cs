using Microsoft.Data.Sqlite;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

/// <summary>
/// Playsets aus launcher-v2.sqlite (EU IV, CK3, Stellaris). Die Testdatenbank bildet nur die Spalten nach, die PMT liest; die
/// Tabellennamen und Spalten stammen aus einer echten EU-IV-Datenbank.
/// </summary>
public class LauncherDatabaseTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "pmt-launcher-" + Guid.NewGuid().ToString("N") + ".sqlite");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _file, _file + ".pmt-backup" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private void CreateDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_file};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            create table mods (id char(36) not null primary key, displayName varchar(255), dirPath text, status text not null, source text not null);
            create table playsets (id char(36) not null primary key, name varchar(255) not null, isActive boolean, isRemoved boolean not null default false, createdOn datetime not null);
            create table playsets_mods (playsetId char(36) not null, modId char(36) not null, enabled boolean default '1', position integer);
            insert into mods values ('m1', 'Erste', 'D:\steam\236850\1', 'ready_to_play', 'steam');
            insert into mods values ('m2', 'Zweite', 'D:\steam\236850\2', 'ready_to_play', 'steam');
            insert into mods values ('m3', 'Ohne Ordner', null, 'ready_to_play', 'steam');
            insert into playsets values ('p1', 'Initial playset', 0, 0, '2026-01-01');
            insert into playsets values ('p2', 'RNW Test', 1, 0, '2026-01-02');
            insert into playsets values ('p3', 'Geloescht', 0, 1, '2026-01-03');
            insert into playsets_mods values ('p1', 'm2', 1, 1);
            insert into playsets_mods values ('p1', 'm1', 0, 0);
            insert into playsets_mods values ('p1', 'm3', 1, 2);
            insert into playsets_mods values ('p2', 'm1', 1, 0);
            """;
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Lists_playsets_without_removed_ones()
    {
        CreateDatabase();
        var playsets = PlaysetStore.ListPlaysets(_file);
        Assert.Equal(new[] { ("Initial playset", false, 3, 2), ("RNW Test", true, 1, 1) },
            playsets.Select(p => (p.Name, p.IsActive, p.ModCount, p.EnabledModCount)));
    }

    [Fact]
    public void Lists_mods_in_position_order_and_skips_mods_without_folder()
    {
        CreateDatabase();
        var mods = PlaysetStore.ListPlaysetMods(_file, "Initial playset");
        Assert.Equal(new[] { (@"D:\steam\236850\1", false), (@"D:\steam\236850\2", true) }, mods.Select(m => (m.Path, m.IsEnabled)));
    }

    [Fact]
    public void Reads_while_the_launcher_holds_a_write_lock()
    {
        CreateDatabase();
        using var launcher = new SqliteConnection($"Data Source={_file};Pooling=False");
        launcher.Open();
        using var begin = launcher.CreateCommand();
        begin.CommandText = "begin exclusive";
        begin.ExecuteNonQuery();

        Assert.Equal(2, PlaysetStore.ListPlaysets(_file).Count);
        Assert.Single(PlaysetStore.ListPlaysetMods(_file, "RNW Test"));
    }

    [Fact]
    public void Broken_database_and_unknown_playset_give_empty_lists()
    {
        CreateDatabase();
        Assert.Empty(PlaysetStore.ListPlaysetMods(_file, "Gibt es nicht"));
        File.WriteAllText(_file, "keine Datenbank");
        Assert.Empty(PlaysetStore.ListPlaysets(_file));
        Assert.Empty(PlaysetStore.ListPlaysetMods(_file, "RNW Test"));
    }

    [Fact]
    public void Switching_the_active_playset_is_refused_and_leaves_the_database_unchanged()
    {
        CreateDatabase();
        var before = File.ReadAllBytes(_file);
        var result = PlaysetStore.SetActivePlayset(_file, "Initial playset");
        Assert.False(result.Success);
        Assert.Equal(PlaysetTexts.LauncherDatabaseReadOnly, result.ErrorMessage);
        Assert.Equal(before, File.ReadAllBytes(_file));
        Assert.False(File.Exists(_file + ".pmt-backup"));
    }

    [Fact]
    public void Adding_a_mod_is_refused_as_launcher_database()
    {
        CreateDatabase();
        var result = PlaysetEdits.AddMod(_file, "RNW Test", @"D:\steam\236850\2", Array.Empty<string>(), () => false);
        Assert.Equal(PlaysetEditStatus.LauncherDatabase, result.Status);
        Assert.Single(PlaysetStore.ListPlaysetMods(_file, "RNW Test"));
    }

    [Fact]
    public void Recognizes_launcher_databases_by_extension()
    {
        Assert.True(PlaysetStore.IsLauncherDatabase(@"C:\x\launcher-v2.sqlite"));
        Assert.True(PlaysetStore.IsLauncherDatabase(@"C:\x\LAUNCHER-V2.SQLITE"));
        Assert.False(PlaysetStore.IsLauncherDatabase(@"C:\x\playsets.json"));
        Assert.False(PlaysetStore.IsLauncherDatabase(null));
    }
}
