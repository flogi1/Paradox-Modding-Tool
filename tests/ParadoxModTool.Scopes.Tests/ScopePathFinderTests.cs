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

        Assert.Equal(new[] { "capital", "random_owned_location" }, paths.Select(p => p.Describe("every")));
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

        Assert.Equal(new[] { "scope:actor.capital", "scope:actor › random_owned_location" }, paths.Select(p => p.Describe("every")));
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
    public void Scopes_ListsEveryScopeOfTheGraphSorted()
    {
        var scopes = Finder().Scopes;

        Assert.Equal(new[] { "area", "character", "country", "location" }, scopes);
    }

    [Fact]
    public void ToBlock_NestsChainsAndIterators_AndPlacesTheCaretInTheInnermostBlock()
    {
        var path = Finder().Find("country", "character", maxSteps: 2).First(p => p.Steps.Count == 2 && !p.IsChain);

        var text = path.ToBlock("every", "\t", "\n", out var caret);

        Assert.Equal("every_neighbor_country = {\n\t\truler = {\n\t\t\t\n\t\t}\n\t}", text);
        Assert.Equal("every_neighbor_country = {\n\t\truler = {\n\t\t\t".Length, caret);
    }

    [Fact]
    public void ToBlock_UsesTheGivenNewlineAndTab()
    {
        var path = Finder().Find("character", "location", maxSteps: 2).First(p => p.IsChain && p.Steps.Count == 2);

        var text = path.ToBlock("every", string.Empty, "\r\n", out var caret, tab: "    ");

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
