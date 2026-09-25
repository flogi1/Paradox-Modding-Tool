using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Wendet die Beiträge zu einem Schlüssel in Ladereihenfolge an (Ruling 6 im Plan).</summary>
internal static class EffectiveCombiner
{
    /// <param name="ordered">Beiträge in Ladereihenfolge (siehe <see cref="EffectiveEntryResolver"/>).</param>
    public static Effective Combine(IReadOnlyList<KeyPart> ordered, OverrideBehavior behavior, bool certain)
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

                entries = Inject(entries, part, ref uncertain);
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

    private static IReadOnlyList<ScriptEntry> Inject(IReadOnlyList<ScriptEntry> baseEntries, KeyPart part, ref bool uncertain)
    {
        if (baseEntries.Count != 1 || baseEntries[0] is not ScriptStatement { Value: ScriptBlock baseBlock } baseStatement
            || part.Entries.Count != 1 || part.Entries[0] is not ScriptStatement { Value: ScriptBlock injected })
        {
            uncertain = true;
            return baseEntries;
        }

        // Ob INJECT vorhandene Teilschlüssel überschreibt, ist nicht dokumentiert: dann unsicher, angehängt wird trotzdem.
        var existing = EntryUnits.Build(baseBlock.Entries).Select(u => u.BaseKey).ToHashSet(StringComparer.Ordinal);
        if (EntryUnits.Build(injected.Entries).Any(u => existing.Contains(u.BaseKey)))
        {
            uncertain = true;
        }

        return new ScriptEntry[] { EntryUnits.WithBlockValue(baseStatement, new ScriptBlock { Entries = baseBlock.Entries.Concat(injected.Entries).ToList() }) };
    }
}
