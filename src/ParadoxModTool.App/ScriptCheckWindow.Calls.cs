using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;
using Binding = System.Windows.Data.Binding;
using Controls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Wer ruft wen": links Suchfeld und passende Namen (genau, dann Teilstring, über alle Arten), rechts die Definitionen des gewählten Namens,
/// darunter Aufrufer und Gerufene. Doppelklick oder Enter öffnet die Stelle.
/// </summary>
internal sealed partial class ScriptCheckWindow
{
    private readonly Controls.TextBox _callsSearch = new() { VerticalContentAlignment = VerticalAlignment.Center, ToolTip = ScriptCheckTexts.SearchCallsTip };
    private readonly Controls.ListBox _namesList = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TextBlock _definitions = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Controls.TextBlock _callsNote = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.ListView _callersList = new() { SelectionMode = Controls.SelectionMode.Single };
    private readonly Controls.ListView _calleesList = new() { SelectionMode = Controls.SelectionMode.Single };
    private readonly Controls.TextBlock _callersHeader = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };
    private readonly Controls.TextBlock _calleesHeader = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) };
    private DispatcherTimer? _callsTimer;

    /// <summary>Zeigt <see cref="ShowCalls"/> gerade den gewählten Namen, liest SelectionChanged nicht mit.</summary>
    private bool _fillingNames;

    private string? _selectedName;

    private Controls.Grid BuildCallsTab()
    {
        _callsNote.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _callsTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Input, (_, _) => ShowCalls(), Dispatcher);
        _callsTimer.Stop();
        _callsSearch.TextChanged += (_, _) =>
        {
            _callsTimer.Stop();
            _callsTimer.Start();
        };
        _callsSearch.KeyDown += OnCallsSearchKeyDown;
        Controls.VirtualizingPanel.SetIsVirtualizing(_namesList, true);
        _namesList.SelectionChanged += (_, _) =>
        {
            if (!_fillingNames && _namesList.SelectedItem is string name)
            {
                _selectedName = name;
                ShowSelectedName();
            }
        };

        SetUpCallList(_callersList);
        SetUpCallList(_calleesList);

        var left = new Controls.DockPanel { Margin = new Thickness(0, 6, 8, 0) };
        Controls.DockPanel.SetDock(_callsSearch, Controls.Dock.Top);
        left.Children.Add(_callsSearch);
        left.Children.Add(_namesList);

        var lists = new Controls.Grid();
        lists.ColumnDefinitions.Add(new Controls.ColumnDefinition());
        lists.ColumnDefinitions.Add(new Controls.ColumnDefinition());
        var callers = CallPanel(_callersHeader, _callersList);
        callers.Margin = new Thickness(0, 0, 4, 0);
        var callees = CallPanel(_calleesHeader, _calleesList);
        callees.Margin = new Thickness(4, 0, 0, 0);
        Controls.Grid.SetColumn(callees, 1);
        lists.Children.Add(callers);
        lists.Children.Add(callees);

        var right = new Controls.DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        Controls.DockPanel.SetDock(_definitions, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_callsNote, Controls.Dock.Top);
        right.Children.Add(_definitions);
        right.Children.Add(_callsNote);
        right.Children.Add(lists);

        var grid = new Controls.Grid();
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(280) });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition());
        Controls.Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private static Controls.DockPanel CallPanel(Controls.TextBlock header, Controls.ListView list)
    {
        var panel = new Controls.DockPanel();
        Controls.DockPanel.SetDock(header, Controls.Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(list);
        return panel;
    }

    private void SetUpCallList(Controls.ListView list)
    {
        var grid = new Controls.GridView();
        grid.Columns.Add(Column(ScriptCheckTexts.ColName, 170, nameof(CallDisplay.Name)));
        grid.Columns.Add(Column(ScriptCheckTexts.ColKind, 90, nameof(CallDisplay.KindText)));
        grid.Columns.Add(Column(ScriptCheckTexts.ColPlace, 200, nameof(CallDisplay.Place)));
        grid.Columns.Add(Column(ScriptCheckTexts.ColConditional, 60, nameof(CallDisplay.ConditionalText)));
        list.View = grid;
        Controls.VirtualizingPanel.SetIsVirtualizing(list, true);
        Controls.VirtualizingPanel.SetVirtualizationMode(list, Controls.VirtualizationMode.Recycling);
        list.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && Controls.ItemsControl.ContainerFromElement(list, source) is Controls.ListViewItem)
            {
                OpenSelectedCall(list);
            }
        };
        list.KeyDown += OnCallListKeyDown;
    }

    /// <summary>Wechselt zum Reiter und zeigt <paramref name="name"/>.</summary>
    private void ShowCallsOf(string name)
    {
        _callsSearch.Text = name;
        _callsSearch.CaretIndex = name.Length;
        _selectedName = name;
        ShowCalls();
        _tabs.SelectedItem = _callsTab;
    }

    /// <summary>Andere Mod: Der gewählte Name gehört zur alten Auswahl.</summary>
    private void ClearCallSelection()
    {
        _selectedName = null;
        _callsSearch.Text = string.Empty;
        ShowCalls();
    }

    /// <summary>Füllt die Namensliste zum Suchtext und zeigt den gewählten Namen (ein genauer Treffer wird gewählt, wenn noch nichts passt).</summary>
    private void ShowCalls()
    {
        _callsTimer?.Stop();
        var names = UsableIndex?.SearchNames(_callsSearch.Text.Trim()) ?? Array.Empty<string>();
        if (_selectedName is null || !names.Contains(_selectedName))
        {
            _selectedName = names.FirstOrDefault(n => string.Equals(n, _callsSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        _fillingNames = true;
        _namesList.ItemsSource = names;
        _namesList.SelectedItem = _selectedName;
        _fillingNames = false;
        ShowSelectedName();
    }

    private void ShowSelectedName()
    {
        _definitions.Inlines.Clear();
        _callersHeader.Text = ScriptCheckTexts.Callers;
        _calleesHeader.Text = ScriptCheckTexts.Callees;
        if (UsableIndex is not { } index || _selectedName is not { } name)
        {
            _callersList.ItemsSource = null;
            _calleesList.ItemsSource = null;
            _callsNote.Visibility = Visibility.Collapsed;
            return;
        }

        var definitions = index.DefinitionsOf(name);
        _definitions.Inlines.Add(new Run(definitions.Count > 0 ? ScriptCheckTexts.Definitions + " " : ScriptCheckTexts.NoDefinition));
        foreach (var (file, definition) in definitions)
        {
            var location = file.File with { Line = definition.Line };
            var link = new Hyperlink(new Run(location.RelativePath + ":" + location.Line + " (" + file.OriginName + ")"));
            link.Click += (_, _) => OpenLocation(location);
            _definitions.Inlines.Add(link);
            _definitions.Inlines.Add(new Run("  "));
        }

        var view = ScriptCheckView.Calls(index, name);
        _callersList.ItemsSource = view.Callers.Select(r => CallDisplay.From(r, r.Owner ?? r.Location.RelativePath)).ToList();
        _calleesList.ItemsSource = view.Callees.Select(r => CallDisplay.From(r, r.Name)).ToList();
        if (view.Callers.Count == 0)
        {
            _callersHeader.Text = ScriptCheckTexts.Callers + ": " + ScriptCheckTexts.NoCallers;
        }

        if (view.Callees.Count == 0)
        {
            _calleesHeader.Text = ScriptCheckTexts.Callees + ": " + ScriptCheckTexts.NoCallees;
        }

        _callsNote.Text = ScriptCheckTexts.CallsOnlyEvents;
        _callsNote.Visibility = view.OnlyEventCallers ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenSelectedCall(Controls.ListView list)
    {
        if (list.SelectedItem is CallDisplay row)
        {
            OpenLocation(row.Location);
        }
    }

    private void OnCallListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && sender is Controls.ListView list)
        {
            OpenSelectedCall(list);
            e.Handled = true;
        }
    }

    /// <summary>Enter im Suchfeld sucht sofort, ohne die Wartezeit nach dem Tippen.</summary>
    private void OnCallsSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            ShowCalls();
            e.Handled = true;
        }
    }

    private void StopTimers()
    {
        _findingsTimer?.Stop();
        _callsTimer?.Stop();
        _perfTimer?.Stop();
    }

    /// <summary>Zeile der Listen "Aufrufer" und "Gerufene".</summary>
    private sealed record CallDisplay(string Name, string KindText, string Place, string ConditionalText, SiteLocation Location)
    {
        public static CallDisplay From(CallRow row, string name) => new(
            name,
            ScriptCheckTexts.NameKind(row.Kind),
            row.Location.RelativePath + ":" + row.Location.Line,
            row.Conditional ? ScriptCheckTexts.Yes : string.Empty,
            row.Location);
    }
}
