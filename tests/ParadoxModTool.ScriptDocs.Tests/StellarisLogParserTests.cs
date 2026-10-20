using Xunit;

namespace ParadoxModTool.ScriptDocs.Tests;

/// <summary>Format der Doku-Logs von Stellaris (logs/script_documentation, Konsolenbefehl script_docs); Auszüge aus den echten Dateien von 4.5.2.</summary>
public class StellarisLogParserTests
{
    private const string Effects =
        "[14:52:31][game_application.cpp:1422]: \r\n" +
        "== EFFECT DOCUMENTATION ==\r\n" +
        "tooltip - Just a tooltip\r\n" +
        "Supported Scopes: all\r\n" +
        "\r\n" +
        "remove_deposit - Remove resource deposit on the scoped colony carrier or deposit, does not fire on_cleared if used on a blocker\r\n" +
        "remove_deposit = <key/yes>\r\n" +
        "Supported Scopes: planet ship deposit astral_rift colony\r\n" +
        "\r\n" +
        "kill_leader - Kills the scoped leader or leader of the scoped object\r\n" +
        "kill_leader = {\r\n" +
        "\tclass = <key, optional, if left out will kill scoped leader, can be used multiple times>\r\n" +
        "\r\n" +
        "\tshow_notification = <yes/no>\r\n" +
        "}\r\n" +
        "Supported Scopes: planet country ship fleet leader army colony\r\n" +
        "\r\n" +
        "set_owner - Instantly sets the owner of the scoped object to target country\r\n" +
        "set_owner = <target>\r\n" +
        "Supported Scopes: megastructure planet ship fleet leader army starbase colony\r\n";

    private const string Triggers =
        "[14:52:31][game_application.cpp:1419]: \n" +
        "== TRIGGER DOCUMENTATION ==\n" +
        "not - An inverted trigger\n" +
        "Supported Scopes: all\n" +
        "\n" +
        "switch - Switch case for a trigger\n" +
        "switch = {\n" +
        "\ttrigger = has_ethic\n" +
        "\tethic_xenophile = { <trigger> }\n" +
        "\tdefault = { <trigger> }\n" +
        "}\n" +
        "Supported Scopes: all\n" +
        "\n" +
        "num_fleets - Checks the country's number of fleets\n" +
        "num_fleets < 8\n" +
        "Supported Scopes: country\n";

    private const string Scopes =
        "[14:52:31][game_application.cpp:1434]: \n" +
        "== SCOPE DOCUMENTATION ==\n" +
        "These work as context switches (links) in triggers and effects (e.g. 'owner = { <triggers/effects> }'), or as targets of triggers and effects (e.g. 'is_owned_by = from'). Dot scoping (e.g. root.owner.capital_scope) is allowed.\n" +
        "Complete list of scope changes:\n" +
        "\n" +
        "target - Scopes from a spy network to its target country, or from an espionage operation to its target (can be various objects, as set in common/espionage_operation_types).\n" +
        "Supported Scopes: spy_network espionage_operation agreement situation\n" +
        "Output Scope: various\n" +
        "\n" +
        "background_planet - Scopes from a leader to their background planet.\n" +
        "Supported Scopes: leader\n" +
        "Output Scope: planet\n";

    private const string Modifiers =
        "[14:52:29][modifier.cpp:2274]: \n" +
        " == MODIFIER DOCUMENTATION ==\n" +
        "Note on Modifier Categories:\n" +
        "-- These are used internally as a tag to suggest where they are intended to be used, but exact behavior depends upon individual implementation.\n" +
        "-- Pop scope modifiers will generally work on e.g. planets and countries as well as pops, in which case they will apply to all pops belonging to that object.\n" +
        "\n" +
        "Printing Modifier Definitions:\n" +
        "- blank_modifier, Category: Pops\n" +
        "- pop_environment_tolerance, Category: Habitability\n" +
        "- ship_hull_add, Category: Orbital Stations, Space Stations, Military Ships, Civilian Ships, Science Ships, Transport Ships, Ship Design Stats\n";

