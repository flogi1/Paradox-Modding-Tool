using ParadoxModTool.Session;

namespace ParadoxModTool.Settings;

/// <summary>Arbeitsbereich eines Bildschirms in WPF-Einheiten (ohne Taskleiste).</summary>
public sealed record ScreenArea(double Left, double Top, double Width, double Height)
{
    public double Area => Width * Height;

    public bool Contains(double x, double y) => x >= Left && x < Left + Width && y >= Top && y < Top + Height;
}

/// <summary>Wo ein Fenster ohne gemerkte Position erscheint (Fehlerprotokoll als eigenes Fenster, Plan 2b Ruling 6).</summary>
public static class WindowPlacement
{
    private const double Margin = 40;

    /// <summary>
    /// Zentriert auf dem größten Bildschirm, der den Punkt (<paramref name="ownerX"/>, <paramref name="ownerY"/>), die Mitte des
    /// Hauptfensters, nicht enthält; höchstens so groß wie dieser Bildschirm abzüglich eines Rands. <see langword="null"/>, wenn es keinen
    /// anderen Bildschirm gibt.
    /// </summary>
    public static WindowBoundsState? OnOtherScreen(IReadOnlyList<ScreenArea> screens, double ownerX, double ownerY, double width, double height)
    {
        var target = screens
            .Where(s => !s.Contains(ownerX, ownerY) && s.Width > Margin * 2 && s.Height > Margin * 2)
            .OrderByDescending(s => s.Area)
            .FirstOrDefault();
        if (target is null)
        {
            return null;
        }

        var w = Math.Min(width, target.Width - Margin);
        var h = Math.Min(height, target.Height - Margin);
        return new WindowBoundsState
        {
            Left = target.Left + ((target.Width - w) / 2),
            Top = target.Top + ((target.Height - h) / 2),
            Width = w,
            Height = h,
        };
    }
}
