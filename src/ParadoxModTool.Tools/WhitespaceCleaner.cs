using System.Text;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Tools;

/// <summary>
/// Bereinigt überschüssigen Whitespace in Moddateien (Konzept, Abschnitt 10: "ein Tool zum
/// bereinigen überschüssiger Leerzeichen oder Tab Sprünge. Absätze von einer Zeile werden
/// meistens der Übersicht zwischen Code Abschnitten dienen und sollten nicht entfernt werden.").
/// Arbeitet bewusst rein zeilenbasiert und dateityp-unabhängig (im Gegensatz zu
/// <see cref="ScriptPrettyPrinter"/>, der nur für das Jomini-Skriptdialekt gilt) - funktioniert
/// also unverändert auch für .yml/.lua-Dateien, deren Syntax der Skript-Parser nicht kennt.
///
/// Zwei bewusste Einschränkungen, um NIE mehr als "überschüssigen" Whitespace zu entfernen:
/// 1) Nur Leerzeichen/Tabs am ZEILENENDE werden entfernt - mehrfache Leerzeichen INNERHALB einer
///    Zeile (z. B. zur Ausrichtung von "=" untereinander, eine verbreitete Modding-Konvention)
///    bleiben unangetastet.
/// 2) Läufe von zwei oder mehr aufeinanderfolgenden Leerzeilen werden auf genau EINE Leerzeile
///    reduziert, niemals auf null - einzelne Leerzeilen als optische Trennung zwischen
///    Codeabschnitten ("Absätze von einer Zeile ... sollten nicht entfernt werden") bleiben
///    erhalten.
/// </summary>
public static class WhitespaceCleaner
{
    private static readonly Regex LineSplitPattern = new("\r\n|\r|\n", RegexOptions.Compiled);

    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lineEnding = text.Contains("\r\n") ? "\r\n" : "\n";
        var endsWithNewline = text.EndsWith("\r\n") || (text.EndsWith('\n') && !text.EndsWith("\r\n"));

        var rawLines = LineSplitPattern.Split(text);

        // Split() liefert bei einem abschließenden Zeilenumbruch ein leeres letztes Element -
        // das wird unten separat über endsWithNewline wieder ergänzt, nicht als eigene
        // "Leerzeile" mitgezählt (sonst würde aus einer Datei mit EINEM abschließenden
        // Zeilenumbruch fälschlich eine zusätzliche Leerzeile am Ende entstehen).
        var lines = endsWithNewline && rawLines.Length > 0
            ? rawLines[..^1]
            : rawLines;

        var trimmed = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            trimmed.Add(line.TrimEnd(' ', '\t'));
        }

        var collapsed = new List<string>(trimmed.Count);
        var previousWasBlank = false;
        foreach (var line in trimmed)
        {
            var isBlank = line.Length == 0;
            if (isBlank && previousWasBlank)
            {
                continue;
            }

            collapsed.Add(line);
            previousWasBlank = isBlank;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < collapsed.Count; i++)
        {
            sb.Append(collapsed[i]);
            if (i < collapsed.Count - 1 || endsWithNewline)
            {
                sb.Append(lineEnding);
            }
        }

        return sb.ToString();
    }
}
