using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ParadoxModTool.ScriptDocs;
// UseWindowsForms macht diese Namen zwischen WPF und WinForms mehrdeutig (CS0104).
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Nachschlagefenster "Script-Datenbank": Modifier, Trigger, Effects, Event-Targets, On-Actions und Custom-Localization des Spiels, gelesen aus den
/// Doku-Logs, mit Suche, Filtern, Scope-Angaben, "veraltet"/"neu"-Markierung und einer Beispielsuche in Vanilla- und Mod-Dateien.
/// </summary>
public partial class ScriptDocsWindow : Window
{
    private readonly ScriptDocsService _service;
    private readonly Func<IReadOnlyList<string>> _getSearchRoots;
    private readonly Action<string> _insertName;
    private readonly Action<string, int> _openFileAtLine;
    private readonly Dictionary<ScriptDocKind, CheckBox> _kindBoxes = new();
    private readonly System.Windows.Threading.DispatcherTimer _filterTimer;

    private ScriptDocDatabase _database = new();
    private ScriptDocEntry? _selected;
    private CancellationTokenSource? _usageCancellation;
    private bool _loading = true;

    public ScriptDocsWindow(
        ScriptDocsService service,
        Func<IReadOnlyList<string>> getSearchRoots,
        Action<string> insertName,
        Action<string, int> openFileAtLine)
    {
        InitializeComponent();
        _service = service;
        _getSearchRoots = getSearchRoots;
        _insertName = insertName;
        _openFileAtLine = openFileAtLine;

        foreach (var kind in Enum.GetValues<ScriptDocKind>())
        {
            var box = new CheckBox
            {
                Content = ScriptDocLogFiles.DisplayNameOf(kind),
                IsChecked = true,
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = MiscWindowTexts.ScriptDocsKindToggleTip(ScriptDocLogFiles.DisplayNameOf(kind)),
            };
            box.Checked += Filter_Changed;
            box.Unchecked += Filter_Changed;
            _kindBoxes[kind] = box;
            KindPanel.Children.Add(box);
        }

        // Tippen im Suchfeld filtert erst kurz nach der letzten Eingabe, damit die Liste nicht bei jedem Zeichen neu aufgebaut wird.
        _filterTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            ApplyFilter();
        };

        ScopeCombo.Items.Add(MiscWindowTexts.ScriptDocsAllScopes);
        ScopeCombo.SelectedIndex = 0;
        ClearDetails();

