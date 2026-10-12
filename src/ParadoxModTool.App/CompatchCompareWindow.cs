using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;
using MergeAction = ParadoxModTool.Compatch.MergeAction;

namespace ParadoxModTool.App;

/// <summary>
/// Vergleichsfenster (Spec Nachtrag 2): ganzer Codeblock einer Adresse in den Spalten Mod A | Ergebnis | Mod B, Vanilla bzw. Basis zuschaltbar.
/// Nur lesend, nicht modal. Das Merge-Fenster ruft <see cref="Refresh"/> nach jeder Änderung und schließt es nach einem neuen Lauf.
/// Springt beim Öffnen zur Stelle der Aktion; Übersichtsleiste, "Vorige/Nächste Abweichung" und F7 / Umschalt+F7 führen durch lange Blöcke.
/// </summary>
public sealed class CompatchCompareWindow : Window
{
    private const int MaxRows = 5000;
    private readonly CompatchWindow _owner;
    private readonly MergeAction _action;
    private readonly Controls.CheckBox _vanilla = new() { Content = new Controls.TextBlock { Text = CompatchTexts.VanillaColumn }, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.CheckBox _hideComments = new() { Content = new Controls.TextBlock { Text = CompatchTexts.HideComments }, ToolTip = CompatchTexts.HideCommentsTip, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.TextBlock _status = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly RowOverviewBar _overview = new() { Margin = new Thickness(4, 0, 0, 0) };

    // Rechter Rand = Bildlaufleiste der Zeilen plus Übersichtsleiste, damit Titel und Spalten gleich breit sind.
    private readonly Controls.Grid _titles = new() { Margin = new Thickness(0, 0, SystemParameters.VerticalScrollBarWidth + 18, 2) };
    private readonly Controls.StackPanel _rows = new();
    private readonly Controls.ScrollViewer _scroll;
    private IReadOnlyList<AlignedRow> _shown = Array.Empty<AlignedRow>();
    private IReadOnlyList<int> _stops = Array.Empty<int>();

    /// <summary>"Ganze Datei zeigen" ist an (nur bei einem Schlüssel mit Form "Ganze Datei"); dann ist Zeilen-Übernehmen aus.</summary>
    private bool _keyFile;

    private readonly Controls.CheckBox _showWholeFile = new() { Content = new Controls.TextBlock { Text = CompatchTexts.ShowWholeFile }, ToolTip = CompatchTexts.ShowWholeFileTip, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    private int _current = -1;
    private bool _jumped;
    private bool _showVanilla;
    private readonly CompatchRowSelection _selection;

    public CompatchCompareWindow(CompatchWindow owner, MergeAction action)
    {
        _owner = owner;
        _action = action;
        Owner = owner;
        Title = CompatchTexts.CompareTitle(action.Address.Kind == AddressKind.Key ? action.Address.Key : action.Address.Scope, TreePath.FileOf(action));
        Width = 1100;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        var top = new Controls.StackPanel();
        Controls.DockPanel.SetDock(top, Controls.Dock.Top);
        top.Children.Add(_summary);
        var bar = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        bar.Children.Add(NavButton(CompatchTexts.PreviousDifference, CompatchTexts.PreviousDifferenceTip, () => Jump(forward: false)));
        bar.Children.Add(NavButton(CompatchTexts.NextDifference, CompatchTexts.NextDifferenceTip, () => Jump(forward: true)));
        bar.Children.Add(_showWholeFile);
        _showWholeFile.Click += (_, _) => Refresh();
        bar.Children.Add(_vanilla);
        bar.Children.Add(_hideComments);
        _hideComments.Click += (_, _) => _owner.SetHideComments(_hideComments.IsChecked == true);
        bar.Children.Add(CompatchLegend.Build(owner.ModAName, owner.ModBName));
        top.Children.Add(bar);
        _status.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        top.Children.Add(_status);
        top.Children.Add(_titles);
        root.Children.Add(top);

        // Kein waagrechtes Scrollen: lange Zeilen brechen um, damit jede Spalte in allen Zeilen gleich breit bleibt.
        _scroll = new Controls.ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Visible };
        var body = new Controls.Grid();
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });
        body.Children.Add(_scroll);
        Controls.Grid.SetColumn(_overview, 1);
        body.Children.Add(_overview);
        root.Children.Add(body);
        Content = root;

        _overview.RowClicked += (_, index) => ScrollTo(index);
        PreviewKeyDown += OnKeyDown;
        _selection = new CompatchRowSelection(
            _rows,
            () => _shown,
            () => _action.Address.Kind != AddressKind.WholeFile && !_keyFile,
            () => _showVanilla,
            column => owner.SourceName(column),
            (column, from, to) => _owner.TakeBlockText(_action.Address, CompareNavigation.TakeRows(_shown, from, to, column), column),
            text => _status.Text = text);
        _vanilla.Click += (_, _) => Refresh();
        Refresh();
    }

    public MergeAction Action => _action;

