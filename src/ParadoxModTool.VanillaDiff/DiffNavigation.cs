namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Springen zur nächsten bzw. vorigen Abweichung im C-U-Tool (Nutzerwunsch, wie in WinMerge). Haltepunkte sind die erste Zeile jedes
/// Blocks geänderter Zeilen und die Stellen, an denen auf dieser Seite ein Leerraum steht (dort hat nur die andere Seite Text).
/// </summary>
public static class DiffNavigation
{
    /// <param name="changedLines">Zeilen (1-basiert), die als hinzugefügt, entfernt oder geändert eingefärbt sind.</param>
    /// <param name="gapLines">Zeilen, vor denen auf dieser Seite Leerraum steht (<see cref="AlignmentGap.BeforeLine"/>).</param>
    public static IReadOnlyList<int> Stops(IEnumerable<int> changedLines, IEnumerable<int> gapLines)
    {
        var changed = changedLines.ToHashSet();
        var stops = changed.Where(line => !changed.Contains(line - 1)).ToHashSet();
        foreach (var line in gapLines)
        {
            if (!changed.Contains(line) && !changed.Contains(line - 1))
            {
                stops.Add(line);
            }
        }

        return stops.OrderBy(line => line).ToList();
    }

    /// <summary>Der nächste Haltepunkt nach bzw. vor <paramref name="currentLine"/>; <see langword="null"/> am Ende (kein Umlauf).</summary>
    public static int? Next(IReadOnlyList<int> stops, int currentLine, bool forward) =>
        forward
            ? stops.Where(line => line > currentLine).Select(line => (int?)line).FirstOrDefault()
            : stops.Where(line => line < currentLine).Select(line => (int?)line).LastOrDefault();
}
