using System.Text.Json;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ScriptCheck.Tests;

public class ScriptCheckRulesTests
{
    private static readonly string[] Games = { "ck3", "vic3", "eu4", "eu5", "hoi4", "stellaris" };
    private static readonly string ProfilesDir = Path.Combine(AppContext.BaseDirectory, "profiles");

    private static ScriptCheckRules Rules(string id) =>
        ScriptCheckRules.ForProfile(RepoFiles.Profile(id), ProfilesDir) ?? throw new InvalidOperationException("Keine Regeln für " + id);

    [Fact]
    public void EveryGameHasRules()
    {
        foreach (var id in Games)
        {
            var r = ScriptCheckRules.ForProfile(RepoFiles.Profile(id), ProfilesDir);
            Assert.NotNull(r);
            Assert.NotEmpty(r!.EventFolders);
            Assert.NotEmpty(r.EffectFolders);
            Assert.NotEmpty(r.TriggerFolders);
            Assert.NotEmpty(r.OnActionFolders);
            Assert.NotEmpty(r.TransparentKeys);
            Assert.Contains("AND", r.TransparentKeys);
            Assert.Contains("OR", r.TransparentKeys);
            Assert.Contains("NOT", r.TransparentKeys);
            Assert.NotEmpty(r.ScriptContextKeys);
        }
    }

    [Fact]
    public void NoRulesFileHasConditionalKeys()
    {
        foreach (var id in Games)
        {
            var text = File.ReadAllText(Path.Combine(ProfilesDir, "scriptcheck", id + ".json"));
            Assert.DoesNotContain("conditionalKeys", text);
        }
    }

    [Fact]
    public void FoldersMatchInstalls()
    {
        Assert.Equal(new[] { "common/on_action" }, Rules("ck3").OnActionFolders);
        Assert.Equal(new[] { "common/on_actions" }, Rules("vic3").OnActionFolders);
        foreach (var id in new[] { "eu4", "hoi4", "stellaris" })
        {
            Assert.Equal(new[] { "common/on_actions" }, Rules(id).OnActionFolders);
        }

        Assert.Equal(new[] { "common/on_action" }, Rules("eu5").OnActionFolders);
        Assert.Empty(Rules("eu4").ValueFolders);
        Assert.Empty(Rules("hoi4").ValueFolders);
        foreach (var id in new[] { "ck3", "vic3", "eu5", "stellaris" })
        {
            Assert.Equal(new[] { "common/script_values" }, Rules(id).ValueFolders);
        }
    }

    [Fact]
    public void SelfFiringRules()
    {
        foreach (var id in new[] { "eu4", "hoi4", "stellaris" })
        {
            Assert.Equal("is_triggered_only", Rules(id).TriggeredOnlyField);
            Assert.Contains("mean_time_to_happen", Rules(id).SelfFiringFields);
        }

        Assert.Contains("dynamic_historical_event", Rules("eu5").SelfFiringFields);
        foreach (var id in new[] { "ck3", "vic3" })
        {
            Assert.Empty(Rules(id).SelfFiringFields);
            Assert.Null(Rules(id).TriggeredOnlyField);
        }
    }

    [Fact]
    public void EventCallKeys()
    {
        Assert.Equal(new[] { "trigger_event" }, Rules("ck3").EventCallKeys);
        Assert.Equal(new[] { "trigger_event" }, Rules("vic3").EventCallKeys);
        Assert.Contains("trigger_event_silently", Rules("eu5").EventCallKeys);
        Assert.Contains("trigger_event_non_silently", Rules("eu5").EventCallKeys);
        foreach (var key in RepoFiles.Profile("hoi4").EventBlockScopes!.Keys)
        {
            Assert.Contains(key, Rules("hoi4").EventCallKeys);
        }

        foreach (var key in RepoFiles.Profile("eu4").EventBlockScopes!.Keys)
        {
            Assert.Contains(key, Rules("eu4").EventCallKeys);
        }

        var st = Rules("stellaris");
        Assert.Contains("country_event", st.EventBlocks);
        Assert.Contains("ship_event", st.EventBlocks);
        Assert.Contains("situation_event", st.EventBlocks);
        Assert.Equal(st.EventBlocks, st.EventCallKeys);
        Assert.False(st.EventSubfolders);
        foreach (var id in new[] { "ck3", "vic3", "eu4", "eu5", "hoi4" })
        {
            Assert.True(Rules(id).EventSubfolders, id);
        }
    }

