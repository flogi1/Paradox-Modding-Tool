using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace ParadoxModTool.App;

/// <summary>
/// Schließt der Nutzer ein aktives Unterfenster (Mods, eigenes Fenster des Fehlerprotokolls, Dialog), holt PMT dessen Besitzer oder
/// sonst das Hauptfenster dieses Programmfensters nach vorn. Ohne das wählt Windows das nächste Fenster in der Stapelfolge, und das
/// ist bei zwei offenen Spielen oft das PMT des anderen Spiels.
/// </summary>
public static class FocusReturnOnClose
{
    private static bool _registered;

    /// <summary>Einmal beim Start: gilt für jedes Fenster, das danach geladen wird.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || !ReferenceEquals(e.OriginalSource, window) || Watched.TryGetValue(window, out _))
        {
            return;
        }

        Watched.Add(window, Marker);
        Window? target = null;
        window.Closing += (_, args) =>
        {
            var main = Application.Current?.MainWindow;
            target = !args.Cancel && window.IsActive && !ReferenceEquals(window, main) ? window.Owner ?? main : null;
        };
        window.Closed += (_, _) =>
        {
            if (target is not { } owner)
            {
                return;
            }

            // Erst nach dem Schließen: Bei einem modalen Dialog ist der Besitzer bis dahin noch gesperrt. Nur eingreifen, wenn der Fokus
            // dieses Programm verlassen hat; öffnet der Aufrufer danach selbst ein Fenster (Startdialog → Merge-Fenster), bleibt es vorn.
            owner.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (owner.Dispatcher.HasShutdownStarted || Application.Current is not { } app || app.Windows.OfType<Window>().Any(w => w.IsActive))
                {
                    return;
                }

                if (owner.IsLoaded && owner.IsVisible && owner.WindowState != WindowState.Minimized)
                {
                    owner.Activate();
                }
            });
        };
    }

    /// <summary>Fenster, deren Schließen schon beobachtet wird (<c>Loaded</c> kann mehrfach kommen).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, object> Watched = new();

    private static readonly object Marker = new();
}
