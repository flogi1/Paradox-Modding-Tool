using System.IO.Compression;
using ParadoxModTool.Mods;

namespace ParadoxModTool.GitIntegration;

public sealed record PackagedZip(GitHubZipVariant Variant, string FileName, string Path, int FileCount, long TotalBytes);

/// <summary>Baut die ZIP-Anhänge eines Releases aus dem Mod-Ordner: je Variante eine Kopie mit den Regeln der Variante (siehe <see cref="ModStaging"/>), gepackt mit <c>System.IO.Compression</c>.</summary>
public static class ZipPackager
{
    public static ModStagingRules RulesFor(GitHubZipVariant variant) =>
        ModStagingRules.Default.Extend(variant.ExcludedFolders, variant.ExcludedFilePatterns, variant.OnlyFolders);

    public static StagingResult Measure(string modFolder, GitHubZipVariant variant) => ModStaging.Measure(modFolder, RulesFor(variant));

    /// <summary>Probleme der angehakten Varianten: doppelte Dateinamen, Varianten ohne Datei, nicht lesbarer Ordner. Keine angehakte Variante ist kein Problem (Release ohne Anhang).</summary>
    public static IReadOnlyList<string> Validate(string modFolder, string modName, string version, string tag, IReadOnlyList<GitHubZipVariant> variants)
    {
        var problems = new List<string>();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in variants.Where(v => v.Enabled))
        {
            var fileName = GitHubNames.ExpandFileName(variant.FileNameFormat, modName, version, tag, variant.Name);
            if (names.TryGetValue(fileName, out var other))
            {
                problems.Add(GitTexts.ZipSameName(other, variant.Name, fileName));
            }
            else
            {
                names[fileName] = variant.Name;
            }

            try
            {
                if (Measure(modFolder, variant).FileCount == 0)
                {
                    problems.Add(GitTexts.ZipEmpty(variant.Name));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problems.Add(GitTexts.ModFolderUnreadable(ex.Message));
            }
        }

        return problems;
    }

    public static IReadOnlyList<PackagedZip> Build(
        string modFolder,
        string modName,
        string version,
        string tag,
        IReadOnlyList<GitHubZipVariant> variants,
        string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);
        var result = new List<PackagedZip>();
        foreach (var variant in variants.Where(v => v.Enabled))
        {
            var fileName = GitHubNames.ExpandFileName(variant.FileNameFormat, modName, version, tag, variant.Name);
            var zipPath = System.IO.Path.Combine(outputFolder, fileName);
            var stage = System.IO.Path.Combine(outputFolder, ".stage-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var staged = ModStaging.Stage(modFolder, stage, RulesFor(variant));
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                using (var stream = File.Create(zipPath))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    foreach (var file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        var relative = System.IO.Path.GetRelativePath(stage, file).Replace('\\', '/');
                        var entryName = string.IsNullOrEmpty(variant.RootFolder) ? relative : variant.RootFolder + "/" + relative;
                        archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                    }
                }

                result.Add(new PackagedZip(variant, fileName, zipPath, staged.FileCount, staged.TotalBytes));
            }
            finally
            {
                if (Directory.Exists(stage))
                {
                    try
                    {
                        Directory.Delete(stage, recursive: true);
                    }
                    catch (IOException)
                    {
                        // Aufräumen ist nur ein Versuch.
                    }
                }
            }
        }

        return result;
    }
}
