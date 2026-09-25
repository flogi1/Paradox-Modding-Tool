using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Rendering;
using ParadoxModTool.VanillaDiff;
// Wie in CategoryColorsWindow.xaml.cs/MainWindow.xaml.cs: ImplicitUsings=enable + UseWindowsForms
// macht Brush/Color zwischen System.Drawing und System.Windows.Media mehrdeutig (CS0104) -
// explizite using-Aliase lösen das eindeutig zugunsten der WPF-eigenen Typen auf.
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using DrawingContext = System.Windows.Media.DrawingContext;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace ParadoxModTool.App;

/// <summary>
/// Färbt AvalonEdit-Zeilen nach ihrem strukturellen <see cref="DiffKind"/> ein (Abschnitt 7:
/// Whitespace/Tabs/Absätze werden NIE eingefärbt, nur echte AST-Unterschiede). Eine Instanz
/// pro Pane (links/rechts), da jede Seite ihre eigene Zeilenzuordnung hat.
/// </summary>
public sealed class DiffBackgroundRenderer : IBackgroundRenderer
{
    private const byte RowAlpha = 70;

    /// <summary>Zeilenhintergrund in der Theme-Farbe (Token) mit fester Transparenz.</summary>
    private static Brush RowBrush(string token)
    {
        var color = ThemeManager.GetColor(token);
        var brush = new SolidColorBrush(Color.FromArgb(RowAlpha, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private readonly IReadOnlyDictionary<int, DiffKind> _lineKinds;
    private readonly AlignmentSpacerGenerator? _spacers;

    public DiffBackgroundRenderer(IReadOnlyDictionary<int, DiffKind> lineKinds, AlignmentSpacerGenerator? spacers = null)
    {
        _lineKinds = lineKinds;
        _spacers = spacers;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        var addedBrush = RowBrush("DiffAdded");
        var removedBrush = RowBrush("DiffRemoved");
        var modifiedBrush = RowBrush("DiffModified");

        foreach (var visualLine in textView.VisualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;
            if (!_lineKinds.TryGetValue(lineNumber, out var kind) || kind == DiffKind.Unchanged)
            {
                continue;
            }

            var brush = kind switch
            {
                DiffKind.Added => addedBrush,
                DiffKind.Removed => removedBrush,
                DiffKind.Modified => modifiedBrush,
                _ => null
            };

            if (brush is null)
            {
                continue;
            }

            var gapRows = _spacers?.RowsBefore(visualLine.FirstDocumentLine) ?? 0;
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, visualLine.FirstDocumentLine))
            {
                var drawRect = gapRows > 0
                    ? new System.Windows.Rect(rect.X, rect.Y + gapRows * textView.DefaultLineHeight, rect.Width, Math.Max(0, rect.Height - gapRows * textView.DefaultLineHeight))
                    : rect;
                drawingContext.DrawRectangle(brush, null, drawRect);
            }
        }
    }
}
