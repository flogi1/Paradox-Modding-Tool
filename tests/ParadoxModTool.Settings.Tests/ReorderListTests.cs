using Xunit;

namespace ParadoxModTool.Settings.Tests;

public sealed class ReorderListTests
{
    [Fact]
    public void MoveFromBehindLandsBeforeTheTarget()
    {
        var o = new List<string> { "a", "b", "c", "d" };

        Assert.True(ReorderList.MoveNextTo(o, "d", "b"));
        Assert.Equal(new[] { "a", "d", "b", "c" }, o);
    }

    [Fact]
    public void MoveFromFrontLandsAfterTheTarget()
    {
        var o = new List<string> { "a", "b", "c", "d" };

        Assert.True(ReorderList.MoveNextTo(o, "a", "c"));
        Assert.Equal(new[] { "b", "c", "a", "d" }, o);
    }

    [Theory]
    [InlineData("b", "b")]
    [InlineData("x", "b")]
    [InlineData("b", "x")]
    public void MoveWithSameOrUnknownIdsChangesNothing(string id, string target)
    {
        var o = new List<string> { "a", "b", "c" };

        Assert.False(ReorderList.MoveNextTo(o, id, target));
        Assert.Equal(new[] { "a", "b", "c" }, o);
    }

    [Fact]
    public void SwapExchangesNeighboursAndRejectsBadIndexes()
    {
        var o = new List<string> { "a", "b", "c" };

        Assert.True(ReorderList.Swap(o, 0, 1));
        Assert.Equal(new[] { "b", "a", "c" }, o);
        Assert.False(ReorderList.Swap(o, 2, 3));
        Assert.False(ReorderList.Swap(o, 1, 1));
    }

    [Fact]
    public void CompleteKeepsOrderDropsUnknownAndDuplicatesAppendsMissingKeepsExtras()
    {
        var catalog = new[] { "a", "b", "c", "d" };

        var result = ReorderList.Complete(new[] { "c", "sep:1", "x", "a", "c", "sep:1" }, catalog, id => id.StartsWith("sep:", StringComparison.Ordinal));

        Assert.Equal(new[] { "c", "sep:1", "a", "b", "d" }, result);
    }

    [Fact]
    public void CompleteWithNullReturnsTheCatalog() =>
        Assert.Equal(new[] { "a", "b" }, ReorderList.Complete(null, new[] { "a", "b" }));
}
