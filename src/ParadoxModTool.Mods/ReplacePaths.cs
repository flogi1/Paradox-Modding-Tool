namespace ParadoxModTool.Mods;

/// <summary>
/// Ordner, deren Vanilla-Dateien eine Mod nicht laden lässt (<c>replace_path</c> in <c>.mod</c>/<c>descriptor.mod</c>,
/// <c>game_custom_data.replace_paths</c> in <c>metadata.json</c>). Betroffen sind nur Dateien direkt im Ordner, nicht in
/// Unterordnern (siehe docs/superpowers/specs/2026-09-30-ck3-part1-design.md, Abschnitt 4). Ob auch Dateien früher ladender Mods
/// betroffen sind, sagt das Spielprofil (<c>replacePathHidesEarlierMods</c>).
/// </summary>
public static class ReplacePaths
{
    /// <summary>Einheitliche Form: <c>/</c> als Trenner, ohne Leerraum und ohne Schrägstrich am Anfang oder Ende.</summary>
    public static string Normalize(string path) => path.Trim().Replace('\\', '/').Trim('/');

    /// <summary>
    /// Liegt <paramref name="relativeFile"/> direkt in einem der Ordner? Groß-/Kleinschreibung zählt nicht. Mit <paramref name="roots"/> (EU V:
    /// <c>in_game</c>, <c>main_menu</c>, <c>loading_screen</c>) nennt das Spiel die Ordner ohne diese Wurzel: <c>common/x</c> trifft
    /// <c>in_game/common/x</c>, und Einträge, die selbst mit einer Wurzel beginnen, wirken nicht (gemessen 2026-09-30).
    /// </summary>
    public static bool Hides(IEnumerable<string> hiddenFolders, string relativeFile, IReadOnlyCollection<string>? roots = null)
    {
        var normalized = Normalize(relativeFile);
        var slash = normalized.LastIndexOf('/');
        var directory = slash < 0 ? string.Empty : normalized[..slash];
        if (StartsWithRoot(directory, roots))
        {
            var rootSlash = directory.IndexOf('/');
            directory = rootSlash < 0 ? string.Empty : directory[(rootSlash + 1)..];
        }

        return hiddenFolders.Any(folder =>
        {
            var hidden = Normalize(folder);
            return !StartsWithRoot(hidden, roots) && string.Equals(hidden, directory, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Alle Ordner, die die Mods in <paramref name="folders"/> ausblenden, ohne Doppel und sortiert. Einträge, die mit einer der
    /// <paramref name="roots"/> beginnen, fehlen, weil das Spiel sie nicht beachtet.
    /// </summary>
    public static IReadOnlyList<string> Of(IEnumerable<string> folders, string metadataFile, IReadOnlyCollection<string>? roots = null) =>
        folders
            .Select(folder => ModMetadata.Read(folder, metadataFile))
            .Where(metadata => metadata is not null)
            .SelectMany(metadata => metadata!.ReplacePaths)
            .Where(path => !StartsWithRoot(path, roots))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool StartsWithRoot(string path, IReadOnlyCollection<string>? roots)
    {
        if (roots is null || roots.Count == 0)
        {
            return false;
        }

        var slash = path.IndexOf('/');
        var first = slash < 0 ? path : path[..slash];
        return roots.Any(root => string.Equals(root, first, StringComparison.OrdinalIgnoreCase));
    }
}
