using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.GuiDesign.Conditions;

public enum LogicOp
{
    And,
    Or,
}

/// <summary>Vergleichsfamilien des Spiels; die Funktion heißt <c>&lt;Art&gt;_&lt;Typ&gt;</c>, z. B. <c>GreaterThan_int32</c>.</summary>
public enum CompareKind
{
    EqualTo,
    NotEqualTo,
    LessThan,
    LessThanOrEqualTo,
    GreaterThan,
    GreaterThanOrEqualTo,
    Between,
    BetweenInclusive,
    BetweenInclusiveOfMin,
    BetweenInclusiveOfMax,
    IsOdd,
    IsEven,
}

/// <summary>
/// Knoten eines Bedingungsbaums. <see cref="Source"/> ist der eingelesene, unveränderte Ausdruck; der Writer kopiert dann dessen Originaltext.
/// <c>null</c> heißt neu oder geändert.
/// </summary>
public abstract record CondNode(ExprNode? Source);

/// <summary>UND bzw. ODER über beliebig viele Glieder (<c>And</c>…<c>And8</c>, <c>Or</c>…<c>Or8</c>).</summary>
public sealed record LogicNode(LogicOp Op, IReadOnlyList<CondNode> Items, ExprNode? Source) : CondNode(Source);

public sealed record NotNode(CondNode Inner, ExprNode? Source) : CondNode(Source);

/// <summary>Vergleich; <see cref="Variant"/> ist der Typ-Suffix (<c>int32</c>), <c>null</c> wenn noch keiner gewählt ist.</summary>
public sealed record CompareNode(CompareKind Kind, string? Variant, ExprNode Left, ExprNode? Right, ExprNode? Right2, ExprNode? Source) : CondNode(Source);

/// <summary>Andere globale Prüfung wie <c>IsDataModelEmpty(x)</c> oder <c>ObjectsEqual(a, b)</c>.</summary>
public sealed record CheckNode(string Name, IReadOnlyList<ExprNode> Args, ExprNode? Source) : CondNode(Source);

/// <summary>Kette oder Konstante als Bedingung (<c>AdvanceItem.IsResearched</c>).</summary>
public sealed record ValueNode(ExprNode Expr, ExprNode? Source) : CondNode(Source);

public sealed record RawCondNode(string Text, ExprNode? Source) : CondNode(Source);
