using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Scopes.Tests;

internal static class TestData
{
    private static readonly DateTime T0 = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private static ScriptDocEntry E(ScriptDocKind kind, string name, string[]? scopes = null, string[]? targets = null) =>
        new() { Kind = kind, Name = name, Scopes = (scopes ?? Array.Empty<string>()).ToList(), Targets = (targets ?? Array.Empty<string>()).ToList() };

    public static ScopeDb Db()
    {
        var database = new ScriptDocDatabase();
        database.ApplyImport(
            new[]
            {
                E(ScriptDocKind.Effect, "add_gold", new[] { "country" }),
                E(ScriptDocKind.Effect, "add_prestige", new[] { "country" }),
                E(ScriptDocKind.Trigger, "is_female", new[] { "character" }),
                E(ScriptDocKind.Trigger, "is_ruler", new[] { "character" }),
                E(ScriptDocKind.Trigger, "has_dlc", new[] { "none" }),
                E(ScriptDocKind.Effect, "every_neighbor_country", new[] { "country" }, new[] { "country" }),
                E(ScriptDocKind.Trigger, "any_owned_location", new[] { "country" }, new[] { "location" }),
                E(ScriptDocKind.Trigger, "any_ruling_country", new[] { "character" }, new[] { "country", "location" }),
                E(ScriptDocKind.Trigger, "is_capital", new[] { "location" }),
                E(ScriptDocKind.EventTarget, "owner", new[] { "location" }, new[] { "country" }),
                E(ScriptDocKind.EventTarget, "capital", new[] { "country" }, new[] { "location" }),
                E(ScriptDocKind.EventTarget, "multi", new[] { "country" }, new[] { "location", "character" }),
                E(ScriptDocKind.Modifier, "country_income", new[] { "Country" }),
            },
            Enum.GetValues<ScriptDocKind>().ToList(),
            T0,
            new Dictionary<string, long>());
        return ScopeDb.FromDatabase(database);
    }

    public static ScopeRules Rules()
    {
        var rules = new ScopeRules();
        rules.Fields["buildings/*/potential"] = new FieldRule { Scope = "country", Support = 1.0, Total = 50 };
        rules.Fields["buildings/*/allow"] = new FieldRule { Scope = "location", Support = 0.95, Total = 20 };
        rules.Fields["buildings/*/weak"] = new FieldRule { Scope = "country", Support = 0.8, Total = 20 };
        rules.Fields["buildings/*/tied"] = new FieldRule { Scope = null, Support = 0.5, Total = 20 };
        rules.Fields["events/country_event/immediate"] = new FieldRule { Scope = "country", Support = 1.0, Total = 30 };
        rules.Fields["cl/*/text/trigger"] = new FieldRule { Scope = null, Support = 0.5, Total = 40 };
        rules.Discriminators["cl/*/text/trigger"] = new DiscriminatorRule
        {
            Key = "type",
            Map = new Dictionary<string, string> { ["character"] = "character", ["country"] = "country" },
        };
        return rules;
    }
}

public class ScopeEngineTests
{
    private const string BuildingFile = @"C:\mods\m\in_game\common\buildings\a.txt";

    private static readonly ScopeEngine Engine = new(TestData.Db(), TestData.Rules());

    /// <summary>Der Text mit einem "|" an der Cursorposition.</summary>
    private static ScopeContext At(string textWithCaret, string? path = BuildingFile, ScopeEngine? engine = null)
    {
        var caret = textWithCaret.IndexOf('|');
        return (engine ?? Engine).Analyze(textWithCaret.Remove(caret, 1), caret, path);
    }

    [Fact]
    public void StartScope_ComesFromTheLearnedFieldRule()
    {
        var context = At("b = { potential = { | } }");

        Assert.Equal("country", context.CurrentScope);
        Assert.True(context.Confident);
        Assert.Equal("buildings/*/potential", context.FieldPath);
        Assert.Equal(new[] { "potential" }, context.Frames.Select(f => f.Label));
    }

    [Fact]
    public void DifferentFieldsHaveDifferentStartScopes()
    {
        Assert.Equal("location", At("b = { allow = { | } }").CurrentScope);
    }

    [Fact]
    public void Iterator_ChangesToItsSingleTarget()
    {
        var context = At("b = { potential = { any_owned_location = { | } } }");

        Assert.Equal("location", context.CurrentScope);
        Assert.True(context.Confident);
        Assert.Equal(new[] { "potential", "any_owned_location" }, context.Frames.Select(f => f.Label));
    }

    [Fact]
    public void Iterator_WithSeveralTargets_IsUnknown()
    {
        var context = At("b = { potential = { any_ruling_country = { | } } }");

        Assert.Null(context.CurrentScope);
        Assert.Contains("mehrere Ziele", context.UnknownReason);
        Assert.False(context.Confident);
    }

