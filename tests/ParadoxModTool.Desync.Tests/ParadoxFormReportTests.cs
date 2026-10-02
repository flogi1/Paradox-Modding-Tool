using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class ParadoxFormReportTests
{
    private static readonly IReadOnlyList<ParadoxFormField> Eu5Form =
    [
        new("I have verified my game files (on Steam)", FormFieldKind.Hint, FormSource.VerifiedFiles, null),
        new("I have disabled all mods", FormFieldKind.Option, FormSource.ModsDisabled, null),
        new("Summary", FormFieldKind.Text, FormSource.Summary, null),
        new("Description", FormFieldKind.Multiline, FormSource.Description, null),
        new("Issue Type", FormFieldKind.Hint, FormSource.IssueType, null),
        new("Steps to reproduce", FormFieldKind.Multiline, FormSource.Steps, null),
        new("Game Version", FormFieldKind.Option, FormSource.GameVersion, null),
        new("Bug Type", FormFieldKind.Option, FormSource.Fixed, "Out of Sync"),
        new("Attachments", FormFieldKind.Attachments, FormSource.Dumps, null),
        new("Screenshot", FormFieldKind.Hint, FormSource.Screenshot, null),
    ];

    private static MachineSummary Machine(
        int id, string? role = "Host", string? version = "1.0.3", IReadOnlyList<DumpMod>? mods = null,
        string? player = "Alice", IReadOnlyList<string>? dlcs = null) =>
        new(id, role, player, "0123456789abcdef", "fedcba9876543210",
            new SaveHeader("1425.2.4.8", version, 1, 2, dlcs ?? ["Dlc A", "Dlc B"]), mods ?? [], false, false);

    private static ReportFacts Facts(
        IReadOnlyList<MachineSummary>? machines = null,
        string? date = "1425.2.4.8",
        IReadOnlyList<string>? categories = null,
        string? sessionKind = "preparation",
        IReadOnlyList<ReportCause>? causes = null,
        bool diffAvailable = true,
        IReadOnlyList<ReportLead>? leads = null,
        IReadOnlyList<ReportDump>? dumps = null,
        IReadOnlyList<SummaryWarning>? warnings = null,
        IReadOnlyList<string>? explanations = null) =>
        new(new OosSummary("11c0de175691b2bb", "11c0de175691b2bb99999999aaaaaaaa", date, 100, 120,
                categories ?? ["RANDOM_COUNT", "SAVE_HASH"],
                machines ?? [Machine(1), Machine(2, "Client", player: "Bob")],
                warnings ?? [], sessionKind),
            leads ?? [], causes ?? [], diffAvailable,
            dumps ?? [new ReportDump(1, @"C:\d\oos_1.zip", false, 1000, false), new ReportDump(2, @"C:\d\oos_2.zip", false, 2000, false)],
            explanations ?? []);

    private static string Value(IReadOnlyList<ParadoxFormEntry> entries, string label) =>
        entries.Single(e => e.Label == label).Value;

    private static ReportCause Cause(string path, params FieldDiff[] fields) =>
        new(path, UnitChange.Changed, fields);

    [Fact]
    public void Every_eu5_field_is_built_in_profile_order()
    {
        var entries = ParadoxFormReport.Build(Facts(causes: [Cause("countries/12")]), Eu5Form);

        Assert.Equal(Eu5Form.Select(f => f.Label), entries.Select(e => e.Label));
        Assert.Equal(Eu5Form.Select(f => f.Kind), entries.Select(e => e.Kind));
        Assert.Equal(
            "Verify the game files in Steam (Properties > Installed Files > Verify integrity) on every machine before posting, then select Yes.",
            entries[0].Value);
        Assert.Equal("Yes", entries[1].Value);
        Assert.Equal("Out of Sync (RANDOM_COUNT, SAVE_HASH) at 1425.2.4.8 in multiplayer \u2014 countries/12 diverges", entries[2].Value);
        Assert.Contains("Out of Sync in multiplayer (session start: preparation).", entries[3].Value);
        Assert.Equal("Pick the type closest to the affected feature; there is no dedicated type for desyncs.", entries[4].Value);
        Assert.Equal(
            "1. Host a multiplayer game with 2 players (players joined in the lobby).\n" +
            "2. Play at speed <game speed> until <what happened right before the desync>.\n" +
            "3. Out of Sync is detected at game date 1425.2.4.8.\n" +
            "4. <Does it happen again after rehosting or reloading? yes/no>",
            entries[5].Value);
        Assert.Equal("1.0.3", entries[6].Value);
        Assert.Equal("Out of Sync", entries[7].Value);
        Assert.Equal("Attach both OOS dump folders as zip files.", entries[8].Value);
        Assert.Equal(2, entries[8].Attachments.Count);
        Assert.Empty(entries[0].Attachments);
        Assert.Equal("Optional: a screenshot of the Out of Sync message.", entries[9].Value);
    }

    [Fact]
    public void Summary_is_capped_at_150_characters_with_an_ellipsis()
    {
        var facts = Facts(causes: [Cause(new string('x', 300))]);

        var summary = ParadoxFormReport.SummaryLine(facts);

        Assert.Equal(ParadoxFormReport.MaxSummaryLength, summary.Length);
        Assert.EndsWith("\u2026", summary);
        Assert.StartsWith("Out of Sync (RANDOM_COUNT, SAVE_HASH) at 1425.2.4.8 in multiplayer \u2014 xxx", summary);
    }

    [Fact]
    public void Summary_without_a_cause_or_date_is_short_and_says_unknown_date()
    {
        var summary = ParadoxFormReport.SummaryLine(Facts(date: null));

        Assert.Equal("Out of Sync (RANDOM_COUNT, SAVE_HASH) at an unknown date in multiplayer", summary);
    }

    [Fact]
    public void Summary_names_the_first_cause_with_a_path_when_the_top_level_comes_first()
    {
        var summary = ParadoxFormReport.SummaryLine(Facts(causes: [Cause(""), Cause("countries/12")]));

        Assert.Equal("Out of Sync (RANDOM_COUNT, SAVE_HASH) at 1425.2.4.8 in multiplayer \u2014 countries/12 diverges", summary);
    }

    [Fact]
    public void Summary_has_no_cause_suffix_when_only_the_top_level_diverges()
    {
        var summary = ParadoxFormReport.SummaryLine(Facts(causes: [Cause(""), Cause("  ")]));

        Assert.Equal("Out of Sync (RANDOM_COUNT, SAVE_HASH) at 1425.2.4.8 in multiplayer", summary);
    }

    [Fact]
    public void A_top_level_cause_is_called_top_level_in_the_description()
    {
        var cause = new ReportCause("", UnitChange.OwnScalarsChanged, [new FieldDiff("random_count", "1", "2", FieldChangeKind.Changed)]);

        var description = Value(ParadoxFormReport.Build(Facts(causes: [cause]), Eu5Form), "Description");

        Assert.Contains("\n- top level (own values changed): random_count = 1 vs 2\n", description);
    }

    [Fact]
    public void Mods_disabled_is_yes_only_when_no_machine_has_mods()
    {
        var mod = new DumpMod("Some Mod", ModSourceKind.Workshop, "123", null, null);
        var withMods = Facts(machines: [Machine(1), Machine(2, "Client", mods: [mod])]);

        Assert.Equal("Yes", Value(ParadoxFormReport.Build(Facts(), Eu5Form), "I have disabled all mods"));
        Assert.Equal("No", Value(ParadoxFormReport.Build(withMods, Eu5Form), "I have disabled all mods"));
    }

    [Fact]
    public void Mods_disabled_is_no_when_a_machines_dump_is_missing()
    {
        var facts = Facts(dumps: [new ReportDump(1, @"C:\d\oos_1.zip", false, 1000, false), new ReportDump(2, null, false, 0, true)]);

        Assert.Equal("No", Value(ParadoxFormReport.Build(facts, Eu5Form), "I have disabled all mods"));
    }

    [Fact]
    public void Differing_versions_are_both_named()
    {
        var facts = Facts(machines: [Machine(1, version: "1.0.3"), Machine(2, "Client", version: "1.0.2")]);

        var version = Value(ParadoxFormReport.Build(facts, Eu5Form), "Game Version");

        Assert.Equal("1.0.3 (machine 1) / 1.0.2 (machine 2) \u2014 versions differ", version);
    }

    [Fact]
    public void The_host_is_the_reference_even_with_a_higher_machine_id()
    {
        var facts = Facts(machines: [Machine(1, "Client", version: "1.0.2"), Machine(2, "Host", version: "1.0.3")]);

        var version = Value(ParadoxFormReport.Build(facts, Eu5Form), "Game Version");

        Assert.Equal("1.0.3 (machine 2) / 1.0.2 (machine 1) \u2014 versions differ", version);
    }

    [Fact]
    public void Without_a_diff_the_description_says_state_comparison_not_run()
    {
        var description = Value(ParadoxFormReport.Build(Facts(diffAvailable: false), Eu5Form), "Description");

        Assert.Contains("State comparison not run.", description);
        Assert.DoesNotContain("State comparison (top cause candidates", description);
        Assert.Contains("No leads in the 7 game days before the OOS.", description);
    }

    [Fact]
    public void An_available_diff_without_causes_says_no_cause_candidates()
    {
        var description = Value(ParadoxFormReport.Build(Facts(diffAvailable: true, causes: []), Eu5Form), "Description");

        Assert.Contains("State comparison found no cause candidates.", description);
        Assert.DoesNotContain("State comparison (top cause candidates", description);
        Assert.DoesNotContain("State comparison not run.", description);
    }

    [Fact]
    public void A_cause_path_with_line_breaks_is_one_line_in_the_description()
    {
        var description = Value(ParadoxFormReport.Build(Facts(causes: [Cause("countries/1\n2")]), Eu5Form), "Description");

        Assert.Contains("- countries/1 2 (changed)", description);
    }

    [Fact]
    public void An_empty_game_date_counts_as_unknown()
    {
        var entries = ParadoxFormReport.Build(Facts(date: ""), Eu5Form);

        Assert.Contains("Detected at game date unknown,", Value(entries, "Description"));
        Assert.Contains("3. Out of Sync is detected at game date unknown.", Value(entries, "Steps to reproduce"));
        Assert.Contains("at an unknown date in multiplayer", Value(entries, "Summary"));
    }

    [Fact]
    public void Every_warning_kind_is_written_as_words()
    {
        var warnings = new List<SummaryWarning>
        {
            new(SummaryWarningKind.DifferentGameCommit, "1=a, 2=b"),
            new(SummaryWarningKind.DifferentEngineCommit, "1=c, 2=d"),
            new(SummaryWarningKind.DifferentDlcs, "Dlc X"),
            new(SummaryWarningKind.DifferentMods, "Mod Y"),
            new(SummaryWarningKind.MissingMachine, "2"),
        };

        var description = Value(ParadoxFormReport.Build(Facts(warnings: warnings), Eu5Form), "Description");

        Assert.Contains("- Difference: game build: 1=a, 2=b\n", description);
        Assert.Contains("- Difference: engine build: 1=c, 2=d\n", description);
        Assert.Contains("- Difference: DLCs: Dlc X\n", description);
        Assert.Contains("- Difference: mods: Mod Y\n", description);
        Assert.Contains("- Difference: missing machine: 2\n", description);
    }

    [Fact]
    public void A_summary_of_exactly_150_characters_is_not_cut()
    {
        const string prefix = "Out of Sync (RANDOM_COUNT, SAVE_HASH) at 1425.2.4.8 in multiplayer — ";
        const string suffix = " diverges";
        var path = new string('x', ParadoxFormReport.MaxSummaryLength - prefix.Length - suffix.Length);

        var exact = ParadoxFormReport.SummaryLine(Facts(causes: [Cause(path)]));
        var oneOver = ParadoxFormReport.SummaryLine(Facts(causes: [Cause(path + "x")]));

        Assert.Equal(prefix + path + suffix, exact);
        Assert.Equal(ParadoxFormReport.MaxSummaryLength, exact.Length);
        Assert.DoesNotContain("…", exact);
        Assert.Equal(ParadoxFormReport.MaxSummaryLength, oneOver.Length);
        Assert.EndsWith("…", oneOver);
    }

    [Fact]
    public void A_missing_machine_is_named_in_description_and_attachments()
    {
        var dumps = new List<ReportDump> { new(1, @"C:\d\oos_1.zip", false, 1000, false), new(2, null, false, 0, true) };
        var facts = Facts(dumps: dumps);

        var entries = ParadoxFormReport.Build(facts, Eu5Form);

        Assert.Contains("Only the dump of machine 1 is available; the dump of machine 2 is missing.", Value(entries, "Description"));
        Assert.DoesNotContain("Both OOS dumps", Value(entries, "Description"));
        Assert.Contains(entries.Single(e => e.Kind == FormFieldKind.Attachments).Attachments, d => d.Missing && d.MachineId == 2);
    }

    [Fact]
    public void Steps_use_hotjoin_or_lobby_from_the_session_kind()
    {
        string FirstStep(string? kind) =>
            Value(ParadoxFormReport.Build(Facts(sessionKind: kind), Eu5Form), "Steps to reproduce").Split('\n')[0];

        Assert.Equal("1. Host a multiplayer game with 2 players (player joined during the game (hotjoin)).", FirstStep("hotjoin"));
        Assert.Equal("1. Host a multiplayer game with 2 players (players joined in the lobby).", FirstStep("preparation"));
        Assert.Equal("1. Host a multiplayer game with 2 players (players joined in the lobby).", FirstStep(null));
    }

    [Fact]
    public void The_description_lists_every_section_in_order()
    {
        var mod = new DumpMod("Flogi Fixes", ModSourceKind.Workshop, "3000", null, null);
        var local = new DumpMod("Mine", ModSourceKind.Local, null, "my_mod", null);
        var facts = Facts(
            machines: [Machine(1, mods: [mod, local]), Machine(2, null, "1.0.3", [], player: null, dlcs: [])],
            causes: [Cause("countries/12",
                new FieldDiff("gold", "10", "12", FieldChangeKind.Changed),
                new FieldDiff("flag", "yes", null, FieldChangeKind.OnlyReference))],
            leads: [
                new ReportLead([1], "1425.2.3.1", "Error in\nevent", "events/a.txt", null, 7, "Flogi Fixes", true, LeadKind.ScriptError),
                new ReportLead([1, 2], null, "Plain", null, null, null, null, false, LeadKind.ScriptError)],
            warnings: [new SummaryWarning(SummaryWarningKind.DifferentMods, "Mine")],
            explanations: ["Random count explanation."]);

        var description = Value(ParadoxFormReport.Build(facts, Eu5Form), "Description");

        Assert.Equal(
            "Out of Sync in multiplayer (session start: preparation).\n" +
            "OOS id: 11c0de175691b2bb99999999aaaaaaaa. Detected at game date 1425.2.4.8, frames 100\u2013120.\n" +
            "Checksum categories that differed: RANDOM_COUNT, SAVE_HASH.\n" +
            "Random count explanation.\n" +
            "\n" +
            "Environment:\n" +
            "- Machine 1 (Host, Alice): version 1.0.3, game 0123456789, engine fedcba9876\n" +
            "  DLCs: Dlc A, Dlc B\n" +
            "  Mods: Flogi Fixes (Workshop 3000), Mine (local folder my_mod)\n" +
            "- Machine 2 (unknown role): version 1.0.3, game 0123456789, engine fedcba9876\n" +
            "  DLCs: none\n" +
            "  Mods: none\n" +
            "- Difference: mods: Mine\n" +
            "\n" +
            "State comparison (top cause candidates, reference vs desynced):\n" +
            "- countries/12 (changed): gold = 10 vs 12; flag = yes vs (absent)\n" +
            "\n" +
            "Leads in the 7 game days before the OOS:\n" +
            "- 1425.2.3.1 machine 1: Error in event (events/a.txt:7) [Flogi Fixes]\n" +
            "- unknown date machines 1, 2: Plain\n" +
            "\n" +
            "Both OOS dumps (all machines) are attached.",
            description);
    }

    [Fact]
    public void Unknown_values_print_unknown_and_empty_sections_are_left_out()
    {
        var machine = new MachineSummary(1, "Host", null, null, null, new SaveHeader(null, null, null, null, []), [], false, false);
        var facts = Facts(machines: [machine], categories: [], date: null, sessionKind: null, diffAvailable: false);

        var description = Value(ParadoxFormReport.Build(facts, Eu5Form), "Description");

        Assert.Contains("(session start: unknown)", description);
        Assert.Contains("Detected at game date unknown, frames 100\u2013120.", description);
        Assert.DoesNotContain("Checksum categories", description);
        Assert.Contains("- Machine 1 (Host): version unknown, game unknown, engine unknown", description);
    }

    [Fact]
    public void One_line_values_collapse_line_breaks_and_fixed_values_pass_through()
    {
        var form = new List<ParadoxFormField> { new("Fixed", FormFieldKind.Text, FormSource.Fixed, "a\r\nb\nc") };

        var entries = ParadoxFormReport.Build(Facts(), form);

        Assert.Equal("a b c", entries[0].Value);
    }
}
