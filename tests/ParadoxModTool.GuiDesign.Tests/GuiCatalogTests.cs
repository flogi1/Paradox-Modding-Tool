using System.Diagnostics;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiCatalogTests
{
    private readonly ITestOutputHelper _output;

    public GuiCatalogTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData("12", false, GuiValueForm.Number)]
    [InlineData("-0.5", false, GuiValueForm.Number)]
    [InlineData("@my_constant", false, GuiValueForm.Number)]
    [InlineData("50%", false, GuiValueForm.Percent)]
    [InlineData("yes", false, GuiValueForm.Bool)]
    [InlineData("expanding", false, GuiValueForm.Word)]
    [InlineData("top|right", false, GuiValueForm.Word)]
    [InlineData("gfx/interface/a.dds", true, GuiValueForm.TexturePath)]
    [InlineData("[GetPlayer.GetName]", true, GuiValueForm.Binding)]
    [InlineData("#yellow Hallo", true, GuiValueForm.Text)]
    public void ClassifiesScalars(string value, bool quoted, GuiValueForm expected)
    {
        Assert.Equal(expected, GuiValueClassifier.ClassifyScalar(value, quoted));
    }

    [Fact]
    public void ClassifiesValueBlocks()
    {
        Assert.Equal(GuiValueForm.Pair, GuiValueClassifier.ClassifyItems(new[] { "10", "100%" }));
        Assert.Equal(GuiValueForm.Color, GuiValueClassifier.ClassifyItems(new[] { "1", "0.5", "0.2", "1" }));
        Assert.Equal(GuiValueForm.List, GuiValueClassifier.ClassifyItems(new[] { "a", "b" }));
    }

    private static GuiPropertyCatalog LearnFrom(params (GuiSourceFile File, string Text)[] files) =>
        GuiCatalogLearner.Learn(GuiIndexTests.Build(files), GuiResolverTests.Rules, "fp");

    [Fact]
    public void LearnsExamplesPerMember()
    {
        var catalog = LearnFrom((GuiIndexTests.F("in_game/gui/a.gui"),
            "widget = { datacontext = \"[Country]\" text = \"[Country.GetCapital.GetName]\" visible = \"[Not(Country.IsPlayer)]\" }\n"));

        Assert.Contains(catalog.ExamplesFor("Country.GetCapital"), e => e.Expression == "[Country.GetCapital.GetName]" && e.File == "in_game/gui/a.gui");
        Assert.Contains(catalog.ExamplesFor("Country.IsPlayer"), e => e.Expression == "[Not(Country.IsPlayer)]");
        Assert.Contains(catalog.ExamplesFor("Not"), e => e.Expression == "[Not(Country.IsPlayer)]");
        Assert.Contains(catalog.ExamplesFor("Country"), e => e.Expression == "[Country]");
        Assert.Empty(catalog.ExamplesFor("GetCapital.GetName"));
    }

    [Fact]
    public void KeepsAtMostFiveExamples_MostFrequentFirst()
    {
        var text = string.Concat(Enumerable.Range(0, 6).Select(i => $"widget = {{ text = \"[Country.GetName{i}]\" tooltip = \"[Country.GetName0]\" }}\n"));
        var examples = LearnFrom((GuiIndexTests.F("in_game/gui/a.gui"), text)).ExamplesFor("Country");

        Assert.Equal(5, examples.Count);
        Assert.Equal("[Country.GetName0]", examples[0].Expression);
    }

    [Fact]
    public void LearnsListItemTypeFromItemExpressions()
    {
        var items = "item = { widget = { text = \"[Location.GetName]\" visible = \"[Location.HasOwner]\" tooltip = \"[Location.GetName]\" } }";
        var catalog = LearnFrom((GuiIndexTests.F("in_game/gui/a.gui"), $"dynamicgridbox = {{ datamodel = \"[Province.GetLocations]\" {items} }}\n"));

        Assert.Equal("Location", catalog.ItemTypeOf("Province.GetLocations"));
        Assert.Equal("Location", catalog.ItemTypeOf("[Province.GetLocations]"));
    }

    [Fact]
    public void ListItemTypeIgnoresWidgetAndGlobalContexts_AndNestedDataContexts()
    {
        var items = "item = { widget = { text = \"[Location.GetName]\" visible = \"[IsOdd_int32(PdxGuiWidget.GetIndexInDataModel)]\" " +
                    "enabled = \"[PdxGuiWidget.IsUberButtonEnabled]\" tooltip = \"[GetPlayer.GetName]\" " +
                    "sub = { datacontext = \"[Country]\" a = \"[Country.GetName]\" b = \"[Country.GetName]\" c = \"[Country.GetName]\" } } }";
        var catalog = LearnFrom((GuiIndexTests.F("in_game/gui/a.gui"), $"dynamicgridbox = {{ datamodel = \"[Province.GetLocations]\" {items} }}\n"));

        Assert.Equal("Location", catalog.ItemTypeOf("Province.GetLocations"));
    }

    [Fact]
    public void ListItemTypeNeedsSixtyPercent()
    {
        var items = "item = { widget = { text = \"[Location.GetName]\" tooltip = \"[Country.GetName]\" } }";
        var catalog = LearnFrom((GuiIndexTests.F("in_game/gui/a.gui"), $"dynamicgridbox = {{ datamodel = \"[Province.GetLocations]\" {items} }}\n"));

        Assert.Null(catalog.ItemTypeOf("Province.GetLocations"));
    }

    [Fact]
    public void LearnsPropertiesPerBaseWidget()
    {
        var hboxes = string.Concat(new[] { "expanding", "fixed", "expanding", "preferred", "expanding" }
            .Select(p => $"hbox = {{ spacing = 5 layoutpolicy_horizontal = {p} }}\n"));
        var catalog = LearnFrom((GuiIndexTests.F("in_game/gui/a.gui"),
            "types T { type my_btn = button { size = { 10 10 } } }\n" +
            "my_btn = { text = \"Hallo\" }\n" +
            "icon = { texture = \"gfx/a.dds\" modify_texture = { blend_mode = add } }\n" +
            hboxes));

        Assert.Equal(2, catalog.Widgets["button"].Count);
        Assert.Equal(GuiValueForm.Pair, catalog.Property("button", "size")!.DominantForm);
        Assert.Equal(GuiValueForm.Text, catalog.Property("button", "text")!.DominantForm);
        Assert.Equal(GuiValueForm.TexturePath, catalog.Property("icon", "texture")!.DominantForm);
        Assert.Equal(GuiValueForm.Block, catalog.Property("icon", "modify_texture")!.DominantForm);
        Assert.Equal(GuiValueForm.Number, catalog.Property("hbox", "spacing")!.DominantForm);
        var policy = catalog.Property("hbox", "layoutpolicy_horizontal")!;
        Assert.True(policy.IsEnum);
        Assert.Equal(3, policy.Words["expanding"]);
        Assert.Equal(new[] { "layoutpolicy_horizontal", "spacing" }, catalog.PropertiesFor("hbox").Select(p => p.Name));
        Assert.True(catalog.IsKnownEngineWidget("hbox"));
        Assert.False(catalog.IsKnownEngineWidget("my_btn"));
    }

    [Fact]
    public void LearnsOnlyFromVanilla()
    {
        var catalog = LearnFrom(
            (GuiIndexTests.F("in_game/gui/a.gui"), "hbox = { spacing = 1 }"),
            (GuiIndexTests.F("in_game/gui/m.gui", GuiLayer.Mod), "hbox = { mod_only = 1 }"));

        Assert.Null(catalog.Property("hbox", "mod_only"));
    }

    [Fact]
    public void GroupsBindingsByDataContext()
    {
        var catalog = LearnFrom((GuiIndexTests.F("in_game/gui/a.gui"),
            "widget = {\n\tdatacontext = \"[GetPlayer]\"\n\ttext_single = { text = \"[Country.GetName] [Country.GetName] ist [GetValue]\" }\n}\n" +
            "text_single = { text = \"[GetGlobal]\" }\n"));

        Assert.Equal(new[] { "[Country.GetName]", "[GetValue]", "[GetGlobal]" }, catalog.SuggestBindings("[GetPlayer]"));
        Assert.Equal(new[] { "[GetGlobal]" }, catalog.SuggestBindings(null));
        Assert.Equal(2, catalog.Bindings["[GetPlayer]"]["[Country.GetName]"]);
    }

    [Fact]
    public void StoreRoundTripsAndRejectsOtherFingerprints()
    {
        var path = Path.Combine(Path.GetTempPath(), "pmt-gui-" + Guid.NewGuid().ToString("N"), "catalog.json");
        try
        {
            var catalog = LearnFrom((GuiIndexTests.F("in_game/gui/a.gui"), "hbox = { spacing = 5 }"));
            GuiCatalogStore.Save(path, catalog);

            var loaded = GuiCatalogStore.Load(path, "fp")!;
            Assert.Equal(GuiValueForm.Number, loaded.Property("hbox", "spacing")!.DominantForm);
            Assert.Null(GuiCatalogStore.Load(path, "other"));
            Assert.Null(GuiCatalogStore.Load(path + ".missing", "fp"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void LearnsFromVanilla()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var watch = Stopwatch.StartNew();
        var index = GuiRulesVanillaTests.Vanilla;
        var catalog = GuiCatalogLearner.Learn(index, GuiRulesVanillaTests.ProfileRules(), GuiSourceSet.ComputeFingerprint(index.Files));
        _output.WriteLine($"{catalog.Widgets.Count} Engine-Widgets gelernt in {watch.ElapsedMilliseconds} ms");

        // Ab 1.4 ist "icon" im Ladebildschirm ein Typ auf "ubericon": die Eigenschaften stehen dann unter dem Basis-Widget.
        var iconBase = new GuiResolver(index, GuiRulesVanillaTests.ProfileRules()).BaseWidgetOf("icon");
        Assert.Equal(GuiValueForm.TexturePath, catalog.Property(iconBase, "texture")!.DominantForm);
        var policy = catalog.Property("uberhbox", "layoutpolicy_horizontal")!;
        Assert.True(policy.IsEnum);
        Assert.Contains("expanding", policy.Words.Keys);
        Assert.NotNull(catalog.Property("ubertextbox", "text"));
        Assert.NotEmpty(catalog.SuggestBindings(null));
    }
}
