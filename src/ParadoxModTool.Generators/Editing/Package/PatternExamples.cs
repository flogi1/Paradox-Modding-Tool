using ParadoxModTool.Patches;

namespace ParadoxModTool.Generators.Editing.Package;

/// <summary>Beispiele aus einer Mod für den Vorschlag-Dialog (Spec Teil 5b, Abschnitt 1): Dateien, oberste Blöcke einer Datei, englische Lokalisierungsschlüssel.</summary>
public static class PatternExamples
{
    /// <summary>Alle Dateien der Mod, relativ mit "/", sortiert.</summary>
    public static IReadOnlyList<string> Files(string modRoot) =>
        Directory.Exists(modRoot)
            ? Directory.EnumerateFiles(modRoot, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(modRoot, f).Replace(Path.DirectorySeparatorChar, '/'))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : Array.Empty<string>();

    /// <summary>Schlüssel der obersten Blöcke einer Datei (wie der Einleser sie vergleicht); leer, wenn die Datei fehlt, unsicher oder unlesbar ist.</summary>
    public static IReadOnlyList<string> BlockKeys(string modRoot, string relativePath)
    {
        if (!SafeRelativePath.IsSafe(relativePath.Trim()) || !File.Exists(GeneratorStateReader.Full(modRoot, relativePath)))
        {
            return Array.Empty<string>();
        }

        try
        {
            return GeneratorStateReader.Parse(modRoot, relativePath).Statements.Select(s => s.Key).ToList();
        }
        catch (GeneratorOutputException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Schlüssel aller englischen Lokalisierungsdateien der Mod (<c>*_l_english.yml</c>), ohne Doppelte, sortiert.</summary>
    public static IReadOnlyList<string> LocalizationKeys(string modRoot) =>
        Directory.Exists(modRoot)
            ? Directory.EnumerateFiles(modRoot, "*_l_english.yml", SearchOption.AllDirectories)
                .SelectMany(f => YmlReader.Values(TextFile.Read(f).Text).Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : Array.Empty<string>();
}
