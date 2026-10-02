using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.DataTypes;

/// <summary>Bestimmt den Typ eines Datenkontexts: aus einem <c>datacontext</c>-Ausdruck und für eine Stelle in einer <c>.gui</c>-Datei.</summary>
public static class DataContextResolver
{
    /// <summary>
    /// Typ eines Ausdrucks: <c>[Country]</c> (Typname), <c>[GetPlayer]</c> (globaler Promote, auch mit Argumenten), <c>[Country.GetCapital]</c> (den Promotes
    /// folgend); <see langword="null"/>, wenn ein Teil unbekannt ist oder keinen Typ liefert.
    /// </summary>
    public static string? TypeOf(string expression, DataTypeDatabase db)
    {
        if (DataExpression.Chains(DataExpression.Strip(expression)).FirstOrDefault() is not { } chain)
        {
            return null;
        }

        var first = chain.Parts[0];
        var type = !chain.IsCall && db.IsType(first) ? first : db.GlobalPromote(first);
        foreach (var part in chain.Parts.Skip(1))
        {
            if (type is null)
            {
                return null;
            }

            type = db.Promote(type, part);
        }

        return type;
    }

    /// <summary>
    /// Start-Typ an einem Knoten: der nächste <c>datacontext</c> am Element oder darüber; liegt das Element in einem <c>item</c>-Block neben einem
    /// <c>datamodel</c>, der gelernte Elementtyp der Liste (nur, wenn er ein bekannter Typ ist). Ein <c>datacontext</c> innerhalb des Eintrags hat Vorrang.
    /// </summary>
    public static string? StartAt(GuiNode node, DataTypeDatabase db, GuiPropertyCatalog catalog)
    {
        for (var container = node.Kind == GuiNodeKind.Property ? node.Parent : node; container is not null; container = container.Parent)
        {
            if (container.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name == "datacontext" && c.Value is not null) is { } context)
            {
                return TypeOf(context.Value!, db);
            }

            if (container is { Kind: GuiNodeKind.KeyedBlock, Name: "item" }
                && container.Parent?.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name == "datamodel" && c.Value is not null) is { } model)
            {
                return catalog.ItemTypeOf(model.Value!) is { } itemType && db.IsType(itemType) ? itemType : null;
            }
        }

        return null;
    }
}
