using System.Text;
using ParadoxModTool.VersionManager;

namespace ParadoxModTool.VanillaDiff;

/// <summary>
/// Bündelt den Ablauf der Merge-Funktion aus Abschnitt 7, aufgeteilt in zwei Schritte, damit
/// eine UI zwischen Berechnung und Schreiben eine Konfliktauflösung einschieben kann (siehe
/// <see cref="ConflictResolution"/>): <see cref="ComputeMerge"/> liest nur und merged;
/// <see cref="WriteMergedResult"/> legt den Sicherheits-Snapshot der alten Moddatei an
/// (Wiederverwendung von <see cref="ModVersionManager.CreateSnapshot"/>, Abschnitt 6) und
/// schreibt das Ergebnis als UTF-8-mit-BOM in den Mod-Ordner.
/// </summary>
public static class VanillaMergeWorkflow
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

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
        File.WriteAllText(modFilePath, mergedText, Utf8WithBom);
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
