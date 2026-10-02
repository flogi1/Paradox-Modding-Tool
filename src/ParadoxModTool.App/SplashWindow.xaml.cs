using System.Windows;
using System.Windows.Threading;

namespace ParadoxModTool.App;

/// <summary>
/// Startbild mit Fortschrittsbalken. Der Balken ist echt bestimmt: <see cref="Report"/> wird von den
/// Ladeschritten des Hauptfensters aufgerufen (Profil, Syntax-Hervorhebung, Sitzung je Dokument, Wiederherstellung).
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppInfo.Version}";
        StatusText.Text = MiscWindowTexts.SplashStarting;
    }

    /// <summary>
    /// Setzt Fortschritt (0 bis 1) und Statustext und zeichnet sofort neu - der Ladevorgang läuft auf dem
    /// UI-Thread, ohne diese Zeichenpause bliebe das Startbild bis zum Schluss unverändert.
    /// </summary>
    public void Report(double fraction, string status)
    {
        Progress.Value = Math.Clamp(fraction, 0, 1);
        StatusText.Text = status;
        Dispatcher.Invoke(DispatcherPriority.Render, () => { });
    }
}

/// <summary>Fortschrittsmeldungen der Startphase; ohne Startbild (z. B. im Test) wirkungslos.</summary>
public static class StartupProgress
{
    private static SplashWindow? _splash;

    public static void Attach(SplashWindow splash) => _splash = splash;

    public static void Report(double fraction, string status) => _splash?.Report(fraction, status);

    /// <summary>Schließt das Startbild und gibt es frei.</summary>
    public static void Finish()
    {
        _splash?.Close();
        _splash = null;
    }
}
