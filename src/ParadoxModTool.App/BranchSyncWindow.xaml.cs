using System.IO;
using System.Windows;
using ParadoxModTool.Localization;
using ParadoxModTool.Mods;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Fenster "Abgleich": zwei Mods eines Projekts ordnerweit vergleichen und einzelne Dateien übernehmen (siehe <see cref="FolderComparer"/>, <see cref="BranchTransfer"/>).</summary>
public partial class BranchSyncWindow : Window
{
    private readonly ModRegistry _registry;
    private readonly Action<ModInfo, ModInfo, string> _compare;
    private readonly BranchHooks _hooks;
    private IReadOnlyList<FileDifference> _all = Array.Empty<FileDifference>();
    private ModInfo? _leftMod;
    private ModInfo? _rightMod;
    private string? _leftFolder;
    private string? _rightFolder;
    private CancellationTokenSource? _cts;

    public BranchSyncWindow(ModRegistry registry, ModInfo start, Action<ModInfo, ModInfo, string> compare, BranchHooks hooks)
    {
        InitializeComponent();
        _registry = registry;
        _compare = compare;
        _hooks = hooks;

        var members = ModProjects.Members(registry, start);
        LeftCombo.ItemsSource = members;
        RightCombo.ItemsSource = members;
        LeftCombo.SelectedItem = members.FirstOrDefault(m => m.Id == start.Id) ?? members[0];
        RightCombo.SelectedItem = members.FirstOrDefault(m => m.Id != start.Id && m.Role != ModRole.Submod) ?? members.FirstOrDefault(m => m.Id != start.Id);
        Title = MainTexts.SyncWindowTitle(ModProjects.Root(registry, start).Name);
        Closed += (_, _) => _cts?.Cancel();
        Activated += (_, _) => RefreshLocks();
        UpdateButtons();
    }

    private ModInfo? SelectedLeft => LeftCombo.SelectedItem as ModInfo;

    private ModInfo? SelectedRight => RightCombo.SelectedItem as ModInfo;

    private List<SyncRow> SelectedRows => ResultList.SelectedItems.Cast<SyncRow>().ToList();

    private void Combo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateButtons();

    private void ResultList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateButtons();

    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private void Filter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

    private void CategoryBar_Changed(object? sender, EventArgs e) => ApplyFilter();

