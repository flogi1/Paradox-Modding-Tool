using System.Net.Http;
using System.Windows;
using System.Windows.Controls.Primitives;
using ParadoxModTool.ModOverview;
using ParadoxModTool.Notes;
using ParadoxModTool.SteamIntegration;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Übersicht "Meine Mods" (nicht modal, eines je Hauptfenster): die Mods aller Spiele als Kacheln oder Liste, mit Hinweisbereich, Spielliste, Filtern,
/// Sortierung und Steam-Abruf. Liest die Register aller Spiele nur; Änderungen an einer Mod laufen über das PMT ihres Spiels.
/// </summary>
internal sealed partial class MyModsWindow : Window
{
    private static readonly HttpClient Http = CreateHttp();

    private static readonly OverviewSortKey[] SortKeys = { OverviewSortKey.Name, OverviewSortKey.Subscribers, OverviewSortKey.OpenBugs, OverviewSortKey.LastRelease };

    private readonly IMyModsHost _host;
    private readonly string _session = Guid.NewGuid().ToString("N");
    private readonly ToggleButton _tilesButton = Segment();
    private readonly ToggleButton _listButton = Segment();
    private readonly ToggleButton _gamesButton = Segment();
    private readonly ToggleButton _allButton = Segment();
    private readonly Controls.ComboBox _sortBox = new() { MinWidth = 130, Margin = new Thickness(12, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ToggleButton _directionButton = new() { MinWidth = 28, Padding = new Thickness(6, 1, 6, 1), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBox _searchBox = new() { Width = 170, Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _publishedBox = FilterBox();
    private readonly Controls.CheckBox _openBugsBox = FilterBox();
    private readonly Controls.CheckBox _warningsBox = FilterBox();
    private readonly Controls.Button _refreshButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(12, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _statusText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.StackPanel _gameList = new();
    private readonly Controls.StackPanel _errorsPanel = new();
    private readonly Controls.Expander _alertsExpander = new() { Margin = new Thickness(0, 0, 0, 10) };
    private readonly Controls.StackPanel _alertsPanel = new();
    private readonly Controls.StackPanel _content = new();
    private readonly Controls.ScrollViewer _scroll = new() { VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto };
    private OverviewSnapshot? _snapshot;
    private IReadOnlyList<OverviewAlert> _alerts = Array.Empty<OverviewAlert>();
    private IReadOnlyDictionary<OverviewMod, BugCount> _bugCounts = new Dictionary<OverviewMod, BugCount>();
    private bool _syncing;
    private bool _loading;

    public MyModsWindow(IMyModsHost host)
    {
        _host = host;
        Title = OverviewTexts.WindowTitle;
        Width = 1200;
        Height = 780;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Controls.DockPanel { Margin = new Thickness(10) };
        var toolbar = BuildToolbar();
        Controls.DockPanel.SetDock(toolbar, Controls.Dock.Top);
        root.Children.Add(toolbar);
        var games = BuildGamePane();
        Controls.DockPanel.SetDock(games, Controls.Dock.Left);
        root.Children.Add(games);

        _alertsExpander.Content = _alertsPanel;
        _alertsExpander.Expanded += (_, _) => { if (!_syncing) { _host.Settings.MyModsAlertsCollapsed = false; } };
        _alertsExpander.Collapsed += (_, _) => { if (!_syncing) { _host.Settings.MyModsAlertsCollapsed = true; } };
        var main = new Controls.StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        main.Children.Add(_errorsPanel);
        main.Children.Add(_alertsExpander);
        main.Children.Add(_content);
        _scroll.Content = main;
        Controls.DockPanel.SetDock(_detailPane, Controls.Dock.Right);
        root.Children.Add(_detailPane);
        root.Children.Add(_scroll);
        var outer = new Controls.DockPanel();
        var menu = BuildMenu();
        Controls.DockPanel.SetDock(menu, Controls.Dock.Top);
        outer.Children.Add(menu);
        outer.Children.Add(root);
        Content = outer;

        // Vollbild (Standard) oder die gemerkte Fenstergröße; im Fenster-Modus nie maximiert öffnen.
        var bounds = _host.Settings.MyModsWindowBounds;
        if (bounds is not null)
        {
            bounds.IsMaximized = false;
        }

        WindowBoundsPersistence.Apply(this, bounds, null);
        if (_host.Settings.MyModsStartMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        SyncToolbar();
        Loaded += async (_, _) => await ReloadAsync(fetchSteam: true);
        Closing += (_, _) => SaveBounds();
    }

    /// <summary>Merkt Position und Größe (auch das Hauptfenster ruft das vor dem Speichern der Einstellungen auf).</summary>
    public void SaveBounds()
    {
        if (WindowBoundsPersistence.Capture(this) is { } bounds)
        {
            _host.Settings.MyModsWindowBounds = bounds;
        }
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ParadoxModTool");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return http;
    }

    private static ToggleButton Segment() => new() { Padding = new Thickness(10, 2, 10, 2), MinWidth = 70, VerticalAlignment = VerticalAlignment.Center };

    private static Controls.CheckBox FilterBox() => new() { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };

    private FrameworkElement BuildToolbar()
    {
        var bar = new Controls.WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        _tilesButton.Content = OverviewTexts.ViewTiles;
        _listButton.Content = OverviewTexts.ViewList;
        _gamesButton.Content = OverviewTexts.GroupGames;
        _allButton.Content = OverviewTexts.GroupAll;
        _tilesButton.Click += (_, _) => ChangeSetting(() => _host.Settings.MyModsView = "Tiles");
        _listButton.Click += (_, _) => ChangeSetting(() => _host.Settings.MyModsView = "List");
        _gamesButton.Click += (_, _) => ChangeSetting(() => _host.Settings.MyModsGrouping = "Games");
        _allButton.Click += (_, _) => ChangeSetting(() => _host.Settings.MyModsGrouping = "All");
        bar.Children.Add(Pair(_tilesButton, _listButton));
        bar.Children.Add(Pair(_gamesButton, _allButton));

        foreach (var text in new[] { OverviewTexts.SortName, OverviewTexts.SortSubscribers, OverviewTexts.SortOpenBugs, OverviewTexts.SortLastRelease })
        {
            _sortBox.Items.Add(text);
        }

        _sortBox.ToolTip = OverviewTexts.SortTip;
        _sortBox.SelectionChanged += (_, _) =>
        {
            if (_sortBox.SelectedIndex >= 0)
            {
                ChangeSetting(() => _host.Settings.MyModsSort = SortKeys[_sortBox.SelectedIndex].ToString());
            }
        };
        _directionButton.ToolTip = OverviewTexts.SortDirectionTip;
        _directionButton.Click += (_, _) => ChangeSetting(() => _host.Settings.MyModsSortDescending = _directionButton.IsChecked == true);
        bar.Children.Add(_sortBox);
        bar.Children.Add(_directionButton);

        _searchBox.ToolTip = OverviewTexts.SearchTip;
        _searchBox.TextChanged += (_, _) => Rebuild();
        bar.Children.Add(_searchBox);

        _publishedBox.Content = new Controls.TextBlock { Text = OverviewTexts.FilterPublished };
        _openBugsBox.Content = new Controls.TextBlock { Text = OverviewTexts.FilterOpenBugs };
        _warningsBox.Content = new Controls.TextBlock { Text = OverviewTexts.FilterWarnings };
        _publishedBox.Click += (_, _) => ChangeSetting(() => _host.Settings.MyModsOnlyPublished = _publishedBox.IsChecked == true);
        _openBugsBox.Click += (_, _) => ChangeSetting(() => _host.Settings.MyModsOnlyOpenBugs = _openBugsBox.IsChecked == true);
        _warningsBox.Click += (_, _) => ChangeSetting(() => _host.Settings.MyModsOnlyWarnings = _warningsBox.IsChecked == true);
        bar.Children.Add(_publishedBox);
        bar.Children.Add(_openBugsBox);
        bar.Children.Add(_warningsBox);

        _refreshButton.Content = OverviewTexts.Refresh;
        _refreshButton.ToolTip = OverviewTexts.RefreshTip;
        _refreshButton.Click += async (_, _) => await ReloadAsync(fetchSteam: true);
        _statusText.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        bar.Children.Add(_refreshButton);
        bar.Children.Add(_statusText);
        return bar;
    }

    private static FrameworkElement Pair(ToggleButton first, ToggleButton second)
    {
        var panel = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };
        panel.Children.Add(first);
        panel.Children.Add(second);
        return panel;
    }

    private FrameworkElement BuildGamePane()
    {
        var pane = new Controls.StackPanel { Width = 190 };
        var header = new Controls.TextBlock { Text = OverviewTexts.GamesHeader, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        pane.Children.Add(header);
        pane.Children.Add(_gameList);
        return new Controls.Border { Child = pane, Padding = new Thickness(0, 0, 10, 0), BorderThickness = new Thickness(0, 0, 1, 0) }
            .With(b => b.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.Separator"));
    }

    /// <summary>Überträgt die gespeicherten Einstellungen in die Knöpfe, ohne dabei Änderungen auszulösen.</summary>
    private void SyncToolbar()
    {
        var settings = _host.Settings;
        _syncing = true;
        _tilesButton.IsChecked = settings.MyModsView == "Tiles";
        _listButton.IsChecked = settings.MyModsView == "List";
        _gamesButton.IsChecked = settings.MyModsGrouping == "Games";
        _allButton.IsChecked = settings.MyModsGrouping == "All";
        _sortBox.SelectedIndex = Math.Max(0, Array.FindIndex(SortKeys, k => k.ToString() == settings.MyModsSort));
        _directionButton.IsChecked = settings.MyModsSortDescending;
        _directionButton.Content = settings.MyModsSortDescending ? "↓" : "↑";
        _publishedBox.IsChecked = settings.MyModsOnlyPublished;
        _openBugsBox.IsChecked = settings.MyModsOnlyOpenBugs;
        _warningsBox.IsChecked = settings.MyModsOnlyWarnings;
        _alertsExpander.IsExpanded = !settings.MyModsAlertsCollapsed;
        _syncing = false;
    }

    private void ChangeSetting(Action change)
    {
        if (_syncing)
        {
            return;
        }

        change();
        SyncToolbar();
        Rebuild();
    }

    /// <summary>Liest alle Register neu (im Hintergrund), zeigt sie und holt danach die Steam-Werte; danach wird noch einmal gelesen.</summary>
    private async Task ReloadAsync(bool fetchSteam)
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        _refreshButton.IsEnabled = false;
        _statusText.Text = OverviewTexts.Fetching;
        try
        {
            var libraries = await Task.Run(() => SteamFolders.Find() is { } steam ? SteamFolders.LibraryFolders(steam) : (IReadOnlyList<string>)Array.Empty<string>());
            await LoadSnapshotAsync(libraries);
            if (!fetchSteam || _snapshot is null)
            {
                return;
            }

            var ids = _snapshot.Mods.Select(m => m.WorkshopId).OfType<string>().ToList();
            var result = await OverviewRefresher.RefreshSteamAsync(Http, new OverviewPaths(_host.AppDataRoot), ids, _session, DateTime.UtcNow, CancellationToken.None);
            if (result.Reached)
            {
                await LoadSnapshotAsync(libraries);
                _statusText.Text = OverviewTexts.FetchedAt(DateTime.Now.ToString("t"));
            }
            else
            {
                _statusText.Text = OverviewTexts.SteamOffline(_snapshot.SteamFetchedUtc is { } fetched ? fetched.ToLocalTime().ToString("g") : "–");
            }

            await RefreshBugsAsync();
        }
        finally
        {
            _loading = false;
            _refreshButton.IsEnabled = true;
        }
    }

    private async Task LoadSnapshotAsync(IReadOnlyList<string> libraries)
    {
        var root = _host.AppDataRoot;
        var profiles = _host.Profiles;
        var (snapshot, bugs) = await Task.Run(() => (ModOverviewLoader.Load(root, profiles, libraries), BugReportStore.Load(BugReportStore.GetDefaultPath())));
        _snapshot = snapshot;
        _thumbnails.Clear();
        BuildGameList();
        ApplyBugData(bugs);
    }

    /// <summary>Spiele in der gespeicherten Reihenfolge, dann die übrigen in Profil-Reihenfolge.</summary>
    private IReadOnlyList<OverviewGame> OrderedGames() =>
        _snapshot is null ? Array.Empty<OverviewGame>()
        : _host.Settings.MyModsGameOrder
            .Select(id => _snapshot.Games.FirstOrDefault(g => g.Id == id))
            .OfType<OverviewGame>()
            .Concat(_snapshot.Games)
            .DistinctBy(g => g.Id)
            .ToList();

    private void BuildGameList()
    {
        _gameList.Children.Clear();
        var games = OrderedGames();
        var hidden = _host.Settings.MyModsHiddenGames;
        for (var i = 0; i < games.Count; i++)
        {
            var game = games[i];
            var row = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var down = SmallButton("↓", OverviewTexts.GamesDownTip, i < games.Count - 1, () => MoveGame(game.Id, +1));
            var up = SmallButton("↑", OverviewTexts.GamesUpTip, i > 0, () => MoveGame(game.Id, -1));
            Controls.DockPanel.SetDock(down, Controls.Dock.Right);
            Controls.DockPanel.SetDock(up, Controls.Dock.Right);
            row.Children.Add(down);
            row.Children.Add(up);
            var box = new Controls.CheckBox
            {
                Content = new Controls.TextBlock { Text = game.DisplayName, TextTrimming = TextTrimming.CharacterEllipsis },
                IsChecked = !hidden.Contains(game.Id),
                VerticalAlignment = VerticalAlignment.Center,
            };
            box.Click += (_, _) =>
            {
                var set = _host.Settings.MyModsHiddenGames.Where(id => id != game.Id).ToList();
                if (box.IsChecked != true)
                {
                    set.Add(game.Id);
                }

                _host.Settings.MyModsHiddenGames = set;
                Rebuild();
            };
            row.Children.Add(box);
            _gameList.Children.Add(row);
        }
    }

    private static Controls.Button SmallButton(string text, string tip, bool enabled, Action click)
    {
        var button = new Controls.Button { Content = text, ToolTip = tip, IsEnabled = enabled, Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(2, 0, 0, 0) };
        button.Click += (_, _) => click();
        return button;
    }

    private void MoveGame(string gameId, int step)
    {
        var order = OrderedGames().Select(g => g.Id).ToList();
        var index = order.IndexOf(gameId);
        var target = index + step;
        if (index < 0 || target < 0 || target >= order.Count)
        {
            return;
        }

        (order[index], order[target]) = (order[target], order[index]);
        _host.Settings.MyModsGameOrder = order;
        BuildGameList();
        Rebuild();
    }

    private OverviewViewOptions CurrentOptions()
    {
        var settings = _host.Settings;
        return new OverviewViewOptions(
            settings.MyModsGrouping == "Games",
            Enum.TryParse<OverviewSortKey>(settings.MyModsSort, out var sort) ? sort : OverviewSortKey.Name,
            settings.MyModsSortDescending,
            settings.MyModsOnlyPublished,
            settings.MyModsOnlyOpenBugs,
            settings.MyModsOnlyWarnings,
            _searchBox.Text,
            OrderedGames().Select(g => g.Id).ToList(),
            settings.MyModsHiddenGames.ToList());
    }

    /// <summary>Baut Hinweise und Inhalt aus dem Stand im Speicher neu (ohne Plattenzugriff).</summary>
    private void Rebuild()
    {
        if (_snapshot is not { } snapshot)
        {
            return;
        }

        var options = CurrentOptions();
        BuildErrors(snapshot);
        BuildAlerts(snapshot, options);
        BuildContent(OverviewView.Build(snapshot, OverviewAlertFilter.Visible(_alerts, _host.Settings.MyModsHiddenAlertKinds), _bugCounts, options));
    }

    private void BuildErrors(OverviewSnapshot snapshot)
    {
        _errorsPanel.Children.Clear();
        foreach (var error in snapshot.GameErrors)
        {
            var text = new Controls.TextBlock { Text = OverviewTexts.GameLoadFailed(error.DisplayName, error.Message), TextWrapping = TextWrapping.Wrap };
            text.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.NoticeText");
            var border = new Controls.Border { Child = text, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
            border.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.NoticeBackground");
            border.SetResourceReference(Controls.Border.BorderBrushProperty, "Theme.NoticeBorder");
            _errorsPanel.Children.Add(border);
        }
    }

    private void BuildAlerts(OverviewSnapshot snapshot, OverviewViewOptions options)
    {
        _alertsPanel.Children.Clear();
        var hidden = options.HiddenGames.ToHashSet(StringComparer.Ordinal);
        var hiddenKinds = _host.Settings.MyModsHiddenAlertKinds.ToHashSet(StringComparer.Ordinal);
        var inGames = _alerts.Where(a => !hidden.Contains(a.GameId)).ToList();
        var newBugs = _bugGroups.Where(g => !hidden.Contains(g.Root.GameId)).Sum(g => g.Reports.Count);
        var counts = OverviewAlertFilter.Counts(inGames, newBugs, WorkshopCount(snapshot, hidden));
        _alertCounts = counts;

        var shown = OverviewAlertFilter.Visible(inGames, hiddenKinds);
        var extraRows = (hiddenKinds.Contains(OverviewAlertFilter.NewBugs) ? 0 : AddBugRows(hidden))
            + (hiddenKinds.Contains(OverviewAlertFilter.WorkshopId) ? 0 : AddWorkshopRows(snapshot, hidden));
        _syncing = true;
        _alertsExpander.Header = new Controls.TextBlock { Text = OverviewTexts.AlertsHeader(shown.Count + extraRows), FontWeight = FontWeights.SemiBold };
        _alertsExpander.Visibility = shown.Count + extraRows == 0 ? Visibility.Collapsed : Visibility.Visible;
        _syncing = false;
        foreach (var alert in shown)
        {
            var mod = snapshot.Mods.FirstOrDefault(m => m.GameId == alert.GameId && m.Mod.Id == alert.ModId);
            _alertsPanel.Children.Add(AlertRow("⚠", "Theme.Warning", mod?.Mod.Name ?? alert.ModId, alert.Text, () => ScrollToMod(alert.GameId, alert.ModId)).With(row => row.ToolTip = alert.Detail));
        }
    }

    /// <summary>Eine Zeile im Hinweisbereich: Symbol, Mod-Name fett, Text; ein Klick führt zur Kachel.</summary>
    private static FrameworkElement AlertRow(string icon, string iconBrush, string modName, string text, Action click)
    {
        var line = new Controls.TextBlock { TextWrapping = TextWrapping.Wrap };
        var symbol = new System.Windows.Documents.Run(icon + "  ");
        symbol.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, iconBrush);
        line.Inlines.Add(symbol);
        line.Inlines.Add(new System.Windows.Documents.Run(modName) { FontWeight = FontWeights.SemiBold });
        line.Inlines.Add(new System.Windows.Documents.Run("  " + text));
        var row = new Controls.Border { Child = line, Padding = new Thickness(6, 3, 6, 3), Cursor = System.Windows.Input.Cursors.Hand, Background = System.Windows.Media.Brushes.Transparent };
        row.MouseEnter += (_, _) => row.SetResourceReference(Controls.Border.BackgroundProperty, "Theme.ControlHover");
        row.MouseLeave += (_, _) => row.Background = System.Windows.Media.Brushes.Transparent;
        row.MouseLeftButtonUp += (_, _) => click();
        return row;
    }
}

/// <summary>Kleine Hilfe für Aufbau im Code: ein Element nach dem Anlegen einrichten.</summary>
internal static class ElementExtensions
{
    public static T With<T>(this T element, Action<T> setup)
    {
        setup(element);
        return element;
    }
}
