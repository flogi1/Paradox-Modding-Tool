using System.IO;
using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>
/// Bildschirm-Leiste der Vorschau: Auflösung und UI-Skalierung aus den Spieleinstellungen (klassisch settings.txt, Jomini pdx_settings), sonst
/// klassisch 1920×1080, Jomini der Hauptbildschirm, jeweils mit 1.
/// </summary>
public sealed class GuiDesignerClassicToolsTests
{
    [Fact]
    public void Classic_preview_options_come_from_game_settings_and_jomini_without_user_folder_uses_primary_screen()
    {
        var settings = TempFiles.Write("classic-tools/settings.txt");
        File.WriteAllText(settings, "graphics={\n\tsize={\n\t\tx=2048\n\t\ty=1152\n\t}\n\tmin_gui={ x=1280 y=720 }\n\tgame_ui_scale=1.300\n}\n");
        var previousLayout = GuiSettings.Layout;
        var previousPath = GuiSettings.GameSettingsPath;
        var previousFolder = GuiSettings.GameUserFolder;
        try
        {
            Sta.Run(() =>
            {
                GuiSettings.Layout = GuiLayout.Default with { Dialect = GuiDialect.Classic };
                GuiSettings.GameSettingsPath = settings;
                // Ohne Benutzerordner hat Jomini keine Spieleinstellungen: "Spiel" mit der Auflösung des Hauptbildschirms (vorgewählt) und 1.
                GuiSettings.GameUserFolder = null;
                var view = new GuiDesignerView();
                view.SyncScreenTools();

                var classic = view.PreviewOptions;
                Assert.Equal(2048, classic.ScreenWidth);
                Assert.Equal(1152, classic.ScreenHeight);
                Assert.Equal(1.3, classic.UiScale);

                GuiSettings.Layout = GuiLayout.Default;
                view.SyncScreenTools();
                var jomini = view.PreviewOptions;
                var primary = GuiPreviewScreen.PrimaryScreenPixels();
                Assert.Equal(primary.Width, jomini.ScreenWidth);
                Assert.Equal(primary.Height, jomini.ScreenHeight);
                Assert.Equal(1, jomini.UiScale);
                var selected = (ComboBoxItem)view.ScreenTools.Children.OfType<ComboBox>().First().SelectedItem;
                Assert.Equal(GuiTexts.DesignerResolutionGame(primary.Width, primary.Height), selected.Content);
            });
        }
        finally
        {
            GuiSettings.Layout = previousLayout;
            GuiSettings.GameSettingsPath = previousPath;
            GuiSettings.GameUserFolder = previousFolder;
        }
    }

    [Fact]
    public void Classic_preview_without_settings_uses_full_hd_and_scale_one()
    {
        var previousLayout = GuiSettings.Layout;
        var previousPath = GuiSettings.GameSettingsPath;
        try
        {
            Sta.Run(() =>
            {
                GuiSettings.Layout = GuiLayout.Default with { Dialect = GuiDialect.Classic };
                GuiSettings.GameSettingsPath = Path.Combine(TempFiles.Root, "classic-tools", "missing", "settings.txt");
                var view = new GuiDesignerView();
                view.SyncScreenTools();

                var options = view.PreviewOptions;
                Assert.Equal(1920, options.ScreenWidth);
                Assert.Equal(1080, options.ScreenHeight);
                Assert.Equal(1, options.UiScale);
            });
        }
        finally
        {
            GuiSettings.Layout = previousLayout;
            GuiSettings.GameSettingsPath = previousPath;
        }
    }

    [Fact]
    public void Screen_tools_are_visible_for_jomini()
    {
        var previousLayout = GuiSettings.Layout;
        var previousFolder = GuiSettings.GameUserFolder;
        try
        {
            Sta.Run(() =>
            {
                GuiSettings.Layout = GuiLayout.Default;
                GuiSettings.GameUserFolder = null;
                var view = new GuiDesignerView();
                view.SyncScreenTools();

                Assert.Equal(Visibility.Visible, view.ScreenTools.Visibility);
            });
        }
        finally
        {
            GuiSettings.Layout = previousLayout;
            GuiSettings.GameUserFolder = previousFolder;
        }
    }

    [Fact]
    public void Jomini_preview_options_use_selected_resolution_and_scale()
    {
        // pdx_settings.txt wie bei Crusader Kings III: Vollbild 2560×1440, GUI-Skalierung 1.5; "Spiel" ist dann vorgewählt.
        var settings = TempFiles.Write("jomini-tools/pdx_settings.txt");
        File.WriteAllText(
            settings,
            "\"Graphics\"={\n\t\"display_mode\"={\n\t\tversion=0\n\t\tvalue=\"fullscreen\"\n\t}\n"
            + "\t\"fullscreen_resolution\"={\n\t\tversion=0\n\t\tvalue=\"2560x1440\"\n\t}\n}\n"
            + "\"GUI\"={\n\t\"scale\"={\n\t\tversion=1\n\t\tvalue=\"1.5\"\n\t}\n}\n");
        var previousLayout = GuiSettings.Layout;
        var previousFolder = GuiSettings.GameUserFolder;
        try
        {
            Sta.Run(() =>
            {
                GuiSettings.Layout = GuiLayout.Default;
                GuiSettings.GameUserFolder = Path.GetDirectoryName(settings);
                var view = new GuiDesignerView();
                view.SyncScreenTools();

                var options = view.PreviewOptions;
                Assert.Equal(2560, options.ScreenWidth);
                Assert.Equal(1440, options.ScreenHeight);
                Assert.Equal(1.5, options.UiScale);
            });
        }
        finally
        {
            GuiSettings.Layout = previousLayout;
            GuiSettings.GameUserFolder = previousFolder;
        }
    }

    [Theory]
    [InlineData("2048×1152", 2048, 1152)]
    [InlineData("2048x1152", 2048, 1152)]
    [InlineData(" 3440 1440 ", 3440, 1440)]
    public void Custom_resolution_parses(string text, int width, int height)
    {
        Assert.Equal((width, height), GuiDesignerView.ParseSize(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2048")]
    [InlineData("100x100")]
    [InlineData("abcxdef")]
    [InlineData("1x2x3")]
    public void Invalid_custom_resolution_is_rejected(string text)
    {
        Assert.Null(GuiDesignerView.ParseSize(text));
    }
}
