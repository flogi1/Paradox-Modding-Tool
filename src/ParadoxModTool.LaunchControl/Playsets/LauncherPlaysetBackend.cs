using System.Globalization;
using Microsoft.Data.Sqlite;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Playsets in launcher-v2.sqlite (CK III, Vic 3, EU IV). Schreibt nur die Tabellen playsets, playsets_mods, playsets_dlcs und neue Zeilen in mods; alle
/// anderen Spalten und Zeilen bleiben unberührt. Jede Änderung läuft in einer Transaktion (bei gesperrter Datenbank: Rollback, nichts ändert sich).
/// Gemessen am 2026-10-16 (CK III): Der Launcher übernimmt ein so angelegtes Playset samt neu eingetragener lokaler Mod ohne Fehler.
/// </summary>
public sealed class LauncherPlaysetBackend : IPlaysetBackend
{
    private readonly string _databasePath;
    private readonly string _gameModFolder;

    /// <param name="databasePath">launcher-v2.sqlite.</param>
    /// <param name="gameModFolder">Documents/Paradox Interactive/&lt;Spiel&gt;/mod, daraus entsteht <c>mod/&lt;Name&gt;.mod</c> für neue Mods.</param>
    public LauncherPlaysetBackend(string databasePath, string gameModFolder)
    {
        _databasePath = databasePath;
        _gameModFolder = gameModFolder;
    }

    public IReadOnlyList<string> FilesToBackUp =>
        new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" }.Where((f, i) => i == 0 || File.Exists(f)).ToList();

    public bool UsesLauncherDatabase => true;

