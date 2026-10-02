namespace ParadoxModTool.Modifiers;

/// <summary>
/// Wie das Spiel den Wert eines Modifiers anzeigt (siehe docs/superpowers/specs/2026-09-26-modifier-values-design.md).
/// </summary>
/// <param name="Percent">Wert × 100 mit " %" (<c>percent = yes</c>).</param>
/// <param name="AlreadyPercent">Wert unverändert mit " %" (<c>already_percent = yes</c>).</param>
/// <param name="Decimals">Nachkommastellen, auf die gerundet wird.</param>
/// <param name="Factor">Umrechnung ohne Prozent, z. B. 1000 für Werte, die das Spiel in Tausend speichert.</param>
/// <param name="NoDifferenceSign">Kein "+" vor positiven Werten (<c>no_difference_sign = yes</c>).</param>
/// <param name="TrimTrailingZeros">Nullen am Ende der Nachkommastellen weglassen.</param>
/// <param name="Estimated">Format ist geschätzt (z. B. aus der Wiki), nicht aus den Spieldateien.</param>
public sealed record ModifierFormat(bool Percent, bool AlreadyPercent, int Decimals, decimal Factor, bool NoDifferenceSign, bool TrimTrailingZeros, bool Estimated);
