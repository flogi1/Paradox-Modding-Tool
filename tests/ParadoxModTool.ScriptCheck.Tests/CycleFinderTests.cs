using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class CycleFinderTests
{
    [Fact]
    public void UnconditionalTwoCycle()
    {
        var group = Assert.Single(CycleFinder.Find(new[] { ("a", "b", false), ("b", "a", false) }));

        Assert.Equal(new[] { "a", "b" }, group.Members);
        Assert.True(group.Unconditional);
    }

    [Fact]
    public void ConditionalEdgeMakesCycleConditional()
    {
        var group = Assert.Single(CycleFinder.Find(new[] { ("a", "b", false), ("b", "a", true) }));

        Assert.Equal(new[] { "a", "b" }, group.Members);
        Assert.False(group.Unconditional);
    }

    [Fact]
    public void SelfLoopIsCycle()
    {
        var group = Assert.Single(CycleFinder.Find(new[] { ("a", "a", false), ("a", "b", false) }));

        Assert.Equal(new[] { "a" }, group.Members);
        Assert.True(group.Unconditional);
    }

    [Fact]
    public void ChainIsNoCycle()
    {
        Assert.Empty(CycleFinder.Find(new[] { ("a", "b", false), ("b", "c", true) }));
    }

    [Fact]
    public void UnconditionalInnerCycleMakesComponentUnconditional()
    {
        var edges = new[] { ("a", "b", false), ("b", "a", false), ("b", "c", true), ("c", "a", true), ("x", "y", true), ("y", "x", true) };

        var groups = CycleFinder.Find(edges);

        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { "a", "b", "c" }, groups[0].Members);
        Assert.True(groups[0].Unconditional);
        Assert.Equal(new[] { "x", "y" }, groups[1].Members);
        Assert.False(groups[1].Unconditional);
    }

    [Fact]
    public void LongChainDoesNotOverflow()
    {
        var edges = Enumerable.Range(0, 100_000).Select(i => ("n" + i, "n" + (i + 1), false)).Append(("n100000", "n0", false)).ToList();

        var group = Assert.Single(CycleFinder.Find(edges));

        Assert.Equal(100_001, group.Members.Count);
        Assert.True(group.Unconditional);
    }

    [Fact]
    public void ManyParallelEdgesDeduped()
    {
        var edges = Enumerable.Range(0, 1000).SelectMany(_ => new[] { ("a", "b", false), ("b", "a", false) }).ToList();

        var group = Assert.Single(CycleFinder.Find(edges));

        Assert.Equal(new[] { "a", "b" }, group.Members);
    }
}
