using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using DrawingContext = System.Windows.Media.DrawingContext;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace ParadoxModTool.App;

/// <summary>Zeichnet den Leerraum des <see cref="AlignmentSpacerGenerator"/> als schraffierte Fläche über die volle Breite (neutral, in gedämpfter Textfarbe).</summary>
public sealed class AlignmentGapRenderer : IBackgroundRenderer
{
    private readonly AlignmentSpacerGenerator _generator;

    public AlignmentGapRenderer(AlignmentSpacerGenerator generator)
    {
        _generator = generator;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        var brush = HatchBrush();
        foreach (var visualLine in textView.VisualLines)
        {
            var rows = _generator.RowsBefore(visualLine.FirstDocumentLine);
            if (rows <= 0)
            {
                continue;
            }

            var top = visualLine.VisualTop - textView.VerticalOffset;
            drawingContext.DrawRectangle(brush, null, new Rect(0, top, textView.ActualWidth, rows * textView.DefaultLineHeight));
        }
    }

    private static Brush HatchBrush()
    {
        var color = ThemeManager.GetColor("TextMuted");
        var line = new Pen(new SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B)), 1);
        line.Freeze();
        var drawing = new GeometryDrawing(
            new SolidColorBrush(Color.FromArgb(24, color.R, color.G, color.B)),
            line,
            new LineGeometry(new Point(0, 8), new Point(8, 0)));
        drawing.Freeze();
        var brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 8, 8),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 8, 8),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }
}
