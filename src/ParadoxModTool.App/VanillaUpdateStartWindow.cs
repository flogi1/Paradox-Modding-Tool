using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ParadoxModTool.Compatch;
using ParadoxModTool.VanillaDiff;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Was ein Update-Lauf braucht: die Mod, ihre überschriebenen Dateien, die Vanilla-Historie und die installierte Version.</summary>
public sealed record VanillaUpdateContext(
    string ModName,
    string ModFolder,
    string VanillaRoot,
    string CurrentVersion,
    IReadOnlyList<OverriddenFile> Overridden,
    OverriddenFile? Selected,
    VanillaHistoryStore Store,
    VanillaVersionLedger Ledger,
    string? SupportedPattern,
    Func<IProgress<(int Done, int Total)>?, SnapshotRecordResult> RecordSnapshots,
    IReadOnlyList<string> HiddenKinds,
    Action<IReadOnlyList<string>> RememberHiddenKinds);

/// <summary>
/// Startfenster "Mod an neue Vanilla anpassen" (Spec 2026-09-30-vanilla-update-merge, Abschnitt 8): alte Version, Umfang, Zähler und Hinweise.
/// Listet Mod-Dateien, die eine Strukturänderung von Vanilla trifft (<see cref="VanillaStructureCheck"/>, im Hintergrund).
/// Liest beim Öffnen einmal die installierte Vanilla-Version ein (Abschnitt 7.1).
/// </summary>
public sealed class VanillaUpdateStartWindow : Window
{
    private readonly VanillaUpdateContext _context;
    private readonly Controls.ComboBox _oldVersion = new() { MinWidth = 200, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.RadioButton _thisFile = new() { Content = VanillaUpdateTexts.ScopeThisFile, GroupName = "scope" };
    private readonly Controls.RadioButton _allChanged = new() { Content = VanillaUpdateTexts.ScopeAllChanged, GroupName = "scope", IsChecked = true };
    private readonly Controls.CheckBox _includeWithoutOld = new() { Content = new Controls.TextBlock { Text = VanillaUpdateTexts.IncludeWithoutOld }, Margin = new Thickness(20, 2, 0, 6) };
    private readonly Controls.TextBlock _counts = new() { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly Controls.TextBlock _notes = new() { Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly Controls.Button _read = new() { Content = VanillaUpdateTexts.ReadVersionButton, ToolTip = VanillaUpdateTexts.ReadVersionTip, Padding = new Thickness(10, 1, 10, 1), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Controls.Button _start = new() { Content = VanillaUpdateTexts.StartButton, Width = 100, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
    private readonly FileKindFilterBar _kinds;
    private readonly Controls.TextBlock _structureBusy = new() { Text = VanillaUpdateTexts.StructureBusy, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.StackPanel _structurePanel = new() { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Controls.ListView _structureList = new() { MaxHeight = 140, Margin = new Thickness(0, 4, 0, 4) };
    private readonly Controls.Button _openVanilla = new() { Content = VanillaUpdateTexts.StructureOpen, ToolTip = VanillaUpdateTexts.StructureOpenTip, Padding = new Thickness(10, 1, 10, 1), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, IsEnabled = false };
    private readonly CancellationTokenSource _closing = new();
    private CancellationTokenSource? _structureRun;
    private bool _structureNoHistory;
    private UpdateScan? _scan;
    private IReadOnlyList<UpdateFile> _files = Array.Empty<UpdateFile>();

    private VanillaUpdateStartWindow(VanillaUpdateContext context)
    {
        _context = context;
        Title = VanillaUpdateTexts.StartTitle;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new Controls.StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(Row(VanillaUpdateTexts.ModLabel, new Controls.TextBlock { Text = context.ModName, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }));
        panel.Children.Add(Row(VanillaUpdateTexts.CurrentVersionLabel, new Controls.TextBlock { Text = context.CurrentVersion }));
        panel.Children.Add(Row(VanillaUpdateTexts.OldVersionLabel, _oldVersion));
        _thisFile.IsEnabled = context.Selected is not null;
        _thisFile.ToolTip = context.Selected?.RelativePath;
        panel.Children.Add(_thisFile);
        panel.Children.Add(_allChanged);
        panel.Children.Add(_includeWithoutOld);
        _kinds = new FileKindFilterBar(context.HiddenKinds) { Margin = new Thickness(0, 4, 0, 0) };
        _kinds.Changed += (_, _) =>
        {
            _context.RememberHiddenKinds(_kinds.HiddenKinds);
            RefreshSelection();
        };
        panel.Children.Add(_kinds);
        _counts.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        _notes.SetResourceReference(Controls.TextBlock.ForegroundProperty, "Theme.TextMuted");
        panel.Children.Add(_counts);
        panel.Children.Add(_structureBusy);
        _structurePanel.Children.Add(new Controls.TextBlock { Text = VanillaUpdateTexts.StructureHeader, TextWrapping = TextWrapping.Wrap });
        var grid = new Controls.GridView();
        grid.Columns.Add(new Controls.GridViewColumn { Header = VanillaUpdateTexts.StructureColFile, Width = 220, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(StructureRow.File)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = VanillaUpdateTexts.StructureColChange, Width = 120, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(StructureRow.Label)) });
        grid.Columns.Add(new Controls.GridViewColumn { Header = VanillaUpdateTexts.StructureColNew, Width = 180, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(StructureRow.NewPlace)) });
        _structureList.View = grid;
        _structurePanel.Children.Add(_structureList);
        _structurePanel.Children.Add(_openVanilla);
        panel.Children.Add(_structurePanel);
        panel.Children.Add(_notes);
        panel.Children.Add(_read);

        var buttons = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        _start.Click += (_, _) =>
        {
            DialogResult = true;
        };
        buttons.Children.Add(_start);
        buttons.Children.Add(new Controls.Button { Content = CompatchTexts.Cancel, Width = 100, IsCancel = true });
        panel.Children.Add(buttons);
        Content = panel;

        _thisFile.Checked += (_, _) => RefreshSelection();
        _allChanged.Checked += (_, _) => RefreshSelection();
        _includeWithoutOld.Click += (_, _) => RefreshSelection();
        _read.Click += async (_, _) => await RecordAsync();
        _structureList.SelectionChanged += (_, _) => _openVanilla.IsEnabled = _structureList.SelectedItem is StructureRow { CanOpenVanilla: true };
        _structureList.MouseDoubleClick += OnStructureDoubleClick;
        _openVanilla.Click += (_, _) => OpenVanillaPlace();
        Closed += (_, _) =>
        {
            _closing.Cancel();
            _structureRun?.Cancel();
            _structureRun?.Dispose();
            _structureRun = null;
            _closing.Dispose();
        };
        Loaded += async (_, _) => await RecordAsync();
        Rescan();
    }

    /// <summary>Zeigt das Startfenster; <see langword="null"/> bei Abbruch, sonst die Dateien des Laufs.</summary>
    public static IReadOnlyList<UpdateFile>? Ask(Window owner, VanillaUpdateContext context)
    {
        var window = new VanillaUpdateStartWindow(context) { Owner = owner };
        return window.ShowDialog() == true ? window._files : null;
    }

    private static Controls.DockPanel Row(string label, UIElement value)
    {
        var row = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var caption = new Controls.TextBlock { Text = label, Width = 150, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        Controls.DockPanel.SetDock(caption, Controls.Dock.Left);
        row.Children.Add(caption);
        row.Children.Add(value);
        return row;
    }

    /// <summary>Installierte Vanilla-Fassung einlesen (beim Öffnen und auf Knopfdruck), danach neu zählen.</summary>
    private async Task RecordAsync()
    {
        _read.IsEnabled = false;
        var progress = new Progress<(int Done, int Total)>(p => _notes.Text = VanillaUpdateTexts.Reading(p.Done, p.Total));
        try
        {
            var result = await Task.Run(() => _context.RecordSnapshots(progress));
            Rescan();
            if (result.Recorded > 0 || result.Failed > 0)
            {
                _notes.Text = VanillaUpdateTexts.Recorded(_context.CurrentVersion, result.Recorded, result.AlreadyPresent)
                    + (result.Failed > 0 ? " " + VanillaUpdateTexts.Failed(result.Failed) : string.Empty)
                    + (_notes.Text.Length > 0 ? "\n" + _notes.Text : string.Empty);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _notes.Text = ex.Message;
        }
        finally
        {
            _read.IsEnabled = true;
        }

        // Nach dem Einlesen: eine frisch aufgezeichnete Verzeichnisliste kann neue Treffer bringen.
        await CheckStructureAsync();
    }

    /// <summary>Strukturprüfung im Hintergrund; ein neuer Lauf bricht den alten ab, Schließen des Fensters ebenfalls.</summary>
    private async Task CheckStructureAsync()
    {
        // Nach dem Schließen ist _closing abgebrochen und freigegeben (IsCancellationRequested geht dann noch, Cancel und Token nicht mehr).
        if (_closing.IsCancellationRequested)
        {
            return;
        }

        // Der alte Lauf hält seinen Token vorab fest (siehe unten); Freigeben löst auch die Verknüpfung mit _closing.
        _structureRun?.Cancel();
        _structureRun?.Dispose();
        _structureRun = null;

        // Ohne Vanilla-Ordner würde jeder alte Vanilla-Ordner als verschwunden gelten: dann nicht prüfen.
        if (!Directory.Exists(_context.VanillaRoot))
        {
            ShowStructure(Array.Empty<StructureChange>(), noHistory: false);
            return;
        }

        var run = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        _structureRun = run;
        var token = run.Token;
        _structureBusy.Text = VanillaUpdateTexts.StructureBusy;
        _structureBusy.Visibility = Visibility.Visible;
        var hasSnapshots = _scan is { RecordedVersionsBelowCurrent.Count: > 0 };
        try
        {
            var (changes, noHistory) = await Task.Run(() =>
            {
                var found = VanillaStructureCheck.Find(_context.ModFolder, _context.VanillaRoot, _context.Store, _context.CurrentVersion, token);
                var comparer = GameVersionComparer.Instance;
                var hasListing = _context.Store.ListStructureListingVersions().Any(v => comparer.Compare(v, _context.CurrentVersion) < 0);
                return (found, !hasSnapshots && !hasListing);
            }, token);
            if (!token.IsCancellationRequested)
            {
                ShowStructure(changes, noHistory);
            }
        }
        catch (OperationCanceledException)
        {
            // Neuer Lauf oder Fenster geschlossen: nichts mehr anzeigen.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!token.IsCancellationRequested)
            {
                _structureBusy.Text = ex.Message;
            }
        }
    }

    private void ShowStructure(IReadOnlyList<StructureChange> changes, bool noHistory)
    {
        _structureBusy.Visibility = Visibility.Collapsed;
        _structureNoHistory = noHistory;
        _structureList.ItemsSource = changes.Select(c => new StructureRow(c)).ToList();
        _structurePanel.Visibility = changes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshSelection();
    }

    /// <summary>Doppelklick nur auf einer Zeile (nicht auf leerer Fläche, Spaltenkopf oder Bildlaufleiste).</summary>
    private void OnStructureDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && Controls.ItemsControl.ContainerFromElement(_structureList, source) is Controls.ListViewItem { Content: StructureRow row })
        {
            OpenModEntry(row);
            e.Handled = true;
        }
    }

    /// <summary>Mod-Datei zum Lesen zeigen, einen Mod-Ordner (verschwundener Vanilla-Ordner) im Explorer.</summary>
    private void OpenModEntry(StructureRow row)
    {
        if (row.Change.Kind == StructureChangeKind.FolderGone || row.Change.ModFullPath.EndsWith(Path.DirectorySeparatorChar))
        {
            OpenFolder(row.Change.ModFullPath);
        }
        else
        {
            ShowFile(row.Change.ModFullPath);
        }
    }

    /// <summary>Zeigt eine Datei nur zum Lesen in einem Fenster über diesem (das Hauptfenster dahinter ist gesperrt, ein Laden dort bliebe unsichtbar).</summary>
    private void ShowFile(string path)
    {
        try
        {
            TextViewWindow.ShowDialog(this, path, File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(this, ex.Message, VanillaUpdateTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Den ersten neuen Vanilla-Ort öffnen: Ordner im Explorer, Datei zum Lesen.</summary>
    private void OpenVanillaPlace()
    {
        if (_structureList.SelectedItem is not StructureRow { CanOpenVanilla: true } row)
        {
            return;
        }

        var relative = row.Change.NewVanillaPaths[0];
        var full = Path.Combine(_context.VanillaRoot, relative.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
        if (relative.EndsWith('/'))
        {
            OpenFolder(full);
        }
        else
        {
            ShowFile(full);
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(path.TrimEnd(Path.DirectorySeparatorChar))}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(this, MiscWindowTexts.DiffExplorerFailed(ex.Message), VanillaUpdateTexts.StartTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Rescan()
    {
        var forced = _oldVersion.SelectedIndex > 0 ? _oldVersion.SelectedItem as string : null;
        _scan = VanillaUpdateRun.Scan(_context.Overridden, _context.Store, _context.CurrentVersion, _context.Ledger, _context.SupportedPattern, forced);
        if (_oldVersion.Items.Count == 0 || !_scan.RecordedVersionsBelowCurrent.SequenceEqual(_oldVersion.Items.Cast<object>().Skip(1).OfType<string>()))
        {
            var keep = forced;
            _oldVersion.SelectionChanged -= OnVersionChanged;
            _oldVersion.ItemsSource = new[] { VanillaUpdateTexts.OldVersionAutomatic }.Concat(_scan.RecordedVersionsBelowCurrent).ToList();
            _oldVersion.SelectedItem = keep is not null && _scan.RecordedVersionsBelowCurrent.Contains(keep) ? keep : VanillaUpdateTexts.OldVersionAutomatic;
            _oldVersion.SelectionChanged += OnVersionChanged;
        }

        RefreshSelection();
    }

    private void OnVersionChanged(object sender, Controls.SelectionChangedEventArgs e) => Rescan();

    private void RefreshSelection()
    {
        if (_scan is not { } scan)
        {
            return;
        }

        // Ausgeblendete Dateiarten (Grafik usw.) kommen nicht in den Lauf und zählen nicht mit.
        var hidden = _kinds.HiddenKinds;
        var changed = scan.Changed.Where(f => !CompatchFileFilter.IsHiddenKind(f.RelativePath, hidden)).ToList();
        var withoutOld = scan.WithoutOldVersion.Where(f => !CompatchFileFilter.IsHiddenKind(f.RelativePath, hidden)).ToList();
        if (_thisFile.IsChecked == true && _context.Selected is { } selected)
        {
            _files = scan.Changed.Concat(scan.WithoutOldVersion)
                .Where(f => string.Equals(f.RelativePath, selected.RelativePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            _files = changed.Concat(_includeWithoutOld.IsChecked == true ? withoutOld : Enumerable.Empty<UpdateFile>()).ToList();
        }

        var lines = new List<string> { VanillaUpdateTexts.Counts(changed.Count, withoutOld.Count) };
        if (_files.Count == 0)
        {
            lines.Add(VanillaUpdateTexts.NothingToDo);
        }

        _counts.Text = string.Join("\n", lines);
        var notes = new List<string>();
        if (_structureNoHistory)
        {
            notes.Add(VanillaUpdateTexts.StructureNoHistory);
        }

        if (withoutOld.Count > 0)
        {
            notes.Add(VanillaUpdateTexts.NoOldHint);
        }

        _notes.Text = string.Join("\n", notes);
        _start.IsEnabled = _files.Count > 0;
    }
}

/// <summary>Auftrag an das Hauptfenster: Lauf vorbereiten und den Kompatch-Merge im Update-Modus öffnen.</summary>
public sealed record VanillaUpdateLaunch(IReadOnlyList<UpdateFile> Files, string ModFolder, string ModName, string CurrentVersion, VanillaHistoryStore Store, Action AfterWrite);

/// <summary>Zeile der Strukturliste: Anzeige-Texte einer <see cref="StructureChange"/>.</summary>
internal sealed record StructureRow(StructureChange Change)
{
    public string File => Change.ModRelativePath;

    public string Label => VanillaDiffTexts.StructureChangeLabel(Change.Kind);

    public string NewPlace => Change.NewVanillaPaths.Count > 0 ? string.Join(", ", Change.NewVanillaPaths) : "–";

    /// <summary>"Vanilla-Ort öffnen" geht nur mit einem neuen Ort (nicht bei "Datei entfernt").</summary>
    public bool CanOpenVanilla => Change.Kind != StructureChangeKind.FileRemoved && Change.NewVanillaPaths.Count > 0;
}
