using System.Text.RegularExpressions;

namespace ParadoxModTool.Patches;

/// <summary>Eine Zeile <c> key:0 "value"</c> einer Lokalisierungsdatei; <see cref="ValueStart"/>/<see cref="ValueLength"/> zeigen in die Zeile.</summary>
public sealed record LocalizationLine(string Key, string Value, int ValueStart, int ValueLength);

/// <summary>Liest Lokalisierungszeilen (<c>.yml</c>): Schlüssel, Versionsnummer, Wert in Anführungszeichen bis zum letzten <c>"</c>, danach evtl. ein Kommentar.</summary>
public static class LocalizationLines
{
    private static readonly Regex Line = new(@"^\s*(?<key>[A-Za-z0-9_.\-]+):\d*\s*""", RegexOptions.CultureInvariant);

    private static readonly Regex Reference = new(@"\$(?<name>[A-Za-z0-9_.\-]+)(?:\|[^$]*)?\$", RegexOptions.CultureInvariant);

    public static LocalizationLine? Parse(string line)
    {
        var match = Line.Match(line);
        if (!match.Success)
        {
            return null;
        }

        var start = match.Index + match.Length;
        var end = line.LastIndexOf('"');
        if (end < start)
        {
            return null;
        }

        return new LocalizationLine(match.Groups["key"].Value, line[start..end], start, end - start);
    }

    /// <summary>Alle Zeilen der Datei mit Schlüssel und Wert (Kopfzeile, Kommentare und Leerzeilen fallen weg).</summary>
    public static IEnumerable<LocalizationLine> All(string text) =>
        text.Split('\n').Select(l => Parse(l.TrimEnd('\r'))).Where(l => l is not null).Select(l => l!);

    /// <summary>Namen der Verweise <c>$name$</c> bzw. <c>$name|U$</c> in einem Wert.</summary>
    public static IEnumerable<string> References(string value) => Reference.Matches(value).Select(m => m.Groups["name"].Value);
}
