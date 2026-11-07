using System.Text;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>
/// Kodierung der Skriptdateien, die der GUI-Editor schreibt (Ruling R10): neue <c>common/*.txt</c> in beiden Spielen UTF-8 ohne BOM (die Probe im Spiel lief
/// ohne BOM, vanilla <c>common/</c> hat keins), bestehende behalten ihre Kodierung, auch Windows-1252; Lokalisierung bleibt UTF-8 mit BOM.
/// </summary>
public sealed class ClassicScriptEncodingTests : IDisposable
{
    private readonly string _mod = Path.Combine(Path.GetTempPath(), "pmt-scriptenc-" + Guid.NewGuid().ToString("N"));

    public ClassicScriptEncodingTests() => Directory.CreateDirectory(_mod);

    public void Dispose() => Directory.Delete(_mod, recursive: true);

    private static GameProfile Profile(string game) => GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", game + ".json"));

    private string ModPath(string relative) => Path.Combine(_mod, relative.Replace('/', Path.DirectorySeparatorChar));

    [Theory]
    [InlineData("eu4", "common/custom_gui/my_mod_custom_gui.txt")]
    [InlineData("hoi4", "common/scripted_guis/my_mod_scripted_guis.txt")]
    [InlineData("hoi4", "common/decisions/categories/my_mod_categories.txt")]
    public void NewScriptFile_HasNoBom(string game, string relative)
    {
        var file = new GuiPlanFile(ModPath(relative), null, "custom_button = {\n}\n");

        Assert.Empty(ClassicOverridePlanner.WriteEncoding(file, Profile(game)).GetPreamble());
    }

    [Theory]
    [InlineData("eu4")]
    [InlineData("hoi4")]
    public void NewLocalisation_KeepsBom(string game)
    {
        var file = new GuiPlanFile(ModPath("localisation/my_mod_l_english.yml"), null, "l_english:\n");

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, ClassicOverridePlanner.WriteEncoding(file, Profile(game)).GetPreamble());
    }

    [Fact]
    public void ExistingWindows1252ScriptFile_StaysWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var latin = Encoding.GetEncoding(1252);
        var path = ModPath("common/custom_gui/old.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, latin.GetBytes("# Größe\ncustom_button = {\n}\n"));
        var (text, encoding) = GuiTextReader.Read(path);
        Assert.Equal(GuiTextReader.Windows1252, encoding);

        var write = ClassicOverridePlanner.WriteEncoding(new GuiPlanFile(path, text, text + "custom_icon = {\n}\n"), Profile("eu4"));

        Assert.Equal(1252, write.CodePage);
    }

    [Fact]
    public void ExistingBomFileWithWindows1252Body_KeepsBom()
    {
        // GuiTextReader liest so eine Datei als Windows-1252 (ohne BOM); geschrieben wird sie wegen des BOM wieder als UTF-8 mit BOM (ModFileEncoding.ForWrite).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var path = ModPath("interface/odd.gui");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.GetEncoding(1252).GetBytes("# Größe\nguiTypes = {\n}\n")).ToArray());
        Assert.Equal(GuiTextReader.Windows1252, GuiTextReader.Read(path).EncodingName);

        var write = ClassicOverridePlanner.WriteEncoding(new GuiPlanFile(path, "x", "y"), Profile("eu4"));

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, write.GetPreamble());
    }

    [Fact]
    public void ExistingUtf8BomScriptFile_KeepsBom()
    {
        var path = ModPath("common/custom_gui/bom.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "custom_button = {\n}\n", new UTF8Encoding(true));

        var write = ClassicOverridePlanner.WriteEncoding(new GuiPlanFile(path, "x", "y"), Profile("eu4"));

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, write.GetPreamble());
    }
}
