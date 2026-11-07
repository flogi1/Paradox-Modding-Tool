using System.Globalization;
using System.Text.RegularExpressions;
using ParadoxModTool.ErrorLog;

namespace ParadoxModTool.Performance;

/// <summary>Die Fehler einer <c>error.log</c> je Datei und Zeile: Wie oft meldet das Spiel eine Stelle, die im Profiling heiß ist.</summary>
public sealed class ErrorHotspots
{
    private static readonly Regex FallbackPattern = new(
        @"\bat (?<file>[^\s:]+\.(txt|gui|yml)):(?<line>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly ErrorHotspots Empty = new(new Dictionary<string, Dictionary<int, List<ErrorEntry>>>(), 0);

    private static readonly IReadOnlyList<ErrorEntry> NoEntries = Array.Empty<ErrorEntry>();

    private readonly Dictionary<string, Dictionary<int, List<ErrorEntry>>> _byFile;

    private ErrorHotspots(Dictionary<string, Dictionary<int, List<ErrorEntry>>> byFile, int total)
    {
        _byFile = byFile;
        Total = total;
    }

    /// <summary>Anzahl der Einträge mit Fundstelle (Datei und Zeile).</summary>
    public int Total { get; }

    /// <summary>
    /// Liest das Log und ordnet die Einträge nach Datei und Zeile. Nicht erkannte Einträge ohne Fundstelle bekommen sie aus einem
    /// <c>at Datei:Zeile</c> in der Meldung; Einträge ganz ohne Stelle zählen nicht mit.
    /// </summary>
    public static ErrorHotspots Load(string errorLogPath, ErrorCatalog catalog) =>
        FromEntries(ErrorClassifier.Classify(LogReader.ReadFile(errorLogPath), catalog));

    /// <summary>
    /// Ordnet schon klassifizierte Einträge nach Datei und Zeile, genau wie <see cref="Load"/> (das Fehlerprotokoll hat sie bereits gelesen
    /// und gibt nur die Einträge der Mod-Seite weiter). Ohne Fundstelle zählt ein Eintrag nicht mit.
    /// </summary>
    public static ErrorHotspots FromEntries(IEnumerable<ErrorEntry> entries)
    {
        var byFile = new Dictionary<string, Dictionary<int, List<ErrorEntry>>>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        foreach (var entry in entries)
        {
            if (!TryPlace(entry, out var path, out var line))
            {
                continue;
            }

            if (!byFile.TryGetValue(path, out var lines))
            {
                lines = new Dictionary<int, List<ErrorEntry>>();
                byFile[path] = lines;
            }

            if (!lines.TryGetValue(line, out var list))
            {
                list = new List<ErrorEntry>();
                lines[line] = list;
            }

            list.Add(entry);
            total++;
        }

        return new ErrorHotspots(byFile, total);
    }

    /// <summary>
    /// Die Stellen der Mod-Seite eines geladenen Fehlerprotokolls (Fehler-Hotspots im Fehlerprotokoll-Fenster): Einträge, die die Sitzung einer
    /// eigenen oder fremden Mod zuordnet, dazu Meldungen, die der Katalog nicht kennt, deren <c>at Datei:Zeile</c> aber auf eine Datei einer Mod
    /// zeigt (aufgelöst über <see cref="ErrorLogSession.Resolve"/>, je Pfad einmal). Vanilla-, Engine- und nicht zuordenbare Meldungen zählen nicht.
    /// </summary>
    public static ErrorHotspots ModSideOf(ErrorLogSession session)
    {
        var modFiles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool IsModFile(string path)
        {
            if (!modFiles.TryGetValue(path, out var isMod))
            {
                isMod = session.Resolve(path) is { IsVanilla: false };
                modFiles[path] = isMod;
            }

            return isMod;
        }

        return FromEntries(session.Entries.Where(entry => session.AttributionOf(entry).Bucket switch
        {
            ErrorBucket.OwnMod or ErrorBucket.OtherMod => true,
            ErrorBucket.Unmatched => TryFallbackPlace(entry.Message, out var path, out _) && IsModFile(path),
            _ => false,
        }));
    }

    /// <summary>Stelle aus einem <c>at Datei:Zeile</c> in der Meldung (Pfad mit <c>/</c>); für Meldungen, die der Katalog nicht kennt.</summary>
    public static bool TryFallbackPlace(string message, out string path, out int line)
    {
        var match = FallbackPattern.Match(message);
        if (match.Success && int.TryParse(match.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out line))
        {
            path = Normalize(match.Groups["file"].Value);
            return true;
        }

        path = string.Empty;
        line = 0;
        return false;
    }

    /// <summary>Die Einträge an dieser Stelle (Pfad ohne Beachtung der Groß-/Kleinschreibung, Zeile genau).</summary>
    public IReadOnlyList<ErrorEntry> At(string path, int line) =>
        _byFile.TryGetValue(Normalize(path), out var lines) && lines.TryGetValue(line, out var list) ? list : NoEntries;

    public int CountAt(string path, int line) => At(path, line).Count;

    /// <summary>Alle Stellen mit Einträgen (Pfad mit <c>/</c>, wie im Log), je mit ihrer Anzahl; ohne feste Reihenfolge.</summary>
    public IEnumerable<(string Path, int Line, int Count)> Places() =>
        _byFile.SelectMany(file => file.Value.Select(line => (file.Key, line.Key, line.Value.Count)));

    public int CountInFile(string path) =>
        _byFile.TryGetValue(Normalize(path), out var lines) ? lines.Values.Sum(l => l.Count) : 0;

    /// <summary>Erste Zeile der häufigsten Meldung an der Stelle (bei Gleichstand die zuerst gesehene); <see langword="null"/> ohne Einträge.</summary>
    public string? MostCommonMessageAt(string path, int line)
    {
        var entries = At(path, line);
        if (entries.Count == 0)
        {
            return null;
        }

        var best = entries
            .GroupBy(e => e.Message, StringComparer.Ordinal)
            .MaxBy(g => g.Count());
        return FirstLine(best!.Key);
    }

    private static bool TryPlace(ErrorEntry entry, out string path, out int line)
    {
        if (entry.FilePath is { Length: > 0 } filePath && entry.FileLine is { } fileLine)
        {
            path = Normalize(filePath);
            line = fileLine;
            return true;
        }

        return TryFallbackPlace(entry.Message, out path, out line);
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private static string FirstLine(string message)
    {
        var end = message.IndexOf('\n');
        return (end < 0 ? message : message[..end]).TrimEnd('\r');
    }
}
