using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Highlighting;
using ParadoxModTool.Localization;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Patches;
using ParadoxModTool.VanillaDiff;
using ParadoxModTool.VersionManager;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>Was das Fenster vom Hauptfenster braucht; alles als Abfrage, damit es immer den aktuellen Stand liest.</summary>
public sealed record PatchWindowHost(
    Func<string?> ActiveModRoot,
    Func<string?> VanillaRoot,
    Func<string?> GameId,
    Func<StorageSettings> Storage,
    Func<IHighlightingDefinition?> Highlighting,
    Func<PatchContext, PatchPlan, FixOutcome> Apply,
    Action<string, int> OpenFileAtLine,
    Action<string, string> OpenInUpdateTool,
    Func<string, Func<OutdatedLookup>> PrepareOutdatedFiles,
    Func<ParadoxModTool.GameProfiles.GameProfile?> Profile,
    Func<IReadOnlyList<(string Name, string Path)>> KnownMods);

/// <summary>Ergebnis der Suche nach "veraltet" für den Wächter: Pfade und, falls sie scheiterte, eine Warnung.</summary>
public sealed record OutdatedLookup(IReadOnlySet<string> Paths, string? Warning);

/// <summary>
/// Fenster "Patch-Rezepte" (Spec 2026-09-30-patch-recipes, Etappe 2): oben Mod und Rezept, darunter die Reiter Tabelle, Regeln und Vorschau
/// (je eine eigene Datei). Änderungen am Rezept werden nach 500 ms und beim Schließen gespeichert.
/// </summary>
public partial class PatchRecipesWindow : Window
{
    private readonly PatchWindowHost _host;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly List<PatchRecipe> _recipes = new();
    private string? _modRoot;
    private bool _dirty;
    private bool _loading;
    private bool _fillingDescription;
    private AiTaskWindow? _aiWindow;

    public PatchRecipesWindow(PatchWindowHost host)
    {
        _host = host;
        InitializeComponent();
        _saveTimer.Tick += (_, _) => SaveNow();
        PreviewKeyDown += Guide_PreviewKeyDown;
        InitTable();
        InitRules();
        InitWatch();
        InitPreview();
        _modRoot = host.ActiveModRoot();
        ReloadRecipes(null);
    }

    /// <summary>Das gewählte Rezept; null ohne Mod oder ohne Rezepte.</summary>
    internal PatchRecipe? Recipe => RecipeBox.SelectedItem as PatchRecipe;

    internal string? ModRoot => _modRoot;

    /// <summary>Wählt eine Mod (z. B. nach "Merge nach Update") und lädt ihre Rezepte.</summary>
    public void SelectMod(string modRoot)
    {
        SaveNow();
        _modRoot = modRoot;
        ReloadRecipes(null);
    }

    /// <summary>Nur Suchmuster oder Ignorierliste des Wächters geändert: speichern (verzögert), die berechnete Vorschau bleibt gültig.</summary>
    internal void MarkWatchSettingsEdited() => ScheduleSave();

    /// <summary>Nur die Erklärung geändert: speichern (verzögert); sie ändert nichts an der Mod, deshalb bleiben Vorschau und Wächter gültig.</summary>
    private void MarkDescriptionEdited() => ScheduleSave();

