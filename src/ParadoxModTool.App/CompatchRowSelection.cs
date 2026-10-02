using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Zeilen markieren und übernehmen (Nutzerwunsch) in einer Spaltenliste des Kompatch-Merges (Vergleichsfenster, Ansicht A): mit gedrückter linker
/// Maustaste Zeilen einer Spalte markieren, Rechtsklick-Menü oder Taste U übernimmt genau diese Zeilen. Die Liste besteht aus je einem
/// <see cref="Controls.Border"/> pro Zeile mit dem Zeilen-Grid von <see cref="CompatchWindow.RowGrid"/>.
/// </summary>
internal sealed class CompatchRowSelection
{
    private readonly Controls.Panel _rows;
    private readonly Func<IReadOnlyList<AlignedRow>> _shown;
    private readonly Func<bool> _enabled;
    private readonly Func<bool> _showVanilla;
    private readonly Func<CompatchColumn, string> _sourceName;
    private readonly Func<CompatchColumn, int, int, string?> _take;
    private readonly Action<string> _report;
    private readonly List<(Controls.Border Cell, System.Windows.Media.Brush Background)> _painted = new();
    private bool _selecting;
    private int _column = -1;
    private int _from = -1;
    private int _to = -1;

    /// <param name="take">Übernimmt die Zeilen <c>from</c> bis <c>to</c> aus der Spalte; liefert den Text für die Statuszeile (übernommen, unverändert, Fehler).</param>
    public CompatchRowSelection(Controls.Panel rows, Func<IReadOnlyList<AlignedRow>> shown, Func<bool> enabled, Func<bool> showVanilla,
        Func<CompatchColumn, string> sourceName, Func<CompatchColumn, int, int, string?> take, Action<string> report)
    {
        _rows = rows;
        _shown = shown;
        _enabled = enabled;
        _showVanilla = showVanilla;
        _sourceName = sourceName;
        _take = take;
        _report = report;
        rows.Background ??= System.Windows.Media.Brushes.Transparent;
        rows.PreviewMouseLeftButtonDown += Start;
        rows.MouseMove += Extend;
        rows.PreviewMouseLeftButtonUp += (_, _) => _selecting = false;
        rows.MouseRightButtonUp += ShowMenu;
    }

    /// <summary>Nach dem Neuaufbau der Zeilen: Markierung vergessen (die alten Zellen gibt es nicht mehr).</summary>
    public void Reset()
    {
        _painted.Clear();
        _selecting = false;
        _column = _from = _to = -1;
    }

    private bool CanSelect => _enabled() && _shown().Count > 0;

    /// <summary>Übernimmt die Markierung (Menü oder Taste U); ohne Markierung oder in der Ergebnis-Spalte nur ein Hinweis.</summary>
    public void Take()
    {
        var column = SelectedColumn();
        if (!CanSelect || _from < 0 || column == CompatchColumn.Result)
        {
            _report(CompatchTexts.NothingMarked);
            return;
        }

        _report(_take(column, _from, _to) ?? CompatchTexts.NothingMarked);
    }

    private CompatchColumn SelectedColumn()
    {
        var order = CompatchColumns.Order(_showVanilla(), CompatchWindow.ResultRight);
        return _column >= 0 && _column < order.Count ? order[_column] : CompatchColumn.Result;
    }

    /// <summary>Zeile und Spalte (Grid-Spalte) der Zelle unter <paramref name="source"/>; <see langword="null"/> außerhalb der Zeilen.</summary>
    private (int Row, int Column)? CellAt(DependencyObject? source)
    {
        for (var node = source; node is not null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is Controls.Border cell && System.Windows.Media.VisualTreeHelper.GetParent(cell) is Controls.Grid grid
                && System.Windows.Media.VisualTreeHelper.GetParent(grid) is Controls.Border row && _rows.Children.Contains(row))
            {
                return (_rows.Children.IndexOf(row), Controls.Grid.GetColumn(cell));
            }
        }

