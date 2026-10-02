using System.Windows;
using System.Windows.Input;
using ParadoxModTool.Notes;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Bugtracker (nicht modal, eines je Hauptfenster): alle Meldungen eines Projekts mit Filtern, links die Liste mit Schnellknöpfen
/// (Erledigt, Verwerfen, Wieder öffnen; Entf = Verwerfen), rechts die Details mit Bezügen. Liest bei <see cref="Reload"/> frisch von der Platte;
/// jede Änderung geht über <see cref="IBugTrackerHost.ChangeBugs"/>, das danach auch dieses Fenster neu lädt.
/// </summary>
internal sealed partial class BugTrackerWindow : Window
{
    // Filter bleiben für die Programmsitzung erhalten, auch wenn das Fenster geschlossen und wieder geöffnet wird.
    private static bool s_open = true;
    private static bool s_done;
    private static bool s_rejected;
    private static bool s_onlyNew;
    private static bool s_withoutLink;
    private static int s_sourceIndex;

    private static readonly BugSource?[] SourceChoices = { null, BugSource.Local, BugSource.GitHub, BugSource.Discord };

    private readonly IBugTrackerHost _host;
    private readonly Controls.ComboBox _projectBox = new() { MinWidth = 220, Margin = new Thickness(6, 0, 12, 0) };
    private readonly Controls.CheckBox _openBox = new() { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _doneBox = new() { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _rejectedBox = new() { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.ComboBox _sourceBox = new() { Width = 130, Margin = new Thickness(0, 0, 12, 0) };
    private readonly Controls.CheckBox _onlyNewBox = new() { Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _withoutLinkBox = new() { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBox _searchBox = new() { Width = 180, VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.ListBox _list = new() { HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch };
    private readonly Controls.TextBlock _emptyText = new() { Margin = new Thickness(4) };
    private readonly Controls.Button _sourcesButton = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
    private readonly Controls.TextBlock _errorText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private BugData _data = new();
    private string? _selectedId;
    private bool _filling;

    public BugTrackerWindow(IBugTrackerHost host)
    {
        _host = host;
        Title = BugTexts.WindowTitle;
        Width = 1000;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Controls.ScrollViewer.SetHorizontalScrollBarVisibility(_list, Controls.ScrollBarVisibility.Disabled);

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        var header = BuildHeader();
        Controls.DockPanel.SetDock(header, Controls.Dock.Top);
        root.Children.Add(header);
        var filters = BuildFilters();
        Controls.DockPanel.SetDock(filters, Controls.Dock.Top);
        root.Children.Add(filters);
        _errorText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        Controls.DockPanel.SetDock(_errorText, Controls.Dock.Top);
        root.Children.Add(_errorText);

        var body = new Controls.Grid();
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(6) });
        body.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        var listHost = new Controls.Grid();
        listHost.Children.Add(_list);
        _emptyText.Text = BugTexts.WindowEmpty;
        _emptyText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        listHost.Children.Add(_emptyText);
        body.Children.Add(listHost);
        var splitter = new Controls.GridSplitter { Width = 6, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        Controls.Grid.SetColumn(splitter, 1);
        body.Children.Add(splitter);
        var detail = BuildDetail();
        Controls.Grid.SetColumn(detail, 2);
        body.Children.Add(detail);
        root.Children.Add(body);
        Content = root;

        _list.SelectionChanged += (_, _) => OnSelectionChanged();
        _list.KeyDown += List_KeyDown;

        FillProjects(_host.CurrentProjectId);
        Reload();
    }

    /// <summary>Id des gewählten Projekts; <see langword="null"/> = "Ohne Projekt".</summary>
    private string? SelectedProjectId => (_projectBox.SelectedItem as Controls.ComboBoxItem)?.Tag as string;

    private BugFilter CurrentFilter => new(
        _openBox.IsChecked == true,
        _doneBox.IsChecked == true,
        _rejectedBox.IsChecked == true,
        SourceChoices[Math.Max(0, _sourceBox.SelectedIndex)],
        _onlyNewBox.IsChecked == true,
        _withoutLinkBox.IsChecked == true,
        _searchBox.Text);

    /// <summary>Liest alle Meldungen frisch und baut Liste und Details neu; die Auswahl bleibt erhalten, solange die Meldung sichtbar ist.</summary>
    public void Reload()
    {
        _data = BugReportStore.Load(BugReportStore.GetDefaultPath());
        RebuildList();
    }

    /// <summary>Stellt den Bugtracker auf ein Projekt (Übersicht "Meine Mods"); unbekannte Projekte ändern nichts.</summary>
    public void SelectProject(string projectId)
    {
        if (projectId == SelectedProjectId || _host.ProjectChoices().All(choice => choice.Id != projectId))
        {
            return;
        }

        FillProjects(projectId);
        RebuildList();
    }

    /// <summary>Wählt eine Meldung aus; wechselt dazu ins Projekt der Meldung und blendet Filter aus, die sie verstecken.</summary>
    public void SelectReport(string id)
    {
        if (BugEditing.Find(_data, id) is not { } report)
        {
            return;
        }

        var projectId = report.ModId is null ? null : _host.BugProjects.RootOf(report.ModId);
        if (projectId != SelectedProjectId)
        {
            FillProjects(projectId);
        }

        _filling = true;
        _openBox.IsChecked = _doneBox.IsChecked = _rejectedBox.IsChecked = true;
        _sourceBox.SelectedIndex = 0;
        _onlyNewBox.IsChecked = _withoutLinkBox.IsChecked = false;
        _searchBox.Text = string.Empty;
        _filling = false;
        _selectedId = id;
        SaveFilters();
        RebuildList();
    }

    private Controls.DockPanel BuildHeader()
    {
        var header = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var refresh = new Controls.Button { Content = BugTexts.WindowRefresh, ToolTip = BugTexts.WindowRefreshTip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
        var newBug = new Controls.Button { Content = BugTexts.WindowNewBug, ToolTip = BugTexts.WindowNewBugTip, Padding = new Thickness(8, 2, 8, 2) };
        newBug.Click += (_, _) => CreateBugWithoutLink();
        refresh.Click += async (_, _) =>
        {
            if (SelectedProjectId is { } projectId && _host.HasBugSources(projectId))
            {
                await _host.RefreshBugsAsync(projectId);
            }
            else
            {
                Reload();
            }
        };
        _sourcesButton.Content = BugTexts.WindowSources;
        _sourcesButton.ToolTip = BugTexts.WindowSourcesTip;
        _sourcesButton.Click += (_, _) =>
        {
            if (SelectedProjectId is { } projectId)
            {
                _host.OpenBugSources(projectId);
            }
        };
        var right = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        right.Children.Add(_sourcesButton);
        right.Children.Add(refresh);
        right.Children.Add(newBug);
        Controls.DockPanel.SetDock(right, Controls.Dock.Right);
        header.Children.Add(right);

        var left = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        left.Children.Add(new Controls.TextBlock { Text = BugTexts.WindowProject, VerticalAlignment = VerticalAlignment.Center });
        left.Children.Add(_projectBox);
        _projectBox.SelectionChanged += (_, _) =>
        {
            if (!_filling)
            {
                _selectedId = null;
                RebuildList();
                if (SelectedProjectId is { } projectId)
                {
                    _host.RefreshBugsInBackground(projectId);
                }
            }
        };
        header.Children.Add(left);
        return header;
    }

    private Controls.WrapPanel BuildFilters()
    {
        var filters = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        _openBox.Content = BugTexts.Status(BugStatus.Open);
        _doneBox.Content = BugTexts.Status(BugStatus.Done);
        _rejectedBox.Content = BugTexts.Status(BugStatus.Rejected);
        _onlyNewBox.Content = BugTexts.WindowOnlyNew;
        _withoutLinkBox.Content = BugTexts.WindowWithoutLink;
        _sourceBox.Items.Add(BugTexts.WindowAllSources);
        _sourceBox.Items.Add(BugTexts.Source(BugSource.Local));
        _sourceBox.Items.Add(BugTexts.Source(BugSource.GitHub));
        _sourceBox.Items.Add(BugTexts.Source(BugSource.Discord));

        _openBox.IsChecked = s_open;
        _doneBox.IsChecked = s_done;
        _rejectedBox.IsChecked = s_rejected;
        _onlyNewBox.IsChecked = s_onlyNew;
        _withoutLinkBox.IsChecked = s_withoutLink;
        _sourceBox.SelectedIndex = s_sourceIndex;

        foreach (var box in new[] { _openBox, _doneBox, _rejectedBox, _onlyNewBox, _withoutLinkBox })
        {
            box.Click += (_, _) => FiltersChanged();
        }

        _sourceBox.SelectionChanged += (_, _) => FiltersChanged();
        _searchBox.TextChanged += (_, _) => FiltersChanged();

        filters.Children.Add(_openBox);
        filters.Children.Add(_doneBox);
        filters.Children.Add(_rejectedBox);
        filters.Children.Add(_sourceBox);
        filters.Children.Add(_onlyNewBox);
        filters.Children.Add(_withoutLinkBox);
        filters.Children.Add(new Controls.TextBlock { Text = BugTexts.WindowSearch, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        filters.Children.Add(_searchBox);
        return filters;
    }

    private void FiltersChanged()
    {
        if (_filling)
        {
            return;
        }

        SaveFilters();
        RebuildList();
    }

    private void SaveFilters()
    {
        s_open = _openBox.IsChecked == true;
        s_done = _doneBox.IsChecked == true;
        s_rejected = _rejectedBox.IsChecked == true;
        s_onlyNew = _onlyNewBox.IsChecked == true;
        s_withoutLink = _withoutLinkBox.IsChecked == true;
        s_sourceIndex = Math.Max(0, _sourceBox.SelectedIndex);
    }

    private void FillProjects(string? selectId)
    {
        _filling = true;
        _projectBox.Items.Clear();
        _projectBox.Items.Add(new Controls.ComboBoxItem { Content = BugTexts.WindowNoProject, Tag = null });
        foreach (var (id, name) in _host.ProjectChoices())
        {
            _projectBox.Items.Add(new Controls.ComboBoxItem { Content = name, Tag = id });
        }

        _projectBox.SelectedItem = _projectBox.Items.OfType<Controls.ComboBoxItem>().FirstOrDefault(item => (item.Tag as string) == selectId)
            ?? _projectBox.Items[0];
        _filling = false;
    }

    private void RebuildList()
    {
        _sourcesButton.Visibility = SelectedProjectId is null ? Visibility.Collapsed : Visibility.Visible;
        var error = SelectedProjectId is { } errorProject ? _host.BugRefreshError(errorProject) : null;
        _errorText.Text = error is null ? string.Empty : BugTexts.WindowLastError(error);
        _errorText.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        var filter = CurrentFilter;
        var reports = BugQueries.ForProject(_data, SelectedProjectId, _host.BugProjects).Where(filter.Matches).ToList();

        _filling = true;
        _list.Items.Clear();
        foreach (var report in reports)
        {
            _list.Items.Add(CreateListItem(report));
        }

        _list.SelectedItem = _list.Items.OfType<Controls.ListBoxItem>().FirstOrDefault(item => (item.Tag as string) == _selectedId);
        _filling = false;
        _emptyText.Visibility = reports.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowDetail(_list.SelectedItem is null || _selectedId is null ? null : BugEditing.Find(_data, _selectedId));
    }

    private Controls.ListBoxItem CreateListItem(BugReport report)
    {
        var grid = new Controls.Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = GridLength.Auto });

        var icon = new Controls.TextBlock { Text = BugListRow.IconFor(report.Source), Margin = new Thickness(0, 1, 6, 0), VerticalAlignment = VerticalAlignment.Top };
        grid.Children.Add(icon);

        var text = new Controls.StackPanel { Opacity = report.IsOpen ? 1.0 : 0.55 };
        var title = new Controls.TextBlock { Text = report.Title, TextWrapping = TextWrapping.Wrap, FontWeight = report.Seen ? FontWeights.SemiBold : FontWeights.Bold };
        if (!report.IsOpen)
        {
            title.TextDecorations = TextDecorations.Strikethrough;
        }

        var info = new Controls.TextBlock { Text = ListInfo(report), FontSize = 11 };
        info.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        text.Children.Add(title);
        text.Children.Add(info);
        Controls.Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, 0, 0, 0) };
        if (report.IsOpen)
        {
            buttons.Children.Add(SmallButton("✓", BugTexts.DoneTip, () => SetStatus(report.Id, BugStatus.Done)));
            buttons.Children.Add(SmallButton("⊘", BugTexts.RejectTip, () => SetStatus(report.Id, BugStatus.Rejected)));
        }
        else
        {
            buttons.Children.Add(SmallButton("↺", BugTexts.ReopenTip, () => SetStatus(report.Id, BugStatus.Open)));
        }

        Controls.Grid.SetColumn(buttons, 2);
        grid.Children.Add(buttons);
        return new Controls.ListBoxItem { Content = grid, Tag = report.Id, HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch };
    }

    private static string ListInfo(BugReport report)
    {
        var parts = new List<string> { BugTexts.Status(report.Status) };
        if (report.Links.Count > 0)
        {
            parts.Add("🔗 " + report.Links.Count);
        }

        if (report.Removed)
        {
            parts.Add(BugTexts.Removed);
        }

        return string.Join(" · ", parts);
    }

    private static Controls.Button SmallButton(string content, string tip, Action action)
    {
        var button = new Controls.Button { Content = content, ToolTip = tip, Padding = new Thickness(4, 0, 4, 0), FontSize = 10, Margin = new Thickness(0, 0, 2, 0) };
        button.Click += (_, _) => action();
        return button;
    }

    private void OnSelectionChanged()
    {
        if (_filling)
        {
            return;
        }

        _selectedId = (_list.SelectedItem as Controls.ListBoxItem)?.Tag as string;
        var report = _selectedId is null ? null : BugEditing.Find(_data, _selectedId);
        if (report is { Seen: false })
        {
            var id = report.Id;
            _host.ChangeBugs(data => BugEditing.MarkSeen(data, id));
            return;
        }

        ShowDetail(report);
    }

    private void List_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Delete && _selectedId is { } id && BugEditing.Find(_data, id) is { IsOpen: true })
        {
            SetStatus(id, BugStatus.Rejected);
            e.Handled = true;
        }
    }

    private async void SetStatus(string reportId, BugStatus status) => await _host.SetBugStatusAsync(reportId, status);

    private void CreateBugWithoutLink()
    {
        var dialog = BugDialog.ForEdit(string.Empty, string.Empty);
        dialog.Owner = this;
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string? createdId = null;
        var projectId = SelectedProjectId;
        _host.ChangeBugs(data => createdId = BugEditing.AddLocal(data, dialog.BugTitle, dialog.Description, projectId, null).Id);
        if (createdId is not null)
        {
            SelectReport(createdId);
        }
    }
}
