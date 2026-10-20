using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Modifiers;

/// <summary>
/// Modifier-Name → Anzeigeformat, gebaut aus dem Profilabschnitt <see cref="GameProfile.ModifierFormats"/>: bei "definitions" aus den
/// Definitionsdateien von Spiel und Mod (die Mod überschreibt gleiche Namen), bei "wiki" aus dem Wiki-Typ der Script-Datenbank.
/// </summary>
public sealed class ModifierFormatIndex
{
    private readonly Dictionary<string, ModifierFormat> _formats;
    private readonly Dictionary<string, ModifierTraits> _traits;

    private ModifierFormatIndex(Dictionary<string, ModifierFormat> formats, Dictionary<string, ModifierTraits>? traits = null)
    {
        _formats = formats;
        _traits = traits ?? new Dictionary<string, ModifierTraits>(StringComparer.OrdinalIgnoreCase);
        AllNames = _formats.Keys.Union(_traits.Keys, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static ModifierFormatIndex Empty { get; } = new(new Dictionary<string, ModifierFormat>(StringComparer.OrdinalIgnoreCase));

    /// <summary>Eigenschaften (ja/nein, schädlich, Kategorie) aller Modifier aus den Definitionsdateien, auch der Boolean-Modifier ohne Anzeigeformat.</summary>
    public IReadOnlyDictionary<string, ModifierTraits> Traits => _traits;

    /// <summary>Alle bekannten Modifier: die mit Anzeigeformat und die nur aus <see cref="Traits"/> (z. B. Boolean-Modifier).</summary>
    public IReadOnlyCollection<string> AllNames { get; }

    public int Count => _formats.Count;

    /// <summary>Alle Modifier-Namen des Index (z.B. für die Auswahl in GUI-Bausteinen).</summary>
    public IReadOnlyCollection<string> Names => _formats.Keys;

    public ModifierFormat? Find(string name) => _formats.GetValueOrDefault(name);

    /// <param name="settings">Profilabschnitt; <see langword="null"/> = leerer Index.</param>
    /// <param name="gameRoots">Spielinhalt-Ordner (z. B. <c>…/game</c>), zuerst gelesen.</param>
    /// <param name="modRoots">Mod-Ordner, danach gelesen.</param>
    /// <param name="wikiModifiers">Modifier-Name und Wiki-Typ ("Multiplicative", "Additive", …) für <c>source = wiki</c>; bei <c>source = suffix</c> alle Modifier der Script-Datenbank mit ihrer Kategorie.</param>
    /// <param name="docDecimals">Bei <c>source = suffix</c>: Nachkommastellen je Modifier aus der Doku des Spiels (Hearts of Iron IV: "Number with N decimal places").</param>
    public static ModifierFormatIndex Build(
        ModifierFormatSettings? settings,
        IEnumerable<string> gameRoots,
        IEnumerable<string> modRoots,
        IEnumerable<(string Name, string EffectType)> wikiModifiers,
        IReadOnlyDictionary<string, int>? docDecimals = null)
    {
        var formats = new Dictionary<string, ModifierFormat>(StringComparer.OrdinalIgnoreCase);
        var traits = new Dictionary<string, ModifierTraits>(StringComparer.OrdinalIgnoreCase);
        switch (settings?.Source)
        {
            case "definitions":
                foreach (var root in gameRoots.Concat(modRoots))
                {
                    foreach (var folder in settings.Folders)
                    {
                        ReadFolder(Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar)), settings, formats, traits);
                    }
                }

                break;
            case "wiki":
                foreach (var (name, effectType) in wikiModifiers)
                {
                    var percent = effectType.Trim() switch
                    {
                        "Multiplicative" => true,
                        "Additive" => false,
                        _ => (bool?)null,
                    };
                    if (percent is { } isPercent)
                    {
                        formats[name] = new ModifierFormat(isPercent, false, settings.DefaultDecimals, 1, false, settings.TrimTrailingZeros, Estimated: true);
                    }
                }

                break;
            case "suffix":
                // Stellaris hat keine Definitionsdateien; die Endung verrät das Format meist (_mult Prozent, _add Zahl), sicher ist es nicht.
                var suffixes = settings.Suffixes.OrderByDescending(s => s.Key.Length).ToList();
                foreach (var (name, category) in wikiModifiers)
                {
                    // Jeder Modifier zählt (Namen, Suche), auch ohne Format; die Kategorie stammt aus modifiers.log.
                    traits[name] = new ModifierTraits(false, false, category.Trim());
                    var match = suffixes.FirstOrDefault(s => s.Key.Length > 0 && name.EndsWith(s.Key, StringComparison.OrdinalIgnoreCase));
                    var percent = match.Value?.Trim() switch
                    {
                        "percent" => true,
                        "number" => false,
                        _ => (bool?)null,
                    };
                    if (percent is { } isPercent)
                    {
                        var decimals = docDecimals is not null && docDecimals.TryGetValue(name, out var fromDocs) ? fromDocs : settings.DefaultDecimals;
                        formats[name] = new ModifierFormat(isPercent, false, decimals, 1, false, settings.TrimTrailingZeros, Estimated: true);
                    }
                }

                // Hearts of Iron IV: eigene Modifier in common/modifier_definitions (value_type, precision) sind sicher und gehen vor die Schätzung.
                foreach (var root in gameRoots.Concat(modRoots))
                {
                    foreach (var folder in settings.Folders)
                    {
                        ReadFolder(Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar)), settings, formats, traits);
                    }
                }

                break;
        }

        return new ModifierFormatIndex(formats, traits);
    }

    private static void ReadFolder(string folder, ModifierFormatSettings settings, Dictionary<string, ModifierFormat> formats, Dictionary<string, ModifierTraits> traits)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(folder, "*.txt").OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
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

            foreach (var (name, entry) in ModifierDefinitionReader.ReadTraits(text))
            {
                traits[name] = entry;
            }

            foreach (var (name, definition) in ModifierDefinitionReader.Read(text))
            {
                var factor = definition.Format is { } format && settings.Formats.TryGetValue(format, out var entry) ? entry.Factor : 1;
                formats[name] = new ModifierFormat(
                    definition.Percent,
                    definition.AlreadyPercent,
                    definition.Decimals ?? settings.DefaultDecimals,
                    factor,
                    definition.NoDifferenceSign,
                    settings.TrimTrailingZeros,
                    Estimated: false);
            }
        }
    }
}
