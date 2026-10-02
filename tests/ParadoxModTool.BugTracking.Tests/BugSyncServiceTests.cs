using ParadoxModTool.Mods;
using ParadoxModTool.Notes;
using Xunit;

namespace ParadoxModTool.BugTracking.Tests;

public class BugSyncServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-bugsvc-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string BugsPath => Path.Combine(_dir, BugReportStore.FileName);

    private sealed class FakeSource : IBugSource
    {
        public Dictionary<string, IReadOnlyList<RemoteBug>> Bugs { get; } = new();

        public HashSet<string> Failing { get; } = new();

        public List<(string Key, string Id, BugStatus Status)> StatusCalls { get; } = new();

        public string Kind => BugSourceKinds.GitHub;

        public BugSource Source => BugSource.GitHub;

        public Task<IReadOnlyList<RemoteBug>> FetchAsync(BugSourceEntry entry, CancellationToken cancellation) =>
            Failing.Contains(entry.Key) ? throw new BugSourceException("kaputt") : Task.FromResult(Bugs.GetValueOrDefault(entry.Key) ?? Array.Empty<RemoteBug>());

        public Task SetStatusAsync(BugSourceEntry entry, BugReport report, BugStatus status, CancellationToken cancellation)
        {
            if (Failing.Contains(entry.Key))
            {
                throw new BugSourceException("kein Netz");
            }

            StatusCalls.Add((entry.Key, report.ExternalId, status));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BugComment>> LoadDiscussionAsync(BugSourceEntry entry, BugReport report, CancellationToken cancellation) =>
            Task.FromResult<IReadOnlyList<BugComment>>(new[] { new BugComment("anna", DateTime.UtcNow, "hallo " + report.ExternalId) });
    }

    private static RemoteBug Remote(string id) => new(id, "u", "t" + id, "b", "a", BugStatus.Open, DateTime.UtcNow, DateTime.UtcNow, null);

    private static BugSourceEntry Entry(string key, bool enabled = true) => new() { Kind = BugSourceKinds.GitHub, Key = key, Enabled = enabled };

    [Fact]
    public async Task Refresh_FehlerEinerQuelle_AndereLaufenWeiter()
    {
        var source = new FakeSource();
        source.Bugs["o/a"] = new[] { Remote("1"), Remote("2") };
        source.Failing.Add("o/b");
        source.Bugs["o/c"] = new[] { Remote("3") };
        var reports = new List<(int, int)>();
        var service = new BugSyncService(new[] { source }, BugsPath);

        var summary = await service.RefreshAsync("main", new[] { Entry("o/a"), Entry("o/b"), Entry("o/c", enabled: false) }, new SyncProgress(reports), default);

        Assert.Equal(2, summary.Added);
        Assert.Equal(new[] { "kaputt" }, summary.Outcomes.Where(o => o.Error is not null).Select(o => o.Error));
        Assert.Equal(2, summary.Outcomes.Count);
        Assert.Equal(new[] { "1", "2" }, BugReportStore.Load(BugsPath).Reports.Select(r => r.ExternalId));
        Assert.Equal((2, 2), reports[^1]);
    }

    [Fact]
    public async Task SetStatus_ExternErstDienstDannLokal()
    {
        var source = new FakeSource();
        source.Bugs["o/a"] = new[] { Remote("1") };
        var service = new BugSyncService(new[] { source }, BugsPath);
        await service.RefreshAsync("main", new[] { Entry("o/a") }, null, default);
        var report = BugReportStore.Load(BugsPath).Reports.Single();

        await service.SetStatusAsync(report, BugStatus.Rejected, new[] { Entry("O/A") }, default);

        Assert.Equal(("O/A", "1", BugStatus.Rejected), Assert.Single(source.StatusCalls));
        Assert.Equal(BugStatus.Rejected, BugReportStore.Load(BugsPath).Reports.Single().Status);
    }

    [Fact]
    public async Task SetStatus_DienstFehler_LokalUnveraendert()
    {
        var source = new FakeSource();
        source.Bugs["o/a"] = new[] { Remote("1") };
        var service = new BugSyncService(new[] { source }, BugsPath);
        await service.RefreshAsync("main", new[] { Entry("o/a") }, null, default);
        var report = BugReportStore.Load(BugsPath).Reports.Single();
        source.Failing.Add("o/a");

        var error = await Assert.ThrowsAsync<BugSourceException>(() => service.SetStatusAsync(report, BugStatus.Done, new[] { Entry("o/a") }, default));

        Assert.Equal("kein Netz", error.Message);
        Assert.Equal(BugStatus.Open, BugReportStore.Load(BugsPath).Reports.Single().Status);
    }

    [Fact]
    public async Task SetStatus_LokalOhneDienst_UnbekannteQuelleWirft()
    {
        var source = new FakeSource();
        var service = new BugSyncService(new[] { source }, BugsPath);
        BugReportStore.Update(BugsPath, data => BugEditing.AddLocal(data, "lokal", string.Empty, null, null));
        var local = BugReportStore.Load(BugsPath).Reports.Single();

        await service.SetStatusAsync(local, BugStatus.Done, Array.Empty<BugSourceEntry>(), default);
        var orphan = new BugReport { Source = BugSource.GitHub, SourceKey = "o/weg", ExternalId = "9" };

        Assert.Empty(source.StatusCalls);
        Assert.Equal(BugStatus.Done, BugReportStore.Load(BugsPath).Reports.Single().Status);
        var error = await Assert.ThrowsAsync<BugSourceException>(() => service.SetStatusAsync(orphan, BugStatus.Done, new[] { Entry("o/a") }, default));
        Assert.Equal(BugTexts.SourceUnknown, error.Message);
    }

    [Fact]
    public async Task Diskussion_LokalLeer_ExternUeberQuelle()
    {
        var service = new BugSyncService(new[] { new FakeSource() }, BugsPath);

        Assert.Empty(await service.LoadDiscussionAsync(new BugReport(), Array.Empty<BugSourceEntry>(), default));
        var comment = Assert.Single(await service.LoadDiscussionAsync(new BugReport { Source = BugSource.GitHub, SourceKey = "o/a", ExternalId = "5" }, new[] { Entry("o/a") }, default));
        Assert.Equal("hallo 5", comment.Body);
    }

    [Fact]
    public void Arten_UndQuellen()
    {
        Assert.Equal(BugSource.GitHub, BugSyncService.SourceOf(BugSourceKinds.GitHub));
        Assert.Equal(BugSource.Discord, BugSyncService.SourceOf(BugSourceKinds.Discord));
        Assert.Equal(BugSourceKinds.GitHub, BugSyncService.KindOf(BugSource.GitHub));
        Assert.Equal(string.Empty, BugSyncService.KindOf(BugSource.Local));
    }

    /// <summary>Synchroner Fortschritt (Progress&lt;T&gt; würde über den Synchronisationskontext verzögert melden).</summary>
    private sealed class SyncProgress : IProgress<(int Done, int Total)>
    {
        private readonly List<(int, int)> _reports;

        public SyncProgress(List<(int, int)> reports) => _reports = reports;

        public void Report((int Done, int Total) value) => _reports.Add(value);
    }
}
