using ParadoxModTool.ModCompat;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// EU IV: Events heißen nach ihrer <c>id</c>, Entscheidungen und GUI-Typen nach ihrem Namen in der gemeinsamen Hülle. Verschieden benannte
/// Dateien stehen nur dann in Konflikt, wenn sie dasselbe Event bzw. denselben Namen enthalten; der Patch schreibt die Hülle wieder mit.
/// </summary>
public class EuIvKeyTests : CompatchTestBase
{
    private CompatchInput EuIvInput(bool certain) => new()
    {
        VanillaFolder = VanillaDir,
        ModAFolder = ADir,
        ModBFolder = BDir,
        Rules = new OverrideRules(new[]
        {
            new OverrideRule("common", OverrideBehavior.PerKey, certain),
            new OverrideRule("decisions", OverrideBehavior.PerKey, certain, new[] { "country_decisions" }),
            new OverrideRule("events", OverrideBehavior.FirstWins, certain, KeyField: "id"),
            new OverrideRule("interface", OverrideBehavior.LastWins, certain, new[] { "guiTypes", "spriteTypes" }, "name"),
        }),
    };

    private static IEnumerable<EntryAddress> Keys(ResolvedEntries resolved) => resolved.Addresses.Keys.Where(a => a.Kind == AddressKind.Key);

