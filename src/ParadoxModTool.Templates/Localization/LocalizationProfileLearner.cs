using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ParadoxModTool.Scopes;

namespace ParadoxModTool.Templates;

/// <summary>Eine Datei, aus der gelernt wird: Skript-, GUI- oder Loc-Datei mit Größe und Änderungszeit für den Fingerabdruck.</summary>
public sealed record LocalizationSourceFile(string Folder, string Path, long Size, long WriteTicks);

public sealed record LocalizationLearnResult(LocalizationProfiles Profiles, TimeSpan Duration);

/// <summary>Liest die Schlüssel aus dem Text einer Loc-Datei (<c> key:0 "Text"</c>).</summary>
public static class LocalizationKeyReader
{
    private static readonly Regex KeyLine = new(@"^[ \t]+([A-Za-z0-9_.\-']+):\d*[ \t]+""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IEnumerable<string> ReadKeys(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var match = KeyLine.Match(line);
            if (match.Success)
            {
                yield return match.Groups[1].Value;
            }
        }
    }
}

/// <summary>
/// Lernt die Lokalisierungs-Profile aus den Vanilla-Dateien eines Spiels: für jeden Ordner unter <c>common</c> sowie <c>events</c>, wie die Schlüssel der Objekte
/// aus dem Objektnamen gebildet werden, und für <c>gui</c>, <c>events</c> und die Ordner unter <c>common</c>, in welchen Feldern Loc-Schlüssel stehen.
/// </summary>
public static class LocalizationProfileLearner
{
    private static readonly string[] AreaFolders = { "in_game", "main_menu", "loading_screen" };

    /// <summary>Skriptdateien: <c>common/&lt;Ordner&gt;</c> und <c>events</c> (<c>*.txt</c>, ohne <c>readme</c>) sowie <c>gui</c> (<c>*.gui</c>), unter der Wurzel und unter <c>in_game</c>.</summary>
    public static IReadOnlyList<LocalizationSourceFile> EnumerateScriptFiles(IEnumerable<string> roots)
    {
        var files = new List<LocalizationSourceFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

        void AddFolder(string folder, string directory, string pattern)
        {
            foreach (var path in Directory.EnumerateFiles(directory, pattern, options))
            {
                if (Path.GetFileName(path).StartsWith("readme", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var full = Path.GetFullPath(path);
                if (!seen.Add(full))
                {
                    continue;
                }

                var info = new FileInfo(full);
                files.Add(new LocalizationSourceFile(folder, full, info.Length, info.LastWriteTimeUtc.Ticks));
            }
        }

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var baseDirectory in new[] { root, Path.Combine(root, "in_game") })
            {
                var common = Path.Combine(baseDirectory, "common");
                if (Directory.Exists(common))
                {
                    foreach (var directory in Directory.GetDirectories(common))
                    {
                        AddFolder("common/" + Path.GetFileName(directory), directory, "*.txt");
                    }
                }

                var events = Path.Combine(baseDirectory, "events");
                if (Directory.Exists(events))
                {
                    AddFolder("events", events, "*.txt");
                }

                var gui = Path.Combine(baseDirectory, "gui");
                if (Directory.Exists(gui))
                {
                    AddFolder("gui", gui, "*.gui");
                }
            }
        }

        return files;
    }

