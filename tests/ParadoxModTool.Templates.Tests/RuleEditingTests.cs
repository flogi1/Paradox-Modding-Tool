using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class RuleEditingTests
{
    private static CountingRule Rule(string id) => new() { Id = id, Name = id, Patterns = { "*" } };

    private static RuleFile File(params string[] ids)
    {
        var file = new RuleFile();
        file.Rules.AddRange(ids.Select(Rule));
        return file;
    }

    [Theory]
    [InlineData(RuleLayer.Shipped, RuleLayer.Mod, true)]
    [InlineData(RuleLayer.Game, RuleLayer.Mod, true)]
    [InlineData(RuleLayer.Mod, RuleLayer.Mod, false)]
    [InlineData(RuleLayer.Template, RuleLayer.Mod, false)]
    [InlineData(RuleLayer.AllGames, RuleLayer.AllGames, false)]
    [InlineData(RuleLayer.Shipped, RuleLayer.AllGames, true)]
    public void Only_less_specific_rules_are_switched_off_through_the_list(RuleLayer rule, RuleLayer chosen, bool expected)
    {
        Assert.Equal(expected, RuleEditing.UsesDisabledList(rule, chosen));
    }

    [Fact]
    public void An_inherited_rule_is_switched_off_on_the_chosen_layer_and_back_on()
    {
        var mod = new RuleFile();

        RuleEditing.SetActive(new RuleFile(), mod, Rule("s"), RuleLayer.Shipped, RuleLayer.Mod, active: false);
        RuleEditing.SetActive(new RuleFile(), mod, Rule("s"), RuleLayer.Shipped, RuleLayer.Mod, active: false);

        Assert.Equal(new[] { "s" }, mod.DisabledRuleIds);

        RuleEditing.SetActive(new RuleFile(), mod, Rule("s"), RuleLayer.Shipped, RuleLayer.Mod, active: true);

        Assert.Empty(mod.DisabledRuleIds);
    }

    [Fact]
    public void A_rule_of_the_chosen_or_a_more_specific_layer_is_switched_through_its_own_flag()
    {
        var own = File("a");

        RuleEditing.SetActive(own, new RuleFile(), own.Rules[0], RuleLayer.Mod, RuleLayer.Game, active: false);

        Assert.False(own.Rules[0].Enabled);

        RuleEditing.SetActive(own, new RuleFile(), own.Rules[0], RuleLayer.Mod, RuleLayer.Game, active: true);

        Assert.True(own.Rules[0].Enabled);
    }

    [Fact]
    public void Copy_gets_a_new_id_and_a_suffix()
    {
        var original = Rule("a");
        original.Enabled = false;
        original.Patterns.Add("x#");

        var copy = RuleEditing.Copy(original);

        Assert.NotEqual("a", copy.Id);
        Assert.Equal(32, copy.Id.Length);
        Assert.Equal("a (Kopie)", copy.Name);
        Assert.True(copy.Enabled);
        Assert.Equal(new[] { "*", "x#" }, copy.Patterns);
        copy.Patterns.Add("y");
        Assert.Equal(2, original.Patterns.Count);
    }

    [Fact]
    public void Copying_a_shipped_rule_switches_the_original_off_on_the_target_layer()
    {
        var target = File("x");
        var shipped = ShippedRules.All[0];

        var copy = RuleEditing.AddCopy(target, shipped, RuleLayer.Shipped);

        Assert.Equal(new[] { "x", copy.Id }, target.Rules.Select(r => r.Id).ToArray());
        Assert.Equal(new[] { shipped.Id }, target.DisabledRuleIds);
    }

    [Fact]
    public void Copying_an_own_rule_leaves_the_original_on()
    {
        var target = new RuleFile();

        RuleEditing.AddCopy(target, Rule("a"), RuleLayer.Game);

        Assert.Empty(target.DisabledRuleIds);
        Assert.Single(target.Rules);
    }

    [Fact]
    public void Rules_can_be_moved_up_and_down_within_their_layer()
    {
        var file = File("a", "b", "c");

        Assert.True(RuleEditing.MoveUp(file, "b"));
        Assert.Equal(new[] { "b", "a", "c" }, file.Rules.Select(r => r.Id).ToArray());
        Assert.False(RuleEditing.MoveUp(file, "b"));
        Assert.True(RuleEditing.MoveDown(file, "b"));
        Assert.True(RuleEditing.MoveDown(file, "b"));
        Assert.False(RuleEditing.MoveDown(file, "b"));
        Assert.Equal(new[] { "a", "c", "b" }, file.Rules.Select(r => r.Id).ToArray());
        Assert.False(RuleEditing.MoveUp(file, "fehlt"));
    }

    [Fact]
    public void Delete_and_replace_work_by_id()
    {
        var file = File("a", "b", "c");
        var edited = Rule("b");
        edited.Name = "neu";

        Assert.True(RuleEditing.Replace(file, edited));
        Assert.Equal("neu", file.Rules[1].Name);
        Assert.False(RuleEditing.Replace(file, Rule("fehlt")));
        Assert.True(RuleEditing.Delete(file, "a"));
        Assert.False(RuleEditing.Delete(file, "a"));
        Assert.Equal(new[] { "b", "c" }, file.Rules.Select(r => r.Id).ToArray());
    }

    [Fact]
    public void Add_appends_a_rule()
    {
        var file = File("a");

        RuleEditing.Add(file, Rule("b"));

        Assert.Equal(new[] { "a", "b" }, file.Rules.Select(r => r.Id).ToArray());
    }
}
