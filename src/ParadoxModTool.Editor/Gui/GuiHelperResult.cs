namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Ergebnis des GUI-Daten-Helfers. <see cref="IsCondition"/>: kam aus dem Tab "Bedingung" und ersetzt den ganzen Wert; sonst (Kette, Nachschlagen) wird
/// der Ausdruck wie beim Daten-Helfer an der Cursorstelle eingefügt.
/// </summary>
public sealed record GuiHelperResult(string Expression, bool IsCondition);
