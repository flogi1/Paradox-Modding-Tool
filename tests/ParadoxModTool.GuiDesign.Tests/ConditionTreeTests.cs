using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class ConditionTreeTests
{
    internal const string Example =
        "[And(Not(AdvanceItem.IsResearched), Not(EqualTo_int32(AdvanceItem.GetPrerequisiteTotal, '(int32)0')))]";

    internal static (ParsedExpression Parsed, CondNode? Root) Read(string text)
    {
        var parsed = DataExpressionParser.Parse(text);
        return (parsed, ConditionTree.From(parsed, ComparisonCatalog.BuiltIn));
    }

    internal static ValueNode Value(string chain) =>
        new(new ChainNode(chain.Split('.').Select(n => new ChainLink(n, null)).ToArray(), null), null);

    [Fact]
    public void From_Example_MapsNodes()
    {
        var and = Assert.IsType<LogicNode>(Read(Example).Root);

        Assert.Equal(LogicOp.And, and.Op);
        Assert.IsType<ValueNode>(Assert.IsType<NotNode>(and.Items[0]).Inner);
        var compare = Assert.IsType<CompareNode>(Assert.IsType<NotNode>(and.Items[1]).Inner);
        Assert.Equal(CompareKind.EqualTo, compare.Kind);
        Assert.Equal("int32", compare.Variant);
        Assert.IsType<LiteralNode>(compare.Right);
    }

    [Fact]
    public void From_AndN_And_OrN()
    {
        Assert.Equal(3, Assert.IsType<LogicNode>(Read("[And3(A.B, C.D, E.F)]").Root).Items.Count);
        var or = Assert.IsType<LogicNode>(Read("[Or5(A.B, C.D, E.F, G.H, I.J)]").Root);
        Assert.Equal(LogicOp.Or, or.Op);
        Assert.Equal(5, or.Items.Count);
    }

    [Fact]
    public void From_LessThanOrEqualTo_IsNotLessThan()
    {
        var compare = Assert.IsType<CompareNode>(Read("[LessThanOrEqualTo_CFixedPoint(A.B, C.D)]").Root);

        Assert.Equal(CompareKind.LessThanOrEqualTo, compare.Kind);
        Assert.Equal("CFixedPoint", compare.Variant);
    }

    [Fact]
    public void From_BetweenHasTwoRightSides()
    {
        var compare = Assert.IsType<CompareNode>(Read("[Between_int32(A.B, '(int32)1', '(int32)5')]").Root);

        Assert.Equal(CompareKind.Between, compare.Kind);
        Assert.Equal("1", Assert.IsType<LiteralNode>(compare.Right).Value);
        Assert.Equal("5", Assert.IsType<LiteralNode>(compare.Right2).Value);
    }

    [Fact]
    public void From_IsOddHasOnlyLeft()
    {
        var compare = Assert.IsType<CompareNode>(Read("[IsOdd_int32(A.B)]").Root);

        Assert.Equal(CompareKind.IsOdd, compare.Kind);
        Assert.Null(compare.Right);
        Assert.Null(compare.Right2);
    }

    [Fact]
    public void From_OtherBoolFunction_IsCheck()
    {
        var check = Assert.IsType<CheckNode>(Read("[IsDataModelEmpty(X.GetList)]").Root);

        Assert.Equal("IsDataModelEmpty", check.Name);
        Assert.Single(check.Args);
    }

    [Fact]
    public void From_WrongArity_IsCheck()
    {
        Assert.Equal("GreaterThan_int32", Assert.IsType<CheckNode>(Read("[GreaterThan_int32(A.B)]").Root).Name);
    }

    [Fact]
    public void Write_Unchanged_IsIdentical()
    {
        var (parsed, root) = Read(Example);

        Assert.Equal(Example, ConditionWriter.WriteFull(parsed, root));
    }

    [Fact]
    public void Write_SiblingChanged_KeepsOtherTextIdentical()
    {
        var (parsed, root) = Read("[And(Not( AdvanceItem.IsResearched ), Not(C.D))]");
        var and = (LogicNode)root!;
        var changed = and with { Items = new CondNode[] { and.Items[0], Value("E.F") }, Source = null };

        Assert.Equal("[And(Not( AdvanceItem.IsResearched ), E.F)]", ConditionWriter.WriteFull(parsed, changed));
    }

    [Theory]
    [InlineData(1, "[A.B1]")]
    [InlineData(2, "[And(A.B1, A.B2)]")]
    [InlineData(3, "[And3(A.B1, A.B2, A.B3)]")]
    [InlineData(8, "[And8(A.B1, A.B2, A.B3, A.B4, A.B5, A.B6, A.B7, A.B8)]")]
    [InlineData(9, "[And8(A.B1, A.B2, A.B3, A.B4, A.B5, A.B6, A.B7, And(A.B8, A.B9))]")]
    [InlineData(12, "[And8(A.B1, A.B2, A.B3, A.B4, A.B5, A.B6, A.B7, And5(A.B8, A.B9, A.B10, A.B11, A.B12))]")]
    public void Write_VariantByCount(int count, string expected)
    {
        var group = new LogicNode(LogicOp.And, Enumerable.Range(1, count).Select(i => (CondNode)Value($"A.B{i}")).ToArray(), null);

        Assert.Equal(expected, ConditionWriter.WriteFull(null, group));
    }

    [Fact]
    public void Write_EmptyGroupIsSkipped()
    {
        var group = new LogicNode(LogicOp.And, new CondNode[] { Value("A.B"), new LogicNode(LogicOp.Or, Array.Empty<CondNode>(), null) }, null);

        Assert.Equal("[A.B]", ConditionWriter.WriteFull(null, group));
        Assert.Equal(string.Empty, ConditionWriter.WriteFull(null, new LogicNode(LogicOp.Or, Array.Empty<CondNode>(), null)));
    }

    [Fact]
    public void Write_NotOverEmptyIsSkipped()
    {
        var emptyOr = new LogicNode(LogicOp.Or, Array.Empty<CondNode>(), null);

        Assert.Equal(string.Empty, ConditionWriter.WriteFull(null, new NotNode(emptyOr, null)));
        Assert.Equal(string.Empty, ConditionWriter.WriteFull(null, new NotNode(new ValueNode(new RawNode(string.Empty, null), null), null)));
        Assert.Equal("[X.Y]", ConditionWriter.WriteFull(null, new LogicNode(LogicOp.And, new CondNode[] { Value("X.Y"), new NotNode(emptyOr, null) }, null)));
    }

    [Fact]
    public void Write_NewCompareAndLiteral()
    {
        var compare = new CompareNode(CompareKind.GreaterThan, "CFixedPoint", Value("A.B").Expr, new LiteralNode("CFixedPoint", "1", null), null, null);

        Assert.Equal("[GreaterThan_CFixedPoint(A.B, '(CFixedPoint)1')]", ConditionWriter.WriteFull(null, compare));
        Assert.Equal("[Not(A.B)]", ConditionWriter.WriteFull(null, new NotNode(Value("A.B"), null)));
    }

    [Fact]
    public void Merge_FlattensSameOp()
    {
        var (parsed, root) = Read("[And(And(A.B, C.D), E.F)]");
        Assert.Equal("[And3(A.B, C.D, E.F)]", ConditionWriter.WriteFull(parsed, ConditionWriter.Merge(root!)));

        var (mixed, mixedRoot) = Read("[And(Or(A.B, C.D), E.F)]");
        Assert.Equal("[And(Or(A.B, C.D), E.F)]", ConditionWriter.WriteFull(mixed, ConditionWriter.Merge(mixedRoot!)));
    }

    [Fact]
    public void FromText_IsDetachedFromItsText()
    {
        var root = ConditionTree.FromText("And( A.B ,Not(C.D) )", ComparisonCatalog.BuiltIn);

        Assert.NotNull(root);
        Assert.Null(root!.Source);
        Assert.Equal("[And(A.B, Not(C.D))]", ConditionWriter.WriteFull(DataExpressionParser.Parse("[X.Y]"), root));
        Assert.Equal("[And(A.B, Not(C.D))]", ConditionWriter.WriteFull(null, root));
        Assert.Null(ConditionTree.FromText("And(A.B", ComparisonCatalog.BuiltIn));
        Assert.Null(ConditionTree.FromText("   ", ComparisonCatalog.BuiltIn));
    }

    [Fact]
    public void ParseDetached_ClearsSpans()
    {
        var node = DataExpressionParser.ParseDetached("X.Has( 'a' )");

        Assert.Equal("X.Has('a')", DataExpressionWriter.Write(node!, string.Empty));
        Assert.Null(DataExpressionParser.ParseDetached(""));
        Assert.IsType<RawNode>(DataExpressionParser.ParseDetached("(("));
    }

    [Fact]
    public void Catalog_FunctionNamesAndVariants()
    {
        Assert.Equal("GreaterThanOrEqualTo_int64", ComparisonCatalog.FunctionName(CompareKind.GreaterThanOrEqualTo, "int64"));
        Assert.Contains("string", ComparisonCatalog.BuiltIn.Variants(CompareKind.EqualTo));
        Assert.DoesNotContain("string", ComparisonCatalog.BuiltIn.Variants(CompareKind.GreaterThan));
        Assert.Equal(3, ComparisonCatalog.Arity(CompareKind.BetweenInclusiveOfMax));
        Assert.False(ComparisonCatalog.BuiltIn.TryParse("GreaterThan_bool", out _, out _));
        Assert.False(ComparisonCatalog.BuiltIn.TryParse("And", out _, out _));
    }

    [Fact]
    public void Catalog_FromDumpLearnsVariants()
    {
        var db = new DataTypeDatabase(new[]
        {
            new DataTypeEntry("GreaterThan_date", string.Empty, DataTypeKind.GlobalFunction, new[] { "Arg0", "Arg1" }, "bool", string.Empty, "x"),
            new DataTypeEntry("EqualTo_int32", string.Empty, DataTypeKind.GlobalFunction, new[] { "Arg0", "Arg1" }, "bool", string.Empty, "x"),
        });
        var catalog = ComparisonCatalog.From(db);

        Assert.True(catalog.TryParse("GreaterThan_date", out var kind, out var variant));
        Assert.Equal((CompareKind.GreaterThan, "date"), (kind, variant));
        Assert.Equal(new[] { "int32" }, catalog.Variants(CompareKind.EqualTo));
        Assert.Same(ComparisonCatalog.BuiltIn, ComparisonCatalog.From(DataTypeDatabase.Empty));
    }
}
