using System.Globalization;
using ParadoxModTool.GuiDesign.Layout;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Welche Farbregel ein Farbfeld hat: die der Texturen (<c>color</c>, <c>tintcolor</c>) oder die des Textes (<c>fontcolor</c>).</summary>
public enum JominiColorKind
{
    Texture,
    Text,
}

/// <summary>
/// Eine gelesene Farbe. <see cref="Shown"/> ist auf 0–1 begrenzt (Farbfeld), <see cref="ByteForm"/> sagt, ob die Datei das Tupel in 0–255 schreibt,
/// <see cref="AboveOne"/> ob eine RGB-Komponente über 1 steht (die Vorschau begrenzt sie), <see cref="AlphaText"/> ist das Alpha wie geschrieben.
/// </summary>
public sealed record JominiColorValue(GuiColor Shown, bool ByteForm, bool AboveOne, int Components, string AlphaText);

/// <summary>
/// Liest eine Farbe aus dem Eingabetext so, wie die Vorschau sie zeichnet, und schreibt eine gewählte Farbe in der Form des bisherigen Werts.
/// Texturfarben (Regel gemessen, Lauf 5) gelten immer als 0–1 und werden begrenzt, sie haben also keine 0–255-Form; Textfarben (<c>fontcolor</c>)
/// gelten als 0–255, sobald eine Komponente über 1 steht.
/// </summary>
public static class JominiColorForm
{
    /// <summary>Regel des Namens: <c>color</c>/<c>tintcolor</c> = Textur, <c>fontcolor</c> = Text, sonst null.</summary>
    public static JominiColorKind? KindOf(string propertyName) => propertyName switch
    {
        "color" or "tintcolor" => JominiColorKind.Texture,
        "fontcolor" => JominiColorKind.Text,
        _ => null,
    };

    /// <summary>Null bei Namen, Datenbindungen und nicht lesbarem Text.</summary>
    public static JominiColorValue? Read(string input, JominiColorKind kind)
    {
        var items = input.Trim().TrimStart('{').TrimEnd('}').Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Trim('"')).ToArray();
        if (items.Length is not (3 or 4) || input.Contains('['))
        {
            return null;
        }

        var numbers = new double[items.Length];
        for (var i = 0; i < numbers.Length; i++)
        {
            if (!double.TryParse(items[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]) || !double.IsFinite(numbers[i]))
            {
                return null;
            }
        }

        JominiColor? color;
        var byteForm = false;
        var aboveOne = false;
        if (kind == JominiColorKind.Texture)
        {
            color = JominiTextureColor.ParseItems(items, new List<string>());
            aboveOne = numbers[0] > 1 || numbers[1] > 1 || numbers[2] > 1;
        }
        else
        {
            color = JominiTextService.FontColor(items, out byteForm);
        }

        if (color is not { } c)
        {
            return null;
        }

        var shown = new GuiColor(Clamp(c.R), Clamp(c.G), Clamp(c.B), Clamp(c.A));
        return new JominiColorValue(shown, byteForm, aboveOne, items.Length, items.Length > 3 ? items[3] : string.Empty);
    }

    /// <summary>
    /// Die gewählte Farbe in der Form von <paramref name="original"/>: 0–255-Form gibt ganze Zahlen, sonst 0–1 mit höchstens drei Nachkommastellen;
    /// Alpha bleibt wie geschrieben, ein 3er-Tupel bleibt eines. Ohne bisherigen Wert (Name, Bindung) kommt <c>{ r g b 1 }</c> in 0–1.
    /// <para>
    /// Die 0–255-Form gilt für die Textregel nur, solange eine Komponente über 1 steht. Ist die gewählte Farbe fast schwarz (alle RGB ≤ 1) und hält
    /// auch das bisherige Alpha die Form nicht (≤ 1 oder fehlend), läse die Regel das Tupel als 0–1 (hellrot statt schwarz). Dann wird die ganze Farbe
    /// in 0–1 geschrieben, Alpha als der bisher gezeigte Wert.
    /// </para>
    /// </summary>
    public static string FormatLike(byte r, byte g, byte b, JominiColorValue? original)
    {
        var byteForm = original is { ByteForm: true };
        var alpha = original is { AlphaText.Length: > 0 } ? original.AlphaText : "1";
        if (byteForm && r <= 1 && g <= 1 && b <= 1 && !AlphaAboveOne(original!))
        {
            byteForm = false;
            alpha = Fraction(original!.Shown.A);
        }

        string Part(byte v) => byteForm ? v.ToString(CultureInfo.InvariantCulture) : Fraction(v / 255.0);

        var rgb = $"{Part(r)} {Part(g)} {Part(b)}";
        if (original is { Components: 3 })
        {
            return $"{{ {rgb} }}";
        }

        return $"{{ {rgb} {alpha} }}";
    }

    /// <summary>Steht das geschriebene Alpha über 1 (hält also allein die 0–255-Form)?</summary>
    private static bool AlphaAboveOne(JominiColorValue original) =>
        original.Components > 3
        && double.TryParse(original.AlphaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha)
        && alpha > 1;

    private static string Fraction(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    private static double Clamp(double value) => Math.Clamp(value, 0, 1);
}
