using ParadoxModTool.FlagTracker;
using Xunit;

namespace ParadoxModTool.FlagTracker.Tests;

public class ParamResolverTests
{
    private const string VanillaRoot = @"C:\vanilla";
    private const string ModRoot = @"C:\mod";
    private const string EffectPath = "in_game/common/scripted_effects/eff.txt";
    private const string TriggerPath = "in_game/common/scripted_triggers/trg.txt";
    private const string CallerPath = "in_game/events/e.txt";

    private const string UnlockEffect = "unlock_policy_effect = {\n\tset_variable = { name = unlocked_policy_$type$ }\n}";

    private static readonly ScriptReader Reader = new(FlagCommandTable.Eu5);

    /// <summary>Liest alle Dateien wie der Builder: erst Read, dann ReadCalls mit allen Definitionsnamen.</summary>
    private static List<FlagScannedFile> Scan(params (string Root, string Path, FlagOrigin Origin, string Text)[] inputs) => ScanWith(Reader, inputs);

    private static List<FlagScannedFile> ScanWith(ScriptReader reader, params (string Root, string Path, FlagOrigin Origin, string Text)[] inputs)
    {
        var scans = inputs.Select(i => reader.Read(i.Path, i.Text)).ToList();
        var names = scans.SelectMany(s => s.Definitions).Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        var files = new List<FlagScannedFile>();
        for (var i = 0; i < inputs.Length; i++)
        {
            scans[i].Calls.AddRange(reader.ReadCalls(inputs[i].Path, inputs[i].Text, names));
            var originName = inputs[i].Origin == FlagOrigin.Vanilla ? "Vanilla" : "My Mod";
            files.Add(new FlagScannedFile(new SiteLocation(inputs[i].Root, inputs[i].Path, 0), inputs[i].Origin, originName, scans[i]));
        }

        return files;
    }

    private static List<FlagScannedFile> EffectAndCaller(string effect, string caller) => Scan(
        (VanillaRoot, EffectPath, FlagOrigin.Vanilla, effect),
        (ModRoot, CallerPath, FlagOrigin.ActiveMod, caller));

    private static IReadOnlyList<FlagSite> Resolve(List<FlagScannedFile> files) => ParamResolver.Resolve(files, null);

    private static DefinitionKey Effect(string name) => new(name, false);

    [Fact]
    public void EffectAndTriggerWithSameName_BothResolved()
    {
        var files = Scan(
            (VanillaRoot, EffectPath, FlagOrigin.Vanilla, "cd = {\n\tset_variable = x_$k$\n}"),
            (VanillaRoot, TriggerPath, FlagOrigin.Vanilla, "cd = {\n\thas_variable = x_$k$\n}"),
            (ModRoot, CallerPath, FlagOrigin.ActiveMod, "cd = { k = foo }"));
        var effective = new Dictionary<DefinitionKey, FlagScannedFile> { [Effect("cd")] = files[0], [new DefinitionKey("cd", true)] = files[1] };

        var sites = ParamResolver.Resolve(files, effective);

        Assert.Equal(new[] { FlagRole.Set, FlagRole.Read }, sites.Where(s => s.Name == "x_foo").Select(s => s.Role).OrderBy(r => r));
    }

    [Fact]
    public void ResolvesSimpleCall()
    {
        var files = EffectAndCaller(UnlockEffect, "x.1 = {\n\timmediate = {\n\t\tunlock_policy_effect = { type = protestant_tolerance }\n\t}\n}");
        var site = Assert.Single(ParamResolver.Resolve(files, new Dictionary<DefinitionKey, FlagScannedFile> { [Effect("unlock_policy_effect")] = files[0] }));
        Assert.Equal(
            new FlagSite(
                "unlocked_policy_protestant_tolerance",
                FlagKind.Variable,
                null,
                FlagRole.Set,
                new SiteLocation(ModRoot, CallerPath, 3),
                FlagOrigin.ActiveMod,
                "My Mod",
                new SiteLocation(VanillaRoot, EffectPath, 2),
                "unlock_policy_effect",
                false),
            site);
    }

