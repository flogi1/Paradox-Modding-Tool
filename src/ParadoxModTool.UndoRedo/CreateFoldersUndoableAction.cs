namespace ParadoxModTool.UndoRedo;

/// <summary>
/// Legt fehlende Ordner an. Rückgängig entfernt die Ordner, die es vorher nicht gab, sofern sie leer sind (tiefste zuerst). Gehört in einer
/// <see cref="CompositeUndoableAction"/> vor die Dateischritte: Deren Rückgängig läuft zuerst und leert die Ordner.
/// </summary>
public sealed class CreateFoldersUndoableAction : IUndoableAction
{
    private readonly IReadOnlyList<string> _folders;
    private readonly List<string> _created = new();

    public CreateFoldersUndoableAction(string description, IEnumerable<string> folders)
    {
        Description = description;
        _folders = folders.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string Description { get; }

    public void Do()
    {
        _created.Clear();
        foreach (var folder in _folders)
        {
            for (var directory = folder; !string.IsNullOrEmpty(directory) && !Directory.Exists(directory); directory = Path.GetDirectoryName(directory))
            {
                if (!_created.Contains(directory, StringComparer.OrdinalIgnoreCase))
                {
                    _created.Add(directory);
                }
            }

            Directory.CreateDirectory(folder);
        }
    }

    public void Undo()
    {
        foreach (var directory in _created.OrderByDescending(d => d.Length))
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
    }
}