        Loaded += async (_, _) => await ReloadAsync();
    }

    // ---- Laden

    private async Task ReloadAsync()
    {
        ReloadButton.IsEnabled = false;
        StatusText.Text = MiscWindowTexts.ScriptDocsLoading;
        _loading = true;
        try
        {
            var result = await _service.RefreshAsync();
            _database = result.Database;
            ClearDetails();
            FillScopeCombo();
            _loading = false;
            ApplyFilter();
            ShowStatus(result);
        }
        finally
        {
            _loading = false;
            ReloadButton.IsEnabled = true;
        }
    }

    private void ShowStatus(ScriptDocsRefreshResult result)
    {
        var latest = _database.Imports.OrderByDescending(i => i.Id).FirstOrDefault();
        if (latest is null)
        {
            StatusText.Text = string.Empty;
        }
        else
        {
            var outdated = _database.Entries.Count(_database.IsOutdated);
            var isNew = _database.Entries.Count(_database.IsNew);
            var source = result.IsWikiSourced
                ? MiscWindowTexts.ScriptDocsSourceWiki(result.WikiFetchedUtc?.ToLocalTime().ToString("g") ?? MiscWindowTexts.ScriptDocsUnknown)
                : MiscWindowTexts.ScriptDocsAsOf(latest.ImportedUtc.ToLocalTime());
            StatusText.Text =
                (result.Imported ? MiscWindowTexts.ScriptDocsReloaded + " " : string.Empty) +
                MiscWindowTexts.ScriptDocsStatus(_database.Entries.Count, source, outdated, isNew);
        }

        ReloadButton.Visibility = result.IsWikiSourced ? Visibility.Collapsed : Visibility.Visible;
        FolderButton.Visibility = result.IsWikiSourced ? Visibility.Collapsed : Visibility.Visible;

        if (result.IsWikiSourced)
        {
            HintText.Text = !result.WikiFileFound || _database.Entries.Count == 0
                ? MiscWindowTexts.ScriptDocsHintWikiMissing
                : MiscWindowTexts.ScriptDocsHintWikiSource;
            HintBorder.Visibility = Visibility.Visible;
            return;
        }

        if (result.HasLogs)
        {
            HintBorder.Visibility = Visibility.Collapsed;
            return;
        }

        HintText.Text = result.Folder is null
            ? MiscWindowTexts.ScriptDocsHintFolderUnknown
            : MiscWindowTexts.ScriptDocsHintNoLogs(result.Folder);
        HintBorder.Visibility = Visibility.Visible;
    }

    private void FillScopeCombo()
    {
        var previous = ScopeCombo.SelectedItem as string;
        ScopeCombo.Items.Clear();
        ScopeCombo.Items.Add(MiscWindowTexts.ScriptDocsAllScopes);
        foreach (var scope in ScriptDocQuery.AllScopes(_database))
        {
            ScopeCombo.Items.Add(scope);
        }

        var index = previous is null ? 0 : ScopeCombo.Items.IndexOf(previous);
        ScopeCombo.SelectedIndex = index < 0 ? 0 : index;
    }

    // ---- Liste

    private sealed record EntryRow(ScriptDocEntry Entry, string Name, string Detail, double Opacity, string Tooltip);

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        // Das Suchfeld wird verzögert ausgewertet, alle anderen Filter sofort.
        if (ReferenceEquals(sender, SearchBox))
        {
            _filterTimer.Stop();
            _filterTimer.Start();
        }
        else
        {
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        var kinds = _kindBoxes.Where(k => k.Value.IsChecked == true).Select(k => k.Key).ToHashSet();
        var query = new ScriptDocQuery
        {
            Text = SearchBox.Text,
            Kinds = kinds.Count == _kindBoxes.Count ? null : kinds,
            Scope = ScopeCombo.SelectedIndex > 0 ? ScopeCombo.SelectedItem as string : null,
            HideOutdated = HideOutdatedBox.IsChecked == true,
        };

        var entries = kinds.Count == 0 ? new List<ScriptDocEntry>() : query.Run(_database).ToList();
        var rows = entries.Select(ToRow).ToList();

        var keep = _selected;
        EntryList.ItemsSource = rows;
        CountText.Text = MiscWindowTexts.ScriptDocsCount(entries.Count, _database.Entries.Count);

        var row = keep is null ? null : rows.FirstOrDefault(r => ReferenceEquals(r.Entry, keep));
        if (row is not null)
        {
            EntryList.SelectedItem = row;
            EntryList.ScrollIntoView(row);
        }
    }

    private EntryRow ToRow(ScriptDocEntry entry)
    {
        var outdated = _database.IsOutdated(entry);
        var status = outdated ? " · " + MiscWindowTexts.ScriptDocsOutdated : _database.IsNew(entry) ? " · " + MiscWindowTexts.ScriptDocsNew : string.Empty;
        var scopes = entry.Scopes.Count == 0 ? string.Empty : " · " + string.Join(", ", entry.Scopes);
        return new EntryRow(
            entry,
            entry.Name,
            ScriptDocLogFiles.DisplayNameOf(entry.Kind) + status + scopes,
            outdated ? 0.55 : 1.0,
            entry.Name);
    }

    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EntryList.SelectedItem is EntryRow row)
        {
            ShowDetails(row.Entry);
        }
        else if (EntryList.Items.Count == 0 || _selected is null)
        {
            ClearDetails();
        }
    }

    private void EntryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntryList.SelectedItem is EntryRow row && e.OriginalSource is FrameworkElement { DataContext: EntryRow })
        {
            _insertName(row.Entry.Name);
        }
    }

    // ---- Details

    private void ClearDetails()
    {
        _selected = null;
        NameBox.Text = string.Empty;
        MetaText.Text = MiscWindowTexts.ScriptDocsSelectEntry;
        DescriptionBox.Text = string.Empty;
        InsertButton.IsEnabled = false;
        UsageButton.IsEnabled = false;
        UsageHeader.Text = string.Empty;
        HitsList.ItemsSource = null;
    }

    private void ShowDetails(ScriptDocEntry entry)
    {
        if (ReferenceEquals(entry, _selected))
        {
            return;
        }

        CancelUsageSearch();
        _selected = entry;
        NameBox.Text = entry.Name;

        var parts = new List<string> { ScriptDocLogFiles.DisplayNameOf(entry.Kind) };
        if (_database.IsOutdated(entry))
        {
            parts.Add(MiscWindowTexts.ScriptDocsOutdatedDetail);
        }
        else if (_database.IsNew(entry))
        {
            parts.Add(MiscWindowTexts.ScriptDocsNewDetail);
        }

        if (entry.Scopes.Count > 0)
        {
            parts.Add(entry.Kind == ScriptDocKind.Modifier ? MiscWindowTexts.ScriptDocsCategories(string.Join(", ", entry.Scopes)) : "Scopes: " + string.Join(", ", entry.Scopes));
        }

        if (entry.Targets.Count > 0)
        {
            parts.Add(entry.Kind == ScriptDocKind.EventTarget ? MiscWindowTexts.ScriptDocsResult(string.Join(", ", entry.Targets)) : MiscWindowTexts.ScriptDocsTargets(string.Join(", ", entry.Targets)));
        }

        foreach (var (key, value) in entry.Extra)
        {
            parts.Add($"{key}: {value}");
        }

        MetaText.Text = string.Join("  ·  ", parts);
        DescriptionBox.Text = entry.Kind == ScriptDocKind.CustomLocalization && entry.Description.Length > 0
            ? MiscWindowTexts.ScriptDocsEntries + "\n" + entry.Description
            : entry.Description;

        InsertButton.IsEnabled = true;
        UsageButton.IsEnabled = true;
        ShowSavedUsage(entry);
    }

    // ---- Einfügen

    private void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not null)
        {
            _insertName(_selected.Name);
        }
    }

    // ---- Beispielsuche

    private sealed record HitRow(string FullPath, string Location, string Text, int Line);

    private void ShowSavedUsage(ScriptDocEntry entry)
    {
        var saved = _service.SearchStore.TryLoad(entry.Kind, entry.Name);
        if (saved is null)
        {
            UsageHeader.Text = MiscWindowTexts.ScriptDocsNoExampleSearch;
            HitsList.ItemsSource = null;
            UsageButton.Content = MiscWindowTexts.ScriptDocsFindExamples;
            return;
        }

        ShowUsage(saved);
    }

    private void ShowUsage(UsageSearchResult result)
    {
        UsageHeader.Text =
            MiscWindowTexts.ScriptDocsSearchedAt(result.SearchedUtc.ToLocalTime(), result.Hits.Count) +
            (result.Truncated ? " " + MiscWindowTexts.ScriptDocsTruncated(UsageSearch.MaxHits) : string.Empty);
        HitsList.ItemsSource = result.Hits.Select(h => new HitRow(h.FilePath, $"{ShortPath(h.FilePath, result.Roots)}:{h.Line}", h.Text, h.Line)).ToList();
        UsageButton.Content = MiscWindowTexts.ScriptDocsSearchAgain;
    }

    private static string ShortPath(string path, IEnumerable<string> roots)
    {
        var root = roots
            .Where(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Length)
            .FirstOrDefault();
        return root is null ? path : Path.Combine(Path.GetFileName(Path.TrimEndingDirectorySeparator(root)), Path.GetRelativePath(root, path));
    }

    private async void UsageButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is not { } entry)
        {
            return;
        }

        var roots = _getSearchRoots();
        if (roots.Count == 0)
        {
            MessageBox.Show(
                this,
                MiscWindowTexts.ScriptDocsNoRoots,
                MiscWindowTexts.ScriptDocsFindExamples,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        CancelUsageSearch();
        var cancellation = _usageCancellation = new CancellationTokenSource();
        UsageButton.IsEnabled = false;
        CancelUsageButton.Visibility = Visibility.Visible;
        HitsList.ItemsSource = null;

        var progress = new Progress<string>(file => UsageHeader.Text = MiscWindowTexts.ScriptDocsSearching(file));
        try
        {
            var result = await Task.Run(
                () => UsageSearch.Run(entry.Kind, entry.Name, roots, DateTime.UtcNow, cancellation.Token, progress),
                cancellation.Token);

            try
            {
                _service.SearchStore.Save(result);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Das Ergebnis wird angezeigt, aber nicht gemerkt.
            }

            if (ReferenceEquals(_selected, entry))
            {
                ShowUsage(result);
            }
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_selected, entry))
            {
                UsageHeader.Text = MiscWindowTexts.ScriptDocsSearchCancelled;
            }
        }
        finally
        {
            if (ReferenceEquals(_usageCancellation, cancellation))
            {
                _usageCancellation = null;
                CancelUsageButton.Visibility = Visibility.Collapsed;
                UsageButton.IsEnabled = _selected is not null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelUsageButton_Click(object sender, RoutedEventArgs e) => CancelUsageSearch();

    private void CancelUsageSearch() => _usageCancellation?.Cancel();

    private void HitsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HitsList.SelectedItem is HitRow hit && e.OriginalSource is FrameworkElement { DataContext: HitRow })
        {
            _openFileAtLine(hit.FullPath, hit.Line);
        }
    }

    // ---- Toolbar

    private async void ReloadButton_Click(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async void FolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = MiscWindowTexts.ScriptDocsFolderDialog,
            UseDescriptionForTitle = true,
            InitialDirectory = _service.DocsFolder is { } current && Directory.Exists(current) ? ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(current) : string.Empty,
        };

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("scriptdocs.folder");
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _service.SetDocsFolder(dialog.SelectedPath);
        await ReloadAsync();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => CancelUsageSearch();
}
