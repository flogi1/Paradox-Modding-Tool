using ParadoxModTool.Mods;

namespace ParadoxModTool.ModCompat;

/// <summary>Hilfen zum Anlegen von Set-Mitgliedern.</summary>
public static class CompatMembers
{
    /// <summary>
    /// Ein Mitglied für <paramref name="folder"/>; der Name kommt aus der Metadatendatei, sonst aus dem Ordnernamen.
    /// Bei <see cref="CompatOrigin.Workshop"/> mit klassischem Format (<see cref="ModMetadata.ClassicSiblingMetadataFile"/>)
    /// steht die Metadatendatei als <see cref="ModMetadata.ClassicWorkshopDescriptorFile"/> IM Ordner selbst, nicht daneben
    /// (Steam-Workshop-Konvention) — die Geschwisterdatei-Suche würde dort ins Leere laufen.
    /// </summary>
    public static CompatSetMember FromFolder(string folder, CompatOrigin origin, string? modId, string metadataFile)
    {
        var normalized = CompatSet.NormalizeFolder(folder);
        var name = origin == CompatOrigin.Workshop && metadataFile == ModMetadata.ClassicSiblingMetadataFile
            ? ModMetadata.ReadClassicFile(normalized, ModMetadata.ClassicWorkshopDescriptorFile)?.Name
            : ModMetadata.Read(normalized, metadataFile)?.Name;
        return new CompatSetMember
        {
            Folder = normalized,
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(normalized) : name,
            Origin = origin,
            ModId = modId,
        };
    }

    /// <summary>Alle installierten Workshop-Ordner des Spiels (<c>steamapps\workshop\content\&lt;appId&gt;\*</c>) in den angegebenen Steam-Bibliotheken, nach Ordnername.</summary>
    public static IReadOnlyList<string> FindWorkshopFolders(string appId, IEnumerable<string> libraryFolders)
    {
        var result = new List<string>();
        foreach (var library in libraryFolders)
        {
            var content = Path.Combine(library, "steamapps", "workshop", "content", appId);
            if (Directory.Exists(content))
            {
                result.AddRange(Directory.EnumerateDirectories(content));
            }
        }

        return result.OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToList();
    }
}
