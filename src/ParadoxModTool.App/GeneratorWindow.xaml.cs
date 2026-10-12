using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ParadoxModTool.Generators;
using ParadoxModTool.Generators.Candidates;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ParadoxModTool.App;

/// <summary>
/// Was das Fenster vom Hauptfenster braucht: aktive Mod, Spiel, PMT-Datenordner des Spiels, Sprachen des Spielprofils und das Schreiben eines Plans
/// über den Aktionsverlauf (Prüfung auf ungespeicherte Dokumente, Rückgängig, Neuladen offener Tabs).
/// </summary>
public sealed record GeneratorWindowHost(
    Func<string?> ModRoot,
    Func<string> GameId,
    Func<string> GameDirectory,
    Func<IReadOnlyList<string>> ProfileLanguages,
    Func<IReadOnlyList<(string Name, string Path)>> KnownMods,
    Func<GeneratorPlan, string, FixOutcome> Apply,
    Func<IReadOnlyList<string>> GameRoots,
    Func<ParadoxModTool.Session.AppSessionState> Session,
    Func<bool> ChooseGameFolder,
    Action<string?, bool> OpenEditor,
    Action OpenAi,
    Func<string, IReadOnlyList<CandidateSource>> CandidateSources,
    Func<IReadOnlyList<string>, string, IReadOnlyDictionary<string, string>> ModifierNames);

/// <summary>Fenster "Generator-Pakete" (Spec 2026-10-03-generator-packages-core): Paket und Arbeitsstand wählen, Formular ausfüllen, Vorschau, Erzeugen.</summary>
public partial class GeneratorWindow : Window
{
    private readonly GeneratorWindowHost _host;
    private readonly DispatcherTimer _validateTimer;
    private GeneratorPackage? _package;
    private GeneratorRenderer? _renderer;
    private GeneratorState? _state;
    private bool _dirty;
    private bool _switching;

    public GeneratorWindow(GeneratorWindowHost host)
    {
        _host = host;
        InitializeComponent();
        PreviewKeyDown += Guide_PreviewKeyDown;
        _validateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _validateTimer.Tick += (_, _) =>
        {
            _validateTimer.Stop();
            Revalidate();
        };
        InitializePreview();
        Closed += (_, _) => CancelKeyIndexOnClose();
        LoadPackages();
    }

    private string PackagesRoot => GeneratorPackageLoader.PackagesRoot(_host.GameDirectory());

    private void LoadPackages()
    {
        _lookupCatalogs.Clear();
        PackageList.Items.Clear();
        foreach (var (folder, package, error, _) in GeneratorPackageLoader.ListAll(GeneratorPackageLoader.ShippedRoot, PackagesRoot, _host.GameId()))
        {
            PackageList.Items.Add(new ListBoxItem
            {
                Content = new TextBlock { Text = package?.Name ?? GeneratorTexts.BrokenPackage(Path.GetFileName(folder)) },
                Tag = package,
                IsEnabled = package is not null,
                ToolTip = error,
            });
        }

        if (PackageList.Items.Count == 0)
        {
            ShowMessage(GeneratorTexts.NoPackages(PackagesRoot));
            NewStateButton.IsEnabled = false;
            return;
        }

        PackageList.SelectedIndex = PackageList.Items.Cast<ListBoxItem>().ToList().FindIndex(i => i.IsEnabled);
    }

    /// <summary>
    /// Lädt die Paketliste neu (nach dem Speichern im Paket-Editor) und wählt das Paket mit <paramref name="selectId"/>; ein ungespeicherter
    /// Arbeitsstand wird wie beim Paketwechsel erfragt.
    /// </summary>
    public void ReloadPackages(string? selectId)
    {
        if (!ConfirmLeave())
        {
            return;
        }

        // Das Kandidatenfenster gehört zum alten Paket: nicht offen lassen, solange _package leer ist
        _candidateWindow?.Close();
        _switching = true;
        _package = null;
        LoadPackages();
        PackageList.SelectedIndex = -1;
        _switching = false;
        var items = PackageList.Items.Cast<ListBoxItem>().ToList();
        var index = items.FindIndex(i => i.Tag is GeneratorPackage p && p.Id == selectId);
        PackageList.SelectedIndex = index >= 0 ? index : items.FindIndex(i => i.IsEnabled);
    }

    private void EditPackage_Click(object sender, RoutedEventArgs e) => _host.OpenEditor(_package?.Id, false);

