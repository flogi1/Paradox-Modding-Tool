using System.Security.Cryptography;
using System.Text;

namespace ParadoxModTool.VanillaDiff;

/// <summary>Ergebnis einer Zuordnung: die Einheiten der Gegenseite mit neuen Schlüsseln und die Schlüssel, deren Zuordnung nicht eindeutig war.</summary>
public sealed record AlignedUnits(List<EntryUnit> Units, IReadOnlySet<string> AmbiguousKeys);

/// <summary>
/// Ordnet wiederholte Geschwister (mehrere <c>if</c>, <c>modifier</c>, …) zweier Fassungen nach Inhalt statt nach Position zu.
/// Einheiten der Gegenseite, die zu einer Einheit der Referenz passen, bekommen deren Schlüssel. Übrige bekommen einen Schlüssel
/// aus Basisschlüssel und Inhalts-Hash (<c>S:if+1a2b3c4d</c>), damit eine Einfügung an anderer Stelle keine Schlüssel verschiebt.
/// Schlüssel, die in keiner Fassung wiederholt vorkommen, bleiben unverändert (bisheriges Verhalten).
/// </summary>
public static class SiblingMatcher
{
    public const double MinSimilarity = 0.5;

    private const double Exact = 2.0;

    /// <summary>Basisschlüssel, die in mindestens einer Fassung mehr als einmal vorkommen.</summary>
    public static ISet<string> RepeatedBaseKeys(params IReadOnlyList<EntryUnit>[] sides) =>
        sides.SelectMany(side => side.GroupBy(u => u.BaseKey, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key))
            .ToHashSet(StringComparer.Ordinal);

    public static AlignedUnits Align(IReadOnlyList<EntryUnit> reference, IReadOnlyList<EntryUnit> other, ISet<string> repeated)
    {
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        var assigned = new Dictionary<EntryUnit, EntryUnit>();
        var referenceGroups = reference.GroupBy(u => u.BaseKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var group in other.GroupBy(u => u.BaseKey, StringComparer.Ordinal))
        {
            var others = group.ToList();
            if (!repeated.Contains(group.Key))
            {
                foreach (var unit in others)
                {
                    assigned[unit] = unit;
                }

                continue;
            }

            var refs = referenceGroups.TryGetValue(group.Key, out var found) ? found : new List<EntryUnit>();
            var (pairs, unclear) = MatchGroup(refs, others);
            var freshCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var identities = others.Select(IdentityOf).ToList();
            for (var j = 0; j < others.Count; j++)
            {
                EntryUnit rekeyed;
                if (pairs[j] is { } i)
                {
                    rekeyed = others[j].WithKey(refs[i].Key, refs[i].Display);
                }
                else if (identities[j] is { } identity && identities.Count(x => x?.Field == identity.Field && x?.Value == identity.Value) == 1)
                {
                    // Neuer Block mit Kennung (key/name/id): Schlüssel aus der Kennung, damit dieselbe Kennung in A und B denselben Schlüssel bekommt.
                    rekeyed = others[j].WithKey($"{group.Key}@{identity.Field}={identity.Value}", $"{others[j].BaseDisplay}[{identity.Value}]");
                }
                else
                {
                    var hash = Hash(Canonical(others[j].Entry));
                    freshCounts.TryGetValue(hash, out var n);
                    freshCounts[hash] = n + 1;
                    var suffix = "+" + hash + (n == 0 ? string.Empty : "#" + n);
                    rekeyed = others[j].WithKey(group.Key + suffix, others[j].BaseDisplay + suffix);
                }

                assigned[others[j]] = rekeyed;
                if (unclear[j])
                {
                    ambiguous.Add(rekeyed.Key);
                }
            }
        }

        return new AlignedUnits(other.Select(u => assigned[u]).ToList(), ambiguous);
    }

    /// <summary>
    /// Paart übrig gebliebene neue Einheiten (Schlüssel mit Inhalts-Hash) zweier Seiten je Basisschlüssel nach Position: <paramref name="right"/>
    /// bekommt dann den Schlüssel von <paramref name="left"/>. Für <see cref="MergeEngine"/>: Fügen Spiel-Update und Mod an derselben Stelle
    /// verschiedene Blöcke ein, wird das ein Konflikt statt einer stillen Verdopplung. Inhaltsgleiche Einfügungen teilen den Schlüssel schon.
    /// </summary>
    public static List<EntryUnit> PairFreshByPosition(IReadOnlyList<EntryUnit> left, IReadOnlyList<EntryUnit> right)
    {
        static bool Fresh(EntryUnit u) => u.Key.Length > u.BaseKey.Length && u.Key[u.BaseKey.Length] == '+';
        var rightKeys = right.Select(u => u.Key).ToHashSet(StringComparer.Ordinal);
        var leftKeys = left.Select(u => u.Key).ToHashSet(StringComparer.Ordinal);
        var open = left.Where(u => Fresh(u) && !rightKeys.Contains(u.Key))
            .GroupBy(u => u.BaseKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => new Queue<EntryUnit>(g), StringComparer.Ordinal);
        return right.Select(u =>
        {
            if (!Fresh(u) || leftKeys.Contains(u.Key) || !open.TryGetValue(u.BaseKey, out var queue) || queue.Count == 0)
            {
                return u;
            }

            var partner = queue.Dequeue();
            return u.WithKey(partner.Key, partner.Display);
        }).ToList();
    }

