using System.IO;
using System.Windows;
using ParadoxModTool.Compatch;
using ParadoxModTool.Mods;
using ParadoxModTool.VersionManager;
using Controls = System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Reiter "Patch-Dateien" (nur lesend) und "Patch schreiben" (Spec, "Schreiben").</summary>
public sealed partial class CompatchWindow
{
    private readonly Controls.ListBox _fileList = new();
    private readonly Controls.TextBox _filePreview = new() { IsReadOnly = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = Controls.ScrollBarVisibility.Auto };
    private readonly Controls.ListBox _issueList = new() { MaxHeight = 140 };
    private readonly Controls.CheckBox _hideUnchanged = new() { Content = new Controls.TextBlock { Text = CompatchTexts.HideUnchanged }, IsChecked = true, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Center };
    private PreparedWrite? _prepared;
    private bool _filesStale = true;

    private sealed record FileRow(FileChange Change, string Text)
    {
        public override string ToString() => Text;
    }

    partial void InitializeFiles()
    {
        var panel = new Controls.DockPanel();
        var bar = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        Controls.DockPanel.SetDock(bar, Controls.Dock.Top);
        var refresh = new Controls.Button { Content = CompatchTexts.Refresh, Padding = new Thickness(8, 2, 8, 2) };
        refresh.Click += (_, _) => RefreshFiles();
        bar.Children.Add(refresh);
        bar.Children.Add(_hideUnchanged);
        panel.Children.Add(bar);

        var issues = new Controls.StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        Controls.DockPanel.SetDock(issues, Controls.Dock.Bottom);
        issues.Children.Add(new Controls.TextBlock { Text = CompatchTexts.IssuesHeader, FontWeight = FontWeights.Bold });
        issues.Children.Add(_issueList);
        panel.Children.Add(issues);

        var grid = new Controls.Grid();
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(6) });
        grid.ColumnDefinitions.Add(new Controls.ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.Children.Add(_fileList);
        var splitter = new Controls.GridSplitter { Width = 6, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        Controls.Grid.SetColumn(splitter, 1);
        grid.Children.Add(splitter);
        Controls.Grid.SetColumn(_filePreview, 2);
        grid.Children.Add(_filePreview);
        panel.Children.Add(grid);
        _filesHost.Child = panel;

        _fileList.SelectionChanged += (_, _) => _filePreview.Text = (_fileList.SelectedItem as FileRow) is { } row ? PreviewFor(row.Change) : string.Empty;
        _hideUnchanged.Click += (_, _) => FillFiles();
        _tabs.SelectionChanged += (_, e) =>
        {
            if (e.OriginalSource == _tabs && _tabs.SelectedIndex == 1 && _filesStale)
            {
                RefreshFiles();
            }
        };
    }

    partial void OnSessionChanged()
    {
        _filesStale = true;
        if (_tabs.SelectedIndex == 1)
        {
            RefreshFiles();
        }
    }

    private string PreviewFor(FileChange change)
    {
        var normalized = _session?.Run.Plan.Files.Any(f => f.Normalized && string.Equals(f.RelativePath.Replace('\\', '/'), change.RelativePath, StringComparison.OrdinalIgnoreCase)) == true;
        return (normalized ? "# " + CompatchTexts.NormalizedNote + "\n\n" : string.Empty) + change.PreviewText;
    }

    private IReadOnlyCollection<string> TargetLockedPaths() =>
        _state.TargetModId is { } id && _host.Registry.Get(id) is { } mod ? mod.LockedPaths : Array.Empty<string>();

    private void RefreshFiles()
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            _prepared = _session.PrepareWrite(TargetLockedPaths(), _host.EncodingFor);
            _filesStale = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CompatchInputException)
        {
            _status.Text = ex.Message;
            return;
        }

        FillFiles();
    }

    private void FillFiles()
    {
        if (_prepared is null)
        {
            return;
        }

        var keep = (_fileList.SelectedItem as FileRow)?.Change.RelativePath;
        var rows = _prepared.Changes
            .Where(c => _hideUnchanged.IsChecked != true || c.Kind != FileChangeKind.Unchanged)
            .OrderBy(c => c.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(c => new FileRow(c, $"{CompatchTexts.ChangeKind(c.Kind)}   {c.RelativePath}"))
            .ToList();
        _fileList.ItemsSource = rows;
        _fileList.SelectedItem = rows.FirstOrDefault(r => r.Change.RelativePath == keep) ?? rows.FirstOrDefault();
        _issueList.ItemsSource = _prepared.Issues.Select(i => i.Message).ToList();
    }

    partial void WriteClicked()
    {
        if (_session is null)
        {
            return;
        }

        var targetMod = _state.TargetModId is { } id ? _host.Registry.Get(id) : null;
        if (targetMod?.IsLocked == true)
        {
            MessageBox.Show(this, CompatchTexts.TargetLocked, CompatchTexts.WritePatch, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CompatchTargets.IsWorkshopFolder(_state.TargetFolder))
        {
            MessageBox.Show(this, CompatchTexts.ErrorWorkshopTarget, CompatchTexts.WritePatch, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_session.WriteBlockerFor(_cautious.IsChecked == true) is { } blocker)
        {
            SaveStateNow();
            MessageBox.Show(this, blocker, CompatchTexts.WritePatch, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        PreparedWrite prepared;
        try
        {
            prepared = _session.PrepareWrite(TargetLockedPaths(), _host.EncodingFor);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CompatchInputException)
        {
            MessageBox.Show(this, ex.Message, CompatchTexts.WritePatch, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int create = prepared.Count(FileChangeKind.Create), modify = prepared.Count(FileChangeKind.Modify), delete = prepared.Count(FileChangeKind.Delete);
        if (create + modify + delete == 0)
        {
            MessageBox.Show(this, CompatchTexts.NothingToWrite, CompatchTexts.WritePatch, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(this, CompatchTexts.WriteConfirm(create, modify, delete, prepared.Issues.Count, _state.TargetFolder), CompatchTexts.WritePatch, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        WriteResult result;
        _suppressWatchUntil = DateTime.UtcNow.AddSeconds(5);
        try
        {
            var folder = _state.TargetFolder;
            result = PatchWriter.Commit(prepared, folder, () =>
                new ModVersionManager(folder, StorageLocations.ResolveSnapshotsDirectory(folder, _host.Storage())).CreateSnapshot(IsUpdate ? "vor-vanilla-update_" + _state.UpdateNewVersion : "vor-kompatch"));
        }
        catch (CompatchWriteException ex)
        {
            MessageBox.Show(this, ex.Message, CompatchTexts.WritePatch, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally
        {
            _suppressWatchUntil = DateTime.UtcNow.AddSeconds(3);
        }

        var baseline = _session.CaptureBase(prepared,
            ModMetadata.Read(_state.ModAFolder, _host.MetadataFile)?.Version,
            ModMetadata.Read(_state.ModBFolder, _host.MetadataFile)?.Version);
        try
        {
            _store.SaveBase(baseline);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
        }

        SaveStateNow();
        if (IsUpdate)
        {
            RecordUpdatedVersions(prepared);
        }

        CompatchReportWindow.Show(this, result, prepared);
        OnSessionChanged();
        _afterUpdateWrite?.Invoke();
        OfferPatchRecipesAfterUpdate();
    }

    /// <summary>
    /// Update-Modus nach dem Schreiben: Dateien des Laufs ohne offene Stelle stehen jetzt auf der neuen Vanilla-Version (Spec, Abschnitt 8).
    /// Dateien mit offenen Stellen behalten ihren alten Eintrag, damit der nächste Lauf sie wieder gegen ihre alte Fassung vergleicht.
    /// </summary>
    private void RecordUpdatedVersions(PreparedWrite prepared)
    {
        if (_session is null || _state.UpdateNewVersion is not { } version || _updateInput?.Filter.OnlyPaths is not { } files)
        {
            return;
        }

        var open = _session.Actions
            .Where(a => a.Stage is MergeStage.Conflict or MergeStage.Review or MergeStage.Outdated && !_session.IsExplicit(a.Id))
            .Select(a => a.File)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var ledger = VanillaVersionLedger.Load(Path.GetDirectoryName(_store.Directory)!);
            ledger.Set(VanillaUpdateRun.UpdatedFiles(files, open, prepared), version);
            ledger.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = ex.Message;
        }
    }

    /// <summary>Nach einem Vanilla-Update: hat die Mod Patch-Rezepte, bietet PMT an, sie zu öffnen und neu anzuwenden (Spec, Etappe 2).</summary>
    private void OfferPatchRecipesAfterUpdate()
    {
        var folder = _state.TargetFolder;
        if (IsUpdate && !string.IsNullOrEmpty(folder))
        {
            // Der Merge hat Mod-Dateien geändert: ein offener Wächter-Scan dieser Mod zeigt den alten Stand.
            _host.PatchFilesChanged?.Invoke(folder);
        }

        if (!IsUpdate || _host.OpenPatchRecipes is not { } open || string.IsNullOrEmpty(folder)
            || !(ParadoxModTool.Patches.PatchRecipeStore.HasRecipes(StorageLocations.ResolvePatchRecipesDirectory(folder, _host.Storage()))
                || ParadoxModTool.Patches.PatchRecipeStore.HasRecipes(ParadoxModTool.Patches.PatchRecipeStore.Folder(folder))))
        {
            return;
        }

        if (MessageBox.Show(this, PatchRecipeTexts.ReapplyAfterUpdate, PatchRecipeTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            open(folder);
        }
    }
}
