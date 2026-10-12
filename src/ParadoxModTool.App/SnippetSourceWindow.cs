using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ParadoxModTool.Generators.Ai;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>
/// Codefenster zu "Aus Mod-Datei übernehmen" / "Aus Spieldatei übernehmen": zeigt eine Datei schreibgeschützt; die Markierung wird auf ganze Zeilen
/// erweitert und als Codeblock übernommen. Bleibt offen, damit mehrere Stellen nacheinander übernommen werden können.
/// </summary>
internal sealed class SnippetSourceWindow : Window
{
    private readonly TextEditor _editor;
    private readonly TextBox _search = new() { MinWidth = 220, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Func<string, bool, int> _take;

    /// <param name="take">Legt den Block an (Code, ganze Datei) und gibt seine Nummer im Reiter "Codeblöcke" zurück.</param>
    public SnippetSourceWindow(Window owner, string relativePath, bool fromGame, string text, Func<string, bool, int> take)
    {
        Owner = owner;
        _take = take;
        Title = AiPackageTexts.SourceTitle(relativePath);
        Width = 900;
        Height = 720;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _editor = new TextEditor
        {
            Text = text,
            IsReadOnly = true,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            ShowLineNumbers = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(1),
        };
        _editor.SetResourceReference(BackgroundProperty, "Theme.EditorBackground");
        _editor.SetResourceReference(ForegroundProperty, "Theme.EditorForeground");
        _editor.SetResourceReference(BorderBrushProperty, "Theme.ControlBorder");
        _editor.SetResourceReference(TextEditor.LineNumbersForegroundProperty, "Theme.LineNumbers");
        _editor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty, "Theme.EditorSelection");

        var intro = new TextBlock { Text = fromGame ? AiPackageTexts.SourceIntroGame : AiPackageTexts.SourceIntro, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        intro.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        searchRow.Children.Add(new TextBlock { Text = AiPackageTexts.SourceSearch, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _search.ToolTip = AiPackageTexts.SourceSearchTip;
        searchRow.Children.Add(_search);
        var next = Button(AiPackageTexts.SourceFindNext, AiPackageTexts.SourceSearchTip, FindNext);
        next.Margin = new Thickness(6, 0, 0, 0);
        searchRow.Children.Add(next);
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                FindNext();
                e.Handled = true;
            }
        };

        var top = new StackPanel();
        top.Children.Add(intro);
        top.Children.Add(searchRow);

        var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(Button(AiPackageTexts.SourceTakeSelection, AiPackageTexts.SourceTakeSelectionTip, TakeSelection));
        buttons.Children.Add(Button(AiPackageTexts.SourceTakeWhole, AiPackageTexts.SourceTakeWholeTip, () => Report(_take(_editor.Text, true))));
        buttons.Children.Add(Button(AiPackageTexts.SourceClose, AiPackageTexts.SourceCloseTip, Close));
        var bottom = new StackPanel();
        bottom.Children.Add(_status);
        bottom.Children.Add(buttons);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(_editor);
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _search.Focus();
                _search.SelectAll();
                e.Handled = true;
            }
        };
        Loaded += (_, _) => _editor.Focus();
    }

    private void TakeSelection()
    {
        var code = AiTaskBuilder.WholeLines(_editor.Text, _editor.SelectionStart, _editor.SelectionLength);
        if (code.Trim().Length == 0)
        {
            _status.Text = AiPackageTexts.SourceNoSelection;
            _status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            return;
        }

        Report(_take(code, false));
    }

    private void Report(int number)
    {
        _status.Text = AiPackageTexts.SourceTaken(number);
        _status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
    }

    /// <summary>Sucht ab der Markierung weiter (Groß-/Kleinschreibung egal), am Ende wieder von vorn.</summary>
    private void FindNext()
    {
        var term = _search.Text;
        if (term.Length == 0)
        {
            return;
        }

        var text = _editor.Text;
        var from = Math.Min(text.Length, _editor.SelectionLength > 0 ? _editor.SelectionStart + 1 : _editor.CaretOffset);
        var index = text.IndexOf(term, from, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        }

        if (index < 0)
        {
            _status.Text = AiPackageTexts.SourceNotFound;
            _status.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Warning");
            return;
        }

        _editor.Select(index, term.Length);
        var location = _editor.Document.GetLocation(index);
        _editor.ScrollTo(location.Line, location.Column);
        _status.Text = string.Empty;
    }

    private static Button Button(string text, string tip, Action click)
    {
        var button = new Button { Content = new TextBlock { Text = text }, ToolTip = tip, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 6, 4) };
        button.Click += (_, _) => click();
        return button;
    }
}
