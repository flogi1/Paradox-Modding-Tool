using System.Diagnostics;
using System.IO;
using ParadoxModTool.Editor.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.Modifiers;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Parameterart <c>modifier</c>: durchsuchbare Auswahl aus der Modifier-Datenbank, auch mit Tausenden Namen in mehreren Tabellenzeilen.</summary>
public sealed class GuiBlockModifierTests
{
    [Fact]
    public void BlockWindow_WithModifierRows_KeepsItsSize()
    {
        Sta.Run(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "pmt-blockmods-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var wiki = Enumerable.Range(0, 2500).Select(i => ($"modifier_{i:D4}", "Additive")).ToList();
            ModifierValueSettings.SetIndex(ModifierFormatIndex.Build(new ModifierFormatSettings { Source = "wiki" }, Array.Empty<string>(), Array.Empty<string>(), wiki));
            try
            {
                File.WriteAllText(
                    Path.Combine(directory, "Mods.guiblock"),
                    "# block: Mods\n# param: top | Oben | x | modifier\n# repeat: mods | M | 3\n# param: mods.key | Modifier | modifier_0001 | modifier\n" +
                    "$top$\n$repeat mods$\n$mods.key$\n$end mods$\n");
                var watch = Stopwatch.StartNew();
                var window = new GuiBlockWindow(new GuiBlockLibrary(null, directory, null), null, null, new GuiBlockWindowOptions("Mods", null, "Einfügen", true));

                var fields = new List<FreeTextChoice>();
                window.Loaded += (_, _) => fields.AddRange(WpfWindows.Descendants<FreeTextChoice>(window));
                WpfWindows.AssertKeepsItsSizeWhenChoicesAreUsed(window);

                Assert.True(watch.ElapsedMilliseconds < 5000, $"{watch.ElapsedMilliseconds} ms");
                Assert.Equal(4, fields.Count);
                Assert.All(fields, f => Assert.Equal(2500, f.Suggestions.Choice.Items.Count));
                // Die Größenprüfung wählt in jeder Auswahl den letzten Eintrag: die Wahl landet im Wert.
                Assert.All(fields, f => Assert.Equal("modifier_2499", f.Value));
            }
            finally
            {
                ModifierValueSettings.SetIndex(ModifierFormatIndex.Empty);
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    [Fact]
    public void ModifierField_WithoutDatabase_IsAPlainTextField()
    {
        Sta.Run(() =>
        {
            var block = GuiBlockParser.Parse("# block: M\n# param: key | Modifier | x | modifier\n$key$\n", GuiBlockLayer.Game, @"C:\x\M.guiblock");
            var values = GuiBlockValues.Defaults(block);

            var inputs = new GuiBlockInputs(block, values, block.Parameters, block.Repeats);
            var field = Assert.Single(inputs.Children.OfType<System.Windows.Controls.Grid>().SelectMany(g => g.Children.OfType<FreeTextChoice>()));
            field.ValueBox.Text = "frei";

            Assert.Equal("frei", values.Values["key"]);
            Assert.Empty(field.Suggestions.Choice.Items);
        });
    }
}
