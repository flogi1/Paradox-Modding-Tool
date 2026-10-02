using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Blocks;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>"Neue GUI-Datei" und Fenster "Bausteine": Bausteinauswahl mit Suche; die Fenster behalten ihre Größe, auch wenn Auswahlfelder benutzt werden.</summary>
public sealed class GuiNewFileWindowTests
{
    private static IReadOnlyList<GuiBlock> Blocks()
    {
        var blocks = new List<GuiBlock>(new GuiBlockLibrary(null, null, null).Effective);
        blocks.Add(GuiBlockParser.Parse("# block: Fenster\n# group: Fenster\n# description: Ein Fenster\nwindow = { }\n", GuiBlockLayer.Shipped, @"C:\x\Fenster.guiblock"));
        blocks.Add(GuiBlockParser.Parse("# block: Knopf\n# group: Bedienelemente\n# description: Standard-Knopf\nbutton = { }\n", GuiBlockLayer.Game, @"C:\x\Knopf.guiblock"));
        blocks.Add(GuiBlockParser.Parse("# block: Kaputt\nx = $q$\n", GuiBlockLayer.Game, @"C:\x\Kaputt.guiblock"));
        return blocks;
    }

    private static ResourceDictionary LoadTheme()
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

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void Window_KeepsItsSize_WhenTheBlockChoiceIsUsed()
    {
        Sta.Run(() =>
        {
            var window = new GuiNewFileWindow(@"C:\mod", Blocks()) { Left = -3000, Top = -3000, ShowActivated = false };
            window.Resources.MergedDictionaries.Add(LoadTheme());
            window.Show();
            Pump();
            var width = window.ActualWidth;
            var height = window.ActualHeight;

            foreach (var box in Descendants<ComboBox>(window))
            {
                box.IsDropDownOpen = true;
                Pump();
                box.IsDropDownOpen = false;
            }

            foreach (var list in Descendants<ListBox>(window))
            {
                list.SelectedIndex = list.Items.Count - 1;
            }

            for (var i = 0; i < 10; i++)
            {
                Pump();
            }

            Assert.InRange(window.ActualWidth, width - 1, width + 1);
            Assert.InRange(window.ActualHeight, height - 1, height + 1);
            Assert.True(window.ActualWidth < 800, $"Breite {window.ActualWidth}");
            window.Close();
        });
    }

    [Fact]
    public void Search_FiltersTheBlocks_AndOffersOnlyValidOnes()
    {
        Sta.Run(() =>
        {
            var window = new GuiNewFileWindow(@"C:\mod", Blocks());

            Assert.Equal("Fenster", window.Block?.Name);
            Assert.Equal(new[] { "Leere Datei", "Fenster", "Knopf" }, window.OfferedBlocks.Select(b => b.Name));

            window.SearchText = "knopf";
            Assert.Equal(new[] { "Knopf" }, window.OfferedBlocks.Select(b => b.Name));
            Assert.Equal("Knopf", window.Block?.Name);

            window.SearchText = "Standard";
            Assert.Equal(new[] { "Knopf" }, window.OfferedBlocks.Select(b => b.Name));

            window.SearchText = "gibt es nicht";
            Assert.Empty(window.OfferedBlocks);
            Assert.Null(window.Block);

            window.SearchText = string.Empty;
            Assert.Equal(3, window.OfferedBlocks.Count);
        });
    }

    [Fact]
    public void BlockWindow_KeepsItsSize_WhenTheSubRepeatChoiceIsUsed()
    {
        Sta.Run(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "pmt-blockwindow-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "Tabs.guiblock"), string.Join("\n", "# block: Tabs", "# repeat: tabs | Tabs | 3", "# param: tabs.id | Kennung | tab_$index$",
                    "# repeat: tabs.subtabs | Untertabs | 2", "# param: tabs.subtabs.id | Kennung | $tabs.id$_sub_$index$", "$repeat tabs$", "$repeat tabs.subtabs$",
                    "$tabs.subtabs.id$", "$end tabs.subtabs$", "$end tabs$", string.Empty));
                var library = new GuiBlockLibrary(null, directory, null);
                Assert.True(library.Find("Tabs")!.IsValid);
                var window = new GuiBlockWindow(library, null, null, new GuiBlockWindowOptions("Tabs", null, "Einfügen", true))
                {
                    Left = -3000,
                    Top = -3000,
                    ShowActivated = false,
                };
                window.Resources.MergedDictionaries.Add(LoadTheme());
                window.Show();
                Pump();
                var width = window.ActualWidth;
                var boxes = Descendants<ComboBox>(window).ToList();
                Assert.NotEmpty(boxes);
                foreach (var box in boxes)
                {
                    box.IsDropDownOpen = true;
                    Pump();
                    box.IsDropDownOpen = false;
                    box.SelectedIndex = box.Items.Count - 1;
                }

                for (var i = 0; i < 10; i++)
                {
                    Pump();
                }

                Assert.InRange(window.ActualWidth, width - 1, width + 1);
                var widest = Descendants<FrameworkElement>(window).Max(e => e.ActualWidth);
                Assert.True(widest <= width + 1, $"Element {widest} breiter als Fenster {width}");
                window.Close();
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
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
}
