using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>
/// Der Fehlerkatalog von Crusader Kings III gegen die echten Zeilen des Probelaufs "PMT Error Probe" (error.log vom 2026-09-30, Spielversion 1.19.0.6; mit 1.20.0.2 wiederholt, gleiche Meldungstexte,
/// gespeichert unter Data/ck3).
/// </summary>
public class Ck3CatalogTests
{
    private static readonly Lazy<ErrorCatalog> Catalog = new(() =>
        ErrorCatalog.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "errorlog", "ck3.json")));

    private static List<ErrorEntry> Classify(params string[] lines) => ErrorClassifier.Classify(LogReader.Parse(lines), Catalog.Value);

    [Fact]
    public void Catalog_loads_without_problems_with_unique_codes_and_both_languages()
    {
        var catalog = Catalog.Value;

        Assert.Empty(catalog.Problems);
        Assert.Equal(catalog.Kinds.Count, catalog.Kinds.Select(k => k.Code).Distinct().Count());
        Assert.All(catalog.Kinds, k =>
        {
            Assert.True(k.Names.ContainsKey("de") && k.Names.ContainsKey("en"), k.Code);
            Assert.True(k.Explain.ContainsKey("de") && k.Explain.ContainsKey("en"), k.Code);
        });
        Assert.Equal("jomini_effect_impl.cpp", catalog.Trace.DebugLogSource);
        Assert.Equal("effectimpl.cpp", catalog.Trace.DebugScopeSource);
    }

    [Theory]
    [InlineData("[11:21:13][E][dlc_descriptor.cpp:70]: Invalid supported_version in file: mod/Crusader Wars.mod line: 7", "MOD-001", "mod/Crusader Wars.mod", null, 0)]
    [InlineData("[11:21:25][E][pdx_localize.cpp:279]: Duplicate localization key. Key 'pmt_probe.2.a' is defined in both 'localization/english/pmt_probe_l_english.yml' and 'localization/english/pmt_probe_l_english.yml'.", "LOC-006", "pmt_probe.2.a", "localization/english/pmt_probe_l_english.yml", 0)]
    [InlineData("[11:21:33][E][pdx_gui_file_desc.h:43]: gui/pmt_probe.gui:5 - Duplicate property 'size'(47)", "GUI-002", "size", "gui/pmt_probe.gui", 5)]
    [InlineData("[11:21:33][E][pdx_gui_factory.cpp:319]: failed reading property, at line 5 in file gui/pmt_probe.gui", "GUI-003", null, "gui/pmt_probe.gui", 5)]
    [InlineData("[11:21:45][E][pdx_gui_factory.cpp:715]: gui/pmt_probe.gui:5 - 'pmt_probe_invalid_property' is not a valid widget/type/property", "GUI-001", "pmt_probe_invalid_property", "gui/pmt_probe.gui", 5)]
    [InlineData("[11:21:45][E][virtualfilesystem.cpp:456]: Could not find texture due to 'VFSOpen Error: gfx/interface/pmt_probe_missing_texture.dds not found'", "TEX-001", "gfx/interface/pmt_probe_missing_texture.dds", null, 0)]
    [InlineData("[11:22:45][E][jomini_eventtarget.cpp:692]: Failed to find a valid event target link 'pmt_probe_missing_value' at file: events/pmt_probe_events.txt line: 31 (pmt_probe.1:immediate)", "EVT-002", "pmt_probe_missing_value", "events/pmt_probe_events.txt", 31)]
    [InlineData("[11:22:45][E][jomini_scriptvalue.h:626]: Cannot read [pmt_probe_missing_value] as a script value", "SVL-001", "pmt_probe_missing_value", null, 0)]
    [InlineData("[11:22:45][E][jomini_scriptvalue.h:372]: Badly read script value pmt_probe_missing_value at file: events/pmt_probe_events.txt line: 31 (pmt_probe.1:immediate:add_gold)", "SVL-002", "pmt_probe_missing_value", "events/pmt_probe_events.txt", 31)]
    [InlineData("[11:22:45][E][jomini_effect.cpp:784]: Inconsistent effect scopes (landed_title vs. character) infile: events/pmt_probe_events.txt line: 36 (pmt_probe.1:immediate)", "SCP-001", null, "events/pmt_probe_events.txt", 36)]
    [InlineData("[11:22:45][E][jomini_dynamicdescription.cpp:66]: Unrecognized loc key pmt_probe.2.t. file: events/pmt_probe_events.txt line: 45 (pmt_probe.2)", "LOC-007", "pmt_probe.2.t", "events/pmt_probe_events.txt", 45)]
    [InlineData("[11:22:56][E][jomini_effect.cpp:1161]: Variable 'pmt_probe_var_never_set' is used but is never set. Setting it in an unused scripted trigger or effect does not count", "VAR-002", "pmt_probe_var_never_set", null, 0)]
    [InlineData("[11:22:56][E][jomini_effect.cpp:1145]: Variable 'pmt_probe_var_unused' is set but is never used. Note that use in localization doesn't count due to technical limitations. Use in unused scripted triggers and effects also does not count", "VAR-001", "pmt_probe_var_unused", null, 0)]
    [InlineData("[11:22:56][E][jomini_effect.cpp:1161]: Event target 'pmt_probe_unset' is used but is never set. Setting it in an unused scripted trigger or effect does not count", "EVT-001", "pmt_probe_unset", null, 0)]
    [InlineData("[11:22:59][E][jomini_eventmanager.cpp:372]: Event pmt_probe_broken.1 is orphaned", "EVT-003", "pmt_probe_broken.1", null, 0)]
    [InlineData("[11:23:29][E][pdx_data_factory.cpp:1437]: Could not find data system function 'GetPmtUnknownFunction' in 'ROOT.Char.GetPmtUnknownFunction'.", "LOC-003", "GetPmtUnknownFunction", null, 0)]
    [InlineData("[11:23:29][E][pdx_data_factory.cpp:1052]: Failed converting statement for 'ROOT.Char.GetPmtUnknownFunction'", "LOC-004", "ROOT.Char.GetPmtUnknownFunction", null, 0)]
    [InlineData("[11:23:29][E][pdx_data_localize.cpp:146]: Data error in loc string 'pmt_probe.2.desc'", "LOC-005", "pmt_probe.2.desc", null, 0)]
    [InlineData("[11:22:55][E][pdx_persistent_reader.cpp:216]: Error: \"Failed to read key reference: default_nudity: default_nudity, near line: 1\" in file: \"save games/byzanz.ck3\" near line: 1", "SAV-001", "save games/byzanz.ck3", null, 0)]
    [InlineData("[15:43:49][E][court_scene_manager.cpp:2764]: Failed to find applicable culture set for character with id 4294967295", "CRT-001", null, null, 0)]
    [InlineData("[15:59:08][E][pdx_persistent_reader.cpp:216]: Error: \"Failed to read key reference: scholar_perk: scholar_perk, near line: 1164761\" in file: \"\" near line: 1164761", "SAV-001", null, null, 0)]
    [InlineData("[15:59:08][E][dynasty.cpp:174]: Unknown dynasty template 'castiglione' in save, generating a cultural name instead", "SAV-002", null, null, 0)]
    [InlineData("[15:59:09][E][faith.cpp:728]: Failed to read key reference bon from database file:  line: 4019660", "SAV-003", null, null, 0)]
    [InlineData("[15:59:10][E][rite.cpp:499]: Head of rite title 'd_et_armagh' referenced by rite 'Insularism (Internal ID: 1)' does not exist", "RIT-001", "d_et_armagh", null, 0)]
    [InlineData("[15:59:10][E][pdx_assert.cpp:619]: Assertion failed: Trying to end non-active object [#16783982] in the 'vassal_contracts' manager.", "AST-001", null, null, 0)]
    public void Catalog_recognises_real_lines(string line, string code, string? name, string? file, int fileLine)
    {
        var entry = Assert.Single(Classify(line));

        Assert.Equal(code, entry.Code);
        Assert.Equal(name, entry.Name);
        Assert.Equal(file, entry.FilePath);
        Assert.Equal(fileLine == 0 ? null : fileLine, entry.FileLine);
    }

    [Fact]
    public void Read_error_with_unknown_effect_and_trigger_points_at_the_effect()
    {
        var entry = Assert.Single(Classify(
            "[11:22:45][E][pdx_persistent_reader.cpp:216]: Error: \"Unknown effect: pmt_probe_unknown_effect, near line: 12 (expanded from file: events/pmt_probe_events.txt line: 13)",
            "Unknown trigger: pmt_probe_unknown_trigger, near line: 15\" in file: \"events/pmt_probe_events.txt\" near line: 15"));

        Assert.Equal("EFF-001", entry.Code);
        Assert.Equal("pmt_probe_unknown_effect", entry.Name);
        Assert.Equal("events/pmt_probe_events.txt", entry.FilePath);
        Assert.Equal(13, entry.FileLine);
    }

    [Fact]
    public void Multi_line_read_error_without_file_is_a_save_error()
    {
        // Beim Laden eines Spielstands nennt das Spiel keine Datei; eine Mod-Datei nennt immer ihren Pfad.
        var entry = Assert.Single(Classify(
            "[15:59:09][E][pdx_persistent_reader.cpp:216]: Error: \"Failed to read key reference: tenet_monasticism: tenet_monasticism, near line: 4019691",
            "Failed to read key reference: tenet_monasticism: tenet_monasticism, near line: 4019691\" in file: \"\" near line: 4019691"));

        Assert.Equal("SAV-001", entry.Code);
        Assert.Null(entry.FilePath);
        Assert.Null(entry.FileLine);
    }

    [Fact]
    public void Read_error_cut_off_before_its_file_line_is_an_incomplete_parsing_error()
    {
        // Fehlt die Schlusszeile mit in file: "…" (abgeschnitten oder durch eine andere Meldung getrennt), bleibt nur der Anfang.
        var entry = Assert.Single(Classify(
            "[11:22:45][E][pdx_persistent_reader.cpp:216]: Error: \"Unknown effect: pmt_probe_unknown_effect, near line: 12",
            "Unknown trigger: pmt_probe_unknown_trigger, near line: 15"));

        Assert.Equal("PAR-002", entry.Code);
        Assert.StartsWith("Unknown effect: pmt_probe_unknown_effect", entry.Detail);
        Assert.Null(entry.FilePath);
    }

    [Fact]
    public void Read_error_with_unknown_trigger_only_uses_the_file_of_the_message()
    {
        var entry = Assert.Single(Classify(
            "[11:22:45][E][pdx_persistent_reader.cpp:216]: Error: \"Unknown trigger: pmt_probe_unknown_trigger, near line: 15\" in file: \"events/pmt_probe_events.txt\" near line: 15"));

        Assert.Equal("TRG-001", entry.Code);
        Assert.Equal("pmt_probe_unknown_trigger", entry.Name);
        Assert.Equal("events/pmt_probe_events.txt", entry.FilePath);
        Assert.Equal(15, entry.FileLine);
    }

    [Fact]
    public void Script_system_error_with_call_chain_takes_the_first_location()
    {
        var entry = Assert.Single(Classify(
            "[11:23:28][E][jomini_script_system.cpp:303]: Script system error!",
            "  Error: Undefined event target 'pmt_probe_unset'",
            "  Script location: file: events/pmt_probe_events.txt line: 35 (pmt_probe.1:immediate)",
            "    file: common/on_action/pmt_probe_on_actions.txt line: 10 (pmt_probe_start)",
            "    file: common/on_action/pmt_probe_on_actions.txt line: 2 (on_game_start_after_lobby)",
            ""));

        Assert.Equal("RUN-001", entry.Code);
        Assert.Equal("Undefined event target 'pmt_probe_unset'", entry.Detail);
        Assert.Equal("events/pmt_probe_events.txt", entry.FilePath);
        Assert.Equal(35, entry.FileLine);
    }

    [Fact]
    public void Duplicate_property_and_failed_reading_merge_into_one_entry()
    {
        var entries = Classify(
            "[11:21:33][E][pdx_gui_file_desc.h:43]: gui/pmt_probe.gui:5 - Duplicate property 'size'(47)",
            "",
            "[11:21:33][E][pdx_gui_factory.cpp:319]: failed reading property, at line 5 in file gui/pmt_probe.gui");

        Assert.Equal("GUI-002", Assert.Single(entries).Code);
    }

    [Fact]
    public void Every_line_of_the_probe_run_is_recognised()
    {
        var lines = LogReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", "ck3", "error.log"));

        var entries = ErrorClassifier.Classify(lines, Catalog.Value);

        Assert.True(entries.Count > 30, $"nur {entries.Count} Einträge");
        Assert.Empty(entries.Where(e => e.Kind is null).Select(e => e.Message));
    }
}
