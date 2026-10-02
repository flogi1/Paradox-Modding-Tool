using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Compatch;
using Brush = System.Windows.Media.Brush;
using Cursors = System.Windows.Input.Cursors;
using DrawingContext = System.Windows.Media.DrawingContext;

namespace ParadoxModTool.App;

/// <summary>
/// Schmale Übersichtsleiste neben einer Zeilenliste des Kompatch-Vergleichs (wie <see cref="DiffOverviewBar"/> im C-U-Tool): je abweichender
/// Zeile eine Marke auf ihrer Höhe im ganzen Block, in der Farbe ihrer Herkunft. Ein Klick meldet die Zeile in <see cref="RowClicked"/>.
/// </summary>
public sealed class RowOverviewBar : FrameworkElement
{
    private IReadOnlyList<AlignedRow> _rows = Array.Empty<AlignedRow>();
    private bool _openConflict;

    public RowOverviewBar()
    {
        Width = 14;
        Cursor = Cursors.Hand;
        ToolTip = CompatchTexts.OverviewTip;
        EventHandler onThemeChanged = (_, _) => InvalidateVisual();
        Loaded += (_, _) => ThemeManager.ThemeChanged += onThemeChanged;
        Unloaded += (_, _) => ThemeManager.ThemeChanged -= onThemeChanged;
        MouseLeftButtonDown += OnClick;
    }

    /// <summary>Zeile (Index in den übergebenen Zeilen), auf deren Höhe geklickt wurde.</summary>
    public event EventHandler<int>? RowClicked;

    public void Update(IReadOnlyList<AlignedRow> rows, bool openConflict)
    {
        _rows = rows;
        _openConflict = openConflict;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(ThemeManager.GetBrush("OverviewTrack"), null, new Rect(0, 0, ActualWidth, ActualHeight));
        if (_rows.Count == 0 || ActualHeight <= 0)
        {
            return;
        }

        var rowHeight = ActualHeight / _rows.Count;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (!CompareNavigation.IsDifference(_rows[i]))
            {
                continue;
            }

            var height = Math.Max(2, rowHeight);
            drawingContext.DrawRectangle(BrushOf(_rows[i]), null, new Rect(1, Math.Min(i * rowHeight, ActualHeight - height), ActualWidth - 2, height));
        }
    }

    private Brush BrushOf(AlignedRow row) => row.Origin switch
    {
        RowOrigin.FromA => ThemeManager.GetBrush("CompatchA"),
        RowOrigin.FromB => ThemeManager.GetBrush("CompatchB"),
        RowOrigin.Other => ThemeManager.GetBrush(_openConflict ? "CompatchConflict" : "CompatchReview"),
        _ => ThemeManager.GetBrush("CompatchVanilla"),
    };

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        if (_rows.Count == 0 || ActualHeight <= 0)
        {
            return;
        }

        var index = (int)(e.GetPosition(this).Y / ActualHeight * _rows.Count);
        RowClicked?.Invoke(this, Math.Clamp(index, 0, _rows.Count - 1));
        e.Handled = true;
    }
}
