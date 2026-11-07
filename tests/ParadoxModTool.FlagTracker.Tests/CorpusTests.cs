using System.Diagnostics;
using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using ParadoxModTool.GameProfiles;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.FlagTracker.Tests;

/// <summary>
/// Messlauf gegen echte Installationen, nur lesend (Spec „Prüfbare Beispiele“). Läuft nur, wenn die Umgebungsvariablen
/// <c>PMT_FLAGS_EU5_GAME</c>/<c>PMT_FLAGS_EU5_MOD</c> bzw. <c>PMT_FLAGS_CK3_GAME</c>/<c>PMT_FLAGS_CK3_MOD</c> auf vorhandene Ordner zeigen
/// (Runde 2, Spec „Prüfbare Beispiele Runde 2“: <c>PMT_FLAGS_HOI4_GAME</c>, <c>PMT_FLAGS_EU4_GAME</c>/<c>PMT_FLAGS_EU4_MOD</c>,
/// <c>PMT_FLAGS_STELLARIS_GAME</c> und <c>PMT_FLAGS_VIC3_GAME</c>, dort der Installationsordner bzw. bei Victoria 3 <c>game</c>);
/// sonst kehrt jeder Test sofort zurück. Der Speicher von <see cref="FlagIndexStore"/> liegt in einem Temp-Ordner.
/// </summary>
public sealed class CorpusTests : IDisposable
{
    private static readonly Dictionary<string, Lazy<FlagIndex>> Cache = new();

    private readonly ITestOutputHelper _output;
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-flagcorpus-" + Guid.NewGuid().ToString("N"));

