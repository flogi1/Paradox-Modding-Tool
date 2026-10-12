using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Resolve;

public enum GuiRole
{
    TypeInstance,
    EngineWidget,
    WidgetHolder,
    StructuredBlock,
    ValueBlock,
    Property,
    Using,
    Block,
    BlockOverride,
    Constant,
    Comment,
    ListItem,
    Definition,
}

/// <summary>Spielabhängige Einordnungslisten aus dem Profil (Abschnitt <c>gui</c>) und die Einordnung eines Knotens (Plan-Ruling 2).</summary>
public sealed class GuiRules
{
    public GuiRules(IEnumerable<string> structuredBlocks, IEnumerable<string> widgetHolders, IEnumerable<string> layoutHolders)
        : this(structuredBlocks, widgetHolders, layoutHolders, GuiLayout.Default)
    {
    }

    public GuiRules(IEnumerable<string> structuredBlocks, IEnumerable<string> widgetHolders, IEnumerable<string> layoutHolders, GuiLayout layout)
    {
        StructuredBlocks = new HashSet<string>(structuredBlocks, StringComparer.Ordinal);
        WidgetHolders = new HashSet<string>(widgetHolders, StringComparer.Ordinal);
        LayoutHolders = new HashSet<string>(layoutHolders, StringComparer.Ordinal);
        Layout = layout;
    }

    /// <summary>Kontextordner und Überschreib-Reihenfolge des Spiels.</summary>
    public GuiLayout Layout { get; }

    public static GuiRules Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public IReadOnlySet<string> StructuredBlocks { get; }

    public IReadOnlySet<string> WidgetHolders { get; }

    public IReadOnlySet<string> LayoutHolders { get; }

    public static GuiRules FromProfile(GameProfile profile) =>
        profile.Gui is { } gui
            ? new GuiRules(
                gui.StructuredBlocks,
                gui.WidgetHolders,
                gui.LayoutHolders,
                new GuiLayout(
                    gui.Contexts is { } contexts ? contexts.ToList() : GuiLayout.Default.Contexts,
                    string.Equals(gui.TypeOverride, "firstWins", StringComparison.OrdinalIgnoreCase),
                    gui.ModsBeforeVanilla))
            : Empty;

    /// <summary>
    /// Einordnung: Typname → Typ-Instanz; Profilliste → strukturierter Block bzw. Widget-Halter; nur Listeneinträge → Wertblock;
    /// sonst Engine-Widget. Namenlose Blöcke gelten als Wertblock.
    /// </summary>
    public GuiRole Classify(GuiNode node, Func<string, bool> isType) => node.Kind switch
    {
        GuiNodeKind.Property => GuiRole.Property,
        GuiNodeKind.Using => GuiRole.Using,
        GuiNodeKind.Block => GuiRole.Block,
        GuiNodeKind.BlockOverride => GuiRole.BlockOverride,
        GuiNodeKind.ConstantDefinition => GuiRole.Constant,
        GuiNodeKind.Comment => GuiRole.Comment,
        GuiNodeKind.ListItem => GuiRole.ListItem,
        GuiNodeKind.KeyedBlock => ClassifyBlock(node, isType),
        _ => GuiRole.Definition,
    };

    private GuiRole ClassifyBlock(GuiNode node, Func<string, bool> isType)
    {
        if (node.Name.Length == 0)
        {
            return GuiRole.ValueBlock;
        }

        if (isType(node.Name))
        {
            return GuiRole.TypeInstance;
        }

        if (StructuredBlocks.Contains(node.Name))
        {
            return GuiRole.StructuredBlock;
        }

        if (WidgetHolders.Contains(node.Name))
        {
            return GuiRole.WidgetHolder;
        }

        var hasItem = false;
        foreach (var child in node.Children)
        {
            if (child.Kind == GuiNodeKind.ListItem)
            {
                hasItem = true;
            }
            else if (child.Kind != GuiNodeKind.Comment)
            {
                return GuiRole.EngineWidget;
            }
        }

        return hasItem ? GuiRole.ValueBlock : GuiRole.EngineWidget;
    }
}
