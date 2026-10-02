using System.IO;
using System.Windows;
using ParadoxModTool.Localization;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Patches;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>Eine Zeile der Wächter-Liste: Status, Datei, Fundstellen je Muster, Notiz.</summary>
public sealed class WatchRowView
{
    public WatchRowView(WatchHit hit) => Hit = hit;

    public WatchHit Hit { get; }

    public string Status => PatchRecipeTexts.WatchStatusName(Hit.Status);

    public string Path => Hit.File.RelativePath;

    /// <summary>Nach dem Anwenden übrige Treffer, sonst die in Vanilla, sonst die in der Mod.</summary>
    public string Findings
    {
        get
        {
            var file = Hit.File;
            var lines = file.PlannedLines.Count > 0 ? file.PlannedLines : file.VanillaLines.Count > 0 ? file.VanillaLines : file.ModLines;
            return string.Join(", ", lines.GroupBy(l => l.Pattern, StringComparer.Ordinal).Select(g => PatchRecipeTexts.WatchFindingCount(g.Count(), g.Key)));
        }
    }

    public string Note => Hit.IgnoreNote ?? string.Empty;
}

/// <summary>Eine Fundstelle in der Liste unter dem Raster; Doppelklick öffnet die Datei an der Zeile.</summary>
public sealed class WatchLineItem
{
    public required string Display { get; init; }

    public required string FullPath { get; init; }

    public required int Line { get; init; }
}

/// <summary>
/// Reiter "Wächter" (Spec 2026-09-30-patch-recipes, Teil 3): Dateien, die die Suchmuster oder Zeilenschlüssel des Rezepts verwenden, mit Status und
/// passender Aktion. Gescannt wird beim ersten Öffnen des Reiters und auf "Neu scannen"; Ignorieren stuft ohne neuen Scan neu ein.
/// </summary>
public partial class PatchRecipesWindow
{
    // ListView mit GridView wie im Reiter "Tabelle": dafür gibt es Theme-Stile (Themes/Controls.xaml), für DataGrid nicht.
    private readonly Controls.ListView _watchGrid = new() { SelectionMode = Controls.SelectionMode.Single };

    private readonly Controls.TextBox _watchFilter = new() { VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Controls.CheckBox _watchHideDone = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.WatchHideDone }, IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly Dictionary<WatchStatus, Controls.TextBlock> _watchCounts = new();
    private readonly Controls.TextBlock _watchState = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.ListBox _watchLines = new() { DisplayMemberPath = nameof(WatchLineItem.Display), Height = 120, Margin = new Thickness(0, 6, 0, 6) };
    private readonly Controls.Button _watchCopyRule = WatchButton(PatchRecipeTexts.WatchCopyAndRule, PatchRecipeTexts.WatchCopyAndRuleTip);
    private readonly Controls.Button _watchIgnore = WatchButton(PatchRecipeTexts.WatchIgnore, null);
    private readonly Controls.Button _watchIgnoreFolder = WatchButton(PatchRecipeTexts.WatchIgnoreFolder, PatchRecipeTexts.WatchIgnoreFolderTip);
    private readonly Controls.Button _watchUnignore = WatchButton(PatchRecipeTexts.WatchUnignore, PatchRecipeTexts.WatchUnignoreTip);
    private readonly Controls.Button _watchOpenUpdate = WatchButton(PatchRecipeTexts.WatchOpenUpdate, null);
    private readonly Controls.Button _watchOpen = WatchButton(PatchRecipeTexts.WatchOpen, null);
    private WatchScan? _watchScan;
    private IReadOnlySet<string> _watchOutdated = new HashSet<string>();
    private IReadOnlyList<WatchHit> _watchHits = Array.Empty<WatchHit>();
    private bool _watchScanning;
    private bool _watchFilesChanged;

