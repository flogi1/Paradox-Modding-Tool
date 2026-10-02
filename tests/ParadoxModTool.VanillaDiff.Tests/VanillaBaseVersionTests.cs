using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class VanillaBaseVersionTests
{
    [Fact]
    public void Last_updated_version_wins() =>
        Assert.Equal("1.37", VanillaBaseVersion.Choose(new[] { "1.36", "1.37", "1.38" }, "1.39", "1.37", "1.38.*"));

    [Fact]
    public void Supported_pattern_picks_newest_match() =>
        Assert.Equal("1.36.2", VanillaBaseVersion.Choose(new[] { "1.36.1", "1.36.2", "1.37" }, "1.38", null, "1.36.*"));

    [Fact]
    public void Falls_back_to_newest_below_current() =>
        Assert.Equal("1.10", VanillaBaseVersion.Choose(new[] { "1.9", "1.10", "1.11" }, "1.11", null, null));

    [Fact]
    public void Never_current_or_newer() =>
        Assert.Null(VanillaBaseVersion.Choose(new[] { "1.40" }, "1.39", null, null));

    [Fact]
    public void Last_updated_without_snapshot_is_ignored() =>
        Assert.Equal("1.36", VanillaBaseVersion.Choose(new[] { "1.36" }, "1.38", "1.37", null));

    [Fact]
    public void Code_name_in_history_file_name_counts_as_the_same_version() =>
        Assert.Equal("v1.36.0-Inca", VanillaBaseVersion.Choose(new[] { "v1.36.0-Inca", "v1.37.5.0-Inca" }, "v1.37.5.0 Inca", null, null));

    [Fact]
    public void Baseline_resolve_uses_the_version_order()
    {
        var root = Directory.CreateTempSubdirectory("pmt-basever-").FullName;
        try
        {
            var store = VanillaHistoryStore.FromHistoryRoot(root);
            var file = Path.Combine(root, "v.txt");
            File.WriteAllText(file, "a = 1\n");
            store.RecordSnapshot("common/x/a.txt", "1.8", file);
            store.RecordSnapshot("common/x/a.txt", "1.9", file);

            Assert.Equal("1.9", VanillaMergeBaseline.Resolve(store, "common/x/a.txt", "1.10", file).OlderVersion);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
