using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class DecisionApplierTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";
    private static readonly EntryAddress Farm = EntryAddress.ForKey("in_game/common/building_types", "farm");

    private (ResolvedEntries Resolved, List<MergeAction> Actions, CompatchInput Input) Prepare()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 3 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 5 time = 1 }\n");
        var input = Input();
        var resolved = EffectiveEntryResolver.Resolve(input);
        return (resolved, CompatchMerger.Decide(resolved, input), input);
    }

    [Fact]
    public void Defaults_take_automatic_changes_and_winner_on_conflict()
    {
        var (resolved, actions, input) = Prepare();

        var state = DecisionApplier.Apply(resolved, actions, null, input).Desired[Farm];

        Assert.Contains("cost = 5", Text(state.Entries));
        Assert.Contains("time = 3", Text(state.Entries));
        Assert.Single(state.OpenConflicts);
    }

    [Fact]
    public void Decision_changes_result_and_closes_conflict()
    {
        var (resolved, actions, input) = Prepare();
        var conflict = actions.Single(a => a.Stage == MergeStage.Conflict);

        var state = DecisionApplier.Apply(resolved, actions, new Dictionary<string, MergeDecision> { [conflict.Id] = new(DecisionKind.TakeA) }, input).Desired[Farm];

        Assert.Contains("cost = 2", Text(state.Entries));
        Assert.Empty(state.OpenConflicts);
    }

    [Fact]
    public void Undo_of_automatic_change_takes_vanilla()
    {
        var (resolved, actions, input) = Prepare();
        var automatic = actions.Single(a => a.Stage == MergeStage.Automatic);

        var state = DecisionApplier.Apply(resolved, actions, new Dictionary<string, MergeDecision> { [automatic.Id] = new(DecisionKind.TakeVanilla) }, input).Desired[Farm];

        Assert.Contains("time = 1", Text(state.Entries));
    }

    [Fact]
    public void Manual_text_replaces_the_unit()
    {
        var (resolved, actions, input) = Prepare();
        var conflict = actions.Single(a => a.Stage == MergeStage.Conflict);

        var state = DecisionApplier.Apply(resolved, actions, new Dictionary<string, MergeDecision> { [conflict.Id] = new(DecisionKind.Manual, "cost = 7") }, input).Desired[Farm];

        Assert.Contains("cost = 7", Text(state.Entries));
    }

    [Fact]
    public void Invalid_manual_text_is_reported_and_default_is_kept()
    {
        var (resolved, actions, input) = Prepare();
        var conflict = actions.Single(a => a.Stage == MergeStage.Conflict);

        var result = DecisionApplier.Apply(resolved, actions, new Dictionary<string, MergeDecision> { [conflict.Id] = new(DecisionKind.Manual, "cost = {") }, input);

        Assert.Single(result.Errors);
        Assert.Contains("cost = 5", Text(result.Desired[Farm].Entries));
    }
}
