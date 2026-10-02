namespace ParadoxModTool.ModCompat;

/// <summary>Die sortierbaren Spalten der Ergebnisliste im Fenster "Mod-Kompatibilität".</summary>
public enum CompatSortColumn
{
    Severity,
    Kind,
    Path,
    Key,
    Winner,
    Suggestion,
    Certainty,
    Resolved,
}

/// <summary>
/// Sortiert Funde nach einer Spalte. Stabil: Gleichrangige Funde behalten die Reihenfolge der Prüfung. Aufsteigend heißt bei der
/// Schwere "Konflikt zuerst" und bei "Gelöst" "gelöste zuerst", damit der erste Klick auf die Spaltenüberschrift das Wichtige nach oben holt.
/// </summary>
public static class CompatFindingSort
{
    public static IReadOnlyList<CompatFinding> Sort(IEnumerable<CompatFinding> findings, CompatSortColumn column, bool descending, Func<CompatFinding, bool> isResolved)
    {
        var list = findings.ToList();
        return column switch
        {
            CompatSortColumn.Severity => By(list, f => -(int)f.Severity, descending),
            CompatSortColumn.Resolved => By(list, f => isResolved(f) ? 0 : 1, descending),
            CompatSortColumn.Certainty => By(list, f => f.Uncertain ? 1 : 0, descending),
            _ => ByText(list, Text(column), descending),
        };
    }

    private static Func<CompatFinding, string> Text(CompatSortColumn column) => column switch
    {
        CompatSortColumn.Kind => f => CompatText.Kind(f.Kind),
        CompatSortColumn.Key => f => f.Key,
        CompatSortColumn.Winner => f => f.Winner?.Name ?? string.Empty,
        CompatSortColumn.Suggestion => f => CompatText.Suggestion(f.Suggestion),
        _ => f => f.RelativePath,
    };

    private static IReadOnlyList<CompatFinding> By(List<CompatFinding> list, Func<CompatFinding, int> key, bool descending) =>
        (descending ? list.OrderByDescending(key) : list.OrderBy(key)).ToList();

    private static IReadOnlyList<CompatFinding> ByText(List<CompatFinding> list, Func<CompatFinding, string> key, bool descending) =>
        (descending ? list.OrderByDescending(key, NaturalComparer.Instance) : list.OrderBy(key, NaturalComparer.Instance)).ToList();

    /// <summary>Vergleicht Ziffernfolgen als Zahlen (x2 vor x10), sonst ohne Groß-/Kleinschreibung.</summary>
    private sealed class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            x ??= string.Empty;
            y ??= string.Empty;
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    var startX = i;
                    var startY = j;
                    while (i < x.Length && char.IsDigit(x[i]))
                    {
                        i++;
                    }

                    while (j < y.Length && char.IsDigit(y[j]))
                    {
                        j++;
                    }

                    var numberX = x[startX..i].TrimStart('0');
                    var numberY = y[startY..j].TrimStart('0');
                    var byNumber = numberX.Length != numberY.Length
                        ? numberX.Length.CompareTo(numberY.Length)
                        : string.CompareOrdinal(numberX, numberY);
                    if (byNumber != 0)
                    {
                        return byNumber;
                    }

                    continue;
                }

                var byChar = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                if (byChar != 0)
                {
                    return byChar;
                }

                i++;
                j++;
            }

            return (x.Length - i).CompareTo(y.Length - j);
        }
    }
}
