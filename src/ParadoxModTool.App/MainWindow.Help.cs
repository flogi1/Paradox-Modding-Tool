using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using ParadoxModTool.Localization;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Session;
using ParadoxModTool.Settings;
using ParadoxModTool.Updates;

namespace ParadoxModTool.App;

/// <summary>
/// Menü "Hilfe": Update-Prüfung über GitHub Releases (mit Hinweisleiste), Selbst-Update ("Jetzt aktualisieren": laden, prüfen, schließen,
/// Update-Modus der neuen Version starten), Fehlerbericht als vorbefüllte GitHub-Issue-Seite und Über-Fenster. Solange
/// <see cref="AppInfo.GitHubRepository"/> leer ist, sind Update-Prüfung und Fehlerbericht deaktiviert.
/// </summary>
public partial class MainWindow
{
    private DateTime? _lastUpdateCheckUtc;
    private string? _skippedUpdateVersion;
    private ReleaseInfo? _pendingRelease;
    private bool _updateCheckRunning;
    private bool _updateInstallRunning;
    private PreparedUpdate? _preparedUpdate;
    private bool _applyUpdateOnClose;
    private bool _mainWindowClosed;

    private void LoadUpdateSettings(AppSessionState state)
    {
        _lastUpdateCheckUtc = state.LastUpdateCheckUtc;
        _skippedUpdateVersion = state.SkippedUpdateVersion;
    }

    /// <summary>Einmal im Konstruktor: Nach dem Schließen startet der Update-Modus, wenn "Jetzt aktualisieren" das Schließen ausgelöst hat.</summary>
    private void InitializeSelfUpdate()
    {
        Closed += (_, _) =>
        {
            _mainWindowClosed = true;
            if (_applyUpdateOnClose && _preparedUpdate is { } update)
            {
                LaunchUpdater(update);
            }
        };
    }

    private void InitializeUpdateBarTexts()
    {
        UpdateBarInstallButton.Content = UpdateTexts.BarInstall;
        UpdateBarInstallButton.ToolTip = UpdateTexts.BarInstallTip;
        UpdateBarReleaseButton.Content = UpdateTexts.BarReleasePage;
        UpdateBarReleaseButton.ToolTip = UpdateTexts.BarReleasePageTip;
        UpdateBarSkipButton.Content = UpdateTexts.BarSkip;
        UpdateBarLaterButton.ToolTip = UpdateTexts.BarLaterTip;
    }

    private void ApplyRepositoryAvailability()
    {
        var available = AppInfo.HasRepository;
        CheckUpdatesMenuItem.IsEnabled = available;
        ReportBugMenuItem.IsEnabled = available;
        CheckUpdatesMenuItem.ToolTip = available ? MenuTexts.CheckUpdatesTip : MenuTexts.NoRepositoryTip;
        ReportBugMenuItem.ToolTip = available ? MenuTexts.ReportBugTip : MenuTexts.NoRepositoryTip;
    }

    /// <summary>Automatische Prüfung nach dem Start: im Hintergrund, höchstens einmal täglich, Fehler bleiben still.</summary>
    private async void StartBackgroundUpdateCheck()
    {
        ApplyRepositoryAvailability();

        if (!AppInfo.HasRepository || !_settings.CheckForUpdatesOnStartup || !UpdateSchedule.IsDue(_lastUpdateCheckUtc, DateTime.UtcNow))
        {
            return;
        }

        var result = await RunUpdateCheckAsync();
        if (result is { Status: UpdateCheckStatus.UpdateAvailable, Release: { } release } && !IsSkipped(release))
        {
            ShowUpdateBar(release);
        }
    }

    private async Task<UpdateCheckResult> RunUpdateCheckAsync()
    {
        _updateCheckRunning = true;
        try
        {
            var result = await new UpdateChecker(UpdateClients.Check, AppInfo.GitHubRepository).CheckAsync(AppInfo.Version);

            // Nur erfolgreiche Prüfungen zählen für den Tages-Takt; nach einem Fehler wird beim nächsten Start erneut versucht.
            if (result.Status != UpdateCheckStatus.Failed)
            {
                _lastUpdateCheckUtc = DateTime.UtcNow;
            }

            return result;
        }
        finally
        {
            _updateCheckRunning = false;
        }
    }

