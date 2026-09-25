using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// "Wird angezeigt in (GUI)": Fundstellen eines Namens aus einer Skriptdatei in allen .gui-Dateien (Plan-Ruling 8). Doppelklick oder
/// "Öffnen" springt zur Stelle, "Inspizieren" zeigt das umschließende Element im GUI-Inspektor. Sucht im Hintergrund; wird das Fenster
/// geöffnet, während der GUI-Index noch lädt, sucht es erneut, sobald er da ist.
/// </summary>
public sealed class GuiShownInWindow : Window
{
    private sealed record Row(GuiReferenceHit Hit)
    {
        public string File => Hit.File.RelativePath;

        public int Line => Hit.Line;

        public string Element => Hit.Element is { } element ? GuiElementTree.LabelOf(element) : string.Empty;

        public string Text => Hit.LineText;

        public string Layer => Hit.File.Label;
    }

    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _hint = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 8) };
    private readonly ListView _list = new();
    private readonly Button _open = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 8, 0), IsEnabled = false };
    private readonly Button _inspect = new() { Padding = new Thickness(10, 2, 10, 2), IsEnabled = false };
    private GuiWorkspace? _workspace;
    private string? _word;
    private CancellationTokenSource? _run;

    public GuiShownInWindow()
    {
        Width = 1050;
        Height = 560;
        MinWidth = 520;
        MinHeight = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _hint.Text = GuiTexts.ShownInHint;
        _hint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _open.Content = GuiTexts.ShownInOpen;
        _open.ToolTip = GuiTexts.ShownInOpenTip;
        _open.Click += (_, _) => Open();
        _inspect.Content = GuiTexts.ContextInspect;
        _inspect.ToolTip = GuiTexts.ShownInInspectTip;
        _inspect.Click += (_, _) => Inspect();
        _list.MouseDoubleClick += (_, _) => Open();
        _list.SelectionChanged += (_, _) =>
        {
            var row = _list.SelectedItem as Row;
            _open.IsEnabled = row is not null;
            _inspect.IsEnabled = row?.Hit.Element is not null;
        };

        var columns = new GridView();
        columns.Columns.Add(Column(GuiTexts.ColumnFile, nameof(Row.File), 280));
        columns.Columns.Add(Column(GuiTexts.ColumnLine, nameof(Row.Line), 60));
        columns.Columns.Add(Column(GuiTexts.ColumnElement, nameof(Row.Element), 220));
        columns.Columns.Add(Column(GuiTexts.ColumnText, nameof(Row.Text), 360));
        columns.Columns.Add(Column(GuiTexts.ColumnLayer, nameof(Row.Layer), 90));
        _list.View = columns;

        var top = new StackPanel();
        top.Children.Add(_status);
        top.Children.Add(_hint);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(_open);
        buttons.Children.Add(_inspect);

        var root = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(_list);
        Content = root;

        GuiSettings.Changed += OnSettingsChanged;
        Closed += (_, _) =>
        {
            GuiSettings.Changed -= OnSettingsChanged;
            _run?.Cancel();
        };
    }

    public event EventHandler<GuiInspectTarget>? InspectRequested;

    public event EventHandler<GuiLocationEventArgs>? OpenLocationRequested;

    public void Search(string word)
    {
        _word = word;
        _run?.Cancel();
        Title = GuiTexts.ShownInTitle(word);
        _list.ItemsSource = null;
        var workspace = GuiSettings.Workspace;
        _workspace = workspace;
        if (word.Length < GuiReferenceSearch.MinimumLength)
        {
            _status.Text = GuiTexts.ShownInTooShort(GuiReferenceSearch.MinimumLength);
            return;
        }

        if (workspace is null)
        {
            _status.Text = GuiSettings.Status;
            return;
        }

        var run = new CancellationTokenSource();
        _run = run;
        _status.Text = GuiTexts.ShownInSearching(workspace.Index.Files.Count);
        var resolver = workspace.CreateResolver();
        Task.Run(() => GuiReferenceSearch.Find(workspace.Index, resolver, word, run.Token), run.Token).ContinueWith(
            task =>
            {
                if (run.IsCancellationRequested)
                {
                    return;
                }

                if (!task.IsCompletedSuccessfully)
                {
                    _status.Text = GuiTexts.SearchFailed(task.Exception?.GetBaseException().Message ?? string.Empty);
                    return;
                }

                var result = task.Result;
                _list.ItemsSource = result.Hits.Select(hit => new Row(hit)).ToList();
                _status.Text = result.Total == 0 ? GuiTexts.ShownInNone
                    : result.Total > result.Hits.Count ? GuiTexts.ShownInLimited(result.Total, result.Hits.Count)
                    : GuiTexts.ShownInResult(result.Total, result.FileCount);
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static GridViewColumn Column(string header, string path, double width) =>
        new() { Header = header, DisplayMemberBinding = new Binding(path), Width = width };

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_word is { } word && !ReferenceEquals(GuiSettings.Workspace, _workspace))
        {
            Search(word);
        }
    }

    private void Open()
    {
        if (_list.SelectedItem is Row row)
        {
            OpenLocationRequested?.Invoke(this, new GuiLocationEventArgs(row.Hit.File.FullPath, row.Line));
        }
    }

    private void Inspect()
    {
        if (_list.SelectedItem is Row { Hit.Element: { } element } row)
        {
            InspectRequested?.Invoke(this, new GuiInspectTarget(element, row.Hit.Document, row.Hit.File.FullPath));
        }
    }
}