    /// <summary>Neu zeichnen aus dem aktuellen Stand der Sitzung (Entscheidungen im Merge-Fenster).</summary>
    public void Refresh()
    {
        _rows.Children.Clear();
        _selection.Reset();
        _titles.Children.Clear();
        _titles.ColumnDefinitions.Clear();
        _shown = Array.Empty<AlignedRow>();
        _stops = Array.Empty<int>();
        _overview.Update(_shown, false);
        if (_owner.Session is not { } session || !session.Run.Resolved.Addresses.TryGetValue(_action.Address, out var info))
        {
            return;
        }

        var current = session.Actions.FirstOrDefault(a => a.Id == _action.Id) ?? _action;
        _summary.Text = $"{current.DisplayPath}   [{TreePath.FileOf(current)}]\n" + CompatchWindow.Summary(session, current, _owner.ModAName, _owner.ModBName);
        var wholeFile = _action.Address.Kind == AddressKind.WholeFile;
        _showWholeFile.Visibility = _owner.IsWholeFileForm(current) ? Visibility.Visible : Visibility.Collapsed;
        _keyFile = _showWholeFile.Visibility == Visibility.Visible && _showWholeFile.IsChecked == true;
        // In A und B entfernt: der Text steht nur noch in Vanilla, also diese Spalte zeigen.
        var removed = CompatchWindow.RemovedInBoth(current);
        var showVanilla = _vanilla.IsChecked == true || removed;
        if (removed)
        {
            _summary.Text += "\n" + CompatchTexts.RemovedInBoth(_owner.BaseColumnTitle);
        }

        _showVanilla = showVanilla;
        var hide = _owner.HideComments;
        _hideComments.IsChecked = hide;
        session.Run.Applied.Desired.TryGetValue(_action.Address, out var desired);
        var open = desired?.OpenConflicts.Count > 0;
        var rows = wholeFile
            ? ActionText.WholeFileRows(info, session.EffectiveDecision(current), showVanilla)
            : _keyFile
                ? ActionText.KeyFileRows(info, _owner.PreviewWrite(), showVanilla)
            : EntryAligner.Align(
                info.A.Entries,
                desired?.Entries ?? info.NoPatch.Entries,
                info.B.Entries,
                showVanilla ? MergeBasis.Entries(info, session.Input) : null,
                hide);

        CompatchWindow.AddColumnTitles(_titles, _owner.ModAName, _owner.ModBName, showVanilla ? _owner.BaseColumnTitle : null, FontWeights.Bold);

        _shown = rows.Take(MaxRows).ToList();
        _stops = CompareNavigation.Stops(_shown);
        foreach (var row in _shown)
        {
            // Rahmen immer da (durchsichtig), damit das Hervorheben der Stelle die Zeilen nicht verschiebt.
            _rows.Children.Add(new Controls.Border { Child = CompatchWindow.RowGrid(row, showVanilla, open), BorderThickness = new Thickness(1), BorderBrush = System.Windows.Media.Brushes.Transparent });
        }

        _overview.Update(_shown, open);
        _status.Text = CompatchTexts.DifferenceCount(_stops.Count);
        if (!_jumped)
        {
            _jumped = true;
            // Eine ganze Datei hat keine eigene Stelle: zur ersten Abweichung.
            var target = wholeFile
                ? _stops.Count > 0 ? _stops[0] : (int?)null
                : CompareNavigation.RowOf(_shown, ActionText.Rows(current, session.EffectiveDecision(current), withVanilla: false, hide));
            if (target is { } index)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => ScrollTo(index));
            }
        }
    }

    private Controls.Button NavButton(string text, string tip, Action click)
    {
        var button = new Controls.Button { Content = new Controls.TextBlock { Text = text }, ToolTip = tip, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 0) };
        button.Click += (_, _) => click();
        return button;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F7 && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            Jump(forward: Keyboard.Modifiers == ModifierKeys.None);
            e.Handled = true;
        }
        else if (e.Key == Key.U && Keyboard.Modifiers == ModifierKeys.None)
        {
            _selection.Take();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _status.Text = _owner.UndoTake() ? CompatchTexts.TakeUndone : CompatchTexts.NothingToUndo;
            e.Handled = true;
        }
    }

    private void Jump(bool forward)
    {
        if (CompareNavigation.Next(_stops, _current, forward) is { } index)
        {
            ScrollTo(index);
        }
        else
        {
            _status.Text = CompatchTexts.NoFurtherDifference;
        }
    }

    /// <summary>Holt die Zeile nach oben in den sichtbaren Bereich und hebt sie hervor.</summary>
    private void ScrollTo(int index)
    {
        if (index < 0 || index >= _rows.Children.Count)
        {
            return;
        }

        if (_current >= 0 && _current < _rows.Children.Count && _rows.Children[_current] is Controls.Border previous)
        {
            previous.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }

        _current = index;
        var row = (Controls.Border)_rows.Children[index];
        row.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.Warning");
        _rows.UpdateLayout();
        var top = row.TranslatePoint(new System.Windows.Point(0, 0), _rows).Y;
        _scroll.ScrollToVerticalOffset(Math.Max(0, top - 24));
        _status.Text = CompatchTexts.DifferencePosition(_stops.Count(s => s <= index), _stops.Count);
    }
}
