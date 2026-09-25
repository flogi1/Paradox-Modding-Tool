using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Der gewünschte Zustand einer Adresse nach allen Entscheidungen.</summary>
public sealed class DesiredState
{
    public required EntryAddress Address { get; init; }

    public IReadOnlyList<ScriptEntry> Entries { get; init; } = Array.Empty<ScriptEntry>();

    /// <summary>Nur bei <see cref="AddressKind.WholeFile"/>: gewählte Seite.</summary>
    public MergeSide? FileChoice { get; init; }

    /// <summary>Konflikte dieser Adresse ohne Nutzerentscheidung (Vorauswahl = Gewinner ohne Patch).</summary>
    public IReadOnlyList<MergeAction> OpenConflicts { get; init; } = Array.Empty<MergeAction>();
}

public sealed record ApplyResult(IReadOnlyDictionary<EntryAddress, DesiredState> Desired, IReadOnlyList<string> Errors);

/// <summary>Wendet Entscheidungen an (fehlende = Vorauswahl). Adressen ohne Aktion fehlen im Ergebnis, dort sind A und B gleich.</summary>
public static class DecisionApplier
{
    public static ApplyResult Apply(ResolvedEntries resolved, IReadOnlyList<MergeAction> actions, IReadOnlyDictionary<string, MergeDecision>? decisions, CompatchInput input, IReadOnlyDictionary<EntryAddress, IReadOnlyList<ScriptEntry>>? overrides = null)
    {
        decisions ??= new Dictionary<string, MergeDecision>();
        var byId = actions.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var desired = new Dictionary<EntryAddress, DesiredState>();
        var errors = new List<string>();

        foreach (var group in actions.GroupBy(a => a.Address))
        {
            var info = resolved.Addresses[group.Key];
            var open = group.Where(a => (a.Stage == MergeStage.Conflict && !decisions.ContainsKey(a.Id)) || a.Stage == MergeStage.Outdated).ToList();
            if (group.Key.Kind == AddressKind.WholeFile)
            {
                var action = group.Single();
                var kind = decisions.TryGetValue(action.Id, out var chosen) && chosen.Kind != DecisionKind.Manual ? chosen.Kind : action.DefaultDecision;
                desired[group.Key] = new DesiredState
                {
                    Address = group.Key,
                    // Plan 4, Ruling 3: "Vanilla übernehmen" heißt bei Option 1 "Basis übernehmen" (Datei der Hauptmod).
                    FileChoice = kind switch { DecisionKind.TakeA => MergeSide.A, DecisionKind.TakeB => MergeSide.B, _ => MergeBasis.BaseSide(input) },
                    OpenConflicts = open,
                };
                continue;
            }

            if (overrides is not null && overrides.TryGetValue(group.Key, out var fixedEntries) && !group.Any(a => decisions.ContainsKey(a.Id)))
            {
                desired[group.Key] = new DesiredState { Address = group.Key, Entries = fixedEntries };
                continue;
            }

            var entries = MergeWalker.Merge(MergeBasis.Entries(info, input), info.A.Entries, info.B.Entries, input.LogicFieldKeys, point =>
            {
                var id = info.Address.Id + "::" + point.Id;
                var fallback = byId.TryGetValue(id, out var action) ? action.DefaultDecision : CompatchMerger.ActionFor(info, point, input).DefaultDecision;
                if (!decisions.TryGetValue(id, out var decision))
                {
                    return MergeChoice.Entries(point, fallback);
                }

                if (decision.Kind != DecisionKind.Manual)
                {
                    return MergeChoice.Entries(point, decision.Kind);
                }

                try
                {
                    return ScriptDocument.Parse(decision.ManualText ?? string.Empty).Entries;
                }
                catch (ScriptParseException ex)
                {
                    errors.Add($"{point.DisplayPath}: {ex.Message}");
                    return MergeChoice.Entries(point, fallback);
                }
            });
            desired[group.Key] = new DesiredState { Address = group.Key, Entries = entries, OpenConflicts = open };
        }

        return new ApplyResult(desired, errors);
    }
}
