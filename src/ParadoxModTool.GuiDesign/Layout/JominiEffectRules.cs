namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Das Spielverhalten bei Effekten (<c>modify_texture</c>, <c>glow</c>, <c>effectname</c>), an einer Stelle. Probefenster 8 (Lauf 7, Fälle 16a–16y, in allen
/// drei Spielen gleich) hat einen Teil der Werte gemessen; die übrigen sind Annahmen. Die Fälle stehen an jeder Eigenschaft. Die Werte sind
/// Prozess-Einstellungen und werden nur beim Start gesetzt, nicht während einer Berechnung. Auch nach der Messung bleibt der Hinweis "Effekte angenähert"
/// (nur Sichtvergleich).
/// </summary>
public static class JominiEffectRules
{
    /// <summary>
    /// Mischart einer Ebene ohne oder mit unbekanntem <c>blend_mode</c>. Gemessen, Lauf 7, Fall 16x (<c>fx_base</c> mit Ebene <c>fx_layer</c> ohne
    /// <c>blend_mode</c>): genau wie <c>add</c>.
    /// </summary>
    public static JominiBlendMode DefaultBlendMode { get; set; } = JominiBlendMode.Add;

    /// <summary>
    /// Der Kanal der Ebene, mit dem <c>alphamultiply</c> das Alpha des Grundbilds multipliziert. Gemessen, Lauf 7, Fall 16y (<c>fx_alpha</c>, Farbe und Alpha
    /// gegenläufig, mit <c>alpha = 0.5</c>): Ebenen-Alpha 0/85/170/255 ergibt 0,5/0,67/0,83/1,0, also α = αb · (1 − o + o · m) mit dem Alphakanal als m.
    /// </summary>
    public static JominiAlphaChannel AlphaMultiplyChannel { get; set; } = JominiAlphaChannel.Alpha;

    /// <summary>Der Kanal der Ebene, den <c>mask</c> als Maske liest. Gemessen, Lauf 7, Fall 16i (<c>fx_layer</c> mit <c>blend_mode = mask</c>).</summary>
    public static JominiAlphaChannel MaskChannel { get; set; } = JominiAlphaChannel.Alpha;

    /// <summary>
    /// Gewichte für Rot, Grün und Blau beim Graustufen-Effekt (<see cref="JominiBlend.Grayscale"/>) und für den Kanal <see cref="JominiAlphaChannel.Luminance"/>.
    /// Annahme; wirkt nur mit <see cref="GrayscaleApplies"/>.
    /// </summary>
    public static (double R, double G, double B) GrayscaleWeights { get; set; } = (0.299, 0.587, 0.114);

    /// <summary>
    /// Graustufen-Namen in <c>effectname</c> (<c>PdxGuiGrayscale</c>, <c>PdxGuiDesaturate</c>, <c>GreyedOut</c>, …) zeichnen grau. Gemessen, Lauf 7, Fälle
    /// 16u–16w (<c>fx_base</c> mit diesen drei Namen): in allen drei Spielen ohne sichtbare Wirkung auf ein Icon. Deshalb aus; die Elemente bekommen
    /// den Hinweis "Effekt angenähert (effectname)". Der Graustufen-Weg bleibt für ein späteres Einschalten erhalten.
    /// </summary>
    public static bool GrayscaleApplies { get; set; }

    /// <summary>
    /// Die Farbe des Elements (<c>color</c> × <c>tintcolor</c>, <see cref="JominiSpriteVisual.Tint"/> des Grundbilds) multipliziert das Ergebnis nach allen
    /// Ebenen; sonst färbt sie nur das Grundbild vor den Ebenen. Gemessen, Lauf 7, Fall 16l (Icon <c>color = { 0.5 1 1 1 }</c> mit einer
    /// <c>overlay</c>-Ebene: danach gibt 123, davor wären es 197).
    /// </summary>
    public static bool TintAfterLayers { get; set; } = true;

    /// <summary>
    /// Faktor für den Radius des Leuchtens: Kastenfilter-Radius in GUI-Einheiten = (<c>glow_radius</c> − <see cref="GlowRadiusOffset"/>) × Faktor, ein
    /// Durchgang (<see cref="JominiEffectComposer.BlurRadius"/>). Gemessen, Lauf 7, Fälle 16r–16t (<c>fx_square</c>, Dichte 1, mit <c>glow_radius</c> 3, 8
    /// und 3 bei <c>alpha</c> 1, 1 und 0,3): bei 8 fällt das Leuchten linear von 0,48 an der Kante auf 0 nach 6 Einheiten (0,48 0,40 0,31 0,23 0,14 0,06),
    /// also ein Kastenfilter mit Radius 6; bei 3 ist es fast unsichtbar (0,02 an der Kante), also Radius 0. Jeder Faktor in [1,1; 1,3) gibt beide Messungen.
    /// </summary>
    public static double GlowRadiusScale { get; set; } = 1.2;

    /// <summary>
    /// Abzug von <c>glow_radius</c> in GUI-Einheiten, bevor <see cref="GlowRadiusScale"/> wirkt; bis zu diesem Wert zeichnet das Leuchten nichts. Gemessen,
    /// Lauf 7, Fall 16r (<c>glow_radius = 3</c>: höchstens 0,02 Alpha in der ersten Einheit) zusammen mit 16s (siehe <see cref="GlowRadiusScale"/>).
    /// </summary>
    public static double GlowRadiusOffset { get; set; } = 3.0;

    /// <summary>
    /// <c>mirror</c> am Element spiegelt das ganze Ergebnis samt Ebenen; sonst nur das Grundbild, die Ebenen bleiben. Gemessen, Lauf 7, Fall 16p
    /// (<c>fx_base</c> mit <c>mirror = horizontal</c> und der Ebene <c>fx_arrow</c>, links rot, rechts blau).
    /// </summary>
    public static bool ElementMirrorFlipsLayers { get; set; } = true;

    /// <summary>
    /// Eine spätere Ebene mit demselben <c>name</c> ersetzt die frühere an deren Stelle; sonst gelten beide. Gemessen, Lauf 7, Fall 16q (Typ
    /// <c>pmt_fx_icon</c> mit Ebene <c>name = a</c> (normal), Instanz mit einer zweiten <c>name = a</c> (multiply, grün)): beide gelten.
    /// </summary>
    public static bool SameNameLayerReplaces { get; set; }
}
