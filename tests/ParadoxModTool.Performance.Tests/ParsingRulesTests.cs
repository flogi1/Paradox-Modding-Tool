using System.Globalization;
using ParadoxModTool.GameProfiles;
using Xunit;
using static ParadoxModTool.Performance.Tests.RuleTestData;

namespace ParadoxModTool.Performance.Tests;

public sealed class ParsingRulesTests
{
    private const string Values = "common/script_values/mod_values.txt";
    private const string Actions = "common/generic_actions/mod_actions.txt";

    private static readonly OriginClassifier Origins = Classifier(new[] { Values, Actions }, Array.Empty<string>());

    private static readonly PerformanceSettings Eu5Like = new()
    {
        FrequentTickMarkers = new() { "ai_tick = daily" },
    };

    /// <summary>Mit <c>ai_tick = monthly</c> als Kennzeichnung, damit die Grenze 30 Tage wirklich gerechnet wird.</summary>
    private static readonly PerformanceSettings DailyAndMonthly = new()
    {
        FrequentTickMarkers = new() { "ai_tick = daily", "ai_tick = monthly" },
    };

    /// <summary>Die echte IV2-Form (02_iv2_script_values.txt:244-262): Der Guard steht im limit des umgebenden if.</summary>
    private static readonly string[] GuardedIv2 =
    {
        "iv2_res_skill_bonus_calc_adm = {",
        "\tif = {",
        "\t\tlimit = {",
        "\t\t\thas_variable = Iv2_researcher_adm",
        "\t\t}",
        "\t\tadd = {",
        "\t\t\tvalue = var:Iv2_researcher_adm.total_abilities",
        "\t\t\tdivide = 2",
        "\t\t}",
        "\t\tadd = {",
        "\t\t\tvalue = var:Iv2_researcher_adm.adm",
        "\t\t}",
        "\t}",
        "\tmultiply = 0.015",
        "}",
    };

    /// <summary>Dieselbe Form ohne if/limit.</summary>
    private static readonly string[] UnguardedIv2 =
    {
        "iv2_res_skill_bonus_calc_dip = {",
        "\tadd = {",
        "\t\tvalue = var:Iv2_researcher_dip.total_abilities",
        "\t\tdivide = 2",
        "\t}",
        "\tadd = {",
        "\t\tvalue = var:Iv2_researcher_dip.dip",
        "\t}",
        "\tmultiply = 0.015",
        "}",
    };

    private static RuleContext Context(
        string[] rows,
        string valuesText = "",
        string actionsText = "",
        PerformanceSettings? settings = null,
        ErrorHotspots? errors = null) =>
        new(
            new ProfilingSummary(Dump(rows), Origins),
            new RunDuration(12, "pulse", true),
            Origins,
            settings,
            errors,
            path => path switch
            {
                Values => valuesText,
                Actions => actionsText,
                _ => null,
            },
            new RuleThresholds());

    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

    private static string Lines(params string[][] blocks) => string.Join("\n", blocks.Select(b => string.Join("\n", b))) + "\n";

    /// <summary>Das Detail eines Kandidaten aus dem Dump: mit der Gesamtzeit seiner Zeile am Ende.</summary>
    private static string WithRowTotal(string detail, double total) => detail + " " + PerformanceTexts.HintUnguardedRowTotal(total);

    // UnguardedVariableRule

