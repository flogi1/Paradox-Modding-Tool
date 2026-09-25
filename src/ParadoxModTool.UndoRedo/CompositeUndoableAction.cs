namespace ParadoxModTool.UndoRedo;

/// <summary>
/// Fasst mehrere Einzelaktionen zu einem Eintrag im Aktionsverlauf zusammen — z. B. für eine
/// Massenumbenennung, die mehrere Dateien anfasst, aber im Rückgängig-Menü als EIN Schritt
/// erscheinen soll (Abschnitt 5: "Werkzeug-Operationen (Merge, Massenumbenennung,
/// Formatierung) laufen über denselben Undo-Stack").
/// </summary>
public sealed class CompositeUndoableAction : IUndoableAction
{
    private readonly IReadOnlyList<IUndoableAction> _actions;

    public CompositeUndoableAction(string description, IReadOnlyList<IUndoableAction> actions)
    {
        Description = description;
        _actions = actions;
    }

    public string Description { get; }

    /// <summary>
    /// Die zusammengefassten Einzelaktionen — genutzt vom Hauptfenster, um nach einem Undo/Redo
    /// rekursiv alle betroffenen <see cref="FileWriteUndoableAction.FilePath"/>-Werte einzusammeln
    /// (eine Massenersetzung besteht aus mehreren <see cref="FileWriteUndoableAction"/>-Einzelschritten).
    /// </summary>
    public IReadOnlyList<IUndoableAction> Actions => _actions;

    public void Do()
    {
        foreach (var action in _actions)
        {
            action.Do();
        }
    }

    public void Undo()
    {
        // In umgekehrter Reihenfolge rückgängig machen, damit etwaige Abhängigkeiten
        // zwischen den Einzelschritten korrekt aufgelöst werden (zuletzt ausgeführter
        // Teilschritt wird zuerst rückgängig gemacht).
        for (var i = _actions.Count - 1; i >= 0; i--)
        {
            _actions[i].Undo();
        }
    }
}
