using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class GameVersionMatchTests
{
    [Theory]
    [InlineData("1.37.*", "v1.37.5.0", VersionSupport.Supported)]
    [InlineData("1.37.*", "1.38.0", VersionSupport.Outdated)]
    [InlineData("1.3", "1.3.2", VersionSupport.Supported)]
    [InlineData("1.3", "1.4", VersionSupport.Outdated)]
    [InlineData("1.*.*", "1.19.0.6", VersionSupport.Supported)]
    [InlineData("v1.37", "1.37.5", VersionSupport.Supported)]
    [InlineData("2.*", "1.37.5", VersionSupport.Outdated)]
    [InlineData("", "1.37.5", VersionSupport.Unknown)]
    [InlineData("1.37.*", null, VersionSupport.Unknown)]
    [InlineData("abc", "1.37.5", VersionSupport.Unknown)]
    public void Check_Cases(string? supported, string? installed, VersionSupport expected)
    {
        Assert.Equal(expected, GameVersionMatch.Check(supported, installed));
    }
}
