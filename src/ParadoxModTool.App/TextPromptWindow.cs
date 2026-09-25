using System.Windows;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace ParadoxModTool.App;

/// <summary>Kleines Eingabefenster für einen einzeiligen Text.</summary>
public sealed class TextPromptWindow : Window
{
    private readonly TextBox _box;

    private TextPromptWindow(string title, string message, string initial)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        _box = new TextBox { Text = initial, Margin = new Thickness(0, 0, 0, 12), VerticalContentAlignment = System.Windows.VerticalAlignment.Center };
        panel.Children.Add(_box);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "Abbrechen", Width = 80, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) =>
        {
            _box.Focus();
            _box.SelectAll();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
    }

    /// <summary>Fragt einen Text ab; <see langword="null"/> bei Abbruch.</summary>
    public static string? Ask(Window owner, string title, string message, string initial)
    {
        var window = new TextPromptWindow(title, message, initial) { Owner = owner };
        return window.ShowDialog() == true ? window._box.Text : null;
    }
}