    /// <summary>Ähnlichkeit zweier Werte zwischen 0 und 1: zur Hälfte gleiche Teileinträge, zur Hälfte gleiche Teilschlüssel. <c>limit</c> zählt doppelt.</summary>
    public static double Similarity(ScriptValue a, ScriptValue b)
    {
        if (a is ScriptScalar sa && b is ScriptScalar sb)
        {
            return ScriptStructuralComparer.ValueEquals(sa, sb) ? 1.0 : 0.0;
        }

        if (a is not ScriptBlock ba || b is not ScriptBlock bb)
        {
            return 0.0;
        }

        var (fullA, keysA) = Signatures(ba);
        var (fullB, keysB) = Signatures(bb);
        return 0.5 * Dice(fullA, fullB) + 0.5 * Dice(keysA, keysB);
    }

    /// <summary>Einheitliche Textform ohne Kommentare und Formatierung, Grundlage für Hash und Ähnlichkeit.</summary>
    public static string Canonical(ScriptEntry entry) => entry switch
    {
        ScriptStatement s => s.Key + OperatorText(s.Operator) + Canonical(s.Value),
        ScriptListItem l => Canonical(l.Value),
        _ => string.Empty,
    };

    public static string Canonical(ScriptValue value) => value switch
    {
        ScriptScalar s => s.IsQuoted ? "\"" + s.Text + "\"" : s.Text,
        ScriptBlock b => "{" + string.Join(" ", b.Entries.Where(e => e is not ScriptCommentEntry).Select(Canonical)) + "}",
        _ => string.Empty,
    };

