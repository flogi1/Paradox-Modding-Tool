using ParadoxModTool.ErrorLog;
using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public sealed class StellarisTests : IDisposable
{
    private static readonly FlagCommandTable Table = FlagCommandTable.Stellaris(Array.Empty<string>());
    private static readonly ScriptReader Reader = new(Table);

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "pmt-flagstl-" + Guid.NewGuid().ToString("N"));
    private readonly string _vanilla;
    private readonly string _mod;

    public StellarisTests()
    {
        _vanilla = Path.Combine(_temp, "v");
        _mod = Path.Combine(_temp, "m");
        Directory.CreateDirectory(_vanilla);
        Directory.CreateDirectory(_mod);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temp, true);
        }
        catch (IOException)
        {
        }
    }

    private static List<RawSite> Read(string text, string path = "events/e.txt") => Reader.Read(path, text).Sites;

    private static (string Name, FlagKind Kind, FlagRole Role) Short(RawSite s) => (s.Name, s.Kind, s.Role);

    [Fact]
    public void For_Stellaris()
    {
        var table = FlagCommandTable.For("stellaris", new[] { "set_dynasty_flag" })!;
        Assert.NotNull(table.Find("set_dynasty_flag"));
        Assert.Null(FlagCommandTable.Stellaris(new[] { "set_evopred_flag", "has_policy_flag" }).Find("set_evopred_flag"));
        Assert.True(Table.AtSuffixPatterns);
        Assert.True(Table.EventTargetPrefix);
        Assert.True(Table.ConditionalBlocks);
        Assert.True(Table.InlineScripts);
        Assert.False(Table.NamesIgnoreCase);
        Assert.False(Table.Hoi4VariableNames);
        Assert.False(Table.MetaBracketPatterns);
        Assert.False(Table.HasSymbolValues);
        Assert.False(FlagCommandTable.Eu4.InlineScripts);
        Assert.Equal(LocStyle.StellarisBrackets, Table.LocalisationStyle);
        Assert.Contains(Table.DefinitionFlagBlocks, b => b.BlockKey == "policy_flags");
        Assert.NotEqual(FlagCommandTable.Eu4.Fingerprint, Table.Fingerprint);
        Assert.NotEqual(Table.Fingerprint, table.Fingerprint);
    }

    [Fact]
    public void Shipped_AllAreFlagCommands()
    {
        Assert.Contains("has_global_flag", StellarisCommands.Shipped);
        Assert.Contains("set_timed_global_flag", StellarisCommands.Shipped);
        Assert.DoesNotContain("has_policy_flag", StellarisCommands.Shipped);
        Assert.All(StellarisCommands.Shipped, n => Assert.True(StellarisCommands.IsFlagCommand(n), n));
    }

    [Fact]
    public void FlagFamilies()
    {
        var sites = Read(
            "set_country_flag = a\nset_timed_star_flag = { flag = b days = 10 }\nhas_relation_flag = { flag = c who = root }\nremove_planet_flag = a\nreverse_has_relation_flag = { flag = c who = root }");

        Assert.Equal(
            new[]
            {
                new RawSite("a", FlagKind.ScopeFlag, "country_flag", FlagRole.Set, 1),
                new RawSite("b", FlagKind.ScopeFlag, "star_flag", FlagRole.Set, 2),
                new RawSite("c", FlagKind.ScopeFlag, "relation_flag", FlagRole.Read, 3),
                new RawSite("a", FlagKind.ScopeFlag, "planet_flag", FlagRole.Remove, 4),
                new RawSite("c", FlagKind.ScopeFlag, "relation_flag", FlagRole.Read, 5),
            },
            sites);
    }

    [Fact]
    public void DeniedFlagNames()
    {
        Assert.Empty(Read("change_country_flag = { icon = x }\nis_occupied_flag = no\nhas_any_flag = no\nset_evopred_flag = { TYPE = a }"));
    }

    [Fact]
    public void AtSuffix()
    {
        Assert.Equal(
            new RawSite("infested_star*", FlagKind.ScopeFlag, "country_flag", FlagRole.Read, 1, IsPattern: true),
            Assert.Single(Read("has_country_flag = infested_star@root")));
    }

    [Fact]
    public void NamesAreCaseSensitive_CommandsAreNot()
    {
        Assert.Equal(("Space_Owner", FlagKind.ScopeFlag, FlagRole.Set), Short(Assert.Single(Read("Set_Country_Flag = Space_Owner"))));
    }

    [Fact]
    public void Variables()
    {
        Assert.Equal(
            new[] { ("v", FlagKind.Variable, FlagRole.Set), ("w", FlagKind.Variable, FlagRole.Read) },
            Read("set_variable = { which = v value = w }").Select(Short));
        Assert.Empty(Read("check_variable_arithmetic = { which = value:sv|A|1| value = 2 }"));
        Assert.Equal(("v", FlagKind.Variable, FlagRole.Read), Short(Assert.Single(Read("is_variable_set = v"))));
        Assert.Equal(("v", FlagKind.Variable, FlagRole.Remove), Short(Assert.Single(Read("clear_variable = v"))));
        Assert.Equal(("v", FlagKind.Variable, FlagRole.Set), Short(Assert.Single(Read("floor_variable = v"))));
        Assert.Equal(("e", FlagKind.Variable, FlagRole.Set), Short(Assert.Single(Read("export_trigger_value_to_variable = { variable = e trigger = t }"))));
        Assert.Empty(Read("check_variable = { which = trigger:x value > 1 }\nis_variable_set = @c"));
    }

    [Fact]
    public void VariableValue_OnlyValueField()
    {
        Assert.Equal(
            ("r", FlagKind.Variable, FlagRole.Set),
            Short(Assert.Single(Read("set_variable_to_random_value = { which = r min = lo max = hi }"))));
        Assert.Equal(
            ("a", FlagKind.Variable, FlagRole.Read),
            Short(Assert.Single(Read("check_variable_arithmetic = { which = a value = 2 multiply = m }"))));
    }

    [Fact]
    public void VariableValue_ScopeChains()
    {
        Assert.Equal(
            new[] { ("a", FlagKind.Variable, FlagRole.Set), ("monthly_increase", FlagKind.Variable, FlagRole.Read) },
            Read("change_variable = { which = a value = root.monthly_increase }").Select(Short));
        Assert.Equal(("a", FlagKind.Variable, FlagRole.Set), Short(Assert.Single(Read("set_variable = { which = a value = from.owner }"))));
        Assert.Equal(("a", FlagKind.Variable, FlagRole.Set), Short(Assert.Single(Read("set_variable = { which = a value = prev.prev }"))));
        Assert.Equal(("number_fleets_spawned", FlagKind.Variable, FlagRole.Set), Short(Assert.Single(Read("change_variable = { which = from.number_fleets_spawned value = 1 }"))));
    }

    [Fact]
    public void EventTargets()
    {
        Assert.Equal(
            new[]
            {
                ("g", FlagKind.GlobalEventTarget, FlagRole.Set),
                ("l", FlagKind.EventTarget, FlagRole.Set),
                ("g", FlagKind.GlobalEventTarget, FlagRole.Remove),
                ("g", FlagKind.EventTarget, FlagRole.Read),
                ("storm_rider", FlagKind.EventTarget, FlagRole.Read),
                ("v", FlagKind.Variable, FlagRole.Set),
                ("days_inside_storm", FlagKind.Variable, FlagRole.Read),
                ("storm_rider", FlagKind.EventTarget, FlagRole.Read),
            },
            Read("save_global_event_target_as = g\nsave_event_target_as = l\nclear_global_event_target = g\nevent_target:g = { }\n"
                + "event_target:storm_rider.days_inside_storm = { }\nset_variable = { which = v value = event_target:storm_rider.days_inside_storm }").Select(Short));
    }

    [Fact]
    public void PolicyFlags()
    {
        var sites = Read("p = {\n\toption = {\n\t\tpolicy_flags = { diplo_stance_belligerent }\n\t}\n}", "common/policies/p.txt");
        Assert.Equal(new RawSite("diplo_stance_belligerent", FlagKind.DefinitionFlag, null, FlagRole.Set, 3), Assert.Single(sites));
        Assert.Equal(
            new RawSite("diplo_stance_belligerent", FlagKind.DefinitionFlag, null, FlagRole.Read, 1),
            Assert.Single(Read("has_policy_flag = diplo_stance_belligerent")));
    }

    [Fact]
    public void InitializerFlags()
    {
        var sites = Read("i = {\n\tflags = { relic_star }\n\tplanet = {\n\t\tflags = { minigalaxy_planet }\n\t}\n}", "common/solar_system_initializers/i.txt");
        Assert.Equal(
            new[]
            {
                new RawSite("relic_star", FlagKind.DefinitionFlag, null, FlagRole.Set, 2),
                new RawSite("minigalaxy_planet", FlagKind.DefinitionFlag, null, FlagRole.Set, 4),
            },
            sites);
    }

    [Fact]
    public void DefinitionFlagBlocks_OnlyInTheirFolders()
    {
        Assert.Empty(Read("e = { flags = { x } }"));
        Assert.Empty(Read("p = { flags = { x } }", "common/policies/p.txt"));
        Assert.Single(Read("c = { flags = { first_alien_life } }", "common/governments/civics/c.txt"));
    }

    [Fact]
    public void ComposedCommand()
    {
        Assert.Equal(
            new RawSite("affected_by_electric_storm", FlagKind.ScopeFlag, null, FlagRole.Set, 1),
            Assert.Single(Read("set_$SCOPE_TYPE$_flag = affected_by_electric_storm")));
        Assert.Equal(
            new RawSite("t", FlagKind.ScopeFlag, null, FlagRole.Set, 1),
            Assert.Single(Read("set_timed_$X$_flag = { flag = t days = 5 }")));
        Assert.Empty(Read("$KEY$ = yes\nhas_$TYPE$_assimilation = x"));
    }

    [Fact]
    public void InlineScriptFile_TopKeysNotDefinitions()
    {
        var scan = Reader.Read("common/inline_scripts/contracts/h.txt", "set_country_flag = harvest_$RESOURCE$_contract_active\nsome_key = { x = y }");

        Assert.Equal(new DefinitionInfo("inline_script:contracts/h", false, 1), Assert.Single(scan.Definitions));
        Assert.Equal("inline_script:contracts/h", Assert.Single(scan.Templates).DefinitionName);
    }

    [Fact]
    public void InlineScriptCalls()
    {
        const string text = "e = {\n\tinline_script = { script = contracts/h RESOURCE = minerals }\n\tinline_script = \"a/b\"\n\tinline_script = { script = contracts/$X$ Y = 1 }\n}";

        var calls = Reader.ReadCalls("events/e.txt", text, new HashSet<string> { "inline_script:contracts/h", "inline_script:a/b" });

        Assert.Equal(new[] { "inline_script:contracts/h", "inline_script:a/b", "inline_script:contracts/$X$" }, calls.Select(c => c.Callee));
        Assert.Equal("minerals", calls[0].Params!["RESOURCE"]);
        Assert.False(calls[0].Params!.ContainsKey("script"));
        Assert.Null(calls[1].Params);
        Assert.Equal(new[] { 2, 3, 4 }, calls.Select(c => c.Line));
        Assert.Empty(Reader.ReadCalls("events/e.txt", text, new HashSet<string>()).Where(c => !c.Callee.Contains('$')));
    }

    [Fact]
    public void InlineScriptCalls_InsideInlineFile_HaveCallerDefinition()
    {
        var call = Assert.Single(Reader.ReadCalls(
            "common/inline_scripts/self.txt", "inline_script = { script = self P = $P$ }", new HashSet<string> { "inline_script:self" }));

        Assert.Equal(("inline_script:self", "inline_script:self"), (call.Callee, call.CallerDefinition));
    }

    [Fact]
    public void ScriptValueParam_IsPattern()
    {
        var path = "common/script_values/v.txt";
        var text = "sv = { if = { limit = { is_variable_set = $VARIABLE$ } } }";
        var scan = Reader.Read(path, text);
        var file = new FlagScannedFile(new SiteLocation(@"C:\v", path, 0), FlagOrigin.Vanilla, "Vanilla", scan);

        var site = Assert.Single(ParamResolver.Resolve(new[] { file }, null));
        Assert.Equal(("*", FlagKind.Variable, FlagRole.Read, true), (site.Name, site.Kind, site.Role, site.IsPattern));
    }

    [Fact]
    public void LocBrackets()
    {
        var scan = DataFunctionReader.Read(
            "l_english:\n k:0 \"[root.payback_timer] [persistent] [event_target:merc.upgraded_station_timer] [Root.GetName] [Root.Owner.GetName] [event_target:cfl_winner_1.GetName] [This.Capital] [$t$]\"",
            true,
            LocStyle.StellarisBrackets);

        Assert.Equal(
            new[]
            {
                new RawSite("payback_timer", FlagKind.Variable, null, FlagRole.Read, 2, IsCandidate: true),
                new RawSite("persistent", FlagKind.Variable, null, FlagRole.Read, 2, IsCandidate: true),
                new RawSite("merc", FlagKind.EventTarget, null, FlagRole.Read, 2),
                new RawSite("upgraded_station_timer", FlagKind.Variable, null, FlagRole.Read, 2, IsCandidate: true),
                new RawSite("cfl_winner_1", FlagKind.EventTarget, null, FlagRole.Read, 2),
            },
            scan.Sites);
        Assert.Empty(DataFunctionReader.Read("text = \"[root.x]\"", false, LocStyle.StellarisBrackets).Sites);
        Assert.Empty(DataFunctionReader.Read("l_english:\n# k:0 \"[root.x]\"", true, LocStyle.StellarisBrackets).Sites);
    }

    [Fact]
    public void LocCandidates()
    {
        Write(_mod, "localisation/english/x_l_english.yml", "l_english:\n k:0 \"[root.payback_timer] [persistent]\"");
        Write(_mod, "events/e.txt", "set_variable = { which = payback_timer value = 1 }");

        var index = Builder().Build();

        var read = Assert.Single(index.Search("payback_timer").SitesByName["payback_timer"], s => s.Role == FlagRole.Read);
        Assert.Equal(new SiteLocation(_mod, "localisation/english/x_l_english.yml", 2), read.Location);
        Assert.Empty(index.Search("persistent").Names);
    }

    [Fact]
    public void InlineScriptCall()
    {
        Write(_vanilla, "common/inline_scripts/contracts/h.txt", "set_country_flag = harvest_$RESOURCE$_contract_active");
        Write(_mod, "events/e.txt", "e.1 = {\n\timmediate = {\n\t\tinline_script = { script = contracts/h RESOURCE = minerals }\n\t}\n}");

        var index = Builder().Build();

        var site = Assert.Single(index.Search("harvest_minerals_contract_active").SitesByName["harvest_minerals_contract_active"]);
        Assert.Equal(FlagRole.Set, site.Role);
        Assert.False(site.IsPattern);
        Assert.Equal(new SiteLocation(_mod, "events/e.txt", 3), site.Location);
        Assert.Equal(new SiteLocation(_vanilla, "common/inline_scripts/contracts/h.txt", 1), site.Via);
        Assert.Equal("inline_script:contracts/h", site.ViaName);
    }

    [Fact]
    public void InlineScriptCall_Nested_PassThrough()
    {
        Write(_vanilla, "common/inline_scripts/contracts/h.txt", "set_country_flag = harvest_$RESOURCE$_contract_active");
        Write(_vanilla, "common/inline_scripts/contracts/outer.txt", "inline_script = { script = contracts/h RESOURCE = $R$ }");
        Write(_vanilla, "common/scripted_effects/fx.txt", "fx = {\n\tinline_script = { script = contracts/outer R = $Q$ }\n}");
        Write(_mod, "events/e.txt", "e.1 = {\n\tinline_script = \"contracts/plain\"\n\tfx = { Q = energy }\n}");
        Write(_vanilla, "common/inline_scripts/contracts/plain.txt", "set_country_flag = plain_$P|def$");

        var index = Builder().Build();

        var site = Assert.Single(index.Search("harvest_energy_contract_active").SitesByName["harvest_energy_contract_active"]);
        Assert.Equal(new SiteLocation(_mod, "events/e.txt", 3), site.Location);
        var plain = Assert.Single(index.Search("plain_def").SitesByName["plain_def"]);
        Assert.Equal(new SiteLocation(_mod, "events/e.txt", 2), plain.Location);
        Assert.False(plain.IsPattern);
    }

    [Fact]
    public void ResolvedVariables_ScopeChainAndNumbers()
    {
        Write(_vanilla, "common/scripted_effects/fx.txt", "create_var = {\n\tset_variable = { which = $VARIABLE$ value = $VALUE$ }\n}");
        Write(_mod, "events/e.txt", "create_var = { VARIABLE = target_var VALUE = 0 }\ncreate_var = { VARIABLE = other VALUE = prev.source_var }");

        var index = Builder().Build();

        Assert.Empty(index.Search("0").Names);
        Assert.Single(index.Search("source_var").SitesByName["source_var"]);
        Assert.Empty(index.Search("prev.source_var").Names);
        Assert.Single(index.Search("target_var").SitesByName["target_var"]);
    }

    [Fact]
    public void InlineScriptDynamicPath()
    {
        Write(_vanilla, "common/inline_scripts/contracts/h.txt", "set_country_flag = harvest_$RESOURCE$_contract_active");
        Write(_vanilla, "common/inline_scripts/other/o.txt", "set_country_flag = other_$RESOURCE$");
        Write(_mod, "events/e.txt", "e.1 = {\n\tinline_script = { script = contracts/$X$ RESOURCE = minerals }\n}");

        var index = Builder().Build();

        var site = Assert.Single(index.Search("harvest_*").PatternSites);
        Assert.Equal(("harvest_*_contract_active", true), (site.Name, site.IsPattern));
        Assert.Equal(new SiteLocation(_mod, "events/e.txt", 2), site.Location);
        Assert.Equal("inline_script:contracts/h", site.ViaName);
        var other = Assert.Single(index.Search("other_*").PatternSites);
        Assert.Equal(new SiteLocation(_vanilla, "common/inline_scripts/other/o.txt", 1), other.Location);
    }

    [Fact]
    public void InlineScriptSelfRecursion()
    {
        Write(_vanilla, "common/inline_scripts/self.txt", "set_country_flag = x_$P$\ninline_script = { script = self P = $P$ }");
        Write(_mod, "events/e.txt", "inline_script = { script = self P = a }");

        var index = Builder().Build();

        Assert.Single(index.Search("x_a").SitesByName["x_a"]);
        var pattern = Assert.Single(index.Search("x_*").PatternSites);
        Assert.Equal(new SiteLocation(_vanilla, "common/inline_scripts/self.txt", 2), pattern.Location);
    }

    [Fact]
    public void InlineScript_Refresh_PicksUpNewTemplate()
    {
        var store = new FlagIndexStore(Path.Combine(_temp, "store"));
        Write(_vanilla, "common/inline_scripts/h.txt", "set_country_flag = plain");
        Write(_mod, "events/e.txt", "inline_script = { script = h R = a }");
        Builder().Refresh(store);

        var path = Write(_vanilla, "common/inline_scripts/h.txt", "set_country_flag = r_$R$");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        var index = Builder().Refresh(store);

        Assert.Single(index.Search("r_a").SitesByName["r_a"]);
    }

    [Fact]
    public void InlineScriptCall_PathIgnoresCase()
    {
        Write(_vanilla, "common/inline_scripts/contracts/HarvestResource.txt", "set_country_flag = harvest_$RESOURCE$_contract_active");
        Write(_vanilla, "common/inline_scripts/contracts/Outer.txt", "inline_script = { script = CONTRACTS/harvestresource RESOURCE = $R$ }");
        Write(_vanilla, "common/scripted_effects/fx.txt", "fx = {\n\tinline_script = { script = contracts/OUTER R = $Q$ }\n}");
        Write(_mod, "events/e.txt", "e.1 = {\n\tinline_script = { script = Contracts/harvestresource RESOURCE = minerals }\n\tfx = { Q = energy }\n\tinline_script = { script = CONTRACTS/$X$ RESOURCE = food }\n}");

        var index = Builder().Build();

        var site = Assert.Single(index.Search("harvest_minerals_contract_active").SitesByName["harvest_minerals_contract_active"]);
        Assert.Equal((new SiteLocation(_mod, "events/e.txt", 2), "inline_script:contracts/HarvestResource"), (site.Location, site.ViaName));
        var nested = Assert.Single(index.Search("harvest_energy_contract_active").SitesByName["harvest_energy_contract_active"]);
        Assert.Equal(new SiteLocation(_mod, "events/e.txt", 3), nested.Location);
        Assert.Contains(index.Search("harvest_*").PatternSites, p => p.Location == new SiteLocation(_mod, "events/e.txt", 4));
    }

    [Fact]
    public void InlineScriptCall_PathIgnoresCase_Refresh()
    {
        var store = new FlagIndexStore(Path.Combine(_temp, "store"));
        Write(_vanilla, "common/inline_scripts/contracts/HarvestResource.txt", "set_country_flag = harvest_$RESOURCE$_contract_active");
        Write(_mod, "events/e.txt", "inline_script = { script = contracts/HarvestResource RESOURCE = minerals }");
        Builder().Refresh(store);

        var path = Write(_mod, "events/e.txt", "inline_script = { script = contracts/harvestRESOURCE RESOURCE = food }");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        var index = Builder().Refresh(store);

        Assert.Single(index.Search("harvest_food_contract_active").SitesByName["harvest_food_contract_active"]);
    }

    [Fact]
    public void EffectiveDefinitions_InlineScriptKey()
    {
        Write(_vanilla, "common/inline_scripts/h.txt", "set_country_flag = v_$R$");
        Write(_mod, "common/inline_scripts/h.txt", "set_country_flag = m_$R$");
        Write(_mod, "events/e.txt", "inline_script = { script = h R = a }");

        var index = Builder().Build();

        Assert.Single(index.Search("m_a").SitesByName["m_a"]);
        Assert.Empty(index.Search("v_a").Names);
    }

    private FlagIndexBuilder Builder() =>
        new(FlagSourceSet.ForPlayset(RepoFiles.Profile("stellaris"), new[] { new ModSource("My Mod", _mod, false, 0) }, _mod, new[] { _vanilla }), Table);

    private static string Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }
}
