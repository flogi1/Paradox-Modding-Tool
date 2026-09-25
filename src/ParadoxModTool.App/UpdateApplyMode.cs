using System.IO;
using System.Windows;
using ParadoxModTool.Localization;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Updates;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Update-Modus (<c>--apply-update</c>): läuft aus dem Staging-Ordner der neuen Version, zeigt nur ein Fortschrittsfenster und tauscht
/// den Programmordner (siehe <see cref="UpdateRunner"/>). Einen Weg mit Administratorrechten gibt es bewusst nicht.
/// </summary>
internal static class UpdateApplyMode
{
    private const int InvalidCallExitCode = 3;

    public static int Run(string[] args)
    {
        var log = new UpdateLog(UpdatePaths.Default.LogFile);
        var arguments = ApplyUpdateArguments.Parse(args);
        UiText.Load(Path.Combine(AppContext.BaseDirectory, "locales"), arguments?.Language);
        if (arguments is null)
        {
            log.Write("Ungültiger Aufruf des Update-Modus: " + CommandLine.Join(args));
            MessageBox.Show(UpdateTexts.InvalidArguments, UpdateTexts.ApplyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return InvalidCallExitCode;
        }

        var outcome = ApplyOutcome.RollbackFailed;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            outcome = await RunWithWindowAsync(arguments, log);
            app.Shutdown();
        };
        app.Run();
        return (int)outcome;
    }

    private static async Task<ApplyOutcome> RunWithWindowAsync(ApplyUpdateArguments arguments, UpdateLog log)
    {
        using var cancellation = new CancellationTokenSource();
        var window = new TaskProgressWindow(UpdateTexts.ApplyTitle, UpdateTexts.ApplyVersions(arguments.FromVersion, arguments.ToVersion), UpdateTexts.Cancel, (text, _, _) => text, cancellation.Cancel)
        {
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        window.Show();

        var staging = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var runner = new UpdateRunner(arguments, staging, UpdatePaths.Default, log, new WindowsProcessControl(), new UpdateApplier(log), UpdateRunnerTimings.Default);
        ApplyOutcome outcome;
        try
        {
            outcome = await runner.RunAsync(new WindowUi(window), cancellation.Token);
        }
        catch (Exception ex)
        {
            // Letzte Sicherung: ein unerwarteter Fehler darf den Nutzer nicht ohne Hinweis zurücklassen.
            log.Write("Unerwarteter Fehler im Update-Modus: " + ex);
            outcome = ApplyOutcome.RollbackFailed;
        }
        finally
        {
            window.CloseAfterFinished();
        }

        if (outcome == ApplyOutcome.RollbackFailed)
        {
            MessageBox.Show(UpdateTexts.RollbackFailed(Path.Combine(arguments.Target, UpdatePaths.BackupFolderName), log.Path), UpdateTexts.ApplyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return outcome;
    }

    private sealed class WindowUi(TaskProgressWindow window) : IUpdateRunnerUi
    {
        public void Status(UpdateRunnerPhase phase, int waitingFor) => window.Report(new CompatProgress(UpdateTexts.Phase(phase, waitingFor), 0, 0));

        public Task<bool> KeepWaitingAsync(int count) =>
            Task.FromResult(MessageBox.Show(window, UpdateTexts.KeepWaiting(count), UpdateTexts.KeepWaitingTitle, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
    }
}
