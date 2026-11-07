using System.Text.Json;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public sealed class ScriptCheckReaderTests
{
    private static readonly string ProfilesDir = Path.Combine(AppContext.BaseDirectory, "profiles");

    private static ScriptCheckReader Reader(string id) =>
        new(ScriptCheckRules.ForProfile(RepoFiles.Profile(id), ProfilesDir) ?? throw new InvalidOperationException("Keine Regeln für " + id));

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    private static CheckRef Call(CheckScan scan, string target) => Assert.Single(scan.EventCalls, c => c.Target == target);

    [Fact]
    public void NamedFolderKeysAreNamedDefinitions()
    {
        var scan = Reader("ck3").Read("common/scripted_modifiers/a.txt", "my_mod = { modifier = { add = 1 } }");

        var d = Assert.Single(scan.Definitions);
        Assert.Equal("my_mod", d.Name);
        Assert.Equal(CheckKind.Named, d.Kind);
        Assert.Equal(1, d.Line);
    }

    [Fact]
    public void Ck3KeyedEvents()
    {
        var scan = Reader("ck3").Read("events/a.txt", Lines(
            "namespace = ns",
            "ns.0001 = { immediate = { trigger_event = ns.2 } }",
            "ns.2 = { option = { trigger_event = { id = ns.1 days = 5 } } }"));

        Assert.Equal(new[] { "ns.0001", "ns.2" }, scan.Definitions.Select(d => d.Name));
        Assert.All(scan.Definitions, d => Assert.Equal(CheckKind.Event, d.Kind));
        Assert.All(scan.Definitions, d => Assert.False(d.SelfFiring));
        Assert.Equal(2, scan.Definitions[0].Line);

        var c2 = Call(scan, "ns.2");
        Assert.Equal("ns.0001", c2.Owner);
        Assert.Equal(CheckKind.Event, c2.OwnerKind);
        Assert.Equal(CheckKind.Event, c2.TargetKind);
        Assert.Equal(2, c2.Line);
        Assert.False(c2.Conditional);

        var c1 = Call(scan, "ns.1");
        Assert.Equal("ns.2", c1.Owner);
        Assert.Equal(3, c1.Line);
        Assert.Equal(2, scan.EventCalls.Count);
    }

    [Fact]
    public void Eu4BlockEventsAndSelfFiring()
    {
        var scan = Reader("eu4").Read("events/a.txt", Lines(
            "country_event = { id = x.1 mean_time_to_happen = { months = 5 } }",
            "country_event = { id = x.2 is_triggered_only = yes option = { country_event = { id = x.3 days = 2 } } }",
            "country_event = { id = x.4 trigger = { } }"));

        Assert.Equal(new[] { "x.1", "x.2", "x.4" }, scan.Definitions.Select(d => d.Name));
        Assert.True(scan.Definitions[0].SelfFiring);
        Assert.False(scan.Definitions[1].SelfFiring);
        Assert.True(scan.Definitions[2].SelfFiring);

        var call = Assert.Single(scan.EventCalls);
        Assert.Equal("x.3", call.Target);
        Assert.Equal("x.2", call.Owner);
        Assert.Equal(CheckKind.Event, call.OwnerKind);
    }

    [Fact]
    public void Eu5SilentCallsAndHistorical()
    {
        var scan = Reader("eu5").Read("in_game/events/a.txt", Lines(
            "x.1 = {",
            "  immediate = { trigger_event_silently = x.2 trigger_event_non_silently = { id = x.3 } }",
            "}",
            "x.4 = { dynamic_historical_event = { } }"));

        Assert.Equal(new[] { "x.1", "x.4" }, scan.Definitions.Select(d => d.Name));
        Assert.False(scan.Definitions[0].SelfFiring);
        Assert.True(scan.Definitions[1].SelfFiring);
        Assert.Equal("x.1", Call(scan, "x.2").Owner);
        Assert.Equal("x.1", Call(scan, "x.3").Owner);
    }

    [Fact]
    public void Eu5DlcEventsAreEvents()
    {
        var scan = Reader("eu5").Read("dlc/D001_x/in_game/events/a.txt", "dlc_ns.1 = { immediate = { trigger_event_silently = dlc_ns.2 } }");

        var definition = Assert.Single(scan.Definitions);
        Assert.Equal("dlc_ns.1", definition.Name);
        Assert.Equal(CheckKind.Event, definition.Kind);
        Assert.Equal("dlc_ns.1", Call(scan, "dlc_ns.2").Owner);
    }

    [Fact]
    public void Hoi4ScalarCall()
    {
        var scan = Reader("hoi4").Read("common/on_actions/a.txt",
            "on_actions = { on_startup = { effect = { country_event = x.5 } } }");

        var call = Assert.Single(scan.EventCalls);
        Assert.Equal("x.5", call.Target);
        Assert.False(call.Conditional);
    }

    [Fact]
    public void Hoi4OnActionsWrapper()
    {
        var scan = Reader("hoi4").Read("common/on_actions/a.txt", Lines(
            "on_actions = {",
            "  on_startup = { effect = { country_event = x.5 } }",
            "  on_daily = { effect = { if = { limit = { } country_event = x.6 } } }",
            "}"));

        Assert.Equal(new[] { "on_startup", "on_daily" }, scan.Definitions.Select(d => d.Name));
        Assert.All(scan.Definitions, d => Assert.Equal(CheckKind.OnAction, d.Kind));
        Assert.Equal(2, scan.Definitions[0].Line);

        var c5 = Call(scan, "x.5");
        Assert.Equal("on_startup", c5.Owner);
        Assert.Equal(CheckKind.OnAction, c5.OwnerKind);
        Assert.False(c5.Conditional);
        Assert.Equal("on_daily", Call(scan, "x.6").Owner);
        Assert.True(Call(scan, "x.6").Conditional);
    }

    [Fact]
    public void Ck3LocalHelpersInEventFiles()
    {
        var r = Reader("ck3");
        var text = Lines(
            "scripted_trigger my_local = { always = yes }",
            "scripted_effect my_local_effect = { trigger_event = ns.2 }",
            "ns.1 = { trigger = { my_local = yes } immediate = { my_local_effect = yes } }");

        var scan = r.Read("events/a.txt", text);
        Assert.Equal(
            new[] { new CheckDefinition("my_local", CheckKind.Trigger, 1, EndLine: 1), new CheckDefinition("my_local_effect", CheckKind.Effect, 2, EndLine: 2), new CheckDefinition("ns.1", CheckKind.Event, 3, EndLine: 3) },
            scan.Definitions);

        var call = Assert.Single(scan.EventCalls);
        Assert.Equal("my_local_effect", call.Owner);
        Assert.Equal(CheckKind.Effect, call.OwnerKind);

        var blockCalls = r.ReadBlockCalls("events/a.txt", text);
        // Der Inhalt eines lokalen Helfers ist Skriptkontext wie in Effekt-Dateien; always filtert die Auswertung als eingebaut.
        Assert.Equal(new[] { "always", "my_local", "my_local_effect" }, blockCalls.Select(c => c.Target).OrderBy(t => t, StringComparer.Ordinal));
        Assert.Equal("my_local", blockCalls.Single(c => c.Target == "always").Owner);
        Assert.All(blockCalls.Where(c => c.Target != "always"), c => Assert.Equal("ns.1", c.Owner));

        var names = new Dictionary<string, CheckKind> { ["my_local"] = CheckKind.Trigger };
        var mention = Assert.Single(r.ReadMentions("events/a.txt", text, names));
        Assert.Equal("ns.1", mention.Owner);
        Assert.True(mention.CallForm);

        Assert.Empty(r.Read("common/decisions/a.txt", "scripted_trigger my_local = { always = yes }").Definitions);
    }

    [Fact]
    public void OnActionLists()
    {
        var scan = Reader("ck3").Read("common/on_action/a.txt",
            "on_x = { events = { a.1 delay = { days = 5 } a.2 } random_events = { 100 = a.3 0 = 0 } first_valid = { a.4 } }");

        var def = Assert.Single(scan.Definitions);
        Assert.Equal("on_x", def.Name);
        Assert.Equal(CheckKind.OnAction, def.Kind);

        Assert.Equal(new[] { "a.1", "a.2", "a.3", "a.4" }, scan.EventCalls.Select(c => c.Target).OrderBy(t => t, StringComparer.Ordinal));
        foreach (var t in new[] { "a.1", "a.2", "a.4" })
        {
            Assert.False(Call(scan, t).Conditional, t);
        }

        Assert.True(Call(scan, "a.3").Conditional);
        Assert.All(scan.EventCalls, c => Assert.Equal("on_x", c.Owner));
        Assert.All(scan.EventCalls, c => Assert.Equal(CheckKind.OnAction, c.OwnerKind));
    }

    [Fact]
    public void StellarisInlineScriptParametersDefineEvents()
    {
        var scan = Reader("stellaris").Read("events/a.txt", Lines(
            "inline_script = { script = events/x EVENT_ID = shroud.10420 SPAWN_EVENT_ID = shroud.10421 COUNT = 5 NAME = $X$.7 }"));

        Assert.Equal(new[] { "shroud.10420", "shroud.10421" }, scan.Definitions.Select(d => d.Name));
        Assert.All(scan.Definitions, d =>
        {
            Assert.Equal(CheckKind.Event, d.Kind);
            Assert.True(d.SelfFiring);
            Assert.Equal(1, d.Line);
        });

        Assert.Empty(Reader("ck3").Read("events/a.txt", "inline_script = { script = events/x EVENT_ID = shroud.10420 }").Definitions);
    }

    [Fact]
    public void StellarisSubfolderEventsIgnored()
    {
        var reader = Reader("stellaris");
        var text = "country_event = { id = x.1 is_triggered_only = yes immediate = { country_event = { id = x.2 } } }";
        var scan = reader.Read("events/sub/a.txt", text);
        Assert.Empty(scan.Definitions);
        Assert.Empty(scan.EventCalls);
        Assert.Empty(reader.ReadMentions("events/sub/a.txt", text, new Dictionary<string, CheckKind> { ["x.2"] = CheckKind.Event }));

        var direct = reader.Read("events/a.txt", text);
        Assert.Equal("x.1", Assert.Single(direct.Definitions).Name);
        Assert.Equal("x.2", Assert.Single(direct.EventCalls).Target);
    }


    [Fact]
    public void DefinitionEndLine_Ck3Event()
    {
        var scan = Reader("ck3").Read("events/a.txt", "namespace = a\na.1 = {\n\timmediate = {\n\t\tadd_gold = 1\n\t}\n}\na.2 = { }");

        var a1 = Assert.Single(scan.Definitions, d => d.Name == "a.1");
        Assert.Equal(2, a1.Line);
        Assert.Equal(6, a1.EndLine);
        var a2 = Assert.Single(scan.Definitions, d => d.Name == "a.2");
        Assert.Equal(7, a2.Line);
        Assert.Equal(7, a2.EndLine);
    }

    [Fact]
    public void DefinitionEndLine_Eu4CountryEvent()
    {
        var scan = Reader("eu4").Read("events/x.txt", "namespace = x\ncountry_event = {\n\tid = x.1\n\toption = {\n\t\tname = a\n\t}\n}");

        var d = Assert.Single(scan.Definitions, e => e.Name == "x.1");
        Assert.Equal(2, d.Line);
        Assert.Equal(7, d.EndLine);
    }

    [Fact]
    public void DefinitionEndLine_ScriptedEffect()
    {
        var scan = Reader("ck3").Read("common/scripted_effects/fx.txt", "my_fx = {\n\tadd_gold = 1\n}");

        var d = Assert.Single(scan.Definitions);
        Assert.Equal(1, d.Line);
        Assert.Equal(3, d.EndLine);
    }

    [Fact]
    public void DefinitionEndLine_SoftEqualsLine()
    {
        var scan = Reader("stellaris").Read("events/a.txt", "inline_script = {\n\tscript = x\n\tCOUNT = 1.5\n\tEVENT_ID = ns.5\n}");

        var d = Assert.Single(scan.Definitions, e => e.Soft);
        Assert.Equal(4, d.Line);
        Assert.Equal(d.Line, d.EndLine);
    }
    [Fact]
    public void DefinitionsByFolder()
    {
        var r = Reader("ck3");
        var eff = Assert.Single(r.Read("common/scripted_effects/a.txt", "@x = 5\nmy_effect = { add_gold = 5 }").Definitions);
        Assert.Equal(new CheckDefinition("my_effect", CheckKind.Effect, 2, EndLine: 2), eff);

        var trg = Assert.Single(r.Read("common/scripted_triggers/a.txt", "my_trigger = { always = yes }").Definitions);
        Assert.Equal(CheckKind.Trigger, trg.Kind);

        var vals = r.Read("common/script_values/a.txt", "@y = 2\nmy_sv = 5\nother_sv = { value = 1 }").Definitions;
        Assert.Equal(new[] { "my_sv", "other_sv" }, vals.Select(v => v.Name));
        Assert.All(vals, v => Assert.Equal(CheckKind.Value, v.Kind));

        Assert.Empty(r.Read("common/decisions/a.txt", "my_decision = { }").Definitions);
        Assert.Empty(r.Read("common/scripted_effects/a.txt", "$X$_effect = { }").Definitions);
    }

    [Fact]
    public void ConditionalAncestors()
    {
        var scan = Reader("ck3").Read("events/a.txt",
            "ns.1 = { immediate = { if = { limit = { } trigger_event = a.1 } trigger_event = a.2 } }");

        Assert.True(Call(scan, "a.1").Conditional);
        Assert.False(Call(scan, "a.2").Conditional);
    }

    [Fact]
    public void TransparentAncestorsAreNotConditional()
    {
        var scan = Reader("ck3").Read("events/a.txt", Lines(
            "ns.1 = { immediate = { a_fx = yes AND = { OR = { trigger_event = t.1 } }",
            "  overlord ?= { trigger_event = t.2 } every_vassal = { trigger_event = t.3 } if = { trigger_event = t.4 } } }"));

        Assert.False(Call(scan, "t.1").Conditional);
        Assert.True(Call(scan, "t.2").Conditional);
        Assert.True(Call(scan, "t.3").Conditional);
        Assert.True(Call(scan, "t.4").Conditional);
    }

    [Fact]
    public void DirectCallInImmediateIsNotConditional()
    {
        var scan = Reader("ck3").Read("events/a.txt", "ns.1 = { immediate = { trigger_event = t.1 } }");

        Assert.False(Call(scan, "t.1").Conditional);
    }

    [Fact]
    public void DollarNamesSkipped()
    {
        var scan = Reader("ck3").Read("common/scripted_effects/a.txt",
            "my_effect = { trigger_event = $EVENT$ trigger_event = { id = $EVENT$ } }");

        Assert.Empty(scan.EventCalls);
        Assert.Single(scan.Definitions);
    }

    [Fact]
    public void ParameterisedEventIdsKeptWithNamespace()
    {
        var scan = Reader("ck3").Read("common/scripted_effects/a.txt",
            "my_effect = { trigger_event = ns.$NEXT$ trigger_event = { id = $NS$.5 } trigger_event = $EVENT$ }");

        Assert.Equal("my_effect", Call(scan, "ns.$NEXT$").Owner);
        Assert.Single(scan.EventCalls);

        var onAction = Reader("ck3").Read("common/on_action/a.txt", "on_x = { events = { ns.$ID$ } }");
        Assert.Equal("on_x", Call(onAction, "ns.$ID$").Owner);
    }

    [Fact]
    public void OwnerOutsideDefinitions()
    {
        var scan = Reader("ck3").Read("common/decisions/a.txt", "my_decision = { effect = { trigger_event = ns.1 } }");

        var call = Assert.Single(scan.EventCalls);
        Assert.Equal("my_decision", call.Owner);
        Assert.Equal(CheckKind.Other, call.OwnerKind);
    }

    [Fact]
    public void MentionsSkipSelf()
    {
        var names = new Dictionary<string, CheckKind> { ["ns.1"] = CheckKind.Event };
        var r = Reader("ck3");
        Assert.Empty(r.ReadMentions("events/a.txt", "ns.1 = { option = { trigger_event = ns.1 } }", names));

        var mentions = r.ReadMentions("events/a.txt", Lines(
            "ns.1 = { option = { trigger_event = ns.1 } }",
            "ns.2 = { option = { trigger_event = ns.1 } }"), names);

        var m = Assert.Single(mentions);
        Assert.Equal("ns.1", m.Target);
        Assert.Equal(CheckKind.Event, m.TargetKind);
        Assert.Equal("ns.2", m.Owner);
        Assert.Equal(2, m.Line);
    }

    [Fact]
    public void ReadLocMentionsFindsCalls()
    {
        var names = new Dictionary<string, CheckKind> { ["my_sv"] = CheckKind.Value };
        var r = Reader("ck3");
        var text = Lines(
            "l_english:",
            " a:0 \"plain my_sv word\"",
            " b:0 \"cost [GetPlayer.MakeScope.ScriptValue('my_sv')|0] more\"",
            " # c:0 \"[X.ScriptValue('my_sv')]\"",
            "   # d:0 \"[X.ScriptValue('my_sv')]\"",
            " e:0 \"[X.Var('my_sv')] [X.ScriptValue('other')]\"",
            " f:0 \"[X.ScriptValue(\"my_sv\")]\"");

        var mentions = r.ReadLocMentions("localization/english/a_l_english.yml", text, names);

        Assert.Equal(new[] { 3, 7 }, mentions.Select(m => m.Line).ToArray());
        var m0 = mentions[0];
        Assert.Equal("my_sv", m0.Target);
        Assert.Equal(CheckKind.Value, m0.TargetKind);
        Assert.Null(m0.Owner);
        Assert.False(m0.CallForm);
        Assert.False(m0.Conditional);
    }

    [Fact]
    public void MentionsSkipSelfWithLeadingZeros()
    {
        var names = new Dictionary<string, CheckKind> { ["ns.1"] = CheckKind.Event };
        Assert.Empty(Reader("ck3").ReadMentions("events/a.txt", "ns.0001 = { option = { trigger_event = ns.1 } }", names));
    }

    [Fact]
    public void MentionsInGui()
    {
        var names = new Dictionary<string, CheckKind> { ["my_sv"] = CheckKind.Value };
        var mentions = Reader("ck3").ReadMentions("gui/a.gui",
            "window = { text = \"[GetPlayer.MakeScope.ScriptValue('my_sv')]\" }", names);

        var m = Assert.Single(mentions);
        Assert.Equal("my_sv", m.Target);
        Assert.Equal(CheckKind.Value, m.TargetKind);
        Assert.Equal(CheckKind.Other, m.OwnerKind);
        Assert.False(m.CallForm);
    }

    [Fact]
    public void MentionCallForm()
    {
        var names = new Dictionary<string, CheckKind> { ["my_effect"] = CheckKind.Effect, ["my_trigger"] = CheckKind.Trigger };
        var r = Reader("ck3");

        var inEvent = r.ReadMentions("events/a.txt", "ns.1 = { immediate = { if = { limit = { my_trigger = yes } my_effect = yes } } }", names);
        Assert.Equal(2, inEvent.Count);
        Assert.All(inEvent, m => Assert.True(m.CallForm));
        Assert.True(inEvent.Single(m => m.Target == "my_trigger").Conditional);

        var inEffect = Assert.Single(r.ReadMentions("common/scripted_effects/a.txt", "other = { my_effect = yes }", names));
        Assert.True(inEffect.CallForm);
        Assert.Equal("other", inEffect.Owner);
        Assert.Equal(CheckKind.Effect, inEffect.OwnerKind);

        var asValue = Assert.Single(r.ReadMentions("events/a.txt", "ns.1 = { immediate = { x = my_effect } }", names));
        Assert.False(asValue.CallForm);

        Assert.Empty(r.ReadMentions("common/scripted_effects/a.txt", "my_effect = { my_effect = yes }", names));
    }

    [Fact]
    public void BlockCalls()
    {
        var r = Reader("ck3");
        var calls = r.ReadBlockCalls("events/a.txt",
            "ns.1 = { immediate = { my_effect = yes OTHER = { A = 1 } hidden = yes scope:x = { } limit = { } } }");

        Assert.Equal(new[] { "OTHER", "hidden", "my_effect" }, calls.Select(c => c.Target).OrderBy(t => t, StringComparer.Ordinal));
        Assert.All(calls, c => Assert.Equal(CheckKind.Effect, c.TargetKind));
        Assert.All(calls, c => Assert.True(c.CallForm));
        Assert.All(calls, c => Assert.Equal("ns.1", c.Owner));

        Assert.Empty(r.ReadBlockCalls("events/a.txt", "ns.1 = { immediate = { scope:x = { } } }"));
        Assert.Empty(r.ReadBlockCalls("events/a.txt", "ns.1 = { title = yes }"));

        var inEffect = Assert.Single(r.ReadBlockCalls("common/scripted_effects/a.txt", "top = yes\nmy_effect = { inner = yes }"));
        Assert.Equal("inner", inEffect.Target);
        Assert.Equal(2, inEffect.Line);
    }

    [Theory]
    [InlineData("my_effect = { FLAG = yes }", "my_effect")]
    [InlineData("limit = { OR = { a = yes } }", "a")]
    [InlineData("trigger = { NOT = { a = yes } }", "a")]
    [InlineData("liege = { NOT = { a = yes } }", "a")]
    [InlineData("GER = { NOT = { x = y } }", "")]
    [InlineData("random_list = { 10 = { a = yes } }", "a")]
    [InlineData("OTHER = { A = 1 }", "OTHER")]
    public void BlockCallsOnlyWithScalarParameters(string body, string expected)
    {
        var calls = Reader("ck3").ReadBlockCalls("events/a.txt", "ns.1 = { immediate = { " + body + " } }");
        Assert.Equal(expected.Length == 0 ? Array.Empty<string>() : new[] { expected }, calls.Select(c => c.Target));
    }

    [Fact]
    public void NegatedNameIsABlockCall()
    {
        // my_trigger = no ist ein Aufruf wie my_trigger = yes (negierter Skripttrigger oder Argument-Schalter; ob eingebaut, entscheidet die Auswertung).
        var calls = Reader("ck3").ReadBlockCalls("events/a.txt", "ns.1 = { trigger = { my_trigger = no other = maybe } }");

        var call = Assert.Single(calls);
        Assert.Equal("my_trigger", call.Target);
        Assert.True(call.CallForm);
    }

    [Fact]
    public void EventCallArgumentsAreNoBlockCalls()
    {
        var calls = Reader("vic3").ReadBlockCalls("events/a.txt",
            "ns.1 = { immediate = { trigger_event = { id = ns.2 days = 0 popup = yes } after_call = yes } }");

        Assert.Equal(new[] { "after_call" }, calls.Select(c => c.Target));
    }

    [Fact]
    public void EventDefinitionBlockNamedLikeEventCallKeepsItsCalls()
    {
        // EU IV/HOI IV/Stellaris: country_event ist Definitionsblock (oberste Ebene) und Aufrufschlüssel (darin).
        var calls = Reader("hoi4").ReadBlockCalls("events/a.txt",
            "country_event = { id = x.1 immediate = { my_effect = yes country_event = { id = x.2 days = 1 flag = yes } } }");

        Assert.Equal(new[] { "my_effect" }, calls.Select(c => c.Target));
    }

    [Fact]
    public void Hoi4RandomEvents()
    {
        var scan = Reader("hoi4").Read("common/on_actions/a.txt",
            "on_actions = { on_x = { random_events = { 100 = usa.6 5 = usa.7 0 = 0 } } }");

        Assert.Equal(new[] { "usa.6", "usa.7" }, scan.EventCalls.Select(c => c.Target));
        Assert.All(scan.EventCalls, c => Assert.True(c.Conditional));
        Assert.All(scan.EventCalls, c => Assert.Equal("on_x", c.Owner));
    }

    [Fact]
    public void ScanRoundTripsThroughJson()
    {
        var scan = Reader("eu4").Read("events/a.txt",
            "country_event = { id = x.1 mean_time_to_happen = { months = 5 } option = { if = { limit = { } country_event = x.3 } } }");
        var mentions = Reader("ck3").ReadMentions("events/a.txt", "ns.2 = { option = { trigger_event = ns.1 } }",
            new Dictionary<string, CheckKind> { ["ns.1"] = CheckKind.Event });

        var back = JsonSerializer.Deserialize<CheckScan>(JsonSerializer.Serialize(scan))!;
        Assert.Equal(scan.Definitions, back.Definitions);
        Assert.Equal(scan.EventCalls, back.EventCalls);
        Assert.NotEmpty(back.EventCalls);

        var refs = JsonSerializer.Deserialize<List<CheckRef>>(JsonSerializer.Serialize(mentions))!;
        Assert.Equal(mentions, refs);

        Assert.Empty(JsonSerializer.Deserialize<CheckScan>("{}")!.Definitions);
    }

    [Fact]
    public void InlineScriptNumbersAreNoEvents()
    {
        var scan = Reader("stellaris").Read("events/a.txt", "inline_script = {\n\tscript = x\n\tCOUNT = 1.5\n\tEVENT_ID = ns.5\n}");

        var d = Assert.Single(scan.Definitions);
        Assert.Equal("ns.5", d.Name);
    }

    [Fact]
    public void CallContextKeysHoldCallsButAreNotTransparent()
    {
        var reader = new ScriptCheckReader(new ScriptCheckRules { CallContextKeys = new[] { "can_construct" }, EffectFolders = new[] { "common/scripted_effects" } });

        var calls = reader.ReadBlockCalls("common/buildings/a.txt", "b = {\n\tcan_construct = {\n\t\tmy_trigger = yes\n\t}\n\tother = {\n\t\tno_call = yes\n\t}\n}");
        var call = Assert.Single(calls);
        Assert.Equal("my_trigger", call.Target);
        Assert.Equal(3, call.Line);
        Assert.True(call.Conditional);

        var inEffect = Assert.Single(reader.ReadBlockCalls("common/scripted_effects/a.txt", "a = {\n\tcan_construct = {\n\t\tb = yes\n\t}\n}"));
        Assert.True(inEffect.Conditional);

        var mention = Assert.Single(reader.ReadMentions(
            "common/buildings/a.txt",
            "b = {\n\tcan_construct = {\n\t\tmy_trigger = yes\n\t}\n}",
            new Dictionary<string, CheckKind> { ["my_trigger"] = CheckKind.Trigger }));
        Assert.True(mention.CallForm);
    }

    [Fact]
    public void TopLevelCallContextKeyHoldsCalls()
    {
        var reader = new ScriptCheckReader(new ScriptCheckRules { CallContextKeys = new[] { "on_complete" } });

        var call = Assert.Single(reader.ReadBlockCalls("common/x/a.txt", "on_complete = {\n\tmy_effect = yes\n}"));

        Assert.Equal("my_effect", call.Target);
    }

    [Theory]
    [InlineData("show_as_tooltip", "ck3")]
    [InlineData("effect_tooltip", "hoi4")]
    public void TooltipBlocksAreNotTransparentButHoldCalls(string key, string game)
    {
        var calls = Reader(game).ReadBlockCalls("common/scripted_effects/a.txt", "a = {\n\t" + key + " = {\n\t\tb = yes\n\t}\n}");
        Assert.True(Assert.Single(calls).Conditional);

        var rules = ScriptCheckRules.ForProfile(RepoFiles.Profile(game), ProfilesDir)!;
        Assert.DoesNotContain(key, rules.ScriptContextKeys);
        Assert.Contains(key, rules.CallContextKeys);
    }

    [Fact]
    public void TemplateTokensCarryTheirOwner()
    {
        var scan = Reader("ck3").Read("common/scripted_effects/a.txt", Lines(
            "my_effect = {",
            "\trvt_tmpl_$X$_effect = yes",
            "\tadd_gold = value:kurultai_$ID$_cost",
            "\t$A$ = yes",
            "\t$A$_b = yes",
            "\trvt_tmpl_$X$_effect = yes",
            "}"));

        Assert.Equal(
            new[] { new CheckTemplate("kurultai_$ID$_cost", "my_effect", CheckKind.Effect), new CheckTemplate("rvt_tmpl_$X$_effect", "my_effect", CheckKind.Effect) },
            scan.Templates.OrderBy(t => t.Token, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("rvt_tmpl_$X$_effect", "rvt_tmpl_$X$_effect")]
    [InlineData("value:kurultai_$ID$_cost", "kurultai_$ID$_cost")]
    [InlineData("@[ foo_$X$ * 2 ]", "foo_$X$")]
    [InlineData("scope:bar.$X$_value", "$X$_value")]
    [InlineData("foo_$X|1$_bar", "foo_$X|1$_bar")]
    [InlineData("$A$", null)]
    [InlineData("$A$_b", null)]
    [InlineData("$A$_$B$", null)]
    [InlineData("foo_$X", null)]
    [InlineData("plain_name", null)]
    public void TemplateWords(string token, string? expected)
    {
        var words = TemplateMatcher.WordsOf(token).ToList();

        if (expected is null)
        {
            Assert.Empty(words);
        }
        else
        {
            Assert.Equal(expected, Assert.Single(words));
        }
    }

    private static ScriptCheckPerformanceRules TestPerformance(bool decisionWrappers = true) => new()
    {
        GlobalIterators = new[]
        {
            new GlobalIteratorRule("every_country", null),
            new GlobalIteratorRule("any_country", null),
            new GlobalIteratorRule("every_province", new[] { "every_owned_province" }),
        },
        PulseOnActions = new Dictionary<string, PulseRate> { ["yearly_global_pulse"] = PulseRate.Yearly },
        OnActionCallLists = new[] { "on_actions", "random_on_action" },
        AlwaysCheckedBlocks = new[]
        {
            new CheckedBlockRule("common/decisions", new[] { "is_shown", "is_valid" }),
            new CheckedBlockRule("decisions", new[] { "potential", "allow" }, decisionWrappers ? new[] { "country_decisions" } : null),
        },
        PolledEventBlocks = new[] { "trigger" },
    };

    /// <summary>CK-III-förmige Regeln (Events als <c>ns.N = { … }</c>), unabhängig von der Profildatei.</summary>
    private static ScriptCheckReader Ck3Perf(ScriptCheckPerformanceRules? performance) => new(new ScriptCheckRules
    {
        EventFolders = new[] { "events" },
        EventCallKeys = new[] { "trigger_event" },
        OnActionFolders = new[] { "common/on_action" },
        OnActionEventLists = new[] { "events", "random_events", "first_valid" },
        EffectFolders = new[] { "common/scripted_effects" },
        TriggerFolders = new[] { "common/scripted_triggers" },
        ScriptContextKeys = new[] { "immediate", "option", "effect", "trigger", "limit", "potential", "is_shown", "is_valid" },
        TransparentKeys = new[] { "AND", "OR", "NOT", "hidden_effect" },
        Performance = performance,
    });

    /// <summary>EU-IV-förmige Regeln (<c>country_event = { id = … }</c>, <c>mean_time_to_happen</c>, <c>is_triggered_only</c>), unabhängig von der Profildatei.</summary>
    private static ScriptCheckReader Eu4Perf(ScriptCheckPerformanceRules? performance = null) => new(new ScriptCheckRules
    {
        EventFolders = new[] { "events" },
        EventBlocks = new[] { "country_event", "province_event" },
        EventCallKeys = new[] { "country_event", "province_event" },
        OnActionFolders = new[] { "common/on_actions" },
        OnActionEventLists = new[] { "events", "random_events" },
        SelfFiringFields = new[] { "mean_time_to_happen" },
        TriggeredOnlyField = "is_triggered_only",
        EngineEvents = new[] { "m.3" },
        ScriptContextKeys = new[] { "immediate", "option", "effect", "trigger", "limit", "potential", "allow", "mean_time_to_happen" },
        TransparentKeys = new[] { "AND", "OR", "NOT", "hidden_effect" },
        Performance = performance ?? TestPerformance(),
    });

    [Fact]
    public void IteratorSiteInEventImmediate()
    {
        var scan = Ck3Perf(TestPerformance()).Read("events/m.txt", Lines(
            "namespace = m",
            "m.1 = {",
            "\timmediate = {",
            "\t\tevery_country = {",
            "\t\t\tlimit = { is_ai = no }",
            "\t\t\tadd_gold = 1",
            "\t\t}",
            "\t}",
            "}"));

        Assert.Equal(new IteratorSite("every_country", 4, "m.1", CheckKind.Event, null, null, false), Assert.Single(scan.Iterators));
        Assert.Empty(scan.OnActionCalls);
    }

    [Fact]
    public void NestedIteratorSites()
    {
        var scan = Ck3Perf(TestPerformance()).Read("common/scripted_effects/a.txt", Lines(
            "my_effect = {",
            "\tevery_province = {",
            "\t\tany_country = {",
            "\t\t\tevery_country = { add_gold = 1 }",
            "\t\t}",
            "\t\tevery_country = { add_gold = 2 }",
            "\t}",
            "}"));

        Assert.Equal(
            new[]
            {
                new IteratorSite("every_province", 2, "my_effect", CheckKind.Effect, null, "any_country", false),
                new IteratorSite("any_country", 3, "my_effect", CheckKind.Effect, null, "every_country", true),
                new IteratorSite("every_country", 4, "my_effect", CheckKind.Effect, null, null, true),
                new IteratorSite("every_country", 6, "my_effect", CheckKind.Effect, null, null, true),
            },
            scan.Iterators);
    }

    [Fact]
    public void NonGlobalIteratorIgnored()
    {
        var scan = Ck3Perf(TestPerformance()).Read("common/scripted_effects/a.txt", Lines(
            "my_effect = {",
            "\tevery_vassal = { add_gold = 1 }",
            "\tevery_country = yes",
            "}"));

        Assert.Empty(scan.Iterators);
    }

    [Fact]
    public void CheckedBlockForDecisionFolder()
    {
        var reader = Ck3Perf(TestPerformance());
        var text = Lines(
            "my_decision = {",
            "\tis_shown = {",
            "\t\tOR = {",
            "\t\t\tany_country = { is_ai = no }",
            "\t\t\tmy_trigger = yes",
            "\t\t}",
            "\t\ttrigger_event = m.1",
            "\t}",
            "\teffect = {",
            "\t\tmy_effect = yes",
            "\t\ttrigger_event = m.2",
            "\t}",
            "}");

        var scan = reader.Read("common/decisions/d.txt", text);
        var site = Assert.Single(scan.Iterators);
        Assert.Equal("is_shown", site.CheckedBlock);
        Assert.Equal("my_decision", site.Owner);
        Assert.Equal(4, site.Line);
        Assert.Equal("is_shown", Call(scan, "m.1").CheckedBlock);
        Assert.Null(Call(scan, "m.2").CheckedBlock);

        var calls = reader.ReadBlockCalls("common/decisions/d.txt", text);
        Assert.Equal("is_shown", Assert.Single(calls, c => c.Target == "my_trigger").CheckedBlock);
        Assert.Null(Assert.Single(calls, c => c.Target == "my_effect").CheckedBlock);

        var mentions = reader.ReadMentions("common/decisions/d.txt", text,
            new Dictionary<string, CheckKind> { ["my_trigger"] = CheckKind.Trigger, ["my_effect"] = CheckKind.Effect });
        Assert.Equal("is_shown", Assert.Single(mentions, m => m.Target == "my_trigger").CheckedBlock);
        Assert.Null(Assert.Single(mentions, m => m.Target == "my_effect").CheckedBlock);

        // Außerhalb des Ordners zählt der Block nicht.
        Assert.Null(Assert.Single(reader.Read("common/other/d.txt", text).Iterators).CheckedBlock);
    }

    [Fact]
    public void CheckedBlockNestedOwner()
    {
        var scan = Eu4Perf().Read("decisions/x.txt", Lines(
            "country_decisions = {",
            "\tmy_dec = {",
            "\t\tpotential = {",
            "\t\t\tany_country = { ai = no }",
            "\t\t}",
            "\t\teffect = {",
            "\t\t\tevery_country = { add_prestige = 1 }",
            "\t\t}",
            "\t}",
            "}"));

        Assert.Equal(new[] { "any_country", "every_country" }, scan.Iterators.Select(s => s.Iterator));
        Assert.Equal("potential", scan.Iterators[0].CheckedBlock);
        Assert.Null(scan.Iterators[1].CheckedBlock);
        Assert.All(scan.Iterators, s => Assert.Equal("my_dec", s.Owner));
        Assert.All(scan.Iterators, s => Assert.Equal(CheckKind.Other, s.OwnerKind));
    }

    private const string Eu4Decision = "country_decisions = {\n\tmy_dec = {\n\t\tpotential = {\n\t\t\tany_country = { ai = no }\n\t\t\tmy_trigger = yes\n\t\t}\n\t}\n}";

    [Fact]
    public void WithoutWrappersOwnerStaysTopLevelKey()
    {
        var site = Assert.Single(Eu4Perf(TestPerformance(decisionWrappers: false)).Read("decisions/x.txt", Eu4Decision).Iterators);
        Assert.Equal("country_decisions", site.Owner);
        Assert.Null(site.CheckedBlock);

        // Außerhalb des Regelordners wirkt die Hülle nicht.
        var other = Assert.Single(Eu4Perf().Read("common/other/x.txt", Eu4Decision).Iterators);
        Assert.Equal("country_decisions", other.Owner);
        Assert.Null(other.CheckedBlock);
    }

    [Fact]
    public void WithoutPerformanceRulesOwnersUnchanged()
    {
        var reader = new ScriptCheckReader(new ScriptCheckRules { ScriptContextKeys = new[] { "potential" } });

        var call = Assert.Single(reader.ReadBlockCalls("decisions/x.txt", Eu4Decision), c => c.Target == "my_trigger");
        Assert.Equal("my_trigger", call.Target);
        Assert.Equal("country_decisions", call.Owner);
        Assert.Null(call.CheckedBlock);
    }

    [Fact]
    public void WrapperKeepsFrameFlags()
    {
        var text = Lines(
            "country_decisions = {",
            "\tmy_dec = {",
            "\t\tpotential = { my_trigger = yes }",
            "\t\teffect = {",
            "\t\t\tmy_effect = yes",
            "\t\t\tif = { limit = { always = yes } my_effect = yes country_event = { id = m.1 } }",
            "\t\t\tcountry_event = { id = m.2 }",
            "\t\t}",
            "\t}",
            "\tmy_scalar = yes",
            "}");
        var names = new Dictionary<string, CheckKind> { ["my_trigger"] = CheckKind.Trigger, ["my_effect"] = CheckKind.Effect };
        var with = Eu4Perf();
        var without = Eu4Perf(TestPerformance(decisionWrappers: false));

        static object Flags(CheckRef r) => (r.Target, r.TargetKind, r.Line, r.OwnerKind, r.Conditional, r.CallForm, r.Parent);

        Assert.Equal(without.ReadBlockCalls("decisions/x.txt", text).Select(Flags), with.ReadBlockCalls("decisions/x.txt", text).Select(Flags));
        Assert.Equal(without.ReadMentions("decisions/x.txt", text, names).Select(Flags), with.ReadMentions("decisions/x.txt", text, names).Select(Flags));
        Assert.Equal(without.Read("decisions/x.txt", text).EventCalls.Select(Flags), with.Read("decisions/x.txt", text).EventCalls.Select(Flags));
        Assert.Contains(with.ReadBlockCalls("decisions/x.txt", text), c => c.Target == "my_trigger" && c.Conditional);
        Assert.Equal(2, with.Read("decisions/x.txt", text).EventCalls.Count);
    }

    [Fact]
    public void WrapperOwnerForMentionsAndBlockCalls()
    {
        var reader = Eu4Perf();

        var call = Assert.Single(reader.ReadBlockCalls("decisions/x.txt", Eu4Decision), c => c.Target == "my_trigger");
        Assert.Equal("my_dec", call.Owner);
        Assert.Equal("potential", call.CheckedBlock);

        var mention = Assert.Single(reader.ReadMentions("decisions/x.txt", Eu4Decision, new Dictionary<string, CheckKind> { ["my_trigger"] = CheckKind.Trigger }));
        Assert.Equal("my_dec", mention.Owner);
        Assert.Equal("potential", mention.CheckedBlock);
    }

    [Fact]
    public void NestedCheckedBlockKeyDoesNotCount()
    {
        var site = Assert.Single(Ck3Perf(TestPerformance()).Read("common/decisions/d.txt",
            "my_decision = {\n\teffect = {\n\t\tcustom = {\n\t\t\tis_shown = {\n\t\t\t\tany_country = { is_ai = no }\n\t\t\t}\n\t\t}\n\t}\n}").Iterators);

        Assert.Null(site.CheckedBlock);
    }

    [Fact]
    public void OptionTriggerIsNoPolledBlock()
    {
        var scan = Eu4Perf().Read("events/m.txt", Lines(
            "country_event = {",
            "\tid = m.1",
            "\tmean_time_to_happen = { months = 12 }",
            "\toption = {",
            "\t\ttrigger = { any_country = { ai = no } }",
            "\t}",
            "}"));

        Assert.True(Assert.Single(scan.Definitions).Polled);
        Assert.Null(Assert.Single(scan.Iterators).CheckedBlock);
    }

    [Fact]
    public void OutermostCheckedBlockWins()
    {
        var scan = Eu4Perf().Read("decisions/x.txt", Lines(
            "country_decisions = {",
            "\tmy_dec = {",
            "\t\tpotential = {",
            "\t\t\tNOT = { allow = { any_country = { ai = no } } }",
            "\t\t}",
            "\t\tallow = { any_country = { ai = yes } }",
            "\t}",
            "}"));

        Assert.Equal(new[] { "potential", "allow" }, scan.Iterators.Select(s => s.CheckedBlock));
    }

    [Fact]
    public void PolledEventTriggerBlock()
    {
        var scan = Eu4Perf().Read("events/m.txt", Lines(
            "namespace = m",
            "country_event = {",
            "\tid = m.1",
            "\ttrigger = {",
            "\t\tany_country = { ai = no }",
            "\t}",
            "\tmean_time_to_happen = { months = 12 }",
            "\timmediate = { every_country = { add_prestige = 1 } }",
            "}",
            "country_event = {",
            "\tid = m.2",
            "\tis_triggered_only = yes",
            "\ttrigger = { any_country = { ai = no } }",
            "}",
            "country_event = { id = m.3 is_triggered_only = yes }",
            "country_event = { id = m.4 }"));

        var defs = scan.Definitions.ToDictionary(d => d.Name);
        Assert.True(defs["m.1"].Polled);
        Assert.True(defs["m.1"].SelfFiring);
        Assert.False(defs["m.2"].Polled);
        Assert.False(defs["m.2"].SelfFiring);
        Assert.False(defs["m.3"].Polled);
        Assert.True(defs["m.3"].SelfFiring);
        Assert.True(defs["m.4"].Polled);

        Assert.Equal(
            new[]
            {
                new IteratorSite("any_country", 5, "m.1", CheckKind.Event, "trigger", null, false),
                new IteratorSite("every_country", 8, "m.1", CheckKind.Event, null, null, false),
                new IteratorSite("any_country", 13, "m.2", CheckKind.Event, "trigger", null, false),
            },
            scan.Iterators);
    }

    [Fact]
    public void TriggeredOnlyWithMtthIsNotPolled()
    {
        var scan = Eu4Perf().Read("events/m.txt", Lines(
            "country_event = {",
            "\tid = m.1",
            "\tis_triggered_only = yes",
            "\tmean_time_to_happen = { months = 12 }",
            "\ttrigger = { any_country = { ai = no } }",
            "}"));

        var def = Assert.Single(scan.Definitions);
        Assert.False(def.Polled);
        // Selbst feuernd (für tote Events) bleibt wie bisher: das Feld mean_time_to_happen genügt.
        Assert.True(def.SelfFiring);
    }

    [Fact]
    public void StarWrapperMakesEveryTopLevelBlockAWrapper()
    {
        var reader = Eu4Perf(new ScriptCheckPerformanceRules
        {
            GlobalIterators = new[] { new GlobalIteratorRule("any_country", null) },
            AlwaysCheckedBlocks = new[] { new CheckedBlockRule("common/decisions", new[] { "visible" }, new[] { "*" }) },
        });
        var text = Lines(
            "political_actions = {",
            "\tdec_a = {",
            "\t\tvisible = { any_country = { ai = no } }",
            "\t}",
            "}",
            "economy_decisions = {",
            "\tdec_b = {",
            "\t\tvisible = { any_country = { ai = yes } }",
            "\t}",
            "}");

        var scan = reader.Read("common/decisions/x.txt", text);

        Assert.Equal(new[] { "dec_a", "dec_b" }, scan.Iterators.Select(s => s.Owner));
        Assert.All(scan.Iterators, s => Assert.Equal("visible", s.CheckedBlock));
        // Außerhalb des Ordners keine Hülle.
        Assert.Equal(new[] { "political_actions", "economy_decisions" }, reader.Read("common/other/x.txt", text).Iterators.Select(s => s.Owner));
    }

    [Fact]
    public void NameFieldNamesWrapperChildren()
    {
        var reader = Eu4Perf(new ScriptCheckPerformanceRules
        {
            GlobalIterators = new[] { new GlobalIteratorRule("any_country", null) },
            AlwaysCheckedBlocks = new[] { new CheckedBlockRule("common/national_focus", new[] { "available" }, new[] { "focus_tree" }, "id") },
        });

        var scan = reader.Read("common/national_focus/x.txt", Lines(
            "focus_tree = {",
            "\tid = my_tree",
            "\tfocus = {",
            "\t\tid = FOC_a",
            "\t\tavailable = { any_country = { ai = no } }",
            "\t}",
            "\tfocus = {",
            "\t\tid = FOC_b",
            "\t\tavailable = { any_country = { ai = no } }",
            "\t}",
            "\tcountry = { factor = 1 }",
            "}"));

        Assert.Equal(new[] { "FOC_a", "FOC_b" }, scan.Iterators.Select(s => s.Owner));
        Assert.All(scan.Iterators, s => Assert.Equal(CheckKind.Other, s.OwnerKind));
        Assert.All(scan.Iterators, s => Assert.Equal("available", s.CheckedBlock));
    }

    [Fact]
    public void ExcludedFolderDoesNotMatchTheRule()
    {
        var reader = Eu4Perf(new ScriptCheckPerformanceRules
        {
            GlobalIterators = new[] { new GlobalIteratorRule("any_country", null) },
            AlwaysCheckedBlocks = new[] { new CheckedBlockRule("common/decisions", new[] { "visible" }, new[] { "*" }, null, new[] { "common/decisions/categories" }) },
        });
        var text = Lines(
            "my_category = {",
            "\tvisible = { any_country = { ai = no } }",
            "}");

        var site = Assert.Single(reader.Read("common/decisions/categories/x.txt", text).Iterators);

        // Keine Hülle, kein geprüfter Block: der Schlüssel oberster Ebene bleibt Besitzer.
        Assert.Equal("my_category", site.Owner);
        Assert.Null(site.CheckedBlock);
        // Im Ordner selbst greift die Regel weiter.
        Assert.Equal("visible", Assert.Single(reader.Read("common/decisions/x.txt", Lines(
            "cat = {",
            "\tdec_a = {",
            "\t\tvisible = { any_country = { ai = no } }",
            "\t}",
            "}")).Iterators).CheckedBlock);
    }

    [Fact]
    public void PolledNeedsNoPerformanceRules()
    {
        var scan = Reader("eu4").Read("events/a.txt", Lines(
            "country_event = { id = x.1 mean_time_to_happen = { months = 5 } }",
            "country_event = { id = x.2 is_triggered_only = yes }"));

        Assert.Equal(new[] { true, false }, scan.Definitions.Select(d => d.Polled));
    }

    [Fact]
    public void OnActionNestedCalls()
    {
        var reader = Ck3Perf(TestPerformance());
        var scan = reader.Read("common/on_action/a.txt", Lines(
            "yearly_global_pulse = {",
            "\ton_actions = {",
            "\t\tmy_pulse",
            "\t\tdelay = { days = 5 }",
            "\t}",
            "\trandom_on_action = {",
            "\t\t50 = other_pulse",
            "\t\tchance_of_no_event = 10",
            "\t\t10 = 0",
            "\t}",
            "\tevents = { m.1 }",
            "}"));

        Assert.Equal(
            new[]
            {
                new CheckRef("my_pulse", CheckKind.OnAction, 3, "yearly_global_pulse", CheckKind.OnAction, false, true),
                new CheckRef("other_pulse", CheckKind.OnAction, 7, "yearly_global_pulse", CheckKind.OnAction, true, true),
            },
            scan.OnActionCalls);
        Assert.Equal("m.1", Assert.Single(scan.EventCalls).Target);

        // Nur in On-Action-Dateien.
        Assert.Empty(reader.Read("events/m.txt", "m.1 = { immediate = { on_actions = { my_pulse } } }").OnActionCalls);
    }

    [Fact]
    public void NoPerformanceRulesNoSites()
    {
        var reader = Ck3Perf(null);
        var decision = "my_decision = {\n\tis_shown = {\n\t\tany_country = { is_ai = no }\n\t\tmy_trigger = yes\n\t}\n}";

        var scan = reader.Read("common/decisions/d.txt", decision);
        Assert.Empty(scan.Iterators);
        Assert.Empty(reader.Read("common/on_action/a.txt", "yearly_global_pulse = { on_actions = { my_pulse } }").OnActionCalls);
        Assert.All(reader.ReadBlockCalls("common/decisions/d.txt", decision), c => Assert.Null(c.CheckedBlock));
    }

    /// <summary>CK-III-Entscheidungen mit dem Gewichtungsblock <c>ai_will_do</c> (kein Skriptkontext-Schlüssel).</summary>
    private static ScriptCheckPerformanceRules AiWeightPerformance() => new()
    {
        GlobalIterators = new[] { new GlobalIteratorRule("any_country", null) },
        PulseOnActions = new Dictionary<string, PulseRate> { ["yearly_global_pulse"] = PulseRate.Yearly },
        AlwaysCheckedBlocks = new[] { new CheckedBlockRule("common/decisions", new[] { "is_shown", "ai_will_do" }) },
    };

    [Fact]
    public void AiWillDoCallIsCheckedCall()
    {
        var reader = Ck3Perf(AiWeightPerformance());

        var scan = reader.Read("common/decisions/d.txt", Lines(
            "my_dec = {",
            "\tai_will_do = {",
            "\t\tbase = 10",
            "\t\tmodifier = {",
            "\t\t\tfactor = 0",
            "\t\t\tmy_trigger = yes",
            "\t\t\tmy_param_trigger = { WHO = root }",
            "\t\t}",
            "\t}",
            "}"));

        Assert.Equal(
            new[]
            {
                new CheckRef("my_trigger", CheckKind.Effect, 6, "my_dec", CheckKind.Other, true, true, "modifier", "ai_will_do"),
                new CheckRef("my_param_trigger", CheckKind.Effect, 7, "my_dec", CheckKind.Other, true, true, "modifier", "ai_will_do"),
            },
            scan.CheckedCalls);

        // Im Skriptkontext (is_shown) ist es eine gewöhnliche Erwähnung, kein geprüfter Aufruf.
        Assert.Empty(reader.Read("common/decisions/d.txt", "my_dec = {\n\tis_shown = {\n\t\tmy_trigger = yes\n\t}\n}").CheckedCalls);
        // Außerhalb eines geprüften Blocks nichts.
        Assert.Empty(reader.Read("common/decisions/d.txt", "my_dec = {\n\tcost = {\n\t\tmy_trigger = yes\n\t}\n}").CheckedCalls);
    }

    [Fact]
    public void CheckedCallsNoPerformanceEmpty()
    {
        var scan = Ck3Perf(null).Read("common/decisions/d.txt", "my_dec = {\n\tai_will_do = {\n\t\tmodifier = { my_trigger = yes }\n\t}\n}");

        Assert.Empty(scan.CheckedCalls);
    }

    [Fact]
    public void TopLevelOwnerNamedByField()
    {
        var reader = Eu4Perf(new ScriptCheckPerformanceRules
        {
            GlobalIterators = new[] { new GlobalIteratorRule("any_country", null) },
            AlwaysCheckedBlocks = new[] { new CheckedBlockRule("common/national_focus", new[] { "available" }, new[] { "focus_tree" }, "id") },
        });
        var text = Lines(
            "shared_focus = {",
            "\tid = SF_a",
            "\tavailable = { any_country = { ai = no } }",
            "}",
            "shared_focus = {",
            "\tid = SF_b",
            "\tavailable = { any_country = { ai = no } }",
            "}",
            "joint_focus = {",
            "\tavailable = { any_country = { ai = no } }",
            "}");

        var scan = reader.Read("common/national_focus/x.txt", text);

        // Ohne Feld bleibt der Schlüssel.
        Assert.Equal(new[] { "SF_a", "SF_b", "joint_focus" }, scan.Iterators.Select(s => s.Owner));
        Assert.All(scan.Iterators, s => Assert.Equal(CheckKind.Other, s.OwnerKind));
        Assert.All(scan.Iterators, s => Assert.Equal("available", s.CheckedBlock));
        // Außerhalb des Regelordners bleibt der Schlüssel Besitzer.
        Assert.Equal(new[] { "shared_focus", "shared_focus", "joint_focus" }, reader.Read("common/other/x.txt", text).Iterators.Select(s => s.Owner));
    }

    [Fact]
    public void Hoi4CategoryVisibleChecked()
    {
        // Regeln aus profiles/scriptcheck/hoi4.json: common/decisions (Hülle *, ohne categories) und common/decisions/categories (visible).
        var reader = Reader("hoi4");

        var category = Assert.Single(reader.Read("common/decisions/categories/c.txt", Lines(
            "my_cat = {",
            "\ticon = generic",
            "\tvisible = { any_country = { ai = no } }",
            "}")).Iterators);
        Assert.Equal("my_cat", category.Owner);
        Assert.Equal("visible", category.CheckedBlock);

        var decision = Assert.Single(reader.Read("common/decisions/d.txt", Lines(
            "my_cat = {",
            "\tmy_dec = {",
            "\t\tvisible = { any_country = { ai = no } }",
            "\t}",
            "}")).Iterators);
        Assert.Equal("my_dec", decision.Owner);
        Assert.Equal("visible", decision.CheckedBlock);
    }
}
