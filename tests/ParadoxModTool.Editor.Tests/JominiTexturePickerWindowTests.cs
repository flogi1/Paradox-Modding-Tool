using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Imaging;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Texturauswahl (Jomini-Spiele): Suche im Hintergrund, Filter, vorgewählte aktuelle Textur, Übernahme und Abbruch beim Schließen.</summary>
[Collection("UiLanguage")]
public sealed class JominiTexturePickerWindowTests
{
    private static readonly IReadOnlyList<GuiTextureEntry> Entries = new[]
    {
        new GuiTextureEntry("gfx/interface/buttons/close.dds", @"C:\none\gfx\interface\buttons\close.dds", GuiTextureOrigin.Mod, null),
        new GuiTextureEntry("gfx/interface/icons/gold.dds", @"C:\none\gfx\interface\icons\gold.dds", GuiTextureOrigin.Vanilla, null),
        new GuiTextureEntry("gfx/interface/icons/prestige.png", @"C:\none\gfx\interface\icons\prestige.png", GuiTextureOrigin.Dlc, "dlc001"),
    };

    /// <summary>Arbeitet die Nachrichten des Fensters ab, bis die Suche angekommen ist (ohne Schlafen; höchstens 10 Sekunden).</summary>
    private static void WaitScanned(JominiTexturePickerWindow window)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        using var timeout = new Timer(_ => dispatcher.BeginInvoke(() => frame.Continue = false), null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
        window.Scanned.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
        Assert.True(window.Scanned.IsCompleted, "Die Suche kam nicht rechtzeitig an.");
    }

    private static JominiTexturePickerWindow Scanned(string? current = null)
    {
        var window = new JominiTexturePickerWindow(_ => Entries, current);
        window.StartScan();
        WaitScanned(window);
        return window;
    }

    [Fact]
    public void Shows_AllEntries_AfterScan()
    {
        Sta.Run(() =>
        {
            var window = Scanned();

            Assert.Equal(3, window.ShownPaths.Count);
            Assert.Equal(GuiTexts.TexturePickerCount(3, 3), window.CountText);
        });
    }

    [Fact]
    public void Search_Filters()
    {
        Sta.Run(() =>
        {
            var window = Scanned();

            window.SearchText = "ICONS";
            Assert.Equal(new[] { "gfx/interface/icons/gold.dds", "gfx/interface/icons/prestige.png" }, window.ShownPaths);
            Assert.Equal(GuiTexts.TexturePickerCount(2, 3), window.CountText);

            // Rückwärtsschrägstriche im Suchfeld gelten wie "/".
            window.SearchText = @"buttons\close";
            Assert.Equal(new[] { "gfx/interface/buttons/close.dds" }, window.ShownPaths);

            window.SearchText = "nothing";
            Assert.Empty(window.ShownPaths);

            window.SearchText = string.Empty;
            Assert.Equal(3, window.ShownPaths.Count);
        });
    }

    [Fact]
    public void Current_IsPreselected()
    {
        Sta.Run(() =>
        {
            var window = Scanned("\"GFX/interface/icons/gold.dds\"");

            Assert.Equal("gfx/interface/icons/gold.dds", window.SelectedPath);
            Assert.Contains(GuiTexts.TextureOriginVanilla, window.DetailText);
        });
    }

    [Fact]
    public void Ok_ReturnsRelativePath()
    {
        Sta.Run(() =>
        {
            var window = Scanned();
            Assert.False(window.Choose());
            Assert.Equal(string.Empty, window.Chosen);
            Assert.False(window.OkButton.IsEnabled);

            window.SelectedPath = "gfx/interface/icons/prestige.png";

            Assert.True(window.OkButton.IsEnabled);
            Assert.True(window.Choose());
            Assert.Equal("gfx/interface/icons/prestige.png", window.Chosen);
        });
    }

    [Fact]
    public void Scan_ShowsSearchingText_UntilDone()
    {
        Sta.Run(() =>
        {
            using var release = new ManualResetEventSlim();
            var window = new JominiTexturePickerWindow(
                _ =>
                {
                    release.Wait(TimeSpan.FromSeconds(10));
                    return Entries;
                },
                null);
            window.StartScan();

            Assert.Equal(GuiTexts.TexturePickerSearching, window.CountText);

            // Tippen während der Suche lässt den Hinweis stehen.
            window.SearchText = "icons";
            Assert.Equal(GuiTexts.TexturePickerSearching, window.CountText);

            release.Set();
            WaitScanned(window);
            Assert.Equal(GuiTexts.TexturePickerCount(2, 3), window.CountText);
        });
    }

