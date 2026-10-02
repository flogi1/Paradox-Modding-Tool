using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class GameVersionComparerTests
{
    [Theory]
    [InlineData("1.9", "1.10", -1)]
    [InlineData("v1.37.5.0", "1.38", -1)]
    [InlineData("v1.37.5.0 Inca", "1.37.5.0", 0)]
    [InlineData("v1.37.5.0-Inca", "1.37.5", 0)]
    [InlineData("1.3.11", "1.3.9", 1)]
    public void Compares_by_number(string a, string b, int sign) =>
        Assert.Equal(sign, Math.Sign(GameVersionComparer.Instance.Compare(a, b)));

    [Fact]
    public void Normalize_reads_the_first_number_run() =>
        Assert.Equal("1.37.5.0", GameVersionComparer.Normalize("v1.37.5.0-Inca"));

    [Fact]
    public void Text_without_number_sorts_first()
    {
        Assert.Null(GameVersionComparer.Normalize("Inca"));
        Assert.True(GameVersionComparer.Instance.Compare("Inca", "0.1") < 0);
    }

    [Theory]
    [InlineData("1.37.5.0", "1.37.*", true)]
    [InlineData("v1.37.5.0-Inca", "v1.37.*", true)]
    [InlineData("1.38.0", "1.37.*", false)]
    [InlineData("1.3.11", "1.*.*", true)]
    [InlineData("1.3.11", null, true)]
    [InlineData("1.37.5", "1.37.5", true)]
    [InlineData("1.37.5", "1.37.4", false)]
    public void Matches_wildcards(string version, string? pattern, bool expected) =>
        Assert.Equal(expected, GameVersionComparer.Matches(version, pattern));
}
