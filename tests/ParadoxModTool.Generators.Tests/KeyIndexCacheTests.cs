using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Zwischenspeicher der Index-Teile je Ordner: laufende und fertige Aufgaben werden geteilt, abgebrochene und gescheiterte neu gebaut.</summary>
public sealed class KeyIndexCacheTests
{
    private static readonly LocalizationKeyIndex Empty = LocalizationKeyIndex.Build(Array.Empty<string>());

    [Fact]
    public async Task Running_and_finished_task_is_reused()
    {
        var gate = new TaskCompletionSource();
        var calls = 0;
        var cache = new KeyIndexCache((_, _) =>
        {
            Interlocked.Increment(ref calls);
            gate.Task.Wait();
            return Empty;
        });

        var first = cache.Get("game");
        var second = cache.Get("GAME");
        gate.SetResult();
        await first;

        Assert.Same(first, second);
        Assert.Same(first, cache.Get("game"));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Cancelled_build_is_not_cached()
    {
        var started = new TaskCompletionSource();
        var gate = new TaskCompletionSource();
        var calls = 0;
        var cache = new KeyIndexCache((_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.SetResult();
                gate.Task.Wait();
                ct.ThrowIfCancellationRequested();
            }

            return Empty;
        });
        using var cancellation = new CancellationTokenSource();

        var cancelled = cache.Get("game", cancellation.Token);
        await started.Task; // mitten im Bau abbrechen, nicht vor dem Start
        cancellation.Cancel();
        gate.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        var rebuilt = cache.Get("game");

        Assert.NotSame(cancelled, rebuilt);
        Assert.Same(Empty, await rebuilt);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Running_build_with_cancel_requested_is_not_reused()
    {
        var started = new TaskCompletionSource();
        var gate = new TaskCompletionSource();
        var calls = 0;
        var cache = new KeyIndexCache((_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.SetResult();
                gate.Task.Wait();
                ct.ThrowIfCancellationRequested();
            }

            return Empty;
        });
        using var cancellation = new CancellationTokenSource();

        var cancelled = cache.Get("game", cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        // Der abgebrochene Bau läuft noch (bis zur nächsten Prüfung); ein neues Berechnen darf ihn nicht übernehmen
        var rebuilt = cache.Get("game");
        gate.SetResult();

        Assert.NotSame(cancelled, rebuilt);
        Assert.Same(Empty, await rebuilt);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Finished_build_is_kept_when_its_token_is_cancelled_later()
    {
        var cache = new KeyIndexCache((_, _) => Empty);
        using var cancellation = new CancellationTokenSource();
        var finished = cache.Get("game", cancellation.Token);
        await finished;

        cancellation.Cancel();

        Assert.Same(finished, cache.Get("game"));
    }

    [Fact]
    public async Task Faulted_build_is_not_cached()
    {
        var calls = 0;
        var cache = new KeyIndexCache((_, _) => Interlocked.Increment(ref calls) == 1 ? throw new IOException("locked") : Empty);

        var faulted = cache.Get("game");
        await Assert.ThrowsAsync<IOException>(() => faulted);
        var rebuilt = cache.Get("game");

        Assert.NotSame(faulted, rebuilt);
        Assert.Same(Empty, await rebuilt);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Invalidate_drops_only_that_root()
    {
        var calls = new List<string>();
        var cache = new KeyIndexCache((root, _) =>
        {
            lock (calls)
            {
                calls.Add(root);
            }

            return Empty;
        });
        var game = cache.Get("game");
        var mod = cache.Get("mod");
        await Task.WhenAll(game, mod);

        cache.Invalidate("MOD");

        Assert.Same(game, cache.Get("game"));
        var rebuilt = cache.Get("mod");
        await rebuilt;
        Assert.NotSame(mod, rebuilt);
        Assert.Equal(2, calls.Count(r => r == "mod"));
        Assert.Single(calls, r => r == "game");
    }

    [Fact]
    public async Task Default_builder_reads_the_root()
    {
        using var game = new TempFolder();
        game.Write("in_game/localization/english/a_l_english.yml", "l_english:\n TWS_THE: \"x\"\n");

        var index = await new KeyIndexCache().Get(game.Root);

        Assert.Equal(new[] { "TWS_THE" }, index.Others("WE_PERFORM_iv2_ga_select_national_idea_army_30_3_ACTION_TITLE"));
    }
}
