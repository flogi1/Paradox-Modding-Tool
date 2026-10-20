using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Zeichnet das Texturbild eines Jomini-Kastens (<see cref="JominiSpriteVisual"/>): gestreckt, in neun Teilen, gekachelt, mit Einpassung, Spiegelung,
/// Färbung und Deckkraft. Die Stücke liefert <see cref="JominiSpriteGeometry"/> (in GUI-Einheiten), der Zeichner rechnet sie mit dem Zoom auf den Bildschirm.
/// Dekodiert nie: das Bild kommt über <see cref="GuiImageCache.TryGet"/>, solange es noch aussteht, zeichnet der Aufrufer den schematischen Kasten.
/// Läuft auf dem UI-Thread.
/// </summary>
internal static class JominiSpriteRenderer
{
    /// <summary>
    /// Zeichnet das Sprite in <paramref name="screenRect"/> (Bildschirmkoordinaten). Liefert <see langword="false"/>, wenn das Bild noch aussteht oder über der
    /// Speichergrenze liegt (der Aufrufer zeichnet dann den schematischen Kasten); sonst <see langword="true"/>, auch bei Platzhaltern und leerem Ausschnitt.
    /// </summary>
    /// <param name="zoom">Bildschirmpixel je GUI-Einheit (Randbreiten und Kachelgrößen behalten so ihre Spielgröße).</param>
    /// <param name="renderScale"><see cref="LayoutBox.RenderScale"/> des Kastens (Jomini <c>scale</c>): Ränder und Kacheln werden damit malgenommen.</param>
    internal static bool Draw(DrawingContext dc, JominiSpriteVisual sprite, Rect screenRect, double zoom, GuiImageCache cache, double pixelsPerDip, double renderScale = 1)
    {
        if (!double.IsFinite(zoom) || zoom <= 0 || !(screenRect.Width > 0) || !(screenRect.Height > 0))
        {
            return false;
        }

        if (sprite.Placeholder != JominiSpritePlaceholder.None || sprite.TextureFullPath is null)
        {
            DrawPlaceholder(dc, sprite, screenRect, pixelsPerDip);
            return true;
        }

        var state = cache.TryGet(sprite.TextureFullPath, TintOf(sprite), out var bitmap);
        switch (state)
        {
            case GuiImageState.Pending:
            case GuiImageState.OverBudget:
                return false;
            case GuiImageState.Unreadable:
                DrawPlaceholder(dc, sprite, screenRect, pixelsPerDip);
                return true;
        }

        if (bitmap is null)
        {
            return false;
        }

        // Geometrie in GUI-Einheiten mit dem Ursprung links oben im Kasten; unten wird sie mit dem Zoom auf den Bildschirm gelegt.
        var pieces = JominiSpriteGeometry.Pieces(sprite, new LayoutRect(0, 0, screenRect.Width / zoom, screenRect.Height / zoom), null, renderScale);
        if (pieces.Count == 0)
        {
            return true;
        }

        var opacity = double.IsFinite(sprite.Opacity) ? Math.Clamp(sprite.Opacity, 0, 1) : 1;
        var pushed = 0;
        if (opacity < 1)
        {
            dc.PushOpacity(opacity);
            pushed++;
        }

        if (sprite.MirrorHorizontal || sprite.MirrorVertical)
        {
            // Der ganze Kasten wird um seine Mitte gespiegelt, Ränder und Kacheln mit (gemessen, Lauf 5, Fälle 13i–13k).
            var mirror = new ScaleTransform(
                sprite.MirrorHorizontal ? -1 : 1,
                sprite.MirrorVertical ? -1 : 1,
                screenRect.X + screenRect.Width / 2,
                screenRect.Y + screenRect.Height / 2);
            mirror.Freeze();
            dc.PushTransform(mirror);
            pushed++;
        }

        foreach (var piece in pieces)
        {
            DrawPiece(dc, bitmap, piece, screenRect, zoom);
        }

        for (var i = 0; i < pushed; i++)
        {
            dc.Pop();
        }

        return true;
    }

    private static void DrawPiece(DrawingContext dc, BitmapSource bitmap, JominiSpritePiece piece, Rect screenRect, double zoom)
    {
        var target = new Rect(
            screenRect.X + piece.Target.X * zoom,
            screenRect.Y + piece.Target.Y * zoom,
            piece.Target.Width * zoom,
            piece.Target.Height * zoom);
        if (!(target.Width > 0) || !(target.Height > 0) || ImageRect(piece.Source) is not { } source || ClassicVisualRenderer.Crop(bitmap, source) is not { } image)
        {
            return;
        }

        if (piece.TileWidth > 0 && piece.TileHeight > 0)
        {
            // Kacheln in Spielgröße × Zoom, beginnend an der Ecke links oben des Stücks.
            var brush = new ImageBrush(image)
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.Fill,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(target.X, target.Y, piece.TileWidth * zoom, piece.TileHeight * zoom),
            };
            brush.Freeze();
            dc.DrawRectangle(brush, null, target);
            return;
        }

        dc.DrawImage(image, target);
    }

    /// <summary>
    /// Ausschnitt in ganzen Texturpixeln (Ränder gerundet, mindestens ein Pixel); <see langword="null"/> bei leerem oder nicht endlichem Ausschnitt.
    /// Gebrochene Ausschnitte entstehen nur bei <c>fittype</c> und Dichten; ein halbes Pixel Abweichung bleibt unsichtbar.
    /// </summary>
    private static Int32Rect? ImageRect(LayoutRect source)
    {
        if (!double.IsFinite(source.X) || !double.IsFinite(source.Y) || !double.IsFinite(source.Width) || !double.IsFinite(source.Height)
            || source.Width <= 0 || source.Height <= 0 || Math.Abs(source.X) > int.MaxValue / 2 || Math.Abs(source.Y) > int.MaxValue / 2
            || source.Width > int.MaxValue / 2 || source.Height > int.MaxValue / 2)
        {
            return null;
        }

        var left = (int)Math.Round(source.X);
        var top = (int)Math.Round(source.Y);
        var right = Math.Max(left + 1, (int)Math.Round(source.X + source.Width));
        var bottom = Math.Max(top + 1, (int)Math.Round(source.Y + source.Height));
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    /// <summary>Färbung der Textur; die Deckkraft liegt ganz in <see cref="JominiSpriteVisual.Opacity"/> (der Alphakanal der Färbung ist immer 1).</summary>
    internal static GuiTint TintOf(JominiSpriteVisual sprite) => new(Channel(sprite.Tint.R), Channel(sprite.Tint.G), Channel(sprite.Tint.B));

    private static double Channel(double value) => double.IsFinite(value) ? Math.Max(0, value) : 1;

    private static void DrawPlaceholder(DrawingContext dc, JominiSpriteVisual sprite, Rect screenRect, double pixelsPerDip)
    {
        var label = sprite.PlaceholderLabel is { Length: > 0 } text ? text : sprite.Texture;
        ClassicVisualRenderer.DrawPlaceholder(dc, screenRect, label, 10, pixelsPerDip);
    }
}
