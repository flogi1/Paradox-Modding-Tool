using System.Windows;
using ParadoxModTool.Compatch;
using Controls = System.Windows.Controls;
using MergeAction = ParadoxModTool.Compatch.MergeAction;

namespace ParadoxModTool.App;

/// <summary>
/// Vergleichsfenster (Spec Nachtrag 2): ganzer Codeblock einer Adresse in den Spalten Mod A | Ergebnis | Mod B, Vanilla bzw. Basis zuschaltbar.
/// Nur lesend, nicht modal. Das Merge-Fenster ruft <see cref="Refresh"/> nach jeder Änderung und schließt es nach einem neuen Lauf.
/// </summary>
public sealed class CompatchCompareWindow : Window
{
    private const int MaxRows = 5000;
    private readonly CompatchWindow _owner;
    private readonly MergeAction _action;
    private readonly Controls.CheckBox _vanilla = new() { Content = CompatchTexts.VanillaColumn, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.CheckBox _hideComments = new() { Content = CompatchTexts.HideComments, ToolTip = CompatchTexts.HideCommentsTip, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private readonly Controls.TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.Grid _titles = new() { Margin = new Thickness(0, 0, 0, 2) };
    private readonly Controls.StackPanel _rows = new();

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
        bar.Children.Add(_vanilla);
        bar.Children.Add(_hideComments);
        _hideComments.Click += (_, _) => _owner.SetHideComments(_hideComments.IsChecked == true);
        bar.Children.Add(CompatchLegend.Build(owner.ModAName, owner.ModBName));
        top.Children.Add(bar);
        top.Children.Add(_titles);
        root.Children.Add(top);
        root.Children.Add(new Controls.ScrollViewer { Content = _rows, HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto });
        Content = root;
        _vanilla.Click += (_, _) => Refresh();
        Refresh();
    }

    public MergeAction Action => _action;

    /// <summary>Neu zeichnen aus dem aktuellen Stand der Sitzung (Entscheidungen im Merge-Fenster).</summary>
    public void Refresh()
    {
        _rows.Children.Clear();
        _titles.Children.Clear();
        _titles.ColumnDefinitions.Clear();
        if (_owner.Session is not { } session || !session.Run.Resolved.Addresses.TryGetValue(_action.Address, out var info))
        {
            return;
        }

        var current = session.Actions.FirstOrDefault(a => a.Id == _action.Id) ?? _action;
        _summary.Text = $"{current.DisplayPath}   [{TreePath.FileOf(current)}]\n" + CompatchWindow.Summary(session, current, _owner.ModAName, _owner.ModBName);
        var wholeFile = _action.Address.Kind == AddressKind.WholeFile;
        var showVanilla = _vanilla.IsChecked == true && !wholeFile;
        var hide = _owner.HideComments;
        _hideComments.IsChecked = hide;
        session.Run.Applied.Desired.TryGetValue(_action.Address, out var desired);
        var open = desired?.OpenConflicts.Count > 0;
        var rows = wholeFile
            ? ActionText.WholeFileRows(info, session.EffectiveDecision(current))
            : ColumnAligner.Align(
                ActionText.Render(info.A.Entries, hide),
                ActionText.Render(desired?.Entries ?? info.NoPatch.Entries, hide),
                ActionText.Render(info.B.Entries, hide),
                showVanilla ? ActionText.Render(MergeBasis.Entries(info, session.Input), hide) : null);

        var titles = new List<string> { CompatchTexts.ColumnA(_owner.ModAName), CompatchTexts.ColumnResult, CompatchTexts.ColumnB(_owner.ModBName) };
        if (showVanilla)
        {
            titles.Add(_owner.BaseColumnTitle);
        }

        for (var i = 0; i < titles.Count; i++)
        {
            _titles.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var title = new Controls.TextBlock { Text = titles[i], FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 4, 0) };
            Controls.Grid.SetColumn(title, i);
            _titles.Children.Add(title);
        }

        foreach (var row in rows.Take(MaxRows))
        {
            _rows.Children.Add(CompatchWindow.RowGrid(row, showVanilla, open));
        }
    }
}
