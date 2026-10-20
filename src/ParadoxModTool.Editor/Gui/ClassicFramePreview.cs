using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ParadoxModTool.GuiDesign;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Vorschau einer Textur mit eingezeichneter Teilung in Frames (Bildstreifen: gleich breite Frames nebeneinander). Das Bild lädt im Hintergrund über den
/// gemeinsamen <see cref="GuiImageCache"/>; ein später eintreffendes Ergebnis einer überholten Anfrage wird verworfen. Gemeinsam für "Sprite anlegen" und
/// die Sprite-Auswahl.
/// </summary>
public sealed class ClassicFramePreview : Border
{
    private readonly Viewbox _viewbox = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both };
    private readonly TextBlock _none = new() { Text = GuiTexts.ClassicSpritePreviewNone, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private int _version;

    public ClassicFramePreview(double width = 360, double height = 140)
    {
        Width = width;
        Height = height;
        BorderThickness = new Thickness(1);
        Padding = new Thickness(2);
        SetResourceReference(BorderBrushProperty, "Theme.ControlBorder");
        SetResourceReference(BackgroundProperty, "Theme.EditorBackground");
        _none.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        Child = _none;
    }

    /// <summary>Zeigt die Textur <paramref name="fullPath"/> (oder "keine Vorschau", wenn <see langword="null"/> oder nicht lesbar), geteilt in <paramref name="frames"/> Frames.</summary>
    public void Show(string? fullPath, int frames)
    {
        var mine = ++_version;
        Child = _none;
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
                    Child = _viewbox;
                    _viewbox.Child = Build(bitmap, Math.Max(1, frames));
                }
            }),
            TaskScheduler.Default);
    }

    private static Canvas Build(BitmapSource bitmap, int frames)
    {
        var canvas = new Canvas { Width = bitmap.PixelWidth, Height = bitmap.PixelHeight, SnapsToDevicePixels = true };
        var image = new Image { Source = bitmap, Width = bitmap.PixelWidth, Height = bitmap.PixelHeight };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        canvas.Children.Add(image);

        // Dünne Linie in Schwarz mit hellem Kern, damit sie auf hellen wie dunklen Bildern zu sehen ist; die Dicke folgt der Bildgröße (die Box skaliert).
        var thickness = Math.Max(1, bitmap.PixelWidth / 300.0);
        for (var i = 1; i < frames; i++)
        {
            var x = bitmap.PixelWidth * (double)i / frames;
            canvas.Children.Add(Line(x, bitmap.PixelHeight, Brushes.Black, thickness * 3));
            canvas.Children.Add(Line(x, bitmap.PixelHeight, Brushes.White, thickness));
        }

        return canvas;
    }

    private static Line Line(double x, double height, Brush brush, double thickness) =>
        new() { X1 = x, X2 = x, Y1 = 0, Y2 = height, Stroke = brush, StrokeThickness = thickness, Opacity = 0.8 };
}
