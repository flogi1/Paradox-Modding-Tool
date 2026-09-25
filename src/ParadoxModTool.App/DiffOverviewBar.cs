using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ParadoxModTool.VanillaDiff;
// Wie in DiffBackgroundRenderer.cs: ImplicitUsings=enable + UseWindowsForms macht Brush/Color/Pen
// zwischen System.Drawing und System.Windows.Media mehrdeutig (CS0104).
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DrawingContext = System.Windows.Media.DrawingContext;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace ParadoxModTool.App;

/// <summary>
/// Schmale Übersichtsleiste neben einem Diff-Editor (wie die Positionsleiste in WinMerge): zeigt
/// maßstäblich über die GESAMTE Dokumenthöhe, wo Unterschiede liegen (grün = hinzugefügt, rot =
/// entfernt, gelb = geändert), dazu den aktuell sichtbaren Ausschnitt. Ein Klick springt an die
/// entsprechende Stelle; das synchrone Scrollen zieht die andere Seite mit.
/// </summary>
public sealed class DiffOverviewBar : FrameworkElement
{
    // Farben kommen aus dem aktiven Theme (Tokens); bei einem Themenwechsel wird neu gezeichnet.
    private static Brush TrackBrush => ThemeManager.GetBrush("OverviewTrack");
    private static Brush AddedBrush => ThemeManager.GetBrush("DiffAdded");
    private static Brush RemovedBrush => ThemeManager.GetBrush("DiffRemoved");
    private static Brush ModifiedBrush => ThemeManager.GetBrush("DiffModified");
    private static Brush BookmarkBrush => ParadoxModTool.Editor.TabIconBrushes.Get(ParadoxModTool.Editor.TabIconBrushes.BookmarkKey);
    private static Brush ViewportBrush => ThemeManager.GetBrush("DiffViewport");

    private TextEditor? _editor;
    private IReadOnlyDictionary<int, DiffKind> _lineKinds = new Dictionary<int, DiffKind>();
    private IReadOnlyCollection<int> _bookmarkLines = Array.Empty<int>();

    // Zuletzt gemessene Lage von Schiebebahn und Schieber der Editor-Scrollleiste (in Koordinaten dieser Leiste).
    private Rect _trackRect = Rect.Empty;
    private Rect _thumbRect = Rect.Empty;

    public DiffOverviewBar()
    {
        Width = 14;

        EventHandler onThemeChanged = (_, _) => InvalidateVisual();
        Loaded += (_, _) => ThemeManager.ThemeChanged += onThemeChanged;
        Unloaded += (_, _) => ThemeManager.ThemeChanged -= onThemeChanged;

        Cursor = Cursors.Hand;
        ToolTip = "Übersicht der Unterschiede - Klick springt an die Stelle";

        // Die Editor-Scrollleiste hat Pfeil-Schaltflächen oben/unten und ggf. eine horizontale
        // Leiste darunter; damit beide Leisten optisch parallel laufen, richtet sich diese Leiste
        // nach der tatsächlichen Schiebebahn (Track) der Editor-Scrollleiste. Änderungen der Lage
        // (Fenstergröße, horizontale Scrollleiste ein/aus) werden über LayoutUpdated erkannt -
        // neu gezeichnet wird nur bei echter Änderung, sonst würde InvalidateVisual eine
        // Endlosschleife aus Layout und Neuzeichnen auslösen.
        LayoutUpdated += (_, _) =>
        {
            var (track, thumb) = MeasureScrollBar();
            if (track != _trackRect || thumb != _thumbRect)
            {
                InvalidateVisual();
            }
        };
    }

    /// <summary>Misst Schiebebahn und Schieber der vertikalen Editor-Scrollleiste relativ zu dieser Leiste.</summary>
    private (Rect Track, Rect Thumb) MeasureScrollBar()
    {
        try
        {
            var scrollViewer = _editor?.Template?.FindName("PART_ScrollViewer", _editor) as ScrollViewer;
            var scrollBar = scrollViewer?.Template?.FindName("PART_VerticalScrollBar", scrollViewer) as ScrollBar;
            var track = scrollBar?.Track;

            if (track is null || !track.IsVisible || !IsVisible)
            {
                return (Rect.Empty, Rect.Empty);
            }

            var trackRect = track.TransformToVisual(this).TransformBounds(new Rect(0, 0, track.ActualWidth, track.ActualHeight));
            var thumb = track.Thumb;
            var thumbRect = thumb is null
                ? Rect.Empty
                : thumb.TransformToVisual(this).TransformBounds(new Rect(0, 0, thumb.ActualWidth, thumb.ActualHeight));

            return (trackRect, thumbRect);
        }
        catch (InvalidOperationException)
        {
            // Noch nicht Teil desselben visuellen Baums (z. B. vor dem ersten Layout).
            return (Rect.Empty, Rect.Empty);
        }
    }

