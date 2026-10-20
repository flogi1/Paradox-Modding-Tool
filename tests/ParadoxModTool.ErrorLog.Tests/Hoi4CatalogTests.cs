using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>
/// Der Fehlerkatalog von Hearts of Iron IV gegen die echten Zeilen des Probelaufs "PMT Error Probe" (error.log vom 2026-10-16, gespeichert unter
/// Data/hoi4; dazu die Vanilla-Zeilen desselben Laufs).
/// </summary>
public class Hoi4CatalogTests
{
    private static readonly Lazy<ErrorCatalog> Catalog = new(() =>
        ErrorCatalog.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "errorlog", "hoi4.json")));

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
            Assert.DoesNotContain("Victoria", k.Explain["de"] + k.Explain["en"]);
        });
    }

    [Theory]
    [InlineData("[18:15:34][no_game_date][persistent.cpp:67]: Error: \"Unexpected token: pmt_probe_unknown_property, near line: 6\" in file: \"interface/pmt_probe.gui\" near line: 6", "PAR-003", "pmt_probe_unknown_property", "interface/pmt_probe.gui", 6)]
    [InlineData("[18:15:58][no_game_date][persistent.cpp:67]: Error: \"Unknown modifier: pmt_probe_unknown_modifier, near line: 6\" in file: \"common/ideas/pmt_probe_ideas.txt\" near line: 6", "MDF-001", "pmt_probe_unknown_modifier", "common/ideas/pmt_probe_ideas.txt", 6)]
    [InlineData("[18:15:58][no_game_date][persistent.cpp:67]: Error: \"Unknown category: pmt_probe_missing_category: {, near line: 1\" in file: \"common/decisions/pmt_probe_decisions.txt\" near line: 3", "DEC-001", "pmt_probe_missing_category", "common/decisions/pmt_probe_decisions.txt", 3)]
    [InlineData("[18:16:03][no_game_date][trigger.cpp:700]: Invalid trigger 'pmt_probe_unknown_trigger_in_scripted' in common/scripted_triggers/pmt_probe_triggers.txt line : 2", "TRG-001", "pmt_probe_unknown_trigger_in_scripted", "common/scripted_triggers/pmt_probe_triggers.txt", 2)]
    [InlineData("[18:16:04][no_game_date][effect.cpp:445]: Invalid effect 'pmt_probe_unknown_effect' in events/pmt_probe_events.txt line : 9", "EFF-001", "pmt_probe_unknown_effect", "events/pmt_probe_events.txt", 9)]
    [InlineData("[15:29:30][1936.01.01.12][effect.cpp:445]: Invalid effect 'IRQ_kamil_shabib' in history/countries/IRQ - Iraq.txt line : 116", "EFF-001", "IRQ_kamil_shabib", "history/countries/IRQ - Iraq.txt", 116)]
    [InlineData("[18:16:04][no_game_date][effect.cpp:435]: Invalid scope type for effect add_building_construction in events/pmt_probe_events.txt line : 10", "SCP-001", "add_building_construction", "events/pmt_probe_events.txt", 10)]
    [InlineData("[18:16:04][no_game_date][id.cpp:104]: Failed to create id 44000001 50. Already exists in game. This might crash the game.", "EVT-005", null, null, 0)]
    [InlineData("[18:17:47][1936.01.02.13][effect.cpp:555]: events/pmt_probe_events.txt:52: add_political_power: Invalid Scope, supported: Country, provided: State", "SCP-002", "add_political_power", "events/pmt_probe_events.txt", 52)]
    [InlineData("[15:29:27][no_game_date][triggerimplementation.cpp:9803]: common/scripted_effects/BLT_scripted_effects.txt:77: has_game_rule: game rule LIT_ai_behavior does not exist", "RUL-001", "LIT_ai_behavior", "common/scripted_effects/BLT_scripted_effects.txt", 77)]
    [InlineData("[18:29:42][no_game_date][parser.cpp:1111]: Error: unexpected token in file: \"common/medals/pmt_rp_d_own.txt\" near line: 2 ( { )", "PAR-004", "{", "common/medals/pmt_rp_d_own.txt", 2)]
    [InlineData("[18:25:09][no_game_date][idea_database.cpp:311]: Duplicate idea. pmt_order_idea_y is a duplicate.", "IDE-001", "pmt_order_idea_y", null, 0)]
    [InlineData("[18:25:39][1936.01.01.12][trigger.cpp:460]: common/on_actions/pmt_order_a_on_actions.txt:5: tag: Invalid Scope, supported: Country|Character|Combatant, provided: None", "SCP-002", "tag", "common/on_actions/pmt_order_a_on_actions.txt", 5)]
    public void Catalog_recognises_real_lines(string line, string code, string? name, string? file, int fileLine)
    {
        var entry = Assert.Single(Classify(line));

        Assert.Equal(code, entry.Code);
        Assert.Equal(name, entry.Name);
        Assert.Equal(file, entry.FilePath);
        Assert.Equal(fileLine == 0 ? null : fileLine, entry.FileLine);
    }

    [Theory]
    [InlineData(
        "[18:16:04][no_game_date][effect.cpp:445]: Invalid effect 'pmt_probe_unknown_effect' in events/pmt_probe_events.txt line : 9",
        "[18:16:04][no_game_date][persistent.cpp:67]: Error: \"Unknown effect-type: pmt_probe_unknown_effect, near line: 9\" in file: \"events/pmt_probe_events.txt\" near line: 9",
        "EFF-001")]
    [InlineData(
        "[18:16:03][no_game_date][trigger.cpp:700]: Invalid trigger 'pmt_probe_unknown_trigger_in_scripted' in common/scripted_triggers/pmt_probe_triggers.txt line : 2",
        "[18:16:03][no_game_date][trigger.cpp:568]: Error: \"Unknown trigger-type: pmt_probe_unknown_trigger_in_scripted, near line: 2\" in file: \"common/scripted_triggers/pmt_probe_triggers.txt\" near line: 3",
        "TRG-001")]
    [InlineData(
        "[18:15:58][no_game_date][persistent.cpp:67]: Error: \"Unknown category: pmt_probe_missing_category: {, near line: 1\" in file: \"common/decisions/pmt_probe_decisions.txt\" near line: 3",
        "[18:15:58][no_game_date][gameitemdatabasehelper.h:149]: Error: \"Unknown category for : pmt_probe_decision It will be ignored: }, near line: 6\" in file: \"common/decisions/pmt_probe_decisions.txt\" near line: 7",
        "DEC-001")]
    [InlineData(
        "[18:25:13][no_game_date][id.cpp:104]: Failed to create id 1000002 50. Already exists in game. This might crash the game.",
        "[18:25:13][no_game_date][namespacemanager.h:209]: Reverse id lookup: id 1000002 = pmt_order.2",
        "EVT-005")]
    [InlineData(
        "[18:25:17][no_game_date][triggerimplementation.cpp:9803]: common/scripted_effects/BLT_scripted_effects.txt:77: has_game_rule: game rule LIT_ai_behavior does not exist",
        "[18:25:17][no_game_date][trigger.cpp:117]: Trigger failed to validate: common/scripted_effects/BLT_scripted_effects.txt:77: has_game_rule",
        "RUL-001")]
    public void Follow_up_line_attaches_to_its_lead(string lead, string followUp, string code)
    {
        var entry = Assert.Single(Classify(lead, followUp));

        Assert.Equal(code, entry.Code);
    }

    [Fact]
    public void Every_line_of_the_probe_run_is_classified()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "hoi4", "error.log");
        var entries = ErrorClassifier.Classify(LogReader.ReadFile(path), Catalog.Value);

        Assert.Equal(16, entries.Count);
        Assert.DoesNotContain(entries, e => e.Code == ErrorEntry.UnmatchedCode);
    }
}
