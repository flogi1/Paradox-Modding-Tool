using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

public sealed record GuiSceneBox(LayoutBox Box, int Depth, GuiWidgetCategory Category, string Label);

/// <summary>
/// Die schematische Vorschau als flache Liste von Kästen (Eltern vor Kindern) mit Trefferprüfung. Ein Klick wählt den Syntaxknoten des
/// Kastens; stammt der Kasten aus einem Typ in einer anderen Datei (oder aus einer synthetischen Typ-Instanz), den nächsten Vorfahren, der
/// in der eigenen Datei steht, und zuletzt <c>rootFallback</c>.
/// </summary>
public sealed class GuiSchematicScene
{
    private readonly Dictionary<LayoutBox, LayoutBox?> _parents = new();
    private readonly Func<GuiNode, bool> _isInDocument;
    private readonly GuiNode? _rootFallback;

    public GuiSchematicScene(LayoutBox root, Func<GuiNode, bool> isInDocument, GuiNode? rootFallback = null)
    {
        Root = root;
        _isInDocument = isInDocument;
        _rootFallback = rootFallback;
        var boxes = new List<GuiSceneBox>();
        Visit(root, null, 0, boxes);
        Boxes = boxes;

        var visible = boxes.Where(b => b.Box.Width > 0 && b.Box.Height > 0).ToList();
        if (visible.Count > 0)
        {
            Left = visible.Min(b => b.Box.X);
            Top = visible.Min(b => b.Box.Y);
            Width = visible.Max(b => b.Box.X + b.Box.Width) - Left;
            Height = visible.Max(b => b.Box.Y + b.Box.Height) - Top;
        }
    }

    public LayoutBox Root { get; }

    public IReadOnlyList<GuiSceneBox> Boxes { get; }

    public double Left { get; }

    public double Top { get; }

    public double Width { get; }

    public double Height { get; }

    /// <summary>Oberster (zuletzt gezeichneter) Kasten mit Fläche an dieser Stelle.</summary>
    public LayoutBox? HitTest(double x, double y)
    {
        for (var i = Boxes.Count - 1; i >= 0; i--)
        {
            var box = Boxes[i].Box;
            if (box.Width > 0 && box.Height > 0 && x >= box.X && x < box.X + box.Width && y >= box.Y && y < box.Y + box.Height)
            {
                return box;
            }
        }

        return null;
    }

    public GuiNode? SelectableNode(LayoutBox box)
    {
        for (LayoutBox? current = box; current is not null; current = _parents[current])
        {
            var node = current.Element.Origin.Node;
            if (_isInDocument(node) && IsAttached(node))
            {
                return node;
            }
        }

        return _rootFallback;
    }

    /// <summary>Alle Kästen eines Syntaxknotens (bei Listen und Rastern einer je simuliertem Eintrag).</summary>
    public IReadOnlyList<LayoutBox> BoxesFor(GuiNode node) =>
        Boxes.Where(b => ReferenceEquals(b.Box.Element.Origin.Node, node)).Select(b => b.Box).ToList();

    private void Visit(LayoutBox box, LayoutBox? parent, int depth, List<GuiSceneBox> boxes)
    {
        _parents[box] = parent;
        var element = box.Element;
        var label = element.Name is { Length: > 0 } name ? $"{element.Key} \"{name}\"" : element.Key;
        boxes.Add(new GuiSceneBox(box, depth, GuiWidgetKinds.CategoryOf(element.BaseWidget), label));
        foreach (var child in box.Children)
        {
            Visit(child, box, depth + 1, boxes);
        }
    }

    private static bool IsAttached(GuiNode node) =>
        node.Parent is null ? node.Kind == GuiNodeKind.Document : node.Parent.Children.Contains(node);
}
