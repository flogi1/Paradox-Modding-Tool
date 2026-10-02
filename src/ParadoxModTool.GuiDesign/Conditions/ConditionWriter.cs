using ParadoxModTool.GuiDesign.DataTypes;

namespace ParadoxModTool.GuiDesign.Conditions;

/// <summary>
/// Schreibt einen Bedingungsbaum als GUI-Ausdruck. Unveränderte Knoten kommen textgleich aus dem Original; UND/ODER bekommen die Variante nach der
/// Gliederzahl (<c>And</c>, <c>And3</c> … <c>And8</c>, darüber verschachtelt), leere Gruppen entfallen.
/// </summary>
public static class ConditionWriter
{
    private const int MaxItems = 8;

    public static string Write(CondNode node, string original)
    {
        if (node.Source is { } source)
        {
            return DataExpressionWriter.Write(source, original);
        }

        return node switch
        {
            LogicNode logic => Group(logic.Op, logic.Items.Select(i => Write(i, original)).Where(t => t.Length > 0).ToList()),
            NotNode not => Write(not.Inner, original) is { Length: > 0 } inner ? "Not(" + inner + ")" : string.Empty,
            CompareNode compare => ComparisonCatalog.FunctionName(compare.Kind, compare.Variant ?? string.Empty) + "("
                + string.Join(", ", new[] { compare.Left, compare.Right, compare.Right2 }.OfType<ExprNode>().Select(e => DataExpressionWriter.Write(e, original)))
                + ")",
            CheckNode check => check.Name + "(" + string.Join(", ", check.Args.Select(a => DataExpressionWriter.Write(a, original))) + ")",
            ValueNode value => DataExpressionWriter.Write(value.Expr, original),
            RawCondNode raw => raw.Text,
            _ => string.Empty,
        };
    }

    /// <summary>Ganzer Ausdruck mit der Hülle des eingelesenen Werts (<c>"[</c> … <c>]"</c>), sonst <c>[</c> … <c>]</c>; ohne Inhalt leer.</summary>
    public static string WriteFull(ParsedExpression? loaded, CondNode? root)
    {
        if (root is null)
        {
            return string.Empty;
        }

        var text = Write(root, loaded?.Original ?? string.Empty);
        if (text.Length == 0)
        {
            return string.Empty;
        }

        return loaded is { IsBracketed: true } ? loaded.Prefix + text + loaded.Suffix : "[" + text + "]";
    }

    /// <summary>Fasst verschachtelte UND in UND bzw. ODER in ODER zu einer Gruppe zusammen.</summary>
    public static CondNode Merge(CondNode node)
    {
        switch (node)
        {
            case LogicNode logic:
                var changed = false;
                var items = new List<CondNode>();
                foreach (var item in logic.Items)
                {
                    var merged = Merge(item);
                    changed |= !ReferenceEquals(merged, item);
                    if (merged is LogicNode inner && inner.Op == logic.Op)
                    {
                        items.AddRange(inner.Items);
                        changed = true;
                    }
                    else
                    {
                        items.Add(merged);
                    }
                }

                return changed ? new LogicNode(logic.Op, items, null) : logic;
            case NotNode not:
                var mergedInner = Merge(not.Inner);
                return ReferenceEquals(mergedInner, not.Inner) ? not : new NotNode(mergedInner, null);
            default:
                return node;
        }
    }

    private static string Group(LogicOp op, IReadOnlyList<string> items)
    {
        if (items.Count <= 1)
        {
            return items.Count == 0 ? string.Empty : items[0];
        }

        if (items.Count > MaxItems)
        {
            items = items.Take(MaxItems - 1).Append(Group(op, items.Skip(MaxItems - 1).ToList())).ToList();
        }

        var name = op.ToString() + (items.Count == 2 ? string.Empty : items.Count.ToString());
        return name + "(" + string.Join(", ", items) + ")";
    }
}