    [Fact]
    public void EventTarget_LeadsToItsOutputScope_AndCanBeNested()
    {
        Assert.Equal("location", At("b = { potential = { capital = { | } } }").CurrentScope);
        Assert.Equal("country", At("b = { potential = { capital = { owner = { | } } } }").CurrentScope);
    }

    [Fact]
    public void EventTarget_WithSeveralOutputs_IsUnknown()
    {
        Assert.Null(At("b = { potential = { multi = { | } } }").CurrentScope);
    }

    [Fact]
    public void Root_ReturnsToTheFieldScope_AndPrevToThePreviousScope()
    {
        Assert.Equal("country", At("b = { potential = { any_owned_location = { root = { | } } } }").CurrentScope);
        Assert.Equal("country", At("b = { potential = { any_owned_location = { prev = { | } } } }").CurrentScope);
    }

    [Fact]
    public void This_KeepsTheScope_AndFromIsUnknown()
    {
        Assert.Equal("country", At("b = { potential = { this = { | } } }").CurrentScope);
        Assert.Null(At("b = { potential = { from = { | } } }").CurrentScope);
    }

    [Fact]
    public void TransparentBlocks_KeepTheScope()
    {
        var context = At("b = { potential = { OR = { NOT = { any_owned_location = { limit = { | } } } } } }");

        Assert.Equal("location", context.CurrentScope);
        Assert.Equal(
            new[] { "potential", "OR", "NOT", "any_owned_location", "limit" },
            context.Frames.Select(f => f.Label));
    }

    [Fact]
    public void Chains_AreResolvedStepByStep()
    {
        Assert.Equal("location", At("b = { potential = { root.capital = { | } } }").CurrentScope);
        Assert.Equal("country", At("b = { potential = { root.capital.owner = { | } } }").CurrentScope);
    }

    [Fact]
    public void SavedScope_ResolvesToTheScopeAtTheSavePoint()
    {
        var context = At("b = { potential = { save_scope_as = me  any_owned_location = { scope:me = { | } } } }");

        Assert.Equal("country", context.CurrentScope);
        Assert.True(context.Confident);
    }

    [Fact]
    public void SavedScope_InANestedBlock_IsNotVisibleOutsideIt()
    {
        var context = At("b = { potential = { any_owned_location = { save_scope_as = spot } scope:spot = { | } } }");

        Assert.Null(context.CurrentScope);
        Assert.Contains("scope:spot", context.UnknownReason);
    }

    [Fact]
    public void SavedScope_IsVisibleAtTheCursorLevel()
    {
        var context = At("b = { potential = { save_scope_as = me  | } }");

        Assert.Equal("country", context.SavedScopes["me"]);
    }

    [Fact]
    public void UnknownSavedScope_IsUnknown()
    {
        var context = At("b = { potential = { scope:target = { | } } }");

        Assert.Null(context.CurrentScope);
        Assert.Contains("scope:target", context.UnknownReason);
    }

    [Fact]
    public void ParameterBlocks_HaveNoScopeInside()
    {
        var context = At("b = { potential = { add_gold = { | } } }");

        Assert.Null(context.CurrentScope);
        Assert.Contains("Parameterblock", context.UnknownReason);
    }

    [Fact]
    public void FieldWithoutRule_IsUnknown_AndWeakOrTiedRulesAreNotUsed()
    {
        Assert.Contains("keine Regel", At("b = { unknown_field = { | } }").UnknownReason);
        Assert.Null(At("b = { weak = { | } }").CurrentScope);
        Assert.Null(At("b = { tied = { | } }").CurrentScope);
    }

    [Fact]
    public void ObjectLevelStatements_UseTheObjectPath()
    {
        var rules = TestData.Rules();
        rules.Fields["buildings/*"] = new FieldRule { Scope = "country", Support = 0.99, Total = 10 };
        var engine = new ScopeEngine(TestData.Db(), rules);

        Assert.Equal("country", At("b = { | }", engine: engine).CurrentScope);
    }

    [Fact]
    public void Discriminator_ChoosesTheScopeByTheSiblingValue()
    {
        Assert.Equal("character", At("f = { type = character text = { trigger = { | } } }", @"C:\x\common\cl\a.txt").CurrentScope);
        Assert.Equal("country", At("f = { text = { trigger = { | } } type = country }", @"C:\x\common\cl\a.txt").CurrentScope);
        Assert.Null(At("f = { type = other text = { trigger = { | } } }", @"C:\x\common\cl\a.txt").CurrentScope);
    }

    [Fact]
    public void Events_UseTheEventTypeInThePath()
    {
        var context = At("mod.1 = { type = country_event id = a.1 immediate = { | } }", @"C:\mods\m\in_game\events\x.txt");

        Assert.Equal("country", context.CurrentScope);
        Assert.Equal("events/country_event/immediate", context.FieldPath);
    }

    [Fact]
    public void EventsWithoutType_HaveNoContext()
    {
        Assert.Contains("ohne type", At("mod.1 = { immediate = { | } }", @"C:\mods\m\in_game\events\x.txt").UnknownReason);
    }

