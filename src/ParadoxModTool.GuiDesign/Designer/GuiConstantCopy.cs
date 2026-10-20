using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Kopiert die Konstanten (<c>@name = wert</c>), die eine Definition braucht, auf die oberste Ebene einer Zieldatei. Herausgelöst aus dem
/// <see cref="GuiOverridePlanner"/>, damit der klassische Planer dieselbe Regel nutzt.
/// </summary>
internal static class GuiConstantCopy
{
    /// <summary>
    /// Kopiert die Konstanten, die <paramref name="node"/> braucht (zusammen mit der Mod-Fassung <paramref name="home"/>), in <paramref name="targetText"/>.
    /// Gibt es dort denselben Namen mit anderem Wert, bekommt die Kopie den Namen <c>name_&lt;stem&gt;</c>; das Ergebnis sind diese Umbenennungen.
    /// Die Plan-Einträge (<c>CopyConstant</c>, <c>RenameConstant</c>) kommen an <paramref name="entries"/>.
    /// </summary>
    public static Dictionary<string, string> Copy(
        GuiNode node, GuiNode? home, ref string targetText, string stem, List<GuiPlanEntry> entries, string targetPath)
    {
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        var existing = GuiParser.Parse(targetText).Root.Children
            .Where(c => c.Kind == GuiNodeKind.ConstantDefinition)
            .GroupBy(c => c.Name)
            .ToDictionary(g => g.Key, g => g.First().Value ?? string.Empty, StringComparer.Ordinal);
        foreach (var constant in GuiConstants.RequiredBy(node, home))
        {
            var value = GuiConstants.Rename(constant.Value ?? string.Empty, renames);
            if (existing.TryGetValue(constant.Name, out var present))
            {
                if (present == value)
                {
                    continue;
                }

                var name = constant.Name + "_" + stem;
                for (var n = 2; existing.TryGetValue(name, out var taken) && taken != value; n++)
                {
                    name = $"{constant.Name}_{stem}_{n}";
                }

                renames[constant.Name] = name;
                if (!existing.ContainsKey(name))
                {
                    targetText = Insert(targetText, name, value);
                    existing[name] = value;
                }

                entries.Add(new GuiPlanEntry(GuiPlanEntryKind.RenameConstant, constant.Name, null, targetPath, 0, name));
                continue;
            }

            targetText = Insert(targetText, constant.Name, value);
            existing[constant.Name] = value;
            entries.Add(new GuiPlanEntry(GuiPlanEntryKind.CopyConstant, constant.Name, null, targetPath, 0));
        }

        return renames;
    }

    private static string Insert(string text, string name, string value)
    {
        var document = GuiParser.Parse(text);
        var edits = new GuiTextEdits(document);
        var root = document.Root;
        var line = $"@{name} = {value}";
        var lastConstant = root.Children.LastOrDefault(c => c.Kind == GuiNodeKind.ConstantDefinition);
        var first = root.Children.FirstOrDefault(c => c.Kind != GuiNodeKind.Comment);
        var change = lastConstant is not null ? edits.InsertChild(root, line, InsertPosition.After, lastConstant)
            : first is not null ? edits.InsertChild(root, line, InsertPosition.Before, first)
            : edits.InsertChild(root, line, InsertPosition.LastChild);
        return TextEdit.Apply(text, change);
    }
}
