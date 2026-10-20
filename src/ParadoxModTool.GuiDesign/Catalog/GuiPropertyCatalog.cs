using System.Globalization;
using System.Text.Json.Serialization;

namespace ParadoxModTool.GuiDesign.Catalog;

public enum GuiValueForm
{
    Number,
    Percent,
    Bool,
    Pair,
    Color,
    TexturePath,
    Binding,
    Text,
    Word,
    Block,
    List,
}

public static class GuiValueClassifier
{
    public static GuiValueForm ClassifyScalar(string value, bool quoted)
    {
        if (quoted)
        {
            return value.Contains('[') ? GuiValueForm.Binding : IsTexture(value) ? GuiValueForm.TexturePath : GuiValueForm.Text;
        }

        if (value is "yes" or "no")
        {
            return GuiValueForm.Bool;
        }

        if (value.EndsWith('%') && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return GuiValueForm.Percent;
        }

        if (value.StartsWith('@') || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return GuiValueForm.Number;
        }

        if (IsTexture(value))
        {
            return GuiValueForm.TexturePath;
        }

        return value.Contains('[') ? GuiValueForm.Binding : GuiValueForm.Word;
    }

    public static GuiValueForm ClassifyItems(IReadOnlyList<string> items)
    {
        var numeric = items.Count > 0 && items.All(i => ClassifyScalar(i, false) is GuiValueForm.Number or GuiValueForm.Percent);
        return numeric switch
        {
            true when items.Count == 2 => GuiValueForm.Pair,
            true when items.Count is 3 or 4 => GuiValueForm.Color,
            _ => GuiValueForm.List,
        };
    }

    private static bool IsTexture(string value) =>
        value.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".tga", StringComparison.OrdinalIgnoreCase)
        || value.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
}

public sealed class GuiPropertyStats
{
    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public Dictionary<GuiValueForm, int> Forms { get; set; } = new();

