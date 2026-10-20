using System.Text.RegularExpressions;
using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Generators.Lookups;

/// <summary>
/// Lokalisierung einer Spielsprache als flache Tabelle (Spec 2026-10-05-generator-packages-lookups, Abschnitt 1): alle <c>*.yml</c> unter
/// <c>main_menu/localization/&lt;sprache&gt;/</c> und <c>in_game/localization/&lt;sprache&gt;/</c> der Spielordner, ein späterer Ordner überschreibt.
/// <c>$schlüssel$</c> wird durch den Text des Schlüssels ersetzt, höchstens vier Runden tief.
/// </summary>
public sealed class GameLocalization
{
    private const int MaxRounds = 4;
    // Leerer Teil: flache Spiele (Hearts of Iron IV, EU IV) mit localisation/ direkt im Spielordner.
    private static readonly string[] Parts = { "main_menu", "in_game", "" };
    private static readonly Regex Reference = new(@"\$(?<key>[A-Za-z0-9_.\-]+)\$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, string> _texts;

    private GameLocalization(Dictionary<string, string> texts) => _texts = texts;

    public bool IsEmpty => _texts.Count == 0;

    /// <param name="language">Ordnername des Spiels, z. B. <c>english</c>, <c>german</c>.</param>
    public static GameLocalization Load(IReadOnlyList<string> gameRoots, string language)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in gameRoots)
        {
            foreach (var folder in Parts.SelectMany(part => LocalizationFolders.Existing(Path.Combine(root, part))).Select(f => Path.Combine(f, language)))
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(folder, "*.yml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    string text;
                    try
                    {
                        text = File.ReadAllText(file);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    foreach (var (key, value) in YmlReader.Values(text))
                    {
                        texts[key] = YmlReader.Unescape(value);
                    }
                }
            }
        }

        return new GameLocalization(texts);
    }

    /// <summary>Text des Schlüssels mit aufgelösten Verweisen; <see langword="null"/>, wenn es den Schlüssel nicht gibt.</summary>
    public string? Text(string key)
    {
        if (!_texts.TryGetValue(key, out var text))
        {
            return null;
        }

        for (var round = 0; round < MaxRounds && text.Contains('$'); round++)
        {
            var resolved = Reference.Replace(text, m => _texts.TryGetValue(m.Groups["key"].Value, out var target) ? target : m.Value);
            if (resolved == text)
            {
                break;
            }

            text = resolved;
        }

        return text;
    }

    /// <summary>Beschreibung: <c>&lt;key&gt;_desc</c>, sonst <c>desc_&lt;key&gt;</c>.</summary>
    public string? Desc(string key) => Text(key + "_desc") ?? Text("desc_" + key);
}
