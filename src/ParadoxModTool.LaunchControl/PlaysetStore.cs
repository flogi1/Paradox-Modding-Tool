using System.Text.Json;
using System.Text.Json.Nodes;

namespace ParadoxModTool.LaunchControl;

/// <summary>
/// Liest/ändert playsets.json (Nutzer-Feedback: "Es sollte noch zusätzlich bei der Startoption die
/// Möglichkeit geben, das Playset direkt auszuwählen."). WICHTIG: Diese Datei ist NICHT Teil des
/// Mod-Projekts, sondern eine vom Paradox-Launcher/-Spiel selbst verwaltete, EXTERNE
/// Konfigurationsdatei unter "Documents/Paradox Interactive/&lt;Spiel&gt;/playsets.json" - sie
/// enthält u. a. die vollständige Mod-Liste jedes Playsets samt Ladereihenfolge sowie die
/// aktivierten DLCs des Nutzers. Ein Datenverlust hier (z. B. durch striktes POCO-Deserialisieren/
/// -Serialisieren mit nur den uns bekannten Feldern) würde reale Spieleinstellungen beschädigen,
/// nicht nur unser eigenes Projekt - daher arbeitet diese Klasse bewusst NICHT mit einem festen
/// C#-Datenmodell, sondern mit <see cref="JsonNode"/>/<see cref="JsonObject"/> als veränderbarem
/// DOM: jedes Feld, das wir nicht kennen oder nicht anfassen, bleibt beim Zurückschreiben exakt
/// erhalten. Aus demselben Grund legt <see cref="SetActivePlayset"/> vor jedem Schreibzugriff
/// zusätzlich eine Sicherheitskopie an (siehe dort).
///
/// Bekannte Unsicherheit (siehe README "Bekannte Unsicherheiten"): Es konnte NICHT über die
/// offizielle EU5-Wiki bestätigt werden, dass das Setzen von "isActive" auf ein Playset
/// tatsächlich das beim direkten Start von eu5.exe (ohne Paradox-Launcher) geladene Playset
/// bestimmt - das ist eine plausible, aber unbestätigte Annahme anhand der Struktur der vom Nutzer
/// bereitgestellten playsets.json (genau ein Eintrag trug "isActive": true, alle anderen ließen das
/// Feld ganz weg). Bitte beim ersten Test gezielt gegenprüfen, ob das gewählte Playset tatsächlich
/// verwendet wird.
/// </summary>
public static class PlaysetStore
{
    /// <summary>
    /// Liest alle Playsets aus <paramref name="playsetsFilePath"/> für die Anzeige in der
    /// Werkzeugleiste. Liefert eine leere Liste (statt einer Ausnahme), wenn die Datei fehlt, kein
    /// Pfad eingestellt ist oder der Inhalt nicht dem erwarteten JSON-Format entspricht - Anzeige/
    /// Start sollen dadurch nicht blockiert werden, nur die Playset-Auswahl bleibt dann leer.
    /// </summary>
    public static IReadOnlyList<PlaysetInfo> ListPlaysets(string? playsetsFilePath)
    {
        if (string.IsNullOrWhiteSpace(playsetsFilePath) || !File.Exists(playsetsFilePath))
        {
            return Array.Empty<PlaysetInfo>();
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(playsetsFilePath));
            if (root?["playsets"] is not JsonArray playsetsArray)
            {
                return Array.Empty<PlaysetInfo>();
            }

            var result = new List<PlaysetInfo>();
            foreach (var entry in playsetsArray)
            {
                if (entry is not JsonObject playsetObject)
                {
                    continue;
                }

                var name = playsetObject["name"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var isActive = playsetObject["isActive"]?.GetValue<bool>() ?? false;
                var mods = playsetObject["orderedListMods"] as JsonArray;
                var modCount = mods?.Count ?? 0;

                var enabledModCount = 0;
                if (mods is not null)
                {
                    foreach (var mod in mods)
                    {
                        if (mod is JsonObject modObject && (modObject["isEnabled"]?.GetValue<bool>() ?? false))
                        {
                            enabledModCount++;
                        }
                    }
                }

                result.Add(new PlaysetInfo
                {
                    Name = name,
                    IsActive = isActive,
                    ModCount = modCount,
                    EnabledModCount = enabledModCount,
                });
            }

            return result;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Array.Empty<PlaysetInfo>();
        }
    }

