namespace ParadoxModTool.Updates;

/// <summary>
/// Unerwartete Ausnahmen der App: jede kommt mit Typ, Text, Stack und inneren Ausnahmen ins Protokoll; <see cref="Record"/> entscheidet, ob das
/// Fenster dazu eine Meldung zeigt. Höchstens eine Meldung gleichzeitig, dieselbe Ausnahme (Typ, Text, oberster Stack-Rahmen) höchstens einmal
/// je Minute und insgesamt höchstens <see cref="MaxPerMinute"/> je Minute; alles andere steht nur im Protokoll, damit eine Ausnahme in einer
/// Schleife (Layout, Timer) keine Flut von Meldungen auslöst.
/// </summary>
public sealed class UnexpectedErrors(UpdateLog log)
{
    public const int MaxPerMinute = 3;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly Dictionary<string, DateTime> _lastShown = new(StringComparer.Ordinal);
    private readonly Queue<DateTime> _recent = new();
    private bool _open;

    /// <summary>%LocalAppData%\ParadoxModTool\error.log, neben dem Ordner der Updates.</summary>
    public static string DefaultLogFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ParadoxModTool", "error.log");

    public string LogFile => log.Path;

    /// <summary>Nur ins Protokoll, z. B. für Ausnahmen anderer Threads, die keine Meldung zeigen können.</summary>
    public void Log(Exception exception, string source) => log.Write(source + ": " + exception);

    /// <summary>
    /// Schreibt die Ausnahme ins Protokoll; true, wenn jetzt eine Meldung erscheinen soll. Nach dem Schließen der Meldung <see cref="Closed"/> aufrufen.
    /// </summary>
    public bool Record(Exception exception, string source, DateTime now)
    {
        Log(exception, source);
        lock (_gate)
        {
            if (_open)
            {
                return false;
            }

            var signature = Signature(exception);
            if (_lastShown.TryGetValue(signature, out var last) && now - last < Window)
            {
                return false;
            }

            while (_recent.Count > 0 && now - _recent.Peek() >= Window)
            {
                _recent.Dequeue();
            }

            if (_recent.Count >= MaxPerMinute)
            {
                return false;
            }

            _recent.Enqueue(now);
            _lastShown[signature] = now;
            _open = true;
            return true;
        }
    }

    public void Closed()
    {
        lock (_gate)
        {
            _open = false;
        }
    }

    private static string Signature(Exception exception)
    {
        var stack = exception.StackTrace ?? string.Empty;
        var firstFrame = stack.Split('\n', 2)[0].Trim();
        return exception.GetType().FullName + "|" + exception.Message + "|" + firstFrame;
    }
}
