using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class CompatReportTests
{
    private static readonly CompatParticipant A = new("A", "Mod A", "C:/a", 1, false);
    private static readonly CompatParticipant B = new("B", "Mod B", "C:/b", 2, false);

    private static CompatContribution Contribution(CompatParticipant p, OverrideOperator op = OverrideOperator.None, int line = 0) =>
        new(p, "in_game/common/x/a.txt", "C:/x", op, "farm", null, line, null, OverrideBehavior.PerKey, true);

    private static CompatFinding Conflict() => new()
    {
        Severity = CompatSeverity.Conflict,
        Kind = CompatKind.KeyConflict,
        RelativePath = "in_game/common/x/a.txt",
        Key = "farm",
        Contributions = new[] { Contribution(A, OverrideOperator.None, 12), Contribution(B, OverrideOperator.Replace) },
        Winner = B,
        Suggestion = CompatSuggestion.NeedsCompatch,
        Message = "Beide definieren farm.",
        Uncertain = true,
        Note = "Gleiches Projekt.",
    };

    private static CompatFinding Info() => new()
    {
        Severity = CompatSeverity.Info,
        Kind = CompatKind.FileIdentical,
        RelativePath = "in_game/gfx/i.dds",
        Message = "Inhaltsgleich.",
    };

    [Fact]
    public void Report_lists_counts_and_sections_in_severity_order()
    {
        var text = CompatReport.Build(new[] { Info(), Conflict() });
        Assert.Contains("# Kompatibilitätsbericht", text);
        Assert.Contains("1 Konflikte, 0 Hinweise, 1 Infos", text);
        Assert.True(text.IndexOf("## Konflikte", StringComparison.Ordinal) < text.IndexOf("## Infos", StringComparison.Ordinal));
        Assert.DoesNotContain("## Hinweise", text);
    }

    [Fact]
    public void Finding_shows_key_winner_suggestion_uncertainty_note_and_contributions()
    {
        var text = CompatReport.Build(new[] { Conflict() });
        Assert.Contains("### Schlüsselkonflikt — in_game/common/x/a.txt `farm` (unsicher)", text);
        Assert.Contains("Beide definieren farm.", text);
        Assert.Contains("Gewinner: Mod B", text);
        Assert.Contains("Empfehlung: Kompatibilitätspatch nötig", text);
        Assert.Contains("Hinweis: Gleiches Projekt.", text);
        Assert.Contains("- Mod A: Definition — in_game/common/x/a.txt:12", text);
        Assert.Contains("- Mod B: REPLACE: — in_game/common/x/a.txt", text);
    }

    [Fact]
    public void Min_severity_filters_findings_and_counts_only_shown_ones()
    {
        var text = CompatReport.Build(new[] { Info(), Conflict() }, new CompatReportOptions(CompatSeverity.Conflict));
        Assert.DoesNotContain("Inhaltsgleich.", text);
        Assert.Contains("1 Konflikte, 0 Hinweise, 0 Infos", text);
    }

    [Fact]
    public void Empty_report_says_no_findings_and_shows_title_and_subtitle()
    {
        var text = CompatReport.Build(Array.Empty<CompatFinding>(), new CompatReportOptions(Title: "Mein Bericht", Subtitle: "Mod A gegen Alle"));
        Assert.Contains("# Mein Bericht", text);
        Assert.Contains("Mod A gegen Alle", text);
        Assert.Contains("Keine Funde.", text);
    }

    [Fact]
    public void Text_helpers_cover_every_enum_value()
    {
        foreach (var kind in Enum.GetValues<CompatKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(CompatText.Kind(kind)));
        }

        foreach (var severity in Enum.GetValues<CompatSeverity>())
        {
            Assert.False(string.IsNullOrWhiteSpace(CompatText.Severity(severity)));
        }

        foreach (var suggestion in Enum.GetValues<CompatSuggestion>().Where(s => s != CompatSuggestion.None))
        {
            Assert.False(string.IsNullOrWhiteSpace(CompatText.Suggestion(suggestion)));
        }

        Assert.Equal(string.Empty, CompatText.Suggestion(CompatSuggestion.None));
        foreach (var op in Enum.GetValues<OverrideOperator>().Where(o => o != OverrideOperator.None))
        {
            Assert.EndsWith(":", CompatText.Operator(op));
        }
    }

    [Fact]
    public void Behavior_text_covers_every_behavior()
    {
        foreach (var behavior in Enum.GetValues<OverrideBehavior>())
        {
            Assert.False(string.IsNullOrWhiteSpace(CompatText.Behavior(behavior)));
        }
    }
}
