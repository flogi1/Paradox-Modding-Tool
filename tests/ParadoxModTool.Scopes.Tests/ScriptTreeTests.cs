using Xunit;

namespace ParadoxModTool.Scopes.Tests;

public class ScriptTreeTests
{
    [Fact]
    public void Lexer_ReadsWordsStringsOperatorsAndBraces_SkippingComments()
    {
        var tokens = ScopeLexer.Tokenize("a = { b >= 5 # comment { }\n c = \"x y\" }");

        Assert.Equal(
            new[] { "a", "=", "{", "b", ">=", "5", "c", "=", "x y", "}" },
            tokens.Select(t => t.Text));
        Assert.Equal(ScopeTokenKind.String, tokens[8].Kind);
    }

    [Fact]
    public void Lexer_ToleratesUnclosedStringAndBom()
    {
        var tokens = ScopeLexer.Tokenize("﻿a = \"open\nb = 1");

        Assert.Equal(new[] { "a", "=", "open", "b", "=", "1" }, tokens.Select(t => t.Text));
    }

    [Fact]
    public void Tree_BuildsBlocksScalarsAndListItems()
    {
        var tree = ScriptTree.Parse("obj = { a = 1 list = { x y } { z } }");

        var obj = Assert.Single(tree);
        Assert.Equal("obj", obj.Key);
        Assert.Equal(3, obj.Children!.Count);
        Assert.Equal("1", obj.Children[0].Scalar);
        Assert.Equal(new[] { "x", "y" }, obj.Children[1].Children!.Select(c => c.Scalar));
        Assert.Null(obj.Children[2].Key);
        Assert.Equal("z", obj.Children[2].Children!.Single().Scalar);
    }

    [Fact]
    public void Tree_ClosesMissingBracesAtTheEnd_AndIgnoresStrayOnes()
    {
        var tree = ScriptTree.Parse("} a = { b = { c = 1");

        var a = Assert.Single(tree);
        var b = Assert.Single(a.Children!);
        Assert.Equal("c", Assert.Single(b.Children!).Key);
        Assert.True(a.End > 19);
    }

    [Fact]
    public void Tree_KeepsAKeyWithoutValueWhileTyping()
    {
        var tree = ScriptTree.Parse("a = { b =");

        var b = Assert.Single(Assert.Single(tree).Children!);
        Assert.Equal("b", b.Key);
        Assert.Null(b.Scalar);
        Assert.False(b.IsBlock);
    }

    [Fact]
    public void ContainsCaret_IsTrueBetweenTheBraces_OnlyAfterTheOpeningOne()
    {
        var text = "a = { b }";
        var a = ScriptTree.Parse(text).Single();

        Assert.False(a.ContainsCaret(4));
        Assert.True(a.ContainsCaret(5));
        Assert.True(a.ContainsCaret(8));
        Assert.False(a.ContainsCaret(9));
    }

    [Fact]
    public void Positions_PointAtTheKeyAndTheEndOfTheBlock()
    {
        var text = "  a = { b = 1 }";
        var a = ScriptTree.Parse(text).Single();

        Assert.Equal(2, a.Start);
        Assert.Equal(text.Length, a.End);
        Assert.Equal(text.IndexOf('{'), a.BlockStart);
    }
}
