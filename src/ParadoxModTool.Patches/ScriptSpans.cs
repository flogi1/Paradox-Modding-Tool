using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

/// <summary>Hilfen für Textänderungen über die Quellspannen des Parsers.</summary>
public static class ScriptSpans
{
    public static ScriptStatement? FindTopLevel(ScriptBlock root, string key) =>
        root.Entries.OfType<ScriptStatement>().FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

    /// <summary>Weitet eine Spanne auf ganze Zeilen aus (inklusive Zeilenumbruch), wenn davor und danach auf der Zeile nur Leerraum steht.</summary>
    public static (int Start, int End) LineExtent(string text, int start, int end)
    {
        var lineStart = start;
        while (lineStart > 0 && text[lineStart - 1] is ' ' or '\t')
        {
            lineStart--;
        }

        var lineEnd = end;
        while (lineEnd < text.Length && text[lineEnd] is ' ' or '\t')
        {
            lineEnd++;
        }

        var startsLine = lineStart == 0 || text[lineStart - 1] == '\n';
        var endsLine = lineEnd == text.Length || text[lineEnd] is '\r' or '\n';
        if (!startsLine || !endsLine)
        {
            return (start, end);
        }

        if (lineEnd < text.Length && text[lineEnd] == '\r')
        {
            lineEnd++;
        }

        if (lineEnd < text.Length && text[lineEnd] == '\n')
        {
            lineEnd++;
        }

        return (lineStart, lineEnd);
    }

    /// <summary>Position des ersten <c>#</c> außerhalb von Anführungszeichen, sonst die Zeilenlänge.</summary>
    public static int CodeLength(string line)
    {
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                quoted = !quoted;
            }
            else if (line[i] == '#' && !quoted)
            {
                return i;
            }
        }

        return line.Length;
    }

    /// <summary>Führender Leerraum einer Zeile.</summary>
    public static string Indent(string line) => line[..(line.Length - line.TrimStart(' ', '\t').Length)];

    /// <summary>Einrückung der Zeile, in der <paramref name="position"/> liegt.</summary>
    public static string LineIndent(string text, int position)
    {
        var lineStart = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
        return Indent(text[lineStart..position]);
    }

    /// <summary>Fügt bereits eingerückten Text vor der schließenden Klammer ein; steht sie nicht allein auf ihrer Zeile, wird umbrochen.</summary>
    public static string InsertBeforeBrace(string text, int brace, string indentedText, string braceIndent, string newLine)
    {
        var lineStart = brace == 0 ? 0 : text.LastIndexOf('\n', brace - 1) + 1;
        if (text[lineStart..brace].Trim().Length == 0)
        {
            return text.Insert(lineStart, indentedText + newLine);
        }

        var before = text[..brace].TrimEnd(' ', '\t');
        return before + newLine + indentedText + newLine + braceIndent + text[brace..];
    }

    /// <summary>Rückt jede nicht leere Zeile ein; Zeilen werden mit <paramref name="newLine"/> verbunden.</summary>
    public static string IndentLines(string text, string indent, string newLine) =>
        string.Join(newLine, text.Replace("\r\n", "\n").Split('\n').Select(l => l.Length > 0 ? indent + l : l));
}
