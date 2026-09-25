using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class GitHubNamesTests
{
    [Theory]
    [InlineData("flogi/idea-variation", true)]
    [InlineData("Org-1/Mod_Name.v2", true)]
    [InlineData("nurname", false)]
    [InlineData("a/b/c", false)]
    [InlineData("-a/b", false)]
    [InlineData("a/", false)]
    [InlineData("a/b c", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidRepo(string? text, bool expected) => Assert.Equal(expected, GitHubNames.IsValidRepo(text));

    [Fact]
    public void ExpandTag_ErsetztPlatzhalter_UndBereinigtDenModNamen()
    {
        Assert.Equal("v2.0.5", GitHubNames.ExpandTag("v{version}", "2.0.5", "Idea Variation 2"));
        Assert.Equal("Idea-Variation-2-2.0.5", GitHubNames.ExpandTag("{mod}-{version}", "2.0.5", "Idea Variation 2"));
        Assert.Equal("v1.0", GitHubNames.ExpandTag(null, "1.0", "X"));
    }

    [Theory]
    [InlineData("v1.0.0", true)]
    [InlineData("release/1.0", true)]
    [InlineData("v 1", false)]
    [InlineData("a..b", false)]
    [InlineData("-x", false)]
    [InlineData("x.lock", false)]
    [InlineData("x/", false)]
    [InlineData("a~b", false)]
    [InlineData("a:b", false)]
    [InlineData("", false)]
    public void IsValidTag(string tag, bool expected) => Assert.Equal(expected, GitHubNames.IsValidTag(tag));

    [Fact]
    public void ExpandFileName_ErsetztPlatzhalter_BereinigtUndHaengtZipAn()
    {
        Assert.Equal("Idea Variation 2-2.0.5.zip", GitHubNames.ExpandFileName("{mod}-{version}.zip", "Idea Variation 2", "2.0.5", "v2.0.5", "Standard"));
        Assert.Equal("Mod_v2.0.5_Voll.zip", GitHubNames.ExpandFileName("Mod_{tag}_{variant}", "Mod", "2.0.5", "v2.0.5", "Voll"));
        Assert.Equal("a_b.zip", GitHubNames.ExpandFileName("a/b.zip", "M", "1", "v1", "S"));
    }
}
