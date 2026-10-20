using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class RunDurationTests
{
    private const string MonthlyRow = "on_actions (on_actions) @ common/on_action/location_pulses.txt:38";

    private static ProfilingDump Dump(string text) => ProfilingDumpReader.Read(new StringReader(text));

    [Fact]
    public void RealDumpGivesTwelveMonths()
    {
        var dump = ProfilingDumpReader.ReadFile(RepoFiles.Data("eu5/iv2_before.csv"));
        var duration = RunDuration.From(dump, MonthlyRow);
        Assert.Equal(12, duration.Months);
        Assert.True(duration.RowFound);
        Assert.Equal(MonthlyRow, duration.MonthlyPulseRow);
    }

    [Fact]
    public void ReportsWhatItCountedEvenIfItLooksLikeTwoRuns()
    {
        var dump = Dump("File Location;Call Count;Self Time;Total Time\n" + MonthlyRow + ";24;1;2\n");
        var duration = RunDuration.From(dump, MonthlyRow);
        Assert.Equal(24, duration.Months);
        Assert.True(duration.RowFound);
    }

    [Fact]
    public void MissingRowGivesNullMonths()
    {
        var dump = Dump("File Location;Call Count;Self Time;Total Time\neffect @ a.txt:1;3;1;2\n");
        var duration = RunDuration.From(dump, MonthlyRow);
        Assert.Null(duration.Months);
        Assert.False(duration.RowFound);
        Assert.Equal(MonthlyRow, duration.MonthlyPulseRow);
    }

    [Fact]
    public void NullKeyGivesNullMonths()
    {
        var dump = ProfilingDumpReader.ReadFile(RepoFiles.Data("eu5/iv2_before.csv"));
        var duration = RunDuration.From(dump, null);
        Assert.Null(duration.Months);
        Assert.False(duration.RowFound);
        Assert.Equal("", duration.MonthlyPulseRow);
    }

    [Theory]
    [InlineData("ON_ACTIONS (on_actions) @ common/on_action/location_pulses.txt:38")]
    [InlineData("on_actions (on_actions) @ common/on_action/location_pulses.txt:38 ")]
    [InlineData(" on_actions (on_actions) @ common/on_action/location_pulses.txt:38")]
    public void KeyMustMatchExactly(string key)
    {
        var dump = ProfilingDumpReader.ReadFile(RepoFiles.Data("eu5/iv2_before.csv"));
        var duration = RunDuration.From(dump, key);
        Assert.Null(duration.Months);
        Assert.False(duration.RowFound);
        Assert.Equal(key, duration.MonthlyPulseRow);
    }
}
