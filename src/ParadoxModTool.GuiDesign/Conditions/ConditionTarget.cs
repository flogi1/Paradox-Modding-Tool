using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Conditions;

/// <summary>
/// Bedingungs-Eigenschaft an der Cursorstelle (<c>visible</c>, <c>enabled</c>, <c>down</c>, <c>checked</c> mit <c>[...]</c>-Wert): der Code-Editor öffnet den
/// Bedingungs-Helfer damit und ersetzt danach genau <see cref="ValueSpan"/> (samt Anführungszeichen).
/// </summary>
public sealed record ConditionTarget(string Property, TextSpan ValueSpan, string ValueText)
{
    private static readonly string[] Properties = { "visible", "enabled", "down", "checked" };

    public static bool IsConditionProperty(string name) => Properties.Contains(name, StringComparer.Ordinal);

    /// <summary>
    /// Was der Code-Editor nach dem Helfer ersetzt: den Wert der Bedingung, wenn das Ergebnis aus dem Tab "Bedingung" kommt; sonst <c>null</c> (Einfügen an
    /// der Cursorstelle).
    /// </summary>
    public static TextSpan? ReplaceRange(ConditionTarget? target, bool fromConditionTab) =>
        target is not null && fromConditionTab ? target.ValueSpan : null;

    /// <summary>Die Eigenschaft, in deren Zeile (Schlüssel bis Ende des Werts) der Cursor steht; sonst <c>null</c>.</summary>
    public static ConditionTarget? Find(GuiDocument document, int caret)
    {
        foreach (var node in document.Root.Descendants())
        {
            if (node.Kind == GuiNodeKind.Property
                && IsConditionProperty(node.Name)
                && node.ValueSpan is { } span
                && node.Span.Start <= caret
                && caret <= node.Span.End)
            {
                var text = document.TextOf(span);
                return text.Contains('[') ? new ConditionTarget(node.Name, span, text) : null;
            }
        }

        return null;
    }
}
