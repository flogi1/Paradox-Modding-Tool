using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class CompatchMergerTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private List<MergeAction> Decide(bool euV = true)
    {
        var input = Input(euV);
        return CompatchMerger.Decide(EffectiveEntryResolver.Resolve(input), input);
    }

    private void TwoReplaces(string vanilla, string a, string b)
    {
        PutVanilla(Buildings + "00_x.txt", vanilla);
        PutA(Buildings + "a.txt", a);
        PutB(Buildings + "b.txt", b);
    }

    [Fact]
    public void Change_by_one_side_is_automatic()
    {
        TwoReplaces("farm = { cost = 1 time = 1 }\n", "REPLACE:farm = { cost = 2 time = 1 }\n", "REPLACE:farm = { cost = 1 time = 1 }\n");

        var action = Assert.Single(Decide());

        Assert.Equal(MergeStage.Automatic, action.Stage);
        Assert.Equal(MergeOrigin.A, action.Origin);
        Assert.Equal(DecisionKind.TakeA, action.DefaultDecision);
        Assert.Equal("farm.cost", action.DisplayPath);
    }

    [Fact]
    public void Different_changes_at_same_place_are_a_conflict_defaulting_to_the_winner()
    {
        TwoReplaces("farm = { cost = 1 }\n", "REPLACE:farm = { cost = 2 }\n", "REPLACE:farm = { cost = 3 }\n");

        var action = Assert.Single(Decide());

        Assert.Equal(MergeStage.Conflict, action.Stage);
        Assert.Null(action.Origin);
        Assert.Equal(DecisionKind.TakeB, action.DefaultDecision);
    }

    [Fact]
    public void Automatic_change_inside_logic_block_needs_review()
    {
        TwoReplaces("farm = { potential = { x = 1 } }\n", "REPLACE:farm = { potential = { x = 1 y = 1 } }\n", "REPLACE:farm = { potential = { x = 1 } }\n");

        var action = Assert.Single(Decide());

        Assert.Equal(MergeStage.Review, action.Stage);
        Assert.Contains("Logikblock", action.Note);
        Assert.Equal("farm.potential.y", action.DisplayPath);
    }

    [Fact]
    public void Same_change_on_both_sides_is_automatic_both()
    {
        TwoReplaces("farm = { cost = 1 }\n", "REPLACE:farm = { cost = 2 }\n", "REPLACE:farm = { cost = 2 }\n");

        var action = Assert.Single(Decide());

        Assert.Equal(MergeOrigin.Both, action.Origin);
        Assert.Equal(MergeStage.Automatic, action.Stage);
    }

    [Fact]
    public void Insert_by_A_and_edit_by_B_in_repeated_keys_are_two_automatic_actions()
    {
        const string Ifs = "if = { limit = { x = 1 } add = 1 }\nif = { limit = { x = 2 } add = 2 }";
        TwoReplaces(
            "farm = { " + Ifs + " }\n",
            "REPLACE:farm = { if = { limit = { x = 0 } add = 0 } " + Ifs + " }\n",
            "REPLACE:farm = { " + Ifs.Replace("add = 2", "add = 20") + " }\n");

        var actions = Decide();

        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.NotEqual(MergeStage.Conflict, a.Stage));
        Assert.Contains(actions, a => a.Origin == MergeOrigin.A);
        Assert.Contains(actions, a => a.Origin == MergeOrigin.B);
    }

    [Fact]
    public void Unreadable_same_path_becomes_whole_file_action()
    {
        PutA(Buildings + "x.txt", "farm = { cost = 2\n");
        PutB(Buildings + "x.txt", "farm = { cost = 3 }\n");

        var action = Assert.Single(Decide());

        Assert.Equal(AddressKind.WholeFile, action.Address.Kind);
        Assert.Equal(MergeStage.Conflict, action.Stage);
        Assert.Equal("nicht lesbar", action.Note);
    }

    [Fact]
    public void Ids_are_deterministic_and_unaffected_by_unrelated_insertions()
    {
        TwoReplaces("farm = { cost = 1 time = 1 }\n", "REPLACE:farm = { cost = 2 time = 1 }\n", "REPLACE:farm = { cost = 1 time = 5 }\n");
        var first = Decide().Select(a => a.Id).ToList();
        Assert.Equal(first, Decide().Select(a => a.Id).ToList());

        PutA(Buildings + "a.txt", "REPLACE:farm = { modifier = { m = 1 } cost = 2 time = 1 }\n");
        var second = Decide().Select(a => a.Id).ToList();
        Assert.Subset(second.ToHashSet(), first.ToHashSet());
        Assert.Equal(first.Count + 1, second.Count);
    }
}
