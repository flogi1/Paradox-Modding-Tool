using System.Buffers.Binary;
using System.IO;
using System.Windows.Media.Imaging;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Text;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>
/// Tests, die <see cref="JominiEffectRules"/> lesen (der Schlüssel eines Effektbilds hängt davon ab) oder setzen: nie parallel zueinander, sonst ändert
/// ein Test die Regeln mitten im Abruf eines anderen.
/// </summary>
[CollectionDefinition(nameof(JominiEffectRules), DisableParallelization = true)]
public sealed class JominiEffectRulesCollection
{
}

/// <summary>Effektbilder im Bildpuffer: im Hintergrund zusammengesetzt, Bestandteile geteilt, neu bei geänderter Datei, im selben Speicherbudget.</summary>
[Collection(nameof(JominiEffectRules))]
public sealed class GuiImageCacheCompositeTests
{
    private static readonly (byte B, byte G, byte R) White = (255, 255, 255);
    private static readonly (byte B, byte G, byte R) Red = (0, 0, 255);
    private static readonly (byte B, byte G, byte R) Blue = (255, 0, 0);

    /// <summary>Einfarbiges, deckendes 32-Bit-TGA (Ursprung oben links).</summary>
    internal static string WriteTga(string relative, int width, int height, (byte B, byte G, byte R) colour)
    {
        var path = TempFiles.Write(relative);
        var data = new byte[18 + width * height * 4];
        data[2] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(12), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(14), (ushort)height);
        data[16] = 32;
        data[17] = 0x28;
        for (var i = 18; i < data.Length; i += 4)
        {
            data[i] = colour.B;
            data[i + 1] = colour.G;
            data[i + 2] = colour.R;
            data[i + 3] = 255;
        }

