using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Glied der Brotkrumen-Leiste: Beschriftung wie im Elementbaum und der Knoten, zu dem ein Klick springt.</summary>
public sealed record GuiCrumb(string Label, GuiNode Node);

/// <summary>
/// Element an einer Textstelle und der Pfad dorthin. Gemeinsam genutzt von Brotkrumen-Leiste, "Inspizieren" im Code und den Fundstellen
/// von "Wird angezeigt in (GUI)". Eigenschaften und Block-Eigenschaften (<c>size = { … }</c>) sind keine Elemente.
/// </summary>
public static class GuiElementPath
{
    /// <summary>Innerstes Element an der Stelle, das der Inspektor zeigen kann (Typ- oder Template-Definition, Widget, Widget-Halter); null außerhalb.</summary>
    public static GuiNode? ElementAt(GuiDocument document, int offset, GuiResolver resolver)
    {
        for (var node = NodeAt(document, offset); node is not null; node = node.Parent)
        {
            if (IsInspectable(node, resolver))
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Element an einer Zeile (1-basiert, z.B. aus dem Aufruf des Spiels; Plan 4 Ruling 4): das am ersten Zeichen der Zeile, das kein Leerraum ist.
    /// Zeilen hinter dem Ende gelten als letzte Zeile; eine Zeile unter 1 ergibt null.
    /// </summary>
    public static GuiNode? ElementAtLine(GuiDocument document, int line, GuiResolver resolver)
    {
        if (line < 1)
        {
            return null;
        }

        var text = document.Text;
        var offset = 0;
        for (var current = 1; current < line; current++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0)
            {
                break;
            }

            offset = next + 1;
        }

        while (offset < text.Length && text[offset] is ' ' or '\t' or '\r')
        {
            offset++;
        }

        return ElementAt(document, offset, resolver);
    }

    public static bool IsInspectable(GuiNode node, GuiResolver resolver) =>
        node.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition
        || (node.Kind == GuiNodeKind.KeyedBlock && resolver.Classify(node) is GuiRole.TypeInstance or GuiRole.EngineWidget or GuiRole.WidgetHolder);

    /// <summary>Pfad von der obersten Ebene bis zum innersten Element an der Stelle, mit <c>types</c>-Containern und Blöcken; leer außerhalb.</summary>
    public static IReadOnlyList<GuiCrumb> CrumbsAt(GuiDocument document, int offset, GuiResolver resolver)
    {
        var crumbs = new List<GuiCrumb>();
        for (var node = NodeAt(document, offset); node is not null; node = node.Parent)
        {
            if (node.Kind is GuiNodeKind.TypesContainer or GuiNodeKind.Block or GuiNodeKind.BlockOverride || IsInspectable(node, resolver))
            {
                crumbs.Add(new GuiCrumb(GuiElementTree.LabelOf(node), node));
            }
        }

        crumbs.Reverse();
        return crumbs;
    }

    /// <summary>Innerster Knoten an der Stelle; eine Stelle hinter dem Textende zählt als letztes Zeichen (Cursor am Dateiende).</summary>
    private static GuiNode? NodeAt(GuiDocument document, int offset) =>
        document.Text.Length == 0 ? null : document.FindDeepestAt(Math.Clamp(offset, 0, document.Text.Length - 1));
}
