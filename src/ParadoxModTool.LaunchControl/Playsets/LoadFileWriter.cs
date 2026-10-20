using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Schreibt die Lade-Datei, die das Spiel beim direkten Start liest (der Launcher schreibt sie sonst beim Start): <c>dlc_load.json</c>
/// (CK III, EU IV) oder <c>content_load.json</c> (Vic 3). Andere Schlüssel einer vorhandenen Datei bleiben, nur die Mod- und DLC-Schlüssel werden ersetzt.
/// Stand der Messung: Format von CK III und EU IV aus vom Launcher geschriebenen Dateien (2026-10-16); die Behandlung von Workshop-Mods bei Vic 3
/// (<c>enabledUGC</c>) ist noch nicht gemessen, dort stehen alle Mods als <c>enabledMods</c> mit Pfad.
/// </summary>
public static partial class LoadFileWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [GeneratedRegex("^\\s*path\\s*=\\s*\"([^\"]*)\"", RegexOptions.Multiline)]
    private static partial Regex PathLine();

    [GeneratedRegex("^\\s*archive\\s*=\\s*\"([^\"]*)\"", RegexOptions.Multiline)]
    private static partial Regex ArchiveLine();

    /// <summary>Schreibt <paramref name="loadFilePath"/> für das Playset; Mods in der Reihenfolge der Liste, ausgeschaltete entfallen.</summary>
    public static PlaysetResult Write(string loadFilePath, PlaysetData playset, string gameModFolder)
    {
        var kind = Path.GetFileName(loadFilePath).ToLowerInvariant();
        if (kind is not ("dlc_load.json" or "content_load.json"))
        {
            return new PlaysetResult(PlaysetStatus.LoadFileFailed, loadFilePath);
        }

        try
        {
            var root = File.Exists(loadFilePath) && JsonNode.Parse(File.ReadAllText(loadFilePath)) is JsonObject existing ? existing : new JsonObject();
            var enabled = playset.Mods.Where(m => m.Enabled).ToList();
            var disabledDlcs = new JsonArray(playset.Dlcs.Where(d => !d.Enabled).Select(d => (JsonNode)JsonValue.Create(d.Id)!).ToArray());

            if (kind == "dlc_load.json")
            {
                var registry = ReadModFiles(gameModFolder);
                var names = new List<JsonNode>();
                foreach (var mod in enabled)
                {
                    if (!registry.TryGetValue(PlaysetGuard.NormalizePath(mod.Path).Replace('/', '\\'), out var name))
                    {
                        return new PlaysetResult(PlaysetStatus.LoadFileFailed, PlaysetGuard.ModLabel(mod));
                    }

                    names.Add(JsonValue.Create(name)!);
                }

                root["enabled_mods"] = new JsonArray(names.ToArray());
                root["disabled_dlcs"] = disabledDlcs;
            }
            else
            {
                root["enabledMods"] = new JsonArray(enabled.Select(m => (JsonNode)new JsonObject { ["path"] = m.Path.Replace('/', '\\').TrimEnd('\\') }).ToArray());
                root["disabledDLC"] = disabledDlcs;
                root["enabledUGC"] ??= new JsonArray();
            }

            var temp = loadFilePath + ".tmp";
            try
            {
                File.WriteAllText(temp, root.ToJsonString(WriteOptions));
                File.Move(temp, loadFilePath, overwrite: true);
            }
            catch
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }

                throw;
            }

            return PlaysetResult.Ok;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new PlaysetResult(PlaysetStatus.LoadFileFailed, ex.Message);
        }
    }

    /// <summary>Alle <c>.mod</c>-Dateien des Mod-Ordners: Ordner aus <c>path=</c> (einheitlich geschrieben) auf <c>mod/&lt;Dateiname&gt;</c>.</summary>
    private static Dictionary<string, string> ReadModFiles(string gameModFolder)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(gameModFolder))
        {
            return result;
        }

        foreach (var file in Directory.EnumerateFiles(gameModFolder, "*.mod"))
        {
            var text = File.ReadAllText(file);
            var match = PathLine().Match(text);
            if (!match.Success && ArchiveLine().Match(text) is { Success: true } archive && Path.GetDirectoryName(archive.Groups[1].Value) is { Length: > 0 } archiveFolder)
            {
                // Alte Workshop-Mods (HOI IV) nennen nur archive="…/x.zip"; der Launcher führt den Ordner des Archivs als Mod-Ordner.
                result.TryAdd(PlaysetGuard.NormalizePath(archiveFolder).Replace('/', '\\'), "mod/" + Path.GetFileName(file));
                continue;
            }

            if (match.Success)
            {
                // Ein relativer Pfad in der .mod-Datei (z. B. "mod/Name", wie EU IV und PMT selbst ihn schreiben) gilt ab dem Spielordner, dem Elternordner des Mod-Ordners.
                var value = match.Groups[1].Value;
                if (!Path.IsPathRooted(value) && Path.GetDirectoryName(Path.GetFullPath(gameModFolder).TrimEnd('\\', '/')) is { } gameFolder)
                {
                    value = Path.Combine(gameFolder, value);
                }

                result.TryAdd(PlaysetGuard.NormalizePath(value).Replace('/', '\\'), "mod/" + Path.GetFileName(file));
            }
        }

        return result;
    }
}
