using System.Globalization;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>Eingabefeld für eine klassische Eigenschaft (HOI IV, EU IV).</summary>
public enum ClassicInput
{
    Text,
    Number,
    Bool,
    Anchor,
    Format,
    Pair,
    Sprite,
    Font,
    LocalisedText,
    Frame,
    Block,
}

/// <summary>Schlüssel der beiden Werte eines Paar-Blocks, wie sie geschrieben werden (<c>x</c>/<c>y</c> oder <c>width</c>/<c>height</c>).</summary>
public readonly record struct ClassicPairKeys(string First, string Second);

/// <summary>
/// Bearbeiten einer klassischen Eigenschaft: welches Eingabefeld passt (nach Namen, dann Katalog, dann aktuellem Wert), welcher Code aus der Eingabe
/// entsteht und welche Schlüssel ein Paar-Block bekommt. Namen gelten ohne Rücksicht auf Groß-/Kleinschreibung.
/// </summary>
public static class ClassicPropertyInput
{
    private static readonly HashSet<string> PairNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "position", "show_position", "hide_position", "size", "slotsize", "bordersize", "max_slots", "pdx_tooltip_anchor",
    };

    private static readonly HashSet<string> LocalisedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "buttontext", "pdx_tooltip", "pdx_tooltip_delayed", "tooltip", "tooltiptext", "delayedtooltiptext",
    };

    private static readonly HashSet<string> BlockNames = new(StringComparer.OrdinalIgnoreCase) { "margin", "if_resolution" };

    // Die Namen aus Spec "Lage", in der Reihenfolge von ClassicAnchors.
    private static readonly string[] Anchors =
    {
        "UPPER_LEFT", "UPPER_RIGHT", "LOWER_LEFT", "LOWER_RIGHT", "CENTER_UP", "CENTER_DOWN", "CENTER_LEFT", "CENTER_RIGHT", "CENTER",
    };

    // Werte, die ClassicText.HorizontalFormat kennt; ob sie gelten, entscheidet diese Methode, nicht die Liste.
    private static readonly string[] FormatCandidates =
    {
        "left", "centre", "right", "center", "upper_left", "upper_right", "lower_left", "lower_right", "up", "down",
    };

    public static IReadOnlyList<string> AnchorOptions => Anchors;

    /// <summary>Ein echter Ankername; die nur in Vanilla gesehenen Kurzformen (<c>left</c>, …) und Tippfehler gelten nicht.</summary>
    public static bool IsValidAnchor(string value) => ClassicAnchors.TryParse(value, out _, out _, out var isAlias) && !isAlias;

    /// <summary>Werte, die die Vorschau waagerecht deuten kann; die im Katalog häufigsten zuerst, dann die übrigen.</summary>
    public static IReadOnlyList<string> FormatOptions(GuiPropertyCatalog catalog, string widgetKey)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        if (catalog.Lookup(widgetKey, "format") is { } stats)
        {
            foreach (var (word, count) in stats.Words)
            {
                var cleaned = ClassicAnchors.Clean(word).ToLowerInvariant();
                if (IsValidFormat(cleaned))
                {
                    seen[cleaned] = seen.GetValueOrDefault(cleaned) + count;
                }
            }
        }

        var frequent = seen.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key);
        return frequent.Concat(FormatCandidates.Where(c => IsValidFormat(c) && !seen.ContainsKey(c))).ToList();
    }

    /// <summary>Wie die Vorschau liest: ohne Anführungszeichen und <c>;</c>, klein geschrieben, und ein Wert, den <see cref="ClassicText.HorizontalFormat"/> kennt.</summary>
    public static bool IsValidFormat(string value)
    {
        ClassicText.HorizontalFormat(ClassicAnchors.Clean(value).ToLowerInvariant(), out _, out var unknown);
        return !unknown;
    }

    public static ClassicInput InputFor(string name, ResolvedProperty? current, GuiPropertyCatalog catalog, string widgetKey)
    {
        var lower = name.ToLowerInvariant();
        switch (lower)
        {
            case "orientation" or "origo":
                return ClassicInput.Anchor;
            case "format":
                return ClassicInput.Format;
            case "spritetype" or "quadtexturesprite":
                return ClassicInput.Sprite;
            case "font" or "buttonfont":
                return ClassicInput.Font;
            case "frame":
                return ClassicInput.Frame;
        }

        if (LocalisedNames.Contains(lower))
        {
            return ClassicInput.LocalisedText;
        }

        if (PairNames.Contains(lower) && current?.Role is null or GuiRole.StructuredBlock or GuiRole.ValueBlock)
        {
            return current is null || IsTwoScalarBlock(current.Origin.Node) ? ClassicInput.Pair : ClassicInput.Block;
        }

        if (BlockNames.Contains(lower) || current?.Role is GuiRole.StructuredBlock or GuiRole.ValueBlock)
        {
            return ClassicInput.Block;
        }

        if (current is { Role: GuiRole.Property } && current.Value.ToLowerInvariant() is "yes" or "no")
        {
            return ClassicInput.Bool;
        }

        if (catalog.Lookup(widgetKey, name) is { Forms.Count: > 0 } stats)
        {
            return stats.DominantForm switch
            {
                GuiValueForm.Bool => ClassicInput.Bool,
                GuiValueForm.Number or GuiValueForm.Percent => ClassicInput.Number,
                GuiValueForm.Pair or GuiValueForm.Block or GuiValueForm.List => ClassicInput.Block,
                _ => ClassicInput.Text,
            };
        }

        // Ohne Katalogangaben ist 0/1 der Schalter (wie yes/no).
        return current is { Role: GuiRole.Property } && current.Value is "0" or "1" ? ClassicInput.Bool : ClassicInput.Text;
    }

    /// <summary>
    /// Code für einen Skalarwert; null bei ungültiger Eingabe. Die Anführungszeichen des bisherigen Werts bleiben (<paramref name="wasQuoted"/>), eigene in
    /// der Eingabe gelten, eine neue Eigenschaft bekommt sie, wenn Vanilla sie überwiegend setzt.
    /// </summary>
    public static string? ScalarText(string name, string input, bool? wasQuoted, GuiPropertyCatalog catalog, string widgetKey)
    {
        var value = input.Trim();
        var quoted = wasQuoted;
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
            quoted = true;
        }

        if (value.Trim().Length == 0 || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return null;
        }

        quoted ??= catalog.Lookup(widgetKey, name) is { Count: > 0 } stats && stats.Quoted * 2 > stats.Count;
        return quoted == true ? $"\"{value}\"" : IsWord(value) ? value : null;
    }

    /// <summary>Schlüssel für einen Paar-Block: ein vorhandener behält seine; ein neuer <c>size</c>/<c>slotsize</c> nimmt die häufigsten des Katalogs, alles andere <c>x</c>/<c>y</c>.</summary>
    public static ClassicPairKeys PairKeysFor(string name, GuiNode? existingBlock, GuiPropertyCatalog catalog, string widgetKey)
    {
        if (existingBlock is not null && Scalars(existingBlock) is { Count: >= 2 } inner)
        {
            return new ClassicPairKeys(inner[0].Name, inner[1].Name);
        }

        if (name.Equals("size", StringComparison.OrdinalIgnoreCase) || name.Equals("slotsize", StringComparison.OrdinalIgnoreCase))
        {
            var dominant = catalog.Lookup(widgetKey, name)?.PairKeys
                .OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => p.Key.Split(','))
                .FirstOrDefault(parts => parts.Length == 2);
            if (dominant is not null)
            {
                return new ClassicPairKeys(dominant[0], dominant[1]);
            }
        }

        return new ClassicPairKeys("x", "y");
    }

    /// <summary>Text der beiden Werte eines Paar-Blocks, wie geschrieben (Konstanten bleiben stehen); fehlt einer, ist er leer.</summary>
    public static (string First, string Second) ReadPair(GuiNode block)
    {
        var inner = Scalars(block);
        return (inner.Count > 0 ? inner[0].Value ?? string.Empty : string.Empty, inner.Count > 1 ? inner[1].Value ?? string.Empty : string.Empty);
    }

    /// <summary>Zahl, <c>N%</c>, <c>N%%</c> (Spec "Lage": <c>%%</c> wird gemessen) oder <c>@name</c>.</summary>
    public static bool IsPairComponent(string value)
    {
        if (value.Length > 1 && value[0] == '@')
        {
            return true;
        }

        return IsNumber(value.EndsWith("%%", StringComparison.Ordinal) ? value[..^2] : value.EndsWith('%') ? value[..^1] : value);
    }

    /// <summary>Schaltet Pixel → Prozent → <c>%%</c> → Pixel; Konstanten und alles Fremde bleiben.</summary>
    public static string CyclePercent(string component)
    {
        if (component.EndsWith("%%", StringComparison.Ordinal))
        {
            return IsNumber(component[..^2]) ? component[..^2] : component;
        }

        if (component.EndsWith('%'))
        {
            return IsNumber(component[..^1]) ? component + "%" : component;
        }

        return IsNumber(component) ? component + "%" : component;
    }

    /// <summary>Schreibweise, die Vanilla am häufigsten benutzt; ohne Katalogeintrag der Name wie übergeben.</summary>
    public static string SpellingFor(string name, GuiPropertyCatalog catalog, string widgetKey) =>
        catalog.Lookup(widgetKey, name)?.PreferredSpelling ?? name;

    /// <summary>"Eigenschaft hinzufügen": Eigenschaften des Widgets nach Häufigkeit in üblicher Schreibweise, ohne die hier schon gesetzten (ohne Rücksicht auf Groß-/Kleinschreibung).</summary>
    public static IReadOnlyList<string> AddableProperties(ResolvedElement element, GuiPropertyCatalog catalog)
    {
        var here = element.Properties.Where(p => p.Origin.Kind == GuiOriginKind.Here).Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return catalog.PropertiesOf(element.BaseWidget).Where(s => !here.Contains(s.Name)).Select(s => s.PreferredSpelling).ToList();
    }

    private static List<GuiNode> Scalars(GuiNode block) => block.Children.Where(c => c.Kind == GuiNodeKind.Property).ToList();

    /// <summary>Genau zwei Werte (<c>x = 1 y = 2</c>) und sonst nichts außer Kommentaren.</summary>
    private static bool IsTwoScalarBlock(GuiNode node) =>
        node.Kind == GuiNodeKind.KeyedBlock && node.Children.Where(c => c.Kind != GuiNodeKind.Comment).Count() == 2 && Scalars(node).Count == 2;

    // double.TryParse nähme auch NaN und Infinity; hier zählen nur Ziffern mit Vorzeichen und Dezimalpunkt.
    private static bool IsNumber(string value) =>
        value.Any(char.IsAsciiDigit)
        && value.TrimStart('-', '+').All(c => char.IsAsciiDigit(c) || c == '.')
        && double.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);

    /// <summary>Ein Wort ohne Leerraum und Syntaxzeichen (sonst zerfiele der Wert beim Neu-Parsen).</summary>
    private static bool IsWord(string value) => !value.Any(c => char.IsWhiteSpace(c) || c is '{' or '}' or '=' or '#' or '"');
}
