using System.Windows;
using System.Windows.Controls.Primitives;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>
/// "Element wählen…" im GUI-Inspektor und ganze Fenster einer Datei: ein unbenanntes oberstes Fenster aus der Suche zeigt der Inspektor mit
/// Baum und Vorschau (Florian 2026-10-20, gui/window_county_view.gui).
/// </summary>
public sealed class GuiInspectorPickTests
{
    private const string CountyView =
        "window = {\n\tsize = { 200 100 }\n\thbox = {\n\t\tbutton = { name = \"ok\" size = { 20 20 } }\n\t}\n}\n";

    [Fact]
    public void Unnamed_top_level_window_from_the_search_opens_with_tree_and_preview()
    {
        Sta.Run(() =>
        {
            // Der Inspektor setzt seine Ergebnisse über den Synchronisationskontext des Fensters zurück.
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
            var file = new GuiSourceFile(@"C:\test\Vanilla\gui\window_county_view.gui", "gui/window_county_view.gui", GuiLayer.Vanilla, "Vanilla", 0, 0);
            var index = GuiIndex.Build(new[] { file }, _ => CountyView);
            var entry = Assert.Single(
                GuiElementSearch.Entries(index, new GuiResolver(index, GuiRules.Empty)),
                e => GuiElementSearch.Matches(e, string.Empty, GuiElementFilter.FileRoot, "window_county_view"));
            Assert.True(entry.IsUnnamed);
            Assert.Equal("window_county_view.gui", entry.Name);

            var inspector = new GuiInspectorWindow { Left = -3000, Top = -3000, ShowActivated = false };
            inspector.Show();
            try
            {
                inspector.Inspect(GuiInspection.TargetFor(index, entry.Definition));
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (inspector.PreviewScene is null)
                {
                    Assert.True(DateTime.UtcNow < deadline, "Inspektor hat keine Vorschau geladen");
                    WpfWindows.Pump();
                    Thread.Sleep(10);
                }

                Assert.Contains(inspector.TreeElements, e => ReferenceEquals(e.Origin.Node, entry.Definition.Node));
                Assert.Contains(inspector.TreeElements, e => e.Name == "ok");
            }
            finally
            {
                inspector.Close();
            }
        });
    }

    [Fact]
    public void Pick_button_asks_for_the_search()
    {
        Sta.Run(() =>
        {
            var inspector = new GuiInspectorWindow();
            var asked = 0;
            inspector.PickElementRequested += (_, _) => asked++;

            inspector.PickButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal(1, asked);
            inspector.Close();
        });
    }
}
