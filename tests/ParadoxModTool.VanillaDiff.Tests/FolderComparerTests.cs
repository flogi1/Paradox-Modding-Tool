using System.Text;
using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

/// <summary>Ordnervergleich im Compare-Reiter (wie WinMerge): alle Dateien beider Ordner mit Status.</summary>
public sealed class FolderComparerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-foldercompare-" + Guid.NewGuid().ToString("N"));

    private string A => Path.Combine(_root, "a");

    private string B => Path.Combine(_root, "b");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void Put(string root, string relative, string text, bool bom = false)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(bom));
    }

    private static void PutBytes(string root, string relative, params byte[] bytes)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private IReadOnlyList<FolderCompareEntry> Compare() => FolderComparer.Compare(A, B, CancellationToken.None);

    private static FolderCompareStatus StatusOf(IReadOnlyList<FolderCompareEntry> entries, string relative) =>
        entries.Single(e => e.RelativePath == relative).Status;

    [Fact]
    public void Status_covers_different_only_left_only_right_and_same_including_subfolders()
    {
        Put(A, "common/a.txt", "x = 1\n");
        Put(B, "common/a.txt", "x = 2\n");
        Put(A, "events/only_a.txt", "a\n");
        Put(B, "gui/deep/only_b.gui", "b\n");
        Put(A, "same.txt", "s\n");
        Put(B, "same.txt", "s\n");

        var entries = Compare();

        Assert.Equal(FolderCompareStatus.Different, StatusOf(entries, "common/a.txt"));
        Assert.Equal(FolderCompareStatus.OnlyLeft, StatusOf(entries, "events/only_a.txt"));
        Assert.Equal(FolderCompareStatus.OnlyRight, StatusOf(entries, "gui/deep/only_b.gui"));
        Assert.Equal(FolderCompareStatus.Same, StatusOf(entries, "same.txt"));
        Assert.Equal(new[] { "common/a.txt", "events/only_a.txt", "gui/deep/only_b.gui", "same.txt" }, entries.Select(e => e.RelativePath));
    }

    [Fact]
    public void Line_endings_and_bom_do_not_count_as_difference()
    {
        Put(A, "x.txt", "a\r\nb\r\n", bom: true);
        Put(B, "x.txt", "a\nb\n");

        Assert.Equal(FolderCompareStatus.Same, StatusOf(Compare(), "x.txt"));
    }

    [Fact]
    public void Binary_files_are_compared_byte_by_byte_and_marked()
    {
        PutBytes(A, "gfx/icon.dds", 0x44, 0x00, 0x53, 0x01);
        PutBytes(B, "gfx/icon.dds", 0x44, 0x00, 0x53, 0x02);
        PutBytes(A, "gfx/same.dds", 0x44, 0x00);
        PutBytes(B, "gfx/same.dds", 0x44, 0x00);

        var entries = Compare();

        Assert.Equal(FolderCompareStatus.Different, StatusOf(entries, "gfx/icon.dds"));
        Assert.Equal(FolderCompareStatus.Same, StatusOf(entries, "gfx/same.dds"));
        Assert.True(entries.Single(e => e.RelativePath == "gfx/icon.dds").IsBinary);
    }

    [Fact]
    public void Git_and_pmt_tool_folders_are_skipped_but_metadata_is_kept()
    {
        Put(A, ".git/config", "a\n");
        Put(A, ".pmt-versions/x.txt", "a\n");
        Put(A, ".metadata/metadata.json", "{}\n");

        Assert.Equal(new[] { ".metadata/metadata.json" }, Compare().Select(e => e.RelativePath));
    }

    [Fact]
    public void Progress_ends_with_all_files_done_and_cancel_stops_the_run()
    {
        for (var i = 0; i < 5; i++)
        {
            Put(A, $"f{i}.txt", "a\n");
            Put(B, $"f{i}.txt", "b\n");
        }

        var reports = new List<FolderCompareProgress>();
        FolderComparer.Compare(A, B, CancellationToken.None, new SyncProgress(reports.Add));

        Assert.Equal(new FolderCompareProgress(5, 5, "f4.txt"), reports[^1]);
        Assert.Contains(reports, r => r.Total == 0);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => FolderComparer.Compare(A, B, cancelled.Token));
    }

    /// <summary>Meldet sofort (Progress&lt;T&gt; würde über den Synchronisationskontext verzögern).</summary>
    private sealed class SyncProgress(Action<FolderCompareProgress> report) : IProgress<FolderCompareProgress>
    {
        public void Report(FolderCompareProgress value) => report(value);
    }

    [Fact]
    public void Entry_rechecks_a_file_that_was_created_on_the_missing_side()
    {
        Put(A, "x.txt", "a\n");
        var before = FolderComparer.Entry("x.txt", Path.Combine(A, "x.txt"), Path.Combine(B, "x.txt"));
        Put(B, "x.txt", "a\n");
        var after = FolderComparer.Entry("x.txt", Path.Combine(A, "x.txt"), Path.Combine(B, "x.txt"));

        Assert.Equal(FolderCompareStatus.OnlyLeft, before.Status);
        Assert.Null(before.RightPath);
        Assert.Equal(FolderCompareStatus.Same, after.Status);
    }

    [Fact]
    public void Filter_hides_same_files_and_matches_the_path()
    {
        var entries = new[]
        {
            new FolderCompareEntry("common/a.txt", "l", "r", FolderCompareStatus.Different, false),
            new FolderCompareEntry("common/b.txt", "l", "r", FolderCompareStatus.Same, false),
            new FolderCompareEntry("events/c.txt", "l", null, FolderCompareStatus.OnlyLeft, false),
        };

        Assert.Equal(new[] { "common/a.txt", "events/c.txt" }, FolderComparer.Filter(entries, null, hideSame: true).Select(e => e.RelativePath));
        Assert.Equal(new[] { "common/a.txt", "common/b.txt" }, FolderComparer.Filter(entries, "COMMON", hideSame: false).Select(e => e.RelativePath));
    }
}
