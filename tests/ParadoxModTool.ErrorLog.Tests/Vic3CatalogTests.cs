using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>
/// Der Fehlerkatalog von Victoria 3 gegen die echten Zeilen des Probelaufs "PMT Error Probe" (error.log vom 2026-10-10, gespeichert unter Data/vic3).
/// </summary>
public class Vic3CatalogTests
{
    private static readonly Lazy<ErrorCatalog> Catalog = new(() =>
        ErrorCatalog.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "errorlog", "vic3.json")));

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
            Assert.DoesNotContain("Europa Universalis", k.Explain["de"] + k.Explain["en"]);
            Assert.DoesNotContain("Crusader Kings", k.Explain["de"] + k.Explain["en"]);
        });
        Assert.Equal("jomini_effect_impl.cpp", catalog.Trace.DebugLogSource);
        Assert.Null(catalog.Trace.DebugScopeSource);
    }

    [Theory]
    [InlineData("[22:10:14][pdx_localize.cpp:199]: Duplicate localization key. Key 'pmt_probe.2.a' is defined in both 'localization/english/pmt_probe_l_english.yml' and 'localization/english/pmt_probe_l_english.yml'.", "LOC-006", "pmt_probe.2.a", "localization/english/pmt_probe_l_english.yml", 0)]
    [InlineData("[22:10:14][localization_reader.cpp:451]: Missing colon (:) separator at line 6 and column 22 in localization/english/pmt_probe_l_english.yml", "LOC-009", null, "localization/english/pmt_probe_l_english.yml", 6)]
    [InlineData("[22:10:14][localize.cpp:1974]: Localization file 'localization/english/pmt_probe_no_bom_l_english.yml' should be in in utf-8-bom encoding", "LOC-010", null, "localization/english/pmt_probe_no_bom_l_english.yml", 0)]
    [InlineData("[22:10:14][localization_reader.cpp:402]: Missing UTF8 BOM in 'localization/english/pmt_probe_no_bom_l_english.yml'", "LOC-010", null, "localization/english/pmt_probe_no_bom_l_english.yml", 0)]
    [InlineData("[22:10:17][pdx_data_factory.cpp:1462]: Could not find data system function 'PmtProbeUnknownFunction'", "LOC-003", "PmtProbeUnknownFunction", null, 0)]
    [InlineData("[22:10:17][pdx_gui_factory.cpp:268]: failed reading callback, at gui/pmt_probe.gui:25", "GUI-003", null, "gui/pmt_probe.gui", 25)]
    [InlineData("[22:10:27][pdx_persistent_reader.cpp:268]: Error: \"Unknown modifier type: pmt_probe_unknown_modifier_add. It is either invalid, or a potential dynamic modifier type definition missing from the database.: pmt_probe_unknown_modifier_add, near line: 4\" in file: \"common/static_modifiers/pmt_probe_modifiers.txt\" near line: 4", "MDF-001", "pmt_probe_unknown_modifier_add", "common/static_modifiers/pmt_probe_modifiers.txt", 4)]
    [InlineData("[22:10:27][pdx_persistent_reader.cpp:268]: Error: \"Trigger section already read earlier: is_visible, near line: 3\" in file: \"common/laws/pmt_probe_laws.txt\" near line: 5", "INJ-002", "is_visible", "common/laws/pmt_probe_laws.txt", 5)]
    [InlineData("[22:10:28][gamedatabase.h:395]: You are trying to inject/replace to a non-existing entry: building_pmt_probe_missing:Inject in the file common/buildings/pmt_probe_buildings.txt:2", "INJ-001", "building_pmt_probe_missing", "common/buildings/pmt_probe_buildings.txt", 2)]
    [InlineData("[22:10:28][jomini_eventtarget.cpp:694]: Failed to find a valid event target link 'pmt_probe_nonexistent_value' at common/script_values/pmt_probe_values.txt:3", "EVT-002", "pmt_probe_nonexistent_value", "common/script_values/pmt_probe_values.txt", 3)]
    [InlineData("[22:10:28][jomini_scriptvalue.h:617]: Cannot read [pmt_probe_nonexistent_value] as a script value", "SVL-001", "pmt_probe_nonexistent_value", null, 0)]
    [InlineData("[22:10:28][pdx_persistent_reader.cpp:268]: Error: \"Unknown trigger type: pmt_probe_unknown_trigger_in_scripted, near line: 6\" in file: \"common/scripted_effects/pmt_probe_effects.txt\" near line: 6", "TRG-001", "pmt_probe_unknown_trigger_in_scripted", "common/scripted_effects/pmt_probe_effects.txt", 6)]
    [InlineData("[22:10:29][jomini_effect.cpp:753]: Inconsistent effect scopes (state vs. country) inevents/pmt_probe_events.txt:36", "SCP-001", null, "events/pmt_probe_events.txt", 36)]
    [InlineData("[22:10:29][jomini_eventmanager.cpp:572]: Namespace 'REPLACE:pmt_probe' used in event 'REPLACE:pmt_probe.5' (file: events/pmt_probe_events.txt) is not defined in this file - it might not load properly.", "EVT-004", "REPLACE:pmt_probe.5", "events/pmt_probe_events.txt", 0)]
    [InlineData("[22:10:29][jomini_eventmanager.cpp:118]: 'REPLACE:pmt_probe.5' does not have a valid namespace", "EVT-004", "REPLACE:pmt_probe.5", null, 0)]
    [InlineData("[22:10:29][jomini_eventmanager.cpp:440]: Duplicated event ID 'pmt_probe.3' found. New Location: 'events/pmt_probe_events.txt:63', Previous Location: 'events/pmt_probe_events.txt:68'", "EVT-005", "pmt_probe.3", "events/pmt_probe_events.txt", 63)]
    [InlineData("[22:10:32][eventtargetlinks.cpp:137]: Invalid database object key 'law_pmt_probe_missing' file location: events/pmt_probe_events.txt:29", "DBK-001", "law_pmt_probe_missing", "events/pmt_probe_events.txt", 29)]
    [InlineData("[22:10:32][jomini_eventmanager.cpp:376]: Event pmt_probe.4 is orphaned", "EVT-003", "pmt_probe.4", null, 0)]
    [InlineData("[22:11:38][jomini_scriptvalue.cpp:1659]: Value of wrong type in 'common/on_actions/pmt_probe_on_actions.txt:28'. Got value of type 'none'", "SVL-003", null, "common/on_actions/pmt_probe_on_actions.txt", 28)]
    public void Catalog_recognises_real_lines(string line, string code, string? name, string? file, int fileLine)
    {
        var entry = Assert.Single(Classify(line));

        Assert.Equal(code, entry.Code);
        Assert.Equal(name, entry.Name);
        Assert.Equal(file, entry.FilePath);
        Assert.Equal(fileLine == 0 ? null : fileLine, entry.FileLine);
    }

    [Fact]
    public void Empty_key_reference_without_file_is_vanilla_noise()
    {
        var entry = Assert.Single(Classify(
            "[22:10:34][pdx_persistent_reader.cpp:268]: Error: \"Failed to read key reference: : , near line: 1",
            "Failed to read key reference: : , near line: 1\" in file: \"\" near line: 1"));

        Assert.Equal("REF-001", entry.Code);
        Assert.Null(entry.FilePath);
    }

    [Fact]
    public void Read_error_without_file_that_is_no_key_reference_stays_a_parsing_error()
    {
        // Victoria 3 schreibt in file: "" auch für Daten, die nicht aus einem Spielstand kommen (siehe REF-001); "Spielstand, Mod nicht betroffen" wäre falsch.
        var entry = Assert.Single(Classify("[22:10:27][pdx_persistent_reader.cpp:268]: Error: \"Unexpected token: }, near line: 4\" in file: \"\" near line: 4"));

        Assert.Equal("PAR-001", entry.Code);
    }

    [Fact]
    public void Read_error_in_a_save_game_is_a_save_error()
    {
        var entry = Assert.Single(Classify("[22:10:27][pdx_persistent_reader.cpp:268]: Error: \"Unexpected token: }, near line: 4\" in file: \"save games/autosave.v3\" near line: 4"));

        Assert.Equal("SAV-002", entry.Code);
    }

    [Fact]
    public void Trigger_section_read_twice_explains_both_causes()
    {
        var kind = Catalog.Value.Find("INJ-002")!;

        Assert.Contains("twice", kind.Explain["en"]);
        Assert.Contains("INJECT", kind.Explain["en"]);
        Assert.Contains("doppelt", kind.Explain["de"]);
    }

    [Fact]
    public void Every_line_of_the_probe_run_is_recognised()
    {
        var lines = LogReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", "vic3", "error.log"));

        var entries = ErrorClassifier.Classify(lines, Catalog.Value);

        Assert.True(entries.Count > 50, $"nur {entries.Count} Einträge");
        Assert.Empty(entries.Where(e => e.Kind is null).Select(e => e.Message));
        // Die allgemeinen Lesefehler fangen alles auf; im Probelauf hat jede Meldung einen eigenen Code.
        Assert.DoesNotContain(entries, e => e.Code is "PAR-001" or "PAR-002");
    }

    [Fact]
    public void Two_unknown_triggers_in_one_read_error_name_the_first()
    {
        var entry = Assert.Single(Classify(
            "[22:10:28][pdx_persistent_reader.cpp:268]: Error: \"Unknown trigger type: pmt_a, near line: 6",
            "Unknown trigger type: pmt_b, near line: 9\" in file: \"events/x.txt\" near line: 9"));

        Assert.Equal("TRG-001", entry.Code);
        Assert.Equal("pmt_a", entry.Name);
        Assert.Equal("events/x.txt", entry.FilePath);
    }

    [Fact]
    public void Key_reference_block_with_another_error_is_no_harmless_noise()
    {
        var entry = Assert.Single(Classify(
            "[22:10:34][pdx_persistent_reader.cpp:268]: Error: \"Failed to read key reference: : , near line: 1",
            "Unexpected token: }, near line: 1\" in file: \"\" near line: 1"));

        Assert.NotEqual("REF-001", entry.Code);
    }

    [Fact]
    public void Unlocalized_gui_text_with_an_apostrophe_is_recognised()
    {
        var entry = Assert.Single(Classify("[22:10:16][pdx_gui_localize.cpp:182]: Unlocalized text 'Don't panic' at gui/x.gui:12, either localize it or use the raw_text property instead of text"));

        Assert.Equal("LOC-007", entry.Code);
        Assert.Equal("Don't panic", entry.Name);
        Assert.Equal(12, entry.FileLine);
    }

    [Fact]
    public void Unlocalized_gui_text_in_a_path_with_spaces_is_recognised()
    {
        var entry = Assert.Single(Classify("[22:10:16][pdx_gui_localize.cpp:182]: Unlocalized text 'X' at gui/my mod/x.gui:3, either localize it or use the raw_text property instead of text"));

        Assert.Equal("LOC-007", entry.Code);
        Assert.Equal("gui/my mod/x.gui", entry.FilePath);
    }

    [Fact]
    public void Runtime_error_guards_only_name_forms_victoria_3_writes()
    {
        var kind = Catalog.Value.Find("RUN-001")!;

        Assert.DoesNotContain(kind.TraceGuards, g => g.Detail.ToString().Contains("'scope:", StringComparison.Ordinal));
    }
}
