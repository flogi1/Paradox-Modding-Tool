using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParadoxModTool.Generators.Ai;

/// <summary>
/// Paketdateien als Satz (relativer Pfad → Text) für den Rückweg von der KI: aus einem Ordner lesen, fehlendes Spiel ergänzen und als .pmtgen packen,
/// damit der vorhandene Import (Probe-Laden, Kennungs-Rückfrage) sie übernimmt.
/// </summary>
public static class PackageFileSet
{
    /// <summary>Nur erlaubte Dateien (<see cref="AiResponseParser.IsAllowed"/>); ohne package.json ein Fehler.</summary>
    public static IReadOnlyDictionary<string, string> FromFolder(string folder)
    {
        if (!File.Exists(Path.Combine(folder, "package.json")))
        {
            throw new GeneratorPackageException(GeneratorsTexts.PackageJsonMissing(folder));
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');
            if (AiResponseParser.IsAllowed(relative))
            {
                files[relative] = File.ReadAllText(path);
            }
        }

        return files;
    }

    /// <summary>Setzt <c>gameId</c>, wenn er fehlt oder leer ist; ungültiges JSON bleibt, wie es ist (der Loader meldet es).</summary>
    public static IReadOnlyDictionary<string, string> WithGameId(IReadOnlyDictionary<string, string> files, string gameId)
    {
        if (!files.TryGetValue("package.json", out var text))
        {
            return files;
        }

        JsonObject? json;
        try
        {
            json = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
        }
        catch (JsonException)
        {
            return files;
        }

        if (json is null || json["gameId"] is JsonValue existing && existing.ToString().Length > 0)
        {
            return files;
        }

        json["gameId"] = gameId;
        var result = files.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        result["package.json"] = json.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        return result;
    }

    /// <summary>Schreibt die Dateien als Zip, das <see cref="Editing.Package.GeneratorPackageArchive.Read"/> lesen kann.</summary>
    public static void WriteArchive(IReadOnlyDictionary<string, string> files, string targetFile)
    {
        using var stream = File.Create(targetFile);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (path, text) in files.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            using var entry = zip.CreateEntry(path).Open();
            entry.Write(new UTF8Encoding(false).GetBytes(text));
        }
    }
}
