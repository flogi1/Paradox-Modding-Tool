using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiDraftTests
{
    [Fact]
    public void DraftTakesTheModVersionIndentedLikeVanilla()
    {
        var (request, index) = GuiOverridePlannerTests.Setup(string.Empty, "game", ("in_game/gui/mymod_overrides.gui", GuiOverridePlannerTests.ModOverrides));

        var start = GuiDraft.Start(request.Source, GuiOverridePlannerTests.Vanilla, index);

        Assert.Null(start.ModCopy);
        Assert.Equal(new[] { "button_b" }, start.Substituted);
        Assert.Equal(
            GuiOverridePlannerTests.Vanilla.Replace("\t\tsize = { 20 20 }", "\t\tsize = { 25 25 }"),
            start.Text);
    }

    [Fact]
    public void ModCopyOfTheWholeFileReplacesTheDraft()
    {
        var (request, index) = GuiOverridePlannerTests.Setup(string.Empty, "game", ("in_game/gui/shared/buttons.gui", "window = { }"));

        var start = GuiDraft.Start(request.Source, GuiOverridePlannerTests.Vanilla, index);

        Assert.Equal("in_game/gui/shared/buttons.gui", start.ModCopy!.RelativePath);
        Assert.Equal(GuiLayer.Mod, start.ModCopy.Layer);
        Assert.Empty(start.Substituted);
    }

    [Fact]
    public void DefinitionsAreTheOutermostTypesAndTemplates()
    {
        var document = GuiParser.Parse("types T {\n\ttype a = b {\n\t}\n}\ntemplate t {\n}\nwindow = { }");

        Assert.Equal(new[] { "a", "t" }, GuiDraft.Definitions(document).Select(n => n.Name));
        Assert.Equal(GuiDefinitionKind.Template, GuiDraft.KindOf(GuiDraft.Definitions(document)[1]));
    }

    [Fact]
    public void DependencyCopyOfTheWholeFileIsTheDraftBase()
    {
        var (request, index) = GuiOverridePlannerTests.WithParentCopy();

        var start = GuiDraft.Start(request.Source, GuiOverridePlannerTests.Vanilla, index);

        Assert.Null(start.ModCopy);
        Assert.Equal(GuiLayer.Dependency, start.DependencyCopy!.Layer);
        Assert.Equal(GuiOverridePlannerTests.ParentCopy, start.Text);
        Assert.Equal(GuiOverridePlannerTests.ParentCopy, GuiDraft.BaseText(request.Source, GuiOverridePlannerTests.Vanilla, index));
    }

    [Fact]
    public void ModVersionsAreSubstitutedOverTheDependencyBase()
    {
        var (request, index) = GuiOverridePlannerTests.WithParentCopy(null, ("in_game/gui/mymod_overrides.gui", GuiOverridePlannerTests.ModOverrides));

        var start = GuiDraft.Start(request.Source, GuiOverridePlannerTests.Vanilla, index);

        Assert.Equal(new[] { "button_b" }, start.Substituted);
        Assert.Equal(GuiOverridePlannerTests.ParentCopy.Replace("\t\tsize = { 20 20 }", "\t\tsize = { 25 25 }"), start.Text);
    }

    [Fact]
    public void LineInCopyFindsTheSameElementOrDefinition()
    {
        var vanilla = GuiParser.Parse(GuiOverridePlannerTests.Vanilla);
        int At(string text) => GuiOverridePlannerTests.Vanilla.IndexOf(text, StringComparison.Ordinal);

        // Gleicher Pfad: zwei Zeilen weiter unten.
        var shifted = GuiParser.Parse("# a\n# b\n" + GuiOverridePlannerTests.Vanilla);
        Assert.Equal(14, GuiDraft.LineInCopy(vanilla, At("alpha = 1"), shifted));

        // Anderer Container, gleichnamige Definition.
        var other = GuiParser.Parse("types Other {\n\ttype button_b = button {\n\t\tsize = { 30 30 }\n\t}\n}\n");
        Assert.Equal(2, GuiDraft.LineInCopy(vanilla, At("size = { 20 20 }"), other));

        // Nichts Passendes: Zeile 1.
        Assert.Equal(1, GuiDraft.LineInCopy(vanilla, At("button_a = { }"), other));
    }
}
