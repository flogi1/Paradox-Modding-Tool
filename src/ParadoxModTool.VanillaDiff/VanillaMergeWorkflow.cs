using ParadoxModTool.VersionManager;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Bündelt den Ablauf der Merge-Funktion aus Abschnitt 7, aufgeteilt in zwei Schritte, damit
/// eine UI zwischen Berechnung und Schreiben eine Konfliktauflösung einschieben kann (siehe
/// <see cref="ConflictResolution"/>): <see cref="ComputeMerge"/> liest nur und merged;
/// <see cref="WriteMergedResult"/> legt den Sicherheits-Snapshot der alten Moddatei an
/// (Wiederverwendung von <see cref="ModVersionManager.CreateSnapshot"/>, Abschnitt 6) und
/// schreibt das Ergebnis als UTF-8 in den Mod-Ordner (mit BOM, wenn die Moddatei eins hatte oder kein gültiges UTF-8 war).
/// </summary>
public static class VanillaMergeWorkflow
{
    public static MergeResult ComputeMerge(string oldVanillaFilePath, string newVanillaFilePath, string modFilePath)
    {
        var oldVanilla = ScriptDocument.Parse(File.ReadAllText(oldVanillaFilePath));
        var newVanilla = ScriptDocument.Parse(File.ReadAllText(newVanillaFilePath));
        var mod = ScriptDocument.Parse(File.ReadAllText(modFilePath));

        return MergeEngine.Merge(oldVanilla, newVanilla, mod);
    }

    public static void WriteMergedResult(ScriptBlock finalRoot, string modFilePath, ModVersionManager versionManager, bool preferSourceVerbatim)
    {
        versionManager.CreateSnapshot($"vor-merge_{Path.GetFileName(modFilePath)}");

        var mergedText = ScriptWriter.Write(finalRoot, preferSourceVerbatim, new ScriptWriteOptions { UseTabs = true });
        // Die Mod-Datei behält ihr BOM (Stellaris liest Skriptdateien mit BOM falsch); ohne BOM nur, wenn sie gültiges UTF-8 ohne BOM ist
        File.WriteAllText(modFilePath, mergedText, ParadoxModTool.GameProfiles.ModFileEncoding.ForWrite(null, modFilePath));
    }

    public static MergeResult MergeFileIntoMod(
        string oldVanillaFilePath,
        string newVanillaFilePath,
        string modFilePath,
        ModVersionManager versionManager,
        bool writeEvenWithConflicts = false)
    {
        var result = ComputeMerge(oldVanillaFilePath, newVanillaFilePath, modFilePath);

        if (result.HasConflicts && !writeEvenWithConflicts)
        {
            return result;
        }

        WriteMergedResult(result.MergedRoot, modFilePath, versionManager, preferSourceVerbatim: false);

        return result;
    }
}
