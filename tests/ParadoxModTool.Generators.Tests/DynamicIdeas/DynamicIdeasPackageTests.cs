using ParadoxModTool.Generators.Candidates;
using System.Text.RegularExpressions;
using ParadoxModTool.Generators.Editing.Package;
using ParadoxModTool.Generators.Tests.Iv2;
using ParadoxModTool.Patches;
using Xunit;

namespace ParadoxModTool.Generators.Tests.DynamicIdeas;

/// <summary>
/// Das mitgelieferte Paket iv2-dynamic-ideas ohne Daten der echten Mod: erfundene dynamische Nationalideen in leeren Grundgerüsten der
/// Kategorie-Dateien erzeugen, wieder einlesen und neu planen (Spec 2026-10-10-generator-packages-iv2-dynamic-ideas, Cross-checks "Repo test").
/// </summary>
public sealed class DynamicIdeasPackageTests : IDisposable
{
    private static readonly string[] Languages = { "english", "german" };

    private static readonly string[] Categories = { "adm", "dip", "army", "navy" };

    private readonly TempFolder _temp = new();
    private readonly GeneratorPackage _package;

    public DynamicIdeasPackageTests()
    {
        _package = GeneratorPackageLoader.Load(PackageFolder, _temp.PathOf("data"), shipped: true, uiLanguage: "en");
        foreach (var (path, text) in Skeletons())
        {
            _temp.Write("mod/" + path, text);
        }
    }

    public void Dispose() => _temp.Dispose();

    private static string PackageFolder => Path.Combine(Iv2Fixture.RepoRoot, "generator-packages", "iv2-dynamic-ideas");

    private string Mod => _temp.PathOf("mod");

    /// <summary>
    /// Grundgerüste: je Kategorie eine leere Datei für die Orte 1, 2, 6, 7 und die KI-Listen (Ort 3) mit ihren beiden Blöcken; dazu die gemeinsamen
    /// Dateien mit den Blöcken der Orte 4, 5, 8, 19, 20 wie in der Mod. Die Säuberung (Ort 19) hat army_1 und army_2 vorab, no_ai (Ort 20) eine
    /// Abwahl von army_1, die kein Treffer sein darf.
    /// </summary>
    internal static IEnumerable<(string Path, string Text)> Skeletons()
    {
        foreach (var c in Categories)
        {
            yield return (StaticModifiers(c), string.Empty);
            yield return (GenericActions(c), string.Empty);
            yield return (ScriptedGuis(c), string.Empty);
            yield return (Triggers(c), string.Empty);
            yield return (AiList(c),
                $"iv2_ga_list_dynamic_nat_ideas_progress_cost_{c} = {{\n\tactions = {{\n\t\t\t# All {c} Selects\n\t}}\n}}\n\n" +
                $"iv2_ga_list_dynamic_nat_ideas_innovation_cost_{c} = {{\n\tactions = {{\n\t\t\t# All {c} Level Selects\n\t}}\n}}\n");
            yield return (Overview(c), $"types iv2_nat_ideas_{c}_all {{\n}}\n");
            // Die adm-Datei der Mod endet mit "} # types close bracket"
            yield return (SeeCards(c), $"types iv2_nat_ideas_see_{c} {{\n}}" + (c == "adm" ? " # types close bracket" : string.Empty) + "\n");
            yield return (SelectTooltips(c), string.Empty);
            yield return (LevelTooltips(c), string.Empty);
            yield return (RemoveTooltips(c), string.Empty);
            yield return (CountryView(c), $"types iv2_cv_nat_dyn_{c} {{\n}}\n");
        }

        // Länderansicht (Ort 15): die Hülle der Nationalideen mit einem historischen Eintrag, wie iv2_build_country_view.py sie schreibt
        yield return (CountryViewShells, "types iv2_cv_shells {\n\ttype iv2_cv_ideagroup_shell_adm = widget {\n\t\tsize = { 470 36 }\n\t}\n\n" +
                                         "\ttype iv2_cv_national_idea_shell = container {\n\t\tiv2_cv_nat_hist_CAS_1 = {}\n\t}\n}\n");

        yield return (SetupEffect, "######################################################\n# Dynamic National ideas\n######################################################\n\n" +
                                   "iv2_dynamic_national_ideas_list_setup_effect = {\n}\n");
        yield return (DebugCopy, "iv2_get_test = {\n\tscope = country\n\n\teffect = {\n\t}\n}\n");
        var shells = new[] { "adm_1", "adm_2", "adm_3", "adm_4", "adm_5", "adm_6", "dip_1", "dip_2", "dip_3", "dip_4", "dip_5", "dip_6", "army", "navy" }
            .Select(s => $"\ttype iv2_nat_shell_{s} = widget {{\n\t\tparentanchor = left\n\t}}\n\n");
        yield return (Shells, "types iv2_nat_ideas_setup {\n" + string.Concat(shells) +
                              "\ttype iv2_picked_national_ideas_shell = widget {\n\t\tparentanchor = left\n\t}\n}\n");
        yield return (Purger, "iv2_log_purger = {\n\tcmf_suppress = { v = iv2_nat_army_1 }\n\tcmf_suppress = { v = iv2_nat_army_2 }\n\n" +
                              "\tcmf_suppress = { v = iv2_var_nat_army_1 }\n\tcmf_suppress = { v = iv2_var_nat_army_2 }\n}\n");
        yield return (NoAi, "iv2_ga_list_no_ai = {\n\tactions = {\n\t\tiv2_ga_deselect_ideagroup_army_1\n\t\t# National Ideas Start\n\t\tiv2_ga_deselect_nat_idea_army_1\n\t}\n}\n");
    }

    /// <summary>Ordner der Fenster-Dateien einer Kategorie (Orte 9-13).</summary>
    private static string GuiFolder(string category) =>
        "in_game/gui/Idea Variation 2 National Ideas/Dynamic National Ideas " +
        category switch { "adm" => "Admin", "dip" => "Diplomatic", "army" => "Army", _ => "Navy" };

    private static string Overview(string category) => $"{GuiFolder(category)}/IV2_dyn_national_ideas_overview_{category}.gui";

    private static string SeeCards(string category) => $"{GuiFolder(category)}/IV2_dyn_national_ideas_{category}.gui";

    private static string SelectTooltips(string category) => $"{GuiFolder(category)}/IV2_dyn_select_nat_idea_templates_{category}.gui";

    private static string LevelTooltips(string category) => $"{GuiFolder(category)}/IV2_dyn_national_ideas_nat_ideas_tooltip_templates_{category}.gui";

    private static string RemoveTooltips(string category) => $"{GuiFolder(category)}/IV2_dyn_nat_idea_tt_remove_{category}.gui";

    /// <summary>Die Karte der gewählten Idee (Ort 10) vom Kopf bis zur schließenden Klammer.</summary>
    private static string SeeCard(string text, string id)
    {
        var start = text.IndexOf($"\ttype iv2_nat_idea_see_{id} = widget {{", StringComparison.Ordinal);
        Assert.True(start >= 0, $"see card {id} missing");
        return text[start..(text.IndexOf("\n\t}\n", start, StringComparison.Ordinal) + 4)];
    }

    private static string AiList(string category) => $"in_game/common/generic_action_ai_lists/02_iv2_generic_action_list_dynamic_nat_ideas_{category}.txt";

    private const string SetupEffect = "in_game/common/scripted_effects/01_iv2_scripted_effect_dynamic_national_idea_setup.txt";

    private const string DebugCopy = "in_game/common/scripted_guis/IV2_scripted_gui_reload_gui_button.txt";

    private const string Shells = "in_game/gui/Idea Variation 2 National Ideas/IV2_dynamic_national_ideas_setup.gui";

    private const string Purger = "in_game/common/scripted_effects/01_iv2_scripted_effect_log_purger.txt";

    private const string NoAi = "in_game/common/generic_action_ai_lists/iv2_generic_action_list_no_ai.txt";

    private static string CountryView(string category) => $"in_game/gui/IV2 Country View/IV2_cv_national_dynamic_{category}.gui";

    private const string CountryViewShells = "in_game/gui/IV2 Country View/IV2_cv_shells.gui";

    /// <summary>Paar im Setup-Effekt (Orte 4, 5) wie in der Mod, mit der Einrückung <paramref name="indent"/>.</summary>
    private static string Pair(string id, string list, string indent = "\t\t") =>
        $"{indent}set_variable = {{\n{indent}\tname = iv2_nat_{id}\n{indent}\tvalue = flag:iv2_nat_{id}\n{indent}}}\n" +
        $"{indent}add_to_variable_list = {{\n{indent}\t\tname = {list}\n{indent}\t\ttarget = flag:iv2_nat_{id}\n{indent}}}\n";

    /// <summary>Alle Dateien der Mini-Mod mit ihrem Text.</summary>
    private Dictionary<string, string> Snapshot() =>
        Directory.EnumerateFiles(Mod, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(Mod, f).Replace('\\', '/'), File.ReadAllText, StringComparer.Ordinal);

    private static string StaticModifiers(string category) => $"main_menu/common/static_modifiers/01_IV2_dynamic_national_ideas_{category}.txt";

    private static string GenericActions(string category) => $"in_game/common/generic_actions/iv2_ga_dynamic_national_ideas_{category}.txt";

    private static string ScriptedGuis(string category) => $"in_game/common/scripted_guis/IV2_scripted_gui_dynamic_{category}.txt";

    private static string Triggers(string category) => $"in_game/common/scripted_triggers/iv2_scripted_trigger_dynamic_nat_idea_{category}.txt";

    /// <summary>Ein erfundener Stand: Name mit deutscher Übersetzung, je Stufe ein Modifikator mit steigendem Wert; adm/dip mit dem ersten Unter-Reiter.</summary>
    private GeneratorState State(string category, int number, Action<GeneratorState>? adjust = null)
    {
        var state = GeneratorState.CreateEmpty(_package, Languages);
        PackageKey.Apply(_package, state, $"{category}_{number}");
        state.Head.Values["name"] = $"Test Idea {number}";
        state.Head.Translations["name"] = new Dictionary<string, string> { ["german"] = $"Testidee {number}" };
        state.Head.Values["subTab"] = category is "adm" or "dip" ? category + "_1" : string.Empty;
        var levels = state.Head.Children["levels"];
        for (var i = 0; i < levels.Count; i++)
        {
            levels[i].Values["modifiers"] = $"global_tax_modifier = 0.{i + 1}";
        }

        adjust?.Invoke(state);
        return state;
    }

    private static void SingleLevel(GeneratorState state)
    {
        var levels = state.Head.Children["levels"];
        levels.RemoveRange(1, levels.Count - 1);
    }

