using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// Nach einer Entscheidung rechnet die Sitzung nur die betroffenen Blöcke neu (Leistung bei großen Mods). Jeder Test vergleicht das Ergebnis
/// mit einer vollen Neuberechnung: Wunschzustand aller Adressen, Fehler und Patch-Plan müssen gleich sein.
/// </summary>
public class IncrementalReplanTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private CompatchSession Session()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\nmine = { cost = 1 time = 1 }\nhut = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\nREPLACE:mine = { cost = 2 time = 1 }\nREPLACE:hut = { cost = 2 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 time = 2 }\nREPLACE:mine = { cost = 3 time = 2 }\nREPLACE:hut = { cost = 3 }\n");
        var session = new CompatchSession(Input(), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();
        return session;
    }

    private static string Snapshot(CompatchSession session) =>
        string.Join("\n", session.Run.Applied.Desired.OrderBy(d => d.Key.Id, StringComparer.Ordinal)
                .Select(d => d.Key.Id + " => " + ActionText.Render(d.Value.Entries) + " | open " + d.Value.OpenConflicts.Count))
        + "\nERR " + string.Join(";", session.Run.Applied.Errors)
        + "\nPLAN " + string.Join(";", session.Run.Plan.Entries.Select(e => e.Address.Id + "=" + e.Text))
        + "\nFILES " + string.Join(";", session.Run.Plan.Files.Select(f => f.RelativePath + "=" + f.Text))
        + "\nNOTNEEDED " + string.Join(";", session.Run.Plan.NotNeeded.Select(a => a.Id).OrderBy(i => i, StringComparer.Ordinal))
        + "\nSTAGES " + string.Join(";", session.Actions.Select(a => a.Id + ":" + a.Stage));

    private static void AssertSameAsFullReplan(CompatchSession session)
    {
        var incremental = Snapshot(session);
        session.Replan();
        Assert.Equal(Snapshot(session), incremental);
    }

    private static MergeAction First(CompatchSession session, string key) => session.Actions.First(a => a.Address.Key == key);

    [Fact]
    public void Single_decision_equals_full_replan()
    {
        var session = Session();
        session.Decide(First(session, "farm").Id, new MergeDecision(DecisionKind.TakeA));
        AssertSameAsFullReplan(session);
    }

    [Fact]
    public void Decide_many_and_confirm_many_equal_full_replan()
    {
        var session = Session();
        session.DecideMany(session.Actions.Where(a => a.Address.Key == "mine").Select(a => a.Id), _ => new MergeDecision(DecisionKind.TakeB));
        AssertSameAsFullReplan(session);
        session.ConfirmMany(session.Actions.Select(a => a.Id));
        AssertSameAsFullReplan(session);
    }

    [Fact]
    public void Block_text_set_and_removed_equals_full_replan()
    {
        var session = Session();
        var hut = First(session, "hut").Address;
        session.SetBlockText(hut, "hut = { cost = 9 }");
        AssertSameAsFullReplan(session);
        session.RemoveBlockText(hut);
        AssertSameAsFullReplan(session);
    }

    [Fact]
    public void Apply_to_equal_reset_and_undo_equal_full_replan()
    {
        var session = Session();
        var farm = First(session, "farm");
        session.Decide(farm.Id, new MergeDecision(DecisionKind.TakeA));
        session.ApplyToEqual(farm.Id);
        AssertSameAsFullReplan(session);
        session.ResetDecision(farm.Id);
        AssertSameAsFullReplan(session);
        session.Undo(First(session, "mine").Id);
        AssertSameAsFullReplan(session);
    }

    [Fact]
    public void Unreadable_manual_text_reports_the_same_error_as_full_replan()
    {
        var session = Session();
        session.Decide(First(session, "farm").Id, new MergeDecision(DecisionKind.Manual, "cost = {"));
        Assert.NotEmpty(session.Run.Applied.Errors);
        AssertSameAsFullReplan(session);
    }

    [Fact]
    public void Form_change_equals_full_replan()
    {
        var session = Session();
        var farm = First(session, "farm").Address;
        session.SetForm(farm, PatchForm.WholeFile);
        AssertSameAsFullReplan(session);
    }

    [Fact]
    public void Only_the_changed_block_is_merged_again()
    {
        var session = Session();
        var before = session.Run.Applied.Desired;
        var farm = First(session, "farm").Address;

        session.Decide(First(session, "farm").Id, new MergeDecision(DecisionKind.TakeA));

        var after = session.Run.Applied.Desired;
        Assert.NotSame(before[farm], after[farm]);
        Assert.All(after.Where(d => d.Key != farm), d => Assert.Same(before[d.Key], d.Value));
    }
}
