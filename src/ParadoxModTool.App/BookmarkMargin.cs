using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace ParadoxModTool.App;

/// <summary>
/// Schmaler Rand links im Editor, der auf der Höhe jeder Lesezeichen-Zeile ein kleines Lesezeichen-Symbol
/// zeigt (Nutzer-Feedback: im Vanilla-Diff-Fenster sollen die Lesezeichen der Datei sichtbar sein).
/// Die Zeilen (1-basiert) kommen aus dem <c>BookmarkStore</c>; der Rand zeichnet nur.
/// </summary>
public sealed class BookmarkMargin : AbstractMargin
{
    /// <summary>Gleiche Farbe wie das Lesezeichen-Symbol im Dateireiter.</summary>
    private static System.Windows.Media.Brush RibbonBrush => ParadoxModTool.Editor.TabIconBrushes.Get(ParadoxModTool.Editor.TabIconBrushes.BookmarkKey);

    private HashSet<int> _lines = new();

    public BookmarkMargin()
    {
        EventHandler onThemeChanged = (_, _) => InvalidateVisual();
        Loaded += (_, _) => ThemeManager.ThemeChanged += onThemeChanged;
        Unloaded += (_, _) => ThemeManager.ThemeChanged -= onThemeChanged;
    }

    public void SetLines(IEnumerable<int> lines)
    {
        _lines = new HashSet<int>(lines);
        InvalidateMeasure();
        InvalidateVisual();
    }

    // Ohne Lesezeichen belegt der Rand keinen Platz.
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize) =>
        new(_lines.Count > 0 ? 16 : 0, 0);

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.VisualLinesChanged -= TextView_VisualLinesChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);

        if (newTextView is not null)
        {
            newTextView.VisualLinesChanged += TextView_VisualLinesChanged;
        }
    }

    private void TextView_VisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
    {
        var textView = TextView;
        if (textView is null || !textView.VisualLinesValid || _lines.Count == 0)
        {
            return;
        }

        foreach (var visualLine in textView.VisualLines)
        {
            if (!_lines.Contains(visualLine.FirstDocumentLine.LineNumber))
            {
                continue;
            }

            var top = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
            var height = Math.Max(8, visualLine.TextLines[0].Height);

            // Lesezeichen-Band: Rechteck mit eingekerbter Unterkante.
            var geometry = new System.Windows.Media.StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new System.Windows.Point(3, top + 1), isFilled: true, isClosed: true);
                context.LineTo(new System.Windows.Point(13, top + 1), isStroked: false, isSmoothJoin: false);
                context.LineTo(new System.Windows.Point(13, top + height - 1), isStroked: false, isSmoothJoin: false);
                context.LineTo(new System.Windows.Point(8, top + height - 5), isStroked: false, isSmoothJoin: false);
                context.LineTo(new System.Windows.Point(3, top + height - 1), isStroked: false, isSmoothJoin: false);
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(RibbonBrush, null, geometry);
        }
    }
}
