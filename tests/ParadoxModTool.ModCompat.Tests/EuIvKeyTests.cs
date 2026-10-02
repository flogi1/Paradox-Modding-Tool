using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// EU IV packt Einträge in gemeinsame Blöcke (<c>country_event = { id = … }</c>, <c>country_decisions = { … }</c>, <c>spriteTypes = { … }</c>).
/// Der Schlüssel ist dort die <c>id</c> bzw. der Name im Block, nicht der Blockname; sonst gilt jede Datei als Konflikt mit jeder.
/// </summary>
public class EuIvKeyTests : CompatTestBase
{
    private static OverrideRules EuIvRules() => new(new[]
    {
        new OverrideRule("common", OverrideBehavior.PerKey, false),
        new OverrideRule("decisions", OverrideBehavior.PerKey, false, new[] { "country_decisions" }),
        new OverrideRule("events", OverrideBehavior.FirstWins, false, KeyField: "id"),
        new OverrideRule("interface", OverrideBehavior.LastWins, false, new[] { "spriteTypes" }, "name"),
    });

    private CompatResult Run(params CompatParticipant[] mods) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = mods, Rules = EuIvRules() });

    private static IEnumerable<CompatFinding> KeyFindings(CompatResult result) => result.Findings.Where(f => f.Key.Length > 0);

    [Fact]
    public void Events_with_different_ids_in_differently_named_files_do_not_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "events/AiExpandInfrastructureEvents.txt", "namespace = ai\ncountry_event = { id = ai.1 }\ncountry_event = { id = ai.2 }\n");
        Put(b, "events/ideagroups.txt", "namespace = ideagroups\ncountry_event = { id = ideagroups.100 }\n");

        Assert.Empty(KeyFindings(Run(a, b)));
    }

    [Fact]
    public void Same_event_id_in_differently_named_files_conflicts_and_the_first_wins()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "events/a.txt", "namespace = flavor\ncountry_event = { id = flavor.1 title = a }\n");
        Put(b, "events/b.txt", "namespace = flavor\nprovince_event = { id = flavor.1 title = b }\n");

        var finding = Assert.Single(KeyFindings(Run(a, b)));
        Assert.Equal("flavor.1", finding.Key);
        Assert.Equal(CompatKind.KeyConflict, finding.Kind);
        Assert.Equal("A", finding.Winner!.Name);
    }

    [Fact]
    public void Event_without_id_never_becomes_a_shared_key()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "events/a.txt", "country_event = { title = a }\n");
        Put(b, "events/b.txt", "country_event = { title = b }\n");

        Assert.Empty(KeyFindings(Run(a, b)));
    }

    [Fact]
    public void Decisions_are_keyed_by_their_name_inside_the_wrapper()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "decisions/a.txt", "country_decisions = { dec_a = { major = yes } dec_shared = { x = 1 } }\n");
        Put(b, "decisions/b.txt", "country_decisions = { dec_b = { major = yes } dec_shared = { x = 2 } }\n");

        var finding = Assert.Single(KeyFindings(Run(a, b)));
        Assert.Equal("country_decisions > dec_shared", finding.Key);
        Assert.Equal(CompatKind.KeyConflict, finding.Kind);
    }

    [Fact]
    public void Sprites_are_keyed_by_their_name_inside_spriteTypes()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "interface/a.gfx", "spriteTypes = {\n\tspriteType = { name = \"GFX_a\" texturefile = \"a.dds\" }\n\tspriteType = { name = \"GFX_x\" texturefile = \"x1.dds\" }\n}\n");
        Put(b, "interface/b.gfx", "spriteTypes = {\n\tspriteType = { name = \"GFX_b\" texturefile = \"b.dds\" }\n\tspriteType = { name = \"GFX_x\" texturefile = \"x2.dds\" }\n}\n");

        var finding = Assert.Single(KeyFindings(Run(a, b)));
        Assert.Equal("spriteTypes > GFX_x", finding.Key);
        Assert.Equal("B", finding.Winner!.Name);
    }

    [Fact]
    public void Game_rule_for_the_same_folder_keeps_the_shipped_key_settings()
    {
        var rules = new OverrideRules(
            new[] { new OverrideRule("events", OverrideBehavior.FirstWins, false, KeyField: "id") },
            new[] { new OverrideRule("events", OverrideBehavior.FirstWins, true) });

        var match = rules.Match("events/a.txt");

        Assert.True(match.Certain);
        Assert.Equal("id", match.KeyField);
    }

    [Fact]
    public void Profile_rule_carries_unwrap_and_key_field()
    {
        var profile = new ParadoxModTool.GameProfiles.GameProfile
        {
            OverrideRules =
            {
                new ParadoxModTool.GameProfiles.OverrideRuleEntry { Folder = "interface", Behavior = "LastWins", Unwrap = new List<string> { "spriteTypes" }, KeyField = "name" },
            },
        };

        var match = OverrideRules.FromProfile(profile).Match("interface/a.gfx");

        Assert.Equal(new[] { "spriteTypes" }, match.Unwrap);
        Assert.Equal("name", match.KeyField);
    }

    [Fact]
    public void Shipped_eu4_profile_keys_events_by_id_and_treats_country_files_as_whole_files()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "profiles", "eu4.json");
        var profile = ParadoxModTool.GameProfiles.GameProfileLoader.LoadFromFile(file);
        var rules = OverrideRules.FromProfile(profile);

        Assert.Equal("id", rules.Match("events/x.txt").KeyField);
        Assert.Contains("country_decisions", rules.Match("decisions/x.txt").Unwrap);
        Assert.Equal(OverrideBehavior.WholeFile, rules.Match("common/countries/France.txt").Behavior);
        Assert.Equal(OverrideBehavior.WholeFile, rules.Match("common/units/x.txt").Behavior);
        Assert.Equal("name", rules.Match("common/custom_gui/x.txt").KeyField);
        foreach (var listFolder in new[] { "common/estate_crown_land", "common/custom_country_colors", "common/dynasty_colors", "common/region_colors" })
        {
            Assert.Equal(OverrideBehavior.WholeFile, rules.Match(listFolder + "/x.txt").Behavior);
        }
    }

    [Fact]
    public void Custom_gui_entries_are_keyed_by_name_not_by_their_block()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/custom_gui/countrydecisionsview.txt", "custom_button = { name = artifice_open_inventions frame = 1 }\ncustom_icon = { name = shared_icon frame = 1 }\n");
        Put(b, "common/custom_gui/01_custom_gui_Idea_Variation.txt", "custom_button = { name = idea_variation_button frame = 2 }\ncustom_icon = { name = shared_icon frame = 2 }\n");
        var rules = new OverrideRules(new[] { new OverrideRule("common", OverrideBehavior.PerKey, false), new OverrideRule("common/custom_gui", OverrideBehavior.PerKey, false, KeyField: "name") });

        var findings = CompatAnalyzer.Analyze(new CompatRequest { Participants = new[] { a, b }, Rules = rules }).Findings.Where(f => f.Key.Length > 0).ToList();

        var finding = Assert.Single(findings);
        Assert.Equal("shared_icon", finding.Key);
    }
}
