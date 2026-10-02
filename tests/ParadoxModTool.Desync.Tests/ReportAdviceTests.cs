using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class ReportAdviceTests
{
    private static ReportFacts Facts(
        IReadOnlyList<SummaryWarning>? warnings = null,
        IReadOnlyList<string>? categories = null,
        IReadOnlyList<ReportLead>? leads = null) =>
        new(new OosSummary("11c0de175691b2bb", null, null, null, null, categories ?? [], [], warnings ?? []),
            leads ?? [], [], false, [], []);

    private static ReportLead Lead(string? file, int? line, string? mod) =>
        new([1], null, "m", file, null, line, mod, false, LeadKind.ScriptError);

    [Fact]
    public void Each_rule_fires_only_on_its_condition_and_once()
    {
        var rules = new List<AdviceRule>
        {
            new(AdviceTrigger.Warning, nameof(SummaryWarningKind.DifferentMods), "mods"),
            new(AdviceTrigger.Warning, nameof(SummaryWarningKind.DifferentDlcs), "dlcs"),
            new(AdviceTrigger.Checksum, "RANDOM_COUNT", "random"),
            new(AdviceTrigger.Checksum, "OTHER", "other"),
            new(AdviceTrigger.LeadInMod, null, "lead"),
        };
        var facts = Facts(
            // Zwei Warnungen derselben Art: die Regel löst trotzdem nur einmal aus.
            [new(SummaryWarningKind.DifferentMods, "A"), new(SummaryWarningKind.DifferentMods, "B")],
            ["RANDOM_COUNT"],
            [Lead("common/x.txt", 1, "M1"), Lead("common/y.txt", 2, "M2")]);

        Assert.Equal(["mods", "random", "lead"], ReportAdvice.For(facts, rules));
    }

    [Fact]
    public void Detail_and_leads_placeholders_are_filled()
    {
        var rules = new List<AdviceRule>
        {
            new(AdviceTrigger.Warning, nameof(SummaryWarningKind.DifferentMods), "Mods differ: {detail}"),
            new(AdviceTrigger.LeadInMod, null, "Check {leads}"),
            new(AdviceTrigger.Checksum, "RANDOM_COUNT", "Unknown {detail} and {other} stay"),
        };
        var facts = Facts(
            [new(SummaryWarningKind.DifferentMods, "Alpha, Beta")],
            ["RANDOM_COUNT"],
            [Lead("common/x.txt", 92, "Flogis Quick Fixes"), Lead("events/e.txt", null, "Other Mod")]);

        Assert.Equal(
            [
                "Mods differ: Alpha, Beta",
                "Check common/x.txt:92 (Flogis Quick Fixes), events/e.txt (Other Mod)",
                "Unknown {detail} and {other} stay",
            ],
            ReportAdvice.For(facts, rules));
    }

    [Fact]
    public void Lead_list_is_distinct_and_capped_at_five()
    {
        var leads = new List<ReportLead> { Lead("a.txt", 1, "M"), Lead("a.txt", 1, "M"), Lead("plain.txt", 3, null) };
        for (var i = 2; i <= 7; i++)
            leads.Add(Lead($"f{i}.txt", i, "M"));
        var rules = new List<AdviceRule> { new(AdviceTrigger.LeadInMod, null, "{leads}") };

        var advice = ReportAdvice.For(Facts(leads: leads), rules);

        Assert.Equal(["a.txt:1 (M), f2.txt:2 (M), f3.txt:3 (M), f4.txt:4 (M), f5.txt:5 (M)"], advice);
    }

    [Fact]
    public void No_matching_rule_gives_no_advice()
    {
        var rules = new List<AdviceRule>
        {
            new(AdviceTrigger.Warning, nameof(SummaryWarningKind.MissingMachine), "x"),
            new(AdviceTrigger.Checksum, "RANDOM_COUNT", "y"),
            new(AdviceTrigger.LeadInMod, null, "z"),
        };
        // Ein Hinweis aus dem Spiel (ohne Mod) löst LeadInMod nicht aus.
        var facts = Facts(leads: [Lead("game/x.txt", 1, null)]);

        Assert.Empty(ReportAdvice.For(facts, rules));
        Assert.Empty(ReportAdvice.For(facts, []));
    }
}
