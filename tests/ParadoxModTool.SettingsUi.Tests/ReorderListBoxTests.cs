using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace ParadoxModTool.SettingsUi.Tests;

public sealed class ReorderListBoxTests
{
    private static (ReorderListBox Box, List<string> Order, List<string> Commits) Build(params string[] ids)
    {
        var order = ids.ToList();
        var commits = new List<string>();
        ReorderListBox? box = null;
        box = new ReorderListBox(
            () => order,
            committed => { order = committed.ToList(); commits.Add(string.Join(",", committed)); box!.Rebuild(); },
            id => new ReorderRow(id, "L:" + id, IsSeparator: id.StartsWith("sep:")));
        return (box, order, commits);
    }

    [Fact]
    public void RowsFollowTheOrderAndEdgeArrowsAreDisabled() => Sta.Run(() =>
    {
        var (box, _, _) = Build("a", "b", "c");
        Assert.Equal(new[] { "a", "b", "c" }, box.CurrentIds);
        Assert.False(box.UpButton("a").IsEnabled);
        Assert.True(box.DownButton("a").IsEnabled);
        Assert.False(box.DownButton("c").IsEnabled);
    });

    [Fact]
    public void ArrowClicksSwapAndCommitOnce() => Sta.Run(() =>
    {
        var (box, _, commits) = Build("a", "b", "c");
        UiFind.Click(box.DownButton("a"));
        Assert.Equal(new[] { "b,a,c" }, commits);
        Assert.Equal(new[] { "b", "a", "c" }, box.CurrentIds);
        Assert.False(box.UpButton("b").IsEnabled);
        UiFind.Click(box.UpButton("b"));
        Assert.Single(commits);
    });

    [Fact]
    public void MoveNextToUsesTheReorderListRuleAndIgnoresBadIds() => Sta.Run(() =>
    {
        var (box, _, commits) = Build("a", "b", "c", "d");
        Assert.True(box.MoveNextTo("d", "b"));
        Assert.Equal(new[] { "a", "d", "b", "c" }, box.CurrentIds);
        Assert.False(box.MoveNextTo("a", "a"));
        Assert.False(box.MoveNextTo("a", "gone"));
        Assert.Single(commits);
    });

    [Fact]
    public void SeparatorRowsShowRemoveAndCallIt() => Sta.Run(() =>
    {
        var removed = new List<string>();
        var box = new ReorderListBox(() => new[] { "a", "sep:1" }, _ => { }, id => new ReorderRow(id, id, IsSeparator: id == "sep:1", Remove: id == "sep:1" ? () => removed.Add(id) : null));
        var remove = UiFind.All<Button>(box.Row("sep:1")).Single(b => UiFind.Label(b) == "✕");
        UiFind.Click(remove);
        Assert.Equal(new[] { "sep:1" }, removed);
        Assert.DoesNotContain(UiFind.All<Button>(box.Row("a")), b => UiFind.Label(b) == "✕");
    });

    [Theory]
    [InlineData(10, 300, -1)]
    [InlineData(150, 300, 0)]
    [InlineData(290, 300, 1)]
    [InlineData(24, 300, 0)]
    public void AutoScrollDirectionScrollsOnlyNearTheEdges(double y, double height, int expected) =>
        Assert.Equal(expected, ReorderListBox.AutoScrollDirection(y, height));

    [Fact]
    public void DropIsAcceptedOnlyForOwnIdsOnADifferentRow() => Sta.Run(() =>
    {
        var (box, _, commits) = Build("a", "b");
        Assert.False(box.CanAcceptDrop("x", "a"));   // fremde Kennung
        Assert.False(box.CanAcceptDrop("a", "a"));   // auf sich selbst
        Assert.True(box.CanAcceptDrop("a", "b"));
        Assert.Empty(commits);
    });

    [Fact]
    public void AltArrowKeysMoveTheRow() => Sta.Run(() =>
    {
        var (box, _, _) = Build("a", "b");
        Assert.True(box.HandleKey("b", Key.Up, ModifierKeys.Alt));
        Assert.Equal(new[] { "b", "a" }, box.CurrentIds);
        Assert.False(box.HandleKey("b", Key.Up, ModifierKeys.Alt));      // schon oben
        Assert.False(box.HandleKey("a", Key.Up, ModifierKeys.None));     // ohne Alt keine Aktion
    });

    [Fact]
    public void RowShowsTheIconBeforeTheLabel() => Sta.Run(() =>
    {
        var icon = new Border { Width = 16, Height = 16 };
        var box = new ReorderListBox(() => new[] { "a" }, _ => { }, id => new ReorderRow(id, "A", Icon: icon));
        var grid = (Grid)((Border)box.Row("a")).Child;
        Assert.Contains(icon, grid.Children.OfType<FrameworkElement>());
        Assert.True(Grid.GetColumn(icon) < Grid.GetColumn(grid.Children.OfType<TextBlock>().Single(t => t.Text == "A")));
    });
}
