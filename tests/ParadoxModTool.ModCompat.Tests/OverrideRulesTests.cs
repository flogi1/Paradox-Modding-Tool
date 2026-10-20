using ParadoxModTool.GameProfiles;
using ParadoxModTool.Mods;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class OverrideRulesTests : TempDirTests
{
    private static OverrideRules Sample(IEnumerable<OverrideRule>? game = null) => new(
        new[]
        {
            new OverrideRule("*/common", OverrideBehavior.PerKey),
            new OverrideRule("*/common/defines", OverrideBehavior.PartialKeys),
            new OverrideRule("*/events", OverrideBehavior.FirstWins),
            new OverrideRule("*/gfx", OverrideBehavior.WholeFile),
        },
        game);

    [Fact]
    public void Longest_prefix_wins()
    {
        var match = Sample().Match("in_game/common/defines/00_defines.txt");
        Assert.Equal(OverrideBehavior.PartialKeys, match.Behavior);
        Assert.True(match.Known);
    }

    [Fact]
    public void Star_matches_any_phase_folder()
    {
        Assert.Equal(OverrideBehavior.PerKey, Sample().Match("main_menu/common/foo/a.txt").Behavior);
        Assert.Equal(OverrideBehavior.FirstWins, Sample().Match("loading_screen/events/e.txt").Behavior);
    }

    [Fact]
    public void Unknown_folder_is_per_key_and_uncertain()
    {
        var match = Sample().Match("in_game/setup/start/a.txt");
        Assert.Equal(OverrideBehavior.PerKey, match.Behavior);
        Assert.False(match.Certain);
        Assert.False(match.Known);
    }

    [Fact]
    public void Game_layer_replaces_shipped_rule_of_same_folder()
    {
        var rules = Sample(new[] { new OverrideRule("*/common", OverrideBehavior.WholeFile, Certain: false) });
        var match = rules.Match("in_game/common/foo/a.txt");
        Assert.Equal(OverrideBehavior.WholeFile, match.Behavior);
        Assert.False(match.Certain);
    }

    [Fact]
    public void Game_layer_can_add_a_new_folder()
    {
        var rules = Sample(new[] { new OverrideRule("in_game/setup", OverrideBehavior.WholeFile) });
        Assert.Equal(OverrideBehavior.WholeFile, rules.Match("in_game/setup/a.txt").Behavior);
    }

    [Fact]
    public void Scope_of_per_key_rule_is_the_type_folder()
    {
        Assert.Equal("in_game/common/building_types", Sample().Match("in_game/common/building_types/a.txt").Scope);
        Assert.Equal("in_game/common/building_types", Sample().Match("in_game/common/building_types/sub/b.txt").Scope);
    }

    [Fact]
    public void Scope_of_other_rules_is_the_rule_folder()
    {
        Assert.Equal("in_game/events", Sample().Match("in_game/events/x/a.txt").Scope);
    }

    [Fact]
    public void Match_ignores_case_and_backslashes()
    {
        Assert.Equal(OverrideBehavior.FirstWins, Sample().Match(@"IN_GAME\Events\a.txt").Behavior);
    }

    [Fact]
    public void Store_roundtrips_and_ignores_broken_file()
    {
        var file = Path.Combine(Dir, "compat", "rules.json");
        OverrideRulesStore.Save(file, new[] { new OverrideRule("in_game/setup", OverrideBehavior.WholeFile, false) });
        var loaded = OverrideRulesStore.Load(file);
        Assert.Equal(new[] { new OverrideRule("in_game/setup", OverrideBehavior.WholeFile, false) }, loaded);

        File.WriteAllText(file, "{ kaputt");
        Assert.Empty(OverrideRulesStore.Load(file));
        Assert.Empty(OverrideRulesStore.Load(Path.Combine(Dir, "fehlt.json")));
    }

    [Fact]
    public void Eu5_profile_ships_the_wiki_table()
    {
        var profile = GameProfileLoader.LoadFromFile(RepoFile("profiles/eu5.json"));
        var rules = OverrideRules.FromProfile(profile);
        Assert.Equal(OverrideBehavior.PerKey, rules.Match("in_game/common/building_types/a.txt").Behavior);
        Assert.True(rules.Match("in_game/common/building_types/a.txt").Certain);
        Assert.Equal(OverrideBehavior.FirstWins, rules.Match("in_game/events/a.txt").Behavior);
        Assert.Equal(OverrideBehavior.LastWins, rules.Match("in_game/gui/a.gui").Behavior);
        Assert.Equal(OverrideBehavior.PartialKeys, rules.Match("loading_screen/common/defines/a.txt").Behavior);
        Assert.Equal(OverrideBehavior.AppendOnly, rules.Match("in_game/common/on_action/a.txt").Behavior);
        Assert.Equal(OverrideBehavior.LocalizationKeys, rules.Match("in_game/localization/english/a_l_english.yml").Behavior);
    }

    [Fact]
    public void Eu4_profile_ships_the_wiki_table()
    {
        var profile = GameProfileLoader.LoadFromFile(RepoFile("profiles/eu4.json"));
        Assert.Equal("eu4", profile.Id);
        Assert.Equal("Europa Universalis IV", profile.DisplayName);
        Assert.Equal("alphabetical-filename-wins", profile.LoadOrderRule);
        Assert.Equal("wiki", profile.ScriptDocsSourceKind);
        Assert.Equal("scriptdocs/eu4.json", profile.ScriptDocsWikiFile);
        Assert.Equal(ModMetadata.ClassicSiblingMetadataFile, profile.MetadataFile);

        var rules = OverrideRules.FromProfile(profile);
        var commonMatch = rules.Match("common/religions/00_religion.txt");
        Assert.Equal(OverrideBehavior.PerKey, commonMatch.Behavior);
        Assert.False(commonMatch.Certain);

        var definesMatch = rules.Match("common/defines/00_mymod.lua");
        Assert.Equal(OverrideBehavior.PartialKeys, definesMatch.Behavior);
        Assert.True(definesMatch.Certain);
    }

    [Fact]
    public void Stellaris_profile_names_the_root_scope_of_each_event_block()
    {
        var profile = GameProfileLoader.LoadFromFile(RepoFile("profiles/stellaris.json"));

        Assert.Null(profile.DefaultEventType);
        Assert.Equal("country", profile.EventBlockScopes!["country_event"]);
        Assert.Equal("galactic_object", profile.EventBlockScopes["system_event"]);
        Assert.Equal("none", profile.EventBlockScopes["event"]);
        Assert.False(profile.EventBlockScopes.ContainsKey("inline_script"));
        Assert.Null(GameProfileLoader.LoadFromFile(RepoFile("profiles/ck3.json")).EventBlockScopes);
    }

    [Fact]
    public void Ck3_profile_ships_the_wiki_table()
    {
        var profile = GameProfileLoader.LoadFromFile(RepoFile("profiles/ck3.json"));
        Assert.Equal(ModMetadata.ClassicSiblingMetadataFile, profile.MetadataFile);
        Assert.Equal("logs", profile.ScriptDocsFolder);
        Assert.Empty(profile.KeyOperators);
        Assert.Equal("character_event", profile.DefaultEventType);
        Assert.True(profile.ReplacePathHidesEarlierMods);

        var rules = OverrideRules.FromProfile(profile);
        void Check(string path, OverrideBehavior behavior, bool certain)
        {
            var match = rules.Match(path);
            Assert.True(match.Behavior == behavior, $"{path}: {match.Behavior}");
            Assert.True(match.Certain == certain, $"{path}: certain {match.Certain}");
        }

        Check("common/traits/00_traits.txt", OverrideBehavior.PerKey, true);
        Check("common/defines/x.txt", OverrideBehavior.PartialKeys, true);
        Check("common/on_action/x.txt", OverrideBehavior.AppendOnly, false);
        Check("common/religion/religion_types/x.txt", OverrideBehavior.WholeFile, true);
        Check("common/landed_titles/00_landed_titles.txt", OverrideBehavior.PerKey, false);
        Check("events/x.txt", OverrideBehavior.FirstWins, false);
        Check("gui/x.gui", OverrideBehavior.FirstWins, true);
        Check("localization/english/x_l_english.yml", OverrideBehavior.LocalizationKeys, true);
        Check("history/characters/x.txt", OverrideBehavior.PerKey, false);
        Check("gfx/x.dds", OverrideBehavior.WholeFile, false);
        Check("map_data/x.png", OverrideBehavior.WholeFile, false);
    }

    [Fact]
    public void Eu5_profile_uses_docs_folder_and_replace_path_hides_earlier_mods_below_the_roots()
    {
        var profile = GameProfileLoader.LoadFromFile(RepoFile("profiles/eu5.json"));
        Assert.Equal("docs", profile.ScriptDocsFolder);
        Assert.True(profile.ReplacePathHidesEarlierMods);
        Assert.Equal(new[] { "in_game", "main_menu", "loading_screen" }, profile.ReplacePathRoots);
    }

    [Fact]
    public void Eu4_profile_replace_path_only_hides_vanilla()
    {
        var profile = GameProfileLoader.LoadFromFile(RepoFile("profiles/eu4.json"));
        Assert.False(profile.ReplacePathHidesEarlierMods);
        Assert.Empty(profile.ReplacePathRoots);
    }

    [Fact]
    public void Only_the_eu4_profile_names_interface_as_gui_crash_root()
    {
        Assert.Equal(new[] { "interface" }, GameProfileLoader.LoadFromFile(RepoFile("profiles/eu4.json")).ReplacePathGuiCrashRoots);
        Assert.Empty(GameProfileLoader.LoadFromFile(RepoFile("profiles/eu5.json")).ReplacePathGuiCrashRoots);
        Assert.Empty(GameProfileLoader.LoadFromFile(RepoFile("profiles/ck3.json")).ReplacePathGuiCrashRoots);
    }

    [Fact]
    public void Eu5_profile_lists_11_localization_languages()
    {
        var languages = GameProfileLoader.LoadFromFile(RepoFile("profiles/eu5.json")).LocalizationLanguages;

        Assert.Equal(11, languages.Count);
        Assert.Equal("english", languages[0]);
        Assert.Contains("braz_por", languages);
        Assert.Contains("simp_chinese", languages);
    }

    [Fact]
    public void Profile_without_script_docs_source_defaults_to_log()
    {
        var profile = new GameProfile { Id = "x" };
        Assert.Equal("log", profile.ScriptDocsSourceKind);
        Assert.Null(profile.ScriptDocsWikiFile);
    }

    [Fact]
    public void Other_profile_without_table_is_all_uncertain()
    {
        var rules = OverrideRules.FromProfile(new GameProfile { Id = "ck3" });
        var match = rules.Match("common/foo/a.txt");
        Assert.False(match.Certain);
        Assert.False(match.Known);
    }

    [Fact]
    public void Rule_operators_come_from_the_profile_and_reach_the_match()
    {
        var profile = new GameProfile
        {
            Id = "x",
            InjectAppends = true,
            OverrideRules =
            {
                new OverrideRuleEntry { Folder = "common", Behavior = "LastWins", Operators = new() },
                new OverrideRuleEntry { Folder = "common/scripted_effects", Behavior = "PerKey", Operators = new() { "REPLACE" } },
                new OverrideRuleEntry { Folder = "common/buildings", Behavior = "PerKey" },
            },
        };
        var rules = OverrideRules.FromProfile(profile);

        Assert.True(rules.InjectAppends);
        Assert.Empty(rules.Match("common/history/x.txt").Operators!);
        Assert.Equal(new[] { "REPLACE" }, rules.Match("common/scripted_effects/x.txt").Operators);
        Assert.Null(rules.Match("common/buildings/x.txt").Operators);
        Assert.True(rules.Match("common/buildings/x.txt").Allows("INJECT", new[] { "INJECT" }));
        Assert.False(rules.Match("common/scripted_effects/x.txt").Allows("INJECT", new[] { "INJECT", "REPLACE" }));
    }

    [Fact]
    public void Vic3_unlisted_common_folder_is_its_own_scope_without_operators()
    {
        var rules = OverrideRules.FromProfile(GameProfileLoader.LoadFromFile(RepoFile("profiles/vic3.json")));

        var unlisted = rules.Match("common/opinion_modifiers/x.txt");
        Assert.Equal("common/opinion_modifiers", unlisted.Scope);
        Assert.Equal(OverrideBehavior.LastWins, unlisted.Behavior);
        Assert.Empty(unlisted.Operators!);
        Assert.Equal(OverrideBehavior.PerKey, rules.Match("common/buildings/x.txt").Behavior);
        Assert.Equal("common/buildings", rules.Match("common/buildings/x.txt").Scope);
        Assert.Equal(OverrideBehavior.AppendOnly, rules.Match("common/history/states/x.txt").Behavior);
        Assert.True(rules.FirstWinsInLoadOrder);
    }

    [Fact]
    public void User_rule_for_an_unlisted_folder_inherits_the_operators_of_its_shipped_parent()
    {
        var rules = new OverrideRules(
            new[] { new OverrideRule("common", OverrideBehavior.LastWins, Operators: Array.Empty<string>()) },
            new[] { new OverrideRule("common/coat_of_arms", OverrideBehavior.PerKey) });

        Assert.Empty(rules.Match("common/coat_of_arms/x.txt").Operators!);
    }

    [Fact]
    public void User_rule_keeps_the_shipped_operators()
    {
        var rules = new OverrideRules(
            new[] { new OverrideRule("common/scripted_effects", OverrideBehavior.PerKey, Operators: new[] { "REPLACE" }) },
            new[] { new OverrideRule("common/scripted_effects", OverrideBehavior.LastWins) });

        var match = rules.Match("common/scripted_effects/x.txt");
        Assert.Equal(OverrideBehavior.LastWins, match.Behavior);
        Assert.Equal(new[] { "REPLACE" }, match.Operators);
    }

    [Fact]
    public void Stellaris_profile_maps_the_wiki_overwrite_table()
    {
        var rules = OverrideRules.FromProfile(GameProfileLoader.LoadFromFile(RepoFile("profiles/stellaris.json")));

        Assert.True(rules.LastWinsInLoadOrder);
        Assert.True(rules.FirstWinsInLoadOrder);
        Assert.True(rules.LoadOrderCaseSensitive);
        var buildings = rules.Match("common/buildings/x.txt");
        Assert.Equal(OverrideBehavior.LastWins, buildings.Behavior);
        Assert.True(buildings.Certain);
        Assert.Empty(buildings.Operators ?? Array.Empty<string>());
        Assert.Equal(OverrideBehavior.FirstWins, rules.Match("common/governments/authorities/x.txt").Behavior);
        Assert.Equal(OverrideBehavior.LastWins, rules.Match("common/governments/civics/x.txt").Behavior);
        Assert.Equal(OverrideBehavior.FirstWins, rules.Match("common/scripted_variables/x.txt").Behavior);
        Assert.Equal(OverrideBehavior.WholeFile, rules.Match("common/traits/x.txt").Behavior);
        Assert.Equal(OverrideBehavior.AppendOnly, rules.Match("common/on_actions/x.txt").Behavior);
        Assert.Equal(OverrideBehavior.AppendOnly, rules.Match("common/random_names/base/x.txt").Behavior);
        Assert.Equal(OverrideBehavior.PartialKeys, rules.Match("common/defines/x.txt").Behavior);
        Assert.Equal(OverrideBehavior.LastWins, rules.Match("common/diplomacy_economy/x.txt").Behavior);

        // Gemessen 2026-10-16 mit PMT Order Probe A/B und PMT RP Gui Probe.
        var profile = GameProfileLoader.LoadFromFile(RepoFile("profiles/stellaris.json"));
        Assert.True(profile.ReplacePathHidesEarlierMods);
        Assert.Empty(profile.ReplacePathGuiCrashRoots);
        Assert.Equal("errorlog/stellaris.json", profile.ErrorLogRules);
        Assert.All(profile.FileTypes.Where(f => f.Extension != ".yml"), f => Assert.Equal("utf-8", f.DefaultEncoding));
        Assert.Equal("utf-8-bom", profile.FileTypes.Single(f => f.Extension == ".yml").DefaultEncoding);

        var unlisted = rules.Match("common/astral_rifts/x.txt");
        Assert.Equal("common/astral_rifts", unlisted.Scope);
        Assert.Equal(OverrideBehavior.LastWins, unlisted.Behavior);
        Assert.False(unlisted.Certain);

        var events = rules.Match("events/x.txt");
        Assert.Equal(OverrideBehavior.FirstWins, events.Behavior);
        Assert.Equal("id", events.KeyField);
        var gui = rules.Match("interface/x.gui");
        Assert.Equal(OverrideBehavior.LastWins, gui.Behavior);
        Assert.False(gui.Certain);
        Assert.Contains("guiTypes", gui.Unwrap);
        Assert.Equal(OverrideBehavior.LocalizationKeys, rules.Match("localisation/english/x_l_english.yml").Behavior);
    }

    /// <summary>Stellaris-Ordner, deren Einträge alle gleich heißen (<c>component_set = { key = … }</c>): der Name steht im Feld (gezählt in 4.5.2).</summary>
    [Theory]
    [InlineData("common/component_sets/x.txt", "key")]
    [InlineData("common/component_templates/x.txt", "key")]
    [InlineData("common/message_types/x.txt", "key")]
    [InlineData("common/special_projects/x.txt", "key")]
    [InlineData("common/global_ship_designs/x.txt", "name")]
    [InlineData("common/scripted_loc/x.txt", "name")]
    [InlineData("common/ship_behaviors/x.txt", "name")]
    public void Stellaris_folders_with_one_block_name_take_the_key_from_a_field(string path, string field)
    {
        var rules = OverrideRules.FromProfile(GameProfileLoader.LoadFromFile(RepoFile("profiles/stellaris.json")));

        Assert.Equal(field, rules.Match(path).KeyField);
    }

    [Fact]
    public void Stellaris_start_screen_messages_count_as_whole_files()
    {
        var rules = OverrideRules.FromProfile(GameProfileLoader.LoadFromFile(RepoFile("profiles/stellaris.json")));

        Assert.Equal(OverrideBehavior.WholeFile, rules.Match("common/start_screen_messages/x.txt").Behavior);
    }

    /// <summary>
    /// Stellaris-Unterordner mit eigener Objektart (Lebensstandards, Ratsmitglieder, Fokuskarten …): eigener Bereich, damit der Kompatch dort schreibt,
    /// wo das Spiel liest; die Reihenfolge der Unterordner ist nicht gemessen, also unsicher.
    /// </summary>
    [Theory]
    [InlineData("common/species_rights/living_standards/x.txt")]
    [InlineData("common/species_rights/citizenship_types/x.txt")]
    [InlineData("common/governments/councilors/x.txt")]
    [InlineData("common/country_focus/focus_cards/x.txt")]
    [InlineData("common/patrons/callings/x.txt")]
    [InlineData("common/star_classes/randomizers/x.txt")]
    [InlineData("common/technology_ages/preftl/x.txt")]
    public void Stellaris_subfolders_with_their_own_object_type_are_their_own_uncertain_scope(string path)
    {
        var rules = OverrideRules.FromProfile(GameProfileLoader.LoadFromFile(RepoFile("profiles/stellaris.json")));

        var match = rules.Match(path);

        Assert.Equal(path[..path.LastIndexOf('/')], match.Scope);
        Assert.Equal(OverrideBehavior.LastWins, match.Behavior);
        Assert.False(match.Certain);
    }
}
