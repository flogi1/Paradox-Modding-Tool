using System.Windows.Threading;
using ParadoxModTool.GuiDesign.Checks;

namespace ParadoxModTool.Editor.Gui;

/// <summary>
/// Startet einen Befundlauf (klassische Prüfungen oder Jomini-Vorschau) nach der Wartezeit seit der letzten Änderung der Szene auf einem Hintergrund-Task
/// und bricht den vorigen Lauf ab. Ein Ergebnis kommt nur an (auf dem UI-Thread), wenn sein Lauf noch der aktuelle ist; ein abgebrochener Lauf, der
/// trotzdem fertig wird, zählt nicht.
/// </summary>
internal sealed class GuiFindingsScheduler
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly Action<IReadOnlyList<GuiFinding>> _apply;
    private Func<CancellationToken, IReadOnlyList<GuiFinding>>? _pending;
    private CancellationTokenSource? _current;

    public GuiFindingsScheduler(Action<IReadOnlyList<GuiFinding>> apply, TimeSpan delay)
    {
        _apply = apply;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = delay };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Start();
        };
    }

    /// <summary>
    /// Neuer Stand: der laufende Lauf wird abgebrochen, der nächste startet nach der Wartezeit im Hintergrund. Null bricht ab und leert die Befunde
    /// sofort (keine Prüfung).
    /// </summary>
    public void Schedule(Func<CancellationToken, IReadOnlyList<GuiFinding>>? run)
    {
        Cancel();
        _pending = run;
        if (run is not null)
        {
            _timer.Start();
        }
        else
        {
            _apply(Array.Empty<GuiFinding>());
        }
    }

    public void Cancel()
    {
        _timer.Stop();
        _pending = null;
        _current?.Cancel();
        _current = null;
    }

    private void Start()
    {
        if (_pending is not { } pending)
        {
            return;
        }

        _pending = null;
        var run = new CancellationTokenSource();
        _current = run;
        Task.Run(() => pending(run.Token), run.Token).ContinueWith(
            task =>
            {
                // Scheitert der aktuelle Lauf trotzdem (nicht abgebrochen), gilt ein leeres Ergebnis: alte Befunde blieben sonst stehen.
                if (task.IsCanceled)
                {
                    return;
                }

                var findings = task.IsCompletedSuccessfully ? task.Result : Array.Empty<GuiFinding>();
                _dispatcher.BeginInvoke(() =>
                {
                    if (ReferenceEquals(run, _current) && !run.IsCancellationRequested)
                    {
                        _apply(findings);
                    }
                });
            },
            TaskScheduler.Default);
    }
}