    private static void FirstTwoExpensive(GeneratorState state)
    {
        var levels = state.Head.Children["levels"];
        levels[0].Values["expensive"] = "yes";
        levels[1].Values["expensive"] = "yes";
    }

    private void Generate(GeneratorState state) =>
        SlicePackage.Write(GeneratorPlanner.Plan(_package, state, new GeneratorTarget(Mod, null, Languages)));

    private string ModFile(string path) => File.ReadAllText(Path.Combine(Mod, path)).Replace("\r\n", "\n");

    /// <summary>Der Block mit dem Kopf <paramref name="header"/> bis zu seiner schließenden Klammer am Zeilenanfang.</summary>
    private static string Block(string text, string header)
    {
        var start = text.IndexOf(header + " = {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{header} missing");
        return text[start..(text.IndexOf("\n}", start, StringComparison.Ordinal) + 2)];
    }

    [Fact]
    public void Package_check_is_ok()
    {
        var issues = PackageChecker.Check(PackageDocument.Load(PackageFolder), new PackageFiles(PackageFolder), "en");

        Assert.Empty(issues);
    }

    [Fact]
    public void Candidates_section_has_the_spec_values()
    {
        var candidates = _package.Candidates;

        Assert.NotNull(candidates);
        Assert.Equal("country", candidates.Category);
        var used = Assert.Single(candidates.Used);
        Assert.Equal("^main_menu/common/static_modifiers/01_IV2_dynamic_national_ideas_(adm|dip|army|navy)\\.txt$", used.File);
        Assert.Equal("national_idea_modifier_(adm|dip|army|navy)_\\d+(_[123])?", used.Block);
        Assert.Equal(new[] { "game_data" }, used.Except);
        Assert.Equal(
            new[] { "goodsOutput", "goodsOther", "modOwn", "ai", "estates", "religion", "government", "actionCost", "unlock", "pops", "proximity", "diseaseMovement" },
            candidates.Groups.Select(g => g.Id));
        Assert.Equal(new[] { "goodsOutput", "goodsOther" }, candidates.Groups.Where(g => g.Hidden).Select(g => g.Id));
        Assert.Equal(new[] { "category", "subTab" }, candidates.Create.Ask);
        Assert.Equal("name", candidates.Create.Name);
        Assert.Equal("levels.modifiers", candidates.Create.Modifiers);
    }

    [Fact]
    public void New_idea_writes_places_1_2_6_7()
    {
        Generate(State("army", 1));

        var modifiers = ModFile(StaticModifiers("army"));
        foreach (var suffix in new[] { "", "_1", "_2", "_3" })
        {
            // Genau vier Tabs: die ganze Zeile, nicht nur ihr Ende
            Assert.Contains($"\n\t\t\t\tNOT = {{ has_country_modifier = national_idea_modifier_army_1{suffix} }}\n", ModFile(GenericActions("army")));
            Assert.Contains($"iv2_sgui_dynamic_nat_army_1{suffix} = {{\n", ModFile(ScriptedGuis("army")));
            Assert.Contains($"iv2_sct_dnat_idea_army_1{suffix} = {{\n\n}}", ModFile(Triggers("army")));
        }

        Assert.Contains("national_idea_modifier_army_1 = {\n\tgame_data = {\n\t    category = country\n    }\n\tglobal_tax_modifier = 0.1\n}\n", modifiers);
        Assert.Contains("national_idea_modifier_army_1_3 = {\n\tgame_data = {\n\t    category = country\n    }\n\tglobal_tax_modifier = 0.4\n}", modifiers);
        var ga = ModFile(GenericActions("army"));
        var select = Block(ga, "iv2_ga_select_national_idea_army_1");
        Assert.Contains("\tai_tick = never\n\t#ai_tick_frequency = 36\n", select);
        Assert.Contains("\t\t\ttext = iv2_tt_no_points_for_nat_idea_army\n\t\t\tvar:iv2_var_progress_points_army >= var:iv2_var_national_idea_cost_army\n", select);
        Assert.Contains("\t\t\ttext = iv2_tt_sub_points_army\n", select);
        Assert.Contains("\t\t\tiv2_sct_dnat_idea_army_1 = yes\n", select);
        foreach (var (suffix, previous) in new[] { ("_1", ""), ("_2", "_1"), ("_3", "_2") })
        {
            var level = Block(ga, "iv2_ga_select_national_idea_army_1" + suffix);
            Assert.Contains("\tai_tick = monthly\n\tai_tick_frequency = 36\n", level);
            Assert.Contains($"\t\t\thas_country_modifier = national_idea_modifier_army_1{previous}\n", level);
            Assert.Contains("\t\t\ttext = iv2_tt_sub_points_innovation_army\n", level);
            Assert.Contains($"\t\t\t\tremove_country_modifier = national_idea_modifier_army_1{previous}\n", level);
        }

        var deselect = Block(ga, "iv2_ga_deselect_nat_idea_army_1");
        Assert.Contains("\t\t\tOR = {\n\t\t\t\thas_country_modifier = national_idea_modifier_army_1\n", deselect);
        Assert.Contains("\t\t\t\t\t\tsubtract = iv2_innovation_cost_decrease_x3\n", deselect);
        Assert.Contains("#### Deselect National Idea army_1\n###############################################################\n\n\niv2_ga_deselect_nat_idea_army_1 = {", ga);
        Assert.Contains("###################################################\n###### Nat Ideas army_1\n###################################################\n\niv2_sgui_dynamic_nat_army_1 = {",
            ModFile(ScriptedGuis("army")));
    }

    [Fact]
    public void Single_level_idea_writes_only_level_one_actions()
    {
        Generate(State("navy", 2, SingleLevel));

        var ga = ModFile(GenericActions("navy"));
        Assert.Contains("iv2_ga_select_national_idea_navy_2 = {", ga);
        Assert.Contains("iv2_ga_deselect_nat_idea_navy_2 = {", ga);
        Assert.DoesNotContain("iv2_ga_select_national_idea_navy_2_", ga);
        Assert.DoesNotContain("national_idea_modifier_navy_2_", ga);
        Assert.DoesNotContain("#### National Idea Level", ga);
        var deselect = Block(ga, "iv2_ga_deselect_nat_idea_navy_2");
        Assert.Contains("\tallow = {\n\t\tscope:actor = {\n\t\t\thas_country_modifier = national_idea_modifier_navy_2\n\t\t}\n\t}", deselect);
        Assert.DoesNotContain("OR = {", deselect);
        Assert.DoesNotContain("innovation", deselect);
        Assert.Single(ModFile(StaticModifiers("navy")).Split("national_idea_modifier_navy_2").Skip(1));
        var sgui = ModFile(ScriptedGuis("navy"));
        Assert.Contains("iv2_sgui_dynamic_nat_navy_2 = {", sgui);
        Assert.DoesNotContain("navy_2_1", sgui);
        var triggers = ModFile(Triggers("navy"));
        Assert.Equal(4, triggers.Split("iv2_sct_dnat_idea_navy_2").Length - 1);
    }

    [Fact]
    public void Expensive_level_uses_expensive_cost()
    {
        Generate(State("adm", 31, FirstTwoExpensive));

        var ga = ModFile(GenericActions("adm"));
        var select = Block(ga, "iv2_ga_select_national_idea_adm_31");
        Assert.Contains("\t\t\ttext = iv2_tt_no_points_for_nat_idea_expensive_adm\n\t\t\tvar:iv2_var_progress_points_adm >= var:iv2_var_national_idea_cost_expensive_adm\n", select);
        Assert.Contains("\t\t\ttext = iv2_tt_sub_points_expensive_adm\n\t\t\t\tchange_variable = {\n\t\t\t\t\tname = iv2_var_progress_points_adm\n\t\t\t\t\tsubtract = var:iv2_var_national_idea_cost_expensive_adm\n", select);
        // Die Kostensteigerungen bleiben gleich
        Assert.Contains("\t\t\t\t\tname = iv2_var_national_idea_cost_adm\n\t\t\t\t\tadd = iv2_progress_cost_increase\n", select);
        var level2 = Block(ga, "iv2_ga_select_national_idea_adm_31_1");
        Assert.Contains("\t\t\ttext = iv2_tt_no_innovation_points_for_nat_idea_expensive_adm\n", level2);
        Assert.Contains("\t\t\tvar:iv2_var_innovation_points_adm >= var:iv2_var_national_idea_innovation_cost_expensive_adm\n", level2);
        Assert.Contains("\t\t\ttext = iv2_tt_sub_points_innovation_expensive_adm\n\t\t\t\tchange_variable = {\n\t\t\t\t\tname = iv2_var_innovation_points_adm\n\t\t\t\t\tsubtract = var:iv2_var_national_idea_innovation_cost_expensive_adm\n", level2);
        Assert.Contains("\t\t\t\t\tname = iv2_var_national_idea_innovation_cost_adm\n\t\t\t\t\tadd = iv2_innovation_cost_increase\n", level2);
        var level3 = Block(ga, "iv2_ga_select_national_idea_adm_31_2");
        Assert.Contains("\t\t\ttext = iv2_tt_no_innovation_points_for_nat_idea_adm\n", level3);
        Assert.Contains("\t\t\ttext = iv2_tt_sub_points_innovation_adm\n", level3);
        Assert.DoesNotContain("nat_idea_expensive_", level3);
        Assert.DoesNotContain("sub_points_innovation_expensive_", level3);
    }

    [Fact]
    public void Generated_idea_reads_back_and_replans_unchanged()
    {
        Generate(State("army", 1));
        Generate(State("adm", 31, s => { FirstTwoExpensive(s); s.Head.Values["condition"] = "government_type = government_type:republic"; }));
        Generate(State("navy", 54, SingleLevel));
        Generate(State("dip", 47, s =>
        {
            s.Head.Values["subTab"] = "dip_2";
            s.Head.Values["condition"] = "OR = {\n\tcountry_has_estate = estate_type:nobles_estate\n\tcountry_has_estate = estate_type:clergy_estate\n}";
            s.Head.Children["levels"][2].Values["modifiers"] = "global_tax_modifier = 0.3\n# note\nland_morale = 0.05";
        }));

        var keys = new[] { "adm_31", "army_1", "dip_47", "navy_54" };
        Assert.Equal(keys, GeneratorStateReader.Keys(_package, Mod));
        var adm = GeneratorStateReader.Read(_package, Mod, "adm_31", Languages)!.State;
        Assert.Equal("government_type = government_type:republic", adm.Head.Values["condition"]);
        Assert.Equal(new[] { "yes", "yes", "no", "no" }, adm.Head.Children["levels"].Select(l => l.Values["expensive"]));
        Assert.Equal("global_tax_modifier = 0.4", adm.Head.Children["levels"][3].Values["modifiers"]);
        var navy = GeneratorStateReader.Read(_package, Mod, "navy_54", Languages)!.State;
        Assert.Single(navy.Head.Children["levels"]);
        Assert.Equal(string.Empty, navy.Head.Values.GetValueOrDefault("condition") ?? string.Empty);
        var dip = GeneratorStateReader.Read(_package, Mod, "dip_47", Languages)!.State;
        Assert.Equal("OR = {\n\tcountry_has_estate = estate_type:nobles_estate\n\tcountry_has_estate = estate_type:clergy_estate\n}", dip.Head.Values["condition"]);
        Assert.Equal("global_tax_modifier = 0.3\n# note\nland_morale = 0.05", dip.Head.Children["levels"][2].Values["modifiers"]);
        foreach (var key in keys)
        {
            var read = GeneratorStateReader.Read(_package, Mod, key, Languages)!.State;
            // Der Plan muss jede Datei unverändert lassen: alles, was die Vorlagen schreiben, kommt beim Einlesen zurück
            var plan = GeneratorPlanner.Plan(_package, read, new GeneratorTarget(Mod, null, Languages));
            Assert.All(plan.Files, f => Assert.True(f.Status == PlannedFileStatus.Unchanged, $"{key}: {f.RelativePath} {f.Status} {FirstDifference(f.OriginalText, f.NewText)}"));
        }
    }

