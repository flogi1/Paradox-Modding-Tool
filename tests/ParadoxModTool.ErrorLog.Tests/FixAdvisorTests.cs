using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class FixAdvisorTests
{
    private static readonly DateTime RunTime = new(2026, 9, 22, 19, 49, 0, DateTimeKind.Utc);

    private sealed class Setup : IDisposable
    {
        public Setup()
        {
            Tree = new TempTree(
                ("own/in_game/common/scripted_effects/purger.txt",
                    "purger = {\n\tcmf_suppress = { v = iv2_x }\n\tset_variable = { name = iv2_x }\n\tset_variable = { name = iv2_y }\n}\nmy_effect = { }\n"),
                ("own/in_game/localization/english/own_l_english.yml", "l_english:\n iv2_y_text:0 \"[GetVariable('iv2_y')]\"\n"),
                ("fw/in_game/common/scripted_effects/cmf_utility_effects.txt", "cmf_suppress = { }\n"),
                ("dep/in_game/common/scripted_effects/dep.txt", "dep_effect = { }\n"));
            Own = new ModSource("Own", Tree.PathOf("own"), true, 1);
            Dependency = new ModSource("Dep", Tree.PathOf("dep"), false, 0);
            Framework = new ModSource("Community Mod Framework", Tree.PathOf("fw"), false, -1);
            Index = SymbolIndex.Build(new[]
            {
                new SymbolRoot(Own.Directory, Own, true),
                new SymbolRoot(Dependency.Directory, Dependency, true),
                new SymbolRoot(Framework.Directory, Framework, false),
            });
            Advisor = new FixAdvisor(Index, name => name switch
            {
                "add_gold" => EngineCommandKind.Effect,
                "has_gold" => EngineCommandKind.Trigger,
                _ => null,
            });
            Resolver = new SourceResolver(new[] { Own, Dependency, Framework }, Array.Empty<string>(), RunTime, lastWriteUtc: _ => RunTime);
        }

        public TempTree Tree { get; }

        public ModSource Own { get; }

        public ModSource Dependency { get; }

        public ModSource Framework { get; }

        public SymbolIndex Index { get; }

        public FixAdvisor Advisor { get; }

        public SourceResolver Resolver { get; }

        public ErrorAttribution Attribute(ErrorEntry entry) => ErrorAttributor.Attribute(entry, Resolver, Index);

        public void Dispose() => Tree.Dispose();
    }

    [Fact]
    public void DefinitionInInstalledModOutsidePlaysetSuggestsAddingTheMod()
    {
        using var setup = new Setup();
        var entry = Catalogs.Classify("[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:2")[0];

        var hints = setup.Advisor.HintsFor(entry);
        var fixes = setup.Advisor.FixesFor(entry, hints, setup.Attribute(entry));

        var hint = Assert.Single(hints);
        Assert.Equal(HintKind.DefinedOutsidePlayset, hint.Kind);
        Assert.Same(setup.Framework, hint.Mod);
        Assert.Contains("addModToPlayset", fixes);
        Assert.Contains("jump", fixes);
        Assert.Contains("suppress", fixes);
        Assert.DoesNotContain("findDefinition", fixes);
    }

    [Fact]
    public void EngineCommandAndDefinitionInPlaysetAreRecognised()
    {
        using var setup = new Setup();

        var engine = Catalogs.Classify("[01:00:00][e.cpp:1]: Unknown effect add_gold at common/scripted_effects/purger.txt:1")[0];
        Assert.Equal(HintKind.EngineCommand, setup.Advisor.HintsFor(engine)[0].Kind);

        var trigger = Catalogs.Classify("[01:00:00][e.cpp:1]: Unknown effect has_gold at common/scripted_effects/purger.txt:1")[0];
        var wrong = setup.Advisor.HintsFor(trigger)[0];
        Assert.Equal(HintKind.WrongCommandKind, wrong.Kind);
        Assert.Equal(EngineCommandKind.Trigger, wrong.CommandKind);

        var inPlayset = Catalogs.Classify("[01:00:00][e.cpp:1]: Unknown effect dep_effect at common/scripted_effects/purger.txt:1")[0];
        var hint = setup.Advisor.HintsFor(inPlayset)[0];
        Assert.Equal(HintKind.DefinedInPlayset, hint.Kind);
        Assert.Same(setup.Dependency, hint.Mod);
    }

    [Fact]
    public void UndefinedNameOffersPossibleMatchesAndSimilarNames()
    {
        using var setup = new Setup();
        var entry = Catalogs.Classify(
            "[01:00:00][e.cpp:1]: Unknown effect my_efect at common/scripted_effects/purger.txt:1",
            "[01:00:00][e.cpp:1207]:  Possible match: 'my_effect_from_game'")[0];

        var hints = setup.Advisor.HintsFor(entry);
        var fixes = setup.Advisor.FixesFor(entry, hints, setup.Attribute(entry));

        var hint = Assert.Single(hints);
        Assert.Equal(HintKind.NotDefined, hint.Kind);
        Assert.Equal(new[] { "my_effect_from_game", "my_effect" }, hint.Candidates);
        Assert.Contains("replaceWithCandidate", fixes);
        Assert.DoesNotContain("addModToPlayset", fixes);
    }

    [Fact]
    public void VariableUsagesAndLocalizationCheck()
    {
        using var setup = new Setup();
        var entry = Catalogs.Classify("[01:00:00][e.cpp:1162]: Variable 'iv2_y' is set but is never used. Note that use in localization doesn't count")[0];

        var hints = setup.Advisor.HintsFor(entry);
        var fixes = setup.Advisor.FixesFor(entry, hints, setup.Attribute(entry));

        var usages = hints.Single(h => h.Kind == HintKind.Usages);
        Assert.Contains(usages.Occurrences, o => o.Role == SymbolRole.Set);
        var localization = hints.Single(h => h.Kind == HintKind.UsedInLocalization);
        Assert.EndsWith("own_l_english.yml", localization.Occurrences[0].FilePath);
        Assert.Contains("renameEverywhere", fixes);
        Assert.DoesNotContain("jump", fixes);
    }

    [Fact]
    public void LocCheckReportsGuiUsageSeparately()
    {
        using var setup = new Setup();
        Directory.CreateDirectory(setup.Tree.PathOf("own/in_game/gui"));
        File.WriteAllText(setup.Tree.PathOf("own/in_game/gui/own.gui"), "text = \"[GetVariable('iv2_y')]\"\n");
        var index = SymbolIndex.Build(new[] { new SymbolRoot(setup.Own.Directory, setup.Own, true) });
        var advisor = new FixAdvisor(index, _ => null);
        var entry = Catalogs.Classify("[01:00:00][e.cpp:1162]: Variable 'iv2_y' is set but is never used.")[0];

        var hints = advisor.HintsFor(entry);

        Assert.EndsWith("own_l_english.yml", hints.Single(h => h.Kind == HintKind.UsedInLocalization).Occurrences[0].FilePath);
        Assert.EndsWith("own.gui", hints.Single(h => h.Kind == HintKind.UsedInGui).Occurrences[0].FilePath);
    }

    private static ErrorEntry NeverSet(string name) =>
        Catalogs.Classify($"[01:00:00][e.cpp:1160]: Variable '{name}' is used but is never set. Setting it in an unused scripted trigger or effect does not count.")[0];

    [Fact]
    public void NeverSetButPresentInTheSaveIsProbablyHarmless()
    {
        using var setup = new Setup();
        var save = Saves.Load($"countries={{ database={{ 1={{ {Saves.Var("iv2_y")} }} 2={{ {Saves.Var("iv2_y")} }} }} }}\n");

        var hint = setup.Advisor.HintsFor(NeverSet("iv2_y"), save).Single(h => h.Kind == HintKind.SaveSetAtRuntime);

        Assert.Equal(2, hint.Save!.ObjectCount);
    }

    [Fact]
    public void NeverSetWithASimilarNameInTheSaveSuggestsATypo()
    {
        using var setup = new Setup();
        var save = Saves.Load($"countries={{ database={{ 1={{ {Saves.Var("iv2_counter")} }} }} }}\n");
        var entry = NeverSet("iv2_countre");

        var hint = setup.Advisor.HintsFor(entry, save).Single(h => h.Kind == HintKind.SaveLikelyTypo);

        Assert.Equal(new[] { "iv2_counter" }, hint.Candidates);
        Assert.Equal("iv2_counter", setup.Advisor.CandidatesFor(entry, save)[0]);
        var own = new ErrorAttribution(ErrorBucket.OwnMod, setup.Own, null);
        Assert.Contains("replaceWithCandidate", setup.Advisor.FixesFor(entry, setup.Advisor.HintsFor(entry, save), own));
    }

    [Fact]
    public void NeverSetAndMissingEverywhereOnlyExplains()
    {
        using var setup = new Setup();
        var save = Saves.Load($"countries={{ database={{ 1={{ {Saves.Var("totally_different")} }} }} }}\n");
        var entry = NeverSet("iv2_q");

        var hints = setup.Advisor.HintsFor(entry, save);

        Assert.Contains(hints, h => h.Kind == HintKind.SaveNowhere);
        Assert.DoesNotContain("totally_different", setup.Advisor.CandidatesFor(entry, save));
    }

    [Fact]
    public void SetButNeverUsedGetsNoExtraSaveHint()
    {
        using var setup = new Setup();
        var save = Saves.Load($"countries={{ database={{ 1={{ {Saves.Var("iv2_y")} }} }} }}\n");
        var entry = Catalogs.Classify("[01:00:00][e.cpp:1162]: Variable 'iv2_y' is set but is never used.")[0];

        Assert.DoesNotContain(setup.Advisor.HintsFor(entry, save), h => h.Kind is HintKind.SaveSetAtRuntime or HintKind.SaveLikelyTypo or HintKind.SaveNowhere);
        Assert.Equal(1, setup.Advisor.EvidenceFor(entry, save)!.ObjectCount);
    }

    [Fact]
    public void WithoutASaveNothingChanges()
    {
        using var setup = new Setup();
        var entry = NeverSet("iv2_y");

        Assert.Equal(setup.Advisor.HintsFor(entry).Select(h => h.Kind), setup.Advisor.HintsFor(entry, null).Select(h => h.Kind));
        Assert.DoesNotContain(setup.Advisor.HintsFor(entry), h => h.Kind is HintKind.SaveSetAtRuntime or HintKind.SaveLikelyTypo or HintKind.SaveNowhere);
        Assert.Equal(setup.Advisor.CandidatesFor(entry), setup.Advisor.CandidatesFor(entry, null));
        Assert.Null(setup.Advisor.EvidenceFor(entry, null));
    }

    [Fact]
    public void CommentOutSetIsOfferedOnlyForOwnModsWithoutLocalizationOrGuiUse()
    {
        using var setup = new Setup();
        ErrorEntry SetNeverUsed(string name) => Catalogs.Classify($"[01:00:00][e.cpp:1162]: Variable '{name}' is set but is never used.")[0];
        var own = new ErrorAttribution(ErrorBucket.OwnMod, setup.Own, null);

        var plain = SetNeverUsed("iv2_x");
        Assert.Contains("commentOutSet", setup.Advisor.FixesFor(plain, setup.Advisor.HintsFor(plain), own));

        var localized = SetNeverUsed("iv2_y");
        Assert.DoesNotContain("commentOutSet", setup.Advisor.FixesFor(localized, setup.Advisor.HintsFor(localized), own));

        var gui = new ErrorHint(HintKind.UsedInGui, "iv2_x", Array.Empty<SymbolOccurrence>(), Array.Empty<string>());
        Assert.DoesNotContain("commentOutSet", setup.Advisor.FixesFor(plain, new[] { gui }, own));

        Assert.DoesNotContain("commentOutSet", setup.Advisor.FixesFor(plain, setup.Advisor.HintsFor(plain), new ErrorAttribution(ErrorBucket.OtherMod, setup.Dependency, null)));

        var neverSet = Catalogs.Classify("[01:00:00][e.cpp:1160]: Variable 'iv2_x' is used but is never set.")[0];
        Assert.DoesNotContain("commentOutSet", setup.Advisor.FixesFor(neverSet, setup.Advisor.HintsFor(neverSet), own));
    }

    [Fact]
    public void TraceScopeIsOfferedForOwnRuntimeErrorsWithALocation()
    {
        using var setup = new Setup();
        var entry = Catalogs.Classify(
            "[15:19:50][jomini_script_system.cpp:256]: Script system error!",
            "  Error: Undefined event target 'x'",
            "  Script location: common/scripted_effects/purger.txt:3")[0];
        var path = setup.Tree.PathOf("own/in_game/common/scripted_effects/purger.txt");

        var own = new ErrorAttribution(ErrorBucket.OwnMod, setup.Own, new ResolvedSource(path, setup.Own, false));
        var vanilla = new ErrorAttribution(ErrorBucket.Vanilla, null, new ResolvedSource(path, null, false));
        var ownWithoutFile = new ErrorAttribution(ErrorBucket.OwnMod, setup.Own, null);

        Assert.Contains("traceScope", setup.Advisor.FixesFor(entry, Array.Empty<ErrorHint>(), own));
        Assert.DoesNotContain("traceScope", setup.Advisor.FixesFor(entry, Array.Empty<ErrorHint>(), vanilla));
        Assert.DoesNotContain("traceScope", setup.Advisor.FixesFor(entry, Array.Empty<ErrorHint>(), ownWithoutFile));
    }

    [Fact]
    public void NameMissingEverywhereGivesNotFoundHint()
    {
        using var setup = new Setup();
        var entry = Catalogs.Classify("[01:00:00][e.cpp:1162]: Variable 'built_from_param_x' is set but is never used.")[0];

        var hints = setup.Advisor.HintsFor(entry);
        var fixes = setup.Advisor.FixesFor(entry, hints, setup.Attribute(entry));

        Assert.Equal(HintKind.NotFoundInFiles, Assert.Single(hints).Kind);
        Assert.DoesNotContain("renameEverywhere", fixes);
        Assert.Equal(new[] { "suppress" }, fixes);
    }

    [Fact]
    public void SavedScopeAndLocalizationDataErrorsFindTheirUsages()
    {
        using var tree = new TempTree(
            ("own/in_game/common/on_action/probe.txt", "probe = {\n\teffect = {\n\t\tsave_scope_as = pmt_capital\n\t\tscope:pmt_capital = { add_gold = 1 }\n\t}\n}\n"),
            ("own/in_game/localization/english/probe_l_english.yml", "l_english:\n pmt_probe_loc:0 \"[ROOT.GetName]\"\n"));
        var own = new ModSource("Own", tree.PathOf("own"), true, 1);
        var advisor = new FixAdvisor(SymbolIndex.Build(new[] { new SymbolRoot(own.Directory, own, true) }), _ => null);

        var target = Catalogs.Classify("[15:09:56][jomini_effect.cpp:1166]: Event target 'pmt_capital' is used but is never set.")[0];
        var function = Catalogs.Classify("[15:19:50][pdx_data_factory.cpp:1426]: Could not find data system function 'GetName' in 'ROOT.GetName'.")[0];
        var locString = Catalogs.Classify("[15:19:50][pdx_data_localize.cpp:192]: Data error in loc string 'pmt_probe_loc'")[0];

        Assert.Equal(2, advisor.HintsFor(target).Single(h => h.Kind == HintKind.Usages).Occurrences.Count);
        Assert.EndsWith("probe_l_english.yml", advisor.HintsFor(function).Single(h => h.Kind == HintKind.UsedInLocalization).Occurrences[0].FilePath);
        Assert.EndsWith("probe_l_english.yml", advisor.HintsFor(locString).Single(h => h.Kind == HintKind.UsedInLocalization).Occurrences[0].FilePath);
    }

    [Fact]
    public void FixesOnlyForOwnModsExceptSuppress()
    {
        using var setup = new Setup();
        var entry = Catalogs.Classify("[01:00:00][e.cpp:1]: Unknown effect my_efect at common/scripted_effects/dep.txt:1")[0];

        var fixes = setup.Advisor.FixesFor(entry, setup.Advisor.HintsFor(entry), new ErrorAttribution(ErrorBucket.OtherMod, setup.Dependency, null));

        Assert.DoesNotContain("replaceWithCandidate", fixes);
        Assert.Contains("suppress", fixes);
    }

    [Fact]
    public void RealCaseCmfSuppressIsDefinedInWorkshopModOutsideActivePlayset()
    {
        var own = @"C:\Users\flori\Documents\Paradox Interactive\Europa Universalis V\mod\The Idea Variation 2 Testversion Beta Version";
        var cmf = @"C:\Program Files (x86)\Steam\steamapps\workshop\content\3450310\3692202776";
        if (!Directory.Exists(own) || !Directory.Exists(cmf))
        {
            return;
        }

        var ownMod = new ModSource("The Idea Variation 2 Testversion Beta Version", own, true, 0);
        var cmfMod = new ModSource("Community Mod Framework", cmf, false, -1);
        var index = SymbolIndex.Build(new[] { new SymbolRoot(own, ownMod, true), new SymbolRoot(cmf, cmfMod, false) });
        var resolver = new SourceResolver(new[] { ownMod, cmfMod }, Array.Empty<string>(), DateTime.MaxValue);
        var entry = Catalogs.Classify("[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/01_iv2_scripted_effect_log_purger.txt:680")[0];
        var advisor = new FixAdvisor(index, _ => null);

        var attribution = ErrorAttributor.Attribute(entry, resolver, index);
        var hints = advisor.HintsFor(entry);

        Assert.Equal(ErrorBucket.OwnMod, attribution.Bucket);
        var hint = Assert.Single(hints);
        Assert.Equal(HintKind.DefinedOutsidePlayset, hint.Kind);
        Assert.Same(cmfMod, hint.Mod);
        Assert.Contains("addModToPlayset", advisor.FixesFor(entry, hints, attribution));
    }
}
