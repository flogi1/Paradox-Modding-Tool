using System.Globalization;

namespace ParadoxModTool.Modifiers;

/// <summary>Macht aus dem Wert im Skript den Text, den das Spiel anzeigt (z. B. <c>0.1</c> mit Prozent → "+10 %").</summary>
public static class ModifierValueFormatter
{
    public static string Format(decimal value, ModifierFormat format)
    {
        var shown = format.Percent ? value * 100 : format.AlreadyPercent ? value : value * format.Factor;
        var decimals = Math.Max(0, format.Decimals);
        var rounded = Math.Round(shown, decimals, MidpointRounding.AwayFromZero);
        var unit = format.Percent || format.AlreadyPercent ? " %" : string.Empty;
        if (rounded == 0)
        {
            return 0m.ToString(Pattern(decimals, format.TrimTrailingZeros), CultureInfo.InvariantCulture) + unit;
        }

        var text = rounded.ToString(Pattern(decimals, format.TrimTrailingZeros), CultureInfo.InvariantCulture);
        var sign = rounded > 0 && !format.NoDifferenceSign ? "+" : string.Empty;
        return sign + text + unit;
    }

    private static string Pattern(int decimals, bool trim) =>
        decimals == 0 ? "0" : "0." + new string(trim ? '#' : '0', decimals);
}
