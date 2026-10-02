using System.Text;
using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.GuiDesign.Conditions;

/// <summary>
/// Klartext für Bedingungen aus festen Satzvorlagen (<see cref="GuiDataTexts"/>): Logik und Vergleiche als Wörter bzw. Symbole, Blätter aus dem Namen lesbar
/// gemacht (<c>AdvanceItem.IsResearched</c> → "AdvanceItem › Is Researched"). Lange oder verschachtelte Gruppen werden mehrzeilig eingerückt.
/// </summary>
public static class ConditionPhrases
{
    private const string Indent = "  ";

    public static string Describe(CondNode? root) => root is null ? string.Empty : string.Join(Environment.NewLine, Lines(root, nested: false));

    /// <summary>Etikett einer Baumzeile: UND, ODER, NICHT, Vergleichssymbol, Name der Prüfung, Wert oder Rohtext.</summary>
    public static string Label(CondNode node) => node switch
    {
        LogicNode { Op: LogicOp.And } => GuiDataTexts.LabelAnd,
        LogicNode => GuiDataTexts.LabelOr,
        NotNode => GuiDataTexts.LabelNot,
        CompareNode compare => Symbol(compare.Kind),
        CheckNode check => check.Name,
        ValueNode => GuiDataTexts.LabelValue,
        _ => GuiDataTexts.LabelRaw,
    };

    /// <summary>Einzeiliger Text neben dem Etikett; bei Gruppen und NICHT leer, deren Inhalt steht im Baum darunter.</summary>
    public static string Short(CondNode node) => node is LogicNode or NotNode ? string.Empty : Inline(node);

    public static string Leaf(ExprNode node) => node switch
    {
        ChainNode chain => string.Join(" › ", chain.Links.Select((link, i) => (i == 0 ? link.Name : Words(link.Name)) + Arguments(link.Args))),
        CallNode call => call.Name + "(" + string.Join(", ", call.Args.Select(Leaf)) + ")",
        LiteralNode literal => literal.Value,
        RawNode raw => raw.Text,
        _ => string.Empty,
    };

    public static string KindText(CompareKind kind) => kind switch
    {
        CompareKind.EqualTo => GuiDataTexts.KindEqualTo,
        CompareKind.NotEqualTo => GuiDataTexts.KindNotEqualTo,
        CompareKind.LessThan => GuiDataTexts.KindLessThan,
        CompareKind.LessThanOrEqualTo => GuiDataTexts.KindLessThanOrEqualTo,
        CompareKind.GreaterThan => GuiDataTexts.KindGreaterThan,
        CompareKind.GreaterThanOrEqualTo => GuiDataTexts.KindGreaterThanOrEqualTo,
        CompareKind.Between => GuiDataTexts.KindBetween,
        CompareKind.BetweenInclusive => GuiDataTexts.KindBetweenInclusive,
        CompareKind.BetweenInclusiveOfMin => GuiDataTexts.KindBetweenInclusiveOfMin,
        CompareKind.BetweenInclusiveOfMax => GuiDataTexts.KindBetweenInclusiveOfMax,
        CompareKind.IsOdd => GuiDataTexts.KindIsOdd,
        _ => GuiDataTexts.KindIsEven,
    };

    public static string Hint(ConditionHint hint) => hint.Kind switch
    {
        ConditionHintKind.UnknownName => GuiDataTexts.HintUnknownName(hint.Detail),
        ConditionHintKind.TypeMismatch => GuiDataTexts.HintTypeMismatch(hint.Detail),
        ConditionHintKind.NotBool => GuiDataTexts.HintNotBool(hint.Detail),
        ConditionHintKind.ArgumentCount => GuiDataTexts.HintArgumentCount(hint.Detail),
        ConditionHintKind.EmptyGroup => GuiDataTexts.HintEmptyGroup,
        ConditionHintKind.NoVariant => GuiDataTexts.HintNoVariant(hint.Detail),
        ConditionHintKind.MissingValue => GuiDataTexts.HintMissingValue,
        ConditionHintKind.QuoteInText => GuiDataTexts.HintQuoteInText(hint.Detail),
        _ => GuiDataTexts.HintRawText(hint.Detail),
    };

    /// <summary>Zeilen eines Knotens; eine Gruppe in einer mehrzeiligen Gruppe (<paramref name="nested"/>) ist selbst immer mehrzeilig.</summary>
    private static List<string> Lines(CondNode node, bool nested)
    {
        switch (node)
        {
            case LogicNode logic when logic.Items.Count == 0:
                return new List<string> { GuiDataTexts.CondEmpty };
            case LogicNode logic when logic.Items.Count == 1:
                return Lines(logic.Items[0], nested);
            case LogicNode logic when !nested && IsFlat(logic):
                return new List<string> { Inline(logic) };
            case LogicNode logic:
                var lines = new List<string> { logic.Op == LogicOp.And ? GuiDataTexts.CondAll : GuiDataTexts.CondAny };
                lines.AddRange(logic.Items.SelectMany(item => Lines(item, nested: true)).Select(l => Indent + l));
                return lines;
            case NotNode { Inner: LogicNode inner } when !(IsFlat(inner) || inner.Items.Count <= 1):
                var block = new List<string> { GuiDataTexts.CondNotBlock };
                block.AddRange(Lines(inner, nested: false).Select(l => Indent + l));
                return block;
            default:
                return new List<string> { Inline(node) };
        }
    }

