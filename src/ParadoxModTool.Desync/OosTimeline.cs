using System.Globalization;
using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Desync;

/// <summary>Warum ein Eintrag der Zeitleiste ein Hinweis auf die Ursache ist.</summary>
public enum LeadKind
{
    None,
    /// <summary>Zeile "Invalid command." im Multiplayer-Log.</summary>
    InvalidCommand,
    /// <summary>Skriptfehler (Quelle <c>jomini_script_system</c> oder Fehler mit Dateiverweis).</summary>
    ScriptError,
}

/// <summary>
/// Ein Eintrag der gemeinsamen Zeitleiste. <c>GameDate</c> ist das Spieldatum des letzten Ticks bis zur Zeit der Zeile (null ohne Ticks).
/// <c>Error</c> ist gesetzt, wenn der Fehlerkatalog die Zeile erkennt. <c>OnlyThisMachine</c>: dieselbe Meldung kommt im Fenster auf keiner
/// anderen Maschine vor.
/// </summary>
public sealed record TimelineEntry(
    int MachineId,
    string? Role,
    DumpFileKind Source,
    LogLine Line,
    string? GameDate,
    ErrorEntry? Error,
    LeadKind Lead,
    bool OnlyThisMachine);

/// <summary>Führt die Logs mehrerer Maschinen zu einer nach Spielzeit sortierten Zeitleiste vor dem OOS zusammen.</summary>
public static class OosTimeline
{
    /// <summary>Anzahl Zeilen je Log, wenn ein Dump keine Tick-Zeilen hat.</summary>
    private const int FallbackTail = 200;

    private static readonly DumpFileKind[] LogKinds = { DumpFileKind.Error, DumpFileKind.Game, DumpFileKind.Multiplayer };

    /// <summary>
    /// Liest <c>Error</c>, <c>Game</c> und <c>Multiplayer</c> jedes Dumps (je mit eigener Wanduhr) und behält die Zeilen ab
    /// <see cref="TickMap.WindowStart"/>. Das Fenster endet am OOS des Paares (die Id aus den Metadaten), auch wenn das Server-Log frühere OOS enthält.
    /// Ohne Ticks (oder ohne OOS-Tick) zählen die letzten 200 Zeilen je Log, ohne Spieldatum.
    /// Sortiert nach Spieldatum, dann Maschine, dann Zeilenfolge.
    /// </summary>
    public static IReadOnlyList<TimelineEntry> Build(IReadOnlyList<OosDump> dumps, ErrorCatalog catalog, int gameDays)
    {
        var collected = new List<Collected>();
        foreach (var dump in dumps)
            Collect(dump, catalog, gameDays, collected);

        var onlyHere = OnlyOnThisMachine(collected);
        return collected
            .Select((c, index) => (Item: c, Only: onlyHere[index]))
            .OrderBy(x => x.Item.GameDate, DateComparer.Instance)
            .ThenBy(x => x.Item.MachineId)
            .ThenBy(x => x.Item.Wall)
            .ThenBy(x => x.Item.Source)
            .ThenBy(x => x.Item.Line.LineNumber)
            .Select(x => new TimelineEntry(x.Item.MachineId, x.Item.Role, x.Item.Source, x.Item.Line, x.Item.GameDate,
                x.Item.Error, x.Item.Lead, x.Only))
            .ToList();
    }

