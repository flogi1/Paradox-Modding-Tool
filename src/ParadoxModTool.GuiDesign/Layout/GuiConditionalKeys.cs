using System.IO;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Schlüssel ausgeblendeter bedingter Elemente (<c>visible = "[…]"</c>): <c>FilePrefix(angesehene Datei) + RootPart(Vorschau-Wurzel) + "|"</c> und die
/// Schritte von der Vorschau-Wurzel bis zum Element im aufgelösten Baum, mit <c>/</c> verbunden. Die Herkunft (<see cref="ResolvedElement.Origin"/>)
/// zählt bewusst nicht: ein Kind aus einem Typ- oder Template-Rumpf stammt aus der Bibliotheksdatei und wäre sonst in jeder Datei und jeder Instanz
/// ausgeblendet. Designer und Inspektor können für dasselbe Element verschiedene Schlüssel haben (andere Vorschau-Wurzel); jeder merkt sich seinen.
/// </summary>
public static class GuiConditionalKeys
{
    /// <summary>Normalisierter, kleingeschriebener voller Pfad der angesehenen Datei (ohne Datei leer) und <c>"|"</c>.</summary>
    public static string FilePrefix(string? viewedFile)
    {
        if (string.IsNullOrEmpty(viewedFile))
        {
            return "|";
        }

        string full;
        try
        {
            full = Path.GetFullPath(viewedFile);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            full = viewedFile;
        }

        return full.ToLowerInvariant() + "|";
    }

    /// <summary>Weg der Vorschau-Wurzel in ihrer Datei (<see cref="GuiNodePath"/>): Schritte <c>Art:Name:Nummer</c>, mit <c>/</c> verbunden.</summary>
    public static string RootPart(GuiNode previewRoot) =>
        string.Join("/", GuiNodePath.Of(previewRoot).Steps.Select(step => $"{step.Kind}:{step.Name}:{step.Occurrence}"));

    /// <summary>Ein Schritt im aufgelösten Baum: <c>Schlüssel:Name:Nummer</c>; die Nummer zählt frühere Geschwister mit gleichem Schlüssel und Namen.</summary>
    public static string Step(ResolvedElement element, int occurrence) => $"{element.Key}:{element.Name}:{occurrence}";

    /// <summary><c>visible</c> ist gesetzt und weder <c>yes</c> noch <c>no</c> (nicht auswertbarer Ausdruck).</summary>
    public static bool IsConditional(ResolvedElement element) =>
        element.Property("visible")?.Value is { } visible && visible != "yes" && visible != "no";

    /// <summary>
    /// Schlüssel aller bedingten Elemente unter <paramref name="root"/> (die Wurzel eingeschlossen; sie hat keine eigenen Schritte). Gezählt wird über alle
    /// Kinder im aufgelösten Baum, auch unsichtbare und Halter, damit ein ausgeblendetes Geschwister die Nummern der anderen nicht verschiebt.
    /// </summary>
    internal static IReadOnlyDictionary<ResolvedElement, string> Collect(ResolvedElement root, string? viewedFile, GuiNode? previewRoot)
    {
        var keys = new Dictionary<ResolvedElement, string>(ReferenceEqualityComparer.Instance);
        var prefix = FilePrefix(viewedFile) + (previewRoot is null ? string.Empty : RootPart(previewRoot)) + "|";
        var stack = new Stack<(ResolvedElement Element, string Key)>();
        stack.Push((root, prefix));
        while (stack.Count > 0)
        {
            var (element, key) = stack.Pop();
            if (IsConditional(element) && !keys.ContainsKey(element))
            {
                keys[element] = key;
            }

            var seen = new Dictionary<(string Key, string? Name), int>();
            foreach (var child in element.Children)
            {
                var id = (child.Key, child.Name);
                seen.TryGetValue(id, out var occurrence);
                seen[id] = occurrence + 1;
                var separator = ReferenceEquals(element, root) ? string.Empty : "/";
                stack.Push((child, key + separator + Step(child, occurrence)));
            }
        }

        return keys;
    }
}
