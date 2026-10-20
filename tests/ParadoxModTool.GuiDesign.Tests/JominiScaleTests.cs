using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// <c>scale</c> in der Jomini-Vorschau (Lauf 6, Fenster 7): Kasten und Nachkommen werden nach dem Layout um die Ecke oben links des Elements skaliert, das Fach
/// im Elternelement bleibt; Text mit eigenem <c>scale</c> wird in der ganzzahligen Schriftgröße floor(fontsize × scale) gesetzt.
/// </summary>
public sealed class JominiScaleTests : IClassFixture<JominiEngineTextFixture>, IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-scale-" + Guid.NewGuid().ToString("N"));
    private readonly GuiTextureLocator _textures;
    private readonly JominiEngineTextFixture _text;

    public JominiScaleTests(JominiEngineTextFixture text)
    {
        _text = text;
        TestDds.WriteBgra(Path.Combine(_temp, "install", "game", "gfx", "tex.dds"), 100, 100);
        TestDds.WriteBgra(Path.Combine(_temp, "install", "game", "gfx", "market.dds"), 400, 300);
        _textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(Path.Combine(_temp, "install"), Array.Empty<string>(), null, Array.Empty<string>()));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private LayoutBox Layout(string gui, bool withText = false)
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(GuiResolverTests.Rules, _textures.SizeOf, withText ? _text.Service : null, _textures).Layout(element);
    }

    private static void AssertRect(LayoutBox box, double x, double y, double width, double height)
    {
        Assert.Equal(x, box.X, 3);
        Assert.Equal(y, box.Y, 3);
        Assert.Equal(width, box.Width, 3);
        Assert.Equal(height, box.Height, 3);
    }

    [Fact]
    public void Scale_shrinks_own_box_around_top_left()
    {
        var icon = Layout("widget = { size = { 200 100 } icon = { texture = \"gfx/tex.dds\" size = { 100 50 } position = { 20 20 } scale = 0.5 } }").Children.Single();

        AssertRect(icon, 20, 20, 50, 25);
        Assert.Equal(0.5, icon.RenderScale, 6);
        // Drehpunkt und Kasten sind gemessen (15a): kein Hinweis.
        Assert.Empty(icon.Notes);
    }

    [Fact]
    public void Scale_scales_children_around_the_scaled_element()
    {
        // Probe 15a: Kind G 20×20 bei { 10 10 } eines skalierten Icons bei { 20 20 } liegt bei 25/25 und ist 10×10.
        var icon = Layout(
            "widget = { size = { 200 100 } icon = { texture = \"gfx/tex.dds\" size = { 100 50 } position = { 20 20 } scale = 0.5 " +
            "icon = { texture = \"gfx/tex.dds\" size = { 20 20 } position = { 10 10 } } } }").Children.Single();
        var child = icon.Children.Single();

        AssertRect(child, 25, 25, 10, 10);
        Assert.Equal(0.5, child.RenderScale, 6);
        Assert.Empty(child.Notes);
    }

    [Fact]
    public void Nested_scales_multiply()
    {
        var outer = Layout(
            "widget = { size = { 400 400 } widget = { size = { 200 200 } position = { 100 100 } scale = 0.5 " +
            "icon = { texture = \"gfx/tex.dds\" size = { 40 40 } position = { 20 20 } scale = 0.5 } } }").Children.Single();
        var inner = outer.Children.Single();

        AssertRect(outer, 100, 100, 100, 100);
        AssertRect(inner, 110, 110, 10, 10);
        Assert.Equal(0.25, inner.RenderScale, 6);
        // Verschachtelte Faktoren sind nicht gemessen.
        Assert.Contains(GuiTexts.LayoutScaleApproximated("scale × scale"), inner.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutScaleApproximated("scale × scale"), outer.Notes);
    }

    [Fact]
    public void Scale_does_not_change_the_slot_in_an_hbox()
    {
        // Probe 15b: R/G/B je 50×40 fixed, G mit scale 0.5; verteilt auf Fächer je 100, G an der Ecke oben links seines Fachs.
        var hbox = Layout(
            "widget = { size = { 300 60 } hbox = { " +
            "icon = { texture = \"gfx/tex.dds\" size = { 50 40 } layoutpolicy_horizontal = fixed layoutpolicy_vertical = fixed } " +
            "icon = { texture = \"gfx/tex.dds\" size = { 50 40 } layoutpolicy_horizontal = fixed layoutpolicy_vertical = fixed scale = 0.5 } " +
            "icon = { texture = \"gfx/tex.dds\" size = { 50 40 } layoutpolicy_horizontal = fixed layoutpolicy_vertical = fixed } } }").Children.Single();

        AssertRect(hbox.Children[0], 25, 10, 50, 40);
        AssertRect(hbox.Children[1], 125, 10, 25, 20);
        AssertRect(hbox.Children[2], 225, 10, 50, 40);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("50%")]
    public void Invalid_scale_is_one_with_note(string value)
    {
        var icon = Layout($"widget = {{ size = {{ 200 100 }} icon = {{ texture = \"gfx/tex.dds\" size = {{ 100 50 }} scale = {value} }} }}").Children.Single();

        AssertRect(icon, 0, 0, 100, 50);
        Assert.Equal(1, icon.RenderScale, 6);
        Assert.Contains(GuiTexts.LayoutScaleInvalid(value), icon.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutTextureApproximated("scale"), icon.Notes);
    }

    [Theory]
    [InlineData("\"[GetScale]\"", "[GetScale]")]
    [InlineData("@unknown_scale", "@unknown_scale")]
    public void Bound_or_unresolved_scale_is_one_with_note(string value, string shown)
    {
        var icon = Layout($"widget = {{ size = {{ 200 100 }} icon = {{ texture = \"gfx/tex.dds\" size = {{ 100 50 }} scale = {value} }} }}").Children.Single();

        AssertRect(icon, 0, 0, 100, 50);
        Assert.Equal(1, icon.RenderScale, 6);
        Assert.Single(icon.Notes, GuiTexts.LayoutScaleUnresolved(shown));
    }

    [Fact]
    public void Scale_from_show_state_applies()
    {
        var icon = Layout("widget = { size = { 200 100 } icon = { texture = \"gfx/tex.dds\" size = { 100 50 } scale = 1 state = { name = _show scale = 0.5 } } }").Children.Single();

        AssertRect(icon, 0, 0, 50, 25);
        Assert.Equal(0.5, icon.RenderScale, 6);
    }

    [Fact]
    public void Bound_scale_in_show_state_keeps_the_base_value_with_one_note()
    {
        var icon = Layout("widget = { size = { 200 100 } icon = { texture = \"gfx/tex.dds\" size = { 100 50 } scale = 0.5 state = { name = _show scale = \"[GetScale]\" } } }").Children.Single();

        AssertRect(icon, 0, 0, 50, 25);
        Assert.Single(icon.Notes, GuiTexts.LayoutScaleUnresolved("[GetScale]"));
    }

    [Fact]
    public void Market_icon_with_scale_shrinks_its_texture_size()
    {
        // Flogi's Buildings and Technology Mod, show_flogi_market: Hintergrund-Icons mit scale = 0.38 (Textur 400×300).
        var window = Layout("widget = { size = { 300 200 } icon = { texture = \"gfx/market.dds\" scale = 0.38 } }");
        var icon = window.Children.Single();

        AssertRect(icon, 0, 0, 152, 114);
        // Überstand und Beschnitt sehen den skalierten Kasten.
        Assert.False(icon.OutsideParent);
    }

    [Fact]
    public void Clip_of_a_scaled_scrollarea_is_scaled()
    {
        var area = Layout(
            "widget = { size = { 400 400 } scrollarea = { size = { 200 200 } position = { 10 10 } scale = 0.5 " +
            "icon = { texture = \"gfx/tex.dds\" size = { 300 300 } } } }").Children.Single();
        var icon = area.Children.Single(c => !c.IsBackground);

        Assert.Equal(new LayoutRect(10, 10, 100, 100), icon.Clip);
    }

    [Fact]
    public void Border_sprite_under_scale_gets_a_note()
    {
        var icon = Layout(
            "widget = { size = { 200 100 } icon = { texture = \"gfx/tex.dds\" size = { 100 50 } scale = 0.5 spriteType = corneredstretched spriteborder = { 8 8 } } }").Children.Single();

        Assert.Equal(0.5, icon.RenderScale, 6);
        Assert.Contains(GuiTexts.LayoutScaleApproximated("spriteborder"), icon.Notes);
    }

    [Fact]
    public void Scaled_text_is_set_at_the_floored_font_size_and_its_slot_is_divided_by_scale()
    {
        // Probe 15c (alle drei Spiele): Text in Größe floor(15 × 0,5) = 7 gesetzt, das Fach in der hbox ist diese Breite / 0,5.
        const string row = "widget = {{ size = {{ 600 40 }} hbox = {{ " +
                           "icon = {{ texture = \"gfx/tex.dds\" size = {{ 4 30 }} layoutpolicy_horizontal = fixed layoutpolicy_vertical = fixed }} " +
                           "textbox = {{ raw_text = \"Probe text 123\" font = \"StandardGameFont\" fontsize = {0} autoresize = yes {1} }} " +
                           "icon = {{ texture = \"gfx/tex.dds\" size = {{ 4 30 }} layoutpolicy_horizontal = fixed layoutpolicy_vertical = fixed }} expand = {{}} }} }}";
        var scaled = Layout(string.Format(row, 15, "scale = 0.5"), withText: true).Children.Single();
        var seven = Layout(string.Format(row, 7, string.Empty), withText: true).Children.Single();
        var sevenWidth = seven.Children[1].Width;

        var text = scaled.Children[1];
        Assert.Equal(sevenWidth, text.Width, 3);
        Assert.Equal(4 + sevenWidth / 0.5, scaled.Children[2].X, 3);
        Assert.Equal(0.5, text.RenderScale, 6);
        var visual = Assert.IsType<JominiTextVisual>(text.Visual);
        var run = visual.Lines.Single().Runs.Single();
        Assert.Equal(7, run.FontSize * text.RenderScale, 6);
        Assert.Equal(sevenWidth, visual.Width * text.RenderScale, 3);
        // Ein Punkt gemessen (15 × 0,5): Hinweis am skalierten Text.
        Assert.Contains(GuiTexts.LayoutScaleApproximated("text"), text.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("default_format = \"#big\"")]
    public void Fixed_format_sizes_and_fixed_icons_shrink_with_the_own_scale(string defaultFormat)
    {
        // Unter eigenem scale 0,5 wird alles in der gesetzten Größe gesetzt: Grundgröße floor(15 × 0,5) = 7, feste Formatgröße floor(30 × 0,5) = 15,
        // Texticon fester Größe (ohne iconsize.fontsize) 20 × 20 mit Versatz 4 → 10 × 10 mit Versatz 2. Vergleich: derselbe Text ungeskaliert in diesen Größen.
        const string row = "widget = {{ size = {{ 900 60 }} hbox = {{ " +
                           "icon = {{ texture = \"gfx/tex.dds\" size = {{ 4 30 }} layoutpolicy_horizontal = fixed layoutpolicy_vertical = fixed }} " +
                           "textbox = {{ raw_text = \"Ab #big CD#! @coin! ef\" font = \"Arial\" fontsize = {0} autoresize = yes {1} {2} }} " +
                           "icon = {{ texture = \"gfx/tex.dds\" size = {{ 4 30 }} layoutpolicy_horizontal = fixed layoutpolicy_vertical = fixed }} expand = {{}} }} }}";
        const string library = "textformatting = {{ format = {{ name = big format = \"size:{0}\" }} }}\n" +
                               "texticon = {{ icon = coin iconsize = {{ texture = \"gfx/coin.dds\" size = {{ {1} {1} }} offset = {{ 0 {2} }} }} }}";
        var scaled = LayoutWith(string.Format(row, 15, "scale = 0.5", defaultFormat), string.Format(library, 30, 20, 4)).Children.Single();
        var reference = LayoutWith(string.Format(row, 7, string.Empty, defaultFormat), string.Format(library, 15, 10, 2)).Children.Single();
        var setWidth = reference.Children[1].Width;

        var text = scaled.Children[1];
        Assert.Equal(0.5, text.RenderScale, 6);
        // Gezeichnet (Kasten nach scale) und gemessen (Fach in der hbox = gesetzte Breite / 0,5) passen zueinander.
        Assert.Equal(setWidth, text.Width, 3);
        Assert.Equal(4 + setWidth / 0.5, scaled.Children[2].X, 3);
        var visual = Assert.IsType<JominiTextVisual>(text.Visual);
        Assert.Equal(setWidth, visual.Width * text.RenderScale, 3);
        var runs = visual.Lines.Single().Runs;
        var referenceRuns = Assert.IsType<JominiTextVisual>(reference.Children[1].Visual).Lines.Single().Runs;
        Assert.Equal(referenceRuns.Count, runs.Count);
        for (var i = 0; i < runs.Count; i++)
        {
            Assert.Equal(referenceRuns[i].FontSize, runs[i].FontSize * text.RenderScale, 6);
            Assert.Equal(referenceRuns[i].Width, runs[i].Width * text.RenderScale, 3);
            Assert.Equal(referenceRuns[i].IconHeight, runs[i].IconHeight * text.RenderScale, 3);
            Assert.Equal(referenceRuns[i].IconOffsetY, runs[i].IconOffsetY * text.RenderScale, 3);
        }

        Assert.Contains(runs, run => run.Kind == JominiTextRunKind.Icon && Math.Abs(run.Width * text.RenderScale - 10) < 1e-3 && Math.Abs(run.IconOffsetY * text.RenderScale - 2) < 1e-3);
    }

    private LayoutBox LayoutWith(string gui, string library)
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(GuiResolverTests.Rules, _textures.SizeOf, _text.ServiceWithTypes(library), _textures).Layout(element);
    }

    [Fact]
    public void Unscaled_text_has_no_scale_note()
    {
        var text = Layout("widget = { size = { 600 40 } textbox = { raw_text = \"Probe\" font = \"StandardGameFont\" fontsize = 15 autoresize = yes } }", withText: true).Children.Single();

        Assert.Equal(1, text.RenderScale, 6);
        Assert.DoesNotContain(text.Notes, n => n.StartsWith("scale", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("parentanchor = center")]
    [InlineData("parentanchor = right")]
    [InlineData("widgetanchor = bottom|left")]
    public void Scale_with_a_non_top_left_anchor_gets_a_note(string anchor)
    {
        // 15a/15b messen den Drehpunkt nur an Elementen oben links; mit anderem Anker ist er nicht gemessen.
        var icon = Layout($"widget = {{ size = {{ 400 200 }} icon = {{ texture = \"gfx/tex.dds\" size = {{ 100 50 }} {anchor} scale = 0.5 }} }}").Children.Single();

        Assert.Contains(GuiTexts.LayoutScaleApproximated("anchor"), icon.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("parentanchor = top|left")]
    public void Scale_with_a_top_left_anchor_has_no_anchor_note(string anchor)
    {
        var icon = Layout($"widget = {{ size = {{ 400 200 }} icon = {{ texture = \"gfx/tex.dds\" size = {{ 100 50 }} {anchor} scale = 0.5 }} }}").Children.Single();

        Assert.DoesNotContain(GuiTexts.LayoutScaleApproximated("anchor"), icon.Notes);
    }

    [Theory]
    [InlineData(0.38)]
    [InlineData(0.39)]
    [InlineData(0.45)]
    [InlineData(0.7)]
    [InlineData(0.9)]
    public void Scaled_autoresize_text_with_elide_is_not_elided(double scale)
    {
        // Die gemessene Breite (gesetzte Breite / scale) mal scale darf nicht um eine Rundungsstelle unter der gesetzten Breite landen.
        foreach (var text in new[] { "Probe text 123", "Market", "Hello world, this is a longer label", "iii", "W" })
        {
            var box = Layout(
                "widget = { size = { 600 40 } textbox = { raw_text = \"" + text + "\" font = \"StandardGameFont\" fontsize = 15 autoresize = yes elide = right scale = " +
                scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + " } }",
                withText: true).Children.Single();
            var visual = Assert.IsType<JominiTextVisual>(box.Visual);

            Assert.False(visual.Elided, text + " bei " + scale);
        }
    }
}
