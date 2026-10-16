namespace ParadoxModTool.Patches;

/// <summary>Kopiert Ordner samt Unterordnern (KI-Aufträge für Rezepte und Generator-Pakete, Generator-Pakete selbst).</summary>
public static class FolderCopy
{
    /// <summary>Alle Dateien unter <paramref name="source"/> an denselben relativen Pfad unter <paramref name="target"/>; vorhandene Dateien
    /// werden überschrieben. Fehlt <paramref name="source"/>, passiert nichts.</summary>
    public static void CopyTree(string source, string target)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, overwrite: true);
        }
    }
}
