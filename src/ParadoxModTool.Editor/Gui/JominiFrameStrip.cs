using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Layout;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Bildleiste unter <c>frame</c>, <c>framesize</c> und <c>frame_grid</c> (Jomini): alle Bilder der Textur nebeneinander, 48 Pixel hoch (Seitenverhältnis bleibt),
/// je Bild ein Rahmen und die Nummer ab 1; das aktuelle Bild ist in der Akzentfarbe hervorgehoben. Ein Klick meldet die Bildnummer
/// (<see cref="FrameClicked"/>), sofern <see cref="ClicksEnabled"/>. Über <see cref="MaxFrames"/> Bilder steht nur ein Hinweis da. Das Bild lädt im Hintergrund
/// über den <see cref="GuiImageCache"/>; ein später eintreffendes Ergebnis einer überholten Anfrage wird verworfen. Raster, Hervorhebung und Hinweis
/// stehen sofort nach <see cref="Show"/> fest, unabhängig vom Bild.
/// </summary>
public sealed class JominiFrameStrip : FrameworkElement
{
    /// <summary>Höchstens so viele Bilder werden gezeichnet.</summary>
    public const int MaxFrames = 256;

    private const double FrameHeight = 48;
    private const double Gap = 4;
    private const double NumberHeight = 14;
    private const double MessageHeight = 18;

    private BitmapSource? _bitmap;
    private string? _path;
    private int _version;
    private bool _clicksEnabled = true;

    /// <summary>Ein Bild (ab 1) wurde angeklickt.</summary>
    public event EventHandler<long>? FrameClicked;

    /// <summary>Raster der angezeigten Textur oder <see langword="null"/> (nicht auswertbar, z. B. halb getippt oder Bindung).</summary>
    public JominiFrameLayout? Layout { get; private set; }

    /// <summary>Das hervorgehobene Bild oder <see langword="null"/> (Bindung, außerhalb des Rasters, zu viele Bilder).</summary>
    public long? Highlighted { get; private set; }

    /// <summary>Hinweis unter bzw. statt der Leiste (<see cref="GuiTexts.FrameStripOutOfRange"/>, <see cref="GuiTexts.FrameStripTooMany"/>) oder <see langword="null"/>.</summary>
    public string? Message { get; private set; }

    /// <summary>Anzahl der gezeichneten Bilder (0 ohne Raster oder bei mehr als <see cref="MaxFrames"/>).</summary>
    public int DrawnCount { get; private set; }

    /// <summary>Klicks melden (aus bei schreibgeschützten Dateien). Solange <see cref="IsBound"/>, meldet die Leiste trotzdem nichts.</summary>
    public bool ClicksEnabled
    {
        get => _clicksEnabled;
        set
        {
            _clicksEnabled = value;
            UpdateCursor();
        }
    }

    /// <summary>
    /// Das aktuelle Bild kommt aus einer Datenbindung (<c>frame = "[…]"</c>): nichts hervorgehoben, Hinweis <see cref="GuiTexts.FrameStripBound"/>, und
    /// Klicks sind aus, damit kein Klick die Bindung stillschweigend durch eine Zahl ersetzt. Setzt <see cref="Show"/>.
    /// </summary>
    public bool IsBound { get; private set; }

    public JominiFrameStrip()
    {
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
    }

