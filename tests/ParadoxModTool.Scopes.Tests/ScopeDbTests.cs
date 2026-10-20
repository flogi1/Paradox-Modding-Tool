using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Scopes.Tests;

public class ScopeDbTests
{
    private static readonly DateTime T0 = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private static ScriptDocEntry Target(string name, string[] inputs, string output) =>
        new() { Kind = ScriptDocKind.EventTarget, Name = name, Scopes = inputs.ToList(), Targets = new() { output } };

    private static ScriptDocEntry Trigger(string name, string description) =>
        new() { Kind = ScriptDocKind.Trigger, Name = name, Description = description, Scopes = new() { "country" } };

    private static ScopeDb Db(params ScriptDocEntry[] entries)
    {
        var database = new ScriptDocDatabase();
        database.ApplyImport(entries, Enum.GetValues<ScriptDocKind>().ToList(), T0, new Dictionary<string, long>());
        return ScopeDb.FromDatabase(database);
    }

    [Fact]
    public void DuplicateTarget_HasPlainAndDataForm()
    {
        var entry = Target("culture", Array.Empty<string>(), "culture");
        entry.Extra["Requires Data"] = "yes";
        entry.Extra["Plain Inputs"] = "country, character";
        entry.Extra["Data Inputs"] = "";
        var target = Db(entry).GetTarget("culture")!;

        Assert.True(target.PlainForm);
        Assert.Equal(new[] { "character", "country" }, target.PlainInputs.Order());
        Assert.True(target.DataForm);
        Assert.Empty(target.DataInputs);
    }

    [Fact]
    public void OldEntriesWithoutFormInfo_KeepWorking()
    {
        var keyed = Target("area", Array.Empty<string>(), "area");
        keyed.Extra["Requires Data"] = "yes";
        var db = Db(keyed, Target("owner", new[] { "location" }, "country"));

        Assert.False(db.GetTarget("area")!.PlainForm);
        Assert.True(db.GetTarget("area")!.DataForm);
        Assert.True(db.GetTarget("owner")!.PlainForm);
        Assert.Equal(new[] { "location" }, db.GetTarget("owner")!.PlainInputs);
        Assert.False(db.GetTarget("owner")!.DataForm);
    }

    [Fact]
    public void Dynamic_variables_do_not_widen_trigger_scopes()
    {
        // HOI IV: viele Dynamic Variables heißen wie ein Trigger ("Auto generated using the trigger with same name").
        var trigger = new ScriptDocEntry { Kind = ScriptDocKind.Trigger, Name = "has_war", Scopes = new() { "country" } };
        var variable = new ScriptDocEntry { Kind = ScriptDocKind.DynamicVariable, Name = "has_war", Scopes = new() { "state" } };
        var db = Db(trigger, variable);

        Assert.Equal(new[] { "country" }, db.GetCommand("has_war")!.Scopes);
        Assert.False(db.IsScopeType("state") && db.GetCommand("has_war")!.Scopes.Contains("state"));
    }

    [Fact]
    public void NoneAnyScope_OfNewerGameBuilds_MeansAnyScopeLikeNone()
    {
        // Neuere Spiel-Builds schreiben "**Supported Scopes**: none/any", ältere "none". Beides heißt: in jedem Scope erlaubt.
        var saveScope = new ScriptDocEntry { Kind = ScriptDocKind.Effect, Name = "save_scope_as", Scopes = new() { "none/any" } };
        var old = new ScriptDocEntry { Kind = ScriptDocKind.Effect, Name = "activate_situation", Scopes = new() { "none" } };
        var db = Db(saveScope, old);

        Assert.True(db.GetCommand("save_scope_as")!.AnyScope);
        Assert.Empty(db.GetCommand("save_scope_as")!.Scopes);
        Assert.True(db.GetCommand("activate_situation")!.AnyScope);
    }

    [Fact]
    public void ComparisonTriggers_AndArguments_AreRecognised()
    {
        var tax = Trigger("country_tax_base", "Checks the total tax base of a country\nTraits: <, <=, =, !=, >, >=");
        var opinion = Trigger("opinion", "Usage: opinion(<target>) or opinion = { target = x value > 5 }\nTraits: <, <=, =, !=, >, >=");
        var flag = Trigger("has_flag", "Traits: yes/no");
        var db = Db(tax, opinion, flag);

        Assert.True(db.GetCommand("country_tax_base")!.IsComparison);
        Assert.False(db.GetCommand("country_tax_base")!.TakesArgument);
        Assert.True(db.GetCommand("opinion")!.TakesArgument);
        Assert.False(db.GetCommand("has_flag")!.IsComparison);
    }

    [Fact]
    public void VariousOutput_IsNoScope_ButAMarkOnTheTarget()
    {
        // Stellaris: "target" (spy_network, espionage_operation ...) schreibt "Output Scope: various" – das ist kein Scope-Typ.
        var db = Db(Target("target", new[] { "spy_network", "situation" }, "various"), Target("owner", new[] { "planet" }, "country"));

        Assert.Empty(db.GetTarget("target")!.Outputs);
        Assert.True(db.GetTarget("target")!.VariousOutputs);
        Assert.False(db.GetTarget("owner")!.VariousOutputs);
        Assert.False(db.IsScopeType("various"));
        Assert.True(db.IsScopeType("spy_network"));
    }

    [Fact]
    public void VariousOutput_IsNoStepOfThePathFinder()
    {
        var finder = new ScopePathFinder(Db(Target("target", Array.Empty<string>(), "various"), Target("owner", Array.Empty<string>(), "country")));

        var steps = finder.NextSteps("situation", first: true, includeGlobalLinks: false).Select(s => s.Name).ToList();

        Assert.Contains("owner", steps);
        Assert.DoesNotContain("target", steps);
    }
}
