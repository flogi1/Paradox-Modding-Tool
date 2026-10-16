using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Generators.Ai;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Patches.Ai;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using RadioButton = System.Windows.Controls.RadioButton;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// Reiter "Codeblöcke": statt ganzer Dateien fügt der Modder nur die passenden Stellen ein, je mit Pfad, Umfang (ganze Datei oder Ausschnitt)
/// und optionaler Erläuterung. Blöcke lassen sich zuklappen und direkt aus einer Datei der Mod oder des Spiels übernehmen.
/// Das hält den Auftrag klein genug für kostenlose Chat-KIs. Die Blöcke leben nur, solange das Fenster offen ist.
/// </summary>
internal sealed partial class AiTaskWindow
{
    // Dateien, die als Text in Frage kommen; Bilder, Sounds und Modelle bleiben draußen
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".gui", ".yml", ".gfx", ".asset", ".csv", ".json", ".settings", ".lua",
    };

    private sealed record SnippetEditor(Border Frame, TextBox Path, RadioButton WholeFile, CheckBox FromGame, TextBox Note, TextBox Code, FrameworkElement Body, TextBlock Toggle, TextBlock Summary);

    private readonly List<SnippetEditor> _snippetEditors = new();
    private readonly StackPanel _snippetList = new();
    private readonly TextBlock _snippetsHeader = new() { Text = AiPackageTexts.Snippets, ToolTip = AiPackageTexts.SnippetsTip };
    private int _snippetGroups;
    private IReadOnlyList<string>? _gameFiles;
    private string? _gameFilesRoot;

    /// <summary>Angehakte Spieldateien, die es nach einem Wechsel des Spielordners dort nicht mehr gibt; <see cref="ReportDroppedGameFiles"/> meldet sie.</summary>
    private readonly List<string> _droppedGameFiles = new();

    private FrameworkElement SnippetsPanel()
    {
        var panel = new DockPanel { Margin = new Thickness(4) };
        var top = new StackPanel();
        top.Children.Add(Note(_flavor.SnippetsIntro));
        var buttons = new WrapPanel();
        buttons.Children.Add(Button(AiPackageTexts.AddSnippet, AiPackageTexts.AddSnippetTip, () => AddSnippet().Path.Focus()));
        buttons.Children.Add(Button(AiPackageTexts.SnippetFromMod, AiPackageTexts.SnippetFromModTip, () => TakeFromFile(fromGame: false)));
        buttons.Children.Add(Button(AiPackageTexts.SnippetFromGame, AiPackageTexts.SnippetFromGameTip, () => TakeFromFile(fromGame: true)));
        buttons.Children.Add(Button(AiPackageTexts.CollapseAll, AiPackageTexts.CollapseAllTip, () => _snippetEditors.ForEach(e => SetExpanded(e, false))));
        buttons.Children.Add(Button(AiPackageTexts.ExpandAll, AiPackageTexts.ExpandAllTip, () => _snippetEditors.ForEach(e => SetExpanded(e, true))));
        top.Children.Add(buttons);
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);
        panel.Children.Add(new ScrollViewer { Content = _snippetList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return panel;
    }

    private SnippetEditor AddSnippet()
    {
        var path = new TextBox { ToolTip = AiPackageTexts.SnippetPathTip, VerticalContentAlignment = VerticalAlignment.Center };
        var group = "snippet" + _snippetGroups++;
        var whole = new RadioButton { Content = new TextBlock { Text = AiPackageTexts.SnippetWholeFile }, ToolTip = _flavor.SnippetWholeFileTip, GroupName = group, Margin = new Thickness(0, 0, 12, 0) };
        var excerpt = new RadioButton { Content = new TextBlock { Text = AiPackageTexts.SnippetExcerpt }, ToolTip = _flavor.SnippetExcerptTip, GroupName = group, IsChecked = true, Margin = new Thickness(0, 0, 12, 0) };
        var fromGame = new CheckBox { Content = new TextBlock { Text = AiPackageTexts.SnippetFromGameFile }, ToolTip = AiPackageTexts.SnippetFromGameFileTip };
        var note = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 24, MaxHeight = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ToolTip = _flavor.SnippetNoteTip };
        var code = new TextBox
        {
            AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"), MinHeight = 90, MaxHeight = 260,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ToolTip = AiPackageTexts.SnippetCodeTip,
        };

        // Kopfzeile: Auf-/Zuklappen per Klick, daneben die Kurzinfo für den zugeklappten Block
        var toggle = new TextBlock { Text = "▾", Width = 16, VerticalAlignment = VerticalAlignment.Center };
        var summary = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        summary.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        var head = new DockPanel { Background = System.Windows.Media.Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = AiPackageTexts.SnippetToggleTip, Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(toggle, Dock.Left);
        head.Children.Add(toggle);
        head.Children.Add(summary);

        var body = new StackPanel();
        var stack = new StackPanel();
        stack.Children.Add(head);
        stack.Children.Add(body);
        var frame = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(6), Margin = new Thickness(0, 0, 0, 6), Child = stack };
        frame.SetResourceReference(Border.BorderBrushProperty, "Theme.ControlBorder");
        var editor = new SnippetEditor(frame, path, whole, fromGame, note, code, body, toggle, summary);
        head.MouseLeftButtonUp += (_, _) => SetExpanded(editor, body.Visibility != Visibility.Visible);

        var pathRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var label = new TextBlock { Text = AiPackageTexts.SnippetPath, Width = 120, VerticalAlignment = VerticalAlignment.Center, ToolTip = AiPackageTexts.SnippetPathTip };
        DockPanel.SetDock(label, Dock.Left);
        pathRow.Children.Add(label);
        var remove = Button(AiPackageTexts.RemoveSnippet, AiPackageTexts.RemoveSnippetTip, () => RemoveSnippet(editor));
        var pick = Button(AiPackageTexts.PickSnippetPath, AiPackageTexts.PickSnippetPathTip, () => PickSnippetPath(path));
        foreach (var button in new[] { remove, pick })
        {
            button.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(button, Dock.Right);
            pathRow.Children.Add(button);
        }

        pathRow.Children.Add(path);
        body.Children.Add(pathRow);

        var noteRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var noteLabel = new TextBlock { Text = AiPackageTexts.SnippetNote, Width = 120, ToolTip = _flavor.SnippetNoteTip, Margin = new Thickness(0, 3, 0, 0) };
        DockPanel.SetDock(noteLabel, Dock.Left);
        noteRow.Children.Add(noteLabel);
        noteRow.Children.Add(note);
        body.Children.Add(noteRow);

        var scope = new WrapPanel { Margin = new Thickness(120, 0, 0, 4) };
        scope.Children.Add(whole);
        scope.Children.Add(excerpt);
        scope.Children.Add(fromGame);
        body.Children.Add(scope);
        body.Children.Add(code);

        code.TextChanged += (_, _) =>
        {
            UpdateSize();
            UpdateSummary(editor);
        };
        note.TextChanged += (_, _) =>
        {
            UpdateSize();
            UpdateSummary(editor);
        };
        path.TextChanged += (_, _) => UpdateSummary(editor);
        whole.Checked += (_, _) => UpdateSummary(editor);
        excerpt.Checked += (_, _) => UpdateSummary(editor);
        fromGame.Click += (_, _) => UpdateSummary(editor);

        _snippetEditors.Add(editor);
        _snippetList.Children.Add(frame);
        UpdateSummary(editor);
        UpdateSnippetsHeader();
        return editor;
    }

    private static void SetExpanded(SnippetEditor editor, bool expanded)
    {
        editor.Body.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        editor.Toggle.Text = expanded ? "▾" : "▸";
    }

    private static void UpdateSummary(SnippetEditor editor)
    {
        var path = editor.Path.Text.Trim();
        var code = editor.Code.Text.TrimEnd('\r', '\n');
        var lines = code.Length == 0 ? 0 : code.Split('\n').Length;
        var text = AiPackageTexts.SnippetSummary(
            path.Length == 0 ? AiPackageTexts.SnippetNoPath : path,
            editor.WholeFile.IsChecked == true ? AiPackageTexts.SnippetWholeFile : AiPackageTexts.SnippetExcerptShort,
            lines);
        if (editor.FromGame.IsChecked == true)
        {
            text = AiPackageTexts.SnippetGamePrefix + text;
        }

        var note = editor.Note.Text.Trim().Replace("\r\n", " ").Replace('\n', ' ');
        editor.Summary.Text = note.Length == 0 ? text : text + " – " + note;
    }

    private void RemoveSnippet(SnippetEditor editor)
    {
        _snippetEditors.Remove(editor);
        _snippetList.Children.Remove(editor.Frame);
        UpdateSnippetsHeader();
        UpdateSize();
    }

    private void PickSnippetPath(TextBox path)
    {
        if (_modFiles.Count == 0)
        {
            Status(AiPackageTexts.NoMod);
            return;
        }

        var paths = _modFiles.Select(f => f.Path).ToList();
        if (PickListDialog.Ask(this, AiPackageTexts.PickSnippetPath, AiPackageTexts.PickSnippetPathMessage, paths, multi: false) is [var index])
        {
            path.Text = paths[index];
        }
    }

    /// <summary>Datei der Mod oder des Spiels wählen, im Codefenster markieren und als Block übernehmen; das Codefenster bleibt für weitere Stellen offen.</summary>
    private void TakeFromFile(bool fromGame)
    {
        var root = fromGame ? GameRoot() : _modRoot;
        if (root is null)
        {
            Status(fromGame ? _flavor.NoGameFolder : AiPackageTexts.NoMod);
            return;
        }

        var paths = fromGame ? GameFiles(root) : _modFiles.Select(f => f.Path).Where(p => TextExtensions.Contains(Path.GetExtension(p))).ToList();
        var message = fromGame ? AiPackageTexts.SnippetFromGameMessage : AiPackageTexts.SnippetFromModMessage;
        if (PickListDialog.Ask(this, fromGame ? AiPackageTexts.SnippetFromGame : AiPackageTexts.SnippetFromMod, message, paths, multi: false) is not [var index])
        {
            return;
        }

        var relative = paths[index];
        string text;
        try
        {
            text = File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status(ex.Message);
            return;
        }

        new SnippetSourceWindow(this, relative, fromGame, text, (code, wholeFile) =>
        {
            var editor = AddSnippet();
            editor.Path.Text = relative;
            editor.WholeFile.IsChecked = wholeFile;
            editor.FromGame.IsChecked = fromGame;
            editor.Code.Text = code;
            UpdateSummary(editor);
            editor.Frame.BringIntoView();
            return _snippetEditors.Count;
        }).Show();
    }

    /// <summary>Spielordner mit denselben relativen Pfaden wie eine Mod; fehlt er, darf der Nutzer die Spiel-Exe wählen.</summary>
    private string? GameRoot() => _host.GameRoot();

    private IReadOnlyList<string> GameFiles(string root)
    {
        if (_gameFiles is null || !string.Equals(_gameFilesRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            // Anderer Spielordner: Größen gehören zum alten
            _gameFileSizes = null;
            _gameSizeLookup = null;

            // Der erste Durchlauf über den ganzen Spielordner dauert einige Sekunden
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            try
            {
                _gameFiles = PatternExamples.Files(root).Where(p => TextExtensions.Contains(Path.GetExtension(p))).ToList();
                _gameFilesRoot = root;
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }

            // Angehakte Spieldateien bleiben, wenn der neue Ordner sie unter demselben Pfad hat; die übrigen fallen weg und werden gemeldet.
            if (_chosenGame.Count > 0)
            {
                var available = _gameFiles.ToHashSet(StringComparer.Ordinal);
                var dropped = _chosenGame.Where(p => !available.Contains(p)).ToList();
                if (dropped.Count > 0)
                {
                    _chosenGame.ExceptWith(dropped);
                    _droppedGameFiles.AddRange(dropped);
                }

                _chosenBytes = null;
                FillChosen();
            }

            // Zeigt die Dateiliste gerade Spieldateien, gehört sie zum alten Ordner: Größen neu lesen und die Liste neu füllen.
            // Erst hier, nachdem _gameFiles/_gameFilesRoot stehen, sonst liefe LoadGameFileSizes erneut in diesen Zweig.
            if (_showGame)
            {
                LoadGameFileSizes(root);
                FillFiles();
            }
        }

        return _gameFiles;
    }

    /// <summary>Meldet die Spieldateien, die <see cref="GameFiles"/> beim Wechsel des Spielordners abgewählt hat, einmal; true, wenn es welche gab.</summary>
    private bool ReportDroppedGameFiles()
    {
        if (_droppedGameFiles.Count == 0)
        {
            return false;
        }

        const int MaxListed = 20;
        var dropped = _droppedGameFiles.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        _droppedGameFiles.Clear();
        var list = string.Join("\n", dropped.Take(MaxListed)) + (dropped.Count > MaxListed ? "\n…" : string.Empty);
        System.Windows.MessageBox.Show(this, AiPackageTexts.GameFilesDropped(list), _flavor.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        return true;
    }

    private void UpdateSnippetsHeader() =>
        _snippetsHeader.Text = _snippetEditors.Count == 0 ? AiPackageTexts.Snippets : AiPackageTexts.SnippetsCount(_snippetEditors.Count);

    /// <summary>Die Blöcke mit Code; leere Blöcke zählen nicht.</summary>
    private IReadOnlyList<AiTaskSnippet> Snippets() => _snippetEditors
        .Where(e => e.Code.Text.Trim().Length > 0)
        .Select(e => new AiTaskSnippet(e.Path.Text.Trim(), e.WholeFile.IsChecked == true, e.Code.Text) { Note = e.Note.Text.Trim(), FromGame = e.FromGame.IsChecked == true })
        .ToList();
}
