using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests.IdeaGroups;

/// <summary>
/// Das mitgelieferte Paket iv2-idea-groups ohne Daten der echten Mod: erfundene Gruppen in leeren Grundgerüsten der gemeinsamen Dateien erzeugen,
/// wieder einlesen und neu planen (Spec 2026-10-06-generator-packages-idea-groups, Tests "Package").
/// </summary>
public sealed class IdeaGroupsPackageTests : IDisposable
{
    private static readonly string[] Languages = { "english", "german" };

    private readonly TempFolder _temp = new();
    private readonly GeneratorPackage _package;

    public IdeaGroupsPackageTests()
    {
        _package = GeneratorPackageLoader.Load(PackageFolder, _temp.PathOf("data"), shipped: true, uiLanguage: "en");
        foreach (var (path, text) in Skeletons())
        {
            _temp.Write("mod/" + path, text);
        }
    }

    public void Dispose() => _temp.Dispose();

    private static string PackageFolder => Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-idea-groups");

    private string Mod => _temp.PathOf("mod");

    /// <summary>Leere Behälter der Dateien, in die das Paket verschachtelt schreibt (wie in IV2, ohne Gruppen).</summary>
    private static IEnumerable<(string Path, string Text)> Skeletons()
    {
        string Switches(string effect)
        {
            var text = effect + " = {\n";
            for (var k = 1; k <= 6; k++)
            {
                text += $"\tif = {{\n\t\tlimit = {{\n\t\t\troot = {{ var:iv2_reli_ideagroup = {k} }}\n\t\t}}\n\t\tswitch = {{\n\t\t\ttrigger = religion\n\t\t}}\n\t}}\n";
            }

            text += "\telse = {\n\t\tswitch = {\n\t\t\ttrigger = religion\n\t\t}\n\t}\n";
            for (var k = 1; k <= 7; k++)
            {
                text += $"\tif = {{\n\t\tlimit = {{\n\t\t\troot = {{ var:iv2_reli_ideagroup = {k} }}\n\t\t}}\n\t\tswitch = {{\n\t\t\ttrigger = religion.group\n\t\t}}\n\t}}\n";
            }

            return text + "}\n";
        }

        // Regierungs- und Rang-Fälle: je Stufe ein if mit switch über die Regierungsform bzw. den Rang (wie in IV2, ohne else)
        string Levels(string variable, string trigger, bool root)
        {
            var text = string.Empty;
            for (var k = 1; k <= 7; k++)
            {
                var limit = root ? $"root = {{ var:{variable} = {k} }}" : $"var:{variable} = {k}";
                text += $"\tif = {{\n\t\tlimit = {{\n\t\t\t{limit}\n\t\t}}\n\t\tswitch = {{\n\t\t\ttrigger = {trigger}\n\t\t}}\n\t}}\n";
            }

            return text;
        }

        string GovRank(string effectWord, string changeWord, string variable, string trigger) =>
            $"iv2_scripted_effect_remove_{effectWord}_idea_groups_all = {{\n\tswitch = {{\n\t\ttrigger = has_variable\n\t}}\n}}\n" +
            $"iv2_scripted_effect_add_{effectWord}_idea_groups_all = {{\n}}\n" +
            $"iv2_scripted_effect_ig_change_{changeWord} = {{\n\tif = {{\n\t\tlimit = {{\n\t\t\thas_variable = {variable}\n\t\t}}\n\t}}\n" + Levels(variable, trigger, root: false) + "}\n";

        foreach (var c in new[] { "adm", "dip" })
        {
            yield return ($"in_game/common/generic_action_ai_lists/iv2_generic_action_list_{c}_ideas.txt", $"iv2_ga_list_ig_{c} = {{\n\tactions = {{\n\t}}\n}}\n");
            yield return ($"in_game/gui/IV2 Country View/IV2_cv_ideagroups_{c}.gui", $"types iv2_cv_ig_{c} {{\n}}\n");
            yield return ($"in_game/gui/Idea Variation 2 Idea Group Types/IV2_IG_types_{c}.gui", $"types iv2_ideagroups_{c}_all {{\n}}\n");
            yield return ($"in_game/gui/Idea Variation 2 Idea Statistics types/IV2_idea_statistics_{c}.gui", $"types iv2_idea_stats_{c}_all {{\n}}\n");
            // Die Spalten tragen Bereichskommentare wie in IV2; aus ihnen liest das Paket die Spalte einer Gruppe
            string Column(string area) =>
                $"###### {c} Ideas Area {area} Start\n\twidget = {{\n\t\tscrollbox = {{\n\t\t\tblockoverride \"scrollbox_content\"\n\t\t\t{{\n\t\t\t\tvbox = {{\n\t\t\t\t}}\n\t\t\t}}\n\t\t}}\n\t}}\n";
            yield return ($"in_game/gui/Idea_Variation_ideas_selection_window_{c}.gui", "window = {\n\twidget = {\n\t}\n" + Column("Left") + Column("Middle") + Column("Right") + "}\n");
        }

        yield return ("in_game/common/generic_action_ai_lists/iv2_generic_action_list_pick_ig.txt",
            "iv2_ga_list_pick_adm = {\n\tactions = {\n\t}\n}\niv2_ga_list_pick_dip = {\n\tactions = {\n\t}\n}\n");
        yield return ("in_game/common/generic_action_ai_lists/iv2_generic_action_list_no_ai.txt", "iv2_ga_list_no_ai = {\n\tactions = {\n\t}\n}\n");
        yield return ("in_game/common/scripted_effects/01_iv2_scripted_effect_idea_statistics.txt",
            "iv2_add_statistics_list_adm_effect = {\n}\niv2_remove_statistics_list_adm_effect = {\n}\n" +
            "iv2_add_statistics_list_dip_effect = {\n}\niv2_remove_statistics_list_dip_effect = {\n}\n");
        yield return ("in_game/common/scripted_effects/01_iv2_scripted_effect_log_purger.txt", "iv2_log_purger = {\n}\n");
        var revolt = Switches("iv2_scripted_effect_ig_revolt_country");
        revolt = revolt[..^2] + Levels("iv2_gov_ideagroup", "government_type", root: true) + Levels("iv2_rank_ideagroup", "country_rank", root: true) + "}\n";
        yield return ("in_game/common/scripted_effects/02_iv2_scripted_effect_ig_country_formation_revolt.txt", revolt);
        yield return ("in_game/common/scripted_effects/02_iv2_scripted_effect_ig_government_change.txt", GovRank("gov", "government", "iv2_gov_ideagroup", "government_type"));
        yield return ("in_game/common/scripted_effects/02_iv2_scripted_effect_ig_rank_change.txt", GovRank("rank", "rank", "iv2_rank_ideagroup", "country_rank"));
        yield return ("in_game/common/scripted_triggers/iv2_scripted_triggers_government_change.txt", "iv2_scripted_trigger_has_government_ig_selected = {\n\tOR = {\n\t}\n}\n");
        yield return ("in_game/common/scripted_triggers/iv2_scripted_triggers_rank_change.txt", "iv2_scripted_trigger_has_rank_ig_selected = {\n\tOR = {\n\t}\n}\n");
        yield return ("in_game/common/generic_action_ai_lists/iv2_generic_action_list_gov_type.txt", "# Government Type Action Lists\n");
        yield return ("in_game/common/generic_action_ai_lists/iv2_generic_action_list_gov_rank.txt", "# Government Rank Action Lists\n");
        yield return ("in_game/common/scripted_effects/02_iv2_scripted_effect_ig_religion_change.txt",
            "iv2_scripted_effect_remove_reli_idea_groups_all = {\n\tswitch = {\n\t\ttrigger = has_variable\n\t}\n}\n" +
            "iv2_scripted_effect_add_reli_idea_groups_all = {\n}\n" + Switches("iv2_scripted_effect_ig_change"));
        yield return ("in_game/common/scripted_triggers/iv2_scripted_triggers_religion_change.txt", "iv2_scripted_trigger_has_religion_ig_selected = {\n\tOR = {\n\t}\n}\n");
        yield return ("in_game/gui/IV2 Country View/IV2_cv_shells.gui",
            "types iv2_cv_shells {\n\ttype iv2_cv_ideagroup_shell_adm = container {\n\t}\n\ttype iv2_cv_ideagroup_shell_dip = container {\n\t}\n}\n");
        yield return ("in_game/gui/Idea Variation 2 Idea Group Types/IV2_IG_shells.gui",
            "types iv2_shells_all {\n\ttype iv2_ideagroup_shell_adm = widget {\n\t}\n\ttype iv2_ideagroup_shell_dip = widget {\n\t}\n}\n");
        yield return ("in_game/gui/Idea Variation 2 Idea Statistics types/IV2_national_statistics_setup.gui",
            "types iv2_statistics_setup {\n\ttype iv2_statistics_adm = widget {\n\t}\n\ttype iv2_statistics_dip = widget {\n\t}\n}\n");
    }

