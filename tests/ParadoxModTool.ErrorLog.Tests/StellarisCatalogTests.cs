using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

/// <summary>
/// Der Fehlerkatalog von Stellaris gegen die echten Zeilen der Probeläufe mit "PMT Error Probe" und "PMT Order Probe A/B" (2026-10-16, gespeichert unter
/// Data/stellaris): error.log ohne BOM in den Probe-Dateien, error_bom.log aus dem ersten Lauf, in dem alle Skriptdateien eine BOM hatten.
/// Die 80 Zeilen "Invalid supported_version" der Workshop-Mods des Nutzers sind auf drei gekürzt.
/// </summary>
public class StellarisCatalogTests
{
    private static readonly Lazy<ErrorCatalog> Catalog = new(() =>
        ErrorCatalog.Load(Path.Combine(AppContext.BaseDirectory, "profiles", "errorlog", "stellaris.json")));

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
            foreach (var game in new[] { "Europa Universalis", "Crusader Kings", "Victoria", "Hearts of Iron" })
            {
                Assert.DoesNotContain(game, k.Explain["de"] + k.Explain["en"]);
            }
        });
        // Stellaris kennt kein INJECT/REPLACE, und "log" schreibt ins game.log statt ins debug.log: keine Scope-Verfolgung.
        Assert.DoesNotContain(catalog.Kinds, k => k.Code.StartsWith("INJ-", StringComparison.Ordinal));
        Assert.Equal(string.Empty, catalog.Trace.DebugLogSource);
    }

    [Theory]
    [InlineData("[17:14:03][dlc.cpp:1847]: Could not find files for mod: D:\\SteamLibrary\\steamapps\\workshop\\content\\281990\\3253479588", "MOD-001", null, "D:\\SteamLibrary\\steamapps\\workshop\\content\\281990\\3253479588", 0)]
    [InlineData("[17:14:03][dlc.cpp:346]: Invalid supported_version in  file: mod/Flogi Slavemarket.mod line: 6", "MOD-002", null, "mod/Flogi Slavemarket.mod", 6)]
    [InlineData("[17:14:04][localization_reader.cpp:147]: Expected colon(:) at line 5 and column 23 in localisation/english/pmt_probe_l_english.yml", "LOC-009", null, "localisation/english/pmt_probe_l_english.yml", 5)]
    [InlineData("[17:14:04][localize.cpp:1975]: Localization file 'localisation/english/pmt_probe_no_bom_l_english.yml' should be in in utf-8-bom encoding", "LOC-010", null, "localisation/english/pmt_probe_no_bom_l_english.yml", 0)]
    [InlineData("[17:14:04][localization_reader.cpp:83]: Missing UTF8 BOM in localisation/english/pmt_probe_no_bom_l_english.yml", "LOC-010", null, "localisation/english/pmt_probe_no_bom_l_english.yml", 0)]
    [InlineData("[17:14:07][3dicontype.cpp:20]: Inexistent sprite type \"GFX_pmt_probe_unknown_sprite\" referenced at file: interface/pmt_probe.gui line: 9", "GUI-004", "GFX_pmt_probe_unknown_sprite", "interface/pmt_probe.gui", 9)]
    [InlineData("[17:14:09][texturehandler.cpp:66]: Texture Handler encountered missing texture file: gfx/interface/pmt_probe_missing.dds", "TEX-001", "gfx/interface/pmt_probe_missing.dds", null, 0)]
    [InlineData("[17:14:09][texturehandler.cpp:350]: Couldn't find texture file: gfx/interface/pmt_probe_missing.dds.", "TEX-001", "gfx/interface/pmt_probe_missing.dds", null, 0)]
    [InlineData("[17:14:09][spritetype.cpp:317]: Error initialising texture: gfx/interface/pmt_probe_missing.dds for spritetype GFX_pmt_probe_missing_texture", "TEX-001", "gfx/interface/pmt_probe_missing.dds", null, 0)]
    [InlineData("[17:14:15][modifier.cpp:3279]: Missing modifier localization: pmt_probe_static_modifier", "MDF-003", "pmt_probe_static_modifier", null, 0)]
    [InlineData("[17:14:15][game_singleobjectdatabase.h:170]: Object with key: print_scope_effect already exists, using the one at  file: common/scripted_effects/00_scripted_effects.txt line: 16", "DUP-001", "print_scope_effect", "common/scripted_effects/00_scripted_effects.txt", 16)]
    [InlineData("[17:14:16][building_type.cpp:762]: Building 'building_pmt_probe' is lacking the building_sets definition.", "BLD-001", "building_pmt_probe", null, 0)]
    [InlineData("[17:14:26][eventpicture.cpp:43]: pmt_probe.2 is referencing non-existent event picture GFX_evt_pmt_probe_missing", "GFX-001", "pmt_probe.2", null, 0)]
    [InlineData("[17:14:26][eventmanager.cpp:427]: an event with id [tutorial.503] already exists!  file: events/tutorial_events.txt line: 333", "EVT-005", "tutorial.503", "events/tutorial_events.txt", 333)]
    [InlineData("[17:14:26][event.cpp:1508]: Event pmt_probe.4 has neither MTTH nor is_triggered_only = yes, so it will try to fire every day (if you really want to do this, specify mean_time_to_happen = { days = 1 } )", "EVT-007", "pmt_probe.4", null, 0)]
    [InlineData("[17:14:26][event.cpp:1270]: Event pmt_probe_other.5 at  file: events/pmt_probe_events.txt line: 70 has an invalid ID", "EVT-006", "pmt_probe_other.5", "events/pmt_probe_events.txt", 70)]
    [InlineData("[17:14:26][onaction.cpp:104]: OnAction \"on_game_start_country\" is referencing an invalid event: \"pmt_probe.998\"", "EVT-009", "pmt_probe.998", null, 0)]
    [InlineData("[17:14:28][parser_deferred_database_objects.cpp:84]: Failed to deferred read key reference pmt_probe_missing_modifier from database  file: events/pmt_probe_events.txt line: 17", "REF-001", "pmt_probe_missing_modifier", "events/pmt_probe_events.txt", 17)]
    [InlineData("[17:14:28][building_type.cpp:747]: Missing building Localization Key: building_pmt_probe_desc", "BLD-002", "building_pmt_probe_desc", null, 0)]
    [InlineData("[17:14:28][building_type.cpp:830]: No category assigned to building type building_pmt_probe", "BLD-003", "building_pmt_probe", null, 0)]
    [InlineData("[17:14:28][building_type.cpp:844]: Building building_pmt_probe is missing texture gfx/interface/icons/buildings/building_pmt_probe.dds", "BLD-004", "building_pmt_probe", null, 0)]
    [InlineData("[17:14:31][modifier.cpp:2829]: Missing modifier scripted icon gfx/interface/icons/modifiers/pmt_probe_missing_icon.dds", "MDF-002", "gfx/interface/icons/modifiers/pmt_probe_missing_icon.dds", null, 0)]
    [InlineData("[17:14:32][modifier.cpp:4256]: Invalid modifier pmt_probe_unknown_modifier_add at  file: common/static_modifiers/pmt_probe_modifiers.txt line: 4", "MDF-001", "pmt_probe_unknown_modifier_add", "common/static_modifiers/pmt_probe_modifiers.txt", 4)]
    [InlineData("[17:14:32][trigger_impl.cpp:1706]: Scripted Trigger pmt_probe_unknown_trigger is invalid at  file: events/pmt_probe_events.txt line: 14", "TRG-001", "pmt_probe_unknown_trigger", "events/pmt_probe_events.txt", 14)]
    [InlineData("[17:14:33][trigger_impl.cpp:1706]: Scripted Trigger pmt_probe_unknown_trigger_in_scripted_trigger is invalid at  common/scripted_triggers/pmt_probe_triggers.txt:3 @ in scripted trigger pmt_probe_trigger at file: events/pmt_probe_events.txt line: 25", "TRG-001", "pmt_probe_unknown_trigger_in_scripted_trigger", "common/scripted_triggers/pmt_probe_triggers.txt", 3)]
    [InlineData("[17:14:33][trigger_impl.cpp:1677]: [ file: events/pmt_probe_events.txt line: 14]: Error in scripted trigger, cannot find: pmt_probe_unknown_trigger", "TRG-001", "pmt_probe_unknown_trigger", "events/pmt_probe_events.txt", 14)]
    [InlineData("[17:14:37][trigger_impl.cpp:1677]: [ common/scripted_effects/pmt_probe_effects.txt:5 @ scripted effect pmt_probe_effect at file: events/pmt_probe_events.txt line: 22]: Error in scripted trigger, cannot find: pmt_probe_unknown_trigger_in_scripted", "TRG-001", "pmt_probe_unknown_trigger_in_scripted", "common/scripted_effects/pmt_probe_effects.txt", 5)]
    [InlineData("[17:14:35][effect_impl.cpp:1839]: Error in fire event effect at  file: events/pmt_order_ctrl_a.txt line: 83, could not find event: pmt_order.15", "EVT-008", "pmt_order.15", "events/pmt_order_ctrl_a.txt", 83)]
    [InlineData("[17:14:35][effect_impl.cpp:825]: Script Error: Invalid scripted effect: pmt_probe_unknown_effect at  file: events/pmt_probe_events.txt line: 12", "EFF-001", "pmt_probe_unknown_effect", "events/pmt_probe_events.txt", 12)]
    [InlineData("[17:14:35][effect_impl.cpp:825]: Script Error: Invalid scripted effect: pmt_probe_unknown_effect_in_scripted at  common/scripted_effects/pmt_probe_effects.txt:3 @ scripted effect pmt_probe_effect at file: events/pmt_probe_events.txt line: 22", "EFF-001", "pmt_probe_unknown_effect_in_scripted", "common/scripted_effects/pmt_probe_effects.txt", 3)]
    [InlineData("[17:15:19][game_text.cpp:1287]: Unknown property `GetPmtUnknownFunction` in text: GetPmtUnknownFunction] and ", "LOC-003", "GetPmtUnknownFunction", null, 0)]
    [InlineData("[17:15:19][game_text.cpp:1531]: Unknown promotion `FromFrom` in text: FromFrom.GetName]", "LOC-004", "FromFrom", null, 0)]
    [InlineData("[17:15:19][eventtarget.cpp:3849]: Undefined event target: pmt_probe_missing_target, location:  file: events/pmt_probe_events.txt line: 84", "EVT-002", "pmt_probe_missing_target", "events/pmt_probe_events.txt", 84)]
    [InlineData("[17:08:07][scriptedeffect.cpp:33]: scripted effect if is overwriting an existing effect, rename it", "SEF-001", "if", null, 0)]
    [InlineData("[14:52:41][galaxy_generator.cpp:3175]: Can only spawn 0 Advanced Empires out of 1 desired", "GAL-001", null, null, 0)]
    [InlineData("[17:08:35][galaxy_configuration.cpp:654]: Invalid template name in settings.txt", "SET-001", null, null, 0)]
    public void Catalog_recognises_real_lines(string line, string code, string? name, string? file, int fileLine)
    {
        var entry = Assert.Single(Classify(line));

        Assert.Equal(code, entry.Code);
        Assert.Equal(name, entry.Name);
        Assert.Equal(file, entry.FilePath);
        Assert.Equal(fileLine == 0 ? null : fileLine, entry.FileLine);
    }

    [Theory]
    [InlineData("[17:14:03][persistent.cpp:41]: Error: \"Unexpected token: dependencies, near line: 7", "\" in file: \"mod/ugc_2798297351.mod\" near line: 8", "MOD-003", null, "mod/ugc_2798297351.mod", 8)]
    [InlineData("[17:07:56][persistent.cpp:41]: Error: \"Unexpected token: \uFEFF, near line: 2", "\" in file: \"interface/00_pmt_order_a.gfx\" near line: 2", "ENC-001", null, "interface/00_pmt_order_a.gfx", 2)]
    [InlineData("[17:14:07][persistent.cpp:41]: Error: \"Unexpected token: pmt_probe_invalid_property, near line: 6", "\" in file: \"interface/pmt_probe.gui\" near line: 6", "TOK-002", "pmt_probe_invalid_property", "interface/pmt_probe.gui", 6)]
    [InlineData("[17:14:26][effect.cpp:471]: Error: \"Malformed token: @pmt_probe_missing_variable, near line: 19", "\" in file: \"events/pmt_probe_events.txt\" near line: 19", "TOK-001", "@pmt_probe_missing_variable", "events/pmt_probe_events.txt", 19)]
    [InlineData("[17:14:26][effect.cpp:529]: Wrong scope for effect 'add_resource' at  file: events/pmt_probe_events.txt line: 86", "Current Scope: colony", "SCP-001", "add_resource", "events/pmt_probe_events.txt", 86)]
    [InlineData("[17:15:19][effect_impl.cpp:242]: Script Error: Invalid context switch [from] from United Nations of Earth [country], file: events/pmt_probe_events.txt line: 85, Scope: ", "type=country", "RUN-001", "from", "events/pmt_probe_events.txt", 85)]
    [InlineData("[17:08:18][eventmanager.cpp:489]: Corrupt Event Table Entry -  in events/00_pmt_order_a.txtline: 2", "This usually means the syntax is wrong earlier in the file (e.g. wrong number of { or }, non-existent curly brackets effects)", "EVT-010", null, "events/00_pmt_order_a.txt", 2)]
    public void Catalog_recognises_real_two_line_messages(string first, string second, string code, string? name, string? file, int fileLine)
    {
        var entry = Assert.Single(Classify(first, second));

        Assert.Equal(code, entry.Code);
        Assert.Equal(name, entry.Name);
        Assert.Equal(file, entry.FilePath);
        Assert.Equal(fileLine, entry.FileLine);
    }

    [Fact]
    public void Expected_key_format_error_names_the_bom_as_a_cause()
    {
        var entry = Assert.Single(Classify(
            "[17:08:09][persistent.cpp:41]: Error: \"Expected format is \"key={\", check formatting and verify encoding is UTF8 (no BOM): building_pmt_probe, near line: 2",
            "Unexpected token: always, near line: 5",
            "\" in file: \"common/buildings/pmt_probe_buildings.txt\" near line: 5"));

        Assert.Equal("PAR-003", entry.Code);
        Assert.Equal("common/buildings/pmt_probe_buildings.txt", entry.FilePath);
        Assert.Contains("BOM", entry.Kind!.Explain["en"]);
    }

    [Fact]
    public void Duplicate_messages_explain_which_copy_wins()
    {
        // Gemessen 2026-10-16: "Object with key" nennt die geltende Definition, "an event with id" die verworfene Kopie.
        Assert.Contains("names the definition that applies", Catalog.Value.Find("DUP-001")!.Explain["en"]);
        Assert.Contains("names the copy that is dropped", Catalog.Value.Find("EVT-005")!.Explain["en"]);
    }

    [Theory]
    [InlineData("error.log")]
    [InlineData("error_bom.log")]
    public void Every_line_of_the_probe_runs_is_recognised(string file)
    {
        var lines = LogReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Data", "stellaris", file));

        var entries = ErrorClassifier.Classify(lines, Catalog.Value);

        Assert.True(entries.Count > 50, $"nur {entries.Count} Einträge");
        Assert.Empty(entries.Where(e => e.Kind is null).Select(e => e.Message));
        // Die allgemeinen Lesefehler fangen alles auf; in den Probeläufen hat jede Meldung einen eigenen Code.
        Assert.DoesNotContain(entries, e => e.Code is "PAR-001" or "PAR-002");
    }
}