    partial void InitWatch()
    {
        var view = new Controls.GridView();
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.WatchColStatus, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(WatchRowView.Status)), Width = 130 });
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.WatchColPath, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(WatchRowView.Path)), Width = 430 });
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.WatchColFindings, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(WatchRowView.Findings)), Width = 430 });
        view.Columns.Add(new Controls.GridViewColumn { Header = PatchRecipeTexts.WatchColNote, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(WatchRowView.Note)), Width = 200 });
        _watchGrid.View = view;
        _watchGrid.SelectionChanged += (_, _) => ShowWatchSelection();
        _watchGrid.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && FindParent<Controls.ListViewItem>(source) is not null)
            {
                RunMainWatchAction();
            }
        };
        _watchLines.MouseDoubleClick += (_, _) =>
        {
            if (_watchLines.SelectedItem is WatchLineItem item)
            {
                _host.OpenFileAtLine(item.FullPath, item.Line);
            }
        };
        _watchFilter.TextChanged += (_, _) => RefreshWatchList();
        _watchHideDone.Click += (_, _) => RefreshWatchList();

        _watchCopyRule.Click += (_, _) => OpenCopyRuleMenu();
        _watchIgnore.Click += (_, _) => IgnoreSelected();
        _watchIgnoreFolder.Click += (_, _) => IgnoreSelectedFolder();
        _watchUnignore.Click += (_, _) => UnignoreSelected();
        _watchOpenUpdate.Click += (_, _) =>
        {
            if (SelectedWatchHit is { } hit && _modRoot is not null)
            {
                _host.OpenInUpdateTool(_modRoot, hit.File.RelativePath);
            }
        };
        _watchOpen.Click += (_, _) => OpenSelectedModFile();

        var patterns = new Controls.Button { Content = new Controls.TextBlock { Text = PatchRecipeTexts.WatchPatterns }, ToolTip = PatchRecipeTexts.WatchPatternsTip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(12, 0, 0, 0) };
        patterns.Click += (_, _) => EditWatchPatterns();
        var rescan = new Controls.Button { Content = new Controls.TextBlock { Text = PatchRecipeTexts.WatchRescan }, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        rescan.Click += async (_, _) => await ScanWatchAsync();

        var bar = new Controls.DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var loupe = new Controls.TextBlock { Text = "🔍", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
        var right = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        right.Children.Add(_watchHideDone);
        foreach (var (status, token) in new[]
                 {
                     (WatchStatus.Uncovered, "Theme.CompatchConflict"), (WatchStatus.Outdated, "Theme.Warning"), (WatchStatus.OwnFile, "Theme.DiffModified"),
                     (WatchStatus.Covered, "Theme.DiffAdded"), (WatchStatus.Ignored, "Theme.TextMuted"),
                 })
        {
            var count = new Controls.TextBlock();
            _watchCounts[status] = count;
            right.Children.Add(Badge(count, token));
        }

        right.Children.Add(patterns);
        right.Children.Add(rescan);
        Controls.DockPanel.SetDock(loupe, Controls.Dock.Left);
        Controls.DockPanel.SetDock(right, Controls.Dock.Right);
        bar.Children.Add(loupe);
        bar.Children.Add(right);
        bar.Children.Add(_watchFilter);

        var actions = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
        foreach (var button in new[] { _watchCopyRule, _watchOpenUpdate, _watchOpen, _watchIgnore, _watchIgnoreFolder, _watchUnignore })
        {
            actions.Children.Add(button);
        }

        Controls.DockPanel.SetDock(bar, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_watchState, Controls.Dock.Top);
        Controls.DockPanel.SetDock(actions, Controls.Dock.Bottom);
        Controls.DockPanel.SetDock(_watchLines, Controls.Dock.Bottom);
        WatchRoot.Children.Add(bar);
        WatchRoot.Children.Add(_watchState);
        WatchRoot.Children.Add(actions);
        WatchRoot.Children.Add(_watchLines);
        WatchRoot.Children.Add(_watchGrid);

        Tabs.SelectionChanged += async (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, Tabs) && ReferenceEquals(Tabs.SelectedItem, WatchTab) && _watchScan is null && !_watchScanning && Recipe is not null)
            {
                await ScanWatchAsync();
            }
        };
        ReclassifyWatch();
    }

    partial void WatchRecipeSelected()
    {
        _watchScan = null;
        _watchOutdated = new HashSet<string>();
        ReclassifyWatch();
        if (ReferenceEquals(Tabs.SelectedItem, WatchTab) && Recipe is not null)
        {
            Dispatcher.InvokeAsync(async () => await ScanWatchAsync());
        }
    }

    partial void WatchRecipeEdited() => UpdateWatchState();

    private WatchHit? SelectedWatchHit => (_watchGrid.SelectedItem as WatchRowView)?.Hit;

    private static Controls.Button WatchButton(string text, string? tip) =>
        new() { Content = new Controls.TextBlock { Text = text }, ToolTip = tip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0), IsEnabled = false };

    /// <summary>Scan im Hintergrund mit Fortschrittsfenster; Abbrechen behält das vorige Ergebnis.</summary>
    private async Task ScanWatchAsync()
    {
        if (_watchScanning || Recipe is not { } recipe)
        {
            return;
        }

        if (Context() is not { } context || _modRoot is not { } modRoot)
        {
            SetStatus(ContextMissingText);
            return;
        }

        var copy = PatchRecipeStore.Clone(recipe);
        var lookupOutdated = _host.PrepareOutdatedFiles(modRoot);
        using var cancel = new CancellationTokenSource();
        var progressWindow = new TaskProgressWindow(PatchRecipeTexts.WatchScanTitle, PatchRecipeTexts.WatchScanHint, CommonTexts.Cancel, (_, done, total) => PatchRecipeTexts.WatchFileProgress(done, total), () => cancel.Cancel()) { Owner = this };
        var progress = new Progress<(int Done, int Total)>(p => progressWindow.Report(new CompatProgress(PatchRecipeTexts.WatchFileProgress(p.Done, p.Total), p.Done, p.Total)));
        _watchScanning = true;
        progressWindow.Show();
        try
        {
            var (scan, outdated) = await Task.Run(() => (PatchWatcher.Scan(context, copy, cancel.Token, progress), lookupOutdated()), cancel.Token);
            // Ergebnis nur übernehmen, wenn das Rezept noch gewählt ist; sonst holt der Code nach finally den Scan des neuen nach.
            if (ReferenceEquals(Recipe, recipe))
            {
                _watchScan = outdated.Warning is null ? scan : scan with { Warnings = scan.Warnings.Append(outdated.Warning).ToList() };
                _watchOutdated = outdated.Paths;
                _watchFilesChanged = false;
                ReclassifyWatch();
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus(PatchRecipeTexts.WatchScanCancelled);
        }
        catch (Exception ex)
        {
            // Läuft aus async-void-Handlern (Knopf, Reiterwechsel): kein Fehler darf bis zum Dispatcher durchschlagen und PMT beenden.
            SetStatus((ex as AggregateException)?.InnerException?.Message ?? ex.Message);
        }
        finally
        {
            _watchScanning = false;
            progressWindow.CloseAfterFinished();
        }

        // Während des Scans das Rezept gewechselt: dessen Scan wurde verworfen und konnte nicht starten, also jetzt nachholen.
        if (!ReferenceEquals(Recipe, recipe) && Recipe is not null && _watchScan is null && ReferenceEquals(Tabs.SelectedItem, WatchTab))
        {
            await ScanWatchAsync();
        }
    }

    /// <summary>Ein anderes Werkzeug (Merge nach Spiel-Update) hat Dateien dieser Mod geändert.</summary>
    internal void FilesChangedOutside(string modRoot)
    {
        if (_modRoot is not null && string.Equals(Path.TrimEndingDirectorySeparator(_modRoot), Path.TrimEndingDirectorySeparator(modRoot), StringComparison.OrdinalIgnoreCase))
        {
            MarkWatchFilesChanged();
        }
    }

    /// <summary>Nach "Anwenden": Dateien haben sich geändert, der letzte Scan zeigt den alten Stand.</summary>
    internal void MarkWatchFilesChanged()
    {
        _watchFilesChanged = _watchScan is not null;
        UpdateWatchState();
    }

    /// <summary>Stuft den letzten Scan mit dem aktuellen Rezept (Ignorierliste) neu ein und baut Liste, Zähler und Reiter-Zahl neu.</summary>
    private void ReclassifyWatch()
    {
        _watchHits = _watchScan is not null && Recipe is { } recipe ? PatchWatcher.Classify(_watchScan, recipe, _watchOutdated) : Array.Empty<WatchHit>();
        foreach (var (status, text) in _watchCounts)
        {
            text.Text = PatchRecipeTexts.WatchStatusName(status) + ": " + _watchHits.Count(h => h.Status == status);
        }

        var open = _watchHits.Count(h => PatchWatcher.IsOpen(h.Status));
        WatchBadge.Visibility = open > 0 ? Visibility.Visible : Visibility.Collapsed;
        WatchBadgeText.Text = open.ToString();
        RefreshWatchList();
        UpdateWatchState();
    }

    private void UpdateWatchState()
    {
        if (_watchScan is null)
        {
            _watchState.Text = PatchRecipeTexts.WatchNotScanned;
            return;
        }

        var parts = new List<string>();
        if (Recipe is { } recipe && PatchWatcher.IsStale(_watchScan, recipe))
        {
            parts.Add(PatchRecipeTexts.WatchStale);
        }
        else if (_watchFilesChanged)
        {
            parts.Add(PatchRecipeTexts.WatchFilesChanged);
        }

        parts.Add(PatchRecipeTexts.WatchScanned(_watchHits.Count, _watchHits.Count(h => PatchWatcher.IsOpen(h.Status))));
        parts.AddRange(_watchScan.Warnings);
        _watchState.Text = string.Join(" · ", parts);
    }

    private void RefreshWatchList()
    {
        var selected = SelectedWatchHit?.File.RelativePath;
        var filter = _watchFilter.Text.Trim();
        var hideDone = _watchHideDone.IsChecked == true;
        var rows = _watchHits
            .Where(h => !hideDone || h.Status is not (WatchStatus.Covered or WatchStatus.Ignored))
            .Select(h => new WatchRowView(h))
            .Where(r => filter.Length == 0 || r.Path.Contains(filter, StringComparison.OrdinalIgnoreCase) || r.Findings.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _watchGrid.ItemsSource = rows;
        _watchGrid.SelectedItem = rows.FirstOrDefault(r => string.Equals(r.Path, selected, StringComparison.OrdinalIgnoreCase));
        ShowWatchSelection();
    }

    /// <summary>Fundstellen der gewählten Datei und die passenden Aktionen.</summary>
    private void ShowWatchSelection()
    {
        var hit = SelectedWatchHit;
        var items = new List<WatchLineItem>();
        if (hit is not null && _modRoot is not null && _host.VanillaRoot() is var vanilla)
        {
            var file = hit.File;
            var modPath = FullPath(_modRoot, file.RelativePath);
            if (vanilla is not null)
            {
                items.AddRange(file.VanillaLines.DistinctBy(l => l.Line).Select(l => new WatchLineItem { Display = PatchRecipeTexts.WatchLineVanilla(l.Line, l.Text), FullPath = FullPath(vanilla, file.RelativePath), Line = l.Line }));
            }

            items.AddRange(file.ModLines.DistinctBy(l => l.Line).Select(l => new WatchLineItem { Display = PatchRecipeTexts.WatchLineMod(l.Line, l.Text), FullPath = modPath, Line = l.Line }));
            if (file.ModExists)
            {
                items.AddRange(file.PlannedLines.DistinctBy(l => l.Line).Select(l => new WatchLineItem { Display = PatchRecipeTexts.WatchLinePlanned(l.Line, l.Text), FullPath = modPath, Line = l.Line }));
            }
            else
            {
                items.AddRange(file.PlannedLines.DistinctBy(l => l.Line).Select(l => new WatchLineItem { Display = PatchRecipeTexts.WatchLinePlanned(l.Line, l.Text), FullPath = vanilla is null ? modPath : FullPath(vanilla, file.RelativePath), Line = l.Line }));
            }
        }

        _watchLines.ItemsSource = items;
        _watchCopyRule.IsEnabled = hit is { Status: WatchStatus.Uncovered, File.VanillaExists: true };
        _watchOpenUpdate.IsEnabled = hit is { Status: WatchStatus.Outdated };
        _watchOpen.IsEnabled = hit is { File.ModExists: true };
        _watchIgnore.IsEnabled = hit is not null && hit.Status != WatchStatus.Ignored;
        _watchIgnoreFolder.IsEnabled = hit is not null && hit.Status != WatchStatus.Ignored && hit.File.RelativePath.Replace('\\', '/').Contains('/');
        _watchUnignore.IsEnabled = hit is { Status: WatchStatus.Ignored };
    }

    /// <summary>Doppelklick auf eine Zeile: die Hauptaktion ihres Status.</summary>
    private void RunMainWatchAction()
    {
        switch (SelectedWatchHit)
        {
            case { Status: WatchStatus.Uncovered, File.VanillaExists: true }:
                OpenCopyRuleMenu();
                break;
            case { Status: WatchStatus.Outdated } hit when _modRoot is not null:
                _host.OpenInUpdateTool(_modRoot, hit.File.RelativePath);
                break;
            case { File.ModExists: true, Status: not WatchStatus.Ignored }:
                OpenSelectedModFile();
                break;
        }
    }

    /// <summary>"Kopie anlegen + Regel": eine vorhandene Zeilen-Regel wählen oder eine neue, ausgeschaltete anlegen.</summary>
    private void OpenCopyRuleMenu()
    {
        if (SelectedWatchHit is not { } hit || Recipe is not { } recipe)
        {
            return;
        }

        var menu = new Controls.ContextMenu { PlacementTarget = _watchCopyRule, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var rule in recipe.Rules.OfType<LineRule>())
        {
            // TextBlock statt Text: sonst liest das Menü den ersten "_" eines Regelnamens (every_special_…) als Zugriffstaste und blendet ihn aus.
            var name = rule.Name.Length > 0 ? rule.Name : rule.Id;
            var item = new Controls.MenuItem { Header = new Controls.TextBlock { Text = name } };
            item.Click += (_, _) =>
            {
                var used = RecipeEditing.AddFileToLineRule(recipe, rule, hit.File.RelativePath);
                MarkRecipeEdited();
                RefreshRuleList(SelectedRule);
                SetStatus(!used.Enabled ? PatchRecipeTexts.WatchRuleAddedDisabled(used.Name.Length > 0 ? used.Name : used.Id)
                    : ReferenceEquals(used, rule) ? PatchRecipeTexts.WatchRuleAdded(name)
                    : PatchRecipeTexts.WatchRuleSibling(used.Name));
            };
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new Controls.Separator());
        }

        var create = new Controls.MenuItem { Header = new Controls.TextBlock { Text = PatchRecipeTexts.WatchNewRule } };
        create.Click += (_, _) =>
        {
            var line = hit.File.PlannedLines.Concat(hit.File.VanillaLines).Concat(hit.File.ModLines).FirstOrDefault();
            if (line is null)
            {
                return;
            }

            var rule = RecipeEditing.NewLineRuleForHit(recipe, hit.File.RelativePath, line);
            MarkRecipeEdited();
            SelectRule(rule.Id);
            SetStatus(PatchRecipeTexts.WatchRuleCreated);
        };
        menu.Items.Add(create);
        menu.IsOpen = true;
    }

    private void IgnoreSelected()
    {
        if (SelectedWatchHit is not { } hit || Recipe is not { } recipe)
        {
            return;
        }

        var note = TextPromptWindow.Ask(this, PatchRecipeTexts.WatchIgnore, PatchRecipeTexts.WatchIgnorePrompt(hit.File.RelativePath), hit.IgnoreNote ?? string.Empty);
        if (note is null)
        {
            return;
        }

        RecipeEditing.Ignore(recipe, hit.File.RelativePath, note.Trim());
        MarkWatchSettingsEdited();
        ReclassifyWatch();
    }

    /// <summary>"Ordner ignorieren": der Ordner der gewählten Datei samt Unterordnern, mit Notiz.</summary>
    private void IgnoreSelectedFolder()
    {
        if (SelectedWatchHit is not { } hit || Recipe is not { } recipe)
        {
            return;
        }

        var path = hit.File.RelativePath.Replace('\\', '/');
        var slash = path.LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        var note = TextPromptWindow.Ask(this, PatchRecipeTexts.WatchIgnoreFolder, PatchRecipeTexts.WatchIgnoreFolderPrompt(path[..slash]), string.Empty);
        if (note is null)
        {
            return;
        }

        RecipeEditing.IgnoreFolder(recipe, path, note.Trim());
        MarkWatchSettingsEdited();
        ReclassifyWatch();
    }

    private void UnignoreSelected()
    {
        if (SelectedWatchHit is not { } hit || Recipe is not { } recipe)
        {
            return;
        }

        RecipeEditing.Unignore(recipe, hit.File.RelativePath);
        MarkWatchSettingsEdited();
        ReclassifyWatch();
    }

    /// <summary>"Öffnen": die Mod-Datei an der ersten Fundstelle.</summary>
    private void OpenSelectedModFile()
    {
        if (SelectedWatchHit is not { File.ModExists: true } hit || _modRoot is null)
        {
            return;
        }

        var line = hit.File.ModLines.FirstOrDefault()?.Line ?? hit.File.PlannedLines.FirstOrDefault()?.Line ?? 1;
        _host.OpenFileAtLine(FullPath(_modRoot, hit.File.RelativePath), line);
    }

    private void EditWatchPatterns()
    {
        if (Recipe is not { } recipe)
        {
            return;
        }

        var text = TextPromptWindow.Ask(this, PatchRecipeTexts.WatchPatterns, PatchRecipeTexts.WatchPatternsPrompt, string.Join(Environment.NewLine, recipe.Watch.Patterns), multiline: true);
        if (text is null)
        {
            return;
        }

        RecipeEditing.SetWatchPatterns(recipe, text.Split('\n'));
        MarkWatchSettingsEdited();
        UpdateWatchState();
    }

    private static T? FindParent<T>(DependencyObject? child)
        where T : DependencyObject
    {
        while (child is not null and not T)
        {
            child = child is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);
        }

        return child as T;
    }
}
