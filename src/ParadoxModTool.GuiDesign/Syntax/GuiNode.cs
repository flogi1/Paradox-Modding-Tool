namespace ParadoxModTool.GuiDesign.Syntax;

public enum GuiNodeKind
{
    Document,
    TypesContainer,
    TypeDefinition,
    TemplateDefinition,
    KeyedBlock,
    Property,
    ListItem,
    Block,
    BlockOverride,
    Using,
    ConstantDefinition,
    Comment,
}

/// <summary>
/// Knoten des GUI-Syntaxbaums. Jeder Knoten kennt seine exakte Textspanne. Der Baum wird nie zurück in Text geschrieben;
/// Änderungen laufen als Textersetzungen an diesen Spannen (siehe <c>GuiTextEdits</c>).
/// </summary>
public sealed class GuiNode
{
    public GuiNode(GuiNodeKind kind) => Kind = kind;

    public GuiNodeKind Kind { get; }

    /// <summary>
    /// Schlüssel (Property, KeyedBlock), Name (types, type, template, block, blockoverride), Name ohne "@" (Konstante), "using" (Using),
    /// Kommentartext mit "#" (Comment). Leer bei ListItem und namenlosem Block.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Basis bei <c>type name = basis { … }</c>.</summary>
    public string? BaseName { get; set; }

    /// <summary>Skalarwert ohne Anführungszeichen (Property, ListItem, Ziel von Using, Konstante).</summary>
    public string? Value { get; set; }

    /// <summary>Wert (bei ListItem) bzw. Name (bei block/blockoverride/KeyedBlock) stand in Anführungszeichen.</summary>
    public bool IsQuoted { get; set; }

    public string Operator { get; set; } = "=";

    public TextSpan Span { get; set; }

    public TextSpan NameSpan { get; set; }

    /// <summary>Spanne des Skalarwerts inklusive Anführungszeichen. Null, wenn der Wert fehlt oder ein Block ist.</summary>
    public TextSpan? ValueSpan { get; set; }

    /// <summary>Offset der "{", sonst -1.</summary>
    public int OpenBrace { get; set; } = -1;

    /// <summary>Offset der "}", -1 wenn sie fehlt (Diagnose) oder der Knoten keinen Block hat.</summary>
    public int CloseBrace { get; set; } = -1;

    public int Line { get; set; }

    public GuiNode? Parent { get; set; }

    public List<GuiNode> Children { get; } = new();

    public bool HasBody => OpenBrace >= 0 || Kind == GuiNodeKind.Document;

    /// <summary>Inhalt zwischen den Klammern. Fehlt die schließende Klammer, reicht er bis zum Ende der Knotenspanne.</summary>
    public TextSpan BodySpan => Kind == GuiNodeKind.Document
        ? Span
        : new TextSpan(OpenBrace + 1, CloseBrace >= 0 ? CloseBrace : Span.End);

    public GuiNode Root
    {
        get
        {
            var node = this;
            while (node.Parent is not null)
            {
                node = node.Parent;
            }

            return node;
        }
    }

    /// <summary>Alle Nachfahren in Textreihenfolge (Tiefensuche, ohne Rekursion).</summary>
    public IEnumerable<GuiNode> Descendants()
    {
        var stack = new Stack<GuiNode>();
        for (var i = Children.Count - 1; i >= 0; i--)
        {
            stack.Push(Children[i]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (var i = node.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(node.Children[i]);
            }
        }
    }

    public IEnumerable<GuiNode> Ancestors()
    {
        for (var node = Parent; node is not null; node = node.Parent)
        {
            yield return node;
        }
    }

    public override string ToString() => $"{Kind} {Name} (Zeile {Line})";
}
