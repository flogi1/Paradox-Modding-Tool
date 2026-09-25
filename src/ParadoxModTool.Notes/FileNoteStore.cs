using System.IO;
using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Notes;

/// <summary>
/// Speichert/lädt Notizen zu einzelnen Dateien (Nutzer-Feedback Runde 4: "Füge für die
/// Textbearbeitung noch eine Notizfunktion hinzu, sodass man sich zu einer Datei explizit Notizen
/// [machen kann]. Diese Notizen sollten aber nicht in der Datei selbst gespeichert werden, sondern
/// nur für den Modder sichtbar sein, wenn er das Programm nutzt."). Als JSON-Datei außerhalb des
/// Mod-Ordners unter %APPDATA% - nach demselben Muster wie
/// ParadoxModTool.SearchReplace.SearchFavoritesStore und ParadoxModTool.Session.SessionStore: eine
/// fehlende oder beschädigte Datei liefert bewusst ein LEERES Wörterbuch statt eines Fehlers,
/// damit sie nie das Öffnen des Notizfensters blockiert.
///
/// Schlüssel sind über <see cref="Path.GetFullPath"/> normalisierte Dateipfade - dieselbe Lehre
/// aus Runde 3 (siehe ParadoxModTool.Editor.EditorPane.PathsEqual): oberflächlich unterschiedliche
/// Schreibweisen desselben Pfades (abschließender Backslash, doppelte Trennzeichen, ...) dürfen
/// nicht versehentlich zu zwei getrennten Notizen für dieselbe Datei führen.
/// </summary>
public static class FileNoteStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Save(IReadOnlyDictionary<string, string> notes, string path)
    {
        var json = JsonSerializer.Serialize(notes, Options);
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
    /// Wendet <paramref name="change"/> auf den frisch gelesenen Stand der Datei an und speichert ihn - alles unter der Sperre der Datei,
    /// damit ein gleichzeitig laufendes anderes Fenster keine Änderung überschreibt. Gibt den gespeicherten Stand zurück.
    /// </summary>
    public static Dictionary<string, string> Update(string path, Action<Dictionary<string, string>> change) =>
        SharedFile.Locked(path, () =>
        {
            var data = TryLoad(path);
            change(data);
            Save(data, path);
            return data;
        });

    public static string GetDefaultNotesFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "notes.json");
    }

    /// <summary>
    /// Liest die Notiz zu genau einer Datei aus einem bereits geladenen Wörterbuch, mit über
    /// <see cref="Path.GetFullPath"/> normalisiertem Pfad (siehe Klassenkommentar). Liefert einen
    /// leeren String, falls keine Notiz existiert, statt eine Ausnahme durchschlagen zu lassen.
    /// </summary>
    public static string GetNote(IReadOnlyDictionary<string, string> notes, string filePath)
    {
        var key = NormalizeKey(filePath);
        return notes.TryGetValue(key, out var note) ? note : string.Empty;
    }

    /// <summary>
    /// Setzt oder entfernt die Notiz zu genau einer Datei in einem bereits geladenen Wörterbuch.
    /// Eine leere oder nur aus Leerzeichen bestehende Notiz entfernt den Eintrag ganz, statt eine
    /// leere Notiz zu speichern, damit die Notizdatei nicht unbegrenzt mit leeren Einträgen wächst,
    /// nachdem ein Nutzer eine Notiz wieder gelöscht hat.
    /// </summary>
    public static void SetNote(IDictionary<string, string> notes, string filePath, string note)
    {
        var key = NormalizeKey(filePath);

        if (string.IsNullOrWhiteSpace(note))
        {
            notes.Remove(key);
        }
        else
        {
            notes[key] = note;
        }
    }

    private static string NormalizeKey(string filePath) => NoteKeys.ForFile(filePath);
}
