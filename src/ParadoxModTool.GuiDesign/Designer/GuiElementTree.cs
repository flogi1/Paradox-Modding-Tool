using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

public enum GuiTreeItemKind
{
    Types,
    Type,
    Template,
    Widget,
    Holder,
    Block,
    BlockOverride,
    OfferedBlock,
}

/// <summary>Eintrag im Elementbaum des Designers. Eigenschaften stehen nicht im Baum, sondern im Eigenschaftenfeld.</summary>
public sealed class GuiTreeItem
{
    public required GuiTreeItemKind Kind { get; init; }

    public required string Label { get; init; }

    /// <summary>Syntaxknoten; bei einem angebotenen, nicht überschriebenen Block der Knoten der Instanz, die ihn anbietet.</summary>
    public required GuiNode Node { get; init; }

    public string? Detail { get; init; }

    public GuiWidgetCategory Category { get; init; } = GuiWidgetCategory.Other;

    /// <summary>Weder Typ im Index noch bekanntes Engine-Widget (z.B. Typ aus einer fehlenden Abhängigkeits-Mod).</summary>
    public bool Unknown { get; init; }

    /// <summary>Instanz eines Typs (der Widget-Schlüssel ist ein Typname): "Definition öffnen" ist möglich.</summary>
    public bool IsTypeInstance { get; init; }

    /// <summary>Name des Blocks bei <see cref="GuiTreeItemKind.OfferedBlock"/> (für "Block überschreiben").</summary>
    public string? BlockName { get; init; }

    public List<GuiTreeItem> Children { get; } = new();
}

/// <summary>
/// Baut den Elementbaum aus dem Syntaxbaum (der Text bleibt die Wahrheit): Definitionen, Widgets, Halter und Blöcke. Instanzen eines Typs
/// zeigen zusätzlich die Blöcke der Typkette, die sie nicht überschreiben.
/// </summary>
public static class GuiElementTree
{
    public static IReadOnlyList<GuiTreeItem> Build(GuiDocument document, GuiResolver resolver, Func<string, bool>? isKnownEngineWidget = null)
    {
        var items = new List<GuiTreeItem>();
        AddChildren(items, document.Root, resolver, isKnownEngineWidget);
        return items;
    }

    public static string LabelOf(GuiNode node) => node.Kind switch
    {
        GuiNodeKind.TypesContainer => $"types {node.Name}",
        GuiNodeKind.TypeDefinition => $"type {node.Name} = {node.BaseName}",
        GuiNodeKind.TemplateDefinition => $"template {node.Name}",
        GuiNodeKind.Block => $"block \"{node.Name}\"",
        GuiNodeKind.BlockOverride => $"blockoverride \"{node.Name}\"",
        _ => NameOf(node) is { } name ? $"{node.Name} \"{name}\"" : node.Name,
    };

    private static string? NameOf(GuiNode node) =>
        node.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name == "name")?.Value is { Length: > 0 } name ? name : null;

    private static void AddChildren(List<GuiTreeItem> target, GuiNode parent, GuiResolver resolver, Func<string, bool>? known)
    {
        foreach (var node in parent.Children)
        {
            var item = node.Kind switch
            {
                GuiNodeKind.TypesContainer => new GuiTreeItem { Kind = GuiTreeItemKind.Types, Label = LabelOf(node), Node = node },
                GuiNodeKind.TypeDefinition => TypeItem(node, resolver),
                GuiNodeKind.TemplateDefinition => new GuiTreeItem { Kind = GuiTreeItemKind.Template, Label = LabelOf(node), Node = node },
                GuiNodeKind.Block => new GuiTreeItem { Kind = GuiTreeItemKind.Block, Label = LabelOf(node), Node = node },
                GuiNodeKind.BlockOverride => new GuiTreeItem { Kind = GuiTreeItemKind.BlockOverride, Label = LabelOf(node), Node = node },
                GuiNodeKind.KeyedBlock => WidgetItem(node, resolver, known),
                _ => null,
            };
            if (item is null)
            {
                continue;
            }

            AddChildren(item.Children, node, resolver, known);
            if (item.Kind == GuiTreeItemKind.Widget)
            {
                AddOfferedBlocks(item, node, resolver);
            }

            target.Add(item);
        }
    }

    private static GuiTreeItem TypeItem(GuiNode node, GuiResolver resolver)
    {
        var baseWidget = resolver.BaseWidgetOf(node.Name);
        return new GuiTreeItem
        {
            Kind = GuiTreeItemKind.Type,
            Label = LabelOf(node),
            Node = node,
            Detail = GuiWidgetKinds.Normalize(baseWidget),
            Category = GuiWidgetKinds.CategoryOf(baseWidget),
        };
    }

    private static GuiTreeItem? WidgetItem(GuiNode node, GuiResolver resolver, Func<string, bool>? known)
    {
        var role = resolver.Classify(node);
        if (role == GuiRole.WidgetHolder)
        {
            return new GuiTreeItem { Kind = GuiTreeItemKind.Holder, Label = node.Name, Node = node };
        }

        if (role is not (GuiRole.TypeInstance or GuiRole.EngineWidget))
        {
            return null;
        }

        var baseWidget = resolver.BaseWidgetOf(node.Name);
        var unknown = role == GuiRole.EngineWidget && known is not null && !known(baseWidget);
        return new GuiTreeItem
        {
            Kind = GuiTreeItemKind.Widget,
            Label = LabelOf(node),
            Node = node,
            Detail = unknown ? GuiTexts.TreeUnknownType : role == GuiRole.TypeInstance ? GuiWidgetKinds.Normalize(baseWidget) : null,
            Category = GuiWidgetKinds.CategoryOf(baseWidget),
            Unknown = unknown,
            IsTypeInstance = role == GuiRole.TypeInstance,
        };
    }

    private static void AddOfferedBlocks(GuiTreeItem item, GuiNode instance, GuiResolver resolver)
    {
        if (resolver.Classify(instance) != GuiRole.TypeInstance)
        {
            return;
        }

        var overridden = instance.Children
            .Where(c => c.Kind == GuiNodeKind.BlockOverride)
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);
        var chain = resolver.TypeChain(instance.Name);
        var offered = new List<string>();
        foreach (var typeName in chain.Take(chain.Count - 1))
        {
            if (resolver.FindType(typeName) is not { } definition)
            {
                continue;
            }

            foreach (var block in definition.Descendants().Where(n => n.Kind == GuiNodeKind.Block))
            {
                if (!overridden.Contains(block.Name) && !offered.Contains(block.Name))
                {
                    offered.Add(block.Name);
                }
            }
        }

        foreach (var name in offered)
        {
            item.Children.Add(new GuiTreeItem
            {
                Kind = GuiTreeItemKind.OfferedBlock,
                Label = $"block \"{name}\"",
                Node = instance,
                Detail = GuiTexts.TreeNotOverridden,
                BlockName = name,
            });
        }
    }
}
