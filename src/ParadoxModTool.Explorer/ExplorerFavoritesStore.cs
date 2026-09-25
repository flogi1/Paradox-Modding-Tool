using System.IO;
using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Speichert/lädt die Liste angepinnter Dateien/Ordner in der Verzeichnis-Übersicht
/// (Nutzer-Feedback: "Füge auch in diesem Verzeichnis eine extra Favoritenliste hinzu, wo ich
/// einige Dateien oder Ordner anpinnen kann.") — bewusst eine EIGENE Datei/Liste, getrennt von
/// <c>ParadoxModTool.SearchReplace.SearchFavoritesStore</c> (das sind favorisierte SUCHORDNER
/// für "Suchen in Dateien", ein anderer Zweck). Enthält die Pfade GENAU SO, wie sie beim
/// Anpinnen vorlagen (NICHT normalisiert) - nur so lassen sich Name/Pfad eines Favoriten
/// anzeigen, ohne dass sein übergeordneter Ordner gerade im Baum eingelesen ist. Der Abgleich
/// "ist dieser Baumeintrag ein Favorit?" erfolgt in <c>ExplorerPanel</c> über
/// <see cref="PathNormalization.NormalizeKey"/> auf beiden Seiten (siehe Lessons Learned
/// Punkt 11 der Architektur-Dokumentation), NICHT über einen rohen String-Vergleich. Ob ein
/// Eintrag eine Datei oder ein Ordner ist,
/// wird bewusst NICHT mitgespeichert, sondern beim Anzeigen live per <see cref="Directory.Exists"/>/
/// <see cref="File.Exists"/> geprüft, damit sich ein zwischenzeitlich gelöschter/umbenannter
/// Favorit nicht als falscher Typ in einer veralteten Markierung festsetzt.
/// </summary>
public static class ExplorerFavoritesStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Save(IReadOnlyList<string> normalizedPaths, string path)
    {
        var json = JsonSerializer.Serialize(normalizedPaths, Options);
        SharedFile.WriteAllTextAtomic(path, json);
    }

    public static List<string> TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return new List<string>();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<string>>(json, Options) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
        catch (IOException)
        {
            return new List<string>();
        }
    }

    /// <summary>Vergleicht Favoriten-Pfade über ihren normalisierten Schlüssel (Groß-/Kleinschreibung, Trennzeichen).</summary>
    public sealed class PathComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => x is not null && y is not null && PathNormalization.NormalizeKey(x) == PathNormalization.NormalizeKey(y);

        public int GetHashCode(string obj) => PathNormalization.NormalizeKey(obj).GetHashCode();
    }

    /// <summary>
    /// Speichert die Favoriten zusammengeführt mit dem inzwischen auf der Platte stehenden Stand (ein anderes Fenster kann Favoriten ergänzt haben):
    /// nur die eigenen Änderungen gegenüber <paramref name="baseline"/> werden angewendet. Gibt den gespeicherten Gesamtstand zurück.
    /// </summary>
    public static List<string> SaveMerged(IReadOnlyList<string> baseline, IReadOnlyList<string> current, string path) =>
        SharedFile.Locked(path, () =>
        {
            var merged = CollectionMerge.MergeList(TryLoad(path), baseline, current, new PathComparer());
            Save(merged, path);
            return merged;
        });

    public static string GetDefaultFavoritesFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "explorer-favorites.json");
    }
}
