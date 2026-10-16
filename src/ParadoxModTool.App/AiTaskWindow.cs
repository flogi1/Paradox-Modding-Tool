using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ParadoxModTool.Patches.Ai;
using ParadoxModTool.Generators.Editing.Package;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Clipboard = System.Windows.Clipboard;
using FontFamily = System.Windows.Media.FontFamily;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TabControl = System.Windows.Controls.TabControl;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// KI-Fenster für alle Arten (Generator-Paket, Patch-Rezept): Reiter "Auftrag" stellt Anleitung, Vorbild-Dateien und Beschreibung für eine KI zusammen;
/// Reiter "Ergebnis" übernimmt die Antwort (Einfügen oder Ordner). Was davon artspezifisch ist, liefert <see cref="IAiTaskFlavor"/>.
/// </summary>
internal sealed partial class AiTaskWindow : Window
{
    private readonly AiTaskHost _host;
    private readonly IAiTaskFlavor _flavor;
    private readonly HashSet<string> _chosen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _chosenGame = new(StringComparer.Ordinal);
    private readonly TextBlock _modName = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _fileSearch = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly ListBox _files = new() { MinHeight = 160 };
    private readonly ListBox _chosenList = new() { MinHeight = 50, MaxHeight = 150 };
    private readonly Dictionary<string, CheckBox> _boxes = new(StringComparer.Ordinal);
    private readonly TextBlock _size = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    private readonly TextBox _description = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly CheckBox _withOpen;
    private readonly TextBox _answer = new()
    {
        AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"),
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 220,
    };
    private readonly ListBox _messages = new() { MinHeight = 90, MaxHeight = 200 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4), Visibility = Visibility.Collapsed };
    private readonly Button _copyFeedback;
    private readonly List<string> _feedback = new();
    private Button? _applyButton;
    private Button? _importButton;
    private string? _modRoot;
    private IReadOnlyList<(string Path, long Bytes)> _modFiles = Array.Empty<(string, long)>();
    private IReadOnlyList<(string Path, long Bytes)>? _gameFileSizes;
    private Dictionary<string, long>? _gameSizeLookup;
    private bool _showGame;
    private long? _chosenBytes;
    private RadioButton? _modSource;
    private RadioButton? _gameSource;

    public AiTaskWindow(Window owner, AiTaskHost host, IAiTaskFlavor flavor)
    {
        Owner = owner;
        _host = host;
        _flavor = flavor;
        Title = flavor.Title;
        Width = 900;
        Height = 860;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _answer.ToolTip = flavor.AnswerTip;
        _withOpen = new CheckBox { Content = new TextBlock { Text = flavor.WithOpenText }, ToolTip = flavor.WithOpenTip, Margin = new Thickness(0, 6, 0, 6) };
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        _copyFeedback = Button(AiPackageTexts.CopyFeedback, AiPackageTexts.CopyFeedbackTip, () =>
        {
            Clipboard.SetText(_flavor.Feedback(_feedback));
            Status(AiPackageTexts.FeedbackCopied);
        });
        _copyFeedback.IsEnabled = false;

        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = new TextBlock { Text = AiPackageTexts.TaskTab }, Content = TaskTab() });
        tabs.Items.Add(new TabItem { Header = new TextBlock { Text = AiPackageTexts.ResultTab }, Content = ResultTab() });
        var help = Button(AiPackageTexts.Help, AiPackageTexts.HelpTip, ShowHelp);
        help.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(help, Dock.Top);
        root.Children.Add(help);
        root.Children.Add(tabs);
        Content = root;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
            {
                ShowHelp();
                e.Handled = true;
            }
        };

        _fileSearch.TextChanged += (_, _) => FillFiles();

        // Was "Offenes ... mitgeben" erlaubt, kann sich ändern, während das Fenster offen ist (z. B. anderes Rezept gewählt)
        Activated += (_, _) =>
        {
            UpdateWithOpen();
            UpdateGameSource();
        };
        SetMod(host.ActiveModRoot());
    }

    private void UpdateWithOpen()
    {
        _withOpen.IsEnabled = _flavor.CanHandOver(_modRoot);
        if (!_withOpen.IsEnabled)
        {
            _withOpen.IsChecked = false;
        }
    }

    private void ShowHelp() => _flavor.ShowHelp(this);

    // ---- Reiter "Auftrag" ----
    private FrameworkElement TaskTab()
    {
        var panel = new DockPanel { Margin = new Thickness(6) };
        var top = new StackPanel();
        top.Children.Add(Note(_flavor.TaskIntro));
        var modRow = new DockPanel { Margin = new Thickness(0, 6, 0, 6), ToolTip = _flavor.ModTip };
        var choose = Button(AiPackageTexts.ChooseMod, _flavor.ModTip, ChooseMod);
        DockPanel.SetDock(choose, Dock.Right);
        modRow.Children.Add(choose);
        modRow.Children.Add(Labeled(AiPackageTexts.Mod, _modName));
        top.Children.Add(modRow);
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);

        var bottom = new StackPanel();
        bottom.Children.Add(_size);
        bottom.Children.Add(new TextBlock { Text = AiPackageTexts.Description, FontWeight = FontWeights.SemiBold, ToolTip = _flavor.DescriptionTip, Margin = new Thickness(0, 4, 0, 2) });
        _description.ToolTip = _flavor.DescriptionTip;
        bottom.Children.Add(_description);
        bottom.Children.Add(_withOpen);
        var buttons = new WrapPanel();
        buttons.Children.Add(Button(AiPackageTexts.CopyText, _flavor.CopyTextTip, CopyText));
        buttons.Children.Add(Button(AiPackageTexts.SaveFile, AiPackageTexts.SaveFileTip, SaveFile));
        buttons.Children.Add(Button(AiPackageTexts.SaveFolder, _flavor.SaveFolderTip, SaveFolder));
        bottom.Children.Add(buttons);
        DockPanel.SetDock(bottom, Dock.Bottom);
        panel.Children.Add(bottom);

        // Vorbild als ganze Dateien aus der Mod oder als eingefügte Codeblöcke; beides zusammen geht auch.
        var sources = new TabControl { Margin = new Thickness(0, 0, 0, 4) };
        sources.Items.Add(new TabItem { Header = new TextBlock { Text = AiPackageTexts.Files, ToolTip = _flavor.FilesTip }, Content = FilesPanel() });
        sources.Items.Add(new TabItem { Header = _snippetsHeader, Content = SnippetsPanel() });
        panel.Children.Add(sources);
        return panel;
    }

    private FrameworkElement FilesPanel()
    {
        var panel = new DockPanel { Margin = new Thickness(4) };
        if (_flavor.OffersGameFiles)
        {
            var switchRow = SourceSwitch();
            DockPanel.SetDock(switchRow, Dock.Top);
            panel.Children.Add(switchRow);
        }

        DockPanel.SetDock(_fileSearch, Dock.Top);
        panel.Children.Add(_fileSearch);

        // Angehakte Dateien getrennt, damit man sie ohne Suchen wieder abwählt
        var chosen = new StackPanel();
        var chosenHead = new DockPanel { Margin = new Thickness(0, 4, 0, 2) };
        var clear = Button(AiPackageTexts.ClearChosen, AiPackageTexts.ClearChosenTip, ClearChosen);
        DockPanel.SetDock(clear, Dock.Right);
        chosenHead.Children.Add(clear);
        chosenHead.Children.Add(new TextBlock { Text = AiPackageTexts.Chosen, FontWeight = FontWeights.SemiBold, ToolTip = AiPackageTexts.ChosenTip, VerticalAlignment = VerticalAlignment.Center });
        chosen.Children.Add(chosenHead);
        _chosenList.ToolTip = AiPackageTexts.ChosenTip;
        chosen.Children.Add(_chosenList);
        DockPanel.SetDock(chosen, Dock.Bottom);
        panel.Children.Add(chosen);

        _files.ToolTip = _flavor.FilesTip;
        panel.Children.Add(_files);
        return panel;
    }

    /// <summary>Umschalter "Mod-Dateien" / "Spieldateien"; die Spieldateien werden beim ersten Umschalten einmal gelesen.</summary>
    private FrameworkElement SourceSwitch()
    {
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        var mod = new RadioButton { Content = new TextBlock { Text = AiPackageTexts.FilesFromMod }, ToolTip = AiPackageTexts.FilesFromModTip, GroupName = "aiTaskSource", IsChecked = true, Margin = new Thickness(0, 0, 12, 0) };
        var game = new RadioButton { Content = new TextBlock { Text = AiPackageTexts.FilesFromGame }, ToolTip = AiPackageTexts.FilesFromGameTip, GroupName = "aiTaskSource", Margin = new Thickness(0, 0, 12, 0) };
        mod.Checked += (_, _) =>
        {
            _showGame = false;
            FillFiles();
        };
        game.Checked += (_, _) =>
        {
            if (GameRoot() is not { } root)
            {
                Status(_flavor.NoGameFolder);
                mod.IsChecked = true;
                return;
            }

            LoadGameFileSizes(root);
            _showGame = true;
            FillFiles();
            ReportDroppedGameFiles();
        };
        _modSource = mod;
        _gameSource = game;
        ToolTipService.SetShowOnDisabled(game, true);
        UpdateGameSource();

        row.Children.Add(mod);
        row.Children.Add(game);
        return row;
    }

    /// <summary>
    /// Ein Spielordner, der nie nachfragt, darf jederzeit gelesen werden: ohne ihn den Umschalter "Spieldateien" sperren und im Tooltip sagen,
    /// warum; wird er später gesetzt (das Fenster wird wieder aktiv), gleich freigeben. Fragt er nach, bleibt der Umschalter frei.
    /// </summary>
    private void UpdateGameSource()
    {
        if (_gameSource is null || _modSource is null || _host.GameRootMayAsk)
        {
            return;
        }

        var hasRoot = GameRoot() is not null;
        _gameSource.IsEnabled = hasRoot;
        _gameSource.ToolTip = hasRoot ? AiPackageTexts.FilesFromGameTip : _flavor.NoGameFolder;
        if (!hasRoot && _showGame)
        {
            _modSource.IsChecked = true;
        }
    }

    /// <summary>Größen der Spieldateien, einmal je Spielordner; die Pfade selbst kommen aus derselben Liste wie bei den Codeblöcken.</summary>
    private void LoadGameFileSizes(string root)
    {
        var paths = GameFiles(root);
        if (_gameFileSizes is not null)
        {
            return;
        }

        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            _gameFileSizes = paths.Select(p => (p, new FileInfo(Path.Combine(root, p.Replace('/', Path.DirectorySeparatorChar))).Length)).ToList();
            _gameSizeLookup = _gameFileSizes.ToDictionary(f => f.Path, f => f.Bytes, StringComparer.Ordinal);
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
        }
    }

    private void ChooseMod()
    {
        var mods = _host.KnownMods().ToList();
        var active = _host.ActiveModRoot();
        var ordered = mods.OrderByDescending(m => string.Equals(m.Path, active, StringComparison.OrdinalIgnoreCase)).ToList();
        var labels = ordered.Select(m => string.Equals(m.Path, active, StringComparison.OrdinalIgnoreCase) ? AiPackageTexts.ActiveMod(m.Name) : m.Name).ToList();
        if (PickListDialog.Ask(this, AiPackageTexts.ChooseMod, _flavor.ChooseModMessage, labels, multi: false) is [var index])
        {
            SetMod(ordered[index].Path);
        }
    }

    private void SetMod(string? root)
    {
        _modRoot = root is not null && Directory.Exists(root) ? root : null;
        _chosen.Clear();
        _chosenBytes = null;
        _modName.Text = _modRoot is null ? AiPackageTexts.NoMod : _host.KnownMods().FirstOrDefault(m => string.Equals(m.Path, _modRoot, StringComparison.OrdinalIgnoreCase)).Name ?? _modRoot;
        _modFiles = _modRoot is null
            ? Array.Empty<(string, long)>()
            : PatternExamples.Files(_modRoot).Select(p => (p, new FileInfo(Path.Combine(_modRoot, p.Replace('/', Path.DirectorySeparatorChar))).Length)).ToList();

        // "Offenes ... mitgeben" kann von der Mod abhängen; nicht möglich heißt auch nicht angehakt
        UpdateWithOpen();
        FillFiles();
    }

    private void FillFiles()
    {
        var filter = _fileSearch.Text.Trim();
        _files.Items.Clear();
        _boxes.Clear();
        var game = _showGame;
        var source = game ? _gameFileSizes ?? Array.Empty<(string, long)>() : _modFiles;
        var chosen = game ? _chosenGame : _chosen;
        foreach (var (path, bytes) in source.Where(f => filter.Length == 0 || f.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(2000))
        {
            var box = new CheckBox { Content = new TextBlock { Text = AiPackageTexts.FileLine(path, bytes) }, IsChecked = chosen.Contains(path) };
            box.Click += (_, _) => SetChosen(path, game, box.IsChecked == true);
            _boxes[path] = box;
            _files.Items.Add(new ListBoxItem { Content = box });
        }

        FillChosen();
    }

    private void SetChosen(string path, bool game, bool chosen)
    {
        var set = game ? _chosenGame : _chosen;
        if (chosen)
        {
            set.Add(path);
        }
        else
        {
            set.Remove(path);
        }

        _chosenBytes = null;

        // Häkchen in der großen Liste mitziehen, ohne sie neu aufzubauen (Scrollstand bleibt)
        if (game == _showGame && _boxes.TryGetValue(path, out var box))
        {
            box.IsChecked = chosen;
        }

        FillChosen();
    }

    private void ClearChosen()
    {
        foreach (var path in (_showGame ? _chosenGame : _chosen).ToList())
        {
            if (_boxes.TryGetValue(path, out var box))
            {
                box.IsChecked = false;
            }
        }

        _chosen.Clear();
        _chosenGame.Clear();
        _chosenBytes = null;
        FillChosen();
    }

    private void FillChosen()
    {
        _chosenList.Items.Clear();
        var sizes = _modFiles.ToDictionary(f => f.Path, f => f.Bytes, StringComparer.Ordinal);
        foreach (var path in _chosen.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            // Herkunft nur zeigen, wenn es auch Spieldateien geben kann
            var line = AiPackageTexts.FileLine(path, sizes.GetValueOrDefault(path));
            AddChosenLine(_flavor.OffersGameFiles ? AiPackageTexts.ChosenModPrefix + line : line, path, game: false);
        }

        foreach (var path in _chosenGame.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            AddChosenLine(AiPackageTexts.ChosenGamePrefix + AiPackageTexts.FileLine(path, _gameSizeLookup?.GetValueOrDefault(path) ?? 0), path, game: true);
        }

        if (_chosen.Count == 0 && _chosenGame.Count == 0)
        {
            var none = new TextBlock { Text = AiPackageTexts.ChosenNone, FontStyle = FontStyles.Italic };
            none.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            _chosenList.Items.Add(new ListBoxItem { Content = none, IsEnabled = false });
        }

        UpdateSize();
    }

    private void AddChosenLine(string text, string path, bool game)
    {
        var box = new CheckBox { Content = new TextBlock { Text = text }, IsChecked = true };
        box.Click += (_, _) => SetChosen(path, game, false);
        _chosenList.Items.Add(new ListBoxItem { Content = box });
    }

    private void UpdateSize()
    {
        var snippets = Snippets();
        // Dateigröße nur neu lesen, wenn sich die Auswahl geändert hat; Tippen in einem Codeblock liest sonst bei jedem Zeichen alle Dateien.
        _chosenBytes ??= (_modRoot is null ? 0 : AiTaskFiles.TotalBytes(AiTaskFiles.ReadFiles(_modRoot, _chosen.ToList())))
            + (_chosenGame.Count == 0 || _gameFilesRoot is null ? 0 : AiTaskFiles.TotalBytes(AiTaskFiles.ReadFiles(_gameFilesRoot, _chosenGame.ToList())));
        var bytes = _chosenBytes.Value + AiTaskFiles.SnippetBytes(snippets);
        var files = _chosen.Count + _chosenGame.Count;
        var summary = snippets.Count == 0 ? AiPackageTexts.Size(files, bytes) : AiPackageTexts.SizeWithSnippets(files, snippets.Count, bytes);
        _size.Text = summary + (bytes > AiTaskFiles.WarnTotalBytes ? "\n" + AiPackageTexts.SizeWarning : string.Empty);
        _size.SetResourceReference(TextBlock.ForegroundProperty, bytes > AiTaskFiles.WarnTotalBytes ? "Theme.Warning" : "Theme.TextMuted");
    }

    /// <summary>Null, wenn angehakte Spieldateien weggefallen sind: das Fenster hat es gemeldet, der Auftrag geht so nicht hinaus.</summary>
    private AiTaskInput? Input()
    {
        // Mit Spieldateien braucht der Auftrag den echten Spielordner, auch ohne angehakte Spieldatei. Hat er sich seit dem Lesen der Liste geändert,
        // liest GameFiles die Liste neu und wählt angehakte Spieldateien ab, die der neue Ordner nicht hat, damit nie alte Pfade mit dem neuen
        // Ordner zusammen in den Auftrag gehen. Ohne gelesene Liste wird nichts gelesen (das dauert einige Sekunden), nur der Ordner genannt.
        var gameRoot = _gameFilesRoot;
        if (_flavor.OffersGameFiles && GameRoot() is { } current && !string.Equals(current, _gameFilesRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (_gameFilesRoot is not null)
            {
                GameFiles(current);
            }

            gameRoot = current;
        }

        if (ReportDroppedGameFiles())
        {
            return null;
        }

        return new AiTaskInput(
            _description.Text,
            _modRoot,
            gameRoot,
            _chosen.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            _chosenGame.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            Snippets(),
            _withOpen.IsChecked == true);
    }

    private void CopyText()
    {
        if (Input() is not { } input)
        {
            return;
        }

        Clipboard.SetText(_flavor.BuildText(input));
        MessageBox.Show(this, AiPackageTexts.Copied, _flavor.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveFile()
    {
        if (Input() is not { } input)
        {
            return;
        }

        var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md", FileName = _flavor.SaveFileName };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, _flavor.BuildText(input), new UTF8Encoding(false));
        MessageBox.Show(this, AiPackageTexts.Saved(dialog.FileName), _flavor.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveFolder()
    {
        if (Input() is not { } input)
        {
            return;
        }

        var dialog = new OpenFolderDialog { Title = AiPackageTexts.SaveFolder };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(dialog.FolderName).Any())
        {
            MessageBox.Show(this, AiPackageTexts.FolderNotEmpty, _flavor.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _flavor.BuildFolder(input, dialog.FolderName);
        MessageBox.Show(this, AiPackageTexts.Saved(dialog.FolderName), _flavor.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---- Reiter "Ergebnis" ----
    private FrameworkElement ResultTab()
    {
        var panel = new DockPanel { Margin = new Thickness(6) };
        var top = new StackPanel();
        top.Children.Add(Note(_flavor.ResultIntro));
        top.Children.Add(new TextBlock { Text = AiPackageTexts.Answer, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) });
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);

        var bottom = new StackPanel();
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
        _applyButton = Button(AiPackageTexts.Apply, _flavor.ApplyTip, ApplyAnswer);
        _importButton = Button(AiPackageTexts.ImportFolder, _flavor.ImportFolderTip, ImportFolder);
        buttons.Children.Add(_applyButton);
        buttons.Children.Add(_importButton);
        bottom.Children.Add(buttons);
        bottom.Children.Add(new TextBlock { Text = AiPackageTexts.Messages, FontWeight = FontWeights.SemiBold });
        bottom.Children.Add(_messages);
        bottom.Children.Add(_status);
        bottom.Children.Add(_copyFeedback);
        DockPanel.SetDock(bottom, Dock.Bottom);
        panel.Children.Add(bottom);
        panel.Children.Add(_answer);
        return panel;
    }

    private async void ApplyAnswer() => await RunImportAsync(() => _flavor.ApplyAnswerAsync(this, _answer.Text, _modRoot, _withOpen.IsChecked == true));

    private async void ImportFolder()
    {
        var dialog = new OpenFolderDialog { Title = AiPackageTexts.ImportFolder };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await RunImportAsync(() => _flavor.ImportFolderAsync(this, dialog.FolderName, _modRoot, _withOpen.IsChecked == true));
    }

    /// <summary>
    /// Übernehmen oder Importieren: beide Knöpfe sind gesperrt, solange die Art arbeitet (kein zweiter Import per Doppelklick). Läuft aus
    /// async-void-Klicks, deshalb wird ein unerwarteter Fehler zur roten Meldung, statt PMT zu beenden.
    /// </summary>
    private async Task RunImportAsync(Func<Task<AiTaskOutcome>> work)
    {
        SetImportEnabled(false);
        try
        {
            Show(await work());
        }
        catch (Exception ex)
        {
            Show(new AiTaskOutcome(false, new[] { ex.Message }, string.Empty) { Problem = true });
        }
        finally
        {
            SetImportEnabled(true);
        }
    }

    private void SetImportEnabled(bool enabled)
    {
        if (_applyButton is not null && _importButton is not null)
        {
            _applyButton.IsEnabled = _importButton.IsEnabled = enabled;
        }
    }

    private void Show(AiTaskOutcome outcome)
    {
        _messages.Items.Clear();
        foreach (var line in outcome.Messages)
        {
            var text = new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap };
            if (outcome.Problem)
            {
                text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
            }

            _messages.Items.Add(new ListBoxItem { Content = text });
        }

        _feedback.Clear();
        _feedback.AddRange(outcome.Feedback ?? (outcome.Problem ? outcome.Messages : Array.Empty<string>()));
        _copyFeedback.IsEnabled = _feedback.Count > 0;
        _status.Text = outcome.Status;
        _status.Visibility = outcome.Status.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Status(string text) => MessageBox.Show(this, text, _flavor.Title, MessageBoxButton.OK, MessageBoxImage.Information);

    // ---- Bausteine ----
    private static TextBlock Note(string text)
    {
        var note = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        return note;
    }

    private static FrameworkElement Labeled(string label, FrameworkElement input)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = label, Width = 120, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(input);
        return row;
    }

    private static Button Button(string text, string tip, Action click)
    {
        var button = new Button { Content = new TextBlock { Text = text }, ToolTip = tip, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 4) };
        button.Click += (_, _) => click();
        return button;
    }
}
