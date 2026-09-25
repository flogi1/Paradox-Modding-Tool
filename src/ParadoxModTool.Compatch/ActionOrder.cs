namespace ParadoxModTool.Compatch;

/// <summary>Eine Reihenfolge für Ordnerbaum, Karten und ◀ ▶ (Plan 5, Ruling 2): Ordnerstruktur und Datei, dann Anzeigepfad.</summary>
public static class ActionOrder
{
    public static IReadOnlyList<MergeAction> Sort(IEnumerable<MergeAction> actions) => actions
        .OrderBy(TreePath.FileOf, TreePath.Comparer)
        .ThenBy(a => a.DisplayPath, StringComparer.Ordinal)
        .ToList();

    public static int IndexOf(IReadOnlyList<MergeAction> ordered, string id)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Nachbar in Richtung <paramref name="direction"/> (+1/-1). Fehlende oder unbekannte ID: erste bzw. letzte Aktion. Am Rand: <see langword="null"/>.</summary>
    public static MergeAction? Step(IReadOnlyList<MergeAction> ordered, string? currentId, int direction)
    {
        if (ordered.Count == 0)
        {
            return null;
        }

        var index = currentId is null ? -1 : IndexOf(ordered, currentId);
        if (index < 0)
        {
            return direction >= 0 ? ordered[0] : ordered[^1];
        }

        var next = index + Math.Sign(direction);
        return next >= 0 && next < ordered.Count ? ordered[next] : null;
    }
}