    [Fact]
    public void New_idea_goes_after_the_last_idea_of_its_category()
    {
        Generate(State("army", 1));
        Generate(State("army", 2));
        // Neu erzeugt nach army_2: army_1 bleibt vorn, army_3 kommt ans Ende
        Generate(State("army", 1));
        Generate(State("army", 3));

        foreach (var (path, unit) in new[]
                 {
                     (StaticModifiers("army"), "national_idea_modifier_army_{0} = {{"),
                     (GenericActions("army"), "iv2_ga_select_national_idea_army_{0} = {{"),
                     (ScriptedGuis("army"), "iv2_sgui_dynamic_nat_army_{0} = {{"),
                     (Triggers("army"), "iv2_sct_dnat_idea_army_{0} = {{"),
                     (Overview("army"), "\ttype iv2_nat_idea_army_{0} = widget {{"),
                     (SeeCards("army"), "\ttype iv2_nat_idea_see_army_{0} = widget {{"),
                     (SelectTooltips("army"), "template iv_2_tooltip_template_nat_idea_army_{0} {{"),
                     (LevelTooltips("army"), "template iv2_individual_nat_idea_army_{0}_3 {{"),
                     (RemoveTooltips("army"), "template iv2_individual_nat_idea_remove_army_{0} {{"),
                 })
        {
            var text = ModFile(path);
            var positions = new[] { 1, 2, 3 }.Select(n => text.IndexOf(string.Format(unit, n), StringComparison.Ordinal)).ToList();
            Assert.True(positions[0] >= 0 && positions[0] < positions[1] && positions[1] < positions[2], $"{path}: {string.Join(", ", positions)}");
            Assert.Single(text.Split(string.Format(unit, 1)).Skip(1));
        }
    }

    [Fact]
    public void Disabled_idea_comments_its_modifiers_under_the_note()
    {
        Generate(State("adm", 8, s =>
        {
            s.Head.Values["disabled"] = "yes";
            s.Head.Values["disabledNote"] = "2026-10-10 invalid modifier";
        }));

        var block = Block(ModFile(StaticModifiers("adm")), "national_idea_modifier_adm_8_1");
        Assert.Contains("    }\n\t# disabled: 2026-10-10 invalid modifier\n\t# global_tax_modifier = 0.2\n}", block);
    }

    [Fact]
    public void Candidates_used_counts_only_active_dynamic_ideas()
    {
        Generate(State("army", 1));
        Generate(State("army", 2, s =>
        {
            foreach (var level in s.Head.Children["levels"])
            {
                level.Values["modifiers"] = "naval_morale = 0.1";
            }

            s.Head.Values["disabled"] = "yes";
            s.Head.Values["disabledNote"] = "2026-10-12 test";
        }));

        var used = CandidateList.Used(_package, Mod);

        Assert.Contains("global_tax_modifier", used);
        Assert.DoesNotContain("naval_morale", used);
    }

