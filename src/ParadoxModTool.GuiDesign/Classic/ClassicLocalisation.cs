using ParadoxModTool.GuiDesign.Index;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Englische Texte der klassischen Spiele für die Vorschau: liest alle <c>*_l_english.yml</c> unter <c>&lt;Wurzel&gt;/localisation/</c>. Die Wurzeln laden in
/// Reihenfolge (Vanilla, Abhängigkeiten, Mod), ein späterer Schlüssel überschreibt einen früheren. Geladen wird träge beim ersten <see cref="Text"/>,
/// damit der Aufbau des Arbeitsbereichs nicht auf rund 100 MB yml wartet; der erste Aufruf darf von jedem Thread kommen.
/// </summary>
public sealed class ClassicLocalisation
{
    /// <summary>Ordner mit den Textdateien (beide klassischen Spiele schreiben ihn britisch).</summary>
    public const string Folder = "localisation";

    private const string FileFilter = "*_l_english.yml";

    public static readonly ClassicLocalisation Empty = new(Array.Empty<GuiSourceRoot>());

    private readonly IReadOnlyList<GuiSourceRoot> _roots;
    private readonly Lazy<Dictionary<string, string>> _texts;

    private ClassicLocalisation(IReadOnlyList<GuiSourceRoot> roots)
    {
        _roots = roots;
        _texts = new Lazy<Dictionary<string, string>>(LoadAll, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary><paramref name="roots"/> in Ladereihenfolge (wie <see cref="GuiSourceSet.ClassicRootsFor"/>). Liest hier noch nichts.</summary>
    public static ClassicLocalisation Load(IReadOnlyList<GuiSourceRoot> roots) => new(roots.ToList());

    /// <summary>Text zum Schlüssel oder <see langword="null"/>, wenn es ihn nicht gibt.</summary>
    public string? Text(string key) => _texts.Value.TryGetValue(key, out var text) ? text : null;

    private Dictionary<string, string> LoadAll()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in _roots)
        {
            var folder = Path.Combine(root.Directory, Folder);
            if (!Directory.Exists(folder))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.EnumerateFiles(folder, FileFilter, SearchOption.AllDirectories).ToArray();
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            // Dateien nach Namen, eine spätere überschreibt die frühere. Gelesen wird parallel, zusammengeführt in dieser Reihenfolge.
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
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

    private static List<KeyValuePair<string, string>> ReadFile(string path)
    {
        try
        {
            return Parse(GuiTextReader.Read(path).Text);
        }
        catch (IOException)
        {
            return new List<KeyValuePair<string, string>>();
        }
        catch (UnauthorizedAccessException)
        {
            return new List<KeyValuePair<string, string>>();
        }
    }

    /// <summary>Zeilen <c>key:0 "Text"</c> und <c>key: "Text"</c>; der Text reicht bis zum letzten <c>"</c>, <c>\"</c> darin wird zu <c>"</c>.</summary>
    internal static List<KeyValuePair<string, string>> Parse(string yml)
    {
        var entries = new List<KeyValuePair<string, string>>();
        foreach (var raw in yml.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].TrimEnd();
            if (key.Length == 0 || key.Any(char.IsWhiteSpace))
            {
                continue;
            }

            // Version (":0") und Leerraum bis zum Anführungszeichen überspringen; die Kopfzeile "l_english:" hat keinen Text.
            var open = colon + 1;
            while (open < line.Length && (char.IsDigit(line[open]) || char.IsWhiteSpace(line[open])))
            {
                open++;
            }

            if (open >= line.Length || line[open] != '"')
            {
                continue;
            }

            var close = line.LastIndexOf('"');
            if (close <= open)
            {
                continue;
            }

            entries.Add(new KeyValuePair<string, string>(key, line.Substring(open + 1, close - open - 1).Replace("\\\"", "\"")));
        }

        return entries;
    }
}