    [Fact]
    public void PureParamName()
    {
        var files = EffectAndCaller(
            "increment_variable_effect = {\n\tchange_variable = { name = $VAR$ add = $VAL$ }\n}",
            "a = { increment_variable_effect = { VAR = num_feasts VAL = 1 } }");
        var site = Assert.Single(Resolve(files));
        Assert.Equal("num_feasts", site.Name);
        Assert.False(site.IsPattern);
    }

    [Fact]
    public void TriggerReadResolved()
    {
        var files = Scan(
            (VanillaRoot, TriggerPath, FlagOrigin.Vanilla, "policy_unlocked_trigger = {\n\thas_variable = unlocked_policy_$type$\n}"),
            (ModRoot, CallerPath, FlagOrigin.ActiveMod, "a = { trigger = { policy_unlocked_trigger = { type = abc } } }"));
        var site = Assert.Single(Resolve(files));
        Assert.Equal("unlocked_policy_abc", site.Name);
        Assert.Equal(FlagRole.Read, site.Role);
        Assert.Equal(new SiteLocation(VanillaRoot, TriggerPath, 2), site.Via);
        Assert.Equal("policy_unlocked_trigger", site.ViaName);
    }

    [Fact]
    public void PassThrough()
    {
        var files = EffectAndCaller(
            "b_effect = {\n\tset_variable = x_$P$\n}\na_effect = {\n\tb_effect = { P = $Q$ }\n}",
            "e = {\n\ta_effect = { Q = foo }\n}");
        var site = Assert.Single(Resolve(files));
        Assert.Equal("x_foo", site.Name);
        Assert.False(site.IsPattern);
        Assert.Equal(new SiteLocation(ModRoot, CallerPath, 2), site.Location);
        Assert.Equal(FlagOrigin.ActiveMod, site.Origin);
        Assert.Equal(new SiteLocation(VanillaRoot, EffectPath, 2), site.Via);
        Assert.Equal("b_effect", site.ViaName);
    }

    [Fact]
    public void PassThrough_WithoutOuterCall_PatternAtInnerCall()
    {
        var files = EffectAndCaller("b_effect = {\n\tset_variable = x_$P$\n}\na_effect = {\n\tb_effect = { P = $Q$ }\n}", "e = 1");
        var site = Assert.Single(Resolve(files));
        Assert.Equal("x_*", site.Name);
        Assert.True(site.IsPattern);
        Assert.Equal(new SiteLocation(VanillaRoot, EffectPath, 5), site.Location);
    }

