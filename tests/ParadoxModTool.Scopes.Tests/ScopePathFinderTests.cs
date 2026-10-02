using ParadoxModTool.ScriptDocs;
using Xunit;

namespace ParadoxModTool.Scopes.Tests;

public class ScopePathFinderTests
{
    private static readonly DateTime T0 = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private static ScriptDocEntry E(ScriptDocKind kind, string name, string[]? scopes = null, string[]? targets = null, bool requiresData = false)
    {
        var entry = new ScriptDocEntry { Kind = kind, Name = name, Scopes = (scopes ?? Array.Empty<string>()).ToList(), Targets = (targets ?? Array.Empty<string>()).ToList() };
        if (requiresData)
        {
            entry.Extra["Requires Data"] = "yes";
        }

        return entry;
    }

    /// <summary>Setzt die Formangaben, die <see cref="ScriptDocImporter.MergeDuplicates"/> für Event-Targets schreibt.</summary>
    private static ScriptDocEntry Forms(ScriptDocEntry entry, string? plain, string? data)
    {
        if (plain is not null)
        {
            entry.Extra["Plain Inputs"] = plain;
        }

        if (data is not null)
        {
            entry.Extra["Data Inputs"] = data;
        }

        return entry;
    }

    private static ScriptDocEntry Described(ScriptDocEntry entry, string description)
    {
        entry.Description = description;
        return entry;
    }

    private static ScopePathFinder Finder()
    {
        var database = new ScriptDocDatabase();
        database.ApplyImport(
            new[]
            {
                E(ScriptDocKind.EventTarget, "owner", new[] { "location" }, new[] { "country" }),
                E(ScriptDocKind.EventTarget, "capital", new[] { "country" }, new[] { "location" }),
                E(ScriptDocKind.EventTarget, "ruler", new[] { "country" }, new[] { "character" }),
                E(ScriptDocKind.EventTarget, "employer", new[] { "character" }, new[] { "country" }),
                E(ScriptDocKind.EventTarget, "generic", Array.Empty<string>(), new[] { "country" }),
                E(ScriptDocKind.EventTarget, "multi", new[] { "country" }, new[] { "location", "character" }),
                E(ScriptDocKind.EventTarget, "area", Array.Empty<string>(), new[] { "area" }, requiresData: true),
                E(ScriptDocKind.Effect, "every_neighbor_country", new[] { "country" }, new[] { "country" }),
                E(ScriptDocKind.Trigger, "any_neighbor_country", new[] { "country" }, new[] { "country" }),
                E(ScriptDocKind.Effect, "random_owned_location", new[] { "country" }, new[] { "location" }),
                E(ScriptDocKind.Trigger, "any_owned_location", new[] { "country" }, new[] { "location" }),
                E(ScriptDocKind.Trigger, "any_ruling_country", new[] { "character" }, new[] { "country", "location" }),
                Forms(E(ScriptDocKind.EventTarget, "culture", new[] { "country" }, new[] { "culture" }, requiresData: true), plain: "country", data: ""),
                E(ScriptDocKind.EventTarget, "language", new[] { "culture" }, new[] { "language" }),
                Forms(E(ScriptDocKind.EventTarget, "modifier", new[] { "country" }, new[] { "value", "boolean" }, requiresData: true), plain: null, data: "country"),
                Forms(E(ScriptDocKind.EventTarget, "holding", new[] { "country" }, new[] { "location" }, requiresData: true), plain: null, data: "country"),
                E(ScriptDocKind.EventTarget, "estate_power", new[] { "country" }, new[] { "value" }),
                Described(E(ScriptDocKind.Trigger, "country_tax_base", new[] { "country" }), "Checks the total tax base\nTraits: <, <=, =, !=, >, >="),
                Described(E(ScriptDocKind.Trigger, "opinion", new[] { "country" }), "Usage: opinion(<target>)\nTraits: <, <=, =, !=, >, >="),
                Described(E(ScriptDocKind.Trigger, "location_tax", new[] { "location" }), "Traits: <, <=, =, !=, >, >="),
                Described(E(ScriptDocKind.Trigger, "global_value", Array.Empty<string>()), "Traits: <, <=, =, !=, >, >="),
                Described(E(ScriptDocKind.Trigger, "has_flag", new[] { "country" }), "Traits: yes/no"),
            },
            Enum.GetValues<ScriptDocKind>().ToList(),
            T0,
            new Dictionary<string, long>());
        return new ScopePathFinder(ScopeDb.FromDatabase(database));
    }

