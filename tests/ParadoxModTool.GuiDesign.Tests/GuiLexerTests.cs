using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiLexerTests
{
    private static List<GuiToken> Lex(string text, List<GuiDiagnostic>? diagnostics = null) =>
        GuiLexer.Tokenize(text, diagnostics).Where(t => t.Kind != GuiTokenKind.Eof).ToList();

    [Fact]
    public void SplitsWordsOperatorsBracesStringsAndComments()
    {
        var tokens = Lex("size = { 10 20 } # Kommentar\ntext = \"Hallo\"");

        Assert.Equal(
            new[] { GuiTokenKind.Word, GuiTokenKind.Operator, GuiTokenKind.Open, GuiTokenKind.Word, GuiTokenKind.Word, GuiTokenKind.Close, GuiTokenKind.Comment, GuiTokenKind.Word, GuiTokenKind.Operator, GuiTokenKind.String },
            tokens.Select(t => t.Kind));
        Assert.Equal("# Kommentar", tokens[6].Text);
        Assert.Equal("Hallo", tokens[9].Text);
        Assert.Equal(2, tokens[9].Line);
    }

    [Fact]
    public void TokenSpansPointAtTheirSourceText()
    {
        const string text = "a = \"b c\" { @[x - y * 2] }";
        foreach (var token in Lex(text))
        {
            var raw = text[token.Start..token.End];
            Assert.Equal(token.Kind == GuiTokenKind.String ? "\"" + token.Text + "\"" : token.Text, raw);
        }
    }

    [Fact]
    public void KeepsInlineMathAndBracketExpressionsAsOneWord()
    {
        var tokens = Lex("size = { @[tooltip_width - margin * 2] 20 }\nvisible = [Func( a, b )]");

        Assert.Equal("@[tooltip_width - margin * 2]", tokens[3].Text);
        Assert.Equal("20", tokens[4].Text);
        Assert.Equal("[Func( a, b )]", tokens[8].Text);
    }

    [Fact]
    public void AllowsMultiLineStrings()
    {
        var tokens = Lex("visible = \"[And(A,\n\t\tB)]\"\nnext = 1");

        Assert.Equal("[And(A,\n\t\tB)]", tokens[2].Text);
        Assert.Equal(3, tokens[3].Line);
    }

    [Fact]
    public void UnterminatedStringEndsAtLineEndWithDiagnostic()
    {
        var diagnostics = new List<GuiDiagnostic>();
        var tokens = Lex("text = \"offen", diagnostics);

        Assert.Equal("offen", tokens[2].Text);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(GuiDiagnosticKind.UnterminatedString, diagnostic.Kind);
    }

    [Fact]
    public void ReadsTwoCharacterOperators()
    {
        var tokens = Lex("a >= 1 b != 2 c ?= 3 d < 4");

        Assert.Equal(new[] { ">=", "!=", "?=", "<" }, tokens.Where(t => t.Kind == GuiTokenKind.Operator).Select(t => t.Text));
    }

    [Fact]
    public void SkipsByteOrderMark()
    {
        var tokens = Lex("\uFEFFwidget = {}");

        Assert.Equal("widget", tokens[0].Text);
        Assert.Equal(1, tokens[0].Start);
    }
}
