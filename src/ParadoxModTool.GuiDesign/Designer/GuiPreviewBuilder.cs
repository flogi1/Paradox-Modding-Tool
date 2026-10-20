using ParadoxModTool.GuiDesign.Index;
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
    /// <summary>Nächste umschließende Typ- oder Template-Definition, sonst das Widget auf oberster Dateiebene; null außerhalb davon (Jomini).</summary>
    public static GuiNode? PreviewTarget(GuiNode node) => PreviewTarget(node, GuiLayout.Default);

    /// <summary>
    /// Wie <see cref="PreviewTarget(GuiNode)"/>; klassisch (HOI IV, EU IV) das Fenster direkt unter <c>guiTypes</c> (beliebige Schreibweise) auf oberster
    /// Dateiebene, das den Knoten enthält oder selbst ist. Außerhalb eines Fensters (z.B. in <c>spriteTypes</c>) null.
    /// </summary>
    public static GuiNode? PreviewTarget(GuiNode node, GuiLayout layout)
    {
        if (layout.IsClassic)
        {
            for (var current = node; current.Parent is not null; current = current.Parent)
            {
                if (current.Kind == GuiNodeKind.KeyedBlock
                    && current.Parent is { Kind: GuiNodeKind.KeyedBlock, Parent.Kind: GuiNodeKind.Document } holder
                    && string.Equals(holder.Name, "guiTypes", StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }
            }

            return null;
        }

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

    /// <summary>
    /// Löst das Ziel auf und berechnet das Layout. Läuft im Hintergrund; <paramref name="createEngine"/> liefert je Lauf eine neue Engine.
    /// <paramref name="viewedFile"/> (angesehene Datei) und das Ziel als Vorschau-Wurzel bilden den Anfang der Schlüssel bedingter Elemente.
    /// </summary>
    public static GuiPreview Build(
        GuiResolver resolver,
        Func<IGuiLayoutEngine> createEngine,
        GuiDocument document,
        GuiNode target,
        LayoutOptions options,
        CancellationToken cancellation = default,
        string? viewedFile = null)
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

        return Layout(element, target, document, createEngine, options, cancellation, viewedFile);
    }

    /// <summary>
    /// Layout eines schon aufgelösten Elements. Der Inspektor nutzt dieselben <see cref="ResolvedElement"/>-Objekte für Baum und Vorschau
    /// (ein Klick in der Vorschau findet so seinen Baumeintrag) und rechnet bei geänderten Vorschau-Schaltern nur das Layout neu.
    /// <paramref name="viewedFile"/> und <paramref name="target"/> gehen als angesehene Datei und Vorschau-Wurzel in die Optionen (Schlüssel bedingter Elemente).
    /// </summary>
    public static GuiPreview Layout(
        ResolvedElement element,
        GuiNode target,
        GuiDocument document,
        Func<IGuiLayoutEngine> createEngine,
        LayoutOptions options,
        CancellationToken cancellation = default,
        string? viewedFile = null)
    {
        cancellation.ThrowIfCancellationRequested();
        var box = createEngine().Layout(element, options.With(viewedFile ?? options.ViewedFile, target));
        cancellation.ThrowIfCancellationRequested();
        var scene = new GuiSchematicScene(box, node => ReferenceEquals(node.Root, document.Root), target);
        return new GuiPreview(target, element, scene, null);
    }
}
