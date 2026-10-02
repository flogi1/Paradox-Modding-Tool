namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Beobachtet eine Logdatei (Spec, Live-Beobachtung; Plan-2b-Ruling 9). Ein Dateiwächter meldet nur Änderungen; gelesen wird im Takt
/// (Standard 1 s) und ohne Meldung spätestens nach der Rückfallzeit (Standard 2 s). Schnelle Schreibfolgen des Spiels kommen so als eine
/// Portion an. <c>onChunk</c> kommt aus einem Hintergrund-Thread, in Lesereihenfolge, nur mit neuen Zeilen oder bei Rotation.
/// Das Spiel wechselt error.log auch mitten im Lauf (Spec Scope-Verfolgung, Teil 1): Dann liest der Wächter zuerst den Rest der
/// umbenannten Datei (error.1.log), danach entscheidet die erste Uhrzeit der neuen Datei über Fortsetzung (<see cref="LogChunk.Continued"/>)
/// oder neuen Lauf (<see cref="LogChunk.Restarted"/>).
/// </summary>
public sealed class LiveLogWatcher : IDisposable
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly TimeSpan _fallback;
    private readonly Func<DateTime> _now;
    private readonly Action<LogChunk> _onChunk;
    private readonly Action<Exception>? _onError;
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer? _timer;
    private LogPosition _position;
    private int _part;
    private TimeSpan? _lastTime;
    private bool _pendingSwitch;
    private readonly string? _runAnchorPath;
    private long _anchorLength;
    private string _anchorHead = string.Empty;
    private bool? _switchIsNewRun;
    private DateTime _lastPoll;
    private bool _signaled;
    private bool _disposed;

    public LiveLogWatcher(
        string path,
        LogPosition start,
        Action<LogChunk> onChunk,
        Action<Exception>? onError = null,
        TimeSpan? tick = null,
        TimeSpan? fallback = null,
        Func<DateTime>? now = null,
        bool startTimer = true,
        bool watchFile = true,
        int part = 0,
        string? runAnchorPath = null)
    {
        _path = path;
        _runAnchorPath = runAnchorPath;
        ReadAnchor();
        _position = start;
        _part = part;
        _lastTime = start.Offset > 0 && File.Exists(path) ? TryLastTime(path) : null;
        _onChunk = onChunk;
        _onError = onError;
        _fallback = fallback ?? TimeSpan.FromSeconds(2);
        _now = now ?? (() => DateTime.UtcNow);
        _lastPoll = _now();
        // Ohne Wächter (Tests, die Tick selbst aufrufen) liest nur der Takt; der echte Wächter meldet sonst unvorhersehbar dazwischen.
        _watcher = watchFile ? TryCreateWatcher(path) : null;
        if (startTimer)
        {
            var interval = tick ?? TimeSpan.FromSeconds(1);
            _timer = new Timer(_ => Tick(), null, interval, interval);
        }
    }

    public LogPosition Position
    {
        get
        {
            lock (_gate)
            {
                return _position;
            }
        }
    }

    /// <summary>Der Wächter hat eine Änderung gemeldet; gelesen wird beim nächsten Takt.</summary>
    public void Signal()
    {
        lock (_gate)
        {
            _signaled = true;
        }
    }

    /// <summary>Ein Takt: liest, wenn eine Meldung vorliegt oder die Rückfallzeit abgelaufen ist.</summary>
    public void Tick()
    {
        bool due;
        lock (_gate)
        {
            due = !_disposed && (_signaled || _now() - _lastPoll >= _fallback);
        }

        if (due)
        {
            Poll();
        }
    }

    /// <summary>Liest sofort alle neuen vollständigen Zeilen und meldet sie (unter der Sperre, damit die Reihenfolge stimmt).</summary>
    public void Poll()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _signaled = false;
            _lastPoll = _now();
            if (!File.Exists(_path))
            {
                return;
            }

            try
            {
                var chunk = LogReader.ReadFrom(_path, _position, _part);
                var anchorRecreated = ReadAnchor();
                if (chunk.Restarted)
                {
                    _switchIsNewRun = anchorRecreated;
                    DeliverTail();
                    _position = LogPosition.Start;
                    _pendingSwitch = true;
                }

                if (_pendingSwitch)
                {
                    // Ohne Zeile mit Uhrzeit bleibt die Position stehen, damit die Zeilen mit der Entscheidung kommen.
                    if (Decide(chunk.Lines, chunk.Next))
                    {
                        _position = chunk.Next;
                    }
                }
                else
                {
                    _position = chunk.Next;
                    if (chunk.Lines.Count > 0)
                    {
                        Deliver(chunk);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _onError?.Invoke(ex);
            }
        }
    }

    /// <summary>Rest der umbenannten Datei ab der bisherigen Position, nur wenn ihr Anfang zu dem der bisher gelesenen Datei passt.</summary>
    private void DeliverTail()
    {
        var folder = Path.GetDirectoryName(_path);
        var rotated = folder is null ? null : Path.Combine(folder, ErrorLogLocations.RunFileName(1));
        if (rotated is null || !File.Exists(rotated) || _position.Offset == 0)
        {
            return;
        }

        var tail = LogReader.ReadFrom(rotated, _position, _part);
        if (!tail.Restarted && tail.Lines.Count > 0)
        {
            Deliver(tail);
        }
    }

    /// <summary>
    /// Nach einem Wechsel: Sobald die neue Datei eine Zeile mit Uhrzeit hat, gehört sie zum selben Lauf, wenn sie höchstens
    /// <see cref="LogRunChain.MaxGap"/> nach der letzten gelesenen Zeile beginnt (über Mitternacht gerechnet). Vorher wartet die Entscheidung.
    /// </summary>
    private bool Decide(IReadOnlyList<LogLine> lines, LogPosition next)
    {
        var first = lines.FirstOrDefault(l => l.Time.Length > 0);
        if (first is null || !TimeSpan.TryParse(first.Time, System.Globalization.CultureInfo.InvariantCulture, out var firstTime))
        {
            return false;
        }

        _pendingSwitch = false;
        bool continued;
        if (_switchIsNewRun is bool newRun)
        {
            // debug.log legt das Spiel nur beim Start neu an: weitergeschrieben = derselbe Lauf, egal wie lange keine Meldung kam.
            continued = !newRun;
        }
        else
        {
            var gap = _lastTime is { } last ? firstTime - last : TimeSpan.MaxValue;
            if (gap < TimeSpan.Zero)
            {
                gap += TimeSpan.FromDays(1);
            }

            continued = gap <= LogRunChain.MaxGap;
        }

        _part = continued ? _part + 1 : 0;
        var stamped = lines.Select(l => l with { Part = _part }).ToList();
        Deliver(new LogChunk(stamped, next, Restarted: !continued, Continued: continued));
        return true;
    }

    private void Deliver(LogChunk chunk)
    {
        if (chunk.Lines.LastOrDefault(l => l.Time.Length > 0) is { } line
            && TimeSpan.TryParse(line.Time, System.Globalization.CultureInfo.InvariantCulture, out var time))
        {
            _lastTime = time;
        }

        _onChunk(chunk);
    }

    /// <summary>
    /// Liest Größe und Anfang von debug.log. Rückgabe: <c>true</c>, wenn es seit dem letzten Lesen neu angelegt wurde (kürzer oder anderer
    /// Anfang); <c>false</c>, wenn es weiterläuft; <c>null</c> ohne debug.log (dann entscheidet die Lückenregel).
    /// </summary>
    private bool? ReadAnchor()
    {
        if (_runAnchorPath is null || !File.Exists(_runAnchorPath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(_runAnchorPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[Math.Min(64, stream.Length)];
            stream.ReadExactly(buffer);
            var head = Convert.ToHexString(buffer);
            var recreated = _anchorHead.Length > 0
                && (stream.Length < _anchorLength
                    || (!head.StartsWith(_anchorHead, StringComparison.Ordinal) && !_anchorHead.StartsWith(head, StringComparison.Ordinal)));
            _anchorLength = stream.Length;
            _anchorHead = head;
            return recreated;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static TimeSpan? TryLastTime(string path)
    {
        try
        {
            return LogReader.FirstAndLastTime(path).Last;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _timer?.Dispose();
        _watcher?.Dispose();
    }

    private FileSystemWatcher? TryCreateWatcher(string path)
    {
        try
        {
            var folder = Path.GetDirectoryName(path);
            if (folder is null || !Directory.Exists(folder))
            {
                return null;
            }

            var watcher = new FileSystemWatcher(folder, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            };
            watcher.Changed += (_, _) => Signal();
            watcher.Created += (_, _) => Signal();
            watcher.Renamed += (_, _) => Signal();
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or PlatformNotSupportedException)
        {
            // Ohne Wächter liest der Takt nach der Rückfallzeit weiter (Spec: Abfrage als Rückfall).
            return null;
        }
    }
}
