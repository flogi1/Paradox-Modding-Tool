using ParadoxModTool.GuiDesign.Conditions;
using ParadoxModTool.GuiDesign.DataTypes;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Klartext der Bedingungen (deutscher Rückfall; die englischen Schlüssel prüft <see cref="GuiDataTextsTests"/>).</summary>
public sealed class ConditionPhrasesTests
{
    private static string Describe(string text) => ConditionPhrases.Describe(ConditionTreeTests.Read(text).Root).Replace("\r\n", "\n");

    private static ExprNode Expr(string text) => DataExpressionParser.Parse("[" + text + "]").Root!;

    [Fact]
    public void Leaf_SplitsNames()
    {
        Assert.Equal("AdvanceItem › Is Researched", ConditionPhrases.Leaf(Expr("AdvanceItem.IsResearched")));
        Assert.Equal("0", ConditionPhrases.Leaf(Expr("'(int32)0'")));
        Assert.Equal("Player › Has Embraced Institution (X › Self)", ConditionPhrases.Leaf(Expr("Player.HasEmbracedInstitution(X.Self)")));
    }

    [Fact]
    public void Describe_Not() => Assert.Equal("nicht AdvanceItem › Is Researched", Describe("[Not(AdvanceItem.IsResearched)]"));

    [Fact]
    public void Describe_NotCompare() =>
        Assert.Equal("nicht (AdvanceItem › Get Prerequisite Total > 0)", Describe("[Not(GreaterThan_int32(AdvanceItem.GetPrerequisiteTotal, '(int32)0'))]"));

    [Fact]
    public void Describe_AndTwoFlat() => Assert.Equal("A › Is X und B › Is Y", Describe("[And(A.IsX, B.IsY)]"));

    [Fact]
    public void Describe_OrTwoFlat() => Assert.Equal("A › Is X oder B › Is Y", Describe("[Or(A.IsX, B.IsY)]"));

    [Fact]
    public void Describe_AndThreeOrNested_IsMultiline() =>
        Assert.Equal(
            "alle:\n  A › Is X\n  nicht B › Is Y\n  eins von:\n    C › Is Z\n    D › Is W",
            Describe("[And3(A.IsX, Not(B.IsY), Or(C.IsZ, D.IsW))]"));

    [Fact]
    public void Describe_NotOverGroup()
    {
        Assert.Equal("nicht (A › Is X und B › Is Y)", Describe("[Not(And(A.IsX, B.IsY))]"));
        Assert.Equal("nicht:\n  alle:\n    A › Is X\n    B › Is Y\n    C › Is Z", Describe("[Not(And3(A.IsX, B.IsY, C.IsZ))]"));
    }

    [Theory]
    [InlineData("[GreaterThanOrEqualTo_int32(A.B, '(int32)1')]", "A › B ≥ 1")]
    [InlineData("[NotEqualTo_string(A.B, 'x')]", "A › B ≠ x")]
    [InlineData("[EqualTo_CFixedPoint(A.B, C.D)]", "A › B = C › D")]
    [InlineData("[LessThan_int32(A.B, '(int32)1')]", "A › B < 1")]
    [InlineData("[LessThanOrEqualTo_int32(A.B, '(int32)1')]", "A › B ≤ 1")]
    [InlineData("[Between_int32(A.B, '(int32)1', '(int32)5')]", "A › B zwischen 1 und 5 (ohne Grenzen)")]
    [InlineData("[BetweenInclusive_int32(A.B, '(int32)1', '(int32)5')]", "A › B zwischen 1 und 5 (mit Grenzen)")]
    [InlineData("[BetweenInclusiveOfMin_int32(A.B, '(int32)1', '(int32)5')]", "A › B zwischen 1 und 5 (mit Untergrenze)")]
    [InlineData("[BetweenInclusiveOfMax_int32(A.B, '(int32)1', '(int32)5')]", "A › B zwischen 1 und 5 (mit Obergrenze)")]
    [InlineData("[IsOdd_int32(A.B)]", "A › B ist ungerade")]
    [InlineData("[IsEven_int32(A.B)]", "A › B ist gerade")]
    public void Describe_Comparisons(string text, string expected) => Assert.Equal(expected, Describe(text));

    [Theory]
    [InlineData("[IsDataModelEmpty(X.L)]", "X › L ist leer")]
    [InlineData("[DataModelHasItems(X.L)]", "X › L hat Einträge")]
    [InlineData("[ObjectsEqual(A.B, C.D)]", "A › B ist gleich C › D")]
    [InlineData("[StringIsEmpty(A.B)]", "A › B ist leerer Text")]
    [InlineData("[StringContains(A.B, 'x')]", "A › B enthält x")]
    [InlineData("[StartsWith(A.B, 'x')]", "A › B beginnt mit x")]
    [InlineData("[Foo(A.B)]", "Foo(A › B)")]
    public void Describe_KnownChecks(string text, string expected) => Assert.Equal(expected, Describe(text));

    [Fact]
    public void Describe_Raw() => Assert.Equal("$p$", ConditionPhrases.Describe(new RawCondNode("$p$", null)));

    [Fact]
    public void LabelsAndHints()
    {
        var root = (LogicNode)ConditionTreeTests.Read("[And(Not(A.B), GreaterThan_int32(A.B, '(int32)0'))]").Root!;

        Assert.Equal("UND", ConditionPhrases.Label(root));
        Assert.Equal("NICHT", ConditionPhrases.Label(root.Items[0]));
        Assert.Equal(">", ConditionPhrases.Label(root.Items[1]));
        Assert.Equal("A › B > 0", ConditionPhrases.Short(root.Items[1]));
        Assert.Equal("größer als", ConditionPhrases.KindText(CompareKind.GreaterThan));
        Assert.Equal("Unbekannter Name: Foo", ConditionPhrases.Hint(new ConditionHint(CondPath.Root, ConditionHintKind.UnknownName, "Foo")));
    }
}
