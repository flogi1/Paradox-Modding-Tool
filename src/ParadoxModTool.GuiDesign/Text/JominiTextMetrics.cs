namespace ParadoxModTool.GuiDesign.Text;

/// <summary>
/// Zeilenhöhen der Jomini-Spiele, gemessen in Lauf 4 (Proben 11a–11f, alle drei Spiele): ganzzahlige Höhen aus den Metriken der ersten Schriftdatei.
/// Ein Text aus n Zeilen ist <see cref="LineHeight"/> + (n − 1) × <see cref="LinePitch"/> hoch.
/// </summary>
public static class JominiTextMetrics
{
    // s × Em kann knapp neben einer ganzen Zahl landen (CK III 15 × 1,4 = 21,000…); ohne Toleranz kippen ceil/floor um 1.
    private const double Epsilon = 1e-6;

    /// <summary>Die Höhen sind gemessen (Lauf 4); es gibt keine Annäherungsnotiz mehr.</summary>
    public static bool HeightsMeasured => true;

    /// <summary>Höhe einer einzelnen Zeile: <c>ceil(s × asc) + floor(s × desc)</c> der ersten Datei (CK III bei 15: 21).</summary>
    public static double LineHeight(JominiFontFace face, double fontSize) => AscentPixels(face, fontSize) + Math.Floor(face.Descent(fontSize) + Epsilon);

    /// <summary>Abstand umbrochener Zeilen: <c>floor(s × (asc + desc))</c> der ersten Datei (CK III 21, EU V 20, Victoria 3 19 bei 15).</summary>
    public static double LinePitch(JominiFontFace face, double fontSize) => Math.Floor(face.Ascent(fontSize) + face.Descent(fontSize) + Epsilon);

    /// <summary>Abstand der Grundlinie von der Oberkante einer Zeile: <c>ceil(s × asc)</c>.</summary>
    public static double AscentPixels(JominiFontFace face, double fontSize) => Math.Ceiling(face.Ascent(fontSize) - Epsilon);

    /// <summary>Aufrunden mit derselben Toleranz, damit ein genaues Vielfaches nicht um 1 wächst.</summary>
    internal static double Ceiling(double value) => Math.Ceiling(value - Epsilon);
}
