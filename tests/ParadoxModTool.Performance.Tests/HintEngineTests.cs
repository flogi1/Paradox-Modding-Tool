using Xunit;
using static ParadoxModTool.Performance.Tests.RuleTestData;

namespace ParadoxModTool.Performance.Tests;

public sealed class HintEngineTests
{
    private sealed class StubRule(string id, params PerformanceHint[] hints) : IPerformanceRule
    {
        public string Id => id;

        public IEnumerable<PerformanceHint> Check(RuleContext context) => hints;
    }

    private static PerformanceHint Hint(string rule, HintSeverity severity, string path, int line, double value) =>
        new(rule, severity, path, line, "t", "d", value);

    private static RuleContext EmptyContext() =>
        RuleContext.FromErrorLog(ErrorHotspots.Empty, null, _ => null, null, new RuleThresholds());

    [Fact]
    public void RunOrdersWarningsFirstThenByValue()
    {
        var rule = new StubRule(
            "x",
            Hint("x", HintSeverity.Info, "b.txt", 1, 10),
            Hint("x", HintSeverity.Warning, "z.txt", 1, 1),
            Hint("x", HintSeverity.Info, "c.txt", 1, 20),
            Hint("x", HintSeverity.Info, "a.txt", 2, 10),
            Hint("x", HintSeverity.Info, "a.txt", 1, 10));

        var hints = HintEngine.Run(EmptyContext(), new[] { rule });

        Assert.Equal(
            new[] { ("z.txt", 1), ("c.txt", 1), ("a.txt", 1), ("a.txt", 2), ("b.txt", 1) },
            hints.Select(h => (h.Path, h.Line)));
    }

    [Fact]
    public void RunDeduplicatesPerRuleAndPlace()
    {
        var x = new StubRule(
            "x",
            Hint("x", HintSeverity.Info, "a.txt", 1, 5),
            Hint("x", HintSeverity.Info, "a.txt", 1, 9),
            Hint("x", HintSeverity.Info, "a.txt", 2, 1));
        var y = new StubRule("y", Hint("y", HintSeverity.Info, "a.txt", 1, 3));

        var hints = HintEngine.Run(EmptyContext(), new IPerformanceRule[] { x, y });

        Assert.Equal(
            new[] { ("x", "a.txt", 1, 9.0), ("x", "a.txt", 2, 1.0), ("y", "a.txt", 1, 3.0) },
            hints.Select(h => (h.RuleId, h.Path, h.Line, h.Value)));
    }

    [Fact]
    public void RunGroupsHintsInRuleOrderRegardlessOfValue()
    {
        var seconds = new StubRule(
            "seconds",
            Hint("seconds", HintSeverity.Info, "a.txt", 1, 0.5),
            Hint("seconds", HintSeverity.Info, "b.txt", 1, 2));
        var calls = new StubRule(
            "calls",
            Hint("calls", HintSeverity.Info, "c.txt", 1, 900_000),
            Hint("calls", HintSeverity.Warning, "d.txt", 1, 1));
        var errors = new StubRule("errors", Hint("errors", HintSeverity.Warning, "e.txt", 1, 500));

        var hints = HintEngine.Run(EmptyContext(), new IPerformanceRule[] { seconds, calls, errors });

        Assert.Equal(
            new[] { ("calls", "d.txt"), ("errors", "e.txt"), ("seconds", "b.txt"), ("seconds", "a.txt"), ("calls", "c.txt") },
            hints.Select(h => (h.RuleId, h.Path)));
    }

    [Fact]
    public void DefaultRulesRunInTheirDisplayOrder()
    {
        // Die Engine ordnet Hinweise nach der Stelle der Regel, darum ist die Reihenfolge Teil des Vertrags.
        Assert.Equal(
            new[] { "error-spam", "unguarded-var", "frequent-tick", "vanilla-copy", "many-calls" },
            HintEngine.DefaultRules.Select(r => r.Id));
    }

