using System.Diagnostics;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiPreviewTests
{
    private readonly ITestOutputHelper _output;

    public GuiPreviewTests(ITestOutputHelper output) => _output = output;

    private static GuiPreview Build(GuiResolver resolver, GuiDocument doc, GuiNode target, LayoutOptions? options = null) =>
        GuiPreviewBuilder.Build(resolver, () => new GuiLayoutEngine(GuiResolverTests.Rules, _ => null), doc, target, options ?? new LayoutOptions());

    [Fact]
    public void PreviewTargetIsTheEnclosingDefinitionOrTopLevelWidget()
    {
        var doc = GuiParser.Parse("types M { type t = widget { icon = { size = { 1 1 } } } }\nwindow = { hbox = { icon = { } } }");

        Assert.Equal("t", GuiPreviewBuilder.PreviewTarget(doc.Root.Descendants().First(n => n.Name == "size"))!.Name);
        Assert.Equal("window", GuiPreviewBuilder.PreviewTarget(doc.Root.Descendants().Last(n => n.Name == "icon"))!.Name);
        Assert.Null(GuiPreviewBuilder.PreviewTarget(doc.Root.Children[0]));
    }

    [Fact]
    public void BuildsSceneForATypeDefinitionAndMapsClicksBack()
    {
        var (resolver, doc) = GuiResolverTests.Setup(
            "types M {\n\ttype panel = widget {\n\t\tsize = { 100 50 }\n\t\ticon = { size = { 10 10 } position = { 5 5 } }\n\t}\n}");
        var target = doc.Root.Descendants().First(n => n.Kind == GuiNodeKind.TypeDefinition);

        var preview = Build(resolver, doc, target);

        var scene = preview.Scene!;
        Assert.Equal(2, scene.Boxes.Count);
        Assert.Equal(100, scene.Width, 3);
        var icon = doc.Root.Descendants().First(n => n.Name == "icon");
        Assert.Same(icon, scene.SelectableNode(scene.HitTest(7, 7)!));
        Assert.Same(target, scene.SelectableNode(scene.HitTest(50, 40)!));
        Assert.Same(preview.Element, preview.ElementFor(target));
        Assert.Equal("icon", preview.ElementFor(icon)!.Key);
        Assert.Null(scene.HitTest(500, 500));
    }

    [Fact]
    public void ClicksOnElementsFromAnotherFileSelectTheNearestElementOfThisFile()
    {
        var (resolver, doc) = GuiResolverTests.Setup(
            "window = {\n\tsize = { 200 100 }\n\tpanel = { }\n}",
            "types T { type panel = widget { size = { 50 50 } icon = { size = { 10 10 } } } }");
        var window = doc.Root.Children[0];

        var preview = Build(resolver, doc, window);

        var hit = preview.Scene!.HitTest(2, 2)!;
        Assert.Equal("icon", hit.Element.Key);
        Assert.Equal("panel", preview.Scene.SelectableNode(hit)!.Name);
    }

    [Fact]
    public void RepeatedGridItemsShareTheirNode()
    {
        var (resolver, doc) = GuiResolverTests.Setup("fixedgridbox = { addcolumn = 10 addrow = 10 item = { widget = { size = { 10 10 } } } }");

        var preview = Build(resolver, doc, doc.Root.Children[0], new LayoutOptions { ListSampleCount = 3 });

        var widget = doc.Root.Descendants().First(n => n.Name == "widget");
        Assert.Equal(3, preview.Scene!.BoxesFor(widget).Count);
    }

    [Fact]
    public void TemplatesHaveNoPreview()
    {
        var (resolver, doc) = GuiResolverTests.Setup("template t { size = { 1 1 } }");

        var preview = Build(resolver, doc, doc.Root.Children[0]);

        Assert.Null(preview.Scene);
        Assert.NotNull(preview.Message);
    }

    [Fact]
    public void VanillaPreviewsStayFiniteAndReportTheSlowest()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var index = GuiRulesVanillaTests.Vanilla;
        var rules = GuiRulesVanillaTests.ProfileRules();
        var locator = new GuiTextureLocator(GuiTextureLocator.ContextRootsFor(VanillaGui.InstallDirectory, Array.Empty<string>(), null));
        var slowest = (Ms: 0L, Name: string.Empty);
        var count = 0;
        foreach (var file in index.Files)
        {
            var doc = index.DocumentOf(file);
            var resolver = new GuiResolver(index, rules, doc, file.FullPath);
            var targets = doc.Root.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name.Length > 0)
                .Concat(doc.Root.Descendants().Where(n => n.Kind == GuiNodeKind.TypeDefinition).Where((_, i) => i % 5 == 0));
            foreach (var target in targets)
            {
                var watch = Stopwatch.StartNew();
                var preview = GuiPreviewBuilder.Build(resolver, () => new GuiLayoutEngine(rules, locator.SizeOf), doc, target, new LayoutOptions());
                watch.Stop();
                count++;
                if (watch.ElapsedMilliseconds > slowest.Ms)
                {
                    slowest = (watch.ElapsedMilliseconds, $"{file.RelativePath}:{target.Line} {target.Name}");
                }

                if (preview.Scene is { } scene)
                {
                    Assert.All(scene.Boxes, b => Assert.True(double.IsFinite(b.Box.X) && double.IsFinite(b.Box.Width)));
                }
            }
        }

        _output.WriteLine($"{count} Vorschauen, langsamste {slowest.Ms} ms: {slowest.Name}");
    }
}
