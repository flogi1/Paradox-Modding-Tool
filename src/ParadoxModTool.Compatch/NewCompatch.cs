using System.Text;
using System.Text.Json;
using ParadoxModTool.Mods;

namespace ParadoxModTool.Compatch;

public sealed record NewCompatchTarget(string Name, string? MetadataId);

/// <param name="WithoutDependency">Ziele ohne <c>id</c> in ihren Metadaten; für sie steht keine Abhängigkeit in der Metadatendatei.</param>
public sealed record NewCompatchResult(string Folder, IReadOnlyList<string> WithoutDependency);

/// <summary>Legt den Ordner eines neuen Kompatchs samt Metadaten an (Spec, "Kompatch-Art im Register"; Ruling 7).</summary>
public static class NewCompatch
{
    public static string FolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray()).TrimEnd('.', ' ');
    }

    public static NewCompatchResult Create(string modRoot, string name, string metadataFile, IReadOnlyList<NewCompatchTarget> targets, string? supportedGameVersion)
    {
        var folderName = FolderName(name);
        if (folderName.Length == 0)
        {
            throw new CompatchInputException(CompatchTexts.NewCompatchNameEmpty);
        }

        var folder = Path.Combine(modRoot, folderName);
        if (Directory.Exists(folder) || File.Exists(folder))
        {
            throw new CompatchInputException(CompatchTexts.NewCompatchExists(folder));
        }

        var names = string.Join(" + ", targets.Select(t => t.Name));
        Directory.CreateDirectory(folder);
        var without = new List<string>();
        if (metadataFile == ModMetadata.ClassicSiblingMetadataFile)
        {
            var descriptor = $"version=\"1.0.0\"\nname=\"{name.Trim()}\"\nsupported_version=\"{supportedGameVersion ?? "*"}\"\n";
            File.WriteAllText(Path.Combine(folder, "descriptor.mod"), descriptor, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(modRoot, folderName + ".mod"), descriptor + $"path=\"mod/{folderName}\"\n", new UTF8Encoding(false));
            return new NewCompatchResult(folder, without);
        }

        var relationships = new List<object>();
        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.MetadataId))
            {
                without.Add(target.Name);
                continue;
            }

            relationships.Add(new Dictionary<string, string>
            {
                ["rel_type"] = "dependency",
                ["id"] = target.MetadataId.Trim(),
                ["display_name"] = target.Name,
                ["resource_type"] = "mod",
                ["version"] = "*",
            });
        }

        var metadata = new Dictionary<string, object>
        {
            ["name"] = name.Trim(),
            ["id"] = string.Empty,
            ["version"] = "1.0.0",
            ["supported_game_version"] = supportedGameVersion ?? string.Empty,
            ["short_description"] = CompatchTexts.NewCompatchDescription(names),
            ["tags"] = Array.Empty<string>(),
            ["relationships"] = relationships,
            ["game_custom_data"] = new Dictionary<string, bool> { ["multiplayer_synchronized"] = true },
        };
        var path = Path.Combine(folder, metadataFile.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return new NewCompatchResult(folder, without);
    }
}