    private static bool IsFlat(LogicNode logic) => logic.Items.Count == 2 && logic.Items.All(i => i is not LogicNode && i is not NotNode { Inner: LogicNode });

    private static string Inline(CondNode node) => node switch
    {
        LogicNode { Items.Count: 0 } => GuiDataTexts.CondEmpty,
        LogicNode { Items.Count: 1 } logic => Inline(logic.Items[0]),
        LogicNode logic => string.Join(
            " " + (logic.Op == LogicOp.And ? GuiDataTexts.CondAnd : GuiDataTexts.CondOr) + " ",
            logic.Items.Select(i => i is LogicNode or CompareNode ? "(" + Inline(i) + ")" : Inline(i))),
        NotNode not => GuiDataTexts.CondNot + " " + (not.Inner is CompareNode or LogicNode { Items.Count: > 1 } ? "(" + Inline(not.Inner) + ")" : Inline(not.Inner)),
        CompareNode compare => Compare(compare),
        CheckNode check => Check(check),
        ValueNode value => Leaf(value.Expr),
        RawCondNode raw => raw.Text,
        _ => string.Empty,
    };

    private static string Compare(CompareNode compare)
    {
        var left = Leaf(compare.Left);
        var right = compare.Right is null ? string.Empty : Leaf(compare.Right);
        var right2 = compare.Right2 is null ? string.Empty : Leaf(compare.Right2);
        return compare.Kind switch
        {
            CompareKind.IsOdd => GuiDataTexts.IsOdd(left),
            CompareKind.IsEven => GuiDataTexts.IsEven(left),
            CompareKind.Between => GuiDataTexts.Between(left, right, right2, GuiDataTexts.BoundsNone),
            CompareKind.BetweenInclusive => GuiDataTexts.Between(left, right, right2, GuiDataTexts.BoundsBoth),
            CompareKind.BetweenInclusiveOfMin => GuiDataTexts.Between(left, right, right2, GuiDataTexts.BoundsMin),
            CompareKind.BetweenInclusiveOfMax => GuiDataTexts.Between(left, right, right2, GuiDataTexts.BoundsMax),
            _ => $"{left} {Symbol(compare.Kind)} {right}",
        };
    }

    private static string Check(CheckNode check)
    {
        var args = check.Args.Select(Leaf).ToList();
        return (check.Name, args.Count) switch
        {
            ("IsDataModelEmpty", 1) => GuiDataTexts.CheckIsDataModelEmpty(args[0]),
            ("DataModelHasItems", 1) => GuiDataTexts.CheckDataModelHasItems(args[0]),
            ("ObjectsEqual", 2) => GuiDataTexts.CheckObjectsEqual(args[0], args[1]),
            ("StringIsEmpty", 1) => GuiDataTexts.CheckStringIsEmpty(args[0]),
            ("StringContains", 2) => GuiDataTexts.CheckStringContains(args[0], args[1]),
            ("StartsWith", 2) => GuiDataTexts.CheckStartsWith(args[0], args[1]),
            _ => check.Name + "(" + string.Join(", ", args) + ")",
        };
    }

    private static string Symbol(CompareKind kind) => kind switch
    {
        CompareKind.EqualTo => "=",
        CompareKind.NotEqualTo => "≠",
        CompareKind.LessThan => "<",
        CompareKind.LessThanOrEqualTo => "≤",
        CompareKind.GreaterThan => ">",
        CompareKind.GreaterThanOrEqualTo => "≥",
        CompareKind.IsOdd => GuiDataTexts.LabelOdd,
        CompareKind.IsEven => GuiDataTexts.LabelEven,
        _ => GuiDataTexts.LabelBetween,
    };

    private static string Arguments(IReadOnlyList<ExprNode>? args) => args is null ? string.Empty : " (" + string.Join(", ", args.Select(Leaf)) + ")";

    /// <summary><c>GetPrerequisiteTotal</c> → "Get Prerequisite Total": Leerzeichen vor jedem Großbuchstaben nach einem Kleinbuchstaben.</summary>
    private static string Words(string name)
    {
        var result = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
            {
                result.Append(' ');
            }

            result.Append(name[i]);
        }

        return result.ToString();
    }
}
