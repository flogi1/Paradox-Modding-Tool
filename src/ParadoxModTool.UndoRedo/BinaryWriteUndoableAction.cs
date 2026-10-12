namespace ParadoxModTool.UndoRedo;

/// <summary>
/// Rückgängig machbares Schreiben einer Binärdatei (z. B. ein Wappen als .dds aus einem Generator-Paket). Wie <see cref="FileWriteUndoableAction"/>:
/// merkt sich beim ersten Ausführen den alten Inhalt oder dass die Datei fehlte und stellt genau das bei <see cref="Undo"/> wieder her.
/// </summary>
public sealed class BinaryWriteUndoableAction : IUndoableAction
{
    private readonly string _filePath;
    private readonly byte[] _content;

    private byte[]? _previousContent;
    private bool _fileExistedBefore;
    private bool _executed;

    public BinaryWriteUndoableAction(string description, string filePath, byte[] content)
    {
        Description = description;
        _filePath = filePath;
        _content = content;
    }

    public string Description { get; }

    public string FilePath => _filePath;

    public void Do()
    {
        if (!_executed)
        {
            _fileExistedBefore = File.Exists(_filePath);
            _previousContent = _fileExistedBefore ? File.ReadAllBytes(_filePath) : null;
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(_filePath, _content);
        _executed = true;
    }

    public void Undo()
    {
        if (!_executed)
        {
            throw new InvalidOperationException("Aktion wurde noch nicht ausgeführt und kann daher nicht rückgängig gemacht werden.");
        }

        if (_fileExistedBefore)
        {
            File.WriteAllBytes(_filePath, _previousContent!);
        }
        else if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