    /// <summary>Bindet die Leiste an einen Editor und setzt die einzufärbenden Zeilen (1-basiert) neu.</summary>
    public void Update(TextEditor editor, IReadOnlyDictionary<int, DiffKind> lineKinds, IReadOnlyCollection<int>? bookmarkLines = null)
    {
        _bookmarkLines = bookmarkLines ?? Array.Empty<int>();
        if (!ReferenceEquals(_editor, editor))
        {
            if (_editor is not null)
            {
                _editor.TextArea.TextView.ScrollOffsetChanged -= OnScrollChanged;
            }

            _editor = editor;
            _editor.TextArea.TextView.ScrollOffsetChanged += OnScrollChanged;
        }

        _lineKinds = lineKinds;
        InvalidateVisual();
    }

    private void OnScrollChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext drawingContext)
    {
        (_trackRect, _thumbRect) = MeasureScrollBar();

        // Ohne messbare Editor-Scrollleiste: ganze Höhe verwenden.
        var trackTop = _trackRect.IsEmpty ? 0 : _trackRect.Top;
        var trackHeight = _trackRect.IsEmpty ? ActualHeight : _trackRect.Height;
        var width = ActualWidth;
        drawingContext.DrawRectangle(TrackBrush, null, new Rect(0, trackTop, width, Math.Max(0, trackHeight)));

        var totalLines = _editor?.Document?.LineCount ?? 0;
        if (_editor is null || totalLines == 0 || trackHeight <= 0)
        {
            return;
        }

        var lineHeight = trackHeight / totalLines;

        // Zusammenhängende Zeilen gleicher Art zu einem Balken zusammenfassen (mindestens 3 px hoch).
        var line = 1;
        while (line <= totalLines)
        {
            if (!_lineKinds.TryGetValue(line, out var kind) || kind == DiffKind.Unchanged)
            {
                line++;
                continue;
            }

            var runStart = line;
            while (line + 1 <= totalLines && _lineKinds.TryGetValue(line + 1, out var next) && next == kind)
            {
                line++;
            }

            var top = trackTop + (runStart - 1) * lineHeight;
            var runHeight = Math.Max(3, (line - runStart + 1) * lineHeight);
            drawingContext.DrawRectangle(BrushFor(kind), null, new Rect(1, top, Math.Max(0, width - 2), runHeight));
            line++;
        }

        // Lesezeichen als kleine blaue Markierung am linken Rand der Leiste (Nutzer-Feedback).
        foreach (var bookmarkLine in _bookmarkLines)
        {
            if (bookmarkLine >= 1 && bookmarkLine <= totalLines)
            {
                var y = trackTop + (bookmarkLine - 1) * lineHeight;
                drawingContext.DrawRectangle(BookmarkBrush, null, new Rect(0, y, Math.Min(6, width), Math.Max(3, lineHeight)));
            }
        }

        // Sichtbarer Ausschnitt: exakt die Lage des Schiebers der Editor-Scrollleiste (deren
        // Schieberposition skaliert über Bahnlänge minus Schieberlänge, nicht proportional zur
        // Dokumenthöhe - so läuft die Anzeige garantiert parallel).
        if (!_thumbRect.IsEmpty)
        {
            drawingContext.DrawRectangle(ViewportBrush, null, new Rect(0, _thumbRect.Top, width, _thumbRect.Height));
        }
        else if (_editor.ExtentHeight > 0)
        {
            var viewTop = _editor.VerticalOffset / _editor.ExtentHeight * trackHeight;
            var viewHeight = Math.Max(4, _editor.ViewportHeight / _editor.ExtentHeight * trackHeight);
            drawingContext.DrawRectangle(ViewportBrush, null, new Rect(0, trackTop + viewTop, width, viewHeight));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        CaptureMouse();
        ScrollToPoint(e.GetPosition(this).Y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
        {
            ScrollToPoint(e.GetPosition(this).Y);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
    }

    private void ScrollToPoint(double y)
    {
        var trackTop = _trackRect.IsEmpty ? 0 : _trackRect.Top;
        var trackHeight = _trackRect.IsEmpty ? ActualHeight : _trackRect.Height;
        if (_editor is null || trackHeight <= 0)
        {
            return;
        }

        var fraction = Math.Clamp((y - trackTop) / trackHeight, 0, 1);
        _editor.ScrollToVerticalOffset(fraction * _editor.ExtentHeight - _editor.ViewportHeight / 2);
    }

    private static Brush BrushFor(DiffKind kind) => kind switch
    {
        DiffKind.Added => AddedBrush,
        DiffKind.Removed => RemovedBrush,
        _ => ModifiedBrush
    };
}
