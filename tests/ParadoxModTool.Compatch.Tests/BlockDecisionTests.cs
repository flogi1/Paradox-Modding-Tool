using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Sammelentscheidungen: ganzer Block, ganze Datei oder ganzer Ordner in einem Schritt.</summary>
public class BlockDecisionTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private CompatchSession Session()
    {
        var session = new CompatchSession(Input(), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();
        return session;
    }

    private static string Result(CompatchSession session, string key) =>
        ActionText.Render(session.Run.Applied.Desired.Single(d => d.Key.Key == key).Value.Entries);

    [Fact]
    public void Taking_A_for_all_actions_of_a_block_gives_the_block_of_A()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 1 time = 1 size = 3 }\n");
        var session = Session();
        var ids = session.Actions.Where(a => a.Address.Key == "farm").Select(a => a.Id).ToList();
        Assert.True(ids.Count >= 2);

        session.DecideMany(ids, _ => new MergeDecision(DecisionKind.TakeA));

        var result = Result(session, "farm");
        Assert.Contains("cost = 2", result);
        Assert.DoesNotContain("size", result);
        Assert.All(ids, id => Assert.True(session.IsExplicit(id)));
    }

    [Fact]
    public void Confirm_many_keeps_each_suggestion_and_marks_it_explicit()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 size = 1 }\nmine = { x = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 size = 5 }\nREPLACE:mine = { x = 2 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 time = 2 size = 1 }\n");
        var session = Session();
        var automatic = session.Actions.Where(a => a.Stage != MergeStage.Conflict).ToList();
        var openBefore = session.OpenCount;

        session.ConfirmMany(automatic.Select(a => a.Id));

        Assert.All(automatic, a =>
        {
            Assert.Equal(a.DefaultDecision, session.EffectiveDecision(a).Kind);
            Assert.True(session.IsExplicit(a.Id));
        });
        Assert.Equal(openBefore, session.OpenCount);
    }

    [Fact]
    public void Decide_many_with_no_ids_changes_nothing()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 }\n");
        var session = Session();
        var plan = session.Run.Plan;

        session.DecideMany(Array.Empty<string>(), _ => new MergeDecision(DecisionKind.TakeA));

        Assert.Same(plan, session.Run.Plan);
        Assert.Empty(session.State.Decisions);
    }
}
