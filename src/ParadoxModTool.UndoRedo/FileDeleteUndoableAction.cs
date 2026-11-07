namespace ParadoxModTool.UndoRedo;

/// <summary>
/// Rückgängig machbares Löschen einer Datei (z. B. die alte Datei, aus der ein Generator-Paket alles verschoben hat). Merkt sich beim ersten Ausführen die
/// Bytes und schreibt sie bei <see cref="Undo"/> zurück; fehlt die Datei beim Ausführen schon, gibt es nichts zurückzuschreiben.
/// </summary>
public sealed class FileDeleteUndoableAction(string description, string path) : IUndoableAction
{
    private byte[]? _previousContent;
    private bool _executed;

    public string Description { get; } = description;

    public string FilePath => path;

    public void Do()
    {
        if (!_executed)
        {
            // Nur beim allerersten Ausführen einfrieren, wie bei FileWriteUndoableAction
            _previousContent = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        _executed = true;
    }

    public void Undo()
    {
        if (!_executed)
        {
            throw new InvalidOperationException("Aktion wurde noch nicht ausgeführt und kann daher nicht rückgängig gemacht werden.");
        }

        if (_previousContent is not null)
        {
            File.WriteAllBytes(path, _previousContent);
        }
    }
}
