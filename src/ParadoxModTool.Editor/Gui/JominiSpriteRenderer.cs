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
/// Kästen mit Effekten zeigen das Effektbild aus <see cref="GuiImageCache.TryGetComposite"/>, bis es fertig ist das schlichte Sprite.
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
    /// <param name="effects">Effekte des Kastens (<see cref="LayoutBox.Effects"/>; dessen Grundbild ist <paramref name="sprite"/>): ist das Effektbild fertig
    /// (<see cref="GuiImageCache.TryGetComposite"/>), wird es statt des schlichten Sprites gezeichnet.</param>
    internal static bool Draw(
        DrawingContext dc, JominiSpriteVisual sprite, Rect screenRect, double zoom, GuiImageCache cache, double pixelsPerDip, double renderScale = 1,
        JominiEffectRecipe? effects = null)
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

        // Mit Effekten: das fertige Effektbild; steht es noch aus, ist verdrängt oder unlesbar, das schlichte Sprite wie ohne Effekte (nie der
        // schematische Kasten, wenn das schlichte Bild schon da ist).
        if (effects is not null && cache.TryGetComposite(effects, out var composite) == GuiImageState.Ready && composite is not null)
        {
            DrawComposite(dc, sprite, composite, effects, screenRect, zoom);
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

        var pushed = PushOpacityAndMirror(dc, sprite, screenRect, mirror: true);
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

    /// <summary>
    /// Zeichnet das fertige Effektbild in den Kasten, um die Polsterung (<see cref="JominiEffectRecipe.PaddingPixels"/> / Pixel je Einheit × Zoom) je Seite
    /// vergrößert. Die Färbung steckt schon im Bild; die Deckkraft des Sprites kommt hier dazu, die Spiegelung nur, wenn sie das ganze Bild dreht
    /// (<see cref="JominiEffectRules.ElementMirrorFlipsLayers"/>; sonst hat das Zusammensetzen das Grundbild schon gespiegelt).
    /// </summary>
    private static void DrawComposite(DrawingContext dc, JominiSpriteVisual sprite, BitmapSource composite, JominiEffectRecipe effects, Rect screenRect, double zoom)
    {
        var inflate = effects.PixelsPerUnit > 0 ? effects.PaddingPixels / effects.PixelsPerUnit * zoom : 0;
        var target = screenRect;
        if (double.IsFinite(inflate) && inflate > 0)
        {
            target.Inflate(inflate, inflate);
        }

        var pushed = PushOpacityAndMirror(dc, sprite, screenRect, mirror: JominiEffectRules.ElementMirrorFlipsLayers);
        dc.DrawImage(composite, target);
        for (var i = 0; i < pushed; i++)
        {
            dc.Pop();
        }
    }

    /// <summary>Legt die Deckkraft des Sprites und (mit <paramref name="mirror"/>) seine Spiegelung um die Kastenmitte an; liefert die Zahl der Ebenen zum Entfernen.</summary>
    private static int PushOpacityAndMirror(DrawingContext dc, JominiSpriteVisual sprite, Rect screenRect, bool mirror)
    {
        var opacity = double.IsFinite(sprite.Opacity) ? Math.Clamp(sprite.Opacity, 0, 1) : 1;
        var pushed = 0;
        if (opacity < 1)
        {
            dc.PushOpacity(opacity);
            pushed++;
        }

        if (mirror && (sprite.MirrorHorizontal || sprite.MirrorVertical))
        {
            // Der ganze Kasten wird um seine Mitte gespiegelt, Ränder und Kacheln mit (gemessen, Lauf 5, Fälle 13i–13k).
            var transform = new ScaleTransform(
                sprite.MirrorHorizontal ? -1 : 1,
                sprite.MirrorVertical ? -1 : 1,
                screenRect.X + screenRect.Width / 2,
                screenRect.Y + screenRect.Height / 2);
            transform.Freeze();
            dc.PushTransform(transform);
            pushed++;
        }

        return pushed;
    }

    private static void DrawPiece(DrawingContext dc, BitmapSource bitmap, JominiSpritePiece piece, Rect screenRect, double zoom)
    {
        var target = new Rect(
            screenRect.X + piece.Target.X * zoom,
            screenRect.Y + piece.Target.Y * zoom,
            piece.Target.Width * zoom,
            piece.Target.Height * zoom);
        if (!(target.Width > 0) || !(target.Height > 0) || JominiSpriteGeometry.ImageRect(piece.Source) is not { } source
            || ClassicVisualRenderer.Crop(bitmap, new Int32Rect(source.X, source.Y, source.Width, source.Height)) is not { } image)
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

    /// <summary>Färbung der Textur (<see cref="JominiSpriteRaster.TintOf"/>, dieselbe Regel wie beim Zusammensetzen von Effekten).</summary>
    internal static GuiTint TintOf(JominiSpriteVisual sprite) => JominiSpriteRaster.TintOf(sprite);

    private static void DrawPlaceholder(DrawingContext dc, JominiSpriteVisual sprite, Rect screenRect, double pixelsPerDip)
    {
        var label = sprite.PlaceholderLabel is { Length: > 0 } text ? text : sprite.Texture;
        ClassicVisualRenderer.DrawPlaceholder(dc, screenRect, label, 10, pixelsPerDip);
    }
}
