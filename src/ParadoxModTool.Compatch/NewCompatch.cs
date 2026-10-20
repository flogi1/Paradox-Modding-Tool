using System.Text;
using System.Text.RegularExpressions;
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

    /// <param name="supportedGameVersion">Spielversion aus den Metadaten der ersten Ziel-Mod; ohne Angabe <c>*</c> bzw. leer.</param>
    /// <param name="installedGameVersion">Installierte Spielversion (launcher-settings.json), für <paramref name="majorMinorSupportedVersion"/>.</param>
    /// <param name="majorMinorSupportedVersion">Das Spiel nimmt im Deskriptor nur <c>vX.Y.Z</c> oder <c>vX.Y.*</c> (Stellaris), siehe <see cref="SupportedVersion"/>.</param>
    public static NewCompatchResult Create(string modRoot, string name, string metadataFile, IReadOnlyList<NewCompatchTarget> targets, string? supportedGameVersion,
        string? installedGameVersion = null, bool majorMinorSupportedVersion = false)
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
            var supported = SupportedVersion(supportedGameVersion, installedGameVersion, majorMinorSupportedVersion);
            var descriptor = $"version=\"1.0.0\"\nname=\"{name.Trim()}\"\nsupported_version=\"{supported}\"\n";
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

    private static readonly Regex ValidMajorMinor = new(@"^v?(\d+)\.(\d+)\.(\d+|\*)$", RegexOptions.Compiled);

    private static readonly Regex MajorMinor = new(@"v?(\d+)\.(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// <c>supported_version</c> für den klassischen Deskriptor. Ohne <paramref name="majorMinor"/> wie bisher die Version der Ziel-Mod oder <c>*</c>.
    /// Mit <paramref name="majorMinor"/> (Stellaris nimmt nur <c>v4.5.2</c> oder <c>v4.5.*</c>, gemessen 2026-10-16): eine gültige Version der Ziel-Mod
    /// bleibt (fehlendes <c>v</c> wird ergänzt), sonst Haupt- und Nebenversion der installierten Version (<c>v4.5.2</c> oder
    /// <c>Cygnus v4.5.2 (9776)</c> → <c>v4.5.*</c>), sonst die der Ziel-Mod. Ist gar nichts bekannt, bleibt <c>*</c>; das meldet Stellaris als
    /// ungültig, die weitere Wirkung ist nicht gemessen.
    /// </summary>
    public static string SupportedVersion(string? fromMod, string? installed, bool majorMinor)
    {
        if (!majorMinor)
        {
            return fromMod ?? "*";
        }

        var mod = fromMod?.Trim() ?? string.Empty;
        if (ValidMajorMinor.Match(mod) is { Success: true } valid)
        {
            return $"v{valid.Groups[1].Value}.{valid.Groups[2].Value}.{valid.Groups[3].Value}";
        }

        foreach (var source in new[] { installed, mod })
        {
            if (source is not null && MajorMinor.Match(source) is { Success: true } match)
            {
                return $"v{match.Groups[1].Value}.{match.Groups[2].Value}.*";
            }
        }

        return "*";
    }
}
