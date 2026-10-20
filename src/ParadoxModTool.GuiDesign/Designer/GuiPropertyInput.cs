using System.Globalization;
using ParadoxModTool.GuiDesign.Catalog;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Eingabefeld für einen Wert im Eigenschaftenfeld.</summary>
public enum GuiValueEditor
{
    Text,
    Number,
    Bool,
    Enum,
    Pair,
    Color,
    Texture,
    Binding,
    Block,
}

/// <summary>Farbe wie in Vanilla geschrieben: RGBA mit Werten von 0 bis 1 (Plan-Ruling 12).</summary>
public readonly record struct GuiColor(double R, double G, double B, double A);

/// <summary>Wohin ein Wert geschrieben wird: in genau die Zeile, aus der er stammt (<c>InPlace</c>), oder als neue Zeile am gewählten Element.</summary>
public sealed record GuiPropertyTarget(GuiNode Node, bool InPlace);

/// <summary>
/// Bearbeiten einer Eigenschaft: welches Eingabefeld passt (nach Katalog, sonst nach dem aktuellen Wert), welcher Text im Feld steht, welcher
/// Code daraus entsteht und wohin er geschrieben wird (Plan-Ruling 4).
/// </summary>
public static class GuiPropertyInput
{
    public static GuiValueEditor EditorFor(string name, string baseWidget, GuiPropertyCatalog catalog, ResolvedProperty? current)
    {
        if (current?.Role == GuiRole.StructuredBlock)
        {
            return GuiValueEditor.Block;
        }

        if (catalog.Lookup(baseWidget, name) is { } stats)
        {
            if (stats.IsEnum)
            {
                return GuiValueEditor.Enum;
            }

            if (stats.Forms.Count > 0)
            {
                return FromForm(stats.DominantForm);
            }
        }

        if (current is null)
        {
            return GuiValueEditor.Text;
        }

        return FromForm(current.Role == GuiRole.ValueBlock
            ? GuiValueClassifier.ClassifyItems(current.Items)
            : GuiValueClassifier.ClassifyScalar(current.Value, current.IsQuoted));
    }

    /// <summary>
    /// Kann das Feld einen Datenausdruck (<c>[…]</c>) aufnehmen? Ja, wenn der Wert schon einer ist oder Vanilla bei dieser Eigenschaft Datenausdrücke
    /// benutzt, auch wenn sonst meist <c>yes</c>/<c>no</c> dasteht (<c>visible</c>, <c>enabled</c>). Dann bietet der Designer den GUI-Daten-Helfer an.
    /// </summary>
    public static bool AcceptsDataExpression(string name, string baseWidget, GuiPropertyCatalog catalog, string input) =>
        input.Contains('[') || catalog.Lookup(baseWidget, name)?.Forms.ContainsKey(GuiValueForm.Binding) == true;

