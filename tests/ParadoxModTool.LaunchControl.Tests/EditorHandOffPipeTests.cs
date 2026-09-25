using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public sealed class EditorHandOffPipeTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("pmt-handoff-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static string NewGameId() => "test-" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task RequestReachesTheServer()
    {
        var game = NewGameId();
        using var stop = new CancellationTokenSource();
        var received = new TaskCompletionSource<EditorRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = EditorHandOffPipe.ServeAsync(game, request => received.TrySetResult(request), stop.Token);

        Assert.True(await EditorHandOffPipe.SendAsync(game, new EditorRequest(@"C:\Mods\Ärger++\a.gui", 12), TimeSpan.FromSeconds(5)));
        Assert.Equal(new EditorRequest(@"C:\Mods\Ärger++\a.gui", 12), await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        // Der Dienst nimmt weitere Anfragen an.
        var second = new TaskCompletionSource<EditorRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        received = second;
        Assert.True(await EditorHandOffPipe.SendAsync(game, new EditorRequest(@"C:\b.gui", null), TimeSpan.FromSeconds(5)));
        Assert.Equal(@"C:\b.gui", (await second.Task.WaitAsync(TimeSpan.FromSeconds(5))).Path);

        stop.Cancel();
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NobodyListeningGivesFalseAfterTheTimeout()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        Assert.False(await EditorHandOffPipe.SendAsync(NewGameId(), new EditorRequest(@"C:\a.gui", 1), TimeSpan.FromMilliseconds(400)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(4));
    }

    /// <summary>PMT wartet beim Start auf dem UI-Thread auf die Weitergabe; der pumpt dann nicht, also darf nichts auf ihn zurückwollen.</summary>
    [Fact]
    public void SendWorksWhileTheCallingThreadBlocks()
    {
        var game = NewGameId();
        using var stop = new CancellationTokenSource();
        var received = new TaskCompletionSource<EditorRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(() => EditorHandOffPipe.ServeAsync(game, request => received.TrySetResult(request), stop.Token));
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new NeverPumpingContext());
        try
        {
            var sent = EditorHandOffPipe.SendAsync(game, new EditorRequest(@"C:\a.gui", 1), TimeSpan.FromSeconds(5));
            Assert.True(sent.Wait(TimeSpan.FromSeconds(8)));
            Assert.True(sent.Result);

            var nobody = EditorHandOffPipe.SendAsync(NewGameId(), new EditorRequest(@"C:\a.gui", 1), TimeSpan.FromMilliseconds(400));
            Assert.True(nobody.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(nobody.Result);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.Equal(@"C:\a.gui", received.Task.Wait(TimeSpan.FromSeconds(5)) ? received.Task.Result.Path : null);
        stop.Cancel();
        Assert.True(server.Wait(TimeSpan.FromSeconds(5)));
    }

    private sealed class NeverPumpingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }

    [Fact]
    public async Task SendWaitsForAServerThatStartsLate()
    {
        var game = NewGameId();
        using var stop = new CancellationTokenSource();
        var received = new TaskCompletionSource<EditorRequest>(TaskCreationOptions.RunContinuationsAsynchronously);

        var send = EditorHandOffPipe.SendAsync(game, new EditorRequest(@"C:\late.gui", 2), TimeSpan.FromSeconds(5));
        await Task.Delay(700);
        var server = EditorHandOffPipe.ServeAsync(game, request => received.TrySetResult(request), stop.Token);

        Assert.True(await send);
        Assert.Equal(2, (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).Line);
        stop.Cancel();
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void LogAppendsAndKeepsOneOldGeneration()
    {
        var path = Path.Combine(_temp, "logs", "editor-handoff.log");
        var now = new DateTime(2026, 9, 25, 12, 0, 0);

        HandOffLog.Append(path, "erste Zeile", now);
        HandOffLog.Append(path, "zweite Zeile", now);
        Assert.Equal(new[] { "2026-09-25 12:00:00 erste Zeile", "2026-09-25 12:00:00 zweite Zeile" }, File.ReadAllLines(path));

        File.WriteAllText(path, new string('x', (int)HandOffLog.MaxBytes + 1));
        HandOffLog.Append(path, "neu", now);
        Assert.True(File.Exists(path + ".1"));
        Assert.Equal(new[] { "2026-09-25 12:00:00 neu" }, File.ReadAllLines(path));
    }
}
