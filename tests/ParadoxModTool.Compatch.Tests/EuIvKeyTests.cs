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

    [Fact]
    public void Eu4_windows_first_wins_and_sprites_last_wins_in_load_order()
    {
        // Gemessen 2026-10-17: Fenster gewinnt die zuerst geladene Datei, Sprites die zuletzt geladene, über Vanilla und Mods nach Dateinamen.
        var input = new CompatchInput
        {
            VanillaFolder = VanillaDir,
            ModAFolder = ADir,
            ModBFolder = BDir,
            Rules = new OverrideRules(new[]
            {
                new OverrideRule("interface", OverrideBehavior.LastWins, true, new[] { "guiTypes", "spriteTypes" }, "name",
                    UnwrapBehaviors: new Dictionary<string, OverrideBehavior> { ["guiTypes"] = OverrideBehavior.FirstWins, ["spriteTypes"] = OverrideBehavior.LastWins }),
            }),
        };
        PutA("interface/a.gui", "guiTypes = {\n\twindowType = { name = \"win\" size = { x = 1 } }\n}\n");
        PutB("interface/b.gui", "guiTypes = {\n\twindowType = { name = \"win\" size = { x = 2 } }\n}\n");
        PutA("interface/z.gfx", "spriteTypes = {\n\tspriteType = { name = \"GFX_x\" texturefile = \"a.dds\" }\n}\n");
        PutB("interface/b.gfx", "spriteTypes = {\n\tspriteType = { name = \"GFX_x\" texturefile = \"b.dds\" }\n}\n");

        var resolved = EffectiveEntryResolver.Resolve(input);

        var window = resolved.Addresses[EntryAddress.ForKey("interface", "guiTypes > win")];
        Assert.Equal(OverrideBehavior.FirstWins, window.Behavior);
        Assert.Equal("interface/a.gui", window.NoPatch.RelativePath);
        var sprite = resolved.Addresses[EntryAddress.ForKey("interface", "spriteTypes > GFX_x")];
        Assert.Equal(OverrideBehavior.LastWins, sprite.Behavior);
        Assert.Equal("interface/z.gfx", sprite.NoPatch.RelativePath);
    }

    private CompatchInput ClassicInput(bool euIv) => new()
    {
        VanillaFolder = VanillaDir,
        ModAFolder = ADir,
        ModBFolder = BDir,
        Rules = euIv
            ? new OverrideRules(new[]
            {
                new OverrideRule("interface", OverrideBehavior.LastWins, true, new[] { "guiTypes", "spriteTypes", "objectTypes" }, "name",
                    UnwrapBehaviors: new Dictionary<string, OverrideBehavior>
                    {
                        ["guiTypes"] = OverrideBehavior.FirstWins, ["spriteTypes"] = OverrideBehavior.LastWins, ["objectTypes"] = OverrideBehavior.LastWins,
                    }),
            })
            : new OverrideRules(new[] { new OverrideRule("interface", OverrideBehavior.LastWins, true, new[] { "guiTypes", "spriteTypes", "objectTypes" }, "name") })
            {
                FirstWinsInLoadOrder = true, LastWinsInLoadOrder = true, LoadOrderCaseSensitive = true,
            },
    };

    private const string WindowV = "guiTypes = {\n\tcontainerWindowType = { name = \"win\" size = { x = 1 y = 1 } }\n}\n";
    private const string WindowA = "guiTypes = {\n\tcontainerWindowType = { name = \"win\" size = { x = 2 y = 1 } }\n}\n";
    private const string WindowB = "guiTypes = {\n\tcontainerWindowType = { name = \"win\" size = { x = 1 y = 2 } }\n}\n";
    private const string SpriteV = "spriteTypes = {\n\tspriteType = { name = \"GFX_x\" texturefile = \"v.dds\" noOfFrames = 1 }\n}\n";
    private const string SpriteA = "spriteTypes = {\n\tspriteType = { name = \"GFX_x\" texturefile = \"a.dds\" noOfFrames = 1 }\n}\n";
    private const string SpriteB = "spriteTypes = {\n\tspriteType = { name = \"GFX_x\" texturefile = \"v.dds\" noOfFrames = 2 }\n}\n";

    private string PatchFileFor(CompatchInput input, string key)
    {
        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, null);
        return Assert.Single(run.Plan.Entries, e => e.Address.Key == key).TargetFile;
    }

    [Fact]
    public void Eu4_window_patch_is_a_gui_file_loading_first()
    {
        PutVanilla("interface/v.gui", WindowV);
        PutA("interface/a.gui", WindowA);
        PutB("interface/b.gui", WindowB);

        Assert.Equal("interface/00_" + PatchPlanner.OwnFileName + ".gui", PatchFileFor(ClassicInput(euIv: true), "guiTypes > win"));
    }

    [Fact]
    public void Eu4_sprite_patch_is_a_gfx_file_loading_last()
    {
        PutVanilla("interface/core.gfx", SpriteV);
        PutA("interface/mod_a.gfx", SpriteA);
        PutB("interface/mod_b.gfx", SpriteB);

        Assert.Equal("interface/zz_" + PatchPlanner.OwnFileName + ".gfx", PatchFileFor(ClassicInput(euIv: true), "spriteTypes > GFX_x"));
    }

    [Fact]
    public void Hoi4_window_patch_goes_into_the_subfolder_that_loads_last()
    {
        // Unterordner laden nach den Dateien direkt in interface/; ein zz_-Patch dort verlöre gegen interface/sub/mod_b.gui.
        PutVanilla("interface/core.gui", WindowV);
        PutA("interface/mod_a.gui", WindowA);
        PutB("interface/sub/mod_b.gui", WindowB);

        Assert.Equal("interface/sub/zz_" + PatchPlanner.OwnFileName + ".gui", PatchFileFor(ClassicInput(euIv: false), "guiTypes > win"));
    }

    [Fact]
    public void Hoi4_sprite_patch_is_a_gfx_file()
    {
        PutVanilla("interface/core.gfx", SpriteV);
        PutA("interface/mod_a.gfx", SpriteA);
        PutB("interface/mod_b.gfx", SpriteB);

        Assert.Equal("interface/zz_" + PatchPlanner.OwnFileName + ".gfx", PatchFileFor(ClassicInput(euIv: false), "spriteTypes > GFX_x"));
    }

    [Fact]
    public void Hoi4_common_patch_stays_in_the_root_even_if_a_subfolder_loads_last()
    {
        // Die Unterordner-Ablage gilt nur für klassische interface-Hüllen (guiTypes, spriteTypes, objectTypes); andere LastWins-Ordner mit
        // Ladereihenfolge (HOI IV, Stellaris spielweit) behalten zz_ direkt im Bereich.
        var input = new CompatchInput
        {
            VanillaFolder = VanillaDir,
            ModAFolder = ADir,
            ModBFolder = BDir,
            Rules = new OverrideRules(new[] { new OverrideRule("common/scripted_effects", OverrideBehavior.LastWins) })
            {
                FirstWinsInLoadOrder = true, LastWinsInLoadOrder = true, LoadOrderCaseSensitive = true,
            },
        };
        PutVanilla("common/scripted_effects/core.txt", "effect_x = { add_political_power = 1 add_stability = 1 }\neffect_v = { add_war_support = 1 }\n");
        PutA("common/scripted_effects/mod_a.txt", "effect_x = { add_political_power = 2 add_stability = 1 }\neffect_a = { add_war_support = 1 }\n");
        PutB("common/scripted_effects/sub/mod_b.txt", "effect_x = { add_political_power = 1 add_stability = 2 }\neffect_b = { add_war_support = 1 }\n");

        var run = CompatchEngine.Run(input, PatchTarget.Compatch, null, null);

        var entry = Assert.Single(run.Plan.Entries, e => e.Address.Key == "effect_x");
        Assert.Equal("common/scripted_effects/zz_" + PatchPlanner.OwnFileName + ".txt", entry.TargetFile);
    }
}
