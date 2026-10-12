namespace ParadoxModTool.Mods;

public enum ModDetectionKind
{
    None,

    /// <summary>Der Kandidat liegt in einer Mod des Registers.</summary>
    Known,

    /// <summary>Der Kandidat liegt in einem Ordner mit Metadaten-Datei, der noch keine Mod ist.</summary>
    Unknown,

    /// <summary>Der Kandidat liegt in einem Mod-Ordner, den der Nutzer bewusst nicht als Mod führt, oder in einer abonnierten Workshop-Mod. Die Suche endet dort, es gilt keine andere Mod.</summary>
    Ignored,
}

/// <summary>Ergebnis von <see cref="ModDetector.Detect"/>.</summary>
public sealed record ModDetection(ModDetectionKind Kind, ModInfo? Mod = null, string? Folder = null)
{
    public static ModDetection NoMod { get; } = new(ModDetectionKind.None);

    public static ModDetection ForMod(ModInfo mod) => new(ModDetectionKind.Known, mod);

    public static ModDetection ForFolder(string folder) => new(ModDetectionKind.Unknown, Folder: folder);

    public static ModDetection ForIgnoredFolder(string folder) => new(ModDetectionKind.Ignored, Folder: folder);
}

/// <summary>Erkennt, zu welcher Mod eine offene Datei oder ein Explorer-Ordner gehört.</summary>
public static class ModDetector
{
    /// <summary>
    /// Prüft die Kandidaten in der Reihenfolge ihres Rangs (offene Datei, Explorer-Mod-Ordner, Vanilla-Diff-Mod-Ordner). Je Kandidat gilt ein Treffer im Register;
    /// sonst der nächste Ordner nach oben mit <paramref name="metadataFile"/>, sofern er nicht unter <paramref name="excludedRoots"/>
    /// (Vanilla-Ordner, die selbst eine Metadaten-Datei haben können) liegt: Er ist entweder eine noch unbekannte Mod (<see cref="ModDetectionKind.Unknown"/>)
    /// oder ein ignorierter Mod-Ordner (<see cref="ModDetectionKind.Ignored"/>); in beiden Fällen endet die Suche, damit eine Datei einer anderen Mod nicht
    /// als Datei der Mod aus einem niedrigeren Rang gilt. Nicht vorhandene und ungültige Kandidaten sowie Dateien außerhalb jedes Mod-Ordners werden übersprungen.
    /// </summary>
    public static ModDetection Detect(ModRegistry registry, IEnumerable<string?> candidates, string metadataFile, IEnumerable<string>? excludedRoots = null)
    {
        var excluded = (excludedRoots ?? Array.Empty<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !(File.Exists(candidate) || Directory.Exists(candidate)))
            {
                continue;
            }

            if (registry.Resolve(candidate) is { } known)
            {
                return ModDetection.ForMod(known);
            }

            if (IsExcluded(candidate, excluded))
            {
                continue;
            }

            var folder = FindModFolder(candidate, metadataFile);
            if (folder is not null)
            {
                // Abonnierte Workshop-Mods sind fremde Mods: keine Rückfrage, sie gelten wie ein ignorierter Ordner (anlegen geht von Hand im Mods-Fenster)
                return registry.IsIgnored(folder) || CompatchTargets.IsWorkshopFolder(folder) ? ModDetection.ForIgnoredFolder(folder) : ModDetection.ForFolder(folder);
            }
        }

        return ModDetection.NoMod;
    }

    private static bool IsExcluded(string candidate, List<string> excluded) =>
        ModPaths.TryNormalize(candidate, out var normalized)
        && excluded.Any(root => ModPaths.TryNormalize(root, out var normalizedRoot) && ModPaths.IsUnder(normalized, normalizedRoot));

    /// <summary>
    /// Der nächste Ordner vom Kandidaten aufwärts (einschließlich), der <paramref name="metadataFile"/> hat; beim klassischen Format (<c>*.mod</c>) zählt eine
    /// Geschwisterdatei <c>&lt;Ordner&gt;.mod</c> oder eine <c>descriptor.mod</c> im Ordner (siehe <see cref="ModMetadata.ResolveFilePath"/>).
    /// </summary>
    private static string? FindModFolder(string candidate, string metadataFile)
    {
        if (string.IsNullOrWhiteSpace(metadataFile) || !ModPaths.TryNormalize(candidate, out var start))
        {
            return null;
        }

        var directory = File.Exists(start) ? Path.GetDirectoryName(start) : start;
        while (!string.IsNullOrEmpty(directory))
        {
            if (ModMetadata.ResolveFilePath(directory, metadataFile) is not null)
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }
}