    /// <summary>
    /// Die Mods des Playsets <paramref name="playsetName"/> in Ladereihenfolge (nur lesend). Einträge ohne <c>path</c> entfallen.
    /// Eine fehlende oder kaputte Datei und ein unbekanntes Playset ergeben eine leere Liste.
    /// </summary>
    public static IReadOnlyList<PlaysetMod> ListPlaysetMods(string? playsetsFilePath, string playsetName)
    {
        if (string.IsNullOrWhiteSpace(playsetsFilePath) || !File.Exists(playsetsFilePath))
        {
            return Array.Empty<PlaysetMod>();
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(playsetsFilePath));
            if (root?["playsets"] is not JsonArray playsets)
            {
                return Array.Empty<PlaysetMod>();
            }

            foreach (var entry in playsets)
            {
                if (entry is not JsonObject playset || playset["name"]?.GetValue<string>() != playsetName)
                {
                    continue;
                }

                var result = new List<PlaysetMod>();
                if (playset["orderedListMods"] is JsonArray mods)
                {
                    foreach (var mod in mods)
                    {
                        if (mod is JsonObject modObject && modObject["path"]?.GetValue<string>() is { Length: > 0 } path)
                        {
                            result.Add(new PlaysetMod(path, modObject["isEnabled"]?.GetValue<bool>() ?? false));
                        }
                    }
                }

                return result;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Wie ListPlaysets: eine unlesbare Datei ergibt eine leere Auswahl.
        }

        return Array.Empty<PlaysetMod>();
    }

    /// <summary>
    /// Setzt <paramref name="playsetName"/> als aktives Playset in <paramref name="playsetsFilePath"/>
    /// (setzt dessen "isActive" auf <see langword="true"/>, entfernt das Feld bei allen anderen
    /// Playsets, die es zuvor trugen - siehe Klassenkommentar zur Unsicherheit dieser Annahme). Legt
    /// VOR dem Schreiben eine Sicherheitskopie unter demselben Pfad mit angehängtem ".pmt-backup" an
    /// (überschreibt eine vorherige Sicherung), da diese Datei NICHT Teil des von
    /// ParadoxModTool.VersionManager verwalteten Mod-Ordners ist und daher sonst keine eigene
    /// Snapshot-Absicherung hätte. Alle unbekannten Felder (DLC-Liste, Mod-Ladereihenfolge, ...)
    /// bleiben unverändert erhalten, da mit <see cref="JsonNode"/> statt eines festen Datenmodells
    /// gearbeitet wird. Bekannte kosmetische Nebenwirkung: Die Originaldatei ist Tab-eingerückt,
    /// beim Zurückschreiben wird stattdessen die Standard-Einrückung von
    /// <see cref="JsonSerializerOptions.WriteIndented"/> (2 Leerzeichen) verwendet - für JSON
    /// semantisch bedeutungslos, aber sichtbar in einem Diff-Tool.
    /// </summary>
    public static LaunchResult SetActivePlayset(string? playsetsFilePath, string playsetName)
    {
        if (string.IsNullOrWhiteSpace(playsetsFilePath) || !File.Exists(playsetsFilePath))
        {
            return LaunchResult.Failure($"Playset-Datei nicht gefunden: \"{playsetsFilePath}\".");
        }

        try
        {
            var originalText = File.ReadAllText(playsetsFilePath);
            var root = JsonNode.Parse(originalText);
            if (root?["playsets"] is not JsonArray playsetsArray)
            {
                return LaunchResult.Failure("Playset-Datei hat nicht das erwartete Format (kein \"playsets\"-Array gefunden).");
            }

            JsonObject? target = null;
            foreach (var entry in playsetsArray)
            {
                if (entry is JsonObject playsetObject
                    && string.Equals(playsetObject["name"]?.GetValue<string>(), playsetName, StringComparison.Ordinal))
                {
                    target = playsetObject;
                    break;
                }
            }

            if (target is null)
            {
                return LaunchResult.Failure($"Playset \"{playsetName}\" wurde in der Playset-Datei nicht gefunden.");
            }

            foreach (var entry in playsetsArray)
            {
                if (entry is JsonObject playsetObject && !ReferenceEquals(playsetObject, target))
                {
                    playsetObject.Remove("isActive");
                }
            }

            target["isActive"] = true;

            File.WriteAllText(playsetsFilePath + ".pmt-backup", originalText);
            File.WriteAllText(playsetsFilePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            return LaunchResult.Ok();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return LaunchResult.Failure($"Playset-Datei konnte nicht aktualisiert werden: {ex.Message}");
        }
    }
}
