using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Ergebnis einer Designer-Aktion: Ersetzungen gegen den Text und die Stelle im neuen Text, an die der Cursor kommt (das Element dort ist
/// danach ausgewählt); null = Cursor bleibt.
/// </summary>
public sealed record GuiDesignEdit(IReadOnlyList<TextEdit> Edits, int? Focus)
{
    public static GuiDesignEdit None { get; } = new(Array.Empty<TextEdit>(), null);

    public bool IsEmpty => Edits.Count == 0;
}

/// <summary>Wohin ein Element kommt: unter <c>Parent</c> an <c>Position</c>, bei Before/After neben <c>Anchor</c>.</summary>
public sealed record GuiInsertion(GuiNode Parent, InsertPosition Position, GuiNode? Anchor);

/// <summary>Einfügen an der Auswahl, als Kind, davor, danach oder nach Ablageposition (Ziehen und Ablegen).</summary>
public enum GuiPlacement
{
    AtSelection,
    Child,
    Before,
    After,
    Drop,
}

/// <summary>
/// Designer-Aktionen als Ersetzungen gegen den aktuellen Text (Plan-Ruling 6, 8), mit den Regeln, wohin was eingefügt werden darf. Alle
/// Knoten müssen aus <c>document</c> stammen; Knoten aus einem älteren Parse holt <see cref="Rebind"/> vorher in das neue Dokument
/// (Plan-Ruling 11).
/// </summary>
public static class GuiDesignerEdits
{
    /// <summary>
    /// Derselbe Knoten im neu geparsten Dokument: per Pfad (Art, Name, Position unter gleichartigen Geschwistern) gefunden und mit unverändertem
    /// Text. Null, wenn der Knoten nicht aus <paramref name="shown"/> stammt, im neuen Dokument fehlt oder sein Text sich geändert hat (dann
    /// könnte der Pfad auf ein gleichnamiges anderes Element zeigen).
    /// </summary>
    public static GuiNode? Rebind(GuiNode node, GuiDocument shown, GuiDocument fresh)
    {
        if (!ReferenceEquals(node.Root, shown.Root))
        {
            return null;
        }

        if (node.Kind == GuiNodeKind.Document)
        {
            return fresh.Root;
        }

        return GuiNodePath.Of(node).FindExact(fresh) is { } found && shown.TextOf(node) == fresh.TextOf(found) ? found : null;
    }

    /// <summary>Nimmt Widgets als Kinder auf: Dateiebene, Typ- und Template-Definition, block, blockoverride, Widget, Widget-Halter.</summary>
    public static bool AcceptsWidgets(GuiNode node, GuiResolver resolver) => node.Kind switch
    {
        GuiNodeKind.Document or GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition or GuiNodeKind.Block or GuiNodeKind.BlockOverride => true,
        GuiNodeKind.KeyedBlock => node.HasBody && resolver.Classify(node) is GuiRole.TypeInstance or GuiRole.EngineWidget or GuiRole.WidgetHolder,
        _ => false,
    };

    /// <summary>Nimmt <c>using = template</c> auf: Definitionen, block, blockoverride, Widgets (nicht Dateiebene, types-Container, Halter).</summary>
    public static bool AcceptsUsing(GuiNode node, GuiResolver resolver) => node.Kind switch
    {
        GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition or GuiNodeKind.Block or GuiNodeKind.BlockOverride => true,
        GuiNodeKind.KeyedBlock => node.HasBody && resolver.Classify(node) is GuiRole.TypeInstance or GuiRole.EngineWidget,
        _ => false,
    };

    /// <summary>Eintrag des Elementbaums, den Baum-Aktionen bearbeiten: types-Container, Definitionen, Blöcke, Widgets, Halter.</summary>
    public static bool IsElement(GuiNode node, GuiResolver resolver) => node.Kind switch
    {
        GuiNodeKind.TypesContainer or GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition or GuiNodeKind.Block or GuiNodeKind.BlockOverride => true,
        GuiNodeKind.KeyedBlock => resolver.Classify(node) is GuiRole.TypeInstance or GuiRole.EngineWidget or GuiRole.WidgetHolder,
        _ => false,
    };

    /// <summary>
    /// Einfügestelle für ein neues Widget. Ohne Ziel (oder Dateiebene): ans Dateiende. <see cref="GuiPlacement.AtSelection"/>: als letztes
    /// Kind, wenn das Ziel Widgets aufnimmt, sonst direkt dahinter. <see cref="GuiPlacement.Drop"/>: oberes Viertel davor, unteres Viertel
    /// dahinter, Mitte wie AtSelection. Null, wenn an dieser Stelle kein Widget stehen darf.
    /// </summary>
    public static GuiInsertion? InsertionFor(GuiNode? target, GuiPlacement placement, double dropY, GuiDocument document, GuiResolver resolver)
    {
        if (target is null || target.Kind == GuiNodeKind.Document)
        {
            return new GuiInsertion(document.Root, InsertPosition.LastChild, null);
        }

        return placement switch
        {
            GuiPlacement.Child => AcceptsWidgets(target, resolver) ? new GuiInsertion(target, InsertPosition.LastChild, null) : null,
            GuiPlacement.Before => Beside(target, InsertPosition.Before, resolver),
            GuiPlacement.After => Beside(target, InsertPosition.After, resolver),
            GuiPlacement.Drop when dropY < 0.25 => Beside(target, InsertPosition.Before, resolver),
            GuiPlacement.Drop when dropY > 0.75 => Beside(target, InsertPosition.After, resolver),
            _ => AcceptsWidgets(target, resolver) ? new GuiInsertion(target, InsertPosition.LastChild, null) : Beside(target, InsertPosition.After, resolver),
        };
    }

    public static GuiDesignEdit Insert(GuiDocument document, GuiInsertion at, string snippet)
    {
        var edits = new GuiTextEdits(document).InsertChild(at.Parent, snippet, at.Position, at.Anchor);
        return new GuiDesignEdit(edits, FocusOnText(edits, 0));
    }

    /// <summary>Löscht den Knoten; der Cursor kommt auf den vorigen Geschwisterknoten, sonst den nächsten, sonst den Elternknoten.</summary>
    public static GuiDesignEdit Delete(GuiDocument document, GuiNode node)
    {
        var edits = new GuiTextEdits(document).RemoveNode(node);
        return new GuiDesignEdit(edits, Neighbor(node) is { } neighbor ? TextEdit.MapOffset(neighbor.Span.Start, edits) : null);
    }

    public static GuiDesignEdit Duplicate(GuiDocument document, GuiNode node)
    {
        var edits = new GuiTextEdits(document).DuplicateNode(node);
        return new GuiDesignEdit(edits, FocusOnText(edits, 0));
    }

    public static GuiDesignEdit MoveUp(GuiDocument document, GuiNode node)
    {
        var edits = new GuiTextEdits(document).MoveUp(node);
        return edits.Count == 0
            ? GuiDesignEdit.None
            : new GuiDesignEdit(edits, TextEdit.PositionInResult(edits, 0, edits[0].Text.IndexOf(document.TextOf(node), StringComparison.Ordinal)));
    }

    public static GuiDesignEdit MoveDown(GuiDocument document, GuiNode node)
    {
        // Der Tausch ersetzt beide Zeilenblöcke durch einen Text, in dem der Knoten zuletzt steht.
        var edits = new GuiTextEdits(document).MoveDown(node);
        return edits.Count == 0
            ? GuiDesignEdit.None
            : new GuiDesignEdit(edits, TextEdit.PositionInResult(edits, 0, edits[0].Text.LastIndexOf(document.TextOf(node), StringComparison.Ordinal)));
    }

    public static GuiDesignEdit Move(GuiDocument document, GuiNode node, GuiInsertion to)
    {
        var edits = new GuiTextEdits(document).MoveNode(node, to.Parent, to.Position, to.Anchor);
        return edits.Count == 0 ? GuiDesignEdit.None : new GuiDesignEdit(edits, FocusOnText(edits, 1));
    }

    public static GuiDesignEdit WrapIn(GuiDocument document, GuiNode node, string boxKey)
    {
        var edits = new GuiTextEdits(document).WrapIn(node, boxKey);
        return new GuiDesignEdit(edits, FocusOnText(edits, 0));
    }

    /// <summary>
    /// Legt <c>blockoverride "name" { … }</c> als letztes Kind der Instanz an, mit <paramref name="content"/> als Inhalt (relativ eingerückt,
    /// siehe <see cref="GuiBlockOverrides.ContentOf"/>; leer = der Block wird geleert, Plan-Ruling 14). Der Cursor kommt auf die erste Zeile,
    /// die mit <paramref name="focusKey"/> beginnt (das angepasste Element), sonst auf den blockoverride. Keine Änderung, wenn es ihn dort schon gibt.
    /// </summary>
    public static GuiDesignEdit OverrideBlock(GuiDocument document, GuiNode instance, string blockName, string content = "", string? focusKey = null)
    {
        if (instance.Children.Any(c => c.Kind == GuiNodeKind.BlockOverride && c.Name == blockName))
        {
            return GuiDesignEdit.None;
        }

        var edits = new GuiTextEdits(document).InsertChild(instance, GuiBlockOverrides.Snippet(blockName, content), InsertPosition.LastChild);
        return new GuiDesignEdit(edits, (focusKey is null ? null : FocusOnLine(edits, 0, focusKey)) ?? FocusOnText(edits, 0));
    }

    /// <summary>Fügt <c>using = template</c> nach den Eigenschaften ein; keine Änderung, wenn es genau dieses using schon gibt.</summary>
    public static GuiDesignEdit AddUsing(GuiDocument document, GuiNode element, string template)
    {
        if (element.Children.Any(c => c.Kind == GuiNodeKind.Using && c.Value == template))
        {
            return GuiDesignEdit.None;
        }

        // Using-Knoten haben die Art Using, nicht Property: SetProperty legt deshalb immer eine neue Zeile an (hinter der letzten Eigenschaft).
        var edits = new GuiTextEdits(document).SetProperty(element, "using", template);
        return new GuiDesignEdit(edits, FocusOnText(edits, 0));
    }

    /// <summary>Eigenschaft am Element setzen ("Diese Stelle"): vorhandene Zeile ändern, sonst nach den Eigenschaften einfügen.</summary>
    /// <param name="ignoreNameCase">Klassisch (HOI IV, EU IV): <c>orientation</c> trifft auch <c>Orientation</c>; die Schreibweise der Zeile bleibt.</param>
    public static GuiDesignEdit SetProperty(GuiDocument document, GuiNode element, string name, string valueText, bool ignoreNameCase = false)
    {
        var edits = new GuiTextEdits(document).SetProperty(element, name, valueText, ignoreNameCase);
        return new GuiDesignEdit(edits, TextEdit.MapOffset(element.Span.Start, edits));
    }

    /// <summary>Wert genau dieser Zeile ändern (die Zeile, aus der der Wert stammt; Plan-Ruling 4).</summary>
    public static GuiDesignEdit SetValue(GuiDocument document, GuiNode property, string valueText)
    {
        var edits = new GuiTextEdits(document).SetValue(property, valueText);
        return new GuiDesignEdit(edits, ParentStart(property, edits));
    }

    public static GuiDesignEdit RemoveProperty(GuiDocument document, GuiNode property)
    {
        var edits = new GuiTextEdits(document).RemoveNode(property);
        return new GuiDesignEdit(edits, ParentStart(property, edits));
    }

    private static GuiInsertion? Beside(GuiNode target, InsertPosition position, GuiResolver resolver) =>
        target.Parent is { } parent && AcceptsWidgets(parent, resolver) ? new GuiInsertion(parent, position, target) : null;

    private static int? ParentStart(GuiNode node, IReadOnlyList<TextEdit> edits) =>
        node.Parent is { Kind: not GuiNodeKind.Document } parent ? TextEdit.MapOffset(parent.Span.Start, edits) : null;

    private static GuiNode? Neighbor(GuiNode node)
    {
        if (node.Parent is not { } parent)
        {
            return null;
        }

        var siblings = parent.Children.Where(c => c.Kind != GuiNodeKind.Comment).ToList();
        var index = siblings.IndexOf(node);
        if (index > 0)
        {
            return siblings[index - 1];
        }

        if (index + 1 < siblings.Count)
        {
            return siblings[index + 1];
        }

        return parent.Kind == GuiNodeKind.Document ? null : parent;
    }

    /// <summary>Cursor auf die erste Zeile nach der ersten des eingefügten Texts, die mit dem Schlüssel beginnt; null, wenn es keine gibt.</summary>
    private static int? FocusOnLine(IReadOnlyList<TextEdit> edits, int editIndex, string key)
    {
        var text = edits[editIndex].Text;
        for (var newline = text.IndexOf('\n'); newline >= 0; newline = text.IndexOf('\n', newline + 1))
        {
            var first = newline + 1;
            while (first < text.Length && text[first] is ' ' or '\t')
            {
                first++;
            }

            var end = first + key.Length;
            if (end < text.Length && string.CompareOrdinal(text, first, key, 0, key.Length) == 0 && text[end] is ' ' or '=' or '\t')
            {
                return TextEdit.PositionInResult(edits, editIndex, first);
            }
        }

        return null;
    }

    /// <summary>Cursor auf das erste sichtbare Zeichen des eingefügten Texts der Ersetzung <paramref name="editIndex"/>.</summary>
    private static int? FocusOnText(IReadOnlyList<TextEdit> edits, int editIndex)
    {
        if (edits.Count <= editIndex)
        {
            return null;
        }

        var text = edits[editIndex].Text;
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index < text.Length ? TextEdit.PositionInResult(edits, editIndex, index) : null;
    }
}
