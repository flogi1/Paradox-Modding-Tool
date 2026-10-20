using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Klassische Palette (HOI IV, EU IV): nur Elementtypen in der häufigsten Schreibweise, Einfügecode mit Name und Position.</summary>
public class ClassicPaletteTests
{
    private const string Text =
        "guiTypes = { containerWindowType = { name = \"a\" background = { name = \"b\" spriteType = \"x\" } iconType = { name = \"c\" } } }";

    private static (GuiIndex Index, GuiPropertyCatalog Catalog, GuiResolver Resolver) Setup()
    {
        var rules = GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", "hoi4.json")));
        var index = GuiIndex.FromDocuments(
            new[] { GuiIndexTests.F("interface/a.gui") }, new[] { GuiParser.Parse(Text) }, rules.Layout);
        return (index, GuiCatalogLearner.Learn(index, rules, "fp"), new GuiResolver(index, rules));
    }

    [Fact]
    public void Only_element_types_in_preferred_spelling()
    {
        var (index, catalog, resolver) = Setup();

        var entries = GuiPalette.Entries(index, resolver, catalog);

        Assert.Equal(new[] { "containerWindowType", "iconType" }, entries.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.DoesNotContain(entries, e => e.Name is "guiTypes" or "background");
        Assert.All(entries, e =>
        {
            Assert.Equal(GuiPaletteKind.EngineWidget, e.Kind);
            Assert.Equal(string.Empty, e.Group);
        });
        Assert.Equal(GuiWidgetCategory.Container, entries.Single(e => e.Name == "containerWindowType").Category);
        Assert.Equal(GuiWidgetCategory.Icon, entries.Single(e => e.Name == "iconType").Category);
    }

    [Fact]
    public void Snippet_has_name_and_position()
    {
        var (index, catalog, resolver) = Setup();

        var icon = GuiPalette.Entries(index, resolver, catalog).Single(e => e.Name == "iconType");

        Assert.Equal("iconType = {\n\tname = \"new_icontype\"\n\tposition = { x = 0 y = 0 }\n}", icon.Snippet);
    }

    [Fact]
    public void Snippet_is_inserted_indented_into_a_window()
    {
        var (index, catalog, resolver) = Setup();
        var text = "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"w\"\n\t}\n}\n";
        var document = GuiParser.Parse(text);
        var window = document.Root.Descendants().Single(n => n.Name == "containerWindowType");
        var overlay = new GuiResolver(index, GuiResolverTests.Rules, document, "interface/w.gui");
        var icon = GuiPalette.Entries(index, resolver, catalog).Single(e => e.Name == "iconType");

        var at = GuiDesignerEdits.InsertionFor(window, GuiPlacement.AtSelection, 0, document, overlay)!;
        var edit = GuiDesignerEdits.Insert(document, at, icon.Snippet!);

        Assert.Equal(
            "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"w\"\n\t\ticonType = {\n\t\t\tname = \"new_icontype\"\n\t\t\tposition = { x = 0 y = 0 }\n\t\t}\n\t}\n}\n",
            TextEdit.Apply(document.Text, edit.Edits));
    }

    [Fact]
    public void Element_keys_are_lower_case()
    {
        Assert.Equal(19, ClassicPalette.ElementKeys.Count);
        Assert.All(ClassicPalette.ElementKeys, k => Assert.Equal(k.ToLowerInvariant(), k));
    }

    [Fact]
    public void Jomini_palette_unchanged()
    {
        var index = GuiIndexTests.Build((GuiIndexTests.F("gui/a.gui"), "window = { my_button = { } }"));
        var catalog = new GuiPropertyCatalog();
        catalog.Widgets["button"] = new GuiWidgetStats { Name = "button", Count = 3 };

        var entries = GuiPalette.Entries(index, new GuiResolver(index, GuiResolverTests.Rules), catalog);

        var entry = Assert.Single(entries);
        Assert.Equal("button", entry.Name);
        Assert.Equal("button = { }", entry.Snippet);
        Assert.Null(entry.ClassicSnippet);
    }
}