    [Theory]
    [InlineData("adm", "adm_3", "", false)]
    [InlineData("adm", "", "", true)]
    [InlineData("dip", "adm_1", "", true)]
    [InlineData("army", "", "", false)]
    [InlineData("navy", "dip_1", "", true)]
    [InlineData("army", "", "4", false)]
    public void Sub_tab_follows_the_category(string category, string subTab, string guiLevels, bool error)
    {
        // Mit KI-Tags, sonst warnt das Paket wegen fehlender Tags
        var state = State(category, 5, s => { s.Head.Values["subTab"] = subTab; s.Head.Values["guiLevels"] = guiLevels; s.Head.Values["aiTags"] = "ai_balanced"; });

        var issues = FormValidator.Check(_package, state, new GeneratorRenderer(_package));

        Assert.Equal(error, issues.Any(i => i.Severity == IssueSeverity.Error));
        Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void Candidates_batch_states_generate_after_values_are_filled()
    {
        var picks = new[]
        {
            new CandidatePick("naval_morale", false, new Dictionary<string, string>()),
            new CandidatePick("global_tax_modifier", false, new Dictionary<string, string>()),
        };
        var asked = new Dictionary<string, string> { ["category"] = "navy", ["subTab"] = string.Empty };
        var states = CandidateBatch.Create(_package, picks, asked, Array.Empty<string>(), Languages, Mod);
        Assert.Equal(new[] { "navy_1", "navy_2" }, states.Select(s => s.Key));

        // Mit leeren Werten blockiert die Prüfung das Erzeugen
        var empty = FormValidator.Check(_package, states[0], new GeneratorRenderer(_package));
        Assert.Contains(empty, i => i.Severity == IssueSeverity.Error && i.Path == "levels[1].modifiers");

        foreach (var state in states)
        {
            foreach (var level in state.Head.Children["levels"])
            {
                level.Values["modifiers"] = level.Values["modifiers"].TrimEnd() + " 0.1";
            }

            Assert.DoesNotContain(FormValidator.Check(_package, state, new GeneratorRenderer(_package)), i => i.Severity == IssueSeverity.Error);
            Generate(state);
        }

        var text = ModFile(StaticModifiers("navy"));
        Assert.Contains("global_tax_modifier = 0.1", Block(text, "national_idea_modifier_navy_2_1"));
        Assert.Contains("naval_morale = 0.1", Block(text, "national_idea_modifier_navy_1_3"));
    }

    [Fact]
    public void Gui_levels_above_the_level_count_warn()
    {
        var state = State("army", 5, s => { SingleLevel(s); s.Head.Values["guiLevels"] = "2"; });

        var issues = FormValidator.Check(_package, state, new GeneratorRenderer(_package));

        Assert.Contains(issues, i => i.Severity == IssueSeverity.Warning && i.Message == "Levels shown in the window is greater than the number of levels.");
    }

    [Fact]
    public void New_idea_writes_places_9_to_13()
    {
        Generate(State("army", 1));

        var overview = ModFile(Overview("army"));
        Assert.StartsWith("types iv2_nat_ideas_army_all {\n\ttype iv2_nat_idea_army_1 = widget {\n", overview);
        Assert.Contains("\t\t\t\t\taction_name = \"iv2_ga_select_national_idea_army_1\"\n", overview);
        Assert.Contains("\t\t\t\t\t\tusing = iv_2_tooltip_template_nat_idea_army_1\n", overview);
        Assert.Contains("GetVariableSystem.Get('iv2_search_nat_idea_army')))]\"\n", overview);
        var card = SeeCard(ModFile(SeeCards("army")), "army_1");
        Assert.Contains("\t\tvisible = \"[EqualTo_string(Scope.GetFlagName, 'iv2_picked_nat_idea_army_1')]\"\n", card);
        Assert.Contains("\t\t\t\tusing = iv2_individual_nat_idea_remove_army_1\n", card);
        foreach (var k in new[] { 1, 2, 3 })
        {
            Assert.Contains($"\t\t\t\taction_name = \"iv2_ga_select_national_idea_army_1_{k}\"\n", card);
        }

        // Volle Karte: Stufe 4 zeigt die Zahl vor dem Symbol und statt des Knopfs das Symbol iv2_upgrade_no
        Assert.DoesNotContain("iv2_ga_select_national_idea_army_1_4", card);
        Assert.Single(card.Split("iv2_upgrade_no.dds").Skip(1));
        Assert.True(card.IndexOf("iv2_number_4.dds", StringComparison.Ordinal) < card.IndexOf("national_idea_modifier_army_1_3').GetIcon", StringComparison.Ordinal));
        var select = ModFile(SelectTooltips("army"));
        Assert.StartsWith("template iv_2_tooltip_template_nat_idea_army_1 {\n", select);
        Assert.Contains("\t\t\t\ttexture = \"gfx/interface/icons/national_ideas/icon_national_ideas_army.dds\"\n", select);
        Assert.Contains("\t\t\ttext = \"[iv2_gc_national_idea_army|e]\"\n", select);
        var levels = ModFile(LevelTooltips("army"));
        foreach (var suffix in new[] { "", "_1", "_2", "_3" })
        {
            Assert.Contains($"template iv2_individual_nat_idea_army_1{suffix} {{\n", levels);
            Assert.Contains($"\t\t\ttext = \"STATIC_MODIFIER_NAME_national_idea_modifier_army_1{suffix}\"\n", levels);
            Assert.Contains($"\t\t\t\t\ttext = \"iv2_ga_select_national_idea_army_1{suffix}_desc\"\n", levels);
        }

        var remove = ModFile(RemoveTooltips("army"));
        Assert.StartsWith("template iv2_individual_nat_idea_remove_army_1 {\n", remove);
        Assert.Contains("\t\t\ttext = \"iv2_ga_deselect_nat_idea_army_1\"\n", remove);
        Assert.Contains("\t\t\t\t\ttext = \"iv2_tt_give_up_nat_idea_army\"\n", remove);
    }

    [Fact]
    public void Adm_cards_go_inside_the_types_block_before_its_comment()
    {
        Generate(State("adm", 1));
        Generate(State("adm", 2));

        var text = ModFile(SeeCards("adm"));
        Assert.StartsWith("types iv2_nat_ideas_see_adm {\n\ttype iv2_nat_idea_see_adm_1 = widget {\n", text);
        Assert.EndsWith("\t\t# Level 4 End\n\t}\n} # types close bracket\n", text);
        Assert.True(text.IndexOf("see_adm_1 ", StringComparison.Ordinal) < text.IndexOf("see_adm_2 ", StringComparison.Ordinal));
        Assert.Single(text.Split("} # types close bracket").Skip(1));
    }

    [Fact]
    public void Gui_levels_two_shows_two_levels_and_no_upgrade_icon()
    {
        Generate(State("army", 28, s => s.Head.Values["guiLevels"] = "2"));

        var card = SeeCard(ModFile(SeeCards("army")), "army_28");
        Assert.Contains("iv2_number_1.dds", card);
        Assert.Contains("iv2_number_2.dds", card);
        Assert.DoesNotContain("iv2_number_3.dds", card);
        Assert.DoesNotContain("army_28_2", card);
        // Ein Knopf (Stufe 1 -> 2), an Stufe 2 das Symbol statt des Knopfs, mit dem Scripted GUI der Stufe 2
        Assert.Contains("\t\t\t\taction_name = \"iv2_ga_select_national_idea_army_28_1\"\n", card);
        Assert.Single(card.Split("action_name = \"iv2_ga_select_national_idea_army_28_").Skip(1));
        Assert.Single(card.Split("iv2_upgrade_no.dds").Skip(1));
        var noUpgrade = card[card.LastIndexOf("\t\ticon = {", card.IndexOf("iv2_upgrade_no.dds", StringComparison.Ordinal), StringComparison.Ordinal)..];
        Assert.StartsWith("\t\ticon = {\n\t\t\tdatacontext = \"[GetScriptedGui('iv2_sgui_dynamic_nat_army_28_1')]\"\n", noUpgrade);
        // Gekappte Karte: an der letzten Stufe das Symbol vor der Zahl
        Assert.True(card.IndexOf("national_idea_modifier_army_28_1').GetIcon", StringComparison.Ordinal) < card.IndexOf("iv2_number_2.dds", StringComparison.Ordinal));
        // Die anderen Orte bleiben bei vier Stufen
        Assert.Contains("template iv2_individual_nat_idea_army_28_3 {\n", ModFile(LevelTooltips("army")));
        Assert.Contains("iv2_ga_select_national_idea_army_28_3 = {", ModFile(GenericActions("army")));
    }

    [Fact]
    public void Single_level_idea_keeps_four_text_units()
    {
        Generate(State("navy", 2, SingleLevel));

        var levels = ModFile(LevelTooltips("navy"));
        foreach (var suffix in new[] { "", "_1", "_2", "_3" })
        {
            Assert.Single(levels.Split($"template iv2_individual_nat_idea_navy_2{suffix} {{\n").Skip(1));
        }

        var card = SeeCard(ModFile(SeeCards("navy")), "navy_2");
        Assert.Contains("iv2_number_1.dds", card);
        Assert.DoesNotContain("iv2_number_2.dds", card);
        Assert.DoesNotContain("navy_2_", card);
        Assert.DoesNotContain("action_name = \"iv2_ga_select_national_idea_navy_2", card);
        Assert.Single(card.Split("iv2_upgrade_no.dds").Skip(1));
    }

    [Fact]
    public void Gui_levels_read_back()
    {
        Generate(State("army", 28, s => s.Head.Values["guiLevels"] = "2"));
        Generate(State("army", 43, s => s.Head.Values["guiLevels"] = "1"));
        Generate(State("army", 1));
        Generate(State("army", 2, s => s.Head.Values["guiLevels"] = "4"));

        foreach (var (key, expected) in new[] { ("army_28", "2"), ("army_43", "1"), ("army_1", ""), ("army_2", "") })
        {
            var read = GeneratorStateReader.Read(_package, Mod, key, Languages)!.State;
            Assert.Equal(expected, read.Head.Values.GetValueOrDefault("guiLevels") ?? string.Empty);
            // Neu geplant aus dem eingelesenen Stand: dieselbe Karte (der eingelesene Wert wirkt als Zahl)
            AssertUnchanged(key, read);
        }
    }

    [Fact]
    public void Number_boundary_keeps_army_10_and_11()
    {
        Generate(State("army", 1));
        Generate(State("army", 10));
        Generate(State("army", 11));
        var before = Snapshot();

        Generate(State("army", 1, s => s.Head.Children["levels"][0].Values["modifiers"] = "land_morale = 0.5"));

        var after = Snapshot();
        Assert.Contains("\tland_morale = 0.5\n", ModFile(StaticModifiers("army")));
        var files = before.Where(f => f.Value.Contains("army_10", StringComparison.Ordinal)).ToList();
        foreach (var path in new[]
                 {
                     StaticModifiers("army"), GenericActions("army"), ScriptedGuis("army"), Triggers("army"),
                     IdeaLoc("army", "english"), IdeaLoc("army", "german"), Messages("english"), Messages("german"), MessageTypes,
                     CountryView("army"), CountryViewShells,
                 })
        {
            Assert.Contains(files, f => f.Key == path);
        }

        foreach (var (path, text) in files)
        {
            // army_10 und army_11 stehen hinter army_1: alles ab der ersten Nennung von army_10 bleibt zeichengleich
            var now = after[path];
            Assert.Equal(text[text.IndexOf("army_10", StringComparison.Ordinal)..], now[now.IndexOf("army_10", StringComparison.Ordinal)..]);
        }
    }

    [Fact]
    public void Two_level_idea_generates_and_reads_back()
    {
        Generate(State("army", 2, s => s.Head.Children["levels"].RemoveRange(2, 2)));

        var ga = ModFile(GenericActions("army"));
        Assert.Equal(2, Block(ga, "iv2_ga_select_national_idea_army_2").Split("NOT = { has_country_modifier").Length - 1);
        Assert.Contains("iv2_ga_select_national_idea_army_2_1 = {", ga);
        Assert.DoesNotContain("iv2_ga_select_national_idea_army_2_2", ga);
        var deselect = Block(ga, "iv2_ga_deselect_nat_idea_army_2");
        Assert.Contains("\n\t\t\tOR = {\n\t\t\t\thas_country_modifier = national_idea_modifier_army_2\n\t\t\t\thas_country_modifier = national_idea_modifier_army_2_1\n\t\t\t}\n", deselect);
        Assert.Single(deselect.Split("\t\t\t\t\t\tsubtract = iv2_innovation_cost_increase\n").Skip(1));
        Assert.DoesNotContain("decrease_x", deselect);
        Assert.Contains("\n\t\t\tiv2_ga_select_national_idea_army_2_1\n\t\t\t#iv2_ga_select_national_idea_army_2_2\n\t\t\t#iv2_ga_select_national_idea_army_2_3\n", ModFile(AiList("army")));

        var read = GeneratorStateReader.Read(_package, Mod, "army_2", Languages)!.State;
        Assert.Equal(new[] { "global_tax_modifier = 0.1", "global_tax_modifier = 0.2" }, read.Head.Children["levels"].Select(l => l.Values["modifiers"]));
        AssertUnchanged("army_2", read);
    }

    [Fact]
    public void New_idea_joins_lists_and_shells_after_its_category()
    {
        Generate(State("adm", 1));
        Generate(State("dip", 1));
        Generate(State("adm", 2));

        foreach (var path in new[] { SetupEffect, DebugCopy })
        {
            Assert.Contains(Pair("adm_1", "iv2_nat_ideas_list_adm_1") + Pair("adm_2", "iv2_nat_ideas_list_adm_1") + Pair("dip_1", "iv2_nat_ideas_list_dip_1"), ModFile(path));
        }

        var shells = ModFile(Shells);
        Assert.Contains("\ttype iv2_nat_shell_adm_1 = widget {\n\t\tparentanchor = left\n\t\tiv2_nat_idea_adm_1 = {}\n\t\tiv2_nat_idea_adm_2 = {}\n\t}\n", shells);
        Assert.Contains("\ttype iv2_nat_shell_dip_1 = widget {\n\t\tparentanchor = left\n\t\tiv2_nat_idea_dip_1 = {}\n\t}\n", shells);
        Assert.Contains("\t\tparentanchor = left\n\t\tiv2_nat_idea_see_adm_1 = {}\n\t\tiv2_nat_idea_see_adm_2 = {}\n\t\tiv2_nat_idea_see_dip_1 = {}\n\t}\n}", shells);
        var purger = ModFile(Purger);
        Assert.Contains("\tcmf_suppress = { v = iv2_nat_adm_1 }\n\tcmf_suppress = { v = iv2_nat_adm_2 }\n", purger);
        Assert.Contains("\tcmf_suppress = { v = iv2_var_nat_adm_1 }\n\tcmf_suppress = { v = iv2_var_nat_adm_2 }\n", purger);
        Assert.True(purger.IndexOf("iv2_var_nat_adm_2 ", StringComparison.Ordinal) < purger.IndexOf("iv2_nat_dip_1 ", StringComparison.Ordinal));
        var ai = ModFile(AiList("adm"));
        Assert.Contains("\t\t\t# All adm Selects\n\t\t\tiv2_ga_select_national_idea_adm_1\n\t\t\tiv2_ga_select_national_idea_adm_2\n\t}", ai);
        Assert.Contains("\t\t\t# All adm Level Selects\n" + string.Concat(new[] { 1, 2 }.SelectMany(n => new[] { 1, 2, 3 }.Select(k => $"\t\t\tiv2_ga_select_national_idea_adm_{n}_{k}\n"))) + "\t}", ai);
        // Nur deaktivierte Ideen stehen in no_ai
        Assert.DoesNotContain("national_idea_adm", ModFile(NoAi));
    }

    [Fact]
    public void Conditional_idea_regenerates_in_place()
    {
        const string list = "iv2_nat_ideas_list_dip_1";
        var block = "\t\tif = {\n\t\t\tlimit = {\n\t\t\t\tc = yes\n\t\t\t}\n" + Pair("dip_1", list, "\t\t\t") + Pair("dip_2", list, "\t\t\t") + "\t\t}\n";
        _temp.Write("mod/" + SetupEffect, "iv2_dynamic_national_ideas_list_setup_effect = {\n" + Pair("adm_1", "iv2_nat_ideas_list_adm_1") + block + "}\n");
        _temp.Write("mod/" + DebugCopy, "iv2_get_test = {\n\teffect = {\n" + Pair("adm_1", "iv2_nat_ideas_list_adm_1") + block + "\t}\n}\n");
        var setup = ModFile(SetupEffect);
        var debug = ModFile(DebugCopy);

        Generate(State("dip", 1, s => s.Head.Values["condition"] = "c = yes"));

        Assert.Equal(setup, ModFile(SetupEffect));
        Assert.Equal(debug, ModFile(DebugCopy));
        // Eine neue Idee mit derselben Bedingung kommt ans Ende des if
        Generate(State("dip", 3, s => s.Head.Values["condition"] = "c = yes"));
        Assert.Contains(Pair("dip_2", list, "\t\t\t") + Pair("dip_3", list, "\t\t\t") + "\t\t}\n}", ModFile(SetupEffect));
    }

    [Fact]
    public void Disable_and_reenable_round_trip()
    {
        Generate(State("army", 1));
        var original = Snapshot();

        Generate(State("army", 1, s => { s.Head.Values["disabled"] = "yes"; s.Head.Values["disabledNote"] = "test note"; }));

        // 1. Auswahlfeld im Fenster kommentiert, die Karte der gewählten Ideen bleibt
        Assert.Contains("\n\t\t#iv2_nat_idea_army_1 = {} # disabled: test note\n", ModFile(Shells));
        Assert.Contains("\n\t\tiv2_nat_idea_see_army_1 = {}\n", ModFile(Shells));
        // 2. Wahl der Stufe 1 in der KI-Liste kommentiert, die Stufen 2-4 bleiben
        var ai = ModFile(AiList("army"));
        Assert.Contains("\n\t\t\t#iv2_ga_select_national_idea_army_1 # disabled: test note, in iv2_generic_action_list_no_ai\n", ai);
        Assert.Contains("\n\t\t\tiv2_ga_select_national_idea_army_1_1\n", ai);
        // 3. in no_ai, die Abwahl daneben bleibt
        Assert.Equal("iv2_ga_list_no_ai = {\n\tactions = {\n\t\tiv2_ga_deselect_ideagroup_army_1\n\t\t# National Ideas Start\n\t\tiv2_ga_deselect_nat_idea_army_1\n" +
                     "\t\tiv2_ga_select_national_idea_army_1\n\t}\n}\n", ModFile(NoAi));
        // 4. Setup-Effekt und Kopie: aus der Liste genommen
        var removed = "\t\tset_variable = {\n\t\t\tname = iv2_nat_army_1\n\t\t\tvalue = flag:iv2_nat_army_1\n\t\t}\n" +
                      "\t\t#add_to_variable_list = { # disabled: test note: not in the list, so no empty cell in the grid\n" +
                      "\t\t\t\t#name = iv2_nat_ideas_list_army\n\t\t\t\t#target = flag:iv2_nat_army_1\n\t\t#}\n" +
                      "\t\tremove_list_variable = { name = iv2_nat_ideas_list_army target = flag:iv2_nat_army_1 } # cleans saves from before 2026-10-10\n";
        Assert.Contains(removed, ModFile(SetupEffect));
        Assert.Contains(removed, ModFile(DebugCopy));
        // 5. Modifikatoren als Kommentar unter der Notiz
        Assert.Contains("    }\n\t# disabled: test note\n\t# global_tax_modifier = 0.1\n}", Block(ModFile(StaticModifiers("army")), "national_idea_modifier_army_1"));

        var read = GeneratorStateReader.Read(_package, Mod, "army_1", Languages)!.State;
        Assert.Equal("yes", read.Head.Values["disabled"]);
        Assert.Equal("test note", read.Head.Values["disabledNote"]);
        // Kommentierte Modifikatoren im Arbeitsstand sperren das Erzeugen nicht
        Assert.DoesNotContain(FormValidator.Check(_package, read, new GeneratorRenderer(_package)), i => i.Severity == IssueSeverity.Error);
        AssertUnchanged("army_1", read);

        // Wieder aktivieren über den eingelesenen Stand: jede Datei wie vor dem Deaktivieren
        read.Head.Values["disabled"] = "no";
        Generate(read);
        Assert.Equal(original, Snapshot());
    }

    [Fact]
    public void Hand_written_disable_notes_survive_and_reenable_starts_empty()
    {
        Generate(State("army", 5));
        // Wie die am 2026-10-10 von Hand deaktivierten Ideen: nur eine Notiz statt der Modifikatoren, Auswahlfeld und Listeneintrag mit Notiz kommentiert
        var modifiers = ModFile(StaticModifiers("army"));
        for (var i = 0; i < 4; i++)
        {
            modifiers = modifiers.Replace($"\tglobal_tax_modifier = 0.{i + 1}\n", i == 0 ? "\t# disabled 2026-10-10: reason\n" : "\t# disabled 2026-10-10, see base level\n");
        }

        _temp.Write("mod/" + StaticModifiers("army"), modifiers);
        _temp.Write("mod/" + Shells, ModFile(Shells).Replace("\t\tiv2_nat_idea_army_5 = {}", "\t\t#iv2_nat_idea_army_5 = {} # disabled 2026-10-10: invalid modifier, shell kept"));
        _temp.Write("mod/" + SetupEffect, ModFile(SetupEffect).Replace(
            "\t\tadd_to_variable_list = {\n\t\t\t\tname = iv2_nat_ideas_list_army\n\t\t\t\ttarget = flag:iv2_nat_army_5\n\t\t}\n",
            "\t\t#add_to_variable_list = { # disabled 2026-10-10: not in the list, so no empty cell in the grid\n\t\t\t\t#name = iv2_nat_ideas_list_army\n" +
            "\t\t\t\t#target = flag:iv2_nat_army_5\n\t\t#}\n" +
            "\t\tremove_list_variable = { name = iv2_nat_ideas_list_army target = flag:iv2_nat_army_5 } # cleans saves from before 2026-10-10\n"));

        var read = GeneratorStateReader.Read(_package, Mod, "army_5", Languages)!.State;

        Assert.Equal("yes", read.Head.Values["disabled"]);
        Assert.Equal("2026-10-10", read.Head.Values["disabledNote"]);
        Assert.DoesNotContain(FormValidator.Check(_package, read, new GeneratorRenderer(_package)), i => i.Severity == IssueSeverity.Error);
        var plan = GeneratorPlanner.Plan(_package, read, new GeneratorTarget(Mod, null, Languages));
        Assert.Equal(PlannedFileStatus.Unchanged, plan.Files.Single(f => f.RelativePath == StaticModifiers("army")).Status);
        Assert.Contains("\t\t#iv2_nat_idea_army_5 = {} # disabled: 2026-10-10\n", plan.Files.Single(f => f.RelativePath == Shells).NewText!.Replace("\r\n", "\n"));
        Assert.Contains("\t\t#add_to_variable_list = { # disabled: 2026-10-10: not in the list, so no empty cell in the grid\n",
            plan.Files.Single(f => f.RelativePath == SetupEffect).NewText!.Replace("\r\n", "\n"));

        read.Head.Values["disabled"] = "no";
        Generate(read);
        Assert.Equal("national_idea_modifier_army_5 = {\n\tgame_data = {\n\t    category = country\n    }\n}", Block(ModFile(StaticModifiers("army")), "national_idea_modifier_army_5"));
    }

    [Fact]
    public void Sub_tab_change_moves_the_shell_line_and_reenables()
    {
        Generate(State("adm", 7));
        Generate(State("adm", 7, s => { s.Head.Values["disabled"] = "yes"; s.Head.Values["disabledNote"] = "x"; }));
        Assert.Contains("\n\t\t#iv2_nat_idea_adm_7 = {} # disabled: x\n", ModFile(Shells));

        // Unter-Reiter wechseln und wieder aktivieren: keine alte Zeile in adm_1
        Generate(State("adm", 7, s => s.Head.Values["subTab"] = "adm_2"));

        var shells = ModFile(Shells);
        Assert.Equal("\ttype iv2_nat_shell_adm_1 = widget {\n\t\tparentanchor = left\n\t}\n", Shell(shells, "adm_1"));
        Assert.Equal("\ttype iv2_nat_shell_adm_2 = widget {\n\t\tparentanchor = left\n\t\tiv2_nat_idea_adm_7 = {}\n\t}\n", Shell(shells, "adm_2"));
        Assert.Single(shells.Split("iv2_nat_idea_adm_7 ").Skip(1));
        var read = GeneratorStateReader.Read(_package, Mod, "adm_7", Languages)!.State;
        Assert.Equal("no", read.Head.Values["disabled"]);
        Assert.Equal("adm_2", read.Head.Values["subTab"]);
        AssertUnchanged("adm_7", read);
    }

    [Fact]
    public void Disabling_an_idea_keeps_its_neighbours()
    {
        foreach (var n in new[] { 1, 2, 3 })
        {
            Generate(State("army", n));
        }

        var before = Snapshot();
        Generate(State("army", 2, s => { s.Head.Values["disabled"] = "yes"; s.Head.Values["disabledNote"] = "x"; }));
        var after = Snapshot();

        foreach (var (path, text) in before)
        {
            var now = after[path];
            if (!text.Contains("army_2", StringComparison.Ordinal))
            {
                // no_ai: nur die Wahl von army_2 kommt dazu
                Assert.Equal(text, now.Replace("\t\tiv2_ga_select_national_idea_army_2\n", string.Empty));
                continue;
            }

            // army_1 steht vor der ersten Zeile mit army_2, army_3 ab seiner ersten Zeile: beide Bereiche bleiben zeichengleich
            Assert.Equal(text[..LineStart(text, "army_2")], now[..LineStart(now, "army_2")]);
            if (text.Contains("army_3", StringComparison.Ordinal))
            {
                Assert.Equal(text[LineStart(text, "army_3")..], now[LineStart(now, "army_3")..]);
            }
        }
    }

    private static int LineStart(string text, string value) => text.LastIndexOf('\n', text.IndexOf(value, StringComparison.Ordinal)) + 1;

    /// <summary>Eine Fenster-Hülle aus der Shell-Datei, vom Kopf bis zur schließenden Klammer.</summary>
    private static string Shell(string text, string tab)
    {
        var start = text.IndexOf($"\ttype iv2_nat_shell_{tab} = widget {{", StringComparison.Ordinal);
        return text[start..(text.IndexOf("\n\t}\n", start, StringComparison.Ordinal) + 4)];
    }

    [Fact]
    public void Army_idea_gets_purger_lines_once()
    {
        var skeleton = ModFile(Purger);

        Generate(State("army", 1));
        Assert.Equal(skeleton, ModFile(Purger));

        Generate(State("army", 3));
        Generate(State("army", 3));
        Assert.Equal("iv2_log_purger = {\n\tcmf_suppress = { v = iv2_nat_army_1 }\n\tcmf_suppress = { v = iv2_nat_army_2 }\n\tcmf_suppress = { v = iv2_nat_army_3 }\n\n" +
                     "\tcmf_suppress = { v = iv2_var_nat_army_1 }\n\tcmf_suppress = { v = iv2_var_nat_army_2 }\n\tcmf_suppress = { v = iv2_var_nat_army_3 }\n}\n", ModFile(Purger));
    }

    [Fact]
    public void Single_level_idea_with_more_gui_levels_shows_one_level()
    {
        Generate(State("navy", 2, s => { SingleLevel(s); s.Head.Values["guiLevels"] = "2"; }));

        // Gezeigt wird höchstens die Zahl der Stufen: kein Knopf und kein Scripted GUI einer fehlenden Stufe
        var card = SeeCard(ModFile(SeeCards("navy")), "navy_2");
        Assert.DoesNotContain("navy_2_", card);
        Assert.DoesNotContain("iv2_number_2.dds", card);
        Assert.DoesNotContain("action_name = \"iv2_ga_select_national_idea_navy_2", card);
        Assert.Single(card.Split("iv2_upgrade_no.dds").Skip(1));
    }

    [Theory]
    [InlineData("gui")]
    [InlineData("levels")]
    public void Regenerating_a_card_with_fewer_levels_leaves_one_card(string change)
    {
        Generate(State("army", 1, s => s.Head.Values["guiLevels"] = "4"));
        Generate(State("army", 2));

        Generate(State("army", 1, s =>
        {
            if (change == "gui")
            {
                s.Head.Values["guiLevels"] = "2";
            }
            else
            {
                s.Head.Children["levels"].RemoveRange(2, 2);
            }
        }));

        var text = ModFile(SeeCards("army"));
        Assert.Single(text.Split("\ttype iv2_nat_idea_see_army_1 = widget {").Skip(1));
        var card = SeeCard(text, "army_1");
        Assert.Single(card.Split("action_name = \"iv2_ga_select_national_idea_army_1_").Skip(1));
        Assert.Contains("action_name = \"iv2_ga_select_national_idea_army_1_1\"", card);
        Assert.DoesNotContain("army_1_2", card);
        Assert.DoesNotContain("army_1_3", card);
        Assert.Single(card.Split("iv2_upgrade_no.dds").Skip(1));
        // Nichts von army_1 bleibt außerhalb seiner Karte stehen, army_2 folgt unverändert
        Assert.Equal(text.IndexOf("army_1", StringComparison.Ordinal), text.IndexOf(card, StringComparison.Ordinal) + card.IndexOf("army_1", StringComparison.Ordinal));
        Assert.DoesNotContain("army_1", text[(text.IndexOf(card, StringComparison.Ordinal) + card.Length)..]);
        Assert.Contains("\ttype iv2_nat_idea_see_army_2 = widget {", text);
    }

    private static string IdeaLoc(string category, string language) =>
        $"main_menu/localization/{language}/" + (category == "adm" && language == "german" ? "03_IV2_nat_adm_ideas_l_german.yml" : $"03_IV2_dyn_nat_{category}_ideas_l_{language}.yml");

    private static string Messages(string language) => $"main_menu/localization/{language}/03_IV2_messages_l_{language}.yml";

    private const string MessageTypes = "main_menu/gui/messagetypes.txt";

    /// <summary>Die 19 Schlüssel einer Idee wie army_1 der Mod: Name und Wahlen, Abwahl, Statische Modifikatoren, vor jeder Gruppe eine Leerzeile.</summary>
    private static string IdeaLocText(string id, string name, string desc)
    {
        var text = $"\n iv2_dynamic_nat_idea_{id}: \"{name}\"\n" +
                   $" iv2_ga_select_national_idea_{id}: \"$iv2_dynamic_nat_idea_{id}$ $iv2_level_1$\"\n" +
                   $" iv2_ga_select_national_idea_{id}_desc: \"{desc}\"\n";
        for (var k = 1; k <= 3; k++)
        {
            text += $" iv2_ga_select_national_idea_{id}_{k}: \"$iv2_dynamic_nat_idea_{id}$ $iv2_level_{k + 1}$\"\n" +
                    $" iv2_ga_select_national_idea_{id}_{k}_desc: \"$iv2_ga_select_national_idea_{id}_desc$\"\n";
        }

        text += $"\n iv2_ga_deselect_nat_idea_{id}: \"$iv2_remove$ $iv2_dynamic_nat_idea_{id}$\"\n" +
                $" iv2_ga_deselect_nat_idea_{id}_desc: \"$iv2_remove_desc$ $iv2_dynamic_nat_idea_{id}$ $iv2_remove_2_desc$\"\n\n";
        foreach (var s in new[] { "", "_1", "_2", "_3" })
        {
            text += $" STATIC_MODIFIER_NAME_national_idea_modifier_{id}{s}: \"$iv2_ga_select_national_idea_{id}{s}$\"\n" +
                    $" STATIC_MODIFIER_DESC_national_idea_modifier_{id}{s}: \"$iv2_ga_select_national_idea_{id}{s}_desc$\"\n";
        }

        return text;
    }

    /// <summary>Die 36 Meldungen der vier Wahlen einer Idee wie army_1 der Mod (Stufen 2-4 mit Name und Stufe).</summary>
    private static string SelectMessages(string id)
    {
        var text = string.Empty;
        foreach (var s in new[] { "", "_1", "_2", "_3" })
        {
            var p = $" WE_PERFORM_iv2_ga_select_national_idea_{id}{s}_ACTION_";
            var reference = s.Length == 0 ? $"$iv2_dynamic_nat_idea_{id}$" : $"$iv2_ga_select_national_idea_{id}{s}$";
            text += $"{p}SETUP: \"$iv2_message_selected_ig$ {reference}\"\n{p}HEADER: \"$iv2_message_selected_dynamic_nat_idea_1$\"\n" +
                    $"{p}TITLE: \"$iv2_message_selected_dynamic_nat_idea_1$\"\n{p}EFFECTS: \"$iv2_message_selected_ig$ {reference}\"\n" +
                    $"{p}LOG: \"$iv2_message_selected_ig$ {reference}\"\n{p}BTN1: \"OK\"\n{p}BTN2: \"OK\"\n{p}BTN3: \"$common_string_go_to$\"\n{p}MAP: \"\"\n";
        }

        return text;
    }

    /// <summary>Die 9 Meldungen der Abwahl einer Idee wie army_1 der Mod.</summary>
    private static string DeselectMessages(string id)
    {
        var p = $" WE_PERFORM_iv2_ga_deselect_nat_idea_{id}_ACTION_";
        var reference = $"$iv2_dynamic_nat_idea_{id}$";
        return $"{p}SETUP: \"$iv2_message_deselected_ig$ {reference}\"\n{p}HEADER: \"$iv2_message_deselected_dynamic_nat_idea_1$\"\n" +
               $"{p}TITLE: \"$iv2_message_deselected_dynamic_nat_idea_1$\"\n{p}EFFECTS: \"$iv2_message_deselected_ig$ {reference}\"\n" +
               $"{p}LOG: \"$iv2_message_deselected_ig$ {reference}\"\n{p}BTN1: \"OK\"\n{p}BTN2: \"OK\"\n{p}BTN3: \"$common_string_go_to$\"\n{p}MAP: \"\"\n";
    }

    /// <summary>Ein Block der Meldungsarten (Ort 17) wie in der Mod.</summary>
    private static string MessageType(string action) =>
        $"WE_PERFORM_{action}_ACTION={{\nlog=yes\nonmap=no\npopup=no\nidle=no\noption=yes\npausepopup=no\nsound=no\nmessage_category = economy\n}}\n";

    private static string MessageTypes5(string id) =>
        string.Concat(new[] { "", "_1", "_2", "_3" }.Select(s => MessageType($"iv2_ga_select_national_idea_{id}{s}"))) + MessageType($"iv2_ga_deselect_nat_idea_{id}");

    [Fact]
    public void German_adm_loc_goes_to_existing_file()
    {
        _temp.Write("mod/" + IdeaLoc("adm", "german"), "﻿l_german:\n \n iv2_level_1: \"Level 1\"\n \n", new System.Text.UTF8Encoding(false));

        Generate(State("adm", 1));
        Generate(State("army", 1));

        var german = ModFile(IdeaLoc("adm", "german"));
        Assert.StartsWith("l_german:\n \n iv2_level_1: \"Level 1\"\n \n", german);
        Assert.Contains(" iv2_dynamic_nat_idea_adm_1: \"Testidee 1\"\n", german);
        Assert.False(File.Exists(Path.Combine(Mod, "main_menu/localization/german/03_IV2_dyn_nat_adm_ideas_l_german.yml")));
        Assert.Contains(" iv2_dynamic_nat_idea_adm_1: \"Test Idea 1\"\n", ModFile(IdeaLoc("adm", "english")));
        Assert.EndsWith("/03_IV2_dyn_nat_adm_ideas_l_english.yml", IdeaLoc("adm", "english"));
        Assert.Contains(" iv2_dynamic_nat_idea_army_1: \"Testidee 1\"\n", ModFile("main_menu/localization/german/03_IV2_dyn_nat_army_ideas_l_german.yml"));
        // Deutsche Dateien mit adm_1: nur die Ideen-Datei mit dem alten Namen und die Meldungen
        var withAdm = Directory.EnumerateFiles(Path.Combine(Mod, "main_menu", "localization", "german"))
            .Where(f => File.ReadAllText(f).Contains("adm_1", StringComparison.Ordinal)).Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal);
        Assert.Equal(new[] { "03_IV2_messages_l_german.yml", "03_IV2_nat_adm_ideas_l_german.yml" }, withAdm);
    }

