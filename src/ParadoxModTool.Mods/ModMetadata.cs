using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Mods;

/// <summary>Die Felder der Metadaten-Datei einer Mod, die das Programm braucht. Fehlende Felder sind leere Texte.</summary>
/// <param name="Folder">Der Mod-Ordner, in dem die Datei gefunden wurde.</param>
public sealed record ModMetadata(string Name, string Version, string SupportedGameVersion, string Folder)
{
    /// <summary>Die Kennung der Mod im Spiel (<c>id</c>, z. B. <c>Idea_Variation_2</c>), leer wenn nicht vorhanden. Nicht die Workshop-ID.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Kurzbeschreibung (<c>short_description</c>), leer wenn nicht vorhanden.</summary>
    public string ShortDescription { get; init; } = string.Empty;

    /// <summary>Dateiname des Vorschaubilds (<c>picture</c>, relativ zum Ordner der Metadaten-Datei), leer wenn nicht vorhanden.</summary>
    public string Picture { get; init; } = string.Empty;

    /// <summary>Ordner, deren Vanilla-Dateien die Mod nicht laden lässt (<c>replace_path</c>), in der Form von <see cref="global::ParadoxModTool.Mods.ReplacePaths.Normalize"/>.</summary>
    public IReadOnlyList<string> ReplacePaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Workshop-ID aus der klassischen Metadaten-Datei (<c>remote_file_id</c>, vom Launcher beim Hochladen eingetragen), leer wenn nicht vorhanden.
    /// Das Launcher-v2-Format kennt kein solches Feld.
    /// </summary>
    public string RemoteFileId { get; init; } = string.Empty;

    /// <summary>Abhängigkeiten der Mod (<c>dependencies</c> bzw. <c>relationships</c> mit <c>rel_type</c> <c>dependency</c>), leer wenn keine.</summary>
    public IReadOnlyList<ModDependency> Dependencies { get; init; } = Array.Empty<ModDependency>();

    /// <summary>
    /// Sentinel-Wert für <c>GameProfile.MetadataFile</c>: statt einer JSON-Datei im Mod-Ordner (Launcher-v2-Format)
    /// wird die klassische Paradox-Mod-Metadatendatei gelesen — eine Geschwisterdatei <c>&lt;Ordnername&gt;.mod</c>
    /// NEBEN dem Mod-Ordner, im Clausewitz-Skriptformat (<c>key = "value"</c>), wie sie EU IV und andere Spiele
    /// mit klassischem Mod-Format nutzen.
    /// </summary>
    public const string ClassicSiblingMetadataFile = "*.mod";

    /// <summary>
    /// Fester Dateiname der Steam-Workshop-Metadatendatei im klassischen Format: liegt, anders als
    /// <see cref="ClassicSiblingMetadataFile"/>, INNERHALB des Workshop-Ordners (<c>steamapps/workshop/content/&lt;appId&gt;/&lt;ID&gt;/descriptor.mod</c>),
    /// weil Steam beim Hochladen genau diese Datei im Inhalt selbst verlangt. Siehe <see cref="ReadClassicFile"/>.
    /// </summary>
    public const string ClassicWorkshopDescriptorFile = "descriptor.mod";

