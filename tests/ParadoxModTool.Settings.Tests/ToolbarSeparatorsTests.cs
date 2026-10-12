using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class ToolbarSeparatorsTests
{
    private static ToolbarGroup[] SeparatorsWith(params string[] hidden) =>
        ToolbarSeparators.VisibleBefore(id => !hidden.Contains(id)).OrderBy(group => group).ToArray();

    private static string[] Group(ToolbarGroup group) => ToolbarCatalog.All.Where(item => item.Group == group).Select(item => item.Id).ToArray();

    private static string[] AllExcept(ToolbarGroup group) => ToolbarCatalog.All.Where(item => item.Group != group).Select(item => item.Id).ToArray();

    [Fact]
    public void AllVisibleShowsFourSeparators() =>
        Assert.Equal(new[] { ToolbarGroup.History, ToolbarGroup.Tools, ToolbarGroup.Toggles, ToolbarGroup.Launch }, SeparatorsWith());

    [Fact]
    public void EmptyMiddleGroupLeavesExactlyOneSeparator() =>
        Assert.Equal(new[] { ToolbarGroup.History, ToolbarGroup.Tools, ToolbarGroup.Launch }, SeparatorsWith(Group(ToolbarGroup.Toggles)));

    [Fact]
    public void EmptyFirstGroupHidesTheSeparatorAfterIt() =>
        Assert.Equal(new[] { ToolbarGroup.Tools, ToolbarGroup.Toggles, ToolbarGroup.Launch }, SeparatorsWith(Group(ToolbarGroup.Files)));

    [Fact]
    public void EmptyLastGroupHidesItsSeparator() =>
        Assert.Equal(new[] { ToolbarGroup.History, ToolbarGroup.Tools, ToolbarGroup.Toggles }, SeparatorsWith(Group(ToolbarGroup.Launch)));

    [Fact]
    public void OnlyOneVisibleGroupShowsNoSeparator() => Assert.Empty(SeparatorsWith(AllExcept(ToolbarGroup.Launch)));

    [Fact]
    public void NothingVisibleShowsNoSeparator() => Assert.Empty(SeparatorsWith(ToolbarCatalog.All.Select(item => item.Id).ToArray()));

    [Fact]
    public void OneVisibleItemKeepsItsGroup()
    {
        var hidden = Group(ToolbarGroup.Tools).Where(id => id != ToolbarItemIds.Bug).ToArray();
        Assert.Equal(new[] { ToolbarGroup.History, ToolbarGroup.Tools, ToolbarGroup.Toggles, ToolbarGroup.Launch }, SeparatorsWith(hidden));
    }
}
