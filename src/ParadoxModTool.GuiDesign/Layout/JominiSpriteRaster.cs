using ParadoxModTool.GuiDesign.Imaging;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Zeichnet ein <see cref="JominiSpriteVisual"/> auf der CPU in ein kastengroßes Pixelbild, so wie der WPF-Zeichner es auf den Bildschirm legt
/// (Stücke aus <see cref="JominiSpriteGeometry"/>, Ausschnitt in ganzen Texturpixeln, Kacheln ab der Ecke links oben des Stücks, Spiegeln um die Kastenmitte).
/// Grundlage für das Zusammensetzen von <c>modify_texture</c>-Ebenen. Dekodiert nichts und hat keinen Zustand.
/// </summary>
public static class JominiSpriteRaster
{
    /// <summary>
    /// Zeichnet das Sprite in <paramref name="target"/> (Größe ÷ <paramref name="pixelsPerUnit"/> = Kasten in GUI-Einheiten) und ersetzt dabei die Pixel
    /// der Stücke; das Ziel beginnt durchsichtig. Bilinear abgetastet an den Pixelmitten, an den Rändern des Ausschnitts begrenzt (kein Einbluten aus
    /// Nachbarbildern), Kacheln wickeln innerhalb ihres Ausschnitts um. Die Färbung <paramref name="tint"/> multipliziert Rot, Grün und Blau am Ende
    /// wie <see cref="GuiImageOps.MultiplyInPlace"/>; die Deckkraft des Sprites gehört dem Aufrufer.
    /// </summary>
    /// <param name="pixelsPerUnit">Zielpixel je GUI-Einheit.</param>
    /// <param name="renderScale"><see cref="LayoutBox.RenderScale"/> des Kastens, geht an <see cref="JominiSpriteGeometry.Pieces"/>.</param>
    /// <param name="applyMirror">Spiegelt den ganzen Kasten um seine Mitte, wenn das Sprite es verlangt.</param>
    public static void Draw(GuiImage target, GuiImage texture, JominiSpriteVisual sprite, double pixelsPerUnit, double renderScale, bool applyMirror, GuiTint tint)
    {
        if (!double.IsFinite(pixelsPerUnit) || pixelsPerUnit <= 0 || target.Width <= 0 || target.Height <= 0
            || target.Bgra.Length < target.Width * target.Height * 4 || texture.Bgra.Length < texture.Width * texture.Height * 4)
        {
            return;
        }

        var box = new LayoutRect(0, 0, target.Width / pixelsPerUnit, target.Height / pixelsPerUnit);
        var pieces = JominiSpriteGeometry.Pieces(sprite, box, null, renderScale);
        var flipX = applyMirror && sprite.MirrorHorizontal;
        var flipY = applyMirror && sprite.MirrorVertical;

        foreach (var piece in pieces)
        {
            DrawPiece(target, texture, piece, pixelsPerUnit, flipX, flipY);
        }

        GuiImageOps.MultiplyInPlace(target.Bgra, tint);
    }

    private static void DrawPiece(GuiImage target, GuiImage texture, JominiSpritePiece piece, double ppu, bool flipX, bool flipY)
    {
        if (JominiSpriteGeometry.ImageRect(piece.Source) is not { } rect)
        {
            return;
        }

        // Ausschnitt auf die Textur begrenzen (Indizes werden beim Abtasten geklemmt).
        var minX = Math.Max(0, rect.X);
        var minY = Math.Max(0, rect.Y);
        var maxX = Math.Min(texture.Width, rect.X + rect.Width) - 1;
        var maxY = Math.Min(texture.Height, rect.Y + rect.Height) - 1;
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        var left = piece.Target.X * ppu;
        var top = piece.Target.Y * ppu;
        var width = piece.Target.Width * ppu;
        var height = piece.Target.Height * ppu;
        if (!(width > 0) || !(height > 0))
        {
            return;
        }

        // Pixel, deren Mitte im Ziel liegt (benachbarte Stücke lassen so weder Lücken noch Überlappung).
        var x0 = Math.Max(0, (int)Math.Ceiling(left - 0.5));
        var x1 = Math.Min(target.Width, (int)Math.Ceiling(left + width - 0.5));
        var y0 = Math.Max(0, (int)Math.Ceiling(top - 0.5));
        var y1 = Math.Min(target.Height, (int)Math.Ceiling(top + height - 0.5));
        var tiled = piece.TileWidth > 0 && piece.TileHeight > 0;
        var tileW = piece.TileWidth * ppu;
        var tileH = piece.TileHeight * ppu;

        for (var py = y0; py < y1; py++)
        {
            var v = tiled ? Wrap(py + 0.5 - top, tileH) / tileH * rect.Height : (py + 0.5 - top) / height * rect.Height;
            var sy = v - 0.5;
            var dy = flipY ? target.Height - 1 - py : py;
            for (var px = x0; px < x1; px++)
            {
                var u = tiled ? Wrap(px + 0.5 - left, tileW) / tileW * rect.Width : (px + 0.5 - left) / width * rect.Width;
                var sx = u - 0.5;
                var dx = flipX ? target.Width - 1 - px : px;
                Sample(texture, rect, minX, minY, maxX, maxY, tiled, sx, sy, target.Bgra, (dy * target.Width + dx) * 4);
            }
        }
    }

