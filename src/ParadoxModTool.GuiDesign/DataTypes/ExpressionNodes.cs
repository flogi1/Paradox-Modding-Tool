using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>
/// Knoten eines GUI-Datenausdrucks wie <c>[And(Not(Country.IsPlayer), GreaterThan_int32(X.GetCount, '(int32)0'))]</c>. <see cref="Span"/> zeigt in den
/// Originaltext (<see cref="ParsedExpression.Original"/>); <c>null</c> heißt: neu oder geändert, der Writer schreibt ihn neu statt ihn zu kopieren.
/// </summary>
public abstract record ExprNode(TextSpan? Span);

/// <summary>Globaler Aufruf ohne Punkt: <c>And(a, b)</c>, <c>GreaterThan_int32(x, y)</c>.</summary>
public sealed record CallNode(string Name, IReadOnlyList<ExprNode> Args, TextSpan? Span) : ExprNode(Span);

/// <summary>Glied einer Punkt-Kette; <see cref="Args"/> ist <c>null</c>, wenn das Glied ohne Klammern steht.</summary>
public sealed record ChainLink(string Name, IReadOnlyList<ExprNode>? Args);

/// <summary>Punkt-Kette <c>AdvanceItem.GetInstitution.Self</c>; auch ein einzelner Name ohne Klammern.</summary>
public sealed record ChainNode(IReadOnlyList<ChainLink> Links, TextSpan? Span) : ExprNode(Span);

/// <summary>Konstante in Hochkommas: <c>'(int32)0'</c> (Typ <c>int32</c>, Wert <c>0</c>) oder <c>'text'</c> (ohne Typ).</summary>
public sealed record LiteralNode(string? Type, string Value, TextSpan? Span) : ExprNode(Span);

/// <summary>Nicht verstandener Text (<c>$param$</c>, <c>@konstante</c>, kaputter Ausdruck); wird unverändert zurückgeschrieben.</summary>
public sealed record RawNode(string Text, TextSpan? Span) : ExprNode(Span);

public enum ExprErrorKind
{
    MissingClose,
    UnclosedQuote,
    Unexpected,
}

/// <summary>Strukturfehler an einer Stelle im Originaltext.</summary>
public sealed record ExprError(int Position, ExprErrorKind Kind);

/// <summary>
/// Ergebnis von <see cref="DataExpressionParser.Parse"/>: Hülle vor und nach dem Ausdruck (<c>"[</c> … <c>]"</c>, samt Leerraum), Wurzel und Fehler.
/// Ohne <c>[...]</c> ist <see cref="Root"/> <c>null</c> und die Hülle leer.
/// </summary>
public sealed record ParsedExpression(string Original, string Prefix, ExprNode? Root, string Suffix, IReadOnlyList<ExprError> Errors)
{
    public bool IsBracketed => Prefix.Contains('[');
}
