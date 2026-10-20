using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Suchfenster "GUI-Element inspizieren": alle Typen, Templates und benannten Widgets aus Spiel und Mod, dazu die unbenannten obersten
/// Fenster jeder Datei unter ihrem Dateinamen; filterbar nach Name, Dateipfad und Art (Plan-Rulings 6 und 7). Enter, Doppelklick oder
/// "Inspizieren" öffnet den Eintrag im GUI-Inspektor. Die Liste entsteht im Hintergrund und neu, sobald ein neuer GUI-Index da ist.
/// </summary>
public sealed class GuiElementSearchWindow : Window
{
    private sealed record FilterChoice(GuiElementFilter Filter)
    {
        public override string ToString() => GuiTexts.FilterText(Filter);
    }

    private sealed record Row(GuiElementEntry Entry)
    {
        public string Name => Entry.Name;

        public string KindText => Entry.IsUnnamed ? GuiTexts.KindUnnamedRoot : GuiTexts.KindOf(Entry.Kind);

        public string BaseWidget => GuiWidgetKinds.Normalize(Entry.BaseWidget);

        public string Location => $"{Entry.Definition.File.RelativePath}:{Entry.Definition.Line}";

        public string Layer => Entry.Definition.File.Label;
    }

    private readonly TextBox _search = new() { MinWidth = 240, Margin = new Thickness(0, 0, 12, 0) };
    private readonly TextBox _fileSearch = new() { MinWidth = 180, Margin = new Thickness(0, 0, 12, 0) };
    private readonly ComboBox _filter = new() { Width = 200 };
    private readonly ListView _list = new();
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _inspect = new() { Padding = new Thickness(10, 2, 10, 2), IsEnabled = false };
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private GuiWorkspace? _workspace;
    private ListCollectionView? _view;
    private int _total;
    private string _text = string.Empty;
    private string _fileText = string.Empty;
    private GuiElementFilter _filterValue = GuiElementFilter.All;
    private CancellationTokenSource? _run;

    public GuiElementSearchWindow()
    {
        Title = GuiTexts.SearchTitle;
        Width = 920;
        Height = 620;
        MinWidth = 520;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _filter.ItemsSource = Enum.GetValues<GuiElementFilter>().Select(filter => new FilterChoice(filter)).ToList();
        _filter.SelectedIndex = 0;
        _filter.SelectionChanged += (_, _) => ApplyFilter();
        _search.TextChanged += (_, _) => RestartFilterTimer();
        _fileSearch.TextChanged += (_, _) => RestartFilterTimer();
        _fileSearch.ToolTip = GuiTexts.SearchFileTip;
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            ApplyFilter();
        };
        // Enter und Pfeil runter wirken in beiden Suchfeldern gleich (HotkeyCatalog gui.inspect, gui.toList).
        _search.PreviewKeyDown += OnSearchKeyDown;
        _fileSearch.PreviewKeyDown += OnSearchKeyDown;
        void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InspectSelected(firstIfNone: true);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && _list.Items.Count > 0)
            {
                if (_list.SelectedIndex < 0)
                {
                    _list.SelectedIndex = 0;
                }

                _list.Focus();
                e.Handled = true;
            }
        }

        _list.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                InspectSelected(firstIfNone: false);
                e.Handled = true;
            }
        };
        _list.MouseDoubleClick += (_, _) => InspectSelected(firstIfNone: false);
        _list.SelectionChanged += (_, _) => _inspect.IsEnabled = _list.SelectedItem is Row;
        _inspect.Content = GuiTexts.ContextInspect;
        _inspect.ToolTip = GuiTexts.SearchInspectTip;
        _inspect.Click += (_, _) => InspectSelected(firstIfNone: false);

        var columns = new GridView();
        columns.Columns.Add(Column(GuiTexts.ColumnName, nameof(Row.Name), 260));
        columns.Columns.Add(Column(GuiTexts.ColumnKind, nameof(Row.KindText), 120));
        columns.Columns.Add(Column(GuiTexts.ColumnBaseWidget, nameof(Row.BaseWidget), 110));
        columns.Columns.Add(Column(GuiTexts.ColumnFile, nameof(Row.Location), 290));
        columns.Columns.Add(Column(GuiTexts.ColumnLayer, nameof(Row.Layer), 90));
        _list.View = columns;

        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(new TextBlock { Text = GuiTexts.SearchName, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        top.Children.Add(_search);
        top.Children.Add(new TextBlock { Text = GuiTexts.SearchFile, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        top.Children.Add(_fileSearch);
        top.Children.Add(new TextBlock { Text = GuiTexts.SearchKind, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        top.Children.Add(_filter);

        var bottom = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(_inspect, Dock.Right);
        bottom.Children.Add(_inspect);
        bottom.Children.Add(_count);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(_list);
        Content = root;

        Loaded += (_, _) =>
        {
            Reload();
            _search.Focus();
        };
        GuiSettings.Changed += OnSettingsChanged;
        Closed += (_, _) =>
        {
            GuiSettings.Changed -= OnSettingsChanged;
            _run?.Cancel();
            _filterTimer.Stop();
        };
    }

    public event EventHandler<GuiInspectTarget>? InspectRequested;

    private void RestartFilterTimer()
    {
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private static GridViewColumn Column(string header, string path, double width) =>
        new() { Header = header, DisplayMemberBinding = new Binding(path), Width = width };

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (IsLoaded && !ReferenceEquals(GuiSettings.Workspace, _workspace))
        {
            Reload();
        }
    }

    private void Reload()
    {
        _run?.Cancel();
        var workspace = GuiSettings.Workspace;
        _workspace = workspace;
        _view = null;
        _list.ItemsSource = null;
        if (workspace is null)
        {
            _count.Text = GuiSettings.Status;
            return;
        }

        var run = new CancellationTokenSource();
        _run = run;
        _count.Text = GuiTexts.SearchBuilding;
        var resolver = workspace.CreateResolver();
        Task.Run(() => GuiElementSearch.Entries(workspace.Index, resolver, run.Token), run.Token).ContinueWith(
            task =>
            {
                if (run.IsCancellationRequested)
                {
                    return;
                }

                if (!task.IsCompletedSuccessfully)
                {
                    _count.Text = GuiTexts.SearchFailed(task.Exception?.GetBaseException().Message ?? string.Empty);
                    return;
                }

                var rows = task.Result.Select(entry => new Row(entry)).ToList();
                _total = rows.Count;
                _view = new ListCollectionView(rows) { Filter = Accept };
                _list.ItemsSource = _view;
                ApplyFilter();
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ApplyFilter()
    {
        _text = _search.Text.Trim();
        _fileText = _fileSearch.Text.Trim();
        _filterValue = (_filter.SelectedItem as FilterChoice)?.Filter ?? GuiElementFilter.All;
        if (_view is null)
        {
            return;
        }

        _view.Refresh();
        _count.Text = GuiTexts.SearchCount(_view.Count, _total);
    }

    private bool Accept(object item) => item is Row row && GuiElementSearch.Matches(row.Entry, _text, _filterValue, _fileText);

    private void InspectSelected(bool firstIfNone)
    {
        var row = _list.SelectedItem as Row ?? (firstIfNone && _view is { Count: > 0 } view ? view.GetItemAt(0) as Row : null);
        if (row is not null && _workspace is { } workspace)
        {
            InspectRequested?.Invoke(this, GuiInspection.TargetFor(workspace.Index, row.Entry.Definition));
        }
    }
}
