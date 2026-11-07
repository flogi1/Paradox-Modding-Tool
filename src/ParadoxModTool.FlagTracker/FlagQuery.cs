using System.Text.RegularExpressions;

namespace ParadoxModTool.FlagTracker;

/// <summary>Macht aus der Cursorstelle oder Auswahl im Editor eine Suchanfrage für den Flag-Tracker.</summary>
public static partial class FlagQuery
{
    private static readonly string[] Prefixes = { "var:", "global_var:", "local_var:", "flag:", "scope:" };

    /// <summary>
    /// Suchanfrage aus Zeile und Spalte (0-basiert). Eine einzeilige, getrimmte, nicht leere Auswahl gewinnt vor dem Wort an der Spalte.
    /// Null, wenn nichts Brauchbares übrig bleibt.
    /// </summary>
    public static string? FromEditor(string line, int column, string? selection)
    {
        var text = RawFromEditor(line, column, selection) ?? string.Empty;
        text = text.Trim('.');
        var dot = text.LastIndexOf('.');
        if (dot >= 0)
        {
            text = text[(dot + 1)..];
        }

        foreach (var prefix in Prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..];
                break;
            }
        }

        text = Collapse(Placeholder().Replace(text, "*"));
        return text.Length == 0 || text == "*" ? null : text;
    }

    /// <summary>
    /// Auswahl (einzeilig, getrimmt, nicht leer) oder Wort an der Spalte nach dem Entfernen von Anführungszeichen und Wrapper,
    /// ohne weitere Umformung. Null, wenn nichts übrig bleibt.
    /// </summary>
    public static string? RawFromEditor(string line, int column, string? selection)
    {
        var text = SelectionText(selection) ?? StripWrapper(WordAt(line, column) ?? string.Empty);
        return text.Length == 0 ? null : text;
    }

    private static string? SelectionText(string? selection)
    {
        if (selection is null)
        {
            return null;
        }

        var trimmed = selection.Trim();
        if (trimmed.Length == 0 || trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            return null;
        }

        // Eine Auswahl über mehrere Wörter oder eine Zuweisung ist kein Name: dann zählt das Wort an der Spalte.
        var stripped = StripWrapper(trimmed);
        return stripped.Length == 0 || stripped.Any(c => char.IsWhiteSpace(c) || c == '=') ? null : stripped;
    }

    private static string? WordAt(string line, int column)
    {
        if (column < 0 || column > line.Length)
        {
            return null;
        }

        if (column == line.Length || !IsWordChar(line[column]))
        {
            // Der Cursor unmittelbar hinter einem Wort (Zeilenende oder Zeichen danach) zählt noch zum Wort davor.
            if (column > 0 && IsWordChar(line[column - 1]))
            {
                column--;
            }
            else
            {
                return null;
            }
        }

        var start = column;
        while (start > 0 && IsWordChar(line[start - 1]))
        {
            start--;
        }

        var end = column;
        while (end < line.Length && IsWordChar(line[end]))
        {
            end++;
        }

        return line[start..end];
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '$' or '@' or ':' or '.';

    /// <summary>Entfernt Anführungszeichen und einen Datenfunktions-Wrapper wie <c>Var('x')</c>.</summary>
    private static string StripWrapper(string text)
    {
        var wrapper = Wrapper().Match(text);
        if (wrapper.Success)
        {
            text = wrapper.Groups["inner"].Value;
        }

        return text.Trim().Trim('\'', '"').Trim();
    }

    private static string Collapse(string text) => Stars().Replace(text, "*");

    [GeneratedRegex(@"^\w+\(\s*(?<inner>.*?)\s*\)$", RegexOptions.Singleline)]
    private static partial Regex Wrapper();

    [GeneratedRegex(@"\$[^$]*\$")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\*{2,}")]
    private static partial Regex Stars();
}
