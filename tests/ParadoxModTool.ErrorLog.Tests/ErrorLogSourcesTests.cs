using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogSourcesTests
{
    [Fact]
    public void SameFolderFromAllSourcesBecomesOneOwnMod()
    {
        using var tree = new TempTree(("mods/Own/descriptor.txt", "x"));
        var folder = tree.PathOf("mods/Own");

        var mods = ErrorLogSources.BuildMods(
            new[] { new OwnModFolder("Meine Mod", folder) },
            new[] { new PlaysetEntry(folder.Replace('\\', '/').ToUpperInvariant() + "/", true) },
            new[] { new InstalledModFolder("Installiert", folder + Path.DirectorySeparatorChar) });

        var mod = Assert.Single(mods);
        Assert.True(mod.IsOwn);
        Assert.Equal(0, mod.LoadOrder);
        Assert.Equal("Meine Mod", mod.Name);
        Assert.Equal(ErrorLogSources.Key(folder), mod.Directory);
    }

    [Fact]
    public void DisabledPlaysetEntriesGetNoLoadOrder()
    {
        using var tree = new TempTree();
        var a = tree.PathOf("a");
        var b = tree.PathOf("b");
        var c = tree.PathOf("c");

        var mods = ErrorLogSources.BuildMods(
            Array.Empty<OwnModFolder>(),
            new[] { new PlaysetEntry(a, true), new PlaysetEntry(b, false), new PlaysetEntry(c, true) },
            new[] { new InstalledModFolder("B", b) });

        Assert.Equal(new[] { ("a", 0), ("c", 1), ("B", -1) }, mods.Select(m => (m.Name, m.LoadOrder)));
    }

    [Fact]
    public void WithoutPlaysetOwnModsCountAsLoaded()
    {
        using var tree = new TempTree();

        var mods = ErrorLogSources.BuildMods(
            new[] { new OwnModFolder("Eins", tree.PathOf("eins")), new OwnModFolder("Zwei", tree.PathOf("zwei")) },
            null,
            new[] { new InstalledModFolder("Fremd", tree.PathOf("fremd")) });

        Assert.Equal(new[] { ("Eins", 0, true), ("Zwei", 1, true), ("Fremd", -1, false) }, mods.Select(m => (m.Name, m.LoadOrder, m.IsOwn)));
    }

    [Fact]
    public void SymbolRootsIndexReferencesOnlyForPlaysetAndOwnMods()
    {
        using var tree = new TempTree();
        var mods = ErrorLogSources.BuildMods(
            new[] { new OwnModFolder("Eigen", tree.PathOf("eigen")) },
            new[] { new PlaysetEntry(tree.PathOf("playset"), true) },
            new[] { new InstalledModFolder("Nur installiert", tree.PathOf("installiert")) });

        var roots = ErrorLogSources.SymbolRoots(mods, new[] { tree.PathOf("game") });

        Assert.Equal(
            new[] { ("game", false), ("playset", true), ("eigen", true), ("installiert", false) },
            roots.Select(r => (Path.GetFileName(r.Directory), r.IndexReferences)));
        Assert.Null(roots[0].Mod);
    }

    [Fact]
    public void VanillaRootsUseExistingEngineFoldersOrTheContentRoot()
    {
        using var tree = new TempTree(("eu5/game/x.txt", "x"), ("eu5/jomini/x.txt", "x"), ("eu4/common/x.txt", "x"));

        Assert.Equal(
            new[] { tree.PathOf("eu5/game"), tree.PathOf("eu5/jomini") },
            ErrorLogSources.VanillaRoots(tree.PathOf("eu5"), null));
        Assert.Equal(new[] { tree.PathOf("eu4") }, ErrorLogSources.VanillaRoots(tree.PathOf("eu4"), tree.PathOf("eu4")));
        Assert.Empty(ErrorLogSources.VanillaRoots(null, null));
    }
}
