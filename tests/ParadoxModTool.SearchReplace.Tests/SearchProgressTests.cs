using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

public sealed class SearchProgressTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-search-progress-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    private List<string> WriteFiles(int count)
    {
        var paths = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(_dir, $"f{i}.txt");
            File.WriteAllText(path, "tax = 1");
            paths.Add(path);
        }

        return paths;
    }

    [Fact]
    public void SearchReportsEveryFileWithTheTotal()
    {
        WriteFiles(3);
        var reports = new List<(int Done, int Total)>();

        var matches = FileSearchService.Search(_dir, "tax", new SearchOptions(), progress: new SyncProgress(reports.Add));

        Assert.Equal(3, matches.Count);
        Assert.Equal(new[] { (0, 3), (1, 3), (2, 3) }, reports);
    }

    [Fact]
    public void SearchStopsWhenCancelled()
    {
        WriteFiles(3);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => FileSearchService.Search(_dir, "tax", new SearchOptions(), cancellation: cancellation.Token));
    }

    [Fact]
    public void ReplacementPlansReportProgressAndStopWhenCancelled()
    {
        var files = WriteFiles(2);
        var reports = new List<(int Done, int Total)>();

        var plans = FileReplaceService.ComputeReplacements(files, "tax", "income", new SearchOptions(), progress: new SyncProgress(reports.Add));

        Assert.Equal(2, plans.Count);
        Assert.Equal(new[] { (0, 2), (1, 2) }, reports);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => FileReplaceService.ComputeReplacements(files, "tax", "income", new SearchOptions(), cancellation.Token));
    }

    /// <summary>Meldet sofort auf dem aufrufenden Thread (<see cref="Progress{T}"/> würde über den Synchronisationskontext verzögern).</summary>
    private sealed class SyncProgress(Action<(int Done, int Total)> report) : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value) => report(value);
    }
}