    private void Guide_Click(object sender, RoutedEventArgs e) => GeneratorPackageHelpWindow.Show(this, "generatorhelp.generator");

    /// <summary>F1 öffnet die Anleitung beim Kapitel zum Generator-Fenster, auch während ein Eingabefeld den Fokus hat.</summary>
    private void Guide_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F1 && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.None)
        {
            GeneratorPackageHelpWindow.Show(this, "generatorhelp.generator");
            e.Handled = true;
        }
    }

    private void NewPackage_Click(object sender, RoutedEventArgs e) => _host.OpenEditor(null, true);

    private void AiPackage_Click(object sender, RoutedEventArgs e) => _host.OpenAi();

    private void PackageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_switching || PackageList.SelectedItem is not ListBoxItem { Tag: GeneratorPackage package } || package == _package)
        {
            return;
        }

        if (!ConfirmLeave())
        {
            _switching = true;
            PackageList.SelectedItem = PackageList.Items.Cast<ListBoxItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, _package));
            _switching = false;
            return;
        }

        // Das Fenster "Modifier ohne Idee" gehört zu einem Paketobjekt (Gruppen, Ausgabe); bei jedem Paketwechsel oder Neuladen schließt es
        _candidateWindow?.Close();
        _package = package;
        _renderer = new GeneratorRenderer(package);
        WarmUpKeyIndex(package);
        var readable = package.Read is null ? Visibility.Collapsed : Visibility.Visible;
        ReadFromModButton.Visibility = readable;
        TakeOverButton.Visibility = readable;
        CheckModButton.Visibility = readable;
        CandidatesButton.Visibility = package.Candidates is null ? Visibility.Collapsed : Visibility.Visible;
        ShowState(null);
        RefreshStates();
    }

    private void RefreshStates(string? select = null)
    {
        _switching = true;
        StateList.Items.Clear();
        if (_package is not null)
        {
            var filter = StateSearch.Text.Trim();
            foreach (var key in GeneratorStateStore.Keys(_package.DataFolder).Where(k => filter.Length == 0 || k.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                StateList.Items.Add(new ListBoxItem { Content = new TextBlock { Text = key }, Tag = key });
            }
        }

        var current = select ?? _state?.Key;
        StateList.SelectedItem = StateList.Items.Cast<ListBoxItem>().FirstOrDefault(i => (string)i.Tag == current);
        _switching = false;
        UpdateDirtyMarker();
    }

    private void StateSearch_TextChanged(object sender, TextChangedEventArgs e) => RefreshStates();

    private void StateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_switching || _package is null || StateList.SelectedItem is not ListBoxItem { Tag: string key } || key == _state?.Key)
        {
            return;
        }

        if (!ConfirmLeave())
        {
            RefreshStates(_state?.Key);
            return;
        }

        ShowState(GeneratorStateStore.Load(_package.DataFolder, key) ?? NewState(key));
    }

    private void NewState_Click(object sender, RoutedEventArgs e)
    {
        if (_package is null || !ConfirmLeave())
        {
            return;
        }

        var key = (_package.KeyFields.Count > 1 || AutoNumber.FieldOf(_package) is not null
            ? KeyPromptWindow.Ask(this, _package, TakenKeysForPrompt(_host.ModRoot()))
            : TextPromptWindow.Ask(this, GeneratorTexts.Title, GeneratorTexts.NewStatePrompt, string.Empty))?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (GeneratorStateStore.Keys(_package.DataFolder).Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, GeneratorTexts.StateExists(key), GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ShowState(NewState(key));
        SaveState();
    }

    private GeneratorState NewState(string key)
    {
        var state = GeneratorState.CreateEmpty(_package!, _host.ProfileLanguages());
        state.Key = key;
        PackageKey.Apply(_package!, state, key);
        state.ModRoot = _host.ModRoot();
        return state;
    }

    /// <summary>Liest den aktuellen Schlüssel aus der Basis-Mod neu ein; Basis-Mod, Ziel und Ausgabeordner des Stands bleiben (Spec Teil 4).</summary>
    private void ReadFromMod_Click(object sender, RoutedEventArgs e)
    {
        if (_package is null || _state is null)
        {
            return;
        }

        if (_state.ModRoot is not { } modRoot || !Directory.Exists(modRoot))
        {
            MessageBox.Show(this, GeneratorTexts.NoMod, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!ConfirmLeave())
        {
            return;
        }

        try
        {
            if (GeneratorStateReader.Read(_package, modRoot, _state.Key, _host.ProfileLanguages(), ExternalPathStore.Load(_package.DataFolder)) is not { } result)
            {
                MessageBox.Show(this, GeneratorTexts.ReadNothing(_state.Key), GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            result.State.ModRoot = _state.ModRoot;
            result.State.Target = _state.Target;
            result.State.OutputFolder = _state.OutputFolder;
            ShowState(result.State);
            MarkDirty();
            ShowNotes(result.Notes);
        }
        catch (Exception ex) when (ex is GeneratorOutputException or GeneratorTemplateException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Tags einer anderen Mod als Arbeitsstände übernehmen; sie bekommen die gewählte Basis-Mod (sonst die aktive Mod).</summary>
    private void TakeOver_Click(object sender, RoutedEventArgs e)
    {
        if (_package is null || !ConfirmLeave())
        {
            return;
        }

        var existing = GeneratorStateStore.Keys(_package.DataFolder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (GeneratorTakeOverDialog.Ask(this, _package, _host.KnownMods(), existing) is not { } chosen)
        {
            return;
        }

        var replacing = chosen.Keys.Count(existing.Contains);
        if (replacing > 0 && MessageBox.Show(this, GeneratorTexts.TakeOverConfirmReplace(replacing), GeneratorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var result = GeneratorStateReader.TakeOver(_package, chosen.Source, chosen.Keys, _state?.ModRoot ?? _host.ModRoot(), _host.ProfileLanguages(),
                ExternalPathStore.Load(_package.DataFolder));
            foreach (var state in result.States)
            {
                GeneratorStateStore.Save(_package.DataFolder, state);
            }

            _dirty = false;
            RefreshStates(result.States.FirstOrDefault()?.Key);
            if (result.States.FirstOrDefault() is { } first)
            {
                ShowState(GeneratorStateStore.Load(_package.DataFolder, first.Key));
            }

            MessageBox.Show(this, string.Join(Environment.NewLine, new[] { GeneratorTexts.TakeOverDone(result.States.Count) }.Concat(result.Notes)),
                GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is GeneratorOutputException or GeneratorTemplateException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>"Mod prüfen": alle Schlüssel der Basis-Mod frisch lesen und ohne Schreiben planen; Ergebnis in einem eigenen Fenster.</summary>
    private void CheckMod_Click(object sender, RoutedEventArgs e)
    {
        if (_package is null)
        {
            return;
        }

        var modRoot = _state?.ModRoot ?? _host.ModRoot();
        if (modRoot is null || !Directory.Exists(modRoot))
        {
            MessageBox.Show(this, GeneratorTexts.NoMod, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IReadOnlyList<CheckRow> rows;
        var cursor = Cursor;
        Cursor = System.Windows.Input.Cursors.Wait;
        try
        {
            rows = GeneratorCheck.Run(_package, modRoot, _host.ProfileLanguages(), ExternalPathStore.Load(_package.DataFolder));
        }
        finally
        {
            Cursor = cursor;
        }

        new GeneratorCheckWindow(_package, Path.GetFileName(modRoot.TrimEnd('\\', '/')), rows, OpenCheckedState) { Owner = this }.Show();
    }

    /// <summary>Aus "Mod prüfen": gelesenen Stand als Arbeitsstand speichern (vorhandenen nur nach Nachfrage ersetzen), wählen und die Vorschau zeigen.</summary>
    private async void OpenCheckedState(GeneratorState state)
    {
        if (_package is null || !ConfirmLeave())
        {
            return;
        }

        if (GeneratorStateStore.Keys(_package.DataFolder).Contains(state.Key, StringComparer.OrdinalIgnoreCase)
            && MessageBox.Show(this, GeneratorTexts.CheckConfirmReplace(state.Key), GeneratorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        GeneratorStateStore.Save(_package.DataFolder, state);
        _dirty = false;
        ShowState(state);
        RefreshStates(state.Key);
        Activate();
        // Ein noch laufendes Berechnen (wartet auf den Index) ist durch ShowState veraltet; erst abwarten, sonst wäre der Aufruf wirkungslos
        if (_planRun is { IsCompleted: false } pending)
        {
            await pending;
            if (_closed || !ReferenceEquals(_state, state))
            {
                return;
            }
        }

        await ComputePlanAsync(showTab: true);
    }

    private void ShowNotes(IReadOnlyList<string> notes)
    {
        if (notes.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, notes), GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveState();

    /// <summary>Speichert den Arbeitsstand; der Schlüssel folgt dem Schlüssel-Feld des Pakets (ein umbenannter Tag wird als neuer Stand gespeichert).</summary>
    private bool SaveState()
    {
        if (_package is null || _state is null)
        {
            return false;
        }

        if (PackageKey.Compose(_package, _state.Head.Values) is { Length: > 0 } key)
        {
            _state.Key = key;
        }

        try
        {
            GeneratorStateStore.Save(_package.DataFolder, _state);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, GeneratorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        _dirty = false;
        RefreshStates(_state.Key);
        return true;
    }

    /// <summary>Vor einem Wechsel: ungespeicherte Änderungen speichern, verwerfen oder den Wechsel abbrechen.</summary>
    private bool ConfirmLeave()
    {
        if (!_dirty || _state is null)
        {
            return true;
        }

        switch (MessageBox.Show(this, GeneratorTexts.UnsavedAsk(_state.Key), GeneratorTexts.Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question))
        {
            case MessageBoxResult.Yes:
                return SaveState();
            case MessageBoxResult.No:
                _dirty = false;
                return true;
            default:
                return false;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmLeave())
        {
            e.Cancel = true;
        }
    }

    private void ShowState(GeneratorState? state)
    {
        _state = state;
        _dirty = false;
        ClearPlan();
        BuildForm();
        Revalidate();
        UpdateDirtyMarker();
    }

    /// <summary>Nach jeder Eingabe: Plan verwerfen, Liste markieren, Prüfung leicht verzögert.</summary>
    private void MarkDirty()
    {
        if (!_dirty)
        {
            _dirty = true;
            UpdateDirtyMarker();
        }

        ClearPlan();
        _validateTimer.Stop();
        _validateTimer.Start();
    }

    private void UpdateDirtyMarker()
    {
        foreach (var item in StateList.Items.Cast<ListBoxItem>())
        {
            ((TextBlock)item.Content).Text = (string)item.Tag + (_dirty && (string)item.Tag == _state?.Key ? " ●" : string.Empty);
        }
    }

    private IReadOnlyList<FormIssue> Issues()
    {
        if (_package is null || _state is null || _renderer is null)
        {
            return Array.Empty<FormIssue>();
        }

        try
        {
            return FormValidator.Check(_package, _state, _renderer);
        }
        catch (GeneratorTemplateException ex)
        {
            return new[] { new FormIssue(IssueSeverity.Error, ex.Message, null) };
        }
    }

    private void Revalidate()
    {
        IssuesPanel.Children.Clear();
        var hasState = _state is not null;
        SaveButton.IsEnabled = hasState;
        PreviewButton.IsEnabled = GenerateButton.IsEnabled = hasState && !_planRunning;
        if (!hasState)
        {
            return;
        }

        var issues = Issues();
        var errors = issues.Count(i => i.Severity == IssueSeverity.Error);
        var summary = new TextBlock { Text = issues.Count == 0 ? GeneratorTexts.NoIssues : GeneratorTexts.ErrorsAndWarnings(errors, issues.Count - errors) };
        summary.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        IssuesPanel.Children.Add(summary);
        foreach (var issue in issues.Take(4))
        {
            var line = new TextBlock { Text = (issue.Path is null ? string.Empty : issue.Path + ": ") + issue.Message, TextTrimming = TextTrimming.CharacterEllipsis };
            line.SetResourceReference(TextBlock.ForegroundProperty, issue.Severity == IssueSeverity.Error ? "Theme.Danger" : "Theme.Warning");
            IssuesPanel.Children.Add(line);
        }
    }

    /// <summary>Kurze Statuszeile unter der Prüfung (z. B. "Vorschau abgebrochen."); die nächste Prüfung nach einer Eingabe räumt sie weg.</summary>
    private readonly TextBlock _statusLine = MutedLine();

    private static TextBlock MutedLine()
    {
        var line = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis };
        line.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return line;
    }

    private void ShowStatus(string text)
    {
        // Eine Zeile, die ersetzt wird, statt bei jedem Abbruch eine weitere anzuhängen
        _statusLine.Text = text;
        if (!IssuesPanel.Children.Contains(_statusLine))
        {
            IssuesPanel.Children.Add(_statusLine);
        }
    }

    private void ShowMessage(string text)
    {
        FormPanel.Children.Clear();
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
        block.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        FormPanel.Children.Add(block);
    }

}
