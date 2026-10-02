using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Modifiers;

/// <summary>
/// Modifier-Name → Anzeigeformat, gebaut aus dem Profilabschnitt <see cref="GameProfile.ModifierFormats"/>: bei "definitions" aus den
/// Definitionsdateien von Spiel und Mod (die Mod überschreibt gleiche Namen), bei "wiki" aus dem Wiki-Typ der Script-Datenbank.
/// </summary>
public sealed class ModifierFormatIndex
{
    private readonly Dictionary<string, ModifierFormat> _formats;

    private ModifierFormatIndex(Dictionary<string, ModifierFormat> formats) => _formats = formats;

    public static ModifierFormatIndex Empty { get; } = new(new Dictionary<string, ModifierFormat>(StringComparer.OrdinalIgnoreCase));

    public int Count => _formats.Count;

    /// <summary>Alle Modifier-Namen des Index (z.B. für die Auswahl in GUI-Bausteinen).</summary>
    public IReadOnlyCollection<string> Names => _formats.Keys;

    public ModifierFormat? Find(string name) => _formats.GetValueOrDefault(name);

    /// <param name="settings">Profilabschnitt; <see langword="null"/> = leerer Index.</param>
    /// <param name="gameRoots">Spielinhalt-Ordner (z. B. <c>…/game</c>), zuerst gelesen.</param>
    /// <param name="modRoots">Mod-Ordner, danach gelesen.</param>
    /// <param name="wikiModifiers">Modifier-Name und Wiki-Typ ("Multiplicative", "Additive", …) für <c>source = wiki</c>.</param>
    public static ModifierFormatIndex Build(
        ModifierFormatSettings? settings,
        IEnumerable<string> gameRoots,
        IEnumerable<string> modRoots,
        IEnumerable<(string Name, string EffectType)> wikiModifiers)
    {
        var formats = new Dictionary<string, ModifierFormat>(StringComparer.OrdinalIgnoreCase);
        switch (settings?.Source)
        {
            case "definitions":
                foreach (var root in gameRoots.Concat(modRoots))
                {
                    foreach (var folder in settings.Folders)
                    {
                        ReadFolder(Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar)), settings, formats);
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
        }

        return new ModifierFormatIndex(formats);
    }

    private static void ReadFolder(string folder, ModifierFormatSettings settings, Dictionary<string, ModifierFormat> formats)
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
