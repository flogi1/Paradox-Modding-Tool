namespace ParadoxModTool.Performance;

/// <summary>
/// Erkennt, wann Dump-Dateien fertig geschrieben sind: eine Datei gilt als fertig, wenn ihre letzte Berührung
/// mindestens <c>settle</c> zurückliegt. Rein rechnerisch, ohne Timer und ohne Dateisystem; der Aufrufer meldet
/// Watcher-Ereignisse über <see cref="Touched"/> und fragt regelmäßig <see cref="Settled"/> ab.
/// Berührungen vor dem Start werden ignoriert (alte Dumps früherer Läufe); Pfade vergleichen ohne Groß-/Kleinschreibung.
/// </summary>
public sealed class DumpSettleTracker
{
    private readonly DateTime _startedUtc;
    private readonly TimeSpan _settle;
    private readonly Dictionary<string, DateTime> _lastTouch = new(StringComparer.OrdinalIgnoreCase);

    public DumpSettleTracker(DateTime startedUtc, TimeSpan settle)
    {
        _startedUtc = startedUtc;
        _settle = settle;
    }

    /// <summary>Meldet eine Berührung (Created/Changed/Renamed) des Pfads; liegt sie vor dem Start, wird sie ignoriert.</summary>
    public void Touched(string path, DateTime utcNow)
    {
        if (utcNow < _startedUtc)
        {
            return;
        }

        _lastTouch[path] = utcNow;
    }

    /// <summary>Pfade, deren letzte Berührung mindestens <c>settle</c> zurückliegt; jeder Pfad wird einmal geliefert, bis er erneut berührt wird.</summary>
    public IReadOnlyList<string> Settled(DateTime utcNow)
    {
        var settled = _lastTouch.Where(p => utcNow - p.Value >= _settle).Select(p => p.Key).ToList();
        foreach (var path in settled)
        {
            _lastTouch.Remove(path);
        }

        return settled;
    }
}
