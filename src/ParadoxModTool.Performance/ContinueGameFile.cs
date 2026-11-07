using System.Text.Json;
using System.Text.RegularExpressions;

namespace ParadoxModTool.Performance;

/// <summary>
/// Liest und setzt den Titel der <c>continue_game.json</c> des Spiels (Tabs als Einrückung). Schreiben nur über <see cref="WriteTitle"/>
/// (Florian 2026-10-24: das Spiel speichert im Intervall Autosaves, danach zeigt der letzte Stand nicht mehr auf das Mess-Save).
/// </summary>
public static class ContinueGameFile
{
    /// <summary>Titel des zuletzt gespielten Spielstands; null, wenn die Datei fehlt, nicht lesbar ist oder keinen Titel nennt.</summary>
    public static string? ReadTitle(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String
                ? title.GetString()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static readonly Regex TitleValue = new("(\"title\"\\s*:\\s*\")([^\"]*)(\")", RegexOptions.CultureInvariant);

    /// <summary>Dateiendung der Sicherung, die vor jedem Schreiben neben die Datei gelegt wird.</summary>
    public const string BackupSuffix = ".pmt-backup";

    /// <summary>
    /// Setzt den Titel auf <paramref name="title"/> und lässt alles andere in der Datei unverändert (nur der Wert hinter <c>"title"</c> wird ersetzt).
    /// Vorher wird die alte Datei als <c>continue_game.json.pmt-backup</c> gesichert. false, wenn die Datei fehlt oder keinen Titel nennt; dann
    /// wird nichts geschrieben. IO-Fehler werfen.
    /// </summary>
    public static bool WriteTitle(string path, string title)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var text = File.ReadAllText(path);
        var match = TitleValue.Match(text);
        if (!match.Success)
        {
            return false;
        }

        File.Copy(path, path + BackupSuffix, overwrite: true);
        var escaped = title.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var updated = text[..match.Groups[2].Index] + escaped + text[(match.Groups[2].Index + match.Groups[2].Length)..];
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, updated);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }

        return true;
    }

    /// <summary>Der Titel gehört zu einem Autosave; das Spiel überschreibt es beim Beenden, <c>-continuelastsave</c> wäre dann nicht verlässlich.</summary>
    public static bool IsAutosave(string? title) => title is not null && title.StartsWith("autosave", StringComparison.OrdinalIgnoreCase);
}
