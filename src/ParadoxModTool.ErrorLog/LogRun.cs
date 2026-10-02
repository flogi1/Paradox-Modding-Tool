namespace ParadoxModTool.ErrorLog;

/// <summary>
/// Ein Spiellauf aus einer oder mehreren Logdateien. Das Spiel wechselt <c>error.log</c> periodisch mitten im Lauf (benennt es in
/// <c>error.1.log</c> um); <see cref="Files"/> steht älteste zuerst. <see cref="MaybeIncomplete"/>: Die Kette reicht bis
/// <c>error.5.log</c>, ältere Teile kann das Spiel schon gelöscht haben.
/// </summary>
public sealed record LogRun(IReadOnlyList<LogRunFile> Files, DateTime? StartLocal, bool MaybeIncomplete)
{
    public LogRunFile Newest => Files[^1];

    /// <summary>Der Lauf endet mit <c>error.log</c>, ist also der, in den das Spiel gerade schreibt (bzw. zuletzt schrieb).</summary>
    public bool IsCurrent => Newest.Index == 0;
}

/// <summary>Erste und letzte Zeile einer Logdatei als lokale Zeit; <c>null</c> ohne Zeile mit Zeitstempel.</summary>
public sealed record LogFileTimes(LogRunFile File, DateTime? First, DateTime? Last)
{
    /// <summary>
    /// Uhrzeiten aus den Zeilen, Datum aus der letzten Änderung der Datei. Läge die letzte Zeile mehr als eine Minute nach der Änderung,
    /// stammt sie vom Vortag (Mitternacht); die erste Zeile liegt höchstens 24 h vor der letzten.
    /// </summary>
    public static LogFileTimes For(LogRunFile file)
    {
        var (first, last) = LogReader.FirstAndLastTime(file.Path);
        if (first is null || last is null)
        {
            return new LogFileTimes(file, null, null);
        }

        var written = file.LastWriteUtc.ToLocalTime();
        var lastLocal = written.Date + last.Value;
        if (lastLocal > written.AddMinutes(1))
        {
            lastLocal = lastLocal.AddDays(-1);
        }

        var span = last.Value - first.Value;
        if (span < TimeSpan.Zero)
        {
            span += TimeSpan.FromDays(1);
        }

        return new LogFileTimes(file, lastLocal - span, lastLocal);
    }
}

/// <summary>Fasst Logdateien zu Läufen zusammen (Spec Scope-Verfolgung, Teil 1).</summary>
public static class LogRunChain
{
    /// <summary>Die nächste Datei gehört zum selben Lauf, wenn sie höchstens so lange nach dem Ende der vorigen beginnt.</summary>
    public static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gruppiert Dateien zu Läufen, neuester Lauf zuerst. <paramref name="currentRunStart"/> ist der Start des laufenden bzw. letzten
    /// Spiels (erste Zeile von <c>debug.log</c>, das das Spiel beim Start neu anlegt und nicht wechselt): Alle neuesten Dateien, die nicht
    /// vor diesem Start enden, sind dieser Lauf, egal wie lange zwischen zwei Meldungen nichts kam. Für ältere Läufe (oder ohne
    /// <c>debug.log</c>) gilt die Lückenregel: Eine ältere Datei gehört zum Lauf, wenn der Lauf höchstens <see cref="MaxGap"/> nach ihrem
    /// Ende beginnt. Dateien ohne Zeitstempel hängen am neueren Nachbarn; ohne Startzeit bleibt eine <c>error.log</c> ohne Zeitstempel
    /// (direkt nach dem Spielstart) ein eigener Lauf.
    /// </summary>
    public static IReadOnlyList<LogRun> Group(IReadOnlyList<LogFileTimes> files, DateTime? currentRunStart = null)
    {
        var groups = new List<List<LogFileTimes>>();
        List<LogFileTimes>? current = null;
        var inCurrentRun = currentRunStart is not null;
        foreach (var file in files.OrderBy(f => f.File.Index))
        {
            if (inCurrentRun && (file.Last is null || file.Last >= currentRunStart))
            {
                if (current is null)
                {
                    current = new List<LogFileTimes>();
                    groups.Add(current);
                }

                current.Add(file);
                continue;
            }

            if (inCurrentRun)
            {
                // Die erste Datei, die vor dem Start endet, beginnt die älteren Läufe.
                inCurrentRun = false;
                current = null;
            }

            if (current is null || !Joins(current, file))
            {
                current = new List<LogFileTimes>();
                groups.Add(current);
            }

            current.Add(file);
        }

        return groups.Select(ToRun).ToList();
    }

    private static bool Joins(List<LogFileTimes> run, LogFileTimes older)
    {
        var oldestTimed = run.LastOrDefault(f => f.First is not null);
        if (oldestTimed is null)
        {
            // Nur Dateien ohne Zeitstempel: eine leere error.log bleibt allein, sonst hängt die ältere Datei an.
            return !(run.Count == 1 && run[0].File.Index == 0);
        }

        if (older.Last is null)
        {
            return true;
        }

        var gap = oldestTimed.First!.Value - older.Last.Value;
        return gap >= TimeSpan.Zero && gap <= MaxGap;
    }

    private static LogRun ToRun(List<LogFileTimes> newestFirst)
    {
        var files = newestFirst.AsEnumerable().Reverse().ToList();
        var start = files.FirstOrDefault(f => f.First is not null)?.First;
        return new LogRun(
            files.Select(f => f.File).ToList(),
            start,
            files.Any(f => f.File.Index == ErrorLogLocations.MaxPreviousRuns));
    }
}

/// <summary>Ein ganzer gelesener Lauf; <c>NewestNext</c> ist die Leseposition am Ende der neuesten Datei (für den Live-Start).</summary>
public sealed record RunRead(List<LogLine> Lines, LogPosition NewestNext);
