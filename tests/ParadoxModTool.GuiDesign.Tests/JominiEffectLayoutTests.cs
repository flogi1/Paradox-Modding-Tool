using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Die Engine gibt Kästen mit gezeichnetem Sprite und Effekten ein <see cref="LayoutBox.Effects"/> mit der fertigen Kastengröße. Texturen sind kleine
/// DDS-Dateien in einer eigenen Installation im Temp-Ordner. Liest <see cref="JominiEffectRules"/>, daher in deren Sammlung.
/// </summary>
[Collection(nameof(JominiEffectRules))]
public sealed class JominiEffectLayoutTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-effectlayout-" + Guid.NewGuid().ToString("N"));
    private readonly GuiTextureLocator _textures;

    public JominiEffectLayoutTests()
    {
        var gfx = Path.Combine(_temp, "install", "game", "gfx");
        TestDds.WriteBgra(Path.Combine(gfx, "base.dds"), 40, 40);
        TestDds.WriteBgra(Path.Combine(gfx, "layer.dds"), 20, 20);
        _textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(Path.Combine(_temp, "install"), Array.Empty<string>(), null, Array.Empty<string>()));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    /// <summary>Wie <see cref="GuiResolverTests.Rules"/>, dazu <c>glow</c> als Eigenschaftsblock.</summary>
    private static readonly GuiRules Rules = new(
        new[] { "modify_texture", "glow", "state" },
        new[] { "tooltipwidget", "item" },
        new[] { "item" });

    private LayoutBox Layout(string gui)
    {
        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/library.gui"), ""));
        var doc = GuiParser.Parse(gui);
        var resolver = new GuiResolver(index, Rules, doc, @"C:\mod\in_game\gui\edit.gui");
        var element = resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
        return new GuiLayoutEngine(Rules, _textures.SizeOf, null, _textures).Layout(element);
    }

    [Fact]
    public void Background_WithModifyTexture_GetsEffectsAtFinalSize()
    {
        var root = Layout(
            "widget = { size = { 100 50 } scale = 0.5 alpha = 0.5 "
            + "background = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" blend_mode = multiply } } }");
        var box = root.Descendants().Single(b => b.IsBackground);

        var effects = Assert.IsType<JominiEffectRecipe>(box.Effects);
        Assert.Equal(50, effects.BoxWidth, 9);
        Assert.Equal(25, effects.BoxHeight, 9);
        Assert.Equal(box.Width, effects.BoxWidth);
        Assert.Equal(box.RenderScale, effects.RenderScale);
        Assert.Same(box.Sprite, effects.Base);
        Assert.Equal(JominiBlendMode.Multiply, Assert.Single(effects.Effects.Layers).Mode);
        Assert.Single(box.Notes, GuiTexts.LayoutEffectsApproximated);
    }

    [Fact]
    public void Effectname_NoHighlight_HasNoEffects()
    {
        var box = Layout("icon = { size = { 20 20 } texture = \"gfx/base.dds\" effectname = NoHighlight }");

        Assert.Null(box.Effects);
        Assert.Empty(box.Notes);
    }

    [Fact]
    public void DynamicBaseTexture_HasNoEffects()
    {
        var box = Layout("icon = { size = { 20 20 } texture = \"[GetTexture]\" modify_texture = { texture = \"gfx/layer.dds\" } }");

        Assert.Null(box.Effects);
        Assert.DoesNotContain(GuiTexts.LayoutEffectsApproximated, box.Notes);
        Assert.Contains(GuiTexts.LayoutEffectNotDrawn("modify_texture"), box.Notes);
    }

    [Fact]
    public void GlowWithoutBlur_HasNoEffectsAndNoNote()
    {
        // glow_radius 3: (3 − 3) × 1,2 = 0 Kastenfilter-Radius; das Spiel zeichnet nichts (16r), also kein Effektbild und kein Hinweis.
        var box = Layout("icon = { size = { 20 20 } texture = \"gfx/base.dds\" glow = { glow_radius = 3 color = { 1 0 0 1 } } }");

        Assert.Null(box.Effects);
        Assert.DoesNotContain(GuiTexts.LayoutEffectsApproximated, box.Notes);
        Assert.Empty(box.Notes);

        // Mit einer Ebene bleibt das Rezept, ohne Leuchten.
        var layered = Layout(
            "icon = { size = { 20 20 } texture = \"gfx/base.dds\" glow = { glow_radius = 3 } modify_texture = { texture = \"gfx/layer.dds\" blend_mode = multiply } }");
        var effects = Assert.IsType<JominiEffectRecipe>(layered.Effects);
        Assert.Null(effects.Effects.Glow);
        Assert.Equal(0, effects.PaddingPixels);
        Assert.Single(layered.Notes, GuiTexts.LayoutEffectsApproximated);
    }

    [Fact]
    public void BuildNotes_ReachTheBoxOnce()
    {
        var box = Layout(
            "icon = { size = { 20 20 } texture = \"gfx/base.dds\" "
            + "modify_texture = { texture = \"gfx/layer.dds\" blend_mode = foo } modify_texture = { texture = \"gfx/layer.dds\" blend_mode = bar } }");

        Assert.NotNull(box.Effects);
        Assert.Single(box.Notes, GuiTexts.LayoutEffectApproximated("blend_mode"));
        Assert.Single(box.Notes, GuiTexts.LayoutEffectsApproximated);
    }
}
