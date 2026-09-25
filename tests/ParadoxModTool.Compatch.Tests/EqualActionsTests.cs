using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>"Für alle gleichen übernehmen": Stellen mit gleichen A/B-Werten und gleicher Herkunft gemeinsam entscheiden.</summary>
public class EqualActionsTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    /// <summary>A entfernt flag überall, B ändert es: farm und mine gleich (B "no"), quarry anders (B "maybe"). Drei Konflikte.</summary>
    private CompatchSession Session()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 flag = yes }\nmine = { cost = 1 flag = yes }\nquarry = { cost = 1 flag = yes }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 1 }\nREPLACE:mine = { cost = 1 }\nREPLACE:quarry = { cost = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 1 flag = no }\nREPLACE:mine = { cost = 1 flag = no }\nREPLACE:quarry = { cost = 1 flag = maybe }\n");
        var session = new CompatchSession(Input(), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();
        return session;
    }

    private static MergeAction Conflict(CompatchSession session, string key) =>
        session.Actions.Single(a => a.Stage == MergeStage.Conflict && a.Address.Key == key);

    [Fact]
    public void Equal_actions_share_values_and_origin_across_keys()
    {
        var session = Session();
        var farm = Conflict(session, "farm");

        Assert.Equal(new[] { "mine" }, session.EqualTo(farm.Id).Select(a => a.Address.Key));
        Assert.Single(session.EqualPending(farm.Id));
        Assert.Empty(session.EqualTo(Conflict(session, "quarry").Id));
    }

    [Fact]
    public void Apply_to_equal_copies_the_shown_decision_and_leaves_others()
    {
        var session = Session();
        var farm = Conflict(session, "farm");
        var mine = Conflict(session, "mine");
        var quarry = Conflict(session, "quarry");

        session.Decide(farm.Id, new MergeDecision(DecisionKind.TakeA));
        session.ApplyToEqual(farm.Id);

        Assert.Equal(DecisionKind.TakeA, session.EffectiveDecision(mine).Kind);
        Assert.True(session.IsExplicit(mine.Id));
        Assert.False(session.HasDecision(quarry.Id));
        Assert.Empty(session.EqualPending(farm.Id));
        Assert.Equal(1, session.OpenCount);
    }

    [Fact]
    public void Own_different_decisions_stay()
    {
        var session = Session();
        var farm = Conflict(session, "farm");
        var mine = Conflict(session, "mine");

        session.Decide(mine.Id, new MergeDecision(DecisionKind.TakeB));
        session.Decide(farm.Id, new MergeDecision(DecisionKind.TakeA));

        Assert.Empty(session.EqualPending(farm.Id));
        session.ApplyToEqual(farm.Id);
        Assert.Equal(DecisionKind.TakeB, session.EffectiveDecision(mine).Kind);
    }

    [Fact]
    public void Manual_text_is_copied_and_undecided_source_is_decided_too()
    {
        var session = Session();
        var farm = Conflict(session, "farm");
        var mine = Conflict(session, "mine");

        session.ApplyToEqual(farm.Id);
        Assert.True(session.IsExplicit(farm.Id));
        Assert.Equal(session.EffectiveDecision(farm).Kind, session.EffectiveDecision(mine).Kind);

        session.Decide(farm.Id, new MergeDecision(DecisionKind.Manual, "flag = sometimes"));
        session.ResetDecision(mine.Id);
        session.ApplyToEqual(farm.Id);
        var copied = session.EffectiveDecision(mine);
        Assert.Equal(DecisionKind.Manual, copied.Kind);
        Assert.Equal("flag = sometimes", copied.ManualText);
    }
}
