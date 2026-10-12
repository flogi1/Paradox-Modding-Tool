using ParadoxModTool.Generators.Candidates;
using ParadoxModTool.Generators.Editing;
using ParadoxModTool.Generators.Tests.Iv2;
using Xunit;

namespace ParadoxModTool.Generators.Tests;

/// <summary>Die Kandidatenliste des Pakets iv2-dynamic-ideas: Quellen, benutzte Modifier, Gruppen und Einstellungen.</summary>
public sealed class CandidateListTests : IDisposable
{
    private const string ArmyFile = "main_menu/common/static_modifiers/01_IV2_dynamic_national_ideas_army.txt";

    private readonly TempFolder _temp = new();
    private readonly GeneratorPackage _package;

    public CandidateListTests()
    {
        _package = GeneratorPackageLoader.Load(
            Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas"), _temp.PathOf("data"), shipped: true, uiLanguage: "en");
    }

    public void Dispose() => _temp.Dispose();

    private CandidatesDef Def => _package.Candidates!;

    private static IReadOnlyList<CandidateSource> Sources(params string[] names) => names.Select(n => new CandidateSource(n, false, false)).ToList();

    private IReadOnlyList<CandidateRow> Build(IReadOnlyList<CandidateSource> sources, CandidateSettings? settings = null, IReadOnlySet<string>? used = null,
        IReadOnlyDictionary<string, string>? inStates = null) =>
        CandidateList.Build(Def, sources, used ?? new HashSet<string>(), inStates ?? new Dictionary<string, string>(), settings ?? new CandidateSettings(), _ => null);

    private string GroupOf(string modifier) => Build(Sources(modifier)).Single().Group;

    [Fact]
    public void Merge_takes_country_from_definitions_and_script_docs()
    {
        var merged = CandidateSources.Merge("country",
            new (string, string, bool)[] { ("a", "country", false), ("b", "", false), ("c", "all", false), ("d", "location", false), ("f", "Country", true) },
            new (string, IReadOnlyList<string>, bool)[]
            {
                ("e", new[] { "Country" }, true), ("a", new[] { "Country" }, true), ("b", new[] { "Location" }, false),
            });

        Assert.Equal(new[] { "a", "e", "f" }, merged.Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.True(merged.Single(s => s.Name == "a").IsNew);
        Assert.True(merged.Single(s => s.Name == "e").IsNew);
        Assert.True(merged.Single(s => s.Name == "f").Boolean);
        Assert.False(merged.Single(s => s.Name == "f").IsNew);
    }

    [Fact]
    public void Used_ignores_comments_historic_blocks_and_game_data()
    {
        _temp.Write("mod/" + ArmyFile, """
            national_idea_modifier_army_1 = { game_data = { category = country } global_tax_modifier = 0.1 }
            national_idea_modifier_army_1_2 = { game_data = { category = country } land_morale = 0.1 }
            national_idea_modifier_army_2 = { game_data = { category = country }
            	# disabled 2026-10-12: test
            	#naval_morale = 0.1
            }
            national_idea_modifier_army_1_extra = { discipline = 0.1 }
            national_idea_modifier_CAS_1 = { global_manpower = 0.1 }
            """);

        var used = CandidateList.Used(_package, _temp.PathOf("mod"));

        Assert.Equal(new[] { "global_tax_modifier", "land_morale" }, used.OrderBy(u => u, StringComparer.Ordinal));
    }

    [Fact]
    public void Used_reads_only_matching_files()
    {
        _temp.Write("mod/common/other.txt", "national_idea_modifier_army_1 = { global_tax_modifier = 0.1 }");

        Assert.Empty(CandidateList.Used(_package, _temp.PathOf("mod")));
        Assert.Empty(CandidateList.Used(_package, _temp.PathOf("missing")));
    }

    [Theory]
    [InlineData("global_wine_output_modifier", "goodsOutput")]
    [InlineData("ban_exports_of_wine", "goodsOther")]
    [InlineData("wine_impacts_inflation", "goodsOther")]
    [InlineData("global_wine_establishment_speed", "goodsOther")]
    [InlineData("global_building_establishment_speed", "")]
    [InlineData("global_production_efficiency", "")]
    [InlineData("diplomatic_range", "")]
    [InlineData("dhimmi_estate_min_tax", "estates")]
    [InlineData("ask_clergy_for_legitimacy_cost_modifier", "estates")]
    [InlineData("convert_religion_cost_modifier", "religion")]
    [InlineData("allow_assembly_parliament", "government")]
    [InlineData("crackdown_their_strongholds_cost_modifier", "actionCost")]
    [InlineData("p_building_age_4_reformation_cost_modifier", "actionCost")]
    [InlineData("can_convert_galleys_to_light", "unlock")]
    [InlineData("atoll_proximity_impact", "proximity")]
    [InlineData("national_smallpox_growth_modifier", "diseaseMovement")]
    public void Groups_assign_by_first_match(string modifier, string group) => Assert.Equal(group, GroupOf(modifier));

    [Fact]
    public void Goods_groups_hidden_by_default()
    {
        var sources = Sources("global_wine_output_modifier", "ban_exports_of_wine", "diplomatic_range", "convert_religion_cost_modifier");

        var rows = Build(sources).ToDictionary(r => r.Key);
        Assert.True(rows["global_wine_output_modifier"].Hidden);
        Assert.True(rows["ban_exports_of_wine"].Hidden);
        Assert.False(rows["diplomatic_range"].Hidden);
        Assert.False(rows["convert_religion_cost_modifier"].Hidden);

        var shown = Build(sources, new CandidateSettings { HiddenGroups = { ["goodsOutput"] = false } }).ToDictionary(r => r.Key);
        Assert.False(shown["global_wine_output_modifier"].Hidden);
        Assert.True(shown["ban_exports_of_wine"].Hidden);

        var hidden = Build(sources, new CandidateSettings { HiddenGroups = { ["religion"] = true } }).ToDictionary(r => r.Key);
        Assert.True(hidden["convert_religion_cost_modifier"].Hidden);
    }

    [Fact]
    public void Custom_regex_hides_and_bad_regex_is_ignored()
    {
        var sources = Sources("global_production_efficiency", "diplomatic_range");

        var hidden = Build(sources, new CandidateSettings { CustomHide = "^global_production" }).ToDictionary(r => r.Key);
        Assert.True(hidden["global_production_efficiency"].Hidden);
        Assert.False(hidden["diplomatic_range"].Hidden);

        var bad = Build(sources, new CandidateSettings { CustomHide = "(" });
        Assert.All(bad, r => Assert.False(r.Hidden));
    }

    [Fact]
    public void Rows_sort_by_group_then_new_then_name()
    {
        var sources = new[]
        {
            new CandidateSource("aaa_cost_modifier", false, false), new CandidateSource("tithe_cost_modifier", false, false),
            new CandidateSource("alpha", false, false), new CandidateSource("zeta", true, false),
        };

        var rows = Build(sources);

        Assert.Equal(new[] { "zeta", "alpha", "tithe_cost_modifier", "aaa_cost_modifier" }, rows.Select(r => r.Key));
    }

    [Fact]
    public void Used_modifiers_are_not_listed_and_display_name_is_used()
    {
        var rows = CandidateList.Build(Def, Sources("b_mod", "a_mod"), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A_MOD" },
            new Dictionary<string, string>(), new CandidateSettings(), key => key == "b_mod" ? "Bee" : null);

        var row = Assert.Single(rows);
        Assert.Equal("b_mod", row.Key);
        Assert.Equal("Bee", row.Name);
    }

    [Fact]
    public void Not_as_idea_flags_row_and_undo_clears()
    {
        var settings = new CandidateSettings { NotAsIdea = { "land_morale" } };

        Assert.True(Build(Sources("land_morale"), settings).Single().NotAsIdea);

        settings.NotAsIdea.Remove("land_morale");
        Assert.False(Build(Sources("land_morale"), settings).Single().NotAsIdea);
    }

    [Fact]
    public void Row_marks_modifier_in_work_state()
    {
        var state = GeneratorState.CreateEmpty(_package, new[] { "english" });
        PackageKey.Apply(_package, state, "army_9");
        state.Head.Children["levels"][1].Values["modifiers"] = "naval_morale = ";
        GeneratorStateStore.Save(_package.DataFolder, state);

        var inStates = CandidateList.InWorkStates(_package);
        var rows = Build(Sources("naval_morale", "land_morale"), inStates: inStates).ToDictionary(r => r.Key);

        Assert.Equal("army_9", rows["naval_morale"].StateKey);
        Assert.Null(rows["land_morale"].StateKey);
    }

    [Fact]
    public void Used_skips_matching_key_with_scalar_value()
    {
        _temp.Write("mod/" + ArmyFile, """
            national_idea_modifier_army_3 = yes
            national_idea_modifier_army_1 = { game_data = { category = country } land_morale = 0.1 }
            """);

        var used = CandidateList.Used(_package, _temp.PathOf("mod"));

        Assert.Equal(new[] { "land_morale" }, used);
    }

    [Fact]
    public void InWorkStates_skips_unreadable_state_and_keeps_the_others()
    {
        foreach (var (key, modifier) in new[] { ("army_8", "land_morale"), ("army_9", "naval_morale") })
        {
            var state = GeneratorState.CreateEmpty(_package, new[] { "english" });
            PackageKey.Apply(_package, state, key);
            state.Head.Children["levels"][1].Values["modifiers"] = modifier + " = ";
            GeneratorStateStore.Save(_package.DataFolder, state);
        }

        // army_8 ist gesperrt (zweiter Prozess schreibt gerade) -> nur dieser Stand fehlt.
        using (new FileStream(Path.Combine(_package.DataFolder, "states", "army_8.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var inStates = CandidateList.InWorkStates(_package);

            Assert.False(inStates.ContainsKey("land_morale"));
            Assert.Equal("army_9", inStates["naval_morale"]);
        }
    }

    [Fact]
    public void Runaway_regex_counts_as_no_match()
    {
        var name = new string('a', 40) + "!";

        var row = Build(Sources(name), new CandidateSettings { CustomHide = "^(a+)+$" }).Single();

        Assert.False(row.Hidden);
    }

    [Fact]
    public void Settings_store_round_trip_and_broken_file()
    {
        var folder = _temp.PathOf("settings");
        Assert.Empty(CandidateSettingsStore.Load(folder).NotAsIdea);

        CandidateSettingsStore.Save(folder, new CandidateSettings
        {
            NotAsIdea = { "a", "b" }, HiddenGroups = { ["religion"] = true, ["goodsOutput"] = false }, CustomHide = "^x",
        });
        var loaded = CandidateSettingsStore.Load(folder);

        Assert.Equal(new[] { "a", "b" }, loaded.NotAsIdea);
        Assert.True(loaded.HiddenGroups["religion"]);
        Assert.False(loaded.HiddenGroups["goodsOutput"]);
        Assert.Equal("^x", loaded.CustomHide);

        _temp.Write("settings/candidates.json", "{ not json");
        var broken = CandidateSettingsStore.Load(folder);
        Assert.Empty(broken.NotAsIdea);
        Assert.Empty(broken.HiddenGroups);
        Assert.Equal(string.Empty, broken.CustomHide);
    }

    [Fact]
    public void ModifierLines_keys_skip_comments() =>
        Assert.Equal(new[] { "a", "c" }, ModifierLines.Keys("a = 1\n# b = 2\nc = \n a = 3"));
}
