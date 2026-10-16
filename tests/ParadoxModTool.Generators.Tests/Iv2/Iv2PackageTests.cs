using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests.Iv2;

/// <summary>
/// Abnahme Teil 2: Das IV2-Paket erzeugt für das Kunstset TST dieselben Schlüssel, Blöcke und Werte wie der Python-Generator (Erwartung aus
/// <c>make_expected.py</c>); verglichen wird die Token-Folge, bei .yml Schlüssel → Wert mit der erlaubten Maskierungs-Abweichung.
/// </summary>
public sealed class Iv2PackageTests : IDisposable
{
    private const string Coa = "main_menu/gfx/interface/coa/TST.dds";

    private readonly Iv2Fixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    public static TheoryData<string> ExistingFiles => Files("existing");

    public static TheoryData<string> FreshFiles => Files("fresh");

    private static TheoryData<string> Files(string scenario)
    {
        var data = new TheoryData<string>();
        foreach (var file in Bundle.Read(Path.Combine(Iv2Fixture.RepoRoot, "tests", "ParadoxModTool.Generators.Tests", "Iv2", $"Expected-{scenario}.bundle")).Keys)
        {
            data.Add(file);
        }

        return data;
    }

    [Fact]
    public void Iv2_assets_come_from_the_game()
    {
        Assert.Equal(new AssetDef("assets/coa.zip", AssetOrigin.Game), _fixture.Package.AssetDefs["coa"]);
        Assert.Equal(new AssetDef("assets/idea_database.json", AssetOrigin.Game), _fixture.Package.AssetDefs["ideaDatabase"]);
    }

    [Fact]
    public void Iv2_package_has_four_lookup_sources()
    {
        Assert.Equal(new[] { "advances", "reforms", "laws", "nations" }, _fixture.Package.Lookups!.Select(l => l.Id));
        var name = _fixture.Package.Root!.Child!.Fields.Single(f => f.Id == "name");
        Assert.Equal(new[] { "nations", "advances", "reforms", "laws" }, name.Lookup!.Sources);
        Assert.Equal("desc", name.Lookup.Fills["desc"]);
        Assert.Equal("assets/idea_database.json", _fixture.Package.Assets["ideaDatabase"]);
    }

    [Fact]
    public void Package_loads_from_repository()
    {
        var package = _fixture.Package;

        Assert.Equal("iv2", package.Id);
        Assert.Equal("IV2 National Ideas", package.Name);
        Assert.Equal(4, package.Constants["categories"].Count);
        Assert.Equal("Administrative", package.Constants["categories"][0]["label"]);
        Assert.Equal(19, package.Outputs.Count);
        Assert.Equal(3, package.Root!.Child!.Initial);
        Assert.Equal(4, package.Root.Child.Child!.Initial);
    }

    [Theory]
    [MemberData(nameof(ExistingFiles))]
    public void Existing_scenario_matches_python(string file) =>
        Compare(file, _fixture.Expected("existing")[file], _fixture.Plan(_fixture.State(), _fixture.ExistingMod));

    [Theory]
    [MemberData(nameof(FreshFiles))]
    public void Fresh_scenario_matches_python(string file) =>
        Compare(file, _fixture.Expected("fresh")[file], _fixture.Plan(_fixture.State(), _fixture.FreshMod));

    [Fact]
    public void Planned_files_equal_expected_files_plus_coa()
    {
        foreach (var (scenario, mod) in new[] { ("existing", _fixture.ExistingMod), ("fresh", _fixture.FreshMod) })
        {
            var plan = _fixture.Plan(_fixture.State(), mod);
            var expected = _fixture.Expected(scenario).Keys.Append(Coa).OrderBy(p => p, StringComparer.Ordinal);
            Assert.Equal(expected, plan.Files.Select(f => f.RelativePath).OrderBy(p => p, StringComparer.Ordinal));
            Assert.Equal(Iv2Fixture.CoaBytes, plan.Files.Single(f => f.RelativePath == Coa).Bytes);
        }
    }

