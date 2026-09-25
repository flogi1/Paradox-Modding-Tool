namespace ParadoxModTool.Categories;

/// <summary>
/// Schlüssel der Häkchen in der Filterleiste (Herkunft und Kategorien), damit sich einzelne Häkchen aus der Leiste
/// ausblenden und in der Session speichern lassen.
/// </summary>
public static class FilterEntryKeys
{
    private const string OriginPrefix = "origin:";
    private const string CategoryPrefix = "category:";

    public static string ForOrigin(FileOrigin origin) => OriginPrefix + origin;

    public static string ForCategory(string categoryId) => CategoryPrefix + categoryId;

    /// <summary>
    /// Bereinigt gespeicherte Schlüssel: unbekannte Herkünfte, Kategorien, die es nicht mehr gibt, Duplikate und
    /// unlesbare Einträge entfallen.
    /// </summary>
    public static List<string> Prune(IEnumerable<string>? keys, IEnumerable<string> existingCategoryIds)
    {
        var categories = new HashSet<string>(existingCategoryIds);
        var result = new List<string>();
        foreach (var key in keys ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(key) || result.Contains(key))
            {
                continue;
            }

            var valid = key.StartsWith(OriginPrefix, StringComparison.Ordinal)
                    ? Enum.TryParse<FileOrigin>(key[OriginPrefix.Length..], out var origin) && Enum.IsDefined(origin)
                    : key.StartsWith(CategoryPrefix, StringComparison.Ordinal) && categories.Contains(key[CategoryPrefix.Length..]);
            if (valid)
            {
                result.Add(key);
            }
        }

        return result;
    }
}
