using System.Buffers.Binary;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Imaging;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Bildpuffer der klassischen Vorschau: dekodierte Texturen als eingefrorene Bitmaps, nach Pixelbytes begrenzt.</summary>
public sealed class GuiImageCacheTests
{
    [Fact]
    public void Cache_returns_frozen_bitmap_and_evicts()
    {
        var first = WriteTga("cache/first.tga", 4, 2);
        var second = WriteTga("cache/second.tga", 4, 2);

        // Grenze für ein Bild (4 × 2 × 4 = 32 Byte): das zweite verdrängt das erste.
        var cache = new GuiImageCache(40);
        var bitmap = cache.Get(first);

        Assert.NotNull(bitmap);
        Assert.True(bitmap!.IsFrozen);
        Assert.Equal(4, bitmap.PixelWidth);
        Assert.Equal(2, bitmap.PixelHeight);
        Assert.Equal(PixelFormats.Bgra32, bitmap.Format);
        Assert.True(cache.Contains(first));

        Assert.NotNull(cache.Get(second));
        Assert.False(cache.Contains(first));
        Assert.True(cache.Contains(second));
        Assert.Equal(32, cache.CachedBytes);
    }

    [Fact]
    public void Recently_used_image_survives_eviction()
    {
        var a = WriteTga("lru/a.tga", 2, 2);
        var b = WriteTga("lru/b.tga", 2, 2);
        var c = WriteTga("lru/c.tga", 2, 2);

        // Platz für zwei Bilder zu je 16 Byte; a wird vor c noch einmal benutzt, also fällt b heraus.
        var cache = new GuiImageCache(32);
        cache.Get(a);
        cache.Get(b);
        cache.Get(a);
        cache.Get(c);

        Assert.True(cache.Contains(a));
        Assert.False(cache.Contains(b));
        Assert.True(cache.Contains(c));
    }

    [Fact]
    public void Unreadable_file_returns_null()
    {
        var broken = TempFiles.Write("cache/broken.dds");
        File.WriteAllBytes(broken, new byte[] { 1, 2, 3 });
        var cache = new GuiImageCache(1024);

        Assert.Null(cache.Get(broken));
        Assert.Null(cache.Get(Path.Combine(TempFiles.Root, "cache", "missing.tga")));
        Assert.Null(cache.Get(TempFiles.Write("cache/broken.png")));
    }

