using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ParadoxModTool.Generators.Ai;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Localization;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Clipboard = System.Windows.Clipboard;
using FontFamily = System.Windows.Media.FontFamily;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TabControl = System.Windows.Controls.TabControl;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// "Paket mit KI erstellen" (Spec 2026-10-05-generator-packages-ai-guide, Abschnitt 2): Reiter "Auftrag" stellt Anleitung, Vorbild-Dateien und Beschreibung
/// für eine KI zusammen; Reiter "Ergebnis" übernimmt die Antwort (Einfügen oder Ordner) über den Import des Paket-Editors.
/// </summary>
internal sealed partial class AiPackageWindow : Window
{
    private static readonly string GuideFolder = Path.Combine(AppContext.BaseDirectory, "ai-guide", "generator-packages");
    private static readonly string CliPath = Path.Combine(AppContext.BaseDirectory, "pmt-generator.exe");

    private readonly PackageEditorHost _host;
    private readonly string? _packageFolder;
    private readonly Func<string, Action<string>, string?> _import;
    private readonly HashSet<string> _chosen = new(StringComparer.Ordinal);
    private readonly TextBlock _modName = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _fileSearch = new() { Margin = new Thickness(0, 0, 0, 4) };
    private readonly ListBox _files = new() { MinHeight = 160 };
    private readonly ListBox _chosenList = new() { MinHeight = 50, MaxHeight = 150 };
    private readonly Dictionary<string, CheckBox> _boxes = new(StringComparer.Ordinal);
    private readonly TextBlock _size = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
    private readonly TextBox _description = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly CheckBox _withPackage = new() { Content = new TextBlock { Text = AiPackageTexts.WithPackage }, ToolTip = AiPackageTexts.WithPackageTip, Margin = new Thickness(0, 6, 0, 6) };
    private readonly TextBox _answer = new()
    {
        AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"),
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 220, ToolTip = AiPackageTexts.AnswerTip,
    };
    private readonly ListBox _messages = new() { MinHeight = 90, MaxHeight = 200 };
    private readonly Button _copyFeedback;
    private readonly List<string> _feedback = new();
    private string? _modRoot;
    private IReadOnlyList<(string Path, long Bytes)> _modFiles = Array.Empty<(string, long)>();
    private long? _chosenBytes;

    public AiPackageWindow(Window owner, PackageEditorHost host, string? packageFolder, Func<string, Action<string>, string?> importArchive)
    {
        Owner = owner;
        _host = host;
        _packageFolder = packageFolder;
        _import = importArchive;
        Title = AiPackageTexts.Title;
        Width = 900;
        Height = 860;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _copyFeedback = Button(AiPackageTexts.CopyFeedback, AiPackageTexts.CopyFeedbackTip, () =>
        {
            Clipboard.SetText(AiTaskBuilder.Feedback(_feedback));
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

        _withPackage.IsEnabled = packageFolder is not null;
        _fileSearch.TextChanged += (_, _) => FillFiles();
        SetMod(host.ActiveModRoot());
    }

    private void ShowHelp() => GeneratorPackageHelpWindow.Show(this, "generatorhelp.ai");

    // ---- Reiter "Auftrag" ----
    private FrameworkElement TaskTab()
    {
        var panel = new DockPanel { Margin = new Thickness(6) };
        var top = new StackPanel();
        top.Children.Add(Note(AiPackageTexts.TaskIntro));
        var modRow = new DockPanel { Margin = new Thickness(0, 6, 0, 6), ToolTip = AiPackageTexts.ModTip };
        var choose = Button(AiPackageTexts.ChooseMod, AiPackageTexts.ModTip, ChooseMod);
        DockPanel.SetDock(choose, Dock.Right);
        modRow.Children.Add(choose);
        modRow.Children.Add(Labeled(AiPackageTexts.Mod, _modName));
        top.Children.Add(modRow);
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);

        var bottom = new StackPanel();
        bottom.Children.Add(_size);
        bottom.Children.Add(new TextBlock { Text = AiPackageTexts.Description, FontWeight = FontWeights.SemiBold, ToolTip = AiPackageTexts.DescriptionTip, Margin = new Thickness(0, 4, 0, 2) });
        _description.ToolTip = AiPackageTexts.DescriptionTip;
        bottom.Children.Add(_description);
        bottom.Children.Add(_withPackage);
        var buttons = new WrapPanel();
        buttons.Children.Add(Button(AiPackageTexts.CopyText, AiPackageTexts.CopyTextTip, CopyText));
        buttons.Children.Add(Button(AiPackageTexts.SaveFile, AiPackageTexts.SaveFileTip, SaveFile));
        buttons.Children.Add(Button(AiPackageTexts.SaveFolder, AiPackageTexts.SaveFolderTip, SaveFolder));
        bottom.Children.Add(buttons);
        DockPanel.SetDock(bottom, Dock.Bottom);
        panel.Children.Add(bottom);

        // Vorbild als ganze Dateien aus der Mod oder als eingefügte Codeblöcke; beides zusammen geht auch.
        var sources = new TabControl { Margin = new Thickness(0, 0, 0, 4) };
        sources.Items.Add(new TabItem { Header = new TextBlock { Text = AiPackageTexts.Files, ToolTip = AiPackageTexts.FilesTip }, Content = FilesPanel() });
        sources.Items.Add(new TabItem { Header = _snippetsHeader, Content = SnippetsPanel() });
        panel.Children.Add(sources);
        return panel;
    }

    private FrameworkElement FilesPanel()
    {
        var panel = new DockPanel { Margin = new Thickness(4) };
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

        _files.ToolTip = AiPackageTexts.FilesTip;
        panel.Children.Add(_files);
        return panel;
    }

    private void ChooseMod()
    {
        var mods = _host.KnownMods().ToList();
        var active = _host.ActiveModRoot();
        var ordered = mods.OrderByDescending(m => string.Equals(m.Path, active, StringComparison.OrdinalIgnoreCase)).ToList();
        var labels = ordered.Select(m => string.Equals(m.Path, active, StringComparison.OrdinalIgnoreCase) ? AiPackageTexts.ActiveMod(m.Name) : m.Name).ToList();
        if (PickListDialog.Ask(this, AiPackageTexts.ChooseMod, AiPackageTexts.ChooseModMessage, labels, multi: false) is [var index])
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
        FillFiles();
    }

    private void FillFiles()
    {
        var filter = _fileSearch.Text.Trim();
        _files.Items.Clear();
        _boxes.Clear();
        foreach (var (path, bytes) in _modFiles.Where(f => filter.Length == 0 || f.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)).Take(2000))
        {
            var box = new CheckBox { Content = new TextBlock { Text = AiPackageTexts.FileLine(path, bytes) }, IsChecked = _chosen.Contains(path) };
            box.Click += (_, _) => SetChosen(path, box.IsChecked == true);
            _boxes[path] = box;
            _files.Items.Add(new ListBoxItem { Content = box });
        }

        FillChosen();
    }

