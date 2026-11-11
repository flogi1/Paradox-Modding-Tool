using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.Localization;
using ParadoxModTool.ModCompat;
using ParadoxModTool.SearchReplace;
using ParadoxModTool.Settings;
// WPF bringt über System.Windows.Controls bereits eine eigene Klasse "TextSearch" mit
// (Such-Unterstützung in ListBox/ComboBox) - das kollidiert mit unserer gleichnamigen
// ParadoxModTool.SearchReplace.TextSearch (CS0104). Alias löst das eindeutig zugunsten
// unserer eigenen Klasse auf, da Alias-Direktiven Vorrang vor Namespace-Imports haben.
using TextSearch = ParadoxModTool.SearchReplace.TextSearch;
// UseWindowsForms ist im .csproj dieses Projekts aktiviert (FolderBrowserDialog, siehe
// Browse_Click) - wie schon an anderer Stelle in diesem Programm dokumentiert (Lessons Learned
// Punkt 7/12 der Architektur-Dokumentation) werden dadurch mehrere gleichnamige Typen zwischen
// WPF und WinForms mehrdeutig (CS0104). Neu betroffen durch die Schnellzugriff-Buttons (siehe
// früher hier, heute QuickPathBar, die WPF-Controls per Code
// statt XAML erzeugen): "Button" (System.Windows.Controls.Button vs. System.Windows.Forms.Button)
// und "Orientation" (System.Windows.Controls.Orientation vs. System.Windows.Forms.Orientation).
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using UserControl = System.Windows.Controls.UserControl;

namespace ParadoxModTool.App;

/// <summary>
/// Inhalt des vereinheitlichten Suchen-&amp;-Ersetzen-Funktionskastens (Abschnitt 5b): "Suchen und Ersetzen und
/// Suchen in Dateien kann in einem Funktionskasten vereinheitlicht werden [...] es wäre gut,
/// wenn es einfach ist zwischen den Modi hin und her zu wechseln." Für den Modus "Aktuelles
/// Dokument" arbeitet das Panel ausschließlich über die Delegates aus <see cref="SearchPanelContext"/>
/// (damit es selbst keine Kenntnis von EditorPane/ActionHistory braucht); für den
/// Modus "Ordner" ruft es die reinen Lesevorgänge aus ParadoxModTool.SearchReplace direkt auf,
/// nur das tatsächliche Schreiben beim Ersetzen läuft über den Delegate ins Hauptfenster (damit
/// es dort als EIN Schritt im programmweiten Aktionsverlauf landet, siehe ParadoxModTool.UndoRedo).
/// Gastgeber sind das <see cref="SearchReplaceWindow"/> und der Reiter "Suche" im rechten Panel; beide zeigen das volle Panel mit Moduswahl.
/// Der Tab-Moduswechsel gehört nur dem Fenster.
/// </summary>
public partial class SearchReplacePanel : UserControl
{
    /// <summary>Vom Gastgeber in <see cref="Initialize"/> gesetzt; vorher feuern nur Ereignisse aus InitializeComponent, die den Kontext nicht brauchen.</summary>
    private SearchPanelContext _context = null!;

    private readonly string _favoritesPath = SearchFavoritesStore.GetDefaultFavoritesFilePath();
    private readonly ObservableCollection<string> _favorites = new();

    // Stand der Favoriten beim letzten Laden/Speichern (siehe SearchFavoritesStore.SaveMerged).
    private List<string> _favoritesBaseline = new();

    /// <summary>Verhindert eine Endlosschleife, wenn das Hauptfenster den Checkbox-Zustand hier synchronisiert (siehe <see cref="SetHideDescriptionCheckboxState"/>).</summary>
    private bool _suppressHideDescriptionEvent;

    private bool _busy;

    public SearchReplacePanel()
    {
        InitializeComponent();
        ApplyTexts();
    }

    /// <summary>Läuft gerade eine Aufgabe über viele Dateien (Panel gesperrt)?</summary>
    public bool IsBusy => _busy;

    public bool IsFilesMode => FilesModeRadio.IsChecked == true;

