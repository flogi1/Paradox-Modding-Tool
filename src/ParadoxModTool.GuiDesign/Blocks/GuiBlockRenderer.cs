using System.Text;

namespace ParadoxModTool.GuiDesign.Blocks;

/// <summary>
/// Erzeugt aus Baustein und Eingaben fertigen GUI-Code: alle Platzhalter eingesetzt, Wiederholungen aufgelöst, Zeilenenden <c>\n</c>, Einrückung wie
/// im Rumpf. Das Einrücken an der Zielstelle macht der Aufrufer.
/// </summary>
public static class GuiBlockRenderer
{
    public static string Render(GuiBlock block, GuiBlockValues values)
    {
        if (!block.IsValid)
        {
            throw new ArgumentException($"Block '{block.Name}' has errors and cannot be rendered.", nameof(block));
        }

        var text = new StringBuilder();
        Append(text, block.Body, values);
        return text.ToString();
    }

    private static void Append(StringBuilder text, IReadOnlyList<GuiBlockPart> parts, GuiBlockValues values)
    {
        foreach (var part in parts)
        {
            switch (part)
            {
                case GuiBlockText plain:
                    text.Append(plain.Text);
                    break;
                case GuiBlockPlaceholder placeholder:
                    text.Append(values.ValueOf(placeholder.Key));
                    break;
                case GuiBlockSection section:
                    foreach (var entry in values.EntriesOf(section.RepeatKey))
                    {
                        Append(text, section.Parts, entry);
                    }

                    break;
            }
        }
    }
}
