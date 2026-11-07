using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Classic;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>"In Vanilla-Fenster einbauen…" (EU IV, Plan 3 Task 7): die aufgelösten Fenster mit ROOT und FROM, Entfernen des Originals nur auf Wunsch.</summary>
public sealed class CustomGuiHostWindowTests
{
    private static readonly SupportedWindow[] Supported =
    {
        new("countrydecisionsview", "countrydecisionsview", "countrydecisionsview.gui", "same as FROM", "the clicking country", "countrydecisionsview"),
        new("province_window", "province_window", "provinceview.gui", "the clicked province", "the clicking country", "provinceview"),
    };

    [Fact]
    public void HostDialog_ListsResolvedWindowsWithRootFrom()
    {
        Sta.Run(() =>
        {
            var window = new CustomGuiHostWindow("my_window", Supported);

            Assert.Equal(
                new[]
                {
                    GuiTexts.CustomGuiHostItem("countrydecisionsview", "countrydecisionsview.gui", "same as FROM", "the clicking country"),
                    GuiTexts.CustomGuiHostItem("province_window", "provinceview.gui", "the clicked province", "the clicking country"),
                },
                window.ShownWindows);
            Assert.Contains("ROOT: the clicked province", window.ShownWindows[1]);
            Assert.Same(Supported[0], window.SelectedHost);
            Assert.False(window.RemoveOriginal);
            Assert.Contains(GuiTexts.CustomGuiHostKeepsCopy("my_window"), window.HintText);
            Assert.True(window.BuildButton.IsEnabled);

            window.SelectedIndex = 1;
            window.RemoveOriginal = true;

            Assert.Same(Supported[1], window.SelectedHost);
            Assert.True(window.RemoveOriginal);
            Assert.DoesNotContain(GuiTexts.CustomGuiHostKeepsCopy("my_window"), window.HintText);
        });
    }

    [Fact]
    public void HostDialog_NoWindows_BuildDisabled()
    {
        Sta.Run(() =>
        {
            var window = new CustomGuiHostWindow("my_window", Array.Empty<SupportedWindow>());

            Assert.Empty(window.ShownWindows);
            Assert.Null(window.SelectedHost);
            Assert.False(window.BuildButton.IsEnabled);
            Assert.Contains(GuiTexts.CustomGuiHostNone, window.HintText);
        });
    }
}
