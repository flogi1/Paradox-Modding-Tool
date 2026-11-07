using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckAnalyzerTests : IDisposable
{
    private static readonly IReadOnlySet<string> NoBuiltIns = new HashSet<string>(StringComparer.Ordinal);

    private readonly ScriptCheckTestRoots _roots = new();

    public void Dispose() => _roots.Dispose();

    private static string Write(string root, string relative, string text) => ScriptCheckTestRoots.Write(root, relative, text);

    private AnalyzerResult Analyze(string game = "ck3", IReadOnlySet<string>? builtIns = null)
    {
        var rules = ScriptCheckTestRoots.Rules(game);
        var index = new ScriptCheckBuilder(_roots.Sources(game), rules).Build();
        return ScriptCheckAnalyzer.Analyze(index, builtIns ?? NoBuiltIns, rules);
    }

    private static IReadOnlyList<Finding> Of(AnalyzerResult result, FindingKind kind) => result.Findings.Where(f => f.Kind == kind).ToList();

    /// <summary>Vanilla-On-Action, das die genannten Events aufruft (damit sie nicht tot sind).</summary>
    private void CallFromVanilla(params string[] events) =>
        Write(_roots.Vanilla, "common/on_action/v.txt", "on_game_start = {\n\tevents = { " + string.Join(" ", events) + " }\n}");

    [Fact]
    public void BrokenEventReference()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\ttrigger_event = m.9\n\t}\n}");
        CallFromVanilla("m.1");

        var finding = Assert.Single(Of(Analyze(), FindingKind.BrokenReference));

        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Equal("m.9", finding.Name);
        Assert.Equal(CheckKind.Event, finding.NameKind);
        Assert.Equal(_roots.Mod, finding.Location.RootDirectory);
        Assert.Equal("events/m.txt", finding.Location.RelativePath);
        Assert.Equal(4, finding.Location.Line);
        Assert.Equal(new[] { "m.1 (events/m.txt:4)" }, finding.Related);
        Assert.Equal("BrokenReference|m.9", finding.Key);
    }

    [Fact]
    public void NamedBlockCallNotBroken()
    {
        Write(_roots.Vanilla, "common/scripted_modifiers/v.txt", "my_mod = { modifier = { add = 1 } }");
        Write(_roots.Mod, "common/scripted_effects/m.txt", "m_eff = {\n\tmy_mod = yes\n}");
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "v_eff = {\n\tm_eff = yes\n}");

        var result = Analyze("ck3", new HashSet<string>(StringComparer.Ordinal) { "add_gold" });

        Assert.Empty(Of(result, FindingKind.BrokenReference));
    }

    [Fact]
    public void NamedBlockNeverUnused()
    {
        Write(_roots.Mod, "common/scripted_modifiers/m.txt", "my_mod = { modifier = { add = 1 } }");
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "v_eff = {\n\tmy_mod = yes\n}");

        var result = Analyze("ck3", new HashSet<string>(StringComparer.Ordinal) { "add_gold" });

        Assert.Empty(Of(result, FindingKind.UnusedBlock));
        Assert.Empty(Of(result, FindingKind.EffectCycle));
    }

    [Fact]
    public void InlineScriptParametersMakeNoDuplicates()
    {
        // Stellaris leviathans_events_1.txt: EVENT_ID definiert, LOCALIZATION_ID und STAGE_1_EVENT_ID verweisen nur auf Events.
        Write(
            _roots.Mod,
            "events/m.txt",
            "namespace = m\ncountry_event = { id = m.10 is_triggered_only = yes }\n"
                + "inline_script = {\n\tscript = x\n\tEVENT_ID = m.5\n\tLOCALIZATION_ID = m.5\n\tSTAGE_1_EVENT_ID = m.10\n}\n"
                + "inline_script = {\n\tscript = x\n\tEVENT_ID = m.6\n\tLOCALIZATION_ID = m.5\n}\n"
                + "country_event = { id = m.7 is_triggered_only = yes }\ncountry_event = { id = m.7 is_triggered_only = yes }");

        var duplicates = Of(Analyze("stellaris"), FindingKind.DuplicateEvent);

        Assert.Equal(new[] { "m.7", "m.7" }, duplicates.Select(f => f.Name));
    }

    [Fact]
    public void InlineScriptEventNotBroken()
    {
        Write(_roots.Vanilla, "events/shroud.txt", "inline_script = {\n\tscript = events/x\n\tEVENT_ID = shroud.10420\n}");
        Write(_roots.Vanilla, "common/on_actions/v.txt", "on_game_start = {\n\tevents = { shroud.10420 }\n}");

        Assert.Empty(Of(Analyze("stellaris"), FindingKind.BrokenReference));
    }

    [Fact]
    public void InlineScriptModEventNotDead()
    {
        Write(_roots.Mod, "events/m.txt", "inline_script = {\n\tscript = events/x\n\tEVENT_ID = m.5\n}");

        var result = Analyze("stellaris");

        Assert.Empty(Of(result, FindingKind.DeadEvent));
        Assert.Empty(Of(result, FindingKind.BrokenReference));
    }

    [Fact]
    public void NoBrokenWhenDefinedInVanilla()
    {
        Write(_roots.Vanilla, "events/v.txt", "namespace = v\nv.5 = {\n\timmediate = { }\n}");
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\ttrigger_event = v.5\n\t}\n}");
        CallFromVanilla("m.1");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void Eu5NoBrokenWhenDefinedInVanillaDlc()
    {
        Write(_roots.Vanilla, "dlc/D001_x/in_game/events/v.txt", "namespace = v\nv.5 = {\n\timmediate = { }\n}");
        Write(_roots.Vanilla, "in_game/common/on_action/v.txt", "on_game_start = {\n\tevents = { m.1 }\n}");
        Write(_roots.Mod, "in_game/events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\ttrigger_event_silently = v.5\n\t}\n}");

        Assert.Empty(Analyze("eu5").Findings);
    }

    [Fact]
    public void EventIdCaseIgnoredNoBrokenNoDead()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\ttrigger_event = NS.1\n\t}\n}");
        Write(_roots.Mod, "events/ns.txt", "namespace = ns\nns.1 = {\n\timmediate = { }\n}");
        CallFromVanilla("m.1");

        var result = Analyze();
        Assert.Empty(Of(result, FindingKind.BrokenReference));
        Assert.Empty(Of(result, FindingKind.DeadEvent));
    }

    [Fact]
    public void ParameterisedVanillaCallKeepsNamespaceAlive()
    {
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "v_fx = {\n\ttrigger_event = ns.$X$\n}");
        Write(_roots.Mod, "events/ns.txt", "namespace = ns\nns.5 = {\n\timmediate = { }\n}");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void ParameterisedModCallNotBroken()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\ttrigger_event = NS.$X$\n}");
        Write(_roots.Mod, "events/ns.txt", "namespace = ns\nns.5 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}\nother.1 = {\n\timmediate = { }\n}");

        var result = Analyze();

        Assert.Empty(Of(result, FindingKind.BrokenReference));
        Assert.Empty(Of(result, FindingKind.EventCycle));
        Assert.Equal(new[] { "other.1" }, Of(result, FindingKind.DeadEvent).Select(f => f.Name));
    }

    [Fact]
    public void LeadingZerosNoBrokenNoDead()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.0001 = {\n\timmediate = { }\n}\nm.2 = {\n\timmediate = {\n\t\ttrigger_event = m.1\n\t}\n}");
        CallFromVanilla("m.0002");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void BrokenBlockNeedsBuiltIns()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\tghost_effect = yes\n\t\thidden = yes\n\t\tmy_fx = yes\n\t}\n}");
        CallFromVanilla("m.1");

        var withoutHidden = Analyze(builtIns: new HashSet<string>(StringComparer.Ordinal) { "add_gold" });
        Assert.True(withoutHidden.BlockReferencesChecked);
        var broken = Of(withoutHidden, FindingKind.BrokenReference);
        Assert.Equal(new[] { "ghost_effect", "hidden" }, broken.Select(f => f.Name));
        var ghost = broken[0];
        Assert.Equal(FindingSeverity.Error, ghost.Severity);
        Assert.Equal(CheckKind.Effect, ghost.NameKind);
        Assert.Equal(4, ghost.Location.Line);
        Assert.Equal(new[] { "m.1 (events/m.txt:4)" }, ghost.Related);

        var withHidden = Analyze(builtIns: new HashSet<string>(StringComparer.Ordinal) { "add_gold", "hidden" });
        Assert.Equal("ghost_effect", Assert.Single(Of(withHidden, FindingKind.BrokenReference)).Name);

        var unknown = Analyze();
        Assert.False(unknown.BlockReferencesChecked);
        Assert.Empty(Of(unknown, FindingKind.BrokenReference));
    }

    [Fact]
    public void NegatedCallToUnknownNameIsBroken()
    {
        Write(_roots.Mod, "common/scripted_triggers/t.txt", "my_trigger = {\n\talways = yes\n}");
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\ttrigger = {\n\t\tmy_trigger = no\n\t\tghost_trigger = no\n\t\tis_adult = no\n\t}\n}");
        CallFromVanilla("m.1");

        var broken = Of(Analyze(builtIns: new HashSet<string>(StringComparer.Ordinal) { "always", "is_adult" }), FindingKind.BrokenReference);

        Assert.Equal("ghost_trigger", Assert.Single(broken).Name);
    }

    [Fact]
    public void UppercaseKeyIsNoCallName()
    {
        // HOI IV: GER = { NOR_AAT = yes } ist ein Landes-Scope mit Großbuchstaben-Kind, kein Aufruf; ebenso ein Parameter wie AVOID_STORMS = no.
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\tGER = { NOR_AAT = yes }\n\t\tinline_script = { AVOID_STORMS = no }\n\t\tghost_effect = yes\n\t}\n}");
        CallFromVanilla("m.1");

        var broken = Of(Analyze(builtIns: new HashSet<string>(StringComparer.Ordinal) { "add_gold", "inline_script" }), FindingKind.BrokenReference);

        Assert.Equal("ghost_effect", Assert.Single(broken).Name);
    }

    [Fact]
    public void RuleKeywordsNotBroken()
    {
        Write(
            _roots.Mod,
            "events/m.txt",
            "namespace = m\nm.1 = {\n\timmediate = {\n\t\tif = {\n\t\t\tlimit = { NOT = { THIS = $X$ } NOR = { THIS = scope:a } OR = { THIS = ROOT } }\n\t\t\thidden_effect = { FLAG = yes }\n\t\t}\n\t\tif = { limit = { THIS = ROOT } }\n\t\tghost_effect = yes\n\t\tghost_block = { FLAG = yes }\n\t}\n}");
        CallFromVanilla("m.1");

        var broken = Of(Analyze(builtIns: new HashSet<string>(StringComparer.Ordinal) { "add_gold" }), FindingKind.BrokenReference);

        Assert.Equal(new[] { "ghost_effect", "ghost_block" }, broken.Select(f => f.Name));
    }

    [Fact]
    public void LowercaseArgumentsOfScriptedEffectNotBroken()
    {
        // EU IV/EU V: Skripteffekte mit Kleinbuchstaben-Parametern (select_suitable_patriarch = { pentarchy = yes }).
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "v_eff = {\n\tif = { limit = { always = $pentarchy$ } add_gold = 1 }\n}");
        // Der Körper einer Definition ist kein Aufruf: body_ghost bleibt gemeldet.
        Write(_roots.Mod, "common/scripted_effects/m.txt", "m_eff = {\n\tbody_ghost = yes\n\tv_eff = { pentarchy = yes }\n}");
        Write(
            _roots.Mod,
            "events/m.txt",
            "namespace = m\nm.1 = {\n\timmediate = {\n\t\tm_eff = yes\n\t\tv_eff = { target = root pentarchy = yes }\n\t\tadd_gold = { flag_x = yes }\n\t\tghost_effect = yes\n\t}\n}");
        CallFromVanilla("m.1");

        var broken = Of(Analyze(builtIns: new HashSet<string>(StringComparer.Ordinal) { "add_gold", "always" }), FindingKind.BrokenReference);

        Assert.Equal(new[] { "body_ghost", "flag_x", "ghost_effect" }, broken.Select(f => f.Name));
    }

    [Fact]
    public void DeadEvent()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.DeadEvent, finding.Kind);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal("m.1", finding.Name);
        Assert.Equal(CheckKind.Event, finding.NameKind);
        Assert.Equal("events/m.txt", finding.Location.RelativePath);
        Assert.Equal(2, finding.Location.Line);
        Assert.Empty(finding.Related);
        Assert.Equal("DeadEvent|m.1", finding.Key);
    }

    [Fact]
    public void NotDeadWhenOnlyVanillaOnActionMentions()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");
        CallFromVanilla("m.1");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void NotDeadWhenSelfFiring()
    {
        Write(_roots.Mod, "events/m.txt",
            "namespace = m\ncountry_event = {\n\tid = m.1\n\toption = { }\n}\ncountry_event = {\n\tid = m.2\n\tis_triggered_only = yes\n\toption = { }\n}");

        var finding = Assert.Single(Analyze("eu4").Findings);

        Assert.Equal(FindingKind.DeadEvent, finding.Kind);
        Assert.Equal("m.2", finding.Name);
    }

    [Fact]
    public void SelfTriggerStillDead()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\toption = {\n\t\ttrigger_event = m.1\n\t}\n}");

        var finding = Assert.Single(Of(Analyze(), FindingKind.DeadEvent));

        Assert.Equal("m.1", finding.Name);
    }

    [Fact]
    public void NotDeadWhenMentionedInGui()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");
        Write(_roots.Mod, "gui/m.gui", "widget = {\n\ttext = \"m.1\"\n}");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void UnusedEffect()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.UnusedBlock, finding.Kind);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal("my_fx", finding.Name);
        Assert.Equal(CheckKind.Effect, finding.NameKind);
        Assert.Equal("common/scripted_effects/fx.txt", finding.Location.RelativePath);
        Assert.Equal(1, finding.Location.Line);
        Assert.Equal("UnusedBlock|my_fx", finding.Key);
    }

    [Fact]
    public void RecursiveEffectWithoutOutsideCallerIsUnused()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt",
            "my_eff = {\n\tif = {\n\t\tlimit = { always = no }\n\t\tmy_eff = yes\n\t}\n}\nloop_eff = {\n\tloop_eff = yes\n}");

        var result = Analyze();

        Assert.Equal(new[] { "loop_eff", "my_eff" }, Of(result, FindingKind.UnusedBlock).Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(Of(result, FindingKind.UnusedBlock), f => Assert.Equal(CheckKind.Effect, f.NameKind));
    }

    [Fact]
    public void UsedEffectInOtherMod()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");
        Write(_roots.Other, "events/o.txt", "namespace = o\no.1 = {\n\timmediate = {\n\t\tmy_fx = yes\n\t}\n}");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void LocalisationCallMarksValueUsed()
    {
        Write(_roots.Mod, "common/script_values/sv.txt", "my_sv = 1\nother_sv = 2");
        Write(_roots.Mod, "localization/english/m_l_english.yml", "l_english:\n a:0 \"[GetPlayer.MakeScope.ScriptValue('my_sv')|0] other_sv\"");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.UnusedBlock, finding.Kind);
        Assert.Equal("other_sv", finding.Name);
    }

    [Fact]
    public void GermanOnlyRootStillCountsItsCalls()
    {
        Write(_roots.Mod, "common/script_values/sv.txt", "my_sv = 1");
        Write(_roots.Mod, "localization/german/m_l_german.yml", "l_german:\n a:0 \"[GetPlayer.MakeScope.ScriptValue('my_sv')|0]\"");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void GermanNextToEnglishIsNotRead()
    {
        Write(_roots.Mod, "common/script_values/sv.txt", "my_sv = 1");
        Write(_roots.Mod, "localization/english/m_l_english.yml", "l_english:\n a:0 \"nothing\"");
        Write(_roots.Mod, "localization/german/m_l_german.yml", "l_german:\n a:0 \"[GetPlayer.MakeScope.ScriptValue('my_sv')|0]\"");

        Assert.Equal("my_sv", Assert.Single(Analyze().Findings).Name);
    }

    [Fact]
    public void UnusedValueUsedViaValuePrefix()
    {
        Write(_roots.Mod, "common/script_values/sv.txt", "my_sv = 1\nmy_other_sv = 2");
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "v_fx = {\n\tadd_gold = value:my_sv|P|1|\n}");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.UnusedBlock, finding.Kind);
        Assert.Equal("my_other_sv", finding.Name);
        Assert.Equal(CheckKind.Value, finding.NameKind);
        Assert.Equal(2, finding.Location.Line);
    }

    [Fact]
    public void DuplicateWithinMod()
    {
        Write(_roots.Mod, "events/a.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");
        Write(_roots.Mod, "events/b.txt", "namespace = m\n\nm.0001 = {\n\timmediate = { }\n}");
        CallFromVanilla("m.1");

        var findings = Of(Analyze(), FindingKind.DuplicateEvent);

        Assert.Equal(2, findings.Count);
        Assert.All(findings, f => Assert.Equal(FindingSeverity.Warning, f.Severity));
        Assert.Equal(new[] { ("events/a.txt", 2), ("events/b.txt", 3) }, findings.Select(f => (f.Location.RelativePath, f.Location.Line)));
        var expected = new[] { "events/a.txt:2 (My Mod)", "events/b.txt:3 (My Mod)" };
        Assert.All(findings, f => Assert.Equal(expected, f.Related));
        Assert.Equal("DuplicateEvent|m.1", findings[0].Key);
        Assert.Equal("DuplicateEvent|m.0001", findings[1].Key);
    }

    [Fact]
    public void DuplicateWithOtherMod()
    {
        Write(_roots.Other, "events/o.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");
        CallFromVanilla("m.1");

        var finding = Assert.Single(Of(Analyze(), FindingKind.DuplicateEvent));

        Assert.Equal(_roots.Mod, finding.Location.RootDirectory);

        // Events: zuerst geladen gewinnt; die andere Mod lädt vor der gewählten.
        Assert.Equal(new[] { "events/o.txt:2 (Other Mod)", "events/m.txt:2 (My Mod)" }, finding.Related);
    }

    [Fact]
    public void DuplicateInSameFile()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}\nm.1 = {\n\toption = { }\n}");
        CallFromVanilla("m.1");

        var findings = Of(Analyze(), FindingKind.DuplicateEvent);

        Assert.Equal(new[] { 2, 5 }, findings.Select(f => f.Location.Line));
        Assert.All(findings, f => Assert.Equal(new[] { "events/m.txt:2 (My Mod)", "events/m.txt:5 (My Mod)" }, f.Related));
    }

    [Fact]
    public void NoDuplicateWhenOverridingVanilla()
    {
        Write(_roots.Vanilla, "events/v.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");
        CallFromVanilla("m.1");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void EffectCycleUnconditionalError()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt", "b_fx = {\n\ta_fx = yes\n}\na_fx = {\n\tb_fx = yes\n}");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.EffectCycle, finding.Kind);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
        Assert.Equal("a_fx → b_fx", finding.Name);
        Assert.Equal(CheckKind.Effect, finding.NameKind);
        Assert.Equal("common/scripted_effects/fx.txt", finding.Location.RelativePath);
        Assert.Equal(4, finding.Location.Line);
        Assert.Equal(new[] { "a_fx", "b_fx" }, finding.Related);
        Assert.Equal("EffectCycle|a_fx → b_fx", finding.Key);
    }

    [Fact]
    public void UnconditionalCycleThroughAndIsError()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt", "a_fx = { b_fx = yes }\nb_fx = { AND = { a_fx = yes } }");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.EffectCycle, finding.Kind);
        Assert.Equal(FindingSeverity.Error, finding.Severity);
    }

    [Fact]
    public void ScopeChangeCycleIsHint()
    {
        Write(_roots.Mod, "common/scripted_triggers/t.txt", "t = { OR = { AND = { overlord ?= { t = yes } } } }");

        var finding = Assert.Single(Of(Analyze("vic3"), FindingKind.EffectCycle));

        Assert.Equal(FindingKind.EffectCycle, finding.Kind);
        Assert.Equal(FindingSeverity.Hint, finding.Severity);
    }

    [Fact]
    public void EffectCycleInIfHint()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt", "a_fx = {\n\tb_fx = yes\n}\nb_fx = {\n\tif = {\n\t\tlimit = { always = yes }\n\t\ta_fx = yes\n\t}\n}");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.EffectCycle, finding.Kind);
        Assert.Equal(FindingSeverity.Hint, finding.Severity);
        Assert.Equal(1, finding.Location.Line);
    }

    [Fact]
    public void RecursiveEffectIsCycle()
    {
        Write(_roots.Mod, "common/scripted_effects/fx.txt", "a_fx = {\n\tif = {\n\t\tlimit = { always = yes }\n\t\ta_fx = yes\n\t}\n}");
        Write(_roots.Vanilla, "events/v.txt", "namespace = v\nv.1 = {\n\timmediate = {\n\t\ta_fx = yes\n\t}\n}");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.EffectCycle, finding.Kind);
        Assert.Equal(FindingSeverity.Hint, finding.Severity);
        Assert.Equal("a_fx", finding.Name);
    }

    [Fact]
    public void EventCycleHint()
    {
        Write(_roots.Mod, "events/m.txt",
            "namespace = m\nm.2 = {\n\toption = {\n\t\ttrigger_event = m.1\n\t}\n}\nm.1 = {\n\toption = {\n\t\ttrigger_event = m.2\n\t}\n}");
        CallFromVanilla("m.1");

        var finding = Assert.Single(Analyze().Findings);

        Assert.Equal(FindingKind.EventCycle, finding.Kind);
        Assert.Equal(FindingSeverity.Hint, finding.Severity);
        Assert.Equal("m.1 → m.2", finding.Name);
        Assert.Equal(CheckKind.Event, finding.NameKind);
        Assert.Equal(7, finding.Location.Line);
        Assert.Equal(new[] { "m.1", "m.2" }, finding.Related);
    }

    [Fact]
    public void VanillaOnlyCycleIgnored()
    {
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "v_a = {\n\tv_b = yes\n}\nv_b = {\n\tv_a = yes\n}");
        Write(_roots.Vanilla, "events/v.txt", "namespace = v\nv.1 = {\n\toption = {\n\t\ttrigger_event = v.2\n\t}\n}\nv.2 = {\n\toption = {\n\t\ttrigger_event = v.1\n\t}\n}");
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\ttrigger_event = v.1\n\t}\n}");
        CallFromVanilla("m.1");

        Assert.Empty(Analyze().Findings);
    }

    [Fact]
    public void FindingsOrderedBySeverityThenPath()
    {
        Write(_roots.Mod, "events/b.txt", "namespace = b\nb.1 = {\n\timmediate = { }\n}");
        Write(_roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = { }\n}\na.2 = {\n\timmediate = {\n\t\ttrigger_event = a.9\n\t}\n}");

        var findings = Analyze().Findings;

        Assert.Equal(
            new[] { (FindingKind.BrokenReference, "a.9"), (FindingKind.DeadEvent, "a.1"), (FindingKind.DeadEvent, "a.2"), (FindingKind.DeadEvent, "b.1") },
            findings.Select(f => (f.Kind, f.Name)));
    }

    [Fact]
    public void NoActiveModNoFindings()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { }\n}");
        var rules = ScriptCheckTestRoots.Rules();
        var index = new ScriptCheckBuilder(_roots.Sources(withActiveMod: false), rules).Build();

        var result = ScriptCheckAnalyzer.Analyze(index, new HashSet<string>(StringComparer.Ordinal) { "add_gold" }, rules);

        Assert.Empty(result.Findings);
    }

    [Fact]
    public void ModCopyOfForeignDefinitionIsNeverUnusedOrDead()
    {
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "shared_effect = { }");
        Write(_roots.Vanilla, "common/scripted_triggers/v.txt", "shared_trigger = { always = yes }");
        Write(_roots.Vanilla, "common/script_values/v.txt", "shared_value = 1");
        Write(_roots.Other, "common/scripted_effects/o.txt", "other_mod_effect = { }");
        Write(_roots.Vanilla, "events/v.txt", "namespace = s\ns.1 = { }");
        Write(_roots.Mod, "events/m.txt", "namespace = s\ns.0001 = { }\ns.2 = { }");
        Write(_roots.Mod, "common/scripted_effects/m.txt", "shared_effect = { }\nother_mod_effect = { }\nmod_only_effect = { }");
        Write(_roots.Mod, "common/scripted_triggers/m.txt", "shared_trigger = { always = yes }\nmod_only_trigger = { always = yes }");
        Write(_roots.Mod, "common/script_values/m.txt", "shared_value = 1\nmod_only_value = 1");

        var result = Analyze();

        Assert.Equal(new[] { "s.2" }, Of(result, FindingKind.DeadEvent).Select(f => f.Name));
        Assert.Equal(
            new[] { "mod_only_effect", "mod_only_trigger", "mod_only_value" },
            Of(result, FindingKind.UnusedBlock).Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void ForeignDefinitionWithoutModCopyMakesNoFinding()
    {
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "vanilla_only_effect = { }");
        Write(_roots.Vanilla, "events/v.txt", "namespace = v\nv.1 = { }");
        Write(_roots.Mod, "common/scripted_effects/m.txt", "mod_effect = { }");

        var result = Analyze();

        Assert.Empty(Of(result, FindingKind.DeadEvent));
        Assert.Equal(new[] { "mod_effect" }, Of(result, FindingKind.UnusedBlock).Select(f => f.Name));
    }

    [Fact]
    public void TemplateCallMarksMatchingDefinitionsUsed()
    {
        Write(_roots.Mod, "common/scripted_effects/m.txt", "rvt_tmpl_foo_effect = { }\nrvt_tmpl_bar_effect = { }\nrvt_tmpl__effect = { }\nxrvt_tmpl_foo_effect = { }\nrvt_tmpl_foo_effect_x = { }");
        Write(_roots.Mod, "common/scripted_triggers/m.txt", "rvt_tmpl_baz_effect = { always = yes }");
        Write(_roots.Mod, "common/script_values/m.txt", "rvt_tmpl_val_effect = 1");
        Write(_roots.Vanilla, "events/v.txt", "namespace = v\nv.1 = {\n\timmediate = {\n\t\trvt_tmpl_$X$_effect = yes\n\t}\n}");

        var unused = Of(Analyze(), FindingKind.UnusedBlock).Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "rvt_tmpl__effect", "rvt_tmpl_foo_effect_x", "xrvt_tmpl_foo_effect" }, unused);
    }

    [Fact]
    public void TemplateAsScalarMarksDefinitionUsed()
    {
        Write(_roots.Mod, "common/script_values/m.txt", "kurultai_a_cost = 1\nkurultai_b_cost = 2");
        Write(_roots.Vanilla, "common/scripted_effects/v.txt", "user_effect = {\n\tadd_gold = value:kurultai_$ID$_cost\n}");

        Assert.Empty(Of(Analyze(), FindingKind.UnusedBlock));
    }

    [Fact]
    public void TemplateWithoutLiteralTextMarksNothing()
    {
        Write(_roots.Mod, "common/scripted_effects/m.txt", "my_effect = { }\na_b = { }");
        Write(_roots.Vanilla, "events/v.txt", "namespace = v\nv.1 = {\n\timmediate = {\n\t\t$A$ = yes\n\t\t$A$_b = yes\n\t}\n}");

        var unused = Of(Analyze(), FindingKind.UnusedBlock).Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(new[] { "a_b", "my_effect" }, unused);
    }

    [Fact]
    public void TemplateInsideTheSameDefinitionDoesNotCount()
    {
        Write(_roots.Mod, "common/scripted_effects/m.txt", "rvt_tmpl_foo_effect = {\n\trvt_tmpl_$X$_effect = yes\n}\nrvt_tmpl_bar_effect = { }");

        var unused = Of(Analyze(), FindingKind.UnusedBlock).Select(f => f.Name);

        // bar wird aus foo erreicht, foo nur aus sich selbst.
        Assert.Equal(new[] { "rvt_tmpl_foo_effect" }, unused);
    }

    [Fact]
    public void TemplateDoesNotMakeEventsAlive()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = abc_x\nabc_x.1 = { }");
        Write(_roots.Vanilla, "events/v.txt", "namespace = v\nv.1 = {\n\timmediate = {\n\t\tabc_$X$ = yes\n\t}\n}");

        Assert.Single(Of(Analyze(), FindingKind.DeadEvent));
    }

    [Fact]
    public void CycleThroughTooltipIsOnlyAHint()
    {
        Write(_roots.Mod, "common/scripted_effects/m.txt", "a_eff = {\n\tb_eff = yes\n}\nb_eff = {\n\tshow_as_tooltip = {\n\t\ta_eff = yes\n\t}\n}");

        var finding = Assert.Single(Of(Analyze(), FindingKind.EffectCycle));

        Assert.Equal(FindingSeverity.Hint, finding.Severity);
    }

    [Fact]
    public void CallInsideTooltipIsStillChecked()
    {
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\tshow_as_tooltip = {\n\t\tno_such_effect = yes\n\t}\n}");
        CallFromVanilla("m.1");

        var finding = Assert.Single(Of(Analyze("ck3", new HashSet<string>(StringComparer.Ordinal) { "add_gold" }), FindingKind.BrokenReference));

        Assert.Equal("no_such_effect", finding.Name);
    }

    [Fact]
    public void CallContextKeysAreKnownNames()
    {
        var rules = new ScriptCheckRules
        {
            EventFolders = new[] { "events" },
            ScriptContextKeys = new[] { "immediate" },
            CallContextKeys = new[] { "on_complete" },
        };
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = {\n\t\ton_complete = yes\n\t}\n}");

        var index = new ScriptCheckBuilder(_roots.Sources(), rules).Build();
        var result = ScriptCheckAnalyzer.Analyze(index, new HashSet<string>(StringComparer.Ordinal) { "add_gold" }, rules);

        Assert.Empty(Of(result, FindingKind.BrokenReference));
    }

    [Fact]
    public void ModCopyOfVanillaFileAtSamePathIsNeverUnusedOrDead()
    {
        // Gleicher Pfad: Die Mod-Datei ersetzt die Vanilla-Datei, die deshalb nicht im Index steht; ihre Namen sind trotzdem keine Mod-Erfindung.
        Write(_roots.Vanilla, "common/script_values/same.txt", "shared_value = 1");
        Write(_roots.Mod, "common/script_values/same.txt", "shared_value = 1\nnew_value = 1");
        Write(_roots.Vanilla, "common/scripted_effects/same.txt", "shared_effect = { }");
        Write(_roots.Mod, "common/scripted_effects/same.txt", "shared_effect = { }\nnew_effect = { }");
        Write(_roots.Vanilla, "events/same.txt", "namespace = s\ns.1 = { }");
        Write(_roots.Mod, "events/same.txt", "namespace = s\ns.1 = { }\ns.2 = { }");

        var result = Analyze();

        Assert.Equal(new[] { "s.2" }, Of(result, FindingKind.DeadEvent).Select(f => f.Name));
        Assert.Equal(new[] { "new_effect", "new_value" }, Of(result, FindingKind.UnusedBlock).Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void ReplacedVanillaFileMakesNoCallersAndNoDefinitions()
    {
        // Die ersetzte Vanilla-Datei zählt nur für „gibt es auch außerhalb der Mod“: weder als Definition für Aufrufe noch als Aufrufer.
        Write(_roots.Vanilla, "common/scripted_effects/same.txt", "gone_effect = { }\ncaller_effect = {\n\tnew_effect = yes\n}");
        Write(_roots.Mod, "common/scripted_effects/same.txt", "new_effect = { }\nuser_effect = {\n\tgone_effect = yes\n}");

        var result = Analyze("ck3", new HashSet<string>(StringComparer.Ordinal) { "add_gold" });

        Assert.Equal(new[] { "gone_effect" }, Of(result, FindingKind.BrokenReference).Select(f => f.Name));
        Assert.Contains("new_effect", Of(result, FindingKind.UnusedBlock).Select(f => f.Name));
    }
}
