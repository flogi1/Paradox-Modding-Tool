using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Eigener Text für einen ganzen Block (markierte Zeilen im Vergleichsfenster übernehmen), mit Rückgängig und Speichern im Arbeitsstand.</summary>
public class BlockTextTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private CompatchState State() => new() { Target = PatchTarget.Compatch, TargetFolder = PatchDir };

    private CompatchSession Session(CompatchState state)
    {
        var session = new CompatchSession(Input(), state, null);
        session.Load();
        return session;
    }

    private void FarmConflict()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 time = 2 }\n");
    }

    private static EntryAddress Farm(CompatchSession session) => session.Actions.First(a => a.Address.Key == "farm").Address;

    private static string Result(CompatchSession session, EntryAddress address) => ActionText.Render(session.Run.Applied.Desired[address].Entries);

    [Fact]
    public void Block_text_becomes_the_result_and_settles_the_block()
    {
        FarmConflict();
        var session = Session(State());
        var farm = Farm(session);
        Assert.True(session.OpenCount > 0);

        Assert.Null(session.SetBlockText(farm, "farm = {\n\tcost = 2\n\ttime = 2\n}"));

        Assert.Contains("cost = 2", Result(session, farm));
        Assert.Contains("time = 2", Result(session, farm));
        Assert.True(session.HasBlockText(farm));
        Assert.Equal(0, session.OpenCount);
        Assert.All(session.Actions.Where(a => a.Address == farm), a => Assert.Equal(MergeStage.Decided, a.Stage));
    }

    [Fact]
    public void Unreadable_block_text_is_rejected_and_changes_nothing()
    {
        FarmConflict();
        var session = Session(State());
        var farm = Farm(session);
        var before = Result(session, farm);

        Assert.NotNull(session.SetBlockText(farm, "farm = { cost = 2"));

        Assert.False(session.HasBlockText(farm));
        Assert.Equal(before, Result(session, farm));
    }

    [Fact]
    public void Removing_the_block_text_restores_the_previous_result()
    {
        FarmConflict();
        var session = Session(State());
        var farm = Farm(session);
        var before = Result(session, farm);
        var openBefore = session.OpenCount;
        session.SetBlockText(farm, "farm = { cost = 9 }");

        session.RemoveBlockText(farm);

        Assert.Equal(before, Result(session, farm));
        Assert.Equal(openBefore, session.OpenCount);
        Assert.False(session.HasBlockText(farm));
    }

    [Fact]
    public void Block_text_is_kept_in_the_state_and_used_after_reload()
    {
        FarmConflict();
        var state = State();
        var first = Session(state);
        first.SetBlockText(Farm(first), "farm = { cost = 9 }");

        var second = Session(state);

        Assert.Contains("cost = 9", Result(second, Farm(second)));
        Assert.Equal(0, second.OpenCount);
    }

    [Fact]
    public void A_later_single_decision_drops_the_block_text()
    {
        FarmConflict();
        var session = Session(State());
        var farm = Farm(session);
        session.SetBlockText(farm, "farm = { cost = 9 }");

        session.Decide(session.Actions.First(a => a.Address == farm).Id, new MergeDecision(DecisionKind.TakeA));

        Assert.False(session.HasBlockText(farm));
        Assert.DoesNotContain("cost = 9", Result(session, farm));
    }

    [Fact]
    public void Taking_lines_decides_only_the_changed_place()
    {
        // Nutzerbefund 2026-09-30 (CK III church_holding): eine per U übernommene Zeile erledigte den ganzen Block.
        FarmConflict();
        var session = Session(State());
        var farm = Farm(session);
        var cost = session.Actions.Single(a => a.Address == farm && a.DisplayPath.Contains("cost", StringComparison.Ordinal));
        var time = session.Actions.Single(a => a.Address == farm && a.DisplayPath.Contains("time", StringComparison.Ordinal));
        var openBefore = session.OpenCount;

        Assert.Null(session.TakeBlockLines(farm, "farm = {\n\tcost = 2\n\ttime = 2\n}"));

        Assert.False(session.HasBlockText(farm));
        Assert.True(session.IsExplicit(cost.Id));
        Assert.Equal(DecisionKind.TakeA, session.EffectiveDecision(cost).Kind);
        Assert.False(session.IsExplicit(time.Id));
        Assert.Equal(openBefore - 1, session.OpenCount);
        Assert.Contains("cost = 2", Result(session, farm));
    }

    [Fact]
    public void Later_decision_keeps_the_taken_lines()
    {
        FarmConflict();
        var session = Session(State());
        var farm = Farm(session);
        session.TakeBlockLines(farm, "farm = {\n\tcost = 2\n\ttime = 2\n}");
        var time = session.Actions.Single(a => a.Address == farm && a.DisplayPath.Contains("time", StringComparison.Ordinal));

        session.Decide(time.Id, new MergeDecision(DecisionKind.TakeVanilla));

        Assert.Contains("cost = 2", Result(session, farm));
        Assert.Contains("time = 1", Result(session, farm));
    }

    [Fact]
    public void Change_without_matching_place_falls_back_to_block_text()
    {
        FarmConflict();
        var session = Session(State());
        var farm = Farm(session);

        Assert.Null(session.TakeBlockLines(farm, "farm = {\n\tcost = 3\n\ttime = 2\n\textra = yes\n}"));

        Assert.True(session.HasBlockText(farm));
        Assert.Contains("extra = yes", Result(session, farm));
    }
}
