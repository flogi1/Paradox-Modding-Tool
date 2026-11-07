using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Tests, die <see cref="JominiEffectRules"/> umstellen, laufen nicht parallel zu anderen, die die Regeln lesen.</summary>
[CollectionDefinition(nameof(JominiEffectRules), DisableParallelization = true)]
public sealed class JominiEffectRulesCollection
{
}

/// <summary>
/// <see cref="JominiEffects.Build"/> liest <c>modify_texture</c>, <c>glow</c> und <c>effectname</c> eines aufgelösten Elements in ein
/// <see cref="JominiEffectVisual"/>. Texturen sind kleine DDS-Dateien in einer eigenen Installation im Temp-Ordner; gelesen werden nur ihre Köpfe.
/// </summary>
[Collection(nameof(JominiEffectRules))]
public sealed class JominiEffectsTests : IDisposable
{
    private static readonly GuiRules Rules = new(
        new[] { "modify_texture", "glow", "state" },
        new[] { "tooltipwidget" },
        Array.Empty<string>());

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-effects-" + Guid.NewGuid().ToString("N"));
    private readonly GuiTextureLocator _textures;

    public JominiEffectsTests()
    {
        var gfx = Path.Combine(_temp, "install", "game", "gfx");
        TestDds.WriteBgra(Path.Combine(gfx, "base.dds"), 40, 40);
        TestDds.WriteBgra(Path.Combine(gfx, "layer.dds"), 20, 20);
        TestDds.WriteBgra(Path.Combine(gfx, "layer2.dds"), 20, 20);
        _textures = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(Path.Combine(_temp, "install"), Array.Empty<string>(), null, Array.Empty<string>()));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private ResolvedElement Resolve(string gui, string library = "")
    {
        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/library.gui"), library));
        var doc = GuiParser.Parse(gui);
        var resolver = new GuiResolver(index, Rules, doc, @"C:\mod\in_game\gui\edit.gui");
        return resolver.Resolve(doc.Root.Children.First(c => c.Kind == GuiNodeKind.KeyedBlock));
    }

    /// <summary>Löst das Element auf, baut das Grund-Sprite wie die Engine und dann die Effekte.</summary>
    private (JominiEffectVisual? Effects, List<string> Notes) Build(string gui, string library = "")
    {
        var element = Resolve(gui, library);
        var notes = new List<string>();
        var sprite = JominiSprites.Build(element, _textures, notes);
        return (JominiEffects.Build(element, sprite, _textures, notes), notes);
    }

    [Fact]
    public void Build_LayerFromUsing_HasTextureAndMode()
    {
        var (effects, _) = Build(
            "icon = { texture = \"gfx/base.dds\" modify_texture = { using = color_red_texture blend_mode = overlay alpha = 0.5 } }",
            "template color_red_texture { texture = \"gfx/layer.dds\" }");

        var layer = Assert.Single(effects!.Layers);
        Assert.Equal(JominiBlendMode.Overlay, layer.Mode);
        Assert.Equal("gfx/layer.dds", layer.Sprite.Texture);
        Assert.Equal(0.5, layer.Sprite.Opacity, 9);
        Assert.Null(effects.Glow);
        Assert.Equal(JominiColorEffect.None, effects.ColorEffect);
        Assert.False(effects.IsEmpty);
    }

