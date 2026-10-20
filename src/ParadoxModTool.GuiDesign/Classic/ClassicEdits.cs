using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Minimale Textänderungen für klassische Dateien (HOI IV, EU IV): Paar-Blöcke ändern nur die beiden Werte, Abstände wie <c>{ x= 188 y = -22 }</c> bleiben.
/// Schlüssel gelten ohne Rücksicht auf Groß-/Kleinschreibung.
/// </summary>
public static class ClassicEdits
{
    /// <summary>
    /// Setzt ein Paar. Ist <paramref name="target"/> der Block <paramref name="blockName"/> selbst (auch in einem <c>if_resolution</c>-Block), ändern sich nur
    /// seine zwei inneren Werte; ein fehlender innerer Schlüssel wird ergänzt. Ist es das Element, wird dessen Block so geändert, oder ein neuer
    /// <c>name = { A = a B = b }</c> eingefügt.
    /// </summary>
    public static GuiDesignEdit SetPair(GuiDocument document, GuiNode target, string blockName, ClassicPairKeys keys, string first, string second)
    {
        if (IsBlockNamed(target, blockName))
        {
            return SetInner(document, target, keys, first, second);
        }

        // Das Element hat den Block schon (nur direkt darin, nie in if_resolution): dort ändern statt ersetzen, damit die Abstände bleiben.
        var existing = target.Children.LastOrDefault(c => IsBlockNamed(c, blockName));
        if (existing is not null)
        {
            return SetInner(document, existing, keys, first, second);
        }

        var edits = new GuiTextEdits(document).SetProperty(target, blockName, $"{{ {keys.First} = {first} {keys.Second} = {second} }}", ignoreNameCase: true);
        return new GuiDesignEdit(edits, TextEdit.MapOffset(target.Span.Start, edits));
    }

    /// <summary>"Grundwert ändern": setzt die Zeile direkt im Element (ohne Rücksicht auf Groß-/Kleinschreibung), nie eine in einem <c>if_resolution</c>-Block.</summary>
    public static GuiDesignEdit SetBaseValue(GuiDocument document, GuiNode element, string name, string valueText) =>
        GuiDesignerEdits.SetProperty(document, element, name, valueText, ignoreNameCase: true);

    private static bool IsBlockNamed(GuiNode node, string blockName) =>
        node.Kind == GuiNodeKind.KeyedBlock && node.HasBody && string.Equals(node.Name, blockName, StringComparison.OrdinalIgnoreCase);

    private static GuiDesignEdit SetInner(GuiDocument document, GuiNode block, ClassicPairKeys keys, string first, string second)
    {
        var edits = new GuiTextEdits(document);
        var firstNode = Inner(block, keys.First);
        var secondNode = Inner(block, keys.Second);
        var result = new List<TextEdit>();
        if (firstNode is not null)
        {
            result.AddRange(edits.SetValue(firstNode, first));
        }

        if (secondNode is not null)
        {
            result.AddRange(edits.SetValue(secondNode, second));
        }

        if (firstNode is null && secondNode is null)
        {
            result.AddRange(edits.InsertChild(block, $"{keys.First} = {first}\n{keys.Second} = {second}", InsertPosition.LastChild));
        }
        else if (secondNode is null)
        {
            result.AddRange(Missing(document, edits, block, firstNode!, $"{keys.Second} = {second}", after: true));
        }
        else if (firstNode is null)
        {
            result.AddRange(Missing(document, edits, block, secondNode, $"{keys.First} = {first}", after: false));
        }

        return new GuiDesignEdit(result, TextEdit.MapOffset(block.Span.Start, result));
    }

    /// <summary>Ergänzt einen fehlenden inneren Schlüssel neben dem vorhandenen: in einem einzeiligen Block in derselben Zeile, sonst als eigene Zeile.</summary>
    private static IReadOnlyList<TextEdit> Missing(GuiDocument document, GuiTextEdits edits, GuiNode block, GuiNode anchor, string line, bool after)
    {
        var singleLine = !document.Text.AsSpan(block.Span.Start, block.Span.Length).Contains('\n');
        if (!singleLine)
        {
            return edits.InsertChild(block, line, after ? InsertPosition.After : InsertPosition.Before, anchor);
        }

        return after
            ? new[] { new TextEdit(anchor.Span.End, 0, " " + line) }
            : new[] { new TextEdit(anchor.Span.Start, 0, line + " ") };
    }

    /// <summary>Die letzte passende Zeile gilt, wie in der Engine.</summary>
    private static GuiNode? Inner(GuiNode block, string key) =>
        block.Children.LastOrDefault(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase));
}
