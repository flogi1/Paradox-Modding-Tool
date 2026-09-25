using Xunit;

namespace ParadoxModTool.Scopes.Tests;

public class ScopeIssueTests
{
    private const string BuildingFile = @"C:\mods\m\in_game\common\buildings\a.txt";

    private static ScopeRules RulesWithSavedScopes()
    {
        var rules = TestData.Rules();
        rules.SavedFields["buildings/*/allow"] = new Dictionary<string, string> { ["actor"] = "country" };
        rules.SavedNames["actor"] = "country";
        rules.SavedNames["target_character"] = "character";
        return rules;
    }

    private static readonly ScopeEngine Engine = new(TestData.Db(), RulesWithSavedScopes());

    private static IReadOnlyList<ScopeIssue> Issues(string text, string? path = BuildingFile) => Engine.FindIssues(text, path);

    [Fact]
    public void AStatementThatIsNotValidInTheSureScope_IsReported()
    {
        const string text = "b = { potential = { is_female = yes } }";

        var issue = Assert.Single(Issues(text));

        Assert.Equal("is_female", issue.Command);
        Assert.Equal(text.IndexOf("is_female", StringComparison.Ordinal), issue.Start);
        Assert.Equal("is_female".Length, issue.Length);
        Assert.Equal("country", issue.Scope);
        Assert.Equal(new[] { "character" }, issue.Supported);
        Assert.Equal("is_female gilt nur in character – hier: country", issue.Message);
    }

    [Fact]
    public void ValidStatementsAndUnknownNames_AreNotReported()
    {
        Assert.Empty(Issues("b = { potential = { add_gold = 1 has_dlc = x some_unknown_thing = yes } }"));
    }

    [Fact]
    public void AScopeThatIsNotSure_IsNotReported()
    {
        // buildings/*/weak: Zustimmung nur 80 %, buildings/*/tied: kein eindeutiger Scope.
        Assert.Empty(Issues("b = { weak = { is_female = yes } tied = { is_female = yes } }"));
    }

    [Fact]
    public void StatementsDirectlyInTheObject_AreFieldsAndNotChecked()
    {
        Assert.Empty(Issues("b = { is_female = yes }"));
    }

    [Fact]
    public void AnIteratorMakesTheScopeSure_AndItsBodyIsChecked()
    {
        var issue = Assert.Single(Issues("b = { potential = { any_owned_location = { add_gold = 1 } } }"));

        Assert.Equal("add_gold", issue.Command);
        Assert.Equal("location", issue.Scope);
    }

    [Fact]
    public void TheIteratorItselfMustBeValidInTheOuterScope()
    {
        // any_owned_location gilt in country, hier aber im Scope character (Feld allow ist location).
        var issue = Assert.Single(Issues("b = { allow = { any_owned_location = { is_capital = yes } } }"));

        Assert.Equal("any_owned_location", issue.Command);
        Assert.Equal("location", issue.Scope);
    }

    [Fact]
    public void TransparentBlocks_KeepTheScope()
    {
        var issue = Assert.Single(Issues("b = { potential = { OR = { limit = { is_female = yes } } } }"));

        Assert.Equal("is_female", issue.Command);
    }

    [Fact]
    public void ParameterBlocksOfCommands_AreNotEntered()
    {
        Assert.Empty(Issues("b = { potential = { add_gold = { is_female = yes } } }"));
    }

    [Fact]
    public void ALearnedSavedScopeGivesASureScopeInsideItsBlock()
    {
        var issue = Assert.Single(Issues("b = { allow = { scope:actor = { is_female = yes add_gold = 1 } } }"));

        Assert.Equal("is_female", issue.Command);
        Assert.Equal("country", issue.Scope);
    }

    [Fact]
    public void ALearnedSavedNameWithoutFieldRule_FallsBackToTheNameRule()
    {
        var issue = Assert.Single(Issues("b = { allow = { scope:target_character = { add_gold = 1 } } }"));

        Assert.Equal("add_gold", issue.Command);
        Assert.Equal("character", issue.Scope);
    }

    [Fact]
    public void ASavedScopeOfTheFileWinsOverTheLearnedRule()
    {
        // save_scope_as speichert den Scope location; die gelernte Regel für "actor" (country) darf ihn nicht überdecken.
        var context = Engine.FindIssues("b = { allow = { save_scope_as = actor scope:actor = { is_capital = yes } } }", BuildingFile);

        Assert.Empty(context);
    }

    [Fact]
    public void AnUnknownSavedScope_MeansNoStatementInsideIsReported()
    {
        Assert.Empty(Issues("b = { allow = { scope:mystery = { is_female = yes } } }"));
    }

    [Fact]
    public void AnalyzeUsesTheLearnedSavedScopes()
    {
        var text = "b = { allow = { scope:actor = {  } } }";
        var caret = text.IndexOf("  ", StringComparison.Ordinal) + 1;

        var context = Engine.Analyze(text, caret, BuildingFile);

        Assert.Equal("country", context.CurrentScope);
        Assert.True(context.Confident);
        Assert.Equal("country", context.LearnedSavedScopes["actor"]);
    }

    [Fact]
    public void EventsWithoutType_AreSkipped()
    {
        Assert.Empty(Engine.FindIssues("e.1 = { immediate = { is_female = yes } }", @"C:\mods\m\in_game\events\a.txt"));
    }

    [Fact]
    public void FilesOutsideCommonAndEvents_AreSkipped()
    {
        Assert.Empty(Engine.FindIssues("b = { potential = { is_female = yes } }", @"C:\mods\m\gui\a.gui"));
    }

    [Fact]
    public void WithoutRules_NothingIsReported()
    {
        Assert.Empty(new ScopeEngine(TestData.Db(), null).FindIssues("b = { potential = { is_female = yes } }", BuildingFile));
    }

    [Fact]
    public void IncompleteTextDoesNotThrow()
    {
        Assert.NotNull(Issues("b = { potential = { is_female = yes"));
        Assert.NotNull(Issues("b = { potential = { any_owned_location = { "));
    }
}
