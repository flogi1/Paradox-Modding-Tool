using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Crusader Kings III: .gui-Dateien direkt in gui/, die zuerst geladene Datei gewinnt.</summary>
public class GuiFlatLayoutTests
{
    private static readonly GuiLayout Ck3 = new(Array.Empty<string>(), FirstWins: true);

    [Fact]
    public void Flat_firstWins_targets()
    {
        var targets = GuiModTargets.For(@"C:\mods\My Mod", firstWins: true);

        Assert.Equal(@"C:\mods\My Mod\gui\00_my_mod_overrides.gui", targets.OverridePath(""));
        Assert.Equal(@"C:\mods\My Mod\gui\00_my_mod_types.gui", targets.TypesPath(""));
        Assert.Equal("MyModOverrides", targets.OverrideContainer);
    }

    [Fact]
    public void FirstWins_keeps_user_names()
    {
        var targets = GuiModTargets.For(@"C:\mods\x", "x.gui", "y.gui", firstWins: true);

        Assert.Equal("x.gui", targets.OverrideFileName);
        Assert.Equal("y.gui", targets.TypesFileName);
    }

    [Fact]
    public void Eu5_targets_unchanged()
    {
        var targets = GuiModTargets.For(@"C:\mods\Better UI 2\");

        Assert.Equal("better_ui_2_overrides.gui", targets.OverrideFileName);
        Assert.Equal(@"C:\mods\Better UI 2\in_game\gui\better_ui_2_overrides.gui", targets.OverridePath("in_game"));
    }

    [Fact]
    public void Flat_layout_finds_textures_in_base_folders()
    {
        var root = Path.Combine(Path.GetTempPath(), "pmt-flat-" + Guid.NewGuid().ToString("N"));
        var mod = Path.Combine(root, "mod");
        Directory.CreateDirectory(Path.Combine(mod, "gfx", "interface"));
        File.WriteAllBytes(Path.Combine(mod, "gfx", "interface", "a.dds"), new byte[] { 1 });
        try
        {
            var roots = GuiTextureLocator.ContextRootsFor(Path.Combine(root, "install"), Array.Empty<string>(), mod, Array.Empty<string>());

            Assert.Contains(mod, roots);
            Assert.Equal(Path.Combine(mod, "gfx", "interface", "a.dds"), new GuiTextureLocator(roots).Locate("gfx/interface/a.dds"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Planner_uses_flat_context()
    {
        var source = GuiOverridePlannerTests.V("gui/shared/buttons.gui");
        var index = GuiIndex.Build(new[] { source }, _ => GuiOverridePlannerTests.Vanilla, Ck3);
        var targets = GuiModTargets.For(@"C:\test\Mod", firstWins: true) with { ContainerBase = "MyMod" };
        var draft = GuiOverridePlannerTests.Vanilla.Replace("size = { @w @h }", "size = { @w 5 }");

        var plan = GuiOverridePlanner.Plan(new GuiOverrideRequest(source, GuiOverridePlannerTests.Vanilla, draft, index, targets, _ => null));

        Assert.Equal(@"C:\test\Mod\gui\00_mod_overrides.gui", Assert.Single(plan.Files).FullPath);
    }
}
