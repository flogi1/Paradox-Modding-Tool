using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiConditionalKeysTests
{
    private const string FileA = @"C:\Mod\GUI\a.gui";
    private const string FileB = @"C:\Mod\GUI\b.gui";

    private static LayoutBox Layout(GuiResolver resolver, GuiNode target, string? viewedFile, IReadOnlySet<string>? hidden = null) =>
        new GuiLayoutEngine(GuiResolverTests.Rules, _ => null).Layout(
            resolver.Resolve(target),
            new LayoutOptions { ViewedFile = viewedFile, PreviewRootNode = target, HiddenConditionals = hidden ?? new HashSet<string>() });

    private const string TwoRows =
        "window = { name = \"main\" size = { 100 100 } widget = { name = \"row\" visible = \"[A]\" size = { 1 1 } } widget = { name = \"row\" visible = \"[B]\" size = { 1 1 } } }";

    [Fact]
    public void Hidden_key_is_per_file_and_node_path()
    {
        var (resolver, doc) = GuiResolverTests.Setup(TwoRows);
        var window = GuiResolverTests.Top(doc, "window");

        var inA = Layout(resolver, window, FileA).Children.Select(c => c.ConditionalKey).ToList();
        var inB = Layout(resolver, window, FileB).Children.Select(c => c.ConditionalKey).ToList();

        Assert.Equal(new[] { @"c:\mod\gui\a.gui|KeyedBlock:window:0|widget:row:0", @"c:\mod\gui\a.gui|KeyedBlock:window:0|widget:row:1" }, inA);
        Assert.NotEqual(inA[0], inB[0]);
        Assert.StartsWith(GuiConditionalKeys.FilePrefix(FileB), inB[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Hiding_the_first_same_named_sibling_keeps_the_second()
    {
        // Gezählt wird über alle Geschwister im aufgelösten Baum: der zweite behält Nummer 1, auch wenn der erste ausgeblendet ist.
        var (resolver, doc) = GuiResolverTests.Setup(TwoRows);
        var window = GuiResolverTests.Top(doc, "window");
        var first = Layout(resolver, window, FileA).Children[0].ConditionalKey!;

        var box = Layout(resolver, window, FileA, new HashSet<string> { first });

        var remaining = Assert.Single(box.Children);
        Assert.EndsWith("widget:row:1", remaining.ConditionalKey, StringComparison.Ordinal);
        Assert.Contains(first, box.ConditionalKeys.Values);
    }

    [Fact]
    public void Type_body_child_hidden_in_one_file_stays_visible_in_another()
    {
        const string library = "types T { type my_panel = widget { size = { 100 100 } widget = { name = \"cond\" visible = \"[X]\" size = { 10 10 } } } }";
        var (resolverA, docA) = GuiResolverTests.Setup("my_panel = { }", library);
        var (resolverB, docB) = GuiResolverTests.Setup("my_panel = { }", library);
        var panelA = GuiResolverTests.Top(docA, "my_panel");
        var panelB = GuiResolverTests.Top(docB, "my_panel");

        var key = Layout(resolverA, panelA, FileA).Children.Single().ConditionalKey!;
        var hidden = new HashSet<string> { key };

        Assert.Empty(Layout(resolverA, panelA, FileA, hidden).Children);
        var inB = Assert.Single(Layout(resolverB, panelB, FileB, hidden).Children);
        Assert.True(inB.Conditional);
        // Der Schlüssel hängt an der angesehenen Datei, nicht an der Herkunft des Kindes (Bibliothek).
        Assert.DoesNotContain("library", key, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(@"c:\mod\gui\a.gui|KeyedBlock:my_panel:0|widget:cond:0", key);
    }

    [Fact]
    public void File_prefix_without_file_is_only_the_separator()
    {
        Assert.Equal("|", GuiConditionalKeys.FilePrefix(null));
        Assert.Equal(@"c:\mod\gui\a.gui|", GuiConditionalKeys.FilePrefix("C:/Mod/GUI/a.gui"));
    }

    [Fact]
    public void Conditional_means_visible_with_an_expression()
    {
        var (resolver, doc) = GuiResolverTests.Setup(
            "window = { widget = { visible = \"[A]\" } widget = { visible = yes } widget = { visible = no } widget = { } }");
        var children = resolver.Resolve(GuiResolverTests.Top(doc, "window")).Children;

        Assert.Equal(new[] { true, false, false, false }, children.Select(GuiConditionalKeys.IsConditional));
    }
}
