using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.GuiDesign.Conditions;

/// <summary>Baut aus einem eingelesenen Ausdruck den Bedingungsbaum; jeder Knoten behält seinen Ausdruck als <see cref="CondNode.Source"/>.</summary>
public static partial class ConditionTree
{
    public static CondNode? From(ParsedExpression parsed, ComparisonCatalog catalog) =>
        parsed.Root is null ? null : Node(parsed.Root, catalog, detached: false);

    /// <summary>Eingetippte Bedingung ohne Hülle als Baum ohne Textbezug (für Rohtext, der wieder gültig wurde); leer oder kaputt: <c>null</c>.</summary>
    public static CondNode? FromText(string text, ComparisonCatalog catalog)
    {
        if (text.Trim().Length == 0)
        {
            return null;
        }

        var parsed = DataExpressionParser.Parse("[" + text + "]");
        return parsed.Errors.Count > 0 || parsed.Root is null ? null : Node(DataExpressionParser.Detach(parsed.Root), catalog, detached: true);
    }

    private static CondNode Node(ExprNode expr, ComparisonCatalog catalog, bool detached)
    {
        var node = Build(expr, catalog, detached);
        return detached ? node with { Source = null } : node;
    }

    private static CondNode Build(ExprNode expr, ComparisonCatalog catalog, bool detached)
    {
        CondNode Child(ExprNode e) => Node(e, catalog, detached);
        switch (expr)
        {
            case CallNode { Name: "Not", Args.Count: 1 } not:
                return new NotNode(Child(not.Args[0]), expr);
            case CallNode call when LogicName().Match(call.Name) is { Success: true } logic:
                var op = logic.Groups["op"].Value == "And" ? LogicOp.And : LogicOp.Or;
                return new LogicNode(op, call.Args.Select(Child).ToArray(), expr);
            case CallNode call when catalog.TryParse(call.Name, out var kind, out var variant) && call.Args.Count == ComparisonCatalog.Arity(kind):
                return new CompareNode(
                    kind,
                    variant,
                    call.Args[0],
                    call.Args.Count > 1 ? call.Args[1] : null,
                    call.Args.Count > 2 ? call.Args[2] : null,
                    expr);
            case CallNode call:
                return new CheckNode(call.Name, call.Args, expr);
            case RawNode raw:
                return new RawCondNode(raw.Text, expr);
            default:
                return new ValueNode(expr, expr);
        }
    }

    [GeneratedRegex("^(?<op>And|Or)[2-8]?$")]
    private static partial Regex LogicName();
}
