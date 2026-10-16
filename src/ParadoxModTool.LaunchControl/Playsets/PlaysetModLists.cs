using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParadoxModTool.LaunchControl;

/// <summary>Stichwortsuche in der Mod-Liste eines Playsets und in der Auswahl "Mod hinzufügen".</summary>
public static class PlaysetModFilter
{
    /// <summary>Alle durch Leerzeichen getrennten Wörter müssen im Namen oder im Pfad vorkommen (Groß-/Kleinschreibung egal). Eine leere Suche trifft alles.</summary>
    public static bool Matches(PlaysetModEntry mod, string query)
    {
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var text = (mod.DisplayName ?? string.Empty) + " " + mod.Path;
        return words.All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Die Mods, die man einem Playset hinzufügen kann.</summary>
public static class PlaysetInstalledMods
{
    /// <summary>
    /// Bekannte Mods (bei Launcher-Spielen die <c>mods</c>-Tabelle), Unterordner des Mod-Ordners und jeder Pfad, der schon in einem Playset steht; ohne Doppelte
    /// (Pfade ohne Rücksicht auf Schreibweise und Schlusstrenner), nach Name sortiert. Bei Launcher-Spielen zählen nur Ordner mit Geschwisterdatei <c>.mod</c>, weil
    /// PMT nur damit eine neue Mod eintragen kann (<paramref name="requireDescriptor"/>). Der Name kommt aus <c>.metadata/metadata.json</c>, sonst aus dem Ordnernamen.
    /// </summary>
    public static IReadOnlyList<PlaysetModEntry> Collect(IEnumerable<PlaysetModEntry> known, string gameModFolder, IEnumerable<PlaysetData> playsets, bool requireDescriptor)
    {
        var result = new Dictionary<string, PlaysetModEntry>(StringComparer.OrdinalIgnoreCase);

        void Add(PlaysetModEntry mod)
        {
            var key = PlaysetGuard.NormalizePath(mod.Path).Replace('/', '\\');
            if (key.Length == 0)
            {
                return;
            }

            if (!result.TryGetValue(key, out var existing))
            {
                result[key] = mod with { Enabled = true };
            }
            else if (existing.DisplayName is null && mod.DisplayName is not null)
            {
                result[key] = existing with { DisplayName = mod.DisplayName };
            }
        }

        foreach (var mod in known)
        {
            Add(mod);
        }

        if (Directory.Exists(gameModFolder))
        {
            foreach (var folder in Directory.EnumerateDirectories(gameModFolder))
            {
                if (!requireDescriptor || File.Exists(folder + ".mod"))
                {
                    Add(new PlaysetModEntry(folder, ReadMetadataName(folder) ?? Path.GetFileName(folder), true, null));
                }
            }
        }

        foreach (var mod in playsets.SelectMany(p => p.Mods))
        {
            Add(mod with { DisplayName = mod.DisplayName ?? ReadMetadataName(mod.Path) ?? NameFromPath(mod.Path) });
        }

        return result.Values
            .Select(m => m with { DisplayName = m.DisplayName ?? NameFromPath(m.Path) })
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NameFromPath(string path) => Path.GetFileName(PlaysetGuard.NormalizePath(path));

    /// <summary>Der Name aus <c>.metadata/metadata.json</c> des Mod-Ordners; null, wenn es sie nicht gibt.</summary>
    public static string? ReadMetadataName(string folder)
    {
        try
        {
            var file = Path.Combine(folder, ".metadata", "metadata.json");
            return File.Exists(file) && JsonNode.Parse(File.ReadAllText(file)) is JsonObject root && root["name"] is JsonValue name && name.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
                ? text
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
