using System.Globalization;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// <see cref="JominiEffectRecipe"/> und <see cref="JominiEffectComposer"/>: Grundbild, Ebenen, Farbe, Graustufen und Leuchten auf der CPU. Texturen sind
/// Bilder im Speicher, <c>load</c> ein Wörterbuch. Liest und setzt <see cref="JominiEffectRules"/>, daher in deren Sammlung.
/// </summary>
[Collection(nameof(JominiEffectRules))]
public sealed class JominiEffectComposerTests
{
    private static readonly JominiColor White = new(1, 1, 1, 1);

    private readonly Dictionary<string, GuiImage> _images = new(StringComparer.OrdinalIgnoreCase);

    private GuiImage? Load(string path) => _images.TryGetValue(path, out var image) ? image : null;

    private static GuiImage Solid(int width, int height, byte r, byte g, byte b, byte a = 255)
    {
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = b;
            bgra[i + 1] = g;
            bgra[i + 2] = r;
            bgra[i + 3] = a;
        }

        return new GuiImage(width, height, bgra);
    }

    private JominiSpriteVisual Sprite(string name, GuiImage image, double opacity = 1, JominiColor? tint = null, double density = 1, bool mirror = false)
    {
        var path = @"C:\gfx\" + name + ".dds";
        _images[path] = image;
        return new JominiSpriteVisual(
            "gfx/" + name + ".dds", path, image.Width, image.Height, new LayoutRect(0, 0, image.Width, image.Height), density,
            JominiSpriteKind.Stretched, default, JominiFit.Fill, mirror, false, tint ?? White, opacity, JominiSpritePlaceholder.None, null);
    }

    private static JominiEffectRecipe Recipe(JominiSpriteVisual sprite, double size, params JominiTextureLayer[] layers) =>
        new(sprite, new JominiEffectVisual(layers, null, JominiColorEffect.None), size, size, 1, 1, 0);

    private static (int R, int G, int B, int A) Pixel(GuiImage image, int x, int y)
    {
        var at = (y * image.Width + x) * 4;
        return (image.Bgra[at + 2], image.Bgra[at + 1], image.Bgra[at], image.Bgra[at + 3]);
    }

    private static void AssertNear((int R, int G, int B) expected, (int R, int G, int B, int A) actual)
    {
        Assert.InRange(actual.R, expected.R - 1, expected.R + 1);
        Assert.InRange(actual.G, expected.G - 1, expected.G + 1);
        Assert.InRange(actual.B, expected.B - 1, expected.B + 1);
    }

    [Fact]
    public void Compose_NormalLayerAtZeroOpacityEqualsPlainBase()
    {
        var texture = new GuiImage(4, 4, Enumerable.Range(0, 64).Select(i => (byte)(i * 4 % 256 | 3)).ToArray());
        var sprite = Sprite("base", texture);
        var layer = new JominiTextureLayer(Sprite("layer", Solid(4, 4, 255, 0, 0)) with { Opacity = 0 }, JominiBlendMode.Normal);

        var composite = JominiEffectComposer.Compose(Recipe(sprite, 4, layer), Load);

        var plain = new GuiImage(4, 4, new byte[64]);
        JominiSpriteRaster.Draw(plain, texture, sprite, 1, 1, true, GuiTint.White);
        Assert.Equal(4, composite.Width);
        Assert.Equal(4, composite.Height);
        Assert.Equal(plain.Bgra, composite.Bgra);
    }

    [Fact]
    public void Compose_MultiplyLayer_SolidColours()
    {
        var recipe = Recipe(
            Sprite("base", Solid(4, 4, 200, 200, 200)), 4,
            new JominiTextureLayer(Sprite("layer", Solid(4, 4, 128, 64, 255)), JominiBlendMode.Multiply));

        AssertNear((100, 50, 200), Pixel(JominiEffectComposer.Compose(recipe, Load), 1, 1));
    }

    [Fact]
    public void Compose_LayersInOrder()
    {
        var recipe = Recipe(
            Sprite("base", Solid(4, 4, 255, 255, 255)), 4,
            new JominiTextureLayer(Sprite("red", Solid(4, 4, 255, 0, 0)), JominiBlendMode.Normal),
            new JominiTextureLayer(Sprite("blue", Solid(4, 4, 0, 0, 255), opacity: 0.5), JominiBlendMode.Normal));

        AssertNear((128, 0, 128), Pixel(JominiEffectComposer.Compose(recipe, Load), 2, 2));
    }

    [Fact]
    public void Compose_LayerUsesItsOwnTint()
    {
        var recipe = Recipe(
            Sprite("base", Solid(4, 4, 0, 0, 0)), 4,
            new JominiTextureLayer(Sprite("layer", Solid(4, 4, 255, 255, 255), tint: new JominiColor(0, 1, 0, 1)), JominiBlendMode.Normal));

        AssertNear((0, 255, 0), Pixel(JominiEffectComposer.Compose(recipe, Load), 0, 0));
    }

    [Fact]
    public void Compose_ElementTintAfterLayers_FollowsRule()
    {
        // Gemessen 16l: die Farbe des Elements wirkt nach den Ebenen. Weißes Grundbild mit Farbe (0,5 1 1), darüber volles Rot (normal).
        var recipe = Recipe(
            Sprite("base", Solid(4, 4, 255, 255, 255), tint: new JominiColor(0.5, 1, 1, 1)), 4,
            new JominiTextureLayer(Sprite("red", Solid(4, 4, 255, 0, 0)), JominiBlendMode.Normal));

        AssertNear((128, 0, 0), Pixel(JominiEffectComposer.Compose(recipe, Load), 1, 1));
        try
        {
            JominiEffectRules.TintAfterLayers = false;
            AssertNear((255, 0, 0), Pixel(JominiEffectComposer.Compose(recipe, Load), 1, 1));
        }
        finally
        {
            JominiEffectRules.TintAfterLayers = true;
        }
    }

    [Fact]
    public void Compose_ElementMirror_FollowsRule()
    {
        var texture = new GuiImage(2, 1, new byte[] { 0, 0, 255, 255, 255, 0, 0, 255 });   // links rot, rechts blau
        var recipe = Recipe(Sprite("base", texture, mirror: true), 2, new JominiTextureLayer(Sprite("layer", Solid(2, 2, 0, 0, 0)), JominiBlendMode.Add));

        // Spiegelt das Zeichnen das ganze Ergebnis, bleibt das Grundbild im Bild ungespiegelt.
        AssertNear((255, 0, 0), Pixel(JominiEffectComposer.Compose(recipe, Load), 0, 0));
        try
        {
            JominiEffectRules.ElementMirrorFlipsLayers = false;
            AssertNear((0, 0, 255), Pixel(JominiEffectComposer.Compose(recipe, Load), 0, 0));
        }
        finally
        {
            JominiEffectRules.ElementMirrorFlipsLayers = true;
        }
    }

    [Fact]
    public void Compose_Glow_PadsAndColours()
    {
        var texture = Solid(8, 8, 0, 0, 0, 0);
        for (var y = 2; y < 6; y++)
        {
            for (var x = 2; x < 6; x++)
            {
                var at = (y * 8 + x) * 4;
                texture.Bgra[at] = 10;
                texture.Bgra[at + 1] = 20;
                texture.Bgra[at + 2] = 30;
                texture.Bgra[at + 3] = 255;
            }
        }

        // glow_radius 5: (5 − 3) × 1,2 = 2,4 → Kastenfilter-Radius 2 Pixel, Polsterung 2.
        var effects = new JominiEffectVisual(Array.Empty<JominiTextureLayer>(), new JominiGlow(new JominiColor(1, 0, 0, 1), 5, 1), JominiColorEffect.None);
        var recipe = JominiEffectRecipe.Create(Sprite("base", texture), effects, 8, 8, 1, new List<string>());
        Assert.Equal(1, recipe.PixelsPerUnit);
        Assert.Equal(2, recipe.PaddingPixels);

        var composite = JominiEffectComposer.Compose(recipe, Load);

        Assert.Equal(12, composite.Width);
        Assert.Equal(12, composite.Height);
        var outside = Pixel(composite, 3, 6);   // Spalte vor dem Quadrat (Quadrat bei 4..7 im gepolsterten Bild)
        Assert.Equal((255, 0, 0), (outside.R, outside.G, outside.B));
        Assert.InRange(outside.A, 1, 254);
        Assert.Equal((30, 20, 10, 255), Pixel(composite, 5, 5));
    }

    [Fact]
    public void Create_GlowAtOrBelowOffset_HasNoPaddingAndNoGlow()
    {
        // Gemessen 16r: glow_radius 3 ist im Spiel fast unsichtbar; bis GlowRadiusOffset gibt es kein Leuchten.
        var effects = new JominiEffectVisual(Array.Empty<JominiTextureLayer>(), new JominiGlow(new JominiColor(1, 0, 0, 1), 3, 1), JominiColorEffect.None);
        var recipe = JominiEffectRecipe.Create(Sprite("base", Solid(4, 4, 0, 0, 0, 0)), effects, 4, 4, 1, new List<string>());

        Assert.Null(recipe.Effects.Glow);
        Assert.True(recipe.Effects.IsEmpty);
        Assert.Equal(0, recipe.GlowRadiusPixels);
        Assert.Equal(0, recipe.PaddingPixels);
        Assert.All(JominiEffectComposer.Compose(recipe, Load).Bgra, value => Assert.Equal(0, value));
    }

    [Theory]
    // Dichte 4: (R − 3) × 1,2 × 4 Pixel, kaufmännisch gerundet. 3,05 → 0,24 und 3,1 → 0,48 (nach Round(…, 6) ohne Gleitkommarauschen) ergeben 0.
    [InlineData(3.05)]
    [InlineData(3.1)]
    public void Create_GlowBlurZeroAtPixelsPerUnit_DropsGlow(double radius)
    {
        var notes = new List<string>();
        var glow = new JominiGlow(new JominiColor(1, 0, 0, 1), radius, 1);
        var effects = new JominiEffectVisual(Array.Empty<JominiTextureLayer>(), glow, JominiColorEffect.None);

        var recipe = JominiEffectRecipe.Create(Sprite("base", Solid(4, 4, 255, 0, 0), density: 4), effects, 10, 10, 1, notes);

        Assert.Equal(4, recipe.PixelsPerUnit);
        Assert.Null(recipe.Effects.Glow);
        Assert.True(recipe.Effects.IsEmpty);
        Assert.Equal(0, recipe.PaddingPixels);
        Assert.Empty(notes);

        // Andere Effekte bleiben, nur das Leuchten fällt weg.
        var layer = new JominiTextureLayer(Sprite("layer", Solid(4, 4, 0, 0, 255)), JominiBlendMode.Multiply);
        var withLayer = JominiEffectRecipe.Create(
            Sprite("base", Solid(4, 4, 255, 0, 0), density: 4), effects with { Layers = new[] { layer } }, 10, 10, 1, notes);
        Assert.Null(withLayer.Effects.Glow);
        Assert.Equal(layer, Assert.Single(withLayer.Effects.Layers));
        Assert.Equal(0, withLayer.PaddingPixels);
    }

    [Fact]
    public void Create_GlowBlurOneAtPixelsPerUnit_KeepsGlow()
    {
        // Dichte 4: (3,2 − 3) × 1,2 × 4 = 0,96 → Kastenfilter-Radius 1, Polsterung 1.
        var glow = new JominiGlow(new JominiColor(1, 0, 0, 1), 3.2, 1);
        var effects = new JominiEffectVisual(Array.Empty<JominiTextureLayer>(), glow, JominiColorEffect.None);

        var recipe = JominiEffectRecipe.Create(Sprite("base", Solid(4, 4, 255, 0, 0), density: 4), effects, 10, 10, 1, new List<string>());

        Assert.Equal(4, recipe.PixelsPerUnit);
        Assert.Same(glow, recipe.Effects.Glow);
        Assert.Equal(1, recipe.PaddingPixels);
    }

    [Fact]
    public void Create_GlowAtTwoPixelsPerUnit_ScalesPadding()
    {
        // Dichte 2: (8 − 3) × 1,2 × 2 = 12 Pixel Kastenfilter-Radius, Polsterung 12.
        var glow = new JominiGlow(new JominiColor(1, 0, 0, 1), 8, 1);
        var effects = new JominiEffectVisual(Array.Empty<JominiTextureLayer>(), glow, JominiColorEffect.None);

        var recipe = JominiEffectRecipe.Create(Sprite("base", Solid(4, 4, 255, 0, 0), density: 2), effects, 10, 10, 1, new List<string>());

        Assert.Equal(2, recipe.PixelsPerUnit);
        Assert.Equal(12, recipe.GlowRadiusPixels, 9);
        Assert.Equal(12, recipe.PaddingPixels);
        Assert.Equal((44, 44), (JominiEffectComposer.Compose(recipe, Load).Width, JominiEffectComposer.Compose(recipe, Load).Height));
    }

    [Fact]
    public void Compose_GlowWithCappedPadding_FadesInsideImage()
    {
        var notes = new List<string>();
        var effects = new JominiEffectVisual(Array.Empty<JominiTextureLayer>(), new JominiGlow(new JominiColor(1, 0, 0, 1), 400, 1), JominiColorEffect.None);
        var recipe = JominiEffectRecipe.Create(Sprite("base", Solid(4, 4, 255, 255, 255)), effects, 300, 300, 1, notes);

        Assert.Equal(JominiEffectRecipe.MaxPadding, recipe.PaddingPixels);
        Assert.Contains(GuiTexts.LayoutEffectApproximated("resolution"), notes);

        // Der Radius wird mit der Polsterung gedeckelt: am äußeren Rand ist das Leuchten fast null, nicht abgeschnitten.
        var composite = JominiEffectComposer.Compose(recipe, Load);
        var middle = composite.Height / 2;
        Assert.InRange(Pixel(composite, 0, middle).A, 0, 1);
        Assert.InRange(Pixel(composite, JominiEffectRecipe.MaxPadding - 1, middle).A, 2, 255);
    }

    [Fact]
    public void Compose_Grayscale()
    {
        var recipe = new JominiEffectRecipe(
            Sprite("base", Solid(2, 2, 255, 0, 0)), new JominiEffectVisual(Array.Empty<JominiTextureLayer>(), null, JominiColorEffect.Grayscale), 2, 2, 1, 1, 0);

        Assert.Equal((76, 76, 76, 255), Pixel(JominiEffectComposer.Compose(recipe, Load), 0, 0));
    }

    [Fact]
    public void Compose_SkipsUnloadableLayer()
    {
        var broken = Sprite("broken", Solid(4, 4, 0, 0, 0));
        _images.Remove(broken.TextureFullPath!);
        var recipe = Recipe(
            Sprite("base", Solid(4, 4, 200, 200, 200)), 4,
            new JominiTextureLayer(broken, JominiBlendMode.Multiply),
            new JominiTextureLayer(Sprite("blue", Solid(4, 4, 0, 0, 255), opacity: 0.5), JominiBlendMode.Normal));

        AssertNear((100, 100, 228), Pixel(JominiEffectComposer.Compose(recipe, Load), 1, 1));
    }

    [Fact]
    public void Compose_UnloadableBase_IsTransparent()
    {
        var sprite = Sprite("base", Solid(4, 4, 200, 200, 200));
        _images.Clear();

        var composite = JominiEffectComposer.Compose(Recipe(sprite, 4, new JominiTextureLayer(sprite, JominiBlendMode.Normal)), Load);

        Assert.Equal(4, composite.Width);
        Assert.All(composite.Bgra, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Compose_CapsResolutionAndNotes()
    {
        var notes = new List<string>();
        var effects = new JominiEffectVisual(
            new[] { new JominiTextureLayer(Sprite("layer", Solid(4, 4, 0, 0, 255), density: 2), JominiBlendMode.Normal) }, null, JominiColorEffect.None);

        var recipe = JominiEffectRecipe.Create(Sprite("base", Solid(4, 4, 255, 0, 0), density: 2), effects, 3000, 100, 1, notes);
        var composite = JominiEffectComposer.Compose(recipe, Load);

        Assert.True(recipe.PixelsPerUnit < 2);
        Assert.True(composite.Width <= 2048, $"Breite {composite.Width}");
        Assert.Equal(recipe.PixelWidth, composite.Width);
        Assert.Equal(new[] { GuiTexts.LayoutEffectApproximated("resolution") }, notes);
    }

    [Fact]
    public void Create_DensityPicksPixelsPerUnit()
    {
        var notes = new List<string>();
        var effects = new JominiEffectVisual(
            new[] { new JominiTextureLayer(Sprite("layer", Solid(4, 4, 0, 0, 255), density: 3), JominiBlendMode.Normal) }, null, JominiColorEffect.None);

        var recipe = JominiEffectRecipe.Create(Sprite("base", Solid(4, 4, 255, 0, 0), density: 2), effects, 10, 5, 1, notes);

        Assert.Equal(3, recipe.PixelsPerUnit);
        Assert.Equal(0, recipe.PaddingPixels);
        Assert.Equal((30, 15), (recipe.PixelWidth, recipe.PixelHeight));
        Assert.Empty(notes);
        Assert.Equal(4, JominiEffectRecipe.Create(Sprite("base", Solid(4, 4, 255, 0, 0), density: 9), effects, 10, 5, 1, notes).PixelsPerUnit);
    }

    [Fact]
    public void ComponentPaths_BaseAndLayersDistinct()
    {
        var sprite = Sprite("base", Solid(4, 4, 255, 0, 0));
        var layer = Sprite("layer", Solid(4, 4, 0, 0, 255));
        var recipe = Recipe(sprite, 4, new JominiTextureLayer(layer, JominiBlendMode.Normal), new JominiTextureLayer(layer, JominiBlendMode.Add));

        Assert.Equal(new[] { sprite.TextureFullPath!, layer.TextureFullPath! }, recipe.ComponentPaths);
    }

    [Fact]
    public void CacheKey_IgnoresOpacity()
    {
        var sprite = Sprite("base", Solid(4, 4, 255, 0, 0));
        var layer = new JominiTextureLayer(Sprite("layer", Solid(4, 4, 0, 0, 255), opacity: 0.5), JominiBlendMode.Normal);
        var recipe = Recipe(sprite, 4, layer);

        Assert.Equal(recipe.CacheKey, (recipe with { Base = sprite with { Opacity = 0.3 } }).CacheKey);
        var otherLayer = new JominiTextureLayer(layer.Sprite with { Opacity = 0.75 }, JominiBlendMode.Normal);
        Assert.NotEqual(recipe.CacheKey, Recipe(sprite, 4, otherLayer).CacheKey);
        Assert.NotEqual(recipe.CacheKey, (recipe with { Base = sprite with { Tint = new JominiColor(0.5, 1, 1, 1) } }).CacheKey);
        Assert.NotEqual(recipe.CacheKey, (recipe with { BoxWidth = 5 }).CacheKey);

        // Spiegelt das Zeichnen das ganze Ergebnis, gehört die Spiegelung des Grundbilds nicht in den Schlüssel.
        var mirrored = recipe with { Base = sprite with { MirrorHorizontal = true } };
        Assert.Equal(recipe.CacheKey, mirrored.CacheKey);
        try
        {
            JominiEffectRules.ElementMirrorFlipsLayers = false;
            Assert.NotEqual(recipe.CacheKey, mirrored.CacheKey);
        }
        finally
        {
            JominiEffectRules.ElementMirrorFlipsLayers = true;
        }
    }

    [Fact]
    public void CacheKey_InvariantCulture()
    {
        var recipe = new JominiEffectRecipe(
            Sprite("base", Solid(4, 4, 255, 0, 0), tint: new JominiColor(0.5, 0.25, 1, 1)),
            new JominiEffectVisual(
                new[] { new JominiTextureLayer(Sprite("layer", Solid(4, 4, 0, 0, 255), opacity: 0.5), JominiBlendMode.Overlay) },
                new JominiGlow(new JominiColor(1, 0.5, 0, 1), 2.5, 0.75), JominiColorEffect.None),
            10.5, 4.25, 1.5, 1.5, 4);
        var invariant = recipe.CacheKey;
        Assert.Contains("10.5", invariant);

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(invariant, recipe.CacheKey);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
