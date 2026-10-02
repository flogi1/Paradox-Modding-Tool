using System.Windows;
using ParadoxModTool.Session;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>Gemerkte Position und Größe von Werkzeugfenstern (Fehlerprotokoll, Spielstand-Explorer) anwenden und erfassen.</summary>
internal static class WindowBoundsPersistence
{
    /// <summary>
    /// Nutzt die gemerkte Lage, wenn sie auf den aktuellen Bildschirmen sichtbar ist; sonst <paramref name="otherScreen"/>; sonst zentriert
    /// (über dem Besitzer, wenn es einen gibt). Vor dem ersten Anzeigen aufrufen.
    /// </summary>
    public static void Apply(Window window, WindowBoundsState? saved, WindowBoundsState? otherScreen)
    {
        var usable = WindowBoundsCheck.IsUsable(saved, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight, window.MinWidth, window.MinHeight);
        var bounds = usable ? saved : otherScreen;
        if (bounds is null)
        {
            window.WindowStartupLocation = window.Owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        if (usable && bounds.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Aktuelle Lage (bei maximiertem Fenster die Lage davor); null, wenn das Fenster noch keine gültige Lage hat.</summary>
    public static WindowBoundsState? Capture(Window window)
    {
        var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
        if (bounds.IsEmpty || !double.IsFinite(bounds.Left))
        {
            return null;
        }

        return new WindowBoundsState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = window.WindowState == WindowState.Maximized,
        };
    }
}
