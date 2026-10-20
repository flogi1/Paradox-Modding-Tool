using System.Buffers.Binary;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Der gemeinsame Texturfinder in seiner Jomini-Form: DLC-Wurzeln in der Suchreihenfolge, gemerkte Köpfe je Lauf, Größen aus DDS, PNG, TGA und JPG,
/// Erkennung des Bildtyps an den ersten Byte und Formatnamen für nicht dekodierbare DDS.
/// </summary>
public sealed class GuiTextureLocatorTests : IDisposable
{
    private static readonly string[] EuContexts = { "in_game", "main_menu", "loading_screen" };

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-texloc-" + Guid.NewGuid().ToString("N"));

    public GuiTextureLocatorTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Install => Path.Combine(_temp, "install");

    private string Mod => Path.Combine(_temp, "mod");

    private string Game => Path.Combine(Install, "game", "in_game");

    [Fact]
    public void Dlc_textures_are_found_after_the_mod_and_before_the_game()
    {
        var dlcFile = Touch(Path.Combine(Install, "game", "dlc", "d1", "in_game", "gfx", "a.dds"), 4, 4);
        Touch(Path.Combine(Game, "gfx", "a.dds"), 8, 8);
        var dlcOnly = Touch(Path.Combine(Install, "game", "dlc", "d1", "in_game", "gfx", "b.dds"), 4, 4);
        var roots = GuiTextureLocator.ContextRootsFor(Install, Array.Empty<string>(), Mod, EuContexts);

        Assert.Equal(dlcFile, new GuiTextureLocator(roots).Locate("gfx/a.dds"));
        Assert.Equal(dlcOnly, new GuiTextureLocator(roots).Locate("/gfx/b.dds"));

        // Die Mod schlägt das DLC.
        var modFile = Touch(Path.Combine(Mod, "in_game", "gfx", "a.dds"), 2, 2);
        roots = GuiTextureLocator.ContextRootsFor(Install, Array.Empty<string>(), Mod, EuContexts);
        Assert.Equal(modFile, new GuiTextureLocator(roots).Locate("gfx/a.dds"));
    }

    [Fact]
    public void Later_dlc_beats_earlier_dlc()
    {
        Touch(Path.Combine(Install, "game", "dlc", "d1", "in_game", "gfx", "a.dds"), 4, 4);
        var later = Touch(Path.Combine(Install, "game", "dlc", "d2", "in_game", "gfx", "a.dds"), 6, 6);
        var roots = GuiTextureLocator.ContextRootsFor(Install, Array.Empty<string>(), null, EuContexts);

        Assert.Equal(later, new GuiTextureLocator(roots).Locate("gfx/a.dds"));
        Assert.Equal((6, 6), new GuiTextureLocator(roots).SizeOf("gfx/a.dds"));
    }

    [Fact]
    public void Flat_game_searches_dlc_without_contexts()
    {
        var dlcFile = Touch(Path.Combine(Install, "game", "dlc", "d1", "gfx", "a.dds"), 4, 4);
        Touch(Path.Combine(Install, "game", "gfx", "a.dds"), 8, 8);
        var roots = GuiTextureLocator.ContextRootsFor(Install, Array.Empty<string>(), null, Array.Empty<string>());

        Assert.Equal(dlcFile, new GuiTextureLocator(roots).Locate("gfx/a.dds"));
    }

    [Fact]
    public void Texture_added_after_a_miss_is_found_on_the_next_run()
    {
        var locator = new GuiTextureLocator(Roots());
        Assert.Null(locator.Locate("gfx/new.dds"));
        var created = Touch(Path.Combine(Game, "gfx", "new.dds"), 8, 8);

        Assert.Null(locator.Locate("gfx/new.dds")); // selber Lauf: gemerkter Fehlschlag
        locator.NewRun();

        Assert.Equal(created, locator.Locate("gfx/new.dds"));
    }

    [Fact]
    public void Dotdot_never_leaves_a_root()
    {
        Touch(Path.Combine(Game, "secret.dds"), 8, 8);
        var locator = new GuiTextureLocator(Roots());

        Assert.Null(locator.Locate("gfx/../../secret.dds"));
        Assert.Null(locator.Locate("../secret.dds"));
    }

