namespace ParadoxModTool.GuiDesign.Imaging;

/// <summary>Farbfaktoren für Rot, Grün und Blau, mit denen die Pixel einer Textur multipliziert werden (Weiß = unverändert; über 1 hellt auf).</summary>
public readonly record struct GuiTint(double R, double G, double B)
{
    /// <summary>Keine Färbung.</summary>
    public static GuiTint White => new(1, 1, 1);

    public bool IsWhite => R == 1 && G == 1 && B == 1;
}

/// <summary>Pixelarbeit an BGRA-Puffern.</summary>
public static class GuiImageOps
{
    /// <summary>
    /// Multipliziert Blau, Grün und Rot jedes Pixels mit dem Faktor der Färbung (je <c>min(255, round(Wert × Faktor))</c>), Alpha bleibt.
    /// Negative und ungültige Faktoren zählen als 0.
    /// </summary>
    public static void MultiplyInPlace(Span<byte> bgra, GuiTint tint)
    {
        if (tint.IsWhite)
        {
            return;
        }

        // Je Kanal eine Tabelle mit 256 Einträgen: bei großen Bildern viel schneller als je Pixel zu rechnen.
        Span<byte> blue = stackalloc byte[256];
        Span<byte> green = stackalloc byte[256];
        Span<byte> red = stackalloc byte[256];
        Fill(blue, tint.B);
        Fill(green, tint.G);
        Fill(red, tint.R);

        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            bgra[i] = blue[bgra[i]];
            bgra[i + 1] = green[bgra[i + 1]];
            bgra[i + 2] = red[bgra[i + 2]];
        }
    }

    private static void Fill(Span<byte> table, double factor)
    {
        if (double.IsNaN(factor) || factor < 0)
        {
            factor = 0;
        }

        for (var value = 0; value < 256; value++)
        {
            var scaled = Math.Round(value * factor, MidpointRounding.AwayFromZero);
            table[value] = double.IsNaN(scaled) ? (byte)0 : scaled >= 255 ? (byte)255 : (byte)scaled;
        }
    }
}