    [Fact]
    public void PassThrough_SeveralParamsStayTogetherPerOuterCall()
    {
        var files = EffectAndCaller(
            "b_effect = {\n\tset_variable = $N$_$M$\n}\na_effect = {\n\tb_effect = { N = $X$ M = $Y$ }\n}",
            "e = {\n\ta_effect = { X = a Y = b }\n\ta_effect = { X = c Y = d }\n}");
        Assert.Equal(new[] { "a_b", "c_d" }, Resolve(files).Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void ScopeAndBlockValues_BecomePattern()
    {
        var files = EffectAndCaller(UnlockEffect, "unlock_policy_effect = { type = scope:t }\nunlock_policy_effect = { type = { a = b } }");
        var sites = Resolve(files);
        Assert.Equal(2, sites.Count);
        Assert.All(sites, s =>
        {
            Assert.Equal("unlocked_policy_*", s.Name);
            Assert.True(s.IsPattern);
        });
        Assert.Equal(new[] { 1, 2 }, sites.Select(s => s.Location.Line).OrderBy(l => l));
    }

    [Fact]
    public void MissingParam_BecomesPattern()
    {
        var files = EffectAndCaller(UnlockEffect, "unlock_policy_effect = { other = x }\nunlock_policy_effect = yes");
        var sites = Resolve(files);
        Assert.Equal(2, sites.Count);
        Assert.All(sites, s =>
        {
            Assert.Equal("unlocked_policy_*", s.Name);
            Assert.True(s.IsPattern);
        });
    }

    [Fact]
    public void ParamNames_AreCaseSensitive()
    {
        var site = Assert.Single(Resolve(EffectAndCaller(UnlockEffect, "unlock_policy_effect = { Type = a }")));
        Assert.Equal("unlocked_policy_*", site.Name);
        Assert.True(site.IsPattern);
    }

    [Fact]
    public void ScalarValues_YesAndNumbers_Substitute()
    {
        var files = EffectAndCaller(UnlockEffect, "unlock_policy_effect = { type = yes }\nunlock_policy_effect = { type = 5 }");
        Assert.Equal(new[] { "unlocked_policy_5", "unlocked_policy_yes" }, Resolve(files).Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void SeveralParamsAndRepeatedParam()
    {
        var files = EffectAndCaller("eff = {\n\tset_variable = x_$N$_$M$_$N$\n}", "eff = { N = a M = b }");
        Assert.Equal("x_a_b_a", Assert.Single(Resolve(files)).Name);
    }

    [Fact]
    public void UncalledDefinition_Pattern()
    {
        var files = EffectAndCaller("\n" + UnlockEffect, "e = 1");
        var site = Assert.Single(Resolve(files));
        Assert.Equal("unlocked_policy_*", site.Name);
        Assert.True(site.IsPattern);
        Assert.Equal(new SiteLocation(VanillaRoot, EffectPath, 2), site.Location);
        Assert.Equal(FlagOrigin.Vanilla, site.Origin);
        Assert.Equal(new SiteLocation(VanillaRoot, EffectPath, 3), site.Via);
        Assert.Equal("unlock_policy_effect", site.ViaName);
    }

    [Fact]
    public void TemplateOutsideDefinition_PatternAtOwnLine()
    {
        var files = Scan((ModRoot, CallerPath, FlagOrigin.ActiveMod, "e = {\n\tset_variable = a_$X$\n}"));
        var site = Assert.Single(Resolve(files));
        Assert.Equal(new FlagSite("a_*", FlagKind.Variable, null, FlagRole.Set, new SiteLocation(ModRoot, CallerPath, 2), FlagOrigin.ActiveMod, "My Mod", IsPattern: true), site);
    }

    [Fact]
    public void RecursionStopsAtDepth()
    {
        var files = EffectAndCaller("a_effect = {\n\tset_variable = x_$P$\n\ta_effect = { P = $P$ }\n}", "e = 1");
        var sites = Resolve(files);
        Assert.NotEmpty(sites);
        Assert.All(sites, s => Assert.True(s.IsPattern));
    }

    [Fact]
    public void Recursion_WithOuterCall_ResolvesOuterValue()
    {
        var files = EffectAndCaller("a_effect = {\n\tset_variable = x_$P$\n\ta_effect = { P = $P$ }\n}", "a_effect = { P = foo }");
        var names = Resolve(files).Select(s => s.Name).ToList();
        Assert.Contains("x_foo", names);
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    public void PassThroughDepthLimit(int chainLength, bool expectPattern)
    {
        // e1 setzt x_$P$, e2 ruft e1 { P = $P$ } … ; die Mod ruft e<n> { P = foo }: n - 1 Durchreich-Schritte.
        var effect = "e1 = {\n\tset_variable = x_$P$\n}\n" + string.Concat(
            Enumerable.Range(2, chainLength - 1).Select(i => $"e{i} = {{\n\te{i - 1} = {{ P = $P$ }}\n}}\n"));
        var site = Assert.Single(Resolve(EffectAndCaller(effect, $"e{chainLength} = {{ P = foo }}")));
        Assert.Equal(expectPattern ? "x_*" : "x_foo", site.Name);
        Assert.Equal(expectPattern, site.IsPattern);
    }

    [Fact]
    public void OnlyEffectiveDefinitionExpands()
    {
        var files = Scan(
            (VanillaRoot, EffectPath, FlagOrigin.Vanilla, "unlock_policy_effect = {\n\tset_variable = old_$type$\n}"),
            (ModRoot, "in_game/common/scripted_effects/mod.txt", FlagOrigin.ActiveMod, "unlock_policy_effect = {\n\tset_variable = new_$type$\n}"),
            (ModRoot, CallerPath, FlagOrigin.ActiveMod, "unlock_policy_effect = { type = x }"));
        var sites = ParamResolver.Resolve(files, new Dictionary<DefinitionKey, FlagScannedFile> { [Effect("unlock_policy_effect")] = files[1] });
        Assert.Equal("new_x", Assert.Single(sites).Name);
    }

    [Fact]
    public void NullEffective_EveryDefinitionCounts()
    {
        var files = Scan(
            (VanillaRoot, EffectPath, FlagOrigin.Vanilla, "unlock_policy_effect = {\n\tset_variable = old_$type$\n}"),
            (ModRoot, "in_game/common/scripted_effects/mod.txt", FlagOrigin.ActiveMod, "unlock_policy_effect = {\n\tset_variable = new_$type$\n}"),
            (ModRoot, CallerPath, FlagOrigin.ActiveMod, "unlock_policy_effect = { type = x }"));
        Assert.Equal(new[] { "new_x", "old_x" }, Resolve(files).Select(s => s.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void DuplicateResultsRemoved()
    {
        var files = EffectAndCaller(
            "eff = {\n\tset_variable = x_$P$\n\tset_variable = x_$P$\n}",
            "eff = { P = a } eff = { P = a }");
        Assert.Equal(2, Resolve(files).Count);
    }

    [Fact]
    public void ReadCalls_MultiLineCall()
    {
        var calls = Reader.ReadCalls(CallerPath, "e = {\n\tunlock_policy_effect = {\n\t\ttype = a\n\t}\n}", new HashSet<string> { "unlock_policy_effect" });
        var call = Assert.Single(calls);
        Assert.Equal("unlock_policy_effect", call.Callee);
        Assert.Equal(2, call.Line);
        Assert.Equal("a", call.Params!["type"]);
        Assert.Null(call.CallerDefinition);
    }

    [Fact]
    public void ReadCalls_TopLevelDefinitionIsNoCall()
    {
        var calls = Reader.ReadCalls(EffectPath, "eff = {\n\teff = { P = $P$ }\n}", new HashSet<string> { "eff" });
        var call = Assert.Single(calls);
        Assert.Equal(2, call.Line);
        Assert.Equal("eff", call.CallerDefinition);
        Assert.Equal("$P$", call.Params!["P"]);
    }

    [Fact]
    public void ReadCalls_BareAndBlockParams()
    {
        var calls = Reader.ReadCalls(
            CallerPath,
            "e = {\n\teff = yes\n\teff = { A = 1 B = { eff = yes } }\n}",
            new HashSet<string> { "eff" });
        Assert.Equal(2, calls.Count);
        Assert.Null(calls[0].Params);
        Assert.Equal("1", calls[1].Params!["A"]);
        Assert.True(calls[1].Params!.ContainsKey("B"));
        Assert.Null(calls[1].Params!["B"]);
    }

    [Fact]
    public void ConstantValue_BecomesPattern()
    {
        var site = Assert.Single(Resolve(EffectAndCaller(UnlockEffect, "unlock_policy_effect = { type = @my_const }")));
        Assert.Equal("unlocked_policy_*", site.Name);
        Assert.True(site.IsPattern);
    }

    [Fact]
    public void LeftoverDollar_BecomesPattern()
    {
        var site = Assert.Single(Resolve(EffectAndCaller(UnlockEffect, "unlock_policy_effect = { type = $broken }")));
        Assert.Equal("unlocked_policy_*", site.Name);
        Assert.True(site.IsPattern);
    }

    [Fact]
    public void ValueWithEmbeddedDollar_BecomesPatternAtSubstitution()
    {
        var site = Assert.Single(Resolve(EffectAndCaller(UnlockEffect, "unlock_policy_effect = { type = a$B$c }")));
        Assert.Equal("unlocked_policy_*", site.Name);
        Assert.True(site.IsPattern);
    }

    [Fact]
    public void CallInsideOverriddenDefinition_DoesNotFanOut()
    {
        // Vanilla: outer_eff reicht $Q$ an inner_eff durch; die Mod überschreibt outer_eff ohne diesen Aufruf.
        var files = Scan(
            (VanillaRoot, EffectPath, FlagOrigin.Vanilla,
                "inner_eff = {\n\tset_variable = x_$P$\n}\nouter_eff = {\n\tinner_eff = { P = $Q$ }\n}"),
            (ModRoot, "in_game/common/scripted_effects/mod.txt", FlagOrigin.ActiveMod, "outer_eff = {\n\tset_variable = unrelated\n}"),
            (ModRoot, CallerPath, FlagOrigin.ActiveMod, "outer_eff = { Q = foo }"));
        var effective = new Dictionary<DefinitionKey, FlagScannedFile> { [Effect("inner_eff")] = files[0], [Effect("outer_eff")] = files[1] };

        var names = ParamResolver.Resolve(files, effective).Select(s => s.Name).ToList();

        Assert.Equal(new[] { "x_*" }, names);
        Assert.Contains("x_foo", Resolve(files).Select(s => s.Name));
    }

    private static readonly ScriptReader Eu4Reader = new(FlagCommandTable.Eu4);

    private const string Eu4MagicEffect = "e = {\n\tset_country_flag = magic_list_$group$[[subgroup]_$subgroup$]\n}";

    [Fact]
    public void ConditionalInName_TwoVariants()
    {
        var files = ScanWith(
            Eu4Reader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, Eu4MagicEffect),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "e = { group = g subgroup = s }\ne = { group = g }"));

        var sites = Resolve(files);

        Assert.Equal(new[] { ("magic_list_g_s", 1), ("magic_list_g", 2) }, sites.Select(s => (s.Name, s.Location.Line)));
        Assert.All(sites, s => Assert.False(s.IsPattern));
    }

    [Fact]
    public void ConditionalNegated_KeptWhenNotPassed()
    {
        var files = ScanWith(
            Eu4Reader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, "e = {\n\tset_country_flag = f_$a$[[!b]_nob]\n}"),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "e = { a = x b = y }\ne = { a = x }"));

        Assert.Equal(new[] { "f_x", "f_x_nob" }, Resolve(files).Select(s => s.Name));
    }

    [Fact]
    public void ConditionalInName_Uncalled_BothVariantsArePatterns()
    {
        var files = ScanWith(Eu4Reader, (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, Eu4MagicEffect));

        var sites = Resolve(files);

        Assert.Equal(new[] { "magic_list_*_*", "magic_list_*" }, sites.Select(s => s.Name));
        Assert.All(sites, s => Assert.True(s.IsPattern));
    }

    [Fact]
    public void ConditionalInName_OutsideDefinition_BothVariantsArePatterns()
    {
        var files = ScanWith(Eu4Reader, (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "set_country_flag = lit[[p]_x]"));

        var sites = Resolve(files);

        Assert.Equal(new[] { "lit_x", "lit" }, sites.Select(s => s.Name));
        Assert.All(sites, s => Assert.True(s.IsPattern));
    }

    private static readonly ScriptReader StellarisReader = new(FlagCommandTable.Stellaris(Array.Empty<string>()));

    private const string CrisisEffect = "e = {\n\tset_global_flag = crisis_stage_$STAGE|1$\n}";

    [Fact]
    public void ParamDefault_CallValueOrDefault()
    {
        var files = ScanWith(
            StellarisReader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, CrisisEffect),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "e = { STAGE = 3 }\ne = { OTHER = x }\ne = yes"));

        var sites = Resolve(files);

        Assert.Equal(new[] { ("crisis_stage_3", 1), ("crisis_stage_1", 2), ("crisis_stage_1", 3) }, sites.Select(s => (s.Name, s.Location.Line)));
        Assert.All(sites, s => Assert.False(s.IsPattern));
    }

    [Fact]
    public void ParamDefault_NoCall_IsConcrete()
    {
        var files = ScanWith(StellarisReader, (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, CrisisEffect));

        var site = Assert.Single(Resolve(files));
        Assert.Equal(("crisis_stage_1", 1, false), (site.Name, site.Location.Line, site.IsPattern));
        Assert.Equal(2, site.Via!.Line);
    }

    [Fact]
    public void ParamDefault_MixedWithPlainParam_StaysPattern()
    {
        var files = ScanWith(StellarisReader, (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, "e = {\n\tset_global_flag = a_$X|1$_$Y$\n}"));

        var site = Assert.Single(Resolve(files));
        Assert.Equal(("a_1_*", true), (site.Name, site.IsPattern));
    }

    [Fact]
    public void ParamDefault_PassThroughWithDefault()
    {
        var files = ScanWith(
            StellarisReader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, CrisisEffect + "\nouter = {\n\te = { STAGE = $S|2$ }\n}"),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "outer = { S = 4 }\nouter = { }"));

        Assert.Equal(new[] { "crisis_stage_4", "crisis_stage_2" }, Resolve(files).Select(s => s.Name));
    }