    public static string Hash(string text, int length = 8) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..length].ToLowerInvariant();

    private static string OperatorText(ScriptOperator op) => op switch
    {
        ScriptOperator.LessThan => "<",
        ScriptOperator.LessOrEqual => "<=",
        ScriptOperator.GreaterThan => ">",
        ScriptOperator.GreaterOrEqual => ">=",
        ScriptOperator.EqualsIfExists => "?=",
        ScriptOperator.NotEquals => "!=",
        _ => "=",
    };

    private static bool IsExact(EntryUnit a, EntryUnit b) =>
        ScriptStructuralComparer.EntryEquals(a.Entry, b.Entry) && EntryUnits.HeaderText(a.Header) == EntryUnits.HeaderText(b.Header);

    /// <summary>
    /// Gleiche Einheiten am Anfang und am Ende werden direkt gepaart; nur der Rest dazwischen läuft durch die (quadratische) Zuordnung.
    /// Große Gruppen mit wenigen Änderungen bleiben so schnell (Prüfbefund I9).
    /// </summary>
    private static (int?[] Pairs, bool[] Ambiguous) MatchGroup(List<EntryUnit> refs, List<EntryUnit> others)
    {
        var n = refs.Count;
        var m = others.Count;
        var lead = 0;
        while (lead < n && lead < m && IsExact(refs[lead], others[lead]))
        {
            lead++;
        }

        var trail = 0;
        while (trail < n - lead && trail < m - lead && IsExact(refs[n - 1 - trail], others[m - 1 - trail]))
        {
            trail++;
        }

        var pairs = new int?[m];
        var ambiguous = new bool[m];
        for (var i = 0; i < lead; i++)
        {
            pairs[i] = i;
        }

        for (var t = 0; t < trail; t++)
        {
            pairs[m - 1 - t] = n - 1 - t;
        }

        var (middlePairs, middleAmbiguous) = MatchMiddle(refs.GetRange(lead, n - lead - trail), others.GetRange(lead, m - lead - trail));
        for (var j = 0; j < middlePairs.Length; j++)
        {
            pairs[lead + j] = middlePairs[j] is { } i ? lead + i : null;
            ambiguous[lead + j] = middleAmbiguous[j];
        }

        return (pairs, ambiguous);
    }

    private static (int?[] Pairs, bool[] Ambiguous) MatchMiddle(List<EntryUnit> refs, List<EntryUnit> others)
    {
        var n = refs.Count;
        var m = others.Count;
        var score = new double[n, m];
        var refSignatures = refs.Select(u => u.Value is ScriptBlock b ? Signatures(b) : (new List<string>(), new List<string>())).ToArray();
        var otherSignatures = others.Select(u => u.Value is ScriptBlock b ? Signatures(b) : (new List<string>(), new List<string>())).ToArray();
        var refIds = refs.Select(IdentityOf).ToArray();
        var otherIds = others.Select(IdentityOf).ToArray();
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < m; j++)
            {
                if (ScriptStructuralComparer.EntryEquals(refs[i].Entry, others[j].Entry)
                    && EntryUnits.HeaderText(refs[i].Header) == EntryUnits.HeaderText(others[j].Header))
                {
                    score[i, j] = Exact;
                }
                else if (refIds[i] is { } refId && otherIds[j] is { } otherId && refId.Field == otherId.Field)
                {
                    // Kennung (key/name/id) auf beiden Seiten: nur sie zählt. Gleich = derselbe Block, auch wenn sonst alles anders ist;
                    // verschieden = nie derselbe Block, auch wenn der Rest fast gleich ist (Beispiel modifier_definition in estates_preload).
                    score[i, j] = refId.Value == otherId.Value ? 1.0 : 0.0;
                }
                else if (refs[i].Value is ScriptBlock && others[j].Value is ScriptBlock)
                {
                    score[i, j] = 0.5 * Dice(refSignatures[i].Item1, otherSignatures[j].Item1) + 0.5 * Dice(refSignatures[i].Item2, otherSignatures[j].Item2);
                }
                else
                {
                    score[i, j] = Similarity(refs[i].Value, others[j].Value);
                }
            }
        }

        // Beste Zuordnung ohne Überkreuzen (gewichtete längste gemeinsame Teilfolge).
        var best = new double[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                var value = Math.Max(best[i + 1, j], best[i, j + 1]);
                if (score[i, j] >= MinSimilarity)
                {
                    value = Math.Max(value, score[i, j] + best[i + 1, j + 1]);
                }

                best[i, j] = value;
            }
        }

        var pairs = new int?[m];
        int a = 0, c = 0;
        while (a < n && c < m)
        {
            if (score[a, c] >= MinSimilarity && Math.Abs(best[a, c] - (score[a, c] + best[a + 1, c + 1])) < 1e-9)
            {
                pairs[c] = a;
                a++;
                c++;
            }
            else if (Math.Abs(best[a, c] - best[a + 1, c]) < 1e-9)
            {
                a++;
            }
            else
            {
                c++;
            }
        }

        var ambiguous = new bool[m];
        for (var j = 0; j < m; j++)
        {
            if (pairs[j] is not { } i || score[i, j] >= Exact)
            {
                continue;
            }

            for (var k = 0; k < n; k++)
            {
                if (k != i && Math.Abs(score[k, j] - score[i, j]) < 1e-9)
                {
                    ambiguous[j] = true;
                    break;
                }
            }
        }

        return (pairs, ambiguous);
    }

    /// <summary>Felder, die einen wiederholten Block eindeutig benennen (in dieser Reihenfolge gesucht).</summary>
    private static readonly string[] IdentityFields = { "name", "key", "id" };

    /// <summary>Kennung eines Blocks: das erste Feld aus <see cref="IdentityFields"/> mit einfachem Wert in der obersten Ebene des Blocks.</summary>
    private static (string Field, string Value)? IdentityOf(EntryUnit unit)
    {
        if (unit.Value is not ScriptBlock block)
        {
            return null;
        }

        foreach (var field in IdentityFields)
        {
            if (block.Entries.OfType<ScriptStatement>().FirstOrDefault(s => string.Equals(s.Key, field, StringComparison.OrdinalIgnoreCase)) is { Value: ScriptScalar { Text.Length: > 0 } value })
            {
                return (field, value.Text);
            }
        }

        return null;
    }

    private static (List<string> Full, List<string> Keys) Signatures(ScriptBlock block)
    {
        var full = new List<string>();
        var keys = new List<string>();
        foreach (var unit in EntryUnits.Build(block.Entries))
        {
            var weight = unit.BaseKey == "S:limit" ? 2 : 1;
            var signature = unit.BaseKey + "=" + Canonical(unit.Value);
            for (var w = 0; w < weight; w++)
            {
                full.Add(signature);
                keys.Add(unit.BaseKey);
            }
        }

        return (full, keys);
    }

    private static double Dice(List<string> a, List<string> b)
    {
        if (a.Count + b.Count == 0)
        {
            return 1.0;
        }

        var counts = a.GroupBy(x => x, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var common = 0;
        foreach (var x in b)
        {
            if (counts.TryGetValue(x, out var count) && count > 0)
            {
                counts[x] = count - 1;
                common++;
            }
        }

        return 2.0 * common / (a.Count + b.Count);
    }
}
