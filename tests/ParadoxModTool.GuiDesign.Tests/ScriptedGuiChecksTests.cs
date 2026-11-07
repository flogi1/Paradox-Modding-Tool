using ParadoxModTool.GuiDesign.Checks;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using static ParadoxModTool.GuiDesign.Tests.ScriptedGuiIndexTests;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Prüfung der Scripted GUIs (HOI IV, Plan 3 Task 4): nur Einträge der Mod und Fenster der geöffneten Mod-Datei.</summary>
public sealed class ScriptedGuiChecksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pmt-sguicheck-" + Guid.NewGuid().ToString("N"));

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

    /// <summary>Baut den Arbeitsbereich (HOI IV) und prüft die Mod-Datei <paramref name="open"/>.</summary>
    private IReadOnlyList<GuiFinding> Check(string open)
    {
        Directory.CreateDirectory(Install);
        Directory.CreateDirectory(Mod);
        var workspace = GuiWorkspace.Create(new GuiWorkspaceOptions(Install, Mod, Array.Empty<string>(), ClassicLayoutKit.Rules("hoi4"), null));
        var path = Path.Combine(Mod, open.Replace('/', Path.DirectorySeparatorChar));
        var document = GuiParser.Parse(File.ReadAllText(path));
        var context = new ClassicCheckContext(workspace, document, path, null, true, false);
        return ClassicCheckRunner.Run(context, new[] { new ScriptedGuiChecks() }, CancellationToken.None);
    }

    private static string Entry(string name, string body) =>
        "scripted_gui = {\n\t" + name + " = {\n" + body + "\t}\n}\n";

    [Fact]
    public void Checks_WindowMissing()
    {
        var script = Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui", "\t\tcontext_type = player_context\n\t\twindow_name = \"nope\"\n"));
        Put(Mod, "interface/m.gui", Gui(Window("other")));

        var finding = Assert.Single(Check("interface/m.gui"));

        Assert.Equal(GuiFindingKind.ScriptWindowMissing, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Error, finding.Severity);
        Assert.Contains("nope", finding.Message);
        Assert.Equal(script, finding.JumpPath);
        Assert.Equal(2, finding.JumpLine);
    }

    [Fact]
    public void Checks_ElementUnknown()
    {
        var script = Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui",
            "\t\tcontext_type = player_context\n\t\twindow_name = w\n\t\teffects = {\n\t\t\tghost_click = { }\n\t\t\tbtn_click = { }\n\t\t}\n"));
        Put(Mod, "interface/m.gui", Gui(Window("w", Button("btn"))));

        var finding = Assert.Single(Check("interface/m.gui"));

        Assert.Equal(GuiFindingKind.ScriptElementUnknown, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Warning, finding.Severity);
        Assert.Contains("ghost_click", finding.Message);
        Assert.Equal("containerWindowType", finding.Node?.Name);
        Assert.Equal(script, finding.JumpPath);
        Assert.Equal(6, finding.JumpLine);
    }

    [Fact]
    public void Checks_CategoryMissing()
    {
        Put(Mod, "common/scripted_guis/m.txt",
            "scripted_gui = {\n" +
            "\ta_ui = {\n\t\tcontext_type = decision_category\n\t\twindow_name = w\n\t}\n" +
            "\tb_ui = {\n\t\tcontext_type = decision_category\n\t\twindow_name = w\n\t}\n" +
            "}\n");
        Put(Mod, "common/decisions/categories/m_categories.txt", "b_category = {\n\tscripted_gui = b_ui\n}\n");
        Put(Mod, "interface/m.gui", Gui(Window("w")));

        var finding = Assert.Single(Check("interface/m.gui"));

        Assert.Equal(GuiFindingKind.CategoryMissing, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Warning, finding.Severity);
        Assert.Contains("a_ui", finding.Message);
        Assert.Equal(2, finding.JumpLine);
    }

    [Fact]
    public void Checks_ButtonWithoutEffect_IsHint()
    {
        Put(Mod, "common/scripted_guis/m.txt", Entry("m_ui",
            "\t\tcontext_type = player_context\n\t\twindow_name = w\n\t\teffects = {\n\t\t\twired_right_click = { }\n\t\t}\n\t\ttriggers = {\n\t\t\tloose_click_enabled = { }\n\t\t}\n"));
        Put(Mod, "interface/m.gui", Gui(Window("w", Button("wired"), Button("loose"), Icon("icon")), Window("unwired", Button("free"))));

        var finding = Assert.Single(Check("interface/m.gui"));

        Assert.Equal(GuiFindingKind.ScriptButtonWithoutEffect, finding.Kind);
        Assert.Equal(GuiFindingSeverity.Hint, finding.Severity);
        Assert.Contains("loose", finding.Message);
        Assert.Equal("buttonType", finding.Node?.Name);
    }

    [Fact]
    public void Checks_VanillaEntriesIgnored()
    {
        Put(Install, "common/scripted_guis/v.txt",
            "scripted_gui = {\n" +
            "\tv_missing = {\n\t\tcontext_type = decision_category\n\t\twindow_name = nope\n\t}\n" +
            "\tv_ui = {\n\t\tcontext_type = decision_category\n\t\twindow_name = w\n\t\tproperties = {\n\t\t\tghost = { }\n\t\t}\n\t}\n" +
            "}\n");
        Put(Mod, "interface/m.gui", Gui(Window("w")));

        Assert.Empty(Check("interface/m.gui"));
    }
}
