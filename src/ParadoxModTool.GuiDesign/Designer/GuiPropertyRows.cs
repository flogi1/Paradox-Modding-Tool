using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Gruppen im Eigenschaftenfeld, in Anzeigereihenfolge.</summary>
public enum GuiPropertyGroup
{
    Data,
    Layout,
    Appearance,
    Text,
    Behavior,
    Other,
}

public sealed record GuiPropertyRow(
    string Name,
    string Value,
    GuiPropertyGroup Group,
    bool IsHere,
    string OriginText,
    GuiOrigin Origin,
    int OverriddenCount,
    bool Unresolved,
    bool IsBlock,
    ResolvedProperty Source)
{
    /// <summary>Bedingung des <c>if_resolution</c>-Blocks (EU IV), aus dem der Wert kommt; sonst <see langword="null"/>.</summary>
    public string? Condition { get; init; }
}

/// <summary>Zeilen für das Eigenschaftenfeld: wirksamer Wert, Gruppe und Herkunft ("hier gesetzt", "geerbt von …").</summary>
public static class GuiPropertyRows
{
    private static readonly HashSet<string> LayoutNames = new(StringComparer.Ordinal)
    {
        "size", "position", "parentanchor", "widgetanchor", "spacing", "minimumsize", "maximumsize", "resizeparent", "addcolumn", "addrow",
        "maxhorizontalslots", "maxverticalslots", "flipdirection", "direction", "ignoreinvisible", "alwaystransparent", "scissor",
    };

    private static readonly HashSet<string> AppearanceNames = new(StringComparer.Ordinal)
    {
        "framesize", "frame", "alpha", "color", "tintcolor", "mirror", "modify_texture", "glow", "upframe", "overframe", "downframe",
        "disableframe", "uv", "effectname", "gfxtype", "shaderfile", "background",
    };

    private static readonly HashSet<string> BehaviorNames = new(StringComparer.Ordinal)
    {
        "visible", "enabled", "state", "shortcut", "clicksound", "oversound", "checked", "down", "using_tooltip", "action_tooltip", "tooltipwidget",
    };

    public static GuiPropertyGroup GroupOf(string name)
    {
        if (name == "name" || name.StartsWith("datacontext", StringComparison.Ordinal) || name.StartsWith("datamodel", StringComparison.Ordinal))
        {
            return GuiPropertyGroup.Data;
        }

        if (AppearanceNames.Contains(name) || name.StartsWith("sprite", StringComparison.Ordinal) || name.StartsWith("texture", StringComparison.Ordinal))
        {
            return GuiPropertyGroup.Appearance;
        }

        if (name.StartsWith("font", StringComparison.Ordinal) || name.Contains("text", StringComparison.Ordinal)
            || name is "align" or "autoresize" or "elide" or "multiline" or "max_width" or "default_format")
        {
            return GuiPropertyGroup.Text;
        }

        if (LayoutNames.Contains(name) || name.StartsWith("margin", StringComparison.Ordinal)
            || name.StartsWith("layoutpolicy", StringComparison.Ordinal) || name.StartsWith("scrollbar", StringComparison.Ordinal))
        {
            return GuiPropertyGroup.Layout;
        }

        if (BehaviorNames.Contains(name) || name.StartsWith("on", StringComparison.Ordinal) || name.StartsWith("tooltip", StringComparison.Ordinal))
        {
            return GuiPropertyGroup.Behavior;
        }

        return GuiPropertyGroup.Other;
    }

    public static string OriginText(GuiOrigin origin)
    {
        var where = origin.FilePath is null ? string.Empty : $" ({Path.GetFileName(origin.FilePath)}:{origin.Line})";
        return origin.Kind switch
        {
            GuiOriginKind.Here => GuiTexts.OriginHere,
            GuiOriginKind.Type => GuiTexts.OriginType(origin.Definition, where),
            GuiOriginKind.Template => GuiTexts.OriginTemplate(origin.Definition, where),
            _ => GuiTexts.OriginBlockOverride(origin.Definition, where),
        };
    }

    public static IReadOnlyList<GuiPropertyRow> Build(ResolvedElement element) =>
        element.Properties
            .Select((property, index) => (Row: ToRow(property), Index: index))
            .OrderBy(x => x.Row.Group)
            .ThenBy(x => x.Index)
            .Select(x => x.Row)
            .ToList();

    internal static GuiPropertyRow ToRow(ResolvedProperty property)
    {
        var value = property.Role switch
        {
            GuiRole.StructuredBlock => "{ " + string.Join(" ", property.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) + " }",
            GuiRole.ValueBlock => "{ " + property.Value + " }",
            _ => property.IsQuoted ? $"\"{property.Value}\"" : property.Value,
        };
        return new GuiPropertyRow(
            property.Name,
            value,
            GroupOf(property.Name),
            property.Origin.Kind == GuiOriginKind.Here,
            OriginText(property.Origin),
            property.Origin,
            property.Overridden.Count,
            property.Unresolved,
            property.Role == GuiRole.StructuredBlock,
            property);
    }
}
