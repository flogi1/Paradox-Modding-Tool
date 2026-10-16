using Microsoft.Data.Sqlite;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

/// <summary>
/// Schreibende Playset-Verwaltung in launcher-v2.sqlite. Die Testdatenbank hat das echte Schema des Launchers (Stand 2026-10-16, CK III);
/// die Tabellen stammen aus dessen sqlite_master.
/// </summary>
public class LauncherPlaysetBackendTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-launcher-backend-" + Guid.NewGuid().ToString("N"));
    private readonly string _db;
    private readonly string _modFolder;

    public LauncherPlaysetBackendTests()
    {
        Directory.CreateDirectory(_root);
        _db = Path.Combine(_root, "launcher-v2.sqlite");
        _modFolder = Path.Combine(_root, "mod");
        Directory.CreateDirectory(Path.Combine(_modFolder, "Brand New"));
        File.WriteAllText(Path.Combine(_modFolder, "Brand New.mod"), "name=\"Brand New\"");
        Directory.CreateDirectory(Path.Combine(_modFolder, "No Descriptor"));
        CreateDatabase();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }

    private LauncherPlaysetBackend Backend() => new(_db, _modFolder);

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={_db};Pooling=False");
        connection.Open();
        return connection;
    }

    private void CreateDatabase()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE `mods` (`id` char(36) not null, `pdxId` varchar(255), `steamId` varchar(255), `gameRegistryId` text, `name` varchar(255), `displayName` varchar(255), "descriptionDeprecated" text, `thumbnailUrl` text, `thumbnailPath` text, `version` varchar(255), `tags` json default '[]', `requiredVersion` varchar(255), `arch` text, `os` text, `repositoryPath` text, `dirPath` text, `archivePath` text, `status` text not null, `source` text not null, `cause` text null default null, `timeUpdated` integer, `isNew` boolean, `createdDate` datetime, `subscribedDate` datetime, `size` integer, `metadataId` varchar(255), `remotePdxId` varchar(255), `remoteSteamId` varchar(255), `metadataVersion` varchar(255), `isMetadataApplied` boolean not null default '0', `metadataStatus` text check (`metadataStatus` in ('not_applied', 'missing', 'parsing_error', 'applied', 'validation_error')) not null default 'not_applied', `metadataGameId` varchar(255), `descriptionPdx` varchar(255), `descriptionSteam` varchar(255), `shortDescriptionPdx` varchar(255), `keepLatest` boolean not null default '1', `userVersion` boolean null default null, `remotePdxUserId` varchar(255) null, `remoteSteamUserId` varchar(255) null, `state` varchar(255) null, primary key (`id`));
            CREATE TABLE playsets (
                id char(36) not null,
                name varchar(255) not null,
                isActive boolean,
                loadOrder varchar(255),
                pdxId int,
                pdxUserId char(36),
                createdOn datetime not null,
                updatedOn datetime,
                syncedOn datetime,
                "deprecatedLastServerChecksum" text,
                isRemoved boolean not null default false, `hasNotApprovedChanges` boolean not null default '0', `syncState` varchar(255), `state` text check (`state` in ('private', 'removedByUser', 'public', 'disabledByManager')) not null default 'private', `owned` boolean not null default '1', `author` varchar(255) not null default '', `subscribersCount` integer not null default '0', `ratingsCount` integer not null default '0', "coverImagePath" varchar(255), `description` varchar(255) default '', `offDisk` boolean not null default '0', `version` varchar(255) null, `lastSyncAttemptAt` datetime null, `coverImageUpdatedOn` datetime null, `lastUsedAt` datetime null,
                primary key (id),
                constraint uq_pdxId unique (pdxId)
            );
            CREATE TABLE `playsets_mods` (`playsetId` char(36) not null, `modId` char(36) not null, `enabled` boolean default '1', `position` integer, foreign key(`playsetId`) references `playsets`(`id`) on delete CASCADE, foreign key(`modId`) references `mods`(`id`) on delete CASCADE);
            CREATE TABLE `playsets_dlcs` (`playsetId` char(36) not null, `dlcId` text not null, `enabled` boolean not null default '0', foreign key(`playsetId`) references `playsets`(`id`) on delete CASCADE, primary key (`playsetId`, `dlcId`));

            insert into mods (id, gameRegistryId, displayName, dirPath, status, source, version, tags, createdDate, size, descriptionSteam) values
              ('m1', null, 'Workshop One', 'D:\steam\workshop\1', 'ready_to_play', 'steam', '2.0', '["A"]', 100, 5, 'unknown text kept');
            insert into mods (id, gameRegistryId, displayName, dirPath, status, source, version, tags, createdDate, size) values
              ('m2', 'mod/Local Two.mod', 'Local Two', 'C:\docs\mod\Local Two', 'ready_to_play', 'local', '1.0', '["Utilities"]', 200, 7);
            insert into mods (id, displayName, dirPath, status, source) values
              ('m3', 'Broken', null, 'unsubscribed', 'steam');

            insert into playsets (id, name, isActive, pdxId, createdOn, updatedOn, syncState, author, description) values
              ('p1', 'Cloudish', 0, 100, 1, 1, 'NOT_ELIGIBLE', 'someone', 'unknown description kept');
            insert into playsets (id, name, isActive, pdxId, createdOn, updatedOn, syncState) values
              ('p2', 'Local', 1, null, 2, 2, null);
            insert into playsets (id, name, isActive, pdxId, createdOn, updatedOn, syncState) values
              ('p3', 'Synced', null, 300, 3, 3, 'SYNCED');
            insert into playsets (id, name, isActive, pdxId, createdOn, isRemoved, state) values
              ('p4', 'Removed', 0, null, 4, 1, 'removedByUser');

            insert into playsets_mods values ('p1', 'm1', 1, 0), ('p1', 'm2', 0, 1), ('p1', 'm3', 1, 2);
            insert into playsets_mods values ('p2', 'm2', 1, 0);
            insert into playsets_dlcs values ('p1', 'dlc_a', 1), ('p1', 'dlc_b', 0), ('p2', 'dlc_a', 1), ('p3', 'dlc_c', 1);
            """;
        command.ExecuteNonQuery();
    }

    private string Scalar(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is null or DBNull ? "<null>" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
    }

    /// <summary>Alle Zeilen aller Tabellen als Text, optional ohne die Zeilen eines Playsets.</summary>
    private string Snapshot(string? exceptPlaysetId = null)
    {
        using var connection = Open();
        var text = new System.Text.StringBuilder();
        foreach (var (table, filter) in new[]
        {
            ("mods", "1"),
            ("playsets", exceptPlaysetId is null ? "1" : $"id <> '{exceptPlaysetId}'"),
            ("playsets_mods", exceptPlaysetId is null ? "1" : $"playsetId <> '{exceptPlaysetId}'"),
            ("playsets_dlcs", exceptPlaysetId is null ? "1" : $"playsetId <> '{exceptPlaysetId}'"),
        })
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"select * from {table} where {filter} order by 1, 2";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    text.Append(reader.IsDBNull(i) ? "<null>" : reader.GetValue(i)).Append('|');
                }

                text.AppendLine();
            }
        }

        return text.ToString();
    }

    private PlaysetData NewPlayset(string name, params PlaysetModEntry[] mods) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        Mods = mods.ToList(),
        Dlcs = { new PlaysetDlcEntry("dlc_a", true), new PlaysetDlcEntry("dlc_b", false) },
    };

    [Fact]
    public void LoadAll_reads_mods_in_position_order_dlcs_flags_and_skips_removed()
    {
        var all = Backend().LoadAll();

        Assert.Equal(new[] { "Cloudish", "Local", "Synced" }, all.Select(p => p.Name));
        var first = all[0];
        Assert.Equal(new[] { @"D:\steam\workshop\1", @"C:\docs\mod\Local Two", "" }, first.Mods.Select(m => m.Path));
        Assert.Equal(new[] { true, false, true }, first.Mods.Select(m => m.Enabled));
        Assert.Equal("m2", first.Mods[1].LauncherModId);
        Assert.Equal(new[] { ("dlc_a", true), ("dlc_b", false) }, first.Dlcs.Select(d => (d.Id, d.Enabled)));
        Assert.True(all[1].IsActive);
    }

    [Fact]
    public void IsCloud_only_for_pdxId_with_real_sync_state()
    {
        var all = Backend().LoadAll().ToDictionary(p => p.Name);

        Assert.False(all["Cloudish"].IsCloud);
        Assert.False(all["Local"].IsCloud);
        Assert.True(all["Synced"].IsCloud);
    }

    [Fact]
    public void Save_new_playset_inserts_playset_mods_and_dlcs_in_one_transaction()
    {
        var playset = NewPlayset("Neu", new PlaysetModEntry(@"D:\steam\workshop\1", null, true, "m1"), new PlaysetModEntry(@"C:\docs\mod\Local Two", null, false, null));

        var result = Backend().Save(playset);

        Assert.True(result.Success);
        var id = playset.Id;
        Assert.Equal("m1,m2", Scalar($"select group_concat(modId) from (select modId from playsets_mods where playsetId='{id}' order by position)"));
        Assert.Equal("1,0", Scalar($"select group_concat(enabled) from (select enabled from playsets_mods where playsetId='{id}' order by position)"));
        Assert.Equal("dlc_a:1,dlc_b:0", Scalar($"select group_concat(dlcId || ':' || enabled) from (select * from playsets_dlcs where playsetId='{id}' order by dlcId)"));
    }

    [Fact]
    public void Save_new_playset_uses_local_defaults()
    {
        var playset = NewPlayset("Neu");

        Assert.True(Backend().Save(playset).Success);

        var id = playset.Id;
        Assert.Equal("<null>", Scalar($"select pdxId from playsets where id='{id}'"));
        Assert.Equal("<null>", Scalar($"select syncState from playsets where id='{id}'"));
        Assert.Equal("private", Scalar($"select state from playsets where id='{id}'"));
        Assert.Equal("1", Scalar($"select owned from playsets where id='{id}'"));
        Assert.Equal("0", Scalar($"select isActive from playsets where id='{id}'"));
        Assert.Equal("0", Scalar($"select isRemoved from playsets where id='{id}'"));
        Assert.True(long.Parse(Scalar($"select createdOn from playsets where id='{id}'")) > 1_700_000_000_000);
        Assert.True(long.Parse(Scalar($"select updatedOn from playsets where id='{id}'")) > 1_700_000_000_000);
    }

    [Fact]
    public void Unknown_local_mod_gets_new_mods_row_like_existing_local_rows()
    {
        var folder = Path.Combine(_modFolder, "Brand New");
        var playset = NewPlayset("Neu", new PlaysetModEntry(folder, null, true, null));

        var result = Backend().Save(playset);

        Assert.True(result.Success);
        Assert.Equal("1", Scalar("select count(*) from mods where gameRegistryId='mod/Brand New.mod'"));
        Assert.Equal("local", Scalar("select source from mods where gameRegistryId='mod/Brand New.mod'"));
        Assert.Equal("ready_to_play", Scalar("select status from mods where gameRegistryId='mod/Brand New.mod'"));
        Assert.Equal(folder, Scalar("select dirPath from mods where gameRegistryId='mod/Brand New.mod'"));
        Assert.Equal("Brand New", Scalar("select displayName from mods where gameRegistryId='mod/Brand New.mod'"));
        Assert.Equal("not_applied", Scalar("select metadataStatus from mods where gameRegistryId='mod/Brand New.mod'"));
        Assert.Equal("1", Scalar($"select count(*) from playsets_mods pm join mods m on m.id=pm.modId where pm.playsetId='{playset.Id}' and m.gameRegistryId='mod/Brand New.mod'"));
    }

    [Fact]
    public void Saving_the_same_unknown_mod_twice_creates_only_one_mods_row()
    {
        var folder = Path.Combine(_modFolder, "Brand New");
        Assert.True(Backend().Save(NewPlayset("Eins", new PlaysetModEntry(folder, null, true, null))).Success);
        Assert.True(Backend().Save(NewPlayset("Zwei", new PlaysetModEntry(folder.ToUpperInvariant(), null, true, null))).Success);

        Assert.Equal("1", Scalar("select count(*) from mods where gameRegistryId='mod/Brand New.mod'"));
    }

    [Fact]
    public void Unknown_mod_folder_without_mod_file_is_refused_and_changes_nothing()
    {
        var before = Snapshot();
        var playset = NewPlayset("Neu", new PlaysetModEntry(Path.Combine(_modFolder, "No Descriptor"), null, true, null));

        var result = Backend().Save(playset);

        Assert.Equal(PlaysetStatus.ModMissing, result.Status);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void Edit_keeps_unknown_columns_and_other_playsets()
    {
        var before = Snapshot(exceptPlaysetId: "p1");
        var cloudish = Backend().LoadAll().Single(p => p.Name == "Cloudish");
        cloudish.Mods[1] = cloudish.Mods[1] with { Enabled = true };

        var result = Backend().Save(cloudish);

        Assert.True(result.Success);
        Assert.Equal(before, Snapshot(exceptPlaysetId: "p1"));
        Assert.Equal("unknown description kept", Scalar("select description from playsets where id='p1'"));
        Assert.Equal("someone", Scalar("select author from playsets where id='p1'"));
        Assert.Equal("100", Scalar("select pdxId from playsets where id='p1'"));
        Assert.Equal("unknown text kept", Scalar("select descriptionSteam from mods where id='m1'"));
        Assert.Equal("1", Scalar("select enabled from playsets_mods where playsetId='p1' and modId='m2'"));
    }

    [Fact]
    public void Mod_without_folder_survives_an_edit_by_its_id()
    {
        var cloudish = Backend().LoadAll().Single(p => p.Name == "Cloudish");
        cloudish.Mods.RemoveAt(0);

        Assert.True(Backend().Save(cloudish).Success);

        Assert.Equal("1", Scalar("select count(*) from playsets_mods where playsetId='p1' and modId='m3'"));
    }

    [Fact]
    public void Reorder_writes_positions_zero_based()
    {
        var cloudish = Backend().LoadAll().Single(p => p.Name == "Cloudish");
        cloudish.Mods.Reverse();

        Assert.True(Backend().Save(cloudish).Success);

        Assert.Equal("m3,m2,m1", Scalar("select group_concat(modId) from (select modId from playsets_mods where playsetId='p1' order by position)"));
        Assert.Equal("0,1,2", Scalar("select group_concat(position) from (select position from playsets_mods where playsetId='p1' order by position)"));
    }

    [Fact]
    public void Rename_changes_name_and_updatedOn()
    {
        var local = Backend().LoadAll().Single(p => p.Name == "Local");
        local.Name = "Umbenannt \"x\" Ä";

        Assert.True(Backend().Save(local).Success);

        Assert.Equal("Umbenannt \"x\" Ä", Scalar("select name from playsets where id='p2'"));
        Assert.True(long.Parse(Scalar("select updatedOn from playsets where id='p2'")) > 1_700_000_000_000);
    }

    [Fact]
    public void Delete_local_playset_removes_its_rows_only()
    {
        var before = Snapshot(exceptPlaysetId: "p2");

        Assert.True(Backend().Delete("p2").Success);

        Assert.Equal("0", Scalar("select count(*) from playsets where id='p2'"));
        Assert.Equal("0", Scalar("select count(*) from playsets_mods where playsetId='p2'"));
        Assert.Equal("0", Scalar("select count(*) from playsets_dlcs where playsetId='p2'"));
        Assert.Equal(before, Snapshot(exceptPlaysetId: "p2"));
    }

    [Fact]
    public void Save_with_the_guid_of_a_removed_playset_gets_a_new_id_instead_of_failing()
    {
        var removedGuid = Guid.NewGuid().ToString();
        using (var connection = Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"insert into playsets (id, name, createdOn, isRemoved, state) values ('{removedGuid}', 'Gone', 5, 1, 'removedByUser')";
            command.ExecuteNonQuery();
        }

        var playset = new PlaysetData { Id = removedGuid, Name = "Fresh" };

        var result = Backend().Save(playset);

        Assert.True(result.Success);
        Assert.NotEqual(removedGuid, playset.Id);
        Assert.Equal("Fresh", Scalar($"select name from playsets where id='{playset.Id}'"));
        Assert.Equal("Gone", Scalar($"select name from playsets where id='{removedGuid}'"));
    }

    [Fact]
    public void Delete_playset_with_pdxId_is_marked_removed_instead()
    {
        Assert.True(Backend().Delete("p3").Success);

        Assert.Equal("1", Scalar("select isRemoved from playsets where id='p3'"));
        Assert.Equal("removedByUser", Scalar("select state from playsets where id='p3'"));
        Assert.DoesNotContain(Backend().LoadAll(), p => p.Id == "p3");
    }

    [Fact]
    public void Delete_unknown_playset_is_reported()
    {
        Assert.Equal(PlaysetStatus.PlaysetMissing, Backend().Delete("nope").Status);
    }

    [Fact]
    public void SetActive_sets_isActive_one_for_target_and_zero_for_others()
    {
        Assert.True(Backend().SetActive("p1").Success);

        Assert.Equal("1", Scalar("select isActive from playsets where id='p1'"));
        Assert.Equal("0", Scalar("select isActive from playsets where id='p2'"));
        Assert.True(long.Parse(Scalar("select lastUsedAt from playsets where id='p1'")) > 1_700_000_000_000);
    }

    [Fact]
    public void Duplicate_name_is_not_checked_here_but_unknown_playset_id_creates_a_new_one()
    {
        var playset = NewPlayset("Local");

        Assert.True(Backend().Save(playset).Success);

        Assert.Equal("2", Scalar("select count(*) from playsets where name='Local'"));
    }

    [Fact]
    public void Locked_database_reports_WriteFailed_and_changes_nothing()
    {
        var before = Snapshot();
        using var holder = Open();
        using (var begin = holder.CreateCommand())
        {
            begin.CommandText = "begin exclusive";
            begin.ExecuteNonQuery();
        }

        var result = Backend().Save(NewPlayset("Neu"));

        Assert.Equal(PlaysetStatus.WriteFailed, result.Status);
        using (var rollback = holder.CreateCommand())
        {
            rollback.CommandText = "rollback";
            rollback.ExecuteNonQuery();
        }

        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void DefaultDlcs_are_all_known_dlc_ids_enabled()
    {
        var dlcs = Backend().DefaultDlcs();

        Assert.Equal(new[] { "dlc_a", "dlc_b", "dlc_c" }, dlcs.Select(d => d.Id));
        Assert.All(dlcs, d => Assert.True(d.Enabled));
    }

    [Fact]
    public void InstalledMods_lists_ready_mods_with_folder_by_name()
    {
        var mods = Backend().InstalledMods();

        Assert.Equal(new[] { "Local Two", "Workshop One" }, mods.Select(m => m.DisplayName));
        Assert.Equal(new[] { "m2", "m1" }, mods.Select(m => m.LauncherModId));
    }

    [Fact]
    public void FilesToBackUp_names_database_and_existing_wal_files()
    {
        File.WriteAllText(_db + "-wal", "x");

        Assert.Equal(new[] { _db, _db + "-wal" }, Backend().FilesToBackUp);
    }
}
