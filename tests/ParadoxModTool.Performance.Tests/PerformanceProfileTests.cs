using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class PerformanceProfileTests
{
    [Fact]
    public void Eu5ProfileCarriesProfilerFacts()
    {
        var p = RepoFiles.Profile("eu5").Performance!;
        Assert.Equal("Script.Profiling.Restart", p.RestartCommand);
        Assert.Equal("Script.Profiling.DumpAll", p.DumpCommand);
        Assert.Equal("profiling*.csv", p.DumpFilePattern);
        Assert.Equal("on_actions (on_actions) @ common/on_action/location_pulses.txt:38", p.MonthlyPulseRow);
        Assert.True(p.IsUsable);
    }

    [Theory]
    [InlineData("ck3")]
    [InlineData("vic3")]
    public void JominiSiblingsAreLockedUntilProbed(string id)
    {
        var p = RepoFiles.Profile(id).Performance!;
        Assert.False(p.IsUsable);
        Assert.NotEmpty(p.RestartCommand);
    }

    [Theory]
    [InlineData("eu4")]
    [InlineData("hoi4")]
    [InlineData("stellaris")]
    public void OtherGamesHaveNoProfilerBlock(string id) => Assert.Null(RepoFiles.Profile(id).Performance);
}
