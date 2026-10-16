using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Playsets in playsets.json (EU V). Wie <see cref="PlaysetStore"/> und <see cref="PlaysetEdits"/> über <see cref="JsonNode"/>, damit unbekannte Felder
/// beim Zurückschreiben erhalten bleiben; geschrieben wird über eine temporäre Datei. <see cref="PlaysetData.Id"/> ist der Name des Playsets.
/// </summary>
public sealed class JsonPlaysetBackend : IPlaysetBackend
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly string _path;

    public JsonPlaysetBackend(string playsetsFilePath) => _path = playsetsFilePath;

    public IReadOnlyList<string> FilesToBackUp => new[] { _path };

    public bool UsesLauncherDatabase => false;

    public IReadOnlyList<PlaysetData> LoadAll()
    {
        if (!TryRead(out _, out var playsets, out _))
        {
            return Array.Empty<PlaysetData>();
        }

        var result = new List<PlaysetData>();
        foreach (var node in playsets.OfType<JsonObject>())
        {
            if (NameOf(node) is not { Length: > 0 } name)
            {
                continue;
            }

            var data = new PlaysetData
            {
                Id = name,
                Name = name,
                IsActive = node["isActive"] is JsonValue active && active.TryGetValue<bool>(out var a) && a,
                IsAutomaticallySorted = node["isAutomaticallySorted"] is JsonValue sorted && sorted.TryGetValue<bool>(out var s) && s,
            };
            foreach (var mod in (node["orderedListMods"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                if (PathOf(mod) is { Length: > 0 } path)
                {
                    data.Mods.Add(new PlaysetModEntry(path, PlaysetInstalledMods.ReadMetadataName(path), IsEnabled(mod), null));
                }
            }

            foreach (var dlc in (node["DLC"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                if (dlc["paradoxAppId"] is JsonValue id && id.TryGetValue<string>(out var dlcId))
                {
                    data.Dlcs.Add(new PlaysetDlcEntry(dlcId, IsEnabled(dlc)));
                }
            }

            result.Add(data);
        }

        return result;
    }

    public PlaysetResult Save(PlaysetData playset)
    {
        if (!TryRead(out var root, out var playsets, out var failure))
        {
            return failure!;
        }

        var node = playsets.OfType<JsonObject>().FirstOrDefault(p => NameOf(p) == playset.Id);
        if (node is null)
        {
            node = new JsonObject { ["name"] = playset.Name };
            playsets.Add(node);
        }

        node["name"] = playset.Name;
        var oldMods = (node["orderedListMods"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        var reordered = IsReordered(oldMods.Select(PathOf), playset.Mods.Select(m => m.Path));
        node["orderedListMods"] = new JsonArray(playset.Mods.Select(m => (JsonNode)Merge(oldMods, "path", m.Path, m.Enabled)).ToArray());

        var oldDlcs = (node["DLC"] as JsonArray)?.OfType<JsonObject>().ToList() ?? new List<JsonObject>();
        node["DLC"] = new JsonArray(playset.Dlcs.Select(d => (JsonNode)Merge(oldDlcs, "paradoxAppId", d.Id, d.Enabled)).ToArray());

        node["isAutomaticallySorted"] = playset.IsAutomaticallySorted && !reordered;
        return Write(root);
    }

    public PlaysetResult Delete(string id)
    {
        if (!TryRead(out var root, out var playsets, out var failure))
        {
            return failure!;
        }

        var node = playsets.OfType<JsonObject>().FirstOrDefault(p => NameOf(p) == id);
        if (node is null)
        {
            return new PlaysetResult(PlaysetStatus.PlaysetMissing, id);
        }

        playsets.Remove(node);
        return Write(root);
    }

    public PlaysetResult SetActive(string id)
    {
        if (!TryRead(out var root, out var playsets, out var failure))
        {
            return failure!;
        }

        var target = playsets.OfType<JsonObject>().FirstOrDefault(p => NameOf(p) == id);
        if (target is null)
        {
            return new PlaysetResult(PlaysetStatus.PlaysetMissing, id);
        }

        foreach (var other in playsets.OfType<JsonObject>().Where(p => !ReferenceEquals(p, target)))
        {
            other.Remove("isActive");
        }

        target["isActive"] = true;
        return Write(root);
    }

    public IReadOnlyList<PlaysetDlcEntry> DefaultDlcs() =>
        LoadAll().FirstOrDefault()?.Dlcs.Select(d => d with { Enabled = true }).ToList() ?? new List<PlaysetDlcEntry>();

    /// <summary>Ob die gemeinsamen Mods in anderer Reihenfolge stehen als vorher (Mods, die neu dazukommen oder wegfallen, zählen nicht).</summary>
    private static bool IsReordered(IEnumerable<string?> before, IEnumerable<string> after)
    {
        var oldList = before.Where(p => p is not null).Select(p => PlaysetGuard.NormalizePath(p!).ToLowerInvariant()).ToList();
        var newList = after.Select(p => PlaysetGuard.NormalizePath(p).ToLowerInvariant()).ToList();
        var common = oldList.Intersect(newList).ToHashSet();
        return !oldList.Where(common.Contains).SequenceEqual(newList.Where(common.Contains));
    }

    /// <summary>Übernimmt den vorhandenen Eintrag mit demselben Schlüssel (samt unbekannten Feldern) oder legt einen neuen an.</summary>
    private static JsonObject Merge(List<JsonObject> existing, string keyField, string key, bool enabled)
    {
        var match = existing.FirstOrDefault(o => o[keyField] is JsonValue v && v.TryGetValue<string>(out var s)
            && string.Equals(PlaysetGuard.NormalizePath(s), PlaysetGuard.NormalizePath(key), StringComparison.OrdinalIgnoreCase));
        var node = match is not null ? (JsonObject)match.DeepClone() : new JsonObject { [keyField] = key };
        node["isEnabled"] = enabled;
        return node;
    }

    private bool TryRead(out JsonObject root, out JsonArray playsets, out PlaysetResult? failure)
    {
        root = new JsonObject();
        playsets = new JsonArray();
        failure = null;
        if (!File.Exists(_path))
        {
            failure = new PlaysetResult(PlaysetStatus.FileMissing, _path);
            return false;
        }

        try
        {
            if (JsonNode.Parse(File.ReadAllText(_path)) is JsonObject parsed && parsed["playsets"] is JsonArray array)
            {
                root = parsed;
                playsets = array;
                return true;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Unlesbar wie falsches Format: nichts wird geändert.
        }

        failure = new PlaysetResult(PlaysetStatus.InvalidFile);
        return false;
    }

    private PlaysetResult Write(JsonObject root)
    {
        var temp = _path + ".tmp";
        try
        {
            File.WriteAllText(temp, root.ToJsonString(WriteOptions));
            File.Move(temp, _path, overwrite: true);
            return PlaysetResult.Ok;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Die temporäre Datei bleibt dann liegen; die Originaldatei ist unverändert.
            }

            return new PlaysetResult(PlaysetStatus.WriteFailed, ex.Message);
        }
    }

    private static string? NameOf(JsonObject node) => node["name"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string? PathOf(JsonObject node) => node["path"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool IsEnabled(JsonObject node) => node["isEnabled"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
}
