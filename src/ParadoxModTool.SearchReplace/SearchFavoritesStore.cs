using System.IO;
using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.SearchReplace;

/// <summary>
/// Speichert/lädt eine Liste favorisierter Suchordner (Abschnitt 5b: "Favoriten für häufige
/// Suchordner speichern") als JSON-Datei — nach demselben Muster wie
/// <c>ParadoxModTool.Session.SessionStore</c>: eine fehlende oder beschädigte Datei liefert
/// bewusst eine LEERE Liste statt eines Fehlers zurück, damit sie nie den Start des
/// Suchen-Fensters blockiert.
/// </summary>
public static class SearchFavoritesStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Save(IReadOnlyList<string> folders, string path)
    {
        var json = JsonSerializer.Serialize(folders, Options);
        SharedFile.WriteAllTextAtomic(path, json);
    }

    public static IReadOnlyList<string> TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<string>();
        }

        try
        {
            var json = File.ReadAllText(path);
            return (IReadOnlyList<string>?)JsonSerializer.Deserialize<List<string>>(json, Options) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Speichert die Favoriten zusammengeführt mit dem inzwischen auf der Platte stehenden Stand (ein anderes Fenster kann Favoriten ergänzt haben):
    /// nur die eigenen Änderungen gegenüber <paramref name="baseline"/> werden angewendet. Gibt den gespeicherten Gesamtstand zurück.
    /// </summary>
    public static List<string> SaveMerged(IReadOnlyList<string> baseline, IReadOnlyList<string> current, string path) =>
        SharedFile.Locked(path, () =>
        {
            var merged = CollectionMerge.MergeList(TryLoad(path), baseline, current, StringComparer.OrdinalIgnoreCase);
            Save(merged, path);
            return merged;
        });

    public static string GetDefaultFavoritesFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "search-favorites.json");
    }
}