    private bool IsSkipped(ReleaseInfo release) =>
        AppVersion.TryParse(_skippedUpdateVersion, out var skipped) && release.Version <= skipped;

    private void ShowUpdateBar(ReleaseInfo release)
    {
        _pendingRelease = release;
        UpdateBarText.Text = UpdateTexts.BarAvailable(release.Version, AppInfo.Version);
        SetUpdateBarButtonsVisible(true);
        UpdateBar.Visibility = Visibility.Visible;
    }

    /// <summary>Die drei Aktions-Knöpfe; "✕" bleibt immer sichtbar (auch beim Hinweis "Aktualisiert auf X").</summary>
    private void SetUpdateBarButtonsVisible(bool visible)
    {
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        UpdateBarInstallButton.Visibility = visibility;
        UpdateBarReleaseButton.Visibility = visibility;
        UpdateBarSkipButton.Visibility = visibility;
    }

    private async void CheckUpdatesMenuItem_Click(object sender, RoutedEventArgs e) => await CheckForUpdatesNowAsync();

    private async Task CheckForUpdatesNowAsync()
    {
        if (_updateCheckRunning)
        {
            return;
        }

        CheckUpdatesMenuItem.IsEnabled = false;
        try
        {
            var result = await RunUpdateCheckAsync();
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable when result.Release is { } release:
                    ShowUpdateBar(release);
                    var answer = MessageBox.Show(this, UpdateTexts.AvailableQuestion(release.Version, AppInfo.Version), UpdateTexts.AvailableTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Information);
                    if (answer == MessageBoxResult.Yes)
                    {
                        await InstallUpdateAsync(release);
                    }
                    else if (answer == MessageBoxResult.No)
                    {
                        OpenReleasePage(release.HtmlUrl);
                    }

                    break;

                case UpdateCheckStatus.UpToDate:
                    MessageBox.Show(this, result.Release is null ? UpdateTexts.NoRelease : UpdateTexts.UpToDate(AppInfo.Version), UpdateTexts.CheckTitle);
                    break;

                default:
                    MessageBox.Show(this, UpdateTexts.CheckFailed(result.Error), UpdateTexts.CheckTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }
        finally
        {
            if (!_mainWindowClosed)
            {
                CheckUpdatesMenuItem.IsEnabled = AppInfo.HasRepository;
            }
        }
    }

    private async void UpdateBarInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease is { } release)
        {
            await InstallUpdateAsync(release);
        }
    }

    /// <summary>
    /// Laden und prüfen (mit Fortschrittsfenster), bestätigen lassen, dann über den normalen Weg schließen. Den Update-Modus startet
    /// erst das <see cref="Window.Closed"/>-Ereignis (<see cref="InitializeSelfUpdate"/>), also nach dem Sichern von Sitzung und
    /// Wiederherstellungsdaten. Bricht etwas das Schließen ab, bleibt PMT offen und das Paket für den nächsten Versuch liegen.
    /// </summary>
    private async Task InstallUpdateAsync(ReleaseInfo release)
    {
        if (_updateInstallRunning)
        {
            return;
        }

        _updateInstallRunning = true;
        try
        {
            // Ohne Dateiliste (Entwicklungsordner, "dotnet run", von Hand zusammenkopiert) wüsste der Tausch nicht, was zu PMT gehört.
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, UpdatePaths.InstallListName)))
            {
                OfferReleasePage(release, UpdateTexts.NoInstallList);
                return;
            }

            // Geschützter Ordner (z. B. Programme): kein Update mit Administratorrechten, siehe UpdateRunner. Vor dem Download prüfen.
            var installFolder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            if (!UpdateApplier.CanWrite(installFolder))
            {
                OfferReleasePage(release, UpdateTexts.ProtectedFolder(installFolder));
                return;
            }

            var prepared = _preparedUpdate is { } known && known.Version == release.Version && Directory.Exists(known.StagingFolder)
                ? known
                : await PrepareWithProgressAsync(release);
            if (prepared is null)
            {
                return;
            }

            _preparedUpdate = prepared;

            // Ein anderes Fenster hat für diesen Ordner schon den Update-Modus gestartet; ein zweiter würde dessen Sicherung löschen.
            if (UpdateLock.IsHeld(UpdatePaths.Default, installFolder))
            {
                MessageBox.Show(this, UpdateTexts.AlreadyRunning, UpdateTexts.NotPossibleTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show(this, UpdateTexts.ReadyQuestion(prepared.Version), UpdateTexts.ReadyTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _applyUpdateOnClose = true;
            Close();
            if (!_mainWindowClosed)
            {
                _applyUpdateOnClose = false;
                MessageBox.Show(this, UpdateTexts.CloseCancelled, UpdateTexts.ReadyTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        finally
        {
            _updateInstallRunning = false;
        }
    }

    private async Task<PreparedUpdate?> PrepareWithProgressAsync(ReleaseInfo release)
    {
        using var cancellation = new CancellationTokenSource();
        var window = new TaskProgressWindow(UpdateTexts.DownloadTitle, UpdateTexts.DownloadHint, UpdateTexts.Cancel, UpdateTexts.DownloadCount, cancellation.Cancel) { Owner = this };
        var status = UpdateTexts.DownloadStatus(release.Version);
        var progress = new Progress<DownloadProgress>(p => window.Report(new CompatProgress(status, ToMegabytes(p.Done), ToMegabytes(p.Total))));
        window.Show();
        PrepareResult result;
        try
        {
            var log = new UpdateLog(UpdatePaths.Default.LogFile);
            var preparer = new UpdatePreparer(new UpdateDownloader(UpdateClients.Download), UpdatePaths.Default, UpdateKeys.ProductionPublicKey, log);
            result = await preparer.PrepareAsync(release, AppInfo.Version, progress, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            window.CloseAfterFinished();
        }

        if (result.Update is { } update)
        {
            return update;
        }

        OfferReleasePage(release, UpdateTexts.Problem(result.Problem, result.Detail));
        return null;
    }

    private static int ToMegabytes(long bytes) => (int)(bytes / (1024 * 1024));

    private void OfferReleasePage(ReleaseInfo release, string reason)
    {
        if (MessageBox.Show(this, UpdateTexts.OfferReleasePage(reason), UpdateTexts.NotPossibleTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            OpenReleasePage(release.HtmlUrl);
        }
    }

    /// <summary>Startet die neue Exe aus dem Staging-Ordner im Update-Modus. Läuft nach dem Schließen des Hauptfensters.</summary>
    private static void LaunchUpdater(PreparedUpdate update)
    {
        var log = new UpdateLog(UpdatePaths.Default.LogFile);
        var arguments = new ApplyUpdateArguments(
            ApplyUpdateArguments.CurrentProtocol,
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
            Environment.ProcessId,
            AppInfo.Version,
            update.Version,
            GameContext.GameId,
            UiText.Current.Language);
        try
        {
            new WindowsProcessControl().Start(Path.Combine(update.StagingFolder, UpdatePaths.ExecutableName), arguments.ToArguments());
            log.Write($"Update-Modus gestartet: {AppInfo.Version} -> {update.Version}");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            log.Write("Update-Modus nicht startbar: " + ex.Message);
            MessageBox.Show(UpdateTexts.LaunchFailed(ex.Message, log.Path), UpdateTexts.ApplyTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateBarRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease is { } release)
        {
            OpenReleasePage(release.HtmlUrl);
        }
    }

    private void UpdateBarLater_Click(object sender, RoutedEventArgs e) => UpdateBar.Visibility = Visibility.Collapsed;

    private void UpdateBarSkip_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease is { } release)
        {
            _skippedUpdateVersion = release.Version.ToString();
        }

        UpdateBar.Visibility = Visibility.Collapsed;
    }

    private void ReportBugMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var url = IssueReport.BuildUrl(
            AppInfo.GitHubRepository,
            AppInfo.Version.ToString(),
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription);
        OpenInBrowser(url);
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    /// <summary>Öffnet nur GitHub-Adressen: die Adresse stammt aus einer Netzwerkantwort und wird nicht blind an die Shell gegeben.</summary>
    private void OpenReleasePage(string url)
    {
        if (!ReleaseParser.IsGitHubUrl(url))
        {
            MessageBox.Show(this, UpdateTexts.NotGitHub, UpdateTexts.NotOpenedTitle);
            return;
        }

        OpenInBrowser(url);
    }

    private void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, UpdateTexts.BrowserFailed(ex.Message), UpdateTexts.ErrorTitle);
        }
    }
}
