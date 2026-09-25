using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiInspectionTests
{
    private const string Library =
        "types T {\n" +
        "\ttype base_button = button {\n" +
        "\t\tblock \"text\" { }\n" +
        "\t\tblock \"icon\" { }\n" +
        "\t}\n" +
        "\ttype my_button = base_button {\n" +
        "\t\tsize = { 1 1 }\n" +
        "\t}\n" +
        "\ttype other_button = base_button { }\n" +
        "}\n" +
        "template shiny {\n" +
        "\tblock \"glow\" { }\n" +
        "}\n";

    private const string Use =
        "window = {\n" +
        "\tbase_button = {\n" +
        "\t\tblockoverride \"text\" { }\n" +
        "\t}\n" +
        "\tbase_button = { }\n" +
        "\twidget = {\n" +
        "\t\tusing = shiny\n" +
        "\t\tblockoverride \"glow\" { }\n" +
        "\t}\n" +
        "}\n";

    private static (GuiIndex Index, GuiResolver Resolver) Setup()
    {
        var index = GuiIndexTests.Build(
            (GuiIndexTests.F("in_game/gui/library.gui"), Library),
            (GuiIndexTests.F("in_game/gui/use.gui"), Use));
        return (index, new GuiResolver(index, GuiResolverTests.Rules));
    }

    private static GuiInspectTarget UseTarget(GuiIndex index, Func<GuiDocument, GuiNode> pick)
    {
        var file = index.Files.First(f => f.RelativePath.EndsWith("use.gui", StringComparison.Ordinal));
        var document = index.DocumentOf(file);
        return new GuiInspectTarget(pick(document), document, file.FullPath);
    }

    [Fact]
    public void TypeShowsChainDerivedTypesBlocksAndUsages()
    {
        var (index, resolver) = Setup();

        var inspection = GuiInspection.Create(resolver, index, GuiInspection.TargetFor(index, "base_button", GuiDefinitionKind.Type)!);

        Assert.Equal(GuiInspectKind.Type, inspection.Kind);
        Assert.Equal("type base_button = button", inspection.Title);
        Assert.Equal("base_button", inspection.Subject);
        Assert.Equal(new[] { "base_button", "button" }, inspection.TypeChain);
        Assert.Equal(new[] { "my_button", "other_button" }, inspection.DerivedTypes.Select(d => d.Name));
        Assert.Equal(new[] { "text", "icon" }, inspection.OfferedBlocks.Select(b => b.Name));
        Assert.Single(inspection.OfferedBlocks[0].OverriddenAt);
        Assert.Empty(inspection.OfferedBlocks[1].OverriddenAt);
        Assert.Equal(2, inspection.Usages.Count);
        Assert.StartsWith("button = {", inspection.ResolvedCode);
        Assert.StartsWith("type base_button", inspection.WrittenText);
    }

    [Fact]
    public void TemplateShowsUsingsAndOverriddenBlocksWithoutResolving()
    {
        var (index, resolver) = Setup();

        var inspection = GuiInspection.Create(resolver, index, GuiInspection.TargetFor(index, "shiny", GuiDefinitionKind.Template)!);

        Assert.Equal(GuiInspectKind.Template, inspection.Kind);
        Assert.Null(inspection.Element);
        Assert.Null(inspection.ResolvedCode);
        var usage = Assert.Single(inspection.Usages);
        Assert.Equal(GuiUsageKind.Using, usage.Kind);
        Assert.Single(Assert.Single(inspection.OfferedBlocks).OverriddenAt);
        Assert.Equal("widget", GuiInspection.TargetFor(index, usage).Node.Name);
    }

    [Fact]
    public void WidgetListsItsReferencesAndInstancesShowTheirType()
    {
        var (index, resolver) = Setup();

        var window = GuiInspection.Create(resolver, index, UseTarget(index, d => d.Root.Children[0]));
        var instance = GuiInspection.Create(resolver, index, UseTarget(index, d => d.Root.Children[0].Children[0]));

        Assert.Equal(GuiInspectKind.Widget, window.Kind);
        Assert.Null(window.Subject);
        Assert.Equal(
            new[] { ("base_button", GuiDefinitionKind.Type), ("shiny", GuiDefinitionKind.Template) },
            window.References.Select(r => (r.Name, r.Kind)));
        Assert.Equal("window", window.Element!.Key);
        Assert.Equal(new[] { "window" }, window.TypeChain);
        Assert.Equal("window", GuiInspection.ContextOf(window.References[0].At));
        Assert.Equal("base_button", instance.Subject);
        Assert.Equal(2, instance.Usages.Count);
    }

    [Fact]
    public void WorksWithAnEmptyIndex()
    {
        var doc = GuiParser.Parse("window = { my_button = { } }");
        var resolver = new GuiResolver(GuiIndex.Empty, GuiRules.Empty, doc, null);

        var inspection = GuiInspection.Create(resolver, GuiIndex.Empty, new GuiInspectTarget(doc.Root.Children[0], doc, null));

        Assert.Equal("window", inspection.Element!.Key);
        Assert.Empty(inspection.Usages);
        Assert.Null(GuiInspection.TargetFor(GuiIndex.Empty, "my_button", GuiDefinitionKind.Type));
    }

    [Fact]
    public void LocalDefinitionsOfUnsavedFilesAreFoundFirst()
    {
        var (index, _) = Setup();
        var open = GuiParser.Parse("types X { type base_button = widget { } }");

        var target = GuiInspection.TargetFor(index, "base_button", GuiDefinitionKind.Type, new GuiInspectTarget(open.Root.Children[0], open, @"C:\mod\x.gui"))!;

        Assert.Same(open, target.Document);
        Assert.Equal(@"C:\mod\x.gui", target.FilePath);
    }

    [Fact]
    public void LayoutReusesTheResolvedElement()
    {
        var (resolver, doc) = GuiResolverTests.Setup("widget = { size = { 20 10 } icon = { size = { 5 5 } } }");
        var target = doc.Root.Children[0];
        var element = resolver.Resolve(target);

        var preview = GuiPreviewBuilder.Layout(element, target, doc, () => new GuiLayoutEngine(GuiResolverTests.Rules, _ => null), new LayoutOptions());

        Assert.Same(element, preview.Element);
        Assert.Same(element.Children[0], preview.Scene!.Boxes[1].Box.Element);
    }
}
