using ParadoxModTool.ReferenceTree;
using Xunit;

namespace ParadoxModTool.ReferenceTree.Tests;

public class ReferenceHistoryTests
{
    [Fact]
    public void Back_ReturnsPreviousName()
    {
        var h = new ReferenceHistory();
        h.Open("a");
        h.Open("b");
        Assert.True(h.CanGoBack);
        Assert.Equal("a", h.Back());
        Assert.Equal("a", h.Current);
        Assert.False(h.CanGoBack);
    }

    [Fact]
    public void Open_SameName_DoesNothing()
    {
        var h = new ReferenceHistory();
        h.Open("a");
        h.Open("a");
        Assert.False(h.CanGoBack);
        Assert.Equal("a", h.Current);
    }

    [Fact]
    public void Back_OnEmpty_ReturnsNull()
    {
        var h = new ReferenceHistory();
        Assert.Null(h.Back());
        Assert.Null(h.Current);
    }

    [Fact]
    public void Clear_EmptiesEverything()
    {
        var h = new ReferenceHistory();
        h.Open("a");
        h.Open("b");
        h.Clear();
        Assert.Null(h.Current);
        Assert.False(h.CanGoBack);
    }
}
