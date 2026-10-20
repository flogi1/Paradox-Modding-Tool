using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Sprites, Schriften und Texturen klassischer Spiele (HOI IV, EU IV) aus den <c>.gfx</c>-Dateien.</summary>
public sealed class ClassicSpriteIndexTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-classic-gfx-" + Guid.NewGuid().ToString("N"));

    private const string LogoSprite = "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_frontend_game_logo\"\n\t\ttexturefile = \"gfx/interface/logo.dds\"\n\t}\n}\n";

    private static readonly GuiLayout Hoi4 = new(
        Array.Empty<string>(), FirstWins: false, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic, CaseSensitive: true);

    private static readonly GuiLayout Eu4 = new(
        Array.Empty<string>(), FirstWins: true, ModsBeforeVanilla: false, Dialect: GuiDialect.Classic);

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private static ClassicSpriteIndex Build(bool spriteFirstWins, bool caseSensitive, params (GuiSourceFile File, string Text)[] files) =>
        ClassicSpriteIndex.Build(
            files.Select(f => f.File).ToList(),
            file => GuiParser.Parse(files.First(f => f.File == file).Text),
            spriteFirstWins,
            caseSensitive);

    private static (GuiSourceFile, string) Gfx(string path, GuiLayer layer, string text = LogoSprite) => (GuiIndexTests.F(path, layer), text);

    private string Write(string relative, byte[] bytes)
    {
        var path = Path.Combine(_temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string Write(string relative, string text) => Write(relative, System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public void Reads_all_sprite_kinds()
    {
        const string text =
            "spriteTypes = {\n"
            + "\tcorneredTileSpriteType = {\n\t\tname = \"GFX_tiled_window\"\n\t\tsize= { x=192 y=192 }\n\t\ttextureFile = \"gfx//interface//tiles//tiled_window.dds\"\n\t\tborderSize = { x=64 y=48 }\n\t\ttilingCenter = yes\n\t\teffectFile = \"gfx/FX/buttonstate_nodowneffect.lua\"\n\t}\n"
            + "\tspriteType = {\n\t\tname = \"GFX_global_alert_icons\"\n\t\ttexturefile = \"gfx/interface/alerts/global_alert_icons.dds\"\n\t\tnoOfFrames = 70\n\t}\n"
            + "\tprogressbartype = {\n\t\tname = \"GFX_ability_active_bg\"\n\t\ttextureFile1 = \"gfx/interface/abilitylist/ability_active_bg_full.dds\"\n\t\ttextureFile2 = \"gfx/interface/abilitylist/ability_active_bg.dds\"\n\t}\n"
            + "\ttextSpriteType = {\n\t\tname = \"GFX_sort_button_83x29\"\n\t\ttexturefile = \"gfx//interface//sort_button_83x29.tga\"\n\t\tnoOfFrames = 2\n\t}\n"
            + "\tframeAnimatedSpriteType = {\n\t\tname = \"GFX_anim\"\n\t\ttexturefile = \"gfx/interface/anim.dds\"\n\t}\n"
            + "\tmaskedShieldType = {\n\t\tname = \"GFX_shield\"\n\t\ttextureFile1 = \"gfx/interface/shield.dds\"\n\t}\n"
            + "\tPieChartType = {\n\t\tname = \"GFX_pie\"\n\t\tsize = 128\n\t}\n"
            + "\tLineChartType = {\n\t\tname = \"GFX_line\"\n\t}\n"
            + "\tcircularProgressBarType = {\n\t\tname = \"GFX_circle\"\n\t}\n"
            + "\tunknownThing = {\n\t\tname = \"GFX_ignored\"\n\t}\n"
            + "}\n";
        var index = Build(false, true, Gfx("interface/a.gfx", GuiLayer.Vanilla, text));

        var window = index.Find("GFX_tiled_window").Sprite!;
        Assert.Equal(ClassicSpriteKind.CorneredTile, window.Kind);
        Assert.Equal(64, window.BorderX);
        Assert.Equal(48, window.BorderY);
        Assert.True(window.TilingCenter);
        Assert.Equal("gfx//interface//tiles//tiled_window.dds", window.TexturePath);
        Assert.Equal(1, window.Frames);

        var icons = index.Find("GFX_global_alert_icons").Sprite!;
        Assert.Equal(ClassicSpriteKind.Sprite, icons.Kind);
        Assert.Equal(70, icons.Frames);
        Assert.Equal("gfx/interface/alerts/global_alert_icons.dds", icons.TexturePath);
        Assert.False(icons.TilingCenter);
        Assert.Equal(0, icons.BorderX);

        Assert.Equal(ClassicSpriteKind.ProgressBar, index.Find("GFX_ability_active_bg").Sprite!.Kind);
        Assert.Equal("gfx/interface/abilitylist/ability_active_bg_full.dds", index.Find("GFX_ability_active_bg").Sprite!.TexturePath);
        Assert.Equal(ClassicSpriteKind.TextSprite, index.Find("GFX_sort_button_83x29").Sprite!.Kind);
        Assert.Equal(2, index.Find("GFX_sort_button_83x29").Sprite!.Frames);
        Assert.Equal(ClassicSpriteKind.FrameAnimated, index.Find("GFX_anim").Sprite!.Kind);
        Assert.Equal(ClassicSpriteKind.MaskedShield, index.Find("GFX_shield").Sprite!.Kind);
        Assert.Equal(ClassicSpriteKind.Chart, index.Find("GFX_pie").Sprite!.Kind);
        Assert.Null(index.Find("GFX_pie").Sprite!.TexturePath);
        Assert.Equal(ClassicSpriteKind.Chart, index.Find("GFX_line").Sprite!.Kind);
        Assert.Equal(ClassicSpriteKind.Chart, index.Find("GFX_circle").Sprite!.Kind);
        Assert.Null(index.Find("GFX_ignored").Sprite);
        Assert.Equal(9, index.Sprites.Count);
    }

    [Fact]
    public void Block_keys_and_property_names_ignore_case()
    {
        var index = Build(
            false, true, Gfx("interface/a.gfx", GuiLayer.Vanilla, "SpriteTypes = {\n\tSPRITETYPE = {\n\t\tName = \"GFX_x\"\n\t\tTextureFile = \"gfx/x.dds\"\n\t\tNOOFFRAMES = 3\n\t}\n}\n"));

        var sprite = index.Find("GFX_x").Sprite!;
        Assert.Equal("gfx/x.dds", sprite.TexturePath);
        Assert.Equal(3, sprite.Frames);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Last_sprite_wins_by_load_order(bool caseSensitive)
    {
        // HOI IV (Groß-/Kleinschreibung zählt) und EU IV (Sprites: zuletzt geladen gewinnt): die Datei im Unterordner lädt zuletzt.
        var index = Build(
            false,
            caseSensitive,
            Gfx("interface/00_pmt_order_a.gfx", GuiLayer.Mod),
            Gfx("interface/zz_pmt_order_b.gfx", GuiLayer.Mod),
            Gfx("interface/frontendmainview.gfx", GuiLayer.Vanilla),
            Gfx("interface/pmt_sub/000_pmt_sub_a.gfx", GuiLayer.Mod));

        Assert.Equal("interface/pmt_sub/000_pmt_sub_a.gfx", index.Find("GFX_frontend_game_logo").Sprite!.File.RelativePath);
        Assert.Equal(4, index.FindAll("GFX_frontend_game_logo").Count);
        Assert.Equal("interface/pmt_sub/000_pmt_sub_a.gfx", index.FindAll("GFX_frontend_game_logo")[^1].File.RelativePath);
    }

    [Fact]
    public void First_sprite_wins_when_the_game_says_so()
    {
        var index = Build(
            true,
            false,
            Gfx("interface/frontendmainview.gfx", GuiLayer.Vanilla),
            Gfx("interface/00_pmt_order_a.gfx", GuiLayer.Mod),
            Gfx("interface/pmt_sub/000_pmt_sub_a.gfx", GuiLayer.Mod));

        Assert.Equal("interface/00_pmt_order_a.gfx", index.Find("GFX_frontend_game_logo").Sprite!.File.RelativePath);
    }

    [Fact]
    public void Same_path_higher_layer_wins_for_last_wins()
    {
        var index = Build(false, true, Gfx("interface/a.gfx", GuiLayer.Mod), Gfx("interface/a.gfx", GuiLayer.Vanilla));

        Assert.Equal(GuiLayer.Mod, index.Find("GFX_frontend_game_logo").Sprite!.File.Layer);
    }

    [Fact]
    public void Case_mismatch_matches_with_note()
    {
        var index = Build(false, true, Gfx("interface/a.gfx", GuiLayer.Vanilla, "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_x\"\n\t}\n}\n"));

        var (sprite, mismatch) = index.Find("gfx_x");
        Assert.Equal("GFX_x", sprite!.Name);
        Assert.True(mismatch);

        var (exact, exactMismatch) = index.Find("GFX_x");
        Assert.NotNull(exact);
        Assert.False(exactMismatch);
        Assert.Equal((null, false), index.Find("GFX_missing"));
    }

    [Fact]
    public void Exact_name_beats_case_insensitive_one()
    {
        var index = Build(
            false,
            true,
            Gfx("interface/a.gfx", GuiLayer.Vanilla, "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_x\"\n\t}\n}\n"),
            Gfx("interface/b.gfx", GuiLayer.Vanilla, "spriteTypes = {\n\tspriteType = {\n\t\tname = \"gfx_x\"\n\t}\n}\n"));

        Assert.Equal("interface/a.gfx", index.Find("GFX_x").Sprite!.File.RelativePath);
        Assert.Equal("interface/b.gfx", index.Find("gfx_x").Sprite!.File.RelativePath);
    }

    [Fact]
    public void Reads_bitmapfont_path_and_fontfiles()
    {
        const string text =
            "bitmapfonts = {\n"
            + "\tbitmapfont = {\n\t\tname = \"standard_font\"\n\t\tpath = \"gfx/fonts/standard\"\n\t\tcolor = 0xffffffff\n\t}\n"
            + "\tbitmapfont = {\n\t\tname = \"ToolTip_Font\"\n\t\tfontfiles = {\n\t\t\t\"gfx/fonts/garamond_16.fnt\"\n\t\t\t\"gfx/fonts/garamond_16b\"\n\t\t}\n\t}\n"
            + "\tbitmapfont_override = {\n\t\tname = \"standard_font\"\n\t\tfontfiles = { \"gfx/fonts/chinese/standard_1of4\" }\n\t\tlanguages = { \"l_simp_chinese\" }\n\t}\n"
            + "}\n";
        var index = Build(false, true, Gfx("interface/fonts.gfx", GuiLayer.Vanilla, text));

        Assert.Equal("gfx/fonts/standard", index.Font("standard_font")!.FontPath);
        Assert.Equal("gfx/fonts/garamond_16", index.Font("ToolTip_Font")!.FontPath);
        Assert.Null(index.Font("unknown"));
        Assert.Equal(2, index.Fonts.Count);
    }

    [Fact]
    public void Locate_normalizes_double_slashes_and_falls_back_to_dds()
    {
        var dds = Write(@"root\gfx\interface\a.dds", new byte[4]);
        var locator = new ClassicTextureLocator(new[] { Path.Combine(_temp, "root") });

        Assert.Equal(dds, locator.Locate("gfx//interface//a.tga"));
        Assert.Equal(dds, locator.Locate(@"\gfx\\interface/a.dds"));
        Assert.Equal(dds, locator.Locate("/gfx/interface/a.dds"));
    }

    [Fact]
    public void Locate_searches_roots_in_the_given_order()
    {
        var first = Write(@"mod\gfx\a.dds", new byte[4]);
        Write(@"vanilla\gfx\a.dds", new byte[4]);
        var locator = new ClassicTextureLocator(new[] { Path.Combine(_temp, "mod"), Path.Combine(_temp, "vanilla") });

        Assert.Equal(first, locator.Locate("gfx/a.dds"));
    }

    [Fact]
    public void Missing_texture_resolves_to_null_not_exception()
    {
        var locator = new ClassicTextureLocator(new[] { Path.Combine(_temp, "root") });

        Assert.Null(locator.Locate("gfx/interface/missing.dds"));
        Assert.Null(locator.SizeOf("gfx/interface/missing.dds"));
        Assert.Null(locator.Locate(string.Empty));
        Assert.Null(locator.Locate("gfx/../../secret.dds"));
        Assert.Null(locator.Locate("gfx/c:/x.dds"));
        Assert.Null(locator.Locate("gfx/bad|name?.dds"));
    }

    [Fact]
    public void SizeOf_reads_tga_and_png_headers()
    {
        var tga = new byte[18];
        tga[2] = 2;
        tga[12] = 3;
        tga[14] = 2;
        Write(@"root\gfx\a.tga", tga);
        var png = new byte[]
        {
            0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A,
            0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
            0, 0, 0, 3, 0, 0, 0, 2,
        };
        Write(@"root\gfx\b.png", png);
        var dds = new byte[128];
        dds[0] = (byte)'D';
        dds[1] = (byte)'D';
        dds[2] = (byte)'S';
        dds[3] = (byte)' ';
        dds[12] = 2;
        dds[16] = 3;
        Write(@"root\gfx\c.dds", dds);
        Write(@"root\gfx\broken.png", new byte[] { 1, 2, 3 });
        var locator = new ClassicTextureLocator(new[] { Path.Combine(_temp, "root") });

        Assert.Equal((3, 2), locator.SizeOf("gfx/a.tga"));
        Assert.Equal((3, 2), locator.SizeOf("gfx//a.tga"));
        Assert.Equal((3, 2), locator.SizeOf("gfx/b.png"));
        Assert.Equal((3, 2), locator.SizeOf("gfx/c.dds"));
        Assert.Null(locator.SizeOf("gfx/broken.png"));
        // Gemerkt: ein zweiter Aufruf liefert dasselbe.
        Assert.Equal((3, 2), locator.SizeOf("gfx/a.tga"));
    }

    [Fact]
    public void Assets_search_textures_in_mod_then_dependencies_then_vanilla_reversed()
    {
        var roots = new[]
        {
            new GuiSourceRoot(Path.Combine(_temp, "base"), GuiLayer.Vanilla, "vanilla"),
            new GuiSourceRoot(Path.Combine(_temp, "dlc"), GuiLayer.Vanilla, "dlc"),
            new GuiSourceRoot(Path.Combine(_temp, "dep"), GuiLayer.Dependency, "dep"),
            new GuiSourceRoot(Path.Combine(_temp, "mod"), GuiLayer.Mod, "mod"),
        };

        var assets = ClassicAssets.Build(roots, Array.Empty<GuiSourceFile>(), false, true);

        Assert.Equal(
            new[] { "mod", "dep", "dlc", "base" }.Select(n => Path.Combine(_temp, n)),
            assets.Textures.Roots);
    }

    private static readonly GuiLayout WorkspaceLayout = Hoi4 with { VanillaRoots = new[] { string.Empty } };

    private static readonly GuiRules Rules = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), WorkspaceLayout);

    [Fact]
    public void Workspace_builds_classic_assets_and_a_saved_gfx_replaces_the_file()
    {
        var install = Path.Combine(_temp, "install");
        var mod = Path.Combine(_temp, "mod");
        Write(@"install\interface\a.gui", "guiTypes = { }");
        Write(@"install\interface\a.gfx", LogoSprite);
        var modGfx = Write(@"mod\interface\m.gfx", "spriteTypes = { }");
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(install, mod, Array.Empty<string>(), Rules, null));

        Assert.NotNull(workspace.Classic);
        Assert.Equal("interface/a.gfx", workspace.Classic!.Sprites.Find("GFX_frontend_game_logo").Sprite!.File.RelativePath);
        Assert.Equal(new[] { mod, install }, workspace.Classic.Textures.Roots);

        const string added = "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_frontend_game_logo\"\n\t\ttexturefile = \"gfx/mod.dds\"\n\t}\n}\n";
        File.WriteAllText(modGfx, added);
        var saved = workspace.WithModFile(modGfx, added);

        var winner = saved.Classic!.Sprites.Find("GFX_frontend_game_logo").Sprite!;
        Assert.Equal("interface/m.gfx", winner.File.RelativePath);
        Assert.Equal("gfx/mod.dds", winner.TexturePath);
        Assert.Equal(2, saved.ClassicGfxFiles.Count);
        Assert.Equal(new FileInfo(modGfx).Length, saved.ClassicGfxFiles.Single(f => f.Layer == GuiLayer.Mod).Size);
        // Das alte Workspace-Objekt bleibt unverändert, Index und Katalog sind übernommen.
        Assert.Equal("interface/a.gfx", workspace.Classic.Sprites.Find("GFX_frontend_game_logo").Sprite!.File.RelativePath);
        Assert.Same(workspace.Index, saved.Index);

        // Ein zweites Speichern ersetzt den Eintrag, statt ihn zu verdoppeln.
        var again = saved.WithModFile(modGfx, "spriteTypes = { }");
        Assert.Equal(2, again.ClassicGfxFiles.Count);
        Assert.Equal("interface/a.gfx", again.Classic!.Sprites.Find("GFX_frontend_game_logo").Sprite!.File.RelativePath);
    }

    [Fact]
    public void Saved_gfx_outside_interface_or_in_vanilla_changes_nothing()
    {
        var install = Path.Combine(_temp, "install");
        Write(@"install\interface\a.gui", "guiTypes = { }");
        var vanillaGfx = Write(@"install\interface\a.gfx", LogoSprite);
        Write(@"mod\interface\m.gui", "guiTypes = { }");
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(install, Path.Combine(_temp, "mod"), Array.Empty<string>(), Rules, null));

        Assert.Same(workspace, workspace.WithModFile(Path.Combine(_temp, "mod", "other", "x.gfx"), LogoSprite));
        Assert.Same(workspace, workspace.WithModFile(vanillaGfx, LogoSprite));
    }

    [Fact]
    public void Jomini_workspace_has_no_classic_assets_and_ignores_gfx_saves()
    {
        Write(@"install\game\in_game\gui\base.gui", "types V { type v = icon { } }");
        var modGfx = Path.Combine(_temp, "mod", "interface", "m.gfx");
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Path.Combine(_temp, "install"), Path.Combine(_temp, "mod"), Array.Empty<string>(), GuiResolverTests.Rules, null));

        Assert.Null(workspace.Classic);
        Assert.Same(workspace, workspace.WithModFile(modGfx, LogoSprite));
    }

    [Fact]
    public void Create_reuses_unchanged_gfx_files_from_the_previous_workspace()
    {
        var install = Path.Combine(_temp, "install");
        Write(@"install\interface\a.gui", "guiTypes = { }");
        var gfx = Write(@"install\interface\a.gfx", LogoSprite);
        var options = new GuiWorkspaceOptions(install, null, Array.Empty<string>(), Rules, null);
        var first = GuiWorkspace.Create(options);

        // Gleiche Größe und Zeit: Die Datei gilt als unverändert und wird nicht neu gelesen.
        var second = GuiWorkspace.Create(options, first);

        Assert.Same(first.Classic!.Sprites.Sprites[0].Node, second.Classic!.Sprites.Sprites[0].Node);

        File.WriteAllText(gfx, LogoSprite.Replace("logo.dds", "other.dds"));
        var third = GuiWorkspace.Create(options, second);
        Assert.Equal("gfx/interface/other.dds", third.Classic!.Sprites.Find("GFX_frontend_game_logo").Sprite!.TexturePath);
    }

    [Fact]
    public void Saving_a_gfx_keeps_load_order_of_same_path_in_two_dependencies()
    {
        // Zwei Abhängigkeiten mit demselben relativen Pfad: Die spätere gewinnt. Speichern der ersten ändert daran nichts.
        var install = Path.Combine(_temp, "install");
        Write(@"install\interface\a.gui", "guiTypes = { }");
        Write(@"install\interface\a.gfx", LogoSprite);
        var depA = Path.Combine(_temp, "depA");
        var depB = Path.Combine(_temp, "depB");
        var gfxA = Write(@"depA\interface\x.gfx", LogoSprite.Replace("logo.dds", "a.dds"));
        Write(@"depB\interface\x.gfx", LogoSprite.Replace("logo.dds", "b.dds"));
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(install, null, new[] { depA, depB }, Rules, null));
        Assert.Equal("gfx/interface/b.dds", workspace.Classic!.Sprites.Find("GFX_frontend_game_logo").Sprite!.TexturePath);

        var text = LogoSprite.Replace("logo.dds", "a2.dds");
        File.WriteAllText(gfxA, text);
        var saved = workspace.WithModFile(gfxA, text);

        Assert.Equal("gfx/interface/b.dds", saved.Classic!.Sprites.Find("GFX_frontend_game_logo").Sprite!.TexturePath);
        Assert.Equal(
            new[] { Path.Combine(install, "interface", "a.gfx"), gfxA, Path.Combine(depB, "interface", "x.gfx") },
            saved.ClassicGfxFiles.Select(f => f.FullPath));
        Assert.Equal(
            new[] { "gfx/interface/logo.dds", "gfx/interface/a2.dds", "gfx/interface/b.dds" },
            saved.Classic.Sprites.FindAll("GFX_frontend_game_logo").Select(x => x.TexturePath));
    }

    [Fact]
    public void Saving_a_new_mod_gfx_is_inserted_after_dependencies_not_before_vanilla()
    {
        var install = Path.Combine(_temp, "install");
        var mod = Path.Combine(_temp, "mod");
        Write(@"install\interface\a.gui", "guiTypes = { }");
        Write(@"install\interface\a.gfx", LogoSprite);
        Write(@"dep\interface\d.gfx", LogoSprite);
        Directory.CreateDirectory(Path.Combine(mod, "interface"));
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(install, mod, new[] { Path.Combine(_temp, "dep") }, Rules, null));

        var saved = workspace.WithModFile(Path.Combine(mod, "interface", "n.gfx"), LogoSprite);

        Assert.Equal(new[] { GuiLayer.Vanilla, GuiLayer.Dependency, GuiLayer.Mod }, saved.ClassicGfxFiles.Select(f => f.Layer));
    }
}