    [Fact]
    public void Loc_has_19_keys_in_three_groups()
    {
        _temp.Write("mod/" + IdeaLoc("army", "english"), "l_english:\n # army ideas\n");
        Generate(State("army", 1));
        Generate(State("army", 2, SingleLevel));
        // Neu erzeugt an Ort und Stelle: army_1 bleibt vor army_2
        Generate(State("army", 1));

        // Jede Gruppe mit einer Leerzeile davor (der Planer schreibt sie ohne das Leerzeichen der Mod); auch eine Idee mit einer Stufe hat alle 19
        Assert.Equal("l_english:\n # army ideas\n" + IdeaLocText("army_1", "Test Idea 1", string.Empty) + IdeaLocText("army_2", "Test Idea 2", string.Empty),
            ModFile(IdeaLoc("army", "english")));
        var keys = ModFile(IdeaLoc("army", "german")).Split('\n').Select(l => l.Split(':')[0].Trim()).Where(k => Regex.IsMatch(k, "army_2(?![0-9])")).ToList();
        Assert.Equal(19, keys.Count);
        Assert.Equal(19, keys.Distinct().Count());
    }

    [Fact]
    public void Message_selects_and_deselects_land_in_their_sections()
    {
        // Wie in der Mod: je Kategorie ein Abschnitt der Wahlen, danach die Abschnitte der Abwahlen; davor Meldungen einer Ideengruppe army_1
        const string group = " WE_PERFORM_iv2_ga_select_ideagroup_army_1_ACTION_SETUP: \"x\"\n WE_PERFORM_iv2_ga_ideagroup_army_1_1_ACTION_SETUP: \"y\"\n \n";
        foreach (var language in Languages)
        {
            _temp.Write("mod/" + Messages(language), $"l_{language}:\n" + group + SelectMessages("army_1") + " \n" + SelectMessages("navy_1") + " \n" +
                                                     DeselectMessages("army_1") + " \n" + DeselectMessages("navy_1"));
        }

        var groupTypes = MessageType("iv2_ga_select_ideagroup_army_1") + MessageType("iv2_ga_ideagroup_army_1_1");
        _temp.Write("mod/" + MessageTypes, groupTypes + MessageTypes5("army_1") + MessageTypes5("navy_1") + "\n# === IV2Generator:ARA:START ===\n");
        var before = Snapshot();

        // army_1 wie in der Mod: an Ort und Stelle, nichts ändert sich
        Generate(State("army", 1));
        foreach (var path in Languages.Select(Messages).Append(MessageTypes))
        {
            Assert.Equal(before[path], Snapshot()[path]);
        }

        Generate(State("army", 2));

        foreach (var language in Languages)
        {
            Assert.Equal($"l_{language}:\n" + group + SelectMessages("army_1") + SelectMessages("army_2") + " \n" + SelectMessages("navy_1") + " \n" +
                         DeselectMessages("army_1") + DeselectMessages("army_2") + " \n" + DeselectMessages("navy_1"), ModFile(Messages(language)));
        }

        Assert.Equal(groupTypes + MessageTypes5("army_1") + MessageTypes5("army_2") + MessageTypes5("navy_1") + "\n# === IV2Generator:ARA:START ===\n", ModFile(MessageTypes));
    }

