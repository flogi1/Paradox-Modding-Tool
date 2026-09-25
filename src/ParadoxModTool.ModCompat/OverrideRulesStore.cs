using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Storage;

namespace ParadoxModTool.ModCompat;

/// <summary>Liest und schreibt die Spielebene der Regeltabelle (<c>games\&lt;id&gt;\compat\rules.json</c>). Eine fehlende oder kaputte Datei gilt als leer.</summary>
public static class OverrideRulesStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static List<OverrideRule> Load(string file)
    {
        try
        {
            return File.Exists(file)
                ? JsonSerializer.Deserialize<List<OverrideRule>>(File.ReadAllText(file), Options) ?? new List<OverrideRule>()
                : new List<OverrideRule>();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new List<OverrideRule>();
        }
    }

    public static void Save(string file, IEnumerable<OverrideRule> rules)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        SharedFile.WriteAllTextAtomic(file, JsonSerializer.Serialize(rules.ToList(), Options));
    }
}
