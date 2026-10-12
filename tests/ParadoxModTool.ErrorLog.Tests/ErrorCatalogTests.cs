using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorCatalogTests
{
    private const string SmallCatalog = """
        {
          "version": 1,
          "entries": [
            { "code": "A-1", "name": { "de": "Eins", "en": "One" }, "severity": "warning", "pattern": "^Alpha (?<name>\\w+)",
              "explain": { "de": "Erklärung" }, "tips": { "de": [ "Tipp" ] }, "fixes": [ "jump" ] },
            { "code": "B-1", "name": { "de": "Zwei" }, "severity": "info", "pattern": "^Beta", "defaultSuppressed": true }
          ]
        }
        """;

    [Fact]
    public void TipsFallBackToEnglishThenGerman()
    {
        var catalog = ErrorCatalog.Parse("""
            { "entries": [ { "code": "T-1", "name": { "de": "T" }, "pattern": "^T", "tips": { "de": [ "Tipp" ], "en": [ "Tip" ] } },
                           { "code": "T-2", "name": { "de": "T" }, "pattern": "^U", "tips": { "de": [ "Nur" ] } } ] }
            """);

        Assert.Equal(new[] { "Tip" }, catalog.Find("T-1")!.TipsIn("fr"));
        Assert.Equal(new[] { "Nur" }, catalog.Find("T-2")!.TipsIn("fr"));
    }

    [Fact]
    public void ParsesEntriesWithTextsSeverityAndFlags()
    {
        var catalog = ErrorCatalog.Parse(SmallCatalog);

        Assert.Empty(catalog.Problems);
        var a = catalog.Find("A-1")!;
        Assert.Equal(ErrorSeverity.Warning, a.Severity);
        Assert.Equal("One", a.NameIn("en"));
        Assert.Equal("One", a.NameIn("fr"));
        Assert.Equal("Erklärung", a.ExplainIn("en"));
        Assert.Equal(new[] { "Tipp" }, a.TipsIn("de"));
        Assert.Equal(new[] { "jump" }, a.Fixes);
        Assert.True(catalog.Find("B-1")!.DefaultSuppressed);
        Assert.Equal("A-1", catalog.Match("Alpha x", out var match)!.Code);
        Assert.Equal("x", match!.Groups["name"].Value);
        Assert.Null(catalog.Match("Gamma", out _));
    }

    [Fact]
    public void InvalidPatternSkipsOnlyThatEntry()
    {
        var catalog = ErrorCatalog.Parse("""
            { "entries": [
              { "code": "BAD", "pattern": "^(unclosed" },
              { "code": "OK", "pattern": "^fine" }
            ] }
            """);

        Assert.Null(catalog.Find("BAD"));
        Assert.NotNull(catalog.Find("OK"));
        Assert.Contains(catalog.Problems, p => p.Kind == CatalogProblemKind.InvalidPattern && p.Code == "BAD");
    }

    [Fact]
    public void InvalidJsonGivesEmptyCatalogWithProblem()
    {
        var catalog = ErrorCatalog.Parse("{ not json");

        Assert.Empty(catalog.Kinds);
        Assert.Equal(CatalogProblemKind.InvalidJson, Assert.Single(catalog.Problems).Kind);
    }

    [Fact]
    public void MissingFileGivesEmptyCatalogWithProblem()
    {
        var catalog = ErrorCatalog.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".json"));

        Assert.Empty(catalog.Kinds);
        Assert.Contains(catalog.Problems, p => p.Kind == CatalogProblemKind.FileMissing && p.Detail.Contains("does-not-exist"));
    }

    [Fact]
    public void UnknownSeverityFallsBackToErrorWithProblem()
    {
        var catalog = ErrorCatalog.Parse("""{ "entries": [ { "code": "X", "severity": "fatal", "pattern": "^x" } ] }""");

        Assert.Equal(ErrorSeverity.Error, catalog.Find("X")!.Severity);
        var problem = Assert.Single(catalog.Problems);
        Assert.Equal(CatalogProblemKind.UnknownSeverity, problem.Kind);
        Assert.Equal("X", problem.Code);
    }

    [Fact]
    public void PathForCombinesProfilesDirectoryAndRule()
    {
        Assert.Equal(Path.Combine(@"C:\p", "errorlog/eu5.json"), ErrorCatalog.PathFor(@"C:\p", "errorlog/eu5.json"));
        Assert.Null(ErrorCatalog.PathFor(@"C:\p", null));
        Assert.Null(ErrorCatalog.PathFor(@"C:\p", " "));
    }

    [Fact]
    public void ShippedCatalogLoadsWithoutProblemsWithUniqueCodesAndBothLanguages()
    {
        var catalog = Catalogs.Eu5;

        Assert.Empty(catalog.Problems);
        Assert.True(catalog.Kinds.Count >= 28);
        Assert.Equal(catalog.Kinds.Count, catalog.Kinds.Select(k => k.Code).Distinct().Count());
        Assert.All(catalog.Kinds, k =>
        {
            Assert.True(k.Names.ContainsKey("de") && k.Names.ContainsKey("en"), k.Code);
            Assert.True(k.Explain.ContainsKey("de") && k.Explain.ContainsKey("en"), k.Code);
        });
    }

    [Fact]
    public void Eu5CatalogMarksVariableAndFlagKindsForTheSaveCheck()
    {
        var catalog = Catalogs.Eu5;

        var set = catalog.Find("VAR-001")!;
        Assert.Equal("variable", set.SaveNames);
        Assert.Equal(new[] { "set_variable" }, set.SetEffects);
        Assert.Contains("saveCheck", set.Fixes);
        Assert.Contains("commentOutSet", set.Fixes);
        Assert.Equal("variable", catalog.Find("VAR-002")!.SaveNames);
        Assert.Contains("saveCheck", catalog.Find("VAR-002")!.Fixes);
        Assert.Equal("flag", catalog.Find("FLG-001")!.SaveNames);
        Assert.Equal("flag", catalog.Find("FLG-002")!.SaveNames);
        Assert.Empty(catalog.Find("FLG-001")!.SetEffects);
        var other = catalog.Find("EFF-001")!;
        Assert.Null(other.SaveNames);
        Assert.Empty(other.SetEffects);
    }

    [Theory]
    [InlineData("[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/01_iv2_scripted_effect_log_purger.txt:680", "EFF-001", "cmf_suppress", "common/scripted_effects/01_iv2_scripted_effect_log_purger.txt", 680)]
    [InlineData("[16:58:57][jomini_effect.cpp:557]: Unknown effect cmf_suppress at common/scripted_effects/01_iv2_scripted_effect_log_purger.txt:1871", "EFF-001", "cmf_suppress", "common/scripted_effects/01_iv2_scripted_effect_log_purger.txt", 1871)]
    [InlineData("[10:00:00][jomini_trigger.cpp:1]: Unknown trigger my_trigger at common/scripted_triggers/a.txt:3", "TRG-001", "my_trigger", "common/scripted_triggers/a.txt", 3)]
    [InlineData("[16:59:05][jomini_effect.cpp:1160]: Variable 'iv2_var_nat_adm_179' is set but is never used. Note that use in localization doesn't count due to technical limitations. Use in unused scripted triggers and effects also does not count. Check for typos.", "VAR-001", "iv2_var_nat_adm_179", null, 0)]
    [InlineData("[19:49:53][jomini_effect.cpp:1162]: Variable 'iv2_nat_adm_87' is set but is never used. Note that use in localization doesn't count due to technical limitations. Use in unused scripted triggers and effects also does not count", "VAR-001", "iv2_nat_adm_87", null, 0)]
    [InlineData("[10:00:00][jomini_effect.cpp:1160]: Variable 'iv2_x' is used but is never set. Setting it in an unused scripted trigger or effect does not count.", "VAR-002", "iv2_x", null, 0)]
    [InlineData("[16:59:01][jomini_effect.cpp:1160]: Flag 'iv2_nat_army_9' is set but is never used. Note that use in localization doesn't count due to technical limitations.", "FLG-001", "iv2_nat_army_9", null, 0)]
    [InlineData("[10:00:00][jomini_effect.cpp:1160]: Flag 'iv2_f' is used but is never set. Setting it in an unused scripted trigger or effect does not count.", "FLG-002", "iv2_f", null, 0)]
    [InlineData("[17:02:13][jomini_scriptvalue.h:906]: Cannot read [] as a script value at common/static_modifiers/01_IV2_ideagroup_modifiers_adm.txt:2341", "SVL-001", null, "common/static_modifiers/01_IV2_ideagroup_modifiers_adm.txt", 2341)]
    [InlineData("[17:02:13][jomini_scriptvalue.h:540]: Badly read script value  at common/static_modifiers/01_IV2_ideagroup_modifiers_adm.txt:2341", "SVL-002", null, "common/static_modifiers/01_IV2_ideagroup_modifiers_adm.txt", 2341)]
    [InlineData("[19:49:17][pdx_persistent_reader.cpp:289]: Error: \"Failed to read enum! Type: \"EAutomatedSystems\", Val \"governmentreform\": governmentreform, near line: 19\" in file: \"common/generic_actions/iv2_GA_ideas_ARA.txt\" near line: 19", "PAR-001", null, "common/generic_actions/iv2_GA_ideas_ARA.txt", 19)]
    [InlineData("[16:59:47][pdx_gui_factory.cpp:2296]: gui/IV2_first_templates.gui:36 - untyped is not a valid type", "GUI-001", "untyped", "gui/IV2_first_templates.gui", 36)]
    [InlineData("[16:58:57][pdx_gui_factory.cpp:595]: gui/IV2_first_templates.gui:36 - 'use_global_input_instance' is not a valid widget/type/property", "GUI-001", "use_global_input_instance", "gui/IV2_first_templates.gui", 36)]
    [InlineData("[19:49:48][pdx_gui_validation.cpp:40]: gui/panels/left_panel/left_panel.gui:643 - Not enough widget entries: Property widgets must specify exactly one widget", "GUI-004", null, "gui/panels/left_panel/left_panel.gui", 643)]
    [InlineData("[19:49:44][pdx_gui_texture.cpp:61]: gui/Idea Variation 2 National Ideas/Historic National Ideas/BRA/IV2_historic_see_BRA.gui:32 - Missing file 'gfx/interface/coa/BRA.dds' for texture property", "GUI-005", "gfx/interface/coa/BRA.dds", "gui/Idea Variation 2 National Ideas/Historic National Ideas/BRA/IV2_historic_see_BRA.gui", 32)]
    [InlineData("[16:59:01][tooltip_validation.cpp:246]: Button is missing tooltip! Location: \"gui/alertmanager.gui:507\"", "GUI-006", null, "gui/alertmanager.gui", 507)]
    [InlineData("[10:00:00][pdx_gui_factory.cpp:1]: gui/a.gui:3 - something new", "GUI-000", null, "gui/a.gui", 3)]
    [InlineData("[13:41:32][pdx_gui_validation.cpp:62]: Error at gui/glorpUI_country_header.gui:51 (property origin is at gui/glorpUI_country_header.gui:50) - Unsupported property: position on widgets that are a child of a hbox/vbox. Hboxes/vboxes arrange their children automatically.", "GUI-007", null, "gui/glorpUI_country_header.gui", 51)]
    [InlineData("[16:58:56][virtualfilesystem.cpp:634]: Could not find texture due to 'VFSOpen Error: gfx/interface/coa/BRA.dds not found'. (trying to open file \"gfx/interface/coa/BRA.dds\")", "TEX-001", "gfx/interface/coa/BRA.dds", null, 0)]
    [InlineData("[19:49:46][message_handler.cpp:421]: Failed to find message type: WE_PERFORM_appeal_to_circle_diet_ACTION.", "MSG-001", "WE_PERFORM_appeal_to_circle_diet_ACTION", null, 0)]
    [InlineData("[19:49:03][localization_util.cpp:103]: WE_PERFORM_surrender_civil_war_ACTION_MAP: \"TODO : Write a desc\"", "LOC-001", "WE_PERFORM_surrender_civil_war_ACTION_MAP", null, 0)]
    [InlineData("[19:49:03][pdx_localize.cpp:187]: Localization key hash collision. Key 'TWS_THE' and 'WE_PERFORM_iv2_ga_select_national_idea_army_30_3_ACTION_TITLE' have the same hash: 243924145. ", "LOC-002", "TWS_THE", null, 0)]
    [InlineData("[16:59:33][government.cpp:3751]: Removing invalid bureaucracy 'themata_bureaucracy' for 'TRE Trebizond' 40 at game start - might want to look into this", "GOV-001", "themata_bureaucracy", null, 0)]
    [InlineData("[16:59:33][government.cpp:3792]: Removing invalid reform 'katepanata' for 'BYZ Byzantium' 203 at game start - might want to look into this", "GOV-001", "katepanata", null, 0)]
    [InlineData("[15:17:14][government.cpp:3720]: Removing invalid law 'bayaa_succession_law' for 'MOR Morocco' 1583 at game start - might want to look into this", "GOV-001", "bayaa_succession_law", null, 0)]
    [InlineData("[15:19:50][pdx_data_factory.cpp:1426]: Could not find data system function 'GetName' in 'ROOT.GetName'.", "LOC-003", "GetName", null, 0)]
    [InlineData("[15:19:50][pdx_data_factory.cpp:1044]: Failed converting statement for 'ROOT.GetName'", "LOC-004", "ROOT.GetName", null, 0)]
    [InlineData("[15:18:10][pdx_data_callstack.cpp:67]: Promote 'ROOT' returned nullptr, in 'ROOT.GetCountry.GetCapital.GetName'", "LOC-004", "ROOT.GetCountry.GetCapital.GetName", null, 0)]
    [InlineData("[15:18:10][pdx_data_localize_helper.cpp:315]: FetchData failed for 'ROOT.GetCountry.GetCapital.GetName'", "LOC-004", "ROOT.GetCountry.GetCapital.GetName", null, 0)]
    [InlineData("[15:19:50][pdx_data_localize.cpp:192]: Data error in loc string 'pmt_probe_loc'", "LOC-005", "pmt_probe_loc", null, 0)]
    [InlineData("[15:09:56][jomini_effect.cpp:1166]: Event target 'cm_location' is used but is never set. Setting it in an unused scripted trigger or effect does not count", "EVT-001", "cm_location", null, 0)]
    [InlineData("[15:09:06][tooltip_validation.cpp:227]: Tooltips should use lowpriotextcontext instead of textcontext! Location: \"gui/vanilla/cmfg_location_tooltips_vanilla_types.gui:3225\"", "GUI-008", null, "gui/vanilla/cmfg_location_tooltips_vanilla_types.gui", 3225)]
    [InlineData("[16:59:33][building_type.cpp:2521]: iv2_building_1_0 has a too high efficiency for sailors, as its 0.02 when 0.01875 is allowed", "BLD-001", "iv2_building_1_0", null, 0)]
    [InlineData("[19:49:44][price_database.cpp:117]: Missing modifier type for price. iv2_bc_res_upkeep_cost_modifier", "PRC-001", "iv2_bc_res_upkeep_cost_modifier", null, 0)]
    [InlineData("[17:00:05][game_gui_types.cpp:1540]: couldn't convert parameter value to something we recognise", "PRM-001", null, null, 0)]
    [InlineData("[19:48:50][dlc_reloadable.cpp:363]: Mod The Idea Variation 2 Testversion Beta (Idea_Variation_2_test_beta) version 1.4.* does not match game version 1.3.11.", "MOD-001", null, null, 0)]
    [InlineData("[16:59:01][database_object_reflection.h:694]: Optimization warning: Database static size differs from the actual number of elements, might be wasteful or cause slowdowns! Type: class CBuildingType, Num: 519, Expected: 525", "OPT-001", null, null, 0)]
    [InlineData("[15:21:43][pdx_mod_metadata.cpp:218]: Mod metadata read error. File: C:/Users/flori/Documents/Paradox Interactive/Europa Universalis V/mod/Test/.metadata/metadata.json Error: Failed to open file for reading: C:/Users/flori/Documents/Paradox Interactive/Europa Universalis V/mod/Test/.metadata/metadata.json", "MOD-002", "Test", null, 0)]
    [InlineData("[10:00:00][pdx_mod_metadata.cpp:218]: Mod metadata read error. File: C:/Program Files (x86)/Steam/steamapps/workshop/content/3450310/3601234567/.metadata/metadata.json Error: Unexpected token at line 4", "MOD-002", "3601234567", null, 0)]
    [InlineData("[17:00:52][pdx_arena_allocator.cpp:213]: Arena AudioArena size is too small. Please increase.", "MEM-001", null, null, 0)]
    [InlineData("[17:01:29][pdxinput_context.cpp:2896]: Could not push the provided stack context. ID: 0", "INP-001", null, null, 0)]
    [InlineData("[10:00:00][audio.cpp:1]: [Wwise] Event 'construction_building_generic_1_finished' not found", "AUD-001", "construction_building_generic_1_finished", null, 0)]
    [InlineData("[10:00:00][audio.cpp:1]: [Wwise] Failed to create event construction_building_generic_1_finished, didn't", "AUD-001", "construction_building_generic_1_finished", null, 0)]
    [InlineData("[18:21:52][event_target_links.cpp:2954]: government_type link: real country CMF has invalid government type, date 1337.4.1", "GOV-002", "CMF", null, 0)]
    [InlineData("[18:17:50][building_type.cpp:1114]: duplicated production method name 'iv2_building_1_0_maintenance' for iv2_building_1_0.  Use a unique one", "BLD-002", "iv2_building_1_0", null, 0)]
    [InlineData("[18:17:50][building_type.cpp:1114]: duplicated production method name 'stone_bricks' for mason.  Use a unique one", "BLD-002", "mason", null, 0)]
    [InlineData("[18:12:40][localization_util.cpp:103]: MODIFIER_TYPE_NAME_iv2_bc_res_upkeep_cost_modifier: \"Iv2 bc res upkeep cost modifier\"", "LOC-006", "iv2_bc_res_upkeep_cost_modifier", null, 0)]
    [InlineData("[18:12:40][localization_util.cpp:103]: MODIFIER_TYPE_DESC_iv2_bc_res_upkeep_cost_modifier: \"TODO : Write a desc\"", "LOC-006", "iv2_bc_res_upkeep_cost_modifier", null, 0)]
    [InlineData("[18:05:55][modifier_type.cpp:1314]: Missing Icon for Modifier : iv2_research_base_cost_cost_modifier", "MDF-001", "iv2_research_base_cost_cost_modifier", null, 0)]
    [InlineData("[18:22:21][pdx_gui_widget.cpp:3891]: Widget at gui/cmm/cmm_keybind_handler.gui:6 wants to set input_action 'cmm_open_menu', but the action is not present in any of the loaded profiles nor was created by code", "INP-002", "cmm_open_menu", "gui/cmm/cmm_keybind_handler.gui", 6)]
    [InlineData("[18:13:08][dlc_reloadable.cpp:1531]: incorrect checksum for DLC", "DLC-002", null, null, 0)]
    [InlineData("[18:05:56][jomini_scriptvalue.h:540]: Badly read script value 0.046875 at common/building_types/01_IV2_idea_buildings.txt:120", "SVL-002", null, "common/building_types/01_IV2_idea_buildings.txt", 120)]
    [InlineData("[18:06:02][jomini_scriptvalue.cpp:1659]: Value of wrong type in 'common/auto_modifiers/IVN_researcher_auto_modifiers.txt:12'. Got value of type 'none'", "SVL-003", null, "common/auto_modifiers/IVN_researcher_auto_modifiers.txt", 12)]
    [InlineData("[18:21:32][pdx_gui_localize.cpp:182]: Unlocalized text 'FOREIGN_COUNTRY_LIST_EMPTY' at gui/vanilla/cmf_foreign_country_lateralview_vanilla_types.gui:109, either localize it or use the raw_text property instead of text", "LOC-007", "FOREIGN_COUNTRY_LIST_EMPTY", "gui/vanilla/cmf_foreign_country_lateralview_vanilla_types.gui", 109)]
    [InlineData("[18:21:32][pdx_data_factory.cpp:1367]: Could not find promote for 'GetUIAction' in 'PrivilegeItem.GetUIAction'.", "LOC-003", "GetUIAction", null, 0)]
    [InlineData("[18:21:32][pdx_data_factory.cpp:1065]: Failed to convert statement for argument '0' for call 'GreaterThan_CFixedPoint' in 'GreaterThan_CFixedPoint(Market.GetAvailableManualTradeCapacity(Player.Self),'(CFixedPoint)1.0')'", "LOC-004", "GreaterThan_CFixedPoint(Market.GetAvailableManualTradeCapacity(Player.Self),'(CFixedPoint)1.0')", null, 0)]
    [InlineData("[18:21:32][pdx_data_factory.cpp:1065]: Failed to convert statement for argument '1' for call 'Or' in 'Or(Market.HasStarvingProvinces, Market.WillRunOutOfFoodStochpile)'", "LOC-004", "Or(Market.HasStarvingProvinces, Market.WillRunOutOfFoodStochpile)", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:504]: Country 'CMF CMF' has no government type.", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:507]: Country 'CMF CMF' has no heir-selection.", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:526]: Country 'CMF CMF' has a heir-selection that does not match its government type..", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:534]: Country 'CMF CMF' has not capital set.", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:537]: Country 'CMF CMF' does not know its capital, need a discover_areas =  or discovered_regions = .", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:1841]: 'CMF' has no marriage_law defined!", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:1859]: 'CMF' has no heir_religion_law defined!", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:178]: The country 'CMF CMF' has no society values scripted.", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:50][initialize_from_bookmark.cpp:2002]: Country CMF has no parliament_type scripted.", "SET-001", "CMF", null, 0)]
    [InlineData("[18:21:52][message_popup_handler.cpp:250]: Could not find audio event 'ADD_BUREAUCRACY'.", "AUD-001", "ADD_BUREAUCRACY", null, 0)]
    [InlineData("[18:21:52][jomini_dynamicdescription.cpp:67]: Unrecognized loc key pmt_ns.1.from_probe_a. events/pmt_ns_a.txt:6", "LOC-008", "pmt_ns.1.from_probe_a", "events/pmt_ns_a.txt", 6)]
    [InlineData("[18:21:52][jomini_dynamicdescription.cpp:67]: Unrecognized loc key pmt_ns.1.ok. CEventOptionDesc:172", "LOC-008", "pmt_ns.1.ok", null, 0)]
    [InlineData("[18:13:08][dlc_reloadable.cpp:301]: Found sentinel file from a previous boot, game was shut down unexpectedly before InGame stage. This could be due to mods, disabling mods this startup to make sure in-game mod manager is available.", "MOD-003", null, null, 0)]
    [InlineData("[18:05:56][database_modifier.cpp:89]: Modifier type 'institution_growth' is only allowed in (country) but only (location) is accepted here. At: common/building_types/unique_buildings.txt:5706", "MDF-002", "institution_growth", "common/building_types/unique_buildings.txt", 5706)]
    [InlineData("[21:41:43][static_modifier.cpp:537]: Modifier 'ara_honorable_ruler' was not used by the script or code but exists in the database, this is a waste", "MDF-003", "ara_honorable_ruler", null, 0)]
    [InlineData("[21:41:43][localization_util.cpp:103]: STATIC_MODIFIER_NAME_cas_adelantado_crown_estate_modifier: \"STATIC MODIFIER NAME cas adelantado crown estate modifier\"", "LOC-006", "cas_adelantado_crown_estate_modifier", null, 0)]
    [InlineData("[20:30:24][pdx_data_callstack.cpp:16]: No context supplied (Use SetDataContext), wanted context of type 'UIActionProvider' for 'UIActionProvider.IsVisible'", "GUI-009", "UIActionProvider", null, 0)]
    [InlineData("[20:30:24][pdx_gui_data_manager.cpp:362]: FetchData failed for 'UIActionProvider.IsVisible' - gui/shared/main_menu_buttons.gui:1802", "LOC-004", "UIActionProvider.IsVisible", null, 0)]
    [InlineData("[21:51:45][jomini_trigger.cpp:275]: PostValidate of trigger 'has_dlc' returned false at common/estate_privileges/burghers_estate.txt:1096", "TRG-002", "has_dlc", "common/estate_privileges/burghers_estate.txt", 1096)]
    [InlineData("[21:51:48][jomini_effect.cpp:141]: PostValidate of effect 'add_location_modifier' returned false at events/DHE/flavor_CAS.txt:9671", "TRG-002", "add_location_modifier", "events/DHE/flavor_CAS.txt", 9671)]
    [InlineData("[21:53:51][jomini_eventtarget.h:314]: Failed to find 'battle_for_the_strait' from database for link 'situation' at common/subject_types/tributary.txt:66", "REF-001", "battle_for_the_strait", "common/subject_types/tributary.txt", 66)]
    [InlineData("[21:51:48][jomini_eventtarget.h:314]: Failed to find 'agraw_federation_reform' from database for link 'government_reform' at common/government_reforms/country_specific.txt:4161", "REF-001", "agraw_federation_reform", "common/government_reforms/country_specific.txt", 4161)]
    [InlineData("[21:51:48][persistence_tests.cpp:1068]: Effect localization string missing: change_granadan_sovereignty_effect_tt", "LOC-009", "change_granadan_sovereignty_effect_tt", null, 0)]
    [InlineData("[21:41:12][dlc_setup_reader.h:18]: Invalid DLC specified! DLC: \"d009_across_the_pillars\" for has_dlc in setup data", "DLC-003", "d009_across_the_pillars", null, 0)]
    [InlineData("[20:37:37][localization_util.cpp:103]: iv2_ga_select_historic_SCO_13_1: \"Iv2 ga select historic SCO 13 1\"", "LOC-001", "iv2_ga_select_historic_SCO_13_1", null, 0)]
    [InlineData("[20:37:37][localization_util.cpp:103]: iv2_ga_select_historic_SCO_13_desc: \"Iv2 ga select historic SCO 13 desc\"", "LOC-001", "iv2_ga_select_historic_SCO_13_desc", null, 0)]
    [InlineData("[20:37:35][generic_action_ai_list.cpp:90]: Action iv2_ga_select_historic_SCO_13 is not explicitly listed in an ai list! This has performance considerations!", "GAI-001", "iv2_ga_select_historic_SCO_13", null, 0)]
    [InlineData("[20:37:35][generic_action_ai_list.cpp:24]: Action iv2_ga_select_historic_SCO_3 does not exist in common/generic_actions! common/generic_action_ai_lists/03_iv2_gal_nat_idea_SCO.txt:73", "GAI-002", "iv2_ga_select_historic_SCO_3", "common/generic_action_ai_lists/03_iv2_gal_nat_idea_SCO.txt", 73)]
    [InlineData("[21:51:45][unit_illustration_database.cpp:131]: Unit illustration mask 'gfx/interface/illustrations/units/masks/army_cavalry_2_heavy_tag_east_mediterranean_gfx.dds' has no matching illustration 'gfx/interface/illustrations/units/army_cavalry_2_heavy_tag_east_mediterranean_gfx.dds' - was the illustration renamed without renaming its mask?", "GFX-001", "gfx/interface/illustrations/units/masks/army_cavalry_2_heavy_tag_east_mediterranean_gfx.dds", null, 0)]
    [InlineData("[21:51:37][dnamodifier.cpp:342]: could not find template [castilian_mustache]. gfx/portraits/portrait_modifiers/zz_beard_pool_prototype.txt:79", "GFX-002", "castilian_mustache", "gfx/portraits/portrait_modifiers/zz_beard_pool_prototype.txt", 79)]
    [InlineData("[21:51:49][virtualfilesystem.cpp:634]: Could not find texture due to 'VFSOpen Error: C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/dlc/d015_ancient_monuments_pack/thumbnail.dds filename is illegal or insecure'. (trying to open file \"C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/dlc/d015_ancient_monuments_pack/thumbnail.dds\")", "TEX-002", "C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/dlc/d015_ancient_monuments_pack/thumbnail.dds", null, 0)]
    [InlineData("[21:51:49][pdx_gui_glow.cpp:322]: Failed to load texture data from 'C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/dlc/d015_ancient_monuments_pack/thumbnail.dds'", "TEX-003", "C:/Program Files (x86)/Steam/steamapps/common/Europa Universalis V/game/dlc/d015_ancient_monuments_pack/thumbnail.dds", null, 0)]
    public void ShippedCatalogRecognisesRealLines(string line, string code, string? name, string? file, int fileLine)
    {
        var entry = Assert.Single(Catalogs.Classify(line));

        Assert.Equal(code, entry.Code);
        Assert.Equal(name, entry.Name);
        Assert.Equal(file, entry.FilePath);
        Assert.Equal(fileLine == 0 ? null : fileLine, entry.FileLine);
    }

    [Fact]
    public void MultiLineReadErrorWithoutFileIsASaveReadError()
    {
        // Beim Laden eines Spielstands meldet das Spiel mehrere Zeilen in einem Error: "..." und eine leere Datei; eine Mod-Datei nennt immer ihren Pfad.
        var entry = Assert.Single(Catalogs.Classify(
            "[15:09:56][pdx_persistent_reader.cpp:289]: Error: \"Failed to read key reference: iv2_rule_national_ideas_1: iv2_rule_national_ideas_1, near line: 3",
            "Failed to read key reference: national_idea_limit_2: national_idea_limit_2, near line: 3",
            "Failed to read key reference: iv2_rule_exploration_ideas_1: iv2_rule_exploration_ideas_1, near line: 3\" in file: \"\" near line: 3"));

        Assert.Equal("SAV-002", entry.Code);
        Assert.StartsWith("Failed to read key reference: iv2_rule_national_ideas_1", entry.Detail);
        Assert.Null(entry.FilePath);
        Assert.Null(entry.FileLine);
    }

    [Fact]
    public void ReadErrorCutOffBeforeItsFileLineIsAnIncompleteParsingError()
    {
        // Fehlt die Schlusszeile mit in file: "…" (abgeschnitten oder durch eine andere Meldung getrennt), bleibt nur der Anfang.
        var entry = Assert.Single(Catalogs.Classify(
            "[15:31:17][pdx_persistent_reader.cpp:289]: Error: \"Failed to read key reference: iv2_rule_national_ideas_1: iv2_rule_national_ideas_1, near line: 3",
            "Failed to read key reference: national_idea_limit_2: national_idea_limit_2, near line: 3"));

        Assert.Equal("PAR-002", entry.Code);
        Assert.StartsWith("Failed to read key reference: iv2_rule_national_ideas_1", entry.Detail);
        Assert.Null(entry.FilePath);
    }

    [Fact]
    public void LoadSaveErrorsWithTheirCountLineAreOneSaveEntry()
    {
        var entry = Assert.Single(Catalogs.Classify(
            "[17:00:23][gamestate.cpp:133]: Load Save Errors Detected! Amount: 11, File: \"\", Errors: ",
            "11x Failed to read key reference: \"\""));

        Assert.Equal("SAV-001", entry.Code);
        Assert.Null(entry.Name);
        Assert.Null(entry.FilePath);
        Assert.Equal("11x Failed to read key reference: \"\"", entry.Detail);
    }

    [Fact]
    public void EngineNoiseIsHiddenByDefault()
    {
        Assert.True(Catalogs.Eu5.Find("MEM-001")!.DefaultSuppressed);
        Assert.True(Catalogs.Eu5.Find("INP-001")!.DefaultSuppressed);
    }

    [Fact]
    public void ScriptErrorWithCallChainKeepsTheFirstLocation()
    {
        var entry = Assert.Single(Catalogs.Classify(
            "[15:12:41][jomini_script_system.cpp:252]: Script system error!",
            "  Error: Event target link 'var' returned an unset scope",
            "  Script location: common/scripted_triggers/war_triggers.txt:33",
            "common/peace_treaties/scaligeri_peace_treaties.txt:109",
            "common/scripted_triggers/war_triggers.txt:126"));

        Assert.Equal("RUN-001", entry.Code);
        Assert.Equal("common/scripted_triggers/war_triggers.txt", entry.FilePath);
        Assert.Equal(33, entry.FileLine);
    }

    [Fact]
    public void DuplicatePropertyAndFailedReadingMergeButOtherGuiLinesDoNot()
    {
        var entries = Catalogs.Classify(
            "[12:43:18][pdx_gui_widget_desc.h:18]: gui/a.gui:1 - Duplicate property 'enabled'(773)",
            "[12:43:18][pdx_gui_factory.cpp:290]: failed reading property, at gui/a.gui:1",
            "[12:43:18][pdx_gui_factory.cpp:290]: failed reading property, at gui/a.gui:2");

        Assert.Equal(2, entries.Count);
        Assert.Equal(new[] { 1, 2 }, entries.Select(e => e.FileLine!.Value));
    }

    [Fact]
    public void UnknownLineGetsUnmatchedCodeAndKeepsItsText()
    {
        var entry = Assert.Single(Catalogs.Classify("[10:00:00][x.cpp:1]: Error building from string ( TYPE = NCoat )"));

        Assert.Null(entry.Kind);
        Assert.Equal(ErrorEntry.UnmatchedCode, entry.Code);
        Assert.Equal(ErrorSeverity.Info, entry.Severity);
        Assert.StartsWith("Error building from string", entry.Message);
    }

    [Fact]
    public void PossibleMatchWithoutPreviousEntryStaysAnEntry()
    {
        var entry = Assert.Single(Catalogs.Classify("[10:00:00][jomini_effect.cpp:1207]:  Possible match: 'a'"));

        Assert.Equal("SUG-000", entry.Code);
    }

    [Fact]
    public void TraceRulesAndGuardsAreRead()
    {
        var catalog = ErrorCatalog.Parse("""
        { "version": 1,
          "trace": { "effectBlocks": ["effect", "every_*"], "triggerBlocks": ["limit", "any_*"], "effectFolders": ["common/scripted_effects"],
                     "ifChain": ["if", "else_if", "else"], "debugLogSource": "jomini_effect_impl.cpp" },
          "entries": [ { "code": "RUN-001", "pattern": "^Script system error!", "fixes": ["traceScope"],
                         "traceGuards": [ { "detail": "^Undefined event target '(?<name>[^']+)'", "limit": "NOT = { exists = scope:{name} }" } ] } ] }
        """);

        Assert.True(catalog.Trace.OpensEffect("every_country"));
        Assert.True(catalog.Trace.OpensTrigger("any_owned_location"));
        Assert.False(catalog.Trace.OpensEffect("capital"));
        var guard = Assert.Single(catalog.Find("RUN-001")!.TraceGuards);
        Assert.Equal("NOT = { exists = scope:{name} }", guard.Limit);
        Assert.Equal("jomini_effect_impl.cpp", catalog.Trace.DebugLogSource);
        Assert.Null(catalog.Trace.DebugScopeSource);
        Assert.Empty(catalog.Problems);
    }

    [Fact]
    public void TraceScopeSourceIsRead()
    {
        var catalog = ErrorCatalog.Parse("""
        { "version": 1, "trace": { "debugLogSource": "jomini_effect_impl.cpp", "debugScopeSource": "effectimpl.cpp" }, "entries": [] }
        """);

        Assert.Equal("effectimpl.cpp", catalog.Trace.DebugScopeSource);
    }

    [Fact]
    public void AnInvalidGuardPatternIsAProblemAndIsDropped()
    {
        var catalog = ErrorCatalog.Parse("""
        { "version": 1, "entries": [ { "code": "RUN-001", "pattern": "^x", "traceGuards": [ { "detail": "(", "limit": "y" } ] } ] }
        """);

        Assert.Empty(catalog.Find("RUN-001")!.TraceGuards);
        Assert.Contains(catalog.Problems, p => p.Kind == CatalogProblemKind.InvalidPattern && p.Code == "RUN-001");
        Assert.Same(TraceRules.Empty, catalog.Trace);
    }

    [Fact]
    public void TheRealCatalogHasBothMeasuredGuardsForRuntimeErrors()
    {
        var run = Catalogs.Eu5.Find("RUN-001")!;
        Assert.Contains("traceScope", run.Fixes);
        Assert.Equal(2, run.TraceGuards.Count);
        Assert.Contains(run.TraceGuards, g => g.Detail.IsMatch("Event target link 'scope:x' returned an unset scope"));
        Assert.True(Catalogs.Eu5.Trace.OpensTrigger("limit"));
        Assert.True(Catalogs.Eu5.Trace.OpensEffect("on_monthly"));
        Assert.Empty(Catalogs.Eu5.Problems);
    }
}
