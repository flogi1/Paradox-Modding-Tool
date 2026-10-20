using System.Globalization;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Die Farbe einer Textur (<c>color</c>/<c>tintcolor</c>), gemessen mit Probefenster 6 (Lauf 5): Jede Komponente gilt als 0–1 und wird auf 0–1 begrenzt.
/// <list type="bullet">
/// <item>Victoria 3 und EU V: <c>{ 2 2 2 1 }</c> auf Grau 128 bleibt 128 (13s), <c>{ 200 20 0 1 }</c> auf Weiß ist Gelb (13t). Es gibt also weder
/// Aufhellen über 1 noch eine 0–255-Schreibweise.</item>
/// <item>CK III weicht ab: 13t zeigt (55, 236, 0) und 13s 127. Das passt zu einem Überlauf beim Umrechnen in ein Byte ((200 × 255) mod 256 = 56,
/// (20 × 255) mod 256 = 236, (2 × 255) mod 256 = 254); eine Vermutung aus drei Werten. Die Vorschau begrenzt auch dort und nennt jede Komponente über 1 im
/// Hinweis <see cref="GuiTexts.LayoutTextureApproximated"/> ("color &gt; 1, CK III").</item>
/// </list>
/// Alpha folgt derselben Regel (gemessen nur 0,5: 13u); Alpha über 1 ist in keinem Spiel gemessen und bekommt den Hinweis "alpha &gt; 1". Das Mischen über den Hintergrund geschieht in sRGB (13u, 13w: 165 über 76).
/// </summary>
public static class JominiTextureColor
{
    /// <summary>Hinweistext für Komponenten über 1 (CK III rechnet sie anders, siehe Klassenbeschreibung).</summary>
    internal const string AboveOneNote = "color > 1, CK III";

    /// <summary>Hinweistext für Alpha über 1 (in keinem Spiel gemessen).</summary>
    internal const string AlphaAboveOneNote = "alpha > 1";

    /// <summary>
    /// Liest die Farbe; null bei fehlender Eigenschaft (ohne Hinweis) und bei Namen oder Datenbindungen sowie unlesbaren Tupeln (mit Hinweis
    /// <see cref="GuiTexts.LayoutTextureApproximated"/>).
    /// </summary>
    public static JominiColor? Parse(ResolvedProperty? property, List<string> notes)
    {
        if (property is null)
        {
            return null;
        }

        var items = property.Items;
        if (items.Count < 3 || items.Count > 4)
        {
            AddOnce(notes, GuiTexts.LayoutTextureApproximated("color"));
            return null;
        }

        var numbers = new double[items.Count];
        for (var i = 0; i < numbers.Length; i++)
        {
            if (!double.TryParse(items[i].Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]) || !double.IsFinite(numbers[i]))
            {
                AddOnce(notes, GuiTexts.LayoutTextureApproximated("color"));
                return null;
            }
        }

        if (numbers[0] > 1 || numbers[1] > 1 || numbers[2] > 1)
        {
            AddOnce(notes, GuiTexts.LayoutTextureApproximated(AboveOneNote));
        }

        var alpha = numbers.Length > 3 ? numbers[3] : 1;
        if (alpha > 1)
        {
            AddOnce(notes, GuiTexts.LayoutTextureApproximated(AlphaAboveOneNote));
        }

        return new JominiColor(Clamp(numbers[0]), Clamp(numbers[1]), Clamp(numbers[2]), Clamp(alpha));
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 1);

    private static void AddOnce(List<string> notes, string note)
    {
        if (!notes.Contains(note))
        {
            notes.Add(note);
        }
    }
}
