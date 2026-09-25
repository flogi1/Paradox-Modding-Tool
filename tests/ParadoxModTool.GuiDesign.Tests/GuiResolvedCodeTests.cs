using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiResolvedCodeTests
{
    private const string Overlay =
        "my_button = {\n\tname = \"b\"\n\tblockoverride \"label\" {\n\t\ttext_single = { text = \"hi\" }\n\t}\n\ttooltipwidget = { widget = { size = { 5 5 } } }\n}";

    private const string Library =
        "types T {\n\ttype my_button = button {\n\t\tsize = { 10 20 }\n\t\tusing = shiny\n\t\tblock \"label\" { }\n\t}\n}\n" +
        "template shiny {\n\tmodify_texture = {\n\t\ttexture = \"gfx/a.dds\"\n\t\t# glanz\n\t\tblend_mode = overlay\n\t}\n}";

    [Fact]
    public void WritesTheResolvedElementWithoutReferences()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Overlay, Library);

        var code = GuiResolvedCode.Write(resolver.Resolve(GuiResolverTests.Top(doc, "my_button")));

        Assert.Equal(
            "button = { # my_button → button\n" +
            "\tsize = { 10 20 } # my_button (library.gui:3)\n" +
            "\tmodify_texture = { # shiny (library.gui:9)\n" +
            "\t\ttexture = \"gfx/a.dds\"\n" +
            "\t\t# glanz\n" +
            "\t\tblend_mode = overlay\n" +
            "\t}\n" +
            "\tname = \"b\"\n" +
            "\ttext_single = {\n" +
            "\t\ttext = \"hi\" # label (edit.gui:4)\n" +
            "\t}\n" +
            "\ttooltipwidget = {\n" +
            "\t\twidget = {\n" +
            "\t\t\tsize = { 5 5 }\n" +
            "\t\t}\n" +
            "\t}\n" +
            "}\n",
            code);
    }

    [Fact]
    public void OutputParsesWithoutDiagnosticsAndCanOmitOrigins()
    {
        var (resolver, doc) = GuiResolverTests.Setup(Overlay, Library);
        var element = resolver.Resolve(GuiResolverTests.Top(doc, "my_button"));

        var plain = GuiResolvedCode.Write(element, withOrigins: false);

        Assert.DoesNotContain("library.gui", plain);
        Assert.Contains("# my_button → button", plain);
        Assert.Empty(GuiParser.Parse(plain).Diagnostics);
        Assert.Empty(GuiParser.Parse(GuiResolvedCode.Write(element)).Diagnostics);
    }

    [Fact]
    public void VanillaResolvedCodeParses()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var index = GuiRulesVanillaTests.Vanilla;
        var rules = GuiRulesVanillaTests.ProfileRules();
        var written = 0;
        var files = index.Files.Where((file, i) => i % 10 == 0 && index.DocumentOf(file).Diagnostics.Count == 0);
        foreach (var file in files)
        {
            var doc = index.DocumentOf(file);
            var resolver = new GuiResolver(index, rules, doc, file.FullPath);
            foreach (var top in doc.Root.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name.Length > 0))
            {
                var code = GuiResolvedCode.Write(resolver.Resolve(top));
                Assert.True(GuiParser.Parse(code).Diagnostics.Count == 0, $"{file.RelativePath}:{top.Line}");
                written++;
            }
        }

        Assert.True(written > 0);
    }
}
