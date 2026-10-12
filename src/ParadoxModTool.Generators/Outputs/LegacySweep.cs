using System.Text.RegularExpressions;

namespace ParadoxModTool.Generators;

/// <summary>
/// Unmarkierte Alt-Einträge entfernen (Spec Teil 2, <c>legacyPatterns</c>), wie <c>_sweep_legacy_tag_content</c> im Python-Generator: Jeder Treffer im
/// Bereich nimmt seine Zeilen ab dem Zeilenanfang mit; öffnen sie mehr <c>{</c> als sie schließen, gehen die Folgezeilen mit, bis die Klammern
/// ausgeglichen sind. Treffer in <c>skip</c> (die eigenen Markierungsabschnitte) bleiben. Zeilenenden und alles andere bleiben zeichengleich.
/// </summary>
public static class LegacySweep
{
    public static (string Text, int Removed) Apply(string text, int scopeStart, int scopeEnd, IReadOnlyList<Regex> patterns, IReadOnlyList<(int Start, int End)> skip)
    {
        var spans = new List<(int Start, int End)>();
        foreach (var pattern in patterns)
        {
            for (var match = pattern.Match(text, scopeStart); match.Success && match.Index < scopeEnd; match = match.NextMatch())
            {
                if (skip.Any(s => match.Index >= s.Start && match.Index < s.End))
                {
                    continue;
                }

                var start = BlockText.LineStart(text, match.Index);
                var end = LineEndAfter(text, match.Index + match.Length);
                var depth = Depth(text, start, end);
                while (depth > 0 && end < scopeEnd)
                {
                    var next = BlockText.LineEnd(text, end);
                    depth += Depth(text, end, next);
                    end = next;
                }

                spans.Add((start, Math.Max(start, Math.Min(end, scopeEnd))));
            }
        }

        if (spans.Count == 0)
        {
            return (text, 0);
        }

        var merged = new List<(int Start, int End)>();
        foreach (var span in spans.OrderBy(s => s.Start))
        {
            if (merged.Count > 0 && span.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, span.End));
            }
            else
            {
                merged.Add(span);
            }
        }

        var result = text;
        foreach (var (start, end) in Enumerable.Reverse(merged))
        {
            result = result[..start] + result[end..];
        }

        return (result, spans.Select(s => s.Start).Distinct().Count());
    }

    /// <summary>Index nach dem Zeilenende der Zeile, in der <paramref name="position"/> liegt (ein Treffer, der mit dem Zeilenende endet, zählt zu seiner Zeile).</summary>
    private static int LineEndAfter(string text, int position)
    {
        if (position > 0 && text[position - 1] == '\n')
        {
            return position;
        }

        return BlockText.LineEnd(text, position);
    }

    private static int Depth(string text, int start, int end)
    {
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            depth += text[i] switch { '{' => 1, '}' => -1, _ => 0 };
        }

        return depth;
    }
}
