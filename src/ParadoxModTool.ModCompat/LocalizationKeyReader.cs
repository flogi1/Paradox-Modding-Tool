using System.Text.RegularExpressions;

namespace ParadoxModTool.ModCompat;

/// <summary>Liest die Schlüssel einer Lokalisierungsdatei (<c>key:0 "Text"</c>). Kopfzeile <c>l_english:</c> und Kommentare zählen nicht.</summary>
public static class LocalizationKeyReader
{
    private static readonly Regex KeyLine = new("^\\s*([^\\s:#\"]+):\\d*\\s+\"", RegexOptions.Compiled);

    public static IReadOnlyList<(string Key, int Line, string Text)> Read(string text)
    {
        var result = new List<(string, int, string)>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = KeyLine.Match(lines[i].TrimStart('﻿'));
            if (match.Success)
            {
                result.Add((match.Groups[1].Value, i + 1, lines[i].Trim()));
            }
        }

        return result;
    }
}
