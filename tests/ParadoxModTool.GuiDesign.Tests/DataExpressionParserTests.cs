using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class DataExpressionParserTests
{
    private const string Example =
        "\"[And(Not(AdvanceItem.IsResearched), Not(EqualTo_int32(AdvanceItem.GetPrerequisiteTotal, '(int32)0')))]\"";

    [Fact]
    public void Parse_Example_BuildsCallsChainsAndLiterals()
    {
        var parsed = DataExpressionParser.Parse(Example);

        Assert.Empty(parsed.Errors);
        Assert.Equal("\"[", parsed.Prefix);
        Assert.Equal("]\"", parsed.Suffix);
        Assert.True(parsed.IsBracketed);
        var and = Assert.IsType<CallNode>(parsed.Root);
        Assert.Equal("And", and.Name);
        Assert.Equal(2, and.Args.Count);
        var not = Assert.IsType<CallNode>(and.Args[0]);
        Assert.Equal("Not", not.Name);
        var chain = Assert.IsType<ChainNode>(Assert.Single(not.Args));
        Assert.Equal(new[] { "AdvanceItem", "IsResearched" }, chain.Links.Select(l => l.Name));
        Assert.All(chain.Links, l => Assert.Null(l.Args));
        var compare = Assert.IsType<CallNode>(Assert.Single(Assert.IsType<CallNode>(and.Args[1]).Args));
        Assert.Equal("EqualTo_int32", compare.Name);
        var literal = Assert.IsType<LiteralNode>(compare.Args[1]);
        Assert.Equal("int32", literal.Type);
        Assert.Equal("0", literal.Value);
    }

    [Fact]
    public void Parse_ChainLinkWithArguments()
    {
        var parsed = DataExpressionParser.Parse("[GetVariableSystem.HasValue( 'iv2_var_system_tabs', 'iv2_tabs_ideagroups' )]");

        var chain = Assert.IsType<ChainNode>(parsed.Root);
        Assert.Equal("HasValue", chain.Links[1].Name);
        var args = chain.Links[1].Args!;
        Assert.Equal(2, args.Count);
        Assert.All(args, a => Assert.Null(Assert.IsType<LiteralNode>(a).Type));
        Assert.Equal("iv2_tabs_ideagroups", ((LiteralNode)args[1]).Value);
    }

    [Fact]
    public void Parse_QuotedTextWithCommaAndParens()
    {
        var call = Assert.IsType<CallNode>(DataExpressionParser.Parse("[EqualTo_string(X.GetKey, 'a,(b)')]").Root);

        Assert.Equal(2, call.Args.Count);
        Assert.Equal("a,(b)", Assert.IsType<LiteralNode>(call.Args[1]).Value);
    }

    [Fact]
    public void Parse_UnknownTokensBecomeRaw()
    {
        var parsed = DataExpressionParser.Parse("[And($param$, @value)]");

        Assert.Empty(parsed.Errors);
        var call = Assert.IsType<CallNode>(parsed.Root);
        Assert.Equal("$param$", Assert.IsType<RawNode>(call.Args[0]).Text);
        Assert.Equal("@value", Assert.IsType<RawNode>(call.Args[1]).Text);
    }

    [Fact]
    public void Parse_MissingClose_IsRawWithError()
    {
        var parsed = DataExpressionParser.Parse("[And(A.B, Not(C.D)]");

        Assert.Contains(parsed.Errors, e => e.Kind == ExprErrorKind.MissingClose);
        Assert.Equal("And(A.B, Not(C.D)", Assert.IsType<RawNode>(parsed.Root).Text);
    }

    [Fact]
    public void Parse_UnclosedQuote_IsRawWithError()
    {
        var parsed = DataExpressionParser.Parse("[EqualTo_string(A.B, 'x)]");

        Assert.Contains(parsed.Errors, e => e.Kind == ExprErrorKind.UnclosedQuote);
        Assert.IsType<RawNode>(parsed.Root);
    }

    [Fact]
    public void Parse_NoBrackets_HasNoRoot()
    {
        var parsed = DataExpressionParser.Parse("yes");

        Assert.Null(parsed.Root);
        Assert.Equal(string.Empty, parsed.Prefix);
        Assert.Equal(string.Empty, parsed.Suffix);
        Assert.False(parsed.IsBracketed);
    }

    [Fact]
    public void Write_Unchanged_KeepsSpacingAndLineBreaks()
    {
        const string text = "[And( A.B ,\n\tC.D )]";
        var parsed = DataExpressionParser.Parse(text);

        Assert.Equal(text, DataExpressionWriter.WriteFull(parsed.Prefix, parsed.Root, parsed.Suffix, parsed.Original));
        var example = DataExpressionParser.Parse(Example);
        Assert.Equal(Example, DataExpressionWriter.WriteFull(example.Prefix, example.Root, example.Suffix, example.Original));
    }

    [Fact]
    public void Write_ChangedNode_UsesVanillaStyle()
    {
        var parsed = DataExpressionParser.Parse("[And(A.B,C.D)]");
        var and = (CallNode)parsed.Root!;
        var or = new CallNode("Or", and.Args, null);

        Assert.Equal("[Or(A.B, C.D)]", DataExpressionWriter.WriteFull(parsed.Prefix, or, parsed.Suffix, parsed.Original));
        Assert.Equal("'(int32)5'", DataExpressionWriter.Write(new LiteralNode("int32", "5", null), string.Empty));
        Assert.Equal("'abc'", DataExpressionWriter.Write(new LiteralNode(null, "abc", null), string.Empty));
        var chain = new ChainNode(new[] { new ChainLink("X", null), new ChainLink("Has", new ExprNode[] { new LiteralNode(null, "a", null), or }) }, null);
        Assert.Equal("X.Has('a', Or(A.B, C.D))", DataExpressionWriter.Write(chain, parsed.Original));
        Assert.Equal("[A.B]", DataExpressionWriter.WriteFull(string.Empty, and.Args[0], string.Empty, parsed.Original));
    }
}
