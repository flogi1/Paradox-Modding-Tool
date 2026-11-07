using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckSessionTests : IDisposable
{
    private static readonly IReadOnlySet<string> BuiltIns = new HashSet<string> { "add_gold" };

    private readonly ScriptCheckTestRoots _roots = new();
    private readonly ScriptCheckStore _store;

    public ScriptCheckSessionTests() => _store = new ScriptCheckStore(Path.Combine(_roots.Temp, "store"));

    public void Dispose() => _roots.Dispose();

    private ScriptCheckRequest Request(IReadOnlySet<string>? builtIns = null, bool withActiveMod = true, params string[] notes) =>
        new(_roots.Sources(withActiveMod: withActiveMod), ScriptCheckTestRoots.Rules(), _store, builtIns ?? BuiltIns, notes);

    private void WriteFixture()
    {
        ScriptCheckTestRoots.Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\ttrigger = { always = yes }\n\timmediate = { }\n}\nm.2 = {\n\ttrigger = { always = yes }\n\timmediate = { }\n}");
    }

    private async Task<ScriptCheckSession> ReadySession(params string[] notes)
    {
        WriteFixture();
        var session = new ScriptCheckSession();
        await session.RefreshAsync(() => Request(null, true, notes), null, CancellationToken.None);
        Assert.Equal(ScriptCheckState.Ready, session.State);
        return session;
    }

    [Fact]
    public async Task Refresh_SetsReadyAndFindings()
    {
        var session = await ReadySession("Hinweis");

        Assert.NotNull(session.Index);
        Assert.NotNull(session.Result);
        Assert.Contains(session.Result!.Findings, f => f.Kind == FindingKind.DeadEvent && f.Name == "m.1");
        Assert.Contains("Hinweis", session.StatusText);
        Assert.DoesNotContain(ScriptCheckTexts.NoBuiltIns, session.StatusText);
    }

    [Fact]
    public async Task Refresh_RaisesChangedForBuildingAndReady()
    {
        WriteFixture();
        var session = new ScriptCheckSession();
        var states = new List<ScriptCheckState>();
        session.Changed += (_, _) => states.Add(session.State);

        await session.RefreshAsync(() => Request(), null, CancellationToken.None);

        Assert.Equal(new[] { ScriptCheckState.Building, ScriptCheckState.Ready }, states);
    }

    [Fact]
    public async Task NoBuiltIns_AddsNoteToStatus()
    {
        WriteFixture();
        var session = new ScriptCheckSession();

        await session.RefreshAsync(() => Request(new HashSet<string>()), null, CancellationToken.None);

        Assert.False(session.Result!.BlockReferencesChecked);
        Assert.Contains(ScriptCheckTexts.NoBuiltIns, session.StatusText);
    }

    [Fact]
    public async Task NoActiveMod_AddsNoteToStatus()
    {
        WriteFixture();
        var session = new ScriptCheckSession();

        await session.RefreshAsync(() => Request(null, false), null, CancellationToken.None);

        Assert.Equal(ScriptCheckState.Ready, session.State);
        Assert.Empty(session.Result!.Findings);
        Assert.Contains(ScriptCheckTexts.NoActiveMod, session.StatusText);
    }

    [Fact]
    public async Task NewerRefreshWins()
    {
        WriteFixture();
        var session = new ScriptCheckSession();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = session.RefreshAsync(() =>
        {
            started.SetResult();
            gate.Task.Wait();
            return Request(null, false);
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
        Assert.Equal(ScriptCheckState.Ready, session.State);
    }

    [Fact]
    public async Task Failure_KeepsOldResultAndSetsFailed()
    {
        var session = await ReadySession();
        var old = session.Result;

        await session.RefreshAsync(() => throw new IOException("boom"), null, CancellationToken.None);

        Assert.Equal(ScriptCheckState.Failed, session.State);
        Assert.Same(old, session.Result);
        Assert.Equal(ScriptCheckTexts.StatusFailed("boom"), session.StatusText);
    }

    [Fact]
    public async Task Cancel_KeepsOldResultAndStaysReady()
    {
        var session = await ReadySession();
        var old = session.Result;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await session.RefreshAsync(() => Request(), null, cts.Token);

        Assert.Equal(ScriptCheckState.Ready, session.State);
        Assert.Same(old, session.Result);
        Assert.Equal(ScriptCheckTexts.StatusCancelled, session.StatusText);
    }

    [Fact]
    public async Task Visible_FiltersHiddenAndKind()
    {
        var session = await ReadySession();
        var hide = ScriptCheckHideList.Load(Path.Combine(_roots.Temp, "hide"), _roots.Mod);
        hide.Add("DeadEvent|m.1");

        var all = session.Visible(null, false, null);
        var visible = session.Visible(hide, false, null);
        var shown = session.Visible(hide, true, null);
        var onlyUnused = session.Visible(hide, false, FindingKind.UnusedBlock);

        Assert.Contains(all, f => f.Key == "DeadEvent|m.1");
        Assert.DoesNotContain(visible, f => f.Key == "DeadEvent|m.1");
        Assert.Contains(visible, f => f.Key == "DeadEvent|m.2");
        Assert.Equal(all.Count, shown.Count);
        Assert.All(onlyUnused, f => Assert.Equal(FindingKind.UnusedBlock, f.Kind));
        Assert.Empty(new ScriptCheckSession().Visible(null, false, null));
    }

    [Fact]
    public async Task StatusNoPerformanceRules()
    {
        WriteFixture();
        var without = new ScriptCheckSession();
        await without.RefreshAsync(() => RequestWithRules(RulesWithoutPerformance()), null, CancellationToken.None);
        var with = new ScriptCheckSession();
        await with.RefreshAsync(() => RequestWithRules(ScriptCheckTestRoots.Rules()), null, CancellationToken.None);

        Assert.Contains(ScriptCheckTexts.NoPerformanceRules, without.StatusText);
        Assert.DoesNotContain(ScriptCheckTexts.NoPerformanceRules, with.StatusText);
    }

    /// <summary>Vanilla-Puls ruft m.1, m.1 ruft einen Effekt mit globalem Iterator; m.2 ist tot.</summary>
    private void WritePerformanceFixture()
    {
        ScriptCheckTestRoots.Write(_roots.Vanilla, "common/on_action/v.txt", "yearly_global_pulse = {\n\tevents = { m.1 }\n}");
        ScriptCheckTestRoots.Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { m_eff = yes }\n}\nm.2 = {\n\timmediate = { }\n}");
        ScriptCheckTestRoots.Write(_roots.Mod, "common/scripted_effects/m.txt", "m_eff = {\n\tevery_living_character = { limit = { is_ai = no } }\n}");
    }

    [Fact]
    public async Task PerformanceOnlySessionHasOnlyPerformanceFindings()
    {
        WritePerformanceFixture();
        var full = new ScriptCheckSession();
        await full.RefreshAsync(() => Request(), null, CancellationToken.None);
        var perf = new ScriptCheckSession();
        var foreignStore = new ScriptCheckStore(Path.Combine(_roots.Temp, "performance"));

        await perf.RefreshAsync(
            () => new ScriptCheckRequest(_roots.Sources(), ScriptCheckTestRoots.Rules(), foreignStore, new HashSet<string>(), Array.Empty<string>(), ScriptCheckNames.All, PerformanceOnly: true),
            null,
            CancellationToken.None);

        Assert.Contains(full.Result!.Findings, f => f.Kind == FindingKind.DeadEvent && f.Name == "m.2");
        Assert.Contains(full.Result.Findings, f => f.Kind == FindingKind.ExpensiveIterator);
        Assert.Equal(ScriptCheckState.Ready, perf.State);
        Assert.NotEmpty(perf.Result!.Findings);
        Assert.All(perf.Result.Findings, f => Assert.Equal(FindingKind.ExpensiveIterator, f.Kind));
        Assert.True(perf.Result.BlockReferencesChecked);
        Assert.DoesNotContain(ScriptCheckTexts.NoBuiltIns, perf.StatusText);
    }

    [Fact]
    public async Task ForeignStoreDoesNotEvictOwn()
    {
        WritePerformanceFixture();
        var foreignStore = new ScriptCheckStore(Path.Combine(_roots.Temp, "performance"));
        var session = new ScriptCheckSession();

        await session.RefreshAsync(() => Request(), null, CancellationToken.None);
        await session.RefreshAsync(
            () => new ScriptCheckRequest(_roots.Sources(), ScriptCheckTestRoots.Rules(), foreignStore, new HashSet<string>(), Array.Empty<string>(), ScriptCheckNames.All, PerformanceOnly: true),
            null,
            CancellationToken.None);
        var progress = new Collector();
        await session.RefreshAsync(() => Request(), progress, CancellationToken.None);

        Assert.Equal(ScriptCheckState.Ready, session.State);
        Assert.Equal(new BuildProgress(BuildPhase.Calls, 0, 0), progress.Last(BuildPhase.Calls));
    }

    /// <summary>Sammelt Meldungen synchron (<see cref="Progress{T}"/> meldet asynchron).</summary>
    private sealed class Collector : IProgress<BuildProgress>
    {
        private readonly List<BuildProgress> _reports = new();

        public BuildProgress Last(BuildPhase phase)
        {
            lock (_reports)
            {
                return _reports.Last(r => r.Phase == phase);
            }
        }

        public void Report(BuildProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }
        }
    }

    private ScriptCheckRequest RequestWithRules(ScriptCheckRules rules) => new(_roots.Sources(withActiveMod: true), rules, _store, BuiltIns, Array.Empty<string>());

    private ScriptCheckRules RulesWithoutPerformance()
    {
        var source = Path.Combine(ScriptCheckTestRoots.ProfilesDir, RepoFiles.Profile("ck3").ScriptCheckRules!.Replace('/', Path.DirectorySeparatorChar));
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(source), documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })!.AsObject();
        json.Remove("performance");
        var path = Path.Combine(_roots.Temp, "ck3-noperf-rules.json");
        File.WriteAllText(path, json.ToJsonString());
        return ScriptCheckRules.Load(path);
    }
}
