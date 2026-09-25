using ParadoxModTool.Scopes;

namespace ParadoxModTool.Templates;

/// <summary>Zählt je Ordner und Feld, wie oft der Wert eines Feldes ein Lokalisierungsschlüssel ist.</summary>
public sealed class LocalizationFieldTally
{
    /// <summary>Ein Feld braucht so viele Treffer, um gespeichert zu werden.</summary>
    public const int MinHits = 15;

    /// <summary>Ab diesem Anteil wird ein Feld als Angebot gespeichert.</summary>
    public const double OfferShare = 0.25;

    /// <summary>Ab diesem Anteil ist ein Feld vorgewählt.</summary>
    public const double PreselectShare = 0.80;

    private readonly Dictionary<(string Folder, string Field), FieldCount> _counts = new();

    private sealed class FieldCount
    {
        public int Total;

        public readonly Dictionary<string, int> KeyValues = new(StringComparer.Ordinal);
    }

    public IEnumerable<string> Folders => _counts.Keys.Select(k => k.Folder).Distinct(StringComparer.Ordinal);

    public void Add(string folder, string field, string value, IReadOnlySet<string> keys)
    {
        var id = (folder, field.ToLowerInvariant());
        if (!_counts.TryGetValue(id, out var count))
        {
            _counts[id] = count = new FieldCount();
        }

        count.Total++;
        if (keys.Contains(value))
        {
            count.KeyValues[value] = count.KeyValues.GetValueOrDefault(value) + 1;
        }
    }

    /// <summary>
    /// Die Felder des Ordners, deren Wert oft ein Loc-Schlüssel ist. Ein Wert, der zugleich der Name eines Objekts ist (<paramref name="objectNames"/>), ist ein
    /// Verweis (<c>religion = catholic</c>) und zählt nicht.
    /// </summary>
    public IReadOnlyList<LocalizationFieldPattern> Select(string folder, IReadOnlySet<string> objectNames)
    {
        var result = new List<LocalizationFieldPattern>();
        foreach (var ((countFolder, field), count) in _counts)
        {
            if (countFolder != folder)
            {
                continue;
            }

            var hits = count.KeyValues.Where(kv => !objectNames.Contains(kv.Key)).Sum(kv => kv.Value);
            if (hits < MinHits)
            {
                continue;
            }

            var share = (double)hits / count.Total;
            if (share < OfferShare)
            {
                continue;
            }

            result.Add(new LocalizationFieldPattern { Name = field, Hits = hits, Share = Math.Round(share, 3), Preselected = share >= PreselectShare });
        }

        return result.OrderByDescending(f => f.Hits).ThenBy(f => f.Name, StringComparer.Ordinal).ToList();
    }
}

/// <summary>Einfache Scans über die Token einer Skriptdatei.</summary>
public static class LocalizationScan
{
    /// <summary>Die Schlüssel der Blöcke auf oberster Ebene (<c>marketplace = { … }</c>); skalare Einträge zählen nicht.</summary>
    public static List<string> TopLevelNames(IReadOnlyList<ScopeToken> tokens)
    {
        var names = new List<string>();
        var depth = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == ScopeTokenKind.Open)
            {
                depth++;
            }
            else if (token.Kind == ScopeTokenKind.Close)
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0
                && token.Kind is ScopeTokenKind.Word or ScopeTokenKind.String
                && i + 2 < tokens.Count
                && tokens[i + 1] is { Kind: ScopeTokenKind.Operator, Text: "=" }
                && tokens[i + 2].Kind == ScopeTokenKind.Open)
            {
                names.Add(token.Text);
            }
        }

        return names;
    }

    /// <summary>Zählt jedes <c>feld = wert</c> mit einem Wort oder String als Wert in <paramref name="tally"/>.</summary>
    public static void CountFields(string folder, IReadOnlyList<ScopeToken> tokens, IReadOnlySet<string> keys, LocalizationFieldTally tally)
    {
        for (var i = 0; i + 2 < tokens.Count; i++)
        {
            if (tokens[i].Kind != ScopeTokenKind.Word
                || tokens[i + 1] is not { Kind: ScopeTokenKind.Operator, Text: "=" }
                || tokens[i + 2].Kind is not (ScopeTokenKind.Word or ScopeTokenKind.String))
            {
                continue;
            }

            tally.Add(folder, tokens[i].Text, tokens[i + 2].Text, keys);
        }
    }
}