    [Fact]
    public void Name_and_desc_read_back_with_german_translation()
    {
        foreach (var (category, number) in new[] { ("adm", 3), ("army", 4) })
        {
            Generate(State(category, number, s =>
            {
                s.Head.Values["name"] = "Say \"hi\"";
                s.Head.Translations["name"] = new Dictionary<string, string> { ["german"] = "Sag \"hallo\"" };
                s.Head.Values["desc"] = "Line one";
                s.Head.Translations["desc"] = new Dictionary<string, string> { ["german"] = "Zeile eins" };
            }));
        }

        Assert.Contains(" iv2_dynamic_nat_idea_adm_3: \"Sag \\\"hallo\\\"\"\n", ModFile(IdeaLoc("adm", "german")));
        foreach (var key in new[] { "adm_3", "army_4" })
        {
            var read = GeneratorStateReader.Read(_package, Mod, key, Languages)!.State;
            Assert.Equal("Say \"hi\"", read.Head.Values["name"]);
            Assert.Equal("Sag \"hallo\"", read.Head.Translations["name"]["german"]);
            Assert.Equal("Line one", read.Head.Values["desc"]);
            Assert.Equal("Zeile eins", read.Head.Translations["desc"]["german"]);
            Assert.DoesNotContain(FormValidator.Check(_package, read, new GeneratorRenderer(_package)), i => i.Severity == IssueSeverity.Error);
            AssertUnchanged(key, read);
        }
    }

