using System.Text.Json;
using System.Text.Json.Serialization;
using ParadoxModTool.Mods;

namespace ParadoxModTool.Templates;

/// <summary>Frühere feste Ignorier-Einstellungen. Wird nur noch zum Lesen alter Vorlagen benutzt (siehe <see cref="SavedTemplate.MigrateLegacyRules"/>); sie sind jetzt mitgelieferte Regeln (<see cref="ShippedRules"/>).</summary>
[Flags]
public enum IgnoreRules
{
    None = 0,
    Textures = 1,
    Buttons = 2,
    Position = 4,
    Size = 8,
    All = Textures | Buttons | Position | Size,
}

/// <summary>Eine gespeicherte Vorlage: Snippet, vom Nutzer geänderte Zählpunkte und die Lokalisierungstabelle.</summary>
public sealed class SavedTemplate
{
    /// <summary>Format der Vorlage. 1: feste Ignorier-Einstellungen; 2: Zählregeln (<see cref="Rules"/>). Alte Vorlagen werden beim Laden umgesetzt.</summary>
    public int Version { get; set; } = 2;

    public string Snippet { get; set; } = string.Empty;

    public int Count { get; set; } = 3;

    public bool CollapseExamples { get; set; } = true;

    /// <summary>Prüfmodus (Liste der Zählpunkte, Markierungen im Snippet) war an.</summary>
    public bool ReviewMode { get; set; }

    /// <summary>Welche Zahl zählt bei Bezeichnern mit mehreren Zahlen.</summary>
    public MultiNumberRule MultiNumbers { get; set; } = MultiNumberRule.First;

    /// <summary>Nur zum Lesen alter Vorlagen (Version 1); siehe <see cref="MigrateLegacyRules"/>. Wird nicht mehr geschrieben.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IgnoreRules? Ignore { get; set; }

    /// <summary>Nur zum Lesen alter Vorlagen (Version 1).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IgnoreNamePrefixes { get; set; }

    /// <summary>Nur zum Lesen alter Vorlagen (Version 1).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? NamePrefixes { get; set; }

    /// <summary>Zählregeln der Ebene "Vorlage" (gelten nur für diese Vorlage).</summary>
    public List<CountingRule> Rules { get; set; } = new();

    /// <summary>Kennungen von Regeln anderer Ebenen (auch mitgelieferter), die für diese Vorlage abgeschaltet sind.</summary>
    public List<string> DisabledRuleIds { get; set; } = new();

    /// <summary>Prüfliste: Stellen an ignorierten Orten (texture, position …) ausblenden.</summary>
    public bool HideIgnored { get; set; } = true;

    /// <summary>Prüfliste: einzelne Zahlen (<c>cost = 10</c>) ausblenden.</summary>
    public bool HideSingleNumbers { get; set; }

    /// <summary>Prüfliste: Zahlen in Texten ausblenden.</summary>
    public bool HideTexts { get; set; }

    /// <summary>Prüfliste: durch die Regel für mehrere Zahlen abgeschaltete Stellen (Stufen, Namenszahlen) ausblenden.</summary>
    public bool HideRuleDisabled { get; set; }

    /// <summary>Prüfliste: 0 = alle, 1 = nur aktive, 2 = nur ausgeschaltete, 3 = nur geänderte.</summary>
    public int ShowMode { get; set; }

    /// <summary>Im Prüfmodus die eingesetzten Zahlen im Ergebnis farbig markieren.</summary>
    public bool MarkChanges { get; set; } = true;

    /// <summary>
    /// Eigene Auswahl je Form eines Bezeichners: welche Zahlen (Nummern ab 0, von links) zählen. Formen ohne Eintrag folgen der allgemeinen Regel.
    /// </summary>
    public Dictionary<string, List<int>> ShapeNumbers { get; set; } = new();

    /// <summary>Nur zum Lesen alter Vorlagen; siehe <see cref="MigrateLegacyRules"/>.</summary>
    public Dictionary<string, TokenRule> ShapeRules { get; set; } = new();

    /// <summary>
    /// Rechnet die frühere Regelauswahl je Form in angehakte Zahlen um und setzt bei Version 1 die alten Ignorier-Einstellungen in Zählregeln um
    /// (abgeschaltete mitgelieferte Regeln, eine Regel für eine eigene Präfixliste).
    /// </summary>
    public void MigrateLegacyRules()
    {
        Localization?.MigrateLegacy();
        MigrateIgnoreSettings();
        foreach (var (shape, rule) in ShapeRules)
        {
            if (ShapeNumbers.ContainsKey(shape))
            {
                continue;
            }

            var runs = shape.Count(c => c == '#');
            ShapeNumbers[shape] = rule switch
            {
                TokenRule.First => new List<int> { 0 },
                TokenRule.Second => new List<int> { 1 },
                TokenRule.Last => new List<int> { Math.Max(0, runs - 1) },
                TokenRule.All => Enumerable.Range(0, runs).ToList(),
                _ => new List<int>(),
            };
        }

        ShapeRules.Clear();
    }

