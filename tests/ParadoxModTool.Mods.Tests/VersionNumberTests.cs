using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class VersionNumberTests
{
    [Theory]
    [InlineData("2.0.5")]
    [InlineData("1.0")]
    [InlineData("7")]
    [InlineData(" 1.2.0.1 ")]
    public void TryParse_AkzeptiertZahlenMitPunkten(string text)
    {
        Assert.True(VersionNumber.TryParse(text, out var version));
        Assert.Equal(text.Trim(), version.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1..2")]
    [InlineData("1.x")]
    [InlineData("v1.0")]
    public void TryParse_LehntAbWasKeineVersionIst(string? text)
    {
        Assert.False(VersionNumber.TryParse(text, out _));
    }

    [Fact]
    public void CompareTo_VergleichtZahlenwertNichtText()
    {
        Assert.True(Parse("2.0.10").CompareTo(Parse("2.0.9")) > 0);
        Assert.True(Parse("1.2.0.1").CompareTo(Parse("1.2.0")) > 0);
        Assert.True(Parse("1.0").CompareTo(Parse("1.0.1")) < 0);
        Assert.True(Parse("10.0").CompareTo(Parse("9.9.9")) > 0);
    }

    [Fact]
    public void Equals_FehlendeTeileZaehlenAlsNull()
    {
        Assert.Equal(Parse("1.0"), Parse("1.0.0"));
        Assert.Equal(Parse("1.0").GetHashCode(), Parse("1.0.0").GetHashCode());
        Assert.NotEqual(Parse("1.0"), Parse("1.0.1"));
    }

    private static VersionNumber Parse(string text)
    {
        Assert.True(VersionNumber.TryParse(text, out var version));
        return version;
    }
}
