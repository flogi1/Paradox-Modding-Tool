using System.Buffers.Binary;
using System.Diagnostics;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Synthetischer Textdienst für die Engine-Tests: eine Installation im Temp-Ordner mit <c>game/fonts</c> (Arial aus dem Windows-Schriftordner, als
/// <c>Arial</c> und <c>StandardGameFont</c>) und englischer bzw. deutscher Lokalisierung.
/// </summary>
public sealed class JominiEngineTextFixture : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-jominiengine-" + Guid.NewGuid().ToString("N"));

    public JominiEngineTextFixture()
    {
        var install = Path.Combine(_temp, "install");
        var game = JominiTestFonts.Root(_temp, Path.Combine("install", "game"),
            JominiTestFonts.FontFiles("Arial-Regular", ("\"l_english\" \"l_german\"", "\"fonts/a/arial.ttf\"")) +
            JominiTestFonts.Font("Arial", ("regular", "Arial-Regular")) +
            JominiTestFonts.Font("StandardGameFont", ("regular", "Arial-Regular")),
            "arial.ttf");
        Yml(Path.Combine(game, "localization", "english", "t_l_english.yml"), "english", "HELLO:0 \"Hello\"", "ONLY_EN:0 \"Only English\"");
        Yml(Path.Combine(game, "localization", "german", "t_l_german.yml"), "german", "HELLO:0 \"Hallo\"");

        _textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install, Array.Empty<string>(), null, Array.Empty<string>()));
        _options = new GuiWorkspaceOptions(install, null, Array.Empty<string>(), GuiResolverTests.Rules, null);
        Service = JominiTextService.Build(_options, GuiIndexTests.Build(), _textures);
        Arial = JominiFontLibrary.Load(new[] { game }).Face("Arial", JominiFontStyle.Regular, "l_english")!;

        // Zweite Installation nur mit Arial (ohne StandardGameFont), wie Victoria 3.
        var bare = Path.Combine(_temp, "bare");
        JominiTestFonts.Root(_temp, Path.Combine("bare", "game"),
            JominiTestFonts.FontFiles("Arial-Regular", ("\"l_english\"", "\"fonts/a/arial.ttf\"")) + JominiTestFonts.Font("Arial", ("regular", "Arial-Regular")),
            "arial.ttf");
        ServiceWithoutStandardFont = JominiTextService.Build(
            new GuiWorkspaceOptions(bare, null, Array.Empty<string>(), GuiResolverTests.Rules, null),
            GuiIndexTests.Build(),
            new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(bare, Array.Empty<string>(), null, Array.Empty<string>())));
    }

    private readonly GuiTextureLocator _textures;
    private readonly GuiWorkspaceOptions _options;

    public JominiTextService Service { get; }

    public JominiTextService ServiceWithoutStandardFont { get; }

    /// <summary>Dienst über dieselben Schriften mit einem Index aus <paramref name="library"/> (Typen, Vorlagen).</summary>
    public JominiTextService ServiceWithTypes(string library) =>
        JominiTextService.Build(_options, GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/library.gui"), library)), _textures);

    public JominiFontFace Arial { get; }

    public void Dispose() => JominiTestFonts.TryDelete(_temp);

    private static void Yml(string path, string language, params string[] lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "l_" + language + ":\n" + string.Concat(lines.Select(line => " " + line + "\n")), new System.Text.UTF8Encoding(true));
    }
}

