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
}
