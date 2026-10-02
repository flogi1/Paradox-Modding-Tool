namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>
/// Schreibt Ausdrucksknoten zurück: Knoten mit Textposition kopieren ihren Originaltext (Leerraum, Schreibweise), neue oder geänderte Knoten werden im
/// Vanilla-Stil geschrieben (<c>Name(a, b)</c>, Konstanten <c>'(int32)5'</c>).
/// </summary>
public static class DataExpressionWriter
{
    public static string Write(ExprNode node, string original)
    {
        if (node.Span is { } span)
        {
            return original.Substring(span.Start, span.Length);
        }

        return node switch
        {
            CallNode call => call.Name + "(" + Join(call.Args, original) + ")",
            ChainNode chain => string.Join('.', chain.Links.Select(link => link.Args is null ? link.Name : link.Name + "(" + Join(link.Args, original) + ")")),
            LiteralNode literal => literal.Type is null ? $"'{literal.Value}'" : $"'({literal.Type}){literal.Value}'",
            RawNode raw => raw.Text,
            _ => string.Empty,
        };
    }

    /// <summary>Hülle und Wurzel; ohne Hülle (neuer Ausdruck) mit <c>[</c> und <c>]</c>, ohne Wurzel leer.</summary>
    public static string WriteFull(string prefix, ExprNode? root, string suffix, string original)
    {
        if (root is null)
        {
            return string.Empty;
        }

        return prefix.Length == 0 && suffix.Length == 0
            ? "[" + Write(root, original) + "]"
            : prefix + Write(root, original) + suffix;
    }

    private static string Join(IEnumerable<ExprNode> args, string original) => string.Join(", ", args.Select(a => Write(a, original)));
}
