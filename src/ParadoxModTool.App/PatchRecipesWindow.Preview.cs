using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using ParadoxModTool.Patches;
using Controls = System.Windows.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Eine Datei in der Vorschau-Liste: Status, Pfad, Notizen.</summary>
public sealed class PreviewFileItem
{
    public PreviewFileItem(PlannedFile file) => File = file;

    public PlannedFile File { get; }

    public string Path => File.RelativePath;

    public string Status => File.Status.ToString();

    public string StatusText => File.Status switch
    {
        PlannedFileStatus.New => PatchRecipeTexts.StatusNew,
        PlannedFileStatus.Changed => PatchRecipeTexts.StatusChanged,
        PlannedFileStatus.Conflict => PatchRecipeTexts.StatusConflict,
        _ => PatchRecipeTexts.StatusUnchanged,
    };

    public string Notes => string.Join(" · ", File.Notes);
}

/// <summary>Ein Baustein mit Konflikt in der Konfliktleiste.</summary>
public sealed class ConflictItem
{
    public ConflictItem(ConflictUnit unit) => Unit = unit;

    public ConflictUnit Unit { get; }

    public string Display => Unit.UnitKey + " — " + string.Join("; ", Unit.Messages);
}

/// <summary>
/// Reiter "Vorschau" (Spec, "Apply pipeline and preview"): Plan berechnen, Dateiliste mit Status, Vergleich aktuell gegen geplant
/// (Zeilen mit U zurücknehmen), Konflikte je Baustein lösen, übersprungene Zeilen und Warnungen, Anwenden als ein Rückgängig-Schritt.
/// </summary>
public partial class PatchRecipesWindow
{
    private readonly DiffComparePane _previewPane = new();
    private readonly Controls.ListBox _previewFiles = new();
    private readonly Controls.TextBox _previewFilter = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly Controls.CheckBox _hideUnchanged = new() { Content = new Controls.TextBlock { Text = PatchRecipeTexts.HideUnchanged }, IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly Controls.TextBlock _countNew = new();
    private readonly Controls.TextBlock _countChanged = new();
    private readonly Controls.TextBlock _countConflict = new();
    private readonly Controls.TextBlock _applyText = new();
    private readonly Controls.Button _applyButton = new() { Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0), IsEnabled = false };
    private readonly Controls.ListBox _conflictList = new() { DisplayMemberPath = nameof(ConflictItem.Display), MaxHeight = 90 };
    private readonly Controls.DockPanel _conflictBar = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Controls.Expander _skippedExpander = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly Controls.TextBlock _adoptText = new();
    private readonly Controls.Button _adoptButton = new() { Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 0, 4), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Visibility = Visibility.Collapsed, ToolTip = PatchRecipeTexts.AdoptExistingTip };
    private readonly Controls.TextBox _skippedText = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 110, VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto };
    private readonly Controls.TextBlock _noPlan = new() { Text = PatchRecipeTexts.NoPlan, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private PatchPlan? _plan;
    private string? _previewFile;
    private ConflictUnit? _previewConflict;
    private bool _refreshingPreview;

    partial void InitPreview()
    {
        _previewPane.Initialize(() => false, _ => { }, _ => false);
        _previewPane.RightTextChanged += (_, _) => PreviewTextEdited();
        _previewFiles.ItemTemplate = (DataTemplate)XamlReader.Parse(
            "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">" +
            "<StackPanel Margin=\"0,2\"><StackPanel Orientation=\"Horizontal\">" +
            "<Border CornerRadius=\"3\" Padding=\"4,0\" Margin=\"0,0,6,0\"><Border.Style><Style TargetType=\"Border\">" +
            "<Setter Property=\"Background\" Value=\"{DynamicResource Theme.TextMuted}\" /><Style.Triggers>" +
            "<DataTrigger Binding=\"{Binding Status}\" Value=\"New\"><Setter Property=\"Background\" Value=\"{DynamicResource Theme.DiffAdded}\" /></DataTrigger>" +
            "<DataTrigger Binding=\"{Binding Status}\" Value=\"Changed\"><Setter Property=\"Background\" Value=\"{DynamicResource Theme.DiffModified}\" /></DataTrigger>" +
            "<DataTrigger Binding=\"{Binding Status}\" Value=\"Conflict\"><Setter Property=\"Background\" Value=\"{DynamicResource Theme.CompatchConflict}\" /></DataTrigger>" +
            "</Style.Triggers></Style></Border.Style><TextBlock Text=\"{Binding StatusText}\" FontSize=\"11\" /></Border>" +
            "<TextBlock Text=\"{Binding Path}\" /></StackPanel>" +
            "<TextBlock Text=\"{Binding Notes}\" FontSize=\"11\" Foreground=\"{DynamicResource Theme.TextMuted}\" TextTrimming=\"CharacterEllipsis\" />" +
            "</StackPanel></DataTemplate>");
        _previewFiles.SelectionChanged += (_, _) =>
        {
            if (!_refreshingPreview && _previewFiles.SelectedItem is PreviewFileItem item)
            {
                ShowPreviewFile(item.Path);
            }
        };
        _previewFilter.TextChanged += (_, _) => RefreshPreviewList();
        _hideUnchanged.Click += (_, _) => RefreshPreviewList();
        _conflictList.SelectionChanged += (_, _) =>
        {
            if (!_refreshingPreview && _conflictList.SelectedItem is ConflictItem conflict)
            {
                ShowConflict(conflict.Unit);
            }
        };

        var compute = new Controls.Button { Content = new Controls.TextBlock { Text = PatchRecipeTexts.ComputePreview }, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(12, 0, 0, 0) };
        compute.Click += async (_, _) => await GuardedAsync(ComputePreviewAsync);
        _applyButton.Content = _applyText;
        _applyButton.Click += async (_, _) => await GuardedAsync(ApplyPreviewAsync);
        var bar = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        bar.Children.Add(_hideUnchanged);
        bar.Children.Add(Badge(_countNew, "Theme.DiffAdded"));
        bar.Children.Add(Badge(_countChanged, "Theme.DiffModified"));
        bar.Children.Add(Badge(_countConflict, "Theme.CompatchConflict"));
        bar.Children.Add(compute);
        bar.Children.Add(_applyButton);

        var resolve = new Controls.Button { Content = new Controls.TextBlock { Text = PatchRecipeTexts.ResolveButton }, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        resolve.Click += (_, _) => ResolveShownConflict();
        var conflictTitle = new Controls.TextBlock { Text = PatchRecipeTexts.ConflictBarTitle, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) };
        Controls.DockPanel.SetDock(conflictTitle, Controls.Dock.Top);
        Controls.DockPanel.SetDock(resolve, Controls.Dock.Right);
        _conflictBar.Children.Add(conflictTitle);
        _conflictBar.Children.Add(resolve);
        _conflictBar.Children.Add(_conflictList);

        var filterRow = new Controls.DockPanel();
        var loupe = new Controls.TextBlock { Text = "🔍", Margin = new Thickness(0, 0, 4, 4), VerticalAlignment = VerticalAlignment.Center };
        Controls.DockPanel.SetDock(loupe, Controls.Dock.Left);
        filterRow.Children.Add(loupe);
        filterRow.Children.Add(_previewFilter);
        var left = new Controls.DockPanel { Width = 340, Margin = new Thickness(0, 0, 8, 0) };
        Controls.DockPanel.SetDock(filterRow, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_noPlan, Controls.Dock.Top);
        left.Children.Add(filterRow);
        left.Children.Add(_noPlan);
        left.Children.Add(_previewFiles);

        var right = new Controls.DockPanel();
        Controls.DockPanel.SetDock(_conflictBar, Controls.Dock.Top);
        right.Children.Add(_conflictBar);
        right.Children.Add(_previewPane);

        _adoptButton.Content = _adoptText;
        _adoptButton.Click += async (_, _) => await GuardedAsync(AdoptExistingAsync);
        var skippedPanel = new Controls.DockPanel();
        Controls.DockPanel.SetDock(_adoptButton, Controls.Dock.Top);
        skippedPanel.Children.Add(_adoptButton);
        skippedPanel.Children.Add(_skippedText);
        _skippedExpander.Content = skippedPanel;
        Controls.DockPanel.SetDock(bar, Controls.Dock.Top);
        Controls.DockPanel.SetDock(_skippedExpander, Controls.Dock.Bottom);
        Controls.DockPanel.SetDock(left, Controls.Dock.Left);
        PreviewRoot.Children.Add(bar);
        PreviewRoot.Children.Add(_skippedExpander);
        PreviewRoot.Children.Add(left);
        PreviewRoot.Children.Add(right);
        PreviewKeyDown += PreviewTab_PreviewKeyDown;
        SetPlan(null);
    }

    partial void PreviewRecipeSelected() => SetPlan(null);

    partial void PreviewRecipeEdited()
    {
        if (_plan is not null)
        {
            SetPlan(null);
            SetStatus(PatchRecipeTexts.PlanDropped);
        }
    }

    private static Controls.Border Badge(Controls.TextBlock text, string token)
    {
        var border = new Controls.Border { CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(8, 0, 0, 0), Child = text, VerticalAlignment = VerticalAlignment.Center };
        border.SetResourceReference(Controls.Border.BackgroundProperty, token);
        return border;
    }

    /// <summary>"Vorschau berechnen": der Plan, wenn er berechnet wurde und das Rezept inzwischen noch gewählt ist; sonst null (Grund in der Statuszeile).</summary>
    private async Task<PatchPlan?> ComputePreviewAsync()
    {
        if (Recipe is not { } recipe)
        {
            return null;
        }

        SaveNow();
        var plan = await RunPlanAsync(recipe);
        if (plan is null || !ReferenceEquals(recipe, Recipe))
        {
            return null;
        }

        SetPlan(plan);
        SetStatus(recipe.Rows.Count == 0 && recipe.Rules.Any(r => r.Enabled && r.Scope == RuleScope.PerRow)
            ? PatchRecipeTexts.NoRowsHint
            : PatchRecipeTexts.PlanReady(plan.Files.Count(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed or PlannedFileStatus.Conflict)));
        return plan;
    }

    /// <summary>
    /// Nach einem KI-Import (Spec 2026-10-12-patch-recipes-ai-guide, Abschnitt 2): Rezepte neu laden, das neue wählen, seine Erklärung aufklappen,
    /// in den Reiter "Vorschau" wechseln und den Plan berechnen (ohne zu schreiben). Rückgabe ist der Plan oder null (Grund in der Statuszeile).
    /// </summary>
    internal async Task<PatchPlan?> ShowImportedAsync(string recipeId)
    {
        SaveNow();
        ReloadRecipes(recipeId);
        if (Recipe is not { } recipe || !string.Equals(recipe.Id, recipeId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        DescriptionExpander.IsExpanded = true;
        Tabs.SelectedItem = PreviewTab;
        return await ComputePreviewAsync();
    }

    private void SetPlan(PatchPlan? plan)
    {
        _plan = plan;
        _pendingAdoption = null;
        _previewFile = null;
        _previewConflict = null;
        _noPlan.Visibility = plan is null ? Visibility.Visible : Visibility.Collapsed;
        _previewPane.LoadTexts(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        _conflictBar.Visibility = Visibility.Collapsed;
        RefreshPreviewList();
        var skipped = plan is null
            ? new List<string>()
            : plan.Skipped.Select(s => PatchRecipeTexts.SkippedLine(s.RuleId, s.RowKey, s.Reason)).Concat(plan.Warnings).ToList();
        _skippedExpander.Header = PatchRecipeTexts.SkippedHeader(skipped.Count);
        _skippedText.Text = string.Join(Environment.NewLine, skipped);
        var adoptable = plan?.Skipped.Count(s => s.AlreadyDefined) ?? 0;
        _adoptButton.Visibility = adoptable > 0 ? Visibility.Visible : Visibility.Collapsed;
        _adoptText.Text = PatchRecipeTexts.AdoptButton(adoptable);
        if (adoptable > 0)
        {
            _skippedExpander.IsExpanded = true;
        }
    }

    /// <summary>"Bestehende Blöcke übernehmen": schaltet die Option bei den Block-Kopien mit "bereits definiert" ein und berechnet neu.</summary>
    private async Task AdoptExistingAsync()
    {
        if (_plan is not { } plan || Recipe is not { } recipe)
        {
            return;
        }

        var ruleIds = plan.Skipped.Where(s => s.AlreadyDefined).Select(s => s.RuleId).ToHashSet(StringComparer.Ordinal);
        foreach (var rule in recipe.Rules.OfType<BlockCopyRule>().Where(r => ruleIds.Contains(r.Id)))
        {
            rule.AdoptExisting = true;
        }

        MarkRecipeEdited();
        RefreshRuleList(SelectedRule);
        await ComputePreviewAsync();
    }

    /// <summary>Liste, Zähler und Anwenden-Knopf aus dem aktuellen Plan; die gewählte Datei bleibt gewählt.</summary>
    private void RefreshPreviewList()
    {
        _refreshingPreview = true;
        var files = _plan?.Files ?? (IReadOnlyList<PlannedFile>)Array.Empty<PlannedFile>();
        var filter = _previewFilter.Text.Trim();
        var items = files
            .Where(f => _hideUnchanged.IsChecked != true || f.Status != PlannedFileStatus.Unchanged || f.Notes.Count > 0)
            .Where(f => filter.Length == 0 || f.RelativePath.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Status == PlannedFileStatus.Conflict ? 0 : 1)
            .ThenBy(f => f.RelativePath, StringComparer.Ordinal)
            .Select(f => new PreviewFileItem(f))
            .ToList();
        _previewFiles.ItemsSource = items;
        _previewFiles.SelectedItem = items.FirstOrDefault(i => i.Path == _previewFile);
        _refreshingPreview = false;

        var writable = files.Count(f => f.Status is PlannedFileStatus.New or PlannedFileStatus.Changed);
        _countNew.Text = PatchRecipeTexts.CountNew(files.Count(f => f.Status == PlannedFileStatus.New));
        _countChanged.Text = PatchRecipeTexts.CountChanged(files.Count(f => f.Status == PlannedFileStatus.Changed));
        _countConflict.Text = PatchRecipeTexts.CountConflict(files.Count(f => f.Status == PlannedFileStatus.Conflict));
        _applyText.Text = PatchRecipeTexts.ApplyButton(writable);
        _applyButton.IsEnabled = _plan is not null && writable > 0;
    }

    private void ShowPreviewFile(string relativePath)
    {
        if (_plan?.Files.FirstOrDefault(f => f.RelativePath == relativePath) is not { } file || ModRoot is not { } modRoot)
        {
            return;
        }

        _previewFile = relativePath;
        var conflicts = _plan.ConflictUnits.Where(c => c.File == relativePath).Select(c => new ConflictItem(c)).ToList();
        _refreshingPreview = true;
        _conflictList.ItemsSource = conflicts;
        _refreshingPreview = false;
        _conflictBar.Visibility = conflicts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (conflicts.Count > 0)
        {
            _conflictList.SelectedIndex = 0;
            return;
        }

        _previewConflict = null;
        _previewPane.LoadPreview(file.OriginalText ?? string.Empty, file.NewText, PatchRecipeTexts.ResultBefore, PatchRecipeTexts.ResultAfter, FullPath(modRoot, relativePath));
    }

    private void ShowConflict(ConflictUnit conflict)
    {
        if (ModRoot is not { } modRoot)
        {
            return;
        }

        _previewConflict = conflict;
        _previewPane.LoadPreview(conflict.Mine, conflict.Merged, PatchRecipeTexts.ConflictLeft, PatchRecipeTexts.ConflictRight, FullPath(modRoot, conflict.File));
    }

    /// <summary>Zurückgenommene Zeilen (U) oder Tippen rechts: der Plan übernimmt den Text der Datei.</summary>
    private void PreviewTextEdited()
    {
        if (_plan is null || _previewFile is null || _previewConflict is not null)
        {
            return;
        }

        _plan = PatchPlanEdits.WithEditedFile(_plan, _previewFile, _previewPane.RightText);
        RefreshPreviewList();
    }

    private void ResolveShownConflict()
    {
        if (_plan is null || _previewConflict is not { } conflict)
        {
            return;
        }

        _plan = PatchPlanEdits.ResolveConflict(_plan, conflict.File, conflict.RuleId, conflict.UnitKey, _previewPane.RightText);
        _previewConflict = null;
        RefreshPreviewList();
        ShowPreviewFile(conflict.File);
    }

    private async Task ApplyPreviewAsync()
    {
        if (_plan is not { } plan || Context() is not { } context)
        {
            return;
        }

        if (PatchPlanEdits.LostAdoptions(plan) is { Count: > 0 } lost)
        {
            MessageBox.Show(this, PatchRecipeTexts.AdoptionLost(string.Join(", ", lost)), PatchRecipeTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var conflicted = plan.Files.Count(f => f.Status == PlannedFileStatus.Conflict);
        if (conflicted > 0
            && MessageBox.Show(this, PatchRecipeTexts.ConflictsSkippedConfirm(conflicted), PatchRecipeTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        // Vor dem Schreiben merken: der normale Weg berechnet danach neu, und SetPlan verwirft die ausstehende Übernahme.
        var pendingAdoption = _pendingAdoption;
        var outcome = _host.Apply(context, plan);
        SetStatus(outcome.Message);
        if (outcome.Success)
        {
            MarkWatchFilesChanged();
        }

        if (outcome.Success && pendingAdoption is not null)
        {
            await AfterRenameAppliedAsync(pendingAdoption);
        }
        else if (outcome.Success)
        {
            var message = outcome.Message;
            await ComputePreviewAsync();
            SetStatus(message);
        }
    }

    /// <summary>Kürzel des Vergleichs nur im Reiter "Vorschau" und nicht beim Tippen in Eingabefeldern: U, Esc, F7, Umschalt+F7, Strg+Z.</summary>
    private void PreviewTab_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!ReferenceEquals(Tabs.SelectedItem, PreviewTab))
        {
            return;
        }

        if (e.Key == Key.F7 && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
        {
            _previewPane.JumpToDifference(forward: Keyboard.Modifiers == ModifierKeys.None);
            e.Handled = true;
            return;
        }

        var typing = Keyboard.FocusedElement is Controls.TextBox;
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

    private static string FullPath(string root, string relative) => System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
}
