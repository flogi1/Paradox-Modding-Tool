using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>Filter des Merge-Fensters: übernommene fehlende Blöcke, einfach ersetzte doppelte Dateien, Aktionen unter einem Baumknoten.</summary>
public class ActionFiltersTests : CompatchTestBase
{
    private const string Types = "in_game/common/building_types/";

    private CompatchRun Run(CompatchInput? input = null) => CompatchEngine.Run(input ?? Input(), PatchTarget.Compatch, null, null);

    private static MergeAction Act(string file, EntryAddress address, string path) => new()
    {
        Id = address.Id + "::" + path,
        Address = address,
        Path = new[] { path },
        DisplayPath = path,
        Stage = MergeStage.Automatic,
        DefaultDecision = DecisionKind.TakeA,
        File = file,
    };

    [Fact]
    public void Filled_missing_blocks_are_marked()
    {
        PutVanilla(Types + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Types + "00_x.txt", "farm = { cost = 1 }\nextra = { x = 1 }\n");
        PutB(Types + "00_x.txt", "farm = { cost = 2 }\n");
        var input = new CompatchInput
        {
            VanillaFolder = VanillaDir,
            ModAFolder = ADir,
            ModBFolder = BDir,
            Rules = EuVRules(),
            KeyOperators = EuVOps,
            ExcludeOwnPatchFilesOf = MergeSide.B,
            MainSide = MergeSide.A,
            FillMissingFromMain = true,
        };

        var run = Run(input);

        Assert.True(run.Actions.Single(a => a.Address.Key == "extra").FillsMissing);
        Assert.All(run.Actions.Where(a => a.Address.Key != "extra"), a => Assert.False(a.FillsMissing));
    }

    [Fact]
    public void File_in_both_mods_with_only_automatic_actions_is_replaced()
    {
        PutVanilla(Types + "00_x.txt", "farm = { cost = 1 }\nmine = { cost = 1 }\n");
        PutA(Types + "00_x.txt", "farm = { cost = 2 }\nmine = { cost = 1 }\n");
        PutB(Types + "00_x.txt", "farm = { cost = 1 }\nmine = { cost = 1 }\n");

        var run = Run();

        Assert.NotEmpty(run.Actions);
        Assert.Contains(Types + "00_x.txt", ActionFilters.ReplacedFiles(run.Actions, run.Resolved));
    }

    [Fact]
    public void File_with_a_conflict_is_not_replaced()
    {
        PutVanilla(Types + "00_x.txt", "farm = { cost = 1 }\nmine = { cost = 1 }\n");
        PutA(Types + "00_x.txt", "farm = { cost = 2 }\nmine = { cost = 1 }\n");
        PutB(Types + "00_x.txt", "farm = { cost = 3 }\nmine = { cost = 1 }\n");

        var run = Run();

        Assert.Contains(run.Actions, a => a.Stage == MergeStage.Conflict);
        Assert.Empty(ActionFilters.ReplacedFiles(run.Actions, run.Resolved));
    }

    [Fact]
    public void Different_file_names_are_never_replaced_files()
    {
        PutVanilla(Types + "00_x.txt", "farm = { cost = 1 }\nmine = { cost = 1 }\n");
        PutA(Types + "a.txt", "farm = { cost = 2 }\n");
        PutB(Types + "b.txt", "farm = { cost = 2 }\nmine = { cost = 2 }\n");

        var run = Run();

        Assert.Empty(ActionFilters.ReplacedFiles(run.Actions, run.Resolved));
    }

    [Fact]
    public void Under_selects_by_folder_file_and_block()
    {
        var farm = EntryAddress.ForKey("a", "farm");
        var actions = new[]
        {
            Act("a/x.txt", farm, "farm > cost"),
            Act("a/x.txt", farm, "farm > time"),
            Act("a/y.txt", EntryAddress.ForKey("a", "mine"), "mine > cost"),
            Act("b/z.txt", EntryAddress.ForKey("b", "farm"), "farm > cost"),
            Act("ab/w.txt", EntryAddress.ForKey("ab", "hut"), "hut > cost"),
        };

        Assert.Equal(3, ActionFilters.Under(actions, "a", TreeNodeKind.Folder).Count);
        Assert.Single(ActionFilters.Under(actions, "A/Y.txt", TreeNodeKind.File));
        Assert.Equal(2, ActionFilters.Under(actions, farm.Id, TreeNodeKind.Block).Count);
        Assert.Equal(new[] { actions[1] }, ActionFilters.Under(actions, actions[1].Id, TreeNodeKind.Action));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("building_types", true)]
    [InlineData("00_X.TXT", true)]
    [InlineData("farm", true)]
    [InlineData("> cost", true)]
    [InlineData(" cost ", true)]
    [InlineData("mine", false)]
    public void One_search_text_matches_file_path_place_and_key(string text, bool expected)
    {
        var action = Act(Types + "00_x.txt", EntryAddress.ForKey(Types + "00_x.txt", "farm"), "farm > cost");

        Assert.Equal(expected, ActionFilters.MatchesText(action, text));
    }
}
