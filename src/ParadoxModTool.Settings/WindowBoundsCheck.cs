using ParadoxModTool.Session;

namespace ParadoxModTool.Settings;

/// <summary>Prüft eine gespeicherte Fensterposition: groß genug und mit sichtbarer Titelleiste auf dem virtuellen Bildschirm (alle Monitore).</summary>
public static class WindowBoundsCheck
{
    private const double TitleBarHeight = 40;
    private const double TitleBarGrip = 300;

    public static bool IsUsable(WindowBoundsState? saved, double screenLeft, double screenTop, double screenWidth, double screenHeight, double minWidth, double minHeight)
    {
        if (saved is null
            || !double.IsFinite(saved.Left) || !double.IsFinite(saved.Top) || !double.IsFinite(saved.Width) || !double.IsFinite(saved.Height)
            || saved.Width < minWidth || saved.Height < minHeight)
        {
            return false;
        }

        var gripWidth = Math.Min(saved.Width, TitleBarGrip);
        return saved.Left < screenLeft + screenWidth && saved.Left + gripWidth > screenLeft
            && saved.Top >= screenTop && saved.Top + TitleBarHeight <= screenTop + screenHeight;
    }
}
