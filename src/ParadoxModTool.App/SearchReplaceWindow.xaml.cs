using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace ParadoxModTool.App;

/// <summary>
/// Vereinheitlichtes Suchen-&amp;-Ersetzen-Fenster (Abschnitt 5b): "Suchen und Ersetzen und
/// Suchen in Dateien kann in einem Funktionskasten vereinheitlicht werden [...] es wäre gut,
/// wenn es einfach ist zwischen den Modi hin und her zu wechseln." Für den Modus "Aktuelles
/// Dokument" arbeitet dieses Fenster ausschließlich über die vom Hauptfenster übergebenen
/// Delegates (damit es selbst keine Kenntnis von EditorPane/ActionHistory braucht); für den
/// Modus "Ordner" ruft es die reinen Lesevorgänge aus ParadoxModTool.SearchReplace direkt auf,
/// nur das tatsächliche Schreiben beim Ersetzen läuft über den Delegate ins Hauptfenster (damit
/// es dort als EIN Schritt im programmweiten Aktionsverlauf landet, siehe ParadoxModTool.UndoRedo).
/// </summary>
public partial class SearchReplaceWindow : Window
{
    private readonly Func<string> _getActiveDocumentText;
    private readonly Action<int, int> _selectInActiveDocument;
    private readonly Action<string> _replaceActiveDocumentText;
    private readonly Func<IReadOnlyList<FileReplacementPlan>, string, int> _executeFileReplacements;
    private readonly Action<string, int, int> _openSearchResult;
    private readonly Func<bool> _getHideDescription;
    private readonly Action<bool> _setHideDescription;

    private readonly string _favoritesPath = SearchFavoritesStore.GetDefaultFavoritesFilePath();
    private readonly ObservableCollection<string> _favorites = new();

    // Stand der Favoriten beim letzten Laden/Speichern (siehe SearchFavoritesStore.SaveMerged).
    private List<string> _favoritesBaseline = new();

    /// <summary>Aktives Spielprofil (für die 4 FESTEN Schnellzugriff-Buttons - Mod-Ordner/in_game/
    /// main_menu/loading_screen, siehe <see cref="QuickPathBar"/>) sowie der
    /// Callback zum Auflösen des Mod-Ordner-Pfads (dieselbe Logik wie beim automatischen
    /// Vorschlag der Verzeichnis-Übersicht, siehe MainWindow.ResolveDefaultModRoot - hier als
    /// Delegate übergeben, damit dieses Fenster keine eigene Kopie der Auflösungslogik braucht).</summary>
    private readonly GameProfile? _activeProfile;
    private readonly Func<string?> _resolveDefaultModRoot;

    /// <summary>Liefert den Ordner mit den eigentlichen Vanilla-Spieldateien (common/gui/in_game/...,
    /// z. B. ".../Europa Universalis V/game") - Basis für die in_game/main_menu/loading_screen-
    /// Buttons. Bei einer echten EU5-Installation liegt dieser "game"-Ordner als GESCHWISTER neben
    /// "binaries" (mit der .exe), nicht darunter - vom Nutzer am 2026-09-17 bestätigt, siehe
    /// MainWindow.ComputeGameContentRoot. <see langword="null"/>, solange kein Spielpfad eingestellt
    /// ist oder der "game"-Ordner (noch) nicht existiert.</summary>
    private readonly Func<string?> _resolveGameContentRoot;

    /// <summary>Merkt sich den zuletzt durchsuchten Ordner über das Schließen des Fensters hinaus (Sitzungszustand im Hauptfenster); beide null = nichts merken.</summary>
    private readonly Func<string?>? _getLastFolder;
    private readonly Action<string>? _setLastFolder;

