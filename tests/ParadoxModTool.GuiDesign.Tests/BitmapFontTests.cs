using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Bitmap-Schriften (<c>.fnt</c> im BMFont-Textformat) der klassischen Spiele.</summary>
public sealed class BitmapFontTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-bitmapfont-" + Guid.NewGuid().ToString("N"));

    // Auszug aus gfx/fonts/Arial12.fnt (HOI IV), dazu ein paar Zeichen und eine Kerning-Zeile.
    private const string ArialHeader =
        "info face=\"Arial\" size=14 bold=0 italic=0 charset=\"\" unicode=1 stretchH=100 smooth=1 aa=1 padding=0,0,0,0 spacing=1,1\n"
        + "common lineHeight=14 base=11 scaleW=256 scaleH=256 pages=1 packed=0\n"
        + "page id=0 file=\"Arial.dds\"\n"
        + "chars count=382\n"
        + "char id=32   x=0     y=0     width=0     height=0     xoffset=0     yoffset=11    xadvance=3     page=0  chnl=15\n";

    private const string Letters =
        "char id=65   x=1     y=2     width=9     height=10    xoffset=0     yoffset=1     xadvance=9     page=0  chnl=15\n"
        + "char id=86   x=11    y=2     width=9     height=10    xoffset=0     yoffset=1     xadvance=8     page=0  chnl=15\n"
        + "char id=63   x=21    y=2     width=6     height=10    xoffset=0     yoffset=1     xadvance=6     page=0  chnl=15\n"
        + "kernings count=1\n"
        + "kerning first=65 second=86 amount=-2\n";

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Parses_real_fnt_header()
    {
        var font = BitmapFont.Parse(ArialHeader);

        Assert.Equal(14, font.LineHeight);
        Assert.Equal(11, font.Base);
        Assert.Equal(new[] { "Arial.dds" }, font.Pages);
        var space = font.Glyph(' ')!;
        Assert.Equal(3, space.XAdvance);
        Assert.Equal(11, space.YOffset);
        Assert.Equal(0, space.Page);
        Assert.Null(font.Glyph('A'));
    }

    [Fact]
    public void Measures_with_kerning()
    {
        var font = BitmapFont.Parse(ArialHeader + Letters);

        Assert.Equal(-2, font.Kerning('A', 'V'));
        Assert.Equal(0, font.Kerning('V', 'A'));
        Assert.Equal(9 + 8 - 2, font.MeasureWidth("AV"));
        Assert.Equal(8 + 9, font.MeasureWidth("VA"));
        Assert.Equal(0, font.MeasureWidth(""));
        Assert.Equal(9 + 3 + 8, font.MeasureWidth("A V"));
    }

    [Fact]
    public void Missing_glyph_falls_back_to_question_mark()
    {
        var font = BitmapFont.Parse(ArialHeader + Letters);

        Assert.False(font.HasMissingGlyphs("AV?"));
        Assert.True(font.HasMissingGlyphs("A\u00fcV"));
        Assert.Equal(9 + 6 + 8, font.MeasureWidth("A\u00fcV"));
    }

    [Fact]
    public void Reads_two_pages_and_ignores_broken_lines()
    {
        var font = BitmapFont.Parse(
            "common lineHeight=20 base=16 pages=2\r\npage id=1 file=\"b.dds\"\r\npage id=0 file=\"a name.dds\"\r\n"
            + "char id=66 x=0 y=0 width=1 height=1 xoffset=0 yoffset=0 xadvance=5 page=1\r\nchar id=x\r\nchar id=67 xadvance=oops\r\n");

        Assert.Equal(new[] { "a name.dds", "b.dds" }, font.Pages);
        Assert.Equal(1, font.Glyph('B')!.Page);
        Assert.Null(font.Glyph('C'));
    }

    [Fact]
    public void Library_resolves_font_by_name()
    {
        Write("gfx/fonts/hoi_24header.fnt", ArialHeader + Letters.Replace("kernings count=1\n", string.Empty));
        Write("gfx/fonts/Arial.dds", "x");
        var gfx = new GuiSourceFile(
            Path.Combine(_temp, "interface/fonts.gfx"), "interface/fonts.gfx", GuiLayer.Vanilla, "Vanilla", 0, 0);
        const string text = "bitmapfonts = {\n\tbitmapfont = {\n\t\tname = \"hoi_24header\"\n\t\tpath = \"gfx/fonts/hoi_24header\"\n\t}\n"
            + "\tbitmapfont = {\n\t\tname = \"no_file\"\n\t\tpath = \"gfx/fonts/missing\"\n\t}\n}\n";
        var sprites = ClassicSpriteIndex.Build(new[] { gfx }, _ => GuiParser.Parse(text), spriteFirstWins: false, caseSensitive: true);
        var library = new ClassicFontLibrary(sprites, new ClassicTextureLocator(new[] { _temp }));

        var found = library.Get("hoi_24header")!.Value;

        Assert.Equal(14, found.Font.LineHeight);
        Assert.Equal(Path.Combine(_temp, "gfx", "fonts", "hoi_24header.fnt"), found.FntPath);
        Assert.Equal(new[] { Path.Combine(_temp, "gfx", "fonts", "Arial.dds") }, found.PageTexturePaths);
        Assert.Same(found.Font, library.Get("hoi_24header")!.Value.Font);
        Assert.Null(library.Get("unknown"));
        Assert.Null(library.Get("no_file"));
    }

    [Fact]
    public void Library_page_falls_back_from_tga_to_dds_and_reports_missing_page()
    {
        Write(
            "gfx/fonts/f.fnt",
            "common lineHeight=10 base=8\npage id=0 file=\"p.tga\"\npage id=1 file=\"gone.dds\"\n");
        Write("gfx/fonts/p.dds", "x");
        var gfx = new GuiSourceFile(Path.Combine(_temp, "a.gfx"), "interface/a.gfx", GuiLayer.Vanilla, "Vanilla", 0, 0);
        const string text = "bitmapfonts = { bitmapfont = { name = \"f\" path = \"gfx/fonts/f.fnt\" } }";
        var sprites = ClassicSpriteIndex.Build(new[] { gfx }, _ => GuiParser.Parse(text), false, true);
        var library = new ClassicFontLibrary(sprites, new ClassicTextureLocator(new[] { _temp }));

        var pages = library.Get("f")!.Value.PageTexturePaths;

        Assert.Equal(Path.Combine(_temp, "gfx", "fonts", "p.dds"), pages[0]);
        Assert.Null(pages[1]);
    }

    [Fact]
    public void Library_first_page_falls_back_to_texture_named_after_the_font()
    {
        // Wie hoi_18mbs.fnt (Seite "hoi_18mbs_0.dds" fehlt, daneben liegt hoi_18mbs.dds) und garamond_12.fnt (keine page-Zeile).
        Write("gfx/fonts/f.fnt", "common lineHeight=10 base=8\npage id=0 file=\"f_0.dds\"\n");
        Write("gfx/fonts/f.dds", "x");
        Write("gfx/fonts/g.fnt", "common lineHeight=10 base=8\n");
        Write("gfx/fonts/g.tga", "x");
        var gfx = new GuiSourceFile(Path.Combine(_temp, "a.gfx"), "interface/a.gfx", GuiLayer.Vanilla, "Vanilla", 0, 0);
        const string text = "bitmapfonts = { bitmapfont = { name = \"f\" path = \"gfx/fonts/f\" } bitmapfont = { name = \"g\" path = \"gfx/fonts/g\" } }";
        var sprites = ClassicSpriteIndex.Build(new[] { gfx }, _ => GuiParser.Parse(text), false, true);
        var library = new ClassicFontLibrary(sprites, new ClassicTextureLocator(new[] { _temp }));

        Assert.Equal(new[] { Path.Combine(_temp, "gfx", "fonts", "f.dds") }, library.Get("f")!.Value.PageTexturePaths);
        Assert.Equal(new[] { Path.Combine(_temp, "gfx", "fonts", "g.tga") }, library.Get("g")!.Value.PageTexturePaths);
    }

    [Fact]
    public void Assets_expose_fonts_and_refresh_them_when_a_gfx_is_saved()
    {
        Write("gfx/fonts/f.fnt", "common lineHeight=10 base=8\n");
        Write("gfx/fonts/g.fnt", "common lineHeight=12 base=9\n");
        var gfx = new GuiSourceFile(Path.Combine(_temp, "a.gfx"), "interface/a.gfx", GuiLayer.Mod, "Mod", 0, 0);
        const string before = "bitmapfonts = { bitmapfont = { name = \"f\" path = \"gfx/fonts/f\" } }";
        const string after = "bitmapfonts = { bitmapfont = { name = \"f\" path = \"gfx/fonts/g\" } }";
        var roots = new[] { new GuiSourceRoot(_temp, GuiLayer.Mod, "Mod") };
        var assets = ClassicAssets.Build(roots, new[] { gfx }, spriteFirstWins: false, caseSensitive: true, _ => GuiParser.Parse(before));

        Assert.Equal(10, assets.Fonts.Get("f")!.Value.Font.LineHeight);
        Assert.Equal(12, assets.WithGfxFile(gfx, after).Fonts.Get("f")!.Value.Font.LineHeight);
        Assert.Equal(10, assets.Fonts.Get("f")!.Value.Font.LineHeight);
    }

    [Fact]
    public void Junk_page_ids_are_ignored_and_never_throw()
    {
        var font = BitmapFont.Parse(
            "common lineHeight=10 base=8\npage id=2147483647 file=\"a.dds\"\npage id=100000000 file=\"b.dds\"\npage id=-1 file=\"c.dds\"\npage id=1 file=\"d.dds\"\n");

        Assert.Equal(new[] { string.Empty, "d.dds" }, font.Pages);
    }
}