    private void SetChosen(string path, bool chosen)
    {
        if (chosen)
        {
            _chosen.Add(path);
        }
        else
        {
            _chosen.Remove(path);
        }

        _chosenBytes = null;

        // Häkchen in der großen Liste mitziehen, ohne sie neu aufzubauen (Scrollstand bleibt)
        if (_boxes.TryGetValue(path, out var box))
        {
            box.IsChecked = chosen;
        }

        FillChosen();
    }

    private void ClearChosen()
    {
        foreach (var path in _chosen.ToList())
        {
            if (_boxes.TryGetValue(path, out var box))
            {
                box.IsChecked = false;
            }
        }

        _chosen.Clear();
        _chosenBytes = null;
        FillChosen();
    }

    private void FillChosen()
    {
        _chosenList.Items.Clear();
        var sizes = _modFiles.ToDictionary(f => f.Path, f => f.Bytes, StringComparer.Ordinal);
        foreach (var path in _chosen.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var box = new CheckBox { Content = new TextBlock { Text = AiPackageTexts.FileLine(path, sizes.GetValueOrDefault(path)) }, IsChecked = true };
            box.Click += (_, _) => SetChosen(path, false);
            _chosenList.Items.Add(new ListBoxItem { Content = box });
        }

        if (_chosen.Count == 0)
        {
            var none = new TextBlock { Text = AiPackageTexts.ChosenNone, FontStyle = FontStyles.Italic };
            none.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
            _chosenList.Items.Add(new ListBoxItem { Content = none, IsEnabled = false });
        }

        UpdateSize();
    }

    private void UpdateSize()
    {
        var snippets = Snippets();
        // Dateigröße nur neu lesen, wenn sich die Auswahl geändert hat; Tippen in einem Codeblock liest sonst bei jedem Zeichen alle Dateien.
        _chosenBytes ??= _modRoot is null ? 0 : AiTaskBuilder.TotalBytes(AiTaskBuilder.ReadFiles(_modRoot, _chosen.ToList()));
        var bytes = _chosenBytes.Value + AiTaskBuilder.SnippetBytes(snippets);
        var summary = snippets.Count == 0 ? AiPackageTexts.Size(_chosen.Count, bytes) : AiPackageTexts.SizeWithSnippets(_chosen.Count, snippets.Count, bytes);
        _size.Text = summary + (bytes > AiTaskBuilder.WarnTotalBytes ? "\n" + AiPackageTexts.SizeWarning : string.Empty);
        _size.SetResourceReference(TextBlock.ForegroundProperty, bytes > AiTaskBuilder.WarnTotalBytes ? "Theme.Warning" : "Theme.TextMuted");
    }

    private AiTaskRequest Request(bool forFolder) => new(
        _description.Text,
        _host.GameId(),
        _modRoot,
        _chosen.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
        _withPackage.IsChecked == true ? _packageFolder : null,
        forFolder && File.Exists(CliPath) ? CliPath : null)
    {
        Snippets = Snippets(),
    };

