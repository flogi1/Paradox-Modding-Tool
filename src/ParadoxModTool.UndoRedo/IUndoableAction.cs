namespace ParadoxModTool.UndoRedo;

/// <summary>
/// Eine einzelne, rückgängig machbare Programmaktion (Abschnitt 5 der Architektur-
/// Spezifikation: "Undo für Programmaktionen: nicht nur Texteingaben, sondern auch
/// Werkzeug-Operationen (Merge, Massenumbenennung, Formatierung) laufen über denselben
/// Undo-Stack, protokolliert als Aktionsverlauf mit Beschreibung."). Jede Werkzeug-
/// Operation, die etwas verändert, implementiert diese Schnittstelle (oder nutzt eine der
/// vorgefertigten Implementierungen wie <see cref="DelegateUndoableAction"/>,
/// <see cref="CompositeUndoableAction"/> oder <see cref="FileWriteUndoableAction"/>) und wird
/// über <see cref="ActionHistory.Execute"/> ausgeführt.
/// </summary>
public interface IUndoableAction
{
    /// <summary>Für den Aktionsverlauf in der UI, z. B. "Merge: common/ideas/idea1.txt" oder "Datei gespeichert: idea1.txt".</summary>
    string Description { get; }

    /// <summary>Führt die Aktion (erneut) aus. Wird sowohl beim ersten Ausführen als auch bei Redo aufgerufen.</summary>
    void Do();

    /// <summary>Macht die Aktion rückgängig und stellt den Zustand von vor <see cref="Do"/> wieder her.</summary>
    void Undo();
}
