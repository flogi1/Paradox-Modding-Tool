using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class CountingRuleTests
{
    [Fact]
    public void A_new_rule_has_sensible_defaults_and_a_fresh_id()
    {
        var first = CountingRule.Create("A");
        var second = CountingRule.Create("A");

        Assert.Equal(32, first.Id.Length);
        Assert.NotEqual(first.Id, second.Id);
        Assert.True(first.Enabled);
        Assert.Equal(new[] { "*" }, first.Games);
        Assert.Equal(RuleWhere.Anywhere, first.Where);
        Assert.Equal(RuleAction.OnlyMarkedCounts, first.Action);
        Assert.False(first.CaseSensitive);
        Assert.Empty(first.Patterns);
    }

    [Fact]
    public void Clone_is_a_deep_copy()
    {
        var rule = CountingRule.Create("A");
        rule.Patterns.Add("iv#*");
        rule.Games = new List<string> { "eu5" };

        var copy = rule.Clone();
        copy.Patterns.Add("x");
        copy.Games.Add("ck3");

        Assert.Equal(rule.Id, copy.Id);
        Assert.Single(rule.Patterns);
        Assert.Equal(new[] { "eu5" }, rule.Games);
    }

    [Fact]
    public void The_file_round_trips_with_readable_enum_names()
    {
        var file = new RuleFile();
        file.Rules.Add(new CountingRule
        {
            Id = "r1",
            Name = "ARA",
            Where = RuleWhere.InsideKey,
            WhereKey = "position",
            Patterns = { "iv#_ga_*_ARA_[#]_#" },
            Action = RuleAction.MarkedDoesNotCount,
        });
        file.DisabledRuleIds.Add("shipped-button");

        var json = JsonSerializer.Serialize(file, RuleFile.JsonOptions);
        var back = JsonSerializer.Deserialize<RuleFile>(json, RuleFile.JsonOptions)!;

        Assert.Contains("\"insideKey\"", json);
        Assert.Contains("\"markedDoesNotCount\"", json);
        Assert.Contains("\"disabledRuleIds\"", json);
        Assert.Equal(RuleAction.MarkedDoesNotCount, back.Rules[0].Action);
        Assert.Equal("position", back.Rules[0].WhereKey);
        Assert.Equal(new[] { "shipped-button" }, back.DisabledRuleIds);
        Assert.Equal(1, back.Version);
    }

    [Theory]
    [InlineData(RuleLayer.Shipped, "mitgeliefert")]
    [InlineData(RuleLayer.AllGames, "Alle Spiele")]
    [InlineData(RuleLayer.Game, "Spiel")]
    [InlineData(RuleLayer.Mod, "Mod")]
    [InlineData(RuleLayer.Template, "Vorlage")]
    public void Layers_have_german_names(RuleLayer layer, string name)
    {
        Assert.Equal(name, RuleLayerNames.Of(layer));
    }

    [Fact]
    public void The_reason_names_the_rule_and_the_layer()
    {
        var effective = new EffectiveRule(new CountingRule { Name = "Button" }, RuleLayer.Shipped);

        Assert.Equal("Regel „Button“ (mitgeliefert)", effective.Reason);
    }
}
