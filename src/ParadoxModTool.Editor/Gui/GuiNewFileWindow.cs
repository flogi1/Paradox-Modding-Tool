using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Dialog "Neue GUI-Datei" (Plan 3a Ruling 10): Dateiname, Kontext (Plan 3b Ruling 2: die Datei entsteht in <c>&lt;Mod&gt;/&lt;Kontext&gt;/gui</c>),
/// Gerüst (leer oder Fenster), Zielordner und Hinweis auf das nötige Skript.
/// </summary>
public sealed class GuiNewFileWindow : Window
{
    private readonly string _modRoot;
    private readonly ComboBox _context = new() { ItemsSource = GuiContexts.All, SelectedIndex = 0, MinWidth = 160, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBox _name = new() { MinWidth = 280, Margin = new Thickness(0, 2, 0, 8) };
    private readonly RadioButton _empty = new() { Margin = new Thickness(0, 2, 0, 2) };
    private readonly RadioButton _window = new() { IsChecked = true, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };

    public GuiNewFileWindow(string modRoot)
    {
        _modRoot = modRoot;
        Title = GuiTexts.NewFileTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        _empty.Content = new TextBlock { Text = GuiTexts.NewFileEmpty };
        _window.Content = new TextBlock { Text = GuiTexts.NewFileWindow };
        _error.SetResourceReference(TextBlock.ForegroundProperty, "Theme.Danger");

        var target = new TextBlock { Text = GuiTexts.NewFileTarget(Folder), TextWrapping = TextWrapping.Wrap, MaxWidth = 460, Margin = new Thickness(0, 0, 0, 6) };
        _context.ToolTip = GuiTexts.NewFileContextTip;
        _context.SelectionChanged += (_, _) => target.Text = GuiTexts.NewFileTarget(Folder);
        target.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");
        var hint = new TextBlock { Text = GuiTexts.NewFileHint, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Theme.TextMuted");

        var create = new Button { Content = GuiTexts.NewFileCreate, IsDefault = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2) };
        create.Click += (_, _) =>
        {
            if (Validate())
            {
                DialogResult = true;
            }
        };
        var cancel = new Button { Content = GuiTexts.NewFileCancel, IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(create);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = GuiTexts.NewFileName });
        panel.Children.Add(_name);
        panel.Children.Add(new TextBlock { Text = GuiTexts.NewFileContext });
        panel.Children.Add(_context);
        panel.Children.Add(new TextBlock { Text = GuiTexts.NewFileScaffold });
        panel.Children.Add(_empty);
        panel.Children.Add(_window);
        panel.Children.Add(target);
        panel.Children.Add(hint);
        panel.Children.Add(_error);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _name.Focus();
    }

    /// <summary>Dateiname mit Endung .gui.</summary>
    public string FileName
    {
        get
        {
            var name = _name.Text.Trim();
            return name.EndsWith(".gui", StringComparison.OrdinalIgnoreCase) ? name : name + ".gui";
        }
    }

    public GuiScaffoldKind Scaffold => _window.IsChecked == true ? GuiScaffoldKind.Window : GuiScaffoldKind.Empty;

    /// <summary>Ordner der neuen Datei: <c>&lt;Mod&gt;/&lt;Kontext&gt;/gui</c>.</summary>
    public string Folder => GuiContexts.GuiFolder(_modRoot, _context.SelectedItem as string ?? GuiContexts.Default);

    private bool Validate()
    {
        var name = _name.Text.Trim();
        var stem = name.EndsWith(".gui", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        string? error = null;
        if (stem.Trim().Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            error = GuiTexts.NewFileInvalidName;
        }
        else if (File.Exists(Path.Combine(Folder, FileName)))
        {
            error = GuiTexts.NewFileExists(FileName);
        }

        _error.Text = error ?? string.Empty;
        _error.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        return error is null;
    }
}
