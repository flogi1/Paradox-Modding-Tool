using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using static ParadoxModTool.GuiDesign.Tests.CustomGuiIndexTests;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Prüfung der Custom GUI (EU IV, Plan 3 Task 5): nur Elemente der geöffneten Mod-Datei und Einträge der Mod.</summary>
public sealed class CustomGuiChecksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-cguicheck-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Install => Path.Combine(_root, "install");

    private string Mod => Path.Combine(_root, "mod");

    private string Put(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Baut den Arbeitsbereich (EU IV, mitgelieferte Fensterliste) und prüft die Mod-Datei <paramref name="open"/>.</summary>
    private IReadOnlyList<GuiFinding> Check(string open)
    {
        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Mod);
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Install, Mod, Array.Empty<string>(), ClassicLayoutKit.Rules("eu4"), null));
        Assert.Equal(GuiScriptWiring.CustomGui, workspace.Rules.Layout.ScriptWiring);
        var path = Path.Combine(Mod, open.Replace('/', Path.DirectorySeparatorChar));
        var document = GuiParser.Parse(File.ReadAllText(path));
        var context = new ClassicCheckContext(workspace, document, path, null, true, false);
        return ClassicCheckRunner.Run(context, new[] { new CustomGuiChecks() }, CancellationToken.None);
    }

    private static string Scripted(string type, string name) => type + " = { name = \"" + name + "\" scripted = yes }";

    private static string Plain(string type, string name) => type + " = { name = \"" + name + "\" }";

    [Fact]
    public void Checks_EntryMissing()
    {
        Put(Mod, "common/custom_gui/m.txt", "custom_button = {\n\tname = wired\n}\n");
        Put(Mod, "interface/countrymissionsview.gui", Gui(Window("countrymissionsview",
            Scripted("guiButtonType", "wired"), Scripted("guiButtonType", "loose"), Plain("guiButtonType", "plain"))));

        var finding = Assert.Single(Check("interface/countrymissionsview.gui"));

        Assert.Equal(GuiFindingKind.CustomEntryMissing, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Error, finding.Severity);
        Assert.Contains("loose", finding.Message);
        Assert.Equal("guiButtonType", finding.Node?.Name);
        Assert.Null(finding.JumpPath);
    }

    [Fact]
    public void Checks_ElementMissing()
    {
        var script = Put(Mod, "common/custom_gui/m.txt", "custom_button = {\n\tname = wired\n}\n\ncustom_icon = {\n\tname = ghost\n}\n");
        Put(Mod, "interface/countrymissionsview.gui", Gui(Window("countrymissionsview", Scripted("guiButtonType", "wired"))));

        var finding = Assert.Single(Check("interface/countrymissionsview.gui"));

        Assert.Equal(GuiFindingKind.CustomElementMissing, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Warning, finding.Severity);
        Assert.Contains("ghost", finding.Message);
        Assert.Contains("custom_icon", finding.Message);
        Assert.Null(finding.Node);
        Assert.Equal(script, finding.JumpPath);
        Assert.Equal(5, finding.JumpLine);
    }

    [Fact]
    public void Checks_KindMismatch()
    {
        Put(Mod, "common/custom_gui/m.txt",
            "custom_button = { name = as_icon }\n" +
            "custom_shield = { name = shield_icon }\n" +
            "custom_shield = { name = shield_button }\n" +
            "custom_icon = { name = Icon_OK }\n");
        Put(Mod, "interface/countrymissionsview.gui", Gui(Window("countrymissionsview",
            Scripted("iconType", "as_icon"), Scripted("iconType", "shield_icon"), Scripted("guiButtonType", "shield_button"), Scripted("ICONTYPE", "icon_ok"))));

        var findings = Check("interface/countrymissionsview.gui");

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal(GuiFindingKind.CustomKindMismatch, f.Kind));
        Assert.All(findings, f => Assert.Equal(GuiFindingSeverity.Warning, f.Severity));
        var button = Assert.Single(findings, f => f.Message.Contains("as_icon"));
        Assert.Contains("custom_button", button.Message);
        Assert.DoesNotContain(GuiTexts.CustomUnsure, button.Message);
        var shield = Assert.Single(findings, f => f.Message.Contains("shield_icon"));
        Assert.Contains(GuiTexts.CustomUnsure, shield.Message);
        Assert.Equal("iconType", shield.Node?.Name);
    }

    [Fact]
    public void Checks_OutsideSupportedWindow()
    {
        Put(Mod, "common/custom_gui/m.txt", "custom_button = { name = inside }\ncustom_button = { name = outside }\ncustom_button = { name = nested }\n");
        Put(Mod, "interface/countrymissionsview.gui", Gui(
            Window("countrymissionsview", "windowType = { name = \"plain_box\" " + Scripted("guiButtonType", "nested") + " }", Scripted("guiButtonType", "inside")),
            Window("my_window", Scripted("guiButtonType", "outside"))));

        var finding = Assert.Single(Check("interface/countrymissionsview.gui"));

        Assert.Equal(GuiFindingKind.CustomOutsideSupportedWindow, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Error, finding.Severity);
        Assert.Contains("outside", finding.Message);
        Assert.Contains("my_window", finding.Message);
        Assert.Equal("guiButtonType", finding.Node?.Name);
    }

    [Fact]
    public void Checks_NotScripted()
    {
        var script = Put(Mod, "common/custom_gui/m.txt", "custom_button = {\n\tname = wired\n}\ncustom_button = {\n\tname = plain\n}\n");
        Put(Mod, "interface/countrymissionsview.gui", Gui(Window("countrymissionsview", Scripted("guiButtonType", "wired"), Plain("guiButtonType", "plain"))));

        var finding = Assert.Single(Check("interface/countrymissionsview.gui"));

        Assert.Equal(GuiFindingKind.CustomNotScripted, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Warning, finding.Severity);
        Assert.Equal(GuiTexts.FindingCustomNotScripted("plain"), finding.Message);
        Assert.Null(finding.Node);
        Assert.Equal(script, finding.JumpPath);
        Assert.Equal(4, finding.JumpLine);
    }

    [Fact]
    public void Checks_ProvinceviewOutsideSubwindows()
    {
        Put(Mod, "common/custom_gui/m.txt", "custom_button = { name = in_province }\ncustom_button = { name = in_state }\ncustom_button = { name = loose }\n");
        Put(Mod, "interface/provinceview.gui", Gui(Window("provinceview",
            "windowType = { name = \"province_window\" " + Scripted("guiButtonType", "in_province") + " }",
            "windowType = { name = \"state_window\" windowType = { name = \"box\" " + Scripted("guiButtonType", "in_state") + " } }",
            "windowType = { name = \"buildings_window\" }",
            Scripted("guiButtonType", "loose"))));

        var finding = Assert.Single(Check("interface/provinceview.gui"));

        Assert.Equal(GuiFindingKind.CustomOutsideSupportedWindow, finding.Kind);
        Assert.Contains("loose", finding.Message);
        Assert.Contains("provinceview", finding.Message);
    }

    [Fact]
    public void Checks_SupportedWindowInOtherModFile()
    {
        Put(Install, "interface/countrymissionsview.gui", Gui(Window("countrymissionsview")));
        Put(Mod, "common/custom_gui/m.txt", "custom_button = { name = wired }\n");
        Put(Mod, "interface/00_x_overrides.gui", Gui(Window("countrymissionsview", Scripted("guiButtonType", "wired"))));

        Assert.Empty(Check("interface/00_x_overrides.gui"));
    }

    [Fact]
    public void Checks_VanillaIgnored()
    {
        Put(Install, "common/custom_gui/v.txt", "custom_button = { name = vanilla_ghost }\ncustom_button = { name = wired }\n");
        Put(Install, "interface/vanilla.gui", Gui(Window("vanilla_window", Scripted("guiButtonType", "vanilla_loose"))));
        Put(Mod, "interface/countrymissionsview.gui", Gui(Window("countrymissionsview", Scripted("guiButtonType", "wired"))));

        Assert.Empty(Check("interface/countrymissionsview.gui"));
    }
}