    /// <summary>
    /// Typ einer Idee mit zwei Stufen in der Länderansicht (Ort 14), einmal von <c>iv2_build_country_view.py</c> erzeugt
    /// (<c>_indent(_nat_type(...), 1)</c> für army_2 mit den Modifikatoren national_idea_modifier_army_2 und _1).
    /// </summary>
    private const string Army2CountryViewType =
        "\ttype iv2_cv_nat_dyn_army_2 = widget {\n" +
        "\t\tvisible = \"[EqualTo_string(Scope.GetFlagName, 'iv2_picked_nat_idea_army_2')]\"\n" +
        "\t\tsize = { 40 40 }\n" +
        "\t\ticon = {\n" +
        "\t\t\tsize = { 40 40 }\n" +
        "\t\t\ttexture = \"[GetModifier('national_idea_modifier_army_2').GetIcon]\"\n" +
        "\t\t}\n" +
        "\t\ttext_single = {\n" +
        "\t\t\tusing = iv2_cv_level_label\n" +
        "\t\t\tvisible = \"[GetScriptedGui('iv2_sgui_dynamic_nat_army_2').IsValid(GuiScope.SetRoot(Country.MakeScope).End)]\"\n" +
        "\t\t\traw_text = \"I\"\n" +
        "\t\t}\n" +
        "\t\ttext_single = {\n" +
        "\t\t\tusing = iv2_cv_level_label\n" +
        "\t\t\tvisible = \"[GetScriptedGui('iv2_sgui_dynamic_nat_army_2_1').IsValid(GuiScope.SetRoot(Country.MakeScope).End)]\"\n" +
        "\t\t\traw_text = \"II\"\n" +
        "\t\t}\n" +
        "\t\ttooltipwidget = {\n" +
        "\t\t\tContextualTooltipType = {\n" +
        "\t\t\t\tblockoverride \"title_icon\" {\n" +
        "\t\t\t\t\ticon = {\n" +
        "\t\t\t\t\t\tusing = tooltip_title_icon_size\n" +
        "\t\t\t\t\t\ttexture = \"[GetModifier('national_idea_modifier_army_2').GetIcon]\"\n" +
        "\t\t\t\t\t}\n" +
        "\t\t\t\t}\n" +
        "\t\t\t\tblockoverride \"title_text\" {\n" +
        "\t\t\t\t\ttext = \"iv2_dynamic_nat_idea_army_2\"\n" +
        "\t\t\t\t}\n" +
        "\t\t\t\tblockoverride \"concept_link\" {\n" +
        "\t\t\t\t\ttext = \"IV2_CV_CAT_ARMY\"\n" +
        "\t\t\t\t}\n" +
        "\t\t\t\tblockoverride \"tooltip_content\" {\n" +
        "\t\t\t\t\tvbox = {\n" +
        "\t\t\t\t\t\tlayoutpolicy_horizontal = expanding\n" +
        "\t\t\t\t\t\tTooltipRowList = {\n" +
        "\t\t\t\t\t\t\tvisible = \"[GetScriptedGui('iv2_sgui_dynamic_nat_army_2').IsValid(GuiScope.SetRoot(Country.MakeScope).End)]\"\n" +
        "\t\t\t\t\t\t\ttextcontext = \"[GetModifier('national_idea_modifier_army_2').GetDesc]\"\n" +
        "\t\t\t\t\t\t}\n" +
        "\t\t\t\t\t\tTooltipRowList = {\n" +
        "\t\t\t\t\t\t\tvisible = \"[GetScriptedGui('iv2_sgui_dynamic_nat_army_2_1').IsValid(GuiScope.SetRoot(Country.MakeScope).End)]\"\n" +
        "\t\t\t\t\t\t\ttextcontext = \"[GetModifier('national_idea_modifier_army_2_1').GetDesc]\"\n" +
        "\t\t\t\t\t\t}\n" +
        "\t\t\t\t\t}\n" +
        "\t\t\t\t}\n" +
        "\t\t\t}\n" +
        "\t\t}\n" +
        "\t}\n";

    /// <summary>Der Typ einer Idee in der Länderansicht vom Kopf bis zu seiner schließenden Klammer.</summary>
    private static string CountryViewType(string text, string id)
    {
        var start = text.IndexOf($"\ttype iv2_cv_nat_dyn_{id} = widget {{\n", StringComparison.Ordinal);
        Assert.True(start >= 0, $"country view type {id} missing");
        return text[start..(text.IndexOf("\n\t}\n", start, StringComparison.Ordinal) + 4)];
    }

    [Fact]
    public void Country_view_type_has_one_label_per_level()
    {
        Generate(State("army", 1));
        var army1 = CountryViewType(ModFile(CountryView("army")), "army_1");
        // Die Datei in der Form des Skripts (_types_block: Typen durch eine Leerzeile getrennt, eine Leerzeile vor "}"), army_2 vom Skript erzeugt
        var scriptFile = "types iv2_cv_nat_dyn_army {\n" + army1 + "\n" + Army2CountryViewType + "\n}\n";
        _temp.Write("mod/" + CountryView("army"), scriptFile);
        Generate(State("army", 2, s => s.Head.Children["levels"].RemoveRange(2, 2)));
        Assert.Equal(scriptFile, ModFile(CountryView("army")));
        Generate(State("navy", 1, SingleLevel));

        // Eine neue Idee kommt wie beim Skript mit einer Leerzeile hinter die letzte ihrer Kategorie, vor die Leerzeile vor "}"
        Generate(State("army", 3, SingleLevel));
        var army = ModFile(CountryView("army"));
        var army3 = CountryViewType(army, "army_3");
        Assert.Equal("types iv2_cv_nat_dyn_army {\n" + army1 + "\n" + Army2CountryViewType + "\n" + army3 + "\n}\n", army);
        Assert.Single(Regex.Matches(army3, "text_single = \\{"));
        Assert.Equal(new[] { "I", "II", "III", "IV" }, Regex.Matches(army1, "raw_text = \"([IV]+)\"").Select(m => m.Groups[1].Value));
        Assert.Equal(4, Regex.Matches(army1, "TooltipRowList = \\{").Count);
        Assert.Contains("textcontext = \"[GetModifier('national_idea_modifier_army_1_3').GetDesc]\"", army1);
        var navy = CountryViewType(ModFile(CountryView("navy")), "navy_1");
        Assert.Single(Regex.Matches(navy, "text_single = \\{"));
        Assert.Single(Regex.Matches(navy, "TooltipRowList = \\{"));
        Assert.Contains("\t\t\t\t\ttext = \"IV2_CV_CAT_NAVY\"\n", navy);
        Assert.DoesNotContain("navy_1_1", navy);

        // Neu erzeugt bleibt die Datei zeichengleich; ein deaktivierter Stand behält seinen Typ (das Skript nimmt jede Idee mit Modifikator-Block)
        var before = Snapshot();
        Generate(State("army", 2, s => s.Head.Children["levels"].RemoveRange(2, 2)));
        Assert.Equal(before[CountryView("army")], Snapshot()[CountryView("army")]);
        Generate(State("army", 2, s =>
        {
            s.Head.Children["levels"].RemoveRange(2, 2);
            s.Head.Values["disabled"] = "yes";
        }));
        Assert.Equal(before[CountryView("army")], Snapshot()[CountryView("army")]);
        Assert.Equal(before[CountryViewShells], Snapshot()[CountryViewShells]);
    }