        return null;
    }

    private void Start(object sender, MouseButtonEventArgs e)
    {
        if (!CanSelect || CellAt(e.OriginalSource as DependencyObject) is not { } cell)
        {
            return;
        }

        _selecting = true;
        _column = cell.Column;
        _from = _to = cell.Row;
        Paint();
        TakeFocus();
    }

    /// <summary>
    /// Holt den Tastaturfokus aus einem Eingabefeld auf die Zeilenliste (genauer ihren Bildlaufbereich, damit nichts springt). Die Zeilen sind
    /// nicht fokussierbar; stand der Fokus noch in der Suche, ignorierte das Merge-Fenster U absichtlich, und das U landete in der Suche
    /// (Nutzertest 2026-10-01: in der Vorschau von Ansicht A ging U nie, im Vergleichsfenster ohne Eingabefeld schon).
    /// </summary>
    private void TakeFocus()
    {
        if (Keyboard.FocusedElement is not Controls.Primitives.TextBoxBase)
        {
            return;
        }

        var host = _rows.Parent as Controls.ScrollViewer;
        UIElement target = host is not null ? host : _rows;
        if (target is FrameworkElement element)
        {
            element.FocusVisualStyle = null;
        }

        target.Focusable = true;
        target.Focus();
    }

    private void Extend(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_selecting || e.LeftButton != MouseButtonState.Pressed)
        {
            _selecting = false;
            return;
        }

        if (CellAt(_rows.InputHitTest(e.GetPosition(_rows)) as DependencyObject) is { } cell && cell.Row != _to)
        {
            _to = cell.Row;
            Paint();
        }
    }

    /// <summary>Färbt die markierten Zellen der gewählten Spalte; die vorige Markierung bekommt ihren Hintergrund zurück.</summary>
    private void Paint()
    {
        foreach (var (cell, background) in _painted)
        {
            cell.Background = background;
        }

        _painted.Clear();
        if (_column < 0)
        {
            return;
        }

        var brush = CompatchWindow.Tint("Selection");
        for (var i = Math.Min(_from, _to); i <= Math.Max(_from, _to) && i < _rows.Children.Count; i++)
        {
            if (_rows.Children[i] is Controls.Border { Child: Controls.Grid grid }
                && grid.Children.OfType<Controls.Border>().FirstOrDefault(c => Controls.Grid.GetColumn(c) == _column) is { } cell)
            {
                _painted.Add((cell, cell.Background));
                cell.Background = brush;
            }
        }
    }

    /// <summary>Rechtsklick: Menü "Markierte Zeilen aus … übernehmen" (U). Ein Rechtsklick außerhalb der Markierung markiert die Zeile darunter.</summary>
    private void ShowMenu(object sender, MouseButtonEventArgs e)
    {
        if (!CanSelect || CellAt(e.OriginalSource as DependencyObject) is not { } clicked)
        {
            return;
        }

        if (clicked.Column != _column || clicked.Row < Math.Min(_from, _to) || clicked.Row > Math.Max(_from, _to))
        {
            _column = clicked.Column;
            _from = _to = clicked.Row;
            Paint();
        }

        TakeFocus();

        var column = SelectedColumn();
        var item = new Controls.MenuItem { ToolTip = CompatchTexts.TakeRowsTip, InputGestureText = "U" };
        if (column == CompatchColumn.Result)
        {
            item.Header = CompatchTexts.TakeRowsFromResult;
            item.IsEnabled = false;
        }
        else
        {
            item.Header = CompatchTexts.TakeRows(Math.Abs(_to - _from) + 1, _sourceName(column));
            item.Click += (_, _) => Take();
        }

        var menu = new Controls.ContextMenu { PlacementTarget = _rows };
        menu.Items.Add(item);
        menu.IsOpen = true;
        e.Handled = true;
    }
}
