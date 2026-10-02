using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class LogRunTests
{
    private static readonly DateTime Day = new(2026, 9, 26);

    private static LogFileTimes F(int index, string? first, string? last, DateTime? day = null) =>
        new(new LogRunFile($"error.{index}.log", index, DateTime.MinValue),
            first is null ? null : (day ?? Day) + TimeSpan.Parse(first),
            last is null ? null : (day ?? Day) + TimeSpan.Parse(last));

    [Fact]
    public void FilesWithoutGapFormOneRun()
    {
        var runs = LogRunChain.Group(new[] { F(0, "15:19:50", "15:19:52"), F(1, "15:15:28", "15:19:50"), F(2, "15:10:00", "15:15:27") });

        var run = Assert.Single(runs);
        Assert.Equal(new[] { 2, 1, 0 }, run.Files.Select(f => f.Index));
        Assert.Equal(Day + new TimeSpan(15, 10, 0), run.StartLocal);
        Assert.True(run.IsCurrent);
    }

    [Fact]
    public void AGapOfMoreThanTwoSecondsStartsANewRun()
    {
        var runs = LogRunChain.Group(new[] { F(0, "15:19:50", "15:19:52"), F(1, "15:15:28", "15:19:50"), F(2, "15:08:22", "15:13:40") });

        Assert.Equal(2, runs.Count);
        Assert.Equal(new[] { 1, 0 }, runs[0].Files.Select(f => f.Index));
        Assert.Equal(new[] { 2 }, runs[1].Files.Select(f => f.Index));
        Assert.False(runs[1].IsCurrent);
    }

    [Fact]
    public void TheChainCrossesMidnight()
    {
        var runs = LogRunChain.Group(new[] { F(0, "00:00:01", "00:10:00", Day.AddDays(1)), F(1, "23:00:00", "23:59:59") });

        Assert.Single(runs);
    }

    [Fact]
    public void AnEmptyErrorLogIsItsOwnRunAndEmptyOlderFilesJoinTheNewerNeighbour()
    {
        var runs = LogRunChain.Group(new[] { F(0, null, null), F(1, "15:15:28", "15:19:50"), F(2, null, null), F(3, "15:10:00", "15:15:28") });

        Assert.Equal(2, runs.Count);
        Assert.Equal(new[] { 0 }, runs[0].Files.Select(f => f.Index));
        Assert.Equal(new[] { 3, 2, 1 }, runs[1].Files.Select(f => f.Index));
    }

    [Fact]
    public void QuietPartsBelongToTheCurrentRunFromTheDebugLogStart()
    {
        var files = new[] { F(0, "15:30:00", "15:31:00"), F(1, "15:20:00", "15:25:00"), F(2, "15:10:00", "15:15:00"), F(3, "14:00:00", "14:30:00") };

        var runs = LogRunChain.Group(files, currentRunStart: Day + new TimeSpan(15, 5, 0));

        Assert.Equal(2, runs.Count);
        Assert.Equal(new[] { 2, 1, 0 }, runs[0].Files.Select(f => f.Index));
        Assert.Equal(new[] { 3 }, runs[1].Files.Select(f => f.Index));
    }

    [Fact]
    public void AnEmptyErrorLogRightAfterTheStartBelongsToTheCurrentRun()
    {
        var runs = LogRunChain.Group(new[] { F(0, null, null), F(1, "15:10:00", "15:15:00") }, currentRunStart: Day + new TimeSpan(15, 9, 0));

        Assert.Equal(new[] { 1, 0 }, Assert.Single(runs).Files.Select(f => f.Index));
    }

    [Fact]
    public void ARunReachingErrorFiveMayBeIncomplete()
    {
        var runs = LogRunChain.Group(new[] { F(0, "10:00:02", "10:00:03"), F(5, "10:00:00", "10:00:01"), F(1, "10:00:01", "10:00:02") });

        Assert.True(Assert.Single(runs).MaybeIncomplete);
    }
}
