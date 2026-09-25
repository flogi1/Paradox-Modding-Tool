using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Vorschau des obersten Elements (Plan-Ruling 4). <c>Message</c> erklärt, warum es keine Szene gibt.</summary>
public sealed record GuiPreview(GuiNode Target, ResolvedElement? Element, GuiSchematicScene? Scene, string? Message)
{
    /// <summary>Aufgelöstes Element eines Syntaxknotens (erstes mit dieser Herkunft); für die Vorschau-Wurzel das Wurzelelement.</summary>
    public ResolvedElement? ElementFor(GuiNode node)
    {
        if (Element is null)
        {
            return null;
        }

        if (ReferenceEquals(node, Target))
        {
            return Element;
        }

        var stack = new Stack<ResolvedElement>();
        stack.Push(Element);
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            if (ReferenceEquals(element.Origin.Node, node))
            {
                return element;
            }

            for (var i = element.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(element.Children[i]);
            }
        }

        return null;
    }
}

public static class GuiPreviewBuilder
{
    /// <summary>Nächste umschließende Typ- oder Template-Definition, sonst das Widget auf oberster Dateiebene; null außerhalb davon.</summary>
    public static GuiNode? PreviewTarget(GuiNode node)
    {
        for (var current = node; current.Parent is not null; current = current.Parent)
        {
            if (current.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition)
            {
                return current;
            }

            if (current.Kind == GuiNodeKind.KeyedBlock && current.Parent.Kind == GuiNodeKind.Document)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Löst das Ziel auf und berechnet das Layout. Läuft im Hintergrund; <paramref name="createEngine"/> liefert je Lauf eine neue Engine.</summary>
    public static GuiPreview Build(
        GuiResolver resolver,
        Func<GuiLayoutEngine> createEngine,
        GuiDocument document,
        GuiNode target,
        LayoutOptions options,
        CancellationToken cancellation = default)
    {
        if (target.Kind == GuiNodeKind.TemplateDefinition)
        {
            return new GuiPreview(target, null, null, GuiTexts.PreviewTemplate);
        }

        var element = target.Kind == GuiNodeKind.TypeDefinition ? resolver.ResolveType(target.Name) : resolver.Resolve(target);
        if (element is null)
        {
            return new GuiPreview(target, null, null, GuiTexts.PreviewImpossible);
        }

        return Layout(element, target, document, createEngine, options, cancellation);
    }

    /// <summary>
    /// Layout eines schon aufgelösten Elements. Der Inspektor nutzt dieselben <see cref="ResolvedElement"/>-Objekte für Baum und Vorschau
    /// (ein Klick in der Vorschau findet so seinen Baumeintrag) und rechnet bei geänderten Vorschau-Schaltern nur das Layout neu.
    /// </summary>
    public static GuiPreview Layout(
        ResolvedElement element,
        GuiNode target,
        GuiDocument document,
        Func<GuiLayoutEngine> createEngine,
        LayoutOptions options,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var box = createEngine().Layout(element, options);
        cancellation.ThrowIfCancellationRequested();
        var scene = new GuiSchematicScene(box, node => ReferenceEquals(node.Root, document.Root), target);
        return new GuiPreview(target, element, scene, null);
    }
}