    [Fact]
    public void Effects_read_name_description_usage_and_space_separated_scopes()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Effect, Effects);

        Assert.Equal(new[] { "tooltip", "remove_deposit", "kill_leader", "set_owner" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal(ScriptDocKind.Effect, e.Kind));
        Assert.Equal("Just a tooltip", entries[0].Description);
        Assert.Empty(entries[0].Scopes);
        Assert.Equal(new[] { "planet", "ship", "deposit", "astral_rift", "colony" }, entries[1].Scopes);
        Assert.Equal(
            "Remove resource deposit on the scoped colony carrier or deposit, does not fire on_cleared if used on a blocker\nremove_deposit = <key/yes>",
            entries[1].Description);
    }

    [Fact]
    public void A_blank_line_inside_a_usage_block_does_not_split_the_entry()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Effect, Effects);

        var kill = entries.Single(e => e.Name == "kill_leader");
        Assert.Equal(
            "Kills the scoped leader or leader of the scoped object\nkill_leader = {\n\tclass = <key, optional, if left out will kill scoped leader, can be used multiple times>\n\n\tshow_notification = <yes/no>\n}",
            kill.Description);
        Assert.Equal(new[] { "planet", "country", "ship", "fleet", "leader", "army", "colony" }, kill.Scopes);
    }

    [Fact]
    public void Triggers_use_the_same_format()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Trigger, Triggers);

        Assert.Equal(new[] { "not", "switch", "num_fleets" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal(ScriptDocKind.Trigger, e.Kind));
        Assert.Empty(entries[1].Scopes);
        Assert.Equal(new[] { "country" }, entries[2].Scopes);
        Assert.EndsWith("\tdefault = { <trigger> }\n}", entries[1].Description);
    }

    [Fact]
    public void Scope_links_become_event_targets_with_output_scope_and_skip_the_intro()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.EventTarget, Scopes);

        Assert.Equal(new[] { "target", "background_planet" }, entries.Select(e => e.Name));
        Assert.Equal(new[] { "spy_network", "espionage_operation", "agreement", "situation" }, entries[0].Scopes);
        Assert.Equal(new[] { "various" }, entries[0].Targets);
        Assert.Equal(new[] { "leader" }, entries[1].Scopes);
        Assert.Equal(new[] { "planet" }, entries[1].Targets);
        Assert.Equal("Scopes from a leader to their background planet.", entries[1].Description);
    }

    [Fact]
    public void Modifiers_keep_categories_as_extra_not_as_scopes()
    {
        var entries = ScriptDocLogParser.Parse(ScriptDocKind.Modifier, Modifiers);

        Assert.Equal(new[] { "blank_modifier", "pop_environment_tolerance", "ship_hull_add" }, entries.Select(e => e.Name));
        Assert.All(entries, e => Assert.Equal(ScriptDocKind.Modifier, e.Kind));
        Assert.All(entries, e => Assert.Empty(e.Scopes));
        Assert.Equal("Pops", entries[0].Extra["Category"]);
        Assert.Equal("Orbital Stations, Space Stations, Military Ships, Civilian Ships, Science Ships, Transport Ships, Ship Design Stats", entries[2].Extra["Category"]);
    }

    [Fact]
    public void Profile_file_names_override_the_default_log_names()
    {
        var names = new Dictionary<string, string> { ["EventTarget"] = "scopes.log" };

        Assert.Equal("scopes.log", ScriptDocLogFiles.FileNameOf(ScriptDocKind.EventTarget, names));
        Assert.Equal("effects.log", ScriptDocLogFiles.FileNameOf(ScriptDocKind.Effect, names));
        Assert.Equal("event_targets.log", ScriptDocLogFiles.FileNameOf(ScriptDocKind.EventTarget));
    }

    [Fact]
    public void Import_reads_scopes_log_as_event_targets_and_sees_no_change_afterwards()
    {
        var folder = Path.Combine(Path.GetTempPath(), "pmt-stellaris-docs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "effects.log"), Effects);
            File.WriteAllText(Path.Combine(folder, "scopes.log"), Scopes);
            var names = new Dictionary<string, string> { ["EventTarget"] = "scopes.log" };
            var database = new ScriptDocDatabase();

            Assert.True(ScriptDocImporter.NeedsImport(database, folder, names));
            var record = ScriptDocImporter.Import(database, folder, DateTime.UtcNow, names);

            Assert.NotNull(record);
            Assert.Contains(ScriptDocKind.EventTarget, record!.Kinds);
            Assert.Contains(ScriptDocKind.Effect, record.Kinds);
            Assert.False(ScriptDocImporter.NeedsImport(database, folder, names));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
