using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ParadoxModTool.VanillaDiff;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Zeilen markieren im Compare und Update Tool (Nutzerwunsch 2026-09-30, wie im Komp-Patch-Merge): Ziehen mit gedrückter linker
/// Maustaste über die Zeilennummern markiert ganze Zeilen; Ziehen im Text bleibt die normale Textauswahl. Rechtsklick auf die
/// Zeilennummern bietet Übernehmen und Aufheben an. Die Markierung zeichnet <see cref="LineMarkRenderer"/> (Theme-Farbe DiffMark).
/// </summary>
internal sealed class LineMarkController
{
    private readonly TextEditor _editor;
    private readonly LineMarkRenderer _renderer;
    private readonly FrameworkElement? _margin;
    private readonly MenuItem _takeItem = new();
    private readonly MenuItem _clearItem = new();
    private bool _dragging;

    public LineMarkController(TextEditor editor, AlignmentSpacerGenerator spacers)
    {
        _editor = editor;
        _renderer = new LineMarkRenderer(spacers);
        editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
        _margin = editor.TextArea.LeftMargins.OfType<LineNumberMargin>().FirstOrDefault();
        if (_margin is null)
        {
            return;
        }

        _margin.PreviewMouseLeftButtonDown += OnMouseDown;
        _margin.PreviewMouseMove += OnMouseMove;
        _margin.PreviewMouseLeftButtonUp += OnMouseUp;
        _takeItem.Header = MiscWindowTexts.DiffTakeMenu;
        _takeItem.InputGestureText = "U";
        _takeItem.Click += (_, _) => TakeRequested?.Invoke(this, EventArgs.Empty);
        _clearItem.Header = MiscWindowTexts.DiffClearMarksMenu;
        _clearItem.InputGestureText = "Esc";
        _clearItem.Click += (_, _) => Clear();
        var menu = new ContextMenu();
        menu.Items.Add(_takeItem);
        menu.Items.Add(_clearItem);
        menu.Opened += (_, _) =>
        {
            _takeItem.IsEnabled = Marks is not null;
            _clearItem.IsEnabled = Marks is not null;
        };
        _margin.ContextMenu = menu;
    }

    /// <summary>Markierte Zeilen dieser Seite oder <see langword="null"/>.</summary>
    public LineMarks? Marks { get; private set; }

    /// <summary>Der Nutzer hat eine neue Markierung begonnen (die andere Seite hebt ihre auf).</summary>
    public event EventHandler? MarkingStarted;

    /// <summary>Rechtsklick → "Markierte Zeilen übernehmen".</summary>
    public event EventHandler? TakeRequested;

    public void Clear()
    {
        if (Marks is null)
        {
            return;
        }

        Marks = null;
        Redraw();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (LineAt(e) is not { } line)
        {
            return;
        }

        Marks = new LineMarks(line, line);

        // Tastaturfokus in den Editor, damit U und Esc ankommen (stand er in einem Eingabefeld, würde U dort getippt).
        _editor.TextArea.Focus();
        _dragging = _margin!.CaptureMouse();
        MarkingStarted?.Invoke(this, EventArgs.Empty);
        Redraw();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || Marks is not { } marks || LineAt(e) is not { } line || line == marks.Current)
        {
            return;
        }

        Marks = marks with { Current = line };
        Redraw();
        e.Handled = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        _margin!.ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>Dokumentzeile unter dem Mauszeiger; oberhalb bzw. unterhalb des Texts die erste bzw. letzte Zeile.</summary>
    private int? LineAt(MouseEventArgs e)
    {
        var textView = _editor.TextArea.TextView;
        if (!textView.VisualLinesValid || _editor.Document.LineCount == 0)
        {
            return null;
        }

        var y = e.GetPosition(textView).Y + textView.VerticalOffset;
        if (textView.GetVisualLineFromVisualTop(Math.Max(0, y)) is { } visualLine)
        {
            return visualLine.FirstDocumentLine.LineNumber;
        }

        return y <= 0 ? 1 : _editor.Document.LineCount;
    }

    private void Redraw()
    {
        _renderer.Marks = Marks;
        _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }
}

/// <summary>Hintergrund der markierten Zeilen, unter der Diff-Färbung durchscheinend (Theme-Farbe DiffMark); Lücken bleiben frei.</summary>
internal sealed class LineMarkRenderer : IBackgroundRenderer
{
    private readonly AlignmentSpacerGenerator _spacers;

    public LineMarkRenderer(AlignmentSpacerGenerator spacers) => _spacers = spacers;

    public LineMarks? Marks { get; set; }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Marks is not { } marks || !textView.VisualLinesValid)
        {
            return;
        }

        var brush = new SolidColorBrush(ThemeManager.GetColor("DiffMark"));
        brush.Freeze();
        foreach (var visualLine in textView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine;
            if (!marks.Contains(line.LineNumber))
            {
                continue;
            }

            var gapHeight = _spacers.RowsBefore(line) * textView.DefaultLineHeight;
            var top = visualLine.VisualTop - textView.VerticalOffset + gapHeight;
            var height = Math.Max(0, visualLine.Height - gapHeight);
            drawingContext.DrawRectangle(brush, null, new Rect(0, top, textView.ActualWidth, height));
        }
    }
}
