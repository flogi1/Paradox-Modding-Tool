using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Compatch;

/// <summary>
/// Je Datei einer Mod die Vanilla-Version, auf die PMT sie zuletzt angepasst hat (<c>vanilla-versions.json</c> im Ordner
/// <see cref="CompatchStateStore.DirectoryFor"/> der Mod). Je Datei, nicht je Mod: Nach einem Lauf über eine Datei stehen die anderen noch
/// auf ihrer älteren Grundlage (Spec 2026-09-30-vanilla-update-merge, Abschnitt 7.2).
/// </summary>
public sealed class VanillaVersionLedger
{
    private const string FileName = "vanilla-versions.json";

    private readonly string _directory;
    private readonly Dictionary<string, string> _versions;

    private VanillaVersionLedger(string directory, Dictionary<string, string> versions)
    {
        _directory = directory;
        _versions = versions;
    }

    /// <summary>Liest die Datei; fehlt sie oder ist sie unlesbar, ist das Verzeichnis leer.</summary>
    public static VanillaVersionLedger Load(string modDirectory)
    {
        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = Path.Combine(modDirectory, FileName);
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) is { } stored)
            {
                foreach (var (key, value) in stored)
                {
                    versions[Key(key)] = value;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Unlesbar: wie ohne Verzeichnis, die alte Version wählt dann Regel 2 oder 3.
        }

        return new VanillaVersionLedger(modDirectory, versions);
    }

    public string? Get(string relativePath) => _versions.TryGetValue(Key(relativePath), out var version) ? version : null;

    public void Set(IEnumerable<string> relativePaths, string version)
    {
        foreach (var path in relativePaths)
        {
            _versions[Key(path)] = version;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(_directory);
        var sorted = _versions.OrderBy(v => v.Key, StringComparer.Ordinal).ToDictionary(v => v.Key, v => v.Value);
        SharedFile.WriteAllTextAtomic(Path.Combine(_directory, FileName), JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Key(string relativePath) => relativePath.Replace('\\', '/').Trim('/').ToLowerInvariant();
}