    [Fact]
    public void Scan_Failure_ShowsMessage()
    {
        Sta.Run(() =>
        {
            var window = new JominiTexturePickerWindow(_ => throw new InvalidOperationException("Ordner fehlt"), null);
            window.StartScan();
            WaitScanned(window);

            Assert.Equal(GuiTexts.TexturePickerFailed("Ordner fehlt"), window.CountText);
            Assert.Empty(window.ShownPaths);

            // Tippen danach überschreibt die Meldung nicht mit "0 von 0".
            window.SearchText = "x";
            Assert.Equal(GuiTexts.TexturePickerFailed("Ordner fehlt"), window.CountText);
        });
    }

    [Fact]
    public void Thumbnails_DoNotTouchSharedCache()
    {
        Sta.Run(() =>
        {
            var first = GuiImageCacheTests.WriteTga("picker/thumb-" + Guid.NewGuid().ToString("N") + ".tga", 8, 4);
            var second = GuiImageCacheTests.WriteTga("picker/thumb-" + Guid.NewGuid().ToString("N") + ".tga", 4, 8);
            var entries = new[]
            {
                new GuiTextureEntry("gfx/interface/a.tga", first, GuiTextureOrigin.Mod, null),
                new GuiTextureEntry("gfx/interface/b.tga", second, GuiTextureOrigin.Mod, null),
            };

            // Frühere Tests können den gemeinsamen Puffer noch beschäftigen: erst abwarten, dann den Stand merken.
            var shared = GuiImageCache.Shared;
            var settle = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (shared.QueuedCount > 0 && DateTime.UtcNow < settle)
            {
                Thread.Sleep(10);
            }

            var decodes = shared.DecodeCount;
            var queued = shared.QueuedCount;
            var bytes = shared.CachedBytes;

            var window = new JominiTexturePickerWindow(_ => entries, null) { Left = -3000, Top = -3000, ShowActivated = false };
            try
            {
                window.Show();
                WaitScanned(window);
                window.SelectedPath = "gfx/interface/a.tga";
                window.UpdateLayout();
                var target = new RenderTargetBitmap(Math.Max(1, (int)window.ActualWidth), Math.Max(1, (int)window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                target.Render(window);

                // Bis beide Vorschaubilder dekodiert sind (in welchem Puffer auch immer), höchstens 10 Sekunden.
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                while (!Decoded(window, shared, first, second) && DateTime.UtcNow < deadline)
                {
                    WpfWindows.Pump();
                    Thread.Sleep(10);
                }

                Assert.True(Decoded(window, shared, first, second), "Die Vorschaubilder wurden nicht dekodiert.");
                Assert.NotSame(shared, window.ThumbnailCache);
                Assert.True(window.ThumbnailCache.Contains(first));
                Assert.True(window.ThumbnailCache.Contains(second));
                Assert.False(shared.Contains(first));
                Assert.False(shared.Contains(second));
                Assert.Equal(decodes, shared.DecodeCount);
                Assert.Equal(queued, shared.QueuedCount);
                Assert.Equal(bytes, shared.CachedBytes);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Beide Vorschaubilder sind im eigenen Puffer des Fensters (oder, falsch, im gemeinsamen) angekommen.</summary>
    private static bool Decoded(JominiTexturePickerWindow window, GuiImageCache shared, string first, string second) =>
        (window.ThumbnailCache.Contains(first) && window.ThumbnailCache.Contains(second)) || (shared.Contains(first) && shared.Contains(second));

    [Fact]
    public void Close_CancelsScan()
    {
        Sta.Run(() =>
        {
            using var started = new ManualResetEventSlim();
            CancellationToken seen = default;
            var window = new JominiTexturePickerWindow(
                token =>
                {
                    seen = token;
                    started.Set();
                    token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                    token.ThrowIfCancellationRequested();
                    return Entries;
                },
                null)
            {
                Left = -3000,
                Top = -3000,
                ShowActivated = false,
            };

            // Anzeigen startet die Suche (Loaded).
            window.Show();
            WpfWindows.Pump();
            Assert.True(started.Wait(TimeSpan.FromSeconds(10)), "Die Suche startete nicht.");
            Assert.False(seen.IsCancellationRequested);

            window.Close();

            Assert.True(seen.IsCancellationRequested);
        });
    }
}
