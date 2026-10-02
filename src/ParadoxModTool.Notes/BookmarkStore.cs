using System.IO;
using System.Text.Json;
using ParadoxModTool.Storage;

namespace ParadoxModTool.Notes;

/// <summary>
/// Eine einzelne gebookmarkte Codestelle in einer Datei (Nutzer-Feedback: "Füge die Option hinzu,
/// dass man im Dokument eine Codestelle bookmarken kann, wo man dann über den Notizen Reiter
/// schnell hinspringen kann."). Bewusst über die ZEILENNUMMER verortet (nicht den Zeichen-Offset):
/// dieselbe Zeile bleibt auch nach kleineren Bearbeitungen WEITER OBEN/UNTEN im Dokument noch
/// eindeutig identifizierbar (z. B. über Strg+G/"Gehe zu Zeile", das bereits zeilenbasiert
/// arbeitet), während ein reiner Zeichen-Offset bei jeder Textänderung VOR der Stelle sofort
/// falsch zeigen würde. Bekannte, bewusste Einschränkung: verschiebt sich die Zeile selbst durch
/// eingefügte/gelöschte Zeilen DAVOR, "wandert" das Bookmark mit der falschen Zeilennummer mit -
/// derselbe Kompromiss, den auch Notepad++s eigene Bookmarks eingehen.
/// </summary>
public sealed class BookmarkEntry
{
    public int Line { get; set; }

    /// <summary>Kurzer Textausschnitt der Zeile zum Zeitpunkt des Anlegens (siehe
    /// ParadoxModTool.Editor.EditorPane.GetLineText) - rein zur Wiedererkennung in der Liste,
    /// wird beim Springen NICHT neu abgeglichen.</summary>
    public string Preview { get; set; } = string.Empty;

    /// <summary>
    /// Letzte Zeile des markierten Bereichs, falls das Lesezeichen mit einer Markierung über mehrere
    /// Zeilen gesetzt wurde (Nutzer-Feedback: Kommentar zur markierten Codestelle); <see langword="null"/>
    /// für ein Lesezeichen auf genau einer Zeile. Alte Dateien ohne dieses Feld laden unverändert.
    /// </summary>
    public int? EndLine { get; set; }

    /// <summary>Freier Kommentar des Nutzers zu dieser Stelle (leer = kein Kommentar).</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Letzte Zeile des Bereichs (bei einem Lesezeichen auf einer Zeile gleich <see cref="Line"/>).</summary>
    public int LastLine => Math.Max(Line, EndLine ?? Line);

    /// <summary>Lesbare Zeilenangabe: "Zeile 5" bzw. "Zeilen 5–9".</summary>
    public string LineText => BugTexts.LineText(Line, LastLine);
}

