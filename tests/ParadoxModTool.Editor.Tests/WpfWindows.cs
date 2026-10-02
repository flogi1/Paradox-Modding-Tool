using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Hilfen für Fenstertests: Theme laden, Nachrichten abarbeiten, Elemente finden und prüfen, dass Auswahlfelder ein Fenster nicht wachsen lassen.</summary>
internal static class WpfWindows
{
    public static ResourceDictionary LoadTheme()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "ParadoxModTool.App", "Themes", "Controls.xaml");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                return (ResourceDictionary)XamlReader.Load(stream);
            }
        }

        throw new InvalidOperationException("Themes/Controls.xaml nicht gefunden");
    }

    public static void Pump(int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
    }

    public static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var inner in Descendants<T>(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>Zeigt das Fenster unsichtbar außerhalb des Bildschirms mit Theme, klappt jedes Auswahlfeld auf und wählt den letzten Eintrag; Breite bleibt.</summary>
    public static void AssertKeepsItsSizeWhenChoicesAreUsed(Window window)
    {
        window.Left = -3000;
        window.Top = -3000;
        window.ShowActivated = false;
        window.Resources.MergedDictionaries.Add(LoadTheme());
        window.Show();
        try
        {
            Pump();
            var width = window.ActualWidth;
            foreach (var box in Descendants<ComboBox>(window).ToList())
            {
                box.IsDropDownOpen = true;
                Pump();
                box.IsDropDownOpen = false;
                if (box.Items.Count > 0)
                {
                    box.SelectedIndex = box.Items.Count - 1;
                }
            }

            Pump(10);
            Assert.InRange(window.ActualWidth, width - 1, width + 1);
            var widest = Descendants<FrameworkElement>(window).Max(e => e.ActualWidth);
            Assert.True(widest <= width + 1, $"Element {widest} breiter als Fenster {width}");
        }
        finally
        {
            window.Close();
        }
    }
}
