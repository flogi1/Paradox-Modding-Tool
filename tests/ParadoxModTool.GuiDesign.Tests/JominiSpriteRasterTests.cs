using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class JominiSpriteRasterTests
{
    private static JominiSpriteVisual Sprite(
        int width,
        int height,
        JominiSpriteKind kind = JominiSpriteKind.Stretched,
        JominiBorder border = default,
        double density = 1,
        bool mirrorH = false) =>
        new("gfx/x.dds", "C:\\x.dds", width, height, new LayoutRect(0, 0, width, height), density, kind, border, JominiFit.Fill,
            mirrorH, false, JominiColor.White, 1, JominiSpritePlaceholder.None, null);

    private static GuiImage Image(int width, int height, Func<int, int, (byte R, byte G, byte B, byte A)> color)
    {
        var bgra = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b, a) = color(x, y);
                var i = (y * width + x) * 4;
                bgra[i] = b;
                bgra[i + 1] = g;
                bgra[i + 2] = r;
                bgra[i + 3] = a;
            }
        }

        return new GuiImage(width, height, bgra);
    }

    private static (byte R, byte G, byte B, byte A) Pixel(GuiImage image, int x, int y)
    {
        var i = (y * image.Width + x) * 4;
        return (image.Bgra[i + 2], image.Bgra[i + 1], image.Bgra[i], image.Bgra[i + 3]);
    }

    private static GuiImage Empty(int width, int height) => new(width, height, new byte[width * height * 4]);

    [Fact]
    public void ImageRect_MatchesRendererRounding()
    {
        Assert.Equal(new PixelRect(0, 1, 10, 9), JominiSpriteGeometry.ImageRect(new LayoutRect(0.4, 0.6, 9.2, 9.7)));
        Assert.Null(JominiSpriteGeometry.ImageRect(new LayoutRect(0, 0, 0, 5)));
    }

    [Fact]
    public void Draw_Stretched_OneToOne_CopiesPixels()
    {
        var texture = Image(4, 4, (x, y) => ((x + y) % 2 == 0 ? (byte)255 : (byte)10, (byte)(x * 60), (byte)(y * 60), (x + y) % 3 == 0 ? (byte)128 : (byte)255));
        var target = Empty(4, 4);
        JominiSpriteRaster.Draw(target, texture, Sprite(4, 4), 1, 1, true, GuiTint.White);
        Assert.Equal(texture.Bgra, target.Bgra);
    }

    [Fact]
    public void Draw_Stretched_TwoToOne_SolidColour()
    {
        var texture = Image(4, 4, (_, _) => (10, 200, 30, 255));
        var target = Empty(8, 8);
        JominiSpriteRaster.Draw(target, texture, Sprite(4, 4), 2, 1, true, GuiTint.White);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                Assert.Equal(((byte)10, (byte)200, (byte)30, (byte)255), Pixel(target, x, y));
            }
        }
    }

    [Fact]
    public void Draw_CorneredStretched_KeepsCorners()
    {
        // Die Probetextur "slice": 40x40, Ecken R/Y/M/C je 8 Pixel.
        var texture = Image(40, 40, (x, y) =>
        {
            var left = x < 8;
            var right = x >= 32;
            var top = y < 8;
            var bottom = y >= 32;
            if (top && left) return (255, 0, 0, 255);
            if (top && right) return (255, 255, 0, 255);
            if (bottom && left) return (255, 0, 255, 255);
            if (bottom && right) return (0, 255, 255, 255);
            return (0, 0, 255, 255);
        });
        var target = Empty(80, 40);
        var sprite = Sprite(40, 40, JominiSpriteKind.CorneredStretched, new JominiBorder(8, 8, 8, 8), 2);
        JominiSpriteRaster.Draw(target, texture, sprite, 2, 1, true, GuiTint.White);

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Pixel(target, x, y));
                Assert.Equal(((byte)255, (byte)255, (byte)0, (byte)255), Pixel(target, 72 + x, y));
                Assert.Equal(((byte)255, (byte)0, (byte)255, (byte)255), Pixel(target, x, 32 + y));
                Assert.Equal(((byte)0, (byte)255, (byte)255, (byte)255), Pixel(target, 72 + x, 32 + y));
            }
        }

        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), Pixel(target, 40, 20));
    }

    [Fact]
    public void Draw_Tiled_Repeats()
    {
        var texture = Image(2, 2, (x, y) => ((byte)(x * 100 + 20), (byte)(y * 100 + 20), 7, 255));
        var target = Empty(8, 8);
        JominiSpriteRaster.Draw(target, texture, Sprite(2, 2, JominiSpriteKind.Tiled), 1, 1, true, GuiTint.White);
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                Assert.Equal(Pixel(texture, x % 2, y % 2), Pixel(target, x, y));
            }
        }
    }

    [Fact]
    public void Draw_Mirror_FlipsHorizontally()
    {
        var texture = Image(2, 1, (x, _) => x == 0 ? ((byte)255, (byte)0, (byte)0, (byte)255) : ((byte)0, (byte)0, (byte)255, (byte)255));
        var target = Empty(2, 1);
        JominiSpriteRaster.Draw(target, texture, Sprite(2, 1, mirrorH: true), 1, 1, true, GuiTint.White);
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), Pixel(target, 0, 0));
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), Pixel(target, 1, 0));

        var plain = Empty(2, 1);
        JominiSpriteRaster.Draw(plain, texture, Sprite(2, 1, mirrorH: true), 1, 1, false, GuiTint.White);
        Assert.Equal(texture.Bgra, plain.Bgra);
    }

    [Fact]
    public void Draw_Tint_Multiplies()
    {
        var texture = Image(1, 1, (_, _) => (128, 128, 128, 255));
        var target = Empty(1, 1);
        JominiSpriteRaster.Draw(target, texture, Sprite(1, 1), 1, 1, true, new GuiTint(0.5, 1, 2));
        Assert.Equal(((byte)64, (byte)128, (byte)255, (byte)255), Pixel(target, 0, 0));
    }
}
