using ParadoxModTool.ErrorLog;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class FlagTrackerSessionTests : IDisposable
{
    private static readonly GameProfile Ck3 = GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "ck3.json"));
    private static readonly FlagCommandTable Table = FlagCommandTable.Ck3(Array.Empty<string>());

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-flagsession-" + Guid.NewGuid().ToString("N"));
    private readonly string _vanilla;
    private readonly string _mod;
    private readonly FlagIndexStore _store;

    public FlagTrackerSessionTests()
    {
        _vanilla = Path.Combine(_temp, "v");
        _mod = Path.Combine(_temp, "m");
        Directory.CreateDirectory(_vanilla);
        Directory.CreateDirectory(_mod);
        _store = new FlagIndexStore(Path.Combine(_temp, "store"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, true);
        }
        catch (IOException)
        {
        }
    }

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private FlagSourceSet Sources(string modDirectory) =>
        FlagSourceSet.ForPlayset(Ck3, new[] { new ModSource("My Mod", modDirectory, false, 0) }, modDirectory, new[] { _vanilla });

    private FlagTrackerRequest Request(string? modDirectory = null, params string[] notes) =>
        new(Sources(modDirectory ?? _mod), Table, _store, notes);

    private void WriteFixture()
    {
        Write(_vanilla, "common/scripted_triggers/t.txt", "has_a_trigger = {\n\thas_variable = a\n}");
        Write(_mod, "events/e.txt", "e.1 = {\n\timmediate = {\n\t\tset_variable = a\n\t}\n}");
    }

    private async Task<FlagTrackerSession> ReadySession()
    {
        WriteFixture();
        var session = new FlagTrackerSession();
        await session.RefreshAsync(() => Request(), null, CancellationToken.None);
        Assert.Equal(FlagTrackerState.Ready, session.State);
        return session;
    }

    [Fact]
    public async Task EmptySearch_ShowsWarnings()
    {
        WriteFixture();
        Write(_mod, "events/lonely.txt", "lonely.1 = { immediate = { set_variable = lonely } }");
        var session = new FlagTrackerSession();

        await session.RefreshAsync(() => Request(), null, CancellationToken.None);

        foreach (var query in new[] { "", "   " })
        {
            var view = session.Search(query);
            Assert.Equal(new[] { "lonely" }, view.Overview.Select(r => r.Name));
            Assert.Equal(FlagTrackerTexts.NeverRead, view.Overview[0].Hint);
        }
    }

    [Fact]
    public async Task Refresh_SetsIndexAndStatus()
    {
        WriteFixture();
        var session = new FlagTrackerSession();
        var changed = 0;
        session.Changed += (_, _) => changed++;

        await session.RefreshAsync(() => Request(), null, CancellationToken.None);

        Assert.Equal(FlagTrackerState.Ready, session.State);
        Assert.NotNull(session.Index);
        Assert.True(session.IsPlayset);
        Assert.Equal(new[] { "a" }, session.Search("a").Overview.Select(r => r.Name));
        Assert.Equal(2, session.Index!.FileCount);
        Assert.Equal(FlagTrackerTexts.Status(session.Index.BuiltUtc.ToLocalTime(), 2, 0), session.StatusText);
        Assert.Contains("2", session.StatusText);
        Assert.Equal(2, changed);
    }

    [Fact]
    public void Search_BeforeIndex_Empty()
    {
        var session = new FlagTrackerSession();

        Assert.Same(FlagTrackerView.Empty, session.Search("x"));
        Assert.Equal(FlagTrackerState.Empty, session.State);
        Assert.Equal(FlagTrackerTexts.StatusNoIndex, session.StatusText);
        Assert.Null(session.Index);
    }

    [Fact]
    public async Task NewerRefreshWins()
    {
        var modA = Path.Combine(_temp, "ma");
        var modB = Path.Combine(_temp, "mb");
        Write(modA, "events/a.txt", "a.1 = { immediate = { set_variable = from_a } }");
        Write(modB, "events/b.txt", "b.1 = { immediate = { set_variable = from_b } }");
        var session = new FlagTrackerSession();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = session.RefreshAsync(() =>
        {
            started.SetResult();
            gate.Task.Wait();
            return Request(modA);
        }, null, CancellationToken.None);
        await started.Task;
        await session.RefreshAsync(() => Request(modB), null, CancellationToken.None);
        var indexB = session.Index;
        gate.SetResult();
        await first;

        Assert.NotNull(indexB);
        Assert.Same(indexB, session.Index);
        Assert.Equal(FlagTrackerState.Ready, session.State);
        Assert.Single(session.Search("from_b").Overview);
        Assert.Empty(session.Search("from_a").Overview);
    }

    [Fact]
    public async Task Failure_KeepsOldIndex()
    {
        var session = await ReadySession();
        var old = session.Index;

        await session.RefreshAsync(() => throw new IOException("x"), null, CancellationToken.None);

        Assert.Equal(FlagTrackerState.Failed, session.State);
        Assert.Equal(FlagTrackerTexts.StatusFailed("x"), session.StatusText);
        Assert.Contains("x", session.StatusText);
        Assert.Same(old, session.Index);
    }

    [Fact]
    public async Task StaleFailingRefresh_DoesNotOverwriteNewerResult()
    {
        await AssertStaleRunIgnored(() => throw new IOException("stale"));
    }

    [Fact]
    public async Task StaleCancelledRefresh_DoesNotOverwriteNewerResult()
    {
        await AssertStaleRunIgnored(() => throw new OperationCanceledException());
    }

    /// <summary>Lauf 1 blockiert und endet danach mit <paramref name="ending"/>; Lauf 2 gelingt vorher. Lauf 1 darf Lauf 2 nicht überschreiben.</summary>
    private async Task AssertStaleRunIgnored(Action ending)
    {
        WriteFixture();
        var session = new FlagTrackerSession();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = session.RefreshAsync(() =>
        {
            started.SetResult();
            gate.Task.Wait();
            ending();
            return Request();
        }, null, CancellationToken.None);
        await started.Task;
        await session.RefreshAsync(() => Request(), null, CancellationToken.None);
        var index = session.Index;
        var status = session.StatusText;
        gate.SetResult();
        await first;

        Assert.NotNull(index);
        Assert.Equal(FlagTrackerState.Ready, session.State);
        Assert.Same(index, session.Index);
        Assert.Equal(status, session.StatusText);
    }

    [Fact]
    public async Task Cancel_KeepsOldIndex()
    {
        var session = await ReadySession();
        var old = session.Index;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await session.RefreshAsync(() => Request(), null, cts.Token);

        Assert.Same(old, session.Index);
        Assert.Equal(FlagTrackerState.Ready, session.State);
        Assert.Equal(FlagTrackerTexts.StatusCancelled, session.StatusText);
    }

    [Fact]
    public async Task Cancel_WithoutIndex_Empty()
    {
        var session = new FlagTrackerSession();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await session.RefreshAsync(() => Request(), null, cts.Token);

        Assert.Null(session.Index);
        Assert.Equal(FlagTrackerState.Empty, session.State);
        Assert.Equal(FlagTrackerTexts.StatusCancelledNoIndex, session.StatusText);
        Assert.NotEqual(FlagTrackerTexts.StatusCancelled, session.StatusText);
    }

    [Fact]
    public async Task Notes_AppendedToStatus()
    {
        WriteFixture();
        var session = new FlagTrackerSession();

        await session.RefreshAsync(() => Request(null, FlagTrackerTexts.NoActiveMod), null, CancellationToken.None);

        Assert.EndsWith("; " + FlagTrackerTexts.NoActiveMod, session.StatusText);
        Assert.StartsWith(FlagTrackerTexts.Status(session.Index!.BuiltUtc.ToLocalTime(), 2, 0), session.StatusText);
    }

    [Fact]
    public async Task Search_HideList_HidesWarningsOnlyForEmptyQuery()
    {
        WriteFixture();
        Write(_mod, "events/lonely.txt", "lonely.1 = { immediate = { set_variable = lonely_1 set_variable = lonely_2 set_variable = keep } }");
        var session = new FlagTrackerSession();
        await session.RefreshAsync(() => Request(), null, CancellationToken.None);
        var hide = FlagHideList.Load(Path.Combine(_temp, "hidden"), _mod, false);
        hide.Add("lonely_*");

        var warnings = session.Search("", hide);
        Assert.Equal(new[] { "keep" }, warnings.Overview.Select(r => r.Name));
        Assert.Equal(2, session.HiddenWarningCount(hide));
        Assert.Equal(-1, warnings.IndexOfName("lonely_1"));
        Assert.DoesNotContain(warnings.Sites, r => r.Site?.Name == "lonely_1");

        var shown = session.Search("", hide, showHidden: true);
        Assert.Equal(new[] { "keep", "lonely_1", "lonely_2" }, shown.Overview.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(shown.Overview.Where(r => r.Name.StartsWith("lonely", StringComparison.Ordinal)), r => Assert.Equal("nie gelesen (ausgeblendet)", r.Hint));

        var search = session.Search("lonely_*", hide);
        Assert.Equal(2, search.Overview.Count);
        Assert.All(search.Overview, r => Assert.True(r.Hidden));

        Assert.Equal(3, session.Search("").Overview.Count);
    }

    [Fact]
    public async Task Search_ThousandExactRulesOnTwentyThousandNames_IsFast()
    {
        // Prüfung der Laufzeit (Review I1): 1000 Regeln ohne * plus wenige Muster auf 20 000 Namen mit Warnung.
        WriteFixture();
        var lines = string.Join("\n", Enumerable.Range(0, 20000).Select(i => $"set_variable = n_{i:D5}"));
        Write(_mod, "events/many.txt", "many.1 = { immediate = {\n" + lines + "\n} }");
        var session = new FlagTrackerSession();
        await session.RefreshAsync(() => Request(), null, CancellationToken.None);
        var hide = FlagHideList.Load(Path.Combine(_temp, "hidden"), _mod, false);
        for (var i = 0; i < 1000; i++)
        {
            hide.Add($"n_{i * 7:D5}");
        }

        hide.Add("n_19*");
        hide.Add("other_*_x");

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var view = session.Search("", hide);
        var count = session.HiddenWarningCount(hide);
        watch.Stop();

        Assert.Equal(5000, view.Overview.Count);
        Assert.True(view.Truncated);
        Assert.All(view.Overview, r => Assert.False(r.Hidden));
        Assert.Equal(2000, count);
        Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Search_HideList_HidesBeforeTheCap()
    {
        WriteFixture();
        var lines = string.Join("\n", Enumerable.Range(0, 5200).Select(i => $"set_variable = w_{i:D4}"));
        Write(_mod, "events/many.txt", "many.1 = { immediate = {\n" + lines + "\n} }");
        var session = new FlagTrackerSession();
        await session.RefreshAsync(() => Request(), null, CancellationToken.None);
        var hide = FlagHideList.Load(Path.Combine(_temp, "hidden"), _mod, false);
        hide.Add("w_0*");

        var view = session.Search("", hide);

        Assert.Equal(4200, view.Overview.Count);
        Assert.False(view.Truncated);
        Assert.Equal(1000, session.HiddenWarningCount(hide));
        Assert.True(session.Search("").Truncated);
    }

    [Fact]
    public async Task HiddenWarningCount_CountsHiddenWarnings()
    {
        var session = new FlagTrackerSession();
        var hide = FlagHideList.Load(Path.Combine(_temp, "hidden"), _mod, false);
        hide.Add("lonely_*");
        Assert.Equal(0, session.HiddenWarningCount(hide));

        WriteFixture();
        Write(_mod, "events/lonely.txt", "lonely.1 = { immediate = { set_variable = lonely_1 set_variable = lonely_2 set_variable = keep } }");
        await session.RefreshAsync(() => Request(), null, CancellationToken.None);

        Assert.Equal(2, session.HiddenWarningCount(hide));
        Assert.Equal(0, session.HiddenWarningCount(null));
    }
}
