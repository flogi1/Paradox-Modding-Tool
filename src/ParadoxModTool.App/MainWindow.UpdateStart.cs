using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Updates;

namespace ParadoxModTool.App;

/// <summary>
/// Start nach einem Update: Startsignal für den wartenden Update-Modus, Hinweis "Aktualisiert auf X", Meldung bei Fehlschlag oder
/// Abbruch. Beim normalen Start (ohne Update-Argumente) werden Sicherung und alte Update-Ordner im Hintergrund gelöscht.
/// </summary>
public partial class MainWindow
{
    /// <summary>Aus <see cref="App.OnStartup"/>, sobald das Hauptfenster zum ersten Mal gezeichnet ist.</summary>
    internal void OnFirstRenderedAfterStart(UpdateStartInfo start)
    {
        var log = new UpdateLog(UpdatePaths.Default.LogFile);
        if (start.Applied is { } applied)
        {
            UpdateBarText.Text = UpdateTexts.BarUpdated(applied);
            SetUpdateBarButtonsVisible(false);
            UpdateBar.Visibility = Visibility.Visible;
        }
        else if (!start.IsUpdateStart)
        {
            var installFolder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            _ = Task.Run(() => UpdateApplier.CleanupAfterStart(installFolder, UpdatePaths.Default, AppInfo.Version, log));
        }

        // Erst nach dem Schließen des Startbilds zeigen: eine MessageBox, die im selben Moment erscheint, geht sonst sofort wieder zu.
        if (start.Failed is { } failed)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                MessageBox.Show(this, UpdateTexts.StartFailed(failed, log.Path), UpdateTexts.StartFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning));
        }

        if (start.Cancelled is { } cancelled)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                MessageBox.Show(this, UpdateTexts.StartCancelled(cancelled), UpdateTexts.StartCancelledTitle, MessageBoxButton.OK, MessageBoxImage.Information));
        }
    }

    /// <summary>
    /// Das Signal, auf das der Update-Modus bis zu 90 s wartet. Aus <see cref="App.OnStartup"/> direkt nach dem Erzeugen des
    /// Hauptfensters: Dialoge beim Laden (Wiederherstellung, Profilfehler) dürfen die Frist nicht aufbrauchen. In Debug-Builds unterdrückt
    /// <c>PMT_UPDATE_SKIP_MARKER=1</c> es, um den Rückfall zu testen.
    /// </summary>
    internal static void WriteStartMarker(AppVersion version)
    {
        var log = new UpdateLog(UpdatePaths.Default.LogFile);
#if DEBUG
        if (Environment.GetEnvironmentVariable("PMT_UPDATE_SKIP_MARKER") == "1")
        {
            log.Write("Startsignal absichtlich unterdrückt (PMT_UPDATE_SKIP_MARKER).");
            return;
        }
#endif
        try
        {
            var marker = UpdatePaths.Default.Marker(version);
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            File.WriteAllText(marker, Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            log.Write($"Startsignal für {version} geschrieben.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Write("Startsignal nicht schreibbar: " + ex.Message);
        }
    }
}