    /// <summary>
    /// Zeigt die Textur <paramref name="fullPath"/> im Raster <paramref name="layout"/>, mit <paramref name="current"/> hervorgehoben (<see langword="null"/>: Bindung).
    /// <paramref name="currentLabel"/> ist der Wert für den Hinweis "außerhalb" (sonst die Zahl). <paramref name="bound"/>: das Bild kommt aus einer
    /// Datenbindung (siehe <see cref="IsBound"/>).
    /// </summary>
    public void Show(string? fullPath, JominiFrameLayout? layout, long? current, string? currentLabel = null, bool bound = false)
    {
        Layout = layout;
        Highlighted = null;
        Message = null;
        DrawnCount = 0;
        IsBound = bound;
        UpdateCursor();
        if (layout is { } grid)
        {
            var count = grid.Count;
            if (count > MaxFrames)
            {
                Message = GuiTexts.FrameStripTooMany(count);
            }
            else
            {
                DrawnCount = (int)count;
                if (bound)
                {
                    Message = GuiTexts.FrameStripBound;
                }
                else if (current is { } frame && frame >= 1 && frame <= count)
                {
                    Highlighted = frame;
                }
                else
                {
                    Message = GuiTexts.FrameStripOutOfRange(
                        currentLabel ?? current?.ToString(CultureInfo.InvariantCulture) ?? "?", count);
                }
            }
        }

        LoadImage(fullPath);
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Wie ein Mausklick auf Bild <paramref name="frame"/>: meldet nur, wenn Klicks erlaubt sind, keine Bindung gilt und das Bild gezeichnet ist.</summary>
    internal void ClickFrame(long frame)
    {
        if (_clicksEnabled && !IsBound && frame >= 1 && frame <= DrawnCount)
        {
            FrameClicked?.Invoke(this, frame);
        }
    }

    private void UpdateCursor() => Cursor = _clicksEnabled && !IsBound ? Cursors.Hand : null;

    /// <summary>Das Bild unter <paramref name="point"/> oder <see langword="null"/>.</summary>
    internal long? FrameAt(Point point)
    {
        if (DrawnCount == 0 || point.Y < 0 || point.Y > FrameHeight + NumberHeight)
        {
            return null;
        }

        var step = FrameWidth + Gap;
        var index = (long)Math.Floor(point.X / step);
        return index >= 0 && index < DrawnCount && point.X - index * step <= FrameWidth ? index + 1 : null;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (FrameAt(e.GetPosition(this)) is { } frame)
        {
            ClickFrame(frame);
            e.Handled = true;
        }
    }

    /// <summary>Breite eines Bildes in der Leiste: 48 Pixel Höhe, Seitenverhältnis wie in der Textur (begrenzt, damit sehr schmale oder breite Bilder lesbar bleiben).</summary>
    private double FrameWidth => Layout is { FrameWidth: > 0, FrameHeight: > 0 } grid
        ? Math.Clamp(FrameHeight * grid.FrameWidth / grid.FrameHeight, 12, 480)
        : FrameHeight;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = DrawnCount > 0 ? DrawnCount * (FrameWidth + Gap) - Gap : 0;
        var height = (DrawnCount > 0 ? FrameHeight + NumberHeight : 0) + (Message is null ? 0 : MessageHeight);
        return new Size(Math.Max(width, Message is null ? 0 : 200), height);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var text = Resource("Theme.Text", Brushes.Black);
        var muted = Resource("Theme.TextMuted", Brushes.Gray);
        var accent = Resource("Theme.Accent", Brushes.DodgerBlue);
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var frameBox = new Pen(muted, 1);
        var highlight = new Pen(accent, 2);

        if (Layout is { } grid && DrawnCount > 0)
        {
            var width = FrameWidth;
            for (var frame = 1L; frame <= DrawnCount; frame++)
            {
                var x = (frame - 1) * (width + Gap);
                var box = new Rect(x, 0, width, FrameHeight);
                if (_bitmap is { } bitmap)
                {
                    DrawFrame(dc, bitmap, grid, frame, box);
                }

                dc.DrawRectangle(null, frame == Highlighted ? highlight : frameBox, box);
                var number = new FormattedText(
                    frame.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 10, frame == Highlighted ? accent : text, dip);
                dc.DrawText(number, new Point(x + (width - number.Width) / 2, FrameHeight + 1));
            }
        }

        if (Message is { } message)
        {
            var y = DrawnCount > 0 ? FrameHeight + NumberHeight : 0;
            dc.DrawText(new FormattedText(message, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, muted, dip), new Point(0, y + 2));
        }
    }

    /// <summary>
    /// Den Ausschnitt eines Bildes in seinen Kasten: das ganze Bild verschoben und skaliert zeichnen, auf den Kasten beschnitten (kein Zuschnitt, der am Rand
    /// werfen könnte). Der <see cref="GuiImageCache"/> lädt in voller Größe, ein Bildpixel ist ein Texturpixel.
    /// </summary>
    private static void DrawFrame(DrawingContext dc, BitmapSource bitmap, JominiFrameLayout grid, long frame, Rect box)
    {
        var source = grid.SourceOf(frame);
        if (source.Width <= 0 || source.Height <= 0)
        {
            return;
        }

        var scaleX = box.Width / source.Width;
        var scaleY = box.Height / source.Height;
        dc.PushClip(new RectangleGeometry(box));
        dc.DrawImage(bitmap, new Rect(box.X - source.X * scaleX, box.Y - source.Y * scaleY, bitmap.PixelWidth * scaleX, bitmap.PixelHeight * scaleY));
        dc.Pop();
    }

    private void LoadImage(string? fullPath)
    {
        // Gleiche Datei: schon geladen oder unterwegs.
        if (string.Equals(fullPath, _path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var mine = ++_version;
        _path = fullPath;
        _bitmap = null;
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        var dispatcher = Dispatcher;
        _ = Task.Run(() => GuiImageCache.Shared.Get(fullPath)).ContinueWith(
            task => dispatcher.BeginInvoke(() =>
            {
                if (mine == _version && task.IsCompletedSuccessfully && task.Result is { } bitmap)
                {
                    _bitmap = bitmap;
                    InvalidateVisual();
                }
            }),
            TaskScheduler.Default);
    }

    private Brush Resource(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;
}
