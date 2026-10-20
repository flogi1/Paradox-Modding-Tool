using System.Collections.Concurrent;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.GuiDesign.Text;

/// <summary>
/// Texte der Jomini-Spiele (CK III, Victoria 3, EU V) je Sprache für die Vorschau. Sprachen heißen kanonisch <c>l_&lt;name&gt;</c> (<c>l_english</c>, <c>l_german</c>);
/// geladen werden die Dateien <c>*_l_&lt;name&gt;.yml</c> unter den Lokalisierungsordnern. Ladereihenfolge, ein späterer Schlüssel gewinnt:
/// <c>clausewitz</c>, <c>jomini</c>, <c>game</c>, jedes <c>game/dlc/&lt;name&gt;</c> (sortiert), die Abhängigkeiten in umgekehrter Reihenfolge (die erste gewinnt
/// unter ihnen), zuletzt die Mod. Jede Wurzel zählt mit sich selbst und mit <c>&lt;Wurzel&gt;/&lt;Kontext&gt;</c> (EU V: <c>in_game</c>, <c>main_menu</c>,
/// <c>loading_screen</c>). Dateien unter einem Ordner <c>replace</c> laden je Wurzel nach allen anderen. Geladen wird träge je Sprache beim ersten Zugriff;
/// der erste Aufruf darf von jedem Thread kommen.
/// </summary>
public sealed class JominiLocalisation
{
    /// <summary>Die kanonische Standardsprache und Rückfallsprache.</summary>
    public const string English = "l_english";

    private const string ReplaceFolder = "replace";
    private const string LanguagePrefix = "l_";

    public static readonly JominiLocalisation Empty = new(null, Array.Empty<string>(), null, Array.Empty<string>());

    private readonly string? _installDirectory;
    private readonly string[] _dependencyRoots;
    private readonly string? _modRoot;
    private readonly string[] _contexts;

    private readonly ConcurrentDictionary<string, Lazy<Dictionary<string, string>>> _byLanguage = new(StringComparer.Ordinal);
    private readonly Lazy<IReadOnlyList<string>> _languages;

    private JominiLocalisation(string? installDirectory, IEnumerable<string> dependencyRoots, string? modRoot, IReadOnlyList<string> contexts)
    {
        _installDirectory = installDirectory;
        _dependencyRoots = dependencyRoots.ToArray();
        _modRoot = modRoot;
        _contexts = contexts.ToArray();
        _languages = new Lazy<IReadOnlyList<string>>(FindLanguages, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// <paramref name="dependencyRoots"/> in Reihenfolge der Abhängigkeiten (die erste gewinnt), <paramref name="contexts"/> die Unterordner, in denen ein
    /// Spiel zusätzlich Lokalisierung hält (EU V; leer für CK III und Victoria 3). Liest hier noch nichts.
    /// </summary>
    public static JominiLocalisation Load(string installDirectory, IEnumerable<string> dependencyRoots, string? modRoot, IReadOnlyList<string> contexts) =>
        new(installDirectory, dependencyRoots, modRoot, contexts);

    /// <summary>Dieselben Wurzeln, aber nichts geladen: der nächste Zugriff liest die Dateien neu (nach dem Speichern einer .yml-Datei).</summary>
    public JominiLocalisation Fresh() => new(_installDirectory, _dependencyRoots, _modRoot, _contexts);

    /// <summary>Kanonische Sprachcodes, die Vanilla hat: <c>l_english</c> zuerst, der Rest sortiert.</summary>
    public IReadOnlyList<string> Languages => _languages.Value;

    /// <summary>Text zum Schlüssel in genau dieser Sprache oder <see langword="null"/>.</summary>
    public string? Text(string key, string language) =>
        Texts(language).TryGetValue(key, out var text) ? text : null;

    /// <summary>Text in dieser Sprache, sonst der englische, sonst <see langword="null"/>.</summary>
    public string? TextOrEnglish(string key, string language) =>
        Text(key, language) ?? Text(key, English);

    private Dictionary<string, string> Texts(string language)
    {
        var code = Normalize(language);
        return _byLanguage.GetOrAdd(code, c => new Lazy<Dictionary<string, string>>(() => LoadLanguage(c), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>Kanonische Form <c>l_&lt;name&gt;</c>; fehlend oder leer heißt Englisch.</summary>
    internal static string Normalize(string? language)
    {
        var code = language?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code))
        {
            return English;
        }

        return code.StartsWith(LanguagePrefix, StringComparison.Ordinal) ? code : LanguagePrefix + code;
    }

    /// <summary>Vanilla-Wurzeln in Ladereihenfolge: <c>clausewitz</c>, <c>jomini</c>, <c>game</c> und die DLCs.</summary>
    private IEnumerable<string> VanillaRoots()
    {
        if (string.IsNullOrEmpty(_installDirectory))
        {
            yield break;
        }

        foreach (var name in new[] { "clausewitz", "jomini", "game" })
        {
            var root = Path.Combine(_installDirectory, name);
            if (Directory.Exists(root))
            {
                yield return root;
            }
        }

        var dlc = Path.Combine(_installDirectory, "game", "dlc");
        if (Directory.Exists(dlc))
        {
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(dlc);
            }
            catch (Exception)
            {
                yield break;
            }

            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            foreach (var directory in directories)
            {
                yield return directory;
            }
        }
    }

    /// <summary>Alle Wurzeln in Ladereihenfolge (später gewinnt): Vanilla, Abhängigkeiten rückwärts, Mod.</summary>
    private IEnumerable<string> AllRoots()
    {
        foreach (var root in VanillaRoots())
        {
            yield return root;
        }

        foreach (var root in _dependencyRoots.Reverse())
        {
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                yield return root;
            }
        }

        if (!string.IsNullOrWhiteSpace(_modRoot) && Directory.Exists(_modRoot))
        {
            yield return _modRoot;
        }
    }

    /// <summary>Die Lokalisierungsordner einer Wurzel: sie selbst, dann je Kontext.</summary>
    private IEnumerable<string> LocalizationFoldersOf(string root)
    {
        foreach (var folder in LocalizationFolders.Existing(root))
        {
            yield return folder;
        }

        foreach (var context in _contexts)
        {
            foreach (var folder in LocalizationFolders.Existing(Path.Combine(root, context)))
            {
                yield return folder;
            }
        }
    }

    /// <summary>Die Basen einer Wurzel in Ladereihenfolge: die Wurzel selbst, dann je Kontext in der Reihenfolge von <c>contexts</c>.</summary>
    private IEnumerable<string> BasesOf(string root)
    {
        yield return root;
        foreach (var context in _contexts)
        {
            yield return Path.Combine(root, context);
        }
    }

    private Dictionary<string, string> LoadLanguage(string language)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var pattern = "*_" + language + ".yml";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in AllRoots())
        {
            // Je Basis (Wurzel, dann jeder Kontext) nach Pfad sortiert; "replace"-Dateien aller Basen laden danach, ebenfalls je Basis.
            var normal = new List<string>();
            var replace = new List<string>();
            foreach (var baseDirectory in BasesOf(root))
            {
                var normalOfBase = new List<string>();
                var replaceOfBase = new List<string>();
                foreach (var folder in LocalizationFolders.Existing(baseDirectory))
                {
                    foreach (var file in EnumerateYml(folder, pattern))
                    {
                        try
                        {
                            if (!seen.Add(Path.GetFullPath(file)))
                            {
                                continue;
                            }

                            (IsBelowReplace(folder, file) ? replaceOfBase : normalOfBase).Add(file);
                        }
                        catch (Exception)
                        {
                            // Unbrauchbarer Pfad: Datei überspringen, die Sprache lädt trotzdem.
                        }
                    }
                }

                normalOfBase.Sort(StringComparer.OrdinalIgnoreCase);
                replaceOfBase.Sort(StringComparer.OrdinalIgnoreCase);
                normal.AddRange(normalOfBase);
                replace.AddRange(replaceOfBase);
            }

            // Ein späterer Schlüssel überschreibt den früheren. Gelesen wird parallel, zusammengeführt der Reihe nach.
            var files = normal.Concat(replace).ToArray();
            var parsed = new List<KeyValuePair<string, string>>[files.Length];
            Parallel.For(0, files.Length, i => parsed[i] = ReadFile(files[i]));
            foreach (var entries in parsed)
            {
                foreach (var (key, text) in entries)
                {
                    result[key] = text;
                }
            }
        }

