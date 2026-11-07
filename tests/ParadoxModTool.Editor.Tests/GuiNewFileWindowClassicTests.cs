using System.IO;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Blocks;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>"Neue GUI-Datei" im klassischen Format (Plan 3 Task 9): Ordner <c>interface</c>, Unterordner, Fenster gemäß Baustein, Hinweis auf die Skript-Aktion.</summary>
public sealed class GuiNewFileWindowClassicTests : IDisposable
{
    private readonly string _mod = Path.Combine(Path.GetTempPath(), "pmt-newfile-" + Guid.NewGuid().ToString("N"));

    public GuiNewFileWindowClassicTests() => Directory.CreateDirectory(_mod);

    public void Dispose() => Directory.Delete(_mod, true);

    private static GuiLayout Layout(GuiScriptWiring wiring, params string[] crashRoots) => new(
        Array.Empty<string>(), true, Dialect: GuiDialect.Classic, ReplacePathGuiCrashRoots: crashRoots, MetadataFile: ParadoxModTool.Mods.ModMetadata.ClassicSiblingMetadataFile, ScriptWiring: wiring);

    private static GuiBlock Shipped(string game)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "profiles", "guiblocks", game, "Fenster.guiblock");
            if (File.Exists(path))
            {
                return GuiBlockParser.Parse(File.ReadAllText(path), GuiBlockLayer.Shipped, path);
            }
        }

        throw new InvalidOperationException("Fenster.guiblock nicht gefunden");
    }

    [Fact]
    public void Folder_Classic_IsInterface() => Sta.Run(() =>
    {
        var window = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), Layout(GuiScriptWiring.ScriptedGuis));
        Assert.Equal(Path.Combine(_mod, "interface"), window.Folder);
    });

    [Fact]
    public void Folder_Classic_Subfolder() => Sta.Run(() =>
    {
        var window = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), Layout(GuiScriptWiring.ScriptedGuis)) { };
        window.Subfolder = "my/sub";
        window.NameText = "x";
        Assert.Equal(Path.Combine(_mod, "interface", "my", "sub"), window.Folder);
        Assert.Null(window.Problem());
    });

    [Theory]
    [InlineData("../x")]
    [InlineData("a/../b")]
    [InlineData("C:\temp")]
    [InlineData("/abs")]
    public void Subfolder_Unsafe_IsRefused(string subfolder) => Sta.Run(() =>
    {
        var window = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), Layout(GuiScriptWiring.ScriptedGuis)) { };
        window.Subfolder = subfolder;
        window.NameText = "x";
        Assert.Equal(GuiTexts.NewFileInvalidSubfolder, window.Problem());
        Assert.Equal(Path.Combine(_mod, "interface"), window.Folder);
    });

    [Fact]
    public void Subfolder_NamedByReplacePath_IsRefused_OnlyWhereTheProfileSaysSo() => Sta.Run(() =>
    {
        File.WriteAllText(Path.Combine(_mod, "descriptor.mod"), "name=\"x\"\nreplace_path=\"interface/sub\"\n");
        var eu4 = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), Layout(GuiScriptWiring.CustomGui, "interface")) { };
        eu4.NameText = "x";
        eu4.Subfolder = "sub";
        Assert.Equal(GuiTexts.NewFileReplacePathSubfolder("interface/sub"), eu4.Problem());
        eu4.Subfolder = "other";
        Assert.Null(eu4.Problem());

        var hoi4 = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), Layout(GuiScriptWiring.ScriptedGuis)) { };
        hoi4.NameText = "x";
        hoi4.Subfolder = "sub";
        Assert.Null(hoi4.Problem());
    });

    [Fact]
    public void ExistingFile_IsRefused() => Sta.Run(() =>
    {
        Directory.CreateDirectory(Path.Combine(_mod, "interface"));
        File.WriteAllText(Path.Combine(_mod, "interface", "x.gui"), "");
        var window = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), Layout(GuiScriptWiring.ScriptedGuis)) { };
        window.NameText = "x";
        Assert.Equal(GuiTexts.NewFileExists("x.gui"), window.Problem());
    });

    // Ein GUI-Index mit einer Vanilla-Datei interface/provinceview.gui (Fenster provinceview) und einer Mod-Datei interface/own.gui (Fenster own_window).
    private static GuiIndex VanillaIndex(GuiLayout layout) => GuiIndex.Build(
        new[]
        {
            new GuiSourceFile(@"C:\test\Vanilla\interface\provinceview.gui", "interface/provinceview.gui", GuiLayer.Vanilla, "Vanilla", 0, 0),
            new GuiSourceFile(@"C:\test\Mod\interface\own.gui", "interface/own.gui", GuiLayer.Mod, "Mod", 0, 0),
        },
        file => file.Layer == GuiLayer.Vanilla
            ? "guiTypes = {\n\twindowType = {\n\t\tname = \"provinceview\"\n\t}\n\twindowType = {\n\t\tname = \"other_window\"\n\t}\n}\n"
            : "guiTypes = {\n\twindowType = {\n\t\tname = \"own_window\"\n\t}\n}\n",
        layout);

    [Fact]
    public void VanillaFileOfSamePath_IsRefused() => Sta.Run(() =>
    {
        var layout = Layout(GuiScriptWiring.CustomGui);
        var window = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), layout, VanillaIndex(layout)) { };
        window.NameText = "ProvinceView";
        Assert.Equal(GuiTexts.NewFileShadowsGameFile("interface/ProvinceView.gui"), window.Problem());
        window.Subfolder = "my";
        Assert.Equal(GuiTexts.NewFileWindowTaken("provinceview"), window.Problem());
    });

    [Fact]
    public void VanillaFileInSubfolder_IsRefused_WithLooseSpacing() => Sta.Run(() =>
    {
        var layout = Layout(GuiScriptWiring.ScriptedGuis);
        var index = GuiIndex.Build(
            new[] { new GuiSourceFile(@"C:\test\Vanilla\interface\my\sub\deep.gui", "interface/my/sub/deep.gui", GuiLayer.Vanilla, "Vanilla", 0, 0) },
            _ => "guiTypes = {\n}\n",
            layout);
        var window = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), layout, index) { };
        window.NameText = "deep";
        window.Subfolder = " my / sub ";
        Assert.Equal(GuiTexts.NewFileShadowsGameFile("interface/my/sub/deep.gui"), window.Problem());
        window.Subfolder = "my\\\\sub";
        Assert.Equal(GuiTexts.NewFileShadowsGameFile("interface/my/sub/deep.gui"), window.Problem());
    });

    [Fact]
    public void WindowNameTaken_IsRefused() => Sta.Run(() =>
    {
        var layout = Layout(GuiScriptWiring.ScriptedGuis);
        var window = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), layout, VanillaIndex(layout)) { };
        window.NameText = "other window.gui";
        Assert.Equal(GuiTexts.NewFileWindowTaken("other_window"), window.Problem());
        window.NameText = "own_window";
        Assert.Equal(GuiTexts.NewFileWindowTaken("own_window"), window.Problem());
        window.NameText = "fresh";
        Assert.Null(window.Problem());
    });

    [Fact]
    public void Scaffold_Hoi4_ContainerWindowType()
    {
        var text = ClassicNewFile.Scaffold(Shipped("hoi4"), "my_file");
        Assert.StartsWith("guiTypes = {\n\tcontainerWindowType = {", text);
        Assert.Contains("name = \"my_file\"", text);
        Assert.Contains("quadTextureSprite", text);
        Assert.EndsWith("\t}\n}\n", text);
        Assert.Contains(GuiParser.Parse(text).Root.Descendants(), n => n.Name == "containerWindowType");
    }

    [Fact]
    public void Scaffold_Eu4_WindowType()
    {
        var text = ClassicNewFile.Scaffold(Shipped("eu4"), "my_file");
        Assert.StartsWith("guiTypes = {\n\twindowType = {", text);
        Assert.Contains("name = \"my_file\"", text);
        Assert.Contains("scripted = yes", text);
        Assert.Contains("iconType = {", text);
        Assert.DoesNotContain("backGround", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hint_NamesScriptAction() => Sta.Run(() =>
    {
        var hoi4 = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), Layout(GuiScriptWiring.ScriptedGuis)) { };
        Assert.Contains("Scripted GUI anlegen", hoi4.Hint);
        var eu4 = new GuiNewFileWindow(_mod, Array.Empty<GuiBlock>(), Layout(GuiScriptWiring.CustomGui)) { };
        Assert.Contains("In Vanilla-Fenster einbauen", eu4.Hint);
    });
}