    private void ScheduleSave()
    {
        if (_loading)
        {
            return;
        }

        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Rezeptordner der gewählten Mod (dort liegen Rezepte und Gedächtnis-Dateien); nur mit gewählter Mod.</summary>
    internal string RecipesFolderOfCurrentMod() =>
        _modRoot is null ? throw new InvalidOperationException("no mod selected") : RecipesFolder(_modRoot);

    /// <summary>Vergebene Rezept-Kennungen der gewählten Mod: geladene Rezepte und Rezeptdateien, auch unlesbare (sonst überschriebe ein Import sie).</summary>
    internal IReadOnlyList<string> RecipeIdsOfCurrentMod()
    {
        var ids = _recipes.Select(r => r.Id).ToList();
        var folder = _modRoot is null ? null : RecipesFolder(_modRoot);
        if (folder is not null && Directory.Exists(folder))
        {
            ids.AddRange(Directory.EnumerateFiles(folder, "*.json")
                .Select(Path.GetFileName)
                .Where(name => !name!.EndsWith(".memory.json", StringComparison.OrdinalIgnoreCase))
                .Select(name => Path.GetFileNameWithoutExtension(name!)));
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Das geladene Rezept mit dieser Kennung in der gewählten Mod; null, wenn es keines gibt.</summary>
    internal PatchRecipe? FindRecipe(string id) => _recipes.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Der Text der Statuszeile, z. B. warum keine Vorschau berechnet wurde.</summary>
    internal string StatusLine => StatusText.Text;

    /// <summary>Die Reiter melden hier jede Änderung am Rezept: speichern (verzögert) und abhängige Ansichten verwerfen.</summary>
    internal void MarkRecipeEdited()
    {
        if (_loading)
        {
            return;
        }

        _dirty = true;
        _saveTimer.Stop();
        _saveTimer.Start();
        OnRecipeEdited();
    }

    /// <summary>Kontext für den Motor: Mod, Spielwurzel, installierte Spielversion und Vanilla-Historie der Mod; null ohne Mod oder Spiel.</summary>
    internal PatchContext? Context()
    {
        var vanilla = _host.VanillaRoot();
        if (_modRoot is null || vanilla is null)
        {
            return null;
        }

        var history = VanillaHistoryStore.FromHistoryRoot(StorageLocations.ResolveVanillaHistoryDirectory(_modRoot, _host.Storage()));

        // replace_path der Mod: diese Vanilla-Ordner lädt das Spiel nicht, der Wächter durchsucht sie deshalb nicht.
        var profile = _host.Profile();
        var hidden = profile is null ? null : ParadoxModTool.Mods.ReplacePaths.Of(new[] { _modRoot }, profile.MetadataFile, profile.ReplacePathRoots);
        // Neue Dateien nach dem Spielprofil kodieren (Stellaris: Skriptdateien ohne BOM)
        return new PatchContext(_modRoot, vanilla, GameVersionReader.TryReadVersion(vanilla), history, RecipesFolder(_modRoot), hidden, profile?.ReplacePathRoots)
        {
            NewFileHasBom = extension => ParadoxModTool.GameProfiles.ModFileEncoding.NewFileHasBom(profile, extension),
        };
    }

    internal void SetStatus(string text) => StatusText.Text = text;

    /// <summary>Warum <see cref="Context"/> null ist: keine Mod gewählt oder kein Spielordner gefunden.</summary>
    internal string ContextMissingText => _modRoot is null ? PatchRecipeTexts.NoMod : PatchRecipeTexts.NoGameFolder;

    /// <summary>Für async-void-Klicks: ein Fehler landet in der Statuszeile, statt bis zum Dispatcher durchzuschlagen und PMT zu beenden.</summary>
    internal async Task GuardedAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    /// <summary>
    /// Berechnet einen Plan im Hintergrund mit Fortschrittsfenster (Regel i von n, Abbrechen); null bei Abbruch oder Fehler (Grund in der Statuszeile).
    /// Das Rezept wird vorher kopiert, damit Bearbeitungen im Fenster den Lauf nicht stören.
    /// </summary>
    internal async Task<PatchPlan?> RunPlanAsync(PatchRecipe recipe)
    {
        if (Context() is not { } context)
        {
            SetStatus(ContextMissingText);
            return null;
        }

        var copy = PatchRecipeStore.Clone(recipe);
        using var cancel = new CancellationTokenSource();
        var progressWindow = new TaskProgressWindow(PatchRecipeTexts.PlanProgressTitle, PatchRecipeTexts.PlanProgressHint, CommonTexts.Cancel, (_, done, total) => PatchRecipeTexts.RuleProgress(done, total), () => cancel.Cancel()) { Owner = this };
        var progress = new Progress<(int Done, int Total)>(p => progressWindow.Report(new CompatProgress(PatchRecipeTexts.RuleProgress(p.Done, p.Total), p.Done, p.Total)));
        progressWindow.Show();
        try
        {
            return await Task.Run(() => PatchRunner.Plan(context, copy, cancel.Token, progress), cancel.Token);
        }
        catch (OperationCanceledException)
        {
            SetStatus(PatchRecipeTexts.PlanCancelled);
            return null;
        }
        catch (Exception ex)
        {
            // Läuft aus async-void-Handlern ("Vorschau berechnen"): jeder Fehler gehört in die Statuszeile, keiner darf PMT beenden.
            SetStatus(ex.Message);
            return null;
        }
        finally
        {
            progressWindow.CloseAfterFinished();
        }
    }

    partial void InitTable();

    partial void InitRules();

    partial void InitWatch();

    partial void InitPreview();

    partial void TableRecipeSelected();

    partial void RulesRecipeSelected();

    partial void WatchRecipeSelected();

    partial void WatchRecipeEdited();

    partial void PreviewRecipeSelected();

    partial void PreviewRecipeEdited();

    /// <summary>Ein anderes Rezept (oder keines) ist gewählt: alle Reiter neu aufbauen.</summary>
    private void OnRecipeSelected()
    {
        FillDescription();
        TableRecipeSelected();
        RulesRecipeSelected();
        WatchRecipeSelected();
        PreviewRecipeSelected();
    }

    /// <summary>Das gewählte Rezept wurde geändert: eine berechnete Vorschau passt nicht mehr.</summary>
    private void OnRecipeEdited()
    {
        PreviewRecipeEdited();
        WatchRecipeEdited();
    }

    /// <summary>Wechselt in den Reiter "Regeln" und wählt die Regel aus (z. B. nach "Neue Zeilen-Regel" im Wächter).</summary>
    internal void SelectRule(string ruleId)
    {
        Tabs.SelectedItem = RulesTab;
        RefreshRuleList(Recipe?.Rules.FirstOrDefault(r => r.Id == ruleId));
    }

    /// <summary>Rezeptordner der Mod nach der Speicherort-Einstellung; standardmäßig außerhalb der Mod, damit Rezepte nicht mit hochgeladen werden.</summary>
    private string RecipesFolder(string modRoot) => StorageLocations.ResolvePatchRecipesDirectory(modRoot, _host.Storage());

    /// <summary>Holt Rezepte vom früheren Ort im Mod-Ordner an den eingestellten Ort; Rückgabe ist die Meldung für die Statuszeile.</summary>
    private string MoveRecipesOutOfMod(string modRoot)
    {
        var folder = RecipesFolder(modRoot);
        try
        {
            var move = PatchRecipeStore.MoveFromModFolder(modRoot, folder);
            var notes = new List<string>();
            if (move.Moved > 0)
            {
                notes.Add(PatchRecipeTexts.RecipesMoved(move.Moved, folder));
            }

            if (move.Kept.Count > 0)
            {
                notes.Add(PatchRecipeTexts.RecipesKept(string.Join(", ", move.Kept), PatchRecipeStore.Folder(modRoot)));
            }

            return string.Join(" ", notes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PatchRecipeTexts.RecipesMoveFailed(ex.Message);
        }
    }

    private void ReloadRecipes(string? selectId)
    {
        _loading = true;
        try
        {
            _recipes.Clear();
            var errors = new List<string>();
            var moveNote = string.Empty;
            if (_modRoot is not null)
            {
                moveNote = MoveRecipesOutOfMod(_modRoot);
                _recipes.AddRange(PatchRecipeStore.LoadAll(RecipesFolder(_modRoot), errors).OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase));
            }

            RecipeBox.ItemsSource = null;
            RecipeBox.ItemsSource = _recipes;
            RecipeBox.SelectedItem = _recipes.FirstOrDefault(r => r.Id == selectId) ?? _recipes.FirstOrDefault();

            ModNameText.Text = _modRoot is null ? PatchRecipeTexts.NoMod : Path.GetFileName(_modRoot.TrimEnd('\\', '/'));
            ModNameText.ToolTip = _modRoot;
            var vanilla = _host.VanillaRoot();
            var version = vanilla is null ? null : GameVersionReader.TryReadVersion(vanilla);
            GameVersionText.Text = version is null ? string.Empty : PatchRecipeTexts.GameVersion(version);

            var hasMod = _modRoot is not null;
            RecipeBox.IsEnabled = hasMod && _recipes.Count > 0;
            NewRecipeButton.IsEnabled = AiRecipeButton.IsEnabled = hasMod;
            RenameRecipeButton.IsEnabled = DeleteRecipeButton.IsEnabled = Recipe is not null;
            Tabs.IsEnabled = Recipe is not null;
            var status = errors.Count > 0 ? PatchRecipeTexts.LoadErrors(errors.Count) + ": " + string.Join("; ", errors)
                : hasMod && _recipes.Count == 0 ? PatchRecipeTexts.NoRecipe
                : string.Empty;
            SetStatus(string.Join(" ", new[] { moveNote, status }.Where(s => s.Length > 0)));
        }
        finally
        {
            _loading = false;
        }

        OnRecipeSelected();
    }

    /// <summary>Speichert ausstehende Änderungen am gewählten Rezept sofort (sonst nach 500 ms), z. B. bevor ein KI-Import dasselbe Rezept ersetzt.</summary>
    internal void SaveNow()
    {
        _saveTimer.Stop();
        if (!_dirty || _modRoot is null || Recipe is not { } recipe)
        {
            _dirty = false;
            return;
        }

        try
        {
            PatchRecipeStore.Save(RecipesFolder(_modRoot), recipe);
            _dirty = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus(PatchRecipeTexts.SaveFailed(ex.Message));
        }
    }

    private void ChooseMod_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = PatchRecipeTexts.ChooseMod, UseDescriptionForTitle = true };
        dialog.ClientGuid = ParadoxModTool.GameProfiles.GameDialogMemory.For("patchRecipes.mod");
        if (_modRoot is not null)
        {
            dialog.InitialDirectory = ParadoxModTool.Explorer.PathNormalization.ToWindowsPath(_modRoot);
        }

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SelectMod(dialog.SelectedPath);
        }
    }

