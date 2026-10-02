using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParadoxModTool.LaunchControl;

public enum PlaysetEditStatus
{
    Added,
    Enabled,
    AlreadyPresent,
    GameRunning,
    FileMissing,
    PlaysetMissing,
    InvalidFile,
    WriteFailed,

    /// <summary>Die Playsets liegen in launcher-v2.sqlite, die PMT nur liest.</summary>
    LauncherDatabase,
}

/// <summary>Ergebnis einer Playset-Änderung. Keine Texte: Die App übersetzt die Art (<c>Detail</c> = Pfad, Playset oder Fehlermeldung).</summary>
public sealed record PlaysetEditResult(PlaysetEditStatus Status, string? Detail = null)
{
    public bool Changed => Status is PlaysetEditStatus.Added or PlaysetEditStatus.Enabled;
}

/// <summary>
/// Ändert die Mod-Liste eines Playsets in playsets.json (Fehlerprotokoll Plan 3, "Mod ins Playset aufnehmen"). Wie
/// <see cref="PlaysetStore.SetActivePlayset"/> über <see cref="JsonNode"/>, damit unbekannte Felder erhalten bleiben, mit Sicherung
/// <c>.pmt-backup</c>; geschrieben über eine temporäre Datei, damit nie eine halbe Datei entsteht.
/// </summary>
public static class PlaysetEdits
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Nimmt <paramref name="modFolder"/> ins Playset auf: vor die erste eigene Mod, sonst ans Ende; steht sie ausgeschaltet darin, wird sie
    /// an ihrer Stelle eingeschaltet. Läuft das Spiel, wird nichts geschrieben.
    /// </summary>
    public static PlaysetEditResult AddMod(string? playsetsFilePath, string playsetName, string modFolder, IEnumerable<string> ownModFolders, Func<bool> isGameRunning)
    {
        if (isGameRunning())
        {
            return new PlaysetEditResult(PlaysetEditStatus.GameRunning);
        }

        if (string.IsNullOrWhiteSpace(playsetsFilePath) || !File.Exists(playsetsFilePath))
        {
            return new PlaysetEditResult(PlaysetEditStatus.FileMissing, playsetsFilePath);
        }

        if (PlaysetStore.IsLauncherDatabase(playsetsFilePath))
        {
            return new PlaysetEditResult(PlaysetEditStatus.LauncherDatabase);
        }

        try
        {
            var original = File.ReadAllText(playsetsFilePath);
            if (JsonNode.Parse(original) is not JsonObject root || root["playsets"] is not JsonArray playsets)
            {
                return new PlaysetEditResult(PlaysetEditStatus.InvalidFile);
            }

            var playset = playsets.OfType<JsonObject>().FirstOrDefault(p => p["name"] is JsonValue name && name.TryGetValue<string>(out var text) && text == playsetName);
            if (playset is null)
            {
                return new PlaysetEditResult(PlaysetEditStatus.PlaysetMissing, playsetName);
            }

            if (playset["orderedListMods"] is not JsonArray mods)
            {
                mods = new JsonArray();
                playset["orderedListMods"] = mods;
            }

            var key = Key(modFolder);
            var existing = mods.OfType<JsonObject>().FirstOrDefault(m => PathOf(m) is { } path && string.Equals(Key(path), key, StringComparison.OrdinalIgnoreCase));
            PlaysetEditStatus status;
            if (existing is not null)
            {
                if (existing["isEnabled"] is JsonValue enabled && enabled.TryGetValue<bool>(out var isEnabled) && isEnabled)
                {
                    return new PlaysetEditResult(PlaysetEditStatus.AlreadyPresent);
                }

                existing["isEnabled"] = true;
                status = PlaysetEditStatus.Enabled;
            }
            else
            {
                var own = new HashSet<string>(ownModFolders.Where(f => !string.IsNullOrWhiteSpace(f)).Select(Key), StringComparer.OrdinalIgnoreCase);
                var index = mods.Count;
                for (var i = 0; i < mods.Count; i++)
                {
                    if (mods[i] is JsonObject mod && PathOf(mod) is { } path && own.Contains(Key(path)))
                    {
                        index = i;
                        break;
                    }
                }

                mods.Insert(index, new JsonObject { ["path"] = ToPlaysetPath(modFolder), ["isEnabled"] = true });
                status = PlaysetEditStatus.Added;
            }

            File.WriteAllText(playsetsFilePath + ".pmt-backup", original);
            var temp = playsetsFilePath + ".tmp";
            File.WriteAllText(temp, root.ToJsonString(WriteOptions));
            File.Move(temp, playsetsFilePath, overwrite: true);
            return new PlaysetEditResult(status);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new PlaysetEditResult(PlaysetEditStatus.WriteFailed, ex.Message);
        }
    }

    /// <summary>Vergleichsschlüssel für Mod-Ordner: voller Pfad ohne Trenner am Ende.</summary>
    public static string Key(string folder) => Path.GetFullPath(folder.Trim()).TrimEnd('\\', '/');

    /// <summary>Pfad im Format von playsets.json: Schrägstriche, Trenner am Ende.</summary>
    public static string ToPlaysetPath(string folder) => Key(folder).Replace('\\', '/') + "/";

    private static string? PathOf(JsonObject mod) =>
        mod["path"] is JsonValue value && value.TryGetValue<string>(out var path) && path.Length > 0 ? path : null;
}

