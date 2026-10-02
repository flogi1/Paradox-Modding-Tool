using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class ModAuthorReportTests
{
    private static MachineSummary Machine(
        int id, string? role = "Host", string? version = "1.0.3", IReadOnlyList<DumpMod>? mods = null) =>
        new(id, role, "Alice", "0123456789abcdef", "fedcba9876543210",
            new SaveHeader("1425.2.4.8", version, 1, 2, ["Dlc A"]), mods ?? [], false, false);

    private static ReportFacts Facts(
        IReadOnlyList<ReportLead>? leads = null,
        IReadOnlyList<ReportCause>? causes = null,
        bool diffAvailable = true,
        IReadOnlyList<MachineSummary>? machines = null,
        IReadOnlyList<SummaryWarning>? warnings = null) =>
        new(new OosSummary("11c0de175691b2bb", "11c0de175691b2bb99999999aaaaaaaa", "1425.2.4.8", 100, 120,
                ["SAVE_HASH"], machines ?? [Machine(1), Machine(2, "Client")], warnings ?? [], "preparation"),
            leads ?? [], causes ?? [], diffAvailable, [], []);

    private static ReportLead Lead(
        string message = "Error in effect", string? file = "common/foo.txt", int? line = 12,
        string? mod = "Cool Mod", string? date = "1425.2.4.7", int machine = 2) =>
        new([machine], date, message, file, null, line, mod, true, LeadKind.ScriptError);

    private static ReportCause Cause(string path, params FieldDiff[] fields) =>
        new(path, UnitChange.Changed, fields);

    private static FieldDiff Field(string path, string? reference, string? desynced) =>
        new(path, reference, desynced, FieldChangeKind.Changed);

    [Fact]
    public void Sections_appear_in_order_with_leads_as_file_line()
    {
        var facts = Facts(
            leads: [Lead(), Lead("Second", "events/bar.txt", 3, "Other Mod", "1425.2.4.6", machine: 1)],
            causes: [Cause("countries/12", Field("gold", "10", "11"), Field("army/size", "4", "5")), Cause("provinces/7")],
            warnings: [new SummaryWarning(SummaryWarningKind.DifferentDlcs, "Dlc B")],
            machines: [Machine(1, mods: [new DumpMod("Cool Mod", ModSourceKind.Workshop, "123", null, null)]), Machine(2)]);

        var report = ModAuthorReport.Build(facts,
        [
            new AdviceRule(AdviceTrigger.Checksum, "SAVE_HASH", "Check your mods."),
            new AdviceRule(AdviceTrigger.Checksum, "OTHER", "Never shown."),
            new AdviceRule(AdviceTrigger.Warning, "DifferentDlcs", "Install the same DLCs: {detail}"),
        ]);

        Assert.Equal(
            "# Desync (OOS) in Cool Mod, Other Mod\n" +
            "\n" +
            "## Where it comes from\n" +
            "- `common/foo.txt:12` (Cool Mod) \u2014 Error in effect \u2014 machine 2, 1425.2.4.7\n" +
            "- `events/bar.txt:3` (Other Mod) \u2014 Second \u2014 machine 1, 1425.2.4.6\n" +
            "\n" +
            "**State differences (top cause candidates):**\n" +
            "- countries/12 \u2014 gold: `10` vs `11`; army/size: `4` vs `5`\n" +
            "- provinces/7 \u2014 changed\n" +
            "\n" +
            "## What to do\n" +
            "- Check your mods.\n" +
            "- Install the same DLCs: Dlc B\n" +
            "\n" +
            "## Environment\n" +
            "- Game 1.0.3 (game 0123456789, engine fedcba9876)\n" +
            "- DLCs: differ: Dlc B\n" +
            "- Mods: machine 1: Cool Mod (Workshop 123); machine 2: none\n" +
            "\n" +
            "The full OOS dumps of all machines are available on request.",
            report);
        Assert.Equal("Desync (OOS) in Cool Mod, Other Mod", ModAuthorReport.Title(facts));
    }

    [Fact]
    public void Leads_without_file_have_no_location_and_title_says_unknown_mod()
    {
        var facts = Facts(leads: [Lead("Mismatch", file: null, line: null, mod: null, date: null)]);

        var report = ModAuthorReport.Build(facts, []);

        Assert.Equal("Desync (OOS) in unknown mod", ModAuthorReport.Title(facts));
        Assert.StartsWith("# Desync (OOS) in unknown mod\n", report);
        Assert.Contains("\n- (vanilla/unknown) \u2014 Mismatch \u2014 machine 2, unknown\n", report);
        Assert.DoesNotContain("`:", report);
        Assert.Contains("\n## What to do\n- No specific advice; compare the leads above.\n", report);
        Assert.Contains("- DLCs: identical\n", report);
    }

    [Fact]
    public void Values_with_backticks_or_line_breaks_stay_on_one_line()
    {
        var longValue = new string('x', 300);
        var facts = Facts(
            leads: [Lead("Bad `thing`\r\nhappened", mod: "My `Mod`")],
            causes: [Cause("a/1", Field("f", "one `two`\nthree", longValue))]);

        var report = ModAuthorReport.Build(facts, []);

        Assert.Contains("(My 'Mod') \u2014 Bad 'thing' happened \u2014 machine 2", report);
        Assert.Contains("- a/1 \u2014 f: `one 'two' three` vs `" + new string('x', 119) + "\u2026`\n", report);
        Assert.DoesNotContain(longValue, report);
    }

    [Fact]
    public void Markdown_characters_are_escaped_outside_code_spans_only()
    {
        var facts = Facts(
            leads: [Lead("Undefined <unknown> *x* [a]_b#c\\d", file: "common/a_b*.txt", mod: "Mod *x*")],
            causes: [Cause("countries/<unknown>", Field("my_field", "<v>", "*w*"))],
            machines: [Machine(1, version: "1.0_<3>", mods: [new DumpMod("Mod *x*", ModSourceKind.Workshop, "1", null, null)])],
            warnings: [new SummaryWarning(SummaryWarningKind.DifferentDlcs, "<unknown>")]);

        var report = ModAuthorReport.Build(facts, [new AdviceRule(AdviceTrigger.Checksum, "SAVE_HASH", "Check *these* <places>")]);

        Assert.Equal("Desync (OOS) in Mod *x*", ModAuthorReport.Title(facts));
        Assert.StartsWith("# Desync (OOS) in Mod \\*x\\*\n", report);
        Assert.Contains(
            "\n- `common/a_b*.txt:12` (Mod \\*x\\*) \u2014 Undefined \\<unknown\\> \\*x\\* \\[a\\]\\_b\\#c\\\\d \u2014 machine 2, 1425.2.4.7\n",
            report);
        Assert.Contains("\n- countries/\\<unknown\\> \u2014 my\\_field: `<v>` vs `*w*`\n", report);
        Assert.Contains("\n- Check \\*these\\* \\<places\\>\n", report);
        Assert.Contains("\n- Game 1.0\\_\\<3\\> (game", report);
        Assert.Contains("\n- DLCs: differ: \\<unknown\\>\n", report);
        Assert.Contains("\n- Mods: machine 1: Mod \\*x\\* (Workshop 1)", report);
    }

    [Fact]
    public void A_lead_seen_on_several_machines_names_them_all()
    {
        var lead = Lead() with { MachineIds = [1, 2] };

        var report = ModAuthorReport.Build(Facts(leads: [lead]), []);

        Assert.Contains("(Cool Mod) \u2014 Error in effect \u2014 machines 1, 2, 1425.2.4.7\n", report);
    }

    [Fact]
    public void A_top_level_cause_is_called_top_level()
    {
        var facts = Facts(causes: [Cause("", Field("random_count", "1", "2")), Cause(" ")]);

        var report = ModAuthorReport.Build(facts, []);

        Assert.Contains("\n- top level \u2014 random\\_count: `1` vs `2`\n- top level \u2014 changed\n", report);
    }

    [Fact]
    public void Without_diff_it_says_state_comparison_not_run()
    {
        var report = ModAuthorReport.Build(Facts(diffAvailable: false), []);

        Assert.Contains("\nState comparison not run.\n", report);
        Assert.DoesNotContain("State differences", report);
    }

    [Fact]
    public void With_diff_but_no_causes_it_says_so_and_unknown_environment_prints_unknown()
    {
        var report = ModAuthorReport.Build(Facts(machines: []), []);

        Assert.Contains("\nState comparison found no cause candidates.\n", report);
        Assert.Contains("- Game unknown (game unknown, engine unknown)\n", report);
        Assert.Contains("- Mods: unknown\n", report);
    }
}