    /// <summary>Inhalt des Suchfelds.</summary>
    public string SearchText
    {
        get => SearchTextBox.Text;
        set => SearchTextBox.Text = value;
    }

    /// <summary>
    /// Einmal vom Gastgeber aufzurufen. Die Trefferliste kommt vom Gastgeber (<see cref="SearchPanelContext.Results"/>) und wird beim
    /// Fenster des Hauptfensters geteilt (Nutzer-Feedback: "Ich hätte
    /// auch gern noch die Option die Suchergebnisse aus dem Suchfenster [...] in einem Fenster
    /// über meinem Arbeitsbereich anzeigen zu lassen [...]. Die Ergebnisse im Suchfenster können
    /// aber auf jeden Fall bleiben!") — ein einzelnes <see cref="ObservableCollection{T}"/>-Objekt,
    /// das sowohl an <see cref="ResultsList"/> hier als auch an die Ergebnisleiste im
    /// Hauptfenster gebunden ist, sodass beide Anzeigen automatisch synchron bleiben, ohne dass
    /// zwei getrennte Kopien der Treffer gepflegt werden müssten.
    /// </summary>
    public void Initialize(SearchPanelContext context)
    {
        if (_context is not null)
        {
            throw new InvalidOperationException("SearchReplacePanel.Initialize darf nur einmal aufgerufen werden.");
        }

        _context = context;

        ResultsList.ItemsSource = _context.Results;
        ResultsListCopy.Attach(ResultsList, ResultsDetail);
        HideDescriptionCheckBox.IsChecked = _context.GetHideDescription();

        FavoritesList.ItemsSource = _favorites;
        foreach (var folder in SearchFavoritesStore.TryLoad(_favoritesPath))
        {
            _favorites.Add(folder);
        }

        _favoritesBaseline = _favorites.ToList();

        // Aktives Spielprofil für die 4 FESTEN Schnellzugriff-Buttons (Mod-Ordner/in_game/main_menu/loading_screen, siehe
        // QuickPathBar); der Mod-Ordner wird über denselben Delegate wie beim automatischen Vorschlag der Verzeichnis-Übersicht
        // aufgelöst (MainWindow.ResolveDefaultModRoot), der Ordner mit den Vanilla-Spieldateien über MainWindow.ComputeGameContentRoot.
        QuickPaths.Initialize(_context.ActiveProfile, _context.ResolveDefaultModRoot, _context.ResolveGameContentRoot);
        QuickPaths.PathChosen += (_, path) => FolderTextBox.Text = path;
        QuickPathBar.AcceptDrops(FolderTextBox);

        if (_context.GetLastFolder?.Invoke() is { Length: > 0 } lastFolder)
        {
            FolderTextBox.Text = lastFolder;
        }

        UpdateModeVisibility();
    }

    /// <summary>Gibt den Inhalt des Ordnerfelds an das Hauptfenster, damit ein neu geöffnetes Fenster wieder dort sucht.</summary>
    public void RememberFolder()
    {
        if (FolderTextBox.Text.Trim() is { Length: > 0 } folder)
        {
            _context.SetLastFolder?.Invoke(folder);
        }
    }

    private void ApplyTexts()
    {
        CurrentDocumentModeRadio.Content = SearchTexts.ModeCurrentDocument;
        FilesModeRadio.Content = SearchTexts.ModeFiles;
        BrowseButton.Content = SearchTexts.Browse;
        FolderTextBox.ToolTip = SearchTexts.PathTip;
        RemoveFavoriteButton.Content = SearchTexts.FavoriteRemove;
        RemoveFavoriteButton.ToolTip = SearchTexts.FavoriteRemoveTip;
        AddFavoriteButton.Content = SearchTexts.FavoriteSave;
        AddFavoriteButton.ToolTip = SearchTexts.FavoriteSaveTip;
        FavoritesList.ToolTip = SearchTexts.FavoritesTip;
        SearchLabel.Text = SearchTexts.SearchLabel;
        ReplaceLabel.Text = SearchTexts.ReplaceLabel;
        SwapButton.ToolTip = SearchTexts.SwapTip;
        MatchCaseCheckBox.Content = SearchTexts.MatchCase;
        WholeWordCheckBox.Content = SearchTexts.WholeWord;
        IgnoreFormattingCheckBox.Content = SearchTexts.IgnoreFormatting;
        IgnoreFormattingCheckBox.ToolTip = SearchTexts.IgnoreFormattingTip;
        BothPanesCheckBox.Content = SearchTexts.BothPanes;
        BothPanesCheckBox.ToolTip = SearchTexts.BothPanesTip;
        FindAllButton.Content = SearchTexts.FindAll;
        ReplaceAllButton.Content = SearchTexts.ReplaceAll;
        HideDescriptionCheckBox.Content = SearchTexts.HideDescription;
        HideDescriptionCheckBox.ToolTip = SearchTexts.HideDescriptionTip;
        ResultsList.ToolTip = SearchTexts.ResultsTip;
    }

