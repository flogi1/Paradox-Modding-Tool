using ParadoxModTool.UndoRedo;

namespace ParadoxModTool.Generators;

/// <summary>
/// Sicherung einer externen Datei vor dem Überschreiben: eine bytegleiche Kopie unter <c>&lt;Ordner&gt;/backups/&lt;Name&gt;.&lt;jjjj-MM-tt_HHmmss&gt;&lt;Endung&gt;</c>
/// (gibt es den Namen schon, mit <c>_2</c>, <c>_3</c> … davor der Endung). Rückgängig lässt die Sicherung stehen; sie gehört nicht zum Rückgängig-Schritt,
/// sonst löschte Strg+Z oder das Zurücknehmen nach einem Schreibfehler genau die Kopie, die den alten Stand bewahrt.
/// </summary>
public sealed class ExternalBackupAction : IUndoableAction
{
    private readonly string _sourcePath;
    private readonly DateTime _stamp;

    public ExternalBackupAction(string description, string sourcePath, DateTime stamp)
    {
        Description = description;
        _sourcePath = sourcePath;
        _stamp = stamp;
    }

    public string Description { get; }

    /// <summary>Pfad der angelegten Sicherung; <see langword="null"/>, solange <see cref="Do"/> nicht lief.</summary>
    public string? BackupPath { get; private set; }

    /// <summary>Kopiert die Datei beim ersten Ausführen; ein Wiederholen legt keine zweite Kopie an.</summary>
    public void Do()
    {
        if (BackupPath is not null)
        {
            return;
        }

        var folder = Path.Combine(Path.GetDirectoryName(_sourcePath) ?? string.Empty, "backups");
        Directory.CreateDirectory(folder);
        var name = Path.GetFileNameWithoutExtension(_sourcePath) + "." + _stamp.ToString("yyyy-MM-dd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var extension = Path.GetExtension(_sourcePath);
        var path = Path.Combine(folder, name + extension);
        for (var n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(folder, name + "_" + n + extension);
        }

        File.Copy(_sourcePath, path, overwrite: false);
        BackupPath = path;
    }

    /// <summary>Lässt die Sicherung bewusst stehen.</summary>
    public void Undo()
    {
    }
}
