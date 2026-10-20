using ParadoxModTool.GameProfiles;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Bewertet alle Beiträge zu einem Schlüssel (siehe Tabelle im Plan, Task 6).</summary>
internal static class KeyClassifier
{
    /// <param name="firstWinsInLoadOrder">Victoria 3: bei "zuerst geladen gewinnt" zählt die Ladereihenfolge der Pfade, nicht der Playset-Rang (gemessen 2026-10-11).</param>
    /// <param name="injectAppends">INJECT hängt an (Victoria 3): gleiche Teilschlüssel zweier INJECTs haben keinen bekannten Gewinner.</param>
    /// <param name="vanillaFirst">Victoria 3: die zuerst geladene Vanilla-Definition des Schlüssels; lädt sie vor allen Mods, gewinnt sie.</param>
    /// <param name="lastWinsInLoadOrder">Stellaris: bei "zuletzt geladen gewinnt" zählt die Ladereihenfolge der Dateien, der Playset-Rang nur bei gleichem Pfad (Wiki).</param>
    /// <param name="vanillaLast">Stellaris: die zuletzt geladene Vanilla-Definition des Schlüssels; lädt sie nach allen Mods, gewinnt sie.</param>
    /// <param name="loadOrder">Reihenfolge der Pfade (Stellaris mit Groß-/Kleinschreibung); ohne Angabe <see cref="GameLoadOrder.Instance"/>.</param>
    public static CompatFinding? Classify(KeyGroup group, bool firstWinsInLoadOrder = false, bool injectAppends = false, CompatContribution? vanillaFirst = null,
        bool lastWinsInLoadOrder = false, CompatContribution? vanillaLast = null, GameLoadOrder? loadOrder = null)
    {
        var all = group.Contributions
            .OrderBy(c => c.Participant.Rank)
            .ThenBy(c => c.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (all.Select(c => c.Participant.Id).Distinct().Count() < 2)
        {
            return null;
        }

        var path = all[^1].RelativePath;
        var names = Names(all);
        var uncertain = !group.Certain;
        var order = loadOrder ?? GameLoadOrder.Instance;

        CompatFinding Make(CompatKind kind, CompatSeverity severity, string message, CompatContribution? winner = null, CompatSuggestion suggestion = CompatSuggestion.None, bool extraUncertain = false) => new()
        {
            Severity = severity,
            Kind = kind,
            RelativePath = path,
            Key = group.Key,
            Contributions = all,
            Winner = winner?.Participant,
            Suggestion = suggestion,
            Message = message,
            Uncertain = uncertain || extraUncertain,
        };

        if (all.All(c => c.Operator == all[0].Operator) && AllSame(all))
        {
            return Make(CompatKind.KeyIdentical, CompatSeverity.Info, ModCompatTexts.FindingKeyIdentical(group.Key, names));
        }

        switch (group.Behavior)
        {
            case OverrideBehavior.FirstWins:
            {
                var first = firstWinsInLoadOrder
                    ? all.OrderBy(c => c.RelativePath, order).ThenByDescending(c => c.Participant.Rank).First()
                    : all[0];
                if (vanillaFirst is not null && order.Compare(vanillaFirst.RelativePath, first.RelativePath) < 0)
                {
                    first = vanillaFirst;
                }

                return Make(CompatKind.KeyConflict, CompatSeverity.Conflict, ModCompatTexts.FindingFirstWins(group.Key, names, first.Participant.Name), first, CompatSuggestion.NeedsCompatch);
            }
            case OverrideBehavior.LastWins:
            case OverrideBehavior.PartialKeys:
            {
                var last = lastWinsInLoadOrder
                    ? all.OrderBy(c => c.RelativePath, order).ThenBy(c => c.Participant.Rank).Last()
                    : all[^1];
                if (vanillaLast is not null && order.Compare(vanillaLast.RelativePath, last.RelativePath) > 0)
                {
                    last = vanillaLast;
                }

                return Make(CompatKind.KeyConflict, CompatSeverity.Conflict, ModCompatTexts.FindingLastWins(group.Key, names, last.Participant.Name), last, CompatSuggestion.NeedsCompatch);
            }
            case OverrideBehavior.AppendOnly:
                return Make(CompatKind.KeyCombinable, CompatSeverity.Hint, ModCompatTexts.FindingAppendOnly(group.Key, names), extraUncertain: true);
            case OverrideBehavior.LocalizationKeys:
            {
                var replacing = all.Where(c => c.Operator == OverrideOperator.Replace).ToList();
                var winner = replacing.Count > 0 ? replacing[^1] : all[^1];
                return Make(CompatKind.KeyConflict, CompatSeverity.Conflict, ModCompatTexts.FindingLocalizationKey(group.Key, names, winner.Participant.Name), winner, CompatSuggestion.NeedsCompatch, extraUncertain: replacing.Count == 0);
            }

            default:
                return ClassifyPerKey(group, all, names, injectAppends, (kind, severity, message, winner, suggestion, extra) => Make(kind, severity, message, winner, suggestion, extra));
        }
    }

    private static CompatFinding ClassifyPerKey(KeyGroup group, List<CompatContribution> all, string names, bool injectAppends, Func<CompatKind, CompatSeverity, string, CompatContribution?, CompatSuggestion, bool, CompatFinding> make)
    {
        var replaces = all.Where(c => new OverrideKey(c.Operator, string.Empty).IsReplace).ToList();
        var injects = all.Where(c => new OverrideKey(c.Operator, string.Empty).IsInject).ToList();
        var plains = all.Where(c => c.Operator == OverrideOperator.None).ToList();

        if (replaces.Count > 0)
        {
            var winner = replaces[^1];
            if (replaces.Select(r => r.Participant.Id).Distinct().Count() > 1)
            {
                return make(CompatKind.KeyConflict, CompatSeverity.Conflict, ModCompatTexts.FindingMultipleReplace(group.Key, Names(replaces), winner.Participant.Name), winner, CompatSuggestion.NeedsCompatch, false);
            }

            var lostInjects = injects.Where(i => i.Participant.Id != winner.Participant.Id).ToList();
            if (lostInjects.Count > 0)
            {
                return make(CompatKind.KeyLoss, CompatSeverity.Conflict, ModCompatTexts.FindingInjectLost(winner.Participant.Name, group.Key, Names(lostInjects)), winner, CompatSuggestion.ReviewBothSides, false);
            }

            return make(CompatKind.KeyLoss, CompatSeverity.Hint, ModCompatTexts.FindingReplacesDefinition(winner.Participant.Name, group.Key, Names(plains)), winner, CompatSuggestion.None, false);
        }

        if (plains.Select(p => p.Participant.Id).Distinct().Count() > 1 && !AllSame(plains))
        {
            var winner = plains[^1];
            return make(CompatKind.KeyConflict, CompatSeverity.Conflict, ModCompatTexts.FindingPlainConflict(Names(plains), group.Key, winner.Participant.Name), winner, CompatSuggestion.NeedsCompatch, false);
        }

        var pool = injects.Concat(plains).ToList();
        var clashes = OverlappingChildKeys(pool);
        if (clashes.Count > 0 && injectAppends)
        {
            // Victoria 3 (Wiki): INJECT hängt an, beide Werte stehen danach im Eintrag; welcher gilt, ist nicht gemessen.
            return make(CompatKind.KeyConflict, CompatSeverity.Conflict, ModCompatTexts.FindingChildAppend(names, string.Join(", ", clashes), group.Key), null, CompatSuggestion.NeedsCompatch, true);
        }

        if (clashes.Count > 0)
        {
            // INJECT überschreibt vorhandene Teilschlüssel, das zuletzt angewendete gewinnt (gemessen 2026-10-02 in Europa Universalis V mit einem
            // einfachen Wert). Bei Unterblöcken ist offen, ob das Spiel ersetzt oder tiefer zusammenführt.
            // Ein Konflikt braucht zwei Mods mit verschiedenem Wert; Definitionen ohne Präfix stammen hier von höchstens einer Mod, also ist ein INJECT dabei.
            var winner = injects.LastOrDefault(i => clashes.Any(Children(i).ContainsKey)) ?? pool[^1];
            var blockClash = clashes.Any(key => pool.Any(c => Children(c).TryGetValue(key, out var values) && values.Any(v => v is ScriptBlock)));
            return make(CompatKind.KeyConflict, CompatSeverity.Conflict, ModCompatTexts.FindingChildOverwrite(names, string.Join(", ", clashes), group.Key, winner.Participant.Name), winner, CompatSuggestion.NeedsCompatch, blockClash);
        }

        return make(CompatKind.KeyCombinable, CompatSeverity.Hint, ModCompatTexts.FindingChildCombine(names, group.Key), null, CompatSuggestion.None, false);
    }

    private static string Names(IEnumerable<CompatContribution> contributions) =>
        string.Join(", ", contributions.Select(c => "\"" + c.Participant.Name + "\"").Distinct());

    internal static bool SameContent(CompatContribution a, CompatContribution b) =>
        a.Statement is not null && b.Statement is not null
            ? ScriptStructuralComparer.ValueEquals(a.Statement.Value, b.Statement.Value)
            : string.Equals(a.Text?.Trim(), b.Text?.Trim(), StringComparison.Ordinal);

    private static bool AllSame(IReadOnlyList<CompatContribution> list) => list.Skip(1).All(c => SameContent(list[0], c));

    /// <summary>Teilschlüssel, die zwei verschiedene Mods mit unterschiedlichem Wert setzen.</summary>
    private static List<string> OverlappingChildKeys(IReadOnlyList<CompatContribution> pool)
    {
        var clashes = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < pool.Count; i++)
        {
            for (var j = i + 1; j < pool.Count; j++)
            {
                if (pool[i].Participant.Id == pool[j].Participant.Id)
                {
                    continue;
                }

                var left = Children(pool[i]);
                var right = Children(pool[j]);
                foreach (var (key, values) in left)
                {
                    if (right.TryGetValue(key, out var other) && !(values.Count == other.Count && values.Zip(other).All(p => ScriptStructuralComparer.ValueEquals(p.First, p.Second))))
                    {
                        clashes.Add(key);
                    }
                }
            }
        }

        return clashes.OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, List<ScriptValue>> Children(CompatContribution contribution)
    {
        var result = new Dictionary<string, List<ScriptValue>>(StringComparer.Ordinal);
        if (contribution.Statement?.Value is ScriptBlock block)
        {
            foreach (var child in block.Entries.OfType<ScriptStatement>())
            {
                if (!result.TryGetValue(child.Key, out var list))
                {
                    list = new List<ScriptValue>();
                    result[child.Key] = list;
                }

                list.Add(child.Value);
            }
        }
        else if (contribution.Statement is not null)
        {
            result[string.Empty] = new List<ScriptValue> { contribution.Statement.Value };
        }

        return result;
    }
}