    [Fact]
    public void DirectTargetsComeBeforeIterators()
    {
        var paths = Finder().Find("country", "location", maxSteps: 1);

        Assert.Equal(new[] { "capital", "holding:KEY", "random_owned_location" }, paths.Select(p => p.Describe("every")));
        Assert.All(paths, p => Assert.Single(p.Steps));
    }

    [Fact]
    public void ConsecutiveTargetsFormAChain()
    {
        var path = Finder().Find("character", "location", maxSteps: 2).First(p => p.Steps.Count == 2 && p.IsChain);

        Assert.Equal("employer.capital", path.Describe("every"));
        Assert.Equal("employer.capital", path.ChainText());
    }

    [Fact]
    public void AnIteratorInThePathIsItsOwnBlock_AndHasNoChainText()
    {
        var path = Finder().Find("country", "character", maxSteps: 2).First(p => p.Steps.Any(s => s.Kind == PathStepKind.Iterator));

        Assert.Equal("every_neighbor_country › ruler", path.Describe("every"));
        Assert.Null(path.ChainText());
    }

    [Fact]
    public void TheIteratorFormFollowsThePreferenceAsFarAsTheGameKnowsIt()
    {
        var paths = Finder().Find("country", "location", maxSteps: 1);
        var iterator = paths.Single(p => p.Steps[0].Kind == PathStepKind.Iterator);

        Assert.Equal("any_owned_location", iterator.Describe("any"));
        Assert.Equal("random_owned_location", iterator.Describe("random"));
        Assert.Equal("random_owned_location", iterator.Describe("ordered"));
        Assert.Equal("random_owned_location", iterator.Describe("every"));
    }

    [Fact]
    public void StartAndGoalAreTheSame_ReturnsNothing()
    {
        Assert.Empty(Finder().Find("country", "country"));
    }

    [Fact]
    public void NoWay_ReturnsNothing()
    {
        Assert.Empty(Finder().Find("location", "character", maxSteps: 1));
    }

    [Fact]
    public void TargetsWithSeveralResultsAreLeftOut()
    {
        var all = Finder().Find("country", "location", maxSteps: 2).SelectMany(p => p.Steps).Select(s => s.Name);

        Assert.DoesNotContain("multi", all);
        Assert.DoesNotContain("ruling_country", all);
    }

    [Fact]
    public void GeneralTargetsWithoutInputScopesComeAfterSpecificOnes()
    {
        var paths = Finder().Find("character", "country", maxSteps: 1);

        Assert.Equal(new[] { "employer", "generic" }, paths.Select(p => p.Describe("every")));
    }

    [Fact]
    public void GlobalLinksAreOnlyOfferedOnRequest_AndOnlyAsFirstStep()
    {
        var finder = Finder();

        Assert.Empty(finder.Find("character", "area", maxSteps: 2));
        var path = Assert.Single(finder.Find("character", "area", maxSteps: 2, includeGlobalLinks: true));
        Assert.Equal("area:KEY", path.Describe("every"));
        Assert.DoesNotContain(finder.Find("country", "location", maxSteps: 2, includeGlobalLinks: true).SelectMany(p => p.Steps.Skip(1)), s => s.Kind == PathStepKind.GlobalLink);
    }

    [Fact]
    public void AStartPrefixBeginsThePath()
    {
        var paths = Finder().Find("country", "location", maxSteps: 1, startPrefix: "scope:actor");

        Assert.Equal(new[] { "scope:actor.capital", "scope:actor.holding:KEY", "scope:actor › random_owned_location" }, paths.Select(p => p.Describe("every")));
    }

    [Fact]
    public void NoScopeIsVisitedTwice_ExceptForOneSameScopeHop()
    {
        // country -> location -> country wäre eine Schleife zum Start; nur ein Schritt innerhalb desselben Scopes (Nachbarländer) ist erlaubt.
        var paths = Finder().Find("country", "location", maxSteps: 3);

        Assert.NotEmpty(paths);
        Assert.All(paths, p =>
        {
            var visited = p.Steps.Select(s => s.To).ToList();
            Assert.True(visited.Count(v => v == "country") <= 1, p.Describe("every"));
            Assert.Equal(visited.Where(v => v != "country").Count(), visited.Where(v => v != "country").Distinct().Count());
        });
    }

