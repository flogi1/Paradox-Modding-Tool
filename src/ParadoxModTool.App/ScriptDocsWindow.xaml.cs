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
                ToolTip = $"{ScriptDocLogFiles.DisplayNameOf(kind)}-Einträge ein-/ausblenden",
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

        ScopeCombo.Items.Add("(alle Scopes)");
        ScopeCombo.SelectedIndex = 0;
        ClearDetails();

        Loaded += async (_, _) => await ReloadAsync();
    }

    // ---- Laden

    private async Task ReloadAsync()
    {
        ReloadButton.IsEnabled = false;
        StatusText.Text = "Datenbank wird geladen…";
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
                ? $"Quelle: Wiki, Stand {(result.WikiFetchedUtc?.ToLocalTime().ToString("g") ?? "unbekannt")}"
                : $"Stand vom {latest.ImportedUtc.ToLocalTime():g}";
            StatusText.Text =
                (result.Imported ? "Neu eingelesen. " : string.Empty) +
                $"{_database.Entries.Count:N0} Einträge · {source} · {outdated:N0} veraltet · {isNew:N0} neu";
        }

        ReloadButton.Visibility = result.IsWikiSourced ? Visibility.Collapsed : Visibility.Visible;
        FolderButton.Visibility = result.IsWikiSourced ? Visibility.Collapsed : Visibility.Visible;

        if (result.IsWikiSourced)
        {
            HintText.Text = !result.WikiFileFound || _database.Entries.Count == 0
                ? "Die mit PMT mitgelieferte Wiki-Datei für dieses Spiel fehlt oder ist beschädigt. Die Script-Datenbank bleibt deshalb leer. " +
                  "Eine Neuinstallation von PMT stellt die Datei wieder her."
                : "Diese Script-Datenbank stammt aus der offiziellen Spiel-Wiki (mit PMT mitgeliefert), nicht aus Doku-Logs des Spiels. " +
                  "Eine Aktualisierung erfolgt durch ein neues PMT-Release, nicht durch \"Neu einlesen\".";
            HintBorder.Visibility = Visibility.Visible;
            return;
        }

        if (result.HasLogs)
        {
            HintBorder.Visibility = Visibility.Collapsed;
            return;
        }

        HintText.Text = result.Folder is null
            ? "Der Ordner mit den Doku-Logs des Spiels ist unbekannt. Wähle ihn über \"Ordner wählen\"."
            : $"Im Ordner {result.Folder} liegen keine Doku-Logs (modifiers.log, triggers.log, effects.log …). " +
              "Das Spiel schreibt diese Dateien selbst: Starte es einmal (bei Europa Universalis V ggf. im Debug-Modus) und klicke danach auf \"Neu einlesen\", " +
              "oder wähle den richtigen Ordner über \"Ordner wählen\".";
        HintBorder.Visibility = Visibility.Visible;
    }

    private void FillScopeCombo()
    {
        var previous = ScopeCombo.SelectedItem as string;
        ScopeCombo.Items.Clear();
        ScopeCombo.Items.Add("(alle Scopes)");
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
        CountText.Text = $"{entries.Count:N0} von {_database.Entries.Count:N0} Einträgen";

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
        var status = outdated ? " · veraltet" : _database.IsNew(entry) ? " · neu" : string.Empty;
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
        MetaText.Text = "Wähle links einen Eintrag.";
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
            parts.Add("veraltet (stand im letzten Einlesen nicht mehr in den Logs)");
        }
        else if (_database.IsNew(entry))
        {
            parts.Add("neu seit dem vorletzten Einlesen");
        }

        if (entry.Scopes.Count > 0)
        {
            parts.Add((entry.Kind == ScriptDocKind.Modifier ? "Kategorien: " : "Scopes: ") + string.Join(", ", entry.Scopes));
        }

        if (entry.Targets.Count > 0)
        {
            parts.Add((entry.Kind == ScriptDocKind.EventTarget ? "Ergebnis: " : "Ziele: ") + string.Join(", ", entry.Targets));
        }

        foreach (var (key, value) in entry.Extra)
        {
            parts.Add($"{key}: {value}");
        }

        MetaText.Text = string.Join("  ·  ", parts);
        DescriptionBox.Text = entry.Kind == ScriptDocKind.CustomLocalization && entry.Description.Length > 0
            ? "Einträge:\n" + entry.Description
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
            UsageHeader.Text = "Noch keine Beispielsuche für diesen Eintrag.";
            HitsList.ItemsSource = null;
            UsageButton.Content = "Beispiele suchen";
            return;
        }

        ShowUsage(saved);
    }

    private void ShowUsage(UsageSearchResult result)
    {
        UsageHeader.Text =
            $"Gesucht am {result.SearchedUtc.ToLocalTime():g}: {result.Hits.Count:N0} Treffer" +
            (result.Truncated ? $" (gekürzt auf {UsageSearch.MaxHits:N0})" : string.Empty);
        HitsList.ItemsSource = result.Hits.Select(h => new HitRow(h.FilePath, $"{ShortPath(h.FilePath, result.Roots)}:{h.Line}", h.Text, h.Line)).ToList();
        UsageButton.Content = "Neu suchen";
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
                "Es sind noch keine Vanilla- oder Mod-Ordner bekannt. Stelle den Spielpfad ein oder füge im Explorer einen Mod-Ordner hinzu.",
                "Beispiele suchen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        CancelUsageSearch();
        var cancellation = _usageCancellation = new CancellationTokenSource();
        UsageButton.IsEnabled = false;
        CancelUsageButton.Visibility = Visibility.Visible;
        HitsList.ItemsSource = null;

        var progress = new Progress<string>(file => UsageHeader.Text = "Suche… " + file);
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
                UsageHeader.Text = "Suche abgebrochen.";
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
            Description = "Ordner mit den Doku-Logs des Spiels wählen (der Ordner \"docs\" mit modifiers.log, triggers.log …)",
            UseDescriptionForTitle = true,
            InitialDirectory = _service.DocsFolder is { } current && Directory.Exists(current) ? current : string.Empty,
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _service.SetDocsFolder(dialog.SelectedPath);
        await ReloadAsync();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => CancelUsageSearch();
}
