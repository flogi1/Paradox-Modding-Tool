using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ParadoxModTool.Explorer;

/// <summary>
/// Namenssuche in der Verzeichnis-Übersicht (Nutzer-Feedback: "Suche nach Datei- und Ordnernamen"):
/// Suchfeld über dem Baum; solange es Text enthält, ersetzt eine flache Trefferliste Baum und Favoriten.
/// Die eigentliche Suche (<see cref="ExplorerSearch"/>) läuft entprellt im Hintergrund und wird bei jeder
/// weiteren Eingabe abgebrochen.
/// </summary>
public partial class ExplorerPanel
{
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private readonly ObservableCollection<ExplorerSearchHit> _searchHits = new();
    private DispatcherTimer? _searchTimer;
    private CancellationTokenSource? _searchCts;
    private (string Text, List<ExplorerSearchHit> Hits, string Status)? _lastSearch;
    private bool _restoringSearch;

    private void InitializeSearch()
    {
        SearchResultsList.ItemsSource = _searchHits;
        _searchTimer = new DispatcherTimer { Interval = SearchDelay };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            await RunSearchAsync();
        };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_searchTimer is null)
        {
            return;
        }

        _searchTimer.Stop();

        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Leeres Feld: sofort zurück zum Baum, laufende Suche verwerfen.
            _searchCts?.Cancel();
            ShowSearchResults(false);
            return;
        }

        if (_restoringSearch)
        {
            return;
        }

        // Eine neue Suche ersetzt die gemerkte.
        _lastSearch = null;
        SearchBackButton.Visibility = Visibility.Collapsed;
        _searchTimer.Start();
    }

    private void FoldersOnly_Changed(object sender, RoutedEventArgs e)
    {
        OptionsChanged?.Invoke(this, EventArgs.Empty);
        if (_searchTimer is null || string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            return;
        }

        _searchTimer.Stop();
        _ = RunSearchAsync();
    }

    /// <summary>
    /// Ordner-Treffer: Suche merken, Feld leeren und den Ordner im Baum mit allen Dateien zeigen (Nutzerwunsch). Der Knopf
    /// "Zurück zu den Treffern" holt die Liste wieder, falls man sich verklickt hat.
    /// </summary>
    private void ShowFolderHitInTree(ExplorerSearchHit hit)
    {
        _lastSearch = (SearchBox.Text, _searchHits.ToList(), SearchStatusText.Text);
        SearchBox.Clear();
        RevealInTree(hit.FullPath);
        SearchBackButton.Visibility = Visibility.Visible;
    }

    private void SearchBack_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSearch is not { } last)
        {
            return;
        }

        _restoringSearch = true;
        try
        {
            SearchBox.Text = last.Text;
        }
        finally
        {
            _restoringSearch = false;
        }

        ShowSearchResults(true);
        foreach (var hit in last.Hits)
        {
            _searchHits.Add(hit);
        }

        SearchStatusText.Text = last.Status;
        SearchBackButton.Visibility = Visibility.Collapsed;
    }

    private void SearchResultsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: ExplorerSearchHit { IsDirectory: true } hit } && !IsInsideButton(e.OriginalSource as DependencyObject))
        {
            ShowFolderHitInTree(hit);
            e.Handled = true;
        }
    }

    private static bool IsInsideButton(DependencyObject? element)
    {
        for (var current = element; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            if (current is ListBoxItem)
            {
                return false;
            }
        }

        return false;
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _searchTimer is { IsEnabled: true })
        {
            // Enter startet die Suche ohne die Entprellzeit abzuwarten.
            _searchTimer.Stop();
            _ = RunSearchAsync();
            e.Handled = true;
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e) => SearchBox.Clear();

    private async Task RunSearchAsync()
    {
        var pattern = NamePattern.Parse(SearchBox.Text);
        if (pattern is null)
        {
            ShowSearchResults(false);
            return;
        }

        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        ShowSearchResults(true);
        SearchStatusText.Text = ExplorerTexts.SearchRunning;

        var roots = _roots.Select(r => (r.FullPath, r.Name)).ToList();
        var foldersOnly = SearchFoldersOnly;
        var hideUgc = HideUgcReferences;

        try
        {
            var result = await Task.Run(() => ExplorerSearch.Run(roots, pattern, ExplorerSearch.DefaultLimit, cts.Token, foldersOnly, hideUgc), cts.Token);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            _searchHits.Clear();
            foreach (var hit in result.Hits)
            {
                _searchHits.Add(hit);
            }

            SearchStatusText.Text = result.Hits.Count == 0
                ? ExplorerTexts.SearchNoHits
                : result.Truncated
                    ? ExplorerTexts.SearchHitsTruncated(result.Hits.Count)
                    : ExplorerTexts.SearchHits(result.Hits.Count);
        }
        catch (OperationCanceledException)
        {
            // Von einer neueren Eingabe abgelöst.
        }
    }

    /// <summary>Schaltet zwischen Baum (mit Favoriten) und Trefferliste um.</summary>
    private void ShowSearchResults(bool show)
    {
        SearchResultsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        Tree.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        FavoritesExpander.Visibility = show ? Visibility.Collapsed : Visibility.Visible;

        if (!show)
        {
            _searchHits.Clear();
            SearchStatusText.Text = string.Empty;
        }
    }

    private void SearchResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement { DataContext: ExplorerSearchHit hit })
        {
            return;
        }

        if (hit.IsDirectory)
        {
            // Ordner: schon beim einfachen Klick in den Baum gewechselt (SearchResultsList_MouseLeftButtonUp).
            if (SearchResultsPanel.Visibility == Visibility.Visible)
            {
                ShowFolderHitInTree(hit);
            }
        }
        else
        {
            OpenFileRequested?.Invoke(this, new ExplorerOpenFileEventArgs { FilePath = hit.FullPath, Target = ExplorerOpenTarget.Active });
        }

        e.Handled = true;
    }

    private void SearchHitOpenLeft_Click(object sender, RoutedEventArgs e) => OpenHit(sender, ExplorerOpenTarget.Left);

    private void SearchHitOpenRight_Click(object sender, RoutedEventArgs e) => OpenHit(sender, ExplorerOpenTarget.Right);

    private void OpenHit(object sender, ExplorerOpenTarget target)
    {
        if ((sender as FrameworkElement)?.DataContext is ExplorerSearchHit { IsDirectory: false } hit)
        {
            OpenFileRequested?.Invoke(this, new ExplorerOpenFileEventArgs { FilePath = hit.FullPath, Target = target });
        }
    }

    /// <summary>
    /// Klappt den Baum bis zum angegebenen Pfad auf und wählt ihn aus. Lädt dafür die Kinder der
    /// Zwischenordner selbst nach (Lazy-Loading, siehe <see cref="ExplorerEntry.Children"/>) und erzeugt
    /// die zugehörigen Bau-Container schrittweise per Layout, da WPF verschachtelte Container erst nach
    /// dem Aufklappen des übergeordneten Knotens anlegt.
    /// </summary>
    public void RevealInTree(string fullPath)
    {
        var root = _roots.FirstOrDefault(r => PathNormalization.IsSameOrUnder(r.FullPath, fullPath));
        if (root is null || Tree.ItemContainerGenerator.ContainerFromItem(root) is not TreeViewItem rootContainer)
        {
            return;
        }

        var relative = Path.GetRelativePath(root.FullPath, fullPath);
        var segments = relative == "."
            ? Array.Empty<string>()
            : relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        var currentEntry = root;
        var container = rootContainer;

        foreach (var segment in segments)
        {
            if (!currentEntry.ChildrenLoaded)
            {
                LoadChildren(currentEntry);
            }

            container.IsExpanded = true;
            container.UpdateLayout();

            var next = currentEntry.Children.FirstOrDefault(c => string.Equals(c.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (next is null || container.ItemContainerGenerator.ContainerFromItem(next) is not TreeViewItem nextContainer)
            {
                break;
            }

            currentEntry = next;
            container = nextContainer;
        }

        container.IsSelected = true;
        container.BringIntoView();
    }
}