    /// <summary>
    /// Die Trefferliste wird vom Hauptfenster übergeben und geteilt (Nutzer-Feedback: "Ich hätte
    /// auch gern noch die Option die Suchergebnisse aus dem Suchfenster [...] in einem Fenster
    /// über meinem Arbeitsbereich anzeigen zu lassen [...]. Die Ergebnisse im Suchfenster können
    /// aber auf jeden Fall bleiben!") — ein einzelnes <see cref="ObservableCollection{T}"/>-Objekt,
    /// das sowohl an <see cref="ResultsList"/> hier als auch an die neue Ergebnisleiste im
    /// Hauptfenster gebunden ist, sodass beide Anzeigen automatisch synchron bleiben, ohne dass
    /// zwei getrennte Kopien der Treffer gepflegt werden müssten.
    /// </summary>
    private readonly ObservableCollection<SearchResultRow> _results;

    /// <summary>Verhindert eine Endlosschleife, wenn das Hauptfenster den Checkbox-Zustand hier synchronisiert (siehe <see cref="SetHideDescriptionCheckboxState"/>).</summary>
    private bool _suppressHideDescriptionEvent;

    public SearchReplaceWindow(
        Func<string> getActiveDocumentText,
        Action<int, int> selectInActiveDocument,
        Action<string> replaceActiveDocumentText,
        Func<IReadOnlyList<FileReplacementPlan>, string, int> executeFileReplacements,
        Action<string, int, int> openSearchResult,
        ObservableCollection<SearchResultRow> sharedResults,
        Func<bool> getHideDescription,
        Action<bool> setHideDescription,
        GameProfile? activeProfile,
        Func<string?> resolveDefaultModRoot,
        Func<string?> resolveGameContentRoot,
        Func<string?>? getLastFolder = null,
        Action<string>? setLastFolder = null)
    {
        InitializeComponent();
        ApplyTexts();
        Loaded += (_, _) => FocusSearchBox();
        Closing += (_, _) =>
        {
            RememberFolder();
            ReturnFocusToOwner();
        };

        _getActiveDocumentText = getActiveDocumentText;
        _selectInActiveDocument = selectInActiveDocument;
        _replaceActiveDocumentText = replaceActiveDocumentText;
        _executeFileReplacements = executeFileReplacements;
        _openSearchResult = openSearchResult;
        _results = sharedResults;
        _getHideDescription = getHideDescription;
        _setHideDescription = setHideDescription;
        _activeProfile = activeProfile;
        _resolveDefaultModRoot = resolveDefaultModRoot;
        _resolveGameContentRoot = resolveGameContentRoot;
        _getLastFolder = getLastFolder;
        _setLastFolder = setLastFolder;

        ResultsList.ItemsSource = _results;
        ResultsListCopy.Attach(ResultsList, ResultsDetail);
        FavoritesList.ItemsSource = _favorites;
        HideDescriptionCheckBox.IsChecked = _getHideDescription();

        foreach (var folder in SearchFavoritesStore.TryLoad(_favoritesPath))
        {
            _favorites.Add(folder);
        }

        _favoritesBaseline = _favorites.ToList();

        QuickPaths.Initialize(_activeProfile, _resolveDefaultModRoot, _resolveGameContentRoot);
        QuickPaths.PathChosen += (_, path) => FolderTextBox.Text = path;
        QuickPathBar.AcceptDrops(FolderTextBox);

        if (_getLastFolder?.Invoke() is { Length: > 0 } lastFolder)
        {
            FolderTextBox.Text = lastFolder;
        }

        UpdateModeVisibility();
    }

    /// <summary>Gibt den Inhalt des Ordnerfelds an das Hauptfenster, damit ein neu geöffnetes Fenster wieder dort sucht.</summary>
    private void RememberFolder()
    {
        if (FolderTextBox.Text.Trim() is { Length: > 0 } folder)
        {
            _setLastFolder?.Invoke(folder);
        }
    }

