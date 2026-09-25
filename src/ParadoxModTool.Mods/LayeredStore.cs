using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Mods;

/// <summary>Ebene eines gespeicherten Eintrags. Ein größerer Wert ist spezieller und verdeckt gleichnamige Einträge der niedrigeren Ebenen.</summary>
public enum StoreLayer
{
    /// <summary>Gilt für alle Mods dieses Spiels.</summary>
    Game = 0,

    /// <summary>Gilt nur für eine Mod.</summary>
    Mod = 1,
}

/// <summary>Ein Eintrag der Liste eines <see cref="LayeredStore{T}"/>.</summary>
public sealed record LayeredEntry(string Name, StoreLayer Layer);

public static class StoreNames
{
    /// <summary>Ersetzt Zeichen, die in Dateinamen nicht erlaubt sind; leere Namen werden zu <paramref name="fallback"/>.</summary>
    public static string Safe(string name, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = string.Concat(name.Trim().Select(c => invalid.Contains(c) ? '_' : c)).Trim('.', ' ');
        return cleaned.Length == 0 ? fallback : cleaned;
    }
}

/// <summary>
/// JSON-Speicher über mehrere Ebenen: je Ebene ein Ordner mit einer Datei pro Eintrag. Eine Ebene ohne Ordner (z. B. Mod, solange keine Mod gewählt ist)
/// bleibt leer. Der Speicher führt Ebenen nicht zusammen; gleichnamige Einträge stehen in der Liste je Ebene, <see cref="Resolve"/> nimmt den spezielleren.
/// Dateien, deren Name mit <c>_</c> beginnt, erscheinen nicht in der Liste (interner Zustand wie <c>_last</c>).
/// </summary>
public sealed class LayeredStore<T> where T : class
{
    private readonly Func<StoreLayer, string?> _directoryFor;
    private readonly JsonSerializerOptions _options;
    private readonly Action<T>? _afterLoad;
    private readonly string _fallbackName;

    public LayeredStore(Func<StoreLayer, string?> directoryFor, JsonSerializerOptions options, Action<T>? afterLoad = null, string fallbackName = "Eintrag")
    {
        _directoryFor = directoryFor;
        _options = options;
        _afterLoad = afterLoad;
        _fallbackName = fallbackName;
    }

    /// <summary>Alle Einträge aller Ebenen, nach Name und dann nach Ebene geordnet.</summary>
    public IReadOnlyList<LayeredEntry> List() =>
        Enum.GetValues<StoreLayer>()
            .SelectMany(layer => List(layer).Select(name => new LayeredEntry(name, layer)))
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.Layer)
            .ToList();

    public List<string> List(StoreLayer layer)
    {
        var directory = _directoryFor(layer);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return new List<string>();
        }

        return Directory.EnumerateFiles(directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n) && !n!.StartsWith('_'))
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public bool Exists(string name, StoreLayer layer) => PathFor(name, layer) is { } path && File.Exists(path);

    /// <summary>Lädt den Eintrag; <see langword="null"/>, wenn es ihn nicht gibt oder die Datei kein gültiges JSON enthält.</summary>
    public T? Load(string name, StoreLayer layer)
    {
        var path = PathFor(name, layer);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), _options);
            if (value is not null)
            {
                _afterLoad?.Invoke(value);
            }

            return value;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Der Eintrag mit diesem Namen aus der speziellsten Ebene, die ihn hat.</summary>
    public (T Value, StoreLayer Layer)? Resolve(string name)
    {
        foreach (var layer in Enum.GetValues<StoreLayer>().OrderByDescending(l => l))
        {
            if (Load(name, layer) is { } value)
            {
                return (value, layer);
            }
        }

        return null;
    }

    /// <summary>Speichert atomar. Wirft <see cref="InvalidOperationException"/>, wenn die Ebene keinen Ordner hat.</summary>
    public void Save(string name, StoreLayer layer, T value)
    {
        var path = PathFor(name, layer) ?? throw new InvalidOperationException($"Für die Ebene {layer} gibt es keinen Ordner.");
        var json = JsonSerializer.Serialize(value, _options);
        SharedFile.Locked(path, () => SharedFile.WriteAllTextAtomic(path, json));
    }

    public void Delete(string name, StoreLayer layer)
    {
        var path = PathFor(name, layer);
        if (path is not null && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string? PathFor(string name, StoreLayer layer)
    {
        var directory = _directoryFor(layer);
        return string.IsNullOrEmpty(directory) ? null : Path.Combine(directory, StoreNames.Safe(name, _fallbackName) + ".json");
    }
}
