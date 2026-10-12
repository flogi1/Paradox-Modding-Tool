using System.Text;
using System.Text.Json;

namespace ParadoxModTool.Generators;

/// <summary>Gewählte Pfade der externen Dateien eines Pakets (Kennung → absoluter Pfad) in <c>externals.json</c> im Datenordner des Pakets.</summary>
public static class ExternalPathStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Die gespeicherten Pfade; fehlt die Datei, ist sie gesperrt oder unlesbar, ist die Liste leer (Formular und Vorschau dürfen nicht abstürzen).</summary>
    public static IReadOnlyDictionary<string, string> Load(string dataFolder)
    {
        try
        {
            return Read(dataFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Merkt sich den Pfad einer externen Datei; ein leerer Pfad vergisst sie. Schreibt über eine Temp-Datei. Ist <c>externals.json</c> gesperrt oder
    /// unlesbar, wirft es <see cref="IOException"/> bzw. <see cref="UnauthorizedAccessException"/>, statt die anderen Pfade zu überschreiben.
    /// </summary>
    public static void Set(string dataFolder, string id, string? filePath)
    {
        var paths = new Dictionary<string, string>(Read(dataFolder), StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            paths.Remove(id);
        }
        else
        {
            paths[id] = filePath;
        }

        var path = PathOf(dataFolder);
        Directory.CreateDirectory(dataFolder);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(paths, Options), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Liest die Pfade; fehlt die Datei oder ist ihr Inhalt kein gültiges JSON, ist die Liste leer. Fehler beim Lesen gehen weiter.</summary>
    private static Dictionary<string, string> Read(string dataFolder)
    {
        var path = PathOf(dataFolder);
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), Options);
            return stored is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(stored, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string PathOf(string dataFolder) => Path.Combine(dataFolder, "externals.json");
}