    [Fact]
    public void Country_view_shell_keeps_the_category_order_before_the_historic_entries()
    {
        _temp.Write("mod/" + CountryViewShells,
            "types iv2_cv_shells {\n\ttype iv2_cv_national_idea_shell = container {\n\t\tiv2_cv_nat_dyn_adm_1 = {}\n\t\tiv2_cv_nat_hist_CAS_1 = {}\n\t}\n}\n");

        Generate(State("army", 1));
        Generate(State("dip", 1));
        Generate(State("navy", 1));
        Generate(State("army", 2));
        Generate(State("army", 1));

        // Reihenfolge wie gen_national_shell im Skript: adm, dip, army, navy, dann die historischen Einträge
        Assert.Equal("types iv2_cv_shells {\n\ttype iv2_cv_national_idea_shell = container {\n\t\tiv2_cv_nat_dyn_adm_1 = {}\n\t\tiv2_cv_nat_dyn_dip_1 = {}\n" +
                     "\t\tiv2_cv_nat_dyn_army_1 = {}\n\t\tiv2_cv_nat_dyn_army_2 = {}\n\t\tiv2_cv_nat_dyn_navy_1 = {}\n\t\tiv2_cv_nat_hist_CAS_1 = {}\n\t}\n}\n",
            ModFile(CountryViewShells));
    }

    /// <summary>KI-Tag-Datei im Stil des Tag-Editors (json.dumps mit indent 1, CRLF): Vokabular, eine Gruppe, army_1 und navy_1.</summary>
    private const string TagFile =
        "{\r\n \"version\": 1,\r\n \"tags\": [\r\n  {\r\n   \"id\": \"ai_aggressive\",\r\n   \"label\": \"Aggressiv\"\r\n  }\r\n ],\r\n" +
        " \"entries\": {\r\n  \"group:adm_1\": {\r\n   \"tags\": [\r\n    \"ai_cautious\"\r\n   ],\r\n   \"name\": \"Innovationsideen\"\r\n  },\r\n" +
        "  \"nat:army_1\": {\r\n   \"tags\": [\r\n    \"ai_aggressive\",\r\n    \"ai_balanced\"\r\n   ],\r\n   \"name\": \"Testidee 1\"\r\n  },\r\n" +
        "  \"nat:navy_1\": {\r\n   \"tags\": [],\r\n   \"name\": \"Seeidee äö\"\r\n  }\r\n }\r\n}\r\n";

    /// <summary>Schreibt die KI-Tag-Datei außerhalb der Mod und merkt ihren Pfad wie das Fenster in <c>externals.json</c>; gibt die gewählten Pfade zurück.</summary>
    private IReadOnlyDictionary<string, string> ChooseTagFile(string text)
    {
        var path = _temp.Write("tageditor/iv2_ai_tags.json", text);
        ExternalPathStore.Set(_temp.PathOf("data"), "aiTags", path);
        return ExternalPathStore.Load(_temp.PathOf("data"));
    }

    private GeneratorPlan PlanWithTags(GeneratorState state, IReadOnlyDictionary<string, string> externals) =>
        GeneratorPlanner.Plan(_package, state, new GeneratorTarget(Mod, null, Languages, externals));

    [Fact]
    public void Tags_read_back_and_rewritten_unchanged()
    {
        Generate(State("army", 1));
        var externals = ChooseTagFile(TagFile);
        var path = externals["aiTags"];
        var bytes = File.ReadAllBytes(path);

        var result = GeneratorStateReader.Read(_package, Mod, "army_1", Languages, externals)!;
        Assert.Empty(result.Notes);
        Assert.Equal("ai_aggressive\nai_balanced", result.State.Head.Values["aiTags"]);
        var plan = PlanWithTags(result.State, externals);
        var file = Assert.Single(plan.Files, f => f.ExternalPath is not null);
        Assert.Equal("extern/iv2_ai_tags.json", file.RelativePath);
        Assert.Equal(PlannedFileStatus.Unchanged, file.Status);
        Assert.All(plan.Files, f => Assert.True(f.Status == PlannedFileStatus.Unchanged, $"{f.RelativePath} {FirstDifference(f.OriginalText, f.NewText)}"));
        SlicePackage.Write(plan);
        Assert.Equal(bytes, File.ReadAllBytes(path));

        // Ohne gewählte Datei bleibt das Feld leer und ein Hinweis sagt es; die Ausgabe wird mit Warnung übersprungen
        var without = GeneratorStateReader.Read(_package, Mod, "army_1", Languages)!;
        Assert.Equal(GeneratorsTexts.ReadJsonNoFile("AI tag file (iv2_ai_tags.json)"), Assert.Single(without.Notes));
        Assert.DoesNotContain(GeneratorPlanner.Plan(_package, without.State, new GeneratorTarget(Mod, null, Languages)).Files, f => f.ExternalPath is not null);
    }

    [Fact]
    public void Mod_check_reads_the_tags_from_the_chosen_tag_file()
    {
        Generate(State("army", 1));
        var externals = ChooseTagFile(TagFile);

        // "Mod prüfen → Öffnen" nimmt den Stand der Zeile: ohne die Tag-Datei wäre aiTags leer und der Plan verlöre die vorhandenen Tags
        var row = GeneratorCheck.Check(_package, Mod, "army_1", Languages, externals);
        Assert.Null(row.Error);
        Assert.Equal("ai_aggressive\nai_balanced", row.State!.Head.Values["aiTags"]);
        Assert.Equal("ai_aggressive\nai_balanced", GeneratorCheck.Run(_package, Mod, Languages, externals).Single(r => r.Key == "army_1").State!.Head.Values["aiTags"]);

        // Ein geänderter Tag ersetzt die Tags des Eintrags in der Tag-Datei
        var state = row.State;
        state.Head.Values["aiTags"] = "ai_naval";
        SlicePackage.Write(PlanWithTags(state, externals));
        Assert.Contains("  \"nat:army_1\": {\r\n   \"tags\": [\r\n    \"ai_naval\"\r\n   ],\r\n", File.ReadAllText(externals["aiTags"]));
    }

    [Fact]
    public void New_idea_appends_tag_entry()
    {
        var externals = ChooseTagFile(TagFile);
        var state = State("army", 2, s =>
        {
            s.Head.Values["aiTags"] = "ai_defensive\r\n  ai_cautious \n\n";
            s.Head.Translations["name"] = new Dictionary<string, string> { ["german"] = "Idee \"zwei\" \\ ä" };
        });

        SlicePackage.Write(PlanWithTags(state, externals));

        var expected = TagFile.Replace("   \"name\": \"Seeidee äö\"\r\n  }\r\n }",
            "   \"name\": \"Seeidee äö\"\r\n  },\r\n  \"nat:army_2\": {\r\n   \"tags\": [\r\n    \"ai_defensive\",\r\n    \"ai_cautious\"\r\n   ],\r\n" +
            "   \"name\": \"Idee \\\"zwei\\\" \\\\ ä\"\r\n  }\r\n }", StringComparison.Ordinal);
        Assert.Equal(expected, File.ReadAllText(externals["aiTags"]));
        Assert.Equal("ai_defensive\nai_cautious", GeneratorStateReader.Read(_package, Mod, "army_2", Languages, externals)!.State.Head.Values["aiTags"]);
    }

    [Theory]
    [InlineData("Testidee 3", true, "Testidee 3")]
    [InlineData(null, true, "Test Idea 3")]
    [InlineData("", false, "army_3")]
    public void Tag_entry_name_is_the_german_name_or_the_id(string? german, bool copyEnglish, string expected)
    {
        var externals = ChooseTagFile(TagFile);
        var state = State("army", 3, s =>
        {
            s.Head.Translations["name"] = german is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["german"] = german };
            s.LanguageRules = s.LanguageRules with { CopyEnglish = copyEnglish };
            s.Head.Values["aiTags"] = "ai_balanced";
        });

        SlicePackage.Write(PlanWithTags(state, externals));

        Assert.Contains($"  \"nat:army_3\": {{\r\n   \"tags\": [\r\n    \"ai_balanced\"\r\n   ],\r\n   \"name\": \"{expected}\"\r\n  }}\r\n",
            File.ReadAllText(externals["aiTags"]));
    }

    private const string NoTagsWarning = "No AI tags: the entry in the AI tag file is neither created nor changed.";

    [Fact]
    public void Empty_ai_tags_keep_the_existing_entry_and_add_none()
    {
        Generate(State("army", 1));
        var externals = ChooseTagFile(TagFile);
        var path = externals["aiTags"];
        var bytes = File.ReadAllBytes(path);

        // army_1 hat in der Datei Tags, der Stand keine (z. B. vor Task 6 gespeichert): der Eintrag bleibt, die Datei wird nicht geplant
        var existing = State("army", 1);
        Assert.Contains(FormValidator.Check(_package, existing, new GeneratorRenderer(_package)), i => i.Severity == IssueSeverity.Warning && i.Message == NoTagsWarning);
        var plan = PlanWithTags(existing, externals);
        Assert.DoesNotContain(plan.Files, f => f.ExternalPath is not null);
        SlicePackage.Write(plan);

        // Neue Idee ohne Tags (nur Leerzeilen): kein Eintrag
        var fresh = State("army", 2, s => s.Head.Values["aiTags"] = " \n\r\n ");
        Assert.Contains(FormValidator.Check(_package, fresh, new GeneratorRenderer(_package)), i => i.Message == NoTagsWarning);
        SlicePackage.Write(PlanWithTags(fresh, externals));

        Assert.Equal(bytes, File.ReadAllBytes(path));
        // Mit Tags gibt es die Warnung nicht
        Assert.DoesNotContain(FormValidator.Check(_package, State("army", 2, s => s.Head.Values["aiTags"] = "ai_balanced"), new GeneratorRenderer(_package)),
            i => i.Message == NoTagsWarning);
    }

    [Fact]
    public void After_generate_hint_is_set()
    {
        Assert.Equal("Please run iv2_build_ai_chooser.py afterwards so that the AI chooser knows the new or changed ideas.", _package.AfterGenerate);
        var german = GeneratorPackageLoader.Load(PackageFolder, _temp.PathOf("data"), shipped: true, uiLanguage: "de");
        Assert.Equal("Bitte danach iv2_build_ai_chooser.py laufen lassen, damit der KI-Auswähler die neuen oder geänderten Ideen kennt.", german.AfterGenerate);
        Assert.Equal(new ExternalDef("KI-Tag-Datei (iv2_ai_tags.json)", "Idea Variation 2 Skripte/Tag-Editor/iv2_ai_tags.json"), german.Externals["aiTags"]);
    }

    /// <summary>Ein eingelesener Stand plant jede Datei unverändert.</summary>
    private void AssertUnchanged(string key, GeneratorState state)
    {
        var plan = GeneratorPlanner.Plan(_package, state, new GeneratorTarget(Mod, null, Languages));
        Assert.All(plan.Files, f => Assert.True(f.Status == PlannedFileStatus.Unchanged, $"{key}: {f.RelativePath} {f.Status} {FirstDifference(f.OriginalText, f.NewText)}"));
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
}
