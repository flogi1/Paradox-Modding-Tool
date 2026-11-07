using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign.Layout;
using SketchGeometry = ParadoxModTool.GuiDesign.Layout.JominiBorderSketch;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Randskizze unter <c>spriteborder</c> und <c>spriteborder_left/right/top/bottom</c> (Jomini): der Ausschnitt des aktuellen Bildes, eingepasst in
/// <see cref="SketchWidth"/> × <see cref="SketchHeight"/> (Seitenverhältnis bleibt), mit vier Linien an den Schnittstellen in der Akzentfarbe des Themes
/// (ohne Theme eine feste Ersatzfarbe). Die Geometrie kommt aus <see cref="JominiBorderSketchGeometry.Fit"/>. Das Bild lädt im Hintergrund über den
/// <see cref="GuiImageCache"/>; ein später eintreffendes Ergebnis einer überholten Anfrage wird verworfen.
/// </summary>
public sealed class JominiBorderSketch : FrameworkElement
{
    public const double SketchWidth = 220;
    public const double SketchHeight = 140;

    private BitmapSource? _bitmap;
    private string? _path;
    private int _version;

    /// <summary>Der gezeigte Ausschnitt in Texturpixeln.</summary>
    public LayoutRect Source { get; private set; }

    /// <summary>Der gezeichnete Rand (wie <c>spriteborder</c>, ggf. live getippt).</summary>
    public JominiBorder Border { get; private set; }

    /// <summary>Lage von Bild und Linien in der Skizze; <see langword="null"/> vor dem ersten <see cref="Show"/>.</summary>
    public SketchGeometry? Geometry { get; private set; }

    public JominiBorderSketch()
    {
        SnapsToDevicePixels = true;
        Margin = new Thickness(0, 4, 0, 0);
        HorizontalAlignment = HorizontalAlignment.Left;
    }

    /// <summary>Zeigt den Ausschnitt <paramref name="source"/> der Textur <paramref name="fullPath"/> mit dem Rand <paramref name="border"/>.</summary>
    public void Show(string? fullPath, LayoutRect source, double density, JominiBorder border)
    {
        Source = source;
        Border = border;
        Geometry = JominiBorderSketchGeometry.Fit(source, density, border, SketchWidth, SketchHeight);
        LoadImage(fullPath);
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(SketchWidth, SketchHeight);

    protected override void OnRender(DrawingContext dc)
    {
        var muted = Resource("Theme.TextMuted", Brushes.Gray);
        var accent = Resource("Theme.Accent", Brushes.DodgerBlue);
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, SketchWidth, SketchHeight));
        if (Geometry is not { } sketch)
        {
            return;
        }

        var image = new Rect(sketch.OffsetX, sketch.OffsetY, sketch.ImageWidth, sketch.ImageHeight);
        if (_bitmap is { } bitmap && Source.Width > 0 && Source.Height > 0)
        {
            // Ganze Textur verschoben und skaliert, auf den Ausschnitt beschnitten (ein Bildpixel ist ein Texturpixel).
            dc.PushClip(new RectangleGeometry(image));
            dc.DrawImage(bitmap, new Rect(
                image.X - Source.X * sketch.Scale, image.Y - Source.Y * sketch.Scale, bitmap.PixelWidth * sketch.Scale, bitmap.PixelHeight * sketch.Scale));
            dc.Pop();
        }

        dc.DrawRectangle(null, new Pen(muted, 1), image);
        var line = new Pen(accent, 1.5) { DashStyle = DashStyles.Dash };
        dc.DrawLine(line, new Point(sketch.Left, image.Top), new Point(sketch.Left, image.Bottom));
        dc.DrawLine(line, new Point(sketch.Right, image.Top), new Point(sketch.Right, image.Bottom));
        dc.DrawLine(line, new Point(image.Left, sketch.Top), new Point(image.Right, sketch.Top));
        dc.DrawLine(line, new Point(image.Left, sketch.Bottom), new Point(image.Right, sketch.Bottom));
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
