using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class LiveLogWatcherTests
{
    [Fact]
    public void PollDeliversOnlyNewLines()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: eins\n"));
        var path = tree.PathOf("logs/error.log");
        var chunks = new List<LogChunk>();
        using var watcher = new LiveLogWatcher(path, LogReader.ReadFrom(path, LogPosition.Start).Next, chunks.Add, startTimer: false);

        File.AppendAllText(path, "[10:00:01][a.cpp:1]: zwei\n[10:00:02][a.cpp:1]: drei\n");
        watcher.Poll();
        watcher.Poll();

        var chunk = Assert.Single(chunks);
        Assert.Equal(new[] { "zwei", "drei" }, chunk.Lines.Select(l => l.Message));
        Assert.Equal(new[] { 2, 3 }, chunk.Lines.Select(l => l.LineNumber));
    }

    [Fact]
    public void TickReadsOnlyAfterASignalOrTheFallbackTime()
    {
        using var tree = new TempTree(("logs/error.log", string.Empty));
        var path = tree.PathOf("logs/error.log");
        var now = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var chunks = new List<LogChunk>();
        using var watcher = new LiveLogWatcher(path, LogPosition.Start, chunks.Add, fallback: TimeSpan.FromSeconds(2), now: () => now, startTimer: false, watchFile: false);

        File.AppendAllText(path, "[10:00:00][a.cpp:1]: eins\n");
        watcher.Tick();
        Assert.Empty(chunks);

        watcher.Signal();
        watcher.Tick();
        Assert.Single(chunks);

        File.AppendAllText(path, "[10:00:01][a.cpp:1]: zwei\n");
        now = now.AddSeconds(3);
        watcher.Tick();
        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public void RapidWritesArriveAsOneChunk()
    {
        using var tree = new TempTree(("logs/error.log", string.Empty));
        var path = tree.PathOf("logs/error.log");
        var chunks = new List<LogChunk>();
        using var watcher = new LiveLogWatcher(path, LogPosition.Start, chunks.Add, startTimer: false);

        for (var i = 0; i < 5; i++)
        {
            File.AppendAllText(path, $"[10:00:0{i}][a.cpp:1]: zeile {i}\n");
            watcher.Signal();
        }

        watcher.Tick();

        Assert.Equal(5, Assert.Single(chunks).Lines.Count);
    }

    [Fact]
    public void RotationIsReportedAsRestart()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: alter Lauf mit langer Zeile\n"));
        var path = tree.PathOf("logs/error.log");
        var chunks = new List<LogChunk>();
        using var watcher = new LiveLogWatcher(path, LogReader.ReadFrom(path, LogPosition.Start).Next, chunks.Add, startTimer: false);

        File.WriteAllText(path, "[11:00:00][a.cpp:1]: neu\n");
        watcher.Poll();

        var chunk = Assert.Single(chunks);
        Assert.True(chunk.Restarted);
        Assert.Equal("neu", Assert.Single(chunk.Lines).Message);
    }

    private static LiveLogWatcher Watch(TempTree tree, List<LogChunk> chunks) =>
        new(tree.PathOf("logs/error.log"), LogReader.ReadFrom(tree.PathOf("logs/error.log"), LogPosition.Start).Next, chunks.Add, startTimer: false, watchFile: false);

    private static void Rotate(TempTree tree, string newContent)
    {
        File.Move(tree.PathOf("logs/error.log"), tree.PathOf("logs/error.1.log"));
        File.WriteAllText(tree.PathOf("logs/error.log"), newContent);
    }

    [Fact]
    public void ARotationWithoutGapContinuesTheRunAndKeepsTheUnreadTail()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: a\n"));
        var chunks = new List<LogChunk>();
        using var watcher = Watch(tree, chunks);

        File.AppendAllText(tree.PathOf("logs/error.log"), "[10:00:01][a.cpp:1]: b\n");
        Rotate(tree, "[10:00:02][a.cpp:1]: c\n");
        watcher.Poll();

        Assert.Equal(2, chunks.Count);
        Assert.Equal("b", Assert.Single(chunks[0].Lines).Message);
        Assert.Equal(0, chunks[0].Lines[0].Part);
        Assert.True(chunks[1].Continued);
        Assert.False(chunks[1].Restarted);
        var c = Assert.Single(chunks[1].Lines);
        Assert.Equal(("c", 1, 1), (c.Message, c.Part, c.LineNumber));
    }

    [Fact]
    public void TheDecisionWaitsForTheFirstLineOfAnEmptyNewFile()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: a\n"));
        var chunks = new List<LogChunk>();
        using var watcher = Watch(tree, chunks);

        Rotate(tree, string.Empty);
        watcher.Poll();
        Assert.DoesNotContain(chunks, c => c.Continued || c.Restarted);

        File.AppendAllText(tree.PathOf("logs/error.log"), "[10:00:01][a.cpp:1]: b\n");
        watcher.Poll();

        var decided = Assert.Single(chunks, c => c.Continued || c.Restarted);
        Assert.True(decided.Continued);
        Assert.Equal(1, Assert.Single(decided.Lines).Part);
    }

    [Fact]
    public void ARotationWithAGapIsANewRun()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: a\n"));
        var chunks = new List<LogChunk>();
        using var watcher = Watch(tree, chunks);

        Rotate(tree, "[10:02:00][a.cpp:1]: neu\n");
        watcher.Poll();

        var chunk = Assert.Single(chunks);
        Assert.True(chunk.Restarted);
        Assert.Equal(0, Assert.Single(chunk.Lines).Part);
    }

    [Fact]
    public void ARotationAcrossMidnightContinues()
    {
        using var tree = new TempTree(("logs/error.log", "[23:59:59][a.cpp:1]: a\n"));
        var chunks = new List<LogChunk>();
        using var watcher = Watch(tree, chunks);

        Rotate(tree, "[00:00:01][a.cpp:1]: b\n");
        watcher.Poll();

        Assert.True(Assert.Single(chunks).Continued);
    }

    private static LiveLogWatcher WatchWithDebugLog(TempTree tree, List<LogChunk> chunks) =>
        new(tree.PathOf("logs/error.log"), LogReader.ReadFrom(tree.PathOf("logs/error.log"), LogPosition.Start).Next, chunks.Add,
            startTimer: false, watchFile: false, runAnchorPath: tree.PathOf("logs/debug.log"));

    [Fact]
    public void AQuietSwitchContinuesWhileTheDebugLogGoesOn()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: a\n"), ("logs/debug.log", "[09:59:00][a.cpp:1]: start\n"));
        var chunks = new List<LogChunk>();
        using var watcher = WatchWithDebugLog(tree, chunks);

        File.AppendAllText(tree.PathOf("logs/debug.log"), "[10:04:00][a.cpp:1]: weiter\n");
        Rotate(tree, "[10:05:00][a.cpp:1]: b\n");
        watcher.Poll();

        Assert.True(Assert.Single(chunks).Continued);
    }

    [Fact]
    public void ASwitchIsANewRunWhenTheDebugLogWasRecreated()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: a\n"), ("logs/debug.log", "[09:59:00][a.cpp:1]: start\n"));
        var chunks = new List<LogChunk>();
        using var watcher = WatchWithDebugLog(tree, chunks);

        File.WriteAllText(tree.PathOf("logs/debug.log"), "[10:00:01][a.cpp:1]: neuer Start\n");
        Rotate(tree, "[10:00:01][a.cpp:1]: b\n");
        watcher.Poll();

        Assert.True(Assert.Single(chunks).Restarted);
    }

    [Fact]
    public void AForeignOlderFileIsNotReadAsTail()
    {
        using var tree = new TempTree(("logs/error.log", "[10:00:00][a.cpp:1]: a\n"));
        var chunks = new List<LogChunk>();
        using var watcher = Watch(tree, chunks);

        File.Delete(tree.PathOf("logs/error.log"));
        File.WriteAllText(tree.PathOf("logs/error.1.log"), "[10:00:01][a.cpp:1]: fremd, anderer Anfang\n[10:00:01][a.cpp:1]: x\n");
        File.WriteAllText(tree.PathOf("logs/error.log"), "[10:00:02][a.cpp:1]: c\n");
        watcher.Poll();

        Assert.DoesNotContain(chunks.SelectMany(c => c.Lines), l => l.Message == "x");
    }

    [Fact]
    public void RealWatcherAndTimerDeliverAppendedLines()
    {
        using var tree = new TempTree(("logs/error.log", string.Empty));
        var path = tree.PathOf("logs/error.log");
        using var arrived = new ManualResetEventSlim();
        var messages = new List<string>();
        using var watcher = new LiveLogWatcher(
            path,
            LogPosition.Start,
            chunk =>
            {
                lock (messages)
                {
                    messages.AddRange(chunk.Lines.Select(l => l.Message));
                }

                arrived.Set();
            },
            tick: TimeSpan.FromMilliseconds(50),
            fallback: TimeSpan.FromMilliseconds(200));

        File.AppendAllText(path, "[10:00:00][a.cpp:1]: live\n");

        Assert.True(arrived.Wait(TimeSpan.FromSeconds(5)));
        lock (messages)
        {
            Assert.Equal(new[] { "live" }, messages);
        }
    }

    [Fact]
    public void DisposeStopsDeliveryAndAMissingFolderIsHarmless()
    {
        using var tree = new TempTree(("logs/error.log", string.Empty));
        var path = tree.PathOf("logs/error.log");
        var chunks = new List<LogChunk>();
        var watcher = new LiveLogWatcher(path, LogPosition.Start, chunks.Add, startTimer: false);
        watcher.Dispose();

        File.AppendAllText(path, "[10:00:00][a.cpp:1]: eins\n");
        watcher.Poll();
        Assert.Empty(chunks);

        using var missing = new LiveLogWatcher(tree.PathOf("fehlt/error.log"), LogPosition.Start, chunks.Add, startTimer: false);
        missing.Poll();
        Assert.Empty(chunks);
    }
}
