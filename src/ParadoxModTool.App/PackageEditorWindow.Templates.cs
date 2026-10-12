using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using ParadoxModTool.Generators.Editing.Package;
using FontFamily = System.Windows.Media.FontFamily;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Vorlagen des Pakets (Spec Teil 5a, Abschnitt 3): Dateiliste, AvalonEdit mit Scriban-Hervorhebung und die verfügbaren Namen. Texte bleiben bis
/// "Speichern" in <see cref="_pending"/>; Umbenennen und Löschen speichern das Paket sofort, damit Datei und Verweise zusammenpassen.
/// </summary>
public sealed partial class PackageEditorWindow
{
    private static readonly Regex FunctionName = new(@"func\s+([A-Za-z_]\w*)", RegexOptions.CultureInvariant);

    private string? _selectedTemplate;
    private int? _gotoLine;

    /// <summary>Vorlagennamen auf der Platte und noch ungespeicherte, sortiert (ohne <c>functions.sbn</c>).</summary>
    private IReadOnlyList<string> TemplateNames() =>
        (_files?.Templates ?? Array.Empty<string>()).Concat(_pending.Keys.Where(k => !string.Equals(k, FunctionsName, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Wählt Abschnitt Vorlagen, diese Vorlage und auf Wunsch eine Zeile (Sprung aus "Prüfen" oder "Öffnen").</summary>
    private void ShowTemplate(string name, int? line)
    {
        _selectedTemplate = name;
        _gotoLine = line;
        if (AllItems().FirstOrDefault(i => i.Tag is SectionNode { Kind: SectionKind.Templates }) is { } item)
        {
            if (item.IsSelected)
            {
                ShowMask();
            }
            else
            {
                item.IsSelected = true;
            }
        }
    }

    private string TemplateText(string name) =>
        _pending.TryGetValue(name, out var text) ? text
        : string.Equals(name, FunctionsName, StringComparison.OrdinalIgnoreCase) ? _files!.Functions
        : File.Exists(Path.Combine(_entry!.Folder, "templates", name)) ? _files!.Read(name) : string.Empty;

    private FrameworkElement TemplatesMask()
    {
        var names = new[] { FunctionsName }.Concat(TemplateNames()).ToList();
        if (!names.Contains(_selectedTemplate ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            _selectedTemplate = names.Count > 1 ? names[1] : names[0];
        }

        var list = new ListBox();
        foreach (var file in names)
        {
            list.Items.Add(new ListBoxItem { Content = new TextBlock { Text = file }, Tag = file, IsSelected = string.Equals(file, _selectedTemplate, StringComparison.OrdinalIgnoreCase) });
        }

        list.SelectionChanged += (_, _) =>
        {
            _selectedTemplate = (list.SelectedItem as ListBoxItem)?.Tag as string;
            ShowMask();
        };
        var buttons = ButtonRow(
            (PackageEditorTexts.New, PackageEditorTexts.NewTemplateTip, NewTemplate),
            (PackageEditorTexts.Rename, PackageEditorTexts.RenameTemplateTip, RenameTemplate),
            (PackageEditorTexts.Delete, PackageEditorTexts.DeleteTemplateTip, DeleteTemplate));
        buttons.IsEnabled = !ReadOnly;
        var left = new DockPanel { Width = 200, Margin = new Thickness(0, 0, 8, 0) };
        var header = Heading(PackageEditorTexts.Templates);
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(header);
        left.Children.Add(buttons);
        left.Children.Add(list);

        var name = _selectedTemplate!;
        var editor = new TextEditor
        {
            Text = TemplateText(name),
            SyntaxHighlighting = ScribanHighlighting.Create(this),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            ShowLineNumbers = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(1),
            ToolTip = PackageEditorTexts.TemplateEditorTip,
            IsReadOnly = ReadOnly,
        };
        editor.SetResourceReference(BackgroundProperty, "Theme.EditorBackground");
        editor.SetResourceReference(ForegroundProperty, "Theme.EditorForeground");
        editor.SetResourceReference(BorderBrushProperty, "Theme.ControlBorder");
        editor.SetResourceReference(TextEditor.LineNumbersForegroundProperty, "Theme.LineNumbers");
        editor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");
        editor.TextChanged += (_, _) =>
        {
            _pending[name] = editor.Text;
            UpdateState();
        };
        if (_gotoLine is { } line)
        {
            _gotoLine = null;
            editor.Loaded += (_, _) =>
            {
                var target = Math.Clamp(line, 1, editor.Document.LineCount);
                editor.TextArea.Caret.Line = target;
                editor.ScrollToLine(target);
                editor.TextArea.Focus();
            };
        }

        var namesPanel = NamesPanel(editor);
        var grid = new Grid { Height = Math.Max(420, ActualHeight - 230) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        Grid.SetColumn(left, 0);
        Grid.SetColumn(editor, 1);
        Grid.SetColumn(namesPanel, 2);
        grid.Children.Add(left);
        grid.Children.Add(editor);
        grid.Children.Add(namesPanel);

        var panel = MaskPanel(PackageEditorTexts.SectionTemplates + ": " + name);
        panel.Children.Add(grid);
        return panel;
    }

    /// <summary>Namen, die Vorlagen kennen; Doppelklick fügt sie an der Schreibmarke ein.</summary>
    private FrameworkElement NamesPanel(TextEditor editor)
    {
        var document = _document!;
        var list = new ListBox { ToolTip = PackageEditorTexts.NamesTip };
        void Group(string title, IEnumerable<(string Text, string Insert)> items)
        {
            list.Items.Add(new ListBoxItem { Content = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold }, IsEnabled = false });
            foreach (var (text, insert) in items)
            {
                list.Items.Add(new ListBoxItem { Content = new TextBlock { Text = text }, Tag = insert });
            }
        }

        Group(PackageEditorTexts.NamesHead, document.Head.Items.Select(f => ("  " + f.Id, f.Id)));
        var levels = new List<(string, string)>();
        foreach (var level in document.Levels)
        {
            levels.Add(("  " + level.Id, level.Id));
            levels.AddRange(level.Fields.Items.Select(f => ("    ." + f.Id, f.Id)));
            if (level.ConstantsTable is { } table)
            {
                levels.AddRange(document.ConstantRows(table).SelectMany(r => r.Keys).Distinct().Select(c => ("    ." + c, c)));
            }
        }

        Group(PackageEditorTexts.NamesLevels, levels);
        Group(PackageEditorTexts.NamesElement, new[] { "index", "index0", "first", "last", "number", "suffix", "parent" }.Select(n => ("  " + n, n)));
        Group(PackageEditorTexts.NamesGlobal, new[] { "item", "lang", "constants", "languages", "language_rules" }.Select(n => ("  " + n, n)));
        Group(PackageEditorTexts.NamesFunctions, FunctionName.Matches(TemplateText(FunctionsName)).Select(m => m.Groups[1].Value).Distinct().Select(n => ("  " + n, n)));
        list.MouseDoubleClick += (_, _) =>
        {
            if ((list.SelectedItem as ListBoxItem)?.Tag is string insert && !editor.IsReadOnly)
            {
                editor.Document.Insert(editor.CaretOffset, insert);
                editor.Focus();
            }
        };

        var panel = new DockPanel { Margin = new Thickness(8, 0, 0, 0) };
        var header = Heading(PackageEditorTexts.Names);
        header.ToolTip = PackageEditorTexts.NamesTip;
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(list);
        return panel;
    }

    private string? AskTemplateName(string initial)
    {
        var name = AskName(PackageEditorTexts.TemplateName, initial);
        if (name is null)
        {
            return null;
        }

        if (!name.EndsWith(".sbn", StringComparison.OrdinalIgnoreCase))
        {
            name += ".sbn";
        }

        if (!PackageFiles.IsValidTemplateName(name))
        {
            MessageBox.Show(this, PackageEditorTexts.TemplateNameInvalid, PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        if (TemplateNames().Contains(name, StringComparer.OrdinalIgnoreCase) || string.Equals(name, FunctionsName, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, PackageEditorTexts.TemplateExists, PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        return name;
    }

    private void NewTemplate()
    {
        if (AskTemplateName(string.Empty) is { } name)
        {
            _pending[name] = string.Empty;
            _selectedTemplate = name;
            UpdateState();
            ShowMask();
        }
    }

    /// <summary>Speichert vorher (Datei und Verweise sollen zusammen auf der Platte stehen); <see langword="false"/> bei Abbruch.</summary>
    private bool SaveBeforeFileChange()
    {
        if (!IsDirty)
        {
            return true;
        }

        return MessageBox.Show(this, PackageEditorTexts.SaveFirst, PackageEditorTexts.Title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK && SaveCurrent();
    }

    private void RenameTemplate()
    {
        if (_selectedTemplate is not { } old || string.Equals(old, FunctionsName, StringComparison.OrdinalIgnoreCase) || !SaveBeforeFileChange())
        {
            return;
        }

        if (AskTemplateName(old) is not { } name)
        {
            return;
        }

        try
        {
            _files!.Rename(old, name);
            var users = _document!.OutputsUsingTemplate(old);
            if (users.Count > 0 && MessageBox.Show(this, PackageEditorTexts.RenameReferencesAsk(string.Join(", ", users)), PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _document.RenameTemplateReferences(old, name);
                SaveCurrent();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, PackageEditorTexts.SaveFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _selectedTemplate = name;
        ShowMask();
    }

    private void DeleteTemplate()
    {
        if (_selectedTemplate is not { } name || string.Equals(name, FunctionsName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var users = _document!.OutputsUsingTemplate(name);
        var question = users.Count > 0 ? PackageEditorTexts.DeleteTemplateAsk(name, string.Join(", ", users)) : PackageEditorTexts.DeleteTemplateAskPlain(name);
        if (MessageBox.Show(this, question, PackageEditorTexts.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _pending.Remove(name);
        if (!SaveBeforeFileChange())
        {
            return;
        }

        try
        {
            if (_files!.Templates.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _files.Delete(name);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, PackageEditorTexts.DeleteFailed(ex.Message), PackageEditorTexts.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _selectedTemplate = null;
        UpdateState();
        ShowMask();
    }
}
