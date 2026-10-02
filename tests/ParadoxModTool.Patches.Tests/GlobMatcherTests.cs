using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class GlobMatcherTests
{
    [Fact]
    public void Semicolon_separates_patterns_for_enumerate()
    {
        using var mod = new TempMod();
        mod.WriteMod("a/one.txt", "x");
        mod.WriteMod("a/two.txt", "x");
        mod.WriteMod("b/x.txt", "x");
        mod.WriteMod("b/y.txt", "x");

        Assert.Equal(new[] { "a/one.txt", "a/two.txt", "b/x.txt" }, GlobMatcher.Enumerate(mod.ModRoot, "a/*.txt; b/x.txt;;a/one.txt").ToArray());
    }

    [Fact]
    public void Semicolon_separates_patterns_for_match()
    {
        Assert.True(GlobMatcher.IsMatch("a/*.txt;b/x.txt", "b/x.txt"));
        Assert.True(GlobMatcher.IsMatch("a/*.txt;b/x.txt", "a/q.txt"));
        Assert.False(GlobMatcher.IsMatch("a/*.txt;b/x.txt", "b/y.txt"));
    }
}
