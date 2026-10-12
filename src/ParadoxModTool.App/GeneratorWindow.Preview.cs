using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ParadoxModTool.Generators;
using ParadoxModTool.Patches;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Vorschau" (Spec Abschnitt 3, Mockup): Dateien nach Ordner mit Status, Filter-Chips, rechts der Vergleich "Jetzt" / "Nach dem Erzeugen".
/// Markierte Zeilen nimmt U zurück (wie bei den Patch-Rezepten); der Plan übernimmt den geänderten Text. "Erzeugen" schreibt den Plan als einen Schritt.
/// </summary>
public partial class GeneratorWindow
{
    private readonly DiffComparePane _previewPane = new();
    private readonly ListBox _fileList = new() { MinWidth = 360 };
    private readonly TextBlock _computedText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
    private readonly TextBlock _fileHeader = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBox _fileFilter = new() { Width = 160, VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<string, ToggleButton> _chips = new(StringComparer.Ordinal);
    private readonly ContentControl _detail = new();
    private readonly TextBlock _previewHint = new() { Text = GeneratorTexts.DiscardHint, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
    private GeneratorPlan? _plan;
    private string? _planModRoot;
    private string? _previewFile;
    private bool _loadingPreview;
    /// <summary>Zählt jedes Verwerfen des Plans (<see cref="ClearPlan"/>); ein Berechnen, das auf den Index wartet, erkennt daran, dass es veraltet ist.</summary>
    private int _planGeneration;
    /// <summary>Ein Berechnen läuft (es kann auf den Index warten); kein zweites parallel, Vorschau und Erzeugen sind so lange aus.</summary>
    private bool _planRunning;
    /// <summary>Das zuletzt gestartete Berechnen (für "Mod prüfen", das ein laufendes erst abwartet).</summary>
    private Task<bool>? _planRun;

    private void InitializePreview()
    {
        _previewPane.Initialize(() => false, _ => { }, _ => false);
        _previewPane.RightTextChanged += (_, _) =>
        {
            if (!_loadingPreview && _plan is not null && _previewFile is not null)
            {
                _plan = _plan.WithEditedFile(_previewFile, _previewPane.RightText);
                FillFileList();
            }
        };

        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        bar.Children.Add(_computedText);
        var recompute = new Button { Content = new TextBlock { Text = GeneratorTexts.Recompute }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 14, 0) };
        recompute.Click += async (_, _) => await ComputePlanAsync(showTab: true);
        bar.Children.Add(recompute);
        foreach (var chip in new[] { "new", "changed", "unchanged", "image" })
        {
            var toggle = new ToggleButton { IsChecked = chip != "unchanged", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 0, 6, 0), Content = new TextBlock() };
            toggle.Click += (_, _) => FillFileList();
            _chips[chip] = toggle;
            bar.Children.Add(toggle);
        }

        _fileFilter.ToolTip = GeneratorTexts.FileFilterTip;
        _fileFilter.TextChanged += (_, _) => FillFileList();
        bar.Children.Add(_fileFilter);

        _fileList.SelectionChanged += (_, _) => ShowSelectedFile();
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var next = new Button { Content = new TextBlock { Text = GeneratorTexts.NextDifference }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
        next.Click += (_, _) => _previewPane.JumpToDifference(forward: true);
        var previous = new Button { Content = new TextBlock { Text = GeneratorTexts.PreviousDifference }, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
        previous.Click += (_, _) => _previewPane.JumpToDifference(forward: false);
        DockPanel.SetDock(next, Dock.Right);
        DockPanel.SetDock(previous, Dock.Right);
        header.Children.Add(next);
        header.Children.Add(previous);
        header.Children.Add(_fileHeader);
        _previewHint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");

        var right = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_previewHint, Dock.Bottom);
        right.Children.Add(header);
        right.Children.Add(_previewHint);
        right.Children.Add(_detail);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(_fileList);
        var splitter = new GridSplitter { Width = 5, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        Grid.SetColumn(splitter, 1);
        body.Children.Add(splitter);
        Grid.SetColumn(right, 2);
        body.Children.Add(right);

        var root = new DockPanel { Margin = new Thickness(6) };
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        _planWarnings.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
        DockPanel.SetDock(_planWarnings, Dock.Top);
        root.Children.Add(_planWarnings);
        root.Children.Add(body);
        PreviewHost.Content = root;
        PreviewKeyDown += Preview_PreviewKeyDown;
    }

    private void ClearPlan()
    {
        _planGeneration++;
        _plan = null;
        _previewFile = null;
        ShowPlanWarnings();
        PreviewTabItem.IsEnabled = false;
        PreviewTabHeader.Text = GeneratorTexts.PreviewTab;
        if (ReferenceEquals(Tabs.SelectedItem, PreviewTabItem))
        {
            Tabs.SelectedItem = FormTab;
        }
    }

    private async void Preview_Click(object sender, RoutedEventArgs e) => await ComputePlanAsync(showTab: true);

    /// <summary>
    /// Prüft das Formular (Fehler halten an, Warnungen fragen) und rendert den Plan; mit <paramref name="showTab"/> wechselt das Fenster zur Vorschau.
    /// Muss der Index der Lokalisierungsschlüssel erst eingelesen werden, wartet das im Hintergrund (Fortschrittsfenster mit Abbrechen); so lange sind
    /// Vorschau und Erzeugen aus und der alte Plan im Reiter "Vorschau" gesperrt (Änderungen daran überschriebe der neue Plan). Ohne Warten endet es synchron.
    /// </summary>
    private Task<bool> ComputePlanAsync(bool showTab)
    {
        if (_planRunning || _package is null || _state is null)
        {
            return Task.FromResult(false);
        }

        return _planRun = RunComputePlanAsync(showTab);
    }

    private async Task<bool> RunComputePlanAsync(bool showTab)
    {
        _planRunning = true;
        PreviewButton.IsEnabled = GenerateButton.IsEnabled = PreviewHost.IsEnabled = false;
        try
        {
            return await ComputePlanCoreAsync(showTab);
        }
        finally
        {
            _planRunning = false;
            PreviewHost.IsEnabled = true;
            PreviewButton.IsEnabled = GenerateButton.IsEnabled = _state is not null;
        }
    }

    private async Task<bool> ComputePlanCoreAsync(bool showTab)
    {
        if (_package is null || _state is null)
        {
            return false;
        }

        var issues = Issues();
        if (issues.FirstOrDefault(i => i.Severity == IssueSeverity.Error) is { } error)
        {
            Revalidate();
            MessageBox.Show(this, (error.Path is null ? string.Empty : error.Path + ": ") + error.Message, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (issues.Count > 0 && MessageBox.Show(this, GeneratorTexts.ConfirmWarnings(string.Join(Environment.NewLine, issues.Select(i => i.Message))), GeneratorTexts.Title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return false;
        }

        if (_state.ModRoot is not { } modRoot || !System.IO.Directory.Exists(modRoot))
        {
            MessageBox.Show(this, GeneratorTexts.NoMod, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var output = _state.Target == "output" ? _state.OutputFolder : null;
        if (_state.Target == "output" && string.IsNullOrWhiteSpace(output))
        {
            MessageBox.Show(this, GeneratorTexts.NoOutputFolder, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        string? renumbered;
        try
        {
            if (await PlanWithHashCheckAsync(new GeneratorTarget(modRoot, output, _host.ProfileLanguages(), ExternalPathStore.Load(_package.DataFolder))) is not { } planned)
            {
                // Veraltet (Formular oder Stand geändert, Fenster geschlossen): still verwerfen
                return false;
            }

            (_plan, renumbered) = planned;
            _planModRoot = modRoot;
        }
        catch (OperationCanceledException)
        {
            if (!_closed)
            {
                ClearPlan();
                ShowStatus(GeneratorTexts.PreviewCancelled);
            }

            return false;
        }
        catch (Exception ex) when (ex is GeneratorTemplateException or GeneratorOutputException or IOException or UnauthorizedAccessException)
        {
            _plan = null;
            if (!_closed)
            {
                MessageBox.Show(this, GeneratorTexts.PlanFailed(ex.Message), GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return false;
        }

        if (renumbered is not null)
        {
            // Der Stand heißt jetzt anders: Formular und Liste zeigen den neuen Schlüssel, der Plan bleibt
            var plan = _plan;
            ShowState(_state);
            RefreshStates(_state.Key);
            _plan = plan;
            _planModRoot = modRoot;
            MessageBox.Show(this, renumbered, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        _computedText.Text = GeneratorTexts.Computed(DateTime.Now.ToString("t"));
        _previewFile = null;
        _detail.Content = null;
        PreviewTabItem.IsEnabled = true;
        PreviewTabHeader.Text = GeneratorTexts.PreviewTabCount(_plan.Files.Count);
        ShowPlanWarnings();
        FillFileList();
        if (showTab)
        {
            Tabs.SelectedItem = PreviewTabItem;
        }

        return true;
    }

    private static string Kind(GeneratorPlannedFile file) =>
        file.Bytes is not null ? "image" : file.Status switch { PlannedFileStatus.New => "new", PlannedFileStatus.Changed => "changed", _ => "unchanged" };

    private void FillFileList()
    {
        if (_plan is null)
        {
            return;
        }

        foreach (var (kind, toggle) in _chips)
        {
            ((TextBlock)toggle.Content).Text = GeneratorTexts.Chip(kind, _plan.Files.Count(f => Kind(f) == kind));
        }

        var filter = _fileFilter.Text.Trim();
        var selected = _previewFile;
        _fileList.Items.Clear();
        foreach (var group in _plan.Files
                     .Where(f => _chips[Kind(f)].IsChecked == true && (filter.Length == 0 || f.RelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                     .GroupBy(f => Path.GetDirectoryName(f.RelativePath.Replace('/', Path.DirectorySeparatorChar))?.Replace(Path.DirectorySeparatorChar, '/') ?? string.Empty)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var folder = new TextBlock { Text = group.Key, FontWeight = FontWeights.SemiBold };
            folder.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            _fileList.Items.Add(new ListBoxItem { Content = folder, IsEnabled = false });
            foreach (var file in group.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0) };
                var status = new TextBlock { Text = GeneratorTexts.StatusLabel(Kind(file)), Width = 74 };
                status.SetResourceReference(TextBlock.ForegroundProperty, Kind(file) switch { "new" => "Theme.DiffAdded", "changed" => "Theme.Warning", _ => "Theme.TextMuted" });
                line.Children.Add(status);
                line.Children.Add(new TextBlock { Text = Path.GetFileName(file.RelativePath) });
                if (file.ModBaseText is not null)
                {
                    var fromMod = new TextBlock { Text = " · " + GeneratorTexts.FromModFile, ToolTip = GeneratorTexts.FromModFileTip };
                    fromMod.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
                    line.Children.Add(fromMod);
                }

                if (file.RemovedBlocks.Count > 0)
                {
                    var warning = new TextBlock { Text = " ⚠ " + GeneratorTexts.BlocksRemovedShort(file.RemovedBlocks.Count), ToolTip = string.Join(Environment.NewLine, file.RemovedBlocks) };
                    warning.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
                    line.Children.Add(warning);
                }

                _fileList.Items.Add(new ListBoxItem { Content = line, Tag = file.RelativePath });
            }
        }

        _fileList.SelectedItem = _fileList.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.Tag as string == selected)
            ?? _fileList.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.Tag is string);
    }

    private void ShowSelectedFile()
    {
        if (_plan is null || _fileList.SelectedItem is not ListBoxItem { Tag: string path } || _plan.Files.FirstOrDefault(f => f.RelativePath == path) is not { } file)
        {
            return;
        }

        if (path == _previewFile && _detail.Content is not null)
        {
            return;
        }

        _previewFile = path;
        _fileHeader.Text = path;
        if (file.Bytes is { } bytes)
        {
            var panel = new StackPanel { Margin = new Thickness(8) };
            panel.Children.Add(new TextBlock { Text = GeneratorTexts.ResultBefore + ": " + (file.OriginalBytes is { } old ? GeneratorTexts.Bytes(old.Length) : GeneratorTexts.NoFile) });
            panel.Children.Add(new TextBlock { Text = GeneratorTexts.ResultAfter + ": " + GeneratorTexts.Bytes(bytes.Length), Margin = new Thickness(0, 4, 0, 0) });
            _detail.Content = panel;
            return;
        }

        _loadingPreview = true;
        try
        {
            var target = file.ExternalPath ?? Path.Combine(_plan.WriteRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (file.ModBaseText is { } modBase && _planModRoot is { } modRoot)
            {
                // Ausgabeordner ohne Kopie: links die Datei der Mod als Grundlage, damit nur die echten Änderungen auffallen; ohne U (beide Seiten schreibgeschützt)
                _previewPane.LoadTexts(modBase, file.NewText ?? string.Empty, GeneratorTexts.ResultModBase, GeneratorTexts.ResultAfter,
                    Path.Combine(modRoot, path.Replace('/', Path.DirectorySeparatorChar)), target);
                _previewHint.Text = GeneratorTexts.ModBaseHint;
            }
            else
            {
                _previewPane.LoadPreview(file.OriginalText ?? string.Empty, file.NewText ?? string.Empty, GeneratorTexts.ResultBefore, GeneratorTexts.ResultAfter, target);
                _previewHint.Text = GeneratorTexts.DiscardHint;
            }
        }
        finally
        {
            _loadingPreview = false;
        }

        _detail.Content = _previewPane;
    }

    /// <summary>Kürzel der Vorschau, nur im Reiter "Vorschau" und nicht beim Tippen in Eingabefeldern: U, Esc, F7, Umschalt+F7, Strg+Z.</summary>
    private void Preview_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Während des Berechnens ist der alte Plan gesperrt (siehe ComputePlanAsync)
        if (_planRunning || !ReferenceEquals(Tabs.SelectedItem, PreviewTabItem))
        {
            return;
        }

        if (e.Key == Key.F7 && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            _previewPane.JumpToDifference(forward: Keyboard.Modifiers == ModifierKeys.None);
            e.Handled = true;
            return;
        }

        var typing = Keyboard.FocusedElement is TextBox;
        if (Keyboard.Modifiers == ModifierKeys.None && _previewPane.HasMarks && !typing)
        {
            if (e.Key == Key.U)
            {
                _previewPane.TakeMarked();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                _previewPane.ClearMarks();
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z && !typing && _previewPane.UndoLastTake())
        {
            e.Handled = true;
        }
    }

    /// <summary>Erzeugen: Plan berechnen, falls keiner da ist; entfallende Blöcke bestätigen; schreiben; danach Arbeitsstand speichern.</summary>
    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_planRunning)
        {
            return;
        }

        var seenInPreview = _plan is not null && ReferenceEquals(Tabs.SelectedItem, PreviewTabItem);
        if (_plan is null && !await ComputePlanAsync(showTab: false))
        {
            return;
        }

        // Warnungen des Plans (Hash-Kollisionen) sieht man nur in der Vorschau; wer ohne sie erzeugt, wird vorher gefragt
        if (!seenInPreview && _plan!.Warnings.Count > 0 && MessageBox.Show(this, GeneratorTexts.ConfirmWarnings(string.Join(Environment.NewLine, _plan.Warnings)),
                GeneratorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var removed = _plan!.Files.SelectMany(f => f.RemovedBlocks.Select(b => f.RelativePath + ": " + b)).ToList();
        if (removed.Count > 0 && MessageBox.Show(this, GeneratorTexts.ConfirmRemoved(string.Join(Environment.NewLine, removed.Take(20))), GeneratorTexts.Title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var outcome = _host.Apply(_plan, GeneratorTexts.UndoName(_package!.Name, _state!.Key));
        // Der Hinweis des Pakets (z. B. ein Skript, das danach laufen soll) erscheint nur nach einem erfolgreichen Erzeugen
        var message = outcome.Success && _package.AfterGenerate is { } hint ? outcome.Message + "\n\n" + hint : outcome.Message;
        MessageBox.Show(this, message, GeneratorTexts.Title, MessageBoxButton.OK, outcome.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        if (outcome.Success)
        {
            RebuildModKeyIndex();
            SaveState();
            ClearPlan();
        }
    }
}
