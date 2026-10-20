using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Jomini-Text in der schematischen Vorschau: Farbe, Überstand, Platzhalter-Hintergrund, klassische Bilder unverändert.</summary>
public sealed class JominiTextRendererTests
{
    private static readonly string Arial = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "Fonts", "arial.ttf");

    private static readonly JominiColor Red = new(1, 0, 0, 1);

    private static JominiTextRun TextRun(string text, JominiTextRunKind kind, double size, JominiColor color)
    {
        var glyphs = new GlyphTypeface(new Uri(Arial));
        var advances = new List<double>();
        foreach (var rune in text.EnumerateRunes())
        {
            glyphs.CharacterToGlyphMap.TryGetValue(rune.Value, out var index);
            advances.Add(Math.Round(glyphs.AdvanceWidths[index] * size, MidpointRounding.AwayFromZero));
        }

        return new JominiTextRun(kind, text, Arial, size, false, false, color, false, false, false, advances, advances.Sum(), null, 0, 0);
    }

    /// <summary>Ein einzeiliger Text links oben (Aufruf der Hilfe liefert Zeilenhöhe 20, Grundlinie 15 wie bei Schriftgröße 15).</summary>
    private static JominiTextVisual Visual(string text, JominiTextRunKind kind = JominiTextRunKind.Text, JominiTextAlignV alignV = JominiTextAlignV.Top)
    {
        var run = TextRun(text, kind, 15, Red);
        var line = new JominiTextLine(new[] { run }, run.Width, 20, 15);
        return new JominiTextVisual(new[] { line }, run.Width, 20, JominiTextAlignH.Left, alignV, false, kind == JominiTextRunKind.Placeholder, Array.Empty<string>(), Array.Empty<string>());
    }

    private static LayoutBox Box(GuiVisual? visual, double x, double y, double width, double height)
    {
        var node = GuiParser.Parse("textbox = { name = \"a\" }").Root.Children[0];
        var element = new ResolvedElement
        {
            Key = "textbox",
            Role = GuiRole.EngineWidget,
            BaseWidget = "textbox",
            TypeChain = new[] { "textbox" },
            Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node),
        };
        return new LayoutBox { Element = element, X = x, Y = y, Width = width, Height = height, Visual = visual };
    }

    /// <summary>Zeichnet die Szene auf 204×204 Pixel (Zoom 2 bei Szene 90 breit, Rand 12: Weltpunkt x → Pixel 12 + 2x) und liefert alle Pixel (BGRA).</summary>
    private static int[] Render(LayoutBox root)
    {
        var canvas = new GuiSchematicCanvas();
        canvas.SetScene(new GuiSchematicScene(root, _ => false), keepView: false);
        canvas.Measure(new Size(204, 204));
        canvas.Arrange(new Rect(0, 0, 204, 204));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(204, 204, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var pixels = new int[204 * 204];
        bitmap.CopyPixels(pixels, 204 * 4, 0);
        return pixels;
    }

    private static int At(int[] pixels, int x, int y) => pixels[y * 204 + x];

    /// <summary>Ist das Pixel (BGRA) reines Rot, die Farbe des Testtexts?</summary>
    private static bool IsRedInk(int pixel)
    {
        var r = (pixel >> 16) & 0xFF;
        var g = (pixel >> 8) & 0xFF;
        var b = pixel & 0xFF;
        return r > 200 && g < 60 && b < 60;
    }

    private static bool AnyRedInk(int[] pixels, int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (IsRedInk(At(pixels, x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static LayoutBox Scene(GuiVisual visual, double boxWidth)
    {
        // Wurzel 90×90 legt den Zoom auf 2 fest; der Textkasten liegt oben links.
        var root = Box(null, 0, 0, 90, 90);
        root.Children.Add(Box(visual, 0, 0, boxWidth, 20));
        return root;
    }

    [Fact]
    public void Jomini_text_is_drawn_in_its_color()
    {
        Sta.Run(() =>
        {
            var pixels = Render(Scene(Visual("MMMM"), 80));

            // "MMMM" bei 15: breite, dichte Buchstaben; im Kasten (Pixel 12..172 × 12..52) muss reines Rot stehen.
            Assert.True(AnyRedInk(pixels, 12, 12, 172, 52));
        });
    }

    /// <summary>Breite der roten Tinte in Pixeln (erste bis letzte Spalte mit Rot); 0 ohne Tinte.</summary>
    private static int InkWidth(int[] pixels)
    {
        int left = int.MaxValue, right = -1;
        for (var y = 0; y < 204; y++)
        {
            for (var x = 0; x < 204; x++)
            {
                if (IsRedInk(At(pixels, x, y)))
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }
        }

        return right < 0 ? 0 : right - left + 1;
    }

    [Fact]
    public void Scaled_text_is_drawn_smaller()
    {
        Sta.Run(() =>
        {
            // scale 0,5 (LayoutBox.RenderScale): Der Text wird halb so groß gezeichnet (Schriftgröße, Vorschübe), der Kasten ist schon skaliert.
            var full = Render(Scene(Visual("MMMM"), 80));
            var root = Box(null, 0, 0, 90, 90);
            var scaled = Box(Visual("MMMM"), 0, 0, 40, 10);
            scaled.RenderScale = 0.5;
            root.Children.Add(scaled);
            var half = Render(root);

            var fullWidth = InkWidth(full);
            var halfWidth = InkWidth(half);
            Assert.True(fullWidth > 40, $"Tinte voll {fullWidth}");
            Assert.InRange(halfWidth, fullWidth / 2.0 - 1, fullWidth / 2.0 + 1);
        });
    }

    [Fact]
    public void Text_overflowing_its_box_is_drawn_outside()
    {
        Sta.Run(() =>
        {
            // Der Kasten ist 10 breit (Pixel 12..32), der Text rund 50 breit (7f: der Überstand wird gezeichnet).
            var pixels = Render(Scene(Visual("MMMM"), 10));

            Assert.True(AnyRedInk(pixels, 40, 12, 120, 52));
        });
    }

    [Fact]
    public void Placeholder_run_has_a_marker_background()
    {
        Sta.Run(() =>
        {
            var plain = Render(Scene(Visual("..."), 80));
            var marked = Render(Scene(Visual("...", JominiTextRunKind.Placeholder), 80));

            // Die Punkte lassen die Fläche über dem Text leer: nur der Platzhalter-Hintergrund unterscheidet beide Bilder (Pixel 14|16, Weltpunkt 1|2).
            Assert.NotEqual(At(plain, 14, 16), At(marked, 14, 16));
        });
    }

    [Fact]
    public void Line_without_runs_draws_only_the_box()
    {
        Sta.Run(() =>
        {
            var empty = new JominiTextVisual(Array.Empty<JominiTextLine>(), 0, 0, JominiTextAlignH.Left, JominiTextAlignV.Center, false, false, Array.Empty<string>(), Array.Empty<string>());

            var pixels = Render(Scene(empty, 80));

            Assert.False(AnyRedInk(pixels, 0, 0, 204, 204));
        });
    }

    [Fact]
    public void Vertical_center_places_the_block_in_the_middle_of_the_box()
    {
        Sta.Run(() =>
        {
            // Kasten 20 hoch (Pixel 12..52), Block 20 hoch: Top und Center fallen zusammen. Bei Kasten 60 hoch liegt die Mitte-Zeile 20 weiter unten.
            var root = Box(null, 0, 0, 90, 90);
            root.Children.Add(Box(Visual("MMMM", alignV: JominiTextAlignV.Center), 0, 0, 80, 60));
            var pixels = Render(root);

            // Weltzeile 0..20 bleibt leer, Tinte erst ab Weltpunkt 20 (Pixel 52).
            Assert.False(AnyRedInk(pixels, 12, 12, 172, 50));
            Assert.True(AnyRedInk(pixels, 12, 52, 172, 92));
        });
    }

    /// <summary>Kommt der helle Rosa-Grund des klassischen Platzhalters (ClassicVisualRenderer) im Ausschnitt vor? Nur dieses Zeichnen erzeugt ihn.</summary>
    private static bool AnyHatchGround(int[] pixels, int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if ((pixels[y * 204 + x] & 0xFFFFFF) == 0xF8D8EC)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [Fact]
    public void Classic_text_visual_is_unchanged()
    {
        Sta.Run(() =>
        {
            // Klassischer Text ohne Schrift: schraffierter Platzhalter des ClassicVisualRenderer (rosa Grund), nichts Rotes. Ein bloßer Kasten ohne Bild
            // (der Vergleich) hat diesen Grund nicht.
            var classic = Render(Scene(new GuiTextVisual("Hallo", null, "left", 0, 0), 80));
            var plain = Render(Scene(null!, 80));

            Assert.False(AnyRedInk(classic, 0, 0, 204, 204));
            Assert.True(AnyHatchGround(classic, 12, 12, 172, 52));
            Assert.False(AnyHatchGround(plain, 12, 12, 172, 52));
        });
    }

    [Fact]
    public void Run_with_unusable_size_or_zoom_is_skipped_without_exception()
    {
        Sta.Run(() =>
        {
            var zero = TextRun("MMMM", JominiTextRunKind.Text, 15, Red) with { FontSize = 0 };
            var negative = TextRun("MMMM", JominiTextRunKind.Text, 15, Red) with { FontSize = -4 };
            var nan = TextRun("MMMM", JominiTextRunKind.Text, 15, Red) with { FontSize = double.NaN };
            foreach (var run in new[] { zero, negative, nan })
            {
                var line = new JominiTextLine(new[] { run }, run.Width, 20, 15);
                var visual = new JominiTextVisual(new[] { line }, run.Width, 20, JominiTextAlignH.Left, JominiTextAlignV.Top, false, false, Array.Empty<string>(), Array.Empty<string>());

                Assert.False(AnyRedInk(Render(Scene(visual, 80)), 0, 0, 204, 204));
            }

            // Zoom NaN, unendlich und 0: direkt in einen Zeichenkontext, ohne Ausnahme und ohne Zeichnung.
            var good = Visual("MMMM");
            var drawing = new DrawingVisual();
            using (var dc = drawing.RenderOpen())
            {
                foreach (var zoom in new[] { double.NaN, double.PositiveInfinity, 0 })
                {
                    JominiTextRenderer.Draw(dc, good, new Rect(0, 0, 100, 40), zoom, 1);
                }
            }
        });
    }

    [Fact]
    public void Runs_with_negative_or_nan_width_are_drawn_without_exception()
    {
        Sta.Run(() =>
        {
            // Schlechte Mod-Größen (size:-10, fontsize_min = -5) dürfen OnRender nicht werfen: Rect wirft bei negativer Breite.
            foreach (var width in new[] { -10.0, double.NaN, double.NegativeInfinity })
            {
                foreach (var kind in new[] { JominiTextRunKind.Placeholder, JominiTextRunKind.Text, JominiTextRunKind.Icon })
                {
                    var run = TextRun("MMMM", kind, 15, Red) with { Width = width, Underline = true, IconHeight = width };
                    var line = new JominiTextLine(new[] { run, TextRun("M", kind, 15, Red) }, width, width, width);
                    var visual = new JominiTextVisual(new[] { line }, width, width, JominiTextAlignH.Center, JominiTextAlignV.Center, false, true, Array.Empty<string>(), Array.Empty<string>());

                    _ = Render(Scene(visual, 80));
                    var drawing = new DrawingVisual();
                    using var dc = drawing.RenderOpen();
                    JominiTextRenderer.Draw(dc, visual, new Rect(0, 0, 100, 40), 2, 1);
                }
            }
        });
    }

    [Fact]
    public void Unreadable_font_file_draws_no_text_without_exception()
    {
        Sta.Run(() =>
        {
            var broken = Path.Combine(Path.GetTempPath(), "pmt-broken-" + Guid.NewGuid().ToString("N") + ".ttf");
            File.WriteAllText(broken, "kein Font");
            try
            {
                var run = TextRun("MMMM", JominiTextRunKind.Text, 15, Red) with { FontFile = broken };
                var line = new JominiTextLine(new[] { run }, run.Width, 20, 15);
                var visual = new JominiTextVisual(new[] { line }, run.Width, 20, JominiTextAlignH.Left, JominiTextAlignV.Top, false, false, Array.Empty<string>(), Array.Empty<string>());

                Assert.False(AnyRedInk(Render(Scene(visual, 80)), 0, 0, 204, 204));
            }
            finally
            {
                File.Delete(broken);
            }
        });
    }

    [Fact]
    public void Right_aligned_text_ends_at_the_right_edge_of_the_box()
    {
        Sta.Run(() =>
        {
            var run = TextRun("MMMM", JominiTextRunKind.Text, 15, Red);
            var line = new JominiTextLine(new[] { run }, run.Width, 20, 15);
            var visual = new JominiTextVisual(new[] { line }, run.Width, 20, JominiTextAlignH.Right, JominiTextAlignV.Top, false, false, Array.Empty<string>(), Array.Empty<string>());

            // Kasten 80 breit (Pixel 12..172), Text rund 50 breit: links vom Textanfang (Weltpunkt 25 → Pixel 62) steht keine Tinte.
            var pixels = Render(Scene(visual, 80));

            Assert.False(AnyRedInk(pixels, 12, 12, 60, 52));
            Assert.True(AnyRedInk(pixels, 100, 12, 172, 52));
        });
    }

    [Fact]
    public void Icon_without_texture_is_drawn_as_hatched_square()
    {
        Sta.Run(() =>
        {
            var icon = new JominiTextRun(JominiTextRunKind.Icon, "gold", null, 15, false, false, Red, false, false, false, new double[] { 20 }, 20, null, 20, 0);
            var line = new JominiTextLine(new[] { icon }, 20, 20, 20);
            var visual = new JominiTextVisual(new[] { line }, 20, 20, JominiTextAlignH.Left, JominiTextAlignV.Top, false, false, Array.Empty<string>(), Array.Empty<string>());

            var withIcon = Render(Scene(visual, 80));
            var without = Render(Scene(Visual(string.Empty), 80));

            // Das Quadrat (Weltpunkt 0..20 → Pixel 12..52) hat den hellen Schraffurgrund, die Fläche ohne Icon nur den Kastengrund.
            Assert.NotEqual(At(without, 30, 30), At(withIcon, 30, 30));
        });
    }
}
