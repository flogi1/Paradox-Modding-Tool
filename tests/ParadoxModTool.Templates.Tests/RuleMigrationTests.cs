using System.Text.Json;
using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class RuleMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-rulemig-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private SavedTemplate LoadOld(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "alt.json"), json);
        return new TemplateStore(_dir).Load("alt")!;
    }

    [Fact]
    public void A_version_1_template_with_default_settings_needs_no_rules()
    {
        var loaded = LoadOld("{ \"version\": 1, \"snippet\": \"a_1 = { }\", \"ignore\": 15, \"ignoreNamePrefixes\": true, \"namePrefixes\": [\"iv\"] }");

        Assert.Equal(2, loaded.Version);
        Assert.Empty(loaded.DisabledRuleIds);
        Assert.Empty(loaded.Rules);
        Assert.Null(loaded.Ignore);
        Assert.Null(loaded.IgnoreNamePrefixes);
        Assert.Null(loaded.NamePrefixes);
    }

    [Theory]
    [InlineData(14, new[] { ShippedRules.TextureFileId, ShippedRules.TextureEntryId })]
    [InlineData(13, new[] { ShippedRules.ButtonId })]
    [InlineData(11, new[] { ShippedRules.PositionId })]
    [InlineData(7, new[] { ShippedRules.SizeId })]
    [InlineData(0, new[] { ShippedRules.TextureFileId, ShippedRules.TextureEntryId, ShippedRules.ButtonId, ShippedRules.PositionId, ShippedRules.SizeId })]
    public void A_switched_off_ignore_setting_becomes_a_disabled_shipped_rule(int ignore, string[] expected)
    {
        var loaded = LoadOld("{ \"version\": 1, \"ignore\": " + ignore + " }");

        Assert.Equal(expected.OrderBy(x => x), loaded.DisabledRuleIds.OrderBy(x => x));
        Assert.Empty(loaded.Rules);
    }

    [Fact]
    public void Switched_off_name_prefixes_disable_the_shipped_prefix_rule()
    {
        var loaded = LoadOld("{ \"version\": 1, \"ignoreNamePrefixes\": false, \"namePrefixes\": [\"iv\", \"ck\"] }");

        Assert.Equal(new[] { ShippedRules.NamePrefixId }, loaded.DisabledRuleIds);
        Assert.Empty(loaded.Rules);
    }

    [Fact]
    public void A_custom_prefix_list_becomes_a_template_rule_and_the_shipped_one_is_disabled()
    {
        var loaded = LoadOld("{ \"version\": 1, \"ignoreNamePrefixes\": true, \"namePrefixes\": [\"iv\", \"ck\"] }");

        Assert.Equal(new[] { ShippedRules.NamePrefixId }, loaded.DisabledRuleIds);
        var rule = Assert.Single(loaded.Rules);
        Assert.Equal("Namenspräfix (aus alter Vorlage)", rule.Name);
        Assert.Equal(RuleAction.MarkedDoesNotCount, rule.Action);
        Assert.Equal(new[] { "iv[#]*", "iv_[#]*", "ck[#]*", "ck_[#]*" }, rule.Patterns);
        Assert.Equal(32, rule.Id.Length);
    }

    [Fact]
    public void An_empty_prefix_list_disables_the_rule_without_adding_one()
    {
        var loaded = LoadOld("{ \"version\": 1, \"ignoreNamePrefixes\": true, \"namePrefixes\": [] }");

        Assert.Equal(new[] { ShippedRules.NamePrefixId }, loaded.DisabledRuleIds);
        Assert.Empty(loaded.Rules);
    }

    [Fact]
    public void The_default_prefix_in_another_case_is_still_the_default()
    {
        var loaded = LoadOld("{ \"version\": 1, \"namePrefixes\": [\"IV\"] }");

        Assert.Empty(loaded.DisabledRuleIds);
        Assert.Empty(loaded.Rules);
    }

    [Fact]
    public void A_template_without_a_version_number_and_without_legacy_fields_is_left_alone()
    {
        var loaded = LoadOld("{ \"snippet\": \"a_1 = { }\", \"count\": 4 }");

        Assert.Equal(2, loaded.Version);
        Assert.Empty(loaded.DisabledRuleIds);
        Assert.Empty(loaded.Rules);
    }

    [Fact]
    public void The_migration_happens_once_and_the_saved_file_uses_the_new_format()
    {
        var store = new TemplateStore(_dir);
        var loaded = LoadOld("{ \"version\": 1, \"ignore\": 14, \"namePrefixes\": [\"ck\"] }");

        store.Save("neu", loaded);
        var json = File.ReadAllText(Path.Combine(_dir, "neu.json"));
        var again = store.Load("neu")!;

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("ignore", out _));
        Assert.False(document.RootElement.TryGetProperty("ignoreNamePrefixes", out _));
        Assert.False(document.RootElement.TryGetProperty("namePrefixes", out _));
        Assert.Equal(2, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(loaded.DisabledRuleIds.OrderBy(x => x), again.DisabledRuleIds.OrderBy(x => x));
        Assert.Equal(loaded.Rules.Count, again.Rules.Count);
        Assert.Equal(loaded.Rules[0].Patterns, again.Rules[0].Patterns);
    }

    [Fact]
    public void Rules_of_the_template_layer_round_trip()
    {
        var template = new SavedTemplate { Snippet = "x" };
        template.Rules.Add(new CountingRule { Id = "r1", Name = "R", Patterns = { "a#*" }, Action = RuleAction.Ignore });
        template.DisabledRuleIds.Add(ShippedRules.ButtonId);
        var store = new TemplateStore(_dir);

        store.Save("t", template);
        var back = store.Load("t")!;

        Assert.Equal("r1", Assert.Single(back.Rules).Id);
        Assert.Equal(RuleAction.Ignore, back.Rules[0].Action);
        Assert.Equal(new[] { ShippedRules.ButtonId }, back.DisabledRuleIds);
    }

    [Fact]
    public void A_migrated_template_behaves_like_before()
    {
        // Früher: Textur-Häkchen aus. Die Zahl im Texturnamen muss jetzt zählen.
        var loaded = LoadOld("{ \"version\": 1, \"ignore\": 14 }");
        var file = new RuleFile { Rules = loaded.Rules, DisabledRuleIds = loaded.DisabledRuleIds };
        var rules = RuleResolver.Resolve(new[] { new RuleLayerFile(RuleLayer.Template, file) }, "eu5");

        var model = TemplateAnalyzer.Analyze("x = { texture = \"gfx/frame_3.dds\" }", new AnalyzeOptions(Rules: rules));

        Assert.Contains("frame_4.dds", TemplateEngine.Render(model, 2));
    }
}
