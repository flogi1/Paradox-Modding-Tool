namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Die Mischformeln der <c>modify_texture</c>-Ebenen je Pixel und der Graustufen-Effekt. Kanäle 0–1, gerades (nicht vormultipliziertes) Alpha. Die
/// einstellbaren Teile (Maskenkanäle, Graustufen-Gewichte) stehen in <see cref="JominiEffectRules"/>.
/// </summary>
public static class JominiBlend
{
    /// <summary>
    /// Mischt das Ebenen-Pixel <paramref name="l"/> (schon mit der Farbe der Ebene) auf das Grund-Pixel <paramref name="b"/>.
    /// <list type="bullet">
    /// <item>Farbarten: <c>c = b + (f(b, l) − b) · la</c> mit <c>la = l.A × opacity</c>; das Alpha des Grundbilds bleibt.</item>
    /// <item><c>alphamultiply</c> und <c>mask</c>: die Farbe bleibt, <c>α = αb · (1 − o + o · m)</c> mit <c>o</c> = <paramref name="opacity"/> und <c>m</c> dem
    /// Kanal aus <see cref="JominiEffectRules.AlphaMultiplyChannel"/> bzw. <see cref="JominiEffectRules.MaskChannel"/>.</item>
    /// </list>
    /// </summary>
    /// <param name="opacity">Deckkraft der Ebene (<see cref="JominiSpriteVisual.Opacity"/> des Ebenen-Sprites).</param>
    public static (double R, double G, double B, double A) Apply(
        JominiBlendMode mode, (double R, double G, double B, double A) b, (double R, double G, double B, double A) l, double opacity)
    {
        if (mode is JominiBlendMode.AlphaMultiply or JominiBlendMode.Mask)
        {
            var channel = mode == JominiBlendMode.AlphaMultiply ? JominiEffectRules.AlphaMultiplyChannel : JominiEffectRules.MaskChannel;
            var m = channel == JominiAlphaChannel.Luminance ? Luminance(l.R, l.G, l.B) : l.A;
            return (b.R, b.G, b.B, b.A * (1 - opacity + opacity * m));
        }

        var la = l.A * opacity;
        return (Mix(mode, b.R, l.R, la), Mix(mode, b.G, l.G, la), Mix(mode, b.B, l.B, la), b.A);
    }

    /// <summary>Graustufen in einem BGRA-Puffer: <c>Y = wR·R + wG·G + wB·B</c> (<see cref="JominiEffectRules.GrayscaleWeights"/>), dann R = G = B = Y; Alpha bleibt.</summary>
    public static void Grayscale(Span<byte> bgra)
    {
        var (wr, wg, wb) = JominiEffectRules.GrayscaleWeights;
        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            var y = ToByte(wr * bgra[i + 2] + wg * bgra[i + 1] + wb * bgra[i]);
            bgra[i] = bgra[i + 1] = bgra[i + 2] = y;
        }
    }

    /// <summary>Helligkeit mit den Graustufen-Gewichten (für <see cref="JominiAlphaChannel.Luminance"/>).</summary>
    private static double Luminance(double r, double g, double b)
    {
        var (wr, wg, wb) = JominiEffectRules.GrayscaleWeights;
        return wr * r + wg * g + wb * b;
    }

    private static double Mix(JominiBlendMode mode, double b, double l, double la)
    {
        var f = mode switch
        {
            JominiBlendMode.Multiply => b * l,
            JominiBlendMode.Overlay => b < 0.5 ? 2 * b * l : 1 - 2 * (1 - b) * (1 - l),
            JominiBlendMode.Add => Math.Min(1, b + l),
            JominiBlendMode.ColorDodge => l >= 1 ? 1 : Math.Min(1, b / (1 - l)),
            JominiBlendMode.Lighten => Math.Max(b, l),
            JominiBlendMode.Darken => Math.Min(b, l),
            _ => l,
        };
        return b + (f - b) * la;
    }

    private static byte ToByte(double value) => value <= 0 ? (byte)0 : value >= 255 ? (byte)255 : (byte)Math.Round(value, MidpointRounding.AwayFromZero);
}
