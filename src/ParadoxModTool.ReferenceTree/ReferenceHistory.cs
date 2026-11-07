namespace ParadoxModTool.ReferenceTree;

/// <summary>Verlauf der geöffneten Namen im Verweisbaum für die Zurück-Navigation.</summary>
public sealed class ReferenceHistory
{
    private readonly Stack<string> _stack = new();

    /// <summary>Aktuell geöffneter Name oder null.</summary>
    public string? Current { get; private set; }

    /// <summary>True, wenn es einen vorigen Namen gibt.</summary>
    public bool CanGoBack => _stack.Count > 0;

    /// <summary>Öffnet einen Namen. Gleicher Name wie <see cref="Current"/>: nichts; sonst kommt Current auf den Stapel.</summary>
    public void Open(string name)
    {
        if (Current is not null && string.Equals(Current, name, StringComparison.Ordinal)) return;
        if (Current is not null) _stack.Push(Current);
        Current = name;
    }

    /// <summary>Holt den vorigen Namen (wird Current); null, wenn der Verlauf leer ist.</summary>
    public string? Back()
    {
        if (_stack.Count == 0) return null;
        Current = _stack.Pop();
        return Current;
    }

    /// <summary>Leert den Verlauf samt Current.</summary>
    public void Clear()
    {
        _stack.Clear();
        Current = null;
    }
}