    [Fact]
    public void ASameScopeIteratorCanLeadToTheGoal()
    {
        var paths = Finder().Find("country", "character", maxSteps: 2);

        Assert.Contains("every_neighbor_country › ruler", paths.Select(p => p.Describe("every")));
    }

    [Fact]
    public void PlainFormOfADuplicateTarget_IsANormalStep()
    {
        var path = Finder().Find("location", "language", maxSteps: 3).First();

        Assert.Equal("owner.culture.language", path.ChainText());
    }

    [Fact]
    public void KeyedTargetWithInputs_IsADataLinkAtAnyPosition()
    {
        var path = Finder().Find("character", "location", maxSteps: 2).First(p => p.Steps.Any(s => s.Kind == PathStepKind.DataLink));

        Assert.Equal("employer.holding:KEY", path.ChainText());
    }

    [Fact]
    public void ValueOnlyTargets_AreNotSteps()
    {
        Assert.DoesNotContain("value", Finder().Scopes);
        Assert.DoesNotContain(Finder().Find("country", "location", maxSteps: 2).SelectMany(p => p.Steps), s => s.Name == "modifier");
    }

    /// <summary>hub → goal direkt über "direct"; dazu 40 allgemeine Ziele "to_midN" (→ midN) und "from_midN" (midN → goal): viele längere Wege.</summary>
    private static ScopeDb BigDb()
    {
        var entries = new List<ScriptDocEntry> { E(ScriptDocKind.EventTarget, "direct", new[] { "hub" }, new[] { "goal" }) };
        for (var i = 0; i < 40; i++)
        {
            entries.Add(E(ScriptDocKind.EventTarget, $"to_mid{i}", Array.Empty<string>(), new[] { $"mid{i}" }));
            entries.Add(E(ScriptDocKind.EventTarget, $"from_mid{i}", new[] { $"mid{i}" }, new[] { "goal" }));
        }

        var database = new ScriptDocDatabase();
        database.ApplyImport(entries, Enum.GetValues<ScriptDocKind>().ToList(), T0, new Dictionary<string, long>());
        return ScopeDb.FromDatabase(database);
    }

    [Fact]
    public void Search_ShortPathSurvivesTruncatedDeeperSearch()
    {
        var finder = new ScopePathFinder(BigDb()) { MaxCandidatesPerLength = 50 };

        var result = finder.Search("hub", "goal", 4, false, "", "", "every");

        Assert.Equal("direct", result.Paths[0].Describe("every"));
        Assert.True(result.Truncated);
    }

    [Fact]
    public void Search_FilterAppliesBeforeLimit()
    {
        var finder = new ScopePathFinder(BigDb());
        var unfiltered = finder.Search("hub", "goal", 3, false, "", "", "every", limit: 60);
        var hidden = finder.Search("hub", "goal", 3, false, "", "", "every", limit: int.MaxValue).Paths.Skip(60).First();

        var filtered = finder.Search("hub", "goal", 3, false, "", hidden.Describe("every"), "every", limit: 60);

        Assert.DoesNotContain(unfiltered.Paths, p => p.Describe("every") == hidden.Describe("every"));
        Assert.Contains(filtered.Paths, p => p.Describe("every") == hidden.Describe("every"));
        Assert.False(unfiltered.Truncated);
        Assert.True(unfiltered.Total > 60);
    }

    [Fact]
    public void Search_ReturnsShortestFirst_AndAcceptsFourSteps()
    {
        // location → area:KEY → generic (country) → every_neighbor_country → ruler: nur mit globalen Verweisen vier Schritte lang.
        var result = Finder().Search("location", "character", 4, true, "", "", "every", limit: int.MaxValue);

        Assert.True(result.Paths.Select(p => p.Steps.Count).SequenceEqual(result.Paths.Select(p => p.Steps.Count).Order()));
        Assert.Contains(result.Paths, p => p.Steps.Count == 4);
    }

