using System.Buffers.Binary;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Zeichnen der Jomini-Sprites: Bild nur wenn fertig, Spiegeln, Färbung mit Deckkraft, Platzhalter und Kacheln.</summary>
public sealed class JominiSpriteRendererTests
{
    private static readonly (byte B, byte G, byte R) Red = (0, 0, 255);
    private static readonly (byte B, byte G, byte R) Green = (0, 255, 0);
    private static readonly (byte B, byte G, byte R) White = (255, 255, 255);

    /// <summary>Unkomprimiertes 32-Bit-TGA (Ursprung oben links), deckende Pixel zeilenweise.</summary>
    private static string WriteTga(string relative, int width, int height, params (byte B, byte G, byte R)[] pixels)
    {
        var path = TempFiles.Write(relative);
        var data = new byte[18 + width * height * 4];
        data[2] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(14), (ushort)height);
        data[16] = 32;
        data[17] = 0x28;
        for (var i = 0; i < width * height; i++)
        {
            var (b, g, r) = pixels[i % pixels.Length];
            data[18 + i * 4] = b;
            data[18 + i * 4 + 1] = g;
            data[18 + i * 4 + 2] = r;
            data[18 + i * 4 + 3] = 255;
        }

        File.WriteAllBytes(path, data);
        return path;
    }

    private static JominiSpriteVisual Sprite(
        string? path,
        int width,
        int height,
        JominiSpriteKind kind = JominiSpriteKind.Stretched,
        bool mirrorHorizontal = false,
        JominiColor? tint = null,
        double opacity = 1,
        JominiSpritePlaceholder placeholder = JominiSpritePlaceholder.None,
        string? label = null) =>
        new(
            "texture", path, width, height, new LayoutRect(0, 0, width, height), 1, kind, default, JominiFit.Fill,
            mirrorHorizontal, false, tint ?? JominiColor.White, opacity, placeholder, label);

    /// <summary>Wartet auf <see cref="GuiImageCache.ImageReady"/>, bis das Bild nicht mehr aussteht (höchstens 5 s, ohne Schlafschleife).</summary>
    private static void WaitReady(GuiImageCache cache, string path, GuiTint tint)
    {
        using var signal = new ManualResetEventSlim(false);
        void OnReady(object? sender, EventArgs e) => signal.Set();
        cache.ImageReady += OnReady;
        try
        {
            cache.Prefetch(new[] { (path, tint) });
            var deadline = DateTime.UtcNow.AddSeconds(5);
            // Nach NewRun liefert TryGet sofort den letzten Stand; fertig ist das Bild erst, wenn der Hintergrund es in diesem Lauf geprüft hat.
            while (cache.TryGet(path, tint, out _) == GuiImageState.Pending || !cache.IsSettled(path, tint))
            {
                var left = deadline - DateTime.UtcNow;
                Assert.True(left > TimeSpan.Zero && signal.Wait(left), "Bild wurde nicht rechtzeitig fertig.");
                signal.Reset();
            }
        }
        finally
        {
            cache.ImageReady -= OnReady;
        }
    }

    /// <summary>Zeichnet in eine <paramref name="width"/>×<paramref name="height"/> große Fläche (96 dpi, 1:1) und liefert BGRA je Pixel.</summary>
    private static (bool Drawn, int[] Pixels) Render(JominiSpriteVisual sprite, GuiImageCache cache, int width, int height, bool blackBackground = false, double renderScale = 1)
    {
        var drawn = false;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (blackBackground)
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
            }

            drawn = JominiSpriteRenderer.Draw(dc, sprite, new Rect(0, 0, width, height), 1, cache, 1, renderScale);
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new int[width * height];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return (drawn, pixels);
    }

    private static (int A, int R, int G, int B) At(int[] pixels, int width, int x, int y)
    {
        var value = pixels[y * width + x];
        return ((value >> 24) & 255, (value >> 16) & 255, (value >> 8) & 255, value & 255);
    }

    [Fact]
    public void Pending_image_is_not_drawn()
    {
        Sta.Run(() =>
        {
            var path = WriteTga("sprite/pending.tga", 4, 4, Red);
            var cache = new GuiImageCache(1024 * 1024);
            var (drawn, pixels) = Render(Sprite(path, 4, 4), cache, 20, 20);

            Assert.False(drawn);
            Assert.All(pixels, pixel => Assert.Equal(0, pixel));
        });
    }

    [Fact]
    public void Ready_image_fills_the_box()
    {
        Sta.Run(() =>
        {
            var path = WriteTga("sprite/ready.tga", 4, 4, Red);
            var cache = new GuiImageCache(1024 * 1024);
            WaitReady(cache, path, GuiTint.White);
            var (drawn, pixels) = Render(Sprite(path, 4, 4), cache, 20, 20);

            Assert.True(drawn);
            Assert.Equal((255, 255, 0, 0), At(pixels, 20, 10, 10));
            Assert.Equal((255, 255, 0, 0), At(pixels, 20, 1, 1));
            Assert.Equal((255, 255, 0, 0), At(pixels, 20, 18, 18));
        });
    }

    [Fact]
    public void Mirror_horizontal_swaps_left_and_right()
    {
        Sta.Run(() =>
        {
            // Links Rot, rechts Grün; gespiegelt links Grün, rechts Rot.
            var path = WriteTga("sprite/mirror.tga", 2, 1, Red, Green);
            var cache = new GuiImageCache(1024 * 1024);
            WaitReady(cache, path, GuiTint.White);

            var plain = Render(Sprite(path, 2, 1), cache, 20, 10).Pixels;
            Assert.Equal((255, 255, 0, 0), At(plain, 20, 3, 5));
            Assert.Equal((255, 0, 255, 0), At(plain, 20, 16, 5));

            var mirrored = Render(Sprite(path, 2, 1, mirrorHorizontal: true), cache, 20, 10).Pixels;
            Assert.Equal((255, 0, 255, 0), At(mirrored, 20, 3, 5));
            Assert.Equal((255, 255, 0, 0), At(mirrored, 20, 16, 5));
        });
    }

    [Fact]
    public void Opacity_and_tint_apply()
    {
        Sta.Run(() =>
        {
            // Weißes Bild, Färbung Rot, halb deckend über Schwarz: (128, 0, 0).
            var path = WriteTga("sprite/tint.tga", 4, 4, White);
            var cache = new GuiImageCache(1024 * 1024);
            WaitReady(cache, path, new GuiTint(1, 0, 0));
            var sprite = Sprite(path, 4, 4, tint: new JominiColor(1, 0, 0, 1), opacity: 0.5);
            var (drawn, pixels) = Render(sprite, cache, 20, 20, blackBackground: true);

            Assert.True(drawn);
            var (_, r, g, b) = At(pixels, 20, 10, 10);
            Assert.InRange(r, 126, 130);
            Assert.InRange(g, 0, 2);
            Assert.InRange(b, 0, 2);
        });
    }

    [Fact]
    public void Missing_texture_draws_the_hatched_placeholder()
    {
        Sta.Run(() =>
        {
            var cache = new GuiImageCache(1024 * 1024);
            foreach (var sprite in new[]
            {
                // Das Layout kennt die Textur nicht (kein Pfad): Platzhalter mit Grund.
                Sprite(null, 4, 4, placeholder: JominiSpritePlaceholder.Missing, label: "A"),
                // Die Datei fehlt oder ist unlesbar.
                Sprite(Path.Combine(TempFiles.Root, "sprite", "missing.tga"), 4, 4),
            })
            {
                if (sprite.TextureFullPath is { } path)
                {
                    WaitReady(cache, path, GuiTint.White);
                }

                var (drawn, pixels) = Render(sprite, cache, 40, 40);

                Assert.True(drawn);
                var (a, r, g, b) = At(pixels, 40, 20, 20);
                Assert.Equal(255, a);

                // Platzhalter-Rosa (Grund oder Schräg-Strich): Rot und Blau über Grün.
                Assert.True(r > g + 20 && b > g, $"Pixel {r},{g},{b} ist nicht rosa.");
            }
        });
    }

    [Fact]
    public void Tiled_piece_repeats_the_tile()
    {
        Sta.Run(() =>
        {
            // Kachel 2×2: oben Rot, Grün; unten Grün, Rot. Dichte 1, Zoom 1: Periode 2 Pixel.
            var path = WriteTga("sprite/tile.tga", 2, 2, Red, Green, Green, Red);
            var cache = new GuiImageCache(1024 * 1024);
            WaitReady(cache, path, GuiTint.White);
            var (drawn, pixels) = Render(Sprite(path, 2, 2, JominiSpriteKind.Tiled), cache, 8, 8);

            Assert.True(drawn);
            Assert.Equal(At(pixels, 8, 0, 0), At(pixels, 8, 4, 0));
            Assert.Equal(At(pixels, 8, 0, 0), At(pixels, 8, 0, 4));
            Assert.NotEqual(At(pixels, 8, 0, 0), At(pixels, 8, 1, 0));
            Assert.Equal((255, 255, 0, 0), At(pixels, 8, 0, 0));
        });
    }

    [Fact]
    public void Tiles_follow_the_render_scale()
    {
        Sta.Run(() =>
        {
            // Dieselbe Kachel mit scale 2 am Kasten (LayoutBox.RenderScale): Periode 4 Pixel, jedes Texel 2 Pixel breit.
            var path = WriteTga("sprite/tile-scaled.tga", 2, 2, Red, Green, Green, Red);
            var cache = new GuiImageCache(1024 * 1024);
            WaitReady(cache, path, GuiTint.White);
            var (drawn, pixels) = Render(Sprite(path, 2, 2, JominiSpriteKind.Tiled), cache, 8, 8, renderScale: 2);

            Assert.True(drawn);
            Assert.Equal(At(pixels, 8, 0, 0), At(pixels, 8, 1, 0));
            Assert.NotEqual(At(pixels, 8, 0, 0), At(pixels, 8, 2, 0));
            Assert.Equal(At(pixels, 8, 0, 0), At(pixels, 8, 4, 0));
        });
    }

    [Fact]
    public void Zero_size_source_draws_nothing_without_throwing()
    {
        Sta.Run(() =>
        {
            var path = WriteTga("sprite/zero.tga", 4, 4, Red);
            var cache = new GuiImageCache(1024 * 1024);
            WaitReady(cache, path, GuiTint.White);
            var sprite = Sprite(path, 4, 4) with { Source = new LayoutRect(4, 0, 0, 4) };
            var (_, pixels) = Render(sprite, cache, 20, 20);

            Assert.All(pixels, pixel => Assert.Equal(0, pixel));
        });
    }

    /// <summary>Eine Fläche mit einer Szene aus einem 60×60-Kasten mit Sprite (204×204 Pixel: Zoom 3, Rand 12, Weltpunkt x → Pixel 12 + 3x); die Szene ist gesetzt.</summary>
    private static GuiSchematicCanvas BuildCanvas(JominiSpriteVisual sprite, bool showBounds = false)
    {
        var node = GuiParser.Parse("iconType = { name = \"a\" }").Root.Children[0];
        var root = new LayoutBox
        {
            Element = new ResolvedElement
            {
                Key = "iconType",
                Role = GuiRole.EngineWidget,
                BaseWidget = "iconType",
                TypeChain = new[] { "iconType" },
                Origin = new GuiOrigin(GuiOriginKind.Here, string.Empty, null, 1, node),
            },
            Width = 60,
            Height = 60,
            Sprite = sprite,
        };
        var canvas = new GuiSchematicCanvas { ShowBounds = showBounds };
        canvas.SetScene(new GuiSchematicScene(root, _ => false), keepView: false);
        return canvas;
    }

    /// <summary>Legt die Fläche aus und zeichnet sie auf 204×204 Pixel.</summary>
    private static int[] Snapshot(GuiSchematicCanvas canvas)
    {
        canvas.Measure(new Size(204, 204));
        canvas.Arrange(new Rect(0, 0, 204, 204));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(204, 204, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var pixels = new int[204 * 204];
        bitmap.CopyPixels(pixels, 204 * 4, 0);
        return pixels;
    }

    /// <summary>Wartet, bis der gemeinsame Puffer das weiße Bild hält, ohne selbst etwas anzufragen (nur <c>ImageReady</c> und <c>Contains</c>).</summary>
    private static void WaitUntilCached(string path, Action trigger)
    {
        using var signal = new ManualResetEventSlim(false);
        void OnReady(object? sender, EventArgs e) => signal.Set();
        GuiImageCache.Shared.ImageReady += OnReady;
        try
        {
            trigger();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!GuiImageCache.Shared.Contains(path))
            {
                var left = deadline - DateTime.UtcNow;
                Assert.True(left > TimeSpan.Zero && signal.Wait(left), "Das Bild wurde nicht rechtzeitig geladen.");
                signal.Reset();
            }
        }
        finally
        {
            GuiImageCache.Shared.ImageReady -= OnReady;
        }
    }

    [Fact]
    public void Canvas_draws_the_texture_instead_of_the_schematic_box_and_outlines_it_on_request()
    {
        Sta.Run(() =>
        {
            // Frische Datei, die noch niemand angefragt hat: nur SetScene kann das Bild in die Warteschlange gestellt haben, und es wird nicht gerendert,
            // bevor es da ist (ein Rendern würde per TryGet anfragen).
            var path = WriteTga("sprite/canvas.tga", 4, 4, Red);
            var sprite = Sprite(path, 4, 4);
            Assert.False(GuiImageCache.Shared.Contains(path));
            GuiSchematicCanvas? canvas = null;
            WaitUntilCached(path, () => canvas = BuildCanvas(sprite));

            var plain = Snapshot(canvas!);
            Assert.Equal((255, 255, 0, 0), At(plain, 204, 100, 100));

            // "Rahmen zeigen": der Umriss liegt auf dem Rand (Weltpunkt 0 → Pixel 12) und unterscheidet sich vom reinen Bild.
            var framed = Snapshot(BuildCanvas(sprite, showBounds: true));
            Assert.Equal((255, 255, 0, 0), At(framed, 204, 100, 100));
            Assert.NotEqual(At(plain, 204, 12, 100), At(framed, 204, 12, 100));
        });
    }

    [Fact]
    public void Loaded_repaints_a_canvas_whose_image_became_ready_while_it_was_not_listening()
    {
        Sta.Run(() =>
        {
            // Die Textur fehlt zuerst: die Fläche zeigt den Platzhalter.
            var path = Path.Combine(TempFiles.Root, "sprite", "late.tga");
            var canvas = BuildCanvas(Sprite(path, 4, 4));
            WaitReady(GuiImageCache.Shared, path, GuiTint.White);
            var missing = Snapshot(canvas);
            Assert.NotEqual((255, 255, 0, 0), At(missing, 204, 100, 100));

            // Die Datei entsteht und wird geladen, während die Fläche nicht im Fenster steht (kein Zuhörer): sie zeichnet nicht von selbst neu.
            WriteTga("sprite/late.tga", 4, 4, Red);
            GuiImageCache.Shared.NewRun();
            WaitReady(GuiImageCache.Shared, path, GuiTint.White);
            Assert.NotEqual((255, 255, 0, 0), At(Snapshot(canvas), 204, 100, 100));

            // Loaded: einmal neu zeichnen, jetzt steht die Textur da.
            canvas.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Equal((255, 255, 0, 0), At(Snapshot(canvas), 204, 100, 100));
            canvas.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }
}