    /// <summary>In Vanilla gesehene Werte, die häufigsten zuerst.</summary>
    public static IReadOnlyList<string> EnumOptions(string name, string baseWidget, GuiPropertyCatalog catalog) =>
        catalog.Lookup(baseWidget, name) is { } stats
            ? stats.Words.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key).ToList()
            : Array.Empty<string>();

    /// <summary>Text im Eingabefeld: wie geschrieben (Konstanten bleiben stehen), ohne Anführungszeichen; Wertblöcke als Einträge mit Leerzeichen.</summary>
    public static string InputOf(ResolvedProperty? property)
    {
        if (property is null)
        {
            return string.Empty;
        }

        var node = property.Origin.Node;
        return node.Kind switch
        {
            GuiNodeKind.Property when node.Name == property.Name => node.Value ?? string.Empty,
            GuiNodeKind.KeyedBlock when node.Name == property.Name && property.Role == GuiRole.ValueBlock =>
                string.Join(" ", node.Children.Where(c => c.Kind == GuiNodeKind.ListItem).Select(c => c.Value)),
            _ => property.Role == GuiRole.ValueBlock ? string.Join(" ", property.Items) : property.Value,
        };
    }

    /// <summary>Code für die Eingabe; null bei ungültiger Eingabe. Texte, Datenbindungen und Texturpfade stehen in Anführungszeichen.</summary>
    public static string? ValueText(GuiValueEditor editor, string input, bool wasQuoted)
    {
        var value = input.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
            wasQuoted = true;
        }

        if (value.Contains('"'))
        {
            return null;
        }

        switch (editor)
        {
            case GuiValueEditor.Text or GuiValueEditor.Binding or GuiValueEditor.Texture:
                return $"\"{value}\"";

            case GuiValueEditor.Block:
                return null;

            case GuiValueEditor.Pair:
                var pair = Items(value);
                return pair.Length == 2 && pair.All(IsPairComponent) ? $"{{ {pair[0]} {pair[1]} }}" : null;

            case GuiValueEditor.Color:
                if (value.Contains('['))
                {
                    return $"\"{value}\"";
                }

                var parts = Items(value);
                return parts.Length is 3 or 4 && parts.All(IsWord) ? $"{{ {string.Join(" ", parts)} }}" : null;

            default:
                // Ein Datenausdruck ([GetVariableSystem.HasValue( 'a', 'b' )]) darf in jedem Feld stehen, auch mit Leerzeichen; er steht immer in Anführungszeichen.
                if (value.StartsWith('[') && value.EndsWith(']'))
                {
                    return $"\"{value}\"";
                }

                if (value.Length == 0 || !IsWord(value))
                {
                    return null;
                }

                return wasQuoted ? $"\"{value}\"" : value;
        }
    }

    /// <summary>Zahlenpaar aus dem Eingabetext (mit oder ohne Klammern); fehlt die zweite Zahl, ist sie leer.</summary>
    public static (string First, string Second) SplitPair(string input)
    {
        var items = Items(input);
        return items.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (items[0], string.Empty),
            _ => (items[0], items[1]),
        };
    }

    /// <summary>Schaltet einen Pixelwert auf Prozent um und zurück; Konstanten bleiben.</summary>
    public static string TogglePercent(string component) =>
        component.EndsWith('%') ? component[..^1]
        : IsNumber(component) ? component + "%"
        : component;

    public static bool TryParseColor(string input, out GuiColor color)
    {
        color = default;
        var parts = Items(input);
        if (parts.Length is not (3 or 4))
        {
            return false;
        }

        var values = new double[] { 0, 0, 0, 1 };
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 1)
            {
                return false;
            }

            values[i] = value;
        }

        color = new GuiColor(values[0], values[1], values[2], values[3]);
        return true;
    }

    public static string FormatColor(GuiColor color) =>
        $"{{ {Component(color.R)} {Component(color.G)} {Component(color.B)} {Component(color.A)} }}";

    public static GuiColor FromBytes(byte r, byte g, byte b, double alpha) => new(r / 255.0, g / 255.0, b / 255.0, alpha);

    public static (byte R, byte G, byte B) ToBytes(GuiColor color) => (ToByte(color.R), ToByte(color.G), ToByte(color.B));

    /// <summary>Nächster <c>datacontext</c> am Element oder darüber (für Vorschläge zu Datenbindungen).</summary>
    public static string? DataContextOf(GuiNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Children.LastOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name == "datacontext")?.Value is { Length: > 0 } context)
            {
                return context;
            }
        }

        return null;
    }

    /// <summary>"Eigenschaft hinzufügen": Eigenschaften des Basis-Widgets nach Häufigkeit, ohne die hier schon gesetzten.</summary>
    public static IReadOnlyList<string> AddableProperties(ResolvedElement element, GuiPropertyCatalog catalog)
    {
        var here = element.Properties.Where(p => p.Origin.Kind == GuiOriginKind.Here).Select(p => p.Name).ToHashSet(catalog.Classic ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        return catalog.PropertiesOf(element.BaseWidget).Where(s => !here.Contains(s.Name)).Select(s => catalog.Classic ? s.PreferredSpelling : s.Name).ToList();
    }

    /// <summary>
    /// Die Zeile, aus der der Wert stammt, wenn sie in dieser Datei im gewählten Element steht (auch in einem blockoverride darin); sonst das
    /// gewählte Element (neue Zeile, die den geerbten Wert überschreibt).
    /// </summary>
    public static GuiPropertyTarget TargetFor(ResolvedProperty property, GuiNode selected)
    {
        var node = property.Origin.Node;
        return node.Kind is GuiNodeKind.Property or GuiNodeKind.KeyedBlock && node.Ancestors().Any(a => ReferenceEquals(a, selected))
            ? new GuiPropertyTarget(node, true)
            : new GuiPropertyTarget(selected, false);
    }

    /// <summary>Texturpfad (<c>gfx/…</c>, mit "/") relativ zum ersten Kontextordner, der die Datei enthält; null außerhalb.</summary>
    public static string? TexturePathFor(string fullPath, IEnumerable<string> contextRoots)
    {
        var full = Path.GetFullPath(fullPath);
        foreach (var root in contextRoots)
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(root), full);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            {
                return relative.Replace('\\', '/');
            }
        }

        return null;
    }

    private static GuiValueEditor FromForm(GuiValueForm form) => form switch
    {
        GuiValueForm.Number or GuiValueForm.Percent => GuiValueEditor.Number,
        GuiValueForm.Bool => GuiValueEditor.Bool,
        GuiValueForm.Pair => GuiValueEditor.Pair,
        GuiValueForm.Color => GuiValueEditor.Color,
        GuiValueForm.TexturePath => GuiValueEditor.Texture,
        GuiValueForm.Binding => GuiValueEditor.Binding,
        GuiValueForm.Block => GuiValueEditor.Block,
        _ => GuiValueEditor.Text,
    };

    private static string[] Items(string input) =>
        input.Trim().TrimStart('{').TrimEnd('}').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static bool IsNumber(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static bool IsPairComponent(string value) =>
        IsNumber(value) || (value.EndsWith('%') && IsNumber(value[..^1])) || (value.Length > 1 && value[0] == '@');

    /// <summary>Ein Wort ohne Leerraum und Syntaxzeichen (sonst würde der Wert beim Neu-Parsen zerfallen).</summary>
    private static bool IsWord(string value) => value.Length > 0 && !value.Any(c => char.IsWhiteSpace(c) || c is '{' or '}' or '=' or '#' or '"');

    private static string Component(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
