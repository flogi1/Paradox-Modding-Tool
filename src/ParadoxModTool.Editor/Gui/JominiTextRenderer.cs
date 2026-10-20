using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Zeichnet einen gesetzten Jomini-Text (<see cref="JominiTextVisual"/>) in einen Kasten der Vorschau: Glyphen der Spielschrift mit den vom Layout
/// gemessenen Vorschüben, Farbe, Unterstreichung, Schatten und Glühen (grobe Näherung, Effekte folgen in Plan 4), Platzhalter mit Markierungsgrund und
/// Text-Icons. Der Text wird nicht an seinem Kasten beschnitten (Überstand ist sichtbar); einen Beschnitt legt der Aufrufer um den Kasten.
/// Läuft auf dem UI-Thread; die Schriftdateien öffnet der Zeichner selbst (eigene <see cref="GlyphTypeface"/>-Puffer), das Layout teilt sich nichts mit ihm.
/// </summary>
public static class JominiTextRenderer
{
    private static readonly Dictionary<(string Path, StyleSimulations Simulations), GlyphTypeface?> Typefaces = new();
    private static readonly object TypefaceGate = new();

    /// <summary>Standard-Hintergrund der Platzhalter, wenn der Aufrufer keine Akzentfarbe des Themes übergibt.</summary>
    private static readonly Brush DefaultAccent = Frozen(new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF)));

    private static readonly Brush ShadowBrush = Frozen(new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)));

    /// <summary>Versätze der vier Glühkopien in Spielpixeln.</summary>
    private static readonly (double Dx, double Dy)[] GlowOffsets = { (-1.0, 0.0), (1.0, 0.0), (0.0, -1.0), (0.0, 1.0) };

    /// <summary>Eingefrorene Pinsel je Farbe (Text und Glühen), damit das Neuzeichnen nichts neu anlegt. Unter derselben Sperre wie die Schriftpuffer.</summary>
    private static readonly Dictionary<Color, Brush> SolidBrushes = new();

    /// <param name="rect">Kasten in Bildschirmkoordinaten.</param>
    /// <param name="zoom">Bildschirmpixel je Spielpixel.</param>
    /// <param name="accent">Akzentfarbe des Themes für den Platzhalter-Hintergrund (zu 25 % deckend); <see langword="null"/>: Standardblau.</param>
    /// <param name="renderScale">
    /// <see cref="LayoutBox.RenderScale"/> des Kastens (Jomini <c>scale</c>). Der Text ist in unskalierten Einheiten gesetzt, <paramref name="rect"/> ist schon
    /// skaliert: Schriftgröße, Vorschübe, Zeilenhöhen und Icons werden mit dem Faktor malgenommen. Nicht endlich oder ≤ 0 zählt als 1.
    /// </param>
    public static void Draw(DrawingContext dc, JominiTextVisual visual, Rect rect, double zoom, double pixelsPerDip, Brush? accent = null, double renderScale = 1)
    {
        if (double.IsFinite(renderScale) && renderScale > 0)
        {
            zoom *= renderScale;
        }

        // NaN und unendlich bestehen "<= 0" nicht; sie würden Maße und Glyphenläufe vergiften.
        if (!double.IsFinite(zoom) || zoom <= 0 || visual.Lines.Count == 0)
        {
            return;
        }

        // Alles in Spielpixeln rechnen und auf ganze Spielpixel abrunden (Halbpixel sind nicht gemessen, ±1), dann mit dem Zoom skalieren.
        var boxWidth = rect.Width / zoom;
        var boxHeight = rect.Height / zoom;
        var blockHeight = visual.Lines.Sum(line => Extent(line.Height));
        var top = visual.AlignV switch
        {
            JominiTextAlignV.Center => Math.Floor((boxHeight - blockHeight) / 2),
            JominiTextAlignV.Bottom => Math.Floor(boxHeight - blockHeight),
            _ => 0,
        };

        foreach (var line in visual.Lines)
        {
            var left = visual.AlignH switch
            {
                JominiTextAlignH.Center => Math.Floor((boxWidth - Extent(line.Width)) / 2),
                JominiTextAlignH.Right => Math.Floor(boxWidth - Extent(line.Width)),
                _ => 0,
            };
            DrawLine(dc, line, rect.X + left * zoom, rect.Y + top * zoom, zoom, pixelsPerDip, accent ?? DefaultAccent);
            top += Extent(line.Height);
        }
    }

    private static void DrawLine(DrawingContext dc, JominiTextLine line, double x, double lineTop, double zoom, double pixelsPerDip, Brush accent)
    {
        var baseline = lineTop + (double.IsFinite(line.Baseline) ? line.Baseline : 0) * zoom;
        foreach (var run in line.Runs)
        {
            // Schlechte Mod-Größen (negativ, NaN) ergeben Breite 0: Rect wirft bei negativer Breite, NaN vergiftet die folgenden Abschnitte.
            var width = Extent(run.Width) * zoom;
            switch (run.Kind)
            {
                case JominiTextRunKind.Icon:
                    DrawIcon(dc, run, x, baseline, zoom);
                    break;
                case JominiTextRunKind.Placeholder:
                    // Markierungsgrund: Akzentfarbe zu 25 % über die Höhe der Zeile.
                    dc.PushOpacity(0.25);
                    dc.DrawRectangle(accent, null, new Rect(x, lineTop, width, Math.Max(1, Extent(line.Height) * zoom)));
                    dc.Pop();
                    DrawText(dc, run, x, baseline, zoom, pixelsPerDip);
                    break;
                default:
                    DrawText(dc, run, x, baseline, zoom, pixelsPerDip);
                    break;
            }

            x += width;
        }
    }

    private static void DrawText(DrawingContext dc, JominiTextRun run, double x, double baseline, double zoom, double pixelsPerDip)
    {
        var color = ToColor(run.Color);
        if (color.A == 0)
        {
            return;
        }

        var brush = BrushOf(color);
        if (run.FontFile is not null && GlyphRunOf(run, x, baseline, zoom, pixelsPerDip) is { } glyphs)
        {
            if (run.Shadow)
            {
                dc.PushTransform(new TranslateTransform(zoom, zoom));
                dc.DrawGlyphRun(ShadowBrush, glyphs);
                dc.Pop();
            }

            if (run.Glow)
            {
                // Rohes Glühen: vier Kopien um einen Spielpixel versetzt in der Textfarbe zu 30 % (Plan 4 macht die echten Effekte).
                var glow = BrushOf(Color.FromArgb((byte)Math.Round(color.A * 0.3), color.R, color.G, color.B));
                foreach (var (dx, dy) in GlowOffsets)
                {
                    dc.PushTransform(new TranslateTransform(dx * zoom, dy * zoom));
                    dc.DrawGlyphRun(glow, glyphs);
                    dc.Pop();
                }
            }

            dc.DrawGlyphRun(brush, glyphs);
        }

        if (run.Underline)
        {
            // Linie von einem Spielpixel Dicke knapp unter der Grundlinie, über die ganze Breite des Abschnitts.
            dc.DrawRectangle(brush, null, new Rect(x, baseline + zoom, Math.Max(1, Extent(run.Width) * zoom), zoom));
        }
    }

    /// <summary>Die Glyphen des Abschnitts mit den Vorschüben des Layouts; <see langword="null"/> ohne lesbare Schrift oder ohne Zeichen.</summary>
    private static GlyphRun? GlyphRunOf(JominiTextRun run, double x, double baseline, double zoom, double pixelsPerDip)
    {
        // Ein Format mit size 0 oder negativ (oder NaN) würde den GlyphRun-Konstruktor werfen: der Abschnitt entfällt.
        if (!double.IsFinite(run.FontSize) || run.FontSize <= 0 || !double.IsFinite(zoom) || zoom <= 0)
        {
            return null;
        }

        var typeface = TypefaceFor(run.FontFile!, run.SimulateBold, run.SimulateItalic);
        if (typeface is null)
        {
            return null;
        }

        var indices = new List<ushort>();
        foreach (var rune in run.Text.EnumerateRunes())
        {
            indices.Add(typeface.CharacterToGlyphMap.TryGetValue(rune.Value, out var index) ? index : (ushort)0);
        }

        var count = Math.Min(indices.Count, run.Advances.Count);
        if (count == 0)
        {
            return null;
        }

        var advances = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            advances.Add(run.Advances[i] * zoom);
        }

        return new GlyphRun(
            typeface,
            bidiLevel: 0,
            isSideways: false,
            renderingEmSize: run.FontSize * zoom,
            pixelsPerDip: (float)pixelsPerDip,
            glyphIndices: indices.GetRange(0, count),
            baselineOrigin: new Point(x, baseline),
            advanceWidths: advances,
            glyphOffsets: null,
            characters: null,
            deviceFontName: null,
            clusterMap: null,
            caretStops: null,
            language: null);
    }

    /// <summary>
    /// Das Icon-Bild mit der Unterkante auf der Grundlinie, verschoben um den y-Versatz; fehlt die Textur oder lässt sie sich nicht lesen: schraffiertes
    /// Quadrat. Der Versatz bleibt unskaliert, weil der Abschnitt den <c>iconsize.fontsize</c>-Maßstab nicht trägt (Annahme wie im Layout).
    /// </summary>
    private static void DrawIcon(DrawingContext dc, JominiTextRun run, double x, double baseline, double zoom)
    {
        var height = Extent(run.IconHeight);
        var offsetY = double.IsFinite(run.IconOffsetY) ? run.IconOffsetY : 0;
        var target = new Rect(x, baseline + (offsetY - height) * zoom, Math.Max(1, Extent(run.Width) * zoom), Math.Max(1, height * zoom));
        // Nie auf dem UI-Thread dekodieren: steht das Bild noch aus oder wurde es verdrängt, bleibt die Stelle leer (die Vorschau zeichnet neu, sobald es da ist).
        var state = GuiImageState.Unreadable;
        BitmapSource? image = null;
        if (run.IconTexture is { } path)
        {
            state = GuiImageCache.Shared.TryGet(path, GuiTint.White, out image);
        }

        switch (state)
        {
            case GuiImageState.Ready when image is not null:
                dc.DrawImage(image, target);
                break;
            case GuiImageState.Pending:
            case GuiImageState.OverBudget:
                break;
            default:
                dc.DrawRectangle(ClassicVisualRenderer.PlaceholderFill, ClassicVisualRenderer.PlaceholderPen, target);
                break;
        }
    }

    private static GlyphTypeface? TypefaceFor(string path, bool bold, bool italic)
    {
        var simulations = (bold ? StyleSimulations.BoldSimulation : StyleSimulations.None) | (italic ? StyleSimulations.ItalicSimulation : StyleSimulations.None);
        lock (TypefaceGate)
        {
            if (Typefaces.TryGetValue((path, simulations), out var cached))
            {
                return cached;
            }

            GlyphTypeface? typeface = null;
            try
            {
                if (File.Exists(path))
                {
                    typeface = new GlyphTypeface(new Uri(path, UriKind.Absolute), simulations);
                }
            }
            catch (Exception)
            {
                // Jede Schriftdatei-Störung (unlesbar, kein Font, Rechte, ...) darf die Zeichenfläche nicht zu Fall bringen: wie fehlend behandeln, der Text bleibt
                // unsichtbar (das Layout hat die Schrift als fehlend gemeldet). Der Fehlschlag wird als null gemerkt, eine später angelegte Datei gilt erst nach einem
                // Neustart der App.
            }

            Typefaces[(path, simulations)] = typeface;
            return typeface;
        }
    }

    private static Brush BrushOf(Color color)
    {
        lock (TypefaceGate)
        {
            if (!SolidBrushes.TryGetValue(color, out var brush))
            {
                // Begrenzt, falls eine Vorschau sehr viele verschiedene Farben zeigt.
                if (SolidBrushes.Count >= 1024)
                {
                    SolidBrushes.Clear();
                }

                brush = Frozen(new SolidColorBrush(color));
                SolidBrushes[color] = brush;
            }

            return brush;
        }
    }

    /// <summary>Maß einer Breite oder Höhe aus dem Layout: negativ, NaN oder unendlich (schlechte Mod-Daten) = 0.</summary>
    private static double Extent(double value) => double.IsFinite(value) && value > 0 ? value : 0;

    private static Color ToColor(JominiColor color) => Color.FromArgb(Channel(color.A), Channel(color.R), Channel(color.G), Channel(color.B));

    private static byte Channel(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