    private void RecipeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        foreach (var removed in e.RemovedItems.OfType<PatchRecipe>())
        {
            if (_dirty && _modRoot is not null)
            {
                PatchRecipeStore.Save(RecipesFolder(_modRoot), removed);
                _dirty = false;
            }
        }

        RenameRecipeButton.IsEnabled = DeleteRecipeButton.IsEnabled = Tabs.IsEnabled = Recipe is not null;
        OnRecipeSelected();
    }

    /// <summary>"Neues Rezept": leer oder (nur CK III) aus der Vorlage "Special Buildings in normale Gebäude".</summary>
    private void NewRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_modRoot is null)
        {
            return;
        }

        if (_host.GameId() != "ck3")
        {
            CreateRecipe(fromPreset: false);
            return;
        }

        var menu = new ContextMenu { PlacementTarget = NewRecipeButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var empty = new MenuItem { Header = new TextBlock { Text = PatchRecipeTexts.NewEmpty } };
        empty.Click += (_, _) => CreateRecipe(fromPreset: false);
        var preset = new MenuItem { Header = new TextBlock { Text = PatchRecipeTexts.NewFromPreset } };
        preset.Click += (_, _) => CreateRecipe(fromPreset: true);
        var resources = new MenuItem { Header = new TextBlock { Text = PatchRecipeTexts.NewFromPresetResources } };
        resources.Click += (_, _) => CreateRecipe(fromPreset: true, resourcesPreset: true);
        menu.Items.Add(empty);
        menu.Items.Add(preset);
        menu.Items.Add(resources);
        menu.IsOpen = true;
    }

    private void CreateRecipe(bool fromPreset, bool resourcesPreset = false)
    {
        var initial = !fromPreset ? string.Empty : resourcesPreset ? PatchRecipeTexts.PresetResourcesDefaultName : PatchRecipeTexts.PresetDefaultName;
        if (_modRoot is null || TextPromptWindow.Ask(this, PatchRecipeTexts.NewRecipe, PatchRecipeTexts.NewPrompt, initial)?.Trim() is not { Length: > 0 } name)
        {
            return;
        }

        SaveNow();
        var ids = _recipes.Select(r => r.Id);
        var recipe = !fromPreset ? RecipeEditing.Create(name, _host.GameId() ?? string.Empty, ids)
            : resourcesPreset ? SpecialResourcesPreset.Create(name, ids)
            : SpecialBuildingsPreset.Create(name, ids);
        PatchRecipeStore.Save(RecipesFolder(_modRoot), recipe);
        ReloadRecipes(recipe.Id);
        if (fromPreset)
        {
            // Eine Vorlage bringt Spalten und Regeln, aber keine Zeilen: gleich die Specials zum Auswählen anbieten.
            Tabs.SelectedItem = TableTab;
            ImportSpecials();
        }
    }

    private void RenameRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (Recipe is not { } recipe || TextPromptWindow.Ask(this, PatchRecipeTexts.Rename, PatchRecipeTexts.RenamePrompt, recipe.Name)?.Trim() is not { Length: > 0 } name)
        {
            return;
        }

        recipe.Name = name;
        _dirty = true;
        SaveNow();
        ReloadRecipes(recipe.Id);
    }

    private void DeleteRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_modRoot is null || Recipe is not { } recipe
            || MessageBox.Show(this, PatchRecipeTexts.DeleteConfirm(recipe.Name), PatchRecipeTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _dirty = false;
        PatchRecipeStore.Delete(RecipesFolder(_modRoot), recipe.Id);
        ReloadRecipes(null);
    }

    /// <summary>Erklärung des gewählten Rezepts ins Feld; aufgeklappt, wenn es eine hat. Das Füllen selbst zählt nicht als Änderung.</summary>
    private void FillDescription()
    {
        _fillingDescription = true;
        try
        {
            DescriptionBox.Text = Recipe?.Description ?? string.Empty;
            DescriptionExpander.IsExpanded = !string.IsNullOrWhiteSpace(Recipe?.Description);
            DescriptionExpander.IsEnabled = Recipe is not null;
        }
        finally
        {
            _fillingDescription = false;
        }
    }

    private void DescriptionBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_fillingDescription || _loading || Recipe is not { } recipe)
        {
            return;
        }

        // Leerer Text heißt: keine Erklärung (null, damit die Datei kein leeres Feld bekommt)
        recipe.Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text;
        MarkDescriptionEdited();
    }

    /// <summary>"Mit KI erstellen": höchstens ein KI-Fenster; ein offenes wird nach vorn geholt.</summary>
    private void AiRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_aiWindow is not null)
        {
            _aiWindow.Activate();
            return;
        }

        SaveNow();
        _aiWindow = new AiTaskWindow(this, RecipeAiFlavor.Host(this, _host), new RecipeAiFlavor(this, _host));
        _aiWindow.Closed += (_, _) => _aiWindow = null;
        _aiWindow.Show();
    }

    private void Guide_Click(object sender, RoutedEventArgs e) => ShowGuide();

    /// <summary>F1 öffnet die Anleitung, auch während ein Eingabefeld den Fokus hat.</summary>
    private void Guide_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            ShowGuide();
            e.Handled = true;
        }
    }

    /// <summary>Höchstens ein Anleitungsfenster (auch zusammen mit dem KI-Fenster); ein offenes wird nach vorn geholt.</summary>
    private void ShowGuide() => PatchRecipeHelpWindow.Show(this, null);

    private void Window_Closing(object? sender, CancelEventArgs e) => SaveNow();
}
