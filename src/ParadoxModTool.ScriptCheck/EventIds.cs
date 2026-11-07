namespace ParadoxModTool.ScriptCheck;

/// <summary>Vergleichsschlüssel für Event-Ids: <c>ns.0001</c>, <c>ns.1</c> und <c>NS.1</c> sind dasselbe Event.</summary>
public static class EventIds
{
    /// <summary>
    /// Kleingeschrieben (invariant), dann <c>präfix.ziffern</c> → <c>präfix.</c> plus Ziffern ohne führende Nullen (<c>0</c> bleibt); reine
    /// Ziffern-Ids ohne führende Nullen; alles andere unverändert. Schlüssel werden ordinal verglichen.
    /// </summary>
    /// <remarks>
    /// Grenze: Erwähnungen (<see cref="ScriptCheckReader.ReadMentions"/>) finden Namen weiterhin ordinal; ein Event, das nur in anderer
    /// Schreibweise erwähnt (nicht aufgerufen) wird, gilt dort nicht als erwähnt. Aufrufe, Definitionen und Abfragen nutzen diesen Schlüssel.
    /// </remarks>
    public static string Key(string id)
    {
        id = id.ToLowerInvariant();
        var dot = id.LastIndexOf('.');
        var digits = id[(dot + 1)..];
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            return id;
        }

        var trimmed = digits.TrimStart('0');
        if (trimmed.Length == 0)
        {
            trimmed = "0";
        }

        return trimmed.Length == digits.Length ? id : id[..(dot + 1)] + trimmed;
    }

    /// <summary>Der Namespace einer Id (Teil vor dem ersten Punkt, kleingeschrieben); null ohne Punkt, bei leerem Teil oder wenn er <c>$</c> enthält.</summary>
    public static string? Namespace(string id)
    {
        var dot = id.IndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var ns = id[..dot];
        return ns.Contains('$') ? null : ns.ToLowerInvariant();
    }
}