    private CompatchRun RunAndCheck(bool certain)
    {
        var input = EuIvInput(certain);
        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, null);
        Assert.NotEmpty(run.Plan.Entries.Cast<object>().Concat(run.Plan.Files));
        AssertRoundTrip(input, run);
        return run;
    }

    [Fact]
    public void Events_with_different_ids_in_differently_named_files_are_not_compared()
    {
        PutA("events/AiExpandInfrastructureEvents.txt", "namespace = ai\ncountry_event = { id = ai.1 }\n");
        PutB("events/ideagroups.txt", "namespace = ideagroups\ncountry_event = { id = ideagroups.100 }\n");

        Assert.Empty(Keys(EffectiveEntryResolver.Resolve(EuIvInput(false))));
    }

    [Fact]
    public void Decisions_with_different_names_in_differently_named_files_are_not_compared()
    {
        PutA("decisions/a.txt", "country_decisions = { dec_a = { major = yes } }\n");
        PutB("decisions/b.txt", "country_decisions = { dec_b = { major = yes } }\n");

        Assert.Empty(Keys(EffectiveEntryResolver.Resolve(EuIvInput(false))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Same_event_id_in_differently_named_files_is_merged_as_one_event(bool certain)
    {
        PutVanilla("events/flavor.txt", "namespace = flavor\ncountry_event = { id = flavor.1 title = v option = { name = v } }\n");
        PutA("events/a_flavor.txt", "namespace = flavor\ncountry_event = { id = flavor.1 title = a option = { name = v } }\n");
        PutB("events/b_flavor.txt", "namespace = flavor\ncountry_event = { id = flavor.1 title = v option = { name = b } }\n");

        var run = RunAndCheck(certain);

        Assert.Contains(EntryAddress.ForKey("events", "flavor.1"), run.Resolved.Addresses.Keys);
        if (certain)
        {
            var entry = Assert.Single(run.Plan.Entries);
            Assert.Contains("country_event = {", entry.Text);
            Assert.Contains("id = flavor.1", entry.Text);
        }
    }

    [Fact]
    public void Same_decision_in_differently_named_files_is_one_address()
    {
        PutVanilla("decisions/x.txt", "country_decisions = { dec = { major = yes cost = 1 } }\n");
        PutA("decisions/a.txt", "country_decisions = { dec = { major = no cost = 1 } }\n");
        PutB("decisions/b.txt", "country_decisions = { dec = { major = yes cost = 2 } }\n");

        var keys = Keys(EffectiveEntryResolver.Resolve(EuIvInput(false))).ToList();

        Assert.Equal(new[] { EntryAddress.ForKey("decisions", "country_decisions > dec") }, keys);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Same_sprite_in_differently_named_files_is_written_inside_its_wrapper(bool certain)
    {
        PutVanilla("interface/v.gfx", "spriteTypes = {\n\tspriteType = { name = \"GFX_x\" texturefile = \"v.dds\" noOfFrames = 1 }\n}\n");
        PutA("interface/a.gfx", "spriteTypes = {\n\tspriteType = { name = \"GFX_x\" texturefile = \"a.dds\" noOfFrames = 1 }\n}\n");
        PutB("interface/b.gfx", "spriteTypes = {\n\tspriteType = { name = \"GFX_x\" texturefile = \"v.dds\" noOfFrames = 2 }\n}\n");

        var run = RunAndCheck(certain);

        Assert.Contains(EntryAddress.ForKey("interface", "spriteTypes > GFX_x"), run.Resolved.Addresses.Keys);
        if (certain)
        {
            Assert.StartsWith("spriteTypes = {", Assert.Single(run.Plan.Entries).Text);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Decisions_added_by_both_mods_to_the_same_file_are_combined(bool certain)
    {
        PutVanilla("decisions/x.txt", "country_decisions = {\n\tdec_v = { major = yes }\n}\n");
        PutA("decisions/x.txt", "country_decisions = {\n\tdec_v = { major = yes }\n\tdec_a = { major = yes }\n}\n");
        PutB("decisions/x.txt", "country_decisions = {\n\tdec_v = { major = no }\n\tdec_b = { major = yes }\n}\n");

        var run = RunAndCheck(certain);

        var keys = Keys(run.Resolved).Select(a => a.Key).ToList();
        Assert.Contains("country_decisions > dec_a", keys);
        Assert.Contains("country_decisions > dec_b", keys);
        Assert.Contains("country_decisions > dec_v", keys);
    }

    [Fact]
    public void Different_custom_buttons_are_never_merged_into_one_with_the_shipped_eu4_profile()
    {
        PutA("common/custom_gui/countrydecisionsview.txt", "custom_button = {\n\tname = artifice_open_inventions\n\tframe = 1\n\teffect = { a = yes }\n}\n");
        PutB("common/custom_gui/01_custom_gui_Idea_Variation.txt", "custom_button = {\n\tname = idea_variation_button\n\tframe = 2\n\teffect = { b = yes }\n}\n");
        var profile = ParadoxModTool.GameProfiles.GameProfileLoader.LoadFromFile(RepoFile("profiles/eu4.json"));
        var input = new CompatchInput { VanillaFolder = VanillaDir, ModAFolder = ADir, ModBFolder = BDir, Rules = OverrideRules.FromProfile(profile) };

        var resolved = EffectiveEntryResolver.Resolve(input);

        Assert.Empty(Keys(resolved));
    }

    [Fact]
    public void Modifier_definitions_with_different_keys_are_never_mixed()
    {
        const string Definition = "\tmodifier_definition = {{\n\t\ttype = privileges\n\t\tkey = {0}\n\t\ttrigger = {{ always = yes }}\n\t}}\n";
        string Special(params string[] keys) => "estate_special = {\n" + string.Concat(keys.Select(k => string.Format(Definition, k))) + "}\n";
        // Wie im echten Fall: Anbennar erweitert die Vanilla-Liste, das Addon bringt eine eigene, kurze Liste mit ähnlich gebauten Einträgen.
        PutVanilla("common/estates_preload/00_estate_modifiers.txt", Special("max_terms", "num_of_pronoiars"));
        PutA("common/estates_preload/00_estate_modifiers.txt", Special("max_terms", "num_of_pronoiars", "anb_mage_upkeep"));
        PutB("common/estates_preload/01_estates_preload_Idea_Variation.txt", Special("iv_state_maintenance_upkeep", "iv_base_institution_growth"));

        var run = CompatchEngine.Run(EuIvInput(false), PatchTarget.Compatch, null, null);

        static IEnumerable<string> Keys(IReadOnlyList<ParadoxModTool.VanillaDiff.ScriptEntry> entries) =>
            System.Text.RegularExpressions.Regex.Matches(ActionText.Render(entries), @"key = (\w+)").Select(m => m.Groups[1].Value).Distinct();

        // Eine Stelle darf nie den Eintrag mit einer Kennung auf der einen Seite und einer anderen Kennung auf der anderen Seite verbinden.
        var actions = run.Actions.Where(a => a.Address.Key == "estate_special").ToList();
        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var sides = new[] { action.Vanilla, action.A, action.B }.Where(e => e.Count > 0).Select(e => Keys(e).ToList()).Where(k => k.Count > 0).ToList();
            Assert.True(sides.Select(k => string.Join(",", k)).Distinct().Count() <= 1, $"{action.DisplayPath}: {string.Join(" | ", sides.Select(k => string.Join(",", k)))}");
        }
    }

    [Fact]
    public void Eu4_gui_types_are_keyed_by_name_inside_guiTypes()
    {
        PutA("interface/a.gui", "guiTypes = {\n\twindowType = { name = \"win\" size = { x = 1 } }\n}\n");
        PutB("interface/b.gui", "guiTypes = {\n\twindowType = { name = \"win\" size = { x = 2 } }\n\twindowType = { name = \"other\" }\n}\n");

        var keys = Keys(EffectiveEntryResolver.Resolve(EuIvInput(false))).Select(a => a.Key).ToList();

        Assert.Equal(new[] { "guiTypes > win" }, keys);
    }
}
