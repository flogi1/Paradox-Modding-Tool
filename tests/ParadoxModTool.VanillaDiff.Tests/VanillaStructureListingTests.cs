using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class VanillaStructureListingTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-listing-").FullName;
    private readonly GameProfile _profile = new() { Id = "x", LoadOrderRule = VanillaFileTracker.AlphabeticalFilenameWinsRule };

    private string Vanilla => Path.Combine(_root, "game");

    private string Mods => Path.Combine(_root, "mod");

    private string History => Path.Combine(_root, "history");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static void Put(string root, string relative, string text = "x\n")
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private SnapshotRecordResult Record(string version = "1.0") =>
        VanillaSnapshotRecorder.Record(
            new[] { Path.Combine(Mods, "a") }, Vanilla, _profile, version, _ => VanillaHistoryStore.FromHistoryRoot(History));

    [Fact]
    public void Scan_ListsFilesAndFolders_UnderTopFolders()
    {
        Put(Vanilla, "main_menu/gui/messagetypes/a.txt");
        Put(Vanilla, "main_menu/gui/b.gui");
        Put(Vanilla, "in_game/common/c.txt");
        Put(Vanilla, "other/d.txt");

        var listing = VanillaStructureListing.Scan(Vanilla, new[] { "main_menu/gui", "in_game/common" }, CancellationToken.None);

        Assert.True(listing.HasFile("main_menu/gui/messagetypes/a.txt"));
        Assert.True(listing.HasFile("MAIN_MENU/gui/b.gui"));
        Assert.True(listing.HasFile("in_game/common/c.txt"));
        Assert.True(listing.HasFolder("main_menu/gui/messagetypes"));
        Assert.True(listing.HasFolder("main_menu"));
        Assert.True(listing.HasFolder("in_game/common"));
        Assert.False(listing.HasFile("other/d.txt"));
        Assert.False(listing.HasFolder("other"));
        Assert.False(listing.HasFile("main_menu/gui/messagetypes.txt"));
    }

    [Fact]
    public void SaveLoad_RoundTrip()
    {
        Put(Vanilla, "main_menu/gui/messagetypes/a.txt");
        Put(Vanilla, "main_menu/gui/b.gui");
        var path = Path.Combine(_root, "out", "listing.txt");

        VanillaStructureListing.Scan(Vanilla, new[] { "main_menu/gui" }, CancellationToken.None).Save(path);
        var loaded = VanillaStructureListing.Load(path);

        Assert.NotNull(loaded);
        Assert.True(loaded!.HasFile("main_menu/gui/b.gui"));
        Assert.True(loaded.HasFile("main_menu/gui/messagetypes/a.txt"));
        Assert.True(loaded.HasFolder("main_menu/gui/messagetypes"));
        Assert.Null(VanillaStructureListing.Load(Path.Combine(_root, "missing.txt")));
        Assert.Empty(Directory.GetFiles(Path.Combine(_root, "out"), "*.tmp"));
    }

    [Fact]
    public void Recorder_OneSegmentScope_IsNotRecursive()
    {
        Put(Vanilla, "gfx/a.dds");
        Put(Vanilla, "gfx/sub/deep/b.dds");
        Put(Mods, "a/gfx/foo.dds");

        Assert.Equal(0, Record().Failed);

        var listing = VanillaStructureListing.Load(VanillaHistoryStore.FromHistoryRoot(History).StructureListingPath("1.0"))!;
        Assert.True(listing.HasFile("gfx/a.dds"));
        Assert.True(listing.HasFolder("gfx/sub"));
        Assert.False(listing.HasFolder("gfx/sub/deep"));
        Assert.False(listing.HasFile("gfx/sub/deep/b.dds"));
    }

    [Fact]
    public void FilesNamed_FindsOtherLocations()
    {
        Put(Vanilla, "main_menu/gui/messagetypes/Foo.txt");
        Put(Vanilla, "main_menu/gui/foo.txt");
        Put(Vanilla, "main_menu/gui/bar.txt");

        var listing = VanillaStructureListing.Scan(Vanilla, new[] { "main_menu/gui" }, CancellationToken.None);

        Assert.Equal(new[] { "main_menu/gui/foo.txt", "main_menu/gui/messagetypes/Foo.txt" }, listing.FilesNamed("foo.txt"));
        Assert.Equal(new[] { "main_menu/gui/messagetypes" }, listing.FoldersNamed("MessageTypes"));
        Assert.Empty(listing.FilesNamed("nope.txt"));
    }

    [Fact]
    public void Recorder_WritesListingOnce()
    {
        Put(Vanilla, "main_menu/gui/messagetypes/a.txt");
        Put(Mods, "a/main_menu/gui/messagetypes.txt");
        Put(Mods, "a/.git/ignored/x.txt");
        var store = VanillaHistoryStore.FromHistoryRoot(History);

        Assert.Equal(0, Record().Failed);
        var path = store.StructureListingPath("1.0");
        Assert.True(File.Exists(path));
        var listing = VanillaStructureListing.Load(path)!;
        Assert.True(listing.HasFolder("main_menu/gui/messagetypes"));
        Assert.False(listing.HasFolder(".git"));

        var stamp = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamp);
        Put(Vanilla, "main_menu/gui/new.txt");

        Assert.Equal(0, Record().Failed);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        Assert.False(VanillaStructureListing.Load(path)!.HasFile("main_menu/gui/new.txt"));

        // Eine andere Version bekommt eine eigene Liste.
        Record("2.0");
        Assert.True(store.HasStructureListing("2.0"));
    }

    [Fact]
    public void Recorder_ListingError_CountsFailed()
    {
        Put(Vanilla, "main_menu/gui/b.gui");
        Put(Mods, "a/main_menu/gui/b.gui");
        // Eine Datei dort, wo der Ordner der Liste liegen müsste: Anlegen scheitert.
        Put(History, ".pmt-structure");

        var result = Record();

        Assert.Equal(new SnapshotRecordResult(1, 0, 1), result);
    }
}