    private void ApplyTexts()
    {
        Title = SearchTexts.Title;
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
        MatchCaseCheckBox.Content = SearchTexts.MatchCase;
        WholeWordCheckBox.Content = SearchTexts.WholeWord;
        IgnoreFormattingCheckBox.Content = SearchTexts.IgnoreFormatting;
        IgnoreFormattingCheckBox.ToolTip = SearchTexts.IgnoreFormattingTip;
        FindAllButton.Content = SearchTexts.FindAll;
        ReplaceAllButton.Content = SearchTexts.ReplaceAll;
        HideDescriptionCheckBox.Content = SearchTexts.HideDescription;
        HideDescriptionCheckBox.ToolTip = SearchTexts.HideDescriptionTip;
        ResultsList.ToolTip = SearchTexts.ResultsTip;
    }

    /// <summary>
    /// Setzt den Cursor in das Suchfeld und markiert dessen Inhalt (Nutzer-Feedback: nach Strg+F sofort tippen können, ohne erst
    /// in das Fenster zu klicken). Läuft über den Dispatcher, weil ein gerade erst gezeigtes oder aktiviertes Fenster den
    /// Tastaturfokus sonst noch nicht annimmt.
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
            Activate();
            SearchTextBox.Focus();
            Keyboard.Focus(SearchTextBox);
            SearchTextBox.SelectAll();
        });
    }

    /// <summary>
    /// Holt beim Schließen das Besitzerfenster nach vorn (Nutzer-Feedback: beim Schließen wurde das Hauptfenster manchmal
    /// minimiert). Windows aktiviert nach dem Schließen sonst das nächste Fenster in der Z-Reihenfolge, das nach einem
    /// Ordnerdialog oder einem Klick in ein anderes Programm nicht mehr das Hauptfenster ist.
    /// </summary>
    private void ReturnFocusToOwner()
    {
        // Schließt das Hauptfenster selbst (und damit dieses Fenster), ist es schon unsichtbar: dann nichts tun.
        if (Owner is not { IsVisible: true } owner)
        {
            return;
        }

        try
        {
            if (owner.WindowState == WindowState.Minimized)
            {
                owner.WindowState = WindowState.Normal;
            }

            owner.Activate();
        }
        catch (InvalidOperationException)
        {
            // Besitzer wird gerade geschlossen.
        }
    }

    /// <summary>Besitzer für die WinForms-Ordnerdialoge, damit sie zu diesem Fenster gehören statt frei zu schweben.</summary>
    private System.Windows.Forms.IWin32Window DialogOwner => new Win32DialogOwner(new WindowInteropHelper(this).Handle);

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
        if (_suppressHideDescriptionEvent)
        {
            return;
        }

        _setHideDescription(HideDescriptionCheckBox.IsChecked == true);
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

    private static bool PathExists(string path) => Directory.Exists(path) || File.Exists(path);

    private bool IsFilesMode => FilesModeRadio.IsChecked == true;

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
    private void SearchInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            FindAll_Click(sender, e);
        }
    }

    /// <summary>
    /// Tab (und Umschalt+Tab) wechselt zwischen "Aktuelles Dokument" und "Ordner oder Datei", hin und zurück. Im Suchfeld und im Ersetzen-Feld
    /// bleibt Tab ein Tab-Zeichen (eingerückten Code von Hand tippen); dort greift der Wechsel nicht.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Tab || _busy || Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift) || Keyboard.FocusedElement is System.Windows.Controls.TextBox { AcceptsTab: true })
        {
            return;
        }

        e.Handled = true;
        var focused = Keyboard.FocusedElement as DependencyObject;
        (IsFilesMode ? CurrentDocumentModeRadio : FilesModeRadio).IsChecked = true;

        // Lag der Fokus im jetzt ausgeblendeten Ordnerbereich, ginge er verloren und Tab würde nicht mehr zurückschalten.
        if (!IsFilesMode && focused is not null && FolderPanel.IsAncestorOf(focused))
        {
            FindAllButton.Focus();
        }
    }

    /// <summary>
    /// Ein Klick auf eine Fläche ohne Eingabeelement (Beschriftung, leerer Bereich, Fensterrand) nimmt dem Textfeld den Fokus, damit dort kein Cursor stehen
    /// bleibt: WPF lässt den Fokus sonst im Textfeld, solange man nicht in ein anderes fokussierbares Element klickt. Klicks auf Felder, Knöpfe, Listen und
    /// Optionen (auch das Ersetzen-Feld) bleiben unberührt.
    /// </summary>
    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node is UIElement { Focusable: true } && node is not Window && node != RootGrid)
            {
                return;
            }
        }

        Keyboard.Focus(RootGrid);
    }

    private bool _busy;

    /// <summary>
    /// Führt eine Aufgabe über viele Dateien im Hintergrund aus, mit dem allgemeinen Fortschrittsfenster (Balken, Abbrechen). Eine einzelne Datei läuft ohne Fenster.
    /// <see langword="null"/> = abgebrochen. Solange die Aufgabe läuft, ist dieses Fenster gesperrt.
    /// </summary>
    private async Task<T?> RunFileTaskAsync<T>(string path, string title, Func<CancellationToken, IProgress<(int Done, int Total)>?, T> work)
        where T : class
    {
        if (File.Exists(path))
        {
            return work(CancellationToken.None, null);
        }

        using var cancellation = new CancellationTokenSource();
        var progressWindow = new TaskProgressWindow(title, SettingsTexts.ProgressHint, SettingsTexts.Cancel, SettingsTexts.ProgressCount, cancellation.Cancel) { Owner = this };
        var progress = new Progress<(int Done, int Total)>(value => progressWindow.Report(new CompatProgress(SettingsTexts.ProgressFiles, value.Done, value.Total)));
        _busy = true;
        if (Content is UIElement root)
        {
            root.IsEnabled = false;
        }

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
            if (Content is UIElement unlocked)
            {
                unlocked.IsEnabled = true;
            }

            _busy = false;
            SearchTextBox.Focus();
        }
    }

    private async void FindAll_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var query = SearchTextBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            StatusText.Text = SearchTexts.EnterQuery;
            return;
        }

        _results.Clear();

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
                _results.Add(SearchResultRow.FromFileMatch(match, _getHideDescription()));
            }

            var fileCount = matches.Select(m => m.FilePath).Distinct().Count();
            StatusText.Text = SearchTexts.FilesFound(matches.Count, fileCount);
        }
        else
        {
            var text = _getActiveDocumentText();
            var matches = TextSearch.FindAll(text, query, CurrentOptions);
            foreach (var match in matches)
            {
                _results.Add(SearchResultRow.FromDocumentMatch(match, _getHideDescription()));
            }

            StatusText.Text = SearchTexts.DocumentFound(matches.Count);
        }
    }

    private async void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
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
            var changedFiles = _executeFileReplacements(plans, description);

            StatusText.Text = SearchTexts.FilesReplaced(totalReplacements, changedFiles);

            _results.Clear();
            foreach (var plan in plans)
            {
                _results.Add(SearchResultRow.FromReplacementPlan(plan, _getHideDescription()));
            }

            ReplaceWarningHub.Report(hits);
        }
        else
        {
            var text = _getActiveDocumentText();
            var (newText, count) = TextSearch.ReplaceAll(text, query, replacement, CurrentOptions);

            if (count == 0)
            {
                StatusText.Text = SearchTexts.DocumentNothingReplaced;
                return;
            }

            _replaceActiveDocumentText(newText);
            StatusText.Text = SearchTexts.DocumentReplaced(count);
            _results.Clear();

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
        if (ResultsList.SelectedItem is not SearchResultRow row)
        {
            return;
        }

        if (row.FilePath is { } filePath)
        {
            _openSearchResult(filePath, row.Offset, row.Length);
        }
        else
        {
            _selectInActiveDocument(row.Offset, row.Length);
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

/// <summary>
/// Eine Zeile in der Trefferliste — sowohl für Dokument- als auch Dateisuche/-ersetzung
/// verwendet, UND von zwei Anzeigen gleichzeitig gebunden (dem modalen Suchen-Fenster sowie der
/// Ergebnisleiste im Hauptfenster, siehe <see cref="SearchReplaceWindow"/>). Implementiert
/// <see cref="INotifyPropertyChanged"/>, damit das Umschalten der "Nur gefundenen Code anzeigen"-
/// Option (Nutzer-Feedback) bereits vorhandene Zeilen live aktualisiert, statt die komplette
/// Liste neu aufbauen zu müssen.
/// </summary>
public sealed class SearchResultRow : INotifyPropertyChanged
{
    private bool _hideDescription;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? FilePath { get; private init; }

    public int LineNumber { get; private init; }

    public string LineText { get; private init; } = string.Empty;

    /// <summary>
    /// Der exakt gefundene Code (siehe <see cref="TextMatch.MatchedText"/>) — Nutzer-Feedback:
    /// "nur den Code, der tatsächlich gefunden wurde, anzeigen ... damit man es rauskopieren und
    /// verwenden kann". Bei einer reinen Ersetzungs-Zusammenfassung (<see cref="FromReplacementPlan"/>)
    /// gibt es keinen einzelnen gefundenen Text, daher dort leer.
    /// </summary>
    public string MatchedText { get; private init; } = string.Empty;

    public int Offset { get; private init; }

    public int Length { get; private init; }

    /// <summary>Die beschreibende Zeile ("Datei — Zeile N: ..."), ausblendbar über <see cref="HideDescription"/>.</summary>
    public string Description => FilePath is null
        ? SearchTexts.ResultLine(LineNumber, LineText)
        : LineNumber > 0
            ? SearchTexts.ResultFileLine(Path.GetFileName(FilePath), LineNumber, LineText)
            : $"{Path.GetFileName(FilePath)} — {LineText}";

    /// <summary>Ob die Beschreibungszeile ausgeblendet ist (Nutzer-Feedback: Toggle für "nur den gefundenen Code anzeigen").</summary>
    public bool HideDescription
    {
        get => _hideDescription;
        private set
        {
            if (_hideDescription == value)
            {
                return;
            }

            _hideDescription = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HideDescription)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DescriptionVisibility)));
        }
    }

    public Visibility DescriptionVisibility => HideDescription ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Wird vom Hauptfenster auf ALLEN Zeilen der geteilten Liste aufgerufen, wenn der Toggle umgeschaltet wird.</summary>
    public void SetHideDescription(bool hide) => HideDescription = hide;

    public static SearchResultRow FromDocumentMatch(TextMatch match, bool hideDescription) => new()
    {
        FilePath = null,
        LineNumber = match.LineNumber,
        LineText = match.LineText,
        MatchedText = match.MatchedText,
        Offset = match.Offset,
        Length = match.Length,
        HideDescription = hideDescription,
    };

    public static SearchResultRow FromFileMatch(FileSearchMatch match, bool hideDescription) => new()
    {
        FilePath = match.FilePath,
        LineNumber = match.Match.LineNumber,
        LineText = match.Match.LineText,
        MatchedText = match.Match.MatchedText,
        Offset = match.Match.Offset,
        Length = match.Match.Length,
        HideDescription = hideDescription,
    };

    public static SearchResultRow FromReplacementPlan(FileReplacementPlan plan, bool hideDescription) => new()
    {
        FilePath = plan.FilePath,
        LineNumber = 0,
        LineText = SearchTexts.ResultReplacements(plan.ReplacementCount),
        MatchedText = string.Empty,
        Offset = 0,
        Length = 0,
        HideDescription = hideDescription,
    };
}
