using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Klassischer Katalog (HOI IV, EU IV): Schlüssel nach Kleinschreibung gefaltet, Schreibweisen, Paar-Schlüssel und Anführungszeichen gezählt.</summary>
public class ClassicCatalogTests
{
    private static GuiRules RulesOf(string id) =>
        GuiRules.FromProfile(GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", id + ".json")));

    private static GuiIndex IndexOf(GuiRules rules, params string[] texts) =>
        GuiIndex.FromDocuments(
            texts.Select((_, i) => GuiIndexTests.F($"interface/file{i}.gui")).ToList(),
            texts.Select(GuiParser.Parse).ToList(),
            rules.Layout);

    private static GuiPropertyCatalog LearnClassic(params string[] texts)
    {
        var rules = RulesOf("hoi4");
        return GuiCatalogLearner.Learn(IndexOf(rules, texts), rules, "fp");
    }

    private const string ThreeIcons =
        "guiTypes = {\n"
        + "\tcontainerWindowType = {\n\t\tname = \"win\"\n"
        + "\t\ticonType = { name = \"a\" Orientation = \"UPPER_LEFT\" }\n"
        + "\t\ticonType = { name = \"b\" Orientation = \"UPPER_LEFT\" }\n"
        + "\t\ticonType = { name = \"c\" orientation = upper_left }\n"
        + "\t}\n}\n";

    [Fact]
    public void Folds_case_and_counts_spellings()
    {
        var catalog = LearnClassic(ThreeIcons);

        var property = catalog.Property(GuiPropertyCatalog.ClassicKey("iconType"), "orientation")!;
        Assert.Equal(3, property.Count);
        Assert.Equal("Orientation", property.PreferredSpelling);
        Assert.Equal(2, property.Spellings["Orientation"]);
        Assert.Equal(1, property.Spellings["orientation"]);
        Assert.Equal(2, property.Quoted);
        Assert.Null(catalog.Property("icontype", "Orientation"));
        Assert.True(catalog.Classic);
    }

    [Fact]
    public void Folds_widget_key_case()
    {
        var catalog = LearnClassic(
            "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"w\"\n"
            + "\t\tgridBoxType = { name = \"a\" }\n\t\tgridBoxType = { name = \"b\" }\n\t\tgridboxtype = { name = \"c\" }\n\t}\n}\n");

        var widget = catalog.Widgets["gridboxtype"];
        Assert.Equal(3, widget.Count);
        Assert.Equal("gridBoxType", widget.PreferredSpelling);
        Assert.Equal(2, widget.Spellings["gridBoxType"]);
        Assert.DoesNotContain("gridBoxType", catalog.Widgets.Keys);
    }

    [Fact]
    public void Records_pair_keys()
    {
        var catalog = LearnClassic(
            "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"w\"\n"
            + "\t\ticonType = { name = \"a\" size = { width = 10 height = 20 } }\n"
            + "\t\ticonType = { name = \"b\" size = { Width = 11 Height = 21 } }\n"
            + "\t\ticonType = { name = \"c\" size = { x = 1 y = 2 } }\n\t}\n}\n");

        var size = catalog.Property("icontype", "size")!;
        Assert.Equal(3, size.Count);
        Assert.Equal(2, size.PairKeys["width,height"]);
        Assert.Equal(1, size.PairKeys["x,y"]);
    }

    [Fact]
    public void GuiTypes_container_is_not_a_widget()
    {
        var catalog = LearnClassic(
            ThreeIcons + "spriteTypes = {\n\tspriteType = { name = \"GFX_a\" texturefile = \"gfx/a.dds\" }\n}\n");

        Assert.DoesNotContain("guitypes", catalog.Widgets.Keys);
        Assert.DoesNotContain("spritetypes", catalog.Widgets.Keys);
        Assert.Contains("containerwindowtype", catalog.Widgets.Keys);
        Assert.Contains("spritetype", catalog.Widgets.Keys);
    }

    [Fact]
    public void Unknown_block_without_name_is_not_a_widget()
    {
        var catalog = LearnClassic(
            "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"w\"\n\t\tmyThing = { foo = 1 }\n\t\tmyNamed = { name = \"n\" }\n\t}\n}\n");

        Assert.DoesNotContain("mything", catalog.Widgets.Keys);
        Assert.Contains("mynamed", catalog.Widgets.Keys);
    }

    [Fact]
    public void Jomini_learning_unchanged()
    {
        var rules = RulesOf("eu5");
        var index = IndexOf(
            rules,
            "window = {\n\tname = \"w\"\n\tVisible = yes\n\tvisible = no\n\tsize = { 10 20 }\n\ttext = \"abc\"\n\twidget = { name = \"inner\" Size = { 1 2 } }\n}\n");

        var catalog = GuiCatalogLearner.Learn(index, rules, "fp");

        Assert.False(catalog.Classic);
        Assert.Equal(new[] { "widget", "window" }, catalog.Widgets.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "Size", "Visible", "name", "size", "text", "visible" },
            catalog.Widgets["window"].Properties.Keys.Concat(catalog.Widgets["widget"].Properties.Keys).Distinct().OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(1, catalog.Property("window", "Visible")!.Count);
        Assert.Equal("Visible", catalog.Property("window", "Visible")!.PreferredSpelling);
        Assert.Equal(1, catalog.Property("window", "text")!.Quoted);
    }

    [Fact]
    public void Round_trips_through_store()
    {
        var catalog = LearnClassic(
            ThreeIcons.Replace("}\n}\n", "\t\ticonType = { name = \"d\" size = { width = 1 height = 2 } }\n\t}\n}\n"));
        var path = Path.Combine(Path.GetTempPath(), "pmt-classic-catalog-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            GuiCatalogStore.Save(path, catalog);
            var loaded = GuiCatalogStore.Load(path, "fp")!;

            Assert.True(loaded.Classic);
            Assert.Equal(GuiPropertyCatalog.CurrentVersion, loaded.Version);
            var orientation = loaded.Property("icontype", "orientation")!;
            Assert.Equal(2, orientation.Spellings["Orientation"]);
            Assert.Equal("Orientation", orientation.PreferredSpelling);
            Assert.Equal(2, orientation.Quoted);
            Assert.Equal(1, loaded.Property("icontype", "size")!.PairKeys["width,height"]);
            Assert.Equal("iconType", loaded.Widgets["icontype"].PreferredSpelling);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Catalog_version_is_three()
    {
        Assert.Equal(3, GuiPropertyCatalog.CurrentVersion);
    }

    [Fact]
    public void Preferred_spelling_tie_takes_ordinal_first_and_falls_back_to_name()
    {
        var tie = new GuiPropertyStats { Name = "orientation", Spellings = { ["orientation"] = 1, ["Orientation"] = 1 } };
        Assert.Equal("Orientation", tie.PreferredSpelling);
        Assert.Equal("size", new GuiPropertyStats { Name = "size" }.PreferredSpelling);
        Assert.Equal("iconType", new GuiWidgetStats { Name = "iconType" }.PreferredSpelling);
    }

    [Fact]
    public void Existing_readers_find_folded_keys()
    {
        var rules = RulesOf("hoi4");
        var index = IndexOf(rules, ThreeIcons + "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"other\"\n\t\ticonType = { name = \"x\" }\n\t}\n}\n");
        var catalog = GuiCatalogLearner.Learn(index, rules, "fp");
        var resolver = new GuiResolver(index, rules);
        var icon = index.Find("x", GuiDefinitionKind.NamedWidget)!;
        var element = resolver.Resolve(icon.Node);

        Assert.Contains("Orientation", GuiPropertyInput.AddableProperties(element, catalog));
        Assert.True(catalog.IsKnownEngineWidget("iconType"));
        Assert.Contains(
            GuiPalette.Entries(index, resolver, catalog),
            e => e.Kind == GuiPaletteKind.EngineWidget && e.Name == "iconType");
    }

    [Fact]
    public void Existing_readers_use_folded_stats_for_editor_and_options()
    {
        var icons = string.Concat(Enumerable.Range(0, 5).Select(i =>
            $"\t\ticonType = {{ name = \"i{i}\" Orientation = {(i % 2 == 0 ? "UPPER_LEFT" : "CENTER")} }}\n"));
        var catalog = LearnClassic("guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"w\"\n" + icons + "\t}\n}\n");

        Assert.Equal(GuiValueEditor.Enum, GuiPropertyInput.EditorFor("Orientation", "iconType", catalog, null));
        Assert.Equal(new[] { "UPPER_LEFT", "CENTER" }, GuiPropertyInput.EnumOptions("orientation", "ICONTYPE", catalog));
        Assert.False(GuiPropertyInput.AcceptsDataExpression("Orientation", "iconType", catalog, "x"));
    }
}
