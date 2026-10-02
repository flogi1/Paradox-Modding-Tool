using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Modifiers;

/// <summary>
/// Modifier-Name → übersetzter Anzeigename aus den Lokalisierungsdateien von Spiel und Mod (Profil: <see cref="ModifierFormatSettings.NameLocFolders"/> und
/// <see cref="ModifierFormatSettings.NameKeys"/>). Gelesen werden nur die Zeilen, deren Schlüssel zu einem bekannten Modifier gehört, und in einem zweiten Lauf die
/// Begriffe und Verweise darin (<see cref="ModifierFormatSettings.NameRefKeys"/>), damit "[levy|E]-Größe" in der Sprache des Spiels erscheint. Die Reihenfolge der
/// Sprachen zählt zuerst (Oberflächensprache vor Englisch), danach die Reihenfolge der Schlüsselmuster; die Mod überschreibt das Spiel.
/// </summary>
public sealed class ModifierNameIndex
{
    private const int MaxReferenceRounds = 4;

    private readonly Dictionary<string, string> _names;

    private ModifierNameIndex(Dictionary<string, string> names) => _names = names;

    public static ModifierNameIndex Empty { get; } = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    public int Count => _names.Count;

    public string? Find(string modifierName) => _names.GetValueOrDefault(modifierName);

    /// <summary>
    /// Die Sprachordner bzw. Dateinamen-Endungen der Spiele für eine Oberflächensprache von PMT, in Rangfolge: erst die Sprache selbst, dann Englisch
    /// (<c>de</c> → <c>german</c>, <c>english</c>). Unbekannte Sprachen bekommen nur Englisch.
    /// </summary>
    public static IReadOnlyList<string> GameLanguages(string? uiLanguage)
    {
        var own = uiLanguage?.Trim().ToLowerInvariant() switch
        {
            "de" => "german",
            "fr" => "french",
            "es" => "spanish",
            "pl" => "polish",
            "ru" => "russian",
            "pt-br" => "braz_por",
            "zh-hans" => "simp_chinese",
            _ => null,
        };
        return own is null ? new[] { "english" } : new[] { own, "english" };
    }

    /// <param name="settings">Profilabschnitt; ohne Ordner oder Schlüsselmuster ist der Index leer.</param>
    /// <param name="modifierNames">Die Modifier, für die Namen gesucht werden.</param>
    /// <param name="gameRoots">Spielinhalt-Ordner, zuerst gelesen.</param>
    /// <param name="modRoots">Mod-Ordner, danach gelesen.</param>
    /// <param name="languages">Spielsprachen in Rangfolge, z. B. <c>german</c>, <c>english</c>.</param>
    public static ModifierNameIndex Build(
        ModifierFormatSettings? settings,
        IEnumerable<string> modifierNames,
        IEnumerable<string> gameRoots,
        IEnumerable<string> modRoots,
        IReadOnlyList<string> languages)
    {
        if (settings is null || settings.NameLocFolders.Count == 0 || settings.NameKeys.Count == 0 || languages.Count == 0)
        {
            return Empty;
        }

        var names = modifierNames.ToList();
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            foreach (var pattern in settings.NameKeys)
            {
                wanted.Add(Fill(pattern, "{name}", name));
            }
        }

        var game = gameRoots.ToList();
        var mod = modRoots.ToList();
        var texts = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var language in languages.Distinct(StringComparer.Ordinal))
        {
            var found = Scan(settings, game, mod, language, wanted);

            // Die Texte verweisen auf Begriffe ([levy|E]) und andere Schlüssel ($MOD_X_ADD$), die ihrerseits verweisen können: so lange nachlesen, bis nichts Neues kommt.
            var asked = new HashSet<string>(wanted, StringComparer.OrdinalIgnoreCase);
            for (var round = 0; round < MaxReferenceRounds; round++)
            {
                var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in found.Values)
                {
                    foreach (var key in ModifierNameText.ReferencedKeys(raw).Concat(ModifierNameText.ReferencedConcepts(raw).SelectMany(id => settings.NameRefKeys.Select(pattern => Fill(pattern, "{id}", id)))))
                    {
                        if (asked.Add(key))
                        {
                            next.Add(key);
                        }
                    }
                }

                if (next.Count == 0)
                {
                    break;
                }

                foreach (var (key, value) in Scan(settings, game, mod, language, next))
                {
                    found.TryAdd(key, value);
                }
            }

            texts[language] = found;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (Resolve(name, settings, languages, texts) is { } display)
            {
                result[name] = display;
            }
        }

        return new ModifierNameIndex(result);
    }

    private static string Fill(string pattern, string placeholder, string value) => pattern.Replace(placeholder, value, StringComparison.Ordinal);

    private static string? Resolve(
        string name,
        ModifierFormatSettings settings,
        IReadOnlyList<string> languages,
        Dictionary<string, Dictionary<string, string>> texts)
    {
        foreach (var language in languages)
        {
            var known = texts[language];
            string? Concept(string id) =>
                settings.NameRefKeys.Select(pattern => known.GetValueOrDefault(Fill(pattern, "{id}", id))).FirstOrDefault(text => text is not null);

            string? Reference(string key) => known.GetValueOrDefault(key);

            foreach (var pattern in settings.NameKeys)
            {
                if (known.TryGetValue(Fill(pattern, "{name}", name), out var raw) && ModifierNameText.Clean(raw, Concept, Reference) is { } clean)
                {
                    return clean;
                }
            }
        }

        return null;
    }

    /// <summary>Alle Zeilen der Lokalisierung in dieser Sprache, deren Schlüssel in <paramref name="wanted"/> steht (Spiel zuerst, die erste Definition gilt; die Mod überschreibt).</summary>
    private static Dictionary<string, string> Scan(ModifierFormatSettings settings, List<string> game, List<string> mod, string language, HashSet<string> wanted)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (roots, overwrite) in new[] { (game, false), (mod, true) })
        {
            foreach (var root in roots)
            {
                foreach (var folder in settings.NameLocFolders)
                {
                    ReadFolder(Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar)), language, wanted, found, overwrite);
                }
            }
        }

        return found;
    }

    private static void ReadFolder(string folder, string language, HashSet<string> wanted, Dictionary<string, string> found, bool overwrite)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(folder, "*_l_" + language + ".yml", options).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
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

            foreach (var line in text.Split('\n'))
            {
                var colon = line.IndexOf(':');
                if (colon < 1)
                {
                    continue;
                }

                var key = line[..colon].Trim();
                if (!wanted.Contains(key))
                {
                    continue;
                }

                var open = line.IndexOf('"', colon);
                var close = line.LastIndexOf('"');
                if (open < 0 || close <= open)
                {
                    continue;
                }

                var value = line[(open + 1)..close];
                if (overwrite)
                {
                    found[key] = value;
                }
                else
                {
                    found.TryAdd(key, value);
                }
            }
        }
    }
}
