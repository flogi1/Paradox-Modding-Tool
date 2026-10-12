using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiResolverTests
{
    internal static readonly GuiRules Rules = new(
        new[] { "modify_texture", "state" },
        new[] { "tooltipwidget", "item" },
        new[] { "item" });

    internal static (GuiResolver Resolver, GuiDocument Doc) Setup(string overlay, string library = "")
    {
        var index = GuiIndexTests.Build((GuiIndexTests.F("in_game/gui/library.gui"), library));
        var doc = GuiParser.Parse(overlay);
        return (new GuiResolver(index, Rules, doc, @"C:\mod\in_game\gui\edit.gui"), doc);
    }

    internal static GuiNode Top(GuiDocument doc, string key) => doc.Root.Children.First(c => c.Name == key);

    [Fact]
    public void ClassifiesBlocksByIndexRulesAndContent()
    {
        var (resolver, doc) = Setup(
            "widget = {\n\tsize = { 1 2 }\n\tmodify_texture = { texture = \"a.dds\" }\n\ttooltipwidget = { widget = { } }\n\tmy_button = { }\n\ticon = { }\n\ttext = \"x\"\n}",
            "types T { type my_button = button { } }");
        var children = Top(doc, "widget").Children;

        Assert.Equal(
            new[] { GuiRole.ValueBlock, GuiRole.StructuredBlock, GuiRole.WidgetHolder, GuiRole.TypeInstance, GuiRole.EngineWidget, GuiRole.Property },
            children.Select(resolver.Classify));
    }

    [Fact]
    public void TypeChainEndsAtTheEngineWidget()
    {
        var (resolver, _) = Setup("", "types T {\n\ttype a = b { }\n\ttype b = button { }\n}");

        Assert.Equal(new[] { "a", "b", "button" }, resolver.TypeChain("a"));
        Assert.Equal("button", resolver.BaseWidgetOf("a"));
        Assert.Equal(new[] { "hbox" }, resolver.TypeChain("hbox"));
    }

    [Fact]
    public void InstanceOverridesTypePropertiesAndRemembersTheOverriddenOrigin()
    {
        var (resolver, doc) = Setup(
            "my_button = {\n\tsize = { 20 30 }\n}",
            "types T {\n\ttype my_button = button {\n\t\tsize = { 10 10 }\n\t\ttexture = \"gfx/b.dds\"\n\t}\n}");

        var element = resolver.Resolve(Top(doc, "my_button"));

        Assert.Equal(GuiRole.TypeInstance, element.Role);
        Assert.Equal("button", element.BaseWidget);
        var size = element.Property("size")!;
        Assert.Equal(new[] { "20", "30" }, size.Items);
        Assert.Equal(GuiOriginKind.Here, size.Origin.Kind);
        Assert.Equal(@"C:\mod\in_game\gui\edit.gui", size.Origin.FilePath);
        var overridden = Assert.Single(size.Overridden);
        Assert.Equal(GuiOriginKind.Type, overridden.Kind);
        Assert.Equal("my_button", overridden.Definition);
        Assert.Equal(@"C:\test\Vanilla\in_game\gui\library.gui", overridden.FilePath);
        var texture = element.Property("texture")!;
        Assert.Equal("gfx/b.dds", texture.Value);
        Assert.Equal(GuiOriginKind.Type, texture.Origin.Kind);
    }

    [Fact]
    public void UsingInsertsTemplateContentWithTemplateOrigin()
    {
        var (resolver, doc) = Setup(
            "widget = {\n\tusing = my_template\n\talpha = 0.5\n}",
            "template my_template {\n\talpha = 1\n\tsize = { 5 5 }\n}");

        var element = resolver.Resolve(Top(doc, "widget"));

        Assert.Equal("0.5", element.Property("alpha")!.Value);
        Assert.Equal(GuiOriginKind.Template, element.Property("size")!.Origin.Kind);
        Assert.Equal("my_template", element.Property("size")!.Origin.Definition);
    }

    [Fact]
    public void OutermostBlockOverrideWins()
    {
        var (resolver, doc) = Setup(
            "derived = {\n\tblockoverride \"title\" {\n\t\ttext = \"instance\"\n\t}\n}",
            "types T {\n" +
            "\ttype base_type = widget {\n\t\ttext_single = {\n\t\t\tblock \"title\" {\n\t\t\t\ttext = \"base\"\n\t\t\t}\n\t\t}\n\t}\n" +
            "\ttype derived = base_type {\n\t\tblockoverride \"title\" {\n\t\t\ttext = \"derived\"\n\t\t}\n\t}\n" +
            "}");

        var element = resolver.Resolve(Top(doc, "derived"));

        var text = Assert.Single(element.Children);
        Assert.Equal("text_single", text.Key);
        Assert.Equal("instance", text.Property("text")!.Value);
        Assert.Equal(GuiOriginKind.BlockOverride, text.Property("text")!.Origin.Kind);
        Assert.Equal(GuiOriginKind.Type, text.Origin.Kind);
        Assert.Equal("base_type", text.Origin.Definition);
        Assert.Contains("title", element.AllOfferedBlocks());
    }

    [Fact]
    public void BlockWithoutOverrideKeepsItsDefaultContent()
    {
        var (resolver, doc) = Setup(
            "base_type = { }",
            "types T { type base_type = widget { block \"x\" { alpha = 0.3 } } }");

        Assert.Equal("0.3", resolver.Resolve(Top(doc, "base_type")).Property("alpha")!.Value);
    }

    [Fact]
    public void StructuredBlocksAccumulateInsteadOfOverriding()
    {
        var (resolver, doc) = Setup("icon = {\n\tstate = { name = a }\n\tstate = { name = b }\n}");

        var element = resolver.Resolve(Top(doc, "icon"));

        Assert.Equal(2, element.StructuredBlocks("state").Count());
        Assert.Equal("name = b", element.StructuredBlocks("state").Last().Value);
    }

    [Fact]
    public void ConstantsAreFileLocalAndInlineMathIsEvaluated()
    {
        var (resolver, doc) = Setup(
            "@w = 50\nmy_box = { position = { @w 0 } }",
            "@w = 100\ntypes T { type my_box = widget { size = { @[w * 2] @missing } } }");

        var element = resolver.Resolve(Top(doc, "my_box"));

        Assert.Equal(new[] { "200", "@missing" }, element.Property("size")!.Items);
        Assert.True(element.Property("size")!.Unresolved);
        Assert.Equal(new[] { "50", "0" }, element.Property("position")!.Items);
        Assert.Contains(element.Issues, i => i.Kind == GuiResolveIssueKind.MissingConstant && i.Detail == "missing");
    }

    [Fact]
    public void TypeNamedAfterItsBaseSetsEngineDefaults()
    {
        var (resolver, doc) = Setup("icon = { size = { 1 1 } }", "types D { type icon = icon { alpha = 0.5 } }");

        var element = resolver.Resolve(Top(doc, "icon"));

        Assert.Equal(new[] { "icon", "icon" }, element.TypeChain);
        Assert.Equal("icon", element.BaseWidget);
        Assert.Equal("0.5", element.Property("alpha")!.Value);
        Assert.Equal(GuiOriginKind.Type, element.Property("alpha")!.Origin.Kind);
        Assert.Empty(element.Issues);
    }

    [Fact]
    public void BlockOverrideThatRedeclaresItsBlockUsesTheInnerDefault()
    {
        var (resolver, doc) = Setup(
            "t = {\n\tblockoverride \"x\" {\n\t\tblock \"x\" { alpha = 2 }\n\t\tsize = { 1 1 }\n\t}\n}",
            "types T { type t = widget { block \"x\" { alpha = 1 } } }");

        var element = resolver.Resolve(Top(doc, "t"));

        Assert.Equal("2", element.Property("alpha")!.Value);
        Assert.NotNull(element.Property("size"));
    }

    [Fact]
    public void TypeCycleIsReportedAndStops()
    {
        var (resolver, doc) = Setup("a = { }", "types T {\n\ttype a = b { }\n\ttype b = a { }\n}");

        var element = resolver.Resolve(Top(doc, "a"));

        Assert.Contains(element.Issues, i => i.Kind == GuiResolveIssueKind.TypeCycle);
    }

    [Fact]
    public void SameTypeNestedInTheFile_IsNoCycle()
    {
        // vbox in vbox: in EU V ist vbox ein Typ (type vbox = ubervbox); zwei geschachtelte Instanzen in der Datei sind keine Schleife.
        var (resolver, doc) = Setup(
            "box = {\n\talpha = 1\n\tbox = {\n\t\talpha = 2\n\t\tvisible = \"[GetPlayer.IsValid]\"\n\t\tbox = { alpha = 3 }\n\t}\n}",
            "types T { type box = widget { } }");

        var element = resolver.Resolve(Top(doc, "box"));
        var inner = Assert.Single(element.Children);

        Assert.DoesNotContain(element.Issues.Concat(inner.Issues), i => i.Kind == GuiResolveIssueKind.TypeCycle);
        Assert.Equal("2", inner.Property("alpha")!.Value);
        Assert.NotNull(inner.Property("visible"));
        Assert.Equal("3", Assert.Single(inner.Children).Property("alpha")!.Value);
    }

    [Fact]
    public void SelfContainingTypeStopsImmediately()
    {
        var (resolver, doc) = Setup("types M { type a = widget { a = { } a = { } } }\na = { }");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var element = resolver.Resolve(Top(doc, "a"));

        // Auffanggrenze gegen grobe Rückschritte: das Release-Werkzeug und test.bat fahren alle Testprojekte gleichzeitig, dann dauert dasselbe mehrfach so lang.
        Assert.True(watch.ElapsedMilliseconds < 10000);
        Assert.Contains(element.Issues.Concat(element.Children.SelectMany(c => c.Issues)), i => i.Kind == GuiResolveIssueKind.TypeCycle);
    }

    [Fact]
    public void MissingTemplateAndTemplateCycleAreReported()
    {
        var (resolver, doc) = Setup(
            "widget = { using = nowhere using = loop_a }",
            "template loop_a { using = loop_b }\ntemplate loop_b { using = loop_a }");

        var element = resolver.Resolve(Top(doc, "widget"));

        Assert.Contains(element.Issues, i => i.Kind == GuiResolveIssueKind.MissingTemplate && i.Detail == "nowhere");
        Assert.Contains(element.Issues, i => i.Kind == GuiResolveIssueKind.TemplateCycle);
    }

    [Fact]
    public void OverlayDefinitionsWinOverTheIndex()
    {
        var (resolver, doc) = Setup(
            "types M { type x = hbox { } }\nx = { }",
            "types T { type x = vbox { } }");

        Assert.Equal("hbox", resolver.BaseWidgetOf("x"));
        Assert.Equal("hbox", resolver.Resolve(Top(doc, "x")).BaseWidget);
    }

    [Fact]
    public void WidgetHolderBecomesChildWithItsWidgets()
    {
        var (resolver, doc) = Setup("button = {\n\ttooltipwidget = {\n\t\twidget = { size = { 1 1 } }\n\t}\n}");

        var holder = Assert.Single(resolver.Resolve(Top(doc, "button")).Children);

        Assert.Equal(GuiRole.WidgetHolder, holder.Role);
        Assert.Equal("widget", Assert.Single(holder.Children).Key);
    }

    [Fact]
    public void NameReadsTheNameProperty()
    {
        var (resolver, doc) = Setup("window = { name = \"main\" }");

        Assert.Equal("main", resolver.Resolve(Top(doc, "window")).Name);
    }
}
