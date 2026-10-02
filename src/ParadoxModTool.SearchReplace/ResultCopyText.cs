namespace ParadoxModTool.SearchReplace;

/// <summary>Mehrere markierte Treffer einer Trefferliste als ein Text für die Zwischenablage und das Textfeld unter der Liste.</summary>
public static class ResultCopyText
{
    private static readonly string Separator = Environment.NewLine + Environment.NewLine;

    /// <summary>Je Treffer ein Block, durch eine Leerzeile getrennt, in der Reihenfolge der Liste.</summary>
    public static string Join(IEnumerable<string> texts) => string.Join(Separator, texts);

    /// <summary>Wie <see cref="Join"/>, aber höchstens <paramref name="limit"/> Treffer; <paramref name="hidden"/> sind die weggelassenen.</summary>
    public static string Preview(IReadOnlyList<string> texts, int limit, out int hidden)
    {
        hidden = Math.Max(0, texts.Count - limit);
        return Join(texts.Take(limit));
    }
}
