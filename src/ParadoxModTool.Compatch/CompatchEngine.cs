using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

public sealed record SkipSummary(SkipReason Reason, string Kind, int Count);

/// <summary>Ergebnis eines Laufs. <see cref="Applied"/> und <see cref="Plan"/> ändern sich bei <see cref="CompatchEngine.Replan"/>.</summary>
public sealed class CompatchRun
{
    public required ResolvedEntries Resolved { get; init; }

    public required List<MergeAction> Actions { get; init; }

    public ApplyResult Applied { get; internal set; } = new(new Dictionary<EntryAddress, DesiredState>(), Array.Empty<string>());

    public PatchPlan Plan { get; internal set; } = new();

    /// <summary>Nicht gelesene Dateien je Grund und Art, jeder Pfad einmal gezählt.</summary>
    public IReadOnlyList<SkipSummary> Skipped => Resolved.Skipped
        .GroupBy(s => (s.Reason, s.Kind))
        .Select(g => new SkipSummary(g.Key.Reason, g.Key.Kind, g.Select(s => s.RelativePath.Replace('\\', '/').ToLowerInvariant()).Distinct().Count()))
        .OrderBy(s => s.Reason)
        .ThenBy(s => s.Kind, StringComparer.Ordinal)
        .ToList();
}

/// <summary>Verbindet die vier Schritte. Liest nur, schreibt nie.</summary>
public static class CompatchEngine
{
    public static CompatchRun Run(
        CompatchInput input,
        PatchTarget target,
        IReadOnlyDictionary<string, MergeDecision>? decisions = null,
        IReadOnlyDictionary<EntryAddress, PatchForm>? forms = null,
        IProgress<CompatProgress>? progress = null,
        CancellationToken ct = default)
    {
        var resolved = EffectiveEntryResolver.Resolve(input, progress, ct);
        ct.ThrowIfCancellationRequested();
        progress?.Report(new CompatProgress(CompatchTexts.ProgressCompare, 0, 0));
        var run = new CompatchRun { Resolved = resolved, Actions = CompatchMerger.Decide(resolved, input) };
        ct.ThrowIfCancellationRequested();
        Replan(run, input, target, decisions, forms);
        return run;
    }

    /// <summary>Wendet Entscheidungen und Formwahl neu an. Automatische Aktionen wechseln dabei zwischen "automatisch" und "nicht nötig".</summary>
    public static void Replan(CompatchRun run, CompatchInput input, PatchTarget target, IReadOnlyDictionary<string, MergeDecision>? decisions, IReadOnlyDictionary<EntryAddress, PatchForm>? forms, IReadOnlyDictionary<EntryAddress, IReadOnlyList<ScriptEntry>>? overrides = null)
    {
        run.Applied = DecisionApplier.Apply(run.Resolved, run.Actions, decisions, input, overrides);
        run.Plan = PatchPlanner.Plan(run.Resolved, run.Applied, input, target, forms);
        foreach (var action in run.Actions.Where(a => a.Stage is MergeStage.Automatic or MergeStage.NotNeeded))
        {
            action.Stage = run.Plan.NotNeeded.Contains(action.Address) ? MergeStage.NotNeeded : MergeStage.Automatic;
        }
    }
}
