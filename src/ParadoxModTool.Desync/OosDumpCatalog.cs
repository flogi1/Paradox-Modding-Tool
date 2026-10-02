using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Desync;

/// <summary>Wo das Spiel seine OOS-Dumps ablegt.</summary>
public static class OosLocations
{
    /// <summary>Der Ordner <c>oos</c> neben dem Mod-Ordner des Spiels; <see langword="null"/> ohne Vorlage.</summary>
    public static string? FolderFor(string? modRootTemplate)
    {
        if (string.IsNullOrWhiteSpace(modRootTemplate))
        {
            return null;
        }

        try
        {
            var modRoot = Environment.ExpandEnvironmentVariables(modRootTemplate.Trim()).TrimEnd('/', '\\');
            var userFolder = Path.GetDirectoryName(modRoot);
            return string.IsNullOrEmpty(userFolder) ? null : Path.Combine(userFolder, "oos");
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            return null;
        }
    }
}

/// <summary>
/// Kurzbeschreibung eines Dumps einer Maschine (nur Metadaten und Log-Köpfe, nie der Spielstand).
/// Ein nicht lesbarer Dump hat <paramref name="Error"/> gesetzt; <paramref name="OosId16"/> ist dann leer.
/// <see cref="Open"/> öffnet genau diese Maschine, auch wenn der Pfad die Dateien beider Maschinen enthält.
/// </summary>
/// <param name="Path">Ordner oder Zip-Datei des Dumps (kann auch die Dateien anderer Maschinen enthalten).</param>
/// <param name="OosId16">16-stellige OOS-Id (Ordnername ohne Suffix).</param>
/// <param name="MachineId">Maschine, die diesen Dump geschrieben hat.</param>
/// <param name="OosMachineId">Maschine, bei der die Abweichung erkannt wurde.</param>
/// <param name="PlayerName">Spielername aus den Metadaten.</param>
/// <param name="GameCommit">Spiel-Commit aus den Metadaten.</param>
/// <param name="SessionStart">Zeittext der ersten Zeile von <c>debug_&lt;m&gt;.log</c> (Start des Spielprozesses).</param>
/// <param name="OosIdsInServerLog">Die 16-stelligen Präfixe aller <c>OOS Detected!</c>-Ids im Server-Log, in Reihenfolge ohne Doppelte.</param>
/// <param name="Error">Fehlertext, wenn der Dump nicht gelesen werden konnte.</param>
public sealed record OosDumpInfo(
    string Path,
    string OosId16,
    int MachineId,
    int OosMachineId,
    string? PlayerName,
    string? GameCommit,
    string? SessionStart,
    IReadOnlyList<string> OosIdsInServerLog,
    string? Error)
{
    /// <summary>Schreibzeit der Metadaten-Datei (UTC); nur zum Sortieren der Sitzungen, <see langword="null"/> bei Fehlern.</summary>
    internal DateTime? MetadataWriteUtc { get; init; }

    /// <summary>Öffnet den Dump dieser Maschine unter <see cref="Path"/>; der Aufrufer schließt ihn.</summary>
    /// <exception cref="OosDumpException">Der Dump ist (nicht mehr) lesbar oder enthält die Maschine nicht.</exception>
    public OosDump Open() => OosDump.Open(Path, MachineId);
}

/// <summary>Die Dumps einer OOS-Id: Referenz-Maschine und abgewichene Maschine.</summary>
/// <param name="OosId16">Gemeinsame 16-stellige OOS-Id.</param>
/// <param name="Dumps">Die vorhandenen Dumps, nach Maschinen-Id.</param>
/// <param name="OosMachineId">Maschine, bei der die Abweichung erkannt wurde.</param>
/// <param name="MissingMachineIds">Maschinen, die laut Metadaten beteiligt waren, deren Dump aber fehlt.</param>
public sealed record OosPair(
    string OosId16,
    IReadOnlyList<OosDumpInfo> Dumps,
    int OosMachineId,
    IReadOnlyList<int> MissingMachineIds)
{
    /// <summary>Der Dump mit der niedrigsten Maschinen-Id, die nicht <see cref="OosMachineId"/> ist.</summary>
    public OosDumpInfo? Reference => Dumps.Where(d => d.MachineId != OosMachineId).OrderBy(d => d.MachineId).FirstOrDefault();

    /// <summary>Der Dump der abgewichenen Maschine.</summary>
    public OosDumpInfo? Desynced => Dumps.FirstOrDefault(d => d.MachineId == OosMachineId);
}

/// <summary>Mehrere OOS-Paare derselben Mehrspieler-Sitzung.</summary>
/// <param name="Key">Kleinste OOS-Id der Sitzung.</param>
/// <param name="Pairs">Die Paare der Sitzung, neuestes zuerst.</param>
public sealed record OosSession(string Key, IReadOnlyList<OosPair> Pairs);