    public CorpusTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_temp))
            {
                Directory.Delete(_temp, true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Eu5_UnlockedPolicy()
    {
        if (Folder("PMT_FLAGS_EU5_GAME") is not { } game)
        {
            return;
        }

        var index = Index("eu5", game, null);
        var sites = index.Search("unlocked_policy_protestant_tolerance").SitesByName["unlocked_policy_protestant_tolerance"];
        Print(sites);

        Assert.Contains(sites, s => s.Role == FlagRole.Set && s.Origin == FlagOrigin.Vanilla
            && s.Location.RelativePath == "in_game/common/decisions/flavor_decisions_fra.txt" && s.Location.Line == 342
            && s.Via is { RelativePath: "in_game/common/scripted_effects/country_effects.txt", Line: 131 });
        Assert.Contains(sites, s => s.Role == FlagRole.Read && s.ViaName == "has_unlocked_policy_trigger"
            && s.Via is { RelativePath: "in_game/common/scripted_triggers/country_triggers.txt", Line: 363 });
    }

    /// <summary>Effect und Trigger heißen beide ai_international_organization_diplo_action_cooldown; beide Vorlagen werden aufgelöst.</summary>
    [Fact]
    public void Eu5_EffectAndTriggerWithSameName()
    {
        if (Folder("PMT_FLAGS_EU5_GAME") is not { } game)
        {
            return;
        }

        var result = Index("eu5", game, null).Search("ai_rejected_org_actions_*");
        var sites = result.SitesByName.Values.SelectMany(v => v).Concat(result.PatternSites).ToList();
        Print(sites.Where(s => s.Name == "ai_rejected_org_actions_free_city_status"));

        const string Effects = "in_game/common/scripted_effects/country_effects.txt";
        const string Triggers = "in_game/common/scripted_triggers/international_organization_triggers.txt";
        Assert.Contains(sites, s => s.Role == FlagRole.Set && s.Via?.RelativePath == Effects);
        Assert.Contains(sites, s => s.Role == FlagRole.Remove && s.Via?.RelativePath == Effects);
        Assert.Contains(sites, s => s.Role == FlagRole.Read && s.Via?.RelativePath == Triggers);
        var named = result.SitesByName["ai_rejected_org_actions_free_city_status"];
        Assert.Contains(named, s => s.Role == FlagRole.Set);
        Assert.Contains(named, s => s.Role == FlagRole.Read);
    }

    [Fact]
    public void Ck3_IncrementVariableEffect()
    {
        if (Folder("PMT_FLAGS_CK3_GAME") is not { } game)
        {
            return;
        }

        var index = Index("ck3", game, null);
        var sites = index.Search("num_exorbitant_feasts_hosted").SitesByName["num_exorbitant_feasts_hosted"];
        Print(sites);

        Assert.Contains(sites, s => s.Role == FlagRole.Set && s.ViaName == "increment_variable_effect"
            && s.Location.RelativePath == "common/activities/activity_types/feast.txt" && s.Location.Line == 4885);
    }

    [Fact]
    public void Ck3_FlogiCommentedGlobals()
    {
        if (Folder("PMT_FLAGS_CK3_GAME") is not { } game || Folder("PMT_FLAGS_CK3_MOD") is not { } mod)
        {
            return;
        }

        const string Visit = "common/activities/activity_types/university_visit.txt";
        var index = Index("ck3", game, mod);
        var inFile = index.Sites.Where(s => s.Location.RelativePath == Visit).ToList();
        _output.WriteLine($"{inFile.Count} Fundstellen in {Visit}, davon aus der aktiven Mod: {inFile.Count(s => s.Origin == FlagOrigin.ActiveMod)}");

        // Flogis Datei ersetzt die gleichnamige Vanilla-Datei und wird gelesen.
        var sources = Sources("ck3", game, mod);
        Assert.Contains(sources.EnumerateFiles(), f => f.Root.Origin == FlagOrigin.ActiveMod && f.RelativePath == Visit);
        Assert.DoesNotContain(inFile, s => s.Origin == FlagOrigin.Vanilla);

        // Globale Variablen liest der Leser anderswo, nur hier sind sie auskommentiert.
        Assert.Contains(index.Sites, s => s.Kind == FlagKind.GlobalVariable && s.Role == FlagRole.Set);
        Assert.DoesNotContain(inFile, s => s.Kind is FlagKind.GlobalVariable or FlagKind.GlobalVariableList && s.Role == FlagRole.Set);
        var visits = index.Search("university_visit_*");
        Assert.DoesNotContain(visits.SitesByName.Values.SelectMany(v => v), s => s.Location.RelativePath == Visit && s.Role == FlagRole.Set);
    }

    [Fact]
    public void Eu5_Iv2Family()
    {
        if (Folder("PMT_FLAGS_EU5_GAME") is not { } game || Folder("PMT_FLAGS_EU5_MOD") is not { } mod)
        {
            return;
        }

        var index = Index("eu5", game, mod);
        var family = index.Search("iv2_ideagroup_select_adm_*");
        _output.WriteLine($"iv2_ideagroup_select_adm_*: {family.Names.Count} Namen: {string.Join(", ", family.Names.Select(n => n.Name))}");
        Assert.True(family.Names.Count > 1);

        var cost = index.Search("iv2_var_idea_cost_adm").SitesByName["iv2_var_idea_cost_adm"];
        Print(cost.Where(s => s.Location.RelativePath.EndsWith(".gui", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(cost, s => s.Role == FlagRole.Read && s.Origin == FlagOrigin.ActiveMod
            && s.Location.RelativePath.EndsWith("/IV2_IG_types_adm.gui", StringComparison.Ordinal) && s.Location.Line == 74);
    }

    [Fact]
    public void Ck3_FullBuildTime()
    {
        if (Folder("PMT_FLAGS_CK3_GAME") is not { } game || Folder("PMT_FLAGS_CK3_MOD") is not { } mod)
        {
            return;
        }

        Measure("ck3", game, mod);
    }

    [Fact]
    public void Eu5_FullBuildTime()
    {
        if (Folder("PMT_FLAGS_EU5_GAME") is not { } game || Folder("PMT_FLAGS_EU5_MOD") is not { } mod)
        {
            return;
        }

        Measure("eu5", game, mod);
    }

    [Fact]
    public void Hoi4_CountryFlagSet()
    {
        if (Folder("PMT_FLAGS_HOI4_GAME") is not { } game)
        {
            return;
        }

        var sites = Index("hoi4", game, null).Search("ETH_disintegration_happened_flag").SitesByName["ETH_disintegration_happened_flag"];
        Print(sites);

        Assert.Contains(sites, s => s.Role == FlagRole.Set && s.Kind == FlagKind.ScopeFlag && s.Family == "country_flag"
            && s.Location.RelativePath == "common/bop/ETH.txt");
    }

    /// <summary><c>595.political_platform_@THIS</c>: Zahl vorne abgeschnitten, <c>@THIS</c> wird <c>*</c>.</summary>
    [Fact]
    public void Hoi4_AtThisPattern()
    {
        if (Folder("PMT_FLAGS_HOI4_GAME") is not { } game)
        {
            return;
        }

        const string File = "common/decisions/WTT_politcal_power_struggle_decisions.txt";
        var sites = Index("hoi4", game, null).Sites.Where(s => s.Name == "political_platform_*" && s.Location.RelativePath == File).ToList();
        _output.WriteLine($"{sites.Count} Fundstellen von political_platform_* in {File}, die ersten und die aus der Erhebung (Zeile 323):");
        Print(sites.Take(3).Concat(sites.Where(s => s.Location.Line == 323)));

        Assert.Contains(sites, s => s.IsPattern && s.Role == FlagRole.Read && s.Kind == FlagKind.Variable);
    }

    /// <summary><c>clear_variable = global.CZE_selling_country</c>: Präfix <c>global.</c> abgeschnitten.</summary>
    [Fact]
    public void Hoi4_GlobalPrefixStripped()
    {
        if (Folder("PMT_FLAGS_HOI4_GAME") is not { } game)
        {
            return;
        }

        var sites = Index("hoi4", game, null).Search("CZE_selling_country").SitesByName["CZE_selling_country"];
        Print(sites.Where(s => s.Role == FlagRole.Remove));

        Assert.Contains(sites, s => s.Role == FlagRole.Remove && s.Kind == FlagKind.Variable && s.Location.RelativePath == "common/decisions/CZE.txt");
    }

    /// <summary>Für den Testplan: <c>ETH_AI_RANDOM_ANARCHIST</c> (gesetzt) und <c>ETH_AI_RANDOM_anarchist</c> (gelesen) sind in HOI IV ein Name.</summary>
    [Fact]
    public void Hoi4_CaseVariantsAreOneName()
    {
        if (Folder("PMT_FLAGS_HOI4_GAME") is not { } game)
        {
            return;
        }

        var result = Index("hoi4", game, null).Search("eth_ai_random_anarchist");
        var name = Assert.Single(result.Names);
        Print(result.SitesByName[name.Name]);

        Assert.True(name.SetCount > 0 && name.ReadCount > 0, $"{name.Name}: gesetzt {name.SetCount}, gelesen {name.ReadCount}");
    }

    /// <summary><c>set_variable = { which = blood_kill which = land_units_killed }</c>: erster Name gesetzt, zweiter gelesen.</summary>
    [Fact]
    public void Eu4_TwoNamesInOneBlock()
    {
        if (Folder("PMT_FLAGS_EU4_GAME") is not { } game)
        {
            return;
        }

        const string File = "common/on_actions/00_on_actions.txt";
        var index = Index("eu4", game, null);
        var set = index.Search("blood_kill").SitesByName["blood_kill"].Where(s => s.Location.RelativePath == File).ToList();
        var read = index.Search("land_units_killed").SitesByName["land_units_killed"].Where(s => s.Location.RelativePath == File).ToList();
        Print(set.Concat(read));

        Assert.Contains(set, s => s.Role == FlagRole.Set && s.Kind == FlagKind.Variable);
        Assert.Contains(read, s => s.Role == FlagRole.Read && s.Kind == FlagKind.Variable
            && set.Any(x => x.Role == FlagRole.Set && x.Location.Line + 1 == s.Location.Line));
    }

    [Fact]
    public void Eu4_IdeaVariationDevPoints()
    {
        if (Folder("PMT_FLAGS_EU4_GAME") is not { } game || Folder("PMT_FLAGS_EU4_MOD") is not { } mod)
        {
            return;
        }

        const string File = "common/scripted_effects/02_scripted_effects_for_dev_points.txt";
        var sites = Index("eu4", game, mod).Search("precalc_monthly_dev_points").SitesByName["precalc_monthly_dev_points"];
        Print(sites.Where(s => s.Location.RelativePath == File));

        Assert.Contains(sites, s => s.Role == FlagRole.Set && s.Kind == FlagKind.Variable && s.Origin == FlagOrigin.ActiveMod
            && s.Location.RelativePath == File);
    }

    /// <summary><c>[emerald_buddha_province.GetName]</c> zählt, weil das Skript den Namen als (globales) Event-Target setzt.</summary>
    [Fact]
    public void Eu4_EventTargetReadInLocalisation()
    {
        if (Folder("PMT_FLAGS_EU4_GAME") is not { } game)
        {
            return;
        }

        var sites = Index("eu4", game, null).Search("emerald_buddha_province").SitesByName["emerald_buddha_province"];
        Print(sites);

        Assert.Contains(sites, s => s.Role == FlagRole.Read && s.Kind is FlagKind.EventTarget or FlagKind.GlobalEventTarget
            && s.Location.RelativePath == "localisation/00_lanfang_l_english.yml");
        Assert.Contains(sites, s => s.Role == FlagRole.Set);
    }

    [Fact]
    public void Stellaris_AtRootPattern()
    {
        if (Folder("PMT_FLAGS_STELLARIS_GAME") is not { } game)
        {
            return;
        }

        const string File = "common/casus_belli/00_casus_belli.txt";
        var sites = Index("stellaris", game, null).Sites.Where(s => s.Name == "infested_star*" && s.Location.RelativePath == File).ToList();
        Print(sites);

        Assert.Contains(sites, s => s.IsPattern && s.Role == FlagRole.Read && s.Kind == FlagKind.ScopeFlag && s.Family == "country_flag");
    }

    [Fact]
    public void Stellaris_PolicyFlag()
    {
        if (Folder("PMT_FLAGS_STELLARIS_GAME") is not { } game)
        {
            return;
        }

        var sites = Index("stellaris", game, null).Search("diplo_stance_belligerent").SitesByName["diplo_stance_belligerent"];
        Print(sites.Where(s => s.Role == FlagRole.Set));

        Assert.Contains(sites, s => s.Role == FlagRole.Set && s.Kind == FlagKind.DefinitionFlag && s.Location.RelativePath == "common/policies/00_policies.txt");
    }

    /// <summary>
    /// Für den Testplan: <c>harvest_$RESOURCE$_contract_active</c> in <c>common/inline_scripts/contracts/HarvestResource.txt</c> wird über den
    /// Aufruf mit <c>RESOURCE = minerals</c> in <c>common/missions/missions/02_contracts.txt</c> aufgelöst.
    /// </summary>
    [Fact]
    public void Stellaris_InlineScriptResolved()
    {
        if (Folder("PMT_FLAGS_STELLARIS_GAME") is not { } game)
        {
            return;
        }

        var sites = Index("stellaris", game, null).Search("harvest_minerals_contract_active").SitesByName["harvest_minerals_contract_active"];
        Print(sites);

        Assert.Contains(sites, s => s.ViaName == "inline_script:contracts/HarvestResource"
            && s.Location.RelativePath == "common/missions/missions/02_contracts.txt");
    }

    /// <summary><c>has_variable = $UNLOCK_VAR$</c> im Scripted Effect, aufgelöst über den Aufruf mit <c>UNLOCK_VAR = prestige_good_generic_clothes_var</c>.</summary>
    [Fact]
    public void Vic3_ParamResolvedViaCall()
    {
        if (Folder("PMT_FLAGS_VIC3_GAME") is not { } game)
        {
            return;
        }

        var sites = Index("vic3", game, null).Search("prestige_good_generic_clothes_var").SitesByName["prestige_good_generic_clothes_var"];
        Print(sites);

        Assert.Contains(sites, s => s.Via is not null && s.Location.RelativePath == "common/journal_entries/05_prestige_goods.txt");
    }

    [Fact]
    public void Vic3_GlobalListRead()
    {
        if (Folder("PMT_FLAGS_VIC3_GAME") is not { } game)
        {
            return;
        }

        var sites = Index("vic3", game, null).Search("aes_country_list").SitesByName["aes_country_list"];
        Print(sites.Where(s => s.Location.RelativePath == "common/journal_entries/00_fascism.txt"));

        Assert.Contains(sites, s => s.Role == FlagRole.Read && s.Kind == FlagKind.GlobalVariableList
            && s.Location.RelativePath == "common/journal_entries/00_fascism.txt");
    }

    [Fact]
    public void Hoi4_FullBuildTime()
    {
        if (Folder("PMT_FLAGS_HOI4_GAME") is not { } game)
        {
            return;
        }

        Measure("hoi4", game, null);
    }

    [Fact]
    public void Eu4_FullBuildTime()
    {
        if (Folder("PMT_FLAGS_EU4_GAME") is not { } game || Folder("PMT_FLAGS_EU4_MOD") is not { } mod)
        {
            return;
        }

        Measure("eu4", game, mod);
    }

    [Fact]
    public void Stellaris_FullBuildTime()
    {
        if (Folder("PMT_FLAGS_STELLARIS_GAME") is not { } game)
        {
            return;
        }

        Measure("stellaris", game, null);
    }

    [Fact]
    public void Vic3_FullBuildTime()
    {
        if (Folder("PMT_FLAGS_VIC3_GAME") is not { } game)
        {
            return;
        }

        Measure("vic3", game, null);
    }

    /// <summary>
    /// Zwei volle Aufbauten (der erste in diesem Prozess, mit unbekanntem Datei-Cache des Systems), dann Refresh in einen leeren Speicher
    /// (voller Aufbau mit Speichern) und Refresh ohne Änderungen. Schreibt Zeiten und Zahlen in die Testausgabe; keine Zeitprüfung.
    /// </summary>
    private void Measure(string gameId, string game, string? mod)
    {
        var before = (mod is null ? string.Empty : Stamp(mod)) + Stamp(game);
        var sources = Sources(gameId, game, mod);
        var builder = new FlagIndexBuilder(sources, Table(gameId));

        var watch = Stopwatch.StartNew();
        var cold = builder.Build();
        var tCold = watch.Elapsed;
        watch.Restart();
        var warm = builder.Build();
        var tWarm = watch.Elapsed;

        var store = new FlagIndexStore(_temp);
        watch.Restart();
        builder.Refresh(store);
        var tFirstRefresh = watch.Elapsed;
        watch.Restart();
        var refreshed = builder.Refresh(store);
        var tRefresh = watch.Elapsed;

        _output.WriteLine($"{gameId}: Wurzeln {string.Join(" | ", sources.Roots.Select(r => r.Name + " " + r.Directory))}");
        _output.WriteLine($"Aufbau erster {tCold.TotalSeconds:F1} s, zweiter {tWarm.TotalSeconds:F1} s, Refresh leerer Speicher {tFirstRefresh.TotalSeconds:F1} s, Refresh ohne Änderung {tRefresh.TotalSeconds:F1} s");
        _output.WriteLine($"Dateien {warm.FileCount}, ungelesen {warm.UnreadFiles.Count}, Fundstellen {warm.Sites.Count}, Muster {warm.Sites.Count(s => s.IsPattern)}, über Via {warm.Sites.Count(s => s.Via is not null)}, Namen aktive Mod {warm.Sites.Where(s => s.Origin == FlagOrigin.ActiveMod).Select(s => s.Name).Distinct().Count()}");
        _output.WriteLine($"Kandidaten verworfen {builder.LastDroppedCandidates}");
        _output.WriteLine($"Speicher {new FileInfo(store.FileFor(sources.ScopeKey)).Length / 1024} KiB");

        Assert.Equal(cold.Sites.Count, warm.Sites.Count);
        Assert.Equal(warm.Sites.Count, refreshed.Sites.Count);
        Assert.Equal(warm.FileCount, refreshed.FileCount);
        Assert.Equal(before, (mod is null ? string.Empty : Stamp(mod)) + Stamp(game));
    }

    private static string? Folder(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrEmpty(value) || !Directory.Exists(value) ? null : value;
    }

    private static GameProfile Profile(string gameId) =>
        GameProfileLoader.LoadFromFile(Path.Combine(AppContext.BaseDirectory, "profiles", gameId + ".json"));

    /// <summary>CK III ohne ScriptDocs-Zusatzbefehle (nur die mitgelieferten Flag-Befehle).</summary>
    private static FlagCommandTable Table(string gameId) => FlagCommandTable.For(gameId)!;

    /// <summary>
    /// Bereich „Playset“ ohne weitere Mods: Vanilla-Wurzeln wie in der App, optional die aktive Mod. Installationsordner wie
    /// <c>GameContentRoot.InstallRoot</c>: bei Jomini-Spielen zeigt die Variable auf <c>game</c>, der Installationsordner ist dessen
    /// Elternordner (Wurzeln <c>game</c>, <c>jomini</c>, <c>clausewitz</c>); bei HOI IV, EU IV und Stellaris zeigt sie auf den
    /// Installationsordner, der dann selbst die einzige Wurzel ist.
    /// </summary>
    private static FlagSourceSet Sources(string gameId, string game, string? mod)
    {
        var install = string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(game)), "game", StringComparison.OrdinalIgnoreCase)
            ? Directory.GetParent(Path.TrimEndingDirectorySeparator(game))!.FullName
            : game;
        var vanilla = ErrorLogSources.VanillaRoots(install, game);
        return FlagSourceSet.ForPlayset(Profile(gameId), Array.Empty<ModSource>(), mod ?? string.Empty, vanilla);
    }

    private static FlagIndex Index(string gameId, string game, string? mod)
    {
        Lazy<FlagIndex> lazy;
        lock (Cache)
        {
            var key = gameId + "|" + game + "|" + mod;
            if (!Cache.TryGetValue(key, out lazy!))
            {
                lazy = new Lazy<FlagIndex>(() => new FlagIndexBuilder(Sources(gameId, game, mod), Table(gameId)).Build());
                Cache[key] = lazy;
            }
        }

        return lazy.Value;
    }

    private void Print(IEnumerable<FlagSite> sites)
    {
        foreach (var s in sites)
        {
            _output.WriteLine($"{s.Role} {s.Kind} {s.Origin} {s.Location.RelativePath}:{s.Location.Line}"
                + (s.Via is { } via ? $" via {s.ViaName} {via.RelativePath}:{via.Line}" : string.Empty));
        }
    }

    /// <summary>Anzahl, Gesamtlänge und jüngste Änderungszeit der Dateien eines Ordners, um Schreibzugriffe zu erkennen.</summary>
    private static string Stamp(string directory)
    {
        var files = new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
        return $"{files.Count}/{files.Sum(f => f.Length)}/{(files.Count == 0 ? 0 : files.Max(f => f.LastWriteTimeUtc.Ticks))};";
    }
}
