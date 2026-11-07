using System.Globalization;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Text;

namespace ParadoxModTool.GuiDesign.Layout;

/// <summary>Das Bildraster einer Textur: Größe eines Bildes und Spalten × Zeilen. Bilder zählen zeilenweise ab 1.</summary>
public readonly record struct JominiFrameLayout(double FrameWidth, double FrameHeight, long Columns, long Rows)
{
    /// <summary>Anzahl der Bilder.</summary>
    public long Count => Columns * Rows;

    /// <summary>Der Ausschnitt des Bildes <paramref name="frame"/> (ab 1, zeilenweise) in Texturpixeln.</summary>
    public LayoutRect SourceOf(long frame)
    {
        var index = frame - 1;
        return new LayoutRect((index % Columns) * FrameWidth, (index / Columns) * FrameHeight, FrameWidth, FrameHeight);
    }
}

/// <summary>
/// Die Bildrechnung aus <c>framesize</c> und <c>frame_grid</c>, geteilt von der Vorschau (<see cref="JominiSprites"/>) und der Bildleiste:
/// Spalten = max(1, ⌊W/w⌋), Zeilen = max(1, ⌊H/h⌋), ein Bild hat ganze Pixel (<see cref="JominiSpriteRules.WholeFramePixels"/>, 14d).
/// </summary>
public static class JominiFrameGrid
{
    /// <summary>Raster aus der Bildgröße; die Größe wird auf ganze Pixel gebracht und auf die Textur begrenzt.</summary>
    public static JominiFrameLayout ForFrameSize(int textureWidth, int textureHeight, double frameWidth, double frameHeight) =>
        ForFrameSize(textureWidth, textureHeight, frameWidth, frameHeight, null);

    internal static JominiFrameLayout ForFrameSize(int textureWidth, int textureHeight, double frameWidth, double frameHeight, List<string>? notes)
    {
        var width = Math.Min(WholePixels(frameWidth, notes), textureWidth);
        var height = Math.Min(WholePixels(frameHeight, notes), textureHeight);
        var columns = Math.Max(1, (long)Math.Floor(textureWidth / width + 1e-9));
        var rows = Math.Max(1, (long)Math.Floor(textureHeight / height + 1e-9));
        return new JominiFrameLayout(width, height, columns, rows);
    }

    /// <summary>Raster aus Spalten und Zeilen (<c>frame_grid</c>); Werte unter 1 zählen als 1.</summary>
    public static JominiFrameLayout ForGrid(int textureWidth, int textureHeight, long columns, long rows) =>
        ForGrid(textureWidth, textureHeight, columns, rows, null);

    internal static JominiFrameLayout ForGrid(int textureWidth, int textureHeight, long columns, long rows, List<string>? notes)
    {
        columns = Math.Max(1, columns);
        rows = Math.Max(1, rows);
        return new JominiFrameLayout(
            WholePixels((double)textureWidth / columns, notes), WholePixels((double)textureHeight / rows, notes), columns, rows);
    }

    /// <summary>Die ganze Textur als ein Bild.</summary>
    public static JominiFrameLayout Whole(int textureWidth, int textureHeight) => new(textureWidth, textureHeight, 1, 1);

    /// <summary>
    /// Das Raster eines Elements: <c>framesize</c> gewinnt vor <c>frame_grid</c>, keins von beiden ist die ganze Textur.
    /// <see langword="null"/>, wenn <c>framesize</c> da ist, aber nicht aus zwei positiven Zahlen besteht (Bindung, Prozent).
    /// </summary>
    public static JominiFrameLayout? For(int textureWidth, int textureHeight, ResolvedProperty? framesize, ResolvedProperty? frameGrid) =>
        For(textureWidth, textureHeight, framesize, frameGrid, null);

    internal static JominiFrameLayout? For(int textureWidth, int textureHeight, ResolvedProperty? framesize, ResolvedProperty? frameGrid, List<string>? notes) =>
        ForItems(textureWidth, textureHeight, framesize?.Items, frameGrid?.Items, notes);

    /// <summary>
    /// Wie <see cref="For(int, int, ResolvedProperty?, ResolvedProperty?)"/>, aber mit den Einträgen als Text (z. B. live getippt im Eigenschaftenfeld);
    /// <see langword="null"/> heißt "Eigenschaft fehlt".
    /// </summary>
    public static JominiFrameLayout? ForItems(int textureWidth, int textureHeight, IReadOnlyList<string>? framesize, IReadOnlyList<string>? frameGrid) =>
        ForItems(textureWidth, textureHeight, framesize, frameGrid, null);

    private static JominiFrameLayout? ForItems(int textureWidth, int textureHeight, IReadOnlyList<string>? framesize, IReadOnlyList<string>? frameGrid, List<string>? notes)
    {
        if (framesize is not null)
        {
            if (framesize.Count >= 2
                && Number(framesize[0]) is { } w && w > 0
                && Number(framesize[1]) is { } h && h > 0)
            {
                return ForFrameSize(textureWidth, textureHeight, w, h, notes);
            }

            return null;
        }

        if (frameGrid is { Count: >= 2 } grid)
        {
            var columns = Number(grid[0]) is { } c && c >= 1 ? (long)c : 1;
            var rows = Number(grid[1]) is { } r && r >= 1 ? (long)r : 1;
            return ForGrid(textureWidth, textureHeight, columns, rows, notes);
        }

        return Whole(textureWidth, textureHeight);
    }

    /// <summary>Die Bildnummer ab 1: fehlend, 0 und negativ sind 1, sonst die abgerundete Zahl (auch in Anführungszeichen). <see langword="null"/> bei einer Bindung.</summary>
    public static long? FrameNumber(ResolvedProperty? frame) => frame is null ? 1 : FromNumber(Number(frame.Value));

    /// <summary>Wie <see cref="FrameNumber"/> für einen getippten Wert: leer heißt fehlend (1), eine Bindung <see langword="null"/>.</summary>
    public static long? FrameNumberOf(string? text) => string.IsNullOrWhiteSpace(text) ? 1 : FromNumber(Number(text));

    private static long? FromNumber(double? number) =>
        number is not { } value ? null : value < 1 ? 1 : (long)Math.Min(Math.Floor(value), int.MaxValue);

    /// <summary>Ganze Pixel (abgerundet, mindestens 1), wenn <see cref="JominiSpriteRules.WholeFramePixels"/> gilt; ab Bruchteil 0,5 mit Hinweis "frame size" in <paramref name="notes"/> (falls gegeben).</summary>
    internal static double WholePixels(double value, List<string>? notes)
    {
        if (!JominiSpriteRules.WholeFramePixels)
        {
            return value;
        }

        var whole = Math.Floor(value + 1e-9);
        if (notes is not null && value - whole >= 0.5 - 1e-9)
        {
            var note = GuiTexts.LayoutTextureApproximated("frame size");
            if (!notes.Contains(note))
            {
                notes.Add(note);
            }
        }

        return Math.Max(1, whole);
    }

    /// <summary>Eine endliche Zahl; Bindungen (<c>[…]</c>), Prozente und Namen sind keine.</summary>
    internal static double? Number(string? text) =>
        double.TryParse(text?.Trim().Trim('"').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;
}
