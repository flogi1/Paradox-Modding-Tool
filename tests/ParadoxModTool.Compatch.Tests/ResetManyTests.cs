using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Sammel-Zurücksetzen einer Datei oder eines Ordners (Nutzerwunsch 2026-10-01): Entscheidungen und Blocktexte weg, Vorschläge wieder da.</summary>
public class ResetManyTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private readonly CompatchState _state = new() { Target = PatchTarget.Compatch };

    private CompatchSession Session()
    {
        _state.TargetFolder = PatchDir;
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\nmill = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\nREPLACE:mill = { cost = 2 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 time = 2 }\nREPLACE:mill = { cost = 3 }\n");
        var session = new CompatchSession(Input(), _state, null);
        session.Load();
        return session;
    }

    private static string Result(CompatchSession session, string key) =>
        ActionText.Render(session.Run.Applied.Desired[session.Actions.First(a => a.Address.Key == key).Address].Entries);

    [Fact]
    public void Reset_drops_decisions_and_block_texts_of_all_places()
    {
        var session = Session();
        var farm = session.Actions.First(a => a.Address.Key == "farm").Address;
        var proposed = session.Actions.ToDictionary(a => a.Id, a => a.Stage);
        var farmBefore = Result(session, "farm");
        var millBefore = Result(session, "mill");
        session.SetBlockText(farm, "farm = { cost = 9 time = 9 }");
        session.DecideMany(session.Actions.Where(a => a.Address.Key == "mill").Select(a => a.Id), _ => new MergeDecision(DecisionKind.TakeB));
        Assert.Contains("cost = 9", Result(session, "farm"));

        session.ResetMany(session.Actions.Select(a => a.Id));

        Assert.False(session.HasBlockText(farm));
        Assert.Empty(_state.Decisions);
        Assert.Empty(_state.BlockTexts);
        Assert.All(session.Actions, a => Assert.False(session.IsExplicit(a.Id)));
        Assert.All(session.Actions, a => Assert.Equal(proposed[a.Id], a.Stage));
        Assert.Equal(farmBefore, Result(session, "farm"));
        Assert.Equal(millBefore, Result(session, "mill"));
    }

    [Fact]
    public void Reset_of_some_places_keeps_the_others()
    {
        var session = Session();
        var all = session.Actions.Select(a => a.Id).ToList();
        session.DecideMany(all, _ => new MergeDecision(DecisionKind.TakeB));

        session.ResetMany(session.Actions.Where(a => a.Address.Key == "farm").Select(a => a.Id));

        Assert.All(session.Actions, a => Assert.Equal(a.Address.Key == "mill", _state.Decisions.ContainsKey(a.Id)));
    }
}