    /// <summary>Ein erfundener Stand: sieben Ideen mit Namen, deutscher Übersetzung, Beschreibung und Modifikator.</summary>
    private GeneratorState State(string category, int group, Action<GeneratorState>? adjust = null)
    {
        var state = GeneratorState.CreateEmpty(_package, Languages);
        PackageKey.Apply(_package, state, $"{category}_{group}");
        state.Head.Values["title"] = $"Test Ideas {group}";
        state.Head.Translations["title"] = new Dictionary<string, string> { ["german"] = $"Testideen {group}" };
        var ideas = state.Head.Children["ideas"];
        for (var i = 0; i < ideas.Count; i++)
        {
            ideas[i].Values["name"] = $"Idea {group}-{i + 1}";
            ideas[i].Values["desc"] = $"Description {i + 1} with \"quotes\"";
            ideas[i].Values["modifiers"] = $"global_tax_modifier = 0.{i + 1}";
        }

        ideas[2].Values["recalc"] = "yes";
        adjust?.Invoke(state);
        return state;
    }

    private void Generate(GeneratorState state) =>
        SlicePackage.Write(GeneratorPlanner.Plan(_package, state, new GeneratorTarget(Mod, null, Languages)));

    private static void Religion(GeneratorState state)
    {
        state.Head.Values["groupType"] = "reli";
        state.Head.Values["religions"] = "religion = religion:catholic\nreligion.group = religion_group:test_group";
        state.Head.Values["religionList"] = "4";
        state.Head.Values["aiList"] = "no";
        state.Head.Values["column"] = "Middle";
    }

