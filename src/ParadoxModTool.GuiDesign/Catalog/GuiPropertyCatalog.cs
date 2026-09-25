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
}

/// <summary>
/// Aus Vanilla gelernt: welche Eigenschaften je Engine-Widget vorkommen (gezählt nach Auflösung auf das Basis-Widget), in welcher
/// Wertform, und welche Datenbindungen unter welchem <c>datacontext</c> stehen. Wird als JSON zwischengespeichert.
/// </summary>
public sealed class GuiPropertyCatalog
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string Fingerprint { get; set; } = string.Empty;

    public Dictionary<string, GuiWidgetStats> Widgets { get; set; } = new(StringComparer.Ordinal);

    /// <summary>datacontext-Wert ("" = ohne) → Ausdruck → Anzahl.</summary>
    public Dictionary<string, Dictionary<string, int>> Bindings { get; set; } = new(StringComparer.Ordinal);

    public bool IsKnownEngineWidget(string name) => Widgets.ContainsKey(name);

    public IReadOnlyList<GuiPropertyStats> PropertiesFor(string baseWidget) =>
        Widgets.TryGetValue(baseWidget, out var widget)
            ? widget.Properties.Values.OrderByDescending(p => p.Count).ThenBy(p => p.Name, StringComparer.Ordinal).ToList()
            : Array.Empty<GuiPropertyStats>();

    public GuiPropertyStats? Property(string baseWidget, string name) =>
        Widgets.TryGetValue(baseWidget, out var widget) && widget.Properties.TryGetValue(name, out var stats) ? stats : null;

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
