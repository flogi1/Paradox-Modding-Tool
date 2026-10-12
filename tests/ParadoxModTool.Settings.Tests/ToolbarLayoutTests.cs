using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class ToolbarLayoutTests
{
    private static string[] Seq(string[] order, params string[] hidden) =>
        ToolbarLayout.VisibleSequence(order, id => !hidden.Contains(id)).ToArray();

    [Fact]
    public void HiddenItemsDropAndSeparatorsCollapse() =>
        Assert.Equal(new[] { "a", "sep:1", "c" }, Seq(new[] { "a", "sep:1", "b", "sep:2", "c" }, "b"));

    [Fact]
    public void SeparatorsAtBothEdgesAndTriplesVanish() =>
        Assert.Equal(new[] { "a", "sep:2", "b" }, Seq(new[] { "sep:1", "a", "sep:2", "sep:3", "sep:4", "b", "sep:5" }));

    [Fact]
    public void OnlySeparatorsGiveAnEmptySequence() =>
        Assert.Empty(Seq(new[] { "sep:1", "a", "sep:2" }, "a"));

    [Fact]
    public void DefaultOrderFullyVisibleKeepsAllFiveSeparators() =>
        Assert.Equal(ToolbarCatalog.DefaultOrder, Seq(ToolbarCatalog.DefaultOrder.ToArray()));
}