    private static double Wrap(double value, double period)
    {
        var result = value % period;
        return result < 0 ? result + period : result;
    }

    /// <summary>Bilineare Abtastung bei (<paramref name="sx"/>, <paramref name="sy"/>) in Texeln des Ausschnitts; Farben mit Alpha gewichtet.</summary>
    private static void Sample(GuiImage texture, PixelRect rect, int minX, int minY, int maxX, int maxY, bool wrap, double sx, double sy, byte[] output, int offset)
    {
        // Gleitkommarauschen (2,9999999999) darf kein Nachbartexel werden: auf 1e-9 runden, bevor der Bruchteil entsteht.
        sx = Math.Round(sx, 9);
        sy = Math.Round(sy, 9);
        var fx0 = Math.Floor(sx);
        var fy0 = Math.Floor(sy);
        var wx = sx - fx0;
        var wy = sy - fy0;
        var ix0 = Index((int)fx0, rect.X, rect.Width, minX, maxX, wrap);
        var ix1 = Index((int)fx0 + 1, rect.X, rect.Width, minX, maxX, wrap);
        var iy0 = Index((int)fy0, rect.Y, rect.Height, minY, maxY, wrap);
        var iy1 = Index((int)fy0 + 1, rect.Y, rect.Height, minY, maxY, wrap);
        var bgra = texture.Bgra;
        var stride = texture.Width * 4;

        if (wx < 1e-9 && wy < 1e-9)
        {
            Array.Copy(bgra, iy0 * stride + ix0 * 4, output, offset, 4);
            return;
        }

        double sumB = 0, sumG = 0, sumR = 0, sumA = 0;
        Accumulate(bgra, iy0 * stride + ix0 * 4, (1 - wx) * (1 - wy), ref sumB, ref sumG, ref sumR, ref sumA);
        Accumulate(bgra, iy0 * stride + ix1 * 4, wx * (1 - wy), ref sumB, ref sumG, ref sumR, ref sumA);
        Accumulate(bgra, iy1 * stride + ix0 * 4, (1 - wx) * wy, ref sumB, ref sumG, ref sumR, ref sumA);
        Accumulate(bgra, iy1 * stride + ix1 * 4, wx * wy, ref sumB, ref sumG, ref sumR, ref sumA);

        if (sumA <= 0)
        {
            output[offset] = output[offset + 1] = output[offset + 2] = output[offset + 3] = 0;
            return;
        }

        output[offset] = ToByte(sumB / sumA);
        output[offset + 1] = ToByte(sumG / sumA);
        output[offset + 2] = ToByte(sumR / sumA);
        output[offset + 3] = ToByte(sumA);
    }

    private static void Accumulate(byte[] bgra, int at, double weight, ref double b, ref double g, ref double r, ref double a)
    {
        if (weight <= 0)
        {
            return;
        }

        var alpha = bgra[at + 3] * weight;
        b += bgra[at] * alpha;
        g += bgra[at + 1] * alpha;
        r += bgra[at + 2] * alpha;
        a += alpha;
    }

    /// <summary>Texelindex: in Kacheln um den Ausschnitt gewickelt, sonst an seine Ränder begrenzt; immer innerhalb der Textur.</summary>
    private static int Index(int relative, int start, int size, int min, int max, bool wrap)
    {
        var index = wrap ? start + (((relative % size) + size) % size) : start + relative;
        return Math.Clamp(index, min, max);
    }

    private static byte ToByte(double value) => value <= 0 ? (byte)0 : value >= 255 ? (byte)255 : (byte)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Färbung der Textur aus <see cref="JominiSpriteVisual.Tint"/>; die Deckkraft liegt ganz in <see cref="JominiSpriteVisual.Opacity"/> (der Alphakanal der
    /// Färbung zählt nicht). Ungültige Kanäle zählen als 1, negative als 0. Für den WPF-Zeichner und das Zusammensetzen von Effekten.
    /// </summary>
    public static GuiTint TintOf(JominiSpriteVisual sprite) => new(Channel(sprite.Tint.R), Channel(sprite.Tint.G), Channel(sprite.Tint.B));

    private static double Channel(double value) => double.IsFinite(value) ? Math.Max(0, value) : 1;
}
