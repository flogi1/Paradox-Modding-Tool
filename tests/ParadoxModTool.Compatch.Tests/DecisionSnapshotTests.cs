using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Rückgängig für jede Entscheidung (Sicherung und Wiederherstellung je Block) und die Nachfrage vor dem Überschreiben.</summary>
public class DecisionSnapshotTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private CompatchSession Session()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\nmine = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\nREPLACE:mine = { cost = 2 time = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 time = 2 }\nREPLACE:mine = { cost = 3 time = 2 }\n");
        var session = new CompatchSession(Input(), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();
        return session;
    }

    private static string Snapshot(CompatchSession session) =>
        string.Join("\n", session.Actions.Select(a => $"{a.Id}:{a.Stage}:{session.IsExplicit(a.Id)}:{(session.State.Decisions.TryGetValue(a.Id, out var d) ? d.Kind + d.ManualText : "-")}"))
        + "\nBLOCKS " + string.Join(";", session.State.BlockTexts.Keys.OrderBy(k => k, StringComparer.Ordinal))
        + "\nRESULT " + string.Join(";", session.Run.Applied.Desired.OrderBy(d => d.Key.Id, StringComparer.Ordinal).Select(d => ActionText.Render(d.Value.Entries)));

    private static IEnumerable<string> Ids(CompatchSession session, string key) => session.Actions.Where(a => a.Address.Key == key).Select(a => a.Id);

    private static EntryAddress Address(CompatchSession session, string key) => session.Actions.First(a => a.Address.Key == key).Address;

    [Fact]
    public void Restore_brings_back_decisions_after_a_bulk_decision()
    {
        var session = Session();
        session.Decide(Ids(session, "farm").First(), new MergeDecision(DecisionKind.TakeB));
        var before = Snapshot(session);
        var saved = session.Capture(new[] { Address(session, "farm"), Address(session, "mine") });

        session.DecideMany(session.Actions.Select(a => a.Id), _ => new MergeDecision(DecisionKind.TakeA));
        Assert.NotEqual(before, Snapshot(session));

        session.Restore(saved);
        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void Restore_brings_back_a_block_text_and_removes_a_new_one()
    {
        var session = Session();
        var farm = Address(session, "farm");
        session.SetBlockText(farm, "farm = { cost = 9 }");
        var before = Snapshot(session);
        var saved = session.Capture(new[] { farm });

        session.Decide(Ids(session, "farm").First(), new MergeDecision(DecisionKind.TakeA));
        session.Restore(saved);
        Assert.Equal(before, Snapshot(session));

        var empty = session.Capture(new[] { Address(session, "mine") });
        var clean = Snapshot(session);
        session.SetBlockText(Address(session, "mine"), "mine = { cost = 7 }");
        session.Restore(empty);
        Assert.Equal(clean, Snapshot(session));
    }

    [Fact]
    public void Confirm_many_keeps_earlier_decisions_and_taken_block_texts()
    {
        var session = Session();
        var farm = Address(session, "farm");
        var mineId = Ids(session, "mine").First();
        session.SetBlockText(farm, "farm = { cost = 9 }");
        session.Decide(mineId, new MergeDecision(DecisionKind.TakeB));
        var farmBefore = ActionText.Render(session.Run.Applied.Desired[farm].Entries);

        session.Confirm(session.Actions.First(a => a.Address == farm).Id);
        session.ConfirmMany(session.Actions.Select(a => a.Id));

        Assert.True(session.HasBlockText(farm));
        Assert.Equal(farmBefore, ActionText.Render(session.Run.Applied.Desired[farm].Entries));
        Assert.Equal(DecisionKind.TakeB, session.EffectiveDecision(session.Actions.Single(a => a.Id == mineId)).Kind);
    }

    [Fact]
    public void Open_conflicts_lists_only_undecided_conflicts()
    {
        var session = Session();
        var conflicts = session.Actions.Where(a => a.Stage == MergeStage.Conflict).Select(a => a.Id).ToList();
        Assert.True(conflicts.Count >= 2);
        session.Decide(conflicts[0], new MergeDecision(DecisionKind.TakeA));

        var open = session.OpenConflicts(session.Actions.Select(a => a.Id));

        Assert.DoesNotContain(conflicts[0], open);
        Assert.Contains(conflicts[1], open);
        Assert.All(open, id => Assert.Equal(MergeStage.Conflict, session.Actions.Single(a => a.Id == id).Stage));
    }

    [Fact]
    public void Overwritten_lists_only_decided_places_that_would_change()
    {
        var session = Session();
        var farmIds = Ids(session, "farm").ToList();
        session.Decide(farmIds[0], new MergeDecision(DecisionKind.TakeB));
        session.Decide(farmIds[^1], new MergeDecision(DecisionKind.TakeA));
        session.SetBlockText(Address(session, "mine"), "mine = { cost = 7 }");

        var overwritten = session.Overwritten(session.Actions.Select(a => a.Id), _ => new MergeDecision(DecisionKind.TakeA));

        Assert.Contains(farmIds[0], overwritten);
        if (farmIds.Count > 1)
        {
            Assert.DoesNotContain(farmIds[^1], overwritten);
        }

        Assert.All(Ids(session, "mine"), id => Assert.Contains(id, overwritten));
    }
}