    private void MigrateIgnoreSettings()
    {
        if (Version >= 2)
        {
            return;
        }

        void Disable(params string[] ids)
        {
            foreach (var id in ids.Where(id => !DisabledRuleIds.Contains(id)))
            {
                DisabledRuleIds.Add(id);
            }
        }

        var ignore = Ignore ?? IgnoreRules.All;
        if (!ignore.HasFlag(IgnoreRules.Textures))
        {
            Disable(ShippedRules.TextureFileId, ShippedRules.TextureEntryId);
        }

        if (!ignore.HasFlag(IgnoreRules.Buttons))
        {
            Disable(ShippedRules.ButtonId);
        }

        if (!ignore.HasFlag(IgnoreRules.Position))
        {
            Disable(ShippedRules.PositionId);
        }

        if (!ignore.HasFlag(IgnoreRules.Size))
        {
            Disable(ShippedRules.SizeId);
        }

        var prefixes = (NamePrefixes ?? new List<string> { "iv" }).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (IgnoreNamePrefixes == false)
        {
            Disable(ShippedRules.NamePrefixId);
        }
        else if (!prefixes.SequenceEqual(new[] { "iv" }, StringComparer.OrdinalIgnoreCase))
        {
            Disable(ShippedRules.NamePrefixId);
            if (prefixes.Count > 0)
            {
                var rule = CountingRule.Create("Namenspräfix (aus alter Vorlage)");
                rule.Action = RuleAction.MarkedDoesNotCount;
                rule.Patterns = prefixes.SelectMany(p => new[] { p + "[#]*", p + "_[#]*" }).ToList();
                Rules.Add(rule);
            }
        }

        Ignore = null;
        IgnoreNamePrefixes = null;
        NamePrefixes = null;
        Version = 2;
    }

    public List<PointOverride> PointOverrides { get; set; } = new();

    public LocalizationSettings Localization { get; set; } = new();
}

/// <summary>
/// Vorlagen als JSON-Dateien auf zwei Ebenen: Spiel (<c>games\&lt;id&gt;\templates</c>, dort liegen auch schon vorhandene Vorlagen) und Mod (Ordner <c>templates</c> im Datenordner der Mod).
/// Der Name <see cref="LastName"/> hält den zuletzt bearbeiteten Stand des Spiels und erscheint nicht in den Listen.
/// </summary>
public sealed class TemplateStore
{
    public const string LastName = "_last";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly LayeredStore<SavedTemplate> _layers;

    /// <param name="gameDirectory">Ordner der Spielebene.</param>
    /// <param name="modDirectory">Ordner der Modebene; <see langword="null"/>, solange keine Mod gewählt ist.</param>
    public TemplateStore(string gameDirectory, string? modDirectory = null)
    {
        ModDirectory = modDirectory;
        _layers = new LayeredStore<SavedTemplate>(
            layer => layer == StoreLayer.Game ? gameDirectory : ModDirectory,
            Options,
            template => template.MigrateLegacyRules(),
            "Vorlage");
    }

    /// <summary>Ordner der Modebene; <see langword="null"/> heißt: keine Mod gewählt, die Modebene ist leer und nicht beschreibbar.</summary>
    public string? ModDirectory { get; set; }

    /// <summary>Namen der Vorlagen einer Ebene, nach Name geordnet.</summary>
    public List<string> List(StoreLayer layer = StoreLayer.Game) => _layers.List(layer);

    /// <summary>Alle Vorlagen beider Ebenen; ein Name in beiden Ebenen erscheint zweimal.</summary>
    public IReadOnlyList<LayeredEntry> ListEntries() => _layers.List();

    public SavedTemplate? Load(string name, StoreLayer layer = StoreLayer.Game) => _layers.Load(name, layer);

    /// <summary>Die Vorlage mit diesem Namen aus der speziellsten Ebene, die sie hat (Mod vor Spiel).</summary>
    public (SavedTemplate Value, StoreLayer Layer)? Resolve(string name) => _layers.Resolve(name);

    public void Save(string name, SavedTemplate template, StoreLayer layer = StoreLayer.Game) => _layers.Save(name, layer, template);

    public void Delete(string name, StoreLayer layer = StoreLayer.Game) => _layers.Delete(name, layer);

    /// <summary>Ersetzt Zeichen, die in Dateinamen nicht erlaubt sind; leere Namen werden zu "Vorlage".</summary>
    public static string SafeName(string name) => StoreNames.Safe(name, "Vorlage");

    /// <summary>Was der Nutzer gegenüber der Standard-Analyse geändert hat; nur das wird gespeichert, damit neue Erkennungen bei geändertem Snippet greifen.</summary>
    public static List<PointOverride> DiffOverrides(TemplateModel defaults, TemplateModel current)
    {
        var result = new List<PointOverride>();
        foreach (var point in current.Points)
        {
            var baseline = defaults.Points.FirstOrDefault(p => p.Key == point.Key);
            if (baseline is null)
            {
                continue;
            }

            var changed = point.Enabled != baseline.Enabled || point.Start != baseline.Start || point.Step != baseline.Step || point.InnerStep != baseline.InnerStep;
            if (changed)
            {
                result.Add(new PointOverride(
                    point.Key,
                    point.Enabled != baseline.Enabled ? point.Enabled : null,
                    point.Start != baseline.Start ? point.Start : null,
                    point.Step != baseline.Step ? point.Step : null,
                    point.InnerStep != baseline.InnerStep ? point.InnerStep : null));
            }
        }

        return result;
    }
}
