namespace ParadoxModTool.GuiDesign.Blocks;

public static class GuiBlockInsertion
{
    /// <summary>
    /// Bereitet erzeugten Code zum Einfügen an der Cursorstelle vor (Plan-Ruling 8): <c>\n</c> wird zum Zeilenende des Dokuments, jede nicht leere
    /// Zeile nach der ersten und das Stück nach dem letzten Umbruch bekommen die Einrückung der Cursorzeile; leere Zwischenzeilen bleiben leer.
    /// </summary>
    public static string ForCaret(string text, string lineIndent, string newline)
    {
        var lines = text.Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0 || i == lines.Length - 1)
            {
                lines[i] = lineIndent + lines[i];
            }
        }

        return string.Join(newline, lines);
    }
}