    /// <summary>Die Loc-Dateien der Sprache (<c>*_l_english.yml</c>) unter allen <c>localization</c>-Ordnern der Wurzeln, ihrer Bereiche (<c>in_game</c>, <c>main_menu</c>, <c>loading_screen</c>) und der DLC.</summary>
    public static IReadOnlyList<LocalizationSourceFile> EnumerateLocalizationFiles(IEnumerable<string> roots, string language)
    {
        var files = new List<LocalizationSourceFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var pattern = "*_l_" + language + ".yml";

        IEnumerable<string> Bases(string root)
        {
            yield return root;
            foreach (var area in AreaFolders)
            {
                yield return Path.Combine(root, area);
            }

            var dlc = Path.Combine(root, "dlc");
            if (Directory.Exists(dlc))
            {
                foreach (var directory in Directory.GetDirectories(dlc))
                {
                    yield return directory;
                    foreach (var area in AreaFolders)
                    {
                        yield return Path.Combine(directory, area);
                    }
                }
            }
        }

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var baseDirectory in Bases(root))
            {
                var localization = Path.Combine(baseDirectory, "localization");
                if (!Directory.Exists(localization))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFiles(localization, pattern, options))
                {
                    var full = Path.GetFullPath(path);
                    if (seen.Add(full))
                    {
                        var info = new FileInfo(full);
                        files.Add(new LocalizationSourceFile("localization", full, info.Length, info.LastWriteTimeUtc.Ticks));
                    }
                }
            }
        }

        return files;
    }

    /// <summary>Fingerabdruck der Dateiliste: ändert sich eine Datei (Größe, Zeit) oder kommt eine hinzu oder fällt weg, ändert sich auch er.</summary>
    public static string ComputeFingerprint(IEnumerable<LocalizationSourceFile> files)
    {
        var lines = files
            .Select(f => $"{f.Path.Replace('\\', '/')}|{f.Size}|{f.WriteTicks}")
            .OrderBy(l => l, StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    /// <summary>Der Fingerabdruck der Skript- und Loc-Dateien unter den Wurzeln; liest nur Dateiinformationen.</summary>
    public static string ComputeFingerprint(IEnumerable<string> roots, string language)
    {
        var list = roots.ToList();
        return ComputeFingerprint(EnumerateScriptFiles(list).Concat(EnumerateLocalizationFiles(list, language)));
    }

    /// <summary>
    /// Lernt die Profile. Wirft <see cref="OperationCanceledException"/>, wenn abgebrochen wird; dann gibt es kein Ergebnis.
    /// <paramref name="progress"/> meldet nach jeder begonnenen Datei (erledigt, gesamt).
    /// </summary>
    public static LocalizationLearnResult Learn(IEnumerable<string> roots, string language, DateTime nowUtc, CancellationToken cancellation = default, IProgress<(int Done, int Total)>? progress = null)
    {
        var started = DateTime.UtcNow;
        var rootList = roots.ToList();
        var scripts = EnumerateScriptFiles(rootList);
        var localizations = EnumerateLocalizationFiles(rootList, language);
        var total = scripts.Count + localizations.Count;
        var done = 0;

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in localizations)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report((++done, total));
            if (ReadText(file.Path) is { } text)
            {
                foreach (var key in LocalizationKeyReader.ReadKeys(text))
                {
                    keys.Add(key);
                }
            }
        }

        var namesByFolder = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var allNames = new HashSet<string>(StringComparer.Ordinal);
        var tally = new LocalizationFieldTally();
        foreach (var file in scripts)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report((++done, total));
            if (ReadText(file.Path) is not { } text)
            {
                continue;
            }

            var tokens = ScopeLexer.Tokenize(text);
            if (file.Folder != "gui")
            {
                if (!namesByFolder.TryGetValue(file.Folder, out var names))
                {
                    namesByFolder[file.Folder] = names = new HashSet<string>(StringComparer.Ordinal);
                }

                foreach (var name in LocalizationScan.TopLevelNames(tokens))
                {
                    names.Add(name);
                    allNames.Add(name);
                }
            }

            LocalizationScan.CountFields(file.Folder, tokens, keys, tally);
        }

        var objects = LocalizationObjectLearner.Learn(
            namesByFolder.ToDictionary(k => k.Key, k => (IReadOnlyCollection<string>)k.Value, StringComparer.Ordinal),
            keys,
            cancellation);

        var profiles = new LocalizationProfiles
        {
            Fingerprint = ComputeFingerprint(scripts.Concat(localizations)),
            LearnedUtc = nowUtc,
            FilesScanned = scripts.Count + localizations.Count,
        };

        foreach (var folder in objects.Keys.Union(tally.Folders, StringComparer.Ordinal).OrderBy(f => f, StringComparer.Ordinal))
        {
            var fields = tally.Select(folder, allNames);
            objects.TryGetValue(folder, out var learning);
            if ((learning is null || learning.Patterns.Count == 0) && fields.Count == 0)
            {
                continue;
            }

            profiles.Profiles.Add(new LocalizationFolderProfile
            {
                Path = folder,
                ObjectCount = learning?.ObjectCount ?? 0,
                Objects = learning?.Patterns.ToList() ?? new List<LocalizationObjectPattern>(),
                Fields = fields.ToList(),
            });
        }

        return new LocalizationLearnResult(profiles, DateTime.UtcNow - started);
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
