namespace ParadoxModTool.Desync;

/// <summary>
/// Namenstabellen beider Spielstände zusammen: je Muster aus <see cref="DesyncProfile.Names"/> (Text des Musters, z.B.
/// <c>countries/database/{id}</c>) die Zuordnung Objekt-Id → Name. Beim Zusammenführen gewinnt die Referenzseite.
/// </summary>
public sealed record SaveNameTables(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Tables)
{
    /// <summary>Keine Namen bekannt.</summary>
    public static SaveNameTables Empty { get; } = new(new Dictionary<string, IReadOnlyDictionary<string, string>>());

    /// <summary>Vereinigt beide Seiten; kennen beide eine Id, gilt der Name der Referenz.</summary>
    public static SaveNameTables Merge(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> reference,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> desynced)
    {
        var merged = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var pattern in reference.Keys.Concat(desynced.Keys).Distinct(StringComparer.Ordinal))
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            if (desynced.TryGetValue(pattern, out var desyncedNames))
            {
                foreach (var (id, name) in desyncedNames)
                {
                    names[id] = name;
                }
            }

            if (reference.TryGetValue(pattern, out var referenceNames))
            {
                foreach (var (id, name) in referenceNames)
                {
                    names[id] = name;
                }
            }

            merged[pattern] = names;
        }

        return new SaveNameTables(merged);
    }
}

/// <summary>
/// Macht Spielstandpfade lesbar: Ids von Objekten, für die das Profil eine Namensquelle kennt, erhalten ihren Namen, und das
/// Behältersegment vor der Id entfällt (<c>countries/database/1384/government</c> wird zu <c>countries › 1384 (BLG) › government</c>).
/// </summary>
public sealed class SaveNameResolver
{
    private const string Separator = " › ";

    private readonly SaveNameTables _tables;
    private readonly IReadOnlyList<(PathPattern Pattern, string Key)> _patterns;

    public SaveNameResolver(SaveNameTables tables, DesyncProfile profile)
    {
        _tables = tables;
        _patterns = profile.Names.Keys.Select(key => (PathPattern.Parse(key), key)).ToList();
    }

    /// <summary>
    /// Pfad mit Namen für die Anzeige, Segmente durch <c>›</c> getrennt. Eine Id, deren Objektmuster das Profil kennt, verliert das
    /// Behältersegment davor (sofern davor noch ein Segment bleibt); der Name steht nur dabei, wenn die Tabellen ihn kennen.
    /// </summary>
    public string Display(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var shown = new List<string>(segments.Length);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var key = IsId(segment) ? KeyFor(segments, i + 1) : null;
            if (key is null)
            {
                shown.Add(segment);
                continue;
            }

            if (i >= 2)
            {
                shown.RemoveAt(shown.Count - 1);
            }

            string? name = null;
            var known = _tables.Tables.TryGetValue(key, out var names) && names.TryGetValue(segment, out name);
            shown.Add(known ? segment + " (" + name + ")" : segment);
        }

        return string.Join(Separator, shown);
    }

    /// <summary>Schlüssel der Namenstabelle, deren Objektmuster auf die ersten <paramref name="length"/> Segmente passt.</summary>
    private string? KeyFor(string[] segments, int length)
    {
        var prefix = string.Join('/', segments, 0, length);
        foreach (var (pattern, key) in _patterns)
        {
            if (pattern.Matches(prefix))
            {
                return key;
            }
        }

        return null;
    }

    private static bool IsId(string segment) => segment.Length > 0 && segment.All(char.IsAsciiDigit);
}
