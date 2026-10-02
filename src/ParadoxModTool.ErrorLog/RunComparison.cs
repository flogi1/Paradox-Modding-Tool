using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

public enum RunStatus
{
    New,
    Unchanged,
    Fixed,
}

public sealed record RunGroupDiff(string Key, RunStatus Status, int CurrentCount, int PreviousCount, ErrorEntry Sample);

public sealed class RunDiff
{
    internal RunDiff(IReadOnlyDictionary<string, RunGroupDiff> byKey) => ByKey = byKey;

    public IReadOnlyDictionary<string, RunGroupDiff> ByKey { get; }

    public IEnumerable<RunGroupDiff> Fixed => ByKey.Values.Where(d => d.Status == RunStatus.Fixed);

    public int NewCount => ByKey.Values.Count(d => d.Status == RunStatus.New);

    public int FixedCount => ByKey.Values.Count(d => d.Status == RunStatus.Fixed);

    public RunStatus StatusOf(ErrorEntry entry) =>
        ByKey.TryGetValue(RunComparison.KeyOf(entry), out var diff) ? diff.Status : RunStatus.New;
}

/// <summary>
/// Vergleicht zwei Läufe (Plan-Ruling 3). Schlüssel: Code + Name (ohne Namen die Meldung bzw. das Detail ohne Zeilennummern) + Log-Pfad.
/// Zeilennummern, Zeitstempel und Quellnummern zählen nicht, damit ein Fix, der Zeilen verschiebt, nicht als "behoben + neu" erscheint.
/// </summary>
public static class RunComparison
{
    private static readonly Regex LineNumbers = new(@"(?::\d+|line:?\s*\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string KeyOf(ErrorEntry entry)
    {
        var text = entry.Name ?? LineNumbers.Replace(entry.Kind is null ? entry.Message : entry.Detail ?? string.Empty, string.Empty);
        return $"{entry.Code}|{text}|{entry.FilePath}";
    }

    public static RunDiff Compare(IEnumerable<ErrorEntry> current, IEnumerable<ErrorEntry> previous)
    {
        var now = Count(current);
        var before = Count(previous);
        var result = new Dictionary<string, RunGroupDiff>(StringComparer.Ordinal);
        foreach (var (key, (count, sample)) in now)
        {
            var previousCount = before.TryGetValue(key, out var old) ? old.Count : 0;
            result[key] = new RunGroupDiff(key, previousCount == 0 ? RunStatus.New : RunStatus.Unchanged, count, previousCount, sample);
        }

        foreach (var (key, (count, sample)) in before)
        {
            if (!now.ContainsKey(key))
            {
                result[key] = new RunGroupDiff(key, RunStatus.Fixed, 0, count, sample);
            }
        }

        return new RunDiff(result);
    }

    private static Dictionary<string, (int Count, ErrorEntry Sample)> Count(IEnumerable<ErrorEntry> entries)
    {
        var result = new Dictionary<string, (int Count, ErrorEntry Sample)>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var key = KeyOf(entry);
            result[key] = result.TryGetValue(key, out var existing) ? (existing.Count + entry.Lines.Count, existing.Sample) : (entry.Lines.Count, entry);
        }

        return result;
    }
}
