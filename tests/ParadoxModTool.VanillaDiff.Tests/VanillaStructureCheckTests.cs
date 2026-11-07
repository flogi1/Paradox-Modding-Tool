using Xunit;

namespace ParadoxModTool.VanillaDiff.Tests;

public class VanillaStructureCheckTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-structure-").FullName;

    private string Vanilla => Path.Combine(_root, "game");

    private string OldVanilla => Path.Combine(_root, "old-game");

    private string Mod => Path.Combine(_root, "mod");

    private VanillaHistoryStore Store => VanillaHistoryStore.FromHistoryRoot(Path.Combine(_root, "history"));

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

    private void Snapshot(string relative, string version)
    {
        var source = Path.Combine(_root, "snap-src.txt");
        File.WriteAllText(source, "old\n");
        Store.RecordSnapshot(relative, version, source);
    }

    private void SaveOldListing(string version, params string[] scopes) =>
        VanillaStructureListing.Scan(OldVanilla, scopes, CancellationToken.None).Save(Store.StructureListingPath(version));

    private IReadOnlyList<StructureChange> Find(string current = "1.1") =>
        VanillaStructureCheck.Find(Mod, Vanilla, Store, current, CancellationToken.None);

    [Fact]
    public void FileReplacedByFolder_WithoutHistory()
    {
        Put(Mod, "main_menu/gui/messagetypes.txt");
        Put(Vanilla, "main_menu/gui/messagetypes/00_messagetypes.txt");

        var change = Assert.Single(Find());

        Assert.Equal("main_menu/gui/messagetypes.txt", change.ModRelativePath);
        Assert.Equal(StructureChangeKind.FileReplacedByFolder, change.Kind);
        Assert.Equal(new[] { "main_menu/gui/messagetypes/" }, change.NewVanillaPaths);
        Assert.Null(change.OldVersion);
        Assert.True(File.Exists(change.ModFullPath));
    }

    [Fact]
    public void FileReplacedByFolder_WithSnapshot_CarriesOldVersion()
    {
        Put(Mod, "main_menu/gui/messagetypes.txt");
        Put(Vanilla, "main_menu/gui/messagetypes/00_messagetypes.txt");
        Snapshot("main_menu/gui/messagetypes.txt", "1.0");

        var change = Assert.Single(Find());

        Assert.Equal(StructureChangeKind.FileReplacedByFolder, change.Kind);
        Assert.Equal("1.0", change.OldVersion);
    }

    [Fact]
    public void FileAndFolderSideBySide_NotReported()
    {
        Put(Mod, "main_menu/gui/messagetypes.txt");
        Put(Vanilla, "main_menu/gui/messagetypes.txt");
        Put(Vanilla, "main_menu/gui/messagetypes/00_messagetypes.txt");
        Snapshot("main_menu/gui/messagetypes.txt", "1.0");

        Assert.Empty(Find());
    }

    [Fact]
    public void FolderGone_WithOldListing()
    {
        Put(OldVanilla, "common/foo/x.txt");
        Put(OldVanilla, "common/foo/sub/y.txt");
        Put(OldVanilla, "common/other/z.txt");
        SaveOldListing("1.0", "common/foo", "common/other");
        Put(Vanilla, "common/other/z.txt");
        Put(Vanilla, "common/bar/foo/x.txt");
        Put(Mod, "common/foo/x.txt");
        Put(Mod, "common/foo/sub/y.txt");
        Put(Mod, "common/foo/mine.txt");

        var change = Assert.Single(Find());

        Assert.Equal(StructureChangeKind.FolderGone, change.Kind);
        Assert.Equal("common/foo/", change.ModRelativePath);
        Assert.Equal(new[] { "common/bar/foo/" }, change.NewVanillaPaths);
        Assert.Equal("1.0", change.OldVersion);
        Assert.True(Directory.Exists(change.ModFullPath));
    }

    [Fact]
    public void OldListing_FileGone_ReportedAsRemoved_CurrentVersionListingIgnored()
    {
        Put(OldVanilla, "common/foo/x.txt");
        SaveOldListing("1.0", "common/foo");
        Put(Vanilla, "common/foo/other.txt");
        // Eine Liste der aktuellen Version (schreibt der Recorder auch) zählt nicht als alte Version.
        VanillaStructureListing.Scan(Vanilla, new[] { "common/foo" }, CancellationToken.None).Save(Store.StructureListingPath("1.1"));
        Put(Mod, "common/foo/x.txt");

        var change = Assert.Single(Find());

        Assert.Equal(StructureChangeKind.FileRemoved, change.Kind);
        Assert.Equal("common/foo/x.txt", change.ModRelativePath);
        Assert.Empty(change.NewVanillaPaths);
        Assert.Equal("1.0", change.OldVersion);
    }

    [Fact]
    public void OnlyCurrentVersionListing_NoHistory_NothingReported()
    {
        Put(Vanilla, "common/foo/other.txt");
        VanillaStructureListing.Scan(Vanilla, new[] { "common/foo" }, CancellationToken.None).Save(Store.StructureListingPath("1.1"));
        Put(Mod, "common/foo/x.txt");

        Assert.Empty(Find());
    }

    [Fact]
    public void FolderMoved_SnapshotOnly_Reported()
    {
        Put(Mod, "common/foo/x.txt");
        Snapshot("common/foo/x.txt", "1.0");
        Put(Vanilla, "common/bar/foo/x.txt");

        var change = Assert.Single(Find());

        Assert.Equal(StructureChangeKind.FileMoved, change.Kind);
        Assert.Equal("common/foo/x.txt", change.ModRelativePath);
        Assert.Equal(new[] { "common/bar/foo/x.txt" }, change.NewVanillaPaths);
        Assert.Equal("1.0", change.OldVersion);
    }

    [Fact]
    public void FolderMoved_SnapshotOnly_WithoutNewLocation_Removed()
    {
        Put(Mod, "common/foo/x.txt");
        Snapshot("common/foo/x.txt", "1.0");
        Put(Vanilla, "common/bar/y.txt");

        var change = Assert.Single(Find());

        Assert.Equal(StructureChangeKind.FileRemoved, change.Kind);
        Assert.Empty(change.NewVanillaPaths);
    }

    [Fact]
    public void FileRemoved_InExistingFolder_WithSnapshot()
    {
        Put(Mod, "common/foo/x.txt");
        Snapshot("common/foo/x.txt", "1.0");
        Put(Vanilla, "common/foo/y.txt");

        var change = Assert.Single(Find());

        Assert.Equal(StructureChangeKind.FileRemoved, change.Kind);
    }

    [Fact]
    public void SnapshotOfCurrentVersionOnly_IsNoHistory()
    {
        Put(Mod, "common/foo/x.txt");
        Snapshot("common/foo/x.txt", "1.1");
        Put(Vanilla, "common/foo/y.txt");

        Assert.Empty(Find());
    }

    [Fact]
    public void OwnModFolder_NeverReported()
    {
        Put(Mod, "gfx/interface/mymod/a.dds");
        Put(Mod, "common/mymod_stuff/x.txt");
        Put(Mod, "mymod.txt");
        Put(Mod, ".git/config");
        Put(Vanilla, "gfx/interface/icon.dds");
        Put(OldVanilla, "gfx/interface/old.dds");
        SaveOldListing("1.0", "gfx/interface", "common");

        Assert.Empty(Find());
    }

    [Fact]
    public void OwnSubfolder_BelowGoneVanillaFolder_NotReported()
    {
        Put(OldVanilla, "gfx/interface/oldvanilla/a.dds");
        SaveOldListing("1.0", "gfx/interface");
        Put(Vanilla, "gfx/interface/icon.dds");
        Put(Mod, "gfx/interface/oldvanilla/mymod/a.dds");

        Assert.Empty(Find());
    }

    [Fact]
    public void ListStructureListingVersions_IgnoresTempFiles()
    {
        Put(OldVanilla, "common/foo/x.txt");
        SaveOldListing("1.0", "common/foo");
        SaveOldListing("1.10 Inca", "common/foo");
        File.WriteAllText(Store.StructureListingPath("1.2") + ".tmp", "D common\n");

        Assert.Equal(new[] { "1.0", "1.10-Inca" }, Store.ListStructureListingVersions());
    }

    [Fact]
    public void Labels_DistinctPerKind()
    {
        var labels = Enum.GetValues<StructureChangeKind>().Select(VanillaDiffTexts.StructureChangeLabel).ToList();

        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }

    [Fact]
    public void MissingVanillaRoot_WithOldListing_ReportsNothing()
    {
        Put(OldVanilla, "main_menu/gui/a.txt");
        SaveOldListing("1.0", "main_menu");
        Put(Mod, "main_menu/gui/a.txt");

        Assert.Empty(VanillaStructureCheck.Find(Mod, Path.Combine(_root, "nowhere"), Store, "1.1", CancellationToken.None));
    }

    /// <summary>
    /// Echte EU-V-Installation: läuft nur mit <c>PMT_EU5_GAME_DIR</c> (Ordner <c>…/game</c>), sonst endet er sofort.
    /// </summary>
    [Fact]
    public void RealEu5_OldMessagetypesFile_Reported()
    {
        var game = Environment.GetEnvironmentVariable("PMT_EU5_GAME_DIR");
        if (string.IsNullOrEmpty(game) || !Directory.Exists(game))
        {
            return;
        }

        Put(Mod, "main_menu/gui/messagetypes.txt");

        var change = Assert.Single(VanillaStructureCheck.Find(Mod, game, Store, "99.0", CancellationToken.None));

        Assert.Equal(StructureChangeKind.FileReplacedByFolder, change.Kind);
        Assert.Equal(new[] { "main_menu/gui/messagetypes/" }, change.NewVanillaPaths);
    }
}