        File.WriteAllBytes(path, data);
        return path;
    }

    internal static JominiSpriteVisual Sprite(string path, int width, int height) =>
        new(
            "texture", path, width, height, new LayoutRect(0, 0, width, height), 1, JominiSpriteKind.Stretched, default, JominiFit.Fill,
            false, false, JominiColor.White, 1, JominiSpritePlaceholder.None, null);

    /// <summary>Grundbild mit einer deckenden <c>normal</c>-Ebene darüber: das Ergebnis hat die Farbe der Ebene.</summary>
    internal static JominiEffectRecipe Recipe(JominiSpriteVisual baseSprite, JominiSpriteVisual layer, double width, double height, double ppu = 1, int padding = 0) =>
        new(
            baseSprite,
            new JominiEffectVisual(new[] { new JominiTextureLayer(layer, JominiBlendMode.Normal) }, null, JominiColorEffect.None),
            width, height, 1, ppu, padding);

    private static (byte B, byte G, byte R, byte A) PixelAt(BitmapSource bitmap, int x, int y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new System.Windows.Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return (pixel[0], pixel[1], pixel[2], pixel[3]);
    }

    [Fact]
    public void TryGetComposite_NeverComposesOnCallerThread()
    {
        var basePath = WriteTga("fx/thread-base.tga", 4, 4, White);
        var layerPath = WriteTga("fx/thread-layer.tga", 4, 4, Red);
        var recipe = Recipe(Sprite(basePath, 4, 4), Sprite(layerPath, 4, 4), 8, 8);
        var cache = new GuiImageCache(1024 * 1024);
        var raised = 0;
        cache.ImageReady += (_, _) => Interlocked.Increment(ref raised);

        Assert.Equal(GuiImageState.Pending, cache.TryGetComposite(recipe, out var none));
        Assert.Null(none);

        // ImageReady kommt erst nach dem Speichern: auf beides warten.
        WaitUntil(cache, () => cache.TryGetComposite(recipe, out _) != GuiImageState.Pending && Volatile.Read(ref raised) >= 1);
        Assert.Equal(GuiImageState.Ready, cache.TryGetComposite(recipe, out var bitmap));
        Assert.True(bitmap!.IsFrozen);
        Assert.Equal(8, bitmap.PixelWidth);
        Assert.Equal(8, bitmap.PixelHeight);
        Assert.Equal((0, 0, 255, 255), PixelAt(bitmap, 4, 4));
        Assert.NotEmpty(cache.DecodeThreadIds);
        Assert.DoesNotContain(Environment.CurrentManagedThreadId, cache.DecodeThreadIds);
    }

    [Fact]
    public void TryGetComposite_RecomposesWhenComponentStampChanges()
    {
        var basePath = WriteTga("fx/stamp-base.tga", 4, 4, White);
        var layerPath = WriteTga("fx/stamp-layer.tga", 4, 4, Red);
        var recipe = Recipe(Sprite(basePath, 4, 4), Sprite(layerPath, 4, 4), 8, 8);
        var cache = new GuiImageCache(1024 * 1024);
        Assert.Equal(GuiImageState.Ready, WaitForComposite(cache, recipe, out var first));
        Assert.Equal((0, 0, 255, 255), PixelAt(first!, 4, 4));
        WaitIdle(cache);

        // Gleiche Größe, andere Farbe: nur der Schreibzeitpunkt unterscheidet die Datei.
        WriteTga("fx/stamp-layer.tga", 4, 4, Blue);
        File.SetLastWriteTimeUtc(layerPath, File.GetLastWriteTimeUtc(layerPath).AddSeconds(2));

        // Zwischen zwei Läufen bleibt das alte Bild; im neuen Lauf erst noch das alte (kein Rückfall auf das schlichte Sprite), dann das neue.
        Assert.Equal(GuiImageState.Ready, cache.TryGetComposite(recipe, out var same));
        Assert.Same(first, same);
        cache.NewRun();
        Assert.Equal(GuiImageState.Ready, cache.TryGetComposite(recipe, out var stale));
        Assert.Same(first, stale);
        WaitUntil(cache, () => cache.TryGetComposite(recipe, out var current) == GuiImageState.Ready && PixelAt(current!, 4, 4) == (255, 0, 0, 255));

        // Danach stellt kein Abruf mehr etwas in die Warteschlange.
        WaitIdle(cache);
        var decodes = cache.DecodeCount;
        cache.PrefetchComposites(new[] { recipe });
        Assert.Equal(GuiImageState.Ready, cache.TryGetComposite(recipe, out _));
        Assert.Equal(0, cache.QueuedCount);
        Assert.Equal(decodes, cache.DecodeCount);
    }

    [Fact]
    public void TryGetComposite_ReusesDecodedComponents()
    {
        var basePath = WriteTga("fx/reuse-base.tga", 4, 4, White);
        var layerPath = WriteTga("fx/reuse-layer.tga", 4, 4, Red);
        var recipe = Recipe(Sprite(basePath, 4, 4), Sprite(layerPath, 4, 4), 8, 8);
        var cache = new GuiImageCache(1024 * 1024);

        WaitUntil(cache, () => cache.TryGet(basePath, GuiTint.White, out _) == GuiImageState.Ready);
        Assert.Equal(1, cache.DecodeCount);

        Assert.Equal(GuiImageState.Ready, WaitForComposite(cache, recipe, out _));
        Assert.Equal(2, cache.DecodeCount);

        // Die Bestandteile liegen als weiße Bilder im Puffer: der schlichte Abruf der Ebene dekodiert nicht noch einmal.
        Assert.True(cache.Contains(layerPath));
        Assert.Equal(GuiImageState.Ready, cache.TryGet(layerPath, GuiTint.White, out _));
        Assert.Equal(2, cache.DecodeCount);
    }

    [Fact]
    public void TryGetComposite_CountsAgainstBudget()
    {
        // Bestandteile 1 × 1 (je 4 Byte), Effektbilder 8 × 8 (256 Byte) und 7 × 7 (196 Byte); Grenze 300 Byte: das zweite verdrängt das erste.
        var basePath = WriteTga("fx/budget-base.tga", 1, 1, White);
        var layerPath = WriteTga("fx/budget-layer.tga", 1, 1, Red);
        var older = Recipe(Sprite(basePath, 1, 1), Sprite(layerPath, 1, 1), 8, 8);
        var newer = Recipe(Sprite(basePath, 1, 1), Sprite(layerPath, 1, 1), 7, 7);
        var cache = new GuiImageCache(300);

        Assert.Equal(GuiImageState.Ready, WaitForComposite(cache, older, out _));
        Assert.True(cache.ContainsComposite(older));
        Assert.Equal(256 + 8, cache.CachedBytes);

        Assert.Equal(GuiImageState.Ready, WaitForComposite(cache, newer, out var bitmap));
        Assert.Equal(7, bitmap!.PixelWidth);
        WaitIdle(cache);
        Assert.False(cache.ContainsComposite(older));
        Assert.True(cache.ContainsComposite(newer));
        Assert.Equal(196 + 8, cache.CachedBytes);

        // Verdrängt: in diesem Lauf nicht noch einmal zusammensetzen.
        Assert.Equal(GuiImageState.OverBudget, cache.TryGetComposite(older, out _));
        Assert.Equal(0, cache.QueuedCount);
    }

    [Fact]
    public void TryGetComposite_UnreadableBase_IsUnreadable()
    {
        var basePath = Path.Combine(TempFiles.Root, "fx", "missing-base.tga");
        var layerPath = WriteTga("fx/unreadable-layer.tga", 4, 4, Red);
        var recipe = Recipe(Sprite(basePath, 4, 4), Sprite(layerPath, 4, 4), 8, 8);
        var cache = new GuiImageCache(1024 * 1024);

        Assert.Equal(GuiImageState.Unreadable, WaitForComposite(cache, recipe, out var none));
        Assert.Null(none);
        WaitIdle(cache);
        Assert.Equal(GuiImageState.Unreadable, cache.TryGetComposite(recipe, out _));
        Assert.Equal(0, cache.QueuedCount);
    }

    [Fact]
    public void CompositeKey_BuiltOncePerRecipeInstance()
    {
        var basePath = WriteTga("fx/key-base.tga", 4, 4, White);
        var layerPath = WriteTga("fx/key-layer.tga", 4, 4, Red);
        var recipe = Recipe(Sprite(basePath, 4, 4) with { MirrorHorizontal = true }, Sprite(layerPath, 4, 4), 8, 8);

        // Jeder Abruf (jedes Zeichnen) liest denselben, einmal gebauten Text.
        var first = GuiImageCache.CompositeKeyText(recipe);
        Assert.Same(first, GuiImageCache.CompositeKeyText(recipe));
        Assert.Equal(recipe.CacheKey, first);

        // Eine geänderte Regel gilt für jedes neue Rezept (auch eine with-Kopie ist eine neue Instanz).
        var original = JominiEffectRules.ElementMirrorFlipsLayers;
        try
        {
            JominiEffectRules.ElementMirrorFlipsLayers = !original;
            Assert.NotEqual(first, GuiImageCache.CompositeKeyText(recipe with { }));
        }
        finally
        {
            JominiEffectRules.ElementMirrorFlipsLayers = original;
        }
    }

    /// <summary>Wartet, bis die Warteschlange leer und kein Bild mehr in Arbeit ist (höchstens 5 s).</summary>
    internal static void WaitIdle(GuiImageCache cache) => WaitUntil(cache, () => cache.QueuedCount == 0);

    /// <summary>Fragt das Effektbild ab, bis es nicht mehr aussteht (höchstens 5 s).</summary>
    internal static GuiImageState WaitForComposite(GuiImageCache cache, JominiEffectRecipe recipe, out BitmapSource? bitmap)
    {
        var state = cache.TryGetComposite(recipe, out bitmap);
        if (state != GuiImageState.Pending)
        {
            return state;
        }

        WaitUntil(cache, () => cache.TryGetComposite(recipe, out _) != GuiImageState.Pending);
        return cache.TryGetComposite(recipe, out bitmap);
    }

    /// <summary>Wartet auf <see cref="GuiImageCache.ImageReady"/>, bis die Bedingung gilt (höchstens 5 s); ohne Abfrageschleife mit Schlaf.</summary>
    internal static void WaitUntil(GuiImageCache cache, Func<bool> condition)
    {
        using var signal = new ManualResetEventSlim(false);
        EventHandler handler = (_, _) => signal.Set();
        cache.ImageReady += handler;
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (true)
            {
                // Erst zurücksetzen, dann prüfen: ein Ereignis zwischen beidem geht nicht verloren.
                signal.Reset();
                if (condition())
                {
                    return;
                }

                var remaining = deadline - DateTime.UtcNow;
                Assert.True(remaining > TimeSpan.Zero && signal.Wait(remaining), "Das Bild wurde nicht rechtzeitig fertig.");
            }
        }
        finally
        {
            cache.ImageReady -= handler;
        }
    }
}
