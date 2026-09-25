using System.Windows;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Settings;

namespace ParadoxModTool.App;

/// <summary>Fortschrittsfenster für vom Nutzer ausgelöste lange Läufe (Neu-Lernen, GUI-Index): echter Fortschritt und Abbrechen.</summary>
public partial class MainWindow
{
    /// <summary>
    /// Führt <paramref name="work"/> mit Fortschrittsfenster aus. Eigentümer ist das aktive Fenster des Programms (z. B. das
    /// Einstellungsfenster), sonst das Hauptfenster. <see langword="false"/> = abgebrochen.
    /// </summary>
    private async Task<bool> RunWithProgressAsync(string title, Func<CancellationToken, IProgress<(int Done, int Total)>, Task> work)
    {
        using var cancellation = new CancellationTokenSource();
        var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? this;
        var window = new TaskProgressWindow(title, SettingsTexts.ProgressHint, SettingsTexts.Cancel, SettingsTexts.ProgressCount, cancellation.Cancel) { Owner = owner };
        var progress = new Progress<(int Done, int Total)>(value => window.Report(new CompatProgress(SettingsTexts.ProgressFiles, value.Done, value.Total)));
        window.Show();
        try
        {
            await work(cancellation.Token, progress);
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            window.CloseAfterFinished();
        }
    }
}
