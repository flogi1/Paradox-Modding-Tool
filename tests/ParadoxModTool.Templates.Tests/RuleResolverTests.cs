using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class RuleResolverTests
{
    private static RuleLayerFile Layer(RuleLayer layer, string[] ruleIds, params string[] disabled)
    {
        var file = new RuleFile();
        file.Rules.AddRange(ruleIds.Select(id => new CountingRule { Id = id, Name = id, Patterns = { "*" } }));
        file.DisabledRuleIds.AddRange(disabled);
        return new RuleLayerFile(layer, file);
    }

    private static string[] Ids(IEnumerable<EffectiveRule> rules) => rules.Select(r => r.Rule.Id).ToArray();

    [Fact]
    public void The_most_specific_layer_comes_first_and_shipped_rules_come_last()
    {
        var layers = new[]
        {
            Layer(RuleLayer.AllGames, new[] { "a" }),
            Layer(RuleLayer.Template, new[] { "t1", "t2" }),
            Layer(RuleLayer.Game, new[] { "g" }),
            Layer(RuleLayer.Mod, new[] { "m" }),
        };

        var ids = Ids(RuleResolver.Resolve(layers, "eu5"));

        Assert.Equal(new[] { "t1", "t2", "m", "g", "a" }, ids.Take(5).ToArray());
        Assert.Equal(ShippedRules.All.Select(r => r.Id).ToArray(), ids.Skip(5).ToArray());
    }

    [Fact]
    public void Without_layers_only_the_shipped_rules_remain()
    {
        var ids = Ids(RuleResolver.Resolve(Array.Empty<RuleLayerFile>(), "eu5"));

        Assert.Equal(ShippedRules.All.Select(r => r.Id).ToArray(), ids);
    }

    [Fact]
    public void A_shipped_layer_in_the_input_is_ignored()
    {
        var ids = Ids(RuleResolver.Resolve(new[] { Layer(RuleLayer.Shipped, new[] { "fake" }) }, "eu5"));

        Assert.DoesNotContain("fake", ids);
    }

    [Fact]
    public void A_more_specific_layer_can_switch_off_an_inherited_rule_by_id()
    {
        var layers = new[]
        {
            Layer(RuleLayer.Game, new[] { "g" }),
            Layer(RuleLayer.Mod, Array.Empty<string>(), "g", ShippedRules.ButtonId),
        };

        var rows = RuleResolver.List(layers, "eu5");

        Assert.DoesNotContain("g", Ids(RuleResolver.Resolve(layers, "eu5")));
        Assert.DoesNotContain(ShippedRules.ButtonId, Ids(RuleResolver.Resolve(layers, "eu5")));
        Assert.False(rows.Single(r => r.Effective.Rule.Id == "g").Active);
        Assert.True(rows.Single(r => r.Effective.Rule.Id == ShippedRules.SizeId).Active);
    }

    [Fact]
    public void A_layer_can_switch_off_its_own_rule_by_id()
    {
        var layers = new[] { Layer(RuleLayer.Mod, new[] { "m" }, "m") };

        Assert.DoesNotContain("m", Ids(RuleResolver.Resolve(layers, "eu5")));
    }

    [Fact]
    public void A_less_specific_layer_cannot_switch_off_a_more_specific_rule()
    {
        var layers = new[]
        {
            Layer(RuleLayer.AllGames, Array.Empty<string>(), "m"),
            Layer(RuleLayer.Mod, new[] { "m" }),
        };

        Assert.Contains("m", Ids(RuleResolver.Resolve(layers, "eu5")));
    }

    [Fact]
    public void The_all_games_layer_can_switch_off_a_shipped_rule_for_every_game()
    {
        var layers = new[] { Layer(RuleLayer.AllGames, Array.Empty<string>(), ShippedRules.NamePrefixId) };

        Assert.DoesNotContain(ShippedRules.NamePrefixId, Ids(RuleResolver.Resolve(layers, "eu5")));
    }

    [Fact]
    public void A_rule_with_its_own_switch_off_is_inactive_but_listed()
    {
        var file = new RuleFile();
        file.Rules.Add(new CountingRule { Id = "x", Name = "x", Enabled = false, Patterns = { "*" } });

        var rows = RuleResolver.List(new[] { new RuleLayerFile(RuleLayer.Game, file) }, "eu5");

        Assert.False(rows.Single(r => r.Effective.Rule.Id == "x").Active);
    }

    [Theory]
    [InlineData("*", true)]
    [InlineData("eu5", true)]
    [InlineData("EU5", true)]
    [InlineData("ck3", false)]
    public void Rules_on_all_games_apply_only_to_listed_games(string game, bool applies)
    {
        var file = new RuleFile();
        file.Rules.Add(new CountingRule { Id = "x", Name = "x", Games = new List<string> { game }, Patterns = { "*" } });

        var rows = RuleResolver.List(new[] { new RuleLayerFile(RuleLayer.AllGames, file) }, "eu5");

        Assert.Equal(applies, rows.Single(r => r.Effective.Rule.Id == "x").Active);
    }

    [Fact]
    public void An_empty_games_list_means_every_game_and_other_layers_ignore_the_list()
    {
        var all = new RuleFile();
        all.Rules.Add(new CountingRule { Id = "a", Name = "a", Games = new List<string>(), Patterns = { "*" } });
        var mod = new RuleFile();
        mod.Rules.Add(new CountingRule { Id = "m", Name = "m", Games = new List<string> { "ck3" }, Patterns = { "*" } });

        var ids = Ids(RuleResolver.Resolve(new[] { new RuleLayerFile(RuleLayer.AllGames, all), new RuleLayerFile(RuleLayer.Mod, mod) }, "eu5"));

        Assert.Contains("a", ids);
        Assert.Contains("m", ids);
    }

    [Fact]
    public void With_a_repeated_id_only_the_most_specific_rule_is_active()
    {
        var layers = new[]
        {
            Layer(RuleLayer.Game, new[] { "same" }),
            Layer(RuleLayer.Mod, new[] { "same" }),
        };

        var rows = RuleResolver.List(layers, "eu5").Where(r => r.Effective.Rule.Id == "same").ToList();

        Assert.Equal(new[] { RuleLayer.Mod, RuleLayer.Game }, rows.Select(r => r.Effective.Layer).ToArray());
        Assert.Equal(new[] { true, false }, rows.Select(r => r.Active).ToArray());
    }
}
