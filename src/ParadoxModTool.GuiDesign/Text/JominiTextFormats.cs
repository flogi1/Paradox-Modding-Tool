using System.Globalization;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using ParadoxModTool.GuiDesign.Resolve;

namespace ParadoxModTool.GuiDesign.Text;

/// <summary>Farbe mit Komponenten von 0 bis 1.</summary>
public readonly record struct JominiColor(double R, double G, double B, double A)
{
    public static JominiColor White => new(1, 1, 1, 1);
}

/// <summary>Wirksame Textdarstellung. <c>Font</c> null = Standardschrift des Fensters.</summary>
public sealed record JominiTextStyle(string? Font, JominiFontStyle Style, double Size, JominiColor Color, bool Underline, bool Glow, bool Shadow);

/// <summary>
/// Ein <c>iconsize</c>-Block eines <c>texticon</c>; Breite, Höhe und Versatz in Pixeln. <c>FontSize</c> ist die <c>fontsize</c> des Blocks (null ohne
/// Angabe); der Textsatz skaliert die Größe damit auf die Textgröße. <c>SizeGiven</c> = false: der Block hat gar kein <c>size</c> (Victoria 3), Breite und
/// Höhe sind dann 0 und der Textsatz nimmt die Größe der Textur.
/// </summary>
public sealed record JominiTextIcon(
    string Name,
    string? Texture,
    double Width,
    double Height,
    double OffsetX,
    double OffsetY,
    double? FontSize = null,
    bool SizeGiven = true);

/// <summary>
/// Die Textformate (<c>textformatting.gui</c>) und Texticons (<c>texticon</c>) eines Spiels. Aus dem GUI-Index gelesen, Reihenfolge
/// Vanilla, Abhängigkeiten, Mod; ein späterer Eintrag gleichen Namens ersetzt den früheren.
/// </summary>
public sealed class JominiTextFormats
{
    private const int MaxDepth = 8;

    public static readonly JominiTextFormats Empty = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, List<(double? FontSize, JominiTextIcon Icon)>>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, string> _formats;
    private readonly Dictionary<string, List<(double? FontSize, JominiTextIcon Icon)>> _icons;

    private JominiTextFormats(
        Dictionary<string, string> formats,
        Dictionary<string, List<(double? FontSize, JominiTextIcon Icon)>> icons)
    {
        _formats = formats;
        _icons = icons;
    }

    public static JominiTextFormats From(GuiIndex index)
    {
        var formats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var icons = new Dictionary<string, List<(double? FontSize, JominiTextIcon Icon)>>(StringComparer.OrdinalIgnoreCase);

        // OrderBy ist stabil: innerhalb einer Ebene bleibt die Ladereihenfolge des Index erhalten.
        foreach (var file in index.Files.OrderBy(f => f.Layer))
        {
            foreach (var top in index.DocumentOf(file).Root.Children)
            {
                if (top.Kind != GuiNodeKind.KeyedBlock)
                {
                    continue;
                }

                if (top.Name.Equals("textformatting", StringComparison.OrdinalIgnoreCase))
                {
                    ReadFormats(top, formats);
                }
                else if (top.Name.Equals("texticon", StringComparison.OrdinalIgnoreCase))
                {
                    ReadIcon(top, icons);
                }
            }
        }

        return new JominiTextFormats(formats, icons);
    }

