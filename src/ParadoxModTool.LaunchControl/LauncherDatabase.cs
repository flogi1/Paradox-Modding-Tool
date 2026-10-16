using Microsoft.Data.Sqlite;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Liest Playsets aus launcher-v2.sqlite, der Datenbank des Paradox-Launchers v2 (EU IV, CK3, Stellaris). Nur lesend: PMT schreibt nie
/// in diese Datei, weil sie allein dem Launcher gehört und ihr Schema sich mit jedem Launcher-Update ändern kann. Hält der Launcher
/// die Datenbank gesperrt, wird eine temporäre Kopie gelesen.
/// </summary>
internal static class LauncherDatabase
{
    private const string PlaysetsQuery = """
        select p.name, p.isActive,
               (select count(*) from playsets_mods pm where pm.playsetId = p.id),
               (select count(*) from playsets_mods pm where pm.playsetId = p.id and pm.enabled),
               p.id
        from playsets p
        where not coalesce(p.isRemoved, 0)
        order by p.createdOn, p.name
        """;

    private const string ModsQuery = """
        select m.dirPath, pm.enabled
        from playsets p
        join playsets_mods pm on pm.playsetId = p.id
        join mods m on m.id = pm.modId
        where p.name = $name and not coalesce(p.isRemoved, 0)
        order by pm.position
        """;

    public static IReadOnlyList<PlaysetInfo> ListPlaysets(string path) =>
        Read(path, connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = PlaysetsQuery;
            using var reader = command.ExecuteReader();
            var result = new List<PlaysetInfo>();
            while (reader.Read())
            {
                if (reader.IsDBNull(0) || string.IsNullOrWhiteSpace(reader.GetString(0)))
                {
                    continue;
                }

                result.Add(new PlaysetInfo
                {
                    Name = reader.GetString(0),
                    IsActive = !reader.IsDBNull(1) && reader.GetBoolean(1),
                    ModCount = reader.GetInt32(2),
                    EnabledModCount = reader.GetInt32(3),
                    Id = reader.IsDBNull(4) ? null : reader.GetString(4),
                });
            }

            return (IReadOnlyList<PlaysetInfo>)result;
        }) ?? Array.Empty<PlaysetInfo>();

    public static IReadOnlyList<PlaysetMod> ListPlaysetMods(string path, string playsetName) =>
        Read(path, connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = ModsQuery;
            command.Parameters.AddWithValue("$name", playsetName);
            using var reader = command.ExecuteReader();
            var result = new List<PlaysetMod>();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0) && reader.GetString(0) is { Length: > 0 } folder)
                {
                    result.Add(new PlaysetMod(folder, reader.IsDBNull(1) || reader.GetBoolean(1)));
                }
            }

            return (IReadOnlyList<PlaysetMod>)result;
        }) ?? Array.Empty<PlaysetMod>();

    /// <summary>Führt <paramref name="query"/> auf der Datenbank aus; ist sie gesperrt, auf einer Kopie. <see langword="null"/>, wenn beides scheitert.</summary>
    internal static T? Read<T>(string path, Func<SqliteConnection, T> query) where T : class
    {
        try
        {
            return Query(path, query);
        }
        catch (SqliteException ex) when (IsBusy(ex))
        {
            return ReadCopy(path, query);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidCastException)
        {
            return null;
        }
    }

    private static T? ReadCopy<T>(string path, Func<SqliteConnection, T> query) where T : class
    {
        var folder = Path.Combine(Path.GetTempPath(), "pmt-launcher-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(folder);
            var copy = Path.Combine(folder, Path.GetFileName(path));
            File.Copy(path, copy);
            if (File.Exists(path + "-wal"))
            {
                File.Copy(path + "-wal", copy + "-wal");
            }

            return Query(copy, query);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Eine liegengebliebene Kopie im Temp-Ordner schadet nicht.
            }
        }
    }

    private static T Query<T>(string path, Func<SqliteConnection, T> query)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false, DefaultTimeout = 1 };
        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return query(connection);
    }

    // SQLITE_BUSY (5) und SQLITE_LOCKED (6): Der Launcher schreibt gerade.
    private static bool IsBusy(SqliteException ex) => ex.SqliteErrorCode is 5 or 6;
}
