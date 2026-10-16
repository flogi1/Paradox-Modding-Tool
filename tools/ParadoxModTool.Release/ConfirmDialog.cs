using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ParadoxModTool.Release;

/// <summary>
/// Ja/Nein-Abfrage für lange Texte (die letzte Abfrage vor dem Veröffentlichen enthält den ganzen Changelog-Block): der Text scrollt, die Knöpfe
/// bleiben unten sichtbar, das Fenster ist höchstens so hoch wie der Arbeitsbereich und lässt sich vergrößern. Enter = Ja, Escape = Nein.
/// </summary>
public sealed class ConfirmDialog : Window
{
    private ConfirmDialog(string text, string title)
    {
        Title = title;
        Width = 760;
        Height = Math.Min(640, SystemParameters.WorkArea.Height - 40);
        MinWidth = 420;
        MinHeight = 240;
        MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var yes = new Button { Content = "Ja", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var no = new Button { Content = "Nein", Width = 90, IsCancel = true };
        yes.Click += (_, _) => DialogResult = true;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(yes);
        buttons.Children.Add(no);
        DockPanel.SetDock(buttons, Dock.Bottom);

        var body = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            BorderThickness = new Thickness(0),
            Background = SystemColors.WindowBrush,
        };

        var root = new DockPanel { Margin = new Thickness(12) };
        root.Children.Add(buttons);
        root.Children.Add(body);
        Content = root;

        // Fokus auf "Ja", damit Enter wie bei der MessageBox sofort bestätigt.
        Loaded += (_, _) => Keyboard.Focus(yes);
    }

    /// <summary>Zeigt <paramref name="text"/> modal über <paramref name="owner"/>; <see langword="true"/> nur bei "Ja".</summary>
    public static bool Ask(Window owner, string text, string title) =>
        new ConfirmDialog(text, title) { Owner = owner }.ShowDialog() == true;
}