    private static void Government(GeneratorState state)
    {
        state.Head.Values["groupType"] = "gov";
        state.Head.Values["religions"] = "government_type = government_type:monarchy\ngovernment_type = government_type:republic";
        state.Head.Values["religionList"] = "2";
    }

    private static void Rank(GeneratorState state)
    {
        state.Head.Values["groupType"] = "rank";
        state.Head.Values["religions"] = "country_rank = country_rank:rank_county\ncountry_rank = country_rank:rank_duchy";
        state.Head.Values["religionList"] = "1";
        state.Head.Values["allow"] = "scope:actor = {\n\tOR = {\n\t\tcountry_rank = country_rank:rank_county\n\t\tcountry_rank = country_rank:rank_duchy\n\t}\n}";
    }

    private string ModFile(string path) => File.ReadAllText(Path.Combine(Mod, path)).Replace("\r\n", "\n");

    [Fact]
    public void Government_group_writes_list_trigger_effects_and_cases()
    {
        Generate(State("adm", 3, Government));

        var list = ModFile("in_game/common/generic_action_ai_lists/iv2_generic_action_list_gov_type.txt");
        Assert.Contains("iv2_ga_list_govtype_2 = {\n\tpotential = {\n\t\tOR = {\n\t\t\tgovernment_type = government_type:monarchy\n\t\t\tgovernment_type = government_type:republic\n\t\t}\n\t}", list);
        Assert.Contains("\t\tiv2_ga_select_ideagroup_adm_3\n\t\tiv2_ga_ideagroup_adm_3_1\n", list);
        Assert.Contains("\t\thas_variable = iv2_ideagroup_select_adm_3_1\n", ModFile("in_game/common/scripted_triggers/iv2_scripted_triggers_government_change.txt"));
        var change = ModFile("in_game/common/scripted_effects/02_iv2_scripted_effect_ig_government_change.txt");
        Assert.Contains("\t\tiv2_ideagroup_select_adm_3_1 = {\n\t\t\tfire_generic_action = {\n\t\t\t\ttype = iv2_ga_deselect_ideagroup_adm_3\n", change);
        Assert.Contains("\t\t\t\tiv2_scripted_trigger_has_government_ig_selected = yes\n\t\t\t\tOR = {\n\t\t\t\t\tgovernment_type = government_type:monarchy\n", change);
        var level3 = change[change.IndexOf("var:iv2_gov_ideagroup = 3", StringComparison.Ordinal)..change.IndexOf("var:iv2_gov_ideagroup = 4", StringComparison.Ordinal)];
        Assert.Contains("government_type:monarchy = {\n\t\t\t\tadd_country_modifier = { modifier = ideagroup_modifier_adm_3_1 years = -1 }", level3);
        Assert.Contains("modifier = ideagroup_modifier_adm_3_3 years = -1", level3);
        Assert.DoesNotContain("ideagroup_modifier_adm_3_4", level3);
        Assert.Contains("government_type:republic = {", level3);
        var revolt = ModFile("in_game/common/scripted_effects/02_iv2_scripted_effect_ig_country_formation_revolt.txt");
        var revoltLevel7 = revolt[revolt.IndexOf("var:iv2_gov_ideagroup = 7", StringComparison.Ordinal)..revolt.IndexOf("var:iv2_rank_ideagroup = 1", StringComparison.Ordinal)];
        Assert.Contains("modifier = ideagroup_modifier_adm_3_7 years = -1", revoltLevel7);
    }

