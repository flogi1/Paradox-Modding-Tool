using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Mods;
using ParadoxModTool.Storage;

namespace ParadoxModTool.ModCompat;

/// <summary>Speichert Kompatibilitätssets als JSON, eine Datei je Set (<c>games\&lt;id&gt;\compat\sets</c>).</summary>
public sealed class CompatSetStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public CompatSetStore(string directory)
    {
        _directory = directory;
    }

    private string PathFor(string name) => Path.Combine(_directory, StoreNames.Safe(name, "Set") + ".json");

    /// <summary>Namen aller lesbaren Sets, alphabetisch.</summary>
    public List<string> List()
    {
        if (!Directory.Exists(_directory))
        {
            return new List<string>();
        }

        return Directory.EnumerateFiles(_directory, "*.json")
            .Select(f => TryRead(f)?.Name)
            .OfType<string>()
            .Where(n => n.Length > 0)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public CompatSet? Load(string name) => File.Exists(PathFor(name)) ? TryRead(PathFor(name)) : null;

    public void Save(CompatSet set)
    {
        Directory.CreateDirectory(_directory);
        SharedFile.WriteAllTextAtomic(PathFor(set.Name), JsonSerializer.Serialize(set, Options));
    }

    public void Delete(string name)
    {
        if (File.Exists(PathFor(name)))
        {
            File.Delete(PathFor(name));
        }
    }

    private static CompatSet? TryRead(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<CompatSet>(File.ReadAllText(file), Options);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
