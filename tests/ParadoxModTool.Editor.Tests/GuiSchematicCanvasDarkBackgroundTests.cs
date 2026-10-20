using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Tests;

/// <summary>
/// Jomini-Vorschauen haben einen festen dunklen Hintergrund (#1B1B1F, Florian 2026-10-20), klassische behalten den Hintergrund des Themes.
/// Geometrie: Fläche 204×204, Szene 50×50 → Anpassen gibt Zoom (204 - 2×12) / 50 = 3,6, der Kasten steht auf 12..192. Pixel (1|1) liegt damit im
/// 12 Pixel breiten Rand außerhalb des Kastens (reiner Hintergrund); Pixel (102|102) liegt mitten im Kasten (180 Pixel breit, also über 40: Beschriftung).
/// </summary>
public sealed class GuiSchematicCanvasDarkBackgroundTests
{
    private static readonly int DarkPixel = unchecked((int)0xFF1B1B1F);
    private static readonly int ThemePixel = unchecked((int)0xFFF0F0E0);

    private static LayoutBox Box(double x, double y, double size)
    {
        var node = GuiParser.Parse("iconType = { name = \"a\" }").Root.Children[0];
        var element = new ResolvedElement
        {
            Key = "iconType",
            Role = GuiRole.EngineWidget,
            BaseWidget = "iconType",
            TypeChain = new[] { "iconType" },
            Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node),
        };
        return new LayoutBox { Element = element, X = x, Y = y, Width = size, Height = size };
    }

    private static (GuiSchematicCanvas Canvas, GuiSceneBox Item) Canvas(bool dark, Color? themeText = null)
    {
        var scene = new GuiSchematicScene(Box(0, 0, 50), _ => false);
        var canvas = new GuiSchematicCanvas();
        canvas.Resources["Theme.EditorBackground"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xE0));
        canvas.Resources["Theme.Text"] = new SolidColorBrush(themeText ?? Colors.Black);
        canvas.DarkBackground = dark;
        canvas.SetScene(scene, keepView: false);
        return (canvas, Assert.Single(scene.Boxes));
    }

    private static int[] Render(GuiSchematicCanvas canvas)
    {
        canvas.Measure(new System.Windows.Size(204, 204));
        canvas.Arrange(new System.Windows.Rect(0, 0, 204, 204));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(204, 204, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var pixels = new int[204 * 204];
        bitmap.CopyPixels(pixels, 204 * 4, 0);
        return pixels;
    }

    [Fact]
    public void Jomini_scene_draws_on_the_dark_background()
    {
        Sta.Run(() => Assert.Equal(DarkPixel, Render(Canvas(dark: true).Canvas)[1 * 204 + 1]));
    }

    [Fact]
    public void Classic_scene_keeps_the_theme_background()
    {
        Sta.Run(() => Assert.Equal(ThemePixel, Render(Canvas(dark: false).Canvas)[1 * 204 + 1]));
    }

    [Fact]
    public void Schematic_fill_uses_the_stronger_alpha_on_the_dark_background()
    {
        Sta.Run(() =>
        {
            // Mitte des Kastens: Kategoriefarbe mit Alpha 90 über #1B1B1F; mit Alpha 40 läge der Wert um mehr als die Toleranz daneben.
            var (canvas, item) = Canvas(dark: true);
            var color = GuiSchematicCanvas.ColorOf(item.Category);
            var pixel = Render(canvas)[102 * 204 + 102];

            Assert.InRange(Channel(pixel, 16), Blend(color.R, 0x1B) - 3, Blend(color.R, 0x1B) + 3);
            Assert.InRange(Channel(pixel, 8), Blend(color.G, 0x1B) - 3, Blend(color.G, 0x1B) + 3);
            Assert.InRange(Channel(pixel, 0), Blend(color.B, 0x1F) - 3, Blend(color.B, 0x1F) + 3);
        });
    }

    [Theory]
    [InlineData(0x00, 0x00, 0x00)]
    [InlineData(0xFF, 0xFF, 0xFF)]
    public void Labels_are_light_on_the_dark_background(byte r, byte g, byte b)
    {
        // Gleich in hellem (schwarzer Text) und dunklem Theme (weißer Text): auf dem dunklen Hintergrund ist die Beschriftung hell.
        var theme = new SolidColorBrush(Color.FromRgb(r, g, b));

        var brush = Assert.IsType<SolidColorBrush>(GuiSchematicCanvas.LabelBrush(dark: true, theme));
        Assert.True(Luminance(brush.Color) > 0.6);
        Assert.Same(theme, GuiSchematicCanvas.LabelBrush(dark: false, theme));
    }

    [Fact]
    public void Label_renders_light_pixels_inside_the_box_although_the_theme_text_is_black()
    {
        Sta.Run(() =>
        {
            // Beschriftung oben links im Kasten (ab Pixel 14|13). Die Füllung ist dunkel, helle Pixel (> #B0 je Kanal) kann nur der Text liefern;
            // der Theme-Text ist schwarz, gezeichnet wird er trotzdem hell.
            var light = LightPixelsInLabelArea(Render(Canvas(dark: true, Colors.Black).Canvas));

            Assert.True(light > 10, $"helle Pixel: {light}");
        });
    }

    [Fact]
    public void Message_text_is_light_muted_on_the_dark_background_and_follows_the_theme_otherwise()
    {
        Sta.Run(() =>
        {
            var message = new TextBlock();
            message.Resources["Theme.TextMuted"] = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));

            GuiSchematicCanvas.ApplyMessageForeground(message, dark: true);
            var dark = Assert.IsType<SolidColorBrush>(message.Foreground);
            Assert.True(Luminance(dark.Color) > 0.5);

            GuiSchematicCanvas.ApplyMessageForeground(message, dark: false);
            var themed = Assert.IsType<SolidColorBrush>(message.Foreground);
            Assert.Equal(Color.FromRgb(0x60, 0x60, 0x60), themed.Color);
        });
    }

    private static int LightPixelsInLabelArea(int[] pixels)
    {
        var count = 0;
        for (var y = 13; y < 30; y++)
        {
            for (var x = 14; x < 100; x++)
            {
                var p = pixels[y * 204 + x];
                if (Channel(p, 16) > 0xB0 && Channel(p, 8) > 0xB0 && Channel(p, 0) > 0xB0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int Channel(int pixel, int shift) => (pixel >> shift) & 0xFF;

    /// <summary>Kategoriefarbe mit Alpha 90 über dem Hintergrundkanal.</summary>
    private static int Blend(byte color, int background) => (int)Math.Round(background + (color - background) * 90 / 255.0);

    private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;
}
