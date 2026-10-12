using System.Text;
using System.Text.Json;

namespace ParadoxModTool.Generators;

/// <summary>Arbeitsstände eines Pakets als <c>states/&lt;Schlüssel&gt;.json</c> im Paketordner der PMT-Daten.</summary>
public static class GeneratorStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string Folder(string packagesRoot, string packageId) => Path.Combine(packagesRoot, packageId);

    public static IReadOnlyList<string> Keys(string folder)
    {
        var states = Path.Combine(folder, "states");
        return Directory.Exists(states)
            ? Directory.EnumerateFiles(states, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList()
            : Array.Empty<string>();
    }

    /// <summary>Der Arbeitsstand; <see langword="null"/>, wenn er fehlt oder unlesbar ist (dann bleibt die Datei als <c>.broken</c> erhalten).</summary>
    public static GeneratorState? Load(string folder, string key)
    {
        var path = PathOf(folder, key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GeneratorState>(File.ReadAllText(path), Options);
        }
        catch (JsonException)
        {
            File.Move(path, path + ".broken", overwrite: true);
            return null;
        }
    }

    /// <summary>Schreibt über eine Temp-Datei, damit ein Abbruch keinen halben Stand hinterlässt.</summary>
    public static void Save(string folder, GeneratorState state)
    {
        var path = PathOf(folder, state.Key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, Serialize(state), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Löscht die Datei des Arbeitsstands (nach dem Umbenennen des Schlüssels); ohne Datei passiert nichts.</summary>
    public static void Delete(string folder, string key)
    {
        var path = PathOf(folder, key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static string Serialize(GeneratorState state) => JsonSerializer.Serialize(state, Options);

    /// <summary>Tiefe Kopie über JSON (gleiche Form wie die gespeicherten Stände).</summary>
    public static GeneratorState Clone(GeneratorState state) => JsonSerializer.Deserialize<GeneratorState>(Serialize(state), Options)!;

    private static string PathOf(string folder, string key) => Path.Combine(folder, "states", key + ".json");
}