    [Fact]
    public void Rank_group_writes_cases_per_rank()
    {
        Generate(State("dip", 2, Rank));

        Assert.Contains("iv2_ga_list_govrank_1 = {", ModFile("in_game/common/generic_action_ai_lists/iv2_generic_action_list_gov_rank.txt"));
        var change = ModFile("in_game/common/scripted_effects/02_iv2_scripted_effect_ig_rank_change.txt");
        var level1 = change[change.IndexOf("var:iv2_rank_ideagroup = 1", StringComparison.Ordinal)..change.IndexOf("var:iv2_rank_ideagroup = 2", StringComparison.Ordinal)];
        Assert.Contains("country_rank:rank_county = {\n\t\t\t\tadd_country_modifier = { modifier = ideagroup_modifier_dip_2_1 years = -1 }", level1);
        Assert.Contains("country_rank:rank_duchy = {", level1);
        Assert.Contains("iv2_scripted_trigger_has_rank_ig_selected = yes", change);
        var sgui = ModFile("in_game/common/scripted_guis/IV2_scripted_gui_idea_groups_dip.txt");
        var select = sgui[sgui.IndexOf("iv2_ideagroup_select_dip_2_1 = {", StringComparison.Ordinal)..];
        Assert.Contains("\t\tNOT = {\n\t\t\thas_variable = iv2_ideagroup_select_dip_2_1\n\t\t}\n\t\tOR = {\n\t\t\tcountry_rank = country_rank:rank_county\n", select);
    }

    [Fact]
    public void Government_group_without_values_writes_no_cases()
    {
        Generate(State("adm", 3, s => { Government(s); s.Head.Values["religions"] = string.Empty; }));

        Assert.DoesNotContain("adm_3", ModFile("in_game/common/generic_action_ai_lists/iv2_generic_action_list_gov_type.txt"));
        var change = ModFile("in_game/common/scripted_effects/02_iv2_scripted_effect_ig_government_change.txt");
        Assert.DoesNotContain("ideagroup_modifier_adm_3", change);
        Assert.DoesNotContain("type = iv2_ga_select_ideagroup_adm_3", change);
    }

