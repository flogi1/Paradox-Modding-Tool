using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.GfxAudit.Tests;

public sealed class GfxAuditSessionTests : IDisposable
{
    private readonly GfxTestRoots _roots = new();
    private readonly GfxAuditStore _store;

    public GfxAuditSessionTests() => _store = new GfxAuditStore(Path.Combine(_roots.Temp, "store"));

    public void Dispose() => _roots.Dispose();

    private GfxAuditRequest Request(bool withActiveMod = true, Func<string, string>? readText = null, params string[] notes) =>
        new(_roots.Sources(withActiveMod: withActiveMod), GfxTestRoots.Rules(), _store, notes) { ReadText = readText };

    private void WriteFixture()
    {
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/old_thing.dds", "dds");
        GfxTestRoots.Write(_roots.Mod, "gfx/interface/other.dds", "dds");
    }

    private async Task<GfxAuditSession> ReadySession(params string[] notes)
    {
        WriteFixture();
        var session = new GfxAuditSession();
        await session.RefreshAsync(() => Request(true, null, notes), null, CancellationToken.None);
        Assert.Equal(GfxAuditState.Ready, session.State);
        return session;
    }

    [Fact]
    public async Task RefreshFillsResult()
    {
        var session = await ReadySession("Hinweis");

        Assert.NotNull(session.Index);
        Assert.Contains(session.Result!.Findings, f => f.Kind == GfxFindingKind.UnusedImage && f.Name == "gfx/interface/old_thing.dds");
        Assert.StartsWith(GfxAuditTexts.Status(session.Index!.Images.Count, session.Index.Files.Count, session.Index.BuiltUtc), session.StatusText);
        Assert.Contains("2 Bilder", session.StatusText);
        Assert.EndsWith("; Hinweis", session.StatusText);
        Assert.DoesNotContain("nicht lesbar", session.StatusText);
    }

    [Fact]
    public async Task RaisesChangedForBuildingAndReady()
    {
        WriteFixture();
        var session = new GfxAuditSession();
        var states = new List<GfxAuditState>();
        session.Changed += (_, _) => states.Add(session.State);

        await session.RefreshAsync(() => Request(), null, CancellationToken.None);

        Assert.Equal(new[] { GfxAuditState.Building, GfxAuditState.Ready }, states);
    }

    [Fact]
    public async Task NewerRunWins()
    {
        WriteFixture();
        var session = new GfxAuditSession();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = session.RefreshAsync(() =>
        {
            started.SetResult();
            gate.Task.Wait();
            return Request(false);
        }, null, CancellationToken.None);
        await started.Task;
        await session.RefreshAsync(() => Request(), null, CancellationToken.None);
        var index = session.Index;
        var result = session.Result;
        gate.SetResult();
        await first;

        Assert.NotNull(index?.ModRoot);
        Assert.Same(index, session.Index);
        Assert.Same(result, session.Result);
        Assert.Equal(GfxAuditState.Ready, session.State);
    }

    [Fact]
    public async Task CancelKeepsPreviousResult()
    {
        var session = await ReadySession();
        var old = session.Result;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await session.RefreshAsync(() => Request(), null, cts.Token);

        Assert.Equal(GfxAuditState.Ready, session.State);
        Assert.Same(old, session.Result);
        Assert.Equal(GfxAuditTexts.Cancelled, session.StatusText);
    }

    [Fact]
    public async Task CancelWithoutResultStaysEmpty()
    {
        var session = new GfxAuditSession();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await session.RefreshAsync(() => Request(), null, cts.Token);

        Assert.Equal(GfxAuditState.Empty, session.State);
        Assert.Null(session.Result);
    }

    [Fact]
    public async Task FailureKeepsOldResultAndSetsFailed()
    {
        var session = await ReadySession();
        var old = session.Result;

        await session.RefreshAsync(() => throw new IOException("boom"), null, CancellationToken.None);

        Assert.Equal(GfxAuditState.Failed, session.State);
        Assert.Same(old, session.Result);
        Assert.Equal(GfxAuditTexts.Failed("boom"), session.StatusText);
    }

    [Fact]
    public async Task UnreadFilesInStatus()
    {
        WriteFixture();
        var bad = GfxTestRoots.Write(_roots.Vanilla, "events/bad.txt", "picture = a\n");
        GfxTestRoots.Write(_roots.Vanilla, "events/e.txt", "picture = a\n");
        var session = new GfxAuditSession();

        await session.RefreshAsync(
            () => Request(true, p => string.Equals(p, bad, StringComparison.OrdinalIgnoreCase) ? throw new IOException("gesperrt") : File.ReadAllText(p), "Hinweis"),
            null,
            CancellationToken.None);

        Assert.Equal(GfxAuditState.Ready, session.State);
        Assert.Equal(new[] { bad }, session.Index!.UnreadFiles);
        Assert.Contains(GfxAuditTexts.Unread(1), session.StatusText);
        Assert.EndsWith("; " + GfxAuditTexts.Unread(1) + "; Hinweis", session.StatusText);
    }
}
