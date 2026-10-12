using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Wendet die Beiträge zu einem Schlüssel in Ladereihenfolge an (Ruling 6 im Plan).</summary>
internal static class EffectiveCombiner
{
    /// <param name="ordered">Beiträge in Ladereihenfolge (siehe <see cref="EffectiveEntryResolver"/>).</param>
    /// <param name="injectAppends">INJECT hängt an, statt Teilschlüssel zu überschreiben (Victoria 3, siehe <see cref="OverrideRules.InjectAppends"/>).</param>
    public static Effective Combine(IReadOnlyList<KeyPart> ordered, OverrideBehavior behavior, bool certain, bool injectAppends = false)
    {
        if (ordered.Count == 0)
        {
            return Effective.Absent;
        }

        var plain = ordered.Where(p => p.Operator == OverrideOperator.None).ToList();
        var source = behavior == OverrideBehavior.FirstWins ? plain.FirstOrDefault() : plain.LastOrDefault();
        var entries = source?.Entries;
        var operators = new List<OverrideOperator>();
        if (source is not null)
        {
            operators.Add(OverrideOperator.None);
        }
        var uncertain = !certain
            || (behavior == OverrideBehavior.PerKey && plain.Select(p => p.RelativePath.ToLowerInvariant()).Distinct().Count() > 1);

        foreach (var part in ordered.Where(p => p.Operator != OverrideOperator.None).OrderBy(p => new OverrideKey(p.Operator, string.Empty).Order))
        {
            var op = new OverrideKey(part.Operator, string.Empty);
            if (op.IsInject)
            {
                if (entries is null)
                {
                    if (part.Operator == OverrideOperator.InjectOrCreate)
                    {
                        entries = Unprefixed(part);
                        source = part;
                        operators.Add(part.Operator);
                    }

                    continue;
                }

                entries = injectAppends ? Append(entries, part, ref uncertain) : Inject(entries, part, ref uncertain);
                source = part;
                operators.Add(part.Operator);
            }
            else if (op.IsReplace)
            {
                if (entries is null && part.Operator != OverrideOperator.ReplaceOrCreate && behavior != OverrideBehavior.LocalizationKeys)
                {
                    continue;
                }

                entries = Unprefixed(part);
                source = part;
                operators.Add(part.Operator);
            }
        }

        if (entries is null || source is null)
        {
            return Effective.Absent;
        }

        return new Effective { Entries = entries, RelativePath = source.RelativePath, FullPath = source.FullPath, Side = source.Side, Uncertain = uncertain, Operators = operators };
    }

    /// <summary>Schreibt den Schlüssel ohne Präfix (<c>INJECT:farm</c> → <c>farm</c>). Lokalisierungsschlüssel (<c>english/foo</c>) bleiben, wie sie sind.</summary>
    private static IReadOnlyList<ScriptEntry> Unprefixed(KeyPart part) =>
        part.Entries.Select(e => e is ScriptStatement s && s.Key != part.Address.Key && part.Address.Kind == AddressKind.Key && !part.Address.Key.Contains('/')
            ? new ScriptStatement { Key = part.Address.Key, Operator = s.Operator, Value = s.Value, LeadingComments = s.LeadingComments, TrailingComment = s.TrailingComment }
            : e).ToList();

