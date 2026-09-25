using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace ParadoxModTool.App;

/// <summary>Auswahl der Changelog-Versionen, die als Release erfasst werden sollen (alle sind vorab angehakt).</summary>
public sealed class ChangelogImportWindow : Window
{
    private readonly List<CheckBox> _boxes = new();

    private ChangelogImportWindow(IReadOnlyList<string> versions)
    {
        Title = "Aus Changelog übernehmen";
        Width = 380;
        Height = 480;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var list = new StackPanel();
        foreach (var version in versions)
        {
            var box = new CheckBox { Content = version, IsChecked = true, Margin = new Thickness(0, 2, 0, 2) };
            _boxes.Add(box);
            list.Children.Add(box);
        }

        var all = new Button { Content = "Alle", Width = 70, Margin = new Thickness(0, 0, 6, 0) };
        all.Click += (_, _) => _boxes.ForEach(b => b.IsChecked = true);
        var none = new Button { Content = "Keine", Width = 70 };
        none.Click += (_, _) => _boxes.ForEach(b => b.IsChecked = false);
        var ok = new Button { Content = "OK", Width = 90, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "Abbrechen", Width = 90, IsCancel = true };

        var left = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        left.Children.Add(all);
        left.Children.Add(none);
        var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        right.Children.Add(ok);
        right.Children.Add(cancel);
        var bottom = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        bottom.Children.Add(left);
        bottom.Children.Add(right);

        var header = new TextBlock
        {
            Text = "Diese Versionen stehen im Changelog, sind aber noch nicht als Release erfasst. Das Datum bleibt leer und lässt sich danach ändern.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var dock = new DockPanel { Margin = new Thickness(14) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(header);
        dock.Children.Add(bottom);
        dock.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list });
        Content = dock;
    }

    /// <summary>Gibt die angehakten Versionen zurück; <see langword="null"/> bei Abbruch.</summary>
    public static IReadOnlyList<string>? Pick(Window owner, IReadOnlyList<string> versions)
    {
        var window = new ChangelogImportWindow(versions) { Owner = owner };
        return window.ShowDialog() == true
            ? window._boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Content).ToList()
            : null;
    }
}
