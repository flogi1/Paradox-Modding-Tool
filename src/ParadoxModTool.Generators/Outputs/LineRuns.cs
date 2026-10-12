namespace ParadoxModTool.Generators;

/// <summary>Ergebnis des Ersetzens: neuer Text, Zahl der ersetzten Läufe und die erste Zeile jedes ersatzlos entfernten Laufs.</summary>
internal sealed record RunResult(string Text, int Replaced, IReadOnlyList<string> Removed);

/// <summary>
/// Läufe passender Einheiten ersetzen (Spec 2026-10-06-generator-packages-idea-groups, Abschnitt 1): aufeinanderfolgende Treffer, zwischen denen nur
/// Leerraum steht, bilden einen Lauf. Der erste Lauf bekommt den gerenderten Text, alle weiteren fallen weg. Ein Lauf auf eigenen Zeilen wird
/// zeilenweise ersetzt, sonst nur seine Spanne. Gemeinsam für den Abgleich nach Treffer und die Lokalisierung in gemeinsamen Dateien.
/// </summary>
internal static class LineRuns
{
    /// <param name="matches">Spannen der passenden Einheiten (Start inklusive, Ende exklusiv), beliebige Reihenfolge.</param>
    /// <param name="rendered">Gerenderter Text (bereits normalisiert); leer = Läufe entfernen.</param>
    public static RunResult Apply(string text, IEnumerable<(int Start, int End)> matches, string rendered, string newLine)
    {
        var runs = new List<(int Start, int End)>();
        foreach (var (start, end) in matches.OrderBy(m => m.Start))
        {
            // Kommentarzeilen zwischen zwei Treffern gehören zum Lauf (Abschnittsköpfe wie "#### Select Idea Group adm_18" mit Zierzeilen)
            if (runs.Count > 0 && OnlyBlankOrComments(text[runs[^1].End..start]))
            {
                runs[^1] = (runs[^1].Start, Math.Max(runs[^1].End, end));
            }
            else
            {
                runs.Add((start, end));
            }
        }

        // Reine Zierzeilen (nur #) direkt über und unter einem Lauf gehören dazu, ohne Leerzeile dazwischen
        for (var i = 0; i < runs.Count; i++)
        {
            var (start, end) = runs[i];
            var lineStart = BlockText.LineStart(text, start);
            while (lineStart > 0 && text[lineStart..start].Trim().Length == 0 && IsDecoration(text, BlockText.LineStart(text, lineStart - 1)))
            {
                lineStart = BlockText.LineStart(text, lineStart - 1);
                start = lineStart + Indent(text, lineStart);
            }

            var next = BlockText.LineEnd(text, end);
            while (next < text.Length && text[end..next].Trim().Length == 0 && IsDecoration(text, next))
            {
                end = next + text[next..BlockText.LineEnd(text, next)].TrimEnd('\r', '\n').Length;
                next = BlockText.LineEnd(text, end);
            }

            runs[i] = (start, end);
        }

        var edits = new List<(int Start, int End, string Text)>();
        var removed = new List<string>();
        var replaced = 0;
        for (var i = 0; i < runs.Count; i++)
        {
            var (start, end) = runs[i];
            var lineStart = BlockText.LineStart(text, start);
            var lineEnd = BlockText.LineEnd(text, end);
            var rest = text[end..lineEnd].Trim();
            var wholeLines = text[lineStart..start].Trim().Length == 0 && (rest.Length == 0 || rest.StartsWith('#'));
            if (i == 0 && rendered.Length > 0)
            {
                edits.Add(wholeLines
                    ? (lineStart, lineEnd, rendered.EndsWith('\n') ? rendered : rendered + newLine)
                    : (start, end, rendered.TrimEnd('\r', '\n')));
                replaced++;
                continue;
            }

            if (rendered.Length == 0)
            {
                removed.Add(FirstLine(text, start));
            }

            if (!wholeLines)
            {
                edits.Add((start, end, string.Empty));
                continue;
            }

            // Leerzeile davor und danach: die danach mit entfernen, damit keine doppelten Leerzeilen bleiben
            var removeEnd = lineEnd;
            if (lineStart > 0 && IsBlankLine(text, BlockText.LineStart(text, lineStart - 1)) && removeEnd < text.Length && IsBlankLine(text, removeEnd))
            {
                removeEnd = BlockText.LineEnd(text, removeEnd);
            }

            edits.Add((lineStart, removeEnd, string.Empty));
        }

        var result = text;
        foreach (var (start, end, replacement) in edits.OrderByDescending(e => e.Start))
        {
            result = result[..start] + replacement + result[end..];
        }

        return new RunResult(result, replaced, removed);
    }

    private static bool OnlyBlankOrComments(string between) =>
        between.Split('\n').All(l => l.Trim().Length == 0 || l.TrimStart().StartsWith('#'));

    /// <summary>Zeile, die nur aus # besteht (Zierzeile eines Abschnittskopfs).</summary>
    private static bool IsDecoration(string text, int lineStart)
    {
        var line = text[lineStart..BlockText.LineEnd(text, lineStart)].Trim();
        return line.Length > 0 && line.All(c => c == '#');
    }

    private static int Indent(string text, int lineStart)
    {
        var line = text[lineStart..BlockText.LineEnd(text, lineStart)];
        return line.Length - line.TrimStart(' ', '\t').Length;
    }

    private static bool IsBlankLine(string text, int lineStart) => text[lineStart..BlockText.LineEnd(text, lineStart)].Trim().Length == 0;

    private static string FirstLine(string text, int start)
    {
        var end = text.IndexOf('\n', start);
        return (end < 0 ? text[start..] : text[start..end]).Trim();
    }
}