    /// <summary>
    /// Setzt den Cursor in das Suchfeld und markiert dessen Inhalt (Nutzer-Feedback: nach Strg+F sofort tippen können, ohne erst
    /// in das Fenster zu klicken). Läuft über den Dispatcher, weil ein gerade erst gezeigtes oder aktiviertes Fenster den
    /// Tastaturfokus sonst noch nicht annimmt. Das Fenster zu aktivieren ist Sache des Gastgebers (siehe SearchReplaceWindow.FocusSearchBox).
    /// </summary>
    public void FocusSearchBox(string? selectedText = null)
    {
        // Strg+F mit markiertem Code im Editor: die Markierung wird zum Suchtext (auch mehrzeilig); ohne Markierung bleibt der bisherige Suchtext.
        if (!string.IsNullOrEmpty(selectedText))
        {
            SearchTextBox.Text = selectedText;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            SearchTextBox.Focus();
            Keyboard.Focus(SearchTextBox);
            SearchTextBox.SelectAll();
        });
    }

    /// <summary>Besitzer für die WinForms-Ordnerdialoge, damit sie zum Fenster des Panels gehören statt frei zu schweben.</summary>
    private System.Windows.Forms.IWin32Window? DialogOwner =>
        Window.GetWindow(this) is { } window ? new Win32DialogOwner(new WindowInteropHelper(window).Handle) : null;

    private sealed class Win32DialogOwner(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    /// <summary>Vom Hauptfenster aufgerufen, um den Checkbox-Zustand hier zu spiegeln, ohne den Toggle erneut auszulösen (bidirektionale Synchronisierung mit der Ergebnisleiste im Hauptfenster).</summary>
    public void SetHideDescriptionCheckboxState(bool hide)
    {
        _suppressHideDescriptionEvent = true;
        try
        {
            HideDescriptionCheckBox.IsChecked = hide;
        }
        finally
        {
            _suppressHideDescriptionEvent = false;
        }
    }

    private void HideDescriptionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressHideDescriptionEvent || _context is null)
        {
            return;
        }

        _context.SetHideDescription(HideDescriptionCheckBox.IsChecked == true);
    }

    private SearchOptions CurrentOptions => new()
    {
        MatchCase = MatchCaseCheckBox.IsChecked == true,
        WholeWord = WholeWordCheckBox.IsChecked == true,
        IgnoreFormatting = IgnoreFormattingCheckBox.IsChecked == true,
    };

    /// <summary>
    /// Stellt die Suche auf "Ordner oder Datei" mit diesem Ordner bzw. dieser einzelnen Datei ein und setzt den Cursor in das
    /// Suchfeld (Lupe und Rechtsklick "In Dateien suchen" in der Verzeichnis-Übersicht).
    /// </summary>
    public void SearchInPath(string path)
    {
        FilesModeRadio.IsChecked = true;
        FolderTextBox.Text = path;
        UpdateModeVisibility();
        FocusSearchBox();
    }

    /// <summary>
    /// Wechselt zwischen "Aktuelles Dokument" und "Ordner oder Datei", hin und zurück (Tab im Suchfenster, siehe
    /// SearchReplaceWindow.Window_PreviewKeyDown). <paramref name="focused"/> ist das Element, das vor dem Wechsel den Fokus hatte.
    /// </summary>
    public void ToggleMode(DependencyObject? focused)
    {
        (IsFilesMode ? CurrentDocumentModeRadio : FilesModeRadio).IsChecked = true;

        // Lag der Fokus im jetzt ausgeblendeten Ordnerbereich, ginge er verloren und Tab würde nicht mehr zurückschalten.
        if (!IsFilesMode && focused is not null && FolderPanel.IsAncestorOf(focused))
        {
            FindAllButton.Focus();
        }
    }

    /// <summary>Leert die Trefferliste und zeigt <paramref name="status"/> an; <see langword="null"/> lässt den Status stehen.</summary>
    public void ClearResults(string? status)
    {
        _context.Results.Clear();
        if (status is not null)
        {
            StatusText.Text = status;
        }
    }

    private static bool PathExists(string path) => Directory.Exists(path) || File.Exists(path);

    private void ModeRadio_Checked(object sender, RoutedEventArgs e) => UpdateModeVisibility();

    private void UpdateModeVisibility()
    {
        // FolderPanel ist beim ALLERERSTEN Auslösen von ModeRadio_Checked (durch
        // IsChecked="True" auf CurrentDocumentModeRadio direkt in der XAML) unter Umständen
        // noch nicht angelegt, da es in der XAML-Reihenfolge NACH den RadioButtons steht -
        // daher defensiv prüfen, statt eine NullReferenceException zu riskieren.
        if (FolderPanel is null)
        {
            return;
        }

        FolderPanel.Visibility = IsFilesMode ? Visibility.Visible : Visibility.Collapsed;
        BothPanesCheckBox.Visibility = !IsFilesMode && _context?.GetBothDocuments is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        // Bewusst der klassische WinForms-Ordnerauswahldialog (siehe UseWindowsForms im
        // .csproj) statt des neueren Microsoft.Win32.OpenFolderDialog aus .NET 8 - Letzterer
        // konnte in dieser Cloud-Umgebung ohne .NET-SDK nicht gegen die tatsächlich installierte
        // SDK-Version verifiziert werden, FolderBrowserDialog ist seit Jahren stabil verfügbar.
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = SearchTexts.BrowseDialogTitle,
        };

        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("search.folder");
        if (dialog.ShowDialog(DialogOwner) == System.Windows.Forms.DialogResult.OK)
        {
            FolderTextBox.Text = dialog.SelectedPath;
        }
    }

    /// <summary>Tauscht Such- und Ersetzen-Text (wie in Notepad++); der Fokus bleibt im Suchfeld.</summary>
    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        (SearchTextBox.Text, ReplaceTextBox.Text) = (ReplaceTextBox.Text, SearchTextBox.Text);
        SearchTextBox.Focus();
        SearchTextBox.CaretIndex = SearchTextBox.Text.Length;
    }

    private void AddFavorite_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderTextBox.Text.Trim();
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        if (!_favorites.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            _favorites.Add(folder);
            SaveFavoritesMerged();
        }
    }

    private void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (FavoritesList.SelectedItem is string selected)
        {
            _favorites.Remove(selected);
            SaveFavoritesMerged();
        }
    }

    /// <summary>Speichert die Favoriten und übernimmt dabei Einträge, die ein anderes Fenster inzwischen ergänzt hat.</summary>
    private void SaveFavoritesMerged()
    {
        var merged = SearchFavoritesStore.SaveMerged(_favoritesBaseline, _favorites.ToList(), _favoritesPath);
        _favorites.Clear();
        foreach (var folder in merged)
        {
            _favorites.Add(folder);
        }

        _favoritesBaseline = merged.ToList();
    }

    private void FavoritesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FavoritesList.SelectedItem is string selected)
        {
            FolderTextBox.Text = selected;
        }
    }

    /// <summary>
    /// Enter im Suchfeld (und im Ordnerfeld) startet die Suche; Umschalt+Enter lässt das Suchfeld eine neue Zeile beginnen (mehrzeilige Codeblöcke).
    /// </summary>
    private async void SearchInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            await FindAllAsync(!IsFilesMode);
        }
    }

    /// <summary>
    /// Ein Klick auf eine Fläche ohne Eingabeelement (Beschriftung, leerer Bereich) nimmt dem Textfeld den Fokus, damit dort kein Cursor stehen
    /// bleibt: WPF lässt den Fokus sonst im Textfeld, solange man nicht in ein anderes fokussierbares Element klickt. Klicks auf Felder, Knöpfe, Listen und
    /// Optionen (auch das Ersetzen-Feld) bleiben unberührt. Die Suche nach oben endet an RootGrid, damit ein fokussierbarer Gastgeber (etwa ein
    /// TabItem im rechten Panel) nicht als angeklicktes Eingabeelement zählt.
    /// </summary>
    private void Panel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null && node != RootGrid; node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node is UIElement { Focusable: true })
            {
                return;
            }
        }

        Keyboard.Focus(RootGrid);
    }

    /// <summary>
    /// Führt eine Aufgabe über viele Dateien im Hintergrund aus, mit dem allgemeinen Fortschrittsfenster (Balken, Abbrechen). Eine einzelne Datei läuft ohne Fenster.
    /// <see langword="null"/> = abgebrochen. Solange die Aufgabe läuft, ist dieses Panel gesperrt.
    /// </summary>
    private async Task<T?> RunFileTaskAsync<T>(string path, string title, Func<CancellationToken, IProgress<(int Done, int Total)>?, T> work)
        where T : class
    {
        if (File.Exists(path))
        {
            return work(CancellationToken.None, null);
        }

        using var cancellation = new CancellationTokenSource();
        var progressWindow = new TaskProgressWindow(title, SettingsTexts.ProgressHint, SettingsTexts.Cancel, SettingsTexts.ProgressCount, cancellation.Cancel) { Owner = Window.GetWindow(this) };
        var progress = new Progress<(int Done, int Total)>(value => progressWindow.Report(new CompatProgress(SettingsTexts.ProgressFiles, value.Done, value.Total)));
        _busy = true;
        RootGrid.IsEnabled = false;

        progressWindow.Show();
        try
        {
            return await Task.Run(() => work(cancellation.Token, progress));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            progressWindow.CloseAfterFinished();
            RootGrid.IsEnabled = true;
            _busy = false;

            // Im Reiter des Hauptfensters kann der Nutzer während der Aufgabe im Editor weitergearbeitet haben: dann den Fokus nicht ins
            // Suchfeld ziehen (Tastendrücke landeten sonst im Suchtext, Enter startete eine neue Suche). Sonst wie bisher ins Suchfeld.
            var focused = Keyboard.FocusedElement as DependencyObject;
            var focusElsewhereInWindow = focused is not null and not Window && !IsAncestorOf(focused) && Window.GetWindow(focused) is { } focusWindow
                && ReferenceEquals(focusWindow, Window.GetWindow(this));
            if (!focusElsewhereInWindow)
            {
                SearchTextBox.Focus();
            }
        }
    }

    private async void FindAll_Click(object sender, RoutedEventArgs e) => await FindAllAsync(false);

    /// <summary>Suchsignatur der letzten Enter-Suche im Dokument und Index des zuletzt angesprungenen Treffers (weitere Enter springen zum nächsten).</summary>
    private string? _jumpSignature;

    private int _jumpIndex;

    /// <summary>
    /// Enter im Suchfeld (Dokumentmodus): springt zum ersten Treffer, jedes weitere Enter mit derselben Suche zum nächsten (am Ende wieder zum ersten).
    /// Der Fokus bleibt im Suchfeld, damit Enter weitersucht statt im Dokument die Auswahl zu ersetzen.
    /// </summary>
    private void JumpToHit(string query)
    {
        var count = _context.Results.Count;
        if (count == 0)
        {
            _jumpSignature = null;
            return;
        }

        var signature = string.Join("", query, MatchCaseCheckBox.IsChecked, WholeWordCheckBox.IsChecked, IgnoreFormattingCheckBox.IsChecked, BothPanesCheckBox.IsChecked);
        _jumpIndex = signature == _jumpSignature ? (_jumpIndex + 1) % count : 0;
        _jumpSignature = signature;

        ResultsList.SelectedIndex = _jumpIndex;
        ResultsList.ScrollIntoView(_context.Results[_jumpIndex]);
        SelectRow(_context.Results[_jumpIndex]);
        SearchTextBox.Focus();
    }

    private void SelectRow(SearchResultRow row)
    {
        if (row.SelectAction is { } select)
        {
            select(row.Offset, row.Length);
        }
        else
        {
            _context.SelectInActiveDocument(row.Offset, row.Length);
        }
    }

    private async Task FindAllAsync(bool jumpToHit)
    {
        // Vor Initialize (der Reiter wird erst nach dem Laden der Sitzung eingerichtet) gibt es noch keinen Kontext.
        if (_busy || _context is null)
        {
            return;
        }

        var query = SearchTextBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            StatusText.Text = SearchTexts.EnterQuery;
            return;
        }

        _context.Results.Clear();

        if (IsFilesMode)
        {
            var folder = NormalizeFolderPath(FolderTextBox.Text.Trim());
            if (!PathExists(folder))
            {
                StatusText.Text = SearchTexts.PathNotFound;
                return;
            }

            RememberFolder();
            var options = CurrentOptions;
            var matches = await RunFileTaskAsync(folder, SearchTexts.ProgressFind, (cancellation, progress) => FileSearchService.Search(folder, query, options, cancellation: cancellation, progress: progress));
            if (matches is null)
            {
                StatusText.Text = CommonTexts.Cancelled;
                return;
            }

            foreach (var match in matches)
            {
                _context.Results.Add(SearchResultRow.FromFileMatch(match, _context.GetHideDescription()));
            }

            var fileCount = matches.Select(m => m.FilePath).Distinct().Count();
            StatusText.Text = SearchTexts.FilesFound(matches.Count, fileCount);
            _context.SearchPerformed?.Invoke(false);
        }
        else
        {
            var bothPanes = BothPanesCheckBox.IsChecked == true && _context.GetBothDocuments is not null;
            var found = 0;
            if (bothPanes)
            {
                foreach (var source in _context.GetBothDocuments!())
                {
                    foreach (var match in TextSearch.FindAll(source.GetText(), query, CurrentOptions))
                    {
                        _context.Results.Add(SearchResultRow.FromDocumentMatch(match, _context.GetHideDescription(), source.Label, source.Select));
                        found++;
                    }
                }
            }
            else
            {
                var text = _context.GetActiveDocumentText();
                foreach (var match in TextSearch.FindAll(text, query, CurrentOptions))
                {
                    _context.Results.Add(SearchResultRow.FromDocumentMatch(match, _context.GetHideDescription()));
                    found++;
                }
            }

            StatusText.Text = SearchTexts.DocumentFound(found);
            _context.SearchPerformed?.Invoke(true);
            _context.SearchedBothPanes?.Invoke(bothPanes);
            if (jumpToHit)
            {
                JumpToHit(query);
            }
        }
    }

    private async void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _context is null)
        {
            return;
        }

        var query = SearchTextBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            StatusText.Text = SearchTexts.EnterQuery;
            return;
        }

        var replacement = ReplaceTextBox.Text;

        if (IsFilesMode)
        {
            var folder = NormalizeFolderPath(FolderTextBox.Text.Trim());
            if (!PathExists(folder))
            {
                StatusText.Text = SearchTexts.PathNotFound;
                return;
            }

            RememberFolder();
            var options = CurrentOptions;
            var plans = await RunFileTaskAsync(
                folder,
                SearchTexts.ProgressReplace,
                (cancellation, progress) => FileReplaceService.ComputeReplacements(FileSearchService.EnumerateSearchableFiles(folder), query, replacement, options, cancellation, progress));
            if (plans is null)
            {
                StatusText.Text = CommonTexts.Cancelled;
                return;
            }

            if (plans.Count == 0)
            {
                StatusText.Text = SearchTexts.NothingReplaced;
                return;
            }

            // Vor dem Schreiben prüfen: danach ist der alte Stand der Dateien weg, mit dem die neuen Blöcke verglichen werden.
            var hits = await Task.Run(() => plans.SelectMany(plan => BlocksAddedBy(plan, folder)).ToList());

            var totalReplacements = plans.Sum(p => p.ReplacementCount);
            var description = SearchTexts.ReplaceHistoryDescription(query, replacement, plans.Count);
            var changedFiles = _context.ExecuteFileReplacements(plans, description);

            StatusText.Text = SearchTexts.FilesReplaced(totalReplacements, changedFiles);

            _context.Results.Clear();
            foreach (var plan in plans)
            {
                _context.Results.Add(SearchResultRow.FromReplacementPlan(plan, _context.GetHideDescription()));
            }

            _context.SearchPerformed?.Invoke(false);
            ReplaceWarningHub.Report(hits);
        }
        else
        {
            var text = _context.GetActiveDocumentText();
            var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, CurrentOptions);

            if (count == 0)
            {
                StatusText.Text = SearchTexts.DocumentNothingReplaced;
                return;
            }

            _context.ReplaceActiveDocumentText(newText);
            StatusText.Text = SearchTexts.DocumentReplaced(count);
            _context.Results.Clear();
            _context.SearchPerformed?.Invoke(true);

            ReplaceWarningHub.Report(SingleEntryBlockFinder.NewIn(text, newText).Select(block => new BlockWarningHit(SearchTexts.ModeCurrentDocument, block, null)).ToList());
        }
    }

    /// <summary>OR-/NOR-/AND-Blöcke mit höchstens einem Eintrag, die der Ersetzungsplan in einer Datei neu erzeugt (Vergleich mit dem Stand auf der Platte).</summary>
    private IEnumerable<BlockWarningHit> BlocksAddedBy(FileReplacementPlan plan, string root)
    {
        string oldText;
        try
        {
            oldText = File.ReadAllText(plan.FilePath, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Enumerable.Empty<BlockWarningHit>();
        }

        var display = Directory.Exists(root) ? Path.GetRelativePath(root, plan.FilePath) : Path.GetFileName(plan.FilePath);
        return SingleEntryBlockFinder.NewIn(oldText, plan.NewContent)
            .Select(block => new BlockWarningHit(display, block, plan.FilePath));
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_context is null || ResultsList.SelectedItem is not SearchResultRow row)
        {
            return;
        }

        if (row.FilePath is { } filePath)
        {
            _context.OpenSearchResult(filePath, row.Offset, row.Length);
        }
        else
        {
            SelectRow(row);
        }
    }

    /// <summary>
    /// Normalisiert den eingegebenen/durchsuchten Ordnerpfad über <see cref="Path.GetFullPath"/>,
    /// bevor er für die Dateiaufzählung verwendet wird (Nutzer-Feedback: "nach Find und Replace in
    /// einem Ordner wird die geöffnete Datei nicht aktualisiert"). Ursache: ein z. B. aus dem
    /// Windows-Explorer-Adressfeld eingefügter Ordnerpfad endet häufig auf einen Backslash — ohne
    /// Normalisierung würde <see cref="Directory.EnumerateFiles(string)"/> dadurch Dateipfade mit
    /// doppeltem Backslash liefern, die rein textuell nicht mehr zu dem über den Dateidialog
    /// geöffneten (bereits kanonischen) Pfad derselben Datei passen — der Tab-Abgleich in
    /// <see cref="ParadoxModTool.Editor.EditorPane.ReloadIfOpen"/> schlägt dann fehl, obwohl es
    /// dieselbe Datei ist. Bei einem ungültigen Pfad wird der unveränderte, nur getrimmte Wert
    /// zurückgegeben, damit die anschließende <see cref="Directory.Exists"/>-Prüfung wie gewohnt
    /// eine verständliche Fehlermeldung liefert, statt hier eine Ausnahme durchschlagen zu lassen.
    /// </summary>
    private static string NormalizeFolderPath(string folder)
    {
        if (string.IsNullOrEmpty(folder))
        {
            return folder;
        }

        try
        {
            return Path.GetFullPath(folder);
        }
        catch (ArgumentException)
        {
            return folder;
        }
        catch (NotSupportedException)
        {
            return folder;
        }
        catch (PathTooLongException)
        {
            return folder;
        }
    }
}
