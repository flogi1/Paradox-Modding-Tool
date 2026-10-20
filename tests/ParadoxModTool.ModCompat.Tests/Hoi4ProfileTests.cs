using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>Das mitgelieferte Profil von Hearts of Iron IV (<c>profiles/hoi4.json</c>).</summary>
public sealed class Hoi4ProfileTests
{
    private static GameProfile Profile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParadoxModTool.slnx")))
        {
            dir = dir.Parent;
        }

        return GameProfileLoader.LoadFromFile(Path.Combine(dir!.FullName, "profiles", "hoi4.json"));
    }

    [Fact]
    public void Hoi4_profile_loads_and_resolves_rules()
    {
        var profile = Profile();
        var rules = OverrideRules.FromProfile(profile);

        Assert.Equal("hoi4", profile.Id);
        Assert.Equal("394360", profile.SteamAppId);
        Assert.Equal(new DateOnly(2016, 6, 6), profile.ReleaseDate);
        Assert.Equal(OverrideBehavior.FirstWins, rules.Match("common/ideas/GER.txt").Behavior);
        Assert.Equal(new[] { "ideas/*" }, rules.Match("common/ideas/GER.txt").Unwrap);
        Assert.Equal(OverrideBehavior.PartialKeys, rules.Match("common/defines/zz_mod.lua").Behavior);
        Assert.True(rules.Match("common/defines/zz_mod.lua").Certain);
        Assert.Equal(OverrideBehavior.FirstWins, rules.Match("history/states/1-France.txt").Behavior);
        Assert.Equal(OverrideBehavior.WholeFile, rules.Match("history/countries/GER - Germany.txt").Behavior);
        Assert.False(rules.Match("common/national_focus/germany.txt").Certain);
        Assert.Empty(profile.KeyOperators);
    }

    [Fact]
    public void Measured_rules_are_certain()
    {
        // Gemessen 2026-10-16 mit PMT Order Probe A/B und PMT Replace Probe C/D (Läufe 2a/2b).
        var profile = Profile();
        var rules = OverrideRules.FromProfile(profile);

        Assert.Equal(OverrideBehavior.FirstWins, rules.Match("common/ideas/x.txt").Behavior);
        Assert.True(rules.Match("common/ideas/x.txt").Certain);
        Assert.Equal(OverrideBehavior.FirstWins, rules.Match("events/x.txt").Behavior);
        Assert.True(rules.Match("events/x.txt").Certain);
        Assert.Equal(OverrideBehavior.LastWins, rules.Match("common/scripted_triggers/x.txt").Behavior);
        Assert.True(rules.Match("common/scripted_triggers/x.txt").Certain);
        Assert.Equal(OverrideBehavior.AppendOnly, rules.Match("common/scripted_effects/x.txt").Behavior);
        Assert.True(rules.Match("common/scripted_effects/x.txt").Certain);
        Assert.Equal(OverrideBehavior.AppendOnly, rules.Match("common/on_actions/x.txt").Behavior);
        Assert.True(rules.Match("common/on_actions/x.txt").Certain);
        Assert.True(profile.ReplacePathHidesEarlierMods);
        Assert.True(profile.FirstWinsInLoadOrder);
        Assert.True(profile.LastWinsInLoadOrder);
    }

    [Fact]
    public void Hoi4_profile_reads_docs_from_the_install_folder()
    {
        var profile = Profile();

        Assert.Equal("install", profile.ScriptDocsBase);
        Assert.Equal("documentation", profile.ScriptDocsFolder);
        Assert.Equal("dynamic_variables_documentation.md", profile.ScriptDocsFiles!["DynamicVariable"]);
        Assert.Equal("learned", profile.EventBlockScopes!["country_event"]);
        Assert.Equal(5, profile.EventBlockScopes.Count);
        Assert.True(profile.LoadOrderCaseSensitive);
        Assert.True(profile.NumericKeysChangeScope);
        Assert.Equal(10, profile.LocalizationLanguages.Count);
    }
}
