using Xunit;

namespace ParadoxModTool.Scopes.Tests;

public class BracketMatcherTests
{
    private static BracketMatch? Find(string text, int caret, bool localization = false) => BracketMatcher.Find(text, caret, localization);

    [Fact]
    public void Opener_FindsItsCloser_WhenCaretIsBeforeOrAfterIt()
    {
        const string text = "a = { b = 1 }";

        Assert.Equal(new BracketMatch(4, 12), Find(text, 4));
        Assert.Equal(new BracketMatch(4, 12), Find(text, 5));
    }

    [Fact]
    public void Closer_FindsItsOpener_WhenCaretIsBeforeOrAfterIt()
    {
        const string text = "a = { b = 1 }";

        Assert.Equal(new BracketMatch(12, 4), Find(text, 12));
        Assert.Equal(new BracketMatch(12, 4), Find(text, 13));
    }

    [Fact]
    public void Nested_PairsTheInnerBracketsCorrectly()
    {
        const string text = "{ { } }";

        Assert.Equal(new BracketMatch(0, 6), Find(text, 0));
        Assert.Equal(new BracketMatch(2, 4), Find(text, 2));
        Assert.Equal(new BracketMatch(4, 2), Find(text, 5));
    }

    [Fact]
    public void EachBracketTypeIsCountedOnItsOwn()
    {
        const string text = "{ ( } )";

        Assert.Equal(new BracketMatch(0, 4), Find(text, 0));
        Assert.Equal(new BracketMatch(2, 6), Find(text, 2));
    }

    [Fact]
    public void SquareAndRoundBracketsWorkToo()
    {
        Assert.Equal(new BracketMatch(1, 9), Find("@[ (1+2) ]", 1));
        Assert.Equal(new BracketMatch(3, 7), Find("@[ (1+2) ]", 3));
    }

    [Fact]
    public void BracketsInCommentsAndStringsAreIgnored()
    {
        const string text = "a = { # }\n b = \"}\" }";

        Assert.Equal(new BracketMatch(4, 19), Find(text, 4));
        Assert.Null(Find(text, 8));
    }

    [Fact]
    public void UnclosedStringEndsAtLineEnd()
    {
        const string text = "a = { b = \"open\n c = 1 }";

        Assert.Equal(new BracketMatch(4, 23), Find(text, 4));
    }

    [Fact]
    public void Unbalanced_HasNoPartner()
    {
        Assert.Null(Find("a = { b = 1", 4));
        Assert.Null(Find("a = 1 }", 6));
    }

    [Fact]
    public void NoBracketAtCaret_ReturnsNull()
    {
        Assert.Null(Find("a = 1", 2));
        Assert.Null(Find(string.Empty, 0));
    }

    [Fact]
    public void CaretBeforeAndAfterDifferentBrackets_PrefersTheOneBeforeTheCaret()
    {
        const string text = "{ { } }";

        Assert.Equal(new BracketMatch(0, 6), Find(text, 1));
    }

    [Fact]
    public void FallsBackToTheBracketAfterTheCaret_WhenTheOneBeforeHasNoPartner()
    {
        const string text = "}{ }";

        Assert.Equal(new BracketMatch(1, 3), Find(text, 1));
    }

    [Fact]
    public void Localization_BracketsInQuotesCountAndHashOnlyCommentsAtLineStart()
    {
        const string text = " key: \"Hello [Concept] # not a comment\"\n# [ in comment\n";

        Assert.Equal(new BracketMatch(13, 21), Find(text, 13, localization: true));
        Assert.Null(Find(text, 43, localization: true));
    }
}
