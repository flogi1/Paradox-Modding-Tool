using Xunit;

namespace ParadoxModTool.Scopes.Tests;

public class ScopeChainTests
{
    private static readonly IReadOnlySet<string> NoForms = new HashSet<string>();

    private static PathStep S(string name, string to) => new(PathStepKind.Target, name, to, AnyInput: false, NoForms);

    private static PathStep I(string name, string to) => new(PathStepKind.Iterator, name, to, AnyInput: false, new HashSet<string> { "every", "any" });

    [Fact]
    public void ChainText_ValueDirectlyAtTheStart_WithOrWithoutPrefix()
    {
        Assert.Equal("root.country_tax_base", ScopeChain.Empty("country", "root").WithValue("country_tax_base").ChainText());
        Assert.Equal("country_tax_base", ScopeChain.Empty("country", "").WithValue("country_tax_base").ChainText());
        Assert.True(ScopeChain.Empty("country", "").CanBeChain);
        Assert.Null(ScopeChain.Empty("country", "root").ChainText());
    }

    [Fact]
    public void ChainText_JoinsPrefixLinksAndValue()
    {
        var chain = ScopeChain.Empty("international_organization", "scope:recipient")
            .Append(S("leader_country", "country")).Append(S("culture", "culture")).Append(S("language", "language"));

        Assert.Equal("scope:recipient.leader_country.culture.language", chain.ChainText());
        Assert.Equal("language", chain.EndScope);
        Assert.Equal(
            "scope:recipient.leader_country.country_tax_base",
            ScopeChain.Empty("international_organization", "scope:recipient").Append(S("leader_country", "country")).WithValue("country_tax_base").ChainText());
    }

    [Fact]
    public void Value_IsIgnoredWhenChainIsNotPure()
    {
        var chain = ScopeChain.Empty("country", "").Append(S("capital", "location")).WithValue("x").WithCondition(0, true);

        Assert.False(chain.CanBeChain);
        Assert.Null(chain.ChainText());
        Assert.DoesNotContain("x", chain.ToBlock("every", "", "\n", out _));
    }

    [Fact]
    public void ToBlock_ConditionAndLimit_MatchSpecExample()
    {
        var chain = ScopeChain.Empty("location", "")
            .Append(S("owner", "country")).WithCondition(0, true)
            .Append(I("neighbor_country", "country")).WithLimit(1, true)
            .Append(S("capital", "location"));

        var text = chain.ToBlock("every", "", "\n", out var caret);

        Assert.Equal(
            "owner = {\n\tif = {\n\t\tlimit = {\n\t\t\t\n\t\t}\n\t\tevery_neighbor_country = {\n\t\t\tlimit = {\n\t\t\t}\n\t\t\tcapital = {\n\t\t\t}\n\t\t}\n\t}\n}",
            text);
        Assert.Equal("owner = {\n\tif = {\n\t\tlimit = {\n\t\t\t".Length, caret);
    }

    [Fact]
    public void ToBlock_AnyForm_UsesTriggerIfAndDropsLimit()
    {
        var chain = ScopeChain.Empty("country", "").Append(I("neighbor_country", "country")).WithLimit(0, true).WithCondition(0, true);

        var text = chain.ToBlock("any", "", "\n", out _);

        Assert.Equal("any_neighbor_country = {\n\ttrigger_if = {\n\t\tlimit = {\n\t\t\t\n\t\t}\n\t}\n}", text);
    }

    [Fact]
    public void ToBlock_StartCondition_WrapsEverything_AndPrefixJoinsFirstHeader()
    {
        var chain = ScopeChain.Empty("international_organization", "scope:recipient").WithCondition(-1, true)
            .Append(S("leader_country", "country")).Append(S("capital", "location"));

        var text = chain.ToBlock("every", "", "\n", out _);

        Assert.Equal("if = {\n\tlimit = {\n\t\t\n\t}\n\tscope:recipient.leader_country.capital = {\n\t}\n}", text);
    }

    [Fact]
    public void ToBlock_WithoutConditions_PutsCaretInInnermostBlock()
    {
        var chain = ScopeChain.Empty("country", "").Append(S("capital", "location")).Append(S("owner", "country"));

        var text = chain.ToBlock("every", "", "\n", out var caret);

        Assert.Equal("capital.owner = {\n\t\n}", text);
        Assert.Equal("capital.owner = {\n\t".Length, caret);
    }

    [Fact]
    public void ToBlock_IndentsFollowingLinesAndUsesTheGivenTab()
    {
        var chain = ScopeChain.Empty("country", "").Append(I("neighbor_country", "country")).Append(S("capital", "location"));

        var text = chain.ToBlock("every", "\t", "\r\n", out var caret, tab: "    ");

        Assert.Equal("every_neighbor_country = {\r\n\t    capital = {\r\n\t        \r\n\t    }\r\n\t}", text);
        Assert.Equal("every_neighbor_country = {\r\n\t    capital = {\r\n\t        ".Length, caret);
    }

    [Fact]
    public void TruncateAt_RemovesLinkAndEverythingBehind()
    {
        var chain = ScopeChain.Empty("country", "").Append(S("capital", "location")).Append(S("owner", "country")).TruncateAt(1);

        Assert.Single(chain.Links);
        Assert.Equal("location", chain.EndScope);
    }

    [Fact]
    public void WithLimit_OnlyAffectsIterators()
    {
        var chain = ScopeChain.Empty("country", "").Append(S("capital", "location")).WithLimit(0, true);

        Assert.False(chain.Links[0].Limit);
        Assert.True(ScopeChain.Empty("country", "").Append(I("neighbor_country", "country")).WithLimit(0, true).Links[0].Limit);
    }

    [Fact]
    public void From_TakesStartPrefixAndStepsOfAPath_WithAllSwitchesOff()
    {
        var path = new ScopePath("country", "location", "root", new[] { S("capital", "location") });

        var chain = ScopeChain.From(path);

        Assert.Equal("root.capital", chain.ChainText());
        Assert.False(chain.StartCondition);
        Assert.All(chain.Links, l => Assert.False(l.Condition || l.Limit));
    }

    [Fact]
    public void StartCondition_MakesTheChainImpure()
    {
        var chain = ScopeChain.Empty("country", "").Append(S("capital", "location")).WithCondition(-1, true);

        Assert.True(chain.StartCondition);
        Assert.False(chain.CanBeChain);
        Assert.Null(chain.ChainText());
    }
}
