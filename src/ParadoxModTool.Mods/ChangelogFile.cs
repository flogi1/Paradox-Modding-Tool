namespace ParadoxModTool.Mods;

/// <summary>Sucht die Changelog-Datei einer Mod. Lesen und Schreiben übernimmt <see cref="ModTextFile"/>.</summary>
public static class ChangelogFile
{
    private static readonly string[] CandidateFolders = { "readme", "docs", "doc" };

    /// <summary>
    /// Sucht die Changelog-Datei einer Mod: eine Datei <c>changelog*.txt</c> oder <c>changelog*.md</c> in der Wurzel, sonst in <c>Readme</c> oder <c>docs</c>.
    /// Gibt den Pfad relativ zum Mod-Ordner mit <c>/</c> zurück, sonst <see langword="null"/>.
    /// </summary>
    public static string? Suggest(string modFolder)
    {
        if (!Directory.Exists(modFolder))
        {
            return null;
        }

        var folders = new List<(string Path, string Relative)> { (modFolder, string.Empty) };
        folders.AddRange(Directory.EnumerateDirectories(modFolder)
            .Select(d => (Path: d, Name: Path.GetFileName(d)))
            .Where(d => CandidateFolders.Contains(d.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => (d.Path, d.Name + "/")));

        foreach (var (folder, relative) in folders)
        {
            var match = Directory.EnumerateFiles(folder)
                .Select(Path.GetFileName)
                .Where(name => name is not null && IsChangelogName(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (match is not null)
            {
                return relative + match;
            }
        }

        return null;
    }

    private static bool IsChangelogName(string name) =>
        name.StartsWith("changelog", StringComparison.OrdinalIgnoreCase)
        && (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
}