    [Fact]
    public void NextSteps_SpecificTargetsFirst()
    {
        var steps = Finder().NextSteps("country", first: false, includeGlobalLinks: false);

        Assert.Equal(PathStepKind.Target, steps[0].Kind);
        Assert.False(steps[0].AnyInput);
        Assert.True(steps.Select(s => s.AnyInput).SequenceEqual(steps.Select(s => s.AnyInput).Order()));
        Assert.Contains(steps, s => s.Kind == PathStepKind.Iterator);
        Assert.DoesNotContain(steps, s => s.Name == "modifier");
    }

    [Fact]
    public void ValueSuggestions_BuiltInThenModThenVanilla()
    {
        var list = Finder().ValueSuggestions("country", new[] { "zz_mod", "aa_mod" }, new[] { "zz_vanilla", "aa_vanilla" });
        var builtIn = Finder().Values("country");

        Assert.Equal(builtIn.Concat(new[] { "aa_mod", "zz_mod", "aa_vanilla", "zz_vanilla" }), list);
    }

    [Fact]
    public void ValueSuggestions_NameInModAndVanilla_AppearsOnceInModGroup()
    {
        var list = Finder().ValueSuggestions("country", new[] { "shared" }, new[] { "shared", "other" }).ToList();

        Assert.Single(list, v => v == "shared");
        Assert.True(list.IndexOf("shared") < list.IndexOf("other"));
    }

    [Fact]
    public void ValueSuggestions_BuiltInWins()
    {
        Assert.Single(Finder().ValueSuggestions("country", new[] { "country_tax_base" }, Array.Empty<string>()), v => v == "country_tax_base");
    }

    [Fact]
    public void Values_ListComparisonTriggersValueTargetsKeyedValuesAndVar()
    {
        var values = Finder().Values("country");

        Assert.Contains("country_tax_base", values);
        Assert.Contains("opinion(ARG)", values);
        Assert.Contains("global_value", values);
        Assert.Contains("estate_power", values);
        Assert.Contains("modifier:KEY", values);
        Assert.Contains("var:KEY", values);
        Assert.DoesNotContain("location_tax", values);
        Assert.DoesNotContain("has_flag", values);
        Assert.Equal(values.Order(StringComparer.Ordinal), values);
    }

    [Fact]
    public void Scopes_ListsEveryScopeOfTheGraphSorted()
    {
        var scopes = Finder().Scopes;

        Assert.Equal(new[] { "area", "character", "country", "culture", "language", "location" }, scopes);
    }

    [Fact]
    public void ToBlock_NestsChainsAndIterators_AndPlacesTheCaretInTheInnermostBlock()
    {
        var path = Finder().Find("country", "character", maxSteps: 2).First(p => p.Steps.Count == 2 && !p.IsChain);

        var text = ScopeChain.From(path).ToBlock("every", "\t", "\n", out var caret);

        Assert.Equal("every_neighbor_country = {\n\t\truler = {\n\t\t\t\n\t\t}\n\t}", text);
        Assert.Equal("every_neighbor_country = {\n\t\truler = {\n\t\t\t".Length, caret);
    }

    [Fact]
    public void ToBlock_UsesTheGivenNewlineAndTab()
    {
        var path = Finder().Find("character", "location", maxSteps: 2).First(p => p.IsChain && p.Steps.Count == 2);

        var text = ScopeChain.From(path).ToBlock("every", string.Empty, "\r\n", out var caret, tab: "    ");

        Assert.Equal("employer.capital = {\r\n    \r\n}", text);
        Assert.Equal("employer.capital = {\r\n    ".Length, caret);
    }

    private static ScopeContext ContextAt(string textWithCaret)
    {
        var caret = textWithCaret.IndexOf('|');
        return new ScopeEngine(TestData.Db(), TestData.Rules()).Analyze(textWithCaret.Remove(caret, 1), caret, @"C:\mods\m\in_game\common\buildings\a.txt");
    }

    [Theory]
    [InlineData("b = { potential = { | } }", true)]
    [InlineData("b = { potential = { any_owned_location = { | } } }", true)]
    [InlineData("b = { potential = { every_neighbor_country = { | } } }", false)]
    [InlineData("b = { potential = { every_neighbor_country = { limit = { | } } } }", true)]
    [InlineData("b = { allow = { | } }", true)]
    [InlineData("b = { modifier_stuff = { | } }", false)]
    public void TriggerContextIsGuessedFromTheInnermostBlock(string text, bool expected)
    {
        Assert.Equal(expected, ScopePathFinder.LooksLikeTriggerContext(ContextAt(text)));
    }
}
