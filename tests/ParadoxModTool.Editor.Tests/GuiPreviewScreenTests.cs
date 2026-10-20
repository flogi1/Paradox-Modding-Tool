using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Auflösung und UI-Skalierung "wie im Spiel" für Designer und Inspektor: klassisch settings.txt, Jomini pdx_settings.txt bzw. .json.</summary>
public sealed class GuiPreviewScreenTests
{
    [Fact]
    public void From_game_fills_missing_jomini_resolution_from_primary_screen()
    {
        // Victoria 3: pdx_settings.json nennt keine Auflösung, nur die GUI-Skalierung.
        var folder = JsonFolder("from-game", "{\"GUI\":{\"scale\":\"1.3\"},\"System\":{\"language\":\"l_german\"}}");
        WithJomini(folder, () =>
        {
            var primary = GuiPreviewScreen.PrimaryScreenPixels();
            Assert.True(primary.Width > 0 && primary.Height > 0);
            Assert.Equal((primary.Width, primary.Height, 1.3), GuiPreviewScreen.FromGame());
        });
    }

    [Theory]
    [InlineData("9", 4)]
    [InlineData("0.1", 0.25)]
    public void Jomini_ui_scale_from_game_is_clamped(string scale, double expected)
    {
        var folder = JsonFolder("clamp-" + scale, "{\"GUI\":{\"scale\":\"" + scale + "\"}}");
        WithJomini(folder, () => Assert.Equal(expected, GuiPreviewScreen.FromGame()!.Value.UiScale));
    }

    [Fact]
    public void From_game_without_jomini_settings_is_null()
    {
        WithJomini(Path.Combine(TempFiles.Root, "preview-screen", "missing"), () => Assert.Null(GuiPreviewScreen.FromGame()));
        WithJomini(null, () => Assert.Null(GuiPreviewScreen.FromGame()));
    }

    [Fact]
    public void Fallback_is_full_hd_for_classic_and_primary_screen_for_jomini()
    {
        WithJomini(null, () =>
        {
            var primary = GuiPreviewScreen.PrimaryScreenPixels();
            Assert.Equal((primary.Width, primary.Height, 1.0), GuiPreviewScreen.Fallback());
        });

        var previousLayout = GuiSettings.Layout;
        try
        {
            GuiSettings.Layout = GuiLayout.Default with { Dialect = GuiDialect.Classic };
            Assert.Equal((1920, 1080, 1.0), GuiPreviewScreen.Fallback());
        }
        finally
        {
            GuiSettings.Layout = previousLayout;
        }
    }

    private static string JsonFolder(string name, string json)
    {
        var file = TempFiles.Write(Path.Combine("preview-screen", name, "pdx_settings.json"));
        File.WriteAllText(file, json);
        return Path.GetDirectoryName(file)!;
    }

    private static void WithJomini(string? userFolder, Action action)
    {
        var previousLayout = GuiSettings.Layout;
        var previousFolder = GuiSettings.GameUserFolder;
        try
        {
            GuiSettings.Layout = GuiLayout.Default;
            GuiSettings.GameUserFolder = userFolder;
            Sta.Run(action);
        }
        finally
        {
            GuiSettings.Layout = previousLayout;
            GuiSettings.GameUserFolder = previousFolder;
        }
    }
}
