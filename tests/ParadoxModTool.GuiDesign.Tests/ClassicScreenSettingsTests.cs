using ParadoxModTool.GuiDesign.Classic;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Auflösung und UI-Skalierung aus der <c>settings.txt</c> der klassischen Spiele (Voreinstellung der Vorschau).</summary>
public sealed class ClassicScreenSettingsTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-screensettings-" + Guid.NewGuid().ToString("N"));

    public ClassicScreenSettingsTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void Reads_hoi4_settings()
    {
        // Aufbau wie in der echten HOI-IV-Datei: size einzeilig, min_gui direkt danach (darf nicht gelesen werden).
        var path = Write(
            "language=\"l_english\"\n"
            + "graphics={\n"
            + "\tsize={ x=2048 y=1152 }\n"
            + "\tmin_gui={ x=1280 y=720 }\n"
            + "\tgui_scale=1.000000\n"
            + "\trefreshRate=60\n"
            + "}\n"
            + "last_mods={ \"mod/a.mod\" }\n");

        Assert.Equal((2048, 1152, 1.0), ClassicScreenSettings.TryRead(path));
    }

    [Fact]
    public void Reads_eu4_game_ui_scale()
    {
        // Aufbau wie in der echten EU-IV-Datei: size über mehrere Zeilen.
        var path = Write(
            "lastplayer=\"x\"\n"
            + "graphics={\n"
            + "\tsize={\n\t\tx=2560\n\t\ty=1440\n\t}\n\n"
            + "\tmin_gui={\n\t\tx=1280\n\t\ty=720\n\t}\n"
            + "\tgame_ui_scale=1.300\n"
            + "\trefreshRate=120\n"
            + "}\n");

        Assert.Equal((2560, 1440, 1.3), ClassicScreenSettings.TryRead(path));
    }

    [Fact]
    public void Missing_scale_means_one()
    {
        var path = Write("graphics={ size={ x=1920 y=1080 } }");

        Assert.Equal((1920, 1080, 1.0), ClassicScreenSettings.TryRead(path));
    }

    [Fact]
    public void Missing_file_returns_null()
    {
        Assert.Null(ClassicScreenSettings.TryRead(Path.Combine(_temp, "missing", "settings.txt")));
    }

    [Fact]
    public void Missing_size_or_outer_size_key_returns_null()
    {
        // Ein size außerhalb von graphics zählt nicht.
        Assert.Null(ClassicScreenSettings.TryRead(Write("size={ x=800 y=600 } graphics={ gui_scale=1.2 }")));
    }

    [Fact]
    public void Settings_path_is_next_to_the_mod_folder()
    {
        Assert.Equal(@"C:\Docs\Paradox Interactive\Hearts of Iron IV\settings.txt", ClassicScreenSettings.PathFor(@"C:\Docs\Paradox Interactive\Hearts of Iron IV\mod\"));
        Assert.Equal(@"C:\Docs\Paradox Interactive\Europa Universalis IV\settings.txt", ClassicScreenSettings.PathFor("C:/Docs/Paradox Interactive/Europa Universalis IV/mod"));
        Assert.Null(ClassicScreenSettings.PathFor(null));
        Assert.Null(ClassicScreenSettings.PathFor(" "));
    }

    private string Write(string text)
    {
        var path = Path.Combine(_temp, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, text);
        return path;
    }
}
