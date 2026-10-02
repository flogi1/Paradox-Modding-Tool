using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class SymbolIndexProgressTests
{
    [Fact]
    public void BuildReportsSearchThenTotalThenCompletion()
    {
        using var tree = new TempTree(
            ("m/in_game/common/scripted_effects/a.txt", "a = { }\n"),
            ("m/in_game/common/scripted_effects/b.txt", "b = { }\n"),
            ("m/in_game/localization/english/l_english.yml", "l_english:\n k:0 \"a\"\n"));
        var mod = new ModSource("M", tree.PathOf("m"), IsOwn: true, LoadOrder: 0);
        var reports = new List<IndexProgress>();

        SymbolIndex.Build(new[] { new SymbolRoot(mod.Directory, mod, true) }, default, new SyncProgress(reports));

        Assert.Equal(new IndexProgress(0, 0), reports[0]);
        Assert.Equal(new IndexProgress(0, 3), reports[1]);
        Assert.Equal(new IndexProgress(3, 3), reports[^1]);
    }

    [Fact]
    public void BuildStopsWhenCancelled()
    {
        using var tree = new TempTree(("m/in_game/common/scripted_effects/a.txt", "a = { }\n"));
        var mod = new ModSource("M", tree.PathOf("m"), IsOwn: true, LoadOrder: 0);
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => SymbolIndex.Build(new[] { new SymbolRoot(mod.Directory, mod, true) }, cancel.Token));
    }

    private sealed class SyncProgress : IProgress<IndexProgress>
    {
        private readonly List<IndexProgress> _reports;

        public SyncProgress(List<IndexProgress> reports) => _reports = reports;

        public void Report(IndexProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }
        }
    }
}