    /// <summary>Sperren können im Hauptfenster (Verzeichnis-Übersicht, Mods-Fenster) geändert worden sein: Spalte "Gesperrt" und Knöpfe beim Zurückkehren in dieses Fenster neu bestimmen, die Auswahl bleibt.</summary>
    private void RefreshLocks()
    {
        if (_leftMod is null || _rightMod is null)
        {
            return;
        }

        var selected = SelectedRows.Select(r => r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ApplyFilter();
        foreach (var row in ResultList.Items.Cast<SyncRow>().Where(r => selected.Contains(r.Path)))
        {
            ResultList.SelectedItems.Add(row);
        }
    }

    private void UpdateButtons()
    {
        var hasResult = _leftMod is not null && _rightMod is not null;
        var rows = hasResult ? SelectedRows : new List<SyncRow>();
        var left = _leftMod ?? SelectedLeft;
        var right = _rightMod ?? SelectedRight;

        ToRightButton.Content = new System.Windows.Controls.TextBlock { Text = MainTexts.SyncApplyTo(right?.Name ?? MainTexts.SyncRightLower), TextTrimming = TextTrimming.CharacterEllipsis };
        ToLeftButton.Content = new System.Windows.Controls.TextBlock { Text = MainTexts.SyncApplyTo(left?.Name ?? MainTexts.SyncLeftLower), TextTrimming = TextTrimming.CharacterEllipsis };
        ToRightButton.ToolTip = MainTexts.SyncApplyTip(left?.Name ?? string.Empty, right?.Name ?? string.Empty);
        ToLeftButton.ToolTip = MainTexts.SyncApplyTip(right?.Name ?? string.Empty, left?.Name ?? string.Empty);

        // Die Sperren live prüfen (nicht aus den Zeilen): sie können im Hauptfenster geändert worden sein. Eine gesperrte Datei geht weder als Quelle noch als Ziel.
        bool Transferable(SyncRow row, bool toRight) =>
            (toRight ? row.Difference.Left : row.Difference.Right) is not null && LockedSides(row.Difference) is (false, false);

        ToRightButton.IsEnabled = hasResult && rows.Any(r => Transferable(r, toRight: true)) && BranchTransfer.Check(_registry, _leftMod!, _rightMod!, TransferMode.Copy).Allowed;
        ToLeftButton.IsEnabled = hasResult && rows.Any(r => Transferable(r, toRight: false)) && BranchTransfer.Check(_registry, _rightMod!, _leftMod!, TransferMode.Copy).Allowed;
        CompareFileButton.IsEnabled = hasResult && rows.Count == 1;
    }

    private async void Run_Click(object sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        if (SelectedLeft is not { } left || SelectedRight is not { } right)
        {
            return;
        }

        if (left.Id == right.Id)
        {
            StatusText.Text = MainTexts.SyncSameMod;
            return;
        }

        var leftFolder = BranchTransfer.WorkingFolder(left);
        var rightFolder = BranchTransfer.WorkingFolder(right);
        if (leftFolder is null || rightFolder is null)
        {
            MessageBox.Show(this, MainTexts.ModFolderMissing(leftFolder is null ? left.Name : right.Name), MainTexts.SyncTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        SetBusy(true);
        StatusText.Text = MainTexts.SyncComparing;
        try
        {
            _all = await Task.Run(() => FolderComparer.Compare(ModFileIndex.Build(leftFolder, null, token), ModFileIndex.Build(rightFolder, null, token), token), token);
            _leftMod = left;
            _rightMod = right;
            _leftFolder = leftFolder;
            _rightFolder = rightFolder;
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = CommonTexts.Cancelled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = string.Empty;
            MessageBox.Show(this, MainTexts.SyncFailed(ex.Message), MainTexts.SyncTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        RunButton.IsEnabled = !busy;
        CancelButton.IsEnabled = busy;
        LeftCombo.IsEnabled = !busy;
        RightCombo.IsEnabled = !busy;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void ApplyFilter()
    {
        if (!IsLoaded && _all.Count == 0)
        {
            return;
        }

        var text = PathFilterBox.Text.Trim();
        var rows = _all
            .Where(d => d.Kind switch
            {
                DifferenceKind.OnlyLeft => OnlyLeftCheck.IsChecked == true,
                DifferenceKind.OnlyRight => OnlyRightCheck.IsChecked == true,
                DifferenceKind.Different => DifferentCheck.IsChecked == true,
                _ => SameCheck.IsChecked == true,
            })
            .Where(d => text.Length == 0 || d.RelativePath.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Where(d => CategoryBar.Accepts(CategoryOf(d)))
            .Select(d => new SyncRow(d, LockedSides(d)))
            .ToList();
        ResultList.ItemsSource = rows;

        var counts = _all.GroupBy(d => d.Kind).ToDictionary(g => g.Key, g => g.Count());
        int Count(DifferenceKind kind) => counts.TryGetValue(kind, out var n) ? n : 0;
        StatusText.Text = MainTexts.SyncSummary(Count(DifferenceKind.OnlyLeft), Count(DifferenceKind.OnlyRight), Count(DifferenceKind.Different), Count(DifferenceKind.Same), rows.Count);
        UpdateButtons();
    }

    private string CategoryOf(FileDifference difference)
    {
        var folder = difference.Left is not null ? _leftFolder : _rightFolder;
        return folder is null
            ? ParadoxModTool.Categories.CategoryDefaults.OtherId
            : ParadoxModTool.Editor.FileCategoryClassifier.Classify(System.IO.Path.Combine(folder, difference.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
    }

    private (bool Left, bool Right) LockedSides(FileDifference difference) => (
        _leftMod is not null && ModLocks.LockedBy(_leftMod, difference.RelativePath) is not null,
        _rightMod is not null && ModLocks.LockedBy(_rightMod, difference.RelativePath) is not null);

    private void ToRight_Click(object sender, RoutedEventArgs e) => Apply(toRight: true);

    private void ToLeft_Click(object sender, RoutedEventArgs e) => Apply(toRight: false);

    private async void Apply(bool toRight)
    {
        var from = toRight ? _leftMod : _rightMod;
        var to = toRight ? _rightMod : _leftMod;
        if (from is null || to is null)
        {
            return;
        }

        var paths = SelectedRows
            .Where(r => (toRight ? r.Difference.Left : r.Difference.Right) is not null)
            .Select(r => r.Path)
            .ToList();
        if (paths.Count == 0)
        {
            return;
        }

        var text = BranchActions.Transfer(this, _registry, from, to, paths, TransferMode.Copy, _hooks);
        await RunAsync();
        if (text.Length > 0)
        {
            StatusText.Text = text + "  " + StatusText.Text;
        }
    }

    private void CompareFile_Click(object sender, RoutedEventArgs e) => CompareSelected();

    private void ResultList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => CompareSelected();

    private void CompareSelected()
    {
        if (_leftMod is not null && _rightMod is not null && SelectedRows is [{ } row])
        {
            _compare(_leftMod, _rightMod, row.Path);
        }
    }

    private sealed class SyncRow
    {
        public SyncRow(FileDifference difference, (bool Left, bool Right) locked)
        {
            Difference = difference;
            LockedLeft = locked.Left;
            LockedRight = locked.Right;
        }

        public FileDifference Difference { get; }

        public bool LockedLeft { get; }

        public bool LockedRight { get; }

        public string LockText => LockedLeft && LockedRight ? MainTexts.SyncBoth : LockedLeft ? MainTexts.SyncLeftLower : LockedRight ? MainTexts.SyncRightLower : string.Empty;

        public string Path => Difference.RelativePath;

        public string StatusText => Difference.Kind switch
        {
            DifferenceKind.OnlyLeft => MainTexts.SyncOnlyLeft,
            DifferenceKind.OnlyRight => MainTexts.SyncOnlyRight,
            DifferenceKind.Different => MainTexts.SyncDifferent,
            _ => MainTexts.SyncSame,
        };

        public string LeftSize => Format(Difference.Left?.Size);

        public string RightSize => Format(Difference.Right?.Size);

        public string LeftDate => FormatDate(Difference.Left);

        public string RightDate => FormatDate(Difference.Right);

        private static string Format(long? size) => size is null ? string.Empty : size < 1024 ? $"{size} B" : $"{size / 1024.0:0.#} KB";

        private static string FormatDate(ModFileEntry? entry) => entry is null ? string.Empty : entry.LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}
