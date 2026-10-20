using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Performance;

/// <summary>Liest die CSV-Dumps des Script-Profilers (<c>Script.Profiling.Dump</c>, Trennzeichen <c>;</c>).</summary>
public static class ProfilingDumpReader
{
    private static readonly Regex LocationPattern = new(@"^(.*) @ (.*?):(\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ProfilingDump Read(TextReader reader)
    {
        var header = (reader.ReadLine() ?? string.Empty).TrimStart('\uFEFF');
        var columns = header.Split(';').Select(c => c.Trim()).ToList();
        var location = IndexOf(columns, "File Location");
        var calls = IndexOf(columns, "Call Count");
        var self = IndexOf(columns, "Self Time");
        var total = IndexOf(columns, "Total Time");
        if (location < 0 || calls < 0 || self < 0 || total < 0)
        {
            throw new ProfilingFormatException(
                "Required columns File Location, Call Count, Self Time and Total Time are missing from the header.", header);
        }

        var needed = Math.Max(Math.Max(location, calls), Math.Max(self, total)) + 1;
        var rows = new List<ProfilingRow>();
        var warnings = new List<string>();
        var lineNumber = 1;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = line.Split(';');
            if (fields.Length < needed)
            {
                warnings.Add($"line {lineNumber}: too few columns");
                continue;
            }

            var key = fields[location].Trim();
            var match = LocationPattern.Match(key);
            if (!match.Success || !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sourceLine))
            {
                warnings.Add($"line {lineNumber}: unrecognized file location '{key}'");
                continue;
            }

            if (!long.TryParse(fields[calls].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var callCount))
            {
                warnings.Add($"line {lineNumber}: invalid call count '{fields[calls].Trim()}'");
                continue;
            }

            if (!TryParseTime(fields[self], out var selfTime) || !TryParseTime(fields[total], out var totalTime))
            {
                warnings.Add($"line {lineNumber}: invalid time value");
                continue;
            }

            var path = match.Groups[2].Value.Replace('\\', '/').TrimStart('/');
            rows.Add(new ProfilingRow(key, match.Groups[1].Value, path, sourceLine, callCount, selfTime, totalTime));
        }

        return new ProfilingDump(rows, warnings);
    }

    /// <summary>Liest eine Dump-Datei als UTF-8 (BOM wird erkannt), ungültige Bytes werden ersetzt.</summary>
    public static ProfilingDump ReadFile(string path)
    {
        using var reader = new StreamReader(path, new UTF8Encoding(false, false), detectEncodingFromByteOrderMarks: true);
        return Read(reader);
    }

    private static int IndexOf(List<string> columns, string name) =>
        columns.FindIndex(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));

    private static bool TryParseTime(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
