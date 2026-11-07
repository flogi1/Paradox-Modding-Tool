using System.Globalization;
using Xunit;
using static ParadoxModTool.Performance.Tests.RuleTestData;

namespace ParadoxModTool.Performance.Tests;

public sealed class MeasurementRulesTests
{
    private const string ModValues = "common/script_values/mod_values.txt";
    private const string BothFile = "common/scripted_effects/both_effects.txt";
    private const string ModOnly = "common/scripted_effects/mod_effects.txt";
    private const string VanillaFile = "common/script_values/van_values.txt";

    private static readonly OriginClassifier Origins = Classifier(
        new[] { ModValues, BothFile, ModOnly },
        new[] { BothFile, VanillaFile });

    private static RuleContext Context(ProfilingDump dump, int? months = 12, ErrorHotspots? errors = null, RuleThresholds? thresholds = null) =>
        new(
            new ProfilingSummary(dump, Origins),
            new RunDuration(months, "pulse", months is not null),
            Origins,
            settings: null,
            errors,
            _ => null,
            thresholds ?? new RuleThresholds());

    // ManyCallsRule

    [Fact]
    public void ManyCallsRuleFiresAboveTheYearlyThresholdOnly()
    {
        var context = Context(Dump(
            $"script_value @ {ModValues}:5;100000;1;2",
            $"script_value @ {ModValues}:9;100001;1;3",
            $"named_script_value @ {ModValues}:12;100001;1;4",
            $"trigger @ {ModValues}:20;999999;1;5",
            $"script_value @ {VanillaFile}:3;999999;1;6"));

        var hints = new ManyCallsRule().Check(context).OrderBy(h => h.Line).ToList();

        Assert.Equal(new[] { (9, 3.0), (12, 4.0) }, hints.Select(h => (h.Line, h.Value)));
        Assert.All(hints, h =>
        {
            Assert.Equal("many-calls", h.RuleId);
            Assert.Equal(HintSeverity.Info, h.Severity);
            Assert.Equal(ModValues, h.Path);
            Assert.Equal(PerformanceTexts.HintManyCallsTitle, h.Title);
            Assert.Equal(PerformanceTexts.HintManyCallsDetail(100_001), h.Detail);
        });
    }

    [Theory]
    [InlineData(24, 200000, 200002, 100_001.0)]
    [InlineData(6, 50000, 50001, 100_002.0)]
    [InlineData(0, 100000, 100001, 100_001.0)]
    public void ManyCallsRuleScalesCallsToAYear(int months, long below, long above, double callsPerYear)
    {
        var context = Context(Dump($"script_value @ {ModValues}:5;{below};1;2", $"script_value @ {ModValues}:6;{above};1;7.5"), months: months);

        var hint = Assert.Single(new ManyCallsRule().Check(context));

        Assert.Equal(6, hint.Line);
        Assert.Equal(7.5, hint.Value, 6);
        Assert.Equal(PerformanceTexts.HintManyCallsDetail(callsPerYear), hint.Detail);
    }

    [Fact]
    public void ManyCallsRuleUsesRawCallsWhenMonthsAreUnknown()
    {
        var context = Context(Dump($"script_value @ {ModValues}:5;100000;1;2", $"script_value @ {ModValues}:6;100001;1;9"), months: null);

        var hint = Assert.Single(new ManyCallsRule().Check(context));

        Assert.Equal(6, hint.Line);
        Assert.Equal(9, hint.Value, 6);
        Assert.Equal(PerformanceTexts.HintManyCallsDetail(100_001), hint.Detail);
    }

    [Fact]
    public void ManyCallsRuleHonoursTheThreshold()
    {
        var context = Context(Dump($"script_value @ {ModValues}:5;11;1;2", $"script_value @ {ModValues}:6;10;1;2"), thresholds: new RuleThresholds(ManyCallsPerYear: 10));

        Assert.Equal(5, Assert.Single(new ManyCallsRule().Check(context)).Line);
    }

    // OverriddenVanillaFileRule

    [Fact]
    public void VanillaCopyRuleFiresOncePerOverriddenFileWithTheHighestTotal()
    {
        var context = Context(Dump(
            $"effect @ {BothFile}:7;5;1;6",
            $"effect @ {BothFile}:30;5;1;8",
            $"trigger @ {BothFile}:30;5;1;2",
            $"effect @ {ModOnly}:4;5;1;50",
            $"effect @ {VanillaFile}:4;5;1;50"));

        var hint = Assert.Single(new OverriddenVanillaFileRule().Check(context));

        Assert.Equal("vanilla-copy", hint.RuleId);
        Assert.Equal(HintSeverity.Info, hint.Severity);
        Assert.Equal(BothFile, hint.Path);
        Assert.Equal(30, hint.Line);
        Assert.Equal(8, hint.Value, 6);
        Assert.Equal(PerformanceTexts.HintVanillaCopyTitle, hint.Title);
        Assert.Contains(8.0.ToString("N3", CultureInfo.CurrentCulture), hint.Detail);
        Assert.Contains("Compare und Update Tool", hint.Detail);
    }