    private static Action<GeneratorState> Exclusive(string partners) => state =>
    {
        state.Head.Values["exclusive"] = partners;
        state.Head.Values["exclusiveTitle"] = "Mutually Exclusive with Partner Ideas";
        state.Head.Translations["exclusiveTitle"] = new Dictionary<string, string> { ["german"] = "Schließt Partnerideen aus" };
        state.Head.Values["exclusiveDesc"] = "These ideas cannot be taken together.";
    };

    private string Sgui(string category) => ModFile($"in_game/common/scripted_guis/IV2_scripted_gui_idea_groups_{category}.txt");

    private static string SelectBlock(string sgui, string group)
    {
        var start = sgui.IndexOf($"iv2_ideagroup_select_{group}_1 = {{", StringComparison.Ordinal);
        return sgui[start..sgui.IndexOf("\n}", start, StringComparison.Ordinal)];
    }

    [Fact]
    public void Exclusive_numbers_ignore_own_and_duplicates()
    {
        Generate(State("dip", 5, Exclusive("6, 6 5")));

        var select = SelectBlock(Sgui("dip"), "dip_5");
        Assert.Contains("\t\tNOT = {\n\t\t\tOR = {\n\t\t\t\thas_variable = iv2_ideagroup_select_dip_5_1\n\t\t\t\thas_variable = iv2_ideagroup_select_dip_6_1\n\t\t\t}\n\t\t}", select);
    }

    [Fact]
    public void Exclusive_group_writes_icon_tooltip_and_loc()
    {
        Generate(State("dip", 5, Exclusive("6")));

        var selection = ModFile("in_game/gui/Idea_Variation_ideas_selection_window_dip.gui");
        Assert.Contains("size = { 28 28 }", selection);
        Assert.Contains("texture = \"gfx/interface/dip_exclusive.dds\"", selection);
        Assert.Contains("using = iv_2_tooltip_template_mut_idea_dip_5_1", selection);
        var tooltips = ModFile("in_game/gui/IV2_mutually_exclusive_ideagroup_tooltips.gui");
        Assert.Contains("template iv_2_tooltip_template_mut_idea_dip_5_1 {", tooltips);
        Assert.Contains("text = \"iv2_ideagroup_title_text_mut_dip_5_1\"", tooltips);
        Assert.Contains("text = \"iv2_ideagroup_concept_link_mut_dip\"", tooltips);
        var english = ModFile("main_menu/localization/english/01_IV2_setup_l_english.yml");
        Assert.Contains(" iv2_ideagroup_title_text_mut_dip_5_1: \"Mutually Exclusive with Partner Ideas\"", english);
        Assert.Contains(" iv2_ideagroup_desc_text_mut_dip_5_1: \"These ideas cannot be taken together.\"", english);
        Assert.Contains(" iv2_ideagroup_title_text_mut_dip_5_1: \"Schließt Partnerideen aus\"", ModFile("main_menu/localization/german/01_IV2_setup_l_german.yml"));
    }

    [Fact]
    public void Missing_back_reference_warns()
    {
        Generate(State("dip", 6));

        var plan = GeneratorPlanner.Plan(_package, State("dip", 5, Exclusive("6")), new GeneratorTarget(Mod, null, Languages));
        Assert.Contains(plan.Warnings, w => w.Contains("dip_6") && w.Contains("dip_5"));

        SlicePackage.Write(plan);
        Generate(State("dip", 6, Exclusive("5")));
        Assert.DoesNotContain(GeneratorPlanner.Plan(_package, State("dip", 5, Exclusive("6")), new GeneratorTarget(Mod, null, Languages)).Warnings,
            w => w.Contains("dip_6"));
    }

    [Fact]
    public void Group_no_longer_exclusive_loses_template_and_keys()
    {
        Generate(State("dip", 5, Exclusive("6")));

        Generate(State("dip", 5));

        Assert.DoesNotContain("mut_idea_dip_5_1", ModFile("in_game/gui/IV2_mutually_exclusive_ideagroup_tooltips.gui"));
        Assert.DoesNotContain("mut_dip_5_1", ModFile("main_menu/localization/english/01_IV2_setup_l_english.yml"));
        Assert.DoesNotContain("dip_exclusive.dds", ModFile("in_game/gui/Idea_Variation_ideas_selection_window_dip.gui"));
        Assert.Contains("\t\tNOT = {\n\t\t\thas_variable = iv2_ideagroup_select_dip_5_1\n\t\t}", SelectBlock(Sgui("dip"), "dip_5"));
    }

