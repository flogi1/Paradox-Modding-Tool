using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators;

/// <summary>Liest Paradox-Lokalisierung (Spec Teil 4): <c>schlüssel: "wert"</c> bzw. <c>schlüssel:0 "wert"</c> je Zeile; Kopfzeile und Kommentare zählen nicht.</summary>
public static class YmlReader
{
    private static readonly Regex Line = new(@"^\s*(?<key>[^\s:#""]+):\d*\s*""(?<value>.*)""\s*(#.*)?$", RegexOptions.CultureInvariant);

    /// <summary>Schlüssel → Wert wie in der Datei (noch maskiert, siehe <see cref="Unescape"/>); bei doppelten Schlüsseln gilt der letzte.</summary>
    public static IReadOnlyDictionary<string, string> Values(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n'))
        {
            if (Line.Match(raw) is { Success: true } match)
            {
                values[match.Groups["key"].Value] = match.Groups["value"].Value;
            }
        }

        return values;
    }

    /// <summary>Nimmt die Paradox-Maskierung zurück: <c>\"</c> wird <c>"</c>, <c>\n</c> ein Zeilenumbruch, <c>\\</c> ein Backslash (Gegenstück zu <see cref="LanguageFill.YamlValue"/>).</summary>
    public static string Unescape(string value)
    {
        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length && value[i + 1] is '"' or 'n' or '\\')
            {
                result.Append(value[i + 1] == 'n' ? '\n' : value[i + 1]);
                i++;
            }
            else
            {
                result.Append(value[i]);
            }
        }

        return result.ToString();
    }
}
