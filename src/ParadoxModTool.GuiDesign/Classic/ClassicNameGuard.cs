using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Schutz vor Namensänderungen im klassischen Format: Den Namen eines Vanilla-Elements kann der Spielcode (Skripte, andere Dateien) nutzen,
/// ohne dass die GUI-Dateien es verraten. Löschen oder Umbenennen fragt deshalb vorher nach.
/// </summary>
public static class ClassicNameGuard
{
    /// <summary>
    /// Der betroffene Name, wenn <paramref name="target"/> ein Element ist, das gelöscht wird (<paramref name="deletes"/>), oder die
    /// <c>name</c>-Eigenschaft, die umbenannt wird, und der Name eine Definition aus Vanilla hat; sonst null.
    /// </summary>
    public static string? VanillaNameAffected(GuiNode target, bool deletes, GuiIndex index)
    {
        string? name;
        if (deletes)
        {
            name = target.Kind == GuiNodeKind.KeyedBlock ? NameOf(target) : null;
        }
        else
        {
            name = target.Kind == GuiNodeKind.Property && IsName(target) ? target.Value : null;
        }

        return !string.IsNullOrEmpty(name)
            && index.FindAll(name, GuiDefinitionKind.NamedWidget).Any(d => d.File.Layer == GuiLayer.Vanilla)
                ? name
                : null;
    }

    private static string? NameOf(GuiNode element) =>
        element.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && IsName(c))?.Value;

    private static bool IsName(GuiNode node) => string.Equals(node.Name, "name", StringComparison.OrdinalIgnoreCase);
}
