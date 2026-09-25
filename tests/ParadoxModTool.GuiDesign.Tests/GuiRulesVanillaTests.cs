using System.Diagnostics;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiRulesVanillaTests
{
    private static readonly Lazy<GuiIndex> VanillaIndex = new(() =>
        GuiIndex.Build(GuiSourceSet.Collect(GuiSourceSet.RootsFor(VanillaGui.InstallDirectory, Array.Empty<string>(), null))));

    private readonly ITestOutputHelper _output;

    public GuiRulesVanillaTests(ITestOutputHelper output) => _output = output;

    /// <summary>Vanilla-Index, einmal je Testlauf gebaut. Nur benutzen, wenn <see cref="VanillaGui.Exists"/>.</summary>
    internal static GuiIndex Vanilla => VanillaIndex.Value;

    internal static GuiRules ProfileRules() =>
        GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "eu5.json")));

    [Fact]
    public void Eu5ProfileHasGuiRules()
    {
        var rules = ProfileRules();

        Assert.Contains("modify_texture", rules.StructuredBlocks);
        Assert.Contains("tooltipwidget", rules.WidgetHolders);
        Assert.Contains("item", rules.LayoutHolders);
        Assert.All(rules.LayoutHolders, h => Assert.Contains(h, rules.WidgetHolders));
    }

    [Fact]
    public void StructuredBlocksNeverContainWidgetsInVanilla()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var rules = ProfileRules();
        var resolver = new GuiResolver(Vanilla, rules);
        var seen = new HashSet<string>();
        foreach (var file in Vanilla.Files)
        {
            var blocks = Vanilla.DocumentOf(file).Root.Descendants()
                .Where(n => n.Kind == GuiNodeKind.KeyedBlock && rules.StructuredBlocks.Contains(n.Name) && !resolver.IsType(n.Name));
            foreach (var node in blocks)
            {
                seen.Add(node.Name);
                foreach (var child in node.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock))
                {
                    var role = resolver.Classify(child);
                    Assert.True(role is GuiRole.ValueBlock or GuiRole.StructuredBlock, $"{file.RelativePath}:{child.Line}: {child.Name} in {node.Name} ist {role}");
                }
            }
        }

        Assert.Equal(rules.StructuredBlocks.OrderBy(s => s), seen.OrderBy(s => s));
    }

    [Fact]
    public void EveryWidgetHolderOccursInVanilla()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var rules = ProfileRules();
        var names = Vanilla.Files
            .SelectMany(f => Vanilla.DocumentOf(f).Root.Descendants())
            .Where(n => n.Kind == GuiNodeKind.KeyedBlock)
            .Select(n => n.Name)
            .ToHashSet();

        Assert.All(rules.WidgetHolders, h => Assert.Contains(h, names));
    }

    [Fact]
    public void ResolvesEveryTopLevelVanillaWidget()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var resolver = new GuiResolver(Vanilla, ProfileRules());
        var count = 0;
        var issues = 0;
        foreach (var file in Vanilla.Files)
        {
            foreach (var node in Vanilla.DocumentOf(file).Root.Children.Where(n => n.Kind == GuiNodeKind.KeyedBlock && n.Name.Length > 0))
            {
                var element = resolver.Resolve(node);
                count++;
                issues += element.Issues.Count;
                Assert.DoesNotContain(element.Issues, i => i.Kind is GuiResolveIssueKind.DepthLimit or GuiResolveIssueKind.TypeCycle);
            }
        }

        _output.WriteLine($"{count} oberste Widgets aufgelöst, {issues} Hinweise, {watch.ElapsedMilliseconds} ms");
        Assert.True(count > 100);
        Assert.Equal("ubertextbox", resolver.BaseWidgetOf("text_single"));
    }
}
