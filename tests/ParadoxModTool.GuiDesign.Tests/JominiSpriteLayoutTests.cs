using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Die Jomini-Engine gibt jedem Kasten mit <c>texture</c> ein <see cref="JominiSpriteVisual"/> (Bildausschnitt, Rand, Einpassung, Spiegelung, Farbe,
/// Platzhalter). Texturen sind kleine DDS-Dateien in einer eigenen Installation im Temp-Ordner; gelesen werden nur ihre Köpfe.
/// </summary>
public sealed class JominiSpriteLayoutTests : IClassFixture<JominiEngineTextFixture>, IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-spritelayout-" + Guid.NewGuid().ToString("N"));
    private readonly JominiEngineTextFixture _text;
    private readonly GuiTextureLocator _textures;

    public JominiSpriteLayoutTests(JominiEngineTextFixture text)
    {
        _text = text;
        var gfx = Path.Combine(_temp, "install", "game", "gfx");
        TestDds.WriteBgra(Path.Combine(gfx, "strip.dds"), 120, 40);
        TestDds.WriteBgra(Path.Combine(gfx, "frames.dds"), 80, 80);
        TestDds.WriteBgra(Path.Combine(gfx, "tex.dds"), 100, 100);
        TestDds.WriteBgra(Path.Combine(gfx, "strip3.dds"), 100, 20);
        TestDds.WriteDx10Header(Path.Combine(gfx, "bc5.dds"), 8, 8, 83);
        File.WriteAllBytes(Path.Combine(gfx, "empty.dds"), Array.Empty<byte>());
        _textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(Path.Combine(_temp, "install"), Array.Empty<string>(), null, Array.Empty<string>()));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private LayoutBox Layout(string gui, string library = "", bool locator = true, JominiTextService? text = null)
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui, library);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        var engine = new GuiLayoutEngine(GuiResolverTests.Rules, _textures.SizeOf, text, locator ? _textures : null);
        return engine.Layout(element);
    }

    private JominiSpriteVisual Sprite(string gui, string library = "") => Layout(gui, library).Sprite!;

    [Fact]
    public void Icon_frame_comes_from_framesize()
    {
        var sprite = Layout("icon = { texture = \"gfx/strip.dds\" framesize = { 40 40 } frame = 2 }").Sprite!;

        Assert.Equal(new LayoutRect(40, 0, 40, 40), sprite.Source);
        Assert.Equal("gfx/strip.dds", sprite.Texture);
        Assert.Equal(120, sprite.TextureWidth);
        Assert.Equal(40, sprite.TextureHeight);
        Assert.Equal(JominiSpritePlaceholder.None, sprite.Placeholder);
        Assert.EndsWith("strip.dds", sprite.TextureFullPath);
    }

    [Fact]
    public void Framesize_frames_are_row_major()
    {
        Assert.Equal(new LayoutRect(0, 40, 40, 40), Sprite("icon = { texture = \"gfx/frames.dds\" framesize = { 40 40 } frame = 3 }").Source);
    }

    [Fact]
    public void Frame_zero_and_frame_grid_show_the_first_frame()
    {
        Assert.Equal(new LayoutRect(0, 0, 40, 40), Sprite("icon = { texture = \"gfx/strip.dds\" frame_grid = { 3 1 } frame = 0 }").Source);
        Assert.Equal(new LayoutRect(80, 0, 40, 40), Sprite("icon = { texture = \"gfx/strip.dds\" frame_grid = { 3 1 } frame = 3 }").Source);
    }

    [Fact]
    public void Frame_beyond_the_count_shows_the_last_with_a_note()
    {
        var box = Layout("icon = { texture = \"gfx/strip.dds\" framesize = { 40 40 } frame = 9 }");

        Assert.Equal(new LayoutRect(80, 0, 40, 40), box.Sprite!.Source);
        Assert.Contains(GuiTexts.LayoutFrameOutOfRange(9, 3), box.Notes);
    }

    [Fact]
    public void Quoted_frame_is_read()
    {
        Assert.Equal(new LayoutRect(40, 0, 40, 40), Sprite("icon = { texture = \"gfx/strip.dds\" framesize = { 40 40 } frame = \"2\" }").Source);
    }

    [Fact]
    public void Bound_frame_shows_the_first_with_a_note()
    {
        var box = Layout("icon = { texture = \"gfx/strip.dds\" framesize = { 40 40 } frame = \"[GetFrame]\" }");

        Assert.Equal(new LayoutRect(0, 0, 40, 40), box.Sprite!.Source);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("frame"), box.Notes);
    }

    [Fact]
    public void Button_ignores_upframe_when_frame_is_missing()
    {
        // Lauf 5, Fall 13z (alle drei Spiele): ein Button ohne frame zeigt mit upframe = 2 das erste Bild (rot), nicht das zweite.
        var box = Layout("button = { texture = \"gfx/strip.dds\" framesize = { 40 40 } upframe = 2 }");

        Assert.Equal(new LayoutRect(0, 0, 40, 40), box.Sprite!.Source);
        Assert.DoesNotContain(GuiTexts.LayoutTextureApproximated("upframe"), box.Notes);
        Assert.Equal(new LayoutRect(0, 0, 40, 40), Sprite("button = { texture = \"gfx/strip.dds\" framesize = { 40 40 } upframe = 2 gfxtype = buttongfx }").Source);
    }

    [Fact]
    public void Framed_button_keeps_upframe_with_a_note()
    {
        // Nicht gemessen (die Probe-Buttons hatten kein framedbuttongfx): dort bleibt upframe der Grundzustand, mit Hinweis.
        var box = Layout("button = { texture = \"gfx/strip.dds\" framesize = { 40 40 } upframe = 2 gfxtype = framedbuttongfx }");

        Assert.Equal(new LayoutRect(40, 0, 40, 40), box.Sprite!.Source);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("upframe"), box.Notes);
    }

    [Fact]
    public void Frames_are_whole_texture_pixels()
    {
        // Lauf 5, Fall 14d (alle drei Spiele): 100 Pixel in frame_grid = { 3 1 } und framesize = { 33.3333 20 } geben Bilder von 33 Pixeln.
        Assert.Equal(new LayoutRect(33, 0, 33, 20), Sprite("icon = { texture = \"gfx/strip3.dds\" frame_grid = { 3 1 } frame = 2 }").Source);
        Assert.Equal(new LayoutRect(33, 0, 33, 20), Sprite("icon = { texture = \"gfx/strip3.dds\" framesize = { 33.3333 20 } frame = 2 }").Source);
        var box = Layout("icon = { texture = \"gfx/strip3.dds\" frame_grid = { 3 1 } }");
        Assert.Equal(33, box.Width, 6);
        Assert.Equal(20, box.Height, 6);
        // Bei 33,33 geben Abrunden und Runden dasselbe: kein Hinweis.
        Assert.DoesNotContain(GuiTexts.LayoutTextureApproximated("frame size"), box.Notes);
    }

    [Fact]
    public void Frame_size_with_half_a_pixel_or_more_is_noted()
    {
        // Ab 0,5 unterscheiden sich Abrunden und Runden; 14d zeigt nur 33,33. Abgerundet wird trotzdem, mit Hinweis (einmal, auch über die Größe).
        var box = Layout("icon = { texture = \"gfx/strip.dds\" framesize = { 40.5 40 } frame = 2 }");

        Assert.Equal(new LayoutRect(40, 0, 40, 40), box.Sprite!.Source);
        Assert.Equal(40, box.Width, 6);
        Assert.Single(box.Notes, GuiTexts.LayoutTextureApproximated("frame size"));
    }

    [Fact]
    public void Button_frame_beats_upframe()
    {
        Assert.Equal(new LayoutRect(80, 0, 40, 40), Sprite("button = { texture = \"gfx/strip.dds\" framesize = { 40 40 } frame = 3 upframe = 2 }").Source);
    }

    [Fact]
    public void Icon_ignores_upframe()
    {
        Assert.Equal(new LayoutRect(0, 0, 40, 40), Sprite("icon = { texture = \"gfx/strip.dds\" framesize = { 40 40 } upframe = 2 }").Source);
    }

    [Theory]
    [InlineData("\"[GetW]\" \"[GetH]\"")]
    [InlineData("50% 50%")]
    public void Bound_framesize_gives_a_note_and_the_whole_texture(string items)
    {
        var box = Layout($"icon = {{ texture = \"gfx/strip.dds\" framesize = {{ {items} }} frame = 2 }}");

        Assert.Equal(new LayoutRect(0, 0, 120, 40), box.Sprite!.Source);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("framesize"), box.Notes);
        // Auch die natürliche Größe nimmt dann die ganze Textur (und sagt es).
        Assert.Equal(120, box.Width, 3);
        Assert.Equal(40, box.Height, 3);
    }

    [Fact]
    public void Quoted_mixed_case_spritetype_is_read()
    {
        var sprite = Sprite("widget = { size = { 50 50 } texture = \"gfx/tex.dds\" spriteType = \"CorneredTiled\" spriteborder = { 10 8 } }");

        Assert.Equal(JominiSpriteKind.CorneredTiled, sprite.Kind);
        Assert.Equal(new JominiBorder(10, 8, 10, 8), sprite.Border);
    }

    [Fact]
    public void Corneredtiled_without_border_tiles()
    {
        Assert.Equal(JominiSpriteKind.Tiled, Sprite("widget = { size = { 50 50 } texture = \"gfx/tex.dds\" spriteType = corneredtiled }").Kind);
    }

    [Fact]
    public void Plain_texture_is_stretched_and_a_border_without_type_follows_the_rule()
    {
        Assert.Equal(JominiSpriteKind.Stretched, Sprite("icon = { texture = \"gfx/tex.dds\" }").Kind);
        Assert.Equal(JominiSpriteKind.Stretched, Sprite("icon = { texture = \"gfx/tex.dds\" spriteType = Stretched }").Kind);

        var withBorder = Sprite("icon = { texture = \"gfx/tex.dds\" spriteborder = { 4 4 } }");
        Assert.Equal(JominiSpriteRules.BorderWithoutTypeSlices ? JominiSpriteKind.CorneredStretched : JominiSpriteKind.Stretched, withBorder.Kind);
    }

    [Fact]
    public void Unknown_spritetype_is_stretched_with_a_note()
    {
        var box = Layout("icon = { texture = \"gfx/tex.dds\" spriteType = wobbly }");

        Assert.Equal(JominiSpriteKind.Stretched, box.Sprite!.Kind);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("spritetype"), box.Notes);
    }

    [Fact]
    public void Single_side_border_overrides_and_negative_is_zero()
    {
        var box = Layout("icon = { texture = \"gfx/tex.dds\" spriteType = corneredstretched spriteborder = { 10 20 } spriteborder_right = 5 spriteborder_bottom = -500 }");

        Assert.Equal(new JominiBorder(10, 20, 5, 0), box.Sprite!.Border);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("spriteborder"), box.Notes);
    }

    [Theory]
    [InlineData("horizontal|vertical")]
    [InlineData("vertical|horizontal")]
    public void Mirror_reads_both_axes(string value)
    {
        var sprite = Sprite($"icon = {{ texture = \"gfx/tex.dds\" mirror = \"{value}\" }}");

        Assert.True(sprite.MirrorHorizontal);
        Assert.True(sprite.MirrorVertical);
    }

    [Fact]
    public void Mirror_reads_one_axis_case_insensitively()
    {
        var horizontal = Sprite("icon = { texture = \"gfx/tex.dds\" mirror = Horizontal }");
        var vertical = Sprite("icon = { texture = \"gfx/tex.dds\" mirror = vertical }");
        var none = Sprite("icon = { texture = \"gfx/tex.dds\" }");

        Assert.Equal((true, false), (horizontal.MirrorHorizontal, horizontal.MirrorVertical));
        Assert.Equal((false, true), (vertical.MirrorHorizontal, vertical.MirrorVertical));
        Assert.Equal((false, false), (none.MirrorHorizontal, none.MirrorVertical));
    }

    [Theory]
    [InlineData("fill")]
    [InlineData("centercrop")]
    [InlineData("Center")]
    [InlineData("start")]
    [InlineData("end")]
    public void Fittype_on_an_icon_is_ignored(string value)
    {
        // Lauf 5, Fälle 13l–13r (alle drei Spiele): an einem icon füllt jedes fittype den Kasten wie fill. Gemessen, daher ohne Hinweis.
        var box = Layout($"icon = {{ texture = \"gfx/tex.dds\" fittype = \"{value}\" }}");

        Assert.Equal(JominiFit.Fill, box.Sprite!.Fit);
        Assert.DoesNotContain(GuiTexts.LayoutTextureApproximated("fittype"), box.Notes);
    }

    [Theory]
    [InlineData("fill", JominiFit.Fill)]
    [InlineData("centercrop", JominiFit.CenterCrop)]
    [InlineData("Center", JominiFit.Center)]
    [InlineData("start", JominiFit.Start)]
    [InlineData("end", JominiFit.End)]
    public void Fittype_is_mapped_on_stretched_backgrounds_with_a_note(string value, JominiFit expected)
    {
        // Vanilla setzt fittype fast nur an background; dort ist es nicht gemessen und bleibt die Annahme, mit Hinweis.
        var background = Assert.Single(Layout($"widget = {{ size = {{ 100 50 }} background = {{ texture = \"gfx/tex.dds\" fittype = \"{value}\" }} }}").Children);

        Assert.Equal(expected, background.Sprite!.Fit);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("fittype"), background.Notes);
    }

    [Fact]
    public void Fittype_on_a_cornered_background_is_ignored_with_a_note()
    {
        var background = Assert.Single(Layout(
            "widget = { size = { 100 50 } background = { texture = \"gfx/tex.dds\" spriteType = corneredstretched spriteborder = { 4 4 } fittype = centercrop } }").Children);

        Assert.Equal(JominiFit.Fill, background.Sprite!.Fit);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("fittype"), background.Notes);
    }

    [Fact]
    public void Fittype_on_another_kind_is_ignored_with_a_note()
    {
        var box = Layout("widget = { size = { 100 50 } texture = \"gfx/tex.dds\" fittype = centercrop }");

        Assert.Equal(JominiFit.Fill, box.Sprite!.Fit);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("fittype"), box.Notes);
    }

    [Fact]
    public void Density_defaults_to_one_and_is_read_when_positive()
    {
        Assert.Equal(1, Sprite("icon = { texture = \"gfx/tex.dds\" }").Density);
        Assert.Equal(1, Sprite("icon = { texture = \"gfx/tex.dds\" texture_density = 0 }").Density);
        Assert.Equal(1, Sprite("icon = { texture = \"gfx/tex.dds\" texture_density = -2 }").Density);
        Assert.Equal(2, Sprite("icon = { texture = \"gfx/tex.dds\" texture_density = 2 }").Density);
    }

    [Fact]
    public void Color_tintcolor_and_alpha_combine()
    {
        var sprite = Sprite("icon = { texture = \"gfx/tex.dds\" color = { 1 0.5 1 0.5 } tintcolor = { 0.5 1 1 1 } alpha = 0.5 }");

        Assert.Equal(0.5, sprite.Tint.R, 6);
        Assert.Equal(0.5, sprite.Tint.G, 6);
        Assert.Equal(1.0, sprite.Tint.B, 6);
        Assert.Equal(0.25, sprite.Opacity, 6);
    }

    [Fact]
    public void Default_tint_is_white_and_opacity_is_clamped()
    {
        var plain = Sprite("icon = { texture = \"gfx/tex.dds\" }");
        Assert.Equal((1.0, 1.0, 1.0), (plain.Tint.R, plain.Tint.G, plain.Tint.B));
        Assert.Equal(1.0, plain.Opacity);

        Assert.Equal(1.0, Sprite("icon = { texture = \"gfx/tex.dds\" alpha = 3 }").Opacity);
        Assert.Equal(0.0, Sprite("icon = { texture = \"gfx/tex.dds\" alpha = -1 }").Opacity);
    }

    [Fact]
    public void Non_numeric_alpha_is_one_with_a_note()
    {
        var box = Layout("icon = { texture = \"gfx/tex.dds\" alpha = \"[GetAlpha]\" }");

        Assert.Equal(1.0, box.Sprite!.Opacity);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("alpha"), box.Notes);
    }

    [Fact]
    public void Named_colour_is_white_with_a_note()
    {
        var box = Layout("icon = { texture = \"gfx/tex.dds\" color = \"color_red\" }");

        Assert.Equal((1.0, 1.0, 1.0), (box.Sprite!.Tint.R, box.Sprite.Tint.G, box.Sprite.Tint.B));
        Assert.Contains(GuiTexts.LayoutTextureApproximated("color"), box.Notes);
    }

    [Fact]
    public void Measured_rules_give_no_notes_and_open_cases_still_do()
    {
        // Lauf 5 hat gemessen: Rand (13a–13c, 13g, 13h), Kacheln (13d), Rand ohne spriteType (13e), Spiegelung (13i–13k), fittype am icon (13l–13r),
        // Farbe und Deckkraft (13s–13x), Bilder (13y–14b). Diese Fälle bleiben ohne Hinweis.
        Assert.True(JominiSpriteRules.Measured);
        Assert.Empty(Layout("icon = { texture = \"gfx/tex.dds\" spriteType = corneredstretched spriteborder = { 4 4 } texture_density = 2 }").Notes);
        Assert.Empty(Layout("icon = { texture = \"gfx/tex.dds\" spriteType = corneredtiled mirror = horizontal }").Notes);

        // corneredtiled mit Rand: Kachelperiode von Kanten und Mitte nicht gemessen (13c zeigt sie nicht), Hinweis bleibt.
        Assert.Equal(new[] { GuiTexts.LayoutTextureApproximated("tiling") },
            Layout("icon = { texture = \"gfx/tex.dds\" spriteType = corneredtiled spriteborder = { 4 4 } }").Notes);
        Assert.Empty(Layout("icon = { texture = \"gfx/tex.dds\" fittype = centercrop color = { 1 0 0 0.5 } tintcolor = { 1 1 1 1 } alpha = 0.5 }").Notes);

        // Offen: Farbe über 1 (CK III rechnet anders), Alpha über 1, fittype am background, upframe bei framedbuttongfx.
        Assert.Equal(new[] { GuiTexts.LayoutTextureApproximated(JominiTextureColor.AlphaAboveOneNote) },
            Layout("icon = { texture = \"gfx/tex.dds\" color = { 1 1 1 2 } }").Notes);
        Assert.Equal(new[] { GuiTexts.LayoutTextureApproximated(JominiTextureColor.AboveOneNote) },
            Layout("icon = { texture = \"gfx/tex.dds\" color = { 200 20 0 0.8 } }").Notes);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("fittype"),
            Assert.Single(Layout("widget = { size = { 10 10 } background = { texture = \"gfx/tex.dds\" fittype = centercrop } }").Children).Notes);
        Assert.Contains(GuiTexts.LayoutTextureApproximated("upframe"),
            Layout("button = { texture = \"gfx/strip.dds\" framesize = { 40 40 } upframe = 2 gfxtype = framedbuttongfx }").Notes);
    }

    [Fact]
    public void Alpha_of_ancestors_is_multiplied_into_the_sprite()
    {
        // Lauf 5, Fall 13x (alle drei Spiele): widget mit alpha = 0.5 und weißem Kind sieht aus wie 13w (das Kind selbst mit alpha = 0.5).
        var box = Layout("widget = { size = { 100 50 } alpha = 0.5 background = { texture = \"gfx/tex.dds\" } " +
                         "widget = { size = { 100 50 } alpha = 0.5 icon = { texture = \"gfx/tex.dds\" alpha = 0.5 color = { 1 1 1 0.5 } } } }");

        var background = box.Children.Single(c => c.IsBackground);
        Assert.Equal(0.5, background.Sprite!.Opacity, 6);
        var icon = box.Children.Single(c => !c.IsBackground).Children.Single();
        Assert.Equal(0.5 * 0.5 * 0.5 * 0.5, icon.Sprite!.Opacity, 6);
    }

    [Fact]
    public void Alpha_zero_notes_only_the_topmost_invisible_box()
    {
        var box = Layout("widget = { size = { 100 50 } alpha = 0 background = { texture = \"gfx/tex.dds\" } " +
                         "widget = { size = { 100 50 } alpha = 0 icon = { texture = \"gfx/tex.dds\" } } }");

        Assert.Single(box.Notes, GuiTexts.LayoutAlphaInvisible);
        Assert.True(box.Uncertain);
        foreach (var descendant in box.Descendants())
        {
            Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, descendant.Notes);
        }

        Assert.Equal(0, box.Children.Single(c => !c.IsBackground).Children.Single().Sprite!.Opacity);
    }

    [Fact]
    public void Alpha_zero_on_a_sizeless_widget_is_noted_on_the_first_descendants_with_area()
    {
        // Übliche Einblend-Vorlage: widget ohne size (0×0) mit alpha = 0. Sein Rahmen hat keine Fläche; die Kinder mit Fläche tragen den Hinweis.
        var box = Layout("widget = { alpha = 0 icon = { size = { 20 20 } texture = \"gfx/tex.dds\" icon = { size = { 10 10 } texture = \"gfx/tex.dds\" } } " +
                         "widget = { icon = { size = { 30 30 } texture = \"gfx/tex.dds\" } } }");

        Assert.Contains(GuiTexts.LayoutAlphaInvisible, box.Notes);
        var icon = box.Children[0];
        Assert.Single(icon.Notes, GuiTexts.LayoutAlphaInvisible);
        Assert.True(icon.Uncertain);
        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, icon.Children.Single().Notes);

        // Durch ein weiteres widget ohne size hindurch bis zum ersten Kasten mit Fläche.
        var inner = box.Children[1];
        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, inner.Notes);
        Assert.Single(inner.Children.Single().Notes, GuiTexts.LayoutAlphaInvisible);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.04")]
    [InlineData("-1")]
    public void Own_alpha_near_zero_is_noted(string alpha)
    {
        var box = Layout($"widget = {{ size = {{ 100 50 }} icon = {{ texture = \"gfx/tex.dds\" alpha = {alpha} }} }}");

        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, box.Notes);
        Assert.Single(box.Children.Single().Notes, GuiTexts.LayoutAlphaInvisible);
    }

    [Fact]
    public void Visible_alpha_gives_no_invisible_note()
    {
        var box = Layout("widget = { size = { 100 50 } alpha = 0.5 icon = { texture = \"gfx/tex.dds\" alpha = 0.2 } }");

        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, box.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, box.Children.Single().Notes);
    }

    [Fact]
    public void Text_under_alpha_is_noted_as_unmeasured()
    {
        var box = Layout("widget = { size = { 200 100 } alpha = 0.5 textbox = { size = { 100 20 } raw_text = \"Hello\" font = \"Arial\" fontsize = 15 } } ", text: _text.Service);
        var own = Layout("textbox = { size = { 100 20 } alpha = 0.3 raw_text = \"Hello\" font = \"Arial\" fontsize = 15 }", text: _text.Service);
        var plain = Layout("textbox = { size = { 100 20 } raw_text = \"Hello\" font = \"Arial\" fontsize = 15 }", text: _text.Service);

        Assert.Contains(GuiTexts.LayoutAlphaTextUnmeasured, box.Children.Single().Notes);
        Assert.DoesNotContain(GuiTexts.LayoutAlphaTextUnmeasured, box.Notes);
        Assert.Contains(GuiTexts.LayoutAlphaTextUnmeasured, own.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutAlphaTextUnmeasured, plain.Notes);
    }

    [Theory]
    [InlineData("\"[GetAlpha]\"")]
    [InlineData("50%")]
    public void Unreadable_parent_alpha_counts_as_one_with_a_note(string alpha)
    {
        var box = Layout($"widget = {{ size = {{ 100 50 }} alpha = {alpha} icon = {{ texture = \"gfx/tex.dds\" }} }}");

        Assert.Single(box.Notes, GuiTexts.LayoutTextureApproximated("alpha"));
        Assert.DoesNotContain(GuiTexts.LayoutAlphaInvisible, box.Notes);
        Assert.Equal(1.0, box.Children.Single().Sprite!.Opacity, 6);
    }

    [Fact]
    public void Quoted_parent_alpha_is_read()
    {
        var box = Layout("widget = { size = { 100 50 } alpha = \"0.5\" icon = { texture = \"gfx/tex.dds\" } }");

        Assert.Equal(0.5, box.Children.Single().Sprite!.Opacity, 6);
    }

    [Fact]
    public void Approximation_notes_are_not_duplicated()
    {
        var box = Layout("icon = { texture = \"gfx/tex.dds\" spriteType = corneredstretched spriteborder = { 4 -4 } spriteborder_left = -1 }");

        Assert.Single(box.Notes, GuiTexts.LayoutTextureApproximated("spriteborder"));
    }

    [Fact]
    public void Dynamic_texture_is_a_placeholder_with_a_note()
    {
        var box = Layout("widget = { size = { 20 20 } texture = \"[GetIcon]\" }");

        Assert.Equal(JominiSpritePlaceholder.Dynamic, box.Sprite!.Placeholder);
        Assert.Contains(GuiTexts.LayoutTextureDynamic("[GetIcon]"), box.Notes);
    }

    [Fact]
    public void Empty_texture_gives_no_sprite()
    {
        Assert.Null(Layout("widget = { size = { 20 20 } texture = \"\" }").Sprite);
        Assert.Null(Layout("widget = { size = { 20 20 } }").Sprite);
    }

    [Theory]
    [InlineData("icon = { texture = \"gfx/none.dds\" }")]
    [InlineData("icon = { size = { 20 20 } texture = \"gfx/none.dds\" }")]
    public void Missing_texture_has_exactly_one_note(string gui)
    {
        var box = Layout(gui);

        Assert.Equal(JominiSpritePlaceholder.Missing, box.Sprite!.Placeholder);
        Assert.Null(box.Sprite.TextureFullPath);
        Assert.Single(box.Notes, GuiTexts.LayoutTextureMissing("gfx/none.dds"));
    }

    [Fact]
    public void Unsupported_format_is_a_placeholder_naming_it()
    {
        var box = Layout("icon = { texture = \"gfx/bc5.dds\" }");

        Assert.Equal(JominiSpritePlaceholder.UnsupportedFormat, box.Sprite!.Placeholder);
        Assert.Equal("DDS DXGI 83 (BC5)", box.Sprite.PlaceholderLabel);
        Assert.Contains(GuiTexts.LayoutTextureFormat("gfx/bc5.dds", "DDS DXGI 83 (BC5)"), box.Notes);
    }

    [Fact]
    public void Empty_file_is_an_unsupported_placeholder()
    {
        var box = Layout("widget = { size = { 20 20 } texture = \"gfx/empty.dds\" }");

        Assert.Equal(JominiSpritePlaceholder.UnsupportedFormat, box.Sprite!.Placeholder);
        Assert.Equal(GuiTexts.TextureFormatEmptyFile, box.Sprite.PlaceholderLabel);
    }

    [Fact]
    public void Empty_file_on_unsized_icon_has_only_the_format_note()
    {
        var box = Layout("icon = { texture = \"gfx/empty.dds\" }");

        Assert.Equal(JominiSpritePlaceholder.UnsupportedFormat, box.Sprite!.Placeholder);
        Assert.Single(box.Notes, GuiTexts.LayoutTextureFormat("gfx/empty.dds", GuiTexts.TextureFormatEmptyFile));
        Assert.DoesNotContain(GuiTexts.LayoutTextureMissing("gfx/empty.dds"), box.Notes);
        // Genau ein Hinweis nennt die Textur: der Formathinweis.
        Assert.Single(box.Notes, note => note.Contains("gfx/empty.dds", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_texture_without_locator_keeps_the_missing_note()
    {
        var box = Layout("icon = { texture = \"gfx/empty.dds\" }", locator: false);

        Assert.Null(box.Sprite);
        Assert.Single(box.Notes, GuiTexts.LayoutTextureMissing("gfx/empty.dds"));
    }

    [Fact]
    public void Bound_framesize_note_is_not_duplicated()
    {
        var box = Layout("icon = { texture = \"gfx/strip.dds\" framesize = \"[GetSize]\" }");

        Assert.Single(box.Notes, GuiTexts.LayoutTextureApproximated("framesize"));
    }

    [Fact]
    public void Portrait_kind_is_a_not_drawn_placeholder()
    {
        var box = Layout("my_portrait = { size = { 50 50 } }", "types T { type my_portrait = portrait_button { } }");

        Assert.Equal(JominiSpritePlaceholder.NotDrawn, box.Sprite!.Placeholder);
        Assert.Equal("portrait_button", box.Sprite.PlaceholderLabel);
        Assert.Contains(GuiTexts.LayoutWidgetNotDrawn("portrait_button"), box.Notes);
    }

    [Theory]
    [InlineData("coat_of_arms")]
    [InlineData("video")]
    [InlineData("flag3d")]
    [InlineData("piechart")]
    [InlineData("treemap")]
    public void Other_undrawable_kinds_are_not_drawn_even_with_a_texture(string kind)
    {
        var box = Layout("my_thing = { size = { 50 50 } texture = \"gfx/tex.dds\" }", $"types T {{ type my_thing = {kind} {{ }} }}");

        Assert.Equal(JominiSpritePlaceholder.NotDrawn, box.Sprite!.Placeholder);
        Assert.Contains(GuiTexts.LayoutWidgetNotDrawn(kind), box.Notes);
    }

    [Fact]
    public void Background_child_gets_its_sprite_and_stays_background()
    {
        var box = Layout("widget = { size = { 100 50 } background = { texture = \"gfx/tex.dds\" } }");

        var background = Assert.Single(box.Children);
        Assert.True(background.IsBackground);
        Assert.NotNull(background.Sprite);
        Assert.Equal(new LayoutRect(0, 0, 100, 100), background.Sprite!.Source);
    }

    [Fact]
    public void Button_keeps_texture_and_label()
    {
        var box = Layout("button = { size = { 100 20 } texture = \"gfx/tex.dds\" raw_text = \"Hello\" font = \"Arial\" fontsize = 15 }", text: _text.Service);

        Assert.NotNull(box.Sprite);
        Assert.IsType<JominiTextVisual>(box.Visual);
    }

    [Fact]
    public void Without_locator_there_are_no_sprites()
    {
        var box = Layout("icon = { texture = \"gfx/strip.dds\" framesize = { 40 40 } }", locator: false);

        Assert.Null(box.Sprite);
        Assert.Equal(40, box.Width, 3);
    }

    [Fact]
    public void Layout_does_not_decode()
    {
        var before = GuiImageDecoder.DecodeCalls;

        Layout("widget = { size = { 200 100 } icon = { texture = \"gfx/strip.dds\" } icon = { texture = \"gfx/tex.dds\" spriteType = corneredtiled spriteborder = { 4 4 } } background = { texture = \"gfx/frames.dds\" } }");

        Assert.Equal(before, GuiImageDecoder.DecodeCalls);
    }
}