    [Fact]
    public void Build_BlendModeCaseInsensitive()
    {
        Assert.Equal(JominiBlendMode.AlphaMultiply, Single("alphaMultiply").Mode);
        Assert.Equal(JominiBlendMode.Normal, Single("\"Normal\"").Mode);

        var (effects, notes) = Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" blend_mode = foo } }");
        Assert.Equal(JominiEffectRules.DefaultBlendMode, Assert.Single(effects!.Layers).Mode);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("blend_mode"), notes);

        var (plain, plainNotes) = Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" } }");
        Assert.Equal(JominiEffectRules.DefaultBlendMode, Assert.Single(plain!.Layers).Mode);
        Assert.DoesNotContain(GuiTexts.LayoutEffectApproximated("blend_mode"), plainNotes);

        JominiTextureLayer Single(string mode) =>
            Assert.Single(Build($"icon = {{ texture = \"gfx/base.dds\" modify_texture = {{ texture = \"gfx/layer.dds\" blend_mode = {mode} }} }}").Effects!.Layers);
    }

    [Fact]
    public void Build_DropsInvisibleAndZeroAlphaLayers()
    {
        Assert.Null(Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" visible = no } }").Effects);
        Assert.Null(Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" alpha = 0 } }").Effects);
        Assert.Null(Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" alpha = 0 blend_mode = alphamultiply } }").Effects);
        Assert.Null(Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" alpha = 0 blend_mode = mask } }").Effects);
    }

    [Fact]
    public void Build_InvisibleLayerWithMissingTexture_HasNoTextureNote()
    {
        // EU V: block "unused_color" { visible = no } blendet Ebenen standardmäßig aus; die Textur wird dann gar nicht gesucht.
        var (effects, notes) = Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/nothere.dds\" visible = no } }");

        Assert.Null(effects);
        Assert.DoesNotContain(GuiTexts.LayoutTextureMissing("gfx/nothere.dds"), notes);
    }

    [Fact]
    public void Build_DropsLayerWithMissingTexture_KeepsOthers()
    {
        var (effects, notes) = Build(
            "icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/nothere.dds\" } modify_texture = { texture = \"gfx/layer.dds\" } }");

        Assert.Equal("gfx/layer.dds", Assert.Single(effects!.Layers).Sprite.Texture);
        Assert.Contains(GuiTexts.LayoutTextureMissing("gfx/nothere.dds"), notes);
    }

    [Fact]
    public void Build_DropsDynamicAndVideoLayers()
    {
        var (dynamic, dynamicNotes) = Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"[GetTexture]\" } }");
        Assert.Null(dynamic);
        Assert.Contains(GuiTexts.LayoutTextureDynamic("[GetTexture]"), dynamicNotes);

        var (video, videoNotes) = Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" video = \"gfx/a.bk2\" } }");
        Assert.Null(video);
        Assert.Contains(GuiTexts.LayoutEffectNotDrawn("video"), videoNotes);
    }

    [Fact]
    public void Build_VisibleBinding_KeptWithNote()
    {
        var (effects, notes) = Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" visible = \"[IsShown]\" } }");

        Assert.Single(effects!.Layers);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("visible"), notes);
    }

    [Fact]
    public void Build_LayerKeepsOwnGeometry()
    {
        var (effects, _) = Build(
            "icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" spriteType = corneredstretched spriteborder = { 8 8 } mirror = horizontal } }");

        var sprite = Assert.Single(effects!.Layers).Sprite;
        Assert.Equal(JominiSpriteKind.CorneredStretched, sprite.Kind);
        Assert.Equal(8, sprite.Border.Left);
        Assert.True(sprite.MirrorHorizontal);
        Assert.False(sprite.MirrorVertical);
    }

    [Fact]
    public void Build_UvAnimationNoted()
    {
        var (effects, notes) = Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" translate_uv = { 0 0.1 } } }");
        Assert.Single(effects!.Layers);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("translate_uv"), notes);

        var (rotated, rotatedNotes) = Build("icon = { texture = \"gfx/base.dds\" modify_texture = { texture = \"gfx/layer.dds\" rotate_uv = 0.5 } }");
        Assert.Single(rotated!.Layers);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("rotate_uv"), rotatedNotes);
    }

    [Fact]
    public void Build_LayerInStateNoted()
    {
        var (effects, notes) = Build("icon = { texture = \"gfx/base.dds\" state = { name = a modify_texture = { name = x alpha = 1 } } }");

        Assert.Null(effects);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("state"), notes);

        var (_, glowNotes) = Build("icon = { texture = \"gfx/base.dds\" state = { name = a glow = { alpha = 1 } } }");
        Assert.Contains(GuiTexts.LayoutEffectApproximated("state"), glowNotes);

        var (_, plainNotes) = Build("icon = { texture = \"gfx/base.dds\" state = { name = a alpha = 1 } }");
        Assert.DoesNotContain(GuiTexts.LayoutEffectApproximated("state"), plainNotes);
    }

    [Fact]
    public void Build_Glow()
    {
        var (effects, notes) = Build("icon = { texture = \"gfx/base.dds\" glow = { glow_radius = 3 color = { 1 0 0 1 } alpha = 0.3 } }");
        Assert.Equal(new JominiGlow(new JominiColor(1, 0, 0, 1), 3, 0.3), effects!.Glow);
        Assert.Empty(effects.Layers);
        Assert.Empty(notes);

        Assert.Null(Build("icon = { texture = \"gfx/base.dds\" glow = { glow_radius = 0 color = { 1 0 0 1 } } }").Effects);

        var (defaults, _) = Build("icon = { texture = \"gfx/base.dds\" glow = { glow_radius = 2 } }");
        Assert.Equal(new JominiGlow(new JominiColor(1, 1, 1, 1), 2, 1), defaults!.Glow);

        var (ruled, ruledNotes) = Build(
            "icon = { texture = \"gfx/base.dds\" glow = { glow_radius = 3 glow_generation_rules = { glow_alpha_mask = 0 glow_blur_passes = 2 } } }");
        Assert.NotNull(ruled!.Glow);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("glow_generation_rules"), ruledNotes);

        var (_, alphaNotes) = Build("icon = { texture = \"gfx/base.dds\" glow = { glow_radius = 3 glow_alpha = 0.5 } }");
        Assert.Contains(GuiTexts.LayoutEffectApproximated("glow_generation_rules"), alphaNotes);

        // alpha, das keine Zahl ist (Bindung): Deckkraft 1, Hinweis am Leuchten (nicht der Textur-Hinweis des Element-alpha).
        var (bound, boundNotes) = Build("icon = { texture = \"gfx/base.dds\" glow = { glow_radius = 5 alpha = \"[GetGlowAlpha]\" } }");
        Assert.Equal(1, bound!.Glow!.Alpha);
        Assert.Equal(new[] { GuiTexts.LayoutEffectApproximated("glow") }, boundNotes);
    }

    [Fact]
    public void Build_Effectname()
    {
        try
        {
            JominiEffectRules.GrayscaleApplies = true;
            Assert.Equal(JominiColorEffect.Grayscale, Build("icon = { texture = \"gfx/base.dds\" effectname = \"PdxGuiGrayscale\" }").Effects!.ColorEffect);
            Assert.Equal(JominiColorEffect.Grayscale, Build("icon = { texture = \"gfx/base.dds\" effectname = greyedout }").Effects!.ColorEffect);
            Assert.Equal(JominiColorEffect.Grayscale, Build("icon = { texture = \"gfx/base.dds\" effectname = PdxGuiDesaturate }").Effects!.ColorEffect);
        }
        finally
        {
            JominiEffectRules.GrayscaleApplies = false;
        }

        // Gemessen 16u–16w: ohne Wirkung; PMT lässt den Namen bewusst weg und sagt es.
        var (gray, grayNotes) = Build("icon = { texture = \"gfx/base.dds\" effectname = \"PdxGuiGrayscale\" }");
        Assert.Null(gray);
        Assert.Equal(new[] { GuiTexts.LayoutEffectApproximated("effectname") }, grayNotes);

        var (grayLayer, _) = Build("icon = { texture = \"gfx/base.dds\" effectname = GreyedOut modify_texture = { texture = \"gfx/layer.dds\" } }");
        Assert.Equal(JominiColorEffect.None, grayLayer!.ColorEffect);

        var (silent, silentNotes) = Build("icon = { texture = \"gfx/base.dds\" effectname = \"NoHighlight\" }");
        Assert.Null(silent);
        Assert.Empty(silentNotes);

        var (snow, snowNotes) = Build("icon = { texture = \"gfx/base.dds\" effectname = \"Snow\" }");
        Assert.Null(snow);
        Assert.Contains(GuiTexts.LayoutEffectNotDrawn("Snow"), snowNotes);

        var (bound, boundNotes) = Build("icon = { texture = \"gfx/base.dds\" effectname = \"[GetEffect]\" }");
        Assert.Null(bound);
        Assert.Contains(GuiTexts.LayoutEffectNotDrawn("[GetEffect]"), boundNotes);
    }

    [Fact]
    public void Build_NoEffects_ReturnsNull()
    {
        var (effects, notes) = Build("icon = { texture = \"gfx/base.dds\" }");

        Assert.Null(effects);
        Assert.Empty(notes);
    }

    [Fact]
    public void Build_SameNameLayers()
    {
        const string Gui = "icon = { texture = \"gfx/base.dds\" "
            + "modify_texture = { name = a texture = \"gfx/layer.dds\" } "
            + "modify_texture = { name = b texture = \"gfx/layer2.dds\" blend_mode = add } "
            + "modify_texture = { name = a texture = \"gfx/layer2.dds\" blend_mode = multiply } }";

        var keep = Build(Gui).Effects!.Layers;
        Assert.Equal(new[] { "gfx/layer.dds", "gfx/layer2.dds", "gfx/layer2.dds" }, keep.Select(l => l.Sprite.Texture));

        try
        {
            JominiEffectRules.SameNameLayerReplaces = true;
            var replaced = Build(Gui).Effects!.Layers;
            Assert.Equal(new[] { JominiBlendMode.Multiply, JominiBlendMode.Add }, replaced.Select(l => l.Mode));
            Assert.Equal(new[] { "gfx/layer2.dds", "gfx/layer2.dds" }, replaced.Select(l => l.Sprite.Texture));
        }
        finally
        {
            JominiEffectRules.SameNameLayerReplaces = false;
        }
    }

    [Fact]
    public void Build_NoBaseSprite_EffectsNotDrawn()
    {
        const string Effects = "modify_texture = { texture = \"gfx/layer.dds\" } glow = { glow_radius = 3 } effectname = PdxGuiGrayscale";

        var (none, noneNotes) = Build($"icon = {{ {Effects} }}");
        Assert.Null(none);
        Assert.Contains(GuiTexts.LayoutEffectNotDrawn("modify_texture"), noneNotes);
        Assert.Contains(GuiTexts.LayoutEffectNotDrawn("glow"), noneNotes);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("effectname"), noneNotes);
        Assert.DoesNotContain(GuiTexts.LayoutEffectNotDrawn("PdxGuiGrayscale"), noneNotes);

        try
        {
            JominiEffectRules.GrayscaleApplies = true;
            Assert.Contains(GuiTexts.LayoutEffectNotDrawn("PdxGuiGrayscale"), Build($"icon = {{ {Effects} }}").Notes);
        }
        finally
        {
            JominiEffectRules.GrayscaleApplies = false;
        }

        var (missing, missingNotes) = Build($"icon = {{ texture = \"gfx/nothere.dds\" {Effects} }}");
        Assert.Null(missing);
        Assert.Contains(GuiTexts.LayoutEffectNotDrawn("modify_texture"), missingNotes);
        Assert.Contains(GuiTexts.LayoutEffectNotDrawn("glow"), missingNotes);

        var (text, textNotes) = Build("text_single = { text = \"x\" glow = { glow_radius = 3 alpha = 0.3 } }");
        Assert.Null(text);
        Assert.Equal(new[] { GuiTexts.LayoutEffectNotDrawn("glow") }, textNotes);
    }

    [Fact]
    public void Rules_Defaults()
    {
        Assert.Equal(JominiBlendMode.Add, JominiEffectRules.DefaultBlendMode);
        Assert.True(JominiEffectRules.TintAfterLayers);
        Assert.False(JominiEffectRules.GrayscaleApplies);
        Assert.Equal(JominiAlphaChannel.Alpha, JominiEffectRules.AlphaMultiplyChannel);
        Assert.Equal(JominiAlphaChannel.Alpha, JominiEffectRules.MaskChannel);
        Assert.Equal((0.299, 0.587, 0.114), JominiEffectRules.GrayscaleWeights);
        Assert.Equal(1.2, JominiEffectRules.GlowRadiusScale);
        Assert.Equal(3.0, JominiEffectRules.GlowRadiusOffset);
        Assert.True(JominiEffectRules.ElementMirrorFlipsLayers);
        Assert.False(JominiEffectRules.SameNameLayerReplaces);
    }
}