public class GuiLayoutEngineTests : IClassFixture<JominiEngineTextFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly JominiEngineTextFixture _text;

    public GuiLayoutEngineTests(ITestOutputHelper output, JominiEngineTextFixture text)
    {
        _output = output;
        _text = text;
    }

    private static LayoutBox Layout(string gui, LayoutOptions? options = null, Func<string, (int Width, int Height)?>? textures = null, string library = "")
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui, library);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(GuiResolverTests.Rules, textures ?? (_ => null)).Layout(element, options);
    }

    [Fact]
    public void UberEngineWidgetsLayOutLikeTheirPlainNames()
    {
        var box = Layout(
            "widget = { size = { 85 20 } hbox = { spacing = 5 widget = { size = { 40 20 } } widget = { size = { 40 20 } } } }",
            library: "types D { type hbox = uberhbox { } }").Children[0];

        Assert.Equal("uberhbox", box.Element.BaseWidget);
        AssertRect(box, 0, 0, 85, 20);
        Assert.Equal(45, box.Children[1].X, 3);
    }

    private static void AssertRect(LayoutBox box, double x, double y, double width, double height)
    {
        Assert.Equal(x, box.X, 3);
        Assert.Equal(y, box.Y, 3);
        Assert.Equal(width, box.Width, 3);
        Assert.Equal(height, box.Height, 3);
    }

    [Fact]
    public void CentersWindowOnScreen()
    {
        AssertRect(Layout("window = { size = { 200 100 } parentanchor = center }"), 860, 490, 200, 100);
    }

    [Fact]
    public void ChildPositionIsRelativeToParent()
    {
        var box = Layout("window = { size = { 200 100 } position = { 10 20 } widget = { size = { 50 50 } position = { 5 6 } } }");

        AssertRect(box.Children[0], 15, 26, 50, 50);
    }

    [Fact]
    public void AnchorsAtBottomRight()
    {
        var box = Layout("window = { size = { 200 100 } icon = { size = { 50 20 } parentanchor = bottom|right position = { -5 -5 } } }");

        AssertRect(box.Children[0], 145, 75, 50, 20);
    }

    [Fact]
    public void PercentSizeUsesTheParentSize()
    {
        var box = Layout("window = { size = { 200 100 } widget = { size = { 50% 100% } } }");

        AssertRect(box.Children[0], 0, 0, 100, 100);
    }

    [Fact]
    public void HboxGivesFreeSpaceToExpandAndCentersVertically()
    {
        var box = Layout("hbox = { size = { 300 50 } spacing = 10 widget = { size = { 50 20 } } expand = { } widget = { size = { 50 20 } } }");

        Assert.Equal(3, box.Children.Count);
        AssertRect(box.Children[0], 0, 15, 50, 20);
        Assert.Equal(60, box.Children[1].X, 3);
        Assert.Equal(180, box.Children[1].Width, 3);
        AssertRect(box.Children[2], 250, 15, 50, 20);
    }

    [Fact]
    public void HboxWithoutSizeFitsChildrenSpacingAndMargin()
    {
        // Die hbox ohne size füllt das widget; feste Kinder stehen per space-around: Inhalt 180, frei 95, Polster 47,5.
        var box = Layout("widget = { size = { 200 40 } hbox = { spacing = 5 margin = { 10 4 } widget = { size = { 40 20 } } widget = { size = { 40 20 } } } }").Children[0];

        AssertRect(box, 0, 0, 200, 40);
        Assert.Equal(10 + 23.75, box.Children[0].X, 3);
        Assert.Equal(10 + 23.75 + 40 + 47.5 + 5, box.Children[1].X, 3);
        Assert.Equal(10, box.Children[0].Y, 3);
    }

    // Eine hbox ohne size in einem widget fester Größe; die Kinder sind Icons ohne Textur.
    private static LayoutBox Hbox(string children, double width = 400, double height = 30, string kind = "hbox") =>
        Layout($"widget = {{ size = {{ {width} {height} }} {kind} = {{ {children} }} }}").Children[0];

    private static string Item(double size, string extra = "") => $"icon = {{ size = {{ {size} 20 }} {extra} }}";

    private static double[] Widths(LayoutBox box) => box.Children.Select(c => Math.Round(c.Width, 3)).ToArray();

    private static double[] Xs(LayoutBox box) => box.Children.Select(c => Math.Round(c.X, 3)).ToArray();

    [Fact]
    public void Expanding_children_share_free_space_equally()
    {
        var box = Hbox(Item(50, "layoutpolicy_horizontal = fixed") + Item(50, "layoutpolicy_horizontal = expanding") + Item(150, "layoutpolicy_horizontal = expanding"));

        Assert.Equal(new double[] { 50, 125, 225 }, Widths(box));
        Assert.Equal(new double[] { 0, 50, 175 }, Xs(box));
    }

    [Fact]
    public void Growing_gets_nothing_next_to_expanding()
    {
        var withFixed = Hbox(Item(50, "layoutpolicy_horizontal = fixed") + Item(50, "layoutpolicy_horizontal = growing") + Item(150, "layoutpolicy_horizontal = expanding"));
        var withGrowing = Hbox(Item(50, "layoutpolicy_horizontal = Expanding") + Item(50, "layoutpolicy_horizontal = fixed") + Item(50, "layoutpolicy_horizontal = growing"));

        Assert.Equal(new double[] { 50, 50, 300 }, Widths(withFixed));
        Assert.Equal(new double[] { 300, 50, 50 }, Widths(withGrowing));
    }

    [Fact]
    public void Growing_and_preferred_share_without_expanding()
    {
        Assert.Equal(new double[] { 150, 250 }, Widths(Hbox(Item(50, "layoutpolicy_horizontal = growing") + Item(150, "layoutpolicy_horizontal = preferred"))));
    }

    [Fact]
    public void Stretch_factors_split_final_size()
    {
        var box = Hbox(
            Item(50, "layoutpolicy_horizontal = expanding layoutstretchfactor_horizontal = 1")
            + Item(50, "layoutpolicy_horizontal = expanding layoutstretchfactor_horizontal = 3"));

        Assert.Equal(new double[] { 100, 300 }, Widths(box));
    }

    [Fact]
    public void Maximum_size_caps_and_redistributes()
    {
        var box = Hbox(Item(50, "layoutpolicy_horizontal = expanding maximumsize = { 80 -1 }") + Item(50, "layoutpolicy_horizontal = expanding"));

        Assert.Equal(new double[] { 80, 320 }, Widths(box));
    }

    [Fact]
    public void Stretch_factors_never_shrink_below_base_size()
    {
        // Ziel 1:3 wäre 100/300; das erste Kind bleibt bei seiner Basis 150, das zweite nimmt den Rest.
        var box = Hbox(
            Item(150, "layoutpolicy_horizontal = expanding layoutstretchfactor_horizontal = 1")
            + Item(50, "layoutpolicy_horizontal = expanding layoutstretchfactor_horizontal = 3"));

        Assert.Equal(new double[] { 150, 250 }, Widths(box));
        Assert.DoesNotContain(GuiTexts.LayoutFreeSpaceUnmeasured, box.Notes);
    }

    [Fact]
    public void Free_space_left_after_all_maximum_sizes_adds_a_note()
    {
        var capped = Hbox(Item(50, "layoutpolicy_horizontal = expanding maximumsize = { 80 -1 }") + Item(50, "layoutpolicy_horizontal = growing"));
        var zeroFactors = Hbox(Item(50, "layoutpolicy_horizontal = expanding layoutstretchfactor_horizontal = 0"));
        var stretchCapped = Hbox(Item(50, "layoutpolicy_horizontal = expanding layoutstretchfactor_horizontal = 2 maximumsize = { 100 -1 }"));

        Assert.Equal(new double[] { 80, 50 }, Widths(capped));
        Assert.Equal(new double[] { 0, 80 }, Xs(capped));
        Assert.Contains(GuiTexts.LayoutFreeSpaceUnmeasured, capped.Notes);
        Assert.Contains(GuiTexts.LayoutFreeSpaceUnmeasured, zeroFactors.Notes);
        Assert.Equal(100, stretchCapped.Children[0].Width, 3);
        Assert.Contains(GuiTexts.LayoutFreeSpaceUnmeasured, stretchCapped.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutFreeSpaceUnmeasured, Hbox(Item(50, "layoutpolicy_horizontal = expanding")).Notes);
    }

    [Fact]
    public void Explicit_policy_on_expand_wins_over_growing()
    {
        var box = Hbox("expand = { layoutpolicy_horizontal = fixed } " + Item(50));

        // Beide fest: space-around statt Wachsen des expand (frei 350, Polster 175).
        Assert.Equal(new double[] { 0, 50 }, Widths(box));
        Assert.Equal(new double[] { 87.5, 262.5 }, Xs(box));
    }

    [Fact]
    public void Root_hbox_without_parent_keeps_its_content_size()
    {
        // Wurzel einer Einzelvorschau (kein Elternelement): Inhaltsgröße wie vor der Füllregel, kein freier Platz.
        var box = Layout("hbox = { spacing = 5 widget = { size = { 40 20 } } widget = { size = { 40 20 } } }");

        AssertRect(box, 0, 0, 85, 20);
        Assert.Equal(new double[] { 0, 45 }, Xs(box));
        Assert.Empty(box.Notes);
    }

    [Fact]
    public void Children_without_policy_are_fixed()
    {
        var box = Hbox(Item(50) + Item(50));

        Assert.Equal(new double[] { 50, 50 }, Widths(box));
        Assert.Equal(new double[] { 75, 275 }, Xs(box));
    }

    [Fact]
    public void Only_fixed_children_use_space_around()
    {
        var box = Hbox(Item(50) + Item(100) + Item(150, "layoutpolicy_horizontal = fixed"));

        Assert.Equal(new[] { 16.667, 100, 233.333 }, Xs(box));
    }

    [Fact]
    public void Vbox_uses_the_same_rules_vertically()
    {
        var box = Layout("widget = { size = { 60 200 } vbox = { icon = { size = { 40 20 } } icon = { size = { 40 20 } layoutpolicy_vertical = expanding } icon = { size = { 40 60 } layoutpolicy_vertical = expanding } } }").Children[0];

        Assert.Equal(new double[] { 20, 70, 110 }, box.Children.Select(c => Math.Round(c.Height, 3)));
        Assert.Equal(new double[] { 0, 20, 90 }, box.Children.Select(c => Math.Round(c.Y, 3)));
    }

    [Fact]
    public void Preferred_and_shrinking_shrink_to_minimum()
    {
        var equal = Hbox(
            Item(100, "layoutpolicy_horizontal = preferred minimumsize = { 40 -1 }")
            + Item(100, "layoutpolicy_horizontal = shrinking minimumsize = { 40 -1 }")
            + Item(100, "layoutpolicy_horizontal = fixed minimumsize = { 40 -1 }"),
            width: 200);
        var limited = Hbox(
            Item(100, "layoutpolicy_horizontal = preferred minimumsize = { 80 -1 }")
            + Item(100, "layoutpolicy_horizontal = preferred minimumsize = { 20 -1 }")
            + Item(100, "layoutpolicy_horizontal = fixed"),
            width: 200);

        Assert.Equal(new double[] { 50, 50, 100 }, Widths(equal));
        Assert.Equal(new double[] { 80, 20, 100 }, Widths(limited));
        Assert.Equal(new double[] { 0, 80, 100 }, Xs(limited));
    }

    [Fact]
    public void Fixed_children_overflow_past_the_end()
    {
        var box = Hbox(Item(100) + Item(100) + Item(100, "layoutpolicy_horizontal = growing"), width: 200);

        Assert.Equal(new double[] { 100, 100, 100 }, Widths(box));
        Assert.Equal(new double[] { 0, 100, 200 }, Xs(box));
    }

    [Fact]
    public void Sizeless_hbox_in_widget_fills_the_widget()
    {
        var widget = Layout("widget = { size = { 300 50 } position = { 10 20 } hbox = { parentanchor = right position = { 5 5 } icon = { size = { 40 20 } } } }");

        AssertRect(widget.Children[0], 10, 20, 300, 50);
        // Dass Anker und position beim Füllen nicht wirken, ist nicht gemessen: Hinweis statt stiller Annahme.
        Assert.Equal(new[] { GuiTexts.LayoutFillIgnoresAnchor }, widget.Children[0].Notes);
    }

    [Fact]
    public void Nested_sizeless_boxes_take_content_size()
    {
        // 9e: innere hbox hat Inhaltsgröße 80 und steht wie ein festes Kind (space-around).
        var nested = Hbox(
            "hbox = { icon = { size = { 40 20 } layoutpolicy_horizontal = fixed } icon = { size = { 40 20 } layoutpolicy_horizontal = fixed } }"
            + Item(40, "layoutpolicy_horizontal = fixed"),
            height: 40);
        // 9g: nur die äußerste hbox füllt das widget, das Icon steht mittig.
        var deep = Hbox("hbox = { hbox = { icon = { size = { 40 20 } layoutpolicy_horizontal = fixed layoutpolicy_vertical = fixed } } }", height: 60);

        AssertRect(nested.Children[0], 70, 10, 80, 20);
        Assert.Equal(290, nested.Children[1].X, 3);
        var icon = deep.Children[0].Children[0].Children[0];
        AssertRect(icon, 180, 20, 40, 20);
    }

    [Fact]
    public void Sizeless_box_in_zero_size_widget_takes_content_size_with_note()
    {
        var widget = Layout("widget = { size = { 0 0 } position = { 10 20 } hbox = { spacing = 5 icon = { size = { 40 20 } } icon = { size = { 30 10 } } } }");

        AssertRect(widget.Children[0], 10, 20, 75, 20);
        Assert.Contains(GuiTexts.LayoutBoxInZeroParent, widget.Children[0].Notes);
    }

    [Fact]
    public void Sizeless_box_in_sizeless_widget_takes_content_size_with_note()
    {
        // Vanilla-Normalfall widget = { hbox = { … } }: das widget ohne size ist 0×0 (Regel 9), die hbox nimmt ihre Inhaltsgröße.
        var widget = Layout("widget = { position = { 10 20 } hbox = { spacing = 5 icon = { size = { 40 20 } } icon = { size = { 30 10 } } } }");

        AssertRect(widget, 10, 20, 0, 0);
        Assert.DoesNotContain(GuiTexts.LayoutSizeUnknown, widget.Notes);
        AssertRect(widget.Children[0], 10, 20, 75, 20);
        Assert.Contains(GuiTexts.LayoutBoxInZeroParent, widget.Children[0].Notes);
    }

    [Fact]
    public void Widget_without_size_is_zero_and_children_still_place()
    {
        // 6b: widget ohne size bei (30,20) mit Kind bei (10,10) 40×40; der Marker right|bottom liegt bei (24,14).
        var widget = Layout($"widget = {{ size = {{ 300 100 }} widget = {{ position = {{ 30 20 }} {Icon(40, 40, "position = { 10 10 }")} {Icon(6, 6, "parentanchor = right|bottom")} }} }}").Children[0];

        AssertRect(widget, 30, 20, 0, 0);
        Assert.Empty(widget.Notes);
        AssertRect(widget.Children[0], 40, 30, 40, 40);
        AssertRect(widget.Children[1], 24, 14, 6, 6);
    }

    [Fact]
    public void Container_without_size_wraps_children_from_origin()
    {
        // 6c: container ohne size wächst auf die Hülle der Kinder ab (0,0): 50×50, Marker bei (74,64).
        var container = Layout($"widget = {{ size = {{ 300 100 }} container = {{ position = {{ 30 20 }} {Icon(40, 40, "position = { 10 10 }")} {Icon(6, 6, "parentanchor = right|bottom")} }} }}").Children[0];

        AssertRect(container, 30, 20, 50, 50);
        Assert.Empty(container.Notes);
        AssertRect(container.Children[0], 40, 30, 40, 40);
        AssertRect(container.Children[1], 74, 64, 6, 6);
    }

    [Fact]
    public void Minus_one_means_unset()
    {
        (int, int)? White(string path) => path == "gfx/white.dds" ? (10, 10) : null;

        // 6d: frei steht ein Icon mit size = { -1 30 } mit Breite 0 da, die Texturgröße zählt nicht.
        var free = Layout("widget = { size = { 300 100 } icon = { texture = \"gfx/white.dds\" size = { -1 30 } } }", textures: White).Children[0];
        AssertRect(free, 0, 0, 0, 30);
        Assert.Empty(free.Notes);

        // 6e: in einer vbox mit layoutpolicy_horizontal = expanding füllt es die Breite, senkrecht mittig.
        var stretched = Layout("widget = { size = { 300 100 } vbox = { icon = { texture = \"gfx/white.dds\" size = { -1 30 } layoutpolicy_horizontal = expanding } } }", textures: White).Children[0].Children[0];
        AssertRect(stretched, 0, 35, 300, 30);
        Assert.Empty(stretched.Notes);

        // -1 an einer Box: nicht gesetzt, also Inhaltsgröße in dieser Achse.
        var hbox = Layout($"widget = {{ size = {{ 300 100 }} hbox = {{ size = {{ -1 30 }} {Icon(40, 20)} }} }}").Children[0];
        AssertRect(hbox, 0, 0, 40, 30);
        Assert.Empty(hbox.Notes);
    }

    [Fact]
    public void Texture_density_divides_natural_size()
    {
        // 6f/6g: Icon ohne size = Texturgröße / texture_density (Standard 1).
        (int, int)? Skills(string path) => path == "gfx/skills.dds" ? (360, 60) : null;

        AssertRect(Layout("icon = { texture = \"gfx/skills.dds\" texture_density = 2 }", textures: Skills), 0, 0, 180, 30);
        AssertRect(Layout("icon = { texture = \"gfx/skills.dds\" }", textures: Skills), 0, 0, 360, 60);
    }

    [Fact]
    public void Frame_grid_divides_texture()
    {
        // EU V: frame_grid = { c r } teilt die Textur; CK III/Victoria 3: framesize gilt direkt. Beide durch texture_density.
        (int, int)? Frames(string path) => path == "gfx/frames.dds" ? (138, 46) : null;

        AssertRect(Layout("icon = { texture = \"gfx/frames.dds\" frame_grid = { 3 1 } }", textures: Frames), 0, 0, 46, 46);
        AssertRect(Layout("icon = { texture = \"gfx/frames.dds\" frame_grid = { 3 1 } texture_density = 2 }", textures: Frames), 0, 0, 23, 23);
        AssertRect(Layout("icon = { texture = \"gfx/frames.dds\" framesize = { 60 60 } texture_density = 2 }", textures: Frames), 0, 0, 30, 30);
    }

    [Fact]
    public void Policy_values_are_case_insensitive_with_fixed_default()
    {
        var (resolver, doc) = GuiResolverTests.Setup("hbox = { expand = { } icon = { layoutpolicy_horizontal = Growing layoutpolicy_vertical = sometimes } }", "");
        var hbox = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        var expand = hbox.Children.First(c => c.Key == "expand");
        var icon = hbox.Children.First(c => c.Key == "icon");

        Assert.Equal(LayoutPolicy.Growing, GuiLayoutEngine.PolicyOf(expand, horizontal: true));
        Assert.Equal(LayoutPolicy.Growing, GuiLayoutEngine.PolicyOf(icon, horizontal: true));
        Assert.Equal(LayoutPolicy.Fixed, GuiLayoutEngine.PolicyOf(icon, horizontal: false));
        Assert.Equal(LayoutPolicy.Fixed, GuiLayoutEngine.PolicyOf(hbox, horizontal: true));
    }

    private static string Icon(double width, double height, string extra = "") => $"icon = {{ size = {{ {width} {height} }} {extra} }}";

    [Fact]
    public void Set_parent_size_to_minimum_keeps_box_at_content_size()
    {
        // 10b: vbox mit set_parent_size_to_minimum in widget 200×80: Inhaltsgröße oben links, das widget bleibt 200×80.
        var widget = Layout($"widget = {{ size = {{ 200 80 }} position = {{ 10 20 }} vbox = {{ set_parent_size_to_minimum = yes {Icon(50, 20)} {Icon(50, 20)} }} }}");
        var vbox = widget.Children[0];

        AssertRect(widget, 10, 20, 200, 80);
        AssertRect(vbox, 10, 20, 50, 40);
        AssertRect(vbox.Children[0], 10, 20, 50, 20);
        AssertRect(vbox.Children[1], 10, 40, 50, 20);
    }

    [Fact]
    public void Cross_axis_centers_and_policy_stretches()
    {
        // 3a: feste Kinder quer mittig; 3k: expanding/growing quer strecken auf die volle Höhe.
        var centered = Hbox(Icon(40, 20) + Icon(40, 40) + Icon(40, 60), height: 80);
        var stretched = Hbox(Icon(40, 20, "layoutpolicy_vertical = expanding") + Icon(40, 20, "layoutpolicy_vertical = growing"), height: 80);
        // Engine-expand ohne Policy ist growing in beiden Achsen (wie der Vanilla-Typ expand), streckt also auch quer.
        var expand = Layout("widget = { size = { 60 200 } vbox = { expand = { } } }").Children[0];

        Assert.Equal(new double[] { 30, 20, 10 }, centered.Children.Select(c => Math.Round(c.Y, 3)));
        Assert.Equal(new double[] { 80, 80 }, stretched.Children.Select(c => Math.Round(c.Height, 3)));
        Assert.Equal(new double[] { 0, 0 }, stretched.Children.Select(c => Math.Round(c.Y, 3)));
        Assert.Equal(new double[] { 80, 280 }, Xs(stretched));
        AssertRect(expand.Children[0], 0, 0, 60, 200);
    }

    [Fact]
    public void Invisible_children_take_no_space_by_default()
    {
        // 4a/4b: in einer hbox nimmt ein unsichtbares Kind keinen Platz (Engine-Standard ignoreinvisible = yes).
        var box = Hbox(Item(50) + Item(50, "visible = no") + Item(50));
        var conditional = Layout($"widget = {{ size = {{ 400 30 }} hbox = {{ {Item(50)} {Item(50, "visible = \"[IsShown]\"")} {Item(50)} }} }}", new LayoutOptions { ShowConditional = false }).Children[0];

        Assert.Equal(new double[] { 75, 275 }, Xs(box));
        Assert.Equal(new double[] { 75, 275 }, Xs(conditional));
    }

    [Fact]
    public void Hidden_conditional_takes_no_space_in_hbox()
    {
        // Ein ausgeblendetes bedingtes Element verhält sich wie ein unsichtbares Kind (Regel 7): kein Fach in der hbox.
        var gui = $"widget = {{ size = {{ 400 30 }} hbox = {{ {Item(50)} {Item(50, "visible = \"[IsShown]\"")} {Item(50)} }} }}";
        var key = Layout(gui).Children[0].Children[1].ConditionalKey!;

        var hidden = Layout(gui, new LayoutOptions { HiddenConditionals = new HashSet<string> { key } }).Children[0];

        Assert.Equal(new double[] { 75, 275 }, Xs(hidden));
        Assert.All(hidden.Children, c => Assert.False(c.Conditional));
    }

    [Fact]
    public void Shown_conditional_is_marked_with_its_key()
    {
        var box = Layout("window = { size = { 10 10 } widget = { visible = \"[IsShown]\" size = { 1 1 } } widget = { size = { 1 1 } } widget = { visible = no size = { 1 1 } } }", new LayoutOptions { ShowHidden = true });

        Assert.Equal(new[] { true, false, false }, box.Children.Select(c => c.Conditional));
        // Ohne angesehene Datei und Vorschau-Wurzel bleiben Datei- und Wurzelteil leer.
        Assert.Equal(new[] { "||widget::0", null, null }, box.Children.Select(c => c.ConditionalKey));
        Assert.Equal("||widget::0", box.ConditionalKeys[box.Children[0].Element]);
    }

    [Fact]
    public void Ignoreinvisible_no_keeps_the_slot()
    {
        // 4c: der CK-III-Typ flowcontainer setzt ignoreinvisible = no, das unsichtbare Kind behält seinen Platz; 4d: yes überschreibt.
        var keeps = Layout($"widget = {{ size = {{ 400 30 }} flowcontainer = {{ {Item(50)} {Item(50, "visible = no")} {Item(50)} }} }}", library: JominiProbeLayoutTests.Ck3Types).Children[0];
        var ignores = Layout($"widget = {{ size = {{ 400 30 }} flowcontainer = {{ ignoreinvisible = yes {Item(50)} {Item(50, "visible = no")} {Item(50)} }} }}", library: JominiProbeLayoutTests.Ck3Types).Children[0];
        var hbox = Hbox("ignoreinvisible = no " + Item(50) + Item(50, "visible = no") + Item(50));

        Assert.Equal(new double[] { 0, 100 }, Xs(keeps));
        Assert.Equal(150, keeps.Width, 3);
        Assert.Equal(new double[] { 0, 50 }, Xs(ignores));
        // hbox mit drei Fächern (frei 250, Polster 83,3); das mittlere bleibt leer.
        Assert.Equal(new[] { 41.667, 308.333 }, Xs(hbox));
    }

    [Fact]
    public void Flowcontainer_packs_from_top_left()
    {
        // 3i: gepackt ab oben links, kein Verteilen, Querachse oben, Größe = Inhalt.
        var box = Layout($"widget = {{ size = {{ 400 80 }} flowcontainer = {{ {Icon(40, 20, "layoutpolicy_vertical = expanding")} {Icon(40, 40)} {Icon(40, 60)} }} }}").Children[0];
        var spaced = Layout($"widget = {{ size = {{ 400 80 }} flowcontainer = {{ spacing = 5 {Icon(40, 20, "layoutpolicy_horizontal = expanding")} {Icon(40, 40)} }} }}").Children[0];
        var vertical = Layout($"widget = {{ size = {{ 400 80 }} flowcontainer = {{ direction = vertical {Icon(20, 30)} {Icon(40, 30)} }} }}").Children[0];

        AssertRect(box, 0, 0, 120, 60);
        Assert.Equal(new double[] { 0, 40, 80 }, Xs(box));
        Assert.Equal(new double[] { 0, 0, 0 }, box.Children.Select(c => Math.Round(c.Y, 3)));
        // Entscheid: expanding/growing quer bleibt im flowcontainer ungedehnt, ohne Hinweis (Regel 8 gilt auch für die Querachse).
        Assert.Equal(20, box.Children[0].Height, 3);
        Assert.Empty(box.Children[0].Notes);
        Assert.Equal(new double[] { 0, 45 }, Xs(spaced));
        Assert.Equal(new double[] { 40, 40 }, Widths(spaced));
        Assert.Equal(new double[] { 0, 0 }, Xs(vertical));
        Assert.Equal(new double[] { 0, 30 }, vertical.Children.Select(c => Math.Round(c.Y, 3)));
    }

    [Fact]
    public void Flowcontainer_child_anchor_uses_flowcontainer_size()
    {
        // 3j: parentanchor = vcenter bezieht sich auf die Höhe des flowcontainers (höchstes Kind 60), nicht auf die Hülle 80.
        var box = Layout($"widget = {{ size = {{ 400 80 }} flowcontainer = {{ {Icon(40, 20)} {Icon(40, 40, "parentanchor = vcenter")} {Icon(40, 60)} {Icon(40, 20, "parentanchor = bottom")} }} }}").Children[0];

        Assert.Equal(new double[] { 0, 10, 0, 40 }, box.Children.Select(c => Math.Round(c.Y, 3)));
        Assert.Empty(box.Children[1].Notes);
        Assert.Empty(box.Children[3].Notes);
    }

    [Fact]
    public void Flowcontainer_child_position_and_main_axis_anchor_get_a_note()
    {
        // Nicht gemessen: position und der Hauptachsen-Anteil der Anker an flowcontainer-Kindern; ignoriert, mit Hinweis.
        var box = Layout($"widget = {{ size = {{ 400 80 }} flowcontainer = {{ {Icon(40, 20, "position = { 5 5 }")} {Icon(40, 20, "parentanchor = right|vcenter")} {Icon(40, 20, "widgetanchor = hcenter")} {Icon(40, 20, "parentanchor = left|bottom")} }} }}").Children[0];
        var vertical = Layout($"widget = {{ size = {{ 400 80 }} flowcontainer = {{ direction = vertical {Icon(40, 20, "parentanchor = bottom")} {Icon(20, 20, "parentanchor = right")} }} }}").Children[0];

        Assert.Equal(new double[] { 0, 40, 80, 120 }, Xs(box));
        Assert.Equal(new double[] { 0, 0, 0, 0 }, box.Children.Select(c => Math.Round(c.Y, 3)));
        Assert.Equal(new[] { GuiTexts.LayoutFlowChildUnmeasured("position") }, box.Children[0].Notes);
        Assert.Equal(new[] { GuiTexts.LayoutFlowChildUnmeasured("parentanchor") }, box.Children[1].Notes);
        Assert.Equal(new[] { GuiTexts.LayoutFlowChildUnmeasured("widgetanchor") }, box.Children[2].Notes);
        Assert.Empty(box.Children[3].Notes);
        Assert.Equal(new[] { GuiTexts.LayoutFlowChildUnmeasured("parentanchor") }, vertical.Children[0].Notes);
        Assert.Equal(20, vertical.Children[1].X, 3);
        Assert.Empty(vertical.Children[1].Notes);
    }

    [Fact]
    public void Anchor_on_box_child_is_ignored_with_game_error_note()
    {
        // 3o/3f: parentanchor an einem hbox-Kind wirkt nicht (Kind mittig), das Spiel meldet einen Fehler.
        var box = Hbox(Icon(40, 20) + Icon(40, 40, "parentanchor = bottom") + Icon(40, 60, "position = { 5 5 }") + Icon(40, 20, "widgetanchor = right"), height: 80);

        Assert.Equal(new double[] { 30, 20, 10, 30 }, box.Children.Select(c => Math.Round(c.Y, 3)));
        Assert.Equal(new double[] { 30, 130, 230, 330 }, Xs(box));
        Assert.Empty(box.Children[0].Notes);
        Assert.Equal(new[] { GuiTexts.LayoutGameError("parentanchor in hbox/vbox") }, box.Children[1].Notes);
        Assert.Equal(new[] { GuiTexts.LayoutGameError("position in hbox/vbox") }, box.Children[2].Notes);
        Assert.Equal(new[] { GuiTexts.LayoutGameError("widgetanchor in hbox/vbox") }, box.Children[3].Notes);
        // parentanchor und widgetanchor zusammen: ein Hinweis zum parentanchor.
        Assert.Equal(new[] { GuiTexts.LayoutGameError("parentanchor in hbox/vbox") }, Hbox(Icon(40, 20, "parentanchor = top widgetanchor = top")).Children[0].Notes);
        // ignore_layout = yes: Anker sind dort gültig (Vanilla EU V), also kein Fehlerhinweis, nur der Hinweis auf die angenäherte Lage.
        Assert.Equal(
            new[] { GuiTexts.LayoutIgnoreLayoutApproximated },
            Hbox(Icon(40, 20, "ignore_layout = yes parentanchor = left|bottom position = { 17 1 }")).Children[0].Notes);
    }

    [Fact]
    public void Align_on_hbox_adds_game_error_note()
    {
        // 3b: align an hbox ist wirkungslos (Lage wie 3a), das Spiel meldet einen Fehler.
        var box = Hbox("align = left " + Icon(40, 20) + Icon(40, 40) + Icon(40, 60), height: 80);
        var vbox = Layout($"widget = {{ size = {{ 100 100 }} vbox = {{ align = top {Icon(40, 20)} }} }}").Children[0];

        Assert.Equal(new[] { 46.667, 180, 313.333 }, Xs(box));
        Assert.Contains(GuiTexts.LayoutGameError("align an hbox/vbox"), box.Notes);
        Assert.Contains(GuiTexts.LayoutGameError("align an hbox/vbox"), vbox.Notes);
        Assert.Empty(Hbox(Icon(40, 20)).Notes);
    }

    [Fact]
    public void Margin_on_plain_widget_is_ignored_with_note()
    {
        // 6a: margin an einem widget ist ungültig; das Icon 100% füllt die ganze Hülle 300×100.
        var widget = Layout("widget = { size = { 300 100 } position = { 10 20 } margin = { 20 10 } icon = { size = { 100% 100% } } }");
        // margin an einer Box bleibt gültig: Inhalt 260×80 ab (20, 10), das Icon steht darin mittig.
        var hbox = Layout("widget = { size = { 300 100 } hbox = { margin = { 20 10 } icon = { size = { 40 20 } } } }").Children[0];

        AssertRect(widget.Children[0], 10, 20, 300, 100);
        Assert.Contains(GuiTexts.LayoutGameError("margin an widget"), widget.Notes);
        Assert.Empty(hbox.Notes);
        AssertRect(hbox.Children[0], 130, 40, 40, 20);
    }

    [Fact]
    public void Box_directly_in_flowcontainer_gets_crash_note()
    {
        // Regel 15: hbox/vbox direkt in flowcontainer oder container stürzt das Spiel ab; gelegt wie in einem widget (füllt).
        var flow = Layout($"widget = {{ size = {{ 400 80 }} flowcontainer = {{ hbox = {{ {Icon(40, 20)} }} {Icon(40, 60)} }} }}").Children[0];
        var container = Layout($"widget = {{ size = {{ 400 80 }} container = {{ size = {{ 200 50 }} vbox = {{ {Icon(40, 20)} }} }} }}").Children[0];
        var widget = Layout($"widget = {{ size = {{ 400 80 }} hbox = {{ {Icon(40, 20)} }} }}");

        Assert.Equal(new[] { GuiTexts.LayoutBoxInContainer("flowcontainer") }, flow.Children[0].Notes);
        AssertRect(flow.Children[0], 0, 0, 80, 60);
        Assert.Equal(40, flow.Children[1].X, 3);
        Assert.Equal(new[] { GuiTexts.LayoutBoxInContainer("container") }, container.Children[0].Notes);
        AssertRect(container.Children[0], 0, 0, 200, 50);
        Assert.Empty(widget.Children[0].Notes);
    }

    [Fact]
    public void VboxStretchesHorizontallyExpandingChild()
    {
        var box = Layout("vbox = { size = { 100 200 } widget = { size = { 10 10 } layoutpolicy_horizontal = expanding } }");

        // Senkrecht fest (keine layoutpolicy_vertical), daher space-around: (200 - 10) / 2 = 95.
        AssertRect(box.Children[0], 0, 95, 100, 10);
    }

    [Fact]
    public void IconWithoutSizeUsesTextureSize()
    {
        var box = Layout("icon = { texture = \"gfx/a.dds\" }", textures: path => path == "gfx/a.dds" ? (64, 32) : null);

        AssertRect(box, 0, 0, 64, 32);
        Assert.Empty(box.Notes);
    }

    [Fact]
    public void MissingTextureAddsANote()
    {
        var box = Layout("icon = { texture = \"gfx/missing.dds\" }");

        Assert.Contains(box.Notes, n => n.Contains("gfx/missing.dds"));
        Assert.True(box.Uncertain);
    }

    [Fact]
    public void Without_text_service_the_estimate_stays()
    {
        // Ohne Textdienst (klassische Aufrufer, Tests): 7 px je Zeichen, 20 hoch, mit Hinweis; kein Textbild.
        var box = Layout("text_single = { text = \"Hallo\" }");

        Assert.Equal(35, box.Width, 3);
        Assert.Equal(20, box.Height, 3);
        Assert.Contains("Textgröße geschätzt", box.Notes);
        Assert.Null(box.Visual);
    }

    // ---- Text mit Textdienst (synthetische Arial-Bibliothek und Lokalisierung, siehe JominiEngineTextFixture) ----

    private LayoutBox LayoutText(string gui, string language = "l_english", string library = "")
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui, library);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(GuiResolverTests.Rules, _ => null, _text.Service).Layout(element, new LayoutOptions { Language = language });
    }

    private double ArialWidth(string text, double size = 15) => text.EnumerateRunes().Sum(rune => _text.Arial.Advance(rune.Value, size, out _));

    private double ArialHeight(double size = 15) => JominiTextMetrics.LineHeight(_text.Arial, size);

    private static string VisualText(LayoutBox box) =>
        string.Join("\n", Assert.IsType<JominiTextVisual>(box.Visual).Lines.Select(line => string.Concat(line.Runs.Select(run => run.Text))));

    [Theory]
    [InlineData("texticon = { icon = gold iconsize = { texture = \"gfx/gold.dds\" size = { 20 20 } } }")]
    [InlineData("textformatting = { format = { name = red format = \"color:{1,0,0}\" } }")]
    public void Texticon_and_textformatting_definitions_are_no_text_fields(string gui)
    {
        // Vanilla-Definitionen als Wurzel (Victoria 3: 440 Schrift-Notizen) beginnen mit "text", sind aber keine Textfelder.
        var box = LayoutText(gui);

        Assert.Null(box.Visual);
        Assert.DoesNotContain(GuiTexts.LayoutTextApproximated("fontsize"), box.Notes);
        Assert.DoesNotContain(GuiTexts.LayoutTextEstimated, box.Notes);
        Assert.IsType<JominiTextVisual>(LayoutText("textbox = { raw_text = \"A\" fontsize = 15 autoresize = yes }").Visual);
    }

    [Fact]
    public void Autoresize_text_takes_measured_size_and_beats_size()
    {
        // 7e: autoresize schlägt size = { 100 20 }; in der hbox nimmt das Textfeld die Textbreite.
        var hbox = LayoutText("widget = { size = { 600 40 } hbox = { textbox = { raw_text = \"Probe text 123 with more words\" font = \"Arial\" fontsize = 15 size = { 100 20 } autoresize = yes } expand = {} } }").Children[0];
        var text = hbox.Children[0];

        var width = ArialWidth("Probe text 123 with more words");
        Assert.True(width > 100);
        Assert.Equal(width, text.Width, 3);
        Assert.Equal(ArialHeight(), text.Height, 3);
        Assert.Equal(width, Assert.IsType<JominiTextVisual>(text.Visual).Width, 3);
        Assert.Equal("Probe text 123 with more words", VisualText(text));
        Assert.DoesNotContain(GuiTexts.LayoutTextEstimated, text.Notes);
    }

    [Fact]
    public void Text_without_autoresize_keeps_size_and_may_overflow()
    {
        // 7f: size bleibt, der Text läuft über den Kasten hinaus und wird nicht gekürzt.
        var hbox = LayoutText("widget = { size = { 600 40 } hbox = { textbox = { raw_text = \"Probe text 123 with more words\" font = \"Arial\" fontsize = 15 size = { 100 20 } } expand = {} } }").Children[0];
        var text = hbox.Children[0];

        AssertRect(text, 0, 10, 100, 20);
        var visual = Assert.IsType<JominiTextVisual>(text.Visual);
        Assert.Equal(ArialWidth("Probe text 123 with more words"), visual.Width, 3);
        Assert.False(visual.Elided);
        Assert.Single(visual.Lines);
    }

    [Fact]
    public void Text_without_size_and_autoresize_is_an_empty_box_and_overflows()
    {
        // Probe 11g (alle drei Spiele): 0×0, in einer hbox ohne Breite; der Text wird trotzdem ab dem Ursprung gezeichnet. Ohne Hinweis (gemessen).
        var hbox = LayoutText("widget = { size = { 600 40 } hbox = { icon = { size = { 4 30 } } textbox = { raw_text = \"Probe text\" font = \"Arial\" fontsize = 15 multiline = yes elide = right } icon = { size = { 4 30 } } expand = {} } }").Children[0];
        var text = hbox.Children[1];

        Assert.Equal(0, text.Width, 3);
        Assert.Equal(0, text.Height, 3);
        Assert.Equal(4, hbox.Children[2].X, 3);
        var visual = Assert.IsType<JominiTextVisual>(text.Visual);
        Assert.Equal(ArialWidth("Probe text"), visual.Width, 3);
        Assert.False(visual.Elided);
        Assert.Equal("Probe text", VisualText(text));
        Assert.Empty(text.Notes);
    }

    [Fact]
    public void Resizeparent_sizes_a_sizeless_widget()
    {
        // 10a: das widget ohne size wird so groß wie das Textfeld; der Marker rechts unten liegt an dessen Ecke.
        var outer = LayoutText("widget = { size = { 300 100 } widget = { textbox = { raw_text = \"Probe text 123\" font = \"Arial\" fontsize = 15 autoresize = yes resizeparent = yes } icon = { size = { 6 6 } parentanchor = right|bottom } } }");
        var widget = outer.Children[0];

        var width = ArialWidth("Probe text 123");
        AssertRect(widget, 0, 0, width, ArialHeight());
        AssertRect(widget.Children[1], width - 6, ArialHeight() - 6, 6, 6);
        Assert.DoesNotContain(GuiTexts.LayoutTextApproximated("resizeparent"), widget.Children[0].Notes);

        // Ein Elternelement mit size behält sie; das Textfeld trägt dann einen Hinweis.
        var sized = LayoutText("widget = { size = { 300 100 } textbox = { raw_text = \"Probe\" font = \"Arial\" autoresize = yes resizeparent = yes } }");
        AssertRect(sized, 0, 0, 300, 100);
        Assert.Contains(GuiTexts.LayoutTextApproximated("resizeparent"), sized.Children[0].Notes);
    }

    [Fact]
    public void Text_key_uses_language_then_english_then_key_with_note()
    {
        const string Box = "textbox = {{ text = \"{0}\" font = \"Arial\" fontsize = 15 autoresize = yes }}";

        var german = LayoutText(string.Format(Box, "HELLO"), "l_german");
        Assert.Equal("Hallo", VisualText(german));
        Assert.Equal(ArialWidth("Hallo"), german.Width, 3);

        Assert.Equal("Only English", VisualText(LayoutText(string.Format(Box, "ONLY_EN"), "l_german")));
        Assert.Equal("Hello", VisualText(LayoutText(string.Format(Box, "HELLO"))));

        var missing = LayoutText(string.Format(Box, "NO_SUCH_KEY"), "l_german");
        Assert.Equal("NO_SUCH_KEY", VisualText(missing));
        Assert.Single(missing.Notes, GuiTexts.LayoutTextKeyMissing("NO_SUCH_KEY"));

        // Ein Satz ist kein Schlüssel: gezeigt wie er ist, ohne Hinweis. raw_text wird nie nachgeschlagen.
        var sentence = LayoutText(string.Format(Box, "Just some words"));
        Assert.Equal("Just some words", VisualText(sentence));
        Assert.Empty(sentence.Notes);
        Assert.Equal("HELLO", VisualText(LayoutText("textbox = { raw_text = \"HELLO\" font = \"Arial\" autoresize = yes }", "l_german")));

        // $key$ im Text wird in der Sprache aufgelöst.
        Assert.Equal("Say Hallo", VisualText(LayoutText("textbox = { raw_text = \"Say $HELLO$\" font = \"Arial\" autoresize = yes }", "l_german")));
    }

    [Fact]
    public void Unknown_font_draws_nothing_with_note()
    {
        // Victoria 3, Lauf 4: eine Schrift ohne .font-Definition zeichnet nichts, das autoresize-Feld ist 0×0.
        var unknown = LayoutText("textbox = { raw_text = \"Probe text\" font = \"NoSuchFont\" autoresize = yes }");
        AssertRect(unknown, 0, 0, 0, 0);
        Assert.Empty(Assert.IsType<JominiTextVisual>(unknown.Visual).Lines);
        Assert.Single(unknown.Notes, GuiTexts.LayoutTextFontUnknown("NoSuchFont"));

        // Der Typ setzt Arial, das Element eine unbekannte Schrift: der letzte Wert gilt, es gibt keinen Rückgriff auf den Typ.
        var overridden = LayoutText("my_text = { raw_text = \"Probe\" font = \"NoSuchFont\" autoresize = yes }", library: "types T { type my_text = textbox { font = \"Arial\" } }");
        AssertRect(overridden, 0, 0, 0, 0);
        Assert.Contains(GuiTexts.LayoutTextFontUnknown("NoSuchFont"), overridden.Notes);

        // Ohne font: StandardGameFont der Bibliothek (hier ebenfalls Arial), ohne Hinweis.
        var standard = LayoutText("textbox = { raw_text = \"Probe\" fontsize = 15 autoresize = yes }");
        Assert.Equal(ArialWidth("Probe"), standard.Width, 3);
        Assert.Empty(standard.Notes);
    }

    [Fact]
    public void Binding_text_gets_placeholder_note()
    {
        var box = LayoutText("textbox = { raw_text = \"Name: [GetPlayer.GetName]\" font = \"Arial\" fontsize = 15 autoresize = yes }");

        Assert.True(Assert.IsType<JominiTextVisual>(box.Visual).HasPlaceholder);
        Assert.Single(box.Notes, GuiTexts.LayoutTextPlaceholder);
        Assert.Equal(ArialWidth("Name: ‹GetName›"), box.Width, 3);
    }

    [Fact]
    public void Text_style_comes_from_fontsize_fontcolor_and_align()
    {
        var box = LayoutText("textbox = { raw_text = \"Probe\" font = \"Arial\" fontsize = 18 fontcolor = { 1 0 0 1 } align = right|bottom size = { 200 50 } }");
        var visual = Assert.IsType<JominiTextVisual>(box.Visual);
        var run = Assert.Single(Assert.Single(visual.Lines).Runs);

        Assert.Equal(18, run.FontSize);
        Assert.Equal(new JominiColor(1, 0, 0, 1), run.Color);
        Assert.Equal((JominiTextAlignH.Right, JominiTextAlignV.Bottom), (visual.AlignH, visual.AlignV));
        Assert.Equal((JominiTextAlignH.Center, JominiTextAlignV.Center), Align("center"));
        Assert.Equal((JominiTextAlignH.Center, JominiTextAlignV.Top), Align("hcenter|top|nobaseline"));
        Assert.Equal((JominiTextAlignH.Left, JominiTextAlignV.Center), Align("left"));

        // Ohne align: links und senkrecht mittig (Lauf 4, alle drei Spiele); ohne fontcolor weiß; ohne fontsize 15 mit Hinweis.
        var plain = LayoutText("textbox = { raw_text = \"Probe\" font = \"Arial\" autoresize = yes }");
        var plainVisual = Assert.IsType<JominiTextVisual>(plain.Visual);
        Assert.Equal((JominiTextAlignH.Left, JominiTextAlignV.Center), (plainVisual.AlignH, plainVisual.AlignV));
        Assert.Equal(JominiColor.White, plainVisual.Lines[0].Runs[0].Color);
        Assert.Equal(15, plainVisual.Lines[0].Runs[0].FontSize);
        Assert.Contains(GuiTexts.LayoutTextApproximated("fontsize"), plain.Notes);

        (JominiTextAlignH, JominiTextAlignV) Align(string align)
        {
            var v = Assert.IsType<JominiTextVisual>(LayoutText($"textbox = {{ raw_text = \"P\" font = \"Arial\" fontsize = 15 align = {align} size = {{ 50 20 }} }}").Visual);
            return (v.AlignH, v.AlignV);
        }
    }

    [Fact]
    public void Format_markup_inside_quoted_raw_text_survives_the_parser()
    {
        // Ruling 8: "#" in einem Text in Anführungszeichen ist kein Kommentar.
        var box = LayoutText("textbox = { raw_text = \"#bold Probe#! text\" font = \"Arial\" fontsize = 15 autoresize = yes }");

        Assert.Equal("#bold Probe#! text", box.Element.Property("raw_text")!.Value);
        Assert.Equal("Probe text", VisualText(box));
        Assert.True(Assert.IsType<JominiTextVisual>(box.Visual).Lines[0].Runs[0].SimulateBold);
    }

    [Fact]
    public void Multiline_wraps_at_max_width_and_elide_uses_the_box()
    {
        var wrapped = LayoutText("textbox = { raw_text = \"aaa bbb ccc\" font = \"Arial\" fontsize = 15 multiline = yes max_width = 40 autoresize = yes }");
        var lines = Assert.IsType<JominiTextVisual>(wrapped.Visual).Lines;
        Assert.Equal(3, lines.Count);
        Assert.Equal(lines.Max(l => l.Width), wrapped.Width, 3);
        Assert.Equal(Assert.IsType<JominiTextVisual>(wrapped.Visual).Height, wrapped.Height, 3);

        var elided = LayoutText("textbox = { raw_text = \"Probe text that is too long\" font = \"Arial\" fontsize = 15 size = { 80 20 } elide = right }");
        var visual = Assert.IsType<JominiTextVisual>(elided.Visual);
        Assert.True(visual.Elided);
        Assert.True(visual.Width <= 80);
        Assert.EndsWith(" ...", VisualText(elided));
    }

    [Fact]
    public void Button_text_gets_a_visual_but_keeps_its_size()
    {
        var box = LayoutText("button = { size = { 120 30 } text = \"HELLO\" font = \"Arial\" fontsize = 15 }");

        AssertRect(box, 0, 0, 120, 30);
        Assert.Equal("Hello", VisualText(box));
    }

    [Fact]
    public void Button_label_without_font_or_fontsize_gets_no_approximation_notes()
    {
        // Ruling (Fix-Runde 1): Beschriftungen ändern die Lage nicht; Schriftgröße, fehlende Schrift und fontcolor bleiben ohne Hinweis.
        // Die Bibliothek ohne StandardGameFont zwingt die Ersatzschrift ("–").
        var service = _text.ServiceWithoutStandardFont;
        var box = LayoutWith(service, "button = { size = { 120 30 } text = \"NO_SUCH_KEY\" fontcolor = \"[GetColor]\" }");

        AssertRect(box, 0, 0, 120, 30);
        Assert.Equal("NO_SUCH_KEY", VisualText(box));
        Assert.Equal(new[] { GuiTexts.LayoutTextKeyMissing("NO_SUCH_KEY") }, box.Notes);

        var binding = LayoutWith(service, "button = { size = { 120 30 } raw_text = \"[GetPlayer.GetName]\" }");
        Assert.Equal(new[] { GuiTexts.LayoutTextPlaceholder }, binding.Notes);

        // Ein Textfeld derselben Art behält die Hinweise (seine Größe hängt am Text).
        var textbox = LayoutWith(service, "textbox = { raw_text = \"Probe\" autoresize = yes }");
        Assert.Contains(GuiTexts.LayoutTextApproximated("fontsize"), textbox.Notes);
        Assert.Contains(GuiTexts.LayoutTextFontMissing("–"), textbox.Notes);
    }

    [Fact]
    public void Dollar_text_is_no_missing_key()
    {
        var box = LayoutText("textbox = { text = \"$NO_SUCH_KEY$\" font = \"Arial\" fontsize = 15 autoresize = yes }");

        Assert.DoesNotContain(GuiTexts.LayoutTextKeyMissing("$NO_SUCH_KEY$"), box.Notes);
    }

    [Fact]
    public void Fontcolor_scale_is_decided_per_tuple_and_bindings_get_a_note()
    {
        JominiColor ColorOf(string fontcolor) =>
            Assert.IsType<JominiTextVisual>(LayoutText($"textbox = {{ raw_text = \"P\" font = \"Arial\" fontsize = 15 fontcolor = {fontcolor} autoresize = yes }}").Visual)
                .Lines[0].Runs[0].Color;

        // Eine Komponente über 1: das ganze Tupel ist 0–255.
        Assert.Equal(new JominiColor(1, 1 / 255.0, 0, 1), ColorOf("{ 255 1 0 255 }"));
        Assert.Equal(new JominiColor(0.5, 1, 0, 1), ColorOf("{ 0.5 1 0 1 }"));

        // Datenbindung: weiß mit Hinweis.
        var bound = LayoutText("textbox = { raw_text = \"P\" font = \"Arial\" fontsize = 15 fontcolor = \"[GetColor]\" autoresize = yes }");
        Assert.Equal(JominiColor.White, Assert.IsType<JominiTextVisual>(bound.Visual).Lines[0].Runs[0].Color);
        Assert.Contains(GuiTexts.LayoutTextApproximated("fontcolor"), bound.Notes);
    }

    [Fact]
    public void Warm_fonts_come_from_the_textbox_types()
    {
        // Victoria 3 kennt StandardGameFont nicht: vorgeladen wird die Schrift, die die Typen textbox/text_single setzen.
        var service = _text.ServiceWithTypes("types T { type textbox = textbox { font = \"Arial\" } type text_single = textbox { } }");

        Assert.Contains("Arial", service.WarmFonts);
        Assert.Contains("StandardGameFont", _text.Service.WarmFonts);
        _text.Service.Warm("l_german");
    }

    private LayoutBox LayoutWith(JominiTextService service, string gui)
    {
        var (resolver, doc) = GuiResolverTests.Setup(gui);
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(GuiResolverTests.Rules, _ => null, service).Layout(element);
    }

    [Fact]
    public void GridRepeatsTheItemTemplate()
    {
        var box = Layout("fixedgridbox = { addcolumn = 100 addrow = 40 datamodel = \"[List]\" item = { widget = { size = { 100 40 } } } }");

        Assert.Equal(new[] { 0, 1, 2 }, box.Children.Select(c => c.RepeatIndex));
        Assert.Equal(new[] { 0d, 40d, 80d }, box.Children.Select(c => c.Y));
        AssertRect(box, 0, 0, 100, 120);
    }

    [Fact]
    public void Background_fills_parent_and_takes_no_slot()
    {
        // 8a: background füllt das widget; zuletzt geschrieben und trotzdem zuerst gezeichnet. Seine size zählt nicht.
        var widget = Layout("widget = { size = { 200 60 } position = { 10 20 } icon = { size = { 20 20 } } background = { size = { 5 5 } } }");
        // 8d: background in hbox belegt kein Fach; zwei feste Fächer per space-around (x 75 und 275).
        var hbox = Hbox(Icon(50, 20) + Icon(50, 20) + "background = { size = { 30 30 } }", height: 60);
        // Kein Beitrag zur Inhaltsgröße: innere hbox ohne size = nur ihr Icon (40×20), mittig in 400×60.
        var nested = Hbox($"hbox = {{ background = {{ size = {{ 100 100 }} }} {Icon(40, 20)} }}", height: 60).Children[0];
        // 9j: Hülle eines container ohne size ohne den background.
        var container = Layout($"widget = {{ size = {{ 400 60 }} container = {{ background = {{ size = {{ 100 100 }} }} {Icon(40, 20, "position = { 10 10 }")} }} }}").Children[0];

        Assert.Equal("background", widget.Children[0].Element.BaseWidget);
        Assert.True(widget.Children[0].IsBackground);
        AssertRect(widget.Children[0], 10, 20, 200, 60);
        Assert.False(widget.Children[1].IsBackground);
        AssertRect(widget.Children[1], 10, 20, 20, 20);
        Assert.Empty(widget.Children[0].Notes);
        Assert.Empty(widget.Notes);

        Assert.True(hbox.Children[0].IsBackground);
        AssertRect(hbox.Children[0], 0, 0, 400, 60);
        Assert.Equal(new double[] { 0, 75, 275 }, Xs(hbox));

        AssertRect(nested, 180, 20, 40, 20);
        AssertRect(nested.Children[0], 180, 20, 40, 20);
        AssertRect(nested.Children[1], 180, 20, 40, 20);

        AssertRect(container, 0, 0, 50, 30);
        AssertRect(container.Children[0], 0, 0, 50, 30);
    }

    [Fact]
    public void Background_works_on_any_element_kind()
    {
        // 9h: background an einer textbox mit size in einer hbox; ebenso an button und icon.
        var hbox = Hbox("textbox = { size = { 80 20 } text = \"Probe\" background = { alpha = 1 } } " + Icon(50, 20), height: 30);
        var button = Layout("button = { size = { 60 30 } position = { 5 5 } background = { margin = { 2 2 } } }");
        var icon = Layout("icon = { size = { 40 10 } background = { alpha = 1 } }");

        Assert.Equal(new double[] { 67.5, 282.5 }, Xs(hbox));
        Assert.True(hbox.Children[0].Children[0].IsBackground);
        AssertRect(hbox.Children[0].Children[0], 67.5, 5, 80, 20);
        AssertRect(button.Children[0], 3, 3, 64, 34);
        AssertRect(icon.Children[0], 0, 0, 40, 10);
    }

    [Fact]
    public void Background_margin_grows_outward_and_inward()
    {
        // 8b: negativer margin zieht nach innen (10..190 × 5..55); 8c: positiver vergrößert nach außen.
        var inward = Layout("widget = { size = { 200 60 } background = { margin = { -10 -5 } icon = { size = { 10 10 } } } }");
        var outward = Layout("widget = { size = { 200 60 } background = { margin = { 10 5 } } }");
        var sides = Layout("widget = { size = { 200 60 } background = { margin_left = 4 margin_top = -2 } }");

        AssertRect(inward.Children[0], 10, 5, 180, 50);
        // Der margin verschiebt nur den background selbst, nicht dessen Inhalt.
        AssertRect(inward.Children[0].Children[0], 10, 5, 10, 10);
        Assert.Empty(inward.Notes);
        Assert.Empty(inward.Children[0].Notes);
        AssertRect(outward.Children[0], -10, -5, 220, 70);
        AssertRect(sides.Children[0], -4, 2, 204, 58);
    }

    [Fact]
    public void Hbox_with_datamodel_repeats_the_item()
    {
        // Statisches Kind zuerst, dann ListSampleCount Einträge aus der item-Vorlage; alle feste Fächer per space-around.
        var hbox = Hbox("datamodel = \"[List]\" " + Icon(40, 20) + $" item = {{ {Icon(50, 20)} }}", width: 400, height: 20);
        // vbox ohne size als Wurzel: Inhaltsgröße aus den Einträgen (3 × 10 + 2 × 5).
        var vbox = Layout("vbox = { spacing = 5 datamodel = \"[List]\" item = { widget = { size = { 30 10 } } } }");
        var flow = Layout("flowcontainer = { datamodel = \"[List]\" item = { widget = { size = { 30 10 } } } }", new LayoutOptions { ListSampleCount = 2 });

        Assert.Equal(new[] { -1, 0, 1, 2 }, hbox.Children.Select(c => c.RepeatIndex));
        Assert.Equal(new double[] { 40, 50, 50, 50 }, Widths(hbox));
        Assert.Equal(new double[] { 26.25, 118.75, 221.25, 323.75 }, Xs(hbox));
        Assert.Equal(new[] { 0, 1, 2 }, vbox.Children.Select(c => c.RepeatIndex));
        Assert.Equal(new double[] { 0, 15, 30 }, vbox.Children.Select(c => c.Y));
        AssertRect(vbox, 0, 0, 30, 40);
        Assert.Equal(new double[] { 0, 30 }, Xs(flow));
        AssertRect(flow, 0, 0, 60, 10);
    }

    [Fact]
    public void Percent_addcolumn_uses_grid_width()
    {
        const string item = "datamodel = \"[List]\" maxhorizontalslots = 2 item = { widget = { size = { 10 10 } } }";
        // Raster mit size: 50% von 400 = 200, 25% von 100 = 25.
        var sized = Layout($"widget = {{ size = {{ 400 100 }} fixedgridbox = {{ size = {{ 400 100 }} addcolumn = 50% addrow = 25% {item} }} }}").Children[0];
        // Raster ohne size: Bezug ist der Inhalt des Elternelements; Größe = 2 Spalten × 200, 2 Zeilen × 20.
        var sizeless = Layout($"widget = {{ size = {{ 400 100 }} fixedgridbox = {{ addcolumn = 50% addrow = 20 {item} }} }}").Children[0];

        Assert.Equal(new double[] { 0, 200, 0 }, Xs(sized));
        Assert.Equal(new double[] { 0, 0, 25 }, sized.Children.Select(c => c.Y));
        Assert.Contains(GuiTexts.LayoutGridApproximated, sized.Notes);
        AssertRect(sizeless, 0, 0, 400, 40);
        Assert.Equal(new double[] { 0, 200, 0 }, Xs(sizeless));
    }

    [Fact]
    public void Percent_cells_of_sizeless_grid_use_parent_content_in_measure_and_arrange()
    {
        // Raster ohne size in 400×100: 25% bezieht sich beim Messen und beim Legen auf die Elternhöhe 100 (nicht auf die eigene
        // gemessene Höhe 50); 3 Einträge in 2 Spalten, Zeile 2 also bei y 25.
        var grid = Layout("widget = { size = { 400 100 } fixedgridbox = { addcolumn = 100 addrow = 25% maxhorizontalslots = 2 datamodel = \"[List]\" item = { widget = { size = { 10 10 } } } } }").Children[0];
        // Weniger Einträge als Spalten: 25% von 400 = 100, nicht 25% der eigenen Breite.
        var columns = Layout("widget = { size = { 400 100 } fixedgridbox = { addcolumn = 25% addrow = 20 maxhorizontalslots = 4 datamodel = \"[List]\" item = { widget = { size = { 10 10 } } } } }", new LayoutOptions { ListSampleCount = 2 }).Children[0];
        // -1 in einer Achse von size: dort ebenfalls die Elternlänge.
        var unset = Layout("widget = { size = { 400 100 } fixedgridbox = { size = { 200 -1 } addcolumn = 50% addrow = 25% maxhorizontalslots = 2 datamodel = \"[List]\" item = { widget = { size = { 10 10 } } } } }").Children[0];

        AssertRect(grid, 0, 0, 200, 50);
        Assert.Equal(new double[] { 0, 0, 25 }, grid.Children.Select(c => c.Y));
        AssertRect(columns, 0, 0, 200, 20);
        Assert.Equal(new double[] { 0, 100 }, Xs(columns));
        AssertRect(unset, 0, 0, 200, 50);
        Assert.Equal(new double[] { 0, 100, 0 }, Xs(unset));
        Assert.Equal(new double[] { 0, 0, 25 }, unset.Children.Select(c => c.Y));
    }

    [Fact]
    public void Background_is_not_clipped_by_its_own_parent()
    {
        // Ein background mit margin nach außen (8c) bleibt sichtbar: nur äußere Beschnitte gelten für ihn, nicht der seines Elternelements.
        var widget = Layout("widget = { size = { 200 60 } scissor = yes icon = { size = { 20 20 } } background = { margin = { 10 5 } icon = { size = { 5 5 } } } }");
        var nested = Layout("widget = { size = { 100 40 } scissor = yes scrollarea = { position = { 10 10 } size = { 200 60 } background = { margin = { 10 5 } } } }");

        Assert.True(widget.Children[0].IsBackground);
        Assert.Null(widget.Children[0].Clip);
        Assert.Null(widget.Children[0].Children[0].Clip);
        Assert.Equal(new LayoutRect(0, 0, 200, 60), widget.Children[1].Clip);
        Assert.Equal(new LayoutRect(0, 0, 100, 40), nested.Children[0].Children[0].Clip);
    }

    [Fact]
    public void Scrollarea_clips_descendants()
    {
        var root = Layout("widget = { size = { 400 300 } scrollarea = { position = { 10 20 } size = { 100 50 } widget = { size = { 300 300 } icon = { size = { 20 20 } } } } }");
        var scroll = root.Children[0];
        // scissor = yes beschneidet ebenso; innere Beschnitte werden mit äußeren geschnitten.
        var nested = Layout("widget = { size = { 60 40 } scissor = yes scrollarea = { position = { 10 20 } size = { 100 50 } icon = { size = { 20 20 } } } }");

        Assert.Null(root.Clip);
        Assert.Null(scroll.Clip);
        Assert.Equal(2, scroll.Descendants().Count());
        Assert.All(scroll.Descendants(), d => Assert.Equal(new LayoutRect(10, 20, 100, 50), d.Clip));
        Assert.Null(nested.Clip);
        Assert.Equal(new LayoutRect(0, 0, 60, 40), nested.Children[0].Clip);
        Assert.Equal(new LayoutRect(10, 20, 50, 20), nested.Children[0].Children[0].Clip);
    }

    [Fact]
    public void Hit_test_skips_clipped_parts()
    {
        // Kind 200×200 in einem Scrollbereich 100×100: außerhalb des Ausschnitts trifft der Klick das äußere widget.
        var root = Layout("widget = { size = { 300 300 } scrollarea = { size = { 100 100 } widget = { size = { 200 200 } } } }");
        var scene = new Designer.GuiSchematicScene(root, _ => true);

        Assert.Same(root, scene.HitTest(150, 150));
        Assert.Same(root.Children[0].Children[0], scene.HitTest(50, 50));
    }

    [Fact]
    public void Ignore_layout_child_takes_no_slot_and_uses_its_anchors()
    {
        // Vanilla EU V (Cooltips): ignore_layout-Kinder belegen kein Fach und liegen nach ihren Ankern im Rechteck der Box.
        var hbox = Hbox(Icon(50, 20) + Icon(40, 20, "ignore_layout = yes parentanchor = bottom|right position = { -5 -2 }") + Icon(50, 20), height: 60);
        // Kein Beitrag zur Inhaltsgröße einer Box ohne size.
        var nested = Hbox($"hbox = {{ {Icon(50, 20)} {Icon(100, 50, "ignore_layout = yes")} }}", height: 60).Children[0];
        // Wie ein Kind eines einfachen widget gelegt: eine hbox ohne size füllt dann das Rechteck der Box.
        var filling = Hbox(Icon(50, 20) + $"hbox = {{ ignore_layout = yes {Icon(10, 10)} }}", height: 60);

        Assert.Equal(3, hbox.Children.Count);
        Assert.Equal(new double[] { 75, 275, 355 }, Xs(hbox));
        AssertRect(hbox.Children[2], 355, 38, 40, 20);
        Assert.Equal(new[] { GuiTexts.LayoutIgnoreLayoutApproximated }, hbox.Children[2].Notes);
        Assert.Empty(hbox.Children[0].Notes);
        AssertRect(nested, 175, 20, 50, 20);
        AssertRect(filling.Children[0], 175, 20, 50, 20);
        AssertRect(filling.Children[1], 0, 0, 400, 60);
    }

    [Fact]
    public void Ui_scale_shrinks_the_virtual_screen()
    {
        // Regel 14: virtueller Bildschirm = Auflösung / Skalierung (1920x1080 bei 1,5 = 1280x720).
        var box = Layout("window = { size = { 200 100 } parentanchor = center }", new LayoutOptions { UiScale = 1.5 });
        // Auf dem virtuellen Bildschirm ragt 1300 breit heraus, auf dem unskalierten nicht.
        var wide = Layout("window = { size = { 1300 100 } }", new LayoutOptions { UiScale = 1.5 });
        var fits = Layout("window = { size = { 1300 100 } }");

        AssertRect(box, 540, 310, 200, 100);
        Assert.True(wide.OutsideScreen);
        Assert.False(wide.OffScreen);
        Assert.False(fits.OutsideScreen);
    }

    [Fact]
    public void Fixed_children_overflowing_hbox_are_marked()
    {
        // Probe 2b: drei feste Symbole à 100 in einer hbox von 200 liegen 0-100, 100-200, 200-300.
        var hbox = Hbox(Icon(100, 20) + Icon(100, 20) + Icon(100, 20), width: 200);

        Assert.False(hbox.OutsideParent);
        Assert.False(hbox.OutsideScreen);
        Assert.False(hbox.Children[0].OutsideParent);
        Assert.False(hbox.Children[1].OutsideParent);
        Assert.True(hbox.Children[2].OutsideParent);
        Assert.False(hbox.Children[2].OutsideScreen);
        Assert.False(hbox.Children[2].OffScreen);
    }

    [Fact]
    public void Overflow_ignores_up_to_one_pixel()
    {
        var box = Layout("window = { size = { 100 100 } icon = { size = { 101 20 } } icon = { size = { 102 20 } } }");

        Assert.False(box.Children[0].OutsideParent);
        Assert.True(box.Children[1].OutsideParent);
    }

    [Fact]
    public void Background_with_outer_margin_is_not_overflow()
    {
        // Probe 8c: positiver margin vergrößert den background nach außen; er wird nie markiert.
        var box = Layout("window = { size = { 200 100 } position = { 20 20 } background = { margin = { 10 5 } } icon = { size = { 20 20 } } }");
        var background = box.Children[0];

        Assert.True(background.IsBackground);
        AssertRect(background, 10, 15, 220, 110);
        Assert.False(background.OutsideParent);
        Assert.False(background.OutsideScreen);
        Assert.False(background.OffScreen);
        Assert.False(box.Children[1].OutsideParent);
    }

    [Fact]
    public void Background_is_not_marked_even_beyond_the_screen()
    {
        var box = Layout("window = { size = { 200 100 } position = { 5000 0 } background = { size = { 50 50 } } }");

        Assert.True(box.OffScreen);
        Assert.False(box.Children[0].OutsideParent);
        Assert.False(box.Children[0].OutsideScreen);
        Assert.False(box.Children[0].OffScreen);
    }

    [Fact]
    public void Children_of_zero_size_widget_are_not_overflow()
    {
        // Probe 6b: widget ohne size ist 0x0; Kinder werden gegen den nächsten Vorfahren mit Größe geprüft.
        var inside = Layout("window = { size = { 200 100 } widget = { icon = { size = { 40 40 } position = { 10 10 } } } }");
        var outside = Layout("window = { size = { 200 100 } widget = { widget = { icon = { size = { 40 40 } position = { 190 10 } } } } }");

        Assert.False(inside.Children[0].OutsideParent);
        Assert.False(inside.Children[0].Children[0].OutsideParent);

        var nestedIcon = outside.Children[0].Children[0].Children[0];
        Assert.False(outside.Children[0].Children[0].OutsideParent);
        Assert.True(nestedIcon.OutsideParent);
        Assert.False(nestedIcon.OutsideScreen);
    }

    [Fact]
    public void Window_off_screen_is_marked()
    {
        var box = Layout("window = { size = { 200 100 } position = { 5000 0 } icon = { size = { 20 20 } } }");
        var partly = Layout("window = { size = { 200 100 } position = { 1800 0 } }");

        Assert.True(box.OutsideScreen);
        Assert.True(box.OffScreen);
        Assert.False(box.OutsideParent);
        Assert.True(partly.OutsideScreen);
        Assert.False(partly.OffScreen);
    }

    [Fact]
    public void Repeated_list_items_are_checked_like_other_children()
    {
        // Eine hbox von 200 mit drei Einträgen à 100: der dritte (RepeatIndex 2) liegt 200-300.
        var box = Layout("hbox = { size = { 200 20 } datamodel = \"[List]\" item = { widget = { size = { 100 20 } } } }");

        Assert.Equal(new[] { 0, 1, 2 }, box.Children.Select(c => c.RepeatIndex));
        Assert.Equal(new[] { false, false, true }, box.Children.Select(c => c.OutsideParent));
    }

    [Fact]
    public void VisibilityFollowsTheOptions()
    {
        const string gui = "window = { size = { 10 10 } widget = { visible = no size = { 1 1 } } widget = { visible = \"[IsShown]\" size = { 1 1 } } }";

        Assert.Single(Layout(gui).Children);
        Assert.Equal(2, Layout(gui, new LayoutOptions { ShowHidden = true }).Children.Count);
        Assert.Empty(Layout(gui, new LayoutOptions { ShowConditional = false }).Children);
    }

    [Fact]
    public void MinimumAndMaximumSizeClamp()
    {
        AssertRect(Layout("window = { size = { 10 500 } minimumsize = { 50 50 } maximumsize = { 400 300 } }"), 0, 0, 50, 300);
    }

    [Fact]
    public void Filling_box_is_clamped_and_notes_ignored_anchors()
    {
        // Eine hbox ohne size füllt das widget, bleibt aber in minimumsize/maximumsize.
        var clamped = Layout($"widget = {{ size = {{ 400 100 }} hbox = {{ maximumsize = {{ 200 50 }} {Icon(20, 20)} }} }}").Children[0];
        var grown = Layout($"widget = {{ size = {{ 400 100 }} hbox = {{ minimumsize = {{ 500 -1 }} {Icon(20, 20)} }} }}").Children[0];
        // position und Anker wirken beim Füllen nicht: nicht gemessen, daher mit Hinweis.
        var anchored = Layout($"widget = {{ size = {{ 400 100 }} hbox = {{ position = {{ 10 10 }} {Icon(20, 20)} }} }}").Children[0];
        var parentAnchored = Layout($"widget = {{ size = {{ 400 100 }} vbox = {{ parentanchor = center {Icon(20, 20)} }} }}").Children[0];

        AssertRect(clamped, 0, 0, 200, 50);
        Assert.Empty(clamped.Notes);
        AssertRect(grown, 0, 0, 500, 100);
        AssertRect(anchored, 0, 0, 400, 100);
        Assert.Equal(new[] { GuiTexts.LayoutFillIgnoresAnchor }, anchored.Notes);
        Assert.Equal(new[] { GuiTexts.LayoutFillIgnoresAnchor }, parentAnchored.Notes);
    }

    [Fact]
    public void Minus_one_on_unmeasured_kinds_is_noted()
    {
        // Gemessen ist -1 nur an Icons (6d) und Boxen/containern; andere Arten nehmen 0 an, mit Hinweis.
        var button = Layout("widget = { size = { 300 100 } button = { size = { -1 30 } } }").Children[0];
        var widget = Layout("widget = { size = { 300 100 } widget = { size = { 40 -1 } } }").Children[0];
        var icon = Layout("widget = { size = { 300 100 } icon = { size = { -1 30 } } }").Children[0];
        var container = Layout($"widget = {{ size = {{ 300 100 }} container = {{ size = {{ -1 30 }} {Icon(40, 20)} }} }}").Children[0];

        AssertRect(button, 0, 0, 0, 30);
        Assert.Equal(new[] { GuiTexts.LayoutMinusOneUnmeasured("button") }, button.Notes);
        AssertRect(widget, 0, 0, 40, 0);
        Assert.Equal(new[] { GuiTexts.LayoutMinusOneUnmeasured("widget") }, widget.Notes);
        Assert.Empty(icon.Notes);
        AssertRect(container, 0, 0, 40, 30);
        Assert.Empty(container.Notes);
    }

    [Fact]
    public void Min_size_box_with_anchor_is_noted()
    {
        // set_parent_size_to_minimum: die Box füllt nicht und wird nach ihren Ankern gelegt; gemessen nur oben links ohne position (10b).
        var anchored = Layout($"widget = {{ size = {{ 200 80 }} vbox = {{ set_parent_size_to_minimum = yes parentanchor = right {Icon(50, 20)} }} }}").Children[0];
        var plain = Layout($"widget = {{ size = {{ 200 80 }} vbox = {{ set_parent_size_to_minimum = yes {Icon(50, 20)} }} }}").Children[0];

        AssertRect(anchored, 150, 0, 50, 20);
        Assert.Equal(new[] { GuiTexts.LayoutMinSizeBoxAnchorUnmeasured }, anchored.Notes);
        Assert.Empty(plain.Notes);
    }

    [Fact]
    public void Children_of_clipping_box_are_not_outside_parent()
    {
        // Ein Scrollbereich zeigt nur einen Ausschnitt: Kinder ragen gewollt hinaus (auch durch ein widget ohne size hindurch).
        var root = Layout($"widget = {{ size = {{ 400 300 }} scrollarea = {{ size = {{ 100 50 }} widget = {{ size = {{ 300 300 }} }} widget = {{ {Icon(200, 20)} }} }} }}");
        var scroll = root.Children[0];
        var scissor = Layout($"widget = {{ size = {{ 400 300 }} widget = {{ size = {{ 100 50 }} scissor = yes {Icon(200, 20)} }} }}").Children[0];
        // Über den Bildschirm hinaus bleibt markiert.
        var screen = Layout($"widget = {{ size = {{ 400 300 }} scrollarea = {{ size = {{ 100 50 }} {Icon(3000, 20)} }} }}").Children[0];

        Assert.False(scroll.Children[0].OutsideParent);
        Assert.False(scroll.Children[1].Children[0].OutsideParent);
        Assert.False(scissor.Children[0].OutsideParent);
        Assert.False(screen.Children[0].OutsideParent);
        Assert.True(screen.Children[0].OutsideScreen);
    }

    [Fact]
    public void Percent_child_of_nested_sizeless_box_uses_one_reference()
    {
        // Innere hbox ohne size in einer füllenden hbox (400): Messen und Legen beziehen 50 % auf dieselbe Länge, mit Hinweis.
        var outer = Layout("widget = { size = { 400 100 } hbox = { hbox = { icon = { size = { 50% 20 } } } } }").Children[0];
        var inner = outer.Children[0];
        // Füllende hbox mit margin: Prozent vom eigenen Inhalt (400 - 2×50), ohne Hinweis.
        var filled = Layout("widget = { size = { 400 100 } hbox = { margin = { 50 0 } icon = { size = { 50% 20 } } } }").Children[0];
        // Box mit size in der Achse: Prozent vom eigenen Inhalt, ohne Hinweis.
        var sized = Layout("widget = { size = { 400 100 } hbox = { size = { 200 -1 } icon = { size = { 50% 20 } } } }").Children[0];

        Assert.Equal(inner.Width, inner.Children[0].Width, 3);
        Assert.Equal(200, inner.Width, 3);
        Assert.Contains(GuiTexts.LayoutPercentInSizelessBox, inner.Notes);
        Assert.Equal(150, filled.Children[0].Width, 3);
        Assert.Empty(filled.Notes);
        Assert.Equal(100, sized.Children[0].Width, 3);
        Assert.Empty(sized.Notes);
    }

    [Fact]
    public void Percent_maximumsize_in_sizeless_box_uses_the_measuring_reference()
    {
        // Innere hbox ohne size, auf 300 gestreckt (neben einem festen Icon 100 in 400): maximumsize 50 % bezieht sich wie beim Messen auf 400.
        var outer = Layout(
            $"widget = {{ size = {{ 400 100 }} hbox = {{ {Icon(100, 20)} hbox = {{ layoutpolicy_horizontal = expanding "
            + "icon = { size = { 20 20 } layoutpolicy_horizontal = expanding maximumsize = { 50% -1 } } } } }").Children[0];
        var inner = outer.Children[1];

        AssertRect(inner, 100, 40, 300, 20);
        Assert.Equal(200, inner.Children[0].Width, 3);
        Assert.Contains(GuiTexts.LayoutPercentInSizelessBox, inner.Notes);
    }

    [Fact]
    public void Percent_child_three_sizeless_boxes_deep_uses_the_measuring_reference()
    {
        // Füllende hbox → hbox → hbox → icon 50 %: Messen und Legen beziehen sich auf dieselbe Länge (400).
        var outer = Layout("widget = { size = { 400 100 } hbox = { hbox = { hbox = { icon = { size = { 50% 20 } } } } } }").Children[0];
        var innermost = outer.Children[0].Children[0];
        Assert.Equal(innermost.Width, innermost.Children[0].Width, 3);
        Assert.Equal(200, innermost.Children[0].Width, 3);
        Assert.Contains(GuiTexts.LayoutPercentInSizelessBox, innermost.Notes);
    }

    [Fact]
    public void Percent_grid_two_sizeless_boxes_deep_matches_its_cells()
    {
        // Raster ohne size, addcolumn 25 % unter füllender hbox → hbox → hbox: gemessen gegen 400 (Zelle 100, Raster 200);
        // vor der Korrektur legt ArrangeGrid gegen den Inhalt der inneren hbox (200) und die Zellen werden 50 breit.
        var outer = Layout("widget = { size = { 400 100 } hbox = { hbox = { hbox = { fixedgridbox = { addcolumn = 25% addrow = 20 maxhorizontalslots = 2 datamodel = \"[L]\" item = { widget = { size = { 10 10 } } } } } } } }").Children[0];
        var inner = outer.Children[0].Children[0];
        var grid = inner.Children[0];
        Assert.Equal(200, inner.Width, 3);
        Assert.Equal(200, grid.Width, 3);
        Assert.Equal(new double[] { 0, 100, 0 }, grid.Children.Select(c => c.X - grid.X));
        Assert.Contains(GuiTexts.LayoutPercentInSizelessBox, grid.Notes);
    }

    [Fact]
    public void TooltipsAreNotLaidOut()
    {
        Assert.Empty(Layout("button = { size = { 10 10 } tooltipwidget = { widget = { size = { 300 300 } } } }").Children);
    }

    [Fact]
    public void UnresolvableSizeStaysFiniteWithNotes()
    {
        var box = Layout("window = { size = { \"[GetWidth]\" 20 } }");

        Assert.Equal(0, box.Width, 3);
        Assert.Equal(20, box.Height, 3);
        Assert.Contains(box.Notes, n => n.StartsWith("Größe nicht auswertbar", StringComparison.Ordinal));
    }

    [Fact]
    public void DdsHeaderReadsWidthAndHeight()
    {
        var path = Path.Combine(Path.GetTempPath(), "pmt-gui-" + Guid.NewGuid().ToString("N") + ".dds");
        try
        {
            var bytes = new byte[128];
            "DDS "u8.CopyTo(bytes);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), 32);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 64);
            File.WriteAllBytes(path, bytes);

            Assert.True(DdsHeader.TryReadSize(path, out var width, out var height));
            Assert.Equal((64, 32), (width, height));

            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            Assert.False(DdsHeader.TryReadSize(path, out _, out _));
            Assert.False(DdsHeader.TryReadSize(path + ".missing", out _, out _));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LayoutOfEveryTopLevelVanillaWidgetHasFiniteNumbers()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var rules = GuiRulesVanillaTests.ProfileRules();
        var resolver = new GuiResolver(GuiRulesVanillaTests.Vanilla, rules);
        var locator = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(VanillaGui.InstallDirectory, Array.Empty<string>(), null));
        var engine = new GuiLayoutEngine(rules, locator.SizeOf);
        Assert.NotNull(locator.SizeOf("gfx/interface/_default_icon.dds"));

        var boxes = 0;
        var uncertain = 0;
        foreach (var file in GuiRulesVanillaTests.Vanilla.Files)
        {
            foreach (var node in GuiRulesVanillaTests.Vanilla.DocumentOf(file).Root.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name.Length > 0))
            {
                var root = engine.Layout(resolver.Resolve(node));
                foreach (var box in root.Descendants().Prepend(root))
                {
                    boxes++;
                    uncertain += box.Uncertain ? 1 : 0;
                    Assert.True(
                        double.IsFinite(box.X) && double.IsFinite(box.Y) && double.IsFinite(box.Width) && double.IsFinite(box.Height),
                        $"{file.RelativePath}:{node.Line} {box.Element.Key}: {box.X} {box.Y} {box.Width} {box.Height}");
                }
            }
        }

        _output.WriteLine($"{boxes} Kästen, davon {uncertain} mit Hinweis, {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Main_windows_lay_out_quickly_and_finitely()
    {
        foreach (var game in new[] { "ck3", "vic3", "eu5" })
        {
            var install = VanillaGui.For(game);
            if (!install.Exists)
            {
                continue;   // Spiel nicht installiert: nichts zu prüfen
            }

            // Aufbau wie GuiWorkspace: Profilregeln des Spiels, Index aus clausewitz/jomini/game, Texturen in den Kontextordnern des Profils.
            var rules = GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json")));
            var index = GuiIndex.Build(GuiSourceSet.Collect(GuiSourceSet.RootsFor(install.InstallDirectory, Array.Empty<string>(), null)), layout: rules.Layout);
            var resolver = new GuiResolver(index, rules);
            var locator = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, rules.Layout.Contexts));
            // Mit dem echten Textdienst (wie GuiWorkspace), vorab geladen wie beim Öffnen: so läuft die echte Vanilla-Auszeichnung durch den ganzen Weg.
            // Gelegt wird auf Deutsch (falls vorhanden), damit auch eine gewählte Sprache mit englischem Rückfall durchläuft; das Vorladen (Schriften,
            // Formate, die Sprache und Englisch) zählt nicht zum Zeitbudget.
            var texts = JominiTextService.Build(new GuiWorkspaceOptions(install.InstallDirectory, null, Array.Empty<string>(), rules, null), index, locator);
            var language = texts.Languages.Contains("l_german") ? "l_german" : JominiLocalisation.English;
            var warm = Stopwatch.StartNew();
            texts.Warm(language);
            _output.WriteLine($"{game}: Texte vorgeladen ({language} und l_english) in {warm.ElapsedMilliseconds} ms");
            // Mit dem Texturfinder: so entstehen die Sprites der Kästen (nur Köpfe werden gelesen, nichts dekodiert).
            var engine = new GuiLayoutEngine(rules, locator.SizeOf, texts, locator);
            var options = new LayoutOptions { Language = language };

            var total = Stopwatch.StartNew();
            var windows = 0;
            var boxes = 0;
            var sprites = 0;
            var drawn = 0;
            var placeholders = new Dictionary<JominiSpritePlaceholder, int>();
            var notDrawn = new Dictionary<string, int>(StringComparer.Ordinal);
            var slowest = (Name: "", Milliseconds: 0L);
            var notes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var file in index.Files)
            {
                foreach (var node in index.DocumentOf(file).Root.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name.Length > 0))
                {
                    var where = $"{game} {file.RelativePath}:{node.Line}";
                    var watch = Stopwatch.StartNew();
                    LayoutBox root;
                    try
                    {
                        root = engine.Layout(resolver.Resolve(node), options);
                    }
                    catch (Exception ex)
                    {
                        throw new Xunit.Sdk.XunitException($"{where}: {ex}");
                    }

                    watch.Stop();
                    windows++;
                    if (watch.ElapsedMilliseconds > slowest.Milliseconds)
                    {
                        slowest = (where, watch.ElapsedMilliseconds);
                    }

                    foreach (var box in root.Descendants().Prepend(root))
                    {
                        boxes++;
                        Assert.True(
                            double.IsFinite(box.X) && double.IsFinite(box.Y) && double.IsFinite(box.Width) && double.IsFinite(box.Height),
                            $"{where} {box.Element.Key}: {box.X} {box.Y} {box.Width} {box.Height}");
                        foreach (var note in box.Notes)
                        {
                            notes[note] = notes.GetValueOrDefault(note) + 1;
                        }

                        if (box.Sprite is not { } sprite)
                        {
                            continue;
                        }

                        sprites++;
                        placeholders[sprite.Placeholder] = placeholders.GetValueOrDefault(sprite.Placeholder) + 1;
                        if (sprite.Placeholder == JominiSpritePlaceholder.NotDrawn)
                        {
                            var label = sprite.PlaceholderLabel ?? "";
                            notDrawn[label] = notDrawn.GetValueOrDefault(label) + 1;
                        }

                        if (sprite.Placeholder == JominiSpritePlaceholder.None)
                        {
                            drawn++;
                            // Der Ausschnitt liegt in der Textur (Rundung der Brüche toleriert).
                            var source = sprite.Source;
                            Assert.True(
                                source.X >= -1e-6 && source.Y >= -1e-6 && source.Width >= 0 && source.Height >= 0
                                && source.X + source.Width <= sprite.TextureWidth + 1e-6 && source.Y + source.Height <= sprite.TextureHeight + 1e-6,
                                $"{where} {box.Element.Key}: Ausschnitt {source} liegt nicht in {sprite.TextureWidth}x{sprite.TextureHeight} ({sprite.Texture})");
                        }

                        // Alle Stücke sind endlich und nicht negativ (Platzhalter geben keine).
                        foreach (var piece in JominiSpriteGeometry.Pieces(sprite, new LayoutRect(box.X, box.Y, box.Width, box.Height)))
                        {
                            foreach (var rect in new[] { piece.Source, piece.Target })
                            {
                                Assert.True(
                                    double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height)
                                    && rect.Width >= 0 && rect.Height >= 0,
                                    $"{where} {box.Element.Key}: Stück {rect} ({sprite.Texture}, {sprite.Kind}, Rand {sprite.Border})");
                            }
                        }
                    }
                }
            }

            _output.WriteLine($"{game}: {windows} Fenster, {boxes} Kästen, {total.ElapsedMilliseconds} ms; langsamstes {slowest.Name} mit {slowest.Milliseconds} ms");
            _output.WriteLine($"{game}: {sprites} Sprites, davon {drawn} mit Bild; Platzhalter: " +
                string.Join(", ", placeholders.OrderBy(p => p.Key).Select(p => $"{p.Key} {p.Value}")));
            _output.WriteLine($"{game}: nicht dargestellte Arten (NotDrawn): " +
                (notDrawn.Count == 0 ? "keine" : string.Join(", ", notDrawn.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} {p.Value}"))));
            foreach (var (text, count) in notes.OrderByDescending(n => n.Value).ThenBy(n => n.Key, StringComparer.Ordinal))
            {
                _output.WriteLine($"  {count,6}  {text}");
            }

            Assert.True(slowest.Milliseconds < 5000, $"{slowest.Name} braucht {slowest.Milliseconds} ms");
        }
    }

    [Theory]
    [InlineData("ck3")]
    [InlineData("vic3")]
    [InlineData("eu5")]
    public void Main_window_textures_decode(string game)
    {
        var install = VanillaGui.For(game);
        if (!install.Exists)
        {
            return;   // Spiel nicht installiert: nichts zu prüfen
        }

        // Die beiden Fenster mit den meisten verschiedenen Sprite-Texturen (mit Bild) legen und diese Texturen einsammeln. Nach Zahl der Kästen
        // gewählt, waren es in EU V Tooltips mit kaum Texturen.
        var rules = GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json")));
        var index = GuiIndex.Build(GuiSourceSet.Collect(GuiSourceSet.RootsFor(install.InstallDirectory, Array.Empty<string>(), null)), layout: rules.Layout);
        var resolver = new GuiResolver(index, rules);
        var locator = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(install.InstallDirectory, Array.Empty<string>(), null, rules.Layout.Contexts));
        var engine = new GuiLayoutEngine(rules, locator.SizeOf, null, locator);

        var richest = new List<(string Name, int Boxes, HashSet<string> Textures)>();
        foreach (var file in index.Files)
        {
            foreach (var node in index.DocumentOf(file).Root.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name.Length > 0))
            {
                var root = engine.Layout(resolver.Resolve(node));
                var all = root.Descendants().Prepend(root).ToList();
                var textures = all
                    .Select(b => b.Sprite)
                    .Where(s => s is { Placeholder: JominiSpritePlaceholder.None, TextureFullPath: not null })
                    .Select(s => s!.TextureFullPath!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                richest.Add(($"{file.RelativePath}:{node.Line} {node.Name}", all.Count, textures));
                if (richest.Count > 2)
                {
                    richest.Remove(richest.MinBy(w => w.Textures.Count));
                }
            }
        }

        var paths = richest
            .SelectMany(w => w.Textures)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _output.WriteLine($"{game}: Fenster {string.Join(" | ", richest.Select(w => $"{w.Name} ({w.Boxes} Kästen, {w.Textures.Count} Texturen)"))}; {paths.Count} verschiedene Texturen");
        Assert.True(paths.Count > 0, $"{game}: kein Fenster mit Sprite-Texturen gefunden");

        var watch = Stopwatch.StartNew();
        var failures = new List<string>();
        foreach (var path in paths)
        {
            if (!DecodesLikeTheCache(path))
            {
                failures.Add(path);
            }
        }

        watch.Stop();
        _output.WriteLine($"{game}: {paths.Count - failures.Count} von {paths.Count} dekodiert in {watch.ElapsedMilliseconds} ms, {failures.Count} Fehler");
        foreach (var failure in failures)
        {
            _output.WriteLine("  nicht dekodiert: " + failure);
        }

        Assert.True(failures.Count <= paths.Count * 0.01, $"{game}: {failures.Count} von {paths.Count} Texturen nicht dekodierbar");
    }

    /// <summary>Dekodiert wie der Bildspeicher: PNG und JPG (nach den ersten Byte, nicht der Endung) über WPF, alles andere über den eigenen Dekoder.</summary>
    private static bool DecodesLikeTheCache(string path)
    {
        try
        {
            var head = new byte[8];
            int read;
            using (var stream = File.OpenRead(path))
            {
                read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            }

            var isPng = read >= 8 && head[0] == 0x89 && head[1] == (byte)'P' && head[2] == (byte)'N' && head[3] == (byte)'G';
            var isJpg = read >= 2 && head[0] == 0xFF && head[1] == 0xD8;
            if (isPng || isJpg)
            {
                using var stream = File.OpenRead(path);
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    stream, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                return decoder.Frames.Count > 0 && decoder.Frames[0].PixelWidth > 0;
            }

            return ParadoxModTool.GuiDesign.Imaging.GuiImageDecoder.TryDecode(path) is { Width: > 0 };
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    [Theory]
    [InlineData("ck3")]
    [InlineData("vic3")]
    [InlineData("eu5")]
    public void Localisation_loads_in_reasonable_time(string game)
    {
        var install = VanillaGui.For(game);
        if (!install.Exists)
        {
            return;   // Spiel nicht installiert: nichts zu prüfen
        }

        // Frisch geladen (nicht der geteilte Textdienst anderer Tests), sonst misst die Zeit nur den Zwischenspeicher. Eine Sprache und Englisch,
        // wie beim Öffnen der Vorschau.
        var rules = GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json")));
        var localisation = JominiLocalisation.Load(install.InstallDirectory, Array.Empty<string>(), null, rules.Layout.Contexts);
        var watch = Stopwatch.StartNew();
        var language = localisation.Languages.Contains("l_german") ? "l_german" : JominiLocalisation.English;
        var chosen = localisation.Text("YES", language);
        var english = localisation.Text("YES", JominiLocalisation.English);
        watch.Stop();
        _output.WriteLine($"{game}: {language} und l_english in {watch.ElapsedMilliseconds} ms; YES = \"{chosen}\" / \"{english}\"");

        // YES gibt es in allen drei Spielen (Englisch und Deutsch); der Schlüssel zeigt, dass wirklich Dateien geladen wurden.
        Assert.NotNull(chosen);
        Assert.NotNull(english);
        Assert.True(watch.ElapsedMilliseconds < 15000, $"{game}: Lokalisierung braucht {watch.ElapsedMilliseconds} ms");
    }
}
