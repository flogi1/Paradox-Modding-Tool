using System.Diagnostics;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiPaletteTests
{
    private readonly ITestOutputHelper _output;

    public GuiPaletteTests(ITestOutputHelper output) => _output = output;

    private static (GuiIndex Index, GuiResolver Resolver, GuiPropertyCatalog Catalog) Setup()
    {
        var index = GuiIndexTests.Build(
            (GuiIndexTests.F("in_game/gui/library.gui"),
                "types T {\n\ttype hbox = uberhbox { }\n\ttype my_button = button { }\n}\ntemplate shiny { }\n" +
                "window = {\n\thbox = { my_button = { } }\n\tmy_button = { using = shiny }\n}"),
            (GuiIndexTests.F("gui/mod_types.gui", GuiLayer.Mod), "types M { type mod_button = my_button { } }"));
        var catalog = new GuiPropertyCatalog();
        catalog.Widgets["uberhbox"] = new GuiWidgetStats { Name = "uberhbox", Count = 50 };
        catalog.Widgets["button"] = new GuiWidgetStats { Name = "button", Count = 30 };
        catalog.Widgets["icon"] = new GuiWidgetStats { Name = "icon", Count = 20 };
        return (index, new GuiResolver(index, GuiResolverTests.Rules), catalog);
    }

    [Fact]
    public void EngineWidgetsFirstThenModFilesThenVanillaFiles()
    {
        var (index, resolver, catalog) = Setup();

        var entries = GuiPalette.Entries(index, resolver, catalog);

        var engine = entries.TakeWhile(e => e.Group.Length == 0).ToList();
        Assert.Equal(new[] { "hbox", "button", "icon" }, engine.Select(e => e.Name));
        Assert.All(engine, e => Assert.Equal(GuiPaletteKind.EngineWidget, e.Kind));
        Assert.Equal(50, engine[0].Usages);
        Assert.NotNull(engine[0].Definition);
        Assert.Null(engine[2].Definition);
        Assert.Equal("hbox = { }", engine[0].Snippet);

        var files = entries.Skip(engine.Count).ToList();
        Assert.Equal(new[] { "mod_button", "my_button", "shiny" }, files.Select(e => e.Name));
        Assert.Equal("gui/mod_types.gui", files[0].Group);
        Assert.True(files[0].IsMod);
        var myButton = files[1];
        Assert.Equal(GuiPaletteKind.Type, myButton.Kind);
        Assert.Equal(2, myButton.Usages);
        Assert.Equal(GuiWidgetCategory.Button, myButton.Category);
        Assert.Equal("my_button = { }", myButton.Snippet);
        var shiny = files[2];
        Assert.Equal(GuiPaletteKind.Template, shiny.Kind);
        Assert.Equal(1, shiny.Usages);
        Assert.Null(shiny.Snippet);

        Assert.True(GuiPalette.Matches(myButton, "BUTTON"));
        Assert.False(GuiPalette.Matches(shiny, "button"));
        Assert.True(GuiPalette.Matches(shiny, string.Empty));
    }

    [Fact]
    public void ScopeNamesTheEnclosingDefinitionAndItsUsages()
    {
        var index = GuiIndexTests.Build(
            (GuiIndexTests.F("in_game/gui/a.gui"), "window = { my_button = { } my_button = { } x = { using = shiny } }"),
            (GuiIndexTests.F("gui/m.gui", GuiLayer.Mod), "types M { type my_button = widget { icon = { } } }\ntemplate shiny { text_single = { } }"));
        var mod = index.DocumentOf(index.Files.First(f => f.Layer == GuiLayer.Mod));
        var vanilla = index.DocumentOf(index.Files.First(f => f.Layer == GuiLayer.Vanilla));

        var inType = GuiEditScope.Of(mod.Root.Descendants().First(n => n.Name == "icon"), index);
        var inTemplate = GuiEditScope.Of(mod.Root.Descendants().First(n => n.Name == "text_single"), index);
        var topLevel = GuiEditScope.Of(vanilla.Root.Children[0], index);

        Assert.Equal("type my_button = widget", inType.Label);
        Assert.Equal(GuiDefinitionKind.Type, inType.Kind);
        Assert.Equal(2, inType.Usages);
        Assert.Equal("template shiny", inTemplate.Label);
        Assert.Equal(1, inTemplate.Usages);
        Assert.Null(topLevel.Label);
        Assert.Equal(0, topLevel.Usages);
    }

    [Fact]
    public void EditabilityNamesTheReason()
    {
        var dependencies = new[] { @"C:\mods\cmf" };

        Assert.Equal(GuiReadOnlyReason.Vanilla, GuiEditability.ReasonFor(@"C:\game\in_game\gui\x.gui", true, false, dependencies));
        Assert.Equal(GuiReadOnlyReason.Dependency, GuiEditability.ReasonFor(@"C:\mods\cmf\gui\x.gui", false, false, dependencies));
        Assert.Equal(GuiReadOnlyReason.Locked, GuiEditability.ReasonFor(@"C:\mods\mine\gui\x.gui", false, true, dependencies));
        Assert.Null(GuiEditability.ReasonFor(@"C:\mods\mine\gui\x.gui", false, false, dependencies));
        Assert.Null(GuiEditability.ReasonFor(@"C:\mods\cmf2\gui\x.gui", false, false, dependencies));
        Assert.Null(GuiEditability.ReasonFor(null, false, false, dependencies));
    }

    [Fact]
    public void BlockEntries_HaveNoSnippetAndSkipBuiltInAndBroken()
    {
        var blocks = new List<Blocks.GuiBlock>(new Blocks.GuiBlockLibrary(null, null, null).Effective)
        {
            Blocks.GuiBlockParser.Parse("# block: Knopf\nbutton = { }\n", Blocks.GuiBlockLayer.Game, @"C:\x\Knopf.guiblock"),
            Blocks.GuiBlockParser.Parse("# block: Fenster\nwindow = { }\n", Blocks.GuiBlockLayer.Shipped, @"C:\x\Fenster.guiblock"),
            Blocks.GuiBlockParser.Parse("# block: Kaputt\nx = $q$\n", Blocks.GuiBlockLayer.Game, @"C:\x\Kaputt.guiblock"),
        };

        var entries = GuiPalette.BlockEntries(blocks);

        Assert.Equal(new[] { "Knopf", "Fenster" }, entries.Select(e => e.Name));
        Assert.All(entries, e =>
        {
            Assert.Equal(GuiPaletteKind.Block, e.Kind);
            Assert.Null(e.Snippet);
            Assert.Equal("Bausteine", e.Group);
            Assert.Equal(GuiTexts.PaletteBlocksGroup, e.Group);
            Assert.NotNull(e.Block);
            Assert.Null(e.Definition);
        });
        Assert.Equal(GuiLayer.Mod, entries[0].Layer);
        Assert.Equal(GuiLayer.Vanilla, entries[1].Layer);
    }

    [Fact]
    public void VanillaPaletteReportsItsDuration()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var index = GuiRulesVanillaTests.Vanilla;
        var rules = GuiRulesVanillaTests.ProfileRules();
        var resolver = new GuiResolver(index, rules);
        var catalog = GuiCatalogLearner.Learn(index, rules, "test");
        var watch = Stopwatch.StartNew();

        var entries = GuiPalette.Entries(index, resolver, catalog);

        _output.WriteLine($"Palette {entries.Count} Einträge in {watch.ElapsedMilliseconds} ms, davon {entries.Count(e => e.Group.Length == 0)} Engine-Widgets");
        Assert.Contains(entries, e => e.Name == "hbox" && e.Group.Length == 0);
        Assert.Contains(entries, e => e.Kind == GuiPaletteKind.Template);
    }
}
