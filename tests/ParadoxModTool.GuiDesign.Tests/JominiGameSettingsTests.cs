using ParadoxModTool.GuiDesign.Layout;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Auflösung, UI-Skalierung und Sprache aus den Spieleinstellungen der Jomini-Spiele (<c>pdx_settings.txt</c> / <c>.json</c>).</summary>
public sealed class JominiGameSettingsTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-jominisettings-" + Guid.NewGuid().ToString("N"));

    public JominiGameSettingsTests() => Directory.CreateDirectory(_temp);

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    // Aufbau wie in der echten CK-III-Datei: Gruppen mit Anführungszeichen, Einträge als "name"={ version=… value=… }.
    private static string Ck3(string displayMode = "fullscreen", string gui = "\"GUI\"={\n\t\"scale\"={\n\t\tversion=1\n\t\tvalue=\"1.3\"\n\t}\n}\n") =>
        "\"Graphics\"={\n"
        + $"\t\"display_mode\"={{\n\t\tversion=0\n\t\tvalue=\"{displayMode}\"\n\t}}\n"
        + "\t\"render_scale\"={\n\t\tversion=0\n\t\tvalue=2.000000\n\t}\n"
        + "\t\"fullscreen_resolution\"={\n\t\tversion=0\n\t\tvalue=\"2560x1440\"\n\t}\n"
        + "\t\"windowed_resolution\"={\n\t\tversion=0\n\t\tvalue=\"1920x1080\"\n\t}\n"
        + "}\n"
        + "\"System\"={\n\t\"language\"={\n\t\tversion=0\n\t\tvalue=\"l_german\"\n\t}\n}\n"
        + gui;

    [Fact]
    public void Txt_reads_scale_language_and_fullscreen_resolution()
    {
        var settings = JominiGameSettings.ParseTxt(Ck3());

        Assert.Equal(new GameScreenSettings(2560, 1440, 1.3, "l_german"), settings);
    }

    [Fact]
    public void Windowed_uses_the_windowed_resolution()
    {
        var settings = JominiGameSettings.ParseTxt(Ck3("windowed"));

        Assert.Equal(1920, settings!.Width);
        Assert.Equal(1080, settings.Height);
    }

    [Fact]
    public void Render_scale_is_not_ui_scale()
    {
        // render_scale=2 steht in der Datei, aber nur GUI/scale zählt; ohne GUI-Block bleibt es bei 1.
        var settings = JominiGameSettings.ParseTxt(Ck3(gui: string.Empty));

        Assert.Equal(1.0, settings!.UiScale);
    }

    [Fact]
    public void Txt_without_scale_defaults_to_one()
    {
        var settings = JominiGameSettings.ParseTxt("\"Graphics\"={\n\t\"display_mode\"={ version=0 value=\"borderless\" }\n}\n");

        Assert.Equal(1.0, settings!.UiScale);
        Assert.Null(settings.Language);
        Assert.Null(settings.Width);

        // Komma statt Punkt ist nicht lesbar und ergibt ebenfalls 1.
        Assert.Equal(1.0, JominiGameSettings.ParseTxt("\"GUI\"={ \"scale\"={ version=1 value=\"1,3\" } }")!.UiScale);
    }

    [Fact]
    public void Borderless_has_no_resolution()
    {
        var settings = JominiGameSettings.ParseTxt(Ck3("borderless"));

        Assert.Null(settings!.Width);
        Assert.Null(settings.Height);
        Assert.Equal(1.3, settings.UiScale);
    }

    [Fact]
    public void Json_reads_gui_scale_and_system_language()
    {
        // Victoria 3: einzeilig, Skalierung als Zeichenkette, keine Auflösung.
        const string json = "{\"game\":{},\"Graphics\":{\"quality\":\"medium\",\"display_mode\":\"fullscreen\",\"display_index\":\"0\",\"vsync\":true},"
            + "\"System\":{\"language\":\"l_german\",\"user_bindings\":\"user_bindings/user.bindings\"},\"GUI\":{\"scale\":\"1.3\",\"tools_gui_scale\":\"1.3\"}}";

        Assert.Equal(new GameScreenSettings(null, null, 1.3, "l_german"), JominiGameSettings.ParseJson(json));
    }

    [Fact]
    public void Json_accepts_a_numeric_scale()
    {
        Assert.Equal(1.5, JominiGameSettings.ParseJson("{\"GUI\":{\"scale\":1.5}}")!.UiScale);
    }

    [Fact]
    public void Json_without_language_has_null_language()
    {
        // EU V: eingerückt mit Tabulatoren, Backslashes im Editorpfad, keine Sprache.
        const string json = "{\n\t\"Audio\":{\n\t\t\"master\":0.5\n\t},\n\t\"GUI\":{\n\t\t\"scale\":\"1.3\"\n\t},\n"
            + "\t\"System\":{\n\t\t\"editor\":\"D:\\\\Tools\\\\ParadoxModTool.exe\",\n\t\t\"user_bindings\":\"user.bindings\"\n\t}\n}";

        var settings = JominiGameSettings.ParseJson(json);

        Assert.Equal(new GameScreenSettings(null, null, 1.3, null), settings);
    }

    [Fact]
    public void Malformed_json_returns_null()
    {
        Assert.Null(JominiGameSettings.ParseJson("{\"GUI\":{\"scale\":"));
        Assert.Null(JominiGameSettings.ParseJson("[1,2]"));
    }

    [Fact]
    public void Missing_folder_returns_null()
    {
        Assert.Null(JominiGameSettings.TryRead(Path.Combine(_temp, "gibt-es-nicht")));
        Assert.Null(JominiGameSettings.TryRead(_temp));
    }

    [Fact]
    public void TryRead_prefers_txt_and_falls_back_to_json()
    {
        File.WriteAllText(Path.Combine(_temp, "pdx_settings.json"), "{\"GUI\":{\"scale\":\"1.5\"}}");
        Assert.Equal(1.5, JominiGameSettings.TryRead(_temp)!.UiScale);

        File.WriteAllText(Path.Combine(_temp, "pdx_settings.txt"), Ck3());
        Assert.Equal(1.3, JominiGameSettings.TryRead(_temp)!.UiScale);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("\"GUI\"={ \"scale\"={ version=1 value=\"1.3")]
    [InlineData("}}} { \"a\"= = { {")]
    [InlineData("\"Graphics\"={ \"display_mode\"={ value=\"fullscreen\"")]
    public void Malformed_txt_does_not_throw(string text)
    {
        var exception = Record.Exception(() => JominiGameSettings.ParseTxt(text));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("}}} { \"a\"= = { {")]
    public void Txt_without_known_entries_returns_null(string text)
    {
        Assert.Null(JominiGameSettings.ParseTxt(text));
    }

    [Fact]
    public void Empty_txt_falls_back_to_json()
    {
        File.WriteAllText(Path.Combine(_temp, "pdx_settings.txt"), string.Empty);
        File.WriteAllText(Path.Combine(_temp, "pdx_settings.json"), "{\"GUI\":{\"scale\":\"1.5\"},\"System\":{\"language\":\"l_english\"}}");

        Assert.Equal(new GameScreenSettings(null, null, 1.5, "l_english"), JominiGameSettings.TryRead(_temp));
    }

    [Fact]
    public void Unparseable_json_file_returns_null()
    {
        File.WriteAllText(Path.Combine(_temp, "pdx_settings.json"), "{\"GUI\":{\"scale\":");

        Assert.Null(JominiGameSettings.TryRead(_temp));
    }
}
