using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class CompatchSessionTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    /// <summary>
    /// farm: cost ist ein Konflikt (A 2, B 3), time kommt aus B, size aus A (braucht einen Patch, weil B ohne Patch gewinnt).
    /// mine: nur A ändert, wirkt ohnehin ("nicht nötig").
    /// </summary>
    private void ConflictCase(int sizeInA = 5)
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 size = 1 }\nmine = { x = 1 }\n");
        PutA(Buildings + "a.txt", $"REPLACE:farm = {{ cost = 2 time = 1 size = {sizeInA} }}\nREPLACE:mine = {{ x = 2 }}\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 time = 2 size = 1 }\n");
    }

    private static MergeAction FarmFromA(CompatchSession session) =>
        session.Actions.Single(a => a.Origin == MergeOrigin.A && a.Address.Key == "farm");

    private CompatchState State() => new() { Target = PatchTarget.Compatch, TargetFolder = PatchDir };

    private CompatchSession Session(CompatchState state, CompatchBase? baseline = null)
    {
        var session = new CompatchSession(Input(), state, baseline);
        session.Load();
        return session;
    }

    [Fact]
    public void Stored_decision_with_same_hash_is_decided_other_hash_is_outdated()
    {
        ConflictCase();
        var first = Session(State());
        var conflict = first.Actions.Single(a => a.Stage == MergeStage.Conflict);
        var state = State();
        state.Decisions[conflict.Id] = new StoredDecision(DecisionKind.TakeA, null, conflict.ValueHash);

        Assert.Equal(MergeStage.Decided, Session(state).Actions.Single(a => a.Id == conflict.Id).Stage);

        state.Decisions[conflict.Id] = new StoredDecision(DecisionKind.TakeA, null, "anders");
        var outdated = Session(state);
        Assert.Equal(MergeStage.Outdated, outdated.Actions.Single(a => a.Id == conflict.Id).Stage);
        Assert.Equal(DecisionKind.TakeA, outdated.EffectiveDecision(outdated.Actions.Single(a => a.Id == conflict.Id)).Kind);
        Assert.NotNull(outdated.WriteBlockerFor(cautious: true));
        Assert.Contains(outdated.Run.Plan.Entries, e => e.Text.Contains("# PMT offen"));

        outdated.Confirm(conflict.Id);
        Assert.Equal(MergeStage.Decided, outdated.Actions.Single(a => a.Id == conflict.Id).Stage);
        Assert.DoesNotContain(outdated.Run.Plan.Entries, e => e.Text.Contains("# PMT offen"));
    }

    [Fact]
    public void Decide_undo_and_confirm_update_plan_and_explicit_set()
    {
        ConflictCase();
        var session = Session(State());
        var conflict = session.Actions.Single(a => a.Stage == MergeStage.Conflict);
        var fromA = FarmFromA(session);
        Assert.Equal(MergeStage.Automatic, fromA.Stage);
        Assert.Equal(1, session.OpenCount);

        session.Decide(conflict.Id, new MergeDecision(DecisionKind.TakeA));
        Assert.True(session.IsExplicit(conflict.Id));
        Assert.Equal(0, session.OpenCount);
        Assert.Contains(session.Run.Plan.Entries, e => e.Text.Contains("cost = 2"));

        session.Undo(fromA.Id);
        Assert.Equal(DecisionKind.TakeB, session.EffectiveDecision(fromA).Kind);

        session.ResetDecision(conflict.Id);
        Assert.False(session.HasDecision(conflict.Id));
        Assert.Equal(1, session.OpenCount);
    }

    [Fact]
    public void Cautious_blocks_until_all_open_actions_are_explicit()
    {
        ConflictCase();
        var session = Session(State());
        Assert.Null(session.WriteBlockerFor(cautious: false));
        Assert.NotNull(session.WriteBlockerFor(cautious: true));

        foreach (var action in session.Actions.Where(a => a.Stage is MergeStage.Conflict or MergeStage.Review))
        {
            session.Confirm(action.Id);
        }

        Assert.Null(session.WriteBlockerFor(cautious: true));
    }

    [Fact]
    public void Baseline_makes_unchanged_automatic_actions_decided_and_recheck_restores()
    {
        ConflictCase();
        var first = Session(State());
        var baseline = first.CaptureBase(first.PrepareWrite(Array.Empty<string>(), _ => "utf-8-bom"), "1", "2");

        var second = Session(State(), baseline);
        Assert.Equal(MergeStage.Decided, FarmFromA(second).Stage);
        Assert.Equal(MergeStage.Conflict, second.Actions.Single(a => a.Origin is null).Stage);

        second.RecheckAll();
        Assert.Equal(MergeStage.Automatic, FarmFromA(second).Stage);

        ConflictCase(sizeInA: 6);
        var third = Session(State(), baseline);
        Assert.Equal(MergeStage.Automatic, FarmFromA(third).Stage);
    }

    [Fact]
    public void Existing_patch_without_baseline_is_preselected_from_patch()
    {
        ConflictCase();
        PutPatch(Buildings + "handmade.txt", "REPLACE:farm = { cost = 9 time = 9 }\n");

        var session = Session(State());

        var farmActions = session.Actions.Where(a => a.Address.Key == "farm").ToList();
        Assert.All(farmActions, a => Assert.Equal(MergeStage.FromExistingPatch, a.Stage));
        var farm = session.Run.Applied.Desired[farmActions[0].Address];
        Assert.Contains("cost = 9", Text(farm.Entries));
        Assert.Contains("cost = 9", Text(session.ExistingPatchEntries(farmActions[0].Address)));

        var decided = farmActions.First(a => a.Origin is null);
        session.Decide(decided.Id, new MergeDecision(DecisionKind.TakeA));
        Assert.DoesNotContain("cost = 9", Text(session.Run.Applied.Desired[farmActions[0].Address].Entries));
        Assert.All(farmActions.Where(a => a.Id != decided.Id), a => Assert.Equal(MergeStage.Review, a.Stage));
        Assert.NotNull(session.WriteBlockerFor(cautious: true));
    }

    [Fact]
    public void Seen_and_forms_are_kept_in_state()
    {
        ConflictCase();
        var state = State();
        var session = Session(state);
        var action = session.Actions[0];

        session.SetSeen(action.Id, true);
        session.SetForm(action.Address, PatchForm.WholeFile);

        Assert.Contains(action.Id, state.Seen);
        Assert.Equal(1, session.SeenCount);
        Assert.Equal(PatchForm.WholeFile, state.Forms[action.Address.Id]);
        Assert.Equal(PatchForm.WholeFile, session.FormOf(action.Address));
    }

    [Fact]
    public void Take_other_side_swaps_a_and_b_and_leaves_vanilla_for_the_side_opposite_the_default()
    {
        ConflictCase();
        var session = Session(State());
        var conflict = session.Actions.Single(a => a.Stage == MergeStage.Conflict);
        var start = session.EffectiveDecision(conflict).Kind;

        session.TakeOtherSide(conflict.Id);
        Assert.NotEqual(start, session.EffectiveDecision(conflict).Kind);
        Assert.True(session.IsExplicit(conflict.Id));

        session.TakeOtherSide(conflict.Id);
        Assert.Equal(start, session.EffectiveDecision(conflict).Kind);

        session.Decide(conflict.Id, new MergeDecision(DecisionKind.TakeVanilla));
        session.TakeOtherSide(conflict.Id);
        var expected = conflict.DefaultDecision == DecisionKind.TakeA ? DecisionKind.TakeB : DecisionKind.TakeA;
        Assert.Equal(expected, session.EffectiveDecision(conflict).Kind);
    }
}