    private static ScopeEngine EngineWithDefaultEventType()
    {
        var rules = TestData.Rules();
        rules.DefaultEventType = "country_event";
        return new ScopeEngine(TestData.Db(), rules);
    }

    [Fact]
    public void Events_without_type_use_the_default_event_type()
    {
        var context = At("mod.1 = { immediate = { | } }", @"C:\mods\m\events\x.txt", EngineWithDefaultEventType());

        Assert.Equal("country", context.CurrentScope);
        Assert.Equal("events/country_event/immediate", context.FieldPath);
    }

    [Fact]
    public void Scripted_definition_in_an_event_file_gets_no_default_type()
    {
        var context = At("scripted_trigger my_check = { immediate = { | } }", @"C:\mods\m\events\x.txt", EngineWithDefaultEventType());

        Assert.Null(context.CurrentScope);
        Assert.Contains("ohne type", context.UnknownReason);
    }

    [Fact]
    public void Event_scope_field_sets_the_root_scope()
    {
        var context = At("mod.1 = { type = country_event scope = location immediate = { | } }", @"C:\mods\m\events\x.txt");

        Assert.Equal("location", context.CurrentScope);
        Assert.True(context.Confident);
    }

    [Fact]
    public void Event_scope_none_leaves_the_scope_unknown()
    {
        var context = At("mod.1 = { type = country_event scope = none immediate = { | } }", @"C:\mods\m\events\x.txt");

        Assert.Null(context.CurrentScope);
        Assert.False(context.Confident);
    }

    [Fact]
    public void AnIteratorMakesTheScopeSureEvenWhenTheFieldHasNoRule()
    {
        var context = At("b = { unknown_field = { any_owned_location = { | } } }");

        Assert.Equal("location", context.CurrentScope);
        Assert.True(context.Confident);
        Assert.False(At("b = { unknown_field = { OR = { | } } }").Confident);
    }

    [Fact]
    public void UnfinishedCode_StillGivesTheScope()
    {
        Assert.Equal("location", At("b = { potential = { any_owned_location = { limit = { |").CurrentScope);
        Assert.Equal("location", At("b = { potential = { any_owned_location = { limit = { is_capital = \"unclosed\n  | } } }").CurrentScope);
    }

    [Fact]
    public void OutsideObjectsOrUnknownFiles_HaveNoContext()
    {
        Assert.Null(At("| b = { }").CurrentScope);
        Assert.Contains("common oder events", At("b = { potential = { | } }", @"C:\x\gui\a.gui").UnknownReason);
        Assert.Contains("nicht gelernt", At("b = { potential = { | } }", engine: new ScopeEngine(TestData.Db(), null)).UnknownReason);
    }

    [Fact]
    public void Check_ReportsAllowedNotAllowedAndUnknown()
    {
        var country = At("b = { potential = { | } }");

        Assert.Equal(ScopeCheck.Allowed, Engine.Check("add_gold", country).Result);
        Assert.Equal(ScopeCheck.Allowed, Engine.Check("has_dlc", country).Result);
        var notAllowed = Engine.Check("is_female", country);
        Assert.Equal(ScopeCheck.NotAllowed, notAllowed.Result);
        Assert.Equal(new[] { "character" }, notAllowed.Supported);
        Assert.Equal(ScopeCheck.Unknown, Engine.Check("no_such_thing", country).Result);
        Assert.Equal(ScopeCheck.Unknown, Engine.Check("add_gold", At("b = { potential = { from = { | } } }")).Result);
    }

    [Fact]
    public void Describe_ShowsTheChain()
    {
        var text = At("b = { potential = { any_owned_location = { limit = { | } } } }").Describe();

        Assert.Equal("Scope: location  ·  potential [country] › any_owned_location → location › limit", text);
        Assert.StartsWith("Scope: ?", At("b = { potential = { from = { | } } }").Describe());
    }

    [Theory]
    [InlineData(@"C:\mods\m\in_game\common\buildings\a.txt", "buildings")]
    [InlineData("/home/x/common/scripted_effects/sub/a.txt", "scripted_effects")]
    [InlineData(@"C:\mods\m\in_game\events\x.txt", "events")]
    [InlineData(@"C:\mods\m\in_game\events\sub\x.txt", "events")]
    [InlineData(@"C:\mods\m\in_game\gui\x.gui", null)]
    [InlineData(@"C:\mods\m\common\a.txt", null)]
    [InlineData(@"C:\Program Files\Steam\steamapps\common\Game\game\in_game\events\x.txt", "events")]
    [InlineData(@"C:\Program Files\Steam\steamapps\common\Game\game\in_game\common\buildings\a.txt", "buildings")]
    [InlineData(@"C:\Program Files\Steam\steamapps\common\Game\game\in_game\gui\a.gui", null)]
    [InlineData(null, null)]
    public void FolderOf_FindsTheScriptFolder(string? path, string? expected)
    {
        Assert.Equal(expected, ScopeEngine.FolderOf(path));
    }
}
