using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class ActionTreeTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private static MergeAction Act(string file, string path, MergeStage stage = MergeStage.Automatic) => new()
    {
        Id = file + "::" + path,
        Address = EntryAddress.ForKey("x", path),
        Path = new[] { path },
        DisplayPath = path,
        Stage = stage,
        DefaultDecision = DecisionKind.TakeA,
        File = file,
    };

    private CompatchSession Session(bool bLoadsAfterA = true)
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 }\nonly_a = { x = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 }\n");
        PutA("in_game/gfx/x.txt", "a = 1\n");
        PutB("in_game/gfx/x.txt", "b = 1\n");
        PutVanilla("in_game/gfx/x.txt", "v = 1\n");
        var session = new CompatchSession(Input(bLoadsAfterA: bLoadsAfterA), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();
        return session;
    }

    [Fact]
    public void File_is_the_winning_file_without_patch()
    {
        var farm = Session().Actions.First(a => a.Address.Key == "farm");
        Assert.Equal(Buildings + "b.txt", farm.File);

        // Schlüssel ordnen im Spiel zuerst nach Dateiname: eine spätere Datei in A gewinnt, egal wie die Mods laden.
        Session();
        PutA(Buildings + "z.txt", "REPLACE:farm = { cost = 4 }\n");
        var session = new CompatchSession(Input(), new CompatchState { Target = PatchTarget.Compatch, TargetFolder = PatchDir }, null);
        session.Load();
        Assert.Equal(Buildings + "z.txt", session.Actions.First(a => a.Address.Key == "farm").File);
    }

    [Fact]
    public void Whole_file_action_uses_its_path()
    {
        Assert.Equal("in_game/gfx/x.txt", Session().Actions.Single(a => a.Address.Kind == AddressKind.WholeFile).File);
    }

    [Fact]
    public void File_falls_back_to_the_side_that_has_the_key()
    {
        var info = new AddressInfo
        {
            Address = EntryAddress.ForKey("common/x", "k"),
            Behavior = ParadoxModTool.ModCompat.OverrideBehavior.PerKey,
            Certain = true,
            A = new Effective { RelativePath = "Common/X/A_File.txt" },
        };

        Assert.Equal("common/x/a_file.txt", CompatchMerger.FileOf(info));
        Assert.Equal(MergeSide.A, CompatchMerger.FileSideOf(info));
        Assert.Equal("common/x", CompatchMerger.FileOf(new AddressInfo { Address = info.Address, Behavior = info.Behavior, Certain = true }));

        var winner = new AddressInfo
        {
            Address = info.Address,
            Behavior = info.Behavior,
            Certain = true,
            A = new Effective { RelativePath = "common/x/a.txt" },
            NoPatch = new Effective { RelativePath = "common/x/b.txt", Side = MergeSide.B },
        };
        Assert.Equal(MergeSide.B, CompatchMerger.FileSideOf(winner));
    }

    [Fact]
    public void Tree_nests_folders_before_files_and_counts()
    {
        var actions = new[]
        {
            Act("common/buildings/a.txt", "z", MergeStage.Conflict),
            Act("common/buildings/a.txt", "b"),
            Act("common/buildings/sub/b.txt", "q"),
            Act("events/e.txt", "e"),
            Act("common/c.txt", "c"),
        };

        var roots = ActionTree.Build(actions, a => a.Stage == MergeStage.Conflict, a => a.Stage == MergeStage.Conflict);

        Assert.Equal(new[] { "common", "events" }, roots.Select(r => r.Name));
        var common = roots[0];
        Assert.Equal(TreeNodeKind.Folder, common.Kind);
        Assert.Equal(4, common.Count);
        Assert.Equal(1, common.OpenConflicts);
        Assert.Equal(new[] { "buildings", "c.txt" }, common.Children.Select(c => c.Name));
        var buildings = common.Children[0];
        Assert.Equal(new[] { "sub", "a.txt" }, buildings.Children.Select(c => c.Name));
        var file = buildings.Children[1];
        Assert.Equal(TreeNodeKind.File, file.Kind);
        Assert.Equal("common/buildings/a.txt", file.Path);
        Assert.Equal(new[] { "b", "z" }, file.Children.Select(c => c.Action!.DisplayPath));
        Assert.All(file.Children, c => Assert.Equal(TreeNodeKind.Action, c.Kind));
    }

    [Fact]
    public void Tree_order_equals_action_order()
    {
        var actions = new[]
        {
            Act("common/a.txt", "x"),
            Act("common/b/x.txt", "y"),
            Act("common/b/x.txt", "a"),
            Act("events/e.txt", "e"),
        };

        static IEnumerable<MergeAction> Flatten(IEnumerable<ActionTreeNode> nodes) =>
            nodes.SelectMany(n => n.Kind == TreeNodeKind.Action ? new[] { n.Action! } : Flatten(n.Children));

        Assert.Equal(ActionOrder.Sort(actions), Flatten(ActionTree.Build(actions, _ => false, _ => false)));
    }
}
