using System.Text.RegularExpressions;

namespace ParadoxModTool.Patches;

/// <summary>Texte einer Sprache aus Vanilla (<c>localization/&lt;sprache&gt;/**/*_l_&lt;sprache&gt;.yml</c>); bei doppelten Schlüsseln gilt der erste.</summary>
public sealed class LocalizationIndex
{
    private static readonly Regex Header = new(@"^\s*l_(?<lang>[a-z_]+):\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex FileSuffix = new(@"_l_(?<lang>[a-z_]+)\.yml$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly Dictionary<string, string> _values;

    private LocalizationIndex(Dictionary<string, string> values) => _values = values;

    public static LocalizationIndex LoadVanilla(string vanillaRoot, string language)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var folder in ParadoxModTool.GameProfiles.LocalizationFolders.Existing(vanillaRoot).Select(f => Path.Combine(f, language)).Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*_l_" + language + ".yml", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var line in LocalizationLines.All(TextFile.Read(path).Text))
                {
                    values.TryAdd(line.Key, line.Value);
                }
            }
        }

        return new LocalizationIndex(values);
    }

    public string? Value(string key) => _values.TryGetValue(key, out var value) ? value : null;

    /// <summary>Sprache einer Lokalisierungsdatei: Kopfzeile <c>l_&lt;sprache&gt;:</c> (erste Zeile, die kein Kommentar ist), sonst Dateiname <c>…_l_&lt;sprache&gt;.yml</c>.</summary>
    public static string? LanguageOf(string text, string relativePath)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var header = Header.Match(line);
            if (header.Success)
            {
                return header.Groups["lang"].Value;
            }

            break;
        }

        var suffix = FileSuffix.Match(relativePath);
        return suffix.Success ? suffix.Groups["lang"].Value.ToLowerInvariant() : null;
    }
}