    [Fact]
    public void IsUnderHandlesPhaseFolders()
    {
        var r = new ScriptCheckRules();
        Assert.True(r.IsUnder("in_game/events/a.txt", new[] { "events" }, true));
        Assert.True(r.IsUnder("events/a.txt", new[] { "events" }, false));
        Assert.False(r.IsUnder("backup/events/a.txt", new[] { "events" }, true));
        Assert.False(r.IsUnder("events/sub/a.txt", new[] { "events" }, false));
        Assert.True(r.IsUnder("events/sub/a.txt", new[] { "events" }, true));
    }

    [Fact]
    public void IsUnderStripsRootPrefixes()
    {
        var eu5 = Rules("eu5");
        Assert.Equal(new[] { "dlc/*/" }, eu5.RootPrefixes);
        Assert.True(eu5.IsUnder("dlc/D001_x/in_game/events/a.txt", eu5.EventFolders, true));
        Assert.True(eu5.IsUnder("dlc/D001_x/in_game/common/scripted_effects/a.txt", eu5.EffectFolders, true));
        Assert.True(eu5.IsUnder("DLC/D001_x/events/a.txt", eu5.EventFolders, false));
        Assert.False(eu5.IsUnder("dlc/a/b/in_game/events/a.txt", eu5.EventFolders, true));
        Assert.False(eu5.IsUnder("dlc/D001_x/in_game/gfx/a.txt", eu5.EventFolders, true));

        var plain = new ScriptCheckRules();
        Assert.False(plain.IsUnder("dlc/D001_x/in_game/events/a.txt", new[] { "events" }, true));
        foreach (var id in new[] { "ck3", "vic3", "eu4", "hoi4", "stellaris" })
        {
            Assert.Empty(Rules(id).RootPrefixes);
        }
    }

    [Fact]
    public void FingerprintChangesWithContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string Write(string name, string builtin)
            {
                var p = Path.Combine(dir, name);
                File.WriteAllText(p, "{ \"extraBuiltins\": [\"" + builtin + "\"] }");
                return p;
            }

            var a = ScriptCheckRules.Load(Write("a.json", "x"));
            var a2 = ScriptCheckRules.Load(Write("a2.json", "x"));
            var b = ScriptCheckRules.Load(Write("b.json", "y"));
            Assert.Equal(a.Fingerprint, a2.Fingerprint);
            Assert.NotEqual(a.Fingerprint, b.Fingerprint);
            Assert.Equal(16, a.Fingerprint.Length);

            var bad = Path.Combine(dir, "bad.json");
            File.WriteAllText(bad, "{ nope");
            Assert.ThrowsAny<JsonException>(() => ScriptCheckRules.Load(bad));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Eu4IgnoresPatchnotes()
    {
        Assert.Contains("patchnotes", Rules("eu4").IgnoredFolders);
        Assert.Empty(Rules("ck3").IgnoredFolders);
    }

    [Theory]
    [InlineData("ck3")]
    [InlineData("vic3")]
    [InlineData("eu4")]
    [InlineData("eu5")]
    [InlineData("hoi4")]
    [InlineData("stellaris")]
    public void CallContextHoldsNoWeightBlocks(string id)
    {
        var r = Rules(id);
        Assert.NotEmpty(r.CallContextKeys);
        foreach (var key in new[] { "modifier", "weight", "factor", "chance", "ai_will_do" })
        {
            Assert.DoesNotContain(key, r.CallContextKeys);
            Assert.DoesNotContain(key, r.ScriptContextKeys);
        }

        // Aufrufkontext darf nicht durchsichtig werden: Zyklen über Hooks sind nie ein Error.
        Assert.Empty(r.CallContextKeys.Intersect(r.ScriptContextKeys));
    }

