using System.Text.Json.Nodes;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Bestmögliche Erkennung der aktuell installierten Spielversion aus "launcher-settings.json"
/// im Spielordner (Abschnitt 7, Versionshistorie). Der exakte Feldname ist NICHT gegen eine
/// echte Installation oder die Paradox-Wiki verifiziert (gleiche Kategorie wie die bereits
/// bekannte unverifizierte Annahme zu "isActive" in playsets.json) - deshalb werden mehrere
/// plausible Feldnamen versucht, und ein <see langword="null"/>-Ergebnis wird von der aufrufenden
/// UI als "manuelle Eingabe nötig" behandelt, nie als Fehler.
/// </summary>
public static class GameVersionReader
{
    private static readonly string[] CandidateFieldNames = { "rawVersion", "version", "gameVersion" };

    public static string? TryReadVersion(string gameRoot)
    {
        return TryReadFromFile(Path.Combine(gameRoot, "launcher-settings.json"))
            ?? TryReadFromFile(Path.Combine(gameRoot, "binaries", "launcher-settings.json"));
    }

    private static string? TryReadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var node = JsonNode.Parse(json);

            foreach (var fieldName in CandidateFieldNames)
            {
                var value = node?[fieldName]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or InvalidOperationException)
        {
            return null;
        }
    }
}
