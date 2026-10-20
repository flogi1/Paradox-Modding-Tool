using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Layout;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

public sealed class GuiSettingsHiddenConditionalsTests
{
    [Fact]
    public void Show_all_and_count_work_per_viewed_file()
    {
        // Eigene Pfade je Lauf, damit der statische Bestand anderer Tests unberührt bleibt.
        var folder = Path.Combine(Path.GetTempPath(), "pmt-hidden-" + Guid.NewGuid().ToString("N"));
        var fileA = Path.Combine(folder, "a.gui");
        var fileB = Path.Combine(folder, "b.gui");
        var keyA = GuiConditionalKeys.FilePrefix(fileA) + "KeyedBlock:window:0|widget:x:0";
        var keyB = GuiConditionalKeys.FilePrefix(fileB) + "KeyedBlock:window:0|widget:x:0";
        var changes = 0;
        EventHandler count = (_, _) => changes++;
        GuiSettings.HiddenConditionalsChanged += count;
        try
        {
            GuiSettings.SetConditionalHidden(keyA, true);
            GuiSettings.SetConditionalHidden(keyA, true);
            GuiSettings.SetConditionalHidden(keyB, true);

            Assert.Equal(2, changes);
            Assert.Equal(1, GuiSettings.HiddenCount(fileA));
            Assert.Equal(1, GuiSettings.HiddenCount(fileB.ToUpperInvariant()));

            GuiSettings.ShowAllConditionals(fileA);

            Assert.Equal(0, GuiSettings.HiddenCount(fileA));
            Assert.Contains(keyB, GuiSettings.HiddenConditionals);
            Assert.Equal(3, changes);

            GuiSettings.SetConditionalHidden(keyB, false);
            GuiSettings.ShowAllConditionals(fileA);

            Assert.Equal(0, GuiSettings.HiddenCount(fileB));
            Assert.Equal(4, changes);
        }
        finally
        {
            GuiSettings.HiddenConditionalsChanged -= count;
            GuiSettings.SetConditionalHidden(keyA, false);
            GuiSettings.SetConditionalHidden(keyB, false);
        }
    }
}