/// <summary>
/// Speichert/lädt Bookmarks je Datei (Nutzer-Feedback, siehe <see cref="BookmarkEntry"/>) als
/// JSON-Datei außerhalb des Mod-Ordners unter %APPDATA% - nach demselben Muster wie
/// <see cref="FileNoteStore"/>/ParadoxModTool.SearchReplace.SearchFavoritesStore: eine fehlende
/// oder beschädigte Datei liefert bewusst ein LEERES Wörterbuch statt eines Fehlers zurück.
/// Schlüssel sind über <see cref="Path.GetFullPath"/> normalisierte Dateipfade - dieselbe
/// "abschließender Backslash/doppelte Trennzeichen"-Lehre wie bei <see cref="FileNoteStore"/>.
/// </summary>
public static class BookmarkStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static void Save(IReadOnlyDictionary<string, List<BookmarkEntry>> bookmarks, string path)
    {
        var json = JsonSerializer.Serialize(bookmarks, Options);
        SharedFile.WriteAllTextAtomic(path, json);
    }

    public static Dictionary<string, List<BookmarkEntry>> TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, List<BookmarkEntry>>();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, List<BookmarkEntry>>>(json, Options)
                ?? new Dictionary<string, List<BookmarkEntry>>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, List<BookmarkEntry>>();
        }
        catch (IOException)
        {
            return new Dictionary<string, List<BookmarkEntry>>();
        }
    }

    /// <summary>
    /// Wendet <paramref name="change"/> auf den frisch gelesenen Stand der Datei an und speichert ihn - alles unter der Sperre der Datei,
    /// damit ein gleichzeitig laufendes anderes Fenster keine Änderung überschreibt. Gibt den gespeicherten Stand zurück.
    /// </summary>
    public static Dictionary<string, List<BookmarkEntry>> Update(string path, Action<Dictionary<string, List<BookmarkEntry>>> change) =>
        SharedFile.Locked(path, () =>
        {
            var data = TryLoad(path);
            change(data);
            Save(data, path);
            return data;
        });

    public static string GetDefaultBookmarksFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "ParadoxModTool", "bookmarks.json");
    }

    /// <summary>Liest die (nach Zeile sortierten) Bookmarks einer Datei aus einem bereits
    /// geladenen Wörterbuch - eine leere Liste statt einer Ausnahme, falls keine vorhanden sind.</summary>
    public static List<BookmarkEntry> GetBookmarks(IReadOnlyDictionary<string, List<BookmarkEntry>> bookmarks, string filePath)
    {
        var key = NormalizeKey(filePath);
        return bookmarks.TryGetValue(key, out var list)
            ? list.OrderBy(b => b.Line).ToList()
            : new List<BookmarkEntry>();
    }

    /// <summary>
    /// Setzt oder entfernt ein Bookmark auf einer bestimmten Zeile ("Umschalten" - dieselbe
    /// Notepad++-Konvention wie Strg+F2, siehe MainWindow.ToggleBookmarkAtCursor). Existiert auf
    /// dieser Zeile bereits ein Bookmark, wird es entfernt; sonst neu angelegt.
    /// </summary>
    /// <returns><see langword="true"/>, wenn das Bookmark danach GESETZT ist (neu angelegt), <see langword="false"/>, wenn es entfernt wurde.</returns>
    public static bool Toggle(IDictionary<string, List<BookmarkEntry>> bookmarks, string filePath, int line, string preview)
    {
        var key = NormalizeKey(filePath);
        if (!bookmarks.TryGetValue(key, out var list))
        {
            list = new List<BookmarkEntry>();
            bookmarks[key] = list;
        }

        var existing = list.FirstOrDefault(b => b.Line == line);
        if (existing is not null)
        {
            list.Remove(existing);
            if (list.Count == 0)
            {
                bookmarks.Remove(key);
            }

            return false;
        }

        list.Add(new BookmarkEntry { Line = line, Preview = preview });
        return true;
    }

    /// <summary>
    /// Legt ein Lesezeichen mit Kommentar und optionalem Zeilenbereich an bzw. ersetzt das bereits auf
    /// <paramref name="line"/> beginnende (Nutzer-Feedback: Lesezeichen-Button mit Kommentarfeld).
    /// </summary>
    public static void Set(IDictionary<string, List<BookmarkEntry>> bookmarks, string filePath, int line, int? endLine, string comment, string preview)
    {
        var key = NormalizeKey(filePath);
        if (!bookmarks.TryGetValue(key, out var list))
        {
            list = new List<BookmarkEntry>();
            bookmarks[key] = list;
        }

        list.RemoveAll(b => b.Line == line);
        list.Add(new BookmarkEntry
        {
            Line = line,
            EndLine = endLine is int end && end > line ? end : null,
            Comment = comment,
            Preview = preview,
        });
    }

    /// <summary>Findet das Lesezeichen, dessen Zeile bzw. Zeilenbereich <paramref name="line"/> enthält (für "Kommentar ändern" an der Cursorzeile).</summary>
    public static BookmarkEntry? FindContaining(IReadOnlyDictionary<string, List<BookmarkEntry>> bookmarks, string filePath, int line)
    {
        var key = NormalizeKey(filePath);
        return bookmarks.TryGetValue(key, out var list)
            ? list.FirstOrDefault(b => line >= b.Line && line <= b.LastLine)
            : null;
    }

    /// <summary>Entfernt ein einzelnes Bookmark (z. B. über den "Entfernen"-Button im
    /// Notizfenster) - unwirksam, falls es (z. B. zwischenzeitlich anderswo entfernt) nicht mehr
    /// existiert, statt eine Ausnahme zu werfen.</summary>
    public static void Remove(IDictionary<string, List<BookmarkEntry>> bookmarks, string filePath, int line)
    {
        var key = NormalizeKey(filePath);
        if (!bookmarks.TryGetValue(key, out var list))
        {
            return;
        }

        list.RemoveAll(b => b.Line == line);
        if (list.Count == 0)
        {
            bookmarks.Remove(key);
        }
    }

    private static string NormalizeKey(string filePath) => NoteKeys.ForFile(filePath);
}
