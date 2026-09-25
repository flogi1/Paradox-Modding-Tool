namespace ParadoxModTool.GuiDesign.Syntax;

/// <summary>Geparste .gui-Datei: Originaltext, Wurzelknoten und Diagnosen.</summary>
public sealed class GuiDocument
{
    public GuiDocument(string text, GuiNode root, IReadOnlyList<GuiDiagnostic> diagnostics)
    {
        Text = text;
        Root = root;
        Diagnostics = diagnostics;
    }

    public string Text { get; }

    public GuiNode Root { get; }

    public IReadOnlyList<GuiDiagnostic> Diagnostics { get; }

    public string TextOf(TextSpan span) => Text.Substring(span.Start, span.Length);

    public string TextOf(GuiNode node) => TextOf(node.Span);

    /// <summary>Innerster Knoten, dessen Spanne die Position enthält (Cursor → Element). Null außerhalb aller Knoten.</summary>
    public GuiNode? FindDeepestAt(int offset)
    {
        GuiNode? found = null;
        var current = Root;
        while (true)
        {
            GuiNode? next = null;
            foreach (var child in current.Children)
            {
                if (child.Span.Contains(offset))
                {
                    next = child;
                    break;
                }

                if (child.Span.Start > offset)
                {
                    break;
                }
            }

            if (next is null)
            {
                return found;
            }

            found = next;
            current = next;
        }
    }
}
