using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
// Wie in MainWindow.xaml.cs: UseWindowsForms (nur für FolderBrowserDialog) + ImplicitUsings macht OpenFileDialog mehrdeutig (CS0104);
// der Alias löst es zugunsten des WPF-eigenen Dialogs auf.
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.App;

/// <summary>
/// Compare und Update Tool (Abschnitt 7, Nutzerwunsch 2026-09-30): Reiter "Compare" vergleicht zwei frei gewählte Dateien, Reiter
/// "Update" die automatisch erkannten Vanilla/Mod-Paare (<see cref="VanillaFileTracker"/>) mit 3-Wege-Merge nach einem Spiel-Update
/// (<see cref="VanillaMergeWorkflow"/>). Jeder Reiter hat einen eigenen Vergleich (<see cref="DiffComparePane"/>).
/// </summary>
public partial class VanillaDiffWindow : Window
{
    private const string CompareTabId = "compare";
    private const string UpdateTabId = "update";
    private const string ReplaceTabId = "replace";

    private readonly Func<Func<string>, Action<int, int>, Action<string>, Window, SearchReplaceWindow> _createSearchWindow;
    private SearchReplaceWindow? _searchWindow;
    private readonly GameProfile? _activeProfile;
    private readonly Func<string?> _resolveDefaultModRoot;
    private readonly Func<string?> _resolveGameRoot;
    private readonly Func<string?> _getModRootOverride;
    private readonly Action<string?> _setModRootOverride;
    private readonly Func<ParadoxModTool.VersionManager.StorageSettings> _getStorageSettings;
    private readonly Action<string> _setLastTab;
    private readonly Func<IProgress<(int Done, int Total)>?, SnapshotRecordResult> _recordSnapshots;
    private bool _recording;
    private readonly Func<string, string> _modStateDirectory;
    private readonly Action<VanillaUpdateLaunch> _openUpdateMerge;
    private readonly Func<IReadOnlyList<string>> _getHiddenKinds;
    private readonly Action<IReadOnlyList<string>> _setHiddenKinds;
    private readonly FileKindFilterBar _updateKinds;
    private readonly FileKindFilterBar _folderKinds;
    private IReadOnlyList<OverriddenFile> _overridden = Array.Empty<OverriddenFile>();
    private bool _suppressListEvent;
    private bool _switchingTab;

    /// <summary>Aktuell gewähltes Vanilla/Mod-Paar im Update-Reiter.</summary>
    private OverriddenFile? _selectedOverriddenFile;

    private VanillaMergeBaselineResult? _mergeBaseline;
    private string? _detectedGameVersion;
    private string? _lastCommittedManualVersion;

    public VanillaDiffWindow(
        GameProfile? activeProfile,
        Func<string?> resolveDefaultModRoot,
        Func<string?> resolveGameRoot,
        Func<string?> getModRootOverride,
        Action<string?> setModRootOverride,
        Func<ParadoxModTool.VersionManager.StorageSettings> getStorageSettings,
        Func<bool> getFoldingEnabled,
        Action<bool> setFoldingEnabled,
        Func<Func<string>, Action<int, int>, Action<string>, Window, SearchReplaceWindow> createSearchWindow,
        Func<string> getLastTab,
        Action<string> setLastTab,
        Func<IProgress<(int Done, int Total)>?, SnapshotRecordResult> recordSnapshots,
        Func<string, string> modStateDirectory,
        Action<VanillaUpdateLaunch> openUpdateMerge,
        Func<IReadOnlyList<string>> getHiddenKinds,
        Action<IReadOnlyList<string>> setHiddenKinds,
        Func<string, string> replaceListFile,
        Func<IReadOnlyList<ParadoxModTool.SearchReplace.ReplaceLists.ReplaceFileChange>, string, string, FixOutcome> applyReplaceList)
    {
        InitializeComponent();

        _createSearchWindow = createSearchWindow;
        _activeProfile = activeProfile;
        _resolveDefaultModRoot = resolveDefaultModRoot;
        _resolveGameRoot = resolveGameRoot;
        _getModRootOverride = getModRootOverride;
        _setModRootOverride = setModRootOverride;
        _getStorageSettings = getStorageSettings;
        _setLastTab = setLastTab;
        _recordSnapshots = recordSnapshots;
        _modStateDirectory = modStateDirectory;
        _openUpdateMerge = openUpdateMerge;
        _getHiddenKinds = getHiddenKinds;
        _setHiddenKinds = setHiddenKinds;
        _updateKinds = new FileKindFilterBar(getHiddenKinds());
        _folderKinds = new FileKindFilterBar(getHiddenKinds());
        UpdateKindsHost.Content = _updateKinds;
        FolderKindsHost.Content = _folderKinds;
        _updateKinds.Changed += (_, _) => HiddenKindsChanged(_updateKinds, _folderKinds);
        _folderKinds.Changed += (_, _) => HiddenKindsChanged(_folderKinds, _updateKinds);

        ComparePane.Initialize(getFoldingEnabled, setFoldingEnabled, AskSaveWithoutSnapshot);
        UpdatePane.Initialize(getFoldingEnabled, setFoldingEnabled, SnapshotModBeforeSave);
        foreach (var (pane, status) in new[] { (ComparePane, CompareStatusText), (UpdatePane, UpdateStatusText) })
        {
            pane.StatusChanged += (_, text) => status.Text = text;
            pane.SearchRequested += (_, _) => OpenSearchWindow();
        }

        ComparePane.FoldingToggled += (_, on) => UpdatePane.ApplyFolding(on);
        UpdatePane.FoldingToggled += (_, on) => ComparePane.ApplyFolding(on);

        CompareQuickPaths.Initialize(activeProfile, resolveDefaultModRoot, resolveGameRoot);
        CompareQuickPaths.PathChosen += (_, folder) => ChooseFileIn(folder);
        FileABox.GotKeyboardFocus += (_, _) => _lastFileBox = FileABox;
        FileBBox.GotKeyboardFocus += (_, _) => _lastFileBox = FileBBox;
        QuickPathBar.AcceptDrops(FileABox);
        QuickPathBar.AcceptDrops(FileBBox);
        ComparePane.FileSaved += (_, path) => RecheckSavedFolderFile(path);

        _switchingTab = true;
        ReplaceListPane.Initialize(ResolveModRoot, replaceListFile, (changes, modFolder, listName) => ApplyReplaceList(changes, modFolder, listName, applyReplaceList));
        Tabs.SelectedItem = getLastTab() switch
        {
            CompareTabId => CompareTab,
            ReplaceTabId => ReplaceTab,
            _ => UpdateTab,
        };
        _switchingTab = false;

        LoadOverriddenFiles();
        RecordSnapshotsInBackground();
        if (ReferenceEquals(Tabs.SelectedItem, ReplaceTab))
        {
            ReplaceListPane.Refresh();
        }
    }