    private static ScriptCheckRules LoadJson(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-rules-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var p = Path.Combine(dir, "r.json");
            File.WriteAllText(p, json);
            return ScriptCheckRules.Load(p);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string PerformanceJson(string extraPulse) =>
        "{ \"performance\": { \"globalIterators\": [{ \"name\": \"every_country\", \"narrower\": [\"every_neighboring_country\"] }], "
        + "\"pulseOnActions\": { \"yearly_global_pulse\": \"yearly\", \"on_monthly_pulse\": \"monthly\"" + extraPulse + " }, "
        + "\"onActionCallLists\": [\"on_actions\"], \"alwaysCheckedBlocks\": [{ \"folder\": \"common/decisions\", \"blocks\": [\"is_shown\"] }], "
        + "\"polledEventBlocks\": [\"trigger\"] } }";

    [Fact]
    public void PerformanceRulesLoad()
    {
        var p = LoadJson(PerformanceJson("")).Performance;
        Assert.NotNull(p);
        Assert.Equal("every_country", p!.GlobalIterators[0].Name);
        Assert.Equal(new[] { "every_neighboring_country" }, p.GlobalIterators[0].Narrower);
        Assert.Equal(PulseRate.Yearly, p.PulseOnActions["yearly_global_pulse"]);
        Assert.Equal(PulseRate.Monthly, p.PulseOnActions["on_monthly_pulse"]);
        Assert.Equal(new[] { "on_actions" }, p.OnActionCallLists);
        Assert.Equal("common/decisions", p.AlwaysCheckedBlocks[0].Folder);
        Assert.Equal(new[] { "is_shown" }, p.AlwaysCheckedBlocks[0].Blocks);
        Assert.Equal(new[] { "trigger" }, p.PolledEventBlocks);
    }

    [Fact]
    public void CheckedBlockWrappersLoad()
    {
        var p = LoadJson("{ \"performance\": { \"alwaysCheckedBlocks\": [{ \"folder\": \"decisions\", \"blocks\": [\"potential\"], \"wrappers\": [\"country_decisions\"] }, "
            + "{ \"folder\": \"common/decisions\", \"blocks\": [\"is_shown\"] }] } }").Performance!;

        Assert.Equal(new[] { "country_decisions" }, p.AlwaysCheckedBlocks[0].Wrappers);
        Assert.Null(p.AlwaysCheckedBlocks[1].Wrappers);
        Assert.Null(LoadJson(PerformanceJson("")).Performance!.AlwaysCheckedBlocks[0].Wrappers);
    }

    [Fact]
    public void CheckedBlockNameFieldAndStarWrapperLoad()
    {
        var p = LoadJson("{ \"performance\": { \"alwaysCheckedBlocks\": [{ \"folder\": \"common/national_focus\", \"blocks\": [\"available\"], \"wrappers\": [\"focus_tree\"], \"nameField\": \"id\" }, "
            + "{ \"folder\": \"common/decisions\", \"blocks\": [\"visible\"], \"wrappers\": [\"*\"] }] } }").Performance!;

        Assert.Equal("id", p.AlwaysCheckedBlocks[0].NameField);
        Assert.Equal(new[] { "*" }, p.AlwaysCheckedBlocks[1].Wrappers);
        Assert.Null(p.AlwaysCheckedBlocks[1].NameField);
        Assert.Null(p.AlwaysCheckedBlocks[1].ExcludeFolders);
    }

    [Fact]
    public void CheckedBlockExcludeFoldersLoad()
    {
        var p = LoadJson("{ \"performance\": { \"alwaysCheckedBlocks\": [{ \"folder\": \"common/decisions\", \"blocks\": [\"visible\"], \"wrappers\": [\"*\"], "
            + "\"excludeFolders\": [\"common/decisions/categories\"] }] } }").Performance!;

        Assert.Equal(new[] { "common/decisions/categories" }, p.AlwaysCheckedBlocks[0].ExcludeFolders);
    }

    [Fact]
    public void Hoi4DecisionCategoriesExcluded()
    {
        var rule = ScriptCheckTestRoots.Rules("hoi4").Performance!.AlwaysCheckedBlocks.Single(r => r.Folder == "common/decisions");

        Assert.Equal(new[] { "common/decisions/categories" }, rule.ExcludeFolders);
    }

    [Fact]
    public void PerformanceRulesMissing()
    {
        Assert.Null(LoadJson("{ \"extraBuiltins\": [\"x\"] }").Performance);
    }

    [Fact]
    public void PerformanceChangesFingerprint()
    {
        var a = LoadJson(PerformanceJson(""));
        var b = LoadJson(PerformanceJson(", \"on_daily_pulse\": \"daily\""));
        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
    }
}