    [Fact]
    public void Edited_file_is_decoded_again_on_the_next_run()
    {
        var path = WriteTga("edit/a.tga", 4, 2);
        var cache = new GuiImageCache(1024);
        var first = cache.Get(path);
        Assert.Equal(4, first!.PixelWidth);

        WriteTga("edit/a.tga", 2, 2);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));

        // Zwischen zwei Läufen (Neuzeichnen beim Verschieben und Zoomen) wird die Datei nicht geprüft.
        Assert.Same(first, cache.Get(path));

        cache.NewRun();
        var second = cache.Get(path);
        Assert.Equal(2, second!.PixelWidth);
        Assert.Equal(16, cache.CachedBytes);
    }

    [Fact]
    public void File_added_after_a_failure_is_decoded_on_the_next_run()
    {
        var path = Path.Combine(TempFiles.Root, "late", Guid.NewGuid().ToString("N") + ".tga");
        var cache = new GuiImageCache(1024);
        Assert.Null(cache.Get(path));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(WriteTga("late/source.tga", 2, 2), path);
        Assert.Null(cache.Get(path));

        cache.NewRun();
        Assert.NotNull(cache.Get(path));
    }

    [Fact]
    public void TryGet_is_pending_until_the_worker_decodes()
    {
        var path = WriteTga("bg/pending.tga", 4, 2);
        var cache = new GuiImageCache(1024);

        Assert.Equal(GuiImageState.Pending, cache.TryGet(path, GuiTint.White, out var none));
        Assert.Null(none);

        WaitUntil(cache, () => cache.TryGet(path, GuiTint.White, out _) != GuiImageState.Pending);
        Assert.Equal(GuiImageState.Ready, cache.TryGet(path, GuiTint.White, out var bitmap));
        Assert.Equal(4, bitmap!.PixelWidth);
        Assert.True(bitmap.IsFrozen);

        // Der klassische Abruf teilt den Eintrag mit dem weißen Abruf.
        Assert.Same(bitmap, cache.Get(path));
    }

    [Fact]
    public void Jpg_is_decoded()
    {
        var path = Path.Combine(TempFiles.Root, "bg", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var source = BitmapSource.Create(6, 4, 96, 96, PixelFormats.Bgr24, null, new byte[6 * 4 * 3], 6 * 3);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using (var stream = File.Create(path))
        {
            encoder.Save(stream);
        }

        var cache = new GuiImageCache(1024);
        var state = WaitForResult(cache, path, GuiTint.White, out var bitmap);

        Assert.Equal(GuiImageState.Ready, state);
        Assert.Equal(6, bitmap!.PixelWidth);
        Assert.Equal(4, bitmap.PixelHeight);
        Assert.Equal(PixelFormats.Bgra32, bitmap.Format);
    }

    [Fact]
    public void Broken_jpg_is_unreadable()
    {
        var path = Path.Combine(TempFiles.Root, "bg", "broken.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "xx");
        var cache = new GuiImageCache(1024);
        Assert.Equal(GuiImageState.Unreadable, WaitForResult(cache, path, GuiTint.White, out var bitmap));
        Assert.Null(bitmap);

        // Mit echter JPG-Kennung, aber ohne Bilddaten: WPF wirft, das darf nichts durchbrechen.
        var truncated = Path.Combine(TempFiles.Root, "bg", "truncated.jpg");
        File.WriteAllBytes(truncated, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 4, 1, 2 });
        Assert.Equal(GuiImageState.Unreadable, WaitForResult(cache, truncated, GuiTint.White, out _));

        Assert.Equal(GuiImageState.Unreadable, WaitForResult(cache, Path.Combine(TempFiles.Root, "bg", "missing.jpg"), GuiTint.White, out _));
    }

    [Fact]
    public void Png_named_dds_is_decoded()
    {
        var path = WritePng("bg/disguised.dds", 3, 2, 200);
        var cache = new GuiImageCache(1024);

        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, GuiTint.White, out var bitmap));
        Assert.Equal(3, bitmap!.PixelWidth);
        Assert.Equal(2, bitmap.PixelHeight);
        Assert.NotNull(cache.Get(path));
    }

    [Fact]
    public void Tinted_variant_is_cached_separately()
    {
        var path = WritePng("bg/grey.png", 2, 2, 200);
        var cache = new GuiImageCache(1024);

        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, GuiTint.White, out var white));
        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, new GuiTint(1, 0, 0), out var red));

        Assert.NotSame(white, red);
        Assert.True(cache.Contains(path, GuiTint.White));
        Assert.True(cache.Contains(path, new GuiTint(1, 0, 0)));
        Assert.Equal(2 * 2 * 4 * 2, cache.CachedBytes);
        Assert.Equal(1, cache.DecodeCount);

        var whitePixels = new byte[16];
        white!.CopyPixels(whitePixels, 8, 0);
        Assert.Equal(new byte[] { 200, 200, 200, 255 }, whitePixels[..4]);
        var redPixels = new byte[16];
        red!.CopyPixels(redPixels, 8, 0);
        Assert.Equal(new byte[] { 0, 0, 200, 255 }, redPixels[..4]);
        Assert.True(red.IsFrozen);
    }

    [Fact]
    public void TryGet_never_decodes_on_the_calling_thread()
    {
        var path = WriteTga("bg/thread.tga", 4, 2);
        var cache = new GuiImageCache(1024);

        Assert.Equal(GuiImageState.Pending, cache.TryGet(path, GuiTint.White, out _));
        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, GuiTint.White, out _));
        Assert.Equal(1, cache.DecodeCount);
        Assert.DoesNotContain(Environment.CurrentManagedThreadId, cache.DecodeThreadIds);

        // Weitere Abrufe, auch mit anderer Färbung, dekodieren das Bild nicht noch einmal.
        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, new GuiTint(0.5, 0.5, 0.5), out _));
        Assert.Equal(1, cache.DecodeCount);
    }

    [Fact]
    public void Decoding_past_the_budget_does_not_loop()
    {
        // Grenze 1024 Byte, drei Bilder zu je 16 × 16 × 4 = 1024 Byte: nur das zuletzt gespeicherte bleibt, die anderen sind verdrängt.
        var paths = new[] { WriteTga("budget/a.tga", 16, 16), WriteTga("budget/b.tga", 16, 16), WriteTga("budget/c.tga", 16, 16) };
        var cache = new GuiImageCache(1024);

        cache.Prefetch(paths.Select(p => (p, GuiTint.White)));
        WaitUntil(cache, () => paths.All(p => cache.TryGet(p, GuiTint.White, out _) != GuiImageState.Pending));

        var states = paths.Select(p => cache.TryGet(p, GuiTint.White, out _)).ToArray();
        Assert.Equal(1, states.Count(s => s == GuiImageState.Ready));
        Assert.Equal(2, states.Count(s => s == GuiImageState.OverBudget));
        Assert.Equal(3, cache.DecodeCount);

        // Wiederholte Abrufe und erneutes Vorladen stellen nichts neu in die Warteschlange.
        cache.Prefetch(paths.Select(p => (p, GuiTint.White)));
        var evicted = paths.First(p => !cache.Contains(p));
        Assert.Equal(GuiImageState.OverBudget, cache.TryGet(evicted, GuiTint.White, out _));
        Assert.Equal(GuiImageState.OverBudget, cache.TryGet(evicted, GuiTint.White, out _));
        Assert.Equal(3, cache.DecodeCount);

        // Ein neuer Lauf darf es noch einmal versuchen.
        cache.NewRun();
        Assert.Equal(GuiImageState.Pending, cache.TryGet(evicted, GuiTint.White, out _));
        WaitUntil(cache, () => cache.TryGet(evicted, GuiTint.White, out _) != GuiImageState.Pending);
        Assert.Equal(4, cache.DecodeCount);
    }

    [Fact]
    public void Prefetch_decodes_each_image_once_and_raises_ImageReady()
    {
        var paths = Enumerable.Range(0, 6).Select(i => WriteTga($"prefetch/{i}.tga", 2, 2)).ToArray();
        var cache = new GuiImageCache(1024 * 1024);
        var raised = 0;
        cache.ImageReady += (_, _) => Interlocked.Increment(ref raised);

        var items = paths.Select(p => (p, GuiTint.White)).ToArray();
        cache.Prefetch(items);
        cache.Prefetch(items);
        WaitUntil(cache, () => paths.All(p => cache.TryGet(p, GuiTint.White, out _) == GuiImageState.Ready));

        Assert.Equal(6, cache.DecodeCount);
        Assert.True(Volatile.Read(ref raised) >= 1);
    }

    [Fact]
    public void Edited_file_is_decoded_again_through_TryGet_on_the_next_run()
    {
        var path = WriteTga("bg/edit.tga", 4, 2);
        var cache = new GuiImageCache(1024);
        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, GuiTint.White, out var first));
        Assert.Equal(4, first!.PixelWidth);

        WriteTga("bg/edit.tga", 2, 2);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));

        Assert.Equal(GuiImageState.Ready, cache.TryGet(path, GuiTint.White, out var same));
        Assert.Same(first, same);

        // Neuer Lauf: zuerst weiter das alte Bild (kein Rückfall auf den schematischen Kasten), der Hintergrund prüft die Datei und dekodiert neu.
        cache.NewRun();
        Assert.Equal(GuiImageState.Ready, cache.TryGet(path, GuiTint.White, out var stale));
        Assert.Same(first, stale);
        WaitUntil(cache, () => cache.TryGet(path, GuiTint.White, out var current) == GuiImageState.Ready && current!.PixelWidth == 2);
        Assert.Equal(GuiImageState.Ready, cache.TryGet(path, GuiTint.White, out var second));
        Assert.Equal(2, second!.PixelWidth);
    }

    [Fact]
    public void New_run_keeps_showing_known_images_while_revalidating()
    {
        var path = WriteTga("swr/known.tga", 4, 2);
        var tint = new GuiTint(0.5, 0.5, 0.5);
        var cache = new GuiImageCache(1024 * 1024);
        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, GuiTint.White, out var white));
        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, tint, out var tinted));
        WaitIdle(cache);
        var decodes = cache.DecodeCount;

        // Wie SetScene: neuer Lauf, dann alles vorladen. Sofort fertig, ohne zu warten, mit den bekannten Bildern.
        cache.NewRun();
        cache.Prefetch(new[] { (path, GuiTint.White), (path, tint) });
        Assert.Equal(GuiImageState.Ready, cache.TryGet(path, GuiTint.White, out var whiteNow));
        Assert.Same(white, whiteNow);
        Assert.Equal(GuiImageState.Ready, cache.TryGet(path, tint, out var tintedNow));
        Assert.Same(tinted, tintedNow);

        // Unveränderte Datei: geprüft, nicht neu dekodiert; danach stellt kein Abruf mehr etwas in die Warteschlange (keine Neuzeichnen-Schleife).
        WaitIdle(cache);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(GuiImageState.Ready, cache.TryGet(path, GuiTint.White, out _));
            Assert.Equal(GuiImageState.Ready, cache.TryGet(path, tint, out _));
            cache.Prefetch(new[] { (path, GuiTint.White), (path, tint) });
        }

        Assert.Equal(0, cache.QueuedCount);
        Assert.Equal(decodes, cache.DecodeCount);
    }

    [Fact]
    public void Changed_file_is_decoded_again_after_ImageReady_without_flashing()
    {
        var path = WriteTga("swr/changed.tga", 4, 2);
        var tint = new GuiTint(1, 0, 0);
        var cache = new GuiImageCache(1024 * 1024);
        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, tint, out var oldTinted));
        WaitIdle(cache);

        WriteTga("swr/changed.tga", 2, 2);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(2));
        cache.NewRun();
        cache.Prefetch(new[] { (path, tint) });
        Assert.Equal(GuiImageState.Ready, cache.TryGet(path, tint, out var stale));
        Assert.Same(oldTinted, stale);

        WaitUntil(cache, () => cache.TryGet(path, tint, out var current) == GuiImageState.Ready && current!.PixelWidth == 2);
        WaitIdle(cache);
        var decodes = cache.DecodeCount;
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(GuiImageState.Ready, cache.TryGet(path, tint, out _));
            Assert.Equal(GuiImageState.Ready, cache.TryGet(path, GuiTint.White, out var white));
            Assert.Equal(2, white!.PixelWidth);
        }

        Assert.Equal(0, cache.QueuedCount);
        Assert.Equal(decodes, cache.DecodeCount);
    }

    [Fact]
    public void Unreadable_file_stays_unreadable_across_runs_until_it_is_fixed()
    {
        var path = TempFiles.Write("swr/broken.dds");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        var cache = new GuiImageCache(1024);
        Assert.Equal(GuiImageState.Unreadable, WaitForResult(cache, path, GuiTint.White, out _));
        WaitIdle(cache);

        // Neuer Lauf: sofort weiter unlesbar (schraffiert), nicht ausstehend.
        cache.NewRun();
        cache.Prefetch(new[] { (path, GuiTint.White) });
        Assert.Equal(GuiImageState.Unreadable, cache.TryGet(path, GuiTint.White, out var none));
        Assert.Null(none);
        WaitIdle(cache);
        Assert.Equal(GuiImageState.Unreadable, cache.TryGet(path, GuiTint.White, out _));
        Assert.Equal(0, cache.QueuedCount);

        // Repariert (ein PNG; TGA gilt nur mit der Endung .tga): im nächsten Lauf erst noch unlesbar, nach dem Hintergrund fertig.
        File.Copy(WritePng("swr/fixed.png", 2, 2, 100), path, overwrite: true);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        cache.NewRun();
        Assert.Equal(GuiImageState.Unreadable, cache.TryGet(path, GuiTint.White, out _));
        WaitUntil(cache, () => cache.TryGet(path, GuiTint.White, out _) == GuiImageState.Ready);
    }

    [Fact]
    public void Image_that_breaks_is_unreadable_after_the_check()
    {
        var path = WriteTga("swr/breaks.tga", 2, 2);
        var cache = new GuiImageCache(1024);
        Assert.Equal(GuiImageState.Ready, WaitForResult(cache, path, GuiTint.White, out _));
        WaitIdle(cache);

        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        cache.NewRun();
        Assert.Equal(GuiImageState.Ready, cache.TryGet(path, GuiTint.White, out _));
        WaitUntil(cache, () => cache.TryGet(path, GuiTint.White, out _) == GuiImageState.Unreadable);
        WaitIdle(cache);
        Assert.False(cache.Contains(path));
        Assert.Equal(GuiImageState.Unreadable, cache.TryGet(path, GuiTint.White, out _));
        Assert.Equal(0, cache.QueuedCount);
    }

    [Fact]
    public void Recorded_decode_threads_are_capped()
    {
        var cache = new GuiImageCache(1024 * 1024);
        var path = WriteTga("cap/a.tga", 1, 1);
        for (var i = 0; i < GuiImageCache.MaxRecordedDecodeThreads + 20; i++)
        {
            // Jeder Lauf mit geänderter Datei dekodiert einmal neu.
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i));
            cache.NewRun();
            Assert.NotNull(cache.Get(path));
        }

        Assert.Equal(GuiImageCache.MaxRecordedDecodeThreads + 20, cache.DecodeCount);
        Assert.Equal(GuiImageCache.MaxRecordedDecodeThreads, cache.DecodeThreadIds.Length);
    }

    /// <summary>Wartet, bis die Warteschlange leer und kein Bild mehr in Arbeit ist (höchstens 5 s).</summary>
    private static void WaitIdle(GuiImageCache cache) => WaitUntil(cache, () => cache.QueuedCount == 0);

    /// <summary>Fragt ab, bis das Bild nicht mehr aussteht (höchstens 5 s).</summary>
    private static GuiImageState WaitForResult(GuiImageCache cache, string path, GuiTint tint, out BitmapSource? bitmap)
    {
        var state = cache.TryGet(path, tint, out bitmap);
        if (state != GuiImageState.Pending)
        {
            return state;
        }

        WaitUntil(cache, () => cache.TryGet(path, tint, out _) != GuiImageState.Pending);
        return cache.TryGet(path, tint, out bitmap);
    }

    /// <summary>Wartet auf <see cref="GuiImageCache.ImageReady"/>, bis die Bedingung gilt (höchstens 5 s); ohne Abfrageschleife mit Schlaf.</summary>
    private static void WaitUntil(GuiImageCache cache, Func<bool> condition)
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

    /// <summary>Einfarbig graues, deckendes PNG.</summary>
    private static string WritePng(string relative, int width, int height, byte grey)
    {
        var path = TempFiles.Write(relative);
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = pixels[i + 1] = pixels[i + 2] = grey;
            pixels[i + 3] = 255;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4)));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    /// <summary>Unkomprimiertes 32-Bit-TGA, Ursprung oben links, alle Pixel halbtransparentes Rot.</summary>
    internal static string WriteTga(string relative, int width, int height)
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
            data[i + 2] = 255;
            data[i + 3] = 128;
        }

        File.WriteAllBytes(path, data);
        return path;
    }
}