    [Fact]
    public void UnguardedVarIsFlagged()
    {
        var text = Lines(
            "iv2_x_value = {",
            "\tvalue = var:Iv2_x.total",
            "\tadd = var:b_second",
            "\tadd = var:A_first.adm",
            "\tadd = var:Iv2_x",
            "}");
        var context = Context(new[] { $"script_value @ {Values}:1;10;0.1;0.3" }, text);

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal("unguarded-var", hint.RuleId);
        Assert.Equal(HintSeverity.Warning, hint.Severity);
        Assert.Equal((Values, 1), (hint.Path, hint.Line));
        Assert.Equal(0.3, hint.Value, 6);
        Assert.Equal(PerformanceTexts.HintUnguardedTitle, hint.Title);
        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("A_first, Iv2_x, b_second", null), 0.3), hint.Detail);
    }

    [Fact]
    public void GuardedInEnclosingLimitIsNotFlagged()
    {
        // Beide Blöcke in einer Datei, gleiche Art Zeilen: Der ungeschützte Zwilling beweist, dass die Regel den Text wirklich liest.
        var text = Lines(GuardedIv2, UnguardedIv2);
        var context = Context(
            new[]
            {
                $"script_value @ {Values}:1;10;0.1;0.4",
                $"script_value @ {Values}:7;10;0.1;0.2",
                $"script_value @ {Values}:16;10;0.1;0.3",
            },
            text);

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal(16, hint.Line);
        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_researcher_dip", null), 0.3), hint.Detail);
    }

    [Fact]
    public void QuestionEqualsGuardCounts()
    {
        var text = Lines(
            "iv2_x_value = {",
            "\tvar:Iv2_x ?= { add = 1 }",
            "\tvar:Iv2_y.adm ?= 3",
            "\tvalue = var:Iv2_x.total",
            "\tadd = var:Iv2_y.dip",
            "\tadd = var:Iv2_z",
            "}");
        var context = Context(new[] { $"trigger @ {Values}:1;10;0.1;0.3" }, text);

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_z", null), 0.3), hint.Detail);
    }

    [Fact]
    public void QuestionEqualsWithoutSpaceCounts()
    {
        // Der Lexer liest "var:Iv2_x?=" als Wort "var:Iv2_x?" und Operator "="; "!=" ohne Leerzeichen ist ein normaler Zugriff.
        var text = Lines(
            "iv2_x_value = {",
            "\tvar:Iv2_x?= { }",
            "\tvalue = var:Iv2_x.total",
            "\tvar:Iv2_y!= yes",
            "}");
        var context = Context(new[] { $"trigger @ {Values}:1;10;0.1;0.3" }, text);

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_y", null), 0.3), hint.Detail);
    }

    [Fact]
    public void UnguardedRuleGivesOneHintPerBlockWithTheHighestValue()
    {
        var text = Lines(UnguardedIv2, new[] { "iv2_other_value = {", "\tvalue = var:Iv2_other", "}" });
        var context = Context(
            new[]
            {
                $"script_value @ {Values}:1;10;0.1;0.2",
                $"script_value @ {Values}:3;10;0.1;0.6",
                $"trigger @ {Values}:7;10;0.1;0.4",
                $"script_value @ {Values}:12;10;0.1;0.1",
            },
            text);

        var hints = new UnguardedVariableRule().Check(context).OrderBy(h => h.Line).ToList();

        Assert.Equal(new[] { (3, 0.6), (12, 0.1) }, hints.Select(h => (h.Line, h.Value)));
    }

    [Fact]
    public void UnguardedErrorCountSumsOverTheBlockAndIsLeftOutWhenZero()
    {
        var text = Lines(UnguardedIv2, new[] { "iv2_other_value = {", "\tvalue = var:Iv2_other", "}" });
        var errors = Errors((Values, 3, 2), (Values, 7, 3), (Values, 20, 9));
        var context = Context(new[] { $"script_value @ {Values}:1;10;0.1;0.5", $"script_value @ {Values}:11;10;0.1;0.4" }, text, errors: errors);

        var hints = new UnguardedVariableRule().Check(context).OrderBy(h => h.Line).ToList();

        Assert.Equal(new[] { 1, 11 }, hints.Select(h => h.Line));
        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_researcher_dip", 5), 0.5), hints[0].Detail);
        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_other", null), 0.4), hints[1].Detail);
    }

    [Fact]
    public void FileNameMentioningVariablesDoesNotMakeAnErrorPlaceACandidate()
    {
        const string VariablesFile = "common/script_values/iv2_variables.txt";
        var text = Lines("iv2_x_effect = {", "\tadd_gold = var:Iv2_x", "}", "iv2_y_effect = {", "\tadd_gold = var:Iv2_y", "}");
        var errors = ErrorsWith(
            (VariablesFile, 2, 4, Message(VariablesFile, 2)),
            (VariablesFile, 5, 3, $"Unknown variable Iv2_y at {VariablesFile}:5"));
        var context = RuleContext.FromErrorLog(errors, null, path => path == VariablesFile ? text : null, null, new RuleThresholds());

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal(5, hint.Line);
    }

    [Fact]
    public void ExistsGuardCounts()
    {
        var text = Lines(
            "iv2_x_trigger = {",
            "\texists = var:Iv2_x",
            "\tvar:Iv2_x.total > 2",
            "\tvar:Iv2_z > 1",
            "}");
        var context = Context(new[] { $"trigger @ {Values}:3;10;0.1;0.3" }, text);

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal(3, hint.Line);
        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_z", null), 0.3), hint.Detail);
    }

    [Fact]
    public void NegatedHasVariableIsNotAGuard()
    {
        var text = Lines(
            "iv2_x_trigger = {",
            "\tNOT = { has_variable = Iv2_x }",
            "\tNOT = { AND = { exists = var:Iv2_y } }",
            "\tvar:Iv2_x.total > 2",
            "\tvar:Iv2_y > 2",
            "}");
        var context = Context(new[] { $"named_script_value @ {Values}:1;10;0.1;0.3" }, text);

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_x, Iv2_y", null), 0.3), hint.Detail);
    }

    [Fact]
    public void GuardAfterTheUseDoesNotCount()
    {
        var text = Lines(
            "iv2_x_value = {",
            "\tvalue = var:Iv2_x.total",
            "\tif = { limit = { has_variable = Iv2_x } add = 1 }",
            "}");
        var context = Context(new[] { $"script_value @ {Values}:1;10;0.1;0.3" }, text);

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_x", null), 0.3), hint.Detail);
    }

    [Fact]
    public void OtherRowKindsAndUnreadableFilesAreSkipped()
    {
        var text = Lines("iv2_x_effect = {", "\tadd_gold = var:Iv2_x", "}");
        var context = Context(new[] { $"effect @ {Values}:1;10;0.1;0.3", $"script_value @ {Actions}:1;10;0.1;0.3" }, text, actionsText: "");

        Assert.Empty(new UnguardedVariableRule().Check(context));
    }

    [Fact]
    public void RowAndErrorPlaceAtTheSameLineGiveOneHintWithTheRowTotalAndTheErrorCount()
    {
        var text = Lines("iv2_x_value = {", "\tvalue = var:Iv2_x.total", "}");
        var errors = ErrorsWith((Values, 2, 5, $"Variable Iv2_x is not set at {Values}:2"));
        var context = Context(
            new[] { $"script_value @ {Values}:2;10;0.1;0.25", $"named_script_value @ {Values}:2;10;0.1;0.75" },
            text,
            errors: errors);

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal((Values, 2), (hint.Path, hint.Line));
        Assert.Equal(0.75, hint.Value, 6);
        Assert.Equal(WithRowTotal(PerformanceTexts.HintUnguardedDetail("Iv2_x", 5), 0.75), hint.Detail);
    }

    [Fact]
    public void ErrorPlacesMentioningVariablesAreCheckedWithoutDump()
    {
        var text = Lines(
            "iv2_x_effect = {",
            "\tadd_gold = var:Iv2_x",
            "}",
            "iv2_y_effect = {",
            "\tadd_gold = var:Iv2_y",
            "}");
        var errors = ErrorsWith(
            (Values, 2, 4, $"Unknown variable Iv2_x at {Values}:2"),
            (Values, 5, 9, Message(Values, 5)));
        var context = RuleContext.FromErrorLog(errors, null, path => path == Values ? text : null, null, new RuleThresholds());

        var hint = Assert.Single(new UnguardedVariableRule().Check(context));

        Assert.Equal((Values, 2), (hint.Path, hint.Line));
        Assert.Equal(4, hint.Value, 6);
        Assert.Equal(PerformanceTexts.HintUnguardedDetail("Iv2_x", 4), hint.Detail);
    }

    // FrequentTickRule

    [Fact]
    public void FrequentTickFiresOnDailyGenericAction()
    {
        var text = Lines(
            "iv2_daily_action = {",
            "\tai_tick   =   daily",
            "\teffect = {",
            "\t\tadd_gold = 1",
            "\t}",
            "}");
        var context = Context(new[] { $"generic_action @ {Actions}:3;100;0.1;0.6" }, actionsText: text, settings: Eu5Like);

        var hint = Assert.Single(new FrequentTickRule().Check(context));

        Assert.Equal("frequent-tick", hint.RuleId);
        Assert.Equal(HintSeverity.Warning, hint.Severity);
        Assert.Equal((Actions, 3), (hint.Path, hint.Line));
        Assert.Equal(0.6, hint.Value, 6);
        Assert.Equal(PerformanceTexts.HintFrequentTickTitle, hint.Title);
        Assert.Equal(PerformanceTexts.HintFrequentTickDetail("ai_tick = daily", "iv2_daily_action", 1, 1, 0.6), hint.Detail);
    }

    [Fact]
    public void FrequentTickIgnoresMonthlyAndSmallTotals()
    {
        var text = Lines(
            "monthly_action = {",
            "\tai_tick = monthly",
            "}",
            "small_daily_action = {",
            "\tai_tick = daily",
            "}",
            "boundary_daily_action = {",
            "\tai_tick=daily",
            "}",
            "nested_daily_action = {",
            "\teffect = { ai_tick = daily }",
            "}");
        var context = Context(
            new[]
            {
                $"generic_action @ {Actions}:1;100;0.1;0.9",
                $"generic_action @ {Actions}:4;100;0.1;0.49",
                $"generic_action @ {Actions}:7;100;0.1;0.5",
                $"generic_action @ {Actions}:10;100;0.1;0.9",
            },
            actionsText: text,
            settings: Eu5Like);

        var hint = Assert.Single(new FrequentTickRule().Check(context));

        Assert.Equal(7, hint.Line);
        Assert.Equal(PerformanceTexts.HintFrequentTickDetail("ai_tick = daily", "boundary_daily_action", 1, 1, 0.5), hint.Detail);
    }

    [Fact]
    public void FrequentTickIgnoresTheRealIv2ShapeDailyWithFrequency180()
    {
        // Die echte Form der IV2-Aktionen (iv2_ga_hire_researchers_*): daily mal 180 = alle 180 Tage.
        var text = Lines(
            "iv2_ga_hire_researchers_adm = {",
            "\tai_tick = daily",
            "\tai_tick_frequency = 180",
            "\tpotential = { always = yes }",
            "}");
        var context = Context(new[] { $"generic_action @ {Actions}:1;100;0.1;5" }, actionsText: text, settings: Eu5Like);

        Assert.Empty(new FrequentTickRule().Check(context));
    }

    [Fact]
    public void FrequentTickFiresWhenThePeriodIsBelowThirtyDays()
    {
        // Die Frequenz darf vor ai_tick stehen und beliebig viel Leerraum haben; eine verschachtelte zählt nicht.
        var text = Lines(
            "fast_action = {",
            "\teffect = { ai_tick_frequency = 400 }",
            "\tai_tick_frequency   =   20",
            "\tai_tick = daily",
            "}",
            "exact_action = {",
            "\tai_tick = daily",
            "\tai_tick_frequency = 30",
            "}");
        var context = Context(
            new[] { $"generic_action @ {Actions}:1;100;0.1;0.7", $"generic_action @ {Actions}:6;100;0.1;0.9" },
            actionsText: text,
            settings: Eu5Like);

        var hint = Assert.Single(new FrequentTickRule().Check(context));

        Assert.Equal(HintSeverity.Warning, hint.Severity);
        Assert.Equal(1, hint.Line);
        Assert.Equal(PerformanceTexts.HintFrequentTickDetail("ai_tick = daily", "fast_action", 20, 20, 0.7), hint.Detail);
    }

    [Fact]
    public void FrequentTickIgnoresMonthlyWithFrequencyOne()
    {
        // monthly mal 1 = 30 Tage, nicht unter 30; der tägliche Zwilling mit 29 beweist, dass die Grenze gerechnet wird.
        var text = Lines(
            "monthly_action = {",
            "\tai_tick = monthly",
            "\tai_tick_frequency = 1",
            "}",
            "almost_monthly_action = {",
            "\tai_tick = daily",
            "\tai_tick_frequency = 29",
            "}");
        var context = Context(
            new[] { $"generic_action @ {Actions}:1;100;0.1;0.9", $"generic_action @ {Actions}:5;100;0.1;0.6" },
            actionsText: text,
            settings: DailyAndMonthly);

        var hint = Assert.Single(new FrequentTickRule().Check(context));

        Assert.Equal(5, hint.Line);
        Assert.Equal(PerformanceTexts.HintFrequentTickDetail("ai_tick = daily", "almost_monthly_action", 29, 29, 0.6), hint.Detail);
    }

    [Fact]
    public void FrequentTickWithAScriptValueFrequencyIsAnInfoWithUnknownPeriod()
    {
        var text = Lines(
            "scripted_action = {",
            "\tai_tick = daily",
            "\tai_tick_frequency = my_script_value",
            "}");
        var context = Context(new[] { $"generic_action @ {Actions}:1;100;0.1;0.6" }, actionsText: text, settings: Eu5Like);

        var hint = Assert.Single(new FrequentTickRule().Check(context));

        Assert.Equal(HintSeverity.Info, hint.Severity);
        Assert.Equal(0.6, hint.Value, 6);
        Assert.Equal(PerformanceTexts.HintFrequentTickUnknownDetail("ai_tick = daily", "scripted_action", "my_script_value", 0.6, 30), hint.Detail);
    }

    [Fact]
    public void FrequentTickUsesPulseNames()
    {
        var text = Lines("daily_country_pulse = {", "\teffect = { add_gold = 1 }", "}", "monthly_country_pulse = {", "\teffect = { add_gold = 1 }", "}");
        var settings = new PerformanceSettings { FrequentPulseNames = new() { "daily_country_pulse" } };
        var context = Context(
            new[] { $"on_action @ {Actions}:2;100;0.1;0.7", $"on_action @ {Actions}:5;100;0.1;0.9" },
            actionsText: text,
            settings: settings);

        var hint = Assert.Single(new FrequentTickRule().Check(context));

        Assert.Equal(2, hint.Line);
        Assert.Equal(HintSeverity.Warning, hint.Severity);
        Assert.Equal(PerformanceTexts.HintFrequentTickPulseDetail("daily_country_pulse", "daily_country_pulse", 0.7), hint.Detail);
    }

    [Fact]
    public void FrequentTickGivesOneHintPerBlockWithTheHighestTotal()
    {
        var text = Lines(
            "first_daily_action = {",
            "\tai_tick = daily",
            "\teffect = {",
            "\t\tadd_gold = 1",
            "\t}",
            "}",
            "second_daily_action = {",
            "\tai_tick = daily",
            "}");
        var context = Context(
            new[]
            {
                $"generic_action @ {Actions}:1;100;0.1;0.6",
                $"effect @ {Actions}:4;100;0.1;0.8",
                $"generic_action @ {Actions}:7;100;0.1;0.5",
            },
            actionsText: text,
            settings: Eu5Like);

        var hints = new FrequentTickRule().Check(context).OrderBy(h => h.Line).ToList();

        Assert.Equal(new[] { (4, 0.8), (7, 0.5) }, hints.Select(h => (h.Line, h.Value)));
    }

    [Fact]
    public void FrequentTickIsSilentWithoutSettings()
    {
        var text = Lines("iv2_daily_action = {", "\tai_tick = daily", "}");
        var context = Context(new[] { $"generic_action @ {Actions}:1;100;0.1;0.9" }, actionsText: text, settings: null);

        Assert.NotNull(context.BlockAt(Actions, 1));
        Assert.Empty(new FrequentTickRule().Check(context));
    }

    // Texte

    [Fact]
    public void ParsingRuleTextsFallBackToGerman()
    {
        Assert.Equal("Variablenzugriff ohne Guard", PerformanceTexts.HintUnguardedTitle);
        Assert.Equal("Schwerer Block auf häufigem Tick", PerformanceTexts.HintFrequentTickTitle);
        Assert.Contains("a, b", PerformanceTexts.HintUnguardedDetail("a, b", null));
        Assert.Contains(1234.ToString("N0", CultureInfo.CurrentCulture), PerformanceTexts.HintUnguardedDetail("a", 1234));
        Assert.DoesNotContain(1234.ToString("N0", CultureInfo.CurrentCulture), PerformanceTexts.HintUnguardedDetail("a", null));
        Assert.Contains("In diesem Block stehen", PerformanceTexts.HintUnguardedDetail("a", 1234));
        Assert.Contains(0.75.ToString("N3", CultureInfo.CurrentCulture), PerformanceTexts.HintUnguardedRowTotal(0.75));
        var tick = PerformanceTexts.HintFrequentTickDetail("ai_tick = daily", "my_action", 20, 20, 1.25);
        Assert.Contains("ai_tick = daily", tick);
        Assert.Contains("my_action", tick);
        Assert.Contains("ai_tick_frequency = 20", tick);
        Assert.Contains(1.25.ToString("N3", CultureInfo.CurrentCulture), tick);
        Assert.Contains("ai_tick_frequency erhöhen oder den Trigger enger fassen", tick);
        Assert.DoesNotContain("ai_tick = monthly", tick);
        var unknown = PerformanceTexts.HintFrequentTickUnknownDetail("ai_tick = daily", "my_action", "my_script_value", 1.25, 30);
        Assert.Contains("ai_tick_frequency = my_script_value", unknown);
        Assert.Contains("unbekannt", unknown);
        var pulse = PerformanceTexts.HintFrequentTickPulseDetail("daily_country_pulse", "my_pulse", 1.25);
        Assert.Contains("daily_country_pulse", pulse);
        Assert.DoesNotContain("monthly", pulse);
    }
}
