using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Bewertet alle Beiträge zu einem Schlüssel (siehe Tabelle im Plan, Task 6).</summary>
internal static class KeyClassifier
{
    public static CompatFinding? Classify(KeyGroup group)
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
            return Make(CompatKind.KeyIdentical, CompatSeverity.Info, $"Der Schlüssel \"{group.Key}\" ist in {names} inhaltsgleich.");
        }

        switch (group.Behavior)
        {
            case OverrideBehavior.FirstWins:
                return Make(CompatKind.KeyConflict, CompatSeverity.Conflict, $"Der Schlüssel \"{group.Key}\" ist in {names} verschieden definiert. Die zuerst geladene Fassung gewinnt: \"{all[0].Participant.Name}\".", all[0], CompatSuggestion.NeedsCompatch);
            case OverrideBehavior.LastWins:
            case OverrideBehavior.PartialKeys:
                return Make(CompatKind.KeyConflict, CompatSeverity.Conflict, $"Der Schlüssel \"{group.Key}\" ist in {names} verschieden gesetzt. Es gilt die zuletzt geladene Fassung: \"{all[^1].Participant.Name}\".", all[^1], CompatSuggestion.NeedsCompatch);
            case OverrideBehavior.AppendOnly:
                return Make(CompatKind.KeyCombinable, CompatSeverity.Hint, $"\"{group.Key}\" wird von {names} ergänzt. Das Spiel hängt an; prüfe, ob Trigger- oder Effect-Blöcke doppelt gesetzt werden.", extraUncertain: true);
            case OverrideBehavior.LocalizationKeys:
            {
                var replacing = all.Where(c => c.Operator == OverrideOperator.Replace).ToList();
                var winner = replacing.Count > 0 ? replacing[^1] : all[^1];
                return Make(CompatKind.KeyConflict, CompatSeverity.Conflict, $"Der Lokalisierungsschlüssel \"{group.Key}\" ist in {names} verschieden. Es gilt die Fassung von \"{winner.Participant.Name}\".", winner, CompatSuggestion.NeedsCompatch, extraUncertain: replacing.Count == 0);
            }

            default:
                return ClassifyPerKey(group, all, names, (kind, severity, message, winner, suggestion, extra) => Make(kind, severity, message, winner, suggestion, extra));
        }
    }

    private static CompatFinding ClassifyPerKey(KeyGroup group, List<CompatContribution> all, string names, Func<CompatKind, CompatSeverity, string, CompatContribution?, CompatSuggestion, bool, CompatFinding> make)
    {
        var replaces = all.Where(c => new OverrideKey(c.Operator, string.Empty).IsReplace).ToList();
        var injects = all.Where(c => new OverrideKey(c.Operator, string.Empty).IsInject).ToList();
        var plains = all.Where(c => c.Operator == OverrideOperator.None).ToList();

        if (replaces.Count > 0)
        {
            var winner = replaces[^1];
            if (replaces.Select(r => r.Participant.Id).Distinct().Count() > 1)
            {
                return make(CompatKind.KeyConflict, CompatSeverity.Conflict, $"Mehrere Mods ersetzen \"{group.Key}\" ({Names(replaces)}). Nur die Fassung von \"{winner.Participant.Name}\" gilt.", winner, CompatSuggestion.NeedsCompatch, false);
            }

            var lostInjects = injects.Where(i => i.Participant.Id != winner.Participant.Id).ToList();
            if (lostInjects.Count > 0)
            {
                return make(CompatKind.KeyLoss, CompatSeverity.Conflict, $"\"{winner.Participant.Name}\" ersetzt \"{group.Key}\" (REPLACE). Der INJECT-Beitrag von {Names(lostInjects)} geht verloren, weil REPLACE laut Wiki nach INJECT verarbeitet wird.", winner, CompatSuggestion.ReviewBothSides, false);
            }

            return make(CompatKind.KeyLoss, CompatSeverity.Hint, $"\"{winner.Participant.Name}\" ersetzt die Definition von \"{group.Key}\" aus {Names(plains)} vollständig (REPLACE).", winner, CompatSuggestion.None, false);
        }

        if (plains.Select(p => p.Participant.Id).Distinct().Count() > 1 && !AllSame(plains))
        {
            var winner = plains[^1];
            return make(CompatKind.KeyConflict, CompatSeverity.Conflict, $"{Names(plains)} definieren \"{group.Key}\" jeweils vollständig. Nur die Fassung von \"{winner.Participant.Name}\" gilt.", winner, CompatSuggestion.NeedsCompatch, false);
        }

        var clashes = OverlappingChildKeys(injects.Concat(plains).ToList());
        if (clashes.Count > 0)
        {
            return make(CompatKind.KeyConflict, CompatSeverity.Conflict, $"{names} setzen die Teilschlüssel {string.Join(", ", clashes)} von \"{group.Key}\" verschieden. Ob INJECT einen vorhandenen Teilschlüssel überschreibt, nennt das Wiki nicht.", null, CompatSuggestion.NeedsCompatch, true);
        }

        return make(CompatKind.KeyCombinable, CompatSeverity.Hint, $"{names} ändern \"{group.Key}\" an verschiedenen Teilschlüsseln. INJECT kombiniert sie.", null, CompatSuggestion.None, false);
    }

    private static string Names(IEnumerable<CompatContribution> contributions) =>
        string.Join(", ", contributions.Select(c => "\"" + c.Participant.Name + "\"").Distinct());

    private static bool SameContent(CompatContribution a, CompatContribution b) =>
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
