using System.Text.Json.Nodes;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class PerformanceAnalyzerTests : IDisposable
{
    private static readonly IReadOnlySet<string> NoBuiltIns = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Leistungsregeln für CK III (Puls-On-Actions, On-Action-Listen, Entscheidungen).</summary>
    private const string Ck3Performance = """
        {
          "globalIterators": [
            { "name": "every_country", "narrower": ["every_neighboring_country"] },
            { "name": "any_country" },
            { "name": "every_province" }
          ],
          "pulseOnActions": { "yearly_global_pulse": "yearly", "on_monthly_pulse": "monthly" },
          "onActionCallLists": ["on_actions"],
          "alwaysCheckedBlocks": [{ "folder": "common/decisions", "blocks": ["is_shown", "is_valid"] }]
        }
        """;

    /// <summary>Leistungsregeln für EU IV (abgefragte Event-Trigger, Entscheidungen in <c>country_decisions</c>).</summary>
    private const string Eu4Performance = """
        {
          "globalIterators": [{ "name": "every_country" }, { "name": "any_country" }],
          "pulseOnActions": { "on_monthly_pulse": "monthly" },
          "alwaysCheckedBlocks": [{ "folder": "decisions", "blocks": ["potential", "allow"], "wrappers": ["country_decisions"] }],
          "polledEventBlocks": ["trigger"]
        }
        """;

    private readonly ScriptCheckTestRoots _roots = new();

    public void Dispose() => _roots.Dispose();

    private static string Write(string root, string relative, string text) => ScriptCheckTestRoots.Write(root, relative, text);

    /// <summary>Regeln des Spielprofils mit dem Objekt <c>performance</c> (null = ohne).</summary>
    private ScriptCheckRules Rules(string game, string? performance)
    {
        if (performance is null)
        {
            return ScriptCheckTestRoots.Rules(game);
        }

        var source = Path.Combine(ScriptCheckTestRoots.ProfilesDir, RepoFiles.Profile(game).ScriptCheckRules!.Replace('/', Path.DirectorySeparatorChar));
        var json = JsonNode.Parse(File.ReadAllText(source), documentOptions: new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true })!;
        json["performance"] = JsonNode.Parse(performance);
        var path = Path.Combine(_roots.Temp, game + "-rules.json");
        File.WriteAllText(path, json.ToJsonString());
        return ScriptCheckRules.Load(path);
    }

    private IReadOnlyList<Finding> Analyze(string game = "ck3", string? performance = Ck3Performance)
    {
        var rules = Rules(game, performance);
        var index = new ScriptCheckBuilder(_roots.Sources(game), rules).Build();
        return PerformanceAnalyzer.Analyze(index, rules);
    }

    private AnalyzerResult AnalyzeAll(string? performance)
    {
        var rules = Rules("ck3", performance);
        var index = new ScriptCheckBuilder(_roots.Sources("ck3"), rules).Build();
        return ScriptCheckAnalyzer.Analyze(index, NoBuiltIns, rules);
    }

    private void Pulse(string pulse, string events) =>
        Write(_roots.Vanilla, "common/on_action/v.txt", pulse + " = {\n\tevents = { " + events + " }\n}");

    private void ModEvent(string body) => Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n" + body + "\n}");

    private void ModEffect(string body) => Write(_roots.Mod, "common/scripted_effects/m.txt", "m_eff = {\n" + body + "\n}");

    private void PulseEventEffect(string pulse)
    {
        Pulse(pulse, "m.1");
        ModEvent("\timmediate = { m_eff = yes }");
        ModEffect("\tevery_country = { limit = { is_ai = no } }");
    }

    [Fact]
    public void PulseToEventToEffect()
    {
        PulseEventEffect("yearly_global_pulse");

        var finding = Assert.Single(Analyze());

        Assert.Equal(FindingKind.ExpensiveIterator, finding.Kind);
        Assert.Equal(FindingSeverity.Hint, finding.Severity);
        Assert.Equal("every_country", finding.Name);
        Assert.Equal(CheckKind.Effect, finding.NameKind);
        Assert.Equal(_roots.Mod, finding.Location.RootDirectory);
        Assert.Equal("common/scripted_effects/m.txt", finding.Location.RelativePath);
        Assert.Equal(2, finding.Location.Line);
        Assert.Equal("ExpensiveIterator|m_eff|every_country", finding.Key);
        var info = finding.Performance!;
        Assert.Equal(PerformanceRate.Yearly, info.Rate);
        Assert.Equal(new[] { "yearly_global_pulse", "m.1", "m_eff" }, info.Path);
        Assert.Equal(info.Path, finding.Related);
        Assert.Null(info.Inner);
        Assert.Equal(new[] { 2 }, info.Lines);
    }

    [Fact]
    public void MonthlyIsWarning()
    {
        PulseEventEffect("on_monthly_pulse");

        var finding = Assert.Single(Analyze());

        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(PerformanceRate.Monthly, finding.Performance!.Rate);
        Assert.Equal(new[] { "on_monthly_pulse", "m.1", "m_eff" }, finding.Performance.Path);
    }

    [Fact]
    public void EventWithoutPulseSilent()
    {
        PulseEventEffect("on_game_start");

        Assert.Empty(Analyze());
    }

    [Fact]
    public void NestedOnActionInheritsRate()
    {
        Write(_roots.Vanilla, "common/on_action/v.txt", "yearly_global_pulse = {\n\ton_actions = { m_pulse }\n}");
        Write(_roots.Mod, "common/on_action/m.txt", "m_pulse = {\n\tevents = { m.1 }\n}");
        ModEvent("\timmediate = { m_eff = yes }");
        ModEffect("\tevery_country = { add_gold = 1 }");

        var finding = Assert.Single(Analyze());

        Assert.Equal(new[] { "yearly_global_pulse", "m_pulse", "m.1", "m_eff" }, finding.Performance!.Path);
        Assert.Equal(PerformanceRate.Yearly, finding.Performance.Rate);
    }

    [Fact]
    public void NestedIteratorRaisesToWarning()
    {
        Pulse("yearly_global_pulse", "m.1");
        ModEvent("\timmediate = { m_eff = yes }");
        ModEffect("\tevery_province = {\n\t\tany_country = { is_ai = no }\n\t}");

        var finding = Assert.Single(Analyze());

        Assert.Equal("every_province", finding.Name);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal("any_country", finding.Performance!.Inner);
        Assert.Equal(PerformanceRate.Yearly, finding.Performance.Rate);
    }

    private const string Ck3Decision = "my_decision = {\n\tis_shown = {\n\t\tany_country = { is_ai = no }\n\t}\n\teffect = {\n\t\tevery_country = { add_gold = 1 }\n\t}\n}";

    [Fact]
    public void DecisionIsShownWarning()
    {
        Write(_roots.Mod, "common/decisions/d.txt", Ck3Decision);

        var finding = Assert.Single(Analyze());

        Assert.Equal("any_country", finding.Name);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(3, finding.Location.Line);
        Assert.Equal(PerformanceRate.Checked, finding.Performance!.Rate);
        Assert.Equal(new[] { "my_decision (is_shown)" }, finding.Performance.Path);
        Assert.Equal("ExpensiveIterator|my_decision|any_country", finding.Key);
    }

    [Fact]
    public void DecisionEffectSilent()
    {
        Write(_roots.Mod, "common/decisions/d.txt", Ck3Decision);

        Assert.DoesNotContain(Analyze(), f => f.Name == "every_country");
    }

    [Fact]
    public void WrappedDecisionPotentialWarning()
    {
        Write(_roots.Mod, "decisions/d.txt",
            "country_decisions = {\n\tmy_dec = {\n\t\tpotential = { any_country = { ai = no } }\n\t\teffect = { every_country = { add_prestige = 1 } }\n\t}\n}");

        var finding = Assert.Single(Analyze("eu4", Eu4Performance));

        Assert.Equal("any_country", finding.Name);
        Assert.Equal(CheckKind.Other, finding.NameKind);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(PerformanceRate.Checked, finding.Performance!.Rate);
        Assert.Equal(new[] { "my_dec (potential)" }, finding.Performance.Path);
        Assert.Equal("ExpensiveIterator|my_dec|any_country", finding.Key);
    }

    [Fact]
    public void CheckedBeatsYearly()
    {
        Pulse("yearly_global_pulse", "m.1");
        ModEvent("\ttrigger = { m_trig = yes }");
        Write(_roots.Mod, "common/scripted_triggers/m.txt", "m_trig = {\n\tany_country = { is_ai = no }\n}");
        Write(_roots.Mod, "common/decisions/d.txt", "my_decision = {\n\tis_shown = { m_trig = yes }\n}");

        var finding = Assert.Single(Analyze());

        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(PerformanceRate.Checked, finding.Performance!.Rate);
        Assert.Equal(new[] { "my_decision (is_shown)", "m_trig" }, finding.Performance.Path);
    }

    [Fact]
    public void EventCycleEnds()
    {
        Pulse("yearly_global_pulse", "m.1");
        Write(_roots.Mod, "events/m.txt",
            "namespace = m\nm.1 = {\n\timmediate = {\n\t\ttrigger_event = m.2\n\t\tevery_country = { add_gold = 1 }\n\t}\n}\n"
                + "m.2 = {\n\timmediate = {\n\t\ttrigger_event = m.1\n\t\tany_country = { is_ai = no }\n\t}\n}");

        var findings = Analyze();

        Assert.Equal(2, findings.Count);
        var first = Assert.Single(findings, f => f.Name == "every_country");
        Assert.Equal(new[] { "yearly_global_pulse", "m.1" }, first.Performance!.Path);
        var second = Assert.Single(findings, f => f.Name == "any_country");
        Assert.Equal(new[] { "yearly_global_pulse", "m.1", "m.2" }, second.Performance!.Path);
    }

    [Fact]
    public void AnyWithoutLimitReported()
    {
        Pulse("on_monthly_pulse", "m.1");
        ModEvent("\timmediate = {\n\t\tif = {\n\t\t\tlimit = { any_country = { is_ai = no } }\n\t\t}\n\t}");

        var finding = Assert.Single(Analyze());

        Assert.Equal("any_country", finding.Name);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(new[] { "on_monthly_pulse", "m.1" }, finding.Performance!.Path);
    }

    [Fact]
    public void PolledEventTrigger()
    {
        Write(_roots.Mod, "events/m.txt", string.Join("\n",
            "namespace = m",
            "country_event = {",
            "\tid = m.1",
            "\ttrigger = { any_country = { ai = no } }",
            "\tmean_time_to_happen = { months = 12 }",
            "}",
            "country_event = {",
            "\tid = m.2",
            "\tmean_time_to_happen = { months = 12 }",
            "\toption = { name = x trigger = { any_country = { ai = no } } }",
            "}",
            "country_event = {",
            "\tid = m.3",
            "\tis_triggered_only = yes",
            "\ttrigger = { any_country = { ai = no } }",
            "}"));

        var finding = Assert.Single(Analyze("eu4", Eu4Performance));

        Assert.Equal("ExpensiveIterator|m.1|any_country", finding.Key);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(PerformanceRate.Checked, finding.Performance!.Rate);
        Assert.Equal(4, finding.Location.Line);
        Assert.Equal(new[] { "m.1 (trigger)" }, finding.Performance.Path);
    }

    [Fact]
    public void PolledEventTriggerStartsCalledTrigger()
    {
        Write(_roots.Mod, "events/m.txt", string.Join("\n",
            "namespace = m",
            "country_event = {",
            "\tid = m.1",
            "\tmean_time_to_happen = { months = 12 }",
            "\ttrigger = { m_trig = yes }",
            "}",
            "country_event = {",
            "\tid = m.2",
            "\tis_triggered_only = yes",
            "\ttrigger = { m_trig2 = yes }",
            "}"));
        Write(_roots.Mod, "common/scripted_triggers/m.txt",
            "m_trig = {\n\tany_country = { ai = no }\n}\nm_trig2 = {\n\tany_country = { ai = yes }\n}");

        var finding = Assert.Single(Analyze("eu4", Eu4Performance));

        Assert.Equal("ExpensiveIterator|m_trig|any_country", finding.Key);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(PerformanceRate.Checked, finding.Performance!.Rate);
        Assert.Equal(new[] { "m.1 (trigger)", "m_trig" }, finding.Performance.Path);
    }

    [Fact]
    public void TriggeredOnlyEventWithMtthGivesNoFinding()
    {
        Write(_roots.Mod, "events/m.txt", string.Join("\n",
            "namespace = m",
            "country_event = {",
            "\tid = m.1",
            "\tis_triggered_only = yes",
            "\tmean_time_to_happen = { months = 12 }",
            "\ttrigger = { any_country = { ai = no } }",
            "}"));

        Assert.Empty(Analyze("eu4", Eu4Performance));
    }

    [Fact]
    public void FocusesNamedByIdGiveOneFindingEach()
    {
        const string hoi4 = """
            {
              "globalIterators": [{ "name": "any_country" }],
              "pulseOnActions": { "on_monthly": "monthly" },
              "alwaysCheckedBlocks": [{ "folder": "common/national_focus", "blocks": ["available"], "wrappers": ["focus_tree"], "nameField": "id" }]
            }
            """;
        Write(_roots.Mod, "common/national_focus/m.txt", string.Join("\n",
            "focus_tree = {",
            "\tid = my_tree",
            "\tfocus = {",
            "\t\tid = FOC_a",
            "\t\tavailable = { any_country = { is_ai = no } }",
            "\t}",
            "\tfocus = {",
            "\t\tid = FOC_b",
            "\t\tavailable = { any_country = { is_ai = yes } }",
            "\t}",
            "}"));

        var findings = Analyze("hoi4", hoi4);

        Assert.Equal(new[] { "ExpensiveIterator|FOC_a|any_country", "ExpensiveIterator|FOC_b|any_country" }, findings.Select(f => f.Key));
        Assert.Equal(new[] { "FOC_a (available)" }, findings[0].Performance!.Path);
    }

    [Fact]
    public void SameKeyOneFinding()
    {
        Pulse("yearly_global_pulse", "m.1");
        ModEvent("\timmediate = { m_eff = yes }");
        ModEffect("\tevery_country = { add_gold = 1 }\n\tevery_country = { add_gold = 2 }");

        var finding = Assert.Single(Analyze());

        Assert.Equal(2, finding.Location.Line);
        Assert.Equal(new[] { 2, 3 }, finding.Performance!.Lines);
    }

    /// <summary>CK III mit täglichem und seltenem Puls.</summary>
    private const string RatesPerformance = """
        {
          "globalIterators": [{ "name": "every_country" }],
          "pulseOnActions": { "on_daily_pulse": "daily", "on_rare_pulse": "rare" }
        }
        """;

    [Fact]
    public void DailyIsWarning()
    {
        PulseEventEffect("on_daily_pulse");

        var finding = Assert.Single(Analyze(performance: RatesPerformance));

        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(PerformanceRate.Daily, finding.Performance!.Rate);
    }

    [Fact]
    public void RareIsHint()
    {
        PulseEventEffect("on_rare_pulse");

        var finding = Assert.Single(Analyze(performance: RatesPerformance));

        Assert.Equal(FindingSeverity.Hint, finding.Severity);
        Assert.Equal(PerformanceRate.Rare, finding.Performance!.Rate);
    }

    [Fact]
    public void LocationAnchorsOnFastestSite()
    {
        const string eu4 = """
            {
              "globalIterators": [{ "name": "any_country" }],
              "pulseOnActions": { "on_yearly_pulse": "yearly" },
              "polledEventBlocks": ["trigger"]
            }
            """;
        Write(_roots.Vanilla, "common/on_actions/v.txt", "on_yearly_pulse = {\n\tevents = { m.1 }\n}");
        Write(_roots.Mod, "events/m.txt", string.Join("\n",
            "namespace = m",
            "country_event = {",
            "\tid = m.1",
            "\timmediate = { any_country = { ai = no } }",
            "\tmean_time_to_happen = { months = 12 }",
            "\ttrigger = { any_country = { ai = yes } }",
            "}"));

        var finding = Assert.Single(Analyze("eu4", eu4));

        Assert.Equal(PerformanceRate.Checked, finding.Performance!.Rate);
        Assert.Equal(6, finding.Location.Line);
        Assert.Equal(new[] { "m.1 (trigger)" }, finding.Performance.Path);
        Assert.Equal(new[] { 4, 6 }, finding.Performance.Lines);
    }

    /// <summary>Mod-Trigger <c>y</c> ruft <c>x</c>; <c>x</c> ist Trigger und Effekt, nur der Effekt hat den Iterator.</summary>
    private void SameNamedTriggerAndEffect(string eventBody)
    {
        Pulse("on_monthly_pulse", "m.1");
        ModEvent(eventBody);
        Write(_roots.Mod, "common/scripted_triggers/m.txt", "y = {\n\tx = yes\n}\nx = {\n\tis_ai = no\n}");
        Write(_roots.Mod, "common/scripted_effects/m.txt", "x = {\n\tevery_country = { add_gold = 1 }\n}\ny2 = {\n\tx = yes\n}");
    }

    [Fact]
    public void TriggerReachedDoesNotRateSameNamedEffect()
    {
        SameNamedTriggerAndEffect("\ttrigger = { y = yes }");

        Assert.Empty(Analyze());
    }

    [Fact]
    public void EffectCallReachesSameNamedEffect()
    {
        SameNamedTriggerAndEffect("\timmediate = { y2 = yes }");

        var finding = Assert.Single(Analyze());

        Assert.Equal("ExpensiveIterator|x|every_country", finding.Key);
        Assert.Equal(CheckKind.Effect, finding.NameKind);
        Assert.Equal(new[] { "on_monthly_pulse", "m.1", "y2", "x" }, finding.Performance!.Path);
    }

    [Fact]
    public void NoPulsesNoFindings()
    {
        Write(_roots.Mod, "common/decisions/d.txt", Ck3Decision);

        Assert.Empty(Analyze(performance: """
            {
              "globalIterators": [{ "name": "any_country" }],
              "alwaysCheckedBlocks": [{ "folder": "common/decisions", "blocks": ["is_shown"] }]
            }
            """));
        Assert.Single(Analyze());
    }

    [Fact]
    public void ExcludedFolderNotChecked()
    {
        Write(_roots.Mod, "common/decisions/sub/d.txt", Ck3Decision);

        Assert.Empty(Analyze(performance: """
            {
              "globalIterators": [{ "name": "any_country" }],
              "pulseOnActions": { "on_monthly_pulse": "monthly" },
              "alwaysCheckedBlocks": [{ "folder": "common/decisions", "blocks": ["is_shown"], "excludeFolders": ["common/decisions/sub"] }]
            }
            """));
    }

    [Fact]
    public void NoPerformanceRules()
    {
        PulseEventEffect("on_monthly_pulse");
        Write(_roots.Mod, "common/decisions/d.txt", Ck3Decision);

        Assert.Empty(Analyze(performance: null));
        Assert.DoesNotContain(AnalyzeAll(null).Findings, f => f.Kind == FindingKind.ExpensiveIterator);

        var rules = Rules("ck3", Ck3Performance);
        var withoutMod = new ScriptCheckBuilder(_roots.Sources("ck3", withActiveMod: false), rules).Build();
        Assert.Empty(PerformanceAnalyzer.Analyze(withoutMod, rules));
    }

    [Fact]
    public void OtherKindsUnchanged()
    {
        PulseEventEffect("on_monthly_pulse");
        Write(_roots.Mod, "events/dead.txt", "namespace = d\nd.1 = {\n\timmediate = { }\n}");

        var without = AnalyzeAll(null).Findings;
        var with = AnalyzeAll(Ck3Performance).Findings;

        var dead = Assert.Single(with, f => f.Kind == FindingKind.DeadEvent);
        Assert.Equal(Assert.Single(without, f => f.Kind == FindingKind.DeadEvent).Key, dead.Key);
        Assert.Equal(
            without.Select(f => (f.Kind, f.Key, f.Location)),
            with.Where(f => f.Kind != FindingKind.ExpensiveIterator).Select(f => (f.Kind, f.Key, f.Location)));
        Assert.Single(with, f => f.Kind == FindingKind.ExpensiveIterator);
        Assert.All(with.Where(f => f.Kind != FindingKind.ExpensiveIterator), f => Assert.Null(f.Performance));
    }

    [Fact]
    public void NarrowerFromRule()
    {
        PulseEventEffect("yearly_global_pulse");
        ModEvent("\timmediate = {\n\t\tm_eff = yes\n\t\tany_country = { is_ai = no }\n\t}");

        var findings = Analyze();

        Assert.Equal(new[] { "every_neighboring_country" }, Assert.Single(findings, f => f.Name == "every_country").Performance!.Narrower);
        Assert.Empty(Assert.Single(findings, f => f.Name == "any_country").Performance!.Narrower);
    }

    /// <summary>CK III mit dem Gewichtungsblock <c>ai_will_do</c> der Entscheidungen.</summary>
    private const string AiWeightPerformance = """
        {
          "globalIterators": [{ "name": "any_country" }, { "name": "every_country" }],
          "pulseOnActions": { "on_monthly_pulse": "monthly" },
          "alwaysCheckedBlocks": [{ "folder": "common/decisions", "blocks": ["is_shown", "ai_will_do"] }]
        }
        """;

    private void AiWeightFixture()
    {
        Write(_roots.Mod, "common/decisions/d.txt", "my_dec = {\n\tai_will_do = {\n\t\tbase = 10\n\t\tmodifier = {\n\t\t\tfactor = 0\n\t\t\tmy_trigger = yes\n\t\t}\n\t}\n}");
        Write(_roots.Mod, "common/scripted_triggers/m.txt", "my_trigger = {\n\tany_country = { is_ai = no }\n}");
    }

    [Fact]
    public void AiWeightCalleeGetsChecked()
    {
        AiWeightFixture();

        var finding = Assert.Single(Analyze(performance: AiWeightPerformance));

        Assert.Equal("ExpensiveIterator|my_trigger|any_country", finding.Key);
        Assert.Equal(FindingSeverity.Warning, finding.Severity);
        Assert.Equal(PerformanceRate.Checked, finding.Performance!.Rate);
        Assert.Equal(new[] { "my_dec (ai_will_do)", "my_trigger" }, finding.Performance.Path);
    }

    [Fact]
    public void CheckedCallToEffectOnlyNameIgnored()
    {
        Write(_roots.Mod, "common/decisions/d.txt", "my_dec = {\n\tai_will_do = {\n\t\tmodifier = { my_eff = yes }\n\t}\n}");
        Write(_roots.Mod, "common/scripted_effects/m.txt", "my_eff = {\n\tevery_country = { add_gold = 1 }\n}");

        Assert.Empty(Analyze(performance: AiWeightPerformance));
    }

    [Fact]
    public void CheckedCallsLeaveOtherKindsUnchanged()
    {
        AiWeightFixture();
        // Kaputter Verweis (Event fehlt) und unbenutzter Effekt; dazu eine Aufrufform ohne Definition im Gewichtungsblock.
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { trigger_event = m.99 }\n}");
        Write(_roots.Mod, "common/scripted_effects/u.txt", "unused_eff = {\n\tadd_gold = 1\n}");
        Write(_roots.Mod, "common/decisions/e.txt", "other_dec = {\n\tai_will_do = {\n\t\tmodifier = { missing_trigger = yes }\n\t}\n}");

        var without = AnalyzeAll(null).Findings;
        var with = AnalyzeAll(AiWeightPerformance).Findings;

        Assert.Contains(without, f => f.Kind == FindingKind.BrokenReference);
        Assert.Contains(without, f => f.Kind == FindingKind.UnusedBlock && f.Name == "unused_eff");
        Assert.Equal(
            without.Select(f => (f.Kind, f.Key, f.Location, string.Join(",", f.Related))),
            with.Where(f => f.Kind != FindingKind.ExpensiveIterator).Select(f => (f.Kind, f.Key, f.Location, string.Join(",", f.Related))));
        Assert.Single(with, f => f.Kind == FindingKind.ExpensiveIterator);
    }

    [Fact]
    public void ShortestPathPerRate()
    {
        const string pulses = """
            {
              "globalIterators": [{ "name": "every_country" }],
              "pulseOnActions": { "a_pulse": "yearly", "b_pulse": "yearly" }
            }
            """;
        Write(_roots.Vanilla, "common/on_action/v.txt", "a_pulse = {\n\tevents = { m.1 }\n}\nb_pulse = {\n\teffect = { m_eff = yes }\n}");
        Write(_roots.Mod, "events/m.txt", "namespace = m\nm.1 = {\n\timmediate = { trigger_event = m.2 }\n}\nm.2 = {\n\timmediate = { m_eff = yes }\n}");
        ModEffect("\tevery_country = { add_gold = 1 }");

        var finding = Assert.Single(Analyze(performance: pulses));

        Assert.Equal(PerformanceRate.Yearly, finding.Performance!.Rate);
        Assert.Equal(new[] { "b_pulse", "m_eff" }, finding.Performance.Path);
    }

    [Fact]
    public void TopLevelOwnerNamedByField()
    {
        const string hoi4 = """
            {
              "globalIterators": [{ "name": "any_country" }],
              "pulseOnActions": { "on_monthly": "monthly" },
              "alwaysCheckedBlocks": [{ "folder": "common/national_focus", "blocks": ["available"], "wrappers": ["focus_tree"], "nameField": "id" }]
            }
            """;
        Write(_roots.Mod, "common/national_focus/m.txt", string.Join("\n",
            "shared_focus = {",
            "\tid = SF_a",
            "\tavailable = { any_country = { is_ai = no } }",
            "}",
            "shared_focus = {",
            "\tid = SF_b",
            "\tavailable = { any_country = { is_ai = yes } }",
            "}"));

        var findings = Analyze("hoi4", hoi4);

        Assert.Equal(new[] { "ExpensiveIterator|SF_a|any_country", "ExpensiveIterator|SF_b|any_country" }, findings.Select(f => f.Key));
        Assert.Equal(new[] { "SF_b (available)" }, findings[1].Performance!.Path);
    }

    [Fact]
    public void NestedSortsFirstWithinSeverity()
    {
        Pulse("on_monthly_pulse", "a.1 z.1");
        Write(_roots.Mod, "events/a.txt", "namespace = a\na.1 = {\n\timmediate = { every_country = { add_gold = 1 } }\n}");
        Write(_roots.Mod, "events/z.txt", "namespace = z\nz.1 = {\n\timmediate = { every_province = { any_country = { is_ai = no } } }\n}");

        var performance = AnalyzeAll(Ck3Performance).Findings.Where(f => f.Kind == FindingKind.ExpensiveIterator).ToList();

        Assert.Equal(new[] { "events/z.txt", "events/a.txt" }, performance.Select(f => f.Location.RelativePath));
    }
}
