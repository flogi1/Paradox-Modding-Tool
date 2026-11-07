namespace ParadoxModTool.Performance;

/// <summary>Gemeinsame Regel der Text-Regeln: höchstens ein Hinweis je oberstem Block, sonst wiederholt jede Zeile eines großen Blocks dieselbe Warnung.</summary>
internal static class BlockDedupe
{
    /// <summary>Je oberstem Block (Pfad ohne Groß-/Kleinschreibung, Startzeile) der Hinweis mit dem größten Wert; bei Gleichstand der erste. Die Zeile des Hinweises bleibt.</summary>
    public static IReadOnlyList<PerformanceHint> Keep(IEnumerable<(ScriptBlockInfo Block, PerformanceHint Hint)> hints) =>
        hints.GroupBy(h => (h.Hint.Path.ToUpperInvariant(), h.Block.StartLine))
            .Select(g => g.MaxBy(h => h.Hint.Value).Hint)
            .ToList();
}
