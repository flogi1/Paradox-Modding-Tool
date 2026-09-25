using System.IO;
using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Speichert/lädt frei zugewiesene Farbmarkierungen für einzelne Ordner/Dateien in der
/// Verzeichnis-Übersicht (Nutzer-Feedback: "Ermögliche mir auch in diesem Verzeichnis Ordner
/// und Dateien farblich zu markieren.") als JSON — nach demselben Muster wie
/// <c>ParadoxModTool.SearchReplace.SearchFavoritesStore</c>: eine fehlende oder beschädigte
/// Datei liefert bewusst ein LEERES Dictionary statt eines Fehlers zurück. Schlüssel sind über
/// <see cref="PathNormalization.NormalizeKey"/> normalisierte Pfade (siehe Lessons Learned
/// Punkt 11), damit z. B. ein abschließender Backslash nicht zu zwei getrennten Einträgen für
/// dieselbe Datei führt.
/// </summary>
public static class ExplorerColorStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Save(IReadOnlyDictionary<string, string> colorsByNormalizedPath, string path)
    {
        var json = JsonSerializer.Serialize(colorsByNormalizedPath, Options);
        SharedFile.WriteAllTextAtomic(path, json);
    }

    public static Dictionary<string, string> TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
        catch (IOException)
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Speichert die Farben zusammengeführt mit dem inzwischen auf der Platte stehenden Stand (ein anderes Fenster kann Farben ergänzt haben):
    /// nur die eigenen Änderungen gegenüber <paramref name="baseline"/> werden angewendet. Gibt den gespeicherten Gesamtstand zurück.
    /// </summary>
    public static Dictionary<string, string> SaveMerged(IReadOnlyDictionary<string, string> baseline, IReadOnlyDictionary<string, string> current, string path) =>
        SharedFile.Locked(path, () =>
        {
            var merged = CollectionMerge.MergeDictionary(TryLoad(path), baseline, current);
            Save(merged, path);
            return merged;
        });

    public static string GetDefaultColorsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "explorer-colors.json");
    }
}
