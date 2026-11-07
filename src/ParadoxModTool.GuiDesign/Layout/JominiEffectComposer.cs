using System.Globalization;
using System.Text;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Alles, was das Bild eines Kastens mit Effekten bestimmt: Grund-Sprite, Effekte, Kastengröße in GUI-Einheiten (nach <c>scale</c>), <see cref="LayoutBox.RenderScale"/>,
/// Pixel je GUI-Einheit und Polsterung für das Leuchten (Pixel je Seite). Deckkraft des Elements, geerbtes Alpha und (mit
/// <see cref="JominiEffectRules.ElementMirrorFlipsLayers"/>) die Spiegelung des Elements wendet der Zeichner an; sie gehören nicht zum Bild.
/// </summary>
public sealed record JominiEffectRecipe(
    JominiSpriteVisual Base,
    JominiEffectVisual Effects,
    double BoxWidth,
    double BoxHeight,
    double RenderScale,
    double PixelsPerUnit,
    int PaddingPixels)
{
    /// <summary>Größte Seite des Bilds ohne Polsterung in Pixeln; darüber wird <see cref="PixelsPerUnit"/> kleiner (Hinweis "resolution").</summary>
    public const int MaxSide = 2048;

    /// <summary>Größte Polsterung je Seite in Pixeln (ein riesiges <c>glow_radius</c> soll kein riesiges Bild ergeben; Hinweis "resolution").</summary>
    public const int MaxPadding = 128;

    /// <summary>Breite des Bilds ohne Polsterung: <c>ceil(BoxWidth × PixelsPerUnit)</c>, höchstens <see cref="MaxSide"/>.</summary>
    public int PixelWidth => Pixels(BoxWidth * PixelsPerUnit);

    /// <summary>Höhe des Bilds ohne Polsterung: <c>ceil(BoxHeight × PixelsPerUnit)</c>, höchstens <see cref="MaxSide"/>.</summary>
    public int PixelHeight => Pixels(BoxHeight * PixelsPerUnit);

    /// <summary>
    /// Radius des Leuchtens in Pixeln: <c>max(0, glow_radius − </c><see cref="JominiEffectRules.GlowRadiusOffset"/><c>) × </c>
    /// <see cref="JominiEffectRules.GlowRadiusScale"/><c> × PixelsPerUnit</c>; 0 ohne Leuchten. Der Kastenfilter-Radius ist davon
    /// <see cref="JominiEffectComposer.BlurRadius"/>.
    /// </summary>
    public double GlowRadiusPixels => Effects.Glow is { } glow ? GlowRadius(glow, PixelsPerUnit) : 0;

    /// <summary>Die Dateien der Bestandteile: Grundbild und Ebenen (<see cref="JominiSpriteVisual.TextureFullPath"/>), ohne Doppelte.</summary>
    public IReadOnlyList<string> ComponentPaths =>
        new[] { Base.TextureFullPath }
            .Concat(Effects.Layers.Select(layer => layer.Sprite.TextureFullPath))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Schlüssel für einen Bild-Cache, unabhängig von der Kultur: alle Texturpfade, alle Werte der Ebenen, Kastengröße, <see cref="RenderScale"/>,
    /// <see cref="PixelsPerUnit"/>, <see cref="PaddingPixels"/> und die Effekt-Regeln. Ohne die Deckkraft des Grundbilds und, wenn der Zeichner das
    /// ganze Bild spiegelt, ohne seine Spiegelung. Wird bei jedem Abruf neu gebaut (die Regeln können sich zwischen zwei Abrufen ändern; ein
    /// <c>with</c>-Kopie hat so nie einen alten Schlüssel).
    /// </summary>
    public string CacheKey
    {
        get
        {
            var key = new StringBuilder(256);
            Append(key, "box", BoxWidth, BoxHeight, RenderScale, PixelsPerUnit, PaddingPixels);
            var (wr, wg, wb) = JominiEffectRules.GrayscaleWeights;
            key.Append("|rules:")
                .Append(JominiEffectRules.AlphaMultiplyChannel).Append(',').Append(JominiEffectRules.MaskChannel).Append(',')
                .Append(JominiEffectRules.ElementMirrorFlipsLayers).Append(',').Append(JominiEffectRules.TintAfterLayers);
            Append(key, "", wr, wg, wb, JominiEffectRules.GlowRadiusScale, JominiEffectRules.GlowRadiusOffset);
            AppendSprite(key, "base", Base, mirror: !JominiEffectRules.ElementMirrorFlipsLayers, opacity: false);
            foreach (var layer in Effects.Layers)
            {
                AppendSprite(key, "layer:" + layer.Mode, layer.Sprite, mirror: true, opacity: true);
            }

            if (Effects.Glow is { } glow)
            {
                Append(key, "glow", glow.Color.R, glow.Color.G, glow.Color.B, glow.Color.A, glow.Radius, glow.Alpha);
            }

            key.Append("|fx:").Append(Effects.ColorEffect);
            return key.ToString();
        }
    }

    /// <summary>
    /// Rezept für einen Kasten. Pixel je GUI-Einheit: die größte <see cref="JominiSpriteVisual.Density"/> von Grundbild und Ebenen, begrenzt auf 1–4; wäre eine
    /// Seite größer als <see cref="MaxSide"/>, wird es kleiner (auch unter 1), mit dem Hinweis <see cref="GuiTexts.LayoutEffectApproximated"/>("resolution").
    /// Polsterung: der Kastenfilter-Radius des Leuchtens (<see cref="JominiEffectComposer.BlurRadius"/> von <see cref="GlowRadiusPixels"/>; so weit reicht ein
    /// Durchgang über den Rand), höchstens <see cref="MaxPadding"/> (ebenfalls mit dem Hinweis). Ist dieser Radius bei den gewählten Pixeln je Einheit 0, fällt das
    /// Leuchten aus <see cref="Effects"/> weg (es zeichnete nichts); bleibt dann nichts, ist <see cref="JominiEffectVisual.IsEmpty"/> wahr und der Aufrufer
    /// setzt kein Rezept. Ungültige Kastenmaße zählen als 0.
    /// </summary>
    public static JominiEffectRecipe Create(JominiSpriteVisual sprite, JominiEffectVisual effects, double boxWidth, double boxHeight, double renderScale, List<string> notes)
    {
        boxWidth = double.IsFinite(boxWidth) && boxWidth > 0 ? boxWidth : 0;
        boxHeight = double.IsFinite(boxHeight) && boxHeight > 0 ? boxHeight : 0;
        renderScale = double.IsFinite(renderScale) && renderScale > 0 ? renderScale : 1;

        var density = effects.Layers.Select(layer => layer.Sprite.Density).Append(sprite.Density).Where(double.IsFinite).DefaultIfEmpty(1).Max();
        var ppu = Math.Clamp(density, 1, 4);
        var side = Math.Max(boxWidth, boxHeight);
        var approximated = false;
        if (Exact(side * ppu) > MaxSide)
        {
            ppu = MaxSide / side;
            approximated = true;
        }

        var padding = 0;
        if (effects.Glow is { } glow)
        {
            padding = JominiEffectComposer.BlurRadius(GlowRadius(glow, ppu));
            if (padding == 0)
            {
                // Kastenfilter-Radius 0 bei diesen Pixeln je Einheit: das Leuchten zeichnet nichts (16r), es fällt weg.
                effects = effects with { Glow = null };
            }
            else if (padding > MaxPadding)
            {
                padding = MaxPadding;
                approximated = true;
            }
        }

        if (approximated)
        {
            var note = GuiTexts.LayoutEffectApproximated("resolution");
            if (!notes.Contains(note))
            {
                notes.Add(note);
            }
        }

        return new JominiEffectRecipe(sprite, effects, boxWidth, boxHeight, renderScale, ppu, padding);
    }

    private static double GlowRadius(JominiGlow glow, double ppu) =>
        Math.Max(0, glow.Radius - JominiEffectRules.GlowRadiusOffset) * JominiEffectRules.GlowRadiusScale * ppu;

    /// <summary>Gleitkommarauschen (11,000000000000002) soll keinen Pixel mehr ergeben.</summary>
    private static double Exact(double value) => Math.Ceiling(Math.Round(value, 6));

    private static int Pixels(double value) => double.IsFinite(value) && value > 0 ? (int)Math.Min(MaxSide, Exact(value)) : 0;

    private static void AppendSprite(StringBuilder key, string label, JominiSpriteVisual sprite, bool mirror, bool opacity)
    {
        key.Append('|').Append(label).Append(':').Append(sprite.TextureFullPath ?? sprite.Texture).Append(',')
            .Append(sprite.TextureWidth.ToString(CultureInfo.InvariantCulture)).Append('x').Append(sprite.TextureHeight.ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(sprite.Kind).Append(',').Append(sprite.Fit);
        Append(key, "", sprite.Source.X, sprite.Source.Y, sprite.Source.Width, sprite.Source.Height, sprite.Density);
        Append(key, "", sprite.Border.Left, sprite.Border.Top, sprite.Border.Right, sprite.Border.Bottom);
        Append(key, "", sprite.Tint.R, sprite.Tint.G, sprite.Tint.B, sprite.Tint.A);
        if (mirror)
        {
            key.Append(",m").Append(sprite.MirrorHorizontal ? 'h' : '-').Append(sprite.MirrorVertical ? 'v' : '-');
        }

        if (opacity)
        {
            Append(key, "o", sprite.Opacity);
        }
    }

    private static void Append(StringBuilder key, string label, params double[] values)
    {
        key.Append('|').Append(label);
        foreach (var value in values)
        {
            key.Append(';').Append(value.ToString("R", CultureInfo.InvariantCulture));
        }
    }
}

/// <summary>
/// Setzt das Bild eines Kastens mit Effekten auf der CPU zusammen (nur auf Arbeits-Threads aufrufen; bis 2048 Pixel je Seite). Reihenfolge
/// (gemessen, Lauf 7):
/// <list type="number">
/// <item>Grundbild ungefärbt (<see cref="JominiSpriteRaster.Draw"/>), gespiegelt nur, wenn der Zeichner nicht das ganze Bild spiegelt
/// (<see cref="JominiEffectRules.ElementMirrorFlipsLayers"/>);</item>
/// <item>jede Ebene mit ihrer eigenen Farbe und Spiegelung in ein gleich großes Bild, dann je Pixel <see cref="JominiBlend.Apply"/> (16k);</item>
/// <item>die Farbe des Elements auf das Ergebnis (<see cref="JominiEffectRules.TintAfterLayers"/>, 16l; sonst schon im ersten Schritt);</item>
/// <item>Graustufen (<see cref="JominiColorEffect.Grayscale"/>);</item>
/// <item>Leuchten hinter dem Ergebnis, in der Polsterung (ein Kastenfilter-Durchgang, 16r–16t; <see cref="BlurRadius"/>).</item>
/// </list>
/// Ergebnis mit geradem Alpha. Ohne Zustand.
/// </summary>
public static class JominiEffectComposer
{
    private const double ToUnit = 1.0 / 255;

    /// <summary>
    /// Das Bild, <see cref="JominiEffectRecipe.PixelWidth"/> + 2 · Polsterung mal <see cref="JominiEffectRecipe.PixelHeight"/> + 2 · Polsterung. Lädt
    /// <paramref name="load"/> das Grundbild nicht, ist es durchsichtig; eine Ebene, die nicht lädt, fällt weg.
    /// </summary>
    /// <param name="load">Dekodierte Textur zu einem Pfad aus <see cref="JominiEffectRecipe.ComponentPaths"/> (weiß, ungefärbt) oder <see langword="null"/>.</param>
    public static GuiImage Compose(JominiEffectRecipe recipe, Func<string, GuiImage?> load)
    {
        var pad = Math.Max(0, recipe.PaddingPixels);

        // Das ungepolsterte Bild (und das der Ebenen) ist nach dem Polstern nicht mehr erreichbar und kann vor den Puffern des Leuchtens frei werden.
        var padded = Pad(ComposeUnpadded(recipe, load, out var drawn), pad);

        // Ist die Polsterung gedeckelt (MaxPadding), wird auch der Radius gedeckelt, damit das Leuchten im Bild ausläuft.
        var blur = Math.Min(BlurRadius(recipe.GlowRadiusPixels), pad);
        if (drawn && recipe.Effects.Glow is { } glow && blur > 0)
        {
            AddGlow(padded, glow, blur);
        }

        return padded;
    }

    /// <summary>Grundbild, Ebenen, Farbe des Elements und Graustufen ohne Polsterung; <paramref name="drawn"/> ist falsch, wenn das Grundbild nicht lädt.</summary>
    private static GuiImage ComposeUnpadded(JominiEffectRecipe recipe, Func<string, GuiImage?> load, out bool drawn)
    {
        var width = recipe.PixelWidth;
        var height = recipe.PixelHeight;
        var image = new GuiImage(width, height, new byte[width * height * 4]);
        drawn = false;
        if (recipe.Base.TextureFullPath is not { } basePath || load(basePath) is not { } baseTexture)
        {
            return image;
        }

        drawn = true;

        var elementTint = JominiSpriteRaster.TintOf(recipe.Base);
        JominiSpriteRaster.Draw(
            image, baseTexture, recipe.Base, recipe.PixelsPerUnit, recipe.RenderScale, applyMirror: !JominiEffectRules.ElementMirrorFlipsLayers,
            JominiEffectRules.TintAfterLayers ? GuiTint.White : elementTint);

        GuiImage? layerImage = null;
        foreach (var layer in recipe.Effects.Layers)
        {
            if (layer.Sprite.TextureFullPath is not { } path || load(path) is not { } texture)
            {
                continue;
            }

            layerImage ??= new GuiImage(width, height, new byte[image.Bgra.Length]);
            Array.Clear(layerImage.Bgra);
            JominiSpriteRaster.Draw(layerImage, texture, layer.Sprite, recipe.PixelsPerUnit, recipe.RenderScale, applyMirror: true, JominiSpriteRaster.TintOf(layer.Sprite));
            Blend(image.Bgra, layerImage.Bgra, layer.Mode, Math.Clamp(layer.Sprite.Opacity, 0, 1));
        }

        if (JominiEffectRules.TintAfterLayers)
        {
            GuiImageOps.MultiplyInPlace(image.Bgra, elementTint);
        }

        if (recipe.Effects.ColorEffect == JominiColorEffect.Grayscale)
        {
            JominiBlend.Grayscale(image.Bgra);
        }

        return image;
    }

    /// <summary>
    /// Radius des Kastenfilters fürs Leuchten in Pixeln aus dem Leuchtradius in Pixeln (<see cref="JominiEffectRecipe.GlowRadiusPixels"/>): gerundet, 0 heißt
    /// kein Leuchten. Gemessen, Lauf 7, 16s: <c>glow_radius = 8</c> bei Dichte 1 gibt Radius 6 (Fenster 13 Pixel; gerechnet 0,46 0,38 0,31 0,23 0,15 0,08 0
    /// gegen gemessen 0,48 0,40 0,31 0,23 0,14 0,06 0); 16r: <c>glow_radius = 3</c> gibt 0 (gemessen 0,02 an der Kante).
    /// </summary>
    public static int BlurRadius(double radiusPixels) =>
        double.IsFinite(radiusPixels) && radiusPixels > 0
            ? (int)Math.Min(JominiEffectRecipe.MaxPadding + 1, Math.Round(Math.Round(radiusPixels, 6), MidpointRounding.AwayFromZero))
            : 0;

    /// <summary>
    /// Anzahl der Weichzeichner-Durchgänge des Leuchtens. Gemessen, Lauf 7, 16s: das Profil fällt linear und endet scharf, wie nach einem einzigen Kastenfilter
    /// (mehrere Durchgänge gäben einen weichen, S-förmigen Abfall).
    /// </summary>
    private const int BlurPasses = 1;

    private static void Blend(byte[] target, byte[] layer, JominiBlendMode mode, double opacity)
    {
        var colourMode = mode is not (JominiBlendMode.AlphaMultiply or JominiBlendMode.Mask);
        for (var i = 0; i + 3 < target.Length; i += 4)
        {
            // Farbarten ohne Deckung der Ebene ändern nichts (spart die Rechnung außerhalb der Ebenen-Stücke).
            if (colourMode && layer[i + 3] == 0)
            {
                continue;
            }

            var (r, g, b, a) = JominiBlend.Apply(
                mode,
                (target[i + 2] * ToUnit, target[i + 1] * ToUnit, target[i] * ToUnit, target[i + 3] * ToUnit),
                (layer[i + 2] * ToUnit, layer[i + 1] * ToUnit, layer[i] * ToUnit, layer[i + 3] * ToUnit),
                opacity);
            target[i] = ToByte(b * 255);
            target[i + 1] = ToByte(g * 255);
            target[i + 2] = ToByte(r * 255);
            target[i + 3] = ToByte(a * 255);
        }
    }

    private static GuiImage Pad(GuiImage image, int pad)
    {
        if (pad == 0)
        {
            return image;
        }

        var width = image.Width + 2 * pad;
        var padded = new GuiImage(width, image.Height + 2 * pad, new byte[width * (image.Height + 2 * pad) * 4]);
        for (var y = 0; y < image.Height; y++)
        {
            Array.Copy(image.Bgra, y * image.Width * 4, padded.Bgra, ((y + pad) * width + pad) * 4, image.Width * 4);
        }

        return padded;
    }

    /// <summary>
    /// Leuchten: das Alpha des Bilds <see cref="BlurPasses"/>-mal mit einem Kastenfilter weichgezeichnet (außerhalb zählt 0), mal <c>alpha</c> des Leuchtens
    /// und Alpha seiner Farbe, in der Farbe des Leuchtens; das Bild liegt darüber (Porter-Duff "over", gerades Alpha). Puffer in <c>float</c> (2 × 4 Byte je
    /// Pixel, bei 2304² rund 42 MB).
    /// </summary>
    /// <remarks>
    /// Abweichung (gemessen, Lauf 7, 16s): Im Spiel wird das Leuchten zu den Ecken hin über die ganze Kantenlänge schwächer (direkt an der Kante 0,49 in
    /// der Mitte, 0,20 eine Einheit vor der Ecke, 0,11 auf der Diagonale); der trennbare Kastenfilter bleibt bis 6 Einheiten vor der Ecke bei 0,46 und
    /// gibt an der Ecke 0,21. Die Mittellinie stimmt.
    /// </remarks>
    private static void AddGlow(GuiImage image, JominiGlow glow, int radius)
    {
        var width = image.Width;
        var height = image.Height;
        if (width == 0 || height == 0)
        {
            return;
        }

        var bgra = image.Bgra;
        var alpha = new float[width * height];
        for (var i = 0; i < alpha.Length; i++)
        {
            alpha[i] = bgra[i * 4 + 3] / 255f;
        }

        var scratch = new float[alpha.Length];
        for (var pass = 0; pass < BlurPasses; pass++)
        {
            BoxBlur(alpha, scratch, width, height, radius, horizontal: true);
            BoxBlur(scratch, alpha, width, height, radius, horizontal: false);
        }

        var strength = Unit(glow.Alpha) * Unit(glow.Color.A);
        var (gr, gg, gb) = (Unit(glow.Color.R), Unit(glow.Color.G), Unit(glow.Color.B));
        for (var i = 0; i < alpha.Length; i++)
        {
            var at = i * 4;
            var sa = bgra[at + 3] * ToUnit;
            var ga = Math.Clamp(alpha[i] * strength, 0, 1) * (1 - sa);
            var oa = sa + ga;
            if (oa <= 0)
            {
                continue;
            }

            bgra[at] = ToByte((bgra[at] * sa + gb * 255 * ga) / oa);
            bgra[at + 1] = ToByte((bgra[at + 1] * sa + gg * 255 * ga) / oa);
            bgra[at + 2] = ToByte((bgra[at + 2] * sa + gr * 255 * ga) / oa);
            bgra[at + 3] = ToByte(oa * 255);
        }
    }

    /// <summary>Kastenfilter mit Fenster 2 · <paramref name="radius"/> + 1 längs einer Achse, laufende Summe; außerhalb des Bilds zählt 0.</summary>
    private static void BoxBlur(float[] source, float[] target, int width, int height, int radius, bool horizontal)
    {
        var lines = horizontal ? height : width;
        var length = horizontal ? width : height;
        var step = horizontal ? 1 : width;
        var window = 2.0 * radius + 1;
        for (var line = 0; line < lines; line++)
        {
            var start = horizontal ? line * width : line;
            var sum = 0.0;   // laufende Summe in double: kein Drift über lange Zeilen
            for (var k = 0; k <= Math.Min(radius, length - 1); k++)
            {
                sum += source[start + k * step];
            }

            for (var p = 0; p < length; p++)
            {
                target[start + p * step] = (float)(sum / window);
                var enter = p + radius + 1;
                if (enter < length)
                {
                    sum += source[start + enter * step];
                }

                var leave = p - radius;
                if (leave >= 0)
                {
                    sum -= source[start + leave * step];
                }
            }
        }
    }

    private static double Unit(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1;

    private static byte ToByte(double value) => value <= 0 ? (byte)0 : value >= 255 ? (byte)255 : (byte)Math.Round(value, MidpointRounding.AwayFromZero);
}
