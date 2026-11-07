using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>
/// Die Randskizze: das Bild in den Skizzenkasten eingepasst (<see cref="Scale"/>, Versatz) und die vier Linien in Skizzenpixeln.
/// <see cref="Left"/>/<see cref="Top"/> sind x/y der linken/oberen Linie, <see cref="Right"/>/<see cref="Bottom"/> x/y der rechten/unteren.
/// </summary>
public sealed record JominiBorderSketch(
    double Scale, double OffsetX, double OffsetY, double ImageWidth, double ImageHeight, double Left, double Top, double Right, double Bottom);

/// <summary>Reine Geometrie der Randskizze im Eigenschaftenfeld (<c>spriteborder</c>).</summary>
public static class JominiBorderSketchGeometry
{
    /// <summary>
    /// Der Rand des Elements wie in der Vorschau (<c>spriteborder</c>, dann die Einzelwerte); gibt es <paramref name="liveName"/>, gelten für diese Zeile
    /// <paramref name="liveValues"/> (getippt, noch nicht übernommen). Unlesbar und negativ zählt als 0.
    /// </summary>
    public static JominiBorder BorderOf(ResolvedElement element, string? liveName, IReadOnlyList<string>? liveValues) =>
        JominiSprites.ReadBorder(element, new List<string>(), liveName, liveValues);

    /// <summary>
    /// Passt den Ausschnitt <paramref name="source"/> (Seitenverhältnis bleibt, mittig) in <paramref name="maxWidth"/> × <paramref name="maxHeight"/> ein.
    /// Ein Randwert ist in Texturpixeln Wert × <paramref name="density"/> (<see cref="JominiSpriteRules.BorderInGuiUnits"/>), sonst der Wert selbst;
    /// jede Scheibe wird auf den Ausschnitt begrenzt (nicht unter 0, nicht über Breite bzw. Höhe).
    /// </summary>
    public static JominiBorderSketch Fit(LayoutRect source, double density, JominiBorder border, double maxWidth, double maxHeight)
    {
        var scale = source.Width > 0 && source.Height > 0 ? Math.Min(maxWidth / source.Width, maxHeight / source.Height) : 1;
        var imageWidth = source.Width * scale;
        var imageHeight = source.Height * scale;
        var offsetX = (maxWidth - imageWidth) / 2;
        var offsetY = (maxHeight - imageHeight) / 2;

        var factor = JominiSpriteRules.BorderInGuiUnits ? density : 1;
        double Slice(double value, double limit) => Math.Clamp(double.IsFinite(value) ? value * factor : 0, 0, Math.Max(0, limit));

        var left = Slice(border.Left, source.Width);
        var right = Slice(border.Right, source.Width);
        var top = Slice(border.Top, source.Height);
        var bottom = Slice(border.Bottom, source.Height);
        return new JominiBorderSketch(
            scale, offsetX, offsetY, imageWidth, imageHeight,
            offsetX + left * scale, offsetY + top * scale,
            offsetX + (source.Width - right) * scale, offsetY + (source.Height - bottom) * scale);
    }
}
