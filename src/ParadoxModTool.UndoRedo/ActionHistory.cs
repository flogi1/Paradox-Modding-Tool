namespace ParadoxModTool.UndoRedo;

/// <summary>
/// Der zentrale, spielprofil-/werkzeugübergreifende Undo/Redo-Stack des Programms
/// (Abschnitt 5: "Undo für Programmaktionen ... laufen über denselben Undo-Stack,
/// protokolliert als Aktionsverlauf mit Beschreibung"). Ein einzelnes
/// <c>ActionHistory</c>-Objekt wird beim Start des Hauptfensters angelegt; jede
/// änderende Werkzeug-Operation (Speichern, Merge, Massenumbenennung, Formatierung, ...)
/// wird darüber ausgeführt, statt den jeweiligen Zustand direkt zu verändern — damit der
/// "Rückgängig"-Knopf in der Werkzeugleiste unabhängig davon funktioniert, welches
/// Werkzeug zuletzt etwas verändert hat.
/// </summary>
public sealed class ActionHistory
{
    private readonly List<IUndoableAction> _undoStack = new();
    private readonly List<IUndoableAction> _redoStack = new();
    private readonly int? _maxEntries;

    /// <param name="maxEntries">
    /// Optionale Obergrenze für die Anzahl gespeicherter Undo-Schritte (älteste Einträge
    /// werden verworfen, wenn das Limit überschritten wird). Ohne Angabe unbegrenzt.
    /// </param>
    public ActionHistory(int? maxEntries = null)
    {
        if (maxEntries is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries muss größer als 0 sein, falls angegeben.");
        }

        _maxEntries = maxEntries;
    }

    /// <summary>Wird ausgelöst, nachdem sich der Stack verändert hat (Execute/Record/Undo/Redo/Clear) — z. B. um Werkzeugleisten-Buttons zu aktivieren/deaktivieren.</summary>
    public event Action? Changed;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Beschreibungen der rückgängig machbaren Aktionen, älteste zuerst — Grundlage für eine "Aktionsverlauf"-Anzeige.</summary>
    public IReadOnlyList<string> UndoDescriptions => _undoStack.Select(a => a.Description).ToList();

    public IReadOnlyList<string> RedoDescriptions => _redoStack.Select(a => a.Description).ToList();

    /// <summary>Beschreibung der Aktion, die als Nächstes rückgängig gemacht würde, oder null, wenn der Stack leer ist.</summary>
    public string? NextUndoDescription => CanUndo ? _undoStack[^1].Description : null;

    public string? NextRedoDescription => CanRedo ? _redoStack[^1].Description : null;

    /// <summary>
    /// Führt eine neue Aktion aus (ruft <see cref="IUndoableAction.Do"/> auf), legt sie auf
    /// den Undo-Stack und verwirft den bisherigen Redo-Stack (neue Historie ab diesem Punkt).
    /// </summary>
    public void Execute(IUndoableAction action)
    {
        action.Do();
        Record(action);
    }

    /// <summary>
    /// Registriert eine bereits ausgeführte Aktion nachträglich auf dem Stack, ohne
    /// <see cref="IUndoableAction.Do"/> erneut aufzurufen — z. B. wenn ein AvalonEdit-
    /// Texteintrag bereits über den eigenen Editor-Undo-Mechanismus stattgefunden hat und
    /// nur im gemeinsamen Aktionsverlauf sichtbar gemacht werden soll.
    /// </summary>
    public void Record(IUndoableAction action)
    {
        _undoStack.Add(action);
        TrimIfNeeded();
        _redoStack.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Macht die letzte Aktion rückgängig und liefert sie zurück — der Aufrufer (Hauptfenster)
    /// nutzt das Rückgabeobjekt, um z. B. über <see cref="FileWriteUndoableAction.FilePath"/>
    /// (ggf. rekursiv über <see cref="CompositeUndoableAction.Actions"/>) herauszufinden, welche
    /// Dateien betroffen waren, und offene Tabs dafür neu zu laden (Nutzer-Feedback: "sollten
    /// sich dann auch aktive Dateien neu laden, sonst führt das zu Verwirrung"). Bestehende
    /// Aufrufer, die den Rückgabewert ignorieren (Aufruf als Anweisung), bleiben unverändert
    /// funktionsfähig.
    /// </summary>
    public IUndoableAction Undo()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("Kein Rückgängig verfügbar.");
        }

        var action = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        action.Undo();
        _redoStack.Add(action);
        Changed?.Invoke();
        return action;
    }

    /// <summary>Wie <see cref="Undo"/>, aber für "Wiederholen" — liefert ebenfalls die verarbeitete Aktion zurück.</summary>
    public IUndoableAction Redo()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("Kein Wiederholen verfügbar.");
        }

        var action = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        action.Do();
        _undoStack.Add(action);
        Changed?.Invoke();
        return action;
    }

    /// <summary>Leert den kompletten Verlauf (z. B. beim Wechsel des aktiven Mod-Ordners/Spielprofils).</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Changed?.Invoke();
    }

    private void TrimIfNeeded()
    {
        if (_maxEntries is int max)
        {
            while (_undoStack.Count > max)
            {
                _undoStack.RemoveAt(0);
            }
        }
    }
}
