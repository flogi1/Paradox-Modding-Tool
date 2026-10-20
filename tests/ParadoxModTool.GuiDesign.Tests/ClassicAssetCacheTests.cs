using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Gemerkte Texturpfade, -größen und Schriften der klassischen Vorschau: innerhalb eines Vorschaulaufs nur einmal gesucht, beim nächsten Lauf
/// (<see cref="ClassicTextureLocator.NewRun"/>, jede neue Layout-Runde) sieht die Vorschau neue und geänderte Dateien.
/// </summary>
public sealed class ClassicAssetCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-classiccache-" + Guid.NewGuid().ToString("N"));

    public ClassicAssetCacheTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Texture_added_after_a_miss_gets_its_size_on_the_next_run()
    {
        var locator = new ClassicTextureLocator(new[] { _root });
        Assert.Null(locator.SizeOf("gfx/interface/late.dds"));

        Dds("gfx/interface/late.dds", 10, 20);
        locator.NewRun();

        Assert.Equal(Path.Combine(_root, "gfx", "interface", "late.dds"), locator.Locate("gfx/interface/late.dds"));
        Assert.Equal((10, 20), locator.SizeOf("gfx/interface/late.dds"));
    }

    [Fact]
    public void Edited_texture_gets_its_new_size_on_the_next_run()
    {
        var locator = new ClassicTextureLocator(new[] { _root });
        var path = Dds("gfx/interface/edit.dds", 10, 20);
        Assert.Equal((10, 20), locator.SizeOf("gfx/interface/edit.dds"));

        Dds("gfx/interface/edit.dds", 30, 40);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
        locator.NewRun();

        Assert.Equal((30, 40), locator.SizeOf("gfx/interface/edit.dds"));
    }

    [Fact]
    public void Same_texture_via_two_spellings_is_located_once()
    {
        // Wie gfx//interface//x.tga neben gfx/interface/x.tga: ein Eintrag, eine Suche durch die Wurzeln; die .tga gibt es nur als .dds.
        var locator = new ClassicTextureLocator(new[] { Path.Combine(_root, "mod"), _root });
        Dds("gfx/interface/x.dds", 8, 4);
        var expected = Path.Combine(_root, "gfx", "interface", "x.dds");

        Assert.Equal(expected, locator.Locate("gfx//interface//x.tga"));
        Assert.Equal(expected, locator.Locate("gfx/interface/x.tga"));
        Assert.Equal(expected, locator.Locate(@"GFX\interface\x.tga"));
        Assert.Equal((8, 4), locator.SizeOf("gfx//interface//x.tga"));
        Assert.Equal((8, 4), locator.SizeOf("gfx/interface/x.tga"));
        Assert.Equal(1, locator.Searches);

        // Ein Treffer wird im nächsten Lauf nur bestätigt, nicht neu gesucht.
        locator.NewRun();
        Assert.Equal(expected, locator.Locate("gfx/interface/x.tga"));
        Assert.Equal(1, locator.Searches);
    }

    [Fact]
    public void A_miss_is_searched_once_per_run()
    {
        var locator = new ClassicTextureLocator(new[] { _root });

        Assert.Null(locator.Locate("gfx/interface/none.dds"));
        Assert.Null(locator.Locate("gfx//interface/none.dds"));
        Assert.Equal(1, locator.Searches);

        locator.NewRun();
        Assert.Null(locator.Locate("gfx/interface/none.dds"));
        Assert.Equal(2, locator.Searches);
    }

    [Fact]
    public void Every_layout_is_a_new_run_so_an_added_texture_shows_on_the_next_preview()
    {
        var assets = Assets("spriteTypes = { spriteType = { name = \"GFX_late\" texturefile = \"gfx/interface/late.dds\" } }");
        var element = Window("iconType = { name = \"i\" spriteType = \"GFX_late\" }");

        var before = Find(new ClassicLayoutEngine(assets).Layout(element), "i");
        Assert.Contains(GuiTexts.ClassicTextureMissing("gfx/interface/late.dds"), before.Notes);

        Dds("gfx/interface/late.dds", 12, 6);
        var after = Find(new ClassicLayoutEngine(assets).Layout(element), "i");

        Assert.Empty(after.Notes);
        Assert.Equal((12.0, 6.0), (after.Width, after.Height));
    }

    [Fact]
    public void Font_added_after_a_miss_is_found_on_the_next_run_and_an_edited_font_is_read_again()
    {
        var assets = Assets("bitmapfonts = { bitmapfont = { name = \"f\" path = \"gfx/fonts/f\" } }");
        Assert.Null(assets.Fonts.Get("f"));

        // Im selben Lauf wird nicht erneut gesucht (der Zeichner fragt die Schrift bei jedem Bild).
        var searches = assets.Textures.Searches;
        Assert.Null(assets.Fonts.Get("f"));
        Assert.Equal(searches, assets.Textures.Searches);

        var fnt = Write("gfx/fonts/f.fnt", "common lineHeight=10 base=8\n");
        assets.Textures.NewRun();
        Assert.Equal(10, assets.Fonts.Get("f")!.Value.Font.LineHeight);

        Write("gfx/fonts/f.fnt", "common lineHeight=14 base=8\n");
        File.SetLastWriteTimeUtc(fnt, File.GetLastWriteTimeUtc(fnt).AddSeconds(2));
        Assert.Equal(10, assets.Fonts.Get("f")!.Value.Font.LineHeight);
        assets.Textures.NewRun();
        Assert.Equal(14, assets.Fonts.Get("f")!.Value.Font.LineHeight);
    }

    [Fact]
    public void Font_page_texture_added_later_is_found_on_the_next_run()
    {
        var assets = Assets("bitmapfonts = { bitmapfont = { name = \"f\" path = \"gfx/fonts/f\" } }");
        Write("gfx/fonts/f.fnt", "common lineHeight=10 base=8\npage id=0 file=\"f.dds\"\n");
        Assert.Null(assets.Fonts.Get("f")!.Value.PageTexturePaths[0]);

        var page = Dds("gfx/fonts/f.dds", 16, 16);
        assets.Textures.NewRun();

        Assert.Equal(Path.GetFullPath(page), assets.Fonts.Get("f")!.Value.PageTexturePaths[0]);
    }

    private ClassicAssets Assets(string gfx)
    {
        var file = new GuiSourceFile(Path.Combine(_root, "interface", "a.gfx"), "interface/a.gfx", GuiLayer.Vanilla, "vanilla", 0, 0);
        return ClassicAssets.Build(
            new[] { new GuiSourceRoot(_root, GuiLayer.Vanilla, "vanilla") }, new[] { file }, spriteFirstWins: false, caseSensitive: true, _ => GuiParser.Parse(gfx));
    }

    private static ResolvedElement Window(string body)
    {
        var rules = ClassicLayoutKit.Rules("hoi4");
        var doc = GuiParser.Parse("guiTypes = { containerWindowType = { name = \"w\" size = { width = 100 height = 100 } " + body + " } }");
        var window = doc.Root.Descendants().First(n => n.Name == "containerWindowType");
        return new GuiResolver(GuiIndex.Empty, rules, doc, null).Resolve(window);
    }

    private static LayoutBox Find(LayoutBox root, string name) => ClassicLayoutKit.Find(root, name);

    private string Dds(string relative, int width, int height)
    {
        var header = new byte[128];
        "DDS "u8.CopyTo(header);
        BitConverter.GetBytes(124).CopyTo(header, 4);
        BitConverter.GetBytes(height).CopyTo(header, 12);
        BitConverter.GetBytes(width).CopyTo(header, 16);
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, header);
        return path;
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }
}
