using System.Text;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>Text klassischer Fenster: Farbcodes und Zeilenumbruch, gemeinsam für Layout (Messen) und Vorschau (Zeichnen).</summary>
public static class ClassicText
{
    /// <summary>Text ohne Farbcodes (<c>§Y</c> … <c>§!</c>): <c>§</c> und das folgende Zeichen fallen weg. Die Farbe selbst zeigt die Vorschau nicht (angenähert).</summary>
    public static string StripColorCodes(string line)
    {
        if (!line.Contains('§'))
        {
            return line;
        }

        var result = new StringBuilder(line.Length);
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '§')
            {
                i++;
                continue;
            }

            result.Append(line[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Ordnet einen <c>format</c>-Wert (schon ohne Anführungszeichen, klein geschrieben) der waagerechten Ausrichtung zu, die die Vorschau zeichnet. Die Vorschau
    /// zeichnet Text von oben, daher brauchen <c>upper_*</c> keinen Hinweis; <c>up</c>/<c>down</c>/<c>lower_*</c> sind nicht gemessen
    /// (<paramref name="verticalNote"/>); alles andere ist unbekannt (<paramref name="unknown"/>) und wird linksbündig gezeigt.
    /// </summary>
    public static string HorizontalFormat(string format, out bool verticalNote, out bool unknown)
    {
        verticalNote = false;
        unknown = false;
        switch (format)
        {
            case "left" or "upper_left":
                return "left";
            case "right" or "upper_right":
                return "right";
            case "centre" or "center":
                return format;
            case "lower_right":
                verticalNote = true;
                return "right";
            case "lower_left" or "down" or "up":
                verticalNote = true;
                return "left";
            default:
                unknown = true;
                return "left";
        }
    }

    /// <summary>
    /// Bricht eine Zeile an Leerzeichen um, sodass jede Zeile höchstens <paramref name="maxWidth"/> breit ist (gemessen ohne Farbcodes). Ein Wort, das allein
    /// breiter ist, bleibt eine eigene, zu breite Zeile.
    /// </summary>
    public static List<string> Wrap(string line, double maxWidth, Func<string, double> measure)
    {
        var words = line.Split(' ');
        var lines = new List<string>();
        var current = words[0];
        for (var i = 1; i < words.Length; i++)
        {
            var candidate = current + " " + words[i];
            if (measure(StripColorCodes(candidate)) <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = words[i];
            }
        }

        lines.Add(current);
        return lines;
    }
}
