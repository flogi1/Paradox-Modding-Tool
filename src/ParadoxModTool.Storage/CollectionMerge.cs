namespace ParadoxModTool.Storage;

/// <summary>
/// Drei-Wege-Zusammenführung für Sammlungen, die ein Prozess beim Start lädt (<c>baseline</c>), im Lauf ändert (<c>current</c>) und
/// beim Speichern mit dem inzwischen von einem anderen Prozess veränderten Stand auf der Platte (<c>disk</c>) vereinen muss.
/// Nur die eigenen Änderungen (gegenüber <c>baseline</c>) werden auf <c>disk</c> angewendet; alles, was ein anderer Prozess hinzugefügt hat, bleibt erhalten.
/// </summary>
public static class CollectionMerge
{
    /// <summary>Eigene Ergänzungen und Änderungen setzen sich durch, eigene Löschungen entfernen den Eintrag; unberührte Einträge bleiben, wie sie auf der Platte stehen.</summary>
    public static Dictionary<TKey, TValue> MergeDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> disk,
        IReadOnlyDictionary<TKey, TValue> baseline,
        IReadOnlyDictionary<TKey, TValue> current,
        IEqualityComparer<TKey>? keyComparer = null)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, TValue>(disk, keyComparer);
        var valueComparer = EqualityComparer<TValue>.Default;

        foreach (var key in baseline.Keys)
        {
            if (!current.ContainsKey(key))
            {
                result.Remove(key);
            }
        }

        foreach (var (key, value) in current)
        {
            if (!baseline.TryGetValue(key, out var before) || !valueComparer.Equals(before, value))
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Listen mit eigener Reihenfolge: Eigene Löschungen entfernen den Eintrag, eigene neue Einträge werden hinten angehängt
    /// (sofern die Platte sie nicht schon enthält). Die Reihenfolge der Platte bleibt erhalten.
    /// </summary>
    public static List<T> MergeList<T>(
        IReadOnlyList<T> disk,
        IReadOnlyList<T> baseline,
        IReadOnlyList<T> current,
        IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        var result = new List<T>(disk);

        foreach (var item in baseline)
        {
            if (!current.Contains(item, comparer))
            {
                result.RemoveAll(x => comparer.Equals(x, item));
            }
        }

        foreach (var item in current)
        {
            if (!baseline.Contains(item, comparer) && !result.Contains(item, comparer))
            {
                result.Add(item);
            }
        }

        return result;
    }
}