        return result;
    }

    private static string[] EnumerateYml(string folder, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories).ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Liegt die Datei unter einem Ordner <c>replace</c> (unterhalb des Lokalisierungsordners, Groß-/Kleinschreibung egal)?</summary>
    private static bool IsBelowReplace(string localizationFolder, string file)
    {
        var relative = Path.GetRelativePath(localizationFolder, Path.GetDirectoryName(file) ?? localizationFolder);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, ReplaceFolder, StringComparison.OrdinalIgnoreCase));
    }

    private static List<KeyValuePair<string, string>> ReadFile(string path)
    {
        try
        {
            // Der Text steht in der Datei als \n; im Spiel ist das ein Zeilenumbruch.
            var entries = ClassicLocalisation.Parse(GuiTextReader.Read(path).Text);
            for (var i = 0; i < entries.Count; i++)
            {
                entries[i] = new KeyValuePair<string, string>(entries[i].Key, entries[i].Value.Replace("\\n", "\n"));
            }

            return entries;
        }
        catch (Exception)
        {
            return new List<KeyValuePair<string, string>>();
        }
    }

    /// <summary>Unterordner der Vanilla-Lokalisierungsordner (außer <c>replace</c>), die mindestens eine <c>*_l_&lt;name&gt;.yml</c> halten.</summary>
    private IReadOnlyList<string> FindLanguages()
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in VanillaRoots())
        {
            foreach (var folder in LocalizationFoldersOf(root))
            {
                string[] subfolders;
                try
                {
                    subfolders = Directory.GetDirectories(folder);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var subfolder in subfolders)
                {
                    var name = Path.GetFileName(subfolder).ToLowerInvariant();
                    if (name.Length == 0 || name == ReplaceFolder)
                    {
                        continue;
                    }

                    var code = LanguagePrefix + name;
                    if (!found.Contains(code) && EnumerateYml(subfolder, "*_" + code + ".yml").Length > 0)
                    {
                        found.Add(code);
                    }
                }
            }
        }

        var sorted = found.Where(code => code != English).OrderBy(code => code, StringComparer.Ordinal).ToList();
        if (found.Contains(English))
        {
            sorted.Insert(0, English);
        }

        return sorted;
    }
}