/// <summary>Findet OOS-Dumps, ordnet Host- und Client-Dump einander zu und fasst Paare zu Sitzungen zusammen.</summary>
public static class OosDumpCatalog
{
    /// <summary>Die <c>OOS Detected!</c>-Zeile des Server-Logs mit der vollen 32-stelligen Id (Gruppe <c>id</c>).</summary>
    internal static readonly Regex OosDetected =
        new(@"OOS Detected! OOS ID: (?<id>[0-9a-f]{32})", RegexOptions.Compiled);

    /// <summary>Der Zeitstempel am Anfang einer Log-Zeile (Gruppe <c>time</c>); auch vom Debug-Scan genutzt, um Zeilen von Mod-Namen in eckigen Klammern zu unterscheiden.</summary>
    internal static readonly Regex LineTime =
        new(@"^\[(?<time>\d{1,2}:\d{2}:\d{2})\]", RegexOptions.Compiled);

    /// <summary>
    /// Listet die Dumps in den Wurzelordnern und unter den vom Nutzer hinzugefügten Pfaden. Ein Pfad ist ein Dump,
    /// wenn er eine <c>.zip</c> ist oder <c>oos_metadata_*.txt</c> enthält; sonst werden seine Kinder eine Ebene tief
    /// geprüft. Enthält ein Pfad die Dateien mehrerer Maschinen, erscheint je Maschine ein Eintrag mit demselben Pfad.
    /// Nicht lesbare Zips erscheinen mit Fehlertext. Doppelte (gleiche Id und Maschine) erscheinen einmal,
    /// ein Ordner gewinnt gegen eine Zip.
    /// </summary>
    public static IReadOnlyList<OosDumpInfo> Scan(IEnumerable<string> roots, IEnumerable<string> addedPaths)
    {
        var candidates = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in roots.Concat(addedPaths))
        {
            foreach (var candidate in CandidatesIn(path))
            {
                if (seenPaths.Add(FullPathOrSelf(candidate)))
                {
                    candidates.Add(candidate);
                }
            }
        }

        var result = new List<OosDumpInfo>();
        var indexByKey = new Dictionary<(string OosId16, int MachineId), int>();
        foreach (var info in candidates.SelectMany(DescribeAll))
        {
            if (info.Error is not null)
            {
                result.Add(info);
                continue;
            }

            var key = (info.OosId16, info.MachineId);
            if (!indexByKey.TryGetValue(key, out var existing))
            {
                indexByKey[key] = result.Count;
                result.Add(info);
            }
            else if (Directory.Exists(info.Path) && !Directory.Exists(result[existing].Path))
            {
                result[existing] = info;
            }
        }