    [Fact]
    public void Base_files_keep_other_tags()
    {
        var plan = _fixture.Plan(_fixture.State(), _fixture.ExistingMod);
        var bases = Bundle.Read(Path.Combine(_fixture.Fixtures, "Base.bundle"));
        foreach (var (relative, original) in bases.Where(b => !b.Key.Contains("TST", StringComparison.Ordinal)))
        {
            var planned = plan.Files.Single(f => f.RelativePath == relative).NewText!.Replace("\r\n", "\n");
            foreach (var line in original.Split('\n').Where(l => l.Contains("_TS_", StringComparison.Ordinal) || l.Contains("= TS", StringComparison.Ordinal)
                         || l.Contains("ARA", StringComparison.Ordinal) || l.Contains("FRA", StringComparison.Ordinal) || l.Contains("TST_99", StringComparison.Ordinal)))
            {
                Assert.Contains(line.TrimEnd(), planned.Split('\n').Select(l => l.TrimEnd()));
            }
        }
    }

    [Fact]
    public void Second_run_is_stable()
    {
        SlicePackage.Write(_fixture.Plan(_fixture.State(), _fixture.ExistingMod));

        var again = _fixture.Plan(_fixture.State(), _fixture.ExistingMod);

        Assert.All(again.Files, f => Assert.Equal(PlannedFileStatus.Unchanged, f.Status));
    }

    [Fact]
    public void Empty_category_removes_old_section()
    {
        SlicePackage.Write(_fixture.Plan(_fixture.State(), _fixture.ExistingMod));
        var state = _fixture.State();
        state.Head.Children["categories"][3].Children["ideas"].Clear();

        var plan = _fixture.Plan(state, _fixture.ExistingMod);

        var shells = plan.Files.Single(f => f.RelativePath.EndsWith("IV2_historic_national_ideas_setup.gui", StringComparison.Ordinal)).NewText!;
        Assert.DoesNotContain("iv2_historic_idea_select_TST_31", shells);
        Assert.Contains("iv2_historic_idea_select_TS_31", shells);
    }

    [Fact]
    public void Errors_and_warnings()
    {
        var renderer = new GeneratorRenderer(_fixture.Package);

        var empty = _fixture.State();
        foreach (var category in empty.Head.Children["categories"])
        {
            category.Children["ideas"].Clear();
        }

        Assert.Contains(FormValidator.Check(_fixture.Package, empty, renderer), i => i.Severity == IssueSeverity.Error && i.Message == "No idea has been added.");

        var few = _fixture.State();
        Assert.Contains(FormValidator.Check(_fixture.Package, few, renderer), i => i.Severity == IssueSeverity.Warning && i.Message == "Fewer than 10 ideas.");

        var noEnglish = _fixture.State();
        noEnglish.Languages = new List<string> { "german" };
        Assert.Contains(FormValidator.Check(_fixture.Package, noEnglish, renderer), i => i.Message == "English is not selected as a localization language.");

        var incomplete = _fixture.State();
        incomplete.LanguageRules = incomplete.LanguageRules with { ExtendEnglish = true };
        Assert.Contains(FormValidator.Check(_fixture.Package, incomplete, renderer), i => i.Message.StartsWith("Ideas without name", StringComparison.Ordinal));
    }

    [Fact]
    public void New_state_has_three_ideas_with_four_levels()
    {
        var state = GeneratorState.CreateEmpty(_fixture.Package, new[] { "english" });

        Assert.All(state.Head.Children["categories"], c =>
        {
            Assert.Equal(3, c.Children["ideas"].Count);
            Assert.All(c.Children["ideas"], i => Assert.Equal(4, i.Children["levels"].Count));
        });
    }

    // ---- IV2 AI chooser (mod spec docs/superpowers/specs/2026-10-10-iv2-ai-idea-chooser-design.md, 6.5) ----

    private const string ChooserFile = "in_game/common/generic_actions/iv2_ga_ai_choose_historic_TST.txt";

    private string Planned(string file) =>
        _fixture.Plan(_fixture.State(), _fixture.FreshMod).Files.Single(f => f.RelativePath == file).NewText!.Replace("\r\n", "\n");

    private const string AlertRecalc = "hidden_effect = { if = { limit = { is_ai = no } iv2_recalc_alerts = yes } }";

