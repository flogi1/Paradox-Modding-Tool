namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>
/// Nachschlagetabellen über den Datentyp-Einträgen: Typen, Promotes und Funktionen je Typ, globale Einträge, Stichwortsuche und kürzeste Wege über
/// Promotes (für den GUI-Daten-Helfer).
/// </summary>
public sealed class DataTypeDatabase
{
    private readonly Dictionary<string, List<DataTypeEntry>> _members = new(StringComparer.Ordinal);
    private readonly HashSet<string> _types = new(StringComparer.Ordinal);
    private readonly List<DataTypeEntry> _all;

    public DataTypeDatabase(IEnumerable<DataTypeEntry> entries)
    {
        _all = entries.ToList();
        foreach (var entry in _all)
        {
            if (entry.Kind == DataTypeKind.Type)
            {
                _types.Add(entry.Name);
            }
            else if (!entry.IsGlobal)
            {
                if (!_members.TryGetValue(entry.Owner, out var list))
                {
                    _members[entry.Owner] = list = new List<DataTypeEntry>();
                }

                list.Add(entry);
            }
        }

        foreach (var list in _members.Values)
        {
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name) is var byName && byName != 0 ? byName : a.Kind.CompareTo(b.Kind));
        }

        Types = _types.Order(StringComparer.Ordinal).ToList();
        Globals = _all.Where(e => e.IsGlobal).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
    }

    public static DataTypeDatabase Empty { get; } = new(Array.Empty<DataTypeEntry>());

    public IReadOnlyList<DataTypeEntry> Entries => _all;

    public IReadOnlyList<string> Types { get; }

    public IReadOnlyList<DataTypeEntry> Globals { get; }

    public bool IsType(string name) => _types.Contains(name);

    /// <summary>Promotes und Funktionen eines Typs, nach Name.</summary>
    public IReadOnlyList<DataTypeEntry> Members(string type) =>
        _members.TryGetValue(type, out var list) ? list : Array.Empty<DataTypeEntry>();

    /// <summary>Zieltyp des Promotes <paramref name="name"/> von <paramref name="type"/>; <see langword="null"/> ohne Promote mit bekanntem Typ.</summary>
    public string? Promote(string type, string name) =>
        Members(type).FirstOrDefault(e => e.Kind == DataTypeKind.Promote && e.Name == name && e.HasRegisteredReturn)?.ReturnType;

    /// <summary>Rückgabetyp eines globalen Promotes; <see langword="null"/>, wenn es ihn nicht gibt oder der Typ unbekannt ist.</summary>
    public string? GlobalPromote(string name) =>
        Globals.FirstOrDefault(e => e.Kind == DataTypeKind.GlobalPromote && e.Name == name && e.HasRegisteredReturn)?.ReturnType;

    /// <summary>
    /// Einträge, deren Name, Besitzer-Typ oder Beschreibung zur Anfrage passen (ohne Groß/Klein). Reihenfolge: exakter Name, Namensanfang, ein Wort des
    /// Namens oder Besitzers beginnt mit der Anfrage (<c>GetCapital</c> für "capital"), Name oder Besitzer enthält sie, Beschreibung; jeweils nach vollem Namen.
    /// </summary>
    public IReadOnlyList<DataTypeEntry> Search(string query, int limit = 200)
    {
        var q = query.Trim();
        if (q.Length == 0)
        {
            return Array.Empty<DataTypeEntry>();
        }

        return _all
            .Select(e => (Entry: e, Rank: Rank(e, q)))
            .Where(x => x.Rank >= 0)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Entry.FullName, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.Kind)
            .Take(limit)
            .Select(x => x.Entry)
            .ToList();
    }

    private static int Rank(DataTypeEntry entry, string query)
    {
        if (entry.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (entry.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (Words(entry.Name).Concat(Words(entry.Owner)).Any(w => w.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
        {
            return 2;
        }

        if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || entry.Owner.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ? 4 : -1;
    }

    /// <summary>Wörter eines Bezeichners: Grenzen an Großbuchstaben, Ziffernwechseln und Unterstrichen (<c>GetCapitalName</c> → Get, Capital, Name).</summary>
    internal static IEnumerable<string> Words(string identifier)
    {
        var start = 0;
        for (var i = 1; i <= identifier.Length; i++)
        {
            var boundary = i == identifier.Length
                || identifier[i] == '_'
                || (char.IsUpper(identifier[i]) && !char.IsUpper(identifier[i - 1]))
                || (char.IsDigit(identifier[i]) != char.IsDigit(identifier[i - 1]));
            if (!boundary)
            {
                continue;
            }

            var word = identifier[start..i].Trim('_');
            if (word.Length > 0)
            {
                yield return word;
            }

            start = i;
        }
    }

    /// <summary>
    /// Kürzeste Folge von Promotes von <paramref name="fromType"/> zu <paramref name="ownerType"/> (höchstens <paramref name="maxSteps"/>); leer bei
    /// gleichem Typ, <see langword="null"/> ohne Weg.
    /// </summary>
    public IReadOnlyList<DataTypeEntry>? PathTo(string fromType, string ownerType, int maxSteps = 3)
    {
        if (fromType == ownerType)
        {
            return Array.Empty<DataTypeEntry>();
        }

        var previous = new Dictionary<string, (string From, DataTypeEntry Via)>(StringComparer.Ordinal);
        var frontier = new List<string> { fromType };
        var seen = new HashSet<string>(StringComparer.Ordinal) { fromType };
        for (var step = 0; step < maxSteps && frontier.Count > 0; step++)
        {
            var next = new List<string>();
            foreach (var type in frontier)
            {
                foreach (var promote in Members(type).Where(e => e.Kind == DataTypeKind.Promote && e.HasRegisteredReturn))
                {
                    if (!seen.Add(promote.ReturnType))
                    {
                        continue;
                    }

                    previous[promote.ReturnType] = (type, promote);
                    if (promote.ReturnType == ownerType)
                    {
                        var path = new List<DataTypeEntry>();
                        for (var at = ownerType; at != fromType; at = previous[at].From)
                        {
                            path.Add(previous[at].Via);
                        }

                        path.Reverse();
                        return path;
                    }

                    next.Add(promote.ReturnType);
                }
            }

            frontier = next;
        }

        return null;
    }
}
