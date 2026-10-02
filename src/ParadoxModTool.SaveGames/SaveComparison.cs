namespace ParadoxModTool.SaveGames;

public enum SaveChange
{
    Unchanged,
    Added,
    Removed,
    Changed,
}

/// <summary>Ein Eintrag im Vergleich; <see cref="Before"/> fehlt bei neuen, <see cref="After"/> bei entfernten Einträgen.</summary>
public sealed record SaveDiffEntry(string OwnerPath, string Name, SaveEntryKind Kind, SaveVariableEntry? Before, SaveVariableEntry? After, SaveChange Change);

/// <summary>
/// Vergleich zweier Spielstände. Variablen je (Besitzer, Name): neu, entfernt, geändert (Typ oder Wert) oder unverändert. Listen zählen als
/// Menge mit Wiederholungen je (Besitzer, Name, Typ, Wert): nur neu, entfernt oder unverändert.
/// </summary>
public sealed class SaveComparison
{
    private readonly Dictionary<(string Owner, string Name, SaveEntryKind Kind), SaveDiffEntry> _first = new();
    private readonly Dictionary<string, SaveChange> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SaveChange> _byOwner = new(StringComparer.Ordinal);

    private SaveComparison(IReadOnlyList<SaveDiffEntry> entries)
    {
        Entries = entries;
        foreach (var entry in entries)
        {
            _first.TryAdd((entry.OwnerPath, entry.Name, entry.Kind), entry);
            Merge(_byName, entry.Name, entry.Change);
            Merge(_byOwner, entry.OwnerPath, entry.Change);
        }
    }

    public IReadOnlyList<SaveDiffEntry> Entries { get; }

    public static SaveComparison Compare(SaveSnapshot before, SaveSnapshot after)
    {
        var entries = new List<SaveDiffEntry>();
        CompareVariables(before, after, entries);
        CompareLists(before, after, entries);
        entries.Sort((a, b) =>
        {
            var owner = string.CompareOrdinal(a.OwnerPath, b.OwnerPath);
            return owner != 0 ? owner : string.CompareOrdinal(a.Name, b.Name);
        });
        return new SaveComparison(entries);
    }

    /// <summary>Zusammenfassung je Name: nur neu, nur entfernt, gemischt oder geändert (<see cref="SaveChange.Changed"/>), sonst unverändert.</summary>
    public SaveChange ChangeOfName(string name) => _byName.GetValueOrDefault(name, SaveChange.Unchanged);

    public SaveChange ChangeOfObject(string ownerPath) => _byOwner.GetValueOrDefault(ownerPath, SaveChange.Unchanged);

    /// <summary>Der Eintrag einer Variable; bei Listen der erste Eintrag dieses Namens beim Besitzer.</summary>
    public SaveDiffEntry? Find(string ownerPath, string name, SaveEntryKind kind) => _first.GetValueOrDefault((ownerPath, name, kind));

    private static void CompareVariables(SaveSnapshot before, SaveSnapshot after, List<SaveDiffEntry> result)
    {
        static Dictionary<(string, string), SaveVariableEntry> Index(SaveSnapshot snapshot)
        {
            var index = new Dictionary<(string, string), SaveVariableEntry>();
            foreach (var entry in snapshot.Entries.Where(e => e.Kind == SaveEntryKind.Variable))
            {
                index[(entry.OwnerPath, entry.Name)] = entry;
            }

            return index;
        }

        var old = Index(before);
        var current = Index(after);
        foreach (var (key, was) in old)
        {
            if (current.TryGetValue(key, out var now))
            {
                var same = was.Type == now.Type && was.RawValue == now.RawValue;
                result.Add(new SaveDiffEntry(key.Item1, key.Item2, SaveEntryKind.Variable, was, now, same ? SaveChange.Unchanged : SaveChange.Changed));
            }
            else
            {
                result.Add(new SaveDiffEntry(key.Item1, key.Item2, SaveEntryKind.Variable, was, null, SaveChange.Removed));
            }
        }

        foreach (var (key, now) in current)
        {
            if (!old.ContainsKey(key))
            {
                result.Add(new SaveDiffEntry(key.Item1, key.Item2, SaveEntryKind.Variable, null, now, SaveChange.Added));
            }
        }
    }

    private static void CompareLists(SaveSnapshot before, SaveSnapshot after, List<SaveDiffEntry> result)
    {
        static Dictionary<(string Owner, string Name, string Type, string Value), List<SaveVariableEntry>> Index(SaveSnapshot snapshot)
        {
            var index = new Dictionary<(string, string, string, string), List<SaveVariableEntry>>();
            foreach (var entry in snapshot.Entries.Where(e => e.Kind == SaveEntryKind.ListItem))
            {
                var key = (entry.OwnerPath, entry.Name, entry.Type, entry.RawValue);
                if (!index.TryGetValue(key, out var list))
                {
                    list = new List<SaveVariableEntry>();
                    index[key] = list;
                }

                list.Add(entry);
            }

            return index;
        }

        var old = Index(before);
        var current = Index(after);
        foreach (var key in old.Keys.Union(current.Keys))
        {
            var was = old.GetValueOrDefault(key) ?? new List<SaveVariableEntry>();
            var now = current.GetValueOrDefault(key) ?? new List<SaveVariableEntry>();
            var kept = Math.Min(was.Count, now.Count);
            for (var i = 0; i < kept; i++)
            {
                result.Add(new SaveDiffEntry(key.Owner, key.Name, SaveEntryKind.ListItem, was[i], now[i], SaveChange.Unchanged));
            }

            for (var i = kept; i < was.Count; i++)
            {
                result.Add(new SaveDiffEntry(key.Owner, key.Name, SaveEntryKind.ListItem, was[i], null, SaveChange.Removed));
            }

            for (var i = kept; i < now.Count; i++)
            {
                result.Add(new SaveDiffEntry(key.Owner, key.Name, SaveEntryKind.ListItem, null, now[i], SaveChange.Added));
            }
        }
    }

    private static void Merge(Dictionary<string, SaveChange> summary, string key, SaveChange change)
    {
        if (change == SaveChange.Unchanged)
        {
            return;
        }

        summary[key] = summary.TryGetValue(key, out var existing) && existing != change ? SaveChange.Changed : change;
    }
}
