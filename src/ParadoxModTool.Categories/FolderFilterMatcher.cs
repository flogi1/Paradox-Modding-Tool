namespace ParadoxModTool.Categories;

/// <summary>Prüft, ob eine Datei in einem Ordner-Filter (<see cref="FolderFilterDefinition"/>) liegt.</summary>
public static class FolderFilterMatcher
{
    /// <summary>
    /// Liegt <paramref name="filePath"/> unterhalb von <paramref name="folderPath"/>? Verglichen wird mit Trenner am Ende, damit
    /// "Idea Variation" nicht auch "Idea Variation BT Addon" trifft. Groß-/Kleinschreibung und "/" statt "\" spielen keine Rolle.
    /// </summary>
    public static bool Contains(string folderPath, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var folder = Normalize(folderPath).TrimEnd('/') + "/";
        return Normalize(filePath).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ohne aktiven Filter wird alles gezeigt; sonst Dateien, die in mindestens einem aktiven Filter liegen.</summary>
    public static bool Accepts(IReadOnlyCollection<FolderFilterDefinition> active, string? filePath) =>
        active.Count == 0 || active.Any(f => Contains(f.FolderPath, filePath));

    /// <summary>Gespeicherte Filter bereinigen: ohne Kennung, Name oder Ordner und doppelte Kennungen entfallen.</summary>
    public static List<FolderFilterDefinition> Prune(IEnumerable<FolderFilterDefinition?>? filters)
    {
        var result = new List<FolderFilterDefinition>();
        foreach (var filter in filters ?? Enumerable.Empty<FolderFilterDefinition?>())
        {
            if (filter is null
                || string.IsNullOrWhiteSpace(filter.Id)
                || string.IsNullOrWhiteSpace(filter.Name)
                || string.IsNullOrWhiteSpace(filter.FolderPath)
                || result.Any(f => f.Id == filter.Id))
            {
                continue;
            }

            result.Add(filter.Clone());
        }

        return result;
    }

    /// <summary>Namensvorschlag für einen neuen Filter: der Ordnername.</summary>
    public static string SuggestName(string folderPath) =>
        Path.GetFileName(Normalize(folderPath).TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));

    private static string Normalize(string path) => path.Trim().Replace('\\', '/');
}