    private static void ReadFormats(GuiNode block, Dictionary<string, string> formats)
    {
        foreach (var format in block.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name.Equals("format", StringComparison.OrdinalIgnoreCase)))
        {
            var name = ValueOf(format, "name");
            var spec = ValueOf(format, "format");
            if (!string.IsNullOrEmpty(name) && spec is not null)
            {
                formats[name] = spec;
            }
        }
    }

    private static void ReadIcon(GuiNode block, Dictionary<string, List<(double? FontSize, JominiTextIcon Icon)>> icons)
    {
        var name = ValueOf(block, "icon");
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        // Ein späteres texticon gleichen Namens ersetzt das ganze frühere.
        var sizes = new List<(double? FontSize, JominiTextIcon Icon)>();
        foreach (var iconSize in block.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name.Equals("iconsize", StringComparison.OrdinalIgnoreCase)))
        {
            var sizeGiven = iconSize.Children.Any(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name.Equals("size", StringComparison.OrdinalIgnoreCase));
            var size = NumbersOf(iconSize, "size");
            var offset = NumbersOf(iconSize, "offset");
            var fontSizeNode = iconSize.Children.LastOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name.Equals("fontsize", StringComparison.OrdinalIgnoreCase));
            var fontSize = fontSizeNode is null ? null : ConstantNumber(fontSizeNode.Value, fontSizeNode);
            sizes.Add((fontSize, new JominiTextIcon(
                name,
                ValueOf(iconSize, "texture"),
                size.Count > 0 ? size[0] : 0,
                size.Count > 1 ? size[1] : 0,
                offset.Count > 0 ? offset[0] : 0,
                offset.Count > 1 ? offset[1] : 0,
                fontSize,
                sizeGiven)));
        }

        icons[name] = sizes;
    }

    private static string? ValueOf(GuiNode block, string key) =>
        block.Children.LastOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

    private static List<double> NumbersOf(GuiNode block, string key)
    {
        var list = block.Children.LastOrDefault(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (list is null)
        {
            return new List<double>();
        }

        return list.Children
            .Where(c => c.Kind == GuiNodeKind.ListItem)
            .Select(c => ConstantNumber(c.Value, c))
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .ToList();
    }

    private static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Zahl mit aufgelösten Dateikonstanten (<c>@name</c>, <c>@[Ausdruck]</c>) wie im Resolver; EU V schreibt die meisten <c>iconsize</c>-Werte so
    /// (<c>@font_icon_default_size</c>). Null, wenn sie fehlt oder nicht auswertbar ist.
    /// </summary>
    private static double? ConstantNumber(string? text, GuiNode at) =>
        text is null ? null : Number(GuiResolver.SubstituteValue(text, at));

    /// <summary>
    /// Wendet eine Formatangabe wie <c>medium;bold;size:18</c> auf <paramref name="style"/> an. Schlüsselwörter sind nicht von der Groß-/Kleinschreibung
    /// abhängig; jedes andere Wort ist ein Formatname und wird rekursiv aufgelöst (Tiefe bis 8; Zyklen und unbekannte Namen ändern nichts).
    /// <paramref name="scale"/>: eigenes <c>scale</c> des Textelements; eine feste Größe (<c>size:N</c>) wird dann wie die Grundgröße in floor(N × scale)
    /// gesetzt (<see cref="ScaledSize"/>), damit das spätere Teilen durch scale sie wieder auf N bringt.
    /// </summary>
    public JominiTextStyle Apply(JominiTextStyle style, string spec, double scale = 1)
    {
        var tooDeep = false;
        return Apply(style, spec, 0, ref tooDeep, scale);
    }

    /// <summary>Schriftgröße unter eigenem <c>scale</c>: ganzzahlig abgerundet, mindestens 1 (15 × 0,5 → 7, Lauf 6); ohne scale (und für Werte ≤ 0) unverändert.</summary>
    internal static double ScaledSize(double fontSize, double scale) =>
        scale == 1 || !(fontSize > 0) ? fontSize : Math.Max(1, Math.Floor(fontSize * scale + 1e-9));

    private JominiTextStyle Apply(JominiTextStyle style, string spec, int depth, ref bool tooDeep, double scale)
    {
        foreach (var rawPart in spec.Split(';'))
        {
            // Zyklus erkannt: sofort abbrechen, sonst wächst der Aufwand mit N^8 bei N Selbstverweisen.
            if (tooDeep)
            {
                return style;
            }

            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                continue;
            }

            var colon = part.IndexOf(':');
            if (colon < 0)
            {
                style = ApplyWord(style, part, depth, ref tooDeep, scale);
                continue;
            }

            var key = part[..colon].Trim();
            var value = part[(colon + 1)..].Trim();
            if (key.Equals("color", StringComparison.OrdinalIgnoreCase))
            {
                style = style with { Color = ParseColor(value) ?? style.Color };
            }
            else if (key.Equals("glow_color", StringComparison.OrdinalIgnoreCase))
            {
                // Die übrigen glow_*-Angaben ändern die Darstellung nicht und werden ignoriert.
                style = style with { Glow = true };
            }
            else if (key.Equals("font", StringComparison.OrdinalIgnoreCase))
            {
                style = value.Length > 0 ? style with { Font = value } : style;
            }
            else if (key.Equals("size", StringComparison.OrdinalIgnoreCase))
            {
                // Nur endliche, positive Größen (schlechte Mod-Daten wie size:-10 oder NaN würden negative Breiten setzen).
                style = Number(value) is { } size && double.IsFinite(size) && size > 0 ? style with { Size = ScaledSize(size, scale) } : style;
            }
        }

        return style;
    }

    private JominiTextStyle ApplyWord(JominiTextStyle style, string word, int depth, ref bool tooDeep, double scale)
    {
        if (word.Equals("bold", StringComparison.OrdinalIgnoreCase))
        {
            return style with { Style = style.Style | JominiFontStyle.Bold };
        }

        if (word.Equals("italic", StringComparison.OrdinalIgnoreCase))
        {
            return style with { Style = style.Style | JominiFontStyle.Italic };
        }

        if (word.Equals("underline", StringComparison.OrdinalIgnoreCase))
        {
            return style with { Underline = true };
        }

        if (word.Equals("shadow", StringComparison.OrdinalIgnoreCase))
        {
            return style with { Shadow = true };
        }

        if (!_formats.TryGetValue(word, out var nested))
        {
            return style;
        }

        if (depth >= MaxDepth)
        {
            tooDeep = true;
            return style;
        }

        // Ein Zyklus läuft in die Tiefengrenze; dann zählt der ganze Formatname als wirkungslos.
        var nestedTooDeep = false;
        var result = Apply(style, nested, depth + 1, ref nestedTooDeep, scale);
        if (nestedTooDeep)
        {
            tooDeep = true;
            return style;
        }

        return result;
    }

    private static JominiColor? ParseColor(string text)
    {
        var inner = text.Trim().TrimStart('{').TrimEnd('}');
        var numbers = inner.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(Number).ToList();
        if (numbers.Count < 3 || numbers.Any(n => !n.HasValue))
        {
            return null;
        }

        // Wie fontcolor: ist eine Komponente über 1, gilt das ganze Tupel als 0–255.
        var scale = numbers.Any(n => n!.Value > 1) ? 255.0 : 1.0;
        return new JominiColor(numbers[0]!.Value / scale, numbers[1]!.Value / scale, numbers[2]!.Value / scale, numbers.Count > 3 ? numbers[3]!.Value / scale : 1);
    }

    /// <summary>
    /// Der <c>iconsize</c>-Block mit der größten <c>fontsize</c> bis <paramref name="fontSize"/>, sonst der mit der kleinsten, sonst der ohne
    /// <c>fontsize</c>. Null, wenn es das Icon nicht gibt.
    /// </summary>
    public JominiTextIcon? Icon(string name, double fontSize)
    {
        if (!_icons.TryGetValue(name, out var sizes) || sizes.Count == 0)
        {
            return null;
        }

        var sized = sizes.Where(s => s.FontSize.HasValue).ToList();
        if (sized.Count == 0)
        {
            return sizes[0].Icon;
        }

        var fitting = sized.Where(s => s.FontSize!.Value <= fontSize).ToList();
        return (fitting.Count > 0 ? fitting.MaxBy(s => s.FontSize!.Value) : sized.MinBy(s => s.FontSize!.Value)).Icon;
    }
}
