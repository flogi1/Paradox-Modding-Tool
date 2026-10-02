namespace ParadoxModTool.VanillaDiff;

/// <summary>Eine Zeile der nebeneinanderstehenden Ansicht: welche Zeile (1-basiert) jede Seite dort zeigt; <see langword="null"/> = Lücke.</summary>
public sealed record DiffRow(int? LeftLine, int? RightLine);

/// <summary>
/// Baut aus den Zeilenzahlen beider Seiten und den Ausrichtungslücken (<see cref="DiffAlignment"/>) die Zeilen, die im Compare und Update
/// Tool auf gleicher Höhe stehen. Jede Seite zeigt ihre Zeilen mit den Lücken davor; die kürzere Seite endet mit leeren Zeilen.
/// </summary>
public static class DiffRows
{
    public static IReadOnlyList<DiffRow> Build(int leftLineCount, int rightLineCount, IEnumerable<AlignmentGap> gaps)
    {
        var all = gaps.ToList();
        var left = Side(leftLineCount, all.Where(g => g.Side == DiffSide.Left));
        var right = Side(rightLineCount, all.Where(g => g.Side == DiffSide.Right));
        var rows = new List<DiffRow>(Math.Max(left.Count, right.Count));
        for (var i = 0; i < Math.Max(left.Count, right.Count); i++)
        {
            rows.Add(new DiffRow(i < left.Count ? left[i] : null, i < right.Count ? right[i] : null));
        }

        return rows;
    }

    /// <summary>Zeilen einer Seite mit Lücken: vor Zeile n stehen die Lücken mit <c>BeforeLine == n</c>; Lücken hinter der letzten Zeile ans Ende.</summary>
    private static List<int?> Side(int lineCount, IEnumerable<AlignmentGap> gaps)
    {
        var before = gaps.GroupBy(g => g.BeforeLine).ToDictionary(g => g.Key, g => g.Sum(x => x.LineCount));
        var result = new List<int?>();
        for (var line = 1; line <= lineCount; line++)
        {
            if (before.TryGetValue(line, out var count))
            {
                result.AddRange(Enumerable.Repeat<int?>(null, count));
            }

            result.Add(line);
        }

        result.AddRange(before.Where(g => g.Key > lineCount).OrderBy(g => g.Key).SelectMany(g => Enumerable.Repeat<int?>(null, g.Value)));
        return result;
    }
}