    /// <summary>
    /// INJECT in Victoria 3 (https://vic3.paradoxwikis.com/Mod_compatibility): Die Einträge kommen hinten an den vorhandenen Block, nichts wird
    /// überschrieben. Ein Teilschlüssel, den der Block schon hat, ist unsicher: Trigger- und Effektblöcke lassen sich so laut Wiki nicht
    /// einfügen, und welcher von zwei Werten gilt, ist nicht gemessen.
    /// </summary>
    private static IReadOnlyList<ScriptEntry> Append(IReadOnlyList<ScriptEntry> baseEntries, KeyPart part, ref bool uncertain)
    {
        if (baseEntries.Count != 1 || baseEntries[0] is not ScriptStatement { Value: ScriptBlock baseBlock } baseStatement
            || part.Entries.Count != 1 || part.Entries[0] is not ScriptStatement { Value: ScriptBlock injected })
        {
            uncertain = true;
            return baseEntries;
        }

        var baseKeys = EntryUnits.Build(baseBlock.Entries).Select(u => u.BaseKey).ToHashSet(StringComparer.Ordinal);
        if (EntryUnits.Build(injected.Entries).Any(u => baseKeys.Contains(u.BaseKey)))
        {
            uncertain = true;
        }

        return new ScriptEntry[] { EntryUnits.WithBlockValue(baseStatement, new ScriptBlock { Entries = baseBlock.Entries.Concat(injected.Entries).ToList() }) };
    }

    private static IReadOnlyList<ScriptEntry> Inject(IReadOnlyList<ScriptEntry> baseEntries, KeyPart part, ref bool uncertain)
    {
        if (baseEntries.Count != 1 || baseEntries[0] is not ScriptStatement { Value: ScriptBlock baseBlock } baseStatement
            || part.Entries.Count != 1 || part.Entries[0] is not ScriptStatement { Value: ScriptBlock injected })
        {
            uncertain = true;
            return baseEntries;
        }

        // INJECT überschreibt einen vorhandenen Teilschlüssel an seiner Stelle und hängt neue an (gemessen 2026-10-02 in Europa Universalis V:
        // INJECT:build_gravel_road = { gold = 999 } über Vanilla gold = 10 ergibt 999). Gemessen ist nur ein einfacher Wert: Ob das Spiel einen
        // Unterblock ganz ersetzt oder tiefer zusammenführt, ist offen, ebenso wiederholte Schlüssel. Solche Fälle bleiben unsicher.
        var baseByKey = EntryUnits.Build(baseBlock.Entries).GroupBy(u => u.BaseKey).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var injectedUnits = EntryUnits.Build(injected.Entries);
        var replacements = new Dictionary<string, List<ScriptEntry>>(StringComparer.Ordinal);
        foreach (var unit in injectedUnits)
        {
            if (!baseByKey.TryGetValue(unit.BaseKey, out var old))
            {
                continue;
            }

            if (unit.Kind != EntryUnit.UnitKind.Statement || old.Any(o => o.Kind != EntryUnit.UnitKind.Statement))
            {
                // Listen und GUI-Rümpfe: angehängt wie bisher, Wirkung unbekannt.
                uncertain = true;
                continue;
            }

            if (old.Count > 1 || injectedUnits.Count(u => u.BaseKey == unit.BaseKey) > 1 || unit.Value is ScriptBlock || old.Any(o => o.Value is ScriptBlock))
            {
                uncertain = true;
            }

            if (!replacements.TryGetValue(unit.BaseKey, out var list))
            {
                replacements[unit.BaseKey] = list = new List<ScriptEntry>();
            }

            list.Add(unit.Entry);
        }

        var removed = new Dictionary<ScriptEntry, string>(ReferenceEqualityComparer.Instance);
        foreach (var key in replacements.Keys)
        {
            foreach (var unit in baseByKey[key])
            {
                removed[unit.Entry] = key;
            }
        }

        var consumed = replacements.Values.SelectMany(l => l).ToHashSet(ReferenceEqualityComparer.Instance);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<ScriptEntry>();
        foreach (var entry in baseBlock.Entries)
        {
            if (!removed.TryGetValue(entry, out var key))
            {
                result.Add(entry);
            }
            else if (placed.Add(key))
            {
                result.AddRange(replacements[key]);
            }
        }

        result.AddRange(injected.Entries.Where(e => !consumed.Contains(e)));
        return new ScriptEntry[] { EntryUnits.WithBlockValue(baseStatement, new ScriptBlock { Entries = result }) };
    }
}
