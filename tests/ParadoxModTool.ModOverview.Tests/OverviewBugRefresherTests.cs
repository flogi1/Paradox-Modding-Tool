using ParadoxModTool.BugTracking;
using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using Xunit;
using static ParadoxModTool.ModOverview.Tests.TestMods;

namespace ParadoxModTool.ModOverview.Tests;

public class OverviewBugRefresherTests : TempDirTests
{
    /// <summary>Quelle einer Art, die je Schlüssel eine Meldung liefert oder mit einer lesbaren Meldung scheitert.</summary>
    private sealed class FakeSource(string kind, BugSource source) : IBugSource
    {
        public Dictionary<string, string> Failures { get; } = new();

        public List<string> Fetched { get; } = new();

        public string Kind => kind;

        public BugSource Source => source;

        public Task<IReadOnlyList<RemoteBug>> FetchAsync(BugSourceEntry entry, CancellationToken cancellation)
        {
            Fetched.Add(entry.Key);
            if (Failures.TryGetValue(entry.Key, out var error))
            {
                throw new BugSourceException(error);
            }

            var now = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
            IReadOnlyList<RemoteBug> bugs = new[] { new RemoteBug("1", "https://x.test/1", "Bug in " + entry.Key, "body", "author", BugStatus.Open, now, now, null) };
            return Task.FromResult(bugs);
        }

        public Task SetStatusAsync(BugSourceEntry entry, BugReport report, BugStatus status, CancellationToken cancellation) => Task.CompletedTask;

        public Task<IReadOnlyList<BugComment>> LoadDiscussionAsync(BugSourceEntry entry, BugReport report, CancellationToken cancellation) =>
            Task.FromResult<IReadOnlyList<BugComment>>(Array.Empty<BugComment>());
    }

    private readonly FakeSource _gitHub = new(BugSourceKinds.GitHub, BugSource.GitHub);
    private readonly FakeSource _discord = new(BugSourceKinds.Discord, BugSource.Discord);
    private readonly List<string> _factoryCalls = new();

    private string BugsPath => Path.Combine(Dir, "bugreports.json");

    private BugSyncService ServiceFor(string gameId)
    {
        _factoryCalls.Add(gameId);
        return new BugSyncService(new IBugSource[] { _gitHub, _discord }, BugsPath);
    }

    private static OverviewMod WithSource(OverviewMod mod, string kind, string key, bool enabled = true)
    {
        mod.Mod.BugSources.Add(new BugSourceEntry { Kind = kind, Key = key, Enabled = enabled });
        return mod;
    }

    [Fact]
    public async Task FetchesEveryProjectOfEveryGame()
    {
        var eu4 = WithSource(Mod("IV", "eu4"), BugSourceKinds.GitHub, "a/b");
        var eu5 = WithSource(Mod("IV2", "eu5"), BugSourceKinds.Discord, "1/2");

        var errors = await OverviewBugRefresher.RefreshAllAsync(Snapshot(eu4, eu5), ServiceFor, null, CancellationToken.None);

        Assert.Empty(errors);
        var reports = BugReportStore.Load(BugsPath).Reports;
        Assert.Equal(new[] { "IV", "IV2" }, reports.Select(r => r.ModId).OrderBy(id => id));
        Assert.All(reports, r => Assert.False(r.Seen));
        Assert.Equal(new[] { "eu4", "eu5" }, _factoryCalls);
    }

    [Fact]
    public async Task OneFailingSourceDoesNotStopOthers()
    {
        var eu4 = WithSource(Mod("IV", "eu4"), BugSourceKinds.GitHub, "a/b");
        var eu5 = WithSource(Mod("IV2", "eu5"), BugSourceKinds.Discord, "1/2");
        _gitHub.Failures["a/b"] = "gh not found";

        var errors = await OverviewBugRefresher.RefreshAllAsync(Snapshot(eu4, eu5), ServiceFor, null, CancellationToken.None);

        var error = Assert.Single(errors);
        Assert.Same(eu4, error.Root);
        Assert.Equal("a/b", error.SourceKey);
        Assert.Equal("gh not found", error.Error);
        Assert.Equal("IV2", Assert.Single(BugReportStore.Load(BugsPath).Reports).ModId);
    }

    [Fact]
    public async Task DisabledSourcesAndMembersAreSkipped()
    {
        var disabled = WithSource(Mod("Off"), BugSourceKinds.GitHub, "off/repo", enabled: false);
        var main = Mod("Main");
        var member = WithSource(Mod("Addon", role: ModRole.Submod, root: main), BugSourceKinds.GitHub, "member/repo");

        var errors = await OverviewBugRefresher.RefreshAllAsync(Snapshot(disabled, main, member), ServiceFor, null, CancellationToken.None);

        Assert.Empty(errors);
        Assert.Empty(_gitHub.Fetched);
    }

    [Fact]
    public async Task ProgressCountsProjects()
    {
        var first = WithSource(Mod("A"), BugSourceKinds.GitHub, "a/a");
        var second = WithSource(Mod("B"), BugSourceKinds.GitHub, "b/b");
        var reports = new List<(int, int)>();

        await OverviewBugRefresher.RefreshAllAsync(Snapshot(first, second), ServiceFor, new SyncProgress(reports), CancellationToken.None);

        Assert.Equal(new[] { (0, 2), (1, 2), (2, 2) }, reports);
    }

    [Fact]
    public void MarkSeen_CountsOnlyUnseen()
    {
        var data = new BugData
        {
            Reports = { new BugReport { Id = "a", Seen = false }, new BugReport { Id = "b", Seen = false }, new BugReport { Id = "c", Seen = true } },
        };
        BugReportStore.Save(data, BugsPath);

        Assert.Equal(2, OverviewBugRefresher.MarkSeen(BugsPath, new[] { "a", "b", "c" }));
        Assert.All(BugReportStore.Load(BugsPath).Reports, r => Assert.True(r.Seen));
    }

    /// <summary>Meldet synchron (Progress&lt;T&gt; würde über den SynchronizationContext verzögern).</summary>
    private sealed class SyncProgress(List<(int, int)> target) : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value) => target.Add(value);
    }
}
