using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Templates.Tests;

public class RuleStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-rulestore-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string GlobalFile => Path.Combine(_dir, "rules", "global.json");

    private string GameFile => Path.Combine(_dir, "game", "rules", "game.json");

    private string ModFile => Path.Combine(_dir, "game", "mods", "m1", "rules.json");

    private RuleStore Store(bool withMod = true, string game = "eu5") =>
        new(GlobalFile, GameFile, game) { ModFile = withMod ? ModFile : null };

    private static CountingRule Rule(string id) => new() { Id = id, Name = id, Patterns = { "*" } };

    [Fact]
    public void A_missing_file_is_an_empty_layer_without_warning()
    {
        var store = Store();

        var file = store.Load(RuleLayer.Game);

        Assert.Empty(file.Rules);
        Assert.Empty(file.DisabledRuleIds);
        Assert.Empty(store.Warnings);
    }

    [Fact]
    public void Update_creates_the_folders_and_the_file_with_the_documented_layout()
    {
        var store = Store();

        store.Update(RuleLayer.AllGames, f =>
        {
            f.Rules.Add(Rule("a"));
            f.DisabledRuleIds.Add(ShippedRules.ButtonId);
        });

        using var document = JsonDocument.Parse(File.ReadAllText(GlobalFile));
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("a", document.RootElement.GetProperty("rules")[0].GetProperty("id").GetString());
        Assert.Equal(ShippedRules.ButtonId, document.RootElement.GetProperty("disabledRuleIds")[0].GetString());
        Assert.Equal("a", Store().Load(RuleLayer.AllGames).Rules.Single().Id);
    }

    [Fact]
    public void Update_works_on_a_fresh_read_so_a_second_writer_does_not_lose_changes()
    {
        var first = Store();
        var second = Store();

        first.Update(RuleLayer.Game, f => f.Rules.Add(Rule("one")));
        second.Update(RuleLayer.Game, f => f.Rules.Add(Rule("two")));

        Assert.Equal(new[] { "one", "two" }, Store().Load(RuleLayer.Game).Rules.Select(r => r.Id).ToArray());
    }

    [Fact]
    public void Parallel_updates_all_survive()
    {
        var ids = Enumerable.Range(0, 8).Select(i => "r" + i).ToList();

        Parallel.ForEach(ids, id => Store().Update(RuleLayer.AllGames, f => f.Rules.Add(Rule(id))));

        Assert.Equal(ids.OrderBy(x => x), Store().Load(RuleLayer.AllGames).Rules.Select(r => r.Id).OrderBy(x => x));
    }

    [Fact]
    public void A_broken_file_is_copied_and_the_layer_starts_empty_with_a_warning()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GameFile)!);
        File.WriteAllText(GameFile, "{ das ist kein json");
        var store = Store();

        var file = store.Load(RuleLayer.Game);

        Assert.Empty(file.Rules);
        Assert.Equal("{ das ist kein json", File.ReadAllText(GameFile + ".bad"));
        Assert.Single(store.Warnings);
        store.Load(RuleLayer.Game);
        Assert.Single(store.Warnings);
    }

    [Fact]
    public void Missing_values_in_a_file_are_repaired()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GlobalFile)!);
        File.WriteAllText(
            GlobalFile,
            "{ \"version\": 1, \"rules\": [ { \"name\": \"Ohne\", \"patterns\": null, \"games\": null }, null ], \"disabledRuleIds\": [ \"x\", \"\", null ] }");

        var file = Store().Load(RuleLayer.AllGames);

        var rule = Assert.Single(file.Rules);
        Assert.Equal(32, rule.Id.Length);
        Assert.Empty(rule.Patterns);
        Assert.Equal(new[] { "*" }, rule.Games);
        Assert.Equal(new[] { "x" }, file.DisabledRuleIds);
    }

    [Fact]
    public void Layers_without_a_file_cannot_be_used()
    {
        var store = Store(withMod: false);

        Assert.True(store.HasLayer(RuleLayer.AllGames));
        Assert.True(store.HasLayer(RuleLayer.Game));
        Assert.False(store.HasLayer(RuleLayer.Mod));
        Assert.False(store.HasLayer(RuleLayer.Template));
        Assert.False(store.HasLayer(RuleLayer.Shipped));
        Assert.Throws<InvalidOperationException>(() => store.Update(RuleLayer.Mod, _ => { }));
        Assert.Throws<InvalidOperationException>(() => store.Update(RuleLayer.Template, _ => { }));
        Assert.Empty(store.Load(RuleLayer.Mod).Rules);
        Assert.True(Store().HasLayer(RuleLayer.Mod));
    }

    [Fact]
    public void Resolve_combines_all_layers_with_the_template_and_the_game_list()
    {
        var store = Store();
        store.Update(RuleLayer.AllGames, f =>
        {
            f.Rules.Add(Rule("global-all"));
            f.Rules.Add(new CountingRule { Id = "global-ck3", Name = "x", Games = new List<string> { "ck3" }, Patterns = { "*" } });
        });
        store.Update(RuleLayer.Game, f => f.Rules.Add(Rule("game")));
        store.Update(RuleLayer.Mod, f => f.Rules.Add(Rule("mod")));
        var template = new RuleFile { Rules = { Rule("template") }, DisabledRuleIds = { "game" } };

        var ids = store.Resolve(template).Select(r => r.Rule.Id).ToArray();

        Assert.Equal(new[] { "template", "mod", "global-all" }, ids.Take(3).ToArray());
        Assert.DoesNotContain("game", ids);
        Assert.DoesNotContain("global-ck3", ids);
        Assert.Contains(ShippedRules.NamePrefixId, ids);
    }

    [Fact]
    public void Resolve_leaves_out_the_mod_layer_without_a_mod()
    {
        var withMod = Store();
        withMod.Update(RuleLayer.Mod, f => f.Rules.Add(Rule("mod")));

        var ids = Store(withMod: false).Resolve(new RuleFile()).Select(r => r.Rule.Id).ToArray();

        Assert.DoesNotContain("mod", ids);
    }
}
