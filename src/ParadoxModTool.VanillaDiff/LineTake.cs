using System.Text.RegularExpressions;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Markierte Zeilen einer Seite in die andere übernehmen (Compare und Update Tool, Taste U; wie im Komp-Patch-Merge). Zwischen der Zeile
/// der ersten und der letzten markierten Zeile bekommt das Ziel genau das, was die Quelle zeigt: Quellzeile ersetzt die Zielzeile, steht
/// ihr eine Lücke gegenüber, wird sie eingefügt; eine Zielzeile gegenüber einer Quelllücke fällt weg. Außerhalb bleibt das Ziel, wie es ist.
/// </summary>
public static class LineTake
{
    private static readonly Regex LineBreak = new("\r\n|\n", RegexOptions.Compiled);

    /// <returns>Der neue Text des Ziels, mit dessen Zeilenenden und dessen (fehlendem) Zeilenumbruch am Ende.</returns>
    public static string Apply(IReadOnlyList<DiffRow> rows, string sourceText, string targetText, DiffSide source, int firstMarkedLine, int lastMarkedLine)
    {
        var (first, last) = firstMarkedLine <= lastMarkedLine ? (firstMarkedLine, lastMarkedLine) : (lastMarkedLine, firstMarkedLine);
        var sourceLines = Lines(sourceText);
        var targetLines = Lines(targetText);
        int? SourceLine(DiffRow row) => source == DiffSide.Left ? row.LeftLine : row.RightLine;
        int? TargetLine(DiffRow row) => source == DiffSide.Left ? row.RightLine : row.LeftLine;

        var firstRow = IndexOf(rows, r => SourceLine(r) == first);
        var lastRow = LastIndexOf(rows, r => SourceLine(r) == last);
        if (firstRow < 0 || lastRow < 0)
        {
            return targetText;
        }

        var result = new List<string>();
        for (var i = 0; i < rows.Count; i++)
        {
            var line = i >= firstRow && i <= lastRow ? At(sourceLines, SourceLine(rows[i])) : At(targetLines, TargetLine(rows[i]));
            if (line is not null)
            {
                result.Add(line);
            }
        }

        var newline = targetText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var trailing = targetText.Length == 0 || targetText.EndsWith('\n');
        return string.Join(newline, result) + (trailing && result.Count > 0 ? newline : string.Empty);
    }

    /// <summary>Zeilen ohne das leere Stück hinter einem abschließenden Zeilenumbruch.</summary>
    private static List<string> Lines(string text)
    {
        var lines = LineBreak.Split(text).ToList();
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string? At(List<string> lines, int? line) => line is { } n && n >= 1 && n <= lines.Count ? lines[n - 1] : null;

    private static int IndexOf(IReadOnlyList<DiffRow> rows, Func<DiffRow, bool> match)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (match(rows[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastIndexOf(IReadOnlyList<DiffRow> rows, Func<DiffRow, bool> match)
    {
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (match(rows[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
