using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Sprite anlegen (klassische Spiele): der Plan für den <c>.gfx</c>-Eintrag und die Bildkopie; geschrieben wird hier nichts.</summary>
public sealed class ClassicSpriteDraftTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-spritedraft-" + Guid.NewGuid().ToString("N"));

    public ClassicSpriteDraftTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Vanilla => Path.Combine(_temp, "vanilla");

    private string ModRoot => Path.Combine(_temp, "mymod");

    [Fact]
    public void Suggests_name_and_frames()
    {
        Assert.Equal("GFX_My_Icon", ClassicSpriteDraft.SuggestName(Path.Combine(_temp, "My Icon.dds")));
        Assert.Equal("GFX_a_b_c", ClassicSpriteDraft.SuggestName(Path.Combine(_temp, "a-b.c.png")));
        Assert.Equal(3, ClassicSpriteDraft.SuggestFrames(120, 40));
        Assert.Equal(1, ClassicSpriteDraft.SuggestFrames(100, 40));
        Assert.Equal(1, ClassicSpriteDraft.SuggestFrames(40, 40));
        Assert.Equal(1, ClassicSpriteDraft.SuggestFrames(40, 0));
    }

    [Fact]
    public void Plans_new_gfx_file()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image), ModRoot, assets, roots, _ => null, "\n");

        Assert.True(plan.CanApply);
        var file = Assert.Single(plan.Files);
        Assert.True(file.IsNew);
        Assert.Equal(Path.Combine(ModRoot, "interface", "mymod.gfx"), file.FullPath);
        Assert.Null(file.EncodingName);
        Assert.Equal(
            "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_new\"\n\t\ttexturefile = \"gfx/interface/mymod/a.dds\"\n\t}\n}\n",
            file.NewText);
    }

    [Fact]
    public void Frames_are_written_only_when_more_than_one()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 120, 40);

        var plan = ClassicSpriteDraft.Plan(Request(image) with { Frames = 3 }, ModRoot, assets, roots, _ => null, "\n");

        Assert.Contains("\t\ttexturefile = \"gfx/interface/mymod/a.dds\"\n\t\tnoOfFrames = 3\n\t}", Assert.Single(plan.Files).NewText);
    }

    [Fact]
    public void New_file_uses_the_given_newline()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image), ModRoot, assets, roots, _ => null, "\r\n");

        var text = Assert.Single(plan.Files).NewText;
        Assert.StartsWith("spriteTypes = {\r\n\tspriteType = {\r\n", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty));
    }

    [Fact]
    public void Inserts_into_existing_spriteTypes()
    {
        var existing = "# Kopf\r\nSpriteTypes = {\r\n\tspriteType = {\r\n\t\tname = \"GFX_old\"\r\n\t\ttexturefile = \"gfx/interface/old.dds\"\r\n\t}\r\n}\r\n\r\n# Ende\r\n";
        var (assets, roots) = Setup(("mod", "interface/mymod.gfx", existing));
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(
            Request(image), ModRoot, assets, roots, p => p == Path.Combine(ModRoot, "interface", "mymod.gfx") ? existing : null, "\n");

        Assert.True(plan.CanApply);
        var file = Assert.Single(plan.Files);
        Assert.False(file.IsNew);
        Assert.Equal(existing, file.OldText);
        var entry = "\tspriteType = {\r\n\t\tname = \"GFX_new\"\r\n\t\ttexturefile = \"gfx/interface/mymod/a.dds\"\r\n\t}\r\n";
        Assert.Equal(existing.Insert(existing.IndexOf("\r\n}\r\n\r\n# Ende", StringComparison.Ordinal) + 2, entry), file.NewText);
    }

    [Fact]
    public void Appends_a_new_spriteTypes_block_when_the_file_has_none()
    {
        var existing = "# nur ein Kommentar";
        var (assets, roots) = Setup(("mod", "interface/mymod.gfx", existing));
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image), ModRoot, assets, roots, _ => existing, "\r\n");

        var file = Assert.Single(plan.Files);
        Assert.Equal(
            existing + "\n\nspriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_new\"\n\t\ttexturefile = \"gfx/interface/mymod/a.dds\"\n\t}\n}\n",
            file.NewText);
    }

    [Fact]
    public void Name_collision_blocks()
    {
        var (assets, roots) = Setup(("vanilla", "interface/core.gfx", Sprite("GFX_taken")));
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image) with { Name = "GFX_taken" }, ModRoot, assets, roots, _ => null, "\n");

        Assert.False(plan.CanApply);
        Assert.Single(plan.Problems);
        Assert.Empty(plan.Files);
    }

    [Fact]
    public void Case_only_collision_blocks()
    {
        var (assets, roots) = Setup(("vanilla", "interface/core.gfx", Sprite("GFX_Taken")));
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image) with { Name = "GFX_taken" }, ModRoot, assets, roots, _ => null, "\n");

        Assert.False(plan.CanApply);
        Assert.Single(plan.Problems);
    }

    [Theory]
    [InlineData("")]
    [InlineData("GFX bad")]
    [InlineData("GFX_ä")]
    public void Invalid_name_blocks(string name)
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image) with { Name = name }, ModRoot, assets, roots, _ => null, "\n");

        Assert.False(plan.CanApply);
    }

    [Fact]
    public void Unreadable_image_blocks()
    {
        var (assets, roots) = Setup();
        var broken = Path.Combine(_temp, "src", "broken.png");
        Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
        File.WriteAllBytes(broken, new byte[] { 1, 2, 3 });

        Assert.False(ClassicSpriteDraft.Plan(Request(broken), ModRoot, assets, roots, _ => null, "\n").CanApply);
        Assert.False(ClassicSpriteDraft.Plan(Request(Path.Combine(_temp, "src", "missing.dds")), ModRoot, assets, roots, _ => null, "\n").CanApply);
        Assert.False(ClassicSpriteDraft.Plan(Request(string.Empty), ModRoot, assets, roots, _ => null, "\n").CanApply);
    }

    [Fact]
    public void Png_header_is_read()
    {
        Assert.Null(ClassicTextureLocator.ReadSizeOfFile(Path.Combine(_temp, "nothing.png")));
        var png = Path.Combine(_temp, "ok.png");
        var bytes = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        bytes[11] = 13;
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        bytes[19] = 7;
        bytes[23] = 9;
        File.WriteAllBytes(png, bytes);

        Assert.Equal((7, 9), ClassicTextureLocator.ReadSizeOfFile(png));
    }

    [Fact]
    public void External_image_is_copied_under_mod_gfx()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image), ModRoot, assets, roots, _ => null, "\n");

        Assert.Equal(image, plan.CopyFrom);
        Assert.Equal(Path.Combine(ModRoot, "gfx", "interface", "mymod", "a.dds"), plan.CopyTo);
        Assert.Equal("gfx/interface/mymod/a.dds", plan.TexturePath);
    }

    [Fact]
    public void Existing_copy_target_blocks_unless_overwrite_is_chosen()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);
        Dds(Path.Combine(ModRoot, "gfx", "interface", "mymod", "a.dds"), 8, 8);

        Assert.False(ClassicSpriteDraft.Plan(Request(image), ModRoot, assets, roots, _ => null, "\n").CanApply);
        Assert.True(ClassicSpriteDraft.Plan(Request(image) with { OverwriteCopy = true }, ModRoot, assets, roots, _ => null, "\n").CanApply);
    }

    [Fact]
    public void Image_inside_vanilla_gfx_is_not_copied()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(Vanilla, "gfx", "interface", "v.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image), ModRoot, assets, roots, _ => null, "\n");

        Assert.True(plan.CanApply);
        Assert.Null(plan.CopyFrom);
        Assert.Null(plan.CopyTo);
        Assert.Equal("gfx/interface/v.dds", plan.TexturePath);
    }

    [Fact]
    public void Image_inside_mod_gfx_is_not_copied()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(ModRoot, "gfx", "mine", "m.dds"), 16, 16);

        var (path, copyTo) = ClassicSpriteDraft.TexturePathFor(image, ModRoot, roots);

        Assert.Equal("gfx/mine/m.dds", path);
        Assert.Null(copyTo);
    }

    [Fact]
    public void Default_gfx_path_avoids_vanilla_name()
    {
        var core = Path.Combine(_temp, "core");
        var files = new[]
        {
            new GuiSourceFile(Path.Combine(Vanilla, "interface", "core.gfx"), "interface/core.gfx", GuiLayer.Vanilla, "vanilla", 0, 0),
        };

        Assert.Equal("interface/core_sprites.gfx", ClassicSpriteDraft.DefaultGfxPath(core, files));
        Assert.Equal("interface/core.gfx", ClassicSpriteDraft.DefaultGfxPath(core, Array.Empty<GuiSourceFile>()));

        var more = files.Append(new GuiSourceFile(Path.Combine(Vanilla, "interface", "Core_Sprites.gfx"), "interface/Core_Sprites.gfx", GuiLayer.Dependency, "dep", 0, 0)).ToList();
        Assert.Equal("interface/core_sprites_2.gfx", ClassicSpriteDraft.DefaultGfxPath(core, more));

        // Eine eigene Datei des Mods zählt nicht: Sie wird ergänzt.
        var own = files.Append(new GuiSourceFile(Path.Combine(core, "interface", "core_sprites.gfx"), "interface/core_sprites.gfx", GuiLayer.Mod, "mod", 0, 0)).ToList();
        Assert.Equal("interface/core_sprites.gfx", ClassicSpriteDraft.DefaultGfxPath(core, own));
    }

    [Fact]
    public void Gfx_path_must_be_a_gfx_file_under_interface()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        foreach (var bad in new[] { "common/x.gfx", "interface/x.txt", "interface/.gfx", "interface/../x.gfx", "x.gfx", string.Empty })
        {
            Assert.False(ClassicSpriteDraft.Plan(Request(image) with { GfxRelativePath = bad }, ModRoot, assets, roots, _ => null, "\n").CanApply, bad);
        }

        Assert.True(ClassicSpriteDraft.Plan(Request(image) with { GfxRelativePath = "interface\\sub\\x.gfx" }, ModRoot, assets, roots, _ => null, "\n").CanApply);
    }

    [Fact]
    public void New_gfx_file_with_a_vanilla_path_blocks()
    {
        var (assets, roots) = Setup(("vanilla", "interface/core.gfx", Sprite("GFX_other")));
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image) with { GfxRelativePath = "interface/core.gfx" }, ModRoot, assets, roots, _ => null, "\n");

        Assert.False(plan.CanApply);
    }

    [Fact]
    public void Cornered_tile_writes_borderSize()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 32, 32);

        var plan = ClassicSpriteDraft.Plan(
            Request(image) with { Kind = ClassicSpriteKind.CorneredTile, BorderX = 8, BorderY = 6 }, ModRoot, assets, roots, _ => null, "\n");

        Assert.Equal(
            "spriteTypes = {\n\tcorneredTileSpriteType = {\n\t\tname = \"GFX_new\"\n\t\ttexturefile = \"gfx/interface/mymod/a.dds\"\n\t\tborderSize = { x = 8 y = 6 }\n\t}\n}\n",
            Assert.Single(plan.Files).NewText);
    }

    [Fact]
    public void Other_kinds_are_rejected()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ClassicSpriteDraft.Plan(Request(image) with { Kind = ClassicSpriteKind.Chart }, ModRoot, assets, roots, _ => null, "\n"));
    }

    [Fact]
    public void Planned_entry_is_found_by_the_sprite_index()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 16);

        var plan = ClassicSpriteDraft.Plan(Request(image) with { Kind = ClassicSpriteKind.CorneredTile, BorderX = 4, BorderY = 4 }, ModRoot, assets, roots, _ => null, "\n");

        var file = Assert.Single(plan.Files);
        var next = assets.WithGfxFile(new GuiSourceFile(file.FullPath, "interface/mymod.gfx", GuiLayer.Mod, "mod", 0, 0), file.NewText);
        var sprite = next.Sprites.Find("GFX_new").Sprite;
        Assert.NotNull(sprite);
        Assert.Equal(ClassicSpriteKind.CorneredTile, sprite!.Kind);
        Assert.Equal("gfx/interface/mymod/a.dds", sprite.TexturePath);
        Assert.Equal(4, sprite.BorderX);
    }

    [Fact]
    public void Copied_texture_found_by_next_preview_run()
    {
        var (assets, roots) = Setup();
        var image = Dds(Path.Combine(_temp, "src", "a.dds"), 16, 8);
        var plan = ClassicSpriteDraft.Plan(Request(image), ModRoot, assets, roots, _ => null, "\n");

        // Die Vorschau hat die Textur schon einmal vergeblich gesucht (Fehlschlag gemerkt).
        Assert.Null(assets.Textures.SizeOf(plan.TexturePath));

        Directory.CreateDirectory(Path.GetDirectoryName(plan.CopyTo!)!);
        File.Copy(plan.CopyFrom!, plan.CopyTo!);
        assets.Textures.NewRun();

        Assert.Equal(plan.CopyTo, assets.Textures.Locate(plan.TexturePath));
        Assert.Equal((16, 8), assets.Textures.SizeOf(plan.TexturePath));
    }

    private static ClassicSpriteRequest Request(string image) =>
        new(image, "GFX_new", ClassicSpriteKind.Sprite, 1, 0, 0, "interface/mymod.gfx", false);

    private static string Sprite(string name) =>
        $"spriteTypes = {{\n\tspriteType = {{\n\t\tname = \"{name}\"\n\t\ttexturefile = \"gfx/interface/x.dds\"\n\t}}\n}}\n";

    /// <summary>Wurzeln (Vanilla, Mod) und Assets; <paramref name="files"/>: (Ebene "vanilla"/"mod", relativer Pfad, Text), auf die Platte geschrieben.</summary>
    private (ClassicAssets Assets, IReadOnlyList<GuiSourceRoot> Roots) Setup(params (string Layer, string Relative, string Text)[] files)
    {
        Directory.CreateDirectory(Vanilla);
        Directory.CreateDirectory(ModRoot);
        var roots = new[]
        {
            new GuiSourceRoot(Vanilla, GuiLayer.Vanilla, "vanilla"),
            new GuiSourceRoot(ModRoot, GuiLayer.Mod, "mymod"),
        };
        var sources = new List<GuiSourceFile>();
        foreach (var (layer, relative, text) in files)
        {
            var root = layer == "mod" ? ModRoot : Vanilla;
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
            sources.Add(new GuiSourceFile(full, relative, layer == "mod" ? GuiLayer.Mod : GuiLayer.Vanilla, layer, text.Length, 0));
        }

        var assets = ClassicAssets.Build(roots, sources, spriteFirstWins: false, caseSensitive: false, f => GuiParser.Parse(File.ReadAllText(f.FullPath)));
        return (assets, roots);
    }

    private static string Dds(string path, int width, int height)
    {
        var header = new byte[128];
        "DDS "u8.CopyTo(header);
        BitConverter.GetBytes(124).CopyTo(header, 4);
        BitConverter.GetBytes(height).CopyTo(header, 12);
        BitConverter.GetBytes(width).CopyTo(header, 16);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, header);
        return path;
    }
}
