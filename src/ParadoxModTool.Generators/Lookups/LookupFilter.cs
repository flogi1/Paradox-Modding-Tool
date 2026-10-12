namespace ParadoxModTool.Generators.Lookups;

/// <summary>Filter des Fensters "Aus Datenbank": Suchwörter, Art (Quelle) und Tag.</summary>
public static class LookupFilter
{
    /// <summary>
    /// Jedes Wort der Suche muss in Name, Beschreibung, Schlüssel, Tag oder Nation vorkommen (Groß-/Kleinschreibung egal); <paramref name="sourceId"/> leer = alle
    /// Quellen; <paramref name="tag"/> muss im Tag enthalten sein.
    /// </summary>
    public static IReadOnlyList<LookupEntry> Apply(IReadOnlyList<LookupEntry> entries, string search, string? sourceId, string tag)
    {
        var words = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var wantedTag = tag.Trim();
        return entries.Where(entry =>
                (string.IsNullOrEmpty(sourceId) || entry.SourceId == sourceId)
                && (wantedTag.Length == 0 || entry.Tag.Contains(wantedTag, StringComparison.OrdinalIgnoreCase))
                && words.All(word =>
                    entry.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || entry.Desc.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || entry.Key.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || entry.Tag.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || entry.Nation.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
