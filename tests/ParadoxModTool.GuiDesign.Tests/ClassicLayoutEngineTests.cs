using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Testumgebung der klassischen Vorschau: ein Ordner mit DDS-Köpfen (nur Größe), einer <c>.fnt</c> und einer yml; die <c>.gfx</c> kommt als Text.
/// Gelegt wird mit den Regeln aus dem echten Profil (hoi4.json bzw. eu4.json).
/// </summary>
internal sealed class ClassicLayoutKit : IDisposable
{
    public const string DefaultGfx =
        "spriteTypes = {\n"
        + "\tspriteType = { name = \"GFX_global_alert_icons\" texturefile = \"gfx/interface/alerts.dds\" noOfFrames = 70 }\n"
        + "\tspriteType = { name = \"GFX_icon\" texturefile = \"gfx//interface//icon.dds\" }\n"
        + "\tspriteType = { name = \"GFX_frontend_game_logo\" texturefile = \"gfx/flags/GER_democratic.tga\" }\n"
        + "\tspriteType = { name = \"GFX_missing_texture\" texturefile = \"gfx/interface/not_there.dds\" }\n"
        + "\tspriteType = { name = \"GFX_broken\" texturefile = \"gfx/interface/broken.dds\" }\n"
        + "\tspriteType = { name = \"GFX_button_close\" texturefile = \"gfx/interface/close.dds\" }\n"
        + "\tspriteType = { name = \"button_type_1\" texturefile = \"gfx/interface/button1.dds\" }\n"
        + "\tcorneredTileSpriteType = { name = \"GFX_tiled_window\" texturefile = \"gfx/interface/tiled_window.dds\" borderSize = { x = 64 y = 64 } }\n"
        + "}\n"
        + "bitmapfonts = { bitmapfont = { name = \"test_font\" path = \"gfx/fonts/test\" } }\n";

    // Schrift: Zeilenhöhe 20, A 10 breit, B 12, Leerzeichen 5, ? 8 (Ersatz für fehlende Zeichen).
    private const string Fnt =
        "info face=\"Test\" size=14\n"
        + "common lineHeight=20 base=16 scaleW=256 scaleH=256 pages=1 packed=0\n"
        + "page id=0 file=\"test.dds\"\n"
        + "chars count=4\n"
        + "char id=32 x=0 y=0 width=0 height=0 xoffset=0 yoffset=16 xadvance=5 page=0 chnl=15\n"
        + "char id=65 x=1 y=1 width=9 height=14 xoffset=0 yoffset=2 xadvance=10 page=0 chnl=15\n"
        + "char id=66 x=11 y=1 width=11 height=14 xoffset=0 yoffset=2 xadvance=12 page=0 chnl=15\n"
        + "char id=63 x=23 y=1 width=7 height=14 xoffset=0 yoffset=2 xadvance=8 page=0 chnl=15\n";

    // yml schreibt Zeilenumbrüche als die zwei Zeichen \n.
    private const string Yml = "l_english:\n HELLO:0 \"AB A\"\n TWO_LINES:0 \"AB\\nA\"\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-classiclayout-" + Guid.NewGuid().ToString("N"));

    public ClassicLayoutKit()
    {
        Dds("gfx/interface/alerts.dds", 3290, 42);
        Dds("gfx/interface/icon.dds", 40, 30);
        Dds("gfx/interface/close.dds", 30, 30);
        Dds("gfx/interface/button1.dds", 100, 30);
        Dds("gfx/interface/tiled_window.dds", 192, 192);
        // .tga fehlt, die .dds gleichen Namens gilt (wie im Spiel).
        Dds("gfx/flags/GER_democratic.dds", 82, 52);
        // Abgeschnittener Kopf: die Datei gibt es, ihre Größe ist nicht lesbar.
        Write("gfx/interface/broken.dds", "DDS ");
        Write("gfx/fonts/test.fnt", Fnt);
        Write("localisation/test_l_english.yml", Yml);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    public static GuiRules Rules(string game) =>
        GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json")));

    public ClassicAssets Assets(string gfx = DefaultGfx)
    {
        var file = new GuiSourceFile(Path.Combine(_root, "interface", "test.gfx"), "interface/test.gfx", GuiLayer.Vanilla, "vanilla", 0, 0);
        return ClassicAssets.Build(
            new[] { new GuiSourceRoot(_root, GuiLayer.Vanilla, "vanilla") }, new[] { file }, spriteFirstWins: false, caseSensitive: true, _ => GuiParser.Parse(gfx));
    }

    /// <summary>Legt das erste Fenster unter <c>guiTypes</c> von <paramref name="gui"/>.</summary>
    public LayoutBox Layout(string gui, LayoutOptions? options = null, string game = "hoi4")
    {
        var rules = Rules(game);
        var doc = GuiParser.Parse(gui);
        var index = GuiIndex.FromDocuments(Array.Empty<GuiSourceFile>(), Array.Empty<GuiDocument>(), rules.Layout);
        var resolver = new GuiResolver(index, rules, doc, Path.Combine(_root, "interface", "edit.gui"));
        var guiTypes = doc.Root.Children.First(c => string.Equals(c.Name, "guiTypes", StringComparison.OrdinalIgnoreCase));
        var window = guiTypes.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock);
        return new ClassicLayoutEngine(Assets()).Layout(resolver.Resolve(window), options);
    }

    /// <summary>Erster Kasten (Vorordnung) des Elements mit diesem <c>name</c>; ein Knopftext-Kind kommt erst nach seinem Knopf.</summary>
    public static LayoutBox Find(LayoutBox root, string name) =>
        new[] { root }.Concat(root.Descendants()).First(b => NameOf(b.Element) == name);

    public static string? NameOf(ResolvedElement element) =>
        element.Properties.LastOrDefault(p => p.Role == GuiRole.Property && string.Equals(p.Name, "name", StringComparison.OrdinalIgnoreCase))?.Value;

    public static void AssertRect(LayoutBox box, double x, double y, double width, double height)
    {
        Assert.Equal(x, box.X, 3);
        Assert.Equal(y, box.Y, 3);
        Assert.Equal(width, box.Width, 3);
        Assert.Equal(height, box.Height, 3);
    }

    private void Dds(string relative, int width, int height)
    {
        // Minimaler DDS-Kopf: Kennung, Kopfgröße 124, Höhe an Offset 12, Breite an Offset 16.
        var header = new byte[128];
        "DDS "u8.CopyTo(header);
        BitConverter.GetBytes(124).CopyTo(header, 4);
        BitConverter.GetBytes(height).CopyTo(header, 12);
        BitConverter.GetBytes(width).CopyTo(header, 16);
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, header);
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}