    [Fact]
    public void Tga_does_not_fall_back_to_dds_in_jomini()
    {
        Touch(Path.Combine(Game, "gfx", "x.dds"), 8, 8);

        Assert.Null(new GuiTextureLocator(Roots()).Locate("gfx/x.tga"));
    }

    [Fact]
    public void Png_tga_and_jpg_sizes_come_from_the_header()
    {
        WriteJpg(Path.Combine(Game, "gfx", "photo.jpg"), 37, 21);
        WritePng(Path.Combine(Game, "gfx", "icon.png"), 5, 3);
        var tga = new byte[18 + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(tga.AsSpan(12), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(tga.AsSpan(14), 9);
        File.WriteAllBytes(Path.Combine(Game, "gfx", "pic.tga"), tga);
        var locator = new GuiTextureLocator(Roots());

        Assert.Equal((37, 21), locator.SizeOf("gfx/photo.jpg"));
        Assert.Equal((5, 3), locator.SizeOf("gfx/icon.png"));
        Assert.Equal((12, 9), locator.SizeOf("gfx/pic.tga"));
        Assert.Equal((37, 21), TextureLocator.ReadSizeOfFile(Path.Combine(Game, "gfx", "photo.jpg")));
        Assert.Equal(new TextureHeader(5, 3, null), locator.HeaderOf("gfx/icon.png"));
    }

    [Fact]
    public void Jpg_size_is_found_behind_other_segments()
    {
        // APP0, ein Kommentar, dann SOF2 (progressiv) mit 40 x 30.
        var bytes = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x4A, 0x46, 0xFF, 0xFE, 0x00, 0x03, 0x41 };
        bytes.AddRange(new byte[] { 0xFF, 0xC2, 0x00, 0x0B, 0x08, 0x00, 30, 0x00, 40, 0x01, 0x01, 0x11, 0x00 });
        var path = Path.Combine(Game, "gfx", "prog.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes.ToArray());

        Assert.Equal((40, 30), TextureLocator.ReadSizeOfFile(path));
    }

    [Fact]
    public void Broken_jpg_has_no_header()
    {
        var path = Path.Combine(Game, "gfx", "broken.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x20, 1, 2, 3 });

        Assert.Null(TextureLocator.ReadHeader(path));
        Assert.Null(new GuiTextureLocator(Roots()).SizeOf("gfx/broken.jpg"));
    }

    [Fact]
    public void Png_named_dds_is_read_by_its_magic()
    {
        WritePng(Path.Combine(Game, "gfx", "a.dds"), 5, 3);

        Assert.Equal(new TextureHeader(5, 3, null), new GuiTextureLocator(Roots()).HeaderOf("gfx/a.dds"));
    }

    [Fact]
    public void Empty_file_is_an_unreadable_header()
    {
        var path = Path.Combine(Game, "gfx", "empty.dds");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Array.Empty<byte>());
        var locator = new GuiTextureLocator(Roots());

        Assert.Equal(new TextureHeader(0, 0, GuiTexts.TextureFormatEmptyFile), locator.HeaderOf("gfx/empty.dds"));
        Assert.Null(locator.SizeOf("gfx/empty.dds"));
    }

    [Fact]
    public void Unsupported_dds_names_its_format()
    {
        var dx10 = new byte[148];
        WriteDdsHeader(dx10, 8, 8);
        "DX10"u8.CopyTo(dx10.AsSpan(84));
        BinaryPrimitives.WriteUInt32LittleEndian(dx10.AsSpan(128), 83);
        WriteFile(Path.Combine(Game, "gfx", "bc5.dds"), dx10);

        var ati2 = new byte[128];
        WriteDdsHeader(ati2, 8, 8);
        "ATI2"u8.CopyTo(ati2.AsSpan(84));
        WriteFile(Path.Combine(Game, "gfx", "ati2.dds"), ati2);

        var locator = new GuiTextureLocator(Roots());
        Assert.Equal(new TextureHeader(8, 8, "DDS DXGI 83 (BC5)"), locator.HeaderOf("gfx/bc5.dds"));
        Assert.Equal(new TextureHeader(8, 8, "DDS FourCC ATI2"), locator.HeaderOf("gfx/ati2.dds"));
        Assert.Equal((8, 8), locator.SizeOf("gfx/bc5.dds"));
    }

    [Theory]
    [InlineData("DXT1")]
    [InlineData("DXT3")]
    [InlineData("DXT5")]
    [InlineData("RXGB")]
    public void Decodable_fourcc_has_no_unsupported_format(string fourCc)
    {
        var bytes = new byte[128];
        WriteDdsHeader(bytes, 8, 8);
        System.Text.Encoding.ASCII.GetBytes(fourCc).CopyTo(bytes, 84);

        Assert.Null(GuiImageDecoder.DescribeUnsupported(bytes));
    }

    [Theory]
    [InlineData(28)]
    [InlineData(29)]
    [InlineData(87)]
    [InlineData(91)]
    public void Decodable_dx10_has_no_unsupported_format(uint dxgi)
    {
        var bytes = new byte[148];
        WriteDdsHeader(bytes, 8, 8);
        "DX10"u8.CopyTo(bytes.AsSpan(84));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(128), dxgi);

        Assert.Null(GuiImageDecoder.DescribeUnsupported(bytes));
    }

    [Fact]
    public void Masked_rgb_has_no_unsupported_format_unless_the_masks_are_missing()
    {
        var bytes = new byte[128];
        WriteDdsHeader(bytes, 8, 8);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), 0x41);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(88), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(92), 0x00FF0000);
        Assert.Null(GuiImageDecoder.DescribeUnsupported(bytes));

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(92), 0);
        Assert.NotNull(GuiImageDecoder.DescribeUnsupported(bytes));
    }

    [Fact]
    public void Header_is_read_once_per_run()
    {
        var path = Touch(Path.Combine(Game, "gfx", "once.dds"), 16, 8);
        var locator = new GuiTextureLocator(Roots());
        Assert.Equal(new TextureHeader(16, 8, null), locator.HeaderOf("gfx/once.dds"));

        File.Delete(path);
        Assert.Equal(new TextureHeader(16, 8, null), locator.HeaderOf("gfx/once.dds")); // selber Lauf: gemerkt

        locator.NewRun();
        Assert.Null(locator.HeaderOf("gfx/once.dds"));
    }

    [Fact]
    public void Edited_texture_gets_its_new_header_on_the_next_run()
    {
        var path = Touch(Path.Combine(Game, "gfx", "edit.dds"), 16, 8);
        var locator = new GuiTextureLocator(Roots());
        Assert.Equal((16, 8), locator.SizeOf("gfx/edit.dds"));

        Touch(path, 30, 40);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
        locator.NewRun();

        Assert.Equal((30, 40), locator.SizeOf("gfx/edit.dds"));
    }

    [Fact]
    public void Workspace_layout_engine_starts_a_new_texture_run()
    {
        WriteText(Path.Combine(Install, "game", "in_game", "gui", "base.gui"), "types V { type v = icon { } }");
        var options = new GuiWorkspaceOptions(Install, Mod, Array.Empty<string>(), GuiResolverTests.Rules, Path.Combine(_temp, "cache", "catalog.json"), false);
        var workspace = GuiWorkspace.Create(options);
        Assert.Null(workspace.Classic);

        var before = workspace.Textures.Generation;
        _ = workspace.CreateLayoutEngine();
        Assert.Equal(before + 1, workspace.Textures.Generation);
        _ = workspace.CreateLayoutEngine();
        Assert.Equal(before + 2, workspace.Textures.Generation);
    }

    private IReadOnlyList<string> Roots() =>
        new[] { Game }.Select(r => { Directory.CreateDirectory(r); return r; }).ToList();

    /// <summary>Schreibt einen 128-Byte-DDS-Kopf (DXT5) der Größe <paramref name="width"/> x <paramref name="height"/>.</summary>
    private static string Touch(string path, int width, int height)
    {
        var bytes = new byte[128];
        WriteDdsHeader(bytes, width, height);
        "DXT5"u8.CopyTo(bytes.AsSpan(84));
        WriteFile(path, bytes);
        return path;
    }

    private static void WriteDdsHeader(byte[] bytes, int width, int height)
    {
        "DDS "u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), height);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), 0x4);
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void WritePng(string path, int width, int height) => WriteFile(path, Encode(new PngBitmapEncoder(), width, height));

    private static void WriteJpg(string path, int width, int height) => WriteFile(path, Encode(new JpegBitmapEncoder(), width, height));

    private static byte[] Encode(BitmapEncoder encoder, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, (byte)200);
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
