namespace ParadoxModTool.Notes;

/// <summary>
/// Benennt die Schlüssel in der Notiz- und der Lesezeichen-Datei um (absoluter Pfad zu <see cref="NoteKeys.ModKey"/> und zurück). Die Zuordnung liefert der
/// Aufrufer als Funktion <c>alter Schlüssel → neuer Schlüssel</c>, <see langword="null"/> bedeutet "unverändert". Trifft ein neuer Schlüssel
/// auf einen vorhandenen, werden die Einträge zusammengeführt; der vorhandene gewinnt bei Gleichstand. Der Umzug ist idempotent.
/// </summary>
public static class NoteKeyMigration
{
    /// <summary>Wendet die Zuordnung auf Notizen und Lesezeichen an (Bezüge von Meldungen: <see cref="BugKeyMigration"/>). Nur Dateien, die sich ändern, werden (unter Sperre) geschrieben. <see langword="true"/>, wenn irgendein Schlüssel umbenannt wurde.</summary>
    public static bool Run(string notesPath, string bookmarksPath, Func<string, string?> map)
    {
        var changed = false;

        if (NeedsChange(FileNoteStore.TryLoad(notesPath).Keys, map))
        {
            FileNoteStore.Update(notesPath, notes => RekeyNotes(notes, map));
            changed = true;
        }

        if (NeedsChange(BookmarkStore.TryLoad(bookmarksPath).Keys, map))
        {
            BookmarkStore.Update(bookmarksPath, bookmarks => RekeyBookmarks(bookmarks, map));
            changed = true;
        }

        return changed;
    }

    public static bool RekeyNotes(Dictionary<string, string> notes, Func<string, string?> map) =>
        Rekey(notes, map, (target, moved) =>
            target == moved ? target : target + "\n\n" + moved);

    public static bool RekeyBookmarks(Dictionary<string, List<BookmarkEntry>> bookmarks, Func<string, string?> map) =>
        Rekey(bookmarks, map, (target, moved) =>
        {
            target.AddRange(moved.Where(m => target.All(t => t.Line != m.Line)).ToList());
            return target;
        });

    private static bool NeedsChange(IEnumerable<string> keys, Func<string, string?> map) =>
        keys.Any(k => map(k) is { } newKey && newKey != k);

    private static bool Rekey<T>(Dictionary<string, T> data, Func<string, string?> map, Func<T, T, T> merge)
    {
        var changed = false;
        foreach (var key in data.Keys.ToList())
        {
            if (map(key) is not { } newKey || newKey == key)
            {
                continue;
            }

            var value = data[key];
            data.Remove(key);
            data[newKey] = data.TryGetValue(newKey, out var existing) ? merge(existing, value) : value;
            changed = true;
        }

        return changed;
    }
}