/// <summary>Klassisches Layout (HOI IV, EU IV) nach den Regeln aus Spec "Fakten → Lage" und den Messungen vom 2026-10-17.</summary>
public sealed class ClassicLayoutEngineTests : IDisposable
{
    private readonly ClassicLayoutKit _kit = new();

    public void Dispose() => _kit.Dispose();

    private static string Window(string body, string size = "size = { width = 256 height = 768 }") =>
        "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"parent\"\n\t\t" + size + "\n" + body + "\n\t}\n}\n";

    private const string LowerRightChild =
        "\t\tcontainerWindowType = {\n"
        + "\t\t\tname = \"child\"\n"
        + "\t\t\tposition = { x = -236 y = -330 }\n"
        + "\t\t\tsize = { width = 220 height = 160 }\n"
        + "\t\t\tOrientation = LOWER_RIGHT\n"
        + "{0}"
        + "\t\t}\n";

    private LayoutBox Layout(string gui, LayoutOptions? options = null, string game = "hoi4") => _kit.Layout(gui, options, game);

    [Fact]
    public void Orientation_lower_right_with_negative_position()
    {
        var root = Layout(Window(LowerRightChild.Replace("{0}", string.Empty)));

        ClassicLayoutKit.AssertRect(root, 0, 0, 256, 768);
        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "child"), 20, 438, 220, 160);
    }

    [Fact]
    public void Child_does_not_inherit_orientation()
    {
        var inner = "\t\t\ticonType = { name = \"inner\" spriteType = \"GFX_icon\" position = { x = 0 y = 0 } }\n";

        var root = Layout(Window(LowerRightChild.Replace("{0}", inner)));

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "inner"), 20, 438, 40, 30);
    }

    [Fact]
    public void Origo_center_centers_on_anchor()
    {
        var root = Layout(Window(
            "containerWindowType = { name = \"c\" size = { width = 40 height = 20 } orientation = CENTER origo = center }",
            "size = { width = 200 height = 100 }"));

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "c"), 80, 40, 40, 20);
    }

    [Fact]
    public void Center_position_puts_the_middle_on_the_position()
    {
        var root = Layout(Window(
            "iconType = { name = \"i\" spriteType = \"GFX_icon\" position = { x = 50 y = 50 } centerPosition = yes }",
            "size = { width = 200 height = 100 }"));

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "i"), 30, 35, 40, 30);
    }

    [Fact]
    public void Double_percent_fills_to_edge()
    {
        var root = Layout(Window(
            "containerWindowType = { name = \"double\" position = { x = 60 y = 80 } size = { width = 100%% height = 20 } }\n"
            + "containerWindowType = { name = \"single\" position = { x = 60 y = 110 } size = { width = 100% height = 20 } }",
            "size = { width = 220 height = 160 }"));

        var doubled = ClassicLayoutKit.Find(root, "double");
        var single = ClassicLayoutKit.Find(root, "single");
        ClassicLayoutKit.AssertRect(doubled, 60, 80, 160, 20);
        ClassicLayoutKit.AssertRect(single, 60, 110, 220, 20);
        Assert.False(doubled.OutsideParent);
        Assert.True(single.OutsideParent);
        Assert.False(root.OutsideScreen);
    }

    [Fact]
    public void Negative_size_is_parent_minus()
    {
        var root = Layout(Window("containerWindowType = { name = \"c\" size = { width = -56 height = -68 } }"));

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "c"), 0, 0, 200, 700);
    }

    [Fact]
    public void Size_min_and_max_clamp()
    {
        var root = Layout(Window(
            "containerWindowType = { name = \"small\" size = { width = 10 height = 10 min = { width = 30 height = 5 } } }\n"
            + "containerWindowType = { name = \"big\" size = { width = 100% height = 100% max = { x = 100 y = 50 } } }"));

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "small"), 0, 0, 30, 10);
        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "big"), 0, 0, 100, 50);
    }

    [Fact]
    public void Frame_zero_and_missing_mean_one()
    {
        var root = Layout(Window(
            "iconType = { name = \"f0\" spriteType = \"GFX_global_alert_icons\" frame = 0 }\n"
            + "iconType = { name = \"none\" spriteType = \"GFX_global_alert_icons\" }\n"
            + "iconType = { name = \"f2\" spriteType = \"GFX_global_alert_icons\" frame = 2 }\n"
            + "iconType = { name = \"f99\" spriteType = \"GFX_global_alert_icons\" frame = 99 }"));

        int FrameOf(string name) => Assert.IsType<GuiSpriteVisual>(ClassicLayoutKit.Find(root, name).Visual).Frame;
        Assert.Equal(1, FrameOf("f0"));
        Assert.Equal(1, FrameOf("none"));
        Assert.Equal(2, FrameOf("f2"));
        Assert.Equal(70, FrameOf("f99"));
        Assert.Empty(ClassicLayoutKit.Find(root, "f0").Notes);
        Assert.Contains(GuiTexts.ClassicFrameClamped(99, 70), ClassicLayoutKit.Find(root, "f99").Notes);
    }

    [Fact]
    public void Sprite_natural_size_divides_by_frames()
    {
        var root = Layout(Window("iconType = { name = \"i\" spriteType = \"GFX_global_alert_icons\" }"));

        var box = ClassicLayoutKit.Find(root, "i");
        ClassicLayoutKit.AssertRect(box, 0, 0, 47, 42);
        var visual = Assert.IsType<GuiSpriteVisual>(box.Visual);
        Assert.Equal(70, visual.Frames);
        Assert.Equal(ClassicSpriteKind.Sprite, visual.Kind);
        Assert.EndsWith("alerts.dds", visual.TextureFullPath);
        Assert.False(visual.Background);
    }

    [Fact]
    public void Scale_multiplies_the_natural_size()
    {
        var root = Layout(Window("iconType = { name = \"i\" spriteType = \"GFX_icon\" scale = 0.5 }"));

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "i"), 0, 0, 20, 15);
    }

    [Fact]
    public void Cornered_tile_below_two_borders_is_marked()
    {
        var root = Layout(Window(
            "containerWindowType = { name = \"bar\" size = { width = 220 height = 20 } background = { name = \"Background\" quadTextureSprite = \"GFX_tiled_window\" } }\n"
            + "iconType = { name = \"tile\" spriteType = \"GFX_tiled_window\" position = { x = 0 y = 300 } size = { width = 200 height = 20 } }\n"
            + "iconType = { name = \"bottom\" spriteType = \"GFX_tiled_window\" size = { width = 200 height = 20 } orientation = LOWER_LEFT origo = LOWER_LEFT }"));

        // Der größer gezeichnete 9-Slice wächst um die Mitte der angegebenen Fläche (Probe-Bild); Anker und Überstand rechnen mit der angegebenen Größe.
        var bar = ClassicLayoutKit.Find(root, "bar");
        ClassicLayoutKit.AssertRect(bar, 0, 0, 220, 20);
        Assert.False(bar.OutsideParent);
        var background = Assert.Single(bar.Children);
        ClassicLayoutKit.AssertRect(background, 0, 10 - 64, 220, 128);
        Assert.Contains(GuiTexts.ClassicCorneredMinimum(64, 64), background.Notes);
        Assert.False(background.OutsideParent);
        var tile = ClassicLayoutKit.Find(root, "tile");
        ClassicLayoutKit.AssertRect(tile, 0, 310 - 64, 200, 128);
        Assert.Contains(GuiTexts.ClassicCorneredMinimum(64, 64), tile.Notes);
        Assert.False(tile.OutsideParent);
        var bottom = ClassicLayoutKit.Find(root, "bottom");
        ClassicLayoutKit.AssertRect(bottom, 0, 758 - 64, 200, 128);
        Assert.False(bottom.OutsideParent);
        var visual = Assert.IsType<GuiSpriteVisual>(tile.Visual);
        Assert.Equal((64, 64), (visual.BorderX, visual.BorderY));
        Assert.Equal(ClassicSpriteKind.CorneredTile, visual.Kind);
    }

    [Fact]
    public void Show_position_wins_over_position()
    {
        var root = Layout(
            "guiTypes = { containerWindowType = { name = \"w\" position = { x = 0 y = -200 } show_position = { x = 5 y = -1 } size = { width = 256 height = 768 } } }");

        ClassicLayoutKit.AssertRect(root, 5, -1, 256, 768);
    }

    [Fact]
    public void Margin_shrinks_content()
    {
        var root = Layout(Window(
            "margin = { top = 18 bottom = 13 left = 8 right = 4 }\n"
            + "containerWindowType = { name = \"c\" size = { width = 100% height = 100% } }\n"
            + "containerWindowType = { name = \"r\" size = { width = 10 height = 10 } orientation = LOWER_RIGHT origo = LOWER_RIGHT }",
            "size = { width = 200 height = 100 }"));

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "c"), 8, 18, 188, 69);
        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "r"), 186, 77, 10, 10);
    }

    [Fact]
    public void Unknown_orientation_falls_back_with_note()
    {
        var root = Layout(Window(
            "containerWindowType = { name = \"c\" position = { x = 10 y = 10 } size = { width = 10 height = 10 } Orientation = UPPERL_LEFT }",
            "size = { width = 200 height = 100 }"));

        var box = ClassicLayoutKit.Find(root, "c");
        ClassicLayoutKit.AssertRect(box, 10, 10, 10, 10);
        Assert.Contains(GuiTexts.ClassicAnchorUnknown("UPPERL_LEFT"), box.Notes);
    }

    [Fact]
    public void Alias_orientation_is_mapped_with_note()
    {
        var root = Layout(Window(
            "containerWindowType = { name = \"c\" size = { width = 10 height = 10 } Orientation = left; }",
            "size = { width = 200 height = 100 }"));

        var box = ClassicLayoutKit.Find(root, "c");
        ClassicLayoutKit.AssertRect(box, 0, 50, 10, 10);
        Assert.Contains(GuiTexts.ClassicAnchorAlias("left"), box.Notes);
    }

    [Theory]
    [InlineData("UPPER_LEFT", 0, 0, false)]
    [InlineData("\"upper_right\"", 1, 0, false)]
    [InlineData("LOWER_LEFT", 0, 1, false)]
    [InlineData("lower_right", 1, 1, false)]
    [InlineData("CENTER_UP", 0.5, 0, false)]
    [InlineData("center_down", 0.5, 1, false)]
    [InlineData("CENTER_LEFT", 0, 0.5, false)]
    [InlineData("CENTER_RIGHT;", 1, 0.5, false)]
    [InlineData("center", 0.5, 0.5, false)]
    [InlineData("left", 0, 0.5, true)]
    [InlineData("center_lower", 0.5, 1, true)]
    [InlineData("CENTER_MIDDLE", 0.5, 0.5, true)]
    [InlineData("bottom_left", 0, 1, true)]
    public void Anchor_names_parse(string value, double fx, double fy, bool alias)
    {
        Assert.True(ClassicAnchors.TryParse(value, out var x, out var y, out var isAlias));
        Assert.Equal((fx, fy, alias), (x, y, isAlias));
    }

    [Theory]
    [InlineData("UPPERL_LEFT")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("middle")]
    public void Anchor_junk_does_not_parse(string? value)
    {
        Assert.False(ClassicAnchors.TryParse(value, out _, out _, out _));
    }

    [Fact]
    public void Offscreen_hide_position_is_marked_not_dropped()
    {
        var root = Layout(Window("iconType = { name = \"away\" spriteType = \"GFX_icon\" position = { x = 99512 y = 0 } }"));

        var box = ClassicLayoutKit.Find(root, "away");
        ClassicLayoutKit.AssertRect(box, 99512, 0, 40, 30);
        Assert.True(box.OutsideScreen);
        Assert.True(box.OffScreen);
        Assert.True(box.OutsideParent);
        Assert.False(root.OutsideScreen);
        Assert.False(root.OffScreen);
    }

    [Fact]
    public void Scene_bounds_ignore_boxes_outside_the_screen()
    {
        // "Einpassen" soll das Fenster zeigen, nicht den Bereich bis x = 99512.
        var scene = new Designer.GuiSchematicScene(
            Layout(Window("iconType = { name = \"away\" spriteType = \"GFX_icon\" position = { x = 99512 y = 0 } }")), _ => true);

        Assert.Equal((0, 0, 256, 768), (scene.Left, scene.Top, scene.Width, scene.Height));
    }

    [Fact]
    public void Scene_bounds_keep_a_root_slightly_larger_than_the_screen()
    {
        // Wie die HOI-IV-Leiste oben: -4 … 1924 auf 1920 Pixeln ragt hinaus, liegt aber auf dem Bildschirm und gehört in "Einpassen".
        var root = Layout(
            "guiTypes = { containerWindowType = { name = \"bar\" position = { x = -4 y = 0 } size = { width = 1928 height = 50 }"
            + " iconType = { name = \"date\" spriteType = \"GFX_icon\" position = { x = 100 y = 10 } } } }");
        var scene = new Designer.GuiSchematicScene(root, _ => true);

        Assert.True(root.OutsideScreen);
        Assert.Equal((-4, 0, 1928, 50), (scene.Left, scene.Top, scene.Width, scene.Height));
    }

    [Fact]
    public void Scene_bounds_keep_a_child_partly_off_screen()
    {
        var scene = new Designer.GuiSchematicScene(
            Layout(Window("iconType = { name = \"edge\" spriteType = \"GFX_icon\" position = { x = 0 y = 1070 } }")), _ => true);

        Assert.Equal((0, 0, 256, 1100), (scene.Left, scene.Top, scene.Width, scene.Height));
    }

    [Fact]
    public void Scene_bounds_fall_back_to_all_boxes_when_everything_is_outside()
    {
        var scene = new Designer.GuiSchematicScene(
            Layout("guiTypes = { containerWindowType = { name = \"w\" position = { x = 99512 y = 0 } size = { width = 100 height = 50 } } }"), _ => true);

        Assert.Equal((99512, 0, 100, 50), (scene.Left, scene.Top, scene.Width, scene.Height));
    }

    [Fact]
    public void Constant_in_position_is_substituted()
    {
        var root = Layout(Window(
            "@warn_icon_x = 280\n"
            + "iconType = { name = \"warn\" spriteType = \"GFX_icon\" position = { x = @warn_icon_x y = 22 } }",
            "size = { width = 400 height = 100 }"));

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "warn"), 280, 22, 40, 30);
    }

    [Fact]
    public void Missing_constant_is_noted()
    {
        var root = Layout(Window("iconType = { name = \"warn\" spriteType = \"GFX_icon\" position = { x = @nowhere y = 22 } }"));

        var box = ClassicLayoutKit.Find(root, "warn");
        Assert.Equal(0, box.X, 3);
        Assert.Equal(22, box.Y, 3);
        Assert.Contains(GuiTexts.LayoutPositionUnresolved("@nowhere"), box.Notes);
    }

    // Auszug aus EU IV interface/ages_view.gui (1.37.5), Fenster gekürzt.
    internal const string AgesView =
        "guiTypes = {\n"
        + "\twindowType = {\n"
        + "\t\tname = \"ages_view\"\n"
        + "\t\tsize = { x = 600 y = 720 }\n"
        + "\t\tguiButtonType = {\n"
        + "\t\t\tname =\"close_button\"\n"
        + "            if_resolution = {\n"
        + "                max_height = 867\n"
        + "\t\t\t\tposition = { x = 560 y = -48 }\n"
        + "\t\t\t\tquadTextureSprite = \"GFX_button_close\"\n"
        + "\t\t\t\tbuttonText = \"\"\n"
        + "            }\t\n"
        + "            if_resolution = {\n"
        + "                min_height = 868\n"
        + "\t\t\t\tquadTextureSprite =\"button_type_1\"\n"
        + "\t\t\t\tposition = { x= 214 y = 664 }\n"
        + "\t\t\t\tbuttonText = \"CLOSE\"\t\t\t\t\n"
        + "            }\t\t\t\n"
        + "\t\t\tbuttonFont = \"vic_18\"\n"
        + "\t\t\tshortcut = \"ESCAPE\"\n"
        + "\t\t}\n"
        + "\t}\n"
        + "}\n";

    [Fact]
    public void If_resolution_overrides_by_height()
    {
        var root = Layout(AgesView, new LayoutOptions { ScreenWidth = 1920, ScreenHeight = 1080 }, "eu4");

        var button = ClassicLayoutKit.Find(root, "close_button");
        ClassicLayoutKit.AssertRect(button, 214, 664, 100, 30);
        Assert.Equal("button_type_1", Assert.IsType<GuiSpriteVisual>(button.Visual).SpriteName);
        Assert.Contains(button.Notes, n => n == GuiTexts.ClassicFromIfResolution("position", "min_height = 868"));
        var text = Assert.IsType<GuiTextVisual>(Assert.Single(button.Children).Visual);
        Assert.Equal("CLOSE", text.Text);
        Assert.Equal("vic_18", text.FontName);
    }

    [Fact]
    public void If_resolution_low_screen_picks_first_block()
    {
        var root = Layout(AgesView, new LayoutOptions { ScreenWidth = 1280, ScreenHeight = 800 }, "eu4");

        var button = ClassicLayoutKit.Find(root, "close_button");
        ClassicLayoutKit.AssertRect(button, 560, -48, 30, 30);
        Assert.Equal("GFX_button_close", Assert.IsType<GuiSpriteVisual>(button.Visual).SpriteName);
        Assert.Empty(button.Children);
    }

    [Fact]
    public void Text_uses_localisation_and_font_width()
    {
        var root = Layout(Window(
            "instantTextBoxType = { name = \"hello\" text = \"HELLO\" font = \"test_font\" }\n"
            + "instantTextBoxType = { name = \"two\" text = \"TWO_LINES\" font = \"test_font\" format = centre position = { x = 0 y = 100 } }\n"
            + "instantTextBoxType = { name = \"limited\" text = \"HELLO\" font = \"test_font\" maxWidth = 30 maxHeight = 10 position = { x = 0 y = 200 } }\n"
            + "instantTextBoxType = { name = \"raw\" text = \"NO_SUCH_KEY\" font = \"missing_font\" position = { x = 0 y = 300 } }"));

        var hello = ClassicLayoutKit.Find(root, "hello");
        ClassicLayoutKit.AssertRect(hello, 0, 0, 10 + 12 + 5 + 10, 20);
        var helloText = Assert.IsType<GuiTextVisual>(hello.Visual);
        Assert.Equal("AB A", helloText.Text);
        Assert.Equal("test_font", helloText.FontName);
        Assert.Equal("left", helloText.Format);
        Assert.Empty(hello.Notes);

        var two = ClassicLayoutKit.Find(root, "two");
        ClassicLayoutKit.AssertRect(two, 0, 100, 22, 40);
        Assert.Equal("AB\nA", Assert.IsType<GuiTextVisual>(two.Visual).Text);
        Assert.Equal("centre", Assert.IsType<GuiTextVisual>(two.Visual).Format);

        var limited = ClassicLayoutKit.Find(root, "limited");
        ClassicLayoutKit.AssertRect(limited, 0, 200, 30, 10);
        Assert.Equal((30, 10), (((GuiTextVisual)limited.Visual!).MaxWidth, ((GuiTextVisual)limited.Visual!).MaxHeight));

        var raw = ClassicLayoutKit.Find(root, "raw");
        Assert.Equal("NO_SUCH_KEY", Assert.IsType<GuiTextVisual>(raw.Visual).Text);
        Assert.Contains(GuiTexts.ClassicFontMissing("missing_font"), raw.Notes);
    }

    [Fact]
    public void Background_child_stretches_to_parent()
    {
        var root = Layout(
            "guiTypes = { containerWindowType = { name = \"w\" position = { x = 10 y = 20 } size = { width = 300 height = 200 } margin = { left = 5 top = 5 }\n"
            + "backGround = { name = \"Background\" quadTextureSprite = \"GFX_icon\" } } }");

        var background = Assert.Single(root.Children);
        ClassicLayoutKit.AssertRect(background, 10, 20, 300, 200);
        var visual = Assert.IsType<GuiSpriteVisual>(background.Visual);
        Assert.True(visual.Background);
        Assert.Equal("GFX_icon", visual.SpriteName);
        Assert.EndsWith("icon.dds", visual.TextureFullPath);
        Assert.Empty(background.Notes);
    }

    [Fact]
    public void Missing_sprite_and_texture_give_null_path_with_note()
    {
        var root = Layout(Window(
            "iconType = { name = \"nosprite\" spriteType = \"GFX_nowhere\" }\n"
            + "iconType = { name = \"notexture\" spriteType = \"GFX_missing_texture\" }\n"
            + "iconType = { name = \"tga\" spriteType = \"GFX_frontend_game_logo\" }"));

        var noSprite = ClassicLayoutKit.Find(root, "nosprite");
        Assert.Null(Assert.IsType<GuiSpriteVisual>(noSprite.Visual).TextureFullPath);
        Assert.Contains(GuiTexts.ClassicSpriteMissing("GFX_nowhere"), noSprite.Notes);
        var noTexture = ClassicLayoutKit.Find(root, "notexture");
        Assert.Null(Assert.IsType<GuiSpriteVisual>(noTexture.Visual).TextureFullPath);
        Assert.Contains(GuiTexts.ClassicTextureMissing("gfx/interface/not_there.dds"), noTexture.Notes);
        var tga = ClassicLayoutKit.Find(root, "tga");
        ClassicLayoutKit.AssertRect(tga, 0, 0, 82, 52);
        Assert.EndsWith("GER_democratic.dds", Assert.IsType<GuiSpriteVisual>(tga.Visual).TextureFullPath);
    }

    [Fact]
    public void Unreadable_texture_is_noted()
    {
        var root = Layout(Window("iconType = { name = \"broken\" spriteType = \"GFX_broken\" }"));

        var box = ClassicLayoutKit.Find(root, "broken");
        ClassicLayoutKit.AssertRect(box, 0, 0, 0, 0);
        Assert.EndsWith("broken.dds", Assert.IsType<GuiSpriteVisual>(box.Visual).TextureFullPath);
        Assert.Contains(GuiTexts.ClassicTextureUnreadable("gfx/interface/broken.dds"), box.Notes);
    }

    [Fact]
    public void Sprite_found_only_in_other_case_is_noted()
    {
        var root = Layout(Window("iconType = { name = \"i\" spriteType = \"gfx_icon\" }"));

        var box = ClassicLayoutKit.Find(root, "i");
        ClassicLayoutKit.AssertRect(box, 0, 0, 40, 30);
        Assert.Contains(GuiTexts.ClassicSpriteCaseMismatch("gfx_icon"), box.Notes);
    }

    [Fact]
    public void Grid_shows_sample_slots_with_note()
    {
        var root = Layout(Window(
            "gridBoxType = { name = \"g\" position = { x = 10 y = 10 } size = { width = 200 height = 300 } slotsize = { width = 50 height = 51 } max_slots_horizontal = 2 }"));

        var grid = ClassicLayoutKit.Find(root, "g");
        Assert.Contains(GuiTexts.ClassicGridContent, grid.Notes);
        Assert.Equal(3, grid.Children.Count);
        ClassicLayoutKit.AssertRect(grid.Children[0], 10, 10, 50, 51);
        ClassicLayoutKit.AssertRect(grid.Children[1], 60, 10, 50, 51);
        ClassicLayoutKit.AssertRect(grid.Children[2], 10, 61, 50, 51);
        Assert.Equal(new[] { 0, 1, 2 }, grid.Children.Select(c => c.RepeatIndex));
    }

    [Fact]
    public void Vertical_grid_fills_columns_first()
    {
        var root = Layout(Window(
            "gridBoxType = { name = \"g\" size = { width = 50 height = 300 } slotsize = { width = 50 height = 51 } max_slots_vertical = 1 }"),
            new LayoutOptions { ListSampleCount = 2 });

        var grid = ClassicLayoutKit.Find(root, "g");
        ClassicLayoutKit.AssertRect(grid.Children[0], 0, 0, 50, 51);
        ClassicLayoutKit.AssertRect(grid.Children[1], 50, 0, 50, 51);
    }

    [Fact]
    public void Position_type_is_an_empty_box()
    {
        var root = Layout(Window("positionType = { name = \"p\" position = { x = 12 y = 34 } }"));

        var box = ClassicLayoutKit.Find(root, "p");
        ClassicLayoutKit.AssertRect(box, 12, 34, 0, 0);
        Assert.Null(box.Visual);
    }

    [Fact]
    public void Approximated_and_unknown_kinds_are_noted()
    {
        var root = Layout(Window(
            "extendedScrollbarType = { name = \"s\" size = { width = 10 height = 100 } }\n"
            + "mysteryType = { name = \"m\" size = { width = 10 height = 10 } }"));

        Assert.Contains(GuiTexts.ClassicApproximated("extendedScrollbarType"), ClassicLayoutKit.Find(root, "s").Notes);
        Assert.Contains(GuiTexts.ClassicUnknownElement("mysteryType"), ClassicLayoutKit.Find(root, "m").Notes);
    }

    [Fact]
    public void Unknown_block_without_name_is_noted_on_parent()
    {
        var root = Layout(Window(
            "mysteryBlock = { size = { width = 10 height = 10 } }\n"
            + "iconType = { name = \"i\" spriteType = \"GFX_icon\" }"));

        var child = Assert.Single(root.Children);
        Assert.Equal("i", ClassicLayoutKit.NameOf(child.Element));
        Assert.Contains(GuiTexts.ClassicBlockIgnored("mysteryBlock"), root.Notes);
    }

    [Fact]
    public void Container_without_size_takes_its_children_with_note()
    {
        var root = Layout(
            "guiTypes = { containerWindowType = { name = \"w\" iconType = { name = \"i\" spriteType = \"GFX_icon\" position = { x = 10 y = 5 } } } }");

        ClassicLayoutKit.AssertRect(root, 0, 0, 50, 35);
        Assert.Contains(GuiTexts.ClassicSizeFromChildren, root.Notes);
    }

    [Fact]
    public void Hidden_elements_follow_show_hidden()
    {
        var gui = Window(
            "iconType = { name = \"h\" spriteType = \"GFX_icon\" hide = yes }\n"
            + "iconType = { name = \"v\" spriteType = \"GFX_icon\" Visible = no }\n"
            + "iconType = { name = \"shown\" spriteType = \"GFX_icon\" }");

        Assert.Single(Layout(gui).Children);
        Assert.Equal(3, Layout(gui, new LayoutOptions { ShowHidden = true }).Children.Count);
    }

    [Fact]
    public void Button_has_sprite_and_text_child()
    {
        var root = Layout(Window(
            "buttonType = { name = \"b\" position = { x = 5 y = 6 } quadTextureSprite = \"button_type_1\" buttonText = \"HELLO\" buttonFont = \"test_font\" }"));

        var button = ClassicLayoutKit.Find(root, "b");
        ClassicLayoutKit.AssertRect(button, 5, 6, 100, 30);
        Assert.Equal("button_type_1", Assert.IsType<GuiSpriteVisual>(button.Visual).SpriteName);
        var text = Assert.Single(button.Children);
        Assert.Same(button.Element, text.Element);
        ClassicLayoutKit.AssertRect(text, 5, 6, 100, 30);
        Assert.Equal("AB A", Assert.IsType<GuiTextVisual>(text.Visual).Text);
    }

    // Auszug aus HOI IV interface/frontendmainview.gui (1.19.3), mainmenu_panel_bottom: ein ButtonType mit text/font statt buttonText/buttonFont;
    // Sprite und Schlüssel durch die des Testkits ersetzt.
    private const string SinglePlayerButton =
        "\t\tButtonType = {\n"
        + "\t\t\tname = \"single_player_button\"\n"
        + "\t\t\tposition = { x = 16 y = 18 }\n"
        + "\t\t\tquadTextureSprite =\"button_type_1\"\n"
        + "\t\t\tfont = \"test_font\"\t\t\t\n"
        + "\t\t\ttext = \"HELLO\"\t\t\t\t\n"
        + "\t\t\tclicksound = click_default\t\t\t\n"
        + "\t\t\toversound = ui_menu_over\n"
        + "\t\t}\n";

    [Fact]
    public void Button_falls_back_to_text_and_font()
    {
        var root = Layout(Window(SinglePlayerButton));

        var button = ClassicLayoutKit.Find(root, "single_player_button");
        var text = Assert.IsType<GuiTextVisual>(Assert.Single(button.Children).Visual);
        Assert.Equal("AB A", text.Text);
        Assert.Equal("test_font", text.FontName);
        Assert.Equal("center", text.Format);
    }

    [Fact]
    public void Text_box_with_max_width_is_max_width_wide()
    {
        // Zentrierter Text richtet sich im Kasten aus: der Kasten ist maxWidth breit, nicht nur so breit wie der Text.
        var root = Layout(Window("instantTextBoxType = { name = \"c\" text = \"A\" font = \"test_font\" maxWidth = 300 format = centre }"));

        var box = ClassicLayoutKit.Find(root, "c");
        ClassicLayoutKit.AssertRect(box, 0, 0, 300, 20);
        Assert.Equal(300, Assert.IsType<GuiTextVisual>(box.Visual).MaxWidth);
        Assert.Empty(box.Notes);
    }

    [Fact]
    public void Text_wraps_at_spaces_inside_max_width()
    {
        // "AB AB" ist 49 breit, mit " A" wären es 64.
        var root = Layout(Window("instantTextBoxType = { name = \"w\" text = \"AB AB A\" font = \"test_font\" maxWidth = 50 }"));

        var box = ClassicLayoutKit.Find(root, "w");
        Assert.Equal("AB AB\nA", Assert.IsType<GuiTextVisual>(box.Visual).Text);
        ClassicLayoutKit.AssertRect(box, 0, 0, 50, 40);
        Assert.Empty(box.Notes);
    }

    [Fact]
    public void Max_height_cuts_wrapped_lines_with_note()
    {
        var root = Layout(Window("instantTextBoxType = { name = \"w\" text = \"AB AB A\" font = \"test_font\" maxWidth = 50 maxHeight = 20 }"));

        var box = ClassicLayoutKit.Find(root, "w");
        Assert.Equal("AB AB", Assert.IsType<GuiTextVisual>(box.Visual).Text);
        ClassicLayoutKit.AssertRect(box, 0, 0, 50, 20);
        Assert.Contains(GuiTexts.ClassicTextCut(1, 2), box.Notes);
    }

    [Fact]
    public void Word_wider_than_max_width_is_noted()
    {
        var root = Layout(Window("instantTextBoxType = { name = \"w\" text = \"ABABAB A\" font = \"test_font\" maxWidth = 50 }"));

        var box = ClassicLayoutKit.Find(root, "w");
        Assert.Equal("ABABAB\nA", Assert.IsType<GuiTextVisual>(box.Visual).Text);
        Assert.Contains(GuiTexts.ClassicTextTooWide(50), box.Notes);
    }

    [Fact]
    public void Colour_codes_are_not_measured()
    {
        // §Y und §! zählen nicht (sonst je ein "?" mit 8 Pixeln) und gelten nicht als fehlende Zeichen.
        var root = Layout(Window(
            "instantTextBoxType = { name = \"c\" text = \"§YAB§!\" font = \"test_font\" }\n"
            + "instantTextBoxType = { name = \"w\" text = \"§YAB§! §YAB§!\" font = \"test_font\" maxWidth = 49 position = { x = 0 y = 100 } }"));

        var plain = ClassicLayoutKit.Find(root, "c");
        ClassicLayoutKit.AssertRect(plain, 0, 0, 22, 20);
        Assert.Empty(plain.Notes);
        var wrapped = ClassicLayoutKit.Find(root, "w");
        Assert.Equal("§YAB§! §YAB§!", Assert.IsType<GuiTextVisual>(wrapped.Visual).Text);
        Assert.Equal(20, wrapped.Height, 3);
    }

    [Fact]
    public void Multiline_button_wraps_at_button_width_and_plain_button_does_not()
    {
        // Wie HOI IV show_subscription (multiline = yes). "AB AB AB" ist 76 breit, "AB AB AB AB" 103, der Knopf 100.
        var root = Layout(Window(
            "buttonType = { name = \"m\" quadTextureSprite = \"button_type_1\" buttonText = \"AB AB AB AB AB\" buttonFont = \"test_font\" multiline = yes }\n"
            + "buttonType = { name = \"p\" quadTextureSprite = \"button_type_1\" buttonText = \"AB AB AB AB AB\" buttonFont = \"test_font\" position = { x = 0 y = 100 } }"));

        var multiline = Assert.IsType<GuiTextVisual>(Assert.Single(ClassicLayoutKit.Find(root, "m").Children).Visual);
        Assert.Equal("AB AB AB\nAB AB", multiline.Text);
        var plain = Assert.IsType<GuiTextVisual>(Assert.Single(ClassicLayoutKit.Find(root, "p").Children).Visual);
        Assert.Equal("AB AB AB AB AB", plain.Text);
    }

    [Fact]
    public void Ui_scale_shrinks_virtual_screen()
    {
        var root = Layout(
            "guiTypes = { containerWindowType = { name = \"w\" size = { width = 100% height = 100% } } }",
            new LayoutOptions { ScreenWidth = 2048, ScreenHeight = 1152, UiScale = 1.25 });

        ClassicLayoutKit.AssertRect(root, 0, 0, 1638.4, 921.6);
        Assert.False(root.OutsideScreen);
        Assert.Contains(GuiTexts.ClassicUiScale(1.25), root.Notes);
    }

    // EU IV frontend.gui (gekürzt): country_selection_panel und mainmenu mit fullscreen = yes, darin das untere Panel (CENTER_DOWN), die
    // Social-Knöpfe (CENTER_DOWN, selbst fullScreen = no) und multiplayer_menu (CENTER, selbst fullscreen = yes) mit join_direct oben links.
    private const string Frontend = """
        guiTypes = {
        	containerWindowType = {
        		name ="country_selection_panel"
        		position = { x=0 y =0}
        		size = { x=320 y = 960}
        		fullscreen=yes
        		containerWindowType = {
        			name ="multiplayer_menu"
        			position = { x=-511 y=-280 }
        			size = { x=1022 y=560 }
        			fullscreen=yes
        			Orientation = "CENTER"
        			buttonType = {
        				name = "join_direct"
        				position = { x = 96 y = 506 }
        				quadTextureSprite = "button_type_1"
        			}
        		}
        		containerWindowType = {
        			name ="mainmenu"
        			position = { x=0 y =0}
        			size = { x=1024 y =1024}
        			fullscreen=yes
        			Orientation = "UPPER_LEFT"
        			containerWindowType = {
        				name ="mainmenu_panel_bottom"
        				position = { x=-552 y =-84}
        				size = { x=1024 y =768}
        				fullscreen=yes
        				Orientation = "CENTER_DOWN"
        			}
        			containerWindowType = {
        				name = "PersistentSocialButtons"
        				position = { x= 436 y = -60 }
        				size = { x=288 y = 96 }
        				fullScreen = no
        				Orientation = "CENTER_DOWN"
        			}
        		}
        	}
        }
        """;

    private static readonly LayoutOptions Screen2000 = new() { ScreenWidth = 2000, ScreenHeight = 1000, UiScale = 1 };

    [Fact]
    public void Fullscreen_children_anchor_to_the_screen_extents()
    {
        // Wie mainmenu_panel_bottom im Probe-Bild: CENTER_DOWN des ganzen Bildschirms, nicht des 1024 × 1024 großen mainmenu.
        var root = Layout(Frontend, Screen2000, "eu4");

        var mainmenu = ClassicLayoutKit.Find(root, "mainmenu");
        ClassicLayoutKit.AssertRect(mainmenu, 0, 0, 1024, 1024);
        Assert.Contains(GuiTexts.ClassicApproximated("fullscreen"), mainmenu.Notes);
        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "mainmenu_panel_bottom"), 448, 916, 1024, 768);
    }

    [Fact]
    public void Non_fullscreen_child_of_a_fullscreen_window_anchors_to_the_screen()
    {
        // Wie PersistentSocialButtons: selbst fullScreen = no, aber Kind von mainmenu; rechts außerhalb der 1024 Pixel von mainmenu. Unten ragt es
        // 36 Pixel über den Bildschirm (−60 + 96), wie im Spiel; deshalb hier keine Prüfung auf Überstand (die prüft der %-Test unten).
        var root = Layout(Frontend, Screen2000, "eu4");

        var social = ClassicLayoutKit.Find(root, "PersistentSocialButtons");
        ClassicLayoutKit.AssertRect(social, 1436, 940, 288, 96);
        Assert.DoesNotContain(GuiTexts.ClassicApproximated("fullscreen"), social.Notes);
    }

    [Fact]
    public void Fullscreen_window_keeps_its_own_top_left_as_origin()
    {
        // Falle multiplayer_menu: das Fenster selbst ist fullscreen und CENTER am Bildschirm, join_direct (oben links) bleibt relativ zum Fenster.
        var root = Layout(Frontend, Screen2000, "eu4");

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "multiplayer_menu"), 489, 220, 1022, 560);
        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "join_direct"), 585, 726, 100, 30);
    }

    [Fact]
    public void Nested_fullscreen_uses_the_screen_extents_not_the_parent_content()
    {
        // Das innere Fenster ist 50 × 50 groß und liegt bei (10, 10); sein Kind unten rechts richtet sich am Bildschirm aus (10 + 2000 − 20),
        // nicht an der Fläche des Elternfensters (10 + 50 − 20).
        var root = Layout(
            """
            guiTypes = {
            	containerWindowType = {
            		name = "outer"
            		size = { width = 200 height = 100 }
            		fullscreen = yes
            		containerWindowType = {
            			name = "inner"
            			position = { x = 10 y = 10 }
            			size = { width = 50 height = 50 }
            			fullscreen = yes
            			iconType = { name = "corner" spriteType = "GFX_icon" position = { x = -20 y = -20 } orientation = LOWER_RIGHT }
            		}
            	}
            }
            """,
            Screen2000);

        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "inner"), 10, 10, 50, 50);
        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "corner"), 1990, 990, 40, 30);
    }

    [Fact]
    public void Fullscreen_window_without_size_is_the_screen()
    {
        var root = Layout(
            "guiTypes = { containerWindowType = { name = \"w\" fullscreen = yes iconType = { name = \"i\" spriteType = \"GFX_icon\" position = { x = 5 y = 5 } } } }",
            Screen2000);

        ClassicLayoutKit.AssertRect(root, 0, 0, 2000, 1000);
        Assert.DoesNotContain(GuiTexts.ClassicSizeFromChildren, root.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutSizeUnknown, root.Notes);
    }

    [Fact]
    public void Percent_size_in_a_fullscreen_window_uses_the_screen()
    {
        var root = Layout(
            "guiTypes = { containerWindowType = { name = \"w\" size = { width = 100 height = 100 } fullscreen = yes "
            + "containerWindowType = { name = \"half\" size = { width = 50% height = 100%% } position = { x = 0 y = 100 } } } }",
            Screen2000);

        // Die Kinder richten sich am Bildschirm aus: "half" ist 1000 breit im 100 breiten Fenster und gilt nicht als Überstand.
        ClassicLayoutKit.AssertRect(ClassicLayoutKit.Find(root, "half"), 0, 100, 1000, 900);
        Assert.False(ClassicLayoutKit.Find(root, "half").OutsideParent);
    }

    [Fact]
    public void Unknown_text_format_is_noted()
    {
        var root = Layout(Window(
            "instantTextBoxType = { name = \"u\" text = \"A\" font = \"test_font\" format = sideways }\n"
            +"instantTextBoxType = { name = \"c\" text = \"A\" font = \"test_font\" format = centre position = { x = 0 y = 100 } }"));

        var unknown = ClassicLayoutKit.Find(root, "u");
        Assert.Contains(GuiTexts.ClassicFormatUnknown("sideways"), unknown.Notes);
        Assert.Equal("left", Assert.IsType<GuiTextVisual>(unknown.Visual).Format);
        Assert.Empty(ClassicLayoutKit.Find(root, "c").Notes);
    }

    [Fact]
    public void Upper_left_format_is_left_without_note()
    {
        var root = Layout(Window("instantTextBoxType = { name = \"u\" text = \"A\" font = \"test_font\" format = upper_left }"));

        var box = ClassicLayoutKit.Find(root, "u");
        Assert.Equal("left", Assert.IsType<GuiTextVisual>(box.Visual).Format);
        Assert.Empty(box.Notes);
    }

    [Fact]
    public void Upper_right_format_is_right_without_note()
    {
        var root = Layout(Window("instantTextBoxType = { name = \"u\" text = \"A\" font = \"test_font\" format = upper_right }"));

        var box = ClassicLayoutKit.Find(root, "u");
        Assert.Equal("right", Assert.IsType<GuiTextVisual>(box.Visual).Format);
        Assert.Empty(box.Notes);
    }

    [Fact]
    public void Format_value_ignores_case_and_quotes()
    {
        var root = Layout(Window("instantTextBoxType = { name = \"u\" text = \"A\" font = \"test_font\" format = \"UPPER_LEFT\" }"));

        var box = ClassicLayoutKit.Find(root, "u");
        Assert.Equal("left", Assert.IsType<GuiTextVisual>(box.Visual).Format);
        Assert.Empty(box.Notes);
    }

    [Theory]
    [InlineData("lower_left", "left")]
    [InlineData("lower_right", "right")]
    [InlineData("down", "left")]
    [InlineData("up", "left")]
    public void Lower_and_vertical_formats_map_horizontally_with_note(string format, string expected)
    {
        var root = Layout(Window($"instantTextBoxType = {{ name = \"v\" text = \"A\" font = \"test_font\" format = {format} }}"));

        var box = ClassicLayoutKit.Find(root, "v");
        Assert.Equal(expected, Assert.IsType<GuiTextVisual>(box.Visual).Format);
        Assert.Contains(GuiTexts.ClassicFormatVertical(format), box.Notes);
    }
}
