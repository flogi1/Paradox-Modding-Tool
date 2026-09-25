using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiTypeCopyTests
{
    private static readonly GuiModTargets Targets = GuiOverridePlannerTests.Targets;

    private static readonly GuiRules Rules = new(new[] { "size" }, new[] { "tooltipwidget" }, Array.Empty<string>());

    private static (GuiIndex Index, GuiSourceFile Source) VanillaIndex()
    {
        var (request, index) = GuiOverridePlannerTests.Setup(string.Empty);
        return (index, request.Source);
    }

    private static GuiInspectTarget Target(GuiIndex index, GuiSourceFile file, Func<GuiNode, bool> match)
    {
        var document = index.DocumentOf(file);
        return new GuiInspectTarget(document.Root.Descendants().First(match), document, file.FullPath);
    }

    private static GuiOverridePlan Copy(GuiIndex index, GuiInspectTarget target, string name) =>
        GuiOverridePlanner.PlanNewTypeCopy(new GuiNewTypeCopyRequest(GuiTypeEdits.CopySource(index, target, Rules)!, name, index, Targets, _ => null));

    [Fact]
    public void TypeCopyGoesIntoTheTypesFileWithItsConstants()
    {
        var (index, source) = VanillaIndex();

        var plan = Copy(index, Target(index, source, n => n.Kind == GuiNodeKind.TypeDefinition && n.Name == "button_a"), "mymod_button_a");

        var file = Assert.Single(plan.Files);
        Assert.Equal(GuiOverridePlannerTests.TypesPath, file.FullPath);
        Assert.True(file.IsNew);
        Assert.Contains("@w = 10", file.NewText);
        Assert.Contains("@h = @w", file.NewText);
        Assert.Contains("types MyModTypes {\n\ttype mymod_button_a = button {\n\t\tsize = { @w @h }\n\t}\n}", file.NewText);
        Assert.Equal(2, plan.Entries.Count(e => e.Kind == GuiPlanEntryKind.CopyConstant));
        var entry = plan.Entries.Single(e => e.Kind == GuiPlanEntryKind.NewDefinition);
        Assert.Equal("mymod_button_a", entry.Name);
        Assert.True(entry.Line > 0);
        Assert.Null(plan.CopyReason);
    }

    [Fact]
    public void TemplateCopyStaysOnTopLevel()
    {
        var (index, source) = VanillaIndex();

        var plan = Copy(index, Target(index, source, n => n.Kind == GuiNodeKind.TemplateDefinition), "mymod_tpl_a");

        Assert.Equal("template mymod_tpl_a {\n\talpha = 1\n}\n", Assert.Single(plan.Files).NewText);
    }

    [Fact]
    public void NamedWidgetCopyBecomesATypeWithoutItsName()
    {
        var (index, source) = VanillaIndex();
        var target = Target(index, source, n => n.Kind == GuiNodeKind.KeyedBlock && n.Name == "window");

        Assert.Equal("w", GuiTypeEdits.CopyStem(target.Node));
        var text = Assert.Single(Copy(index, target, "mymod_w").Files).NewText;
        Assert.Contains("type mymod_w = window {", text);
        Assert.Contains("using = tpl_a", text);
        Assert.DoesNotContain("name = ", text);
    }

    [Fact]
    public void TakenNameBlocks()
    {
        var (index, source) = VanillaIndex();

        var plan = Copy(index, Target(index, source, n => n.Kind == GuiNodeKind.TypeDefinition && n.Name == "button_a"), "button_b");

        Assert.Equal(GuiPlanBlockerKind.NameTaken, Assert.Single(plan.Blockers).Kind);
        Assert.False(plan.CanApply);
    }

    [Fact]
    public void CopyFromADependencyTakesItsConstants()
    {
        var parent = GuiIndexTests.F("in_game/gui/parent.gui", GuiLayer.Dependency);
        var index = GuiIndexTests.Build(
            (GuiOverridePlannerTests.V("in_game/gui/shared/buttons.gui"), GuiOverridePlannerTests.Vanilla),
            (parent, "@pw = 99\ntypes Parent {\n\ttype parent_button = button {\n\t\tsize = { @pw @pw }\n\t}\n}\n"));

        var plan = Copy(index, Target(index, parent, n => n.Name == "parent_button"), "mymod_parent_button");

        var text = Assert.Single(plan.Files).NewText;
        Assert.Contains("@pw = 99", text);
        Assert.Contains("type mymod_parent_button = button {", text);
        Assert.Empty(plan.Blockers);
    }

    [Fact]
    public void CopySourceIsVanillaOrDependencyAndRebindsOpenTabs()
    {
        var (index, source) = VanillaIndex();
        var indexTarget = Target(index, source, n => n.Name == "button_a" && n.Kind == GuiNodeKind.TypeDefinition);

        // Derselbe Text in einem offenen Tab: eigener Parse, gleicher Pfad.
        var tab = GuiParser.Parse(GuiOverridePlannerTests.Vanilla);
        var tabTarget = new GuiInspectTarget(tab.Root.Descendants().First(n => n.Name == "button_a" && n.Kind == GuiNodeKind.TypeDefinition), tab, source.FullPath);
        Assert.Same(indexTarget.Node, GuiTypeEdits.CopySource(index, tabTarget, Rules)!.Node);

        // Eigenschaften und Mod-Dateien gehen nicht.
        Assert.Null(GuiTypeEdits.CopySource(index, Target(index, source, n => n.Name == "alpha"), Rules));
        var mod = GuiIndexTests.F("in_game/gui/mine.gui", GuiLayer.Mod);
        var withMod = GuiIndexTests.Build((source, GuiOverridePlannerTests.Vanilla), (mod, "types M {\n\ttype mine = button {\n\t}\n}\n"));
        Assert.Null(GuiTypeEdits.CopySource(withMod, Target(withMod, mod, n => n.Name == "mine"), Rules));
    }

    [Fact]
    public void CopyFromAVanillaFileThatADependencyReplacesUsesTheDependencyVersion()
    {
        var (_, probe) = VanillaIndex();
        var parent = GuiIndexTests.F("in_game/gui/shared/buttons.gui", GuiLayer.Dependency);
        var index = GuiIndexTests.Build((probe, GuiOverridePlannerTests.Vanilla), (parent, GuiOverridePlannerTests.Vanilla.Replace("@w = 10", "@w = 12")));
        var target = Target(index, probe, n => n.Kind == GuiNodeKind.TypeDefinition && n.Name == "button_a");

        var source = GuiTypeEdits.CopySource(index, target, Rules)!;
        Assert.Equal(GuiLayer.Dependency, source.File.Layer);
        var text = Assert.Single(Copy(index, target, "mymod_button_a").Files).NewText;
        Assert.Contains("@w = 12", text);
        Assert.DoesNotContain("@w = 10", text);

        // Fehlt das Element in der Fassung der Abhängigkeit, gibt es nichts zu übernehmen.
        var without = GuiIndexTests.Build((probe, GuiOverridePlannerTests.Vanilla), (parent, "window = { }\n"));
        Assert.Null(GuiTypeEdits.CopySource(without, Target(without, probe, n => n.Kind == GuiNodeKind.TypeDefinition && n.Name == "button_a"), Rules));
    }

    [Fact]
    public void CopyOfANestedWidgetTakesConstantsFromEnclosingBlocks()
    {
        var file = GuiOverridePlannerTests.V("in_game/gui/panel.gui");
        var index = GuiIndexTests.Build((file, "window = {\n\tname = \"panel\"\n\t@w = 5\n\tbutton = {\n\t\tname = \"ok\"\n\t\tsize = { @w @w }\n\t}\n}\n"));

        var plan = Copy(index, Target(index, file, n => n.Kind == GuiNodeKind.KeyedBlock && n.Name == "button"), "mymod_ok");

        Assert.Contains("type mymod_ok = button {\n\t\t@w = 5\n\t\tsize = { @w @w }\n\t}", Assert.Single(plan.Files).NewText);
    }

    [Fact]
    public void HoldersAreNoCopySource()
    {
        var file = GuiOverridePlannerTests.V("in_game/gui/panel.gui");
        var index = GuiIndexTests.Build((file, "window = {\n\ttooltipwidget = {\n\t\twidget = { }\n\t}\n}\n"));

        Assert.Null(GuiTypeEdits.CopySource(index, Target(index, file, n => n.Name == "tooltipwidget"), Rules));
        Assert.NotNull(GuiTypeEdits.CopySource(index, Target(index, file, n => n.Name == "widget"), Rules));
    }
}
