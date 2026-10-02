using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class VanillaSnapshotRecorderTests : IDisposable
{
    private const string Path1 = "common/x/a.txt";
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-recorder-").FullName;
    private readonly GameProfile _profile = new() { Id = "x", LoadOrderRule = VanillaFileTracker.AlphabeticalFilenameWinsRule };

    private string Vanilla => Path.Combine(_root, "game");

    private string Mods => Path.Combine(_root, "mod");

    private string History => Path.Combine(_root, "history");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static void Put(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private string Mod(string name)
    {
        var folder = Path.Combine(Mods, name);
        Put(folder, Path1, "mod\n");
        return folder;
    }

    private SnapshotRecordResult Record(IEnumerable<string> mods, string? version, Func<string, VanillaHistoryStore>? storeFor = null) =>
        VanillaSnapshotRecorder.Record(mods, Vanilla, _profile, version, storeFor ?? (_ => VanillaHistoryStore.FromHistoryRoot(History)));

    [Fact]
    public void Records_each_overridden_file_once_per_version()
    {
        Put(Vanilla, Path1, "vanilla\n");

        var result = Record(new[] { Mod("a"), Mod("b") }, "1.37");

        Assert.Equal(new SnapshotRecordResult(1, 1, 0), result);
        Assert.Equal("vanilla\n", VanillaHistoryStore.FromHistoryRoot(History).ReadSnapshot(Path1, "1.37"));
    }

    [Fact]
    public void Never_overwrites_an_existing_snapshot()
    {
        Put(Vanilla, Path1, "old\n");
        Record(new[] { Mod("a") }, "1.37");
        Put(Vanilla, Path1, "new\n");

        var result = Record(new[] { Mod("a") }, "1.37");

        Assert.Equal(new SnapshotRecordResult(0, 1, 0), result);
        Assert.Equal("old\n", VanillaHistoryStore.FromHistoryRoot(History).ReadSnapshot(Path1, "1.37"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Unknown_version_records_nothing(string? version)
    {
        Put(Vanilla, Path1, "vanilla\n");

        Assert.Equal(new SnapshotRecordResult(0, 0, 0), Record(new[] { Mod("a") }, version));
        Assert.False(Directory.Exists(History));
    }

    [Fact]
    public void Missing_vanilla_file_counts_as_failed()
    {
        Put(Vanilla, Path1, "vanilla\n");
        var mod = Mod("a");

        var result = Record(new[] { mod }, "1.37", _ =>
        {
            File.Delete(Path.Combine(Vanilla, "common", "x", "a.txt"));
            return VanillaHistoryStore.FromHistoryRoot(History);
        });

        Assert.Equal(new SnapshotRecordResult(0, 0, 1), result);
    }

    [Fact]
    public void LocalModFolders_skips_dot_folders()
    {
        Directory.CreateDirectory(Path.Combine(Mods, "b"));
        Directory.CreateDirectory(Path.Combine(Mods, "a"));
        Directory.CreateDirectory(Path.Combine(Mods, ".git"));

        Assert.Equal(new[] { "a", "b" }, VanillaSnapshotRecorder.LocalModFolders(Mods).Select(Path.GetFileName));
        Assert.Empty(VanillaSnapshotRecorder.LocalModFolders(Path.Combine(_root, "missing")));
        Assert.Empty(VanillaSnapshotRecorder.LocalModFolders(null));
    }
}