    [Fact]
    public void Exclusive_values_read_back()
    {
        Generate(State("dip", 5, Exclusive("6")));

        var read = GeneratorStateReader.Read(_package, Mod, "dip_5", Languages)!.State;

        Assert.Equal("6", read.Head.Values["exclusive"]);
        Assert.Equal("Mutually Exclusive with Partner Ideas", read.Head.Values["exclusiveTitle"]);
        Assert.Equal("Schließt Partnerideen aus", read.Head.Translations["exclusiveTitle"]["german"]);
        Assert.Equal("These ideas cannot be taken together.", read.Head.Values["exclusiveDesc"]);
        var plan = GeneratorPlanner.Plan(_package, read, new GeneratorTarget(Mod, null, Languages));
        Assert.All(plan.Files, f => Assert.True(f.Status == PlannedFileStatus.Unchanged, $"{f.RelativePath} {FirstDifference(f.OriginalText, f.NewText)}"));
    }

    [Fact]
    public void Package_check_is_ok()
    {
        var issues = PackageChecker.Check(PackageDocument.Load(PackageFolder), new PackageFiles(PackageFolder), "en");

        Assert.Empty(issues);
    }

    [Fact]
    public void Generated_groups_read_back_and_replan_unchanged()
    {
        Generate(State("adm", 1));
        Generate(State("adm", 2, Religion));
        Generate(State("adm", 3, Government));
        Generate(State("dip", 1));
        Generate(State("dip", 2, Rank));

        var keys = new[] { "adm_1", "adm_2", "adm_3", "dip_1", "dip_2" };
        Assert.Equal(keys, GeneratorStateReader.Keys(_package, Mod));
        var government = GeneratorStateReader.Read(_package, Mod, "adm_3", Languages)!.State;
        Assert.Equal("government_type = government_type:monarchy\ngovernment_type = government_type:republic", government.Head.Values["religions"]);
        Assert.Equal("2", government.Head.Values["religionList"]);
        var rank = GeneratorStateReader.Read(_package, Mod, "dip_2", Languages)!.State;
        Assert.Equal("country_rank = country_rank:rank_county\ncountry_rank = country_rank:rank_duchy", rank.Head.Values["religions"]);
        Assert.Equal("1", rank.Head.Values["religionList"]);
        foreach (var key in keys)
        {
            var read = GeneratorStateReader.Read(_package, Mod, key, Languages)!.State;
            var plan = GeneratorPlanner.Plan(_package, read, new GeneratorTarget(Mod, null, Languages));
            Assert.All(plan.Files, f => Assert.True(f.Status == PlannedFileStatus.Unchanged, $"{key}: {f.RelativePath} {f.Status} {FirstDifference(f.OriginalText, f.NewText)}"));
        }
    }

