using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class TracePlannerTests
{
    private const string ProbePath = "common/on_action/pmt_scope_probe_on_actions.txt";

    /// <summary>Die on_action-Datei der Test-Mod "PMT Scope Probe" (Probelauf 2026-09-26): Fehler in Zeile 41, 42 und 52.</summary>
    private static readonly string ProbeFile = ProbeText.Replace("\r\n", "\n");

    /// <summary>Zeilenenden im Literal folgen der Datei (git kann sie auf CRLF umstellen); <see cref="ProbeFile"/> ist immer LF.</summary>
    private const string ProbeText = """
# PMT Scope Probe: find out what debug_log / debug_log_scopes write to the logs and
# whether a following runtime error lands directly after the trace in debug.log.
# Runs monthly for five countries. Every block starts with a unique PMT_TRACE_ marker.

monthly_country_pulse = { on_actions = { pmt_probe_pulse } }

pmt_probe_pulse = {
	trigger = {
		OR = {
			tag = FRA
			tag = ENG
			tag = CAS
			tag = POR
			tag = BUR
		}
	}
	effect = {
		# 1: plain marker, date, current scope only (country)
		debug_log = "PMT_TRACE_1_COUNTRY_SHORT"
		debug_log_date = yes
		debug_log_scopes = no

		# 2: full scope info (country)
		debug_log = "PMT_TRACE_2_COUNTRY_FULL"
		debug_log_scopes = yes

		# 3: localization key with ROOT/SCOPE in the message
		debug_log = pmt_probe_loc

		# 4: character scope (ruler), full info
		ruler = {
			debug_log = "PMT_TRACE_4_RULER_FULL"
			debug_log_scopes = yes
		}

		# 5: location scope (capital), full info, then a deliberate runtime error in the same scope
		capital = {
			save_scope_as = pmt_probe_capital
			debug_log = "PMT_TRACE_5_CAPITAL_FULL"
			debug_log_scopes = yes
			scope:pmt_probe_missing_scope = {
				add_gold = 1
			}
			debug_log = "PMT_TRACE_5_AFTER_ERROR"
		}

		# 6: deliberate runtime error in a trigger (limit) after a trace
		debug_log = "PMT_TRACE_6_BEFORE_LIMIT_ERROR"
		debug_log_scopes = no
		if = {
			limit = {
				scope:pmt_probe_missing_scope = { tag = FRA }
			}
			debug_log = "PMT_TRACE_6_INSIDE_IF"
		}
		debug_log = "PMT_TRACE_6_AFTER_LIMIT_ERROR"
	}
}
""";

    private static TraceRules Rules => Catalogs.Eu5.Trace;

    private static IReadOnlyList<TraceGuard> Guards => Catalogs.Eu5.Find("RUN-001")!.TraceGuards;

    private static string LineOf(string text, int line) => text.Split('\n')[line - 1].TrimEnd('\r');

    [Fact]
    public void AnUnsetScopeIsTracedWithAGuardBeforeTheFailingStatement()
    {
        var plan = TracePlanner.Plan(ProbeFile, ProbePath, 41, "Undefined event target 'pmt_probe_missing_scope'", Guards, Rules, "3f2a");
        var after = TracePlanner.Apply(ProbeFile, plan);

        Assert.Equal(TraceStatus.Ok, plan.Status);
        Assert.True(plan.Guarded);
        Assert.Equal(
            "\t\t\tif = { limit = { NOT = { exists = scope:pmt_probe_missing_scope } } debug_log = \"PMT_TRACE_3f2a\" debug_log_scopes = yes } scope:pmt_probe_missing_scope = { # PMT-TRACE 3f2a",
            LineOf(after, 41));
        Assert.Equal(ProbeFile.Split('\n').Length, after.Split('\n').Length);
        Assert.Equal((41, 43), (plan.FirstLine, plan.LastLine));
    }

    [Fact]
    public void AnotherErrorIsTracedWithoutGuardInTheSameScope()
    {
        var plan = TracePlanner.Plan(ProbeFile, ProbePath, 42, "add_gold effect [ Wrong scope for effect 'add_gold': got none/any, expected country, international_organization ]", Guards, Rules, "0001");

        Assert.False(plan.Guarded);
        Assert.Equal("\t\t\t\tdebug_log = \"PMT_TRACE_0001\" debug_log_scopes = yes add_gold = 1 # PMT-TRACE 0001", LineOf(TracePlanner.Apply(ProbeFile, plan), 42));
    }

    [Fact]
    public void AnErrorInALimitIsTracedBeforeTheIf()
    {
        var plan = TracePlanner.Plan(ProbeFile, ProbePath, 52, "Undefined event target 'pmt_probe_missing_scope'", Guards, Rules, "0002");

        Assert.Equal(TraceStatus.Ok, plan.Status);
        Assert.Equal(50, plan.Line);
        Assert.Equal((50, 55), (plan.FirstLine, plan.LastLine));
        Assert.StartsWith("\t\tif = { limit = { NOT = { exists = scope:pmt_probe_missing_scope } }", LineOf(TracePlanner.Apply(ProbeFile, plan), 50));
    }

    [Fact]
    public void AnErrorInTheOnActionTriggerHasNoEffectContext()
    {
        var plan = TracePlanner.Plan(ProbeFile, ProbePath, 10, "x", Guards, Rules, "0003");

        Assert.Equal(TraceStatus.NoEffectContext, plan.Status);
    }

    [Fact]
    public void ElseIfChainIsTracedBeforeTheFirstIf()
    {
        const string text = "a = {\n\teffect = {\n\t\tif = {\n\t\t\tlimit = { has_x = yes }\n\t\t\tadd_gold = 1\n\t\t} else_if = {\n\t\t\tlimit = { scope:y = { is_z = yes } }\n\t\t} else = {\n\t\t\tadd_gold = 2\n\t\t}\n\t\tadd_gold = 3\n\t}\n}\n";

        var plan = TracePlanner.Plan(text, "common/on_action/a.txt", 7, "Undefined event target 'y'", Guards, Rules, "0004");

        Assert.Equal(3, plan.Line);
        Assert.Equal((3, 10), (plan.FirstLine, plan.LastLine));
        Assert.StartsWith("\t\tif = { limit = { NOT = { exists = scope:y } }", LineOf(TracePlanner.Apply(text, plan), 3));
    }

    [Fact]
    public void AStatementAfterAClosingBraceIsTracedWhereItStarts()
    {
        const string text = "a = {\n\teffect = {\n\t\tcapital = {\n\t\t\tadd_gold = 1\n\t\t} scope:y = { add_gold = 1 }\n\t}\n}\n";

        var plan = TracePlanner.Plan(text, "common/on_action/a.txt", 5, "Undefined event target 'y'", Guards, Rules, "0005");

        Assert.Equal("\t\t} if = { limit = { NOT = { exists = scope:y } } debug_log = \"PMT_TRACE_0005\" debug_log_scopes = yes } scope:y = { add_gold = 1 } # PMT-TRACE 0005", LineOf(TracePlanner.Apply(text, plan), 5));
    }

    [Fact]
    public void ScriptedTriggersHaveNoEffectContext()
    {
        const string text = "my_trigger = {\n\tscope:y = { is_z = yes }\n}\n";

        Assert.Equal(TraceStatus.NoEffectContext, TracePlanner.Plan(text, "common/scripted_triggers/t.txt", 2, "x", Guards, Rules, "0006").Status);
    }

    [Fact]
    public void ScriptedEffectsAreEffectContextAtTheTop()
    {
        const string text = "my_effect = {\n\tadd_gold = 1\n}\n";

        var plan = TracePlanner.Plan(text, "common/scripted_effects/e.txt", 2, "x", Guards, Rules, "0007");

        Assert.Equal(TraceStatus.Ok, plan.Status);
        Assert.Equal("\tdebug_log = \"PMT_TRACE_0007\" debug_log_scopes = yes add_gold = 1 # PMT-TRACE 0007", LineOf(TracePlanner.Apply(text, plan), 2));
    }

    [Fact]
    public void ATracedLineIsNotTracedAgainButAnOrdinaryDebugLogLineIs()
    {
        var first = TracePlanner.Plan(ProbeFile, ProbePath, 42, "x", Guards, Rules, "0008");
        var traced = TracePlanner.Apply(ProbeFile, first);

        Assert.Equal(TraceStatus.AlreadyTraced, TracePlanner.Plan(traced, ProbePath, 42, "x", Guards, Rules, "0009").Status);
        Assert.Equal(TraceStatus.Ok, TracePlanner.Plan(ProbeFile, ProbePath, 39, "x", Guards, Rules, "000a").Status);
    }

    [Fact]
    public void CommentsWithBracesDoNotMoveTheContext()
    {
        const string plain = "a = {\n\teffect = {\n\t\tscope:y = { add_gold = 1 }\n\t}\n}\n";
        const string commented = "a = {\n\teffect = { # } limit = {\n\t\tscope:y = { add_gold = 1 }\n\t}\n}\n";

        var withComment = TracePlanner.Plan(commented, "common/on_action/a.txt", 3, "Undefined event target 'y'", Guards, Rules, "000b");

        Assert.Equal(TraceStatus.Ok, withComment.Status);
        Assert.Equal(TracePlanner.Plan(plain, "common/on_action/a.txt", 3, "Undefined event target 'y'", Guards, Rules, "000b").Line, withComment.Line);
    }

    [Fact]
    public void CrlfStaysAndOtherLinesAreUntouched()
    {
        var text = ProbeFile.Replace("\n", "\r\n");

        var after = TracePlanner.Apply(text, TracePlanner.Plan(text, ProbePath, 42, "x", Guards, Rules, "000c"));

        var before = text.Split("\r\n");
        var lines = after.Split("\r\n");
        Assert.Equal(before.Length, lines.Length);
        Assert.Equal(1, before.Zip(lines).Count(p => p.First != p.Second));
        Assert.EndsWith("add_gold = 1 # PMT-TRACE 000c", lines[41]);
    }

    [Fact]
    public void AMissingLineIsReported()
    {
        Assert.Equal(TraceStatus.LineNotFound, TracePlanner.Plan(ProbeFile, ProbePath, 999, "x", Guards, Rules, "000d").Status);
    }
}
