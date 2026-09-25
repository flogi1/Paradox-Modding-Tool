namespace ParadoxModTool.UndoRedo;

/// <summary>
/// Bequeme <see cref="IUndoableAction"/>-Implementierung für einfache Fälle: Do/Undo werden
/// als Delegaten übergeben, statt für jede kleine Werkzeug-Operation eine eigene Klasse
/// schreiben zu müssen.
/// </summary>
public sealed class DelegateUndoableAction : IUndoableAction
{
    private readonly Action _do;
    private readonly Action _undo;

    public DelegateUndoableAction(string description, Action doAction, Action undoAction)
    {
        Description = description;
        _do = doAction;
        _undo = undoAction;
    }

    public string Description { get; }

    public void Do() => _do();

    public void Undo() => _undo();
}
