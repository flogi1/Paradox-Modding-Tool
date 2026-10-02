using Xunit;

namespace ParadoxModTool.VersionManager.Tests;

public class StorageLocationsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-storage-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Extern_LegtHistorieUndSnapshotsUnterOberordnerAn()
    {
        var settings = new StorageSettings(@"C:\Backups", false);
        var modRoot = @"C:\Mods\My Cool Mod";

        Assert.Equal(Path.Combine(@"C:\Backups", "EU V Vanilla History"), StorageLocations.ResolveVanillaHistoryDirectory(modRoot, settings));
        Assert.Equal(Path.Combine(@"C:\Backups", "My Cool Mod Snapshots"), StorageLocations.ResolveSnapshotsDirectory(modRoot, settings));
    }

    [Fact]
    public void Patch_recipes_live_outside_the_mod_unless_inside_mode()
    {
        var modRoot = @"C:\Mods\My Cool Mod";

        Assert.Equal(Path.Combine(@"C:\Backups", "My Cool Mod Patch Recipes"), StorageLocations.ResolvePatchRecipesDirectory(modRoot, new StorageSettings(@"C:\Backups", false)));
        Assert.Equal(Path.Combine(modRoot, ".pmt-patches"), StorageLocations.ResolvePatchRecipesDirectory(modRoot, new StorageSettings(@"C:\Backups", true)));
    }

    [Fact]
    public void ImModOrdner_NutztFruehereOrdnernamen()
    {
        var settings = new StorageSettings(@"C:\Backups", true);
        var modRoot = @"C:\Mods\X";

        Assert.Equal(Path.Combine(modRoot, ".pmt-vanilla-history"), StorageLocations.ResolveVanillaHistoryDirectory(modRoot, settings));
        Assert.Equal(Path.Combine(modRoot, "snapshots"), StorageLocations.ResolveSnapshotsDirectory(modRoot, settings));
    }

    [Fact]
    public void OhneOberordner_NutztStandardOberordner()
    {
        var settings = new StorageSettings(null, false);

        Assert.StartsWith(StorageLocations.GetDefaultParentFolder(), StorageLocations.ResolveVanillaHistoryDirectory(@"C:\Mods\X", settings));
    }

    [Fact]
    public void GetModName_ToleriertAbschliessendenTrenner()
    {
        Assert.Equal("Abc", StorageLocations.GetModName(@"C:\Mods\Abc\"));
    }

    [Fact]
    public void CreateSnapshot_MitEigenemZielordner_SchreibtZipDorthinUndNichtInModOrdner()
    {
        var modRoot = Path.Combine(_root, "mod");
        var snapshots = Path.Combine(_root, "out", "mod Snapshots");
        Directory.CreateDirectory(modRoot);
        File.WriteAllText(Path.Combine(modRoot, "a.txt"), "x");

        var zip = new ModVersionManager(modRoot, snapshots).CreateSnapshot("t");

        Assert.Equal(snapshots, Path.GetDirectoryName(zip));
        Assert.True(File.Exists(zip));
        Assert.False(Directory.Exists(Path.Combine(modRoot, "snapshots")));
    }
}
