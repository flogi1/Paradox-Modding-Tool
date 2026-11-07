using Xunit;
using System.Diagnostics;
using ParadoxModTool.GuiDesign.Imaging;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class GuiTextureCatalogTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-texcat-" + Guid.NewGuid().ToString("N"));

    public GuiTextureCatalogTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Touch(string relative)
    {
        var path = Dir(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Array.Empty<byte>());
        return path;
    }

    private string Dir(string relative) => Path.Combine(_temp, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Scan_FindsImageFilesUnderGfx()
    {
        Touch("game/gfx/a.dds");
        Touch("game/gfx/sub/b.PNG");
        Touch("game/gfx/c.tga");
        Touch("game/gfx/d.jpg");
        Touch("game/gfx/e.txt");
        Touch("game/other/f.dds");

        var result = GuiTextureCatalog.Scan(new[] { Dir("game") }, Dir("inst"), null, Array.Empty<string>(), CancellationToken.None);

        Assert.Equal(new[] { "gfx/a.dds", "gfx/c.tga", "gfx/d.jpg", "gfx/sub/b.PNG" }, result.Select(e => e.RelativePath));
    }

    [Fact]
    public void Scan_DeduplicatesByFirstRoot()
    {
        var modFile = Touch("mod/gfx/interface/a.dds");
        Touch("game/gfx/Interface/A.dds");

        var result = GuiTextureCatalog.Scan(new[] { Dir("mod"), Dir("game") }, Dir("inst"), Dir("mod"), Array.Empty<string>(), CancellationToken.None);

        var entry = Assert.Single(result);
        Assert.Equal(GuiTextureOrigin.Mod, entry.Origin);
        Assert.Equal(modFile, entry.FullPath);
    }

    [Fact]
    public void Scan_OriginLabels()
    {
        Touch("deps/dep_one/in_game/gfx/d.dds");
        Touch("deps/dep_oneb/in_game/gfx/e.dds");
        Touch("inst/game/dlc/D001_x/in_game/gfx/dlc.dds");
        Touch("inst/game/in_game/gfx/v.dds");
        Touch("inst/jomini/gfx/j.dds");
        var roots = new[] { Dir("deps/dep_one/in_game"), Dir("deps/dep_oneb/in_game"), Dir("inst/game/dlc/D001_x/in_game"), Dir("inst/game/in_game"), Dir("inst/jomini") };

        var result = GuiTextureCatalog.Scan(roots, Dir("inst"), null, new[] { Dir("deps/dep_one"), Dir("deps/dep_oneb") }, CancellationToken.None)
            .ToDictionary(e => e.RelativePath);

        Assert.Equal((GuiTextureOrigin.Dependency, "dep_one"), (result["gfx/d.dds"].Origin, result["gfx/d.dds"].OriginName));
        Assert.Equal((GuiTextureOrigin.Dependency, "dep_oneb"), (result["gfx/e.dds"].Origin, result["gfx/e.dds"].OriginName));
        Assert.Equal((GuiTextureOrigin.Dlc, "D001_x"), (result["gfx/dlc.dds"].Origin, result["gfx/dlc.dds"].OriginName));
        Assert.Equal(GuiTextureOrigin.Vanilla, result["gfx/v.dds"].Origin);
        Assert.Equal(GuiTextureOrigin.Vanilla, result["gfx/j.dds"].Origin);
        Assert.Equal("Abhängigkeit: dep_one", result["gfx/d.dds"].OriginLabel);
        Assert.Equal("DLC: D001_x", result["gfx/dlc.dds"].OriginLabel);
        Assert.Equal("Vanilla", result["gfx/v.dds"].OriginLabel);
    }

    [Fact]
    public void Scan_DependencyPrefixRespectsPathBoundaries()
    {
        Touch("mods/ab/gfx/x.dds");

        var result = GuiTextureCatalog.Scan(new[] { Dir("mods/ab") }, Dir("inst"), null, new[] { Dir("mods/a") }, CancellationToken.None);

        Assert.Equal(GuiTextureOrigin.Vanilla, Assert.Single(result).Origin);
    }

    [Fact]
    public void Scan_ModLabel()
    {
        Touch("mod/gfx/m.dds");

        var entry = Assert.Single(GuiTextureCatalog.Scan(new[] { Dir("mod") }, Dir("inst"), Dir("mod"), Array.Empty<string>(), CancellationToken.None));

        Assert.Equal("Mod", entry.OriginLabel);
    }

    [Fact]
    public void Scan_RelativePathUsesSlashes()
    {
        Touch("game/gfx/interface/icons/x.dds");

        var entry = Assert.Single(GuiTextureCatalog.Scan(new[] { Dir("game") }, Dir("inst"), null, Array.Empty<string>(), CancellationToken.None));

        Assert.Equal("gfx/interface/icons/x.dds", entry.RelativePath);
    }

    [Fact]
    public void Scan_Cancels()
    {
        Touch("game/gfx/a.dds");
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            GuiTextureCatalog.Scan(new[] { Dir("game") }, Dir("inst"), null, Array.Empty<string>(), source.Token));
    }

    private static GuiTextureEntry Entry(string path) => new(path, "C:/x/" + path, GuiTextureOrigin.Vanilla, null);

    [Fact]
    public void Filter_SegmentStartFirst()
    {
        var entries = new[] { Entry("gfx/interface/big_frame.dds"), Entry("gfx/interface/frame_a.dds") };

        var result = GuiTextureCatalog.Filter(entries, "FRAME");

        Assert.Equal(new[] { "gfx/interface/frame_a.dds", "gfx/interface/big_frame.dds" }, result.Select(e => e.RelativePath));
    }

    [Fact]
    public void Filter_EmptyTextAndMax()
    {
        var entries = new[] { Entry("gfx/a.dds"), Entry("gfx/b.dds"), Entry("gfx/c.dds") };

        Assert.Equal(3, GuiTextureCatalog.Filter(entries, "").Count);
        Assert.Equal(2, GuiTextureCatalog.Filter(entries, "dds", 2).Count);
        Assert.Empty(GuiTextureCatalog.Filter(entries, "zzz"));
    }

    [Fact]
    public void Filter_TenThousandEntries_Fast()
    {
        var entries = Enumerable.Range(0, 10_000).Select(i => Entry($"gfx/interface/folder{i % 50}/item_{i}_frame.dds")).ToList();
        GuiTextureCatalog.Filter(entries, "frame");

        var watch = Stopwatch.StartNew();
        var result = GuiTextureCatalog.Filter(entries, "item_9");
        watch.Stop();

        Assert.NotEmpty(result);
        Assert.True(watch.ElapsedMilliseconds < 200, $"{watch.ElapsedMilliseconds} ms");
    }
}
