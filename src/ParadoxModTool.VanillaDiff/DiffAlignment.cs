namespace ParadoxModTool.VanillaDiff;

public enum DiffSide
{
    Left,
    Right,
}

/// <summary>Leerraum, der auf einer Seite <paramref name="BeforeLine"/> (1-basiert) um <paramref name="LineCount"/> Zeilen nach unten schiebt, damit die andere Seite gegenübersteht. Der Leerraum ist nie Text.</summary>
public sealed record AlignmentGap(DiffSide Side, int BeforeLine, int LineCount);

/// <summary>
/// Berechnet aus dem strukturellen Diff, wo eine Seite Leerraum braucht, damit einander entsprechende Einträge auf gleicher Höhe stehen.
/// Grundlage sind Ankerpaare (Zeile links, Zeile rechts); zwischen zwei Ankern gleicht die kürzere Seite die Zeilendifferenz vor dem nächsten Anker aus.
/// Zeilen ohne Eintrag (Kommentare, Leerzeilen) werden dabei mit ausgeglichen. Rückt eine Seite zwischen zwei Ankern nicht vor (mehrere Einträge stehen auf derselben Zeile) und der vorige Anker lag dort auf einer echten Zeile, steht der Leerraum hinter dieser Zeile statt davor. Nach dem letzten Anker entsteht keine Lücke.
/// </summary>
public static class DiffAlignment
{
    public static IReadOnlyList<AlignmentGap> Compute(IReadOnlyList<EntryDiff> diffs, Func<int, int> leftLineOf, Func<int, int> rightLineOf)
    {
        var nodes = new List<(int? Left, int? Right)>();
        Collect(diffs, leftLineOf, rightLineOf, nodes);

        // Fehlende Seite eines einseitigen Eintrags: Zeile des nächsten Ankers, der diese Seite kennt ("virtuell"). Ohne folgenden Anker entfällt der Eintrag (keine Lücke am Dateiende).
        var resolved = new List<(int Left, int Right, bool LeftReal, bool RightReal)>();
        int? nextLeft = null;
        int? nextRight = null;
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var left = nodes[i].Left ?? nextLeft;
            var right = nodes[i].Right ?? nextRight;
            if (nodes[i].Left is not null)
            {
                nextLeft = nodes[i].Left;
            }

            if (nodes[i].Right is not null)
            {
                nextRight = nodes[i].Right;
            }

            if (left is int l && right is int r)
            {
                resolved.Add((l, r, nodes[i].Left is not null, nodes[i].Right is not null));
            }
        }

        resolved.Reverse();

        var gaps = new Dictionary<(DiffSide Side, int Line), int>();
        var lastLeft = 1;
        var lastRight = 1;
        var lastLeftReal = false;
        var lastRightReal = false;
        foreach (var (left, right, leftReal, rightReal) in resolved)
        {
            if (left < lastLeft || right < lastRight)
            {
                continue;
            }

            var deltaLeft = left - lastLeft;
            var deltaRight = right - lastRight;
            if (deltaLeft < deltaRight)
            {
                // Rückt die linke Seite nicht vor und der letzte Anker lag dort auf einer echten Zeile, steht der Leerraum hinter dieser Zeile.
                Add(gaps, DiffSide.Left, deltaLeft == 0 && lastLeftReal ? left + 1 : left, deltaRight - deltaLeft);
            }
            else if (deltaRight < deltaLeft)
            {
                Add(gaps, DiffSide.Right, deltaRight == 0 && lastRightReal ? right + 1 : right, deltaLeft - deltaRight);
            }

            lastLeft = left;
            lastRight = right;
            lastLeftReal = leftReal;
            lastRightReal = rightReal;
        }

        return gaps
            .OrderBy(g => g.Key.Side)
            .ThenBy(g => g.Key.Line)
            .Select(g => new AlignmentGap(g.Key.Side, g.Key.Line, g.Value))
            .ToList();
    }

    private static void Add(Dictionary<(DiffSide, int), int> gaps, DiffSide side, int line, int count)
    {
        gaps[(side, line)] = gaps.GetValueOrDefault((side, line)) + count;
    }

    private static void Collect(IReadOnlyList<EntryDiff> diffs, Func<int, int> leftLineOf, Func<int, int> rightLineOf, List<(int? Left, int? Right)> nodes)
    {
        foreach (var diff in diffs)
        {
            var left = StartLine(diff.Left, leftLineOf);
            var right = StartLine(diff.Right, rightLineOf);
            if (left is null && right is null)
            {
                continue;
            }

            nodes.Add((left, right));

            if (diff.Kind == DiffKind.Modified && diff.Children is not null)
            {
                Collect(diff.Children, leftLineOf, rightLineOf, nodes);
                var leftEnd = EndLine(diff.Left, leftLineOf);
                var rightEnd = EndLine(diff.Right, rightLineOf);
                if (leftEnd is not null && rightEnd is not null)
                {
                    nodes.Add((leftEnd, rightEnd));
                }
            }
        }
    }

    private static int? StartLine(ScriptEntry? entry, Func<int, int> lineOf) =>
        entry?.SourceStart is int start ? lineOf(start) : null;

    private static int? EndLine(ScriptEntry? entry, Func<int, int> lineOf) =>
        entry?.SourceStart is int start && entry.SourceEnd is int end ? lineOf(Math.Max(start, end - 1)) : null;
}
