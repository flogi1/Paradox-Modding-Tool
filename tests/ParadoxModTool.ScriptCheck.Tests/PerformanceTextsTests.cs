using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class PerformanceTextsTests
{
    private static Finding Make(PerformanceRate rate, string[] path, string? inner = null, string[]? narrower = null, string iterator = "every_country", string file = "events/m.txt", int line = 7) =>
        new(FindingKind.ExpensiveIterator, FindingSeverity.Warning, iterator, CheckKind.Effect, new SiteLocation("C:/mod", file, line), path, "ExpensiveIterator|" + file + "|" + iterator)
        {
            Performance = new PerformanceInfo(rate, path, inner, narrower ?? Array.Empty<string>(), new[] { line }),
        };

    [Fact]
    public void PathJoinsWithArrow()
    {
        var f = Make(PerformanceRate.Monthly, new[] { "on_monthly_pulse", "m.1", "m_eff" });

        Assert.Equal("on_monthly_pulse \u2192 m.1 \u2192 m_eff", PerformanceTexts.Path(f.Performance!));
    }

    [Fact]
    public void SuggestionsForPulseWithNarrower()
    {
        var f = Make(PerformanceRate.Monthly, new[] { "p" }, narrower: new[] { "every_neighboring_country", "every_realm_county" });

        Assert.Equal(
            new[]
            {
                "Engeren Iterator nutzen: every_neighboring_country, every_realm_county",
                "Billige Prüfung vor den Iterator ziehen",
                "Ergebnis in Variable oder Liste zwischenspeichern",
                "Arbeit auf einen selteneren Puls legen",
            },
            PerformanceTexts.Suggestions(f));
    }

    [Fact]
    public void SuggestionsForCheckedHaveNoPulseHint()
    {
        var f = Make(PerformanceRate.Checked, new[] { "p" });

        Assert.Equal(2, PerformanceTexts.Suggestions(f).Count);
        Assert.DoesNotContain(PerformanceTexts.Suggestions(f), s => s.Contains("Puls"));
    }

    [Fact]
    public void LevelNested()
    {
        Assert.Equal("Verschachtelt", PerformanceTexts.Level(Make(PerformanceRate.Daily, new[] { "p" }, inner: "every_province")));
        Assert.Equal("Warnung", PerformanceTexts.Level(Make(PerformanceRate.Daily, new[] { "p" })));
    }

    [Fact]
    public void ReportBlocks()
    {
        var plain = Make(PerformanceRate.Monthly, new[] { "on_monthly_pulse", "m.1" }, narrower: new[] { "every_neighboring_country" });
        var nested = Make(PerformanceRate.Checked, new[] { "a (b)" }, inner: "every_province", iterator: "any_country", file: "common/x.txt", line: 3);
        var other = Finding.Create(FindingKind.DeadEvent, FindingSeverity.Hint, "zzz_dead", CheckKind.Event, new SiteLocation("C:/mod", "events/d.txt", 1), Array.Empty<string>());

        var text = PerformanceTexts.Report(new[] { plain, other, nested }, "Meine Mod", "CK III");
        var lines = text.Replace("\r\n", "\n").Split('\n');

        Assert.Equal("Leistungsbefunde: Meine Mod (CK III), 2 Funde", lines[0]);
        Assert.Contains("Warnung | every_country | events/m.txt:7", lines);
        Assert.Contains("Verschachtelt | any_country | common/x.txt:3", lines);
        Assert.Contains("  Weg: on_monthly_pulse \u2192 m.1", lines);
        Assert.Contains("  Takt: monatlich", lines);
        Assert.Contains("  Takt: ständig geprüft", lines);
        Assert.Contains("  Vorschlag: Engeren Iterator nutzen: every_neighboring_country", lines);
        Assert.Equal(new[] { "  Innerer Iterator: every_province" }, lines.Where(l => l.StartsWith("  Innerer Iterator", StringComparison.Ordinal)));
        Assert.DoesNotContain("zzz_dead", text);
    }
}
