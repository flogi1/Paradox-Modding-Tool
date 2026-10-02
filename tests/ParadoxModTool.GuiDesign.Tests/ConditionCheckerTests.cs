using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public sealed class ConditionCheckerTests
{
    internal static DataTypeDatabase Db { get; } = new(new[]
    {
        Entry("AdvanceItem", string.Empty, DataTypeKind.Type, string.Empty),
        Entry("Country", string.Empty, DataTypeKind.Type, string.Empty),
        Entry("IsResearched", "AdvanceItem", DataTypeKind.Function, "bool"),
        Entry("GetPrerequisiteTotal", "AdvanceItem", DataTypeKind.Function, "int32"),
        Entry("GetCost", "AdvanceItem", DataTypeKind.Function, "CFixedPoint"),
        Entry("GetName", "AdvanceItem", DataTypeKind.Function, "CString"),
        Entry("GetPlayer", string.Empty, DataTypeKind.GlobalPromote, "Country"),
        Entry("IsDataModelEmpty", string.Empty, DataTypeKind.GlobalFunction, "bool", "Arg0"),
    });

    private static DataTypeEntry Entry(string name, string owner, DataTypeKind kind, string returns, params string[] args) =>
        new(name, owner, kind, args, returns, string.Empty, "test");

    private static IReadOnlyList<ConditionHint> Check(string text, DataTypeDatabase? db = null)
    {
        var parsed = DataExpressionParser.Parse(text);
        return ConditionChecker.Check(ConditionTree.From(parsed, ComparisonCatalog.BuiltIn), db ?? Db, null);
    }

    private static ExprNode Expr(string text) => DataExpressionParser.Parse("[" + text + "]").Root!;

    [Fact]
    public void TypeOf_ChainAndLiteral()
    {
        Assert.Equal("int32", ExpressionTypes.TypeOf(Expr("AdvanceItem.GetPrerequisiteTotal"), Db, null));
        Assert.Equal("Country", ExpressionTypes.TypeOf(Expr("GetPlayer"), Db, null));
        Assert.Equal("CFixedPoint", ExpressionTypes.TypeOf(Expr("'(CFixedPoint)1'"), Db, null));
        Assert.Equal("string", ExpressionTypes.TypeOf(Expr("'x'"), Db, null));
        Assert.Null(ExpressionTypes.TypeOf(Expr("Foo.Bar"), Db, null));
        Assert.Equal("bool", ExpressionTypes.TypeOf(Expr("IsResearched"), Db, "AdvanceItem"));
        Assert.Equal("bool", ExpressionTypes.TypeOf(Expr("IsDataModelEmpty(GetPlayer)"), Db, null));
    }

    [Fact]
    public void Check_UnknownName()
    {
        var hint = Assert.Single(Check("[AdvanceItem.IsResearchd]"));

        Assert.Equal(ConditionHintKind.UnknownName, hint.Kind);
        Assert.Equal("IsResearchd", hint.Detail);
        Assert.Equal(CondPath.Root, hint.Path);
    }

    [Fact]
    public void Check_TypeMismatch()
    {
        var hint = Assert.Single(Check("[GreaterThan_int32(AdvanceItem.GetPrerequisiteTotal, AdvanceItem.GetCost)]"));

        Assert.Equal(ConditionHintKind.TypeMismatch, hint.Kind);
        Assert.Equal("int32 ≠ CFixedPoint", hint.Detail);
    }

    [Fact]
    public void Check_MatchingTypesAndLiteral_AreFine()
    {
        Assert.Empty(Check("[GreaterThan_int32(AdvanceItem.GetPrerequisiteTotal, '(int32)0')]"));
        Assert.Empty(Check("[Not(AdvanceItem.IsResearched)]"));
    }

    [Fact]
    public void Check_NotBool()
    {
        var hint = Assert.Single(Check("[And(AdvanceItem.GetName, AdvanceItem.IsResearched)]"));

        Assert.Equal(ConditionHintKind.NotBool, hint.Kind);
        Assert.Equal(CondPath.Root.Child(0), hint.Path);
    }

    [Fact]
    public void Check_ArgumentCount()
    {
        Assert.Contains(Check("[And3(AdvanceItem.IsResearched, AdvanceItem.IsResearched)]"), h => h.Kind == ConditionHintKind.ArgumentCount);
        Assert.Contains(Check("[IsDataModelEmpty(GetPlayer, GetPlayer)]"), h => h.Kind == ConditionHintKind.ArgumentCount);
    }

    [Fact]
    public void Check_EmptyGroup_NoVariant_Raw()
    {
        Assert.Equal(ConditionHintKind.EmptyGroup, Assert.Single(ConditionChecker.Check(new LogicNode(LogicOp.And, Array.Empty<CondNode>(), null), Db, null)).Kind);
        var compare = new CompareNode(CompareKind.GreaterThan, null, Expr("AdvanceItem.GetCost"), Expr("'(CFixedPoint)1'"), null, null);
        Assert.Contains(ConditionChecker.Check(compare, Db, null), h => h.Kind == ConditionHintKind.NoVariant);
        Assert.Equal(ConditionHintKind.RawText, Assert.Single(ConditionChecker.Check(new RawCondNode("$p$", null), Db, null)).Kind);
    }

    [Fact]
    public void Check_EmptySideOrArgument_IsMissingValue()
    {
        var compare = new CompareNode(CompareKind.GreaterThan, "CFixedPoint", Expr("AdvanceItem.GetCost"), new RawNode(string.Empty, null), null, null);
        Assert.Contains(ConditionChecker.Check(compare, Db, null), h => h.Kind == ConditionHintKind.MissingValue);

        var check = new CheckNode("IsDataModelEmpty", new ExprNode[] { new RawNode(string.Empty, null) }, null);
        Assert.Contains(ConditionChecker.Check(check, Db, null), h => h.Kind == ConditionHintKind.MissingValue);

        var value = new ValueNode(new RawNode(string.Empty, null), null);
        Assert.Contains(ConditionChecker.Check(value, DataTypeDatabase.Empty, null), h => h.Kind == ConditionHintKind.MissingValue);
    }

    [Fact]
    public void Check_RawSide_IsRawText()
    {
        var compare = new CompareNode(CompareKind.GreaterThan, "int32", Expr("AdvanceItem.GetPrerequisiteTotal"), new RawNode("5", null), null, null);

        var hint = Assert.Single(ConditionChecker.Check(compare, Db, null));
        Assert.Equal(ConditionHintKind.RawText, hint.Kind);
        Assert.Equal("5", hint.Detail);
    }

    [Fact]
    public void Check_QuoteInConstant()
    {
        var compare = new CompareNode(CompareKind.EqualTo, "string", Expr("AdvanceItem.GetName"), new LiteralNode(null, "it's", null), null, null);

        Assert.Contains(ConditionChecker.Check(compare, Db, null), h => h.Kind == ConditionHintKind.QuoteInText && h.Detail == "it's");
    }

    [Fact]
    public void Check_CheckReturningNonBool_IsNotBool()
    {
        var db = new DataTypeDatabase(new[] { Entry("GetCount", string.Empty, DataTypeKind.GlobalFunction, "int32", "Arg0") });

        var hint = Assert.Single(ConditionChecker.Check(new CheckNode("GetCount", new[] { Expr("'x'") }, null), db, null));
        Assert.Equal(ConditionHintKind.NotBool, hint.Kind);
        Assert.Equal("int32", hint.Detail);
    }

    [Fact]
    public void Check_WithoutDump_SkipsNamesAndTypes()
    {
        Assert.Empty(Check("[AdvanceItem.IsResearchd]", DataTypeDatabase.Empty));
        Assert.Empty(Check("[GreaterThan_int32(AdvanceItem.GetPrerequisiteTotal, AdvanceItem.GetCost)]", DataTypeDatabase.Empty));
    }

    [Fact]
    public void Path_EqualityAndParent()
    {
        Assert.Equal(CondPath.Root.Child(1).Child(0), new CondPath(new[] { 1, 0 }));
        Assert.Equal(CondPath.Root.Child(1), CondPath.Root.Child(1).Child(0).Parent);
        Assert.Null(CondPath.Root.Parent);
    }
}
