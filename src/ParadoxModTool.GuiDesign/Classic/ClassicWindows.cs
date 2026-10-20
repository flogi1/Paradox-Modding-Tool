using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Klassische Fenster (HOI IV, EU IV): die Blöcke mit <c>name</c> direkt in einem obersten <c>guiTypes = { … }</c>. Sie sind die Einheit eines
/// Entwurfs, weil ein Spiel ein Fenster nur ganz ersetzt. Verschachtelte Elemente (<c>iconType</c> in einem Fenster) zählen nie, auch nicht mit
/// demselben Namen.
/// </summary>
public static class ClassicWindows
{
    /// <summary>Die Fenster der Datei in Textreihenfolge (jede Schreibweise von <c>guiTypes</c>, auch mehrere Blöcke).</summary>
    public static IReadOnlyList<GuiNode> Of(GuiDocument document) =>
        document.Root.Children
            .Where(IsContainer)
            .SelectMany(container => container.Children)
            .Where(IsWindow)
            .ToList();

    /// <summary>Der erste oberste <c>guiTypes</c>-Block der Datei, sonst null.</summary>
    public static GuiNode? ContainerOf(GuiDocument document) => document.Root.Children.FirstOrDefault(IsContainer);

    /// <summary>Der Wert von <c>name</c> (jede Schreibweise des Schlüssels); leer, wenn der Block keinen Namen hat.</summary>
    public static string NameOf(GuiNode window) =>
        window.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, "name", StringComparison.OrdinalIgnoreCase))?.Value
        ?? string.Empty;

    /// <summary>Ist der Knoten ein Fenster: ein benannter Block direkt in einem obersten <c>guiTypes</c>?</summary>
    public static bool IsWindow(GuiNode node) =>
        node.Kind == GuiNodeKind.KeyedBlock && node.Parent is { } parent && IsContainer(parent) && NameOf(node).Length > 0;

    /// <summary>Das Fenster, in dem der Knoten steht (oder das er selbst ist); null außerhalb von Fenstern.</summary>
    public static GuiNode? EnclosingOf(GuiNode node) => new[] { node }.Concat(node.Ancestors()).FirstOrDefault(IsWindow);

    /// <summary>Alle Fenster dieses Namens im Index, ohne verschachtelte Elemente.</summary>
    public static IReadOnlyList<GuiDefinition> FindAll(GuiIndex index, string name) =>
        index.FindAll(name, GuiDefinitionKind.NamedWidget).Where(d => d.IsTopLevel && IsWindow(d.Node)).ToList();

    /// <summary>Das Fenster, das im Spiel gilt: nur die obersten Fenster zählen, nach der Regel des Spiels (HOI IV zuletzt, EU IV zuerst geladen).</summary>
    public static GuiDefinition? Winner(GuiIndex index, string name) => Pick(index.Layout, FindAll(index, name));

    /// <summary>Die Regel der Ladereihenfolge auf die gegebenen Fenster angewendet; null, wenn keines übrig ist.</summary>
    internal static GuiDefinition? Pick(GuiLayout layout, IEnumerable<GuiDefinition> windows) =>
        layout.FirstWins
            ? GuiIndex.FirstLoaded(windows, modsBeforeVanilla: false, layout.CaseSensitive)
            : GuiIndex.LastLoaded(windows, layout.CaseSensitive);

    private static bool IsContainer(GuiNode node) =>
        node.Kind == GuiNodeKind.KeyedBlock
        && node.Parent?.Kind == GuiNodeKind.Document
        && string.Equals(node.Name, "guiTypes", StringComparison.OrdinalIgnoreCase);
}
