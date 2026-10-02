using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class SafetyRulesTests
{
    private static readonly string[] Languages = { "de", "en", "es", "fr", "pl", "pt-br", "ru", "zh-hans" };

    private static string ProfilePath => Path.Combine(AppContext.BaseDirectory, "profiles", "desync", "eu5.json");

    private static DesyncProfile ParseRules(string rulesJson) =>
        DesyncProfile.Parse("{ \"safety\": { \"rules\": [" + rulesJson + "] } }");

    [Fact]
    public void Missing_section_is_null()
    {
        Assert.Null(DesyncProfile.Parse("{ \"version\": 1 }").Safety);
    }

    [Fact]
    public void Shipped_eu5_rules_parse()
    {
        var safety = DesyncProfile.Load(ProfilePath).Safety;

        Assert.NotNull(safety);
        Assert.Equal(new[] { "local_dlc", "console_ui", "console_dynamic", "console_other" }, safety.Rules.Select(r => r.Id));
        Assert.Equal(new[] { "in_game", "main_menu", "loading_screen" }, safety.RootFolders);
        Assert.Equal(new[] { "gfx", "gui", "gui_animations", "fonts", "music", "sound" }, safety.LocalFolders);
        Assert.Contains("seed", safety.StopTerms);
        Assert.Equal(2, safety.LocalBlocks.Count);
        Assert.Equal("common/scripted_guis", safety.LocalBlocks[0].Folder);
        Assert.Equal(new[] { "is_shown", "is_valid" }, safety.LocalBlocks[0].Keys);
        Assert.Equal(new[] { "*" }, safety.LocalBlocks[1].Keys);
        Assert.Empty(safety.AreaMap);
        foreach (var rule in safety.Rules)
        {
            foreach (var language in Languages)
            {
                Assert.True(rule.Explain.TryGetValue(language, out var text) && !string.IsNullOrWhiteSpace(text), rule.Id + " " + language);
            }
        }

        var localDlc = safety.Rules[0];
        Assert.Equal(SafetyRuleKind.ScriptKey, localDlc.Kind);
        Assert.Equal(SafetySeverity.Risk, localDlc.Synced);
        Assert.Equal(SafetySeverity.Allowed, localDlc.Local);
        Assert.Equal(SafetyRuleKind.ConsoleDynamic, safety.Rules[2].Kind);
        Assert.Empty(safety.Rules[2].Patterns);
        Assert.Equal(new[] { "*" }, safety.Rules[3].Patterns);
    }

    [Fact]
    public void Match_string_or_array()
    {
        var single = ParseRules("{ \"id\": \"a\", \"kind\": \"scriptKey\", \"match\": \"has_local_dlc\", \"synced\": \"risk\", \"local\": \"allowed\" }");
        Assert.Equal(new[] { "has_local_dlc" }, single.Safety!.Rules[0].Patterns);

        var many = ParseRules("{ \"id\": \"a\", \"kind\": \"consoleCommand\", \"match\": [\"GUI.\", \"reload\"], \"synced\": \"allowed\", \"local\": \"allowed\" }");
        Assert.Equal(new[] { "GUI.", "reload" }, many.Safety!.Rules[0].Patterns);
    }

    [Fact]
    public void Lists_are_lower_case()
    {
        var p = DesyncProfile.Parse(
            "{ \"safety\": { \"rootFolders\": [\"In_Game\"], \"localFolders\": [\"GFX\\\\Sub\"], " +
            "\"localBlocks\": [{ \"folder\": \"Common\\\\Scripted_Guis\", \"keys\": [\"Is_Shown\"] }], \"stopTerms\": [\"Seed\"] } }");

        Assert.Equal(new[] { "in_game" }, p.Safety!.RootFolders);
        Assert.Equal(new[] { "gfx/sub" }, p.Safety.LocalFolders);
        Assert.Equal("common/scripted_guis", p.Safety.LocalBlocks[0].Folder);
        Assert.Equal(new[] { "is_shown" }, p.Safety.LocalBlocks[0].Keys);
        Assert.Contains("seed", p.Safety.StopTerms);
    }

    [Fact]
    public void Invalid_kind_throws_with_rule_number()
    {
        var ex = Assert.Throws<InvalidDataException>(() => ParseRules(
            "{ \"id\": \"a\", \"kind\": \"scriptKey\", \"match\": \"x\", \"synced\": \"risk\", \"local\": \"allowed\" }," +
            "{ \"id\": \"b\", \"kind\": \"nope\", \"match\": \"x\", \"synced\": \"risk\", \"local\": \"allowed\" }"));
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public void Invalid_severity_throws_with_rule_number()
    {
        var ex = Assert.Throws<InvalidDataException>(() => ParseRules(
            "{ \"id\": \"a\", \"kind\": \"scriptKey\", \"match\": \"x\", \"synced\": \"maybe\", \"local\": \"allowed\" }"));
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void Missing_id_throws()
    {
        Assert.Throws<InvalidDataException>(() => ParseRules(
            "{ \"kind\": \"scriptKey\", \"match\": \"x\", \"synced\": \"risk\", \"local\": \"allowed\" }"));
    }

    [Theory]
    [InlineData("scriptKey")]
    [InlineData("consoleCommand")]
    public void Pattern_kind_without_match_throws(string kind)
    {
        Assert.Throws<InvalidDataException>(() => ParseRules(
            "{ \"id\": \"a\", \"kind\": \"" + kind + "\", \"synced\": \"risk\", \"local\": \"allowed\" }"));
    }

    [Theory]
    [InlineData(SafetySeverity.Risk, SafetySeverity.Allowed, SafetySeverity.Risk)]
    [InlineData(SafetySeverity.Check, SafetySeverity.Check, SafetySeverity.Check)]
    [InlineData(SafetySeverity.Allowed, SafetySeverity.Allowed, SafetySeverity.Allowed)]
    [InlineData(SafetySeverity.Allowed, SafetySeverity.Check, SafetySeverity.Check)]
    public void SeverityIn_unknown(SafetySeverity synced, SafetySeverity local, SafetySeverity expected)
    {
        var rule = new SafetyRule("a", SafetyRuleKind.ScriptKey, new[] { "x" }, synced, local, new Dictionary<string, string>());

        Assert.Equal(expected, rule.SeverityIn(SafetyContext.Unknown));
        Assert.Equal(synced, rule.SeverityIn(SafetyContext.Synced));
        Assert.Equal(local, rule.SeverityIn(SafetyContext.Local));
    }
}