    [Fact]
    public void ParamDefault_PassThroughDefault_OuterUncalled_IsConcrete()
    {
        var files = ScanWith(
            StellarisReader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, CrisisEffect + "\nouter = {\n\te = { STAGE = $S|2$ }\n}"));

        var site = Assert.Single(Resolve(files));
        Assert.Equal(("crisis_stage_2", 5, false), (site.Name, site.Location.Line, site.IsPattern));
    }

    [Fact]
    public void ConditionalSegments_MultipleInOneName()
    {
        var files = ScanWith(
            Eu4Reader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, "e = {\n\tset_country_flag = a[[p]_b][[q]_c]\n}"),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "e = { p = yes }\ne = { p = yes q = yes }\ne = { q = yes }"));

        Assert.Equal(new[] { "a_b", "a_b_c", "a_c" }, Resolve(files).Select(s => s.Name));
    }

    [Fact]
    public void ConditionalSegments_Nested()
    {
        var files = ScanWith(
            Eu4Reader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, "e = {\n\tset_country_flag = a[[p]_b[[q]_c]]\n}"),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "e = { p = yes }\ne = { p = yes q = yes }\ne = { q = yes }"));

        Assert.Equal(new[] { "a_b", "a_b_c", "a" }, Resolve(files).Select(s => s.Name));
    }

    [Fact]
    public void ConditionalSegment_PassThroughCall()
    {
        var files = ScanWith(
            Eu4Reader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla,
                "inner = {\n\tset_country_flag = f[[p]_$p$]\n}\nouter = {\n\tinner = { p = $q$ }\n}"),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "outer = { q = z }"));

        var site = Assert.Single(Resolve(files));
        Assert.Equal(("f_z", 1, false), (site.Name, site.Location.Line, site.IsPattern));
    }

    [Fact]
    public void ConditionalInCallValue_ContinuesOutward()
    {
        var files = ScanWith(
            Eu4Reader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla,
                "inner = {\n\tset_country_flag = f_$a$\n}\nouter = {\n\tinner = { a = x[[p]_y] }\n}"),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "outer = { p = yes }\nouter = { }"));

        Assert.Equal(new[] { ("f_x_y", false), ("f_x", false) }, Resolve(files).Select(s => (s.Name, s.IsPattern)));
    }

    [Fact]
    public void ConditionalInCallValue_NoOuterCaller_BothVariantsArePatterns()
    {
        var files = ScanWith(
            Eu4Reader,
            (VanillaRoot, "common/scripted_effects/e.txt", FlagOrigin.Vanilla, "inner = {\n\tset_country_flag = f_$a$\n}"),
            (ModRoot, "events/a.txt", FlagOrigin.ActiveMod, "inner = { a = x[[p]_y] }"));

        Assert.Equal(new[] { ("f_x_y", true), ("f_x", true) }, Resolve(files).Select(s => (s.Name, s.IsPattern)));
    }
}