    /// <summary>
    /// Gehört die Datei zur Beschreibung der Mod und nicht zum Spielinhalt? Das sind alle Dateien direkt im Mod-Ordner (<c>descriptor.mod</c>,
    /// <c>thumbnail.png</c> …) und alles unter <c>.metadata/</c>. Das Spiel lädt sie nicht als Inhalt; zwei Mods geraten darüber nie in Konflikt.
    /// </summary>
    public static bool IsDescriptionFile(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').TrimStart('/');
        return !path.Contains('/') || path.StartsWith(".metadata/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Liest die Metadaten-Datei (Pfad relativ zum Mod-Ordner, aus dem Spielprofil; siehe auch
    /// <see cref="ClassicSiblingMetadataFile"/> für das klassische Format). Verträgt BOM, Tabs, Kommentare und Komma am Ende.
    /// <see langword="null"/>, wenn die Datei fehlt, nicht lesbar oder kein JSON-Objekt ist.
    /// </summary>
    public static ModMetadata? Read(string folder, string metadataFile)
    {
        if (string.IsNullOrWhiteSpace(metadataFile))
        {
            return null;
        }

        if (metadataFile == ClassicSiblingMetadataFile)
        {
            return ReadClassicSibling(folder);
        }

        try
        {
            var file = Path.Combine(folder, metadataFile);
            if (!File.Exists(file))
            {
                return null;
            }

            using var document = JsonDocument.Parse(
                File.ReadAllText(file),
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new ModMetadata(
                Field(document.RootElement, "name"),
                Field(document.RootElement, "version"),
                Field(document.RootElement, "supported_game_version"),
                folder)
            {
                Id = Field(document.RootElement, "id"),
                ShortDescription = Field(document.RootElement, "short_description"),
                Picture = Field(document.RootElement, "picture"),
                ReplacePaths = JsonReplacePaths(document.RootElement),
                Dependencies = JsonDependencies(document.RootElement),
            };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Die Metadaten der Mod aus dem ersten ihrer Ordner, der eine lesbare Metadaten-Datei hat.</summary>
    public static ModMetadata? Find(ModInfo mod, string metadataFile) =>
        mod.Paths.Select(path => Read(path, metadataFile)).FirstOrDefault(m => m is not null);

    /// <summary>Liest die klassische Geschwisterdatei <c>&lt;Ordnername&gt;.mod</c> neben <paramref name="folder"/>; siehe <see cref="ClassicSiblingMetadataFile"/>.</summary>
    private static ModMetadata? ReadClassicSibling(string folder)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(folder);
        var parent = Path.GetDirectoryName(trimmed);
        var name = Path.GetFileName(trimmed);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return null;
        }

        // Passt keine Geschwisterdatei zum Ordnernamen (z. B. "AGOT Flogis Buildings Mod.mod" neben "AGOT Flogis Buildings"), gilt die descriptor.mod im Ordner.
        return ReadClassicFileAt(Path.Combine(parent, name + ".mod"), folder)
            ?? ReadClassicFileAt(Path.Combine(folder, ClassicWorkshopDescriptorFile), folder);
    }

    /// <summary>
    /// Liest eine klassische Metadaten-Datei INNERHALB von <paramref name="folder"/> (z. B. <see cref="ClassicWorkshopDescriptorFile"/>
    /// für Workshop-Ordner), im Gegensatz zu <see cref="ClassicSiblingMetadataFile"/>, die daneben liegt.
    /// <see langword="null"/>, wenn die Datei fehlt oder nicht lesbar ist.
    /// </summary>
    public static ModMetadata? ReadClassicFile(string folder, string relativePath)
    {
        try
        {
            return ReadClassicFileAt(Path.Combine(folder, relativePath), folder);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static ModMetadata? ReadClassicFileAt(string file, string folder)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }

            var content = File.ReadAllText(file);

            return new ModMetadata(
                ClassicField(content, "name"),
                ClassicField(content, "version"),
                ClassicField(content, "supported_version"),
                folder)
            {
                Picture = ClassicField(content, "picture"),
                ReplacePaths = Regex.Matches(content, @"^[ \t]*replace_path[ \t]*=[ \t]*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Multiline)
                    .Select(m => global::ParadoxModTool.Mods.ReplacePaths.Normalize(m.Groups[1].Value))
                    .Where(p => p.Length > 0)
                    .ToList(),
                Dependencies = ClassicDependencies(content),
                RemoteFileId = ClassicField(content, "remote_file_id"),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Liest <c>key = "value"</c> am Zeilenanfang (Clausewitz-Skriptformat); eine mit <c>#</c> beginnende Zeile matcht nie. Leerer Text, wenn nicht vorhanden.</summary>
    private static string ClassicField(string content, string key)
    {
        var match = Regex.Match(
            content,
            $@"^[ \t]*{Regex.Escape(key)}[ \t]*=[ \t]*""([^""]*)""",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    /// <summary>
    /// Die Namen im Block <c>dependencies = { "A" "B" }</c> (klassisches Format). Kommentare (<c>#</c> bis Zeilenende) zählen nicht, der Block darf
    /// über mehrere Zeilen gehen.
    /// </summary>
    private static IReadOnlyList<ModDependency> ClassicDependencies(string content)
    {
        var withoutComments = string.Join("\n", content.Split('\n').Select(line => line.IndexOf('#') is var hash and >= 0 ? line[..hash] : line));
        var start = Regex.Match(withoutComments, @"^[ \t]*dependencies[ \t]*=[ \t]*\{", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        if (!start.Success)
        {
            return Array.Empty<ModDependency>();
        }

        var end = withoutComments.IndexOf('}', start.Index + start.Length);
        var block = end < 0 ? withoutComments[(start.Index + start.Length)..] : withoutComments[(start.Index + start.Length)..end];
        return Regex.Matches(block, "\"([^\"]*)\"")
            .Select(m => m.Groups[1].Value.Trim())
            .Where(name => name.Length > 0)
            .Select(name => new ModDependency(null, name))
            .ToList();
    }

    /// <summary>Die <c>relationships</c>-Einträge mit <c>rel_type</c> <c>dependency</c> (Launcher-v2-Format); leer, wenn es keine gibt.</summary>
    private static IReadOnlyList<ModDependency> JsonDependencies(JsonElement root)
    {
        if (!root.TryGetProperty("relationships", out var relationships) || relationships.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ModDependency>();
        }

        var result = new List<ModDependency>();
        foreach (var entry in relationships.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !string.Equals(Field(entry, "rel_type"), "dependency", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = Field(entry, "id");
            var display = Field(entry, "display_name");
            if (id.Length == 0 && display.Length == 0)
            {
                continue;
            }

            result.Add(new ModDependency(id.Length > 0 ? id : null, display.Length > 0 ? display : id));
        }

        return result;
    }

    /// <summary><c>game_custom_data.replace_paths</c> (Launcher-v2-Format); leer, wenn es fehlt oder keine Liste von Texten ist.</summary>
    private static IReadOnlyList<string> JsonReplacePaths(JsonElement root)
    {
        if (!root.TryGetProperty("game_custom_data", out var custom) || custom.ValueKind != JsonValueKind.Object
            || !custom.TryGetProperty("replace_paths", out var paths) || paths.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return paths.EnumerateArray()
            .Where(p => p.ValueKind == JsonValueKind.String)
            .Select(p => global::ParadoxModTool.Mods.ReplacePaths.Normalize(p.GetString() ?? string.Empty))
            .Where(p => p.Length > 0)
            .ToList();
    }

    private static string Field(JsonElement root, string name)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(name, "name", StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()?.Trim() ?? string.Empty
                    : string.Empty;
            }

            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString()?.Trim() ?? string.Empty,
                JsonValueKind.Number => property.Value.GetRawText(),
                _ => string.Empty,
            };
        }

        return string.Empty;
    }
}