    /// <summary>Gesehene Wörter (unquotierte Skalare, die keine Zahl sind), begrenzt auf die ersten 60 verschiedenen.</summary>
    public Dictionary<string, int> Words { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Schlüssel, wie er geschrieben steht (<c>Orientation</c>, <c>orientation</c>) → Anzahl. Klassisch ist <see cref="Name"/> der gefaltete Schlüssel.</summary>
    public Dictionary<string, int> Spellings { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Strukturierte Blöcke mit genau zwei Skalaren: deren Schlüssel klein, mit Komma verbunden (<c>width,height</c>, <c>x,y</c>) → Anzahl.</summary>
    public Dictionary<string, int> PairKeys { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Wie viele Skalarwerte in Anführungszeichen standen.</summary>
    public int Quoted { get; set; }

    /// <summary>Häufigste Schreibweise; bei Gleichstand die ordinal erste, ohne Angaben <see cref="Name"/>.</summary>
    [JsonIgnore]
    public string PreferredSpelling => GuiSpellings.Preferred(Spellings, Name);

    [JsonIgnore]
    public GuiValueForm DominantForm => Forms.Count == 0 ? GuiValueForm.Word : Forms.MaxBy(pair => pair.Value).Key;

    /// <summary>Auswahlliste sinnvoll: überwiegend Wörter, mindestens 5 Verwendungen, höchstens 40 verschiedene Werte.</summary>
    [JsonIgnore]
    public bool IsEnum => DominantForm == GuiValueForm.Word && Count >= 5 && Words.Count is > 0 and <= 40;
}

public sealed class GuiWidgetStats
{
    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }

    public Dictionary<string, GuiPropertyStats> Properties { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Elementschlüssel, wie er geschrieben steht (<c>gridBoxType</c>) → Anzahl. Klassisch ist <see cref="Name"/> der gefaltete Schlüssel.</summary>
    public Dictionary<string, int> Spellings { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Häufigste Schreibweise; bei Gleichstand die ordinal erste, ohne Angaben <see cref="Name"/>.</summary>
    [JsonIgnore]
    public string PreferredSpelling => GuiSpellings.Preferred(Spellings, Name);
}

internal static class GuiSpellings
{
    public static string Preferred(Dictionary<string, int> spellings, string fallback) =>
        spellings.Count == 0
            ? fallback
            : spellings.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;
}

/// <summary>Ein Vanilla-Ausdruck als Beispiel (<c>[Country.GetCapital.GetName]</c>) mit relativem Dateipfad.</summary>
public sealed record GuiExample(string Expression, string File);

/// <summary>
/// Aus Vanilla gelernt: welche Eigenschaften je Engine-Widget vorkommen (gezählt nach Auflösung auf das Basis-Widget), in welcher
/// Wertform, und welche Datenbindungen unter welchem <c>datacontext</c> stehen. Wird als JSON zwischengespeichert.
/// </summary>
public sealed class GuiPropertyCatalog
{
    /// <summary>
    /// 2: Beispiele je Datentyp-Eintrag und Elementtypen von Listen (GUI-Daten-Helfer).
    /// 3: Schreibweisen, Paar-Schlüssel und Anführungszeichen je Eigenschaft; klassisch mit gefalteten Schlüsseln.
    /// </summary>
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// Klassisch gelernt (HOI IV, EU IV): Elemente und Eigenschaften stehen unter dem kleingeschriebenen Schlüssel (<see cref="ClassicKey"/>),
    /// die Wörterbücher sind ordinal, und beim Laden aus JSON geht jeder Vergleich verloren.
    /// </summary>
    public bool Classic { get; set; }

    /// <summary>Schlüssel für Widgets und Eigenschaften eines klassischen Katalogs.</summary>
    public static string ClassicKey(string name) => name.ToLowerInvariant();

    public string Fingerprint { get; set; } = string.Empty;

    public Dictionary<string, GuiWidgetStats> Widgets { get; set; } = new(StringComparer.Ordinal);

    /// <summary>datacontext-Wert ("" = ohne) → Ausdruck → Anzahl.</summary>
    public Dictionary<string, Dictionary<string, int>> Bindings { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Vanilla-Beispiele je Datentyp-Eintrag: Schlüssel ist der erste Bezeichner einer Kette (<c>Country</c>), die ersten beiden (<c>Country.GetCapital</c>)
    /// oder der Name einer globalen Funktion (<c>Not</c>); höchstens fünf, die häufigsten zuerst.
    /// </summary>
    public Dictionary<string, List<GuiExample>> Examples { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Elementtyp der Einträge einer Liste: <c>datamodel</c>-Ausdruck ohne Klammern (<c>Province.GetLocations</c>) → Typ (<c>Location</c>).</summary>
    public Dictionary<string, string> ListItemTypes { get; set; } = new(StringComparer.Ordinal);

    public IReadOnlyList<GuiExample> ExamplesFor(string key) =>
        Examples.TryGetValue(key, out var examples) ? examples : Array.Empty<GuiExample>();

    public string? ItemTypeOf(string datamodelExpression) =>
        ListItemTypes.GetValueOrDefault(DataTypes.DataExpression.Strip(datamodelExpression));

    public bool IsKnownEngineWidget(string name) => Widgets.ContainsKey(Classic ? ClassicKey(name) : name);

    public IReadOnlyList<GuiPropertyStats> PropertiesFor(string baseWidget) =>
        Widgets.TryGetValue(baseWidget, out var widget)
            ? widget.Properties.Values.OrderByDescending(p => p.Count).ThenBy(p => p.Name, StringComparer.Ordinal).ToList()
            : Array.Empty<GuiPropertyStats>();

    public GuiPropertyStats? Property(string baseWidget, string name) =>
        Widgets.TryGetValue(baseWidget, out var widget) && widget.Properties.TryGetValue(name, out var stats) ? stats : null;

    /// <summary>Widget wie im Dokument geschrieben; klassisch wird der Schlüssel vorher gefaltet (<see cref="ClassicKey"/>).</summary>
    public GuiWidgetStats? LookupWidget(string widget) =>
        Widgets.GetValueOrDefault(Classic ? ClassicKey(widget) : widget);

    /// <summary>Eigenschaft eines Widgets wie im Dokument geschrieben; klassisch werden Widget und Name vorher gefaltet.</summary>
    public GuiPropertyStats? Lookup(string widget, string name) =>
        LookupWidget(widget) is { } stats && stats.Properties.TryGetValue(Classic ? ClassicKey(name) : name, out var property) ? property : null;

    /// <summary>Eigenschaften eines Widgets wie im Dokument geschrieben (klassisch gefaltet), die häufigsten zuerst.</summary>
    public IReadOnlyList<GuiPropertyStats> PropertiesOf(string widget) =>
        PropertiesFor(Classic ? ClassicKey(widget) : widget);

    /// <summary>Vorschläge: zuerst Ausdrücke unter genau diesem datacontext, dann die ohne datacontext, je nach Häufigkeit.</summary>
    public IReadOnlyList<string> SuggestBindings(string? dataContext, int max = 50)
    {
        var result = new List<string>();
        foreach (var key in new[] { dataContext ?? string.Empty, string.Empty }.Distinct())
        {
            if (!Bindings.TryGetValue(key, out var expressions))
            {
                continue;
            }

            foreach (var expression in expressions.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key))
            {
                if (!result.Contains(expression))
                {
                    result.Add(expression);
                }
            }
        }

        return result.Take(max).ToList();
    }
}
