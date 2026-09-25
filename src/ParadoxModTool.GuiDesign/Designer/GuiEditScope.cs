using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Geltungsbereich "Diese Stelle" (Plan-Ruling 3): Liegt die Stelle in einer Typ- oder Template-Definition, wirkt die Änderung überall, wo
/// diese verwendet wird. <c>Usages</c> zählt diese Verwendungen im Index (gespeicherter Stand); ohne umschließende Definition wirkt sie nur hier.
/// </summary>
public sealed record GuiEditScope(GuiNode? Definition, GuiDefinitionKind Kind, int Usages)
{
    public string? Label => Definition is null ? null : GuiElementTree.LabelOf(Definition);

    /// <summary>
    /// Umschließende Definition und ihre Verwendungen. <paramref name="filePath"/>: Datei des angezeigten Texts; dann zählen die Verwendungen in
    /// diesem (womöglich ungespeicherten) Text statt der gespeicherten dieser Datei im Index (Plan 3c: nach "Neuer Typ" kennt der Index die
    /// umgestellten Stellen noch nicht). Ohne Pfad zählt nur der Index.
    /// </summary>
    public static GuiEditScope Of(GuiNode node, GuiIndex index, string? filePath = null)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition)
            {
                var isType = current.Kind == GuiNodeKind.TypeDefinition;
                var usageKind = isType ? GuiUsageKind.Widget : GuiUsageKind.Using;
                var name = current.Name;
                var saved = index.UsagesOf(name).Count(u => u.Kind == usageKind && (filePath is null || !GuiTypeEdits.SamePath(u.File.FullPath, filePath)));
                var live = filePath is null
                    ? 0
                    : node.Root.Descendants().Count(n => isType
                        ? n.Kind == GuiNodeKind.KeyedBlock && n.Name == name
                        : n.Kind == GuiNodeKind.Using && n.Value == name);
                return new GuiEditScope(current, isType ? GuiDefinitionKind.Type : GuiDefinitionKind.Template, saved + live);
            }
        }

        return new GuiEditScope(null, GuiDefinitionKind.NamedWidget, 0);
    }
}

public enum GuiReadOnlyReason
{
    Vanilla,
    Dependency,
    Locked,
}

/// <summary>Ob der Designer eine Datei bearbeiten darf (Plan-Ruling 2): nicht Vanilla, nicht aus einer Abhängigkeits-Mod, nicht gesperrt.</summary>
public static class GuiEditability
{
    public static GuiReadOnlyReason? ReasonFor(string? filePath, bool isVanilla, bool isLocked, IEnumerable<string> dependencyRoots)
    {
        if (isVanilla)
        {
            return GuiReadOnlyReason.Vanilla;
        }

        if (filePath is not null && dependencyRoots.Any(root => IsInside(filePath, root)))
        {
            return GuiReadOnlyReason.Dependency;
        }

        return isLocked ? GuiReadOnlyReason.Locked : null;
    }

    private static bool IsInside(string file, string folder) =>
        Path.GetFullPath(file).StartsWith(
            Path.GetFullPath(folder).TrimEnd('\\', '/') + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
}
