namespace ParadoxModTool.Mods;

/// <summary>Was beim Zusammenstellen der Upload-Kopie nicht mitgeht: Ordner an beliebiger Tiefe (nach Name) und Dateien (nach Name oder Endung, <c>*.zip</c>). Groß-/Kleinschreibung zählt nicht.</summary>
public sealed record ModStagingRules(IReadOnlyList<string> ExcludedFolders, IReadOnlyList<string> ExcludedFilePatterns, IReadOnlyList<string>? OnlyFolders = null)
{
    /// <summary>Versionsäste, Vanilla-Historie, Patch-Rezepte, Snapshots und Versionsverwaltung sowie Archive, Sicherungen und Systemdateien. <c>.metadata</c> bleibt drin.</summary>
    public static ModStagingRules Default { get; } = new(
        new[] { ".pmt-versions", ".pmt-vanilla-history", ".pmt-patches", "snapshots", ".git", ".svn" },
        new[] { "*.zip", "*.orig", "*.bak", "*.tmp", "Thumbs.db", "desktop.ini" });

    /// <summary>Diese Regeln plus zusätzliche Ausschlüsse; <paramref name="onlyFolders"/> (nicht leer) ersetzt die bisherige Auswahl.</summary>
    public ModStagingRules Extend(IEnumerable<string>? folders, IEnumerable<string>? patterns, IEnumerable<string>? onlyFolders)
    {
        var only = (onlyFolders ?? Array.Empty<string>()).Select(NormalizeFolder).Where(f => f.Length > 0).ToList();
        return new ModStagingRules(
            ExcludedFolders.Concat(folders ?? Array.Empty<string>()).ToList(),
            ExcludedFilePatterns.Concat(patterns ?? Array.Empty<string>()).ToList(),
            only.Count == 0 ? OnlyFolders : only);
    }

    internal static string NormalizeFolder(string folder) => folder.Trim().Replace('\\', '/').Trim('/');
}

/// <summary>Das Ergebnis des Zusammenstellens (oder Vermessens): Anzahl und Größe der mitgehenden Dateien und die ausgeschlossenen Einträge (relativ mit <c>/</c>, Ordner enden auf <c>/</c>).</summary>
public sealed record StagingResult(int FileCount, long TotalBytes, IReadOnlyList<string> Excluded);

/// <summary>
/// Stellt eine Kopie des Mod-Ordners für den Upload zusammen. SteamCMD lädt den ganzen <c>contentfolder</c> hoch, deshalb geht nur die Kopie ohne die ausgeschlossenen Einträge hinaus.
/// Der Mod-Ordner wird nie verändert. Verknüpfungen (Reparse Points) werden nicht verfolgt und zählen als ausgeschlossen.
/// </summary>
public static class ModStaging
{
    /// <summary>Wie <see cref="Stage"/>, aber ohne zu kopieren (für die Anzeige des Umfangs).</summary>
    public static StagingResult Measure(string source, ModStagingRules rules) => Walk(source, null, rules);

    public static StagingResult Stage(string source, string target, ModStagingRules rules) => Walk(source, target, rules);

    /// <summary>Alle Dateien, die mit den Regeln mitgehen würden (relativ zu <paramref name="source"/>, mit <c>/</c>). Wirft <see cref="DirectoryNotFoundException"/>, wenn der Ordner fehlt.</summary>
    public static IReadOnlyList<string> EnumerateFiles(string source, ModStagingRules rules)
    {
        var files = new List<string>();
        Walk(source, null, rules, (relative, _) => files.Add(relative));
        return files;
    }

    private static StagingResult Walk(string source, string? target, ModStagingRules rules, Action<string, string>? onFile = null)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(ModsTexts.StagingFolderMissing(source));
        }

        if (target is not null)
        {
            Directory.CreateDirectory(target);
        }

        var only = rules.OnlyFolders?.Select(ModStagingRules.NormalizeFolder).Where(f => f.Length > 0).ToList();
        var excluded = new List<string>();
        var count = 0;
        long bytes = 0;

        void Visit(string directory, string relative)
        {
            foreach (var sub in Directory.EnumerateDirectories(directory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(sub);
                var path = relative.Length == 0 ? name : relative + "/" + name;
                if (rules.ExcludedFolders.Contains(name, StringComparer.OrdinalIgnoreCase) || (File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0)
                {
                    excluded.Add(path + "/");
                    continue;
                }

                if (only is { Count: > 0 } && !only.Any(f => f.Equals(path, StringComparison.OrdinalIgnoreCase)
                        || f.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    excluded.Add(path + "/");
                    continue;
                }

                Visit(sub, path);
            }

            foreach (var file in Directory.EnumerateFiles(directory).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(file);
                var path = relative.Length == 0 ? name : relative + "/" + name;
                if (Matches(name, rules.ExcludedFilePatterns))
                {
                    excluded.Add(path);
                    continue;
                }

                if (only is { Count: > 0 } && !only.Any(f => path.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    excluded.Add(path);
                    continue;
                }

                count++;
                bytes += new FileInfo(file).Length;
                onFile?.Invoke(path, file);
                if (target is not null)
                {
                    var destination = Path.Combine(target, path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination, overwrite: false);
                }
            }
        }

        Visit(source, string.Empty);
        return new StagingResult(count, bytes, excluded);
    }

    private static bool Matches(string fileName, IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            var matches = pattern.StartsWith('*')
                ? fileName.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
                : string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
            if (matches)
            {
                return true;
            }
        }

        return false;
    }
}
