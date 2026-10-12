using System.Text;
using System.Text.Json;

namespace ParadoxModTool.Generators.Candidates;

/// <summary>Einstellungen der Kandidatenliste je Paket: Modifier "nicht als Idee", ein-/ausgeblendete Gruppen und die eigene Ausblend-Regex.</summary>
public sealed class CandidateSettings
{
    /// <summary>Modifier, die der Nutzer nicht als Idee anlegen will.</summary>
    public List<string> NotAsIdea { get; set; } = new();

    /// <summary>Gruppen-Kennung → ausgeblendet; fehlt die Kennung, gilt <see cref="CandidateGroupDef.Hidden"/>.</summary>
    public Dictionary<string, bool> HiddenGroups { get; set; } = new();

    /// <summary>Regex auf den Modifier-Namen: Treffer sind ausgeblendet; leer = keine.</summary>
    public string CustomHide { get; set; } = string.Empty;
}

/// <summary>Die Einstellungen als <c>candidates.json</c> im Datenordner des Pakets.</summary>
public static class CandidateSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Die gespeicherten Einstellungen; fehlt die Datei, ist sie gesperrt oder kein gültiges JSON, sind sie leer (das Fenster darf nicht abstürzen).</summary>
    public static CandidateSettings Load(string dataFolder)
    {
        try
        {
            var path = PathOf(dataFolder);
            return File.Exists(path) ? Normalize(JsonSerializer.Deserialize<CandidateSettings>(File.ReadAllText(path), Options)) : new CandidateSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new CandidateSettings();
        }
    }

    /// <summary>Schreibt über eine Temp-Datei; Fehler beim Schreiben gehen weiter.</summary>
    public static void Save(string dataFolder, CandidateSettings settings)
    {
        var path = PathOf(dataFolder);
        Directory.CreateDirectory(dataFolder);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    private static string PathOf(string dataFolder) => Path.Combine(dataFolder, "candidates.json");

    /// <summary>Ein <c>null</c> im JSON (Datei von Hand bearbeitet) wird zur leeren Einstellung.</summary>
    private static CandidateSettings Normalize(CandidateSettings? settings) => new()
    {
        NotAsIdea = settings?.NotAsIdea ?? new List<string>(),
        HiddenGroups = settings?.HiddenGroups ?? new Dictionary<string, bool>(),
        CustomHide = settings?.CustomHide ?? string.Empty,
    };
}