    [Fact]
    public void LogOnlyContextRanksErrorPlacesAndSkipsDumpRules()
    {
        const string ValuesFile = "common/script_values/x.txt";
        var errors = Errors((ValuesFile, 2, 100), (ValuesFile, 1, 150), (ValuesFile, 3, 99));
        Assert.Equal(349, errors.Total);

        var context = RuleContext.FromErrorLog(errors, TimeSpan.FromMinutes(10), _ => null, null, new RuleThresholds(TopRows: 2));

        Assert.False(context.HasDump);
        Assert.True(context.HasErrorLog);
        Assert.Empty(context.Rows);
        Assert.Null(context.Months);
        Assert.Equal(
            new[] { new ErrorPlace(ValuesFile, 1, 150, 15.0), new ErrorPlace(ValuesFile, 2, 100, 10.0) },
            context.ErrorPlaces);

        var hints = HintEngine.Run(context, HintEngine.DefaultRules);

        Assert.Equal(new[] { ("error-spam", 1), ("error-spam", 2) }, hints.Select(h => (h.RuleId, h.Line)));
    }

    [Fact]
    public void LogOnlyContextRunsErrorSpamAndUnguardedButSkipsDumpRules()
    {
        // Wie im Fehlerprotokoll-Fenster: nur ein error.log, kein Dump. Eine Stelle mit Spam, eine mit einer Variablen-Meldung in einer lesbaren Datei ohne Guard.
        const string SpamFile = "common/scripted_effects/spam.txt";
        const string ValuesFile = "common/script_values/x.txt";
        var errors = ErrorsWith(
            (SpamFile, 1, 100, Message(SpamFile, 1)),
            (ValuesFile, 2, 5, $"Failed to fetch variable for 'iv2_x' due to not being set at {ValuesFile}:2"));
        var text = "iv2_x_value = {\n\tvalue = var:iv2_x\n}\n";

        var context = RuleContext.FromErrorLog(errors, TimeSpan.FromMinutes(10), path => path == ValuesFile ? text : null, null, new RuleThresholds());
        var hints = HintEngine.Run(context, HintEngine.DefaultRules);

        Assert.False(context.HasDump);
        Assert.Equal(
            new[] { ("error-spam", SpamFile, 1), ("unguarded-var", ValuesFile, 2) },
            hints.Select(h => (h.RuleId, h.Path, h.Line)));
        Assert.Equal(PerformanceTexts.HintUnguardedDetail("iv2_x", 5), hints[1].Detail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(59.0)]
    public void PerMinuteIsUnknownWithoutASessionOfAtLeastOneMinute(double? seconds)
    {
        var errors = Errors(("a.txt", 1, 2));
        var length = seconds is { } s ? TimeSpan.FromSeconds(s) : (TimeSpan?)null;

        var context = RuleContext.FromErrorLog(errors, length, _ => null, null, new RuleThresholds());

        Assert.Null(Assert.Single(context.ErrorPlaces).PerMinute);
    }

    [Fact]
    public void DumpContextKeepsOnlyModSideErrorPlacesBeforeTakingTheTop()
    {
        var classifier = Classifier(new[] { "mod.txt" }, new[] { "van.txt" });
        var dump = Dump("effect @ mod.txt:1;1;1;1");
        var errors = Errors(("van.txt", 1, 9), ("mod.txt", 5, 2), ("other.txt", 1, 8));

        var context = new RuleContext(
            new ProfilingSummary(dump, classifier), new RunDuration(12, "p", true), classifier, null, errors, _ => null, new RuleThresholds(TopRows: 1));

        Assert.True(context.HasDump);
        Assert.Equal(12, context.Months);
        Assert.Equal(new[] { new ErrorPlace("mod.txt", 5, 2, null) }, context.ErrorPlaces);
        Assert.Single(context.Rows);
    }

    [Fact]
    public void ReadFileAndBlockAtAreCachedPerPath()
    {
        var reads = new List<string>();
        string? Read(string path)
        {
            reads.Add(path);
            return path == "a.txt" ? "my_value = {\n\tvalue = 1\n}\n" : null;
        }

        var context = RuleContext.FromErrorLog(ErrorHotspots.Empty, null, Read, null, new RuleThresholds());

        Assert.Equal("my_value", context.BlockAt("a.txt", 2)?.TopLevelKey);
        Assert.Same(context.BlockAt("a.txt", 2), context.BlockAt("a.txt", 2));
        Assert.NotNull(context.ReadFile("a.txt"));
        Assert.Null(context.BlockAt("missing.txt", 1));
        Assert.Null(context.ReadFile("missing.txt"));

        Assert.Equal(new[] { "a.txt", "missing.txt" }, reads);
    }
}
