using System.Windows;
using ParadoxModTool.Localization;
using Controls = System.Windows.Controls;

namespace ParadoxModTool.App;

/// <summary>
/// Zeigt einen Text nur zum Lesen (markieren und kopieren geht), z. B. den gespeicherten KI-Rat eines Laufs. "Schließen" ist die Abbrechen-Schaltfläche,
/// Esc schließt also ohne eigene Tastenbehandlung.
/// </summary>
internal sealed class TextViewWindow : Window
{
    private TextViewWindow(string title, string text)
    {
        Title = title;
        Width = 820;
        Height = 680;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var box = new Controls.TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = Controls.ScrollBarVisibility.Auto,
            Padding = new Thickness(4),
        };
        var close = new Controls.Button
        {
            Content = CommonTexts.Close,
            MinWidth = 100,
            Padding = new Thickness(10, 3, 10, 3),
            IsCancel = true,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        close.Click += (_, _) => Close();

        var panel = new Controls.DockPanel { Margin = new Thickness(12) };
        Controls.DockPanel.SetDock(close, Controls.Dock.Bottom);
        panel.Children.Add(close);
        panel.Children.Add(box);
        Content = panel;
        Loaded += (_, _) => box.Focus();
    }

    /// <summary>Öffnet das Fenster nicht modal über <paramref name="owner"/>.</summary>
    public static TextViewWindow Show(Window owner, string title, string text)
    {
        var window = new TextViewWindow(title, text) { Owner = owner };
        window.Show();
        return window;
    }

    /// <summary>Öffnet das Fenster modal über <paramref name="owner"/> (für Besitzer, die selbst modal sind) und kehrt nach dem Schließen zurück.</summary>
    public static void ShowDialog(Window owner, string title, string text) =>
        new TextViewWindow(title, text) { Owner = owner }.ShowDialog();
}
