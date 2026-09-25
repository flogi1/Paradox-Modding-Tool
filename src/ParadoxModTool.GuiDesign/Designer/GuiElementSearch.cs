using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Filter "Art" im Suchfenster "GUI-Element inspizieren".</summary>
public enum GuiElementFilter
{
    All,
    Button,
    Tooltip,
    Window,
    Text,
    Icon,
    Container,
    List,
    Template,
    Other,
}

/// <summary>Eintrag der Suchliste: Definition, aufgelöstes Engine-Widget ("" bei Templates), Art und Tooltip-Merkmal (Plan-Ruling 6).</summary>
public sealed record GuiElementEntry(GuiDefinition Definition, string BaseWidget, GuiWidgetCategory Category, bool IsTooltip)
{
    public string Name => Definition.Name;

    public GuiDefinitionKind Kind => Definition.Kind;
}

/// <summary>
/// Liste für das Suchfenster: Typen und Templates je Name einmal (wirksame Definition), benannte Widgets mit jedem Vorkommen, weil derselbe
/// Name in verschiedenen Dateien verschiedene Elemente bezeichnet (Plan-Ruling 7).
/// </summary>
public static class GuiElementSearch
{
    public const string TooltipHolder = "tooltipwidget";

    public static IReadOnlyList<GuiElementEntry> Entries(GuiIndex index, GuiResolver resolver, CancellationToken cancellation = default)
    {
        var tooltipTypes = TooltipTypes(index);
        var entries = new List<GuiElementEntry>();
        foreach (var group in index.Definitions.GroupBy(d => (d.Kind, d.Name)))
        {
            cancellation.ThrowIfCancellationRequested();
            var definitions = group.Key.Kind == GuiDefinitionKind.NamedWidget
                ? group.ToList()
                : new List<GuiDefinition> { index.Find(group.Key.Name, group.Key.Kind)! };
            entries.AddRange(definitions.Select(definition => Entry(definition, resolver, tooltipTypes)));
        }

        entries.Sort((a, b) =>
        {
            var byName = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            return byName != 0 ? byName : a.Kind.CompareTo(b.Kind);
        });
        return entries;
    }

    public static bool Matches(GuiElementEntry entry, string text, GuiElementFilter filter)
    {
        if (text.Length > 0 && !entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return filter switch
        {
            GuiElementFilter.All => true,
            GuiElementFilter.Template => entry.Kind == GuiDefinitionKind.Template,
            GuiElementFilter.Tooltip => entry.IsTooltip,
            _ when entry.Kind == GuiDefinitionKind.Template => false,
            GuiElementFilter.Button => entry.Category == GuiWidgetCategory.Button,
            GuiElementFilter.Window => entry.Category == GuiWidgetCategory.Window,
            GuiElementFilter.Text => entry.Category == GuiWidgetCategory.Text,
            GuiElementFilter.Icon => entry.Category == GuiWidgetCategory.Icon,
            GuiElementFilter.Container => entry.Category is GuiWidgetCategory.Container or GuiWidgetCategory.Box,
            GuiElementFilter.List => entry.Category == GuiWidgetCategory.List,
            _ => entry.Category == GuiWidgetCategory.Other,
        };
    }

    private static GuiElementEntry Entry(GuiDefinition definition, GuiResolver resolver, IReadOnlySet<string> tooltipTypes)
    {
        switch (definition.Kind)
        {
            case GuiDefinitionKind.Template:
                return new GuiElementEntry(definition, string.Empty, GuiWidgetCategory.Other, false);

            case GuiDefinitionKind.Type:
                var typeBase = resolver.BaseWidgetOf(definition.Name);
                return new GuiElementEntry(definition, typeBase, GuiWidgetKinds.CategoryOf(typeBase), tooltipTypes.Contains(definition.Name));

            default:
                var key = definition.WidgetKey ?? string.Empty;
                var widgetBase = key.Length == 0 ? string.Empty : resolver.BaseWidgetOf(key);
                var tooltip = HasTooltipName(definition.Name)
                    || tooltipTypes.Contains(key)
                    || definition.Node.Parent is { Kind: GuiNodeKind.KeyedBlock, Name: TooltipHolder };
                return new GuiElementEntry(definition, widgetBase, GuiWidgetKinds.CategoryOf(widgetBase), tooltip);
        }
    }

    /// <summary>Typen mit "tooltip" im Namen oder mit einer Verwendung direkt in einem <c>tooltipwidget</c>.</summary>
    private static IReadOnlySet<string> TooltipTypes(GuiIndex index)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in index.Definitions.Where(d => d.Kind == GuiDefinitionKind.Type))
        {
            if (HasTooltipName(definition.Name)
                || index.UsagesOf(definition.Name).Any(u => u.Kind == GuiUsageKind.Widget && u.Node.Parent is { Kind: GuiNodeKind.KeyedBlock, Name: TooltipHolder }))
            {
                result.Add(definition.Name);
            }
        }

        return result;
    }

    private static bool HasTooltipName(string name) => name.Contains("tooltip", StringComparison.OrdinalIgnoreCase);
}
