using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Zeilen des Eigenschaftenfelds für klassische Elemente (HOI IV, EU IV): Gruppen nach dem Namen ohne Rücksicht auf Groß-/Kleinschreibung und
/// Werte aus dem passenden <c>if_resolution</c>-Block (EU IV), so wie die Vorschau sie nimmt (<see cref="ClassicProps.MatchingIfResolution"/>).
/// </summary>
public static class ClassicPropertyRows
{
    private static readonly HashSet<string> LayoutNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "position", "show_position", "hide_position", "size", "orientation", "origo", "centerposition", "margin", "scale", "rotation", "bordersize",
        "slotsize", "max_slots_horizontal", "max_slots_vertical", "max_slots", "add_horizontal", "spacing", "padding", "fullscreen", "fixedsize",
        "if_resolution",
    };

    private static readonly HashSet<string> AppearanceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "spritetype", "quadtexturesprite", "frame", "background", "alwaystransparent", "clipping", "texturefile", "buttonmesh", "dontrender",
        "horizontalborder", "verticalborder",
    };

    private static readonly HashSet<string> TextNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "font", "format", "maxwidth", "maxheight", "buttontext", "buttonfont", "text_color_code", "vertical_alignment", "multiline", "truncate",
    };

    private static readonly HashSet<string> BehaviorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "shortcut", "extra_shortcut", "clicksound", "oversound", "no_clicksound", "pdx_tooltip", "pdx_tooltip_delayed", "tooltip", "tooltiptext",
        "delayedtooltiptext", "moveable", "click_to_front", "scripted", "hide", "enabled", "web_link", "hint_tag", "context_aware_tooltip", "upsound",
        "downsound",
    };

    private static readonly string[] BehaviorPrefixes = { "show_", "hide_", "fade_", "animation_" };

    public static GuiPropertyGroup GroupOf(string name)
    {
        if (string.Equals(name, "name", StringComparison.OrdinalIgnoreCase))
        {
            return GuiPropertyGroup.Data;
        }

        if (LayoutNames.Contains(name))
        {
            return GuiPropertyGroup.Layout;
        }

        if (AppearanceNames.Contains(name))
        {
            return GuiPropertyGroup.Appearance;
        }

        if (TextNames.Contains(name))
        {
            return GuiPropertyGroup.Text;
        }

        return BehaviorNames.Contains(name) || BehaviorPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ? GuiPropertyGroup.Behavior
            : GuiPropertyGroup.Other;
    }

    /// <summary>
    /// Zeilen in Anzeigereihenfolge (Gruppe, dann Textreihenfolge). Setzt ein passender <c>if_resolution</c>-Block einen Schlüssel, zeigt dessen Zeile
    /// den Wert aus dem Block (bei mehreren Blöcken gewinnt der letzte); Schlüssel, die nur der Block setzt, bekommen eine eigene Zeile.
    /// </summary>
    public static IReadOnlyList<GuiPropertyRow> Build(ResolvedElement element, double screenWidth, double screenHeight)
    {
        var overrides = Overrides(element, screenWidth, screenHeight);
        var entries = new List<(GuiPropertyRow Row, int Index, int Sub)>();
        var used = new HashSet<(string, bool)>();
        var properties = element.Properties;
        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            var key = (property.Name.ToLowerInvariant(), property.Role == GuiRole.StructuredBlock);
            var row = GuiPropertyRows.ToRow(property) with { Group = GroupOf(property.Name) };

            // Mehrfach gesetzte Blöcke: die Vorschau nimmt den letzten, also trägt auch nur dessen Zeile den Blockwert.
            if (overrides.TryGetValue(key, out var found) && LastIndexOf(properties, key) == i)
            {
                row = RowFrom(found, property.Name);
                used.Add(key);
            }

            entries.Add((row, i, 0));
        }

        foreach (var (key, found) in overrides)
        {
            if (!used.Contains(key))
            {
                entries.Add((RowFrom(found, found.Node.Name), found.BlockIndex, 1 + found.Node.Span.Start));
            }
        }

        return entries.OrderBy(e => e.Row.Group).ThenBy(e => e.Index).ThenBy(e => e.Sub).Select(e => e.Row).ToList();
    }

    private sealed record Override(ClassicResolutionMatch Match, ResolvedProperty BlockProperty, int BlockIndex, GuiNode Node);

    /// <summary>Je Schlüssel (kleingeschrieben, Block oder Skalar) der Wert aus dem letzten passenden Block, der ihn setzt.</summary>
    private static Dictionary<(string, bool), Override> Overrides(ResolvedElement element, double screenWidth, double screenHeight)
    {
        var result = new Dictionary<(string, bool), Override>();
        var matches = ClassicProps.MatchingIfResolution(element, screenWidth, screenHeight);
        for (var m = matches.Count - 1; m >= 0; m--)
        {
            var blockIndex = element.Properties.FindIndex(p => p.Role == GuiRole.StructuredBlock && ReferenceEquals(p.Origin.Node, matches[m].Block));
            if (blockIndex < 0)
            {
                continue;
            }

            // Innerhalb eines Blocks gilt wie in der Vorschau (ClassicProps) die letzte Stelle eines Schlüssels; hat sie keinen Wert, zählt der Schlüssel
            // in diesem Block nicht.
            var seen = new HashSet<(string, bool)>();
            var children = matches[m].Block.Children;
            for (var c = children.Count - 1; c >= 0; c--)
            {
                var child = children[c];
                var isBlock = child.Kind == GuiNodeKind.KeyedBlock;
                if (!isBlock && child.Kind != GuiNodeKind.Property)
                {
                    continue;
                }

                var key = (child.Name.ToLowerInvariant(), isBlock);
                if (!seen.Add(key))
                {
                    continue;
                }

                // Die Bedingungen selbst sind keine Eigenschaften des Elements.
                if (!isBlock && (child.Value is null || ClassicProps.ResolutionConditions.Contains(child.Name, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.TryAdd(key, new Override(matches[m], element.Properties[blockIndex], blockIndex, child));
            }
        }

        return result;
    }

    private static int LastIndexOf(List<ResolvedProperty> properties, (string, bool) key) =>
        properties.FindLastIndex(p => (p.Name.ToLowerInvariant(), p.Role == GuiRole.StructuredBlock) == key);

    private static GuiPropertyRow RowFrom(Override found, string name)
    {
        var node = found.Node;
        var block = found.BlockProperty;
        var property = new ResolvedProperty
        {
            Name = name,
            Role = node.Kind == GuiNodeKind.KeyedBlock ? GuiRole.StructuredBlock : GuiRole.Property,
            IsQuoted = node.IsQuoted,
            Origin = new GuiOrigin(block.Origin.Kind, block.Origin.Definition, block.Origin.FilePath, node.Line, node),
        };

        if (node.Kind == GuiNodeKind.KeyedBlock)
        {
            property.Value = BodyText(found);
        }
        else
        {
            var raw = node.Value ?? string.Empty;
            var substituted = node.IsQuoted ? null : GuiResolver.SubstituteConstant(raw, node);
            property.Value = substituted ?? raw;
            property.Unresolved = !node.IsQuoted && raw.StartsWith('@') && substituted is null;
        }

        return GuiPropertyRows.ToRow(property) with
        {
            Group = GroupOf(name),
            Condition = found.Match.Condition,
            OriginText = GuiTexts.ClassicOriginIfResolution(found.Match.Condition),
        };
    }

    /// <summary>
    /// Inhalt eines Unterblocks als Rohtext. Der Resolver hält nur den (getrimmten) Inhalt des <c>if_resolution</c>-Blocks; den des Unterblocks
    /// schneidet man über die Offsets der Knoten heraus.
    /// </summary>
    private static string BodyText(Override found)
    {
        var outer = found.Match.Block;
        var text = found.BlockProperty.Value;
        // Der Resolver trimmt den Inhalt; der Versatz des ersten Kindes sagt, wie viel Leerraum vorn wegfiel.
        var leading = outer.Children.Count > 0 ? outer.Children[0].Span.Start - outer.BodySpan.Start : 0;
        var start = found.Node.BodySpan.Start - outer.BodySpan.Start - leading;
        var end = found.Node.BodySpan.End - outer.BodySpan.Start - leading;
        return leading >= 0 && start >= 0 && end >= start && end <= text.Length ? text[start..end].Trim() : string.Empty;
    }
}
