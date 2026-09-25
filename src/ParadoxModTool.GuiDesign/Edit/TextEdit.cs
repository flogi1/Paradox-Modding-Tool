using System.Text;

namespace ParadoxModTool.GuiDesign.Edit;

/// <summary>Ersetzt <c>Length</c> Zeichen ab <c>Offset</c> durch <c>Text</c> (Länge 0 = Einfügen).</summary>
public readonly record struct TextEdit(int Offset, int Length, string Text)
{
    /// <summary>Wendet nicht überlappende Ersetzungen an, in der Reihenfolge von <see cref="InApplyOrder"/>.</summary>
    public static string Apply(string text, IEnumerable<TextEdit> edits)
    {
        var builder = new StringBuilder(text);
        foreach (var edit in InApplyOrder(edits))
        {
            builder.Remove(edit.Offset, edit.Length);
            builder.Insert(edit.Offset, edit.Text);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reihenfolge des Anwendens: von hinten nach vorn, damit die Offsets gültig bleiben; bei gleichem Offset stabil in Listenreihenfolge
    /// (ein Entfernen vor einem Einfügen an derselben Stelle muss deshalb zuerst in der Liste stehen). Der Editor wendet genauso an.
    /// </summary>
    public static IReadOnlyList<TextEdit> InApplyOrder(IEnumerable<TextEdit> edits) => edits.OrderByDescending(e => e.Offset).ToList();

    /// <summary>
    /// Stelle im neuen Text für eine Stelle des alten Texts. Liegt sie in einem ersetzten Bereich, gilt der Anfang der Ersetzung; eine
    /// Einfügung genau an der Stelle schiebt sie nach hinten.
    /// </summary>
    public static int MapOffset(int offset, IEnumerable<TextEdit> edits)
    {
        var shift = 0;
        foreach (var edit in edits)
        {
            if (edit.Offset + edit.Length <= offset)
            {
                shift += edit.Text.Length - edit.Length;
            }
            else if (edit.Offset < offset)
            {
                shift += edit.Offset - offset;
            }
        }

        return offset + shift;
    }

    /// <summary>Stelle im neuen Text, an der Zeichen <paramref name="indexInText"/> der Ersetzung <paramref name="editIndex"/> steht.</summary>
    public static int PositionInResult(IReadOnlyList<TextEdit> edits, int editIndex, int indexInText)
    {
        var target = edits[editIndex];
        var position = target.Offset + indexInText;
        for (var i = 0; i < edits.Count; i++)
        {
            var other = edits[i];

            // Weiter vorn liegende Ersetzungen verschieben; bei gleichem Offset landen später angewendete (spätere in der Liste) davor.
            if (i != editIndex && (other.Offset < target.Offset || (other.Offset == target.Offset && i > editIndex)))
            {
                position += other.Text.Length - other.Length;
            }
        }

        return position;
    }
}

public enum InsertPosition
{
    FirstChild,
    LastChild,
    Before,
    After,
}
