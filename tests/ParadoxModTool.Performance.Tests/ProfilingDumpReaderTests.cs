using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class ProfilingDumpReaderTests
{
    [Fact]
    public void ReadsRealCutDump()
    {
        var dump = ProfilingDumpReader.ReadFile(RepoFiles.Data("eu5/iv2_before.csv"));
        Assert.InRange(dump.Rows.Count, 200, 400);
        var row = dump.Find("on_actions (on_actions) @ common/on_action/location_pulses.txt:38")!;
        Assert.Equal("on_actions (on_actions)", row.Kind);
        Assert.Equal("common/on_action/location_pulses.txt", row.Path);
        Assert.Equal(38, row.Line);
        Assert.Equal(12, row.Calls);
        Assert.Empty(dump.Warnings);
    }

    [Fact]
    public void ReadsHeaderWithBomAndCrLf()
    {
        var text = "\uFEFFFile Location;Bottleneck Time;Call Count;Max Time;Min Time;Self Time;Total Time;Average Time (Inclusive);Average Time (Exclusive)\r\n" +
                   "trigger @ common/x.txt:5;0;203;0;0;0.5;0.75;0;0\r\n";
        var dump = ProfilingDumpReader.Read(new StringReader(text));
        var row = Assert.Single(dump.Rows);
        Assert.Equal(0.5, row.Self);
        Assert.Equal(0.75, row.Total);
    }

    [Fact]
    public void ToleratesMissingOptionalColumnsAndSkipsBadLines()
    {
        var text = "File Location;Call Count;Self Time;Total Time\n" +
                   "effect @ a.txt:1;3;1;2\n" +
                   "garbage line without at\n" +
                   "effect @ b.txt:x;1;1;1\n";
        var dump = ProfilingDumpReader.Read(new StringReader(text));
        Assert.Single(dump.Rows);
        Assert.Equal(2, dump.Warnings.Count);
    }

    [Fact]
    public void MissingRequiredColumnThrowsWithHeader()
    {
        var ex = Assert.Throws<ProfilingFormatException>(() => ProfilingDumpReader.Read(new StringReader("Name;Calls\nx;1\n")));
        Assert.Equal("Name;Calls", ex.Header);
    }

    [Fact]
    public void AverageOfZeroCallsIsZero() => Assert.Equal(0, new ProfilingRow("k", "effect", "a.txt", 1, 0, 0, 0).AverageTotal);
}