    public IReadOnlyList<PlaysetData> LoadAll() =>
        LauncherDatabase.Read(_databasePath, connection =>
        {
            var result = new List<PlaysetData>();
            var byId = new Dictionary<string, PlaysetData>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "select id, name, isActive, pdxId, syncState from playsets where not coalesce(isRemoved, 0) order by createdOn, name";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(0) || reader.IsDBNull(1) || string.IsNullOrWhiteSpace(reader.GetString(1)))
                    {
                        continue;
                    }

                    var playset = new PlaysetData
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        IsActive = !reader.IsDBNull(2) && reader.GetBoolean(2),
                        IsCloud = !reader.IsDBNull(3) && !string.Equals(reader.IsDBNull(4) ? null : reader.GetString(4), "NOT_ELIGIBLE", StringComparison.Ordinal),
                    };
                    byId[playset.Id] = playset;
                    result.Add(playset);
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    select pm.playsetId, m.id, m.displayName, m.dirPath, pm.enabled
                    from playsets_mods pm join mods m on m.id = pm.modId
                    order by pm.playsetId, pm.position
                    """;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (byId.TryGetValue(reader.GetString(0), out var playset))
                    {
                        playset.Mods.Add(new PlaysetModEntry(
                            reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            reader.IsDBNull(2) ? null : reader.GetString(2),
                            reader.IsDBNull(4) || reader.GetBoolean(4),
                            reader.GetString(1)));
                    }
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "select playsetId, dlcId, enabled from playsets_dlcs order by rowid";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (byId.TryGetValue(reader.GetString(0), out var playset))
                    {
                        playset.Dlcs.Add(new PlaysetDlcEntry(reader.GetString(1), !reader.IsDBNull(2) && reader.GetBoolean(2)));
                    }
                }
            }

            return (IReadOnlyList<PlaysetData>)result;
        }) ?? Array.Empty<PlaysetData>();

    public IReadOnlyList<PlaysetModEntry> InstalledMods() =>
        LauncherDatabase.Read(_databasePath, connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "select id, displayName, dirPath from mods where status = 'ready_to_play' and dirPath is not null and dirPath <> '' order by displayName, dirPath";
            using var reader = command.ExecuteReader();
            var result = new List<PlaysetModEntry>();
            while (reader.Read())
            {
                var path = reader.GetString(2);
                result.Add(new PlaysetModEntry(path, reader.IsDBNull(1) ? Path.GetFileName(path.TrimEnd('\\', '/')) : reader.GetString(1), true, reader.GetString(0)));
            }

            return (IReadOnlyList<PlaysetModEntry>)result;
        }) ?? Array.Empty<PlaysetModEntry>();

    public IReadOnlyList<PlaysetDlcEntry> DefaultDlcs() =>
        LauncherDatabase.Read(_databasePath, connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "select distinct dlcId from playsets_dlcs order by dlcId";
            using var reader = command.ExecuteReader();
            var result = new List<PlaysetDlcEntry>();
            while (reader.Read())
            {
                result.Add(new PlaysetDlcEntry(reader.GetString(0), true));
            }

            return (IReadOnlyList<PlaysetDlcEntry>)result;
        }) ?? Array.Empty<PlaysetDlcEntry>();

    public PlaysetResult Save(PlaysetData playset) =>
        Write((connection, transaction) =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var exists = Scalar(connection, transaction, "select count(*) from playsets where id = $id and not coalesce(isRemoved, 0)", ("$id", playset.Id)) > 0;
            if (!exists)
            {
                // Eine Kennung, die schon eine Zeile trägt (auch eine als entfernt markierte), wäre ein Primärschlüssel-Fehler: dann bekommt das Playset eine neue.
                playset.Id = Guid.TryParse(playset.Id, out var parsed) && Scalar(connection, transaction, "select count(*) from playsets where id = $id", ("$id", parsed.ToString())) == 0
                    ? parsed.ToString()
                    : Guid.NewGuid().ToString();
                Execute(connection, transaction,
                    "insert into playsets (id, name, isActive, createdOn, updatedOn, isRemoved, state, owned, author) values ($id, $name, 0, $now, $now, 0, 'private', 1, '')",
                    ("$id", playset.Id), ("$name", playset.Name), ("$now", now));
            }
            else
            {
                Execute(connection, transaction, "update playsets set name = $name, updatedOn = $now where id = $id", ("$id", playset.Id), ("$name", playset.Name), ("$now", now));
            }

            var knownMods = ReadModFolders(connection, transaction);
            var resolved = new List<(string ModId, bool Enabled)>();
            foreach (var mod in playset.Mods)
            {
                var id = ResolveModId(connection, transaction, mod, knownMods, out var failure);
                if (id is null)
                {
                    return failure!;
                }

                resolved.Add((id, mod.Enabled));
            }

            Execute(connection, transaction, "delete from playsets_mods where playsetId = $id", ("$id", playset.Id));
            for (var position = 0; position < resolved.Count; position++)
            {
                Execute(connection, transaction, "insert into playsets_mods (playsetId, modId, enabled, position) values ($p, $m, $e, $pos)",
                    ("$p", playset.Id), ("$m", resolved[position].ModId), ("$e", resolved[position].Enabled ? 1 : 0), ("$pos", position));
            }

            Execute(connection, transaction, "delete from playsets_dlcs where playsetId = $id", ("$id", playset.Id));
            foreach (var dlc in playset.Dlcs)
            {
                Execute(connection, transaction, "insert into playsets_dlcs (playsetId, dlcId, enabled) values ($p, $d, $e)",
                    ("$p", playset.Id), ("$d", dlc.Id), ("$e", dlc.Enabled ? 1 : 0));
            }

            return PlaysetResult.Ok;
        });

    public PlaysetResult Delete(string id) =>
        Write((connection, transaction) =>
        {
            if (Scalar(connection, transaction, "select count(*) from playsets where id = $id and not coalesce(isRemoved, 0)", ("$id", id)) == 0)
            {
                return new PlaysetResult(PlaysetStatus.PlaysetMissing, id);
            }

            if (Scalar(connection, transaction, "select count(*) from playsets where id = $id and pdxId is not null", ("$id", id)) > 0)
            {
                // Mit Paradox-Konto verknüpft: wie der Launcher als entfernt markieren statt die Zeile zu löschen.
                Execute(connection, transaction, "update playsets set isRemoved = 1, state = 'removedByUser', isActive = 0, updatedOn = $now where id = $id",
                    ("$id", id), ("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            }
            else
            {
                Execute(connection, transaction, "delete from playsets_mods where playsetId = $id", ("$id", id));
                Execute(connection, transaction, "delete from playsets_dlcs where playsetId = $id", ("$id", id));
                Execute(connection, transaction, "delete from playsets where id = $id", ("$id", id));
            }

            return PlaysetResult.Ok;
        });

    public PlaysetResult SetActive(string id) =>
        Write((connection, transaction) =>
        {
            if (Scalar(connection, transaction, "select count(*) from playsets where id = $id and not coalesce(isRemoved, 0)", ("$id", id)) == 0)
            {
                return new PlaysetResult(PlaysetStatus.PlaysetMissing, id);
            }

            Execute(connection, transaction, "update playsets set isActive = 0 where isActive = 1 and id <> $id", ("$id", id));
            Execute(connection, transaction, "update playsets set isActive = 1, lastUsedAt = $now where id = $id", ("$id", id), ("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            return PlaysetResult.Ok;
        });

    /// <summary>Ordner (vergleichbar geschrieben) mit der Id der zugehörigen <c>mods</c>-Zeile.</summary>
    private static Dictionary<string, string> ReadModFolders(SqliteConnection connection, SqliteTransaction transaction)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select id, dirPath from mods where dirPath is not null and dirPath <> ''";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.TryAdd(Normalize(reader.GetString(1)), reader.GetString(0));
        }

        return result;
    }

    /// <summary>Die Id der Mod in <c>mods</c>: nach Id, dann nach Ordner; sonst wird für eine lokale Mod mit .mod-Datei eine Zeile angelegt.</summary>
    private string? ResolveModId(SqliteConnection connection, SqliteTransaction transaction, PlaysetModEntry mod, Dictionary<string, string> knownMods, out PlaysetResult? failure)
    {
        failure = null;
        if (mod.LauncherModId is { Length: > 0 } byId && Scalar(connection, transaction, "select count(*) from mods where id = $id", ("$id", byId)) > 0)
        {
            return byId;
        }

        var key = Normalize(mod.Path);
        if (key.Length > 0 && knownMods.TryGetValue(key, out var known))
        {
            return known;
        }

        var folder = mod.Path.Replace('/', '\\').TrimEnd('\\');
        var name = Path.GetFileName(folder);
        var parent = Path.GetDirectoryName(folder);
        var descriptor = parent is null ? null : Path.Combine(parent, name + ".mod");
        if (key.Length == 0 || descriptor is null || !File.Exists(descriptor) || !string.Equals(Normalize(parent!), Normalize(_gameModFolder), StringComparison.OrdinalIgnoreCase))
        {
            failure = new PlaysetResult(PlaysetStatus.ModMissing, descriptor ?? mod.Path);
            return null;
        }

        var id = Guid.NewGuid().ToString();
        Execute(connection, transaction,
            """
            insert into mods (id, gameRegistryId, displayName, version, tags, dirPath, status, source, cause, createdDate, size, isMetadataApplied, metadataStatus, keepLatest)
            values ($id, $registry, $name, null, '[]', $dir, 'ready_to_play', 'local', '', $created, 0, 0, 'not_applied', 1)
            """,
            ("$id", id), ("$registry", "mod/" + name + ".mod"), ("$name", mod.DisplayName ?? name), ("$dir", folder), ("$created", DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        knownMods[key] = id;
        return id;
    }

    private static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');

    private PlaysetResult Write(Func<SqliteConnection, SqliteTransaction, PlaysetResult> action)
    {
        if (!File.Exists(_databasePath))
        {
            return new PlaysetResult(PlaysetStatus.FileMissing, _databasePath);
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder { DataSource = _databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false, DefaultTimeout = 2 };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON";
                pragma.ExecuteNonQuery();
            }

            using var transaction = connection.BeginTransaction();
            var result = action(connection, transaction);
            if (result.Success)
            {
                transaction.Commit();
            }
            else
            {
                transaction.Rollback();
            }

            return result;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidCastException)
        {
            return new PlaysetResult(PlaysetStatus.WriteFailed, ex.Message);
        }
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
