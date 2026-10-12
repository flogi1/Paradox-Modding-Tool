namespace ParadoxModTool.Generators;

/// <summary>
/// Zwischenspeicher der <see cref="LocalizationKeyIndex"/>-Teile je Ordner (Spielordner, Mod), gebaut im Hintergrund. Eine laufende oder fertige Aufgabe
/// wird geteilt (das Vorwärmen beim Öffnen eines Pakets mit <c>autoNumber</c> und die Vorschau nutzen dieselbe); eine abgebrochene oder gescheiterte
/// Aufgabe gilt als nicht vorhanden und wird beim nächsten <see cref="Get"/> neu gebaut, ein halber Index bleibt also nie liegen. Das gilt auch für eine
/// Aufgabe, deren Abbruch angefordert ist, die aber noch läuft (bis zur nächsten Prüfung des Tokens kann das dauern).
/// </summary>
public sealed class KeyIndexCache
{
    private readonly Func<string, CancellationToken, LocalizationKeyIndex> _build;
    private readonly Dictionary<string, (Task<LocalizationKeyIndex> Task, CancellationToken Token)> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>Baut jeden Ordner mit <see cref="LocalizationKeyIndex.Build"/>.</summary>
    public KeyIndexCache()
        : this((root, ct) => LocalizationKeyIndex.Build(new[] { root }, ct: ct))
    {
    }

    /// <param name="build">Baut den Teil eines Ordners (für Tests austauschbar); läuft auf dem Threadpool.</param>
    public KeyIndexCache(Func<string, CancellationToken, LocalizationKeyIndex> build) => _build = build;

    /// <summary>
    /// Der Teil des Ordners <paramref name="root"/>: die vorhandene Aufgabe oder eine neu gestartete. <paramref name="ct"/> gilt nur für eine neu gestartete
    /// Aufgabe; wer auf eine schon laufende nur warten will, wartet mit <see cref="Task.WaitAsync(CancellationToken)"/>.
    /// </summary>
    public Task<LocalizationKeyIndex> Get(string root, CancellationToken ct = default)
    {
        lock (_lock)
        {
            // Fertig gebaut zählt auch, wenn das Token später abgebrochen wurde; eine laufende Aufgabe nur, solange ihr Abbruch nicht angefordert ist
            if (_tasks.TryGetValue(root, out var existing)
                && (existing.Task.IsCompletedSuccessfully || (!existing.Task.IsCompleted && !existing.Token.IsCancellationRequested)))
            {
                return existing.Task;
            }

            var task = Task.Run(() => _build(root, ct), ct);
            // Fehler beobachten, damit eine gescheiterte Vorwärm-Aufgabe, auf die niemand wartet, nicht als unbeobachtet gemeldet wird
            task.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            _tasks[root] = (task, ct);
            return task;
        }
    }

    /// <summary>Verwirft den Teil des Ordners (z. B. die Mod nach dem Erzeugen, weil sie dann neue Schlüssel hat); der nächste <see cref="Get"/> baut neu.</summary>
    public void Invalidate(string root)
    {
        lock (_lock)
        {
            _tasks.Remove(root);
        }
    }
}
