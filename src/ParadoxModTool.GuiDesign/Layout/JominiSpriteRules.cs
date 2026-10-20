namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Das Spielverhalten bei Sprites, an einer Stelle. Gemessen mit Probefenster 6 (Lauf 5, 2026-10-20) in CK III, Victoria 3 und EU V; die Fälle stehen
/// an jeder Eigenschaft, die Messwerte in <c>layout-probe-notes.md</c>, Abschnitt "Lauf 5". Die Werte sind Prozess-Einstellungen und werden nur beim
/// Start gesetzt, nicht während einer Berechnung. Gemessen und ohne eigene Eigenschaft, weil die Engine es ohnehin so macht: <c>mirror</c> spiegelt das
/// gezeichnete Ergebnis samt Rändern (13i–13k), <c>spriteborder_left/right/top/bottom</c> überschreiben <c>spriteborder</c> (13g), zu große Ränder
/// schrumpfen als Paar (13h: 8 + 8 in 12 gibt 6 + 6), die natürliche Größe bleibt nach der Teilung durch <c>texture_density</c> gebrochen (14c: 6 × 16,67 = 100).
/// </summary>
public static class JominiSpriteRules
{
    /// <summary>
    /// <c>spriteborder</c> gilt in GUI-Einheiten; in der Textur sind es <c>spriteborder</c> × <c>texture_density</c> Pixel (13b, alle drei Spiele: bei Dichte 2
    /// und Rand 8 liegen die W-Marken der Textur bei 6/1, 4/4 und 1/6, also unverkleinert im 8 Einheiten breiten Rand aus 16 Texturpixeln; die Mitte
    /// streckt nur die Pixel 16–24). Falsch hieße: Rand in Texturpixeln, im Kasten ÷ Dichte (die Annahme vor der Messung).
    /// </summary>
    public static bool BorderInGuiUnits { get; set; } = true;

    /// <summary>Kachelgröße = Quellpixel ÷ <c>texture_density</c> (13d: 40 Pixel bei Dichte 2 kacheln mit 20 Einheiten).</summary>
    public static bool TileDividedByDensity { get; set; } = true;

    /// <summary>
    /// <c>spriteborder</c> ohne <c>spriteType</c> heißt <c>corneredstretched</c>. Gemessen: nein, das Bild wird als Ganzes gestreckt (13e, gleich 13f mit
    /// <c>spriteType = stretched</c>).
    /// </summary>
    public static bool BorderWithoutTypeSlices { get; set; }

    /// <summary><c>alpha</c> eines Elternelements gilt auch für die Kinder (13x: widget mit <c>alpha = 0.5</c> und weißem Kind gleich 13w).</summary>
    public static bool AlphaInherited { get; set; } = true;

    /// <summary>Ein <c>frame</c> über der Bildanzahl zeigt das letzte Bild (14b: <c>frame = 9</c> bei 4 Bildern zeigt Bild 4).</summary>
    public static bool FrameBeyondCountIsLast { get; set; } = true;

    /// <summary>
    /// Ein Bild (<c>framesize</c>, <c>frame_grid</c>) ist ganze Texturpixel groß: 14d, alle drei Spiele, 6 × 33 = 198 bei <c>frame_grid = { 3 1 }</c> auf 100
    /// Pixeln (EU V) und bei <c>framesize = { 33.3333 20 }</c> (CK III, Victoria 3). Abgerundet; ob das Spiel abrundet oder rundet, zeigt 33,33 nicht.
    /// </summary>
    public static bool WholeFramePixels { get; set; } = true;

    /// <summary>Wurden die Regeln im Spiel gemessen? Dann entfallen die allgemeinen Annäherungs-Hinweise; offene Fälle melden sich weiter selbst.</summary>
    public static bool Measured { get; set; } = true;
}