    private static void Collect(OosDump dump, ErrorCatalog catalog, int gameDays, List<Collected> into)
    {
        var map = dump.Has(DumpFileKind.DedicatedServer)
            ? TickMap.Parse(LogReader.Parse(dump.ReadLines(DumpFileKind.DedicatedServer)), dump.Metadata.OosId16)
            : null;
        var windowStart = map?.WindowStart(gameDays);

        foreach (var kind in LogKinds)
        {
            if (!dump.Has(kind))
                continue;

            var lines = LogReader.Parse(dump.ReadLines(kind));
            var wall = WallClock.Absolute(lines);
            var wallByLine = new Dictionary<int, long>(lines.Count);
            for (var i = 0; i < lines.Count; i++)
                wallByLine[lines[i].LineNumber] = wall[i];

            IReadOnlyList<LogLine> selected;
            if (windowStart is { } start)
            {
                selected = lines.Where((_, i) => wall[i] >= start).ToList();
            }
            else
            {
                selected = lines.Count > FallbackTail ? lines.GetRange(lines.Count - FallbackTail, FallbackTail) : lines;
            }

            foreach (var error in ErrorClassifier.Classify(selected, catalog))
            {
                var seconds = wallByLine[error.Line.LineNumber];
                var date = windowStart is null ? null : map!.DateAt(seconds);
                into.Add(new Collected(dump.MachineId, map?.Role, kind, error.Line, date,
                    error.Kind is null ? null : error, LeadOf(kind, error), seconds));
            }
        }
    }

    private static LeadKind LeadOf(DumpFileKind kind, ErrorEntry entry)
    {
        if (kind == DumpFileKind.Multiplayer && entry.Line.Message.StartsWith("Invalid command.", StringComparison.Ordinal))
            return LeadKind.InvalidCommand;

        if (entry.Line.Source.StartsWith("jomini_script_system", StringComparison.Ordinal)
            || (entry.Severity == ErrorSeverity.Error && entry.HasFileReference))
            return LeadKind.ScriptError;

        return LeadKind.None;
    }

    /// <summary>
    /// Vergleicht die Meldungstexte (ordinal) als Multimengen zwischen den Maschinen: Das n-te Vorkommen ist "nur hier", wenn jede andere
    /// Maschine die Meldung weniger als n-mal hat. Mit nur einer Maschine ist nichts "nur hier".
    /// </summary>
    private static bool[] OnlyOnThisMachine(List<Collected> items)
    {
        var result = new bool[items.Count];
        var machines = items.Select(i => i.MachineId).Distinct().ToList();
        if (machines.Count < 2)
            return result;

        var counts = new Dictionary<(int Machine, string Message), int>();
        foreach (var item in items)
        {
            var key = (item.MachineId, item.Line.Message);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var seen = new Dictionary<(int Machine, string Message), int>();
        // Vorkommen in Zeilenfolge der Maschine zählen, damit "n-tes" eindeutig ist.
        var order = Enumerable.Range(0, items.Count)
            .OrderBy(i => items[i].MachineId).ThenBy(i => items[i].Wall)
            .ThenBy(i => items[i].Source).ThenBy(i => items[i].Line.LineNumber);
        foreach (var index in order)
        {
            var item = items[index];
            var key = (item.MachineId, item.Line.Message);
            var occurrence = seen.GetValueOrDefault(key);
            seen[key] = occurrence + 1;
            result[index] = machines.Where(m => m != item.MachineId)
                .All(m => counts.GetValueOrDefault((m, item.Line.Message)) <= occurrence);
        }

        return result;
    }

    private sealed record Collected(int MachineId, string? Role, DumpFileKind Source, LogLine Line, string? GameDate,
        ErrorEntry? Error, LeadKind Lead, long Wall);

    /// <summary>Ordnet Spieldaten zahlenweise (<c>1425.2.4.8</c> &lt; <c>1425.2.4.10</c>); null kommt zuerst.</summary>
    internal sealed class DateComparer : IComparer<string?>
    {
        public static readonly DateComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x is null || y is null)
                return (x is null ? 0 : 1) - (y is null ? 0 : 1);

            var a = x.Split('.');
            var b = y.Split('.');
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var left = i < a.Length ? Number(a[i]) : 0;
                var right = i < b.Length ? Number(b[i]) : 0;
                if (left != right)
                    return left.CompareTo(right);
            }

            return 0;
        }

        private static long Number(string part) =>
            long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