    private static string Flat(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

    private static string Block(string text, string name)
    {
        var start = text.IndexOf(name + " = {", StringComparison.Ordinal);
        Assert.True(start >= 0, name);
        var depth = 0;
        for (var i = text.IndexOf('{', start); i < text.Length; i++)
        {
            depth += text[i] == '{' ? 1 : text[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return text[start..(i + 1)];
            }
        }

        throw new InvalidOperationException(name);
    }

    private static string Inner(string block, string name) => Block(block, name)[(name.Length + 4)..^1];

    [Fact]
    public void Base_select_has_no_ai_and_levels_keep_it()
    {
        var ga = Planned("in_game/common/generic_actions/iv2_GA_ideas_TST.txt");
        var base1 = Block(ga, "iv2_ga_select_historic_TST_1");
        Assert.Contains("\tai_tick = never\n\t#ai_tick_frequency = 36\n", base1);
        Assert.Contains("\tai_tick = monthly\n\tai_tick_frequency = 36\n", Block(ga, "iv2_ga_select_historic_TST_3_1"));
    }

    [Fact]
    public void Historic_chooser_weights_follow_position()
    {
        // TST: adm 3 ideas, dip 1, army 0, navy 2; weight = floor(100 * 0.6^position * sqrt(mean size / size) + 0.5)
        var chooser = Planned(ChooserFile);
        foreach (var (weight, idea) in new[] { (82, "TST_1"), (49, "TST_2"), (29, "TST_3"), (141, "TST_11"), (100, "TST_31"), (60, "TST_32") })
        {
            Assert.Contains($"\t{weight} = {{  # {idea}\n", chooser);
        }
    }

    [Fact]
    public void Historic_chooser_handles_empty_category()
    {
        var chooser = Planned(ChooserFile);
        Assert.DoesNotContain("TST_21", chooser);
        Assert.DoesNotContain("iv2_var_progress_points_army", chooser);
    }

    [Fact]
    public void Historic_chooser_frame()
    {
        var flat = Flat(Planned(ChooserFile));
        Assert.StartsWith("# GENERATED by PMT package iv2 - historic AI chooser", flat);
        foreach (var part in new[]
                 {
                     "iv2_ga_ai_choose_historic_TST = { type = owncountry", "ai_tick = monthly ai_tick_frequency = 6", "automation_tick = never",
                     "potential = { scope:actor = { is_ai = yes has_or_had_tag = TST } }",
                     "iv2_scripted_trigger_use_historic_ideas = yes",
                     "var:iv2_var_current_national_idea_limit > var:iv2_var_national_idea_current",
                     "ai_will_do = { add = { value = 1000 } }",
                 })
        {
            Assert.Contains(part, flat);
        }
    }

    [Fact]
    public void Historic_chooser_copies_trigger_and_effect()
    {
        var ga = Planned("in_game/common/generic_actions/iv2_GA_ideas_TST.txt");
        var flat = Flat(Planned(ChooserFile));
        foreach (var number in new[] { 1, 2, 3, 11, 31, 32 })
        {
            var action = Block(ga, $"iv2_ga_select_historic_TST_{number}");
            Assert.Contains("trigger = { " + Flat(Inner(action, "allow")) + " }", flat);
            // the alert recalc at the end of the player action is not copied: the chooser runs only for the AI
            Assert.Contains(Flat(Inner(action, "effect")).Replace(" " + AlertRecalc, string.Empty), flat);
        }
    }

    [Fact]
    public void Historic_actions_use_the_trigger_of_their_own_idea()
    {
        // every idea and level has its own iv2_sct_nat_idea_<tag>_<n>[_<level>]; never the trigger of idea 1 or a dynamic one
        var ga = Planned("in_game/common/generic_actions/iv2_GA_ideas_TST.txt");
        Assert.DoesNotContain("iv2_sct_dnat_idea_", ga);
        Assert.DoesNotContain("iv2_sct_dnat_idea_", Planned(ChooserFile));
        foreach (var number in new[] { 1, 2, 3, 11, 31, 32 })
        {
            Assert.Contains($"iv2_sct_nat_idea_TST_{number} = yes", Flat(Inner(Block(ga, $"iv2_ga_select_historic_TST_{number}"), "allow")));
        }

        Assert.Contains("iv2_sct_nat_idea_TST_11_2 = yes", Flat(Inner(Block(ga, "iv2_ga_select_historic_TST_11_2"), "allow")));
    }

    [Fact]
    public void Historic_allow_requires_a_valid_entry()
    {
        // a random_list without a valid option is an engine error ("random_list with no options"): allow = OR over the entry triggers
        var ga = Planned("in_game/common/generic_actions/iv2_GA_ideas_TST.txt");
        var allow = Flat(Inner(Planned(ChooserFile), "allow"));
        Assert.DoesNotContain("has_game_rule = iv2_rule_national_ideas_3", allow);
        foreach (var number in new[] { 1, 2, 3, 11, 31, 32 })
        {
            Assert.Contains("AND = { " + Flat(Inner(Block(ga, $"iv2_ga_select_historic_TST_{number}"), "allow")) + " }", allow);
        }
    }

    [Fact]
    public void Historic_chooser_has_localization()
    {
        // only log cosmetics: the AI-only action is never shown, but the game reports missing keys
        foreach (var lang in new[] { "english", "german" })
        {
            var yml = Planned($"main_menu/localization/{lang}/04_IV2_TST_l_{lang}.yml");
            Assert.Contains(" iv2_ga_ai_choose_historic_TST: \"", yml);
            Assert.Contains(" iv2_ga_ai_choose_historic_TST_desc: \"", yml);
        }
    }

    [Fact]
    public void Historic_chooser_has_own_ai_list()
    {
        var lists = Flat(Planned("in_game/common/generic_action_ai_lists/03_iv2_gal_nat_idea_TST.txt"));
        Assert.Contains("iv2_ga_list_ai_choose_historic_TST = { potential = { is_ai = yes iv2_scripted_trigger_use_historic_ideas = yes "
                        + "has_or_had_tag = TST } actions = { iv2_ga_ai_choose_historic_TST } }", lists);
    }

    [Fact]
    public void Gui_action_parameter_uses_make_scope()
    {
        // EU5 1.4 beta: "[Country]" gives the runtime error "couldn't convert parameter value"; vanilla and the IV2 beta port use MakeScope
        var plan = _fixture.Plan(_fixture.State(), _fixture.FreshMod);
        var gui = plan.Files.Where(f => f.RelativePath.EndsWith(".gui", StringComparison.Ordinal) && f.NewText is not null).ToList();
        Assert.Contains(gui, f => f.NewText!.Contains("parameter_value = \"[Country.MakeScope]\"", StringComparison.Ordinal));
        Assert.DoesNotContain(gui, f => f.NewText!.Contains("parameter_value = \"[Country]\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Historic_constants_match_mod_config()
    {
        // PMT_IV2_CHOOSER_CONFIG = path of iv2_ai_chooser_config.json (Idea Variation 2 Skripte/KI-Auswaehler)
        var path = Environment.GetEnvironmentVariable("PMT_IV2_CHOOSER_CONFIG");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        using var config = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var functions = File.ReadAllText(Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2", "functions.sbn"));
        Assert.Contains($"historic_order_decay = {config.RootElement.GetProperty("historic_order_decay").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture)}", functions);
        Assert.Equal(0.5, config.RootElement.GetProperty("category_balance").GetDouble());  // functions.sbn computes a square root
    }

    internal static void Compare(string file, string expected, GeneratorPlan plan)
    {
        var planned = plan.Files.SingleOrDefault(f => f.RelativePath == file);
        Assert.True(planned is not null, $"PMT plant {file} nicht.");
        var actual = planned!.NewText ?? string.Empty;
        if (file.EndsWith(".yml", StringComparison.Ordinal))
        {
            var want = YmlMap.Of(expected, pythonStyle: true);
            var have = YmlMap.Of(actual, pythonStyle: false);
            var missing = want.Where(w => !have.TryGetValue(w.Key, out var v) || v != w.Value).Select(w => $"{w.Key}: \"{w.Value}\" / PMT \"{have.GetValueOrDefault(w.Key)}\"").ToList();
            var extra = have.Keys.Except(want.Keys).ToList();
            Assert.True(missing.Count == 0 && extra.Count == 0, $"{file}: abweichend {string.Join("; ", missing.Take(5))}; zusätzlich {string.Join(", ", extra.Take(5))}");
            return;
        }

        var wantTokens = ScriptTokens.Of(expected);
        var haveTokens = ScriptTokens.Of(actual);
        var index = Enumerable.Range(0, Math.Min(wantTokens.Count, haveTokens.Count)).FirstOrDefault(i => wantTokens[i] != haveTokens[i], -1);
        if (index < 0 && wantTokens.Count == haveTokens.Count)
        {
            return;
        }

        index = index < 0 ? Math.Min(wantTokens.Count, haveTokens.Count) : index;
        string Around(IReadOnlyList<string> tokens) => string.Join(" ", tokens.Skip(Math.Max(0, index - 8)).Take(16));
        Assert.Fail($"{file}: Token {index} weicht ab.\nPython: {Around(wantTokens)}\nPMT:    {Around(haveTokens)}");
    }
}
