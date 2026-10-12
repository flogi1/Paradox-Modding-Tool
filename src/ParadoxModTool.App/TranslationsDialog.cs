using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Localization;
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

/// <summary>Übersetzungen eines Formularfelds: eine Zeile je gewählter Sprache (ohne Englisch); leere Felder fallen beim Speichern weg.</summary>
public sealed class TranslationsDialog : Window
{
    private readonly Dictionary<string, TextBox> _boxes = new(StringComparer.Ordinal);

    private TranslationsDialog(string title, IReadOnlyList<string> languages, IReadOnlyDictionary<string, string> current, bool multiline)
    {
        Title = title;
        Width = 640;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var rows = new StackPanel { Margin = new Thickness(12) };
        foreach (var language in languages)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var label = new TextBlock { Text = language, Width = 110, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            var box = new TextBox
            {
                Text = current.GetValueOrDefault(language) ?? string.Empty,
                AcceptsReturn = multiline,
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                MinHeight = multiline ? 38 : 0,
            };
            _boxes[language] = box;
            row.Children.Add(box);
            rows.Children.Add(row);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var ok = new Button { Content = new TextBlock { Text = CommonTexts.Ok }, Width = 80, IsDefault = !multiline, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = new TextBlock { Text = CommonTexts.Cancel }, Width = 80, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        rows.Children.Add(buttons);
        Content = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>Die bearbeiteten Übersetzungen; <see langword="null"/> bei Abbruch.</summary>
    public static Dictionary<string, string>? Ask(Window owner, string title, IReadOnlyList<string> languages, IReadOnlyDictionary<string, string> current, bool multiline)
    {
        var dialog = new TranslationsDialog(title, languages, current, multiline) { Owner = owner };
        return dialog.ShowDialog() == true
            ? dialog._boxes.Where(b => b.Value.Text.Trim().Length > 0).ToDictionary(b => b.Key, b => b.Value.Text, StringComparer.Ordinal)
            : null;
    }
}
