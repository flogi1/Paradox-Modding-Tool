using System.IO.Compression;

namespace ParadoxModTool.Desync;

/// <summary>
/// Packt einen entpackten OOS-Dump-Ordner als Zip, damit er sich anhängen lässt. Die Zip enthält den Ordner selbst als obersten
/// Eintrag (<c>&lt;Ordnername&gt;/…</c>), wie die Zips des Spiels. Geschrieben wird über eine temporäre Datei neben dem Ziel,
/// die danach das Ziel ersetzt; im Spielordner wird nichts angelegt.
/// </summary>
public static class DumpZipper
{
    /// <summary>Speichert den Ordner als Zip; eine vorhandene Zieldatei wird ersetzt.</summary>
    /// <exception cref="ArgumentException">Das Ziel liegt innerhalb des Dump-Ordners.</exception>
    /// <exception cref="IOException">Ordner oder Ziel lassen sich nicht lesen bzw. schreiben.</exception>
    /// <exception cref="UnauthorizedAccessException">Kein Zugriffsrecht auf Ordner oder Ziel.</exception>
    public static void SaveAsZip(string dumpFolder, string targetZip)
    {
        var folderFull = Path.GetFullPath(dumpFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var targetFull = Path.GetFullPath(targetZip);
        if (targetFull.StartsWith(folderFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Die Zip darf nicht im Dump-Ordner liegen: " + targetZip, nameof(targetZip));

        var temporary = targetFull + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            ZipFile.CreateFromDirectory(folderFull, temporary, CompressionLevel.Optimal, includeBaseDirectory: true);
            File.Move(temporary, targetFull, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
