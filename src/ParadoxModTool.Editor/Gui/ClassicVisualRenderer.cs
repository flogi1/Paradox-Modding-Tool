using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Layout;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Zeichnet ein klassisches Vorschau-Bild (<see cref="GuiVisual"/>) in einen Kasten: Sprites mit Frame-Ausschnitt und 9-Slice, Texte Zeichen für Zeichen
/// aus der Seitentextur der Bitmap-Schrift. Fehlt Bild oder Schrift, zeigt ein schraffierter Platzhalter den Namen bzw. Text. Läuft auf dem UI-Thread.
/// </summary>
public static class ClassicVisualRenderer
{
    /// <summary>Ausschnitte je Bild; sie leben so lange wie das Bild im <see cref="GuiImageCache"/>.</summary>
    private static readonly ConditionalWeakTable<BitmapSource, Dictionary<Int32Rect, BitmapSource>> Crops = new();

    internal static readonly Brush PlaceholderFill = CreateHatch();
    internal static readonly Pen PlaceholderPen = CreatePen(Color.FromRgb(0xC0, 0x20, 0x90), 1);

    /// <param name="screenRect">Kasten in Bildschirmkoordinaten der Vorschau.</param>
    /// <param name="zoom">Bildschirmpixel je Spielpixel (Ecken, Schrift und Kachelung behalten so ihre Spielgröße).</param>
    /// <param name="centreVertically">Text senkrecht im Kasten mitteln (Knopftext); sonst steht er oben.</param>
    public static void Draw(
        DrawingContext dc,
        GuiVisual visual,
        Rect screenRect,
        double zoom,
        GuiImageCache cache,
        ClassicFontLibrary? fonts,
        double pixelsPerDip = 1,
        bool centreVertically = false)
    {
        if (screenRect.Width <= 0 || screenRect.Height <= 0 || zoom <= 0)
        {
            return;
        }

        switch (visual)
        {
            case GuiSpriteVisual sprite:
                DrawSprite(dc, sprite, screenRect, zoom, cache, pixelsPerDip);
                break;
            case GuiTextVisual text:
                DrawText(dc, text, screenRect, zoom, cache, fonts, pixelsPerDip, centreVertically);
                break;
        }
    }

    /// <summary>Zeilen des Texts: getrennt an echtem Zeilenumbruch und an <c>\n</c> (zwei Zeichen) wie im Spiel.</summary>
    internal static string[] Lines(string text) => text.Replace("\\n", "\n").Replace("\r", string.Empty).Split('\n');

    /// <summary>Linker Rand einer Zeile der Breite <paramref name="lineWidth"/> in einer Fläche der Breite <paramref name="width"/> nach <c>format</c>.</summary>
    internal static double AlignedX(string format, double width, double lineWidth) => format switch
    {
        "centre" or "center" => (width - lineWidth) / 2,
        "right" => width - lineWidth,
        _ => 0,
    };

    private static void DrawSprite(DrawingContext dc, GuiSpriteVisual sprite, Rect rect, double zoom, GuiImageCache cache, double pixelsPerDip)
    {
        var bitmap = sprite.TextureFullPath is { } path ? cache.Get(path) : null;
        if (bitmap is null)
        {
            DrawPlaceholder(dc, rect, sprite.SpriteName, 10, pixelsPerDip);
            return;
        }

        // Frame-Streifen: der Frame-te von Frames gleich breiten Streifen nebeneinander.
        var frames = Math.Max(1, sprite.Frames);
        var frame = Math.Clamp(sprite.Frame, 1, frames);
        var left = (int)((long)bitmap.PixelWidth * (frame - 1) / frames);
        var right = (int)((long)bitmap.PixelWidth * frame / frames);
        var source = new Int32Rect(left, 0, right - left, bitmap.PixelHeight);
        if (source.Width <= 0 || source.Height <= 0)
        {
            DrawPlaceholder(dc, rect, sprite.SpriteName, 10, pixelsPerDip);
            return;
        }

        if (sprite.Kind == ClassicSpriteKind.CorneredTile && (sprite.BorderX > 0 || sprite.BorderY > 0))
        {
            DrawNineSlice(dc, bitmap, source, sprite, rect, zoom);
        }
        else
        {
            DrawCrop(dc, bitmap, source, rect);
        }
    }

