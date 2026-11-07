using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class DumpSettleTrackerTests
{
    private static readonly DateTime Start = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    [Fact]
    public void SettledReturnsOnlyFilesTouchedAfterStartAndQuietForSettle()
    {
        var tracker = new DumpSettleTracker(Start, Settle);
        tracker.Touched("a.csv", Start.AddSeconds(-5));
        tracker.Touched("b.csv", Start.AddSeconds(10));
        tracker.Touched("c.csv", Start.AddSeconds(11));

        var now = Start.AddSeconds(12);

        Assert.Equal(new[] { "b.csv" }, tracker.Settled(now));
        Assert.Equal(new[] { "c.csv" }, tracker.Settled(now.AddSeconds(1)));
    }

    [Fact]
    public void SettledReturnsEachPathOnce()
    {
        var tracker = new DumpSettleTracker(Start, Settle);
        tracker.Touched("a.csv", Start.AddSeconds(1));

        Assert.Single(tracker.Settled(Start.AddSeconds(5)));
        Assert.Empty(tracker.Settled(Start.AddSeconds(6)));

        tracker.Touched("A.CSV", Start.AddSeconds(7));
        Assert.Empty(tracker.Settled(Start.AddSeconds(8)));
        Assert.Single(tracker.Settled(Start.AddSeconds(9)));
    }

    [Fact]
    public void RepeatedTouchPostponesSettling()
    {
        var tracker = new DumpSettleTracker(Start, Settle);
        tracker.Touched("a.csv", Start.AddSeconds(1));
        tracker.Touched("a.csv", Start.AddSeconds(2));

        Assert.Empty(tracker.Settled(Start.AddSeconds(3)));
        Assert.Single(tracker.Settled(Start.AddSeconds(4)));
    }
}