    /// <summary>Vergleich des sichtbaren Reiters.</summary>
    private DiffComparePane ActivePane => ReferenceEquals(Tabs.SelectedItem, CompareTab) ? ComparePane : UpdatePane;

    /// <summary>
    /// Lädt ein Dateipaar mit eigenen Beschriftungen in den Compare-Reiter (z. B. dieselbe Datei in zwei Mods eines Projekts). Fragt bei
    /// ungespeicherten Änderungen zuerst nach.
    /// </summary>
    public void OpenFilePair(string leftPath, string rightPath, string leftLabel, string rightLabel)
    {
        Tabs.SelectedItem = CompareTab;
        if (!ComparePane.ConfirmProceedWithUnsavedChanges(this))
        {
            return;
        }

        ShowFolderList(false);
        FileABox.Text = leftPath;
        FileBBox.Text = rightPath;
        ComparePane.LoadPair(leftPath, rightPath, leftLabel, rightLabel, leftEditable: true, rightEditable: true);
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, Tabs) || _switchingTab)
        {
            return;
        }

        _setLastTab(ReferenceEquals(Tabs.SelectedItem, CompareTab) ? CompareTabId : ReferenceEquals(Tabs.SelectedItem, ReplaceTab) ? ReplaceTabId : UpdateTabId);
        if (ReferenceEquals(Tabs.SelectedItem, UpdateTab))
        {
            RecordSnapshotsInBackground();
        }
        else if (ReferenceEquals(Tabs.SelectedItem, ReplaceTab))
        {
            ReplaceListPane.Refresh();
        }
    }

    /// <summary>
    /// Alte Vanilla-Fassungen still im Hintergrund ablegen (beim Öffnen und beim Wechsel in den Update-Reiter), damit auch ein Spiel, das
    /// während PMT läuft auf eine alte Version gestellt wurde, erfasst wird (Spec 2026-09-30-vanilla-update-merge, Abschnitt 7.1).
    /// </summary>
    private void RecordSnapshotsInBackground()
    {
        if (_recording)
        {
            return;
        }

        _recording = true;
        Task.Run(() =>
        {
            try
            {
                _recordSnapshots(null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine("Vanilla-Historie: " + ex.Message);
            }
        }).ContinueWith(_ => Dispatcher.BeginInvoke(() => _recording = false), TaskScheduler.Default);
    }

    private async void ReadVersionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recording)
        {
            return;
        }

        var version = _resolveGameRoot() is { } root ? GameVersionReader.TryReadVersion(root) : null;
        if (version is null)
        {
            UpdateStatusText.Text = VanillaUpdateTexts.VersionUnknown;
            return;
        }

        _recording = true;
        ReadVersionButton.IsEnabled = false;
        var progress = new Progress<(int Done, int Total)>(p => UpdateStatusText.Text = VanillaUpdateTexts.Reading(p.Done, p.Total));
        try
        {
            var result = await Task.Run(() => _recordSnapshots(progress));
            UpdateStatusText.Text = VanillaUpdateTexts.Recorded(version, result.Recorded, result.AlreadyPresent)
                + (result.Failed > 0 ? " " + VanillaUpdateTexts.Failed(result.Failed) : string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            UpdateStatusText.Text = ex.Message;
        }
        finally
        {
            _recording = false;
            ReadVersionButton.IsEnabled = true;
        }
    }

    // ---- Compare ----

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        var isA = (sender as FrameworkElement)?.Tag as string == "A";
        var box = isA ? FileABox : FileBBox;
        var dialog = new OpenFileDialog { Title = isA ? MiscWindowTexts.DiffChooseFileA : MiscWindowTexts.DiffChooseFileB };
        if (File.Exists(box.Text))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(box.Text);
            dialog.FileName = Path.GetFileName(box.Text);
        }

        dialog.ClientGuid = GameDialogMemory.For(isA ? "diff.fileA" : "diff.fileB");
        if (dialog.ShowDialog(this) == true)
        {
            box.Text = dialog.FileName;
        }
    }

    /// <summary>Feld A oder B, das zuletzt den Fokus hatte (Ziel der Schnellzugriff-Knöpfe).</summary>
    private System.Windows.Controls.TextBox? _lastFileBox;

    /// <summary>
    /// Schnellzugriff-Knopf: Dateiauswahl in diesem Ordner; die Datei kommt in das zuletzt fokussierte Feld, sonst in das erste leere
    /// (beide gefüllt: A).
    /// </summary>
    private void ChooseFileIn(string folder)
    {
        var box = _lastFileBox ?? (FileABox.Text.Length == 0 || FileBBox.Text.Length > 0 ? FileABox : FileBBox);
        var isA = ReferenceEquals(box, FileABox);
        var dialog = new OpenFileDialog
        {
            Title = isA ? MiscWindowTexts.DiffChooseFileA : MiscWindowTexts.DiffChooseFileB,
            InitialDirectory = folder,
        };
        dialog.ClientGuid = GameDialogMemory.For(isA ? "diff.fileA" : "diff.fileB");
        if (dialog.ShowDialog(this) == true)
        {
            box.Text = dialog.FileName;
        }
    }

    /// <summary>Knopf "Ordner" neben A bzw. B: zwei Ordner vergleicht "Vergleichen" wie WinMerge.</summary>
    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var isA = (sender as FrameworkElement)?.Tag as string == "A";
        var box = isA ? FileABox : FileBBox;
        var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = isA ? MiscWindowTexts.DiffChooseFolderA : MiscWindowTexts.DiffChooseFolderB, UseDescriptionForTitle = true };
        if (Directory.Exists(box.Text.Trim()))
        {
            dialog.SelectedPath = box.Text.Trim();
        }

        dialog.ClientGuid = GameDialogMemory.For(isA ? "diff.folderA" : "diff.folderB");
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            box.Text = dialog.SelectedPath;
        }
    }

    private void FileBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CompareFiles();
            e.Handled = true;
        }
    }

    private void CompareButton_Click(object sender, RoutedEventArgs e) => CompareFiles();

    private void CompareFiles()
    {
        var left = FileABox.Text.Trim().Trim('"');
        var right = FileBBox.Text.Trim().Trim('"');
        if (new[] { left, right }.FirstOrDefault(p => !File.Exists(p) && !Directory.Exists(p)) is { } missing)
        {
            CompareStatusText.Text = MiscWindowTexts.DiffCompareMissing(missing);
            return;
        }

        if (Directory.Exists(left) != Directory.Exists(right))
        {
            CompareStatusText.Text = MiscWindowTexts.DiffMixedPaths;
            return;
        }

        if (!ComparePane.ConfirmProceedWithUnsavedChanges(this))
        {
            return;
        }

        if (Directory.Exists(left))
        {
            _ = CompareFoldersAsync(left, right);
            return;
        }

        ShowFolderList(false);
        ComparePane.LoadPair(left, right, MiscWindowTexts.DiffFileA, MiscWindowTexts.DiffFileB, leftEditable: true, rightEditable: true);
        CompareStatusText.Text = string.Empty;
    }

    // ---- Ordnervergleich (wie WinMerge) ----

    private IReadOnlyList<FolderCompareEntry> _folderEntries = Array.Empty<FolderCompareEntry>();
    private (string Left, string Right)? _folderRoots;
    private FolderListItem? _selectedFolderItem;
    private bool _suppressFolderListEvent;

    /// <summary>Vergleicht zwei Ordner im Hintergrund (große Ordner wie das Spielverzeichnis frieren das Fenster nicht ein).</summary>
    private async Task CompareFoldersAsync(string left, string right)
    {
        CompareButton.IsEnabled = false;
        CompareStatusText.Text = MiscWindowTexts.DiffFolderComparing;
        using var cts = new CancellationTokenSource();
        var progressWindow = new TaskProgressWindow(
            MiscWindowTexts.DiffFolderProgressTitle,
            MiscWindowTexts.DiffFolderProgressHint,
            ParadoxModTool.Localization.CommonTexts.Cancel,
            MiscWindowTexts.DiffFolderProgressCount,
            () => cts.Cancel())
        { Owner = this };
        var progress = new Progress<FolderCompareProgress>(p =>
            progressWindow.Report(new ParadoxModTool.ModCompat.CompatProgress(p.Total == 0 ? MiscWindowTexts.DiffFolderSearching : p.RelativePath, p.Done, p.Total)));
        progressWindow.Show();
        try
        {
            var entries = await Task.Run(() => FolderComparer.Compare(left, right, cts.Token, new ThrottledProgress(progress)), cts.Token);
            _folderEntries = entries;
            _folderRoots = (left, right);
            _selectedFolderItem = null;
            ShowFolderList(true);
            ApplyFolderFilter();
            CompareStatusText.Text = MiscWindowTexts.DiffFolderSummary(
                entries.Count(e => e.Status == FolderCompareStatus.Different),
                entries.Count(e => e.Status == FolderCompareStatus.OnlyLeft),
                entries.Count(e => e.Status == FolderCompareStatus.OnlyRight),
                entries.Count(e => e.Status == FolderCompareStatus.Same));
        }
        catch (OperationCanceledException)
        {
            CompareStatusText.Text = MiscWindowTexts.DiffFolderCancelled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CompareStatusText.Text = ex.Message;
        }
        finally
        {
            progressWindow.CloseAfterFinished();
            CompareButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Gibt Fortschritt höchstens alle 100 ms weiter, die letzte Meldung (Done == Total) immer: bei zehntausenden Dateien würde sonst
    /// jede einzelne Meldung die Oberfläche beschäftigen.
    /// </summary>
    private sealed class ThrottledProgress(IProgress<FolderCompareProgress> inner) : IProgress<FolderCompareProgress>
    {
        private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
        private long _last = -1000;

        public void Report(FolderCompareProgress value)
        {
            var now = _watch.ElapsedMilliseconds;
            if (now - _last < 100 && value.Total != 0 && value.Done != value.Total)
            {
                return;
            }

            _last = now;
            inner.Report(value);
        }
    }

    private void ShowFolderList(bool visible)
    {
        FolderListPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        FolderSplitter.Visibility = FolderListPanel.Visibility;
        FolderListColumn.Width = visible ? new GridLength(280) : new GridLength(0);
        FolderSplitterColumn.Width = visible ? new GridLength(6) : new GridLength(0);
        if (!visible)
        {
            _folderRoots = null;
            _folderEntries = Array.Empty<FolderCompareEntry>();
        }
    }

    private void FolderFilter_Changed(object sender, RoutedEventArgs e) => ApplyFolderFilter();

    /// <summary>Ausgeblendete Dateiarten: eine Leiste geändert, die andere folgt, beide Listen neu, Auswahl für Kompatch und Update-Merge merken.</summary>
    private void HiddenKindsChanged(FileKindFilterBar source, FileKindFilterBar other)
    {
        var hidden = source.HiddenKinds;
        other.SetHidden(hidden);
        _setHiddenKinds(hidden);
        ApplyFileFilter();
        ApplyFolderFilter();
    }

    private void ApplyFolderFilter()
    {
        if (FolderFilesList is null)
        {
            return;
        }

        var hiddenKinds = _folderKinds?.HiddenKinds ?? Array.Empty<string>();
        var shown = FolderComparer.Filter(_folderEntries, FolderFilterBox.Text, HideSameCheckBox.IsChecked == true)
            .Where(entry => !ParadoxModTool.Compatch.CompatchFileFilter.IsHiddenKind(entry.RelativePath, hiddenKinds))
            .Select(entry => new FolderListItem(entry))
            .ToList();
        _suppressFolderListEvent = true;
        FolderFilesList.ItemsSource = shown;
        FolderFilesList.SelectedItem = shown.FirstOrDefault(i => i.Entry.RelativePath == _selectedFolderItem?.Entry.RelativePath);
        _suppressFolderListEvent = false;
        FolderCountText.Text = MiscWindowTexts.DiffFolderCount(shown.Count, _folderEntries.Count);
    }

    private void FolderFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFolderListEvent || FolderFilesList.SelectedItem is not FolderListItem item || _folderRoots is not { } roots)
        {
            return;
        }

        if (!ReferenceEquals(item, _selectedFolderItem) && !ComparePane.ConfirmProceedWithUnsavedChanges(this))
        {
            _suppressFolderListEvent = true;
            FolderFilesList.SelectedItem = _selectedFolderItem;
            _suppressFolderListEvent = false;
            return;
        }

        _selectedFolderItem = item;
        if (item.Entry.IsBinary)
        {
            CompareStatusText.Text = MiscWindowTexts.DiffBinary(item.StatusText);
            return;
        }

        var relative = item.Entry.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        ComparePane.LoadPair(
            item.Entry.LeftPath ?? Path.Combine(roots.Left, relative),
            item.Entry.RightPath ?? Path.Combine(roots.Right, relative),
            MiscWindowTexts.DiffFileA,
            MiscWindowTexts.DiffFileB,
            leftEditable: true,
            rightEditable: true);
        CompareStatusText.Text = item.StatusText;
    }

    /// <summary>Nach dem Speichern im Ordnervergleich: Status der Datei neu prüfen (eine angelegte Datei ist nicht mehr "nur in A").</summary>
    private void RecheckSavedFolderFile(string path)
    {
        if (_folderRoots is not { } roots || _selectedFolderItem is not { } item)
        {
            return;
        }

        var relative = item.Entry.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        var entry = FolderComparer.Entry(item.Entry.RelativePath, Path.Combine(roots.Left, relative), Path.Combine(roots.Right, relative));
        _folderEntries = _folderEntries.Select(e => e.RelativePath == entry.RelativePath ? entry : e).ToList();
        _selectedFolderItem = new FolderListItem(entry);
        ApplyFolderFilter();
    }

    /// <summary>Zeile der Dateiliste: Statuszeichen in der Farbe der Seite und relativer Pfad.</summary>
    public sealed class FolderListItem
    {
        public FolderListItem(FolderCompareEntry entry)
        {
            Entry = entry;
            (Marker, var token, StatusText) = entry.Status switch
            {
                FolderCompareStatus.Different => ("≠", "DiffModified", MiscWindowTexts.DiffStatusDifferent),
                FolderCompareStatus.OnlyLeft => ("A", "DiffVanilla", MiscWindowTexts.DiffStatusOnlyA),
                FolderCompareStatus.OnlyRight => ("B", "DiffMod", MiscWindowTexts.DiffStatusOnlyB),
                _ => ("=", "LineNumbers", MiscWindowTexts.DiffStatusSame),
            };
            var brush = new System.Windows.Media.SolidColorBrush(ThemeManager.GetColor(token));
            brush.Freeze();
            MarkerBrush = brush;
        }

        public FolderCompareEntry Entry { get; }

        public string Marker { get; }

        public System.Windows.Media.Brush MarkerBrush { get; }

        public string StatusText { get; }
    }

    /// <summary>Frei gewählte Dateien liegen nicht in einem erkannten Mod-Ordner: ein Hinweis fragt einmal nach, ohne Snapshot zu speichern.</summary>
    private bool AskSaveWithoutSnapshot(string path) =>
        MessageBox.Show(
            this,
            MiscWindowTexts.DiffNoSnapshotPrompt(Path.GetFileName(path)),
            MiscWindowTexts.DiffNoSnapshotTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;

    // ---- Update ----

    /// <summary>
    /// Der Ordner EINER konkreten Mod, gegen den <see cref="VanillaFileTracker"/> abgleicht: der gewählte Override, sonst der
    /// Sammelordner-Vorschlag aus dem Spielprofil (Paradox-Mods liegen als eigener Unterordner unter "mod/").
    /// </summary>
    private string? ResolveModRoot() => _getModRootOverride() ?? _resolveDefaultModRoot();

    private void LoadOverriddenFiles()
    {
        var modRoot = ResolveModRoot();
        var gameRoot = _resolveGameRoot();
        ModRootStatusText.Text = modRoot ?? MiscWindowTexts.DiffNoModFolder;
        ModRootStatusText.ToolTip = modRoot;

        if (_activeProfile is null || modRoot is null || gameRoot is null)
        {
            _overridden = Array.Empty<OverriddenFile>();
            ApplyFileFilter();
            UpdateStatusText.Text = MiscWindowTexts.DiffNoAutoDetect;
            return;
        }

        _overridden = VanillaFileTracker.FindOverriddenFiles(gameRoot, modRoot, _activeProfile);
        ApplyFileFilter();
        UpdateStatusText.Text = _overridden.Count == 0 ? MiscWindowTexts.DiffNoOverridden : string.Empty;
    }

    private void FileFilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFileFilter();

    private void ApplyFileFilter()
    {
        var hidden = _updateKinds?.HiddenKinds ?? Array.Empty<string>();
        var shown = OverriddenFile.Filter(_overridden, FileFilterBox.Text)
            .Where(f => !ParadoxModTool.Compatch.CompatchFileFilter.IsHiddenKind(f.RelativePath, hidden))
            .ToList();
        _suppressListEvent = true;
        OverriddenFilesList.ItemsSource = shown;
        OverriddenFilesList.SelectedItem = _selectedOverriddenFile is { } selected && shown.Contains(selected) ? selected : null;
        _suppressListEvent = false;
        FileCountText.Text = shown.Count == _overridden.Count
            ? MiscWindowTexts.DiffOverriddenCount(_overridden.Count)
            : MiscWindowTexts.DiffFilteredCount(shown.Count, _overridden.Count);
    }

    /// <summary>
    /// Ordner EINER konkreten Mod wählen (der Sammelordner-Vorschlag passt nur, wenn eine Mod direkt im "mod/"-Ordner liegt). Die Wahl
    /// wird über die Session gespeichert und löst sofort eine neue Suche nach überschriebenen Dateien aus.
    /// </summary>
    private void ModRootButton_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdatePane.ConfirmProceedWithUnsavedChanges(this))
        {
            return;
        }

        var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = MiscWindowTexts.DiffModFolderDialog };
        var current = ResolveModRoot();
        if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
        {
            dialog.SelectedPath = current;
        }

        dialog.ClientGuid = GameDialogMemory.For("diff.modFolder");
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _setModRootOverride(dialog.SelectedPath);
        LoadOverriddenFiles();
        ReplaceListPane.Refresh();
    }

    private void OverriddenFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressListEvent || OverriddenFilesList.SelectedItem is not OverriddenFile selected)
        {
            return;
        }

        if (!ReferenceEquals(selected, _selectedOverriddenFile) && !UpdatePane.ConfirmProceedWithUnsavedChanges(this))
        {
            _suppressListEvent = true;
            OverriddenFilesList.SelectedItem = _selectedOverriddenFile;
            _suppressListEvent = false;
            return;
        }

        _selectedOverriddenFile = selected;
        LoadSelectedPair();
        RefreshMergeBaseline();
    }

    private void LoadSelectedPair()
    {
        if (_selectedOverriddenFile is { } pair)
        {
            UpdatePane.LoadPair(pair.VanillaFullPath, pair.ModFullPath, "Vanilla", "Mod", leftEditable: false, rightEditable: true);
        }
    }

    /// <summary>Vor dem ersten Speichern einer Mod-Datei: Snapshot des Mods (wie beim Merge).</summary>
    private bool SnapshotModBeforeSave(string path)
    {
        if (ResolveModRoot() is not { } modRoot)
        {
            return AskSaveWithoutSnapshot(path);
        }

        var manager = new ParadoxModTool.VersionManager.ModVersionManager(
            modRoot,
            ParadoxModTool.VersionManager.StorageLocations.ResolveSnapshotsDirectory(modRoot, _getStorageSettings()));
        manager.CreateSnapshot($"vor-edit_{Path.GetFileName(path)}");
        return true;
    }

    /// <summary>
    /// "Merge nach Update": Startfenster, dann der Kompatch-Merge im Update-Modus (alte Vanilla = Basis, neue Vanilla = A, Mod = B; Spec
    /// 2026-09-30-vanilla-update-merge). Ungespeicherte Änderungen der Mod-Datei müssen vorher gespeichert oder verworfen sein.
    /// </summary>
    private void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeProfile is null || ResolveModRoot() is not { } modRoot || _resolveGameRoot() is not { } gameRoot)
        {
            return;
        }

        if (!UpdatePane.ConfirmProceedWithUnsavedChanges(this))
        {
            return;
        }

        var version = _detectedGameVersion ?? ManualVersionTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            MessageBox.Show(this, VanillaUpdateTexts.VersionUnknownStart, VanillaUpdateTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var store = VanillaHistoryStore.FromHistoryRoot(
            ParadoxModTool.VersionManager.StorageLocations.ResolveVanillaHistoryDirectory(modRoot, _getStorageSettings()));
        var metadata = ParadoxModTool.Mods.ModMetadata.Read(modRoot, _activeProfile.MetadataFile);
        var modName = string.IsNullOrWhiteSpace(metadata?.Name) ? Path.GetFileName(Path.TrimEndingDirectorySeparator(modRoot)) : metadata!.Name;
        var context = new VanillaUpdateContext(
            modName,
            modRoot,
            gameRoot,
            version,
            _overridden,
            ModRelativePaths(modRoot),
            _selectedOverriddenFile,
            store,
            ParadoxModTool.Compatch.VanillaVersionLedger.Load(_modStateDirectory(modRoot)),
            metadata?.SupportedGameVersion,
            _recordSnapshots,
            _updateKinds.HiddenKinds,
            kinds =>
            {
                _setHiddenKinds(kinds);
                _updateKinds.SetHidden(kinds);
                _folderKinds.SetHidden(kinds);
                ApplyFileFilter();
                ApplyFolderFilter();
            });
        if (VanillaUpdateStartWindow.Ask(this, context) is not { Count: > 0 } files)
        {
            return;
        }

        _openUpdateMerge(new VanillaUpdateLaunch(files, modRoot, modName, version, store, ReloadUpdateTab));
    }

    /// <summary>
    /// Zeigt eine Mod-Kopie im Update-Reiter (Wächter der Patch-Rezepte, "Im Update-Tool öffnen"). Wechselt bei Bedarf den Mod-Ordner; ungespeicherte
    /// Änderungen müssen vorher gespeichert oder verworfen sein.
    /// </summary>
    public void ShowUpdateFile(string modRoot, string relativePath)
    {
        Tabs.SelectedItem = UpdateTab;
        if (!string.Equals(Path.TrimEndingDirectorySeparator(ResolveModRoot() ?? string.Empty), Path.TrimEndingDirectorySeparator(modRoot), StringComparison.OrdinalIgnoreCase))
        {
            if (!UpdatePane.ConfirmProceedWithUnsavedChanges(this))
            {
                return;
            }

            _setModRootOverride(modRoot);
            LoadOverriddenFiles();
            ReplaceListPane.Refresh();
        }

        FileFilterBox.Text = string.Empty;
        var wanted = relativePath.Replace('\\', '/');
        var file = _overridden.FirstOrDefault(f => string.Equals(f.RelativePath.Replace('\\', '/'), wanted, StringComparison.OrdinalIgnoreCase));
        if (file is null)
        {
            UpdateStatusText.Text = MiscWindowTexts.DiffFileNotOverridden(relativePath);
            return;
        }

        OverriddenFilesList.SelectedItem = file;
        OverriddenFilesList.ScrollIntoView(file);
    }

    /// <summary>Nach dem Schreiben im Update-Modus: Dateiliste und gewähltes Paar neu laden.</summary>
    public void ReloadUpdateTab()
    {
        LoadOverriddenFiles();
        LoadSelectedPair();
        UpdateStatusText.Text = MiscWindowTexts.DiffMergeDone;
    }

    /// <summary>Alle Dateien der Mod relativ zum Mod-Ordner (ohne Ordner mit Punkt), für "Vanilla-Datei entfällt".</summary>
    private static IReadOnlyList<string> ModRelativePaths(string modRoot)
    {
        if (!Directory.Exists(modRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(modRoot, f).Replace('\\', '/'))
            .Where(r => !r.StartsWith('.') && !r.Contains("/.", StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Für das gewählte Paar: zeichnet bei Bedarf die aktuelle Vanilla-Fassung als Snapshot der aktuellen Spielversion auf, meldet die ältere
    /// Fassung und aktiviert "Merge nach Update", sobald die Spielversion bekannt ist (ohne ältere Fassung legt der Merge jede Abweichung zum
    /// Prüfen vor). Die Version kommt bevorzugt aus <see cref="GameVersionReader"/>, sonst aus der manuellen Eingabe.
    /// </summary>
    private void RefreshMergeBaseline()
    {
        _mergeBaseline = null;
        MergeButton.IsEnabled = false;
        if (_selectedOverriddenFile is null)
        {
            UpdateStatusText.Text = MiscWindowTexts.DiffMergeAutoOnly;
            return;
        }

        var modRoot = ResolveModRoot();
        var gameRoot = _resolveGameRoot();
        if (modRoot is null || gameRoot is null)
        {
            return;
        }

        _detectedGameVersion = GameVersionReader.TryReadVersion(gameRoot);
        var effectiveVersion = _detectedGameVersion ?? ManualVersionTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(effectiveVersion))
        {
            UpdateStatusText.Text = MiscWindowTexts.DiffVersionUnknown;
            return;
        }

        var store = VanillaHistoryStore.FromHistoryRoot(
            ParadoxModTool.VersionManager.StorageLocations.ResolveVanillaHistoryDirectory(modRoot, _getStorageSettings()));
        _mergeBaseline = VanillaMergeBaseline.Resolve(store, _selectedOverriddenFile.RelativePath, effectiveVersion, _selectedOverriddenFile.VanillaFullPath);
        // Ohne ältere Fassung geht der Abgleich trotzdem: jede Abweichung wird dann zum Prüfen vorgelegt (Startfenster erklärt es).
        MergeButton.IsEnabled = true;
        UpdateStatusText.Text = _mergeBaseline.HasOlderBaseline
            ? MiscWindowTexts.DiffMergeAvailable(_mergeBaseline.OlderVersion ?? string.Empty, effectiveVersion)
            : MiscWindowTexts.DiffBaselineRecorded(effectiveVersion);
    }

    /// <summary>Übernimmt die manuell eingetragene Spielversion erst bei Enter oder Fokusverlust (nicht bei jedem Tastendruck).</summary>
    private void CommitManualVersion()
    {
        var version = ManualVersionTextBox.Text.Trim();
        if (version == _lastCommittedManualVersion)
        {
            return;
        }

        _lastCommittedManualVersion = version;
        if (_detectedGameVersion is null)
        {
            RefreshMergeBaseline();
        }
    }

    private void ManualVersionTextBox_Commit(object sender, RoutedEventArgs e) => CommitManualVersion();

    private void ManualVersionTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitManualVersion();
            e.Handled = true;
        }
    }

    // ---- Fenster, Tastenkürzel, Suchen & Ersetzen ----

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ComparePane.ConfirmProceedWithUnsavedChanges(this) || !UpdatePane.ConfirmProceedWithUnsavedChanges(this))
        {
            e.Cancel = true;
        }
    }

    /// <summary>
    /// Ersetzungsliste schreiben: nicht, solange ein Vergleich in diesem Fenster eine betroffene Datei ungespeichert zeigt; danach zeigen
    /// die Vergleiche die neue Fassung.
    /// </summary>
    private FixOutcome ApplyReplaceList(
        IReadOnlyList<ParadoxModTool.SearchReplace.ReplaceLists.ReplaceFileChange> changes,
        string modFolder,
        string listName,
        Func<IReadOnlyList<ParadoxModTool.SearchReplace.ReplaceLists.ReplaceFileChange>, string, string, FixOutcome> apply)
    {
        bool Affects(DiffComparePane pane) =>
            changes.Any(c => (pane.LeftPath is { } left && ParadoxModTool.Editor.EditorPane.PathsEqual(left, c.Path)) || (pane.RightPath is { } right && ParadoxModTool.Editor.EditorPane.PathsEqual(right, c.Path)));

        foreach (var pane in new[] { ComparePane, UpdatePane })
        {
            if (pane.HasUnsavedChanges && Affects(pane))
            {
                var file = changes.First(c => ParadoxModTool.Editor.EditorPane.PathsEqual(c.Path, pane.LeftPath ?? string.Empty) || ParadoxModTool.Editor.EditorPane.PathsEqual(c.Path, pane.RightPath ?? string.Empty)).Path;
                return new FixOutcome(false, ParadoxModTool.ErrorLog.ErrorLogTexts.RenameUnsaved(Path.GetFileName(file)));
            }
        }

        var outcome = apply(changes, modFolder, listName);
        if (outcome.Success)
        {
            foreach (var pane in new[] { ComparePane, UpdatePane }.Where(Affects))
            {
                pane.ReloadFromDisk();
            }
        }

        return outcome;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Die Kürzel der Vergleiche gelten nicht im Tab "Ersetzungslisten": sonst träfen Strg+Z, U, Esc, Strg+S den unsichtbaren Update-Vergleich.
        if (ReferenceEquals(Tabs.SelectedItem, ReplaceTab))
        {
            return;
        }

        var pane = ActivePane;
        if (e.Key == Key.F7 && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            pane.JumpToDifference(forward: Keyboard.Modifiers == ModifierKeys.None);
            e.Handled = true;
            return;
        }

        // Markierte Zeilen: U übernimmt sofort, Esc hebt auf (nicht in den Eingabefeldern der Reiter, dort wird getippt).
        if (Keyboard.Modifiers == ModifierKeys.None && pane.HasMarks && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
        {
            if (e.Key == Key.U)
            {
                pane.TakeMarked();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                pane.ClearMarks();
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.Z && Keyboard.FocusedElement is not System.Windows.Controls.TextBox && pane.UndoLastTake())
        {
            e.Handled = true;
            return;
        }

        if (e.Key is Key.F or Key.H)
        {
            OpenSearchWindow();
            e.Handled = true;
        }
        else if (e.Key == Key.S && pane.HasUnsavedChanges)
        {
            pane.SaveAll();
            e.Handled = true;
        }
    }

    private void OpenSearchWindow()
    {
        if (_searchWindow is null)
        {
            _searchWindow = _createSearchWindow(
                () => ActivePane.FocusedEditor.Text,
                (offset, length) => ActivePane.SelectInFocusedEditor(offset, length),
                text => ActivePane.ReplaceFocusedText(text, this),
                this);
            _searchWindow.Closed += (_, _) => _searchWindow = null;
        }

        if (_searchWindow.IsVisible)
        {
            _searchWindow.Activate();
        }
        else
        {
            _searchWindow.Show();
        }

        _searchWindow.FocusSearchBox(ActivePane.FocusedEditor.SelectedText);
    }
}