    private void CopyText()
    {
        Clipboard.SetText(AiTaskBuilder.BuildText(Request(forFolder: false), GuideFolder));
        MessageBox.Show(this, AiPackageTexts.Copied, AiPackageTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveFile()
    {
        var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md", FileName = "pmt-package-task.md" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, AiTaskBuilder.BuildText(Request(forFolder: false), GuideFolder), new UTF8Encoding(false));
        MessageBox.Show(this, AiPackageTexts.Saved(dialog.FileName), AiPackageTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SaveFolder()
    {
        var dialog = new OpenFolderDialog { Title = AiPackageTexts.SaveFolder };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(dialog.FolderName).Any())
        {
            MessageBox.Show(this, AiPackageTexts.FolderNotEmpty, AiPackageTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AiTaskBuilder.BuildFolder(Request(forFolder: true), GuideFolder, dialog.FolderName);
        MessageBox.Show(this, AiPackageTexts.Saved(dialog.FolderName), AiPackageTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---- Reiter "Ergebnis" ----
    private FrameworkElement ResultTab()
    {
        var panel = new DockPanel { Margin = new Thickness(6) };
        var top = new StackPanel();
        top.Children.Add(Note(AiPackageTexts.ResultIntro));
        top.Children.Add(new TextBlock { Text = AiPackageTexts.Answer, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 2) });
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);

        var bottom = new StackPanel();
        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
        buttons.Children.Add(Button(AiPackageTexts.Apply, AiPackageTexts.ApplyTip, ApplyAnswer));
        buttons.Children.Add(Button(AiPackageTexts.ImportFolder, AiPackageTexts.ImportFolderTip, ImportFolder));
        bottom.Children.Add(buttons);
        bottom.Children.Add(new TextBlock { Text = AiPackageTexts.Messages, FontWeight = FontWeights.SemiBold });
        bottom.Children.Add(_messages);
        bottom.Children.Add(_copyFeedback);
        DockPanel.SetDock(bottom, Dock.Bottom);
        panel.Children.Add(bottom);
        panel.Children.Add(_answer);
        return panel;
    }

    private void ApplyAnswer()
    {
        var parsed = AiResponseParser.Parse(_answer.Text);
        if (parsed.Problems.Count > 0)
        {
            Show(parsed.Problems, problem: true);
            return;
        }

        Install(parsed.Files);
    }

    private void ImportFolder()
    {
        var dialog = new OpenFolderDialog { Title = AiPackageTexts.ImportFolder };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            Install(PackageFileSet.FromFolder(dialog.FolderName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Generators.GeneratorPackageException)
        {
            Show(new[] { ex.Message }, problem: true);
        }
    }

    /// <summary>Packt die Dateien als .pmtgen und schickt sie durch den Import des Paket-Editors; danach "Prüfen" auf dem neuen Paket.</summary>
    private void Install(IReadOnlyDictionary<string, string> files)
    {
        var archive = Path.Combine(Path.GetTempPath(), "pmt-ai-" + Path.GetRandomFileName() + GeneratorPackageArchive.Extension);
        var errors = new List<string>();
        try
        {
            PackageFileSet.WriteArchive(PackageFileSet.WithGameId(files, _host.GameId()), archive);
            if (_import(archive, errors.Add) is not { } folder)
            {
                Show(errors.Count > 0 ? errors : new List<string> { AiPackageTexts.NotImported }, problem: errors.Count > 0);
                return;
            }

            var issues = PackageChecker.Check(PackageDocument.Load(folder), new PackageFiles(folder), UiText.Current.Language)
                .Select(i => i.Line is { } line ? $"{i.Where}:{line}: {i.Message}" : $"{i.Where}: {i.Message}")
                .ToList();
            var lines = new List<string> { AiPackageTexts.Imported(Path.GetFileName(folder)) };
            lines.AddRange(issues.Count == 0 ? new[] { AiPackageTexts.CheckOk } : issues);
            Show(lines, problem: issues.Count > 0, feedback: issues);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Generators.GeneratorPackageException)
        {
            Show(new[] { ex.Message }, problem: true);
        }
        finally
        {
            File.Delete(archive);
        }
    }

    private void Show(IReadOnlyList<string> lines, bool problem, IReadOnlyList<string>? feedback = null)
    {
        _messages.Items.Clear();
        foreach (var line in lines)
        {
            var text = new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap };
            if (problem)
            {
                text.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");
            }

            _messages.Items.Add(new ListBoxItem { Content = text });
        }

        _feedback.Clear();
        _feedback.AddRange(feedback ?? (problem ? lines : Array.Empty<string>()));
        _copyFeedback.IsEnabled = _feedback.Count > 0;
    }

    private void Status(string text) => MessageBox.Show(this, text, AiPackageTexts.Title, MessageBoxButton.OK, MessageBoxImage.Information);

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