    /// <summary>9-Slice: Ecken in Spielgröße × Zoom (höchstens die halbe Kastengröße), Kanten gestreckt, Mitte gestreckt oder bei <c>tilingCenter</c> gekachelt.</summary>
    private static void DrawNineSlice(DrawingContext dc, BitmapSource bitmap, Int32Rect source, GuiSpriteVisual sprite, Rect rect, double zoom)
    {
        var bx = Math.Min(Math.Max(0, sprite.BorderX), source.Width / 2);
        var by = Math.Min(Math.Max(0, sprite.BorderY), source.Height / 2);
        var dx = Math.Min(bx * zoom, rect.Width / 2);
        var dy = Math.Min(by * zoom, rect.Height / 2);

        int[] srcX = { source.X, source.X + bx, source.X + source.Width - bx, source.X + source.Width };
        int[] srcY = { source.Y, source.Y + by, source.Y + source.Height - by, source.Y + source.Height };
        double[] dstX = { rect.X, rect.X + dx, rect.Right - dx, rect.Right };
        double[] dstY = { rect.Y, rect.Y + dy, rect.Bottom - dy, rect.Bottom };
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var src = new Int32Rect(srcX[column], srcY[row], srcX[column + 1] - srcX[column], srcY[row + 1] - srcY[row]);
                var dst = new Rect(dstX[column], dstY[row], Math.Max(0, dstX[column + 1] - dstX[column]), Math.Max(0, dstY[row + 1] - dstY[row]));
                if (src.Width <= 0 || src.Height <= 0 || dst.Width <= 0 || dst.Height <= 0)
                {
                    continue;
                }

                if (row == 1 && column == 1 && sprite.TilingCenter && Crop(bitmap, src) is { } center)
                {
                    // Kacheln in Spielgröße × Zoom, beginnend oben links in der Mitte.
                    var tile = new ImageBrush(center)
                    {
                        TileMode = TileMode.Tile,
                        Stretch = Stretch.Fill,
                        ViewportUnits = BrushMappingMode.Absolute,
                        Viewport = new Rect(dst.X, dst.Y, src.Width * zoom, src.Height * zoom),
                    };
                    dc.DrawRectangle(tile, null, dst);
                    continue;
                }

                DrawCrop(dc, bitmap, src, dst);
            }
        }
    }

    private static void DrawText(
        DrawingContext dc,
        GuiTextVisual text,
        Rect rect,
        double zoom,
        GuiImageCache cache,
        ClassicFontLibrary? fonts,
        double pixelsPerDip,
        bool centreVertically)
    {
        // Farbcodes weg, wie beim Messen im Layout (ClassicLayoutEngine); die Zeilen sind dort schon umbrochen.
        var lines = Lines(text.Text).Select(ClassicText.StripColorCodes).ToArray();
        if (lines.All(line => line.Length == 0))
        {
            return;
        }

        var entry = text.FontName is { Length: > 0 } name ? fonts?.Get(name) : null;
        var pages = entry is { } found
            ? found.PageTexturePaths.Select(path => path is null ? null : cache.Get(path)).ToArray()
            : Array.Empty<BitmapSource?>();
        if (entry is not { } font || pages.All(page => page is null))
        {
            DrawPlaceholder(dc, rect, string.Join("\n", lines), Math.Clamp(12 * zoom, 4, 96), pixelsPerDip);
            return;
        }

        var bitmapFont = font.Font;
        // Ausgerichtet im Kasten: die Engine macht Textfelder mit maxWidth so breit wie maxWidth, Knopftexte so breit wie den Knopf.
        var width = rect.Width / zoom;
        var top = centreVertically ? (rect.Height / zoom - lines.Length * bitmapFont.LineHeight) / 2 : 0;
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var x = AlignedX(text.Format, width, bitmapFont.MeasureWidth(line));
            var y = top + lineIndex * bitmapFont.LineHeight;
            for (var i = 0; i < line.Length; i++)
            {
                // Fehlende Zeichen wie beim Messen als "?".
                if ((bitmapFont.Glyph(line[i]) ?? bitmapFont.Glyph('?')) is not { } glyph)
                {
                    continue;
                }

                if (glyph is { Width: > 0, Height: > 0 } && glyph.Page >= 0 && glyph.Page < pages.Length && pages[glyph.Page] is { } page)
                {
                    var dst = new Rect(rect.X + (x + glyph.XOffset) * zoom, rect.Y + (y + glyph.YOffset) * zoom, glyph.Width * zoom, glyph.Height * zoom);
                    DrawCrop(dc, page, new Int32Rect(glyph.X, glyph.Y, glyph.Width, glyph.Height), dst);
                }

                x += glyph.XAdvance;
                if (i + 1 < line.Length)
                {
                    x += bitmapFont.Kerning(line[i], line[i + 1]);
                }
            }
        }
    }

    private static void DrawCrop(DrawingContext dc, BitmapSource bitmap, Int32Rect source, Rect destination)
    {
        if (Crop(bitmap, source) is { } image)
        {
            dc.DrawImage(image, destination);
        }
    }

    /// <summary>Eingefrorener Ausschnitt, am Bildrand beschnitten; <see langword="null"/>, wenn nichts übrig bleibt.</summary>
    internal static BitmapSource? Crop(BitmapSource bitmap, Int32Rect source)
    {
        var x = Math.Clamp(source.X, 0, bitmap.PixelWidth);
        var y = Math.Clamp(source.Y, 0, bitmap.PixelHeight);
        var clipped = new Int32Rect(x, y, Math.Min(source.X + source.Width, bitmap.PixelWidth) - x, Math.Min(source.Y + source.Height, bitmap.PixelHeight) - y);
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return null;
        }

        if (clipped.X == 0 && clipped.Y == 0 && clipped.Width == bitmap.PixelWidth && clipped.Height == bitmap.PixelHeight)
        {
            return bitmap;
        }

        var crops = Crops.GetValue(bitmap, _ => new Dictionary<Int32Rect, BitmapSource>());
        if (!crops.TryGetValue(clipped, out var crop))
        {
            crop = new CroppedBitmap(bitmap, clipped);
            crop.Freeze();
            crops[clipped] = crop;
        }

        return crop;
    }

    /// <summary>Schraffierter Platzhalter in Kastengröße mit Name bzw. Text in einer Systemschrift (fehlendes Bild, fehlende Schrift).</summary>
    internal static void DrawPlaceholder(DrawingContext dc, Rect rect, string label, double emSize, double pixelsPerDip)
    {
        dc.DrawRectangle(PlaceholderFill, PlaceholderPen, rect);
        if (label.Length == 0 || rect.Width < 12 || rect.Height < 8)
        {
            return;
        }

        var formatted = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), emSize, Brushes.Black, pixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, rect.Width - 4),
            MaxTextHeight = Math.Max(1, rect.Height - 2),
            Trimming = TextTrimming.CharacterEllipsis,
        };
        dc.DrawText(formatted, new Point(rect.X + 2, rect.Y + 1));
    }

    /// <summary>Helles, deckendes Rosa mit dunkleren Schrägstrichen: auffällig auf hellem und dunklem Hintergrund, schwarzer Text bleibt lesbar.</summary>
    private static Brush CreateHatch()
    {
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xF8, 0xD8, 0xEC)), null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
        group.Children.Add(new GeometryDrawing(null, CreatePen(Color.FromRgb(0xD0, 0x50, 0xA8), 1.5), new LineGeometry(new Point(0, 8), new Point(8, 0))));
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 8, 8),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 8, 8),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Color color, double thickness)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