    [Fact]
    public void VanillaCopyRuleIsSilentWithoutOverriddenFiles()
    {
        var context = Context(Dump($"effect @ {ModOnly}:4;5;1;50", $"effect @ {VanillaFile}:4;5;1;50"));

        Assert.Empty(new OverriddenVanillaFileRule().Check(context));
    }

    // ErrorSpamRule

    [Fact]
    public void ErrorSpamRuleFiresFromTheMinimumCountOnModSidePlaces()
    {
        var errors = Errors((ModValues, 9, 3), (ModValues, 40, 2), (VanillaFile, 3, 5), (ModOnly, 4, 3));
        Assert.Equal(13, errors.Total);
        var context = Context(
            Dump($"script_value @ {ModValues}:9;5;1;3", $"effect @ {ModValues}:9;5;1;7.5", $"effect @ {ModOnly}:80;5;1;1"),
            errors: errors,
            thresholds: new RuleThresholds(ErrorSpamMin: 3));

        var hints = new ErrorSpamRule().Check(context).OrderBy(h => h.Path, StringComparer.Ordinal).ToList();

        Assert.Equal(new[] { (ModValues, 9), (ModOnly, 4) }, hints.Select(h => (h.Path, h.Line)));
        Assert.All(hints, h =>
        {
            Assert.Equal("error-spam", h.RuleId);
            Assert.Equal(HintSeverity.Warning, h.Severity);
            Assert.Equal(3, h.Value, 6);
            Assert.Equal(PerformanceTexts.HintErrorSpamTitle, h.Title);
            Assert.Contains(Message(h.Path, h.Line), h.Detail);
        });

        // Mit Dump: Die Stelle mit Profiler-Zeilen bekommt deren höchste Gesamtzeit angehängt, die ohne Zeile nicht.
        Assert.Equal(PerformanceTexts.HintErrorSpamDetail(3, null, Message(ModValues, 9)) + " " + PerformanceTexts.HintErrorSpamRowTotal(7.5), hints[0].Detail);
        Assert.Equal(PerformanceTexts.HintErrorSpamDetail(3, null, Message(ModOnly, 4)), hints[1].Detail);
    }

    [Fact]
    public void ErrorSpamRuleIsSilentWithoutErrorLog()
    {
        var context = Context(Dump($"script_value @ {ModValues}:9;5;1;3"), errors: null, thresholds: new RuleThresholds(ErrorSpamMin: 0));

        Assert.False(context.HasErrorLog);
        Assert.Empty(context.ErrorPlaces);
        Assert.Empty(new ErrorSpamRule().Check(context));
    }

    [Fact]
    public void ErrorSpamRuleAtTheDefaultThresholdInLogOnlyMode()
    {
        var errors = Errors((ModValues, 1, 100), (ModValues, 2, 99));
        var context = RuleContext.FromErrorLog(errors, TimeSpan.FromMinutes(10), _ => null, null, new RuleThresholds());

        var hint = Assert.Single(new ErrorSpamRule().Check(context));

        Assert.Equal(1, hint.Line);
        Assert.Equal(100, hint.Value, 6);
        Assert.Equal(PerformanceTexts.HintErrorSpamDetail(100, 10.0, Message(ModValues, 1)), hint.Detail);
    }

    // Texte

    [Fact]
    public void HintTextsFallBackToGermanAndFormatNumbersInTheCurrentCulture()
    {
        Assert.Equal("Fehler-Spam an dieser Stelle", PerformanceTexts.HintErrorSpamTitle);
        Assert.Equal("Hotspot in einer Mod-Kopie einer Vanilla-Datei", PerformanceTexts.HintVanillaCopyTitle);
        Assert.Equal("Script Value mit sehr vielen Aufrufen", PerformanceTexts.HintManyCallsTitle);
        Assert.Equal("Kein error.log zu diesem Lauf: Fehler-Regeln ausgelassen.", PerformanceTexts.HintNoErrorLog);
        Assert.Equal("Warnung", PerformanceTexts.HintSeverityName(HintSeverity.Warning));
        Assert.Equal("Hinweis", PerformanceTexts.HintSeverityName(HintSeverity.Info));

        var withRate = PerformanceTexts.HintErrorSpamDetail(1234, 12.5, "Meldung {x}");
        Assert.Contains(1234.ToString("N0", CultureInfo.CurrentCulture), withRate);
        Assert.Contains(12.5.ToString("N1", CultureInfo.CurrentCulture), withRate);
        Assert.Contains("Meldung {x}", withRate);
        Assert.DoesNotContain(12.5.ToString("N1", CultureInfo.CurrentCulture), PerformanceTexts.HintErrorSpamDetail(1234, null, "m"));
        Assert.Contains(250_000.0.ToString("N0", CultureInfo.CurrentCulture), PerformanceTexts.HintManyCallsDetail(250_000));
        Assert.Contains(1.25.ToString("N3", CultureInfo.CurrentCulture), PerformanceTexts.HintVanillaCopyDetail(1.25));
        Assert.Contains(1.25.ToString("N3", CultureInfo.CurrentCulture), PerformanceTexts.HintErrorSpamRowTotal(1.25));
    }
}