        return result;
    }

    /// <summary>
    /// Ordnet die Dumps nach OOS-Id zu Paaren. Erwartet werden alle Maschinen, die in den Metadaten als lokal oder
    /// abgewichen vorkommen; fehlt ihr Dump, steht sie in <see cref="OosPair.MissingMachineIds"/>. Fehlerhafte Dumps
    /// werden übersprungen.
    /// </summary>
    public static IReadOnlyList<OosPair> Pair(IReadOnlyList<OosDumpInfo> dumps)
    {
        return dumps
            .Where(d => d.Error is null && d.OosId16.Length > 0)
            .GroupBy(d => d.OosId16, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var ordered = g.OrderBy(d => d.MachineId).ToList();
                var oosMachine = ordered[0].OosMachineId;
                // Maschinen-Ids beginnen bei 1; 0 heißt "Zeile fehlt in den Metadaten".
                var expected = ordered.SelectMany(d => new[] { d.MachineId, d.OosMachineId }).Where(m => m > 0).ToHashSet();
                var present = ordered.Select(d => d.MachineId).ToHashSet();
                var missing = expected.Where(m => !present.Contains(m)).Order().ToList();
                return new OosPair(g.Key, ordered, oosMachine, missing);
            })
            .ToList();
    }

    /// <summary>
    /// Fasst Paare einer Mehrspieler-Sitzung zusammen (Union-Find). Zwei Paare gehören zusammen, wenn sie einen
    /// Spiel-Commit teilen und entweder das Server-Log des einen die Id des anderen nennt oder dieselbe Maschine in
    /// beiden denselben Startzeitpunkt des Spielprozesses hat. Der Commit allein verbindet nie. Neueste Sitzung zuerst.
    /// </summary>
    public static IReadOnlyList<OosSession> Group(IReadOnlyList<OosPair> pairs)
    {
        var parent = Enumerable.Range(0, pairs.Count).ToArray();
        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        for (var i = 0; i < pairs.Count; i++)
        {
            for (var j = i + 1; j < pairs.Count; j++)
            {
                if (SameSession(pairs[i], pairs[j]))
                {
                    parent[Find(i)] = Find(j);
                }
            }
        }

        return Enumerable.Range(0, pairs.Count)
            .GroupBy(Find)
            .Select(g =>
            {
                var members = g.Select(i => pairs[i])
                    .OrderByDescending(WriteTimeOf)
                    .ThenBy(p => p.OosId16, StringComparer.Ordinal)
                    .ToList();
                var key = members.Select(p => p.OosId16).Order(StringComparer.Ordinal).First();
                return (Session: new OosSession(key, members), Time: members.Max(WriteTimeOf));
            })
            .OrderByDescending(s => s.Time)
            .ThenBy(s => s.Session.Key, StringComparer.Ordinal)
            .Select(s => s.Session)
            .ToList();
    }

    private static bool SameSession(OosPair a, OosPair b)
    {
        var commitsA = a.Dumps.Select(d => d.GameCommit).Where(c => c is not null).ToHashSet();
        if (!b.Dumps.Any(d => d.GameCommit is not null && commitsA.Contains(d.GameCommit)))
        {
            return false;
        }

        if (a.Dumps.Any(d => d.OosIdsInServerLog.Contains(b.OosId16))
            || b.Dumps.Any(d => d.OosIdsInServerLog.Contains(a.OosId16)))
        {
            return true;
        }

        return a.Dumps.Any(da => da.SessionStart is not null
            && b.Dumps.Any(db => db.MachineId == da.MachineId && db.SessionStart == da.SessionStart));
    }

    private static DateTime WriteTimeOf(OosPair pair) =>
        (pair.Reference ?? pair.Desynced ?? pair.Dumps.FirstOrDefault())?.MetadataWriteUtc ?? DateTime.MinValue;

    /// <summary>Die Dump-Kandidaten, die ein Pfad liefert: er selbst oder seine Dump-Kinder (eine Ebene).</summary>
    private static IEnumerable<string> CandidatesIn(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        try
        {
            if (File.Exists(path))
            {
                return IsZip(path) ? [path] : [];
            }

            if (!Directory.Exists(path))
            {
                return [];
            }

            if (HoldsMetadata(path))
            {
                return [path];
            }

            return Directory.EnumerateFileSystemEntries(path)
                .Where(child => Directory.Exists(child) ? HoldsMetadata(child) : IsZip(child))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    private static bool IsZip(string path) => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    private static bool HoldsMetadata(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "oos_metadata_*.txt").Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string FullPathOrSelf(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    /// <summary>Je Maschine mit Metadaten-Datei im Pfad ein Eintrag; jede Maschine wird für sich gelesen, ein Fehler trifft nur ihren Eintrag.</summary>
    private static IEnumerable<OosDumpInfo> DescribeAll(string path)
    {
        IReadOnlyList<int> machines;
        try
        {
            using var dump = OosDump.Open(path);
            machines = dump.AvailableMachineIds;
        }
        catch (OosDumpException ex)
        {
            return [Failed(path, ex.Message)];
        }

        return machines.Select(machine => Describe(path, machine)).ToList();
    }

    private static OosDumpInfo Describe(string path, int machineId)
    {
        try
        {
            using var dump = OosDump.Open(path, machineId);
            var metadata = dump.Metadata;
            if (metadata.OosId16.Length == 0)
            {
                return Failed(path, "Die Metadaten enthalten keinen oos_folder_name.");
            }

            return new OosDumpInfo(
                dump.SourcePath,
                metadata.OosId16,
                dump.MachineId,
                metadata.OosMachineId,
                metadata.PlayerName,
                metadata.GameCommit,
                ReadSessionStart(dump),
                ReadServerLogIds(dump),
                null)
            {
                MetadataWriteUtc = dump.StampOf(DumpFileKind.Metadata).LastWriteUtc,
            };
        }
        catch (OosDumpException ex)
        {
            return Failed(path, ex.Message);
        }
    }

    private static OosDumpInfo Failed(string path, string error) =>
        new(path, "", 0, 0, null, null, null, [], error);

    /// <summary>Zeittext der ersten Zeile von <c>debug_&lt;m&gt;.log</c>; <see langword="null"/>, wenn es fehlt oder nicht passt.</summary>
    private static string? ReadSessionStart(OosDump dump)
    {
        if (!dump.Has(DumpFileKind.Debug))
        {
            return null;
        }

        try
        {
            using var reader = new StreamReader(dump.OpenRead(DumpFileKind.Debug), Encoding.UTF8);
            var match = LineTime.Match(reader.ReadLine() ?? "");
            return match.Success ? match.Groups["time"].Value : null;
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Die 16-stelligen Präfixe aller <c>OOS Detected!</c>-Ids im Server-Log.</summary>
    private static IReadOnlyList<string> ReadServerLogIds(OosDump dump)
    {
        var ids = new List<string>();
        if (!dump.Has(DumpFileKind.DedicatedServer))
        {
            return ids;
        }

        try
        {
            using var reader = new StreamReader(dump.OpenRead(DumpFileKind.DedicatedServer), Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!line.Contains("OOS Detected!", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = OosDetected.Match(line);
                if (match.Success)
                {
                    var id = match.Groups["id"].Value[..16];
                    if (!ids.Contains(id))
                    {
                        ids.Add(id);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is OosDumpException or IOException or InvalidDataException)
        {
            // Teilweise gelesene Ids bleiben erhalten; ein unlesbares Log macht den Dump nicht ungültig.
        }

        return ids;
    }
}