    private static string FirstDifference(string? before, string? after)
    {
        var a = (before ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        var b = (after ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var left = i < a.Length ? a[i] : "<end>";
            var right = i < b.Length ? b[i] : "<end>";
            if (left != right)
            {
                return $"line {i + 1}: [{left}] -> [{right}]";
            }
        }

        return string.Empty;
    }

    [Fact]
    public void Read_back_keeps_the_form_values()
    {
        Generate(State("adm", 2, Religion));

        var read = GeneratorStateReader.Read(_package, Mod, "adm_2", Languages)!.State;

        Assert.Equal("Test Ideas 2", read.Head.Values["title"]);
        Assert.Equal("Testideen 2", read.Head.Translations["title"]["german"]);
        Assert.Equal("reli", read.Head.Values["groupType"]);
        Assert.Equal("religion = religion:catholic\nreligion.group = religion_group:test_group", read.Head.Values["religions"]);
        Assert.Equal("4", read.Head.Values["religionList"]);
        Assert.Equal("no", read.Head.Values["aiList"]);
        Assert.Equal("Middle", read.Head.Values["column"]);
        var ideas = read.Head.Children["ideas"];
        Assert.Equal("Idea 2-3", ideas[2].Values["name"]);
        Assert.Equal("Description 3 with \"quotes\"", ideas[2].Values["desc"]);
        Assert.Equal("global_tax_modifier = 0.3", ideas[2].Values["modifiers"]);
        Assert.Equal("yes", ideas[2].Values["recalc"]);
        Assert.Equal("no", ideas[1].Values["recalc"]);
    }

    [Fact]
    public void Select_action_has_no_ai_tick()
    {
        // IV2 AI chooser (Idea Variation 2 Skripte/KI-Auswaehler/iv2_build_ai_chooser.py): the AI picks groups only through the chooser
        Generate(State("adm", 1));

        var ga = ModFile("in_game/common/generic_actions/iv2_ga_idea_groups_adm.txt");
        var select = ga[ga.IndexOf("iv2_ga_select_ideagroup_adm_1 = {", StringComparison.Ordinal)..ga.IndexOf("iv2_ga_deselect_ideagroup_adm_1 = {", StringComparison.Ordinal)];
        Assert.Contains("\tai_tick = never\n\t#ai_tick_frequency = 36\n", select);
        Assert.DoesNotContain("ai_tick = monthly", select);
        var idea = ga[ga.IndexOf("iv2_ga_ideagroup_adm_1_1 = {", StringComparison.Ordinal)..];
        Assert.Contains("\tai_tick = monthly\n\tai_tick_frequency = 36\n", idea);
    }

    [Fact]
    public void Group_missing_in_ai_chooser_warns()
    {
        const string chooser = "in_game/common/generic_actions/iv2_ga_ai_chooser.txt";
        GeneratorPlan WarningsFor(string? chooserText)
        {
            if (chooserText is not null)
            {
                _temp.Write("mod/" + chooser, chooserText);
            }

            return GeneratorPlanner.Plan(_package, State("adm", 1), new GeneratorTarget(Mod, null, Languages));
        }

        Assert.DoesNotContain(WarningsFor(null).Warnings, w => w.Contains("iv2_build_ai_chooser"));
        Assert.Contains(WarningsFor("\t\t\t\t100 = {  # group:adm_10\r\n").Warnings,
            w => w.Contains("adm_1") && w.Contains("iv2_build_ai_chooser"));
        Assert.DoesNotContain(WarningsFor("\t\t\t\t100 = {  # group:adm_1\r\n").Warnings, w => w.Contains("iv2_build_ai_chooser"));
    }

    [Fact]
    public void New_group_is_added_after_existing()
    {
        Generate(State("adm", 1));
        Generate(State("adm", 2));

        var modifiers = File.ReadAllText(Path.Combine(Mod, "main_menu", "common", "static_modifiers", "01_IV2_ideagroup_modifiers_adm.txt"));
        Assert.True(modifiers.IndexOf("ideagroup_modifier_adm_1_7", StringComparison.Ordinal) < modifiers.IndexOf("ideagroup_modifier_adm_2_1", StringComparison.Ordinal));
        var pick = File.ReadAllText(Path.Combine(Mod, "in_game", "common", "generic_action_ai_lists", "iv2_generic_action_list_pick_ig.txt"));
        Assert.Contains("\t\tiv2_ga_select_ideagroup_adm_1\n\t\tiv2_ga_select_ideagroup_adm_2\n", pick.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Religion_group_writes_a_case_per_condition()
    {
        Generate(State("adm", 2, Religion));

        var revolt = File.ReadAllText(Path.Combine(Mod, "in_game", "common", "scripted_effects", "02_iv2_scripted_effect_ig_country_formation_revolt.txt"));
        Assert.Contains("religion:catholic = {", revolt);
        Assert.Contains("religion_group:test_group = {", revolt);
        Assert.Contains("modifier = ideagroup_modifier_adm_2_7 years = -1", revolt);
        var list = File.ReadAllText(Path.Combine(Mod, "in_game", "common", "generic_action_ai_lists", "iv2_generic_action_list_religion.txt"));
        Assert.Contains("iv2_ga_list_reli_4 = {", list);
        Assert.Contains("OR = {", list);
    }
}
