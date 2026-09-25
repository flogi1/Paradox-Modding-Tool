using System.Text;

namespace ParadoxModTool.UndoRedo;

/// <summary>
/// Rückgängig machbares Schreiben einer Datei — der häufigste Fall für Werkzeug-Operationen
/// in diesem Programm (Speichern, Merge-Ergebnis schreiben, Formatierung anwenden, ...).
/// Merkt sich vor dem Schreiben den bisherigen Inhalt (oder, falls die Datei vorher nicht
/// existierte, dass sie neu angelegt wurde) und stellt genau das bei <see cref="Undo"/> wieder her.
/// </summary>
public sealed class FileWriteUndoableAction : IUndoableAction
{
    private readonly string _filePath;
    private readonly string _newContent;
    private readonly Encoding _encoding;

    private string? _previousContent;
    private bool _fileExistedBefore;
    private bool _executed;

    public FileWriteUndoableAction(string description, string filePath, string newContent, Encoding? encoding = null)
    {
        Description = description;
        _filePath = filePath;
        _newContent = newContent;
        _encoding = encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    }

    public string Description { get; }

    /// <summary>
    /// Der Pfad der von dieser Aktion geschriebenen Datei — genutzt vom Hauptfenster, um nach
    /// einem Undo/Redo herauszufinden, welche offenen Tabs neu geladen werden müssen (Nutzer-
    /// Feedback: "sollten sich dann auch aktive Dateien neu laden, sonst führt das zu Verwirrung").
    /// </summary>
    public string FilePath => _filePath;

    public void Do()
    {
        if (!_executed)
        {
            // Nur beim allerersten Ausführen den "vorherigen" Zustand einfrieren — bei einem
            // späteren Redo (nach einem Undo) darf der schon einmal ermittelte Vorzustand
            // nicht durch den zwischenzeitlich von Undo() wiederhergestellten Inhalt ersetzt werden.
            _fileExistedBefore = File.Exists(_filePath);
            _previousContent = _fileExistedBefore ? File.ReadAllText(_filePath) : null;
        }

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_filePath, _newContent, _encoding);
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
            File.WriteAllText(_filePath, _previousContent!, _encoding);
        }
        else if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
