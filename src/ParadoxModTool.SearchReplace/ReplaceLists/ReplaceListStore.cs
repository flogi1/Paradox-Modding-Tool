using System.Text.Json;
using System.Text.Json.Nodes;
using ParadoxModTool.Storage;

namespace ParadoxModTool.SearchReplace.ReplaceLists;

/// <summary>
/// Ersetzungslisten einer Mod in <c>&lt;Spielordner&gt;/replace-lists/&lt;modKey&gt;.json</c> (Spec 2026-09-30-replace-lists, Abschnitt 1). Änderungen
/// laufen über <see cref="Update"/>: frisch lesen, ändern, atomar schreiben, unter Sperre, damit zwei PMT-Fenster sich nicht überschreiben.
/// Eine unlesbare Datei oder eine andere Version bleibt als <c>.broken.json</c> liegen; geladen wird dann eine leere Sammlung.
/// </summary>
public sealed class ReplaceListStore(string filePath)
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public string FilePath { get; } = filePath;

    public static string FileFor(string gameDirectory, string modKey) => Path.Combine(gameDirectory, "replace-lists", modKey + ".json");

    public ReplaceListSet Load() => SharedFile.Locked(FilePath, LoadUnlocked);

    public ReplaceListSet Update(Func<ReplaceListSet, ReplaceListSet> change) =>
        SharedFile.Locked(FilePath, () =>
        {
            var set = change(LoadUnlocked());
            set.Version = CurrentVersion;
            SharedFile.WriteAllTextAtomic(FilePath, JsonSerializer.Serialize(set, Json));
            return set;
        });

    public static void Export(ReplaceList list, string path) =>
        SharedFile.WriteAllTextAtomic(path, JsonSerializer.Serialize(new ExportFile { Version = CurrentVersion, List = list }, Json));

    public static ReplaceList Import(string path)
    {
        ExportFile? file;
        try
        {
            file = JsonSerializer.Deserialize<ExportFile>(File.ReadAllText(path), Json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }

        return Clean(file?.List ?? throw new InvalidDataException(path));
    }

    /// <summary>
    /// Zielordner relativ zur Mod mit "/"; ein absoluter Pfad oder einer mit ".." zeigt aus der Mod heraus und wird zum Mod-Ordner selbst ("").
    /// </summary>
    public static string SafeTargetFolder(string? folder)
    {
        var text = (folder ?? string.Empty).Replace('\\', '/').Trim();
        if (text.StartsWith('/') || text.Contains(':') || Path.IsPathRooted(text))
        {
            return string.Empty;
        }

        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => p != ".").ToList();
        return parts.Contains("..") ? string.Empty : string.Join('/', parts);
    }

    /// <summary><paramref name="name"/>, sonst "name (2)", "name (3)" … (Groß-/Kleinschreibung egal).</summary>
    public static string UniqueName(ReplaceListSet set, string name)
    {
        bool Used(string candidate) => set.Lists.Any(l => string.Equals(l.Name, candidate, StringComparison.OrdinalIgnoreCase));
        if (!Used(name))
        {
            return name;
        }

        var number = 2;
        while (Used($"{name} ({number})"))
        {
            number++;
        }

        return $"{name} ({number})";
    }

    /// <summary>Nicht leer und von keiner anderen Liste benutzt; <paramref name="currentName"/> ist der bisherige Name beim Umbenennen.</summary>
    public static bool IsValidName(ReplaceListSet set, string name, string? currentName) =>
        !string.IsNullOrWhiteSpace(name)
        && !set.Lists.Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
                               && !string.Equals(l.Name, currentName, StringComparison.OrdinalIgnoreCase));

    private ReplaceListSet LoadUnlocked()
    {
        if (!File.Exists(FilePath))
        {
            return new ReplaceListSet();
        }

        try
        {
            var text = File.ReadAllText(FilePath);
            if (JsonNode.Parse(text) is JsonObject root
                && root.FirstOrDefault(p => string.Equals(p.Key, "version", StringComparison.OrdinalIgnoreCase)).Value is JsonValue version
                && version.TryGetValue<int>(out var number) && number == CurrentVersion
                && JsonSerializer.Deserialize<ReplaceListSet>(text, Json) is { } set)
            {
                var lists = (set.Lists ?? new()).Where(l => l is not null).Select(Clean).ToList();
                set.Lists = new();
                foreach (var list in lists)
                {
                    if (string.IsNullOrWhiteSpace(list.Name))
                    {
                        list.Name = UniqueName(set, "-");
                    }

                    set.Lists.Add(list);
                }

                return set;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
        }

        File.Move(FilePath, Path.ChangeExtension(FilePath, ".broken.json"), overwrite: true);
        return new ReplaceListSet();
    }

    /// <summary>Null-Einträge aus Hand-Änderungen oder fremden Dateien: Regeln ohne Inhalt fallen weg, fehlende Texte werden leer.</summary>
    private static ReplaceList Clean(ReplaceList list)
    {
        list.Name ??= string.Empty;
        list.TargetFolder = SafeTargetFolder(list.TargetFolder);
        list.Rules = (list.Rules ?? new()).Where(r => r is not null).ToList();
        foreach (var rule in list.Rules)
        {
            rule.Search ??= string.Empty;
            rule.Replace ??= string.Empty;
        }

        return list;
    }

    private sealed class ExportFile
    {
        public int Version { get; set; }

        public ReplaceList? List { get; set; }
    }
}
