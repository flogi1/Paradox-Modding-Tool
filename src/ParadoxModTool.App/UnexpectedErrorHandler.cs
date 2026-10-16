using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ParadoxModTool.Updates;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ParadoxModTool.App;

/// <summary>
/// Fängt Ausnahmen ab, die sonst PMT beenden: Protokoll in <see cref="UnexpectedErrors.DefaultLogFile"/>, eine Meldung, und PMT läuft weiter.
/// Bis das Hauptfenster zum ersten Mal gezeichnet ist, beendet ein Fehler PMT nach der Meldung; sonst bliebe ein Prozess ohne Fenster, der
/// das Spiel sperrt. Ausnahmen anderer Threads lassen sich nicht abfangen und kommen nur ins Protokoll.
/// </summary>
internal static class UnexpectedErrorHandler
{
    private static readonly UnexpectedErrors Errors = new(new UpdateLog(UnexpectedErrors.DefaultLogFile));

    private static bool _started;
    private static bool _shuttingDown;

    public static void Register(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            Handle(app, e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                Errors.Log(exception, e.IsTerminating ? "AppDomain (terminating)" : "AppDomain");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Errors.Log(e.Exception, "Task");
            e.SetObserved();
        };
    }

    /// <summary>Das Hauptfenster ist gezeichnet: ab jetzt läuft PMT nach einem Fehler weiter.</summary>
    public static void Started() => _started = true;

    private static void Handle(Application app, Exception exception)
    {
        if (!_started)
        {
            Errors.Log(exception, "Startup");
            if (_shuttingDown)
            {
                return;
            }

            _shuttingDown = true;
            MessageBox.Show(MiscWindowTexts.UnexpectedErrorAtStart(exception.Message, Errors.LogFile), MiscWindowTexts.UnexpectedErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            app.Shutdown();
            return;
        }

        if (!Errors.Record(exception, "Dispatcher", DateTime.Now))
        {
            return;
        }

        try
        {
            var text = MiscWindowTexts.UnexpectedError(exception.Message, Errors.LogFile);
            if (app.MainWindow is { IsVisible: true } owner)
            {
                MessageBox.Show(owner, text, MiscWindowTexts.UnexpectedErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(text, MiscWindowTexts.UnexpectedErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            Errors.Closed();
        }
    }
}
