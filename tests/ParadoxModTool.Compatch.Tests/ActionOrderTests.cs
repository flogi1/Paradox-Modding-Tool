using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class ActionOrderTests
{
    private static MergeAction Act(string scope, string path, MergeStage stage) => new()
    {
        Id = scope + "|" + path,
        Address = EntryAddress.ForKey(scope, path),
        Path = new[] { path },
        DisplayPath = path,
        Stage = stage,
        DefaultDecision = DecisionKind.TakeA,
    };

    [Fact]
    public void Sort_follows_folder_structure_then_display_path()
    {
        MergeAction In(string file, string path, MergeStage stage) => new()
        {
            Id = file + "|" + path,
            Address = EntryAddress.ForKey("x", path),
            Path = new[] { path },
            DisplayPath = path,
            Stage = stage,
            DefaultDecision = DecisionKind.TakeA,
            File = file,
        };

        var top = In("common/a.txt", "k", MergeStage.Conflict);
        var subZ = In("common/b/x.txt", "z", MergeStage.Automatic);
        var subA = In("common/b/x.txt", "a", MergeStage.Review);
        var events = In("events/e.txt", "e", MergeStage.Conflict);

        Assert.Equal(new[] { subA, subZ, top, events }, ActionOrder.Sort(new[] { events, top, subZ, subA }));
    }

    [Fact]
    public void Step_moves_and_stops_at_edges()
    {
        var x = Act("s", "x", MergeStage.Conflict);
        var y = Act("s", "y", MergeStage.Conflict);
        var z = Act("s", "z", MergeStage.Conflict);
        var list = new[] { x, y, z };

        Assert.Same(z, ActionOrder.Step(list, y.Id, +1));
        Assert.Same(x, ActionOrder.Step(list, y.Id, -1));
        Assert.Null(ActionOrder.Step(list, z.Id, +1));
        Assert.Null(ActionOrder.Step(list, x.Id, -1));
        Assert.Same(x, ActionOrder.Step(list, null, +1));
        Assert.Same(z, ActionOrder.Step(list, "weg", -1));
        Assert.Null(ActionOrder.Step(Array.Empty<MergeAction>(), null, +1));
        Assert.Equal(1, ActionOrder.IndexOf(list, y.Id));
        Assert.Equal(-1, ActionOrder.IndexOf(list, "weg"));
    }

    [Fact]
    public void View_setting_parses_and_round_trips()
    {
        Assert.Equal(CompatchView.Actions, CompatchViews.Parse(null));
        Assert.Equal(CompatchView.Actions, CompatchViews.Parse("x"));
        Assert.Equal(CompatchView.Document, CompatchViews.Parse("B"));
        Assert.Equal(CompatchView.Cards, CompatchViews.Parse(" c "));
        foreach (var view in Enum.GetValues<CompatchView>())
        {
            Assert.Equal(view, CompatchViews.Parse(CompatchViews.ToSetting(view)));
        }
    }
}
