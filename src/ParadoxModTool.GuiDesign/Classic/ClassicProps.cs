using System.Globalization;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Eigenschaften klassischer Elemente ohne Rücksicht auf Groß-/Kleinschreibung (<c>Orientation</c>/<c>orientation</c>). Steht ein Schlüssel mehrfach da,
/// gilt der letzte. Strukturierte Blöcke (<c>position</c>, <c>size</c>, <c>if_resolution</c> …) liest man über ihren Knoten, weil der Resolver ihren Inhalt
/// als Rohtext lässt; Konstanten darin setzen die Knoten-Zugriffe hier ein.
/// </summary>
public static class ClassicProps
{
    /// <summary>Skalarwert (Konstanten schon vom Resolver eingesetzt) oder <see langword="null"/>.</summary>
    public static string? Scalar(ResolvedElement e, string name) =>
        e.Properties.LastOrDefault(p => p.Role != GuiRole.StructuredBlock && Is(p.Name, name))?.Value;

    /// <summary>Knoten des letzten strukturierten Blocks mit diesem Namen.</summary>
    public static GuiNode? Block(ResolvedElement e, string name) =>
        e.Properties.LastOrDefault(p => p.Role == GuiRole.StructuredBlock && Is(p.Name, name))?.Origin.Node;

    /// <summary>Alle strukturierten Blöcke mit diesem Namen in Textreihenfolge (z.B. mehrere <c>if_resolution</c>).</summary>
    public static IEnumerable<GuiNode> Blocks(ResolvedElement e, string name) =>
        e.Properties.Where(p => p.Role == GuiRole.StructuredBlock && Is(p.Name, name)).Select(p => p.Origin.Node);

    /// <summary>
    /// Wertepaar eines Blocks, je Wert mit zwei möglichen Schlüsseln (<c>size</c>: <c>width</c>/<c>x</c> und <c>height</c>/<c>y</c>).
    /// <see langword="null"/>, wenn der Block fehlt.
    /// </summary>
    public static (string? A, string? B)? Pair(ResolvedElement e, string block, string a1, string a2, string b1, string b2) =>
        Block(e, block) is { } node ? Pair(node, a1, a2, b1, b2) : null;

    /// <summary>Skalarwert in einem Block; eine <c>@konstante</c> wird eingesetzt, wenn es sie gibt, sonst bleibt der Rohtext.</summary>
    public static string? Scalar(GuiNode block, string name)
    {
        var node = block.Children.LastOrDefault(c => c.Kind == GuiNodeKind.Property && Is(c.Name, name));
        if (node?.Value is not { } value)
        {
            return null;
        }

        return node.IsQuoted ? value : GuiResolver.SubstituteConstant(value, node) ?? value;
    }

    /// <summary>Letzter Unterblock mit diesem Namen (z.B. <c>min</c> in <c>size</c>).</summary>
    public static GuiNode? Block(GuiNode block, string name) =>
        block.Children.LastOrDefault(c => c.Kind == GuiNodeKind.KeyedBlock && Is(c.Name, name));

    public static (string? A, string? B) Pair(GuiNode block, string a1, string a2, string b1, string b2) =>
        (Scalar(block, a1) ?? Scalar(block, a2), Scalar(block, b1) ?? Scalar(block, b2));

    /// <summary>Die Bedingungen eines <c>if_resolution</c>-Blocks (EU IV); jede nennt eine Grenze der Bildschirmgröße.</summary>
    internal static readonly string[] ResolutionConditions = { "min_height", "max_height", "min_width", "max_width" };

    /// <summary>
    /// Die <c>if_resolution</c>-Blöcke des Elements, deren Bedingungen zur Bildschirmgröße passen, in Textreihenfolge. Vorschau und Eigenschaftenfeld
    /// lesen beide hieraus; der letzte passende Block gewinnt je Schlüssel.
    /// </summary>
    public static IReadOnlyList<ClassicResolutionMatch> MatchingIfResolution(ResolvedElement e, double screenWidth, double screenHeight)
    {
        var matches = new List<ClassicResolutionMatch>();
        foreach (var block in Blocks(e, "if_resolution"))
        {
            var parts = new List<string>();
            var matching = true;
            foreach (var condition in ResolutionConditions)
            {
                if (Scalar(block, condition) is not { } raw)
                {
                    continue;
                }

                parts.Add(condition + " = " + raw);
                var limit = Number(raw) ?? double.NaN;
                var actual = condition.EndsWith("height", StringComparison.Ordinal) ? screenHeight : screenWidth;
                matching &= condition.StartsWith("min", StringComparison.Ordinal) ? actual >= limit : actual <= limit;
            }

            if (matching)
            {
                matches.Add(new ClassicResolutionMatch(block, string.Join(", ", parts)));
            }
        }

        return matches;
    }

    /// <summary>Zahl (invariant) aus einem Wert; Anführungszeichen und angehängtes <c>;</c> fallen weg.</summary>
    internal static double? Number(string? text) =>
        double.TryParse(ClassicAnchors.Clean(text), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;

    internal static bool IsYes(string? value) => ClassicAnchors.Clean(value) is var v && (v.Equals("yes", StringComparison.OrdinalIgnoreCase) || v == "1");

    internal static bool IsNo(string? value) => ClassicAnchors.Clean(value) is var v && (v.Equals("no", StringComparison.OrdinalIgnoreCase) || v == "0");

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Ein <c>if_resolution</c>-Block, dessen Bedingungen zur Bildschirmgröße passen, samt Bedingung als Text (<c>min_height = 868</c>).</summary>
public sealed record ClassicResolutionMatch(GuiNode Block, string Condition);

/// <summary>
/// Eigenschaften eines Elements samt passender <c>if_resolution</c>-Blöcke (EU IV): deren Werte überschreiben die des Elements, der letzte passende
/// gewinnt. Jeder so übernommene Schlüssel bekommt einen Hinweis mit der Bedingung.
/// </summary>
internal sealed class ClassicElementProps
{
    private readonly ResolvedElement _element;
    private readonly IReadOnlyList<ClassicResolutionMatch> _overrides;
    private readonly Dictionary<string, string> _used = new(StringComparer.OrdinalIgnoreCase);

    public ClassicElementProps(ResolvedElement element, double screenWidth, double screenHeight)
    {
        _element = element;
        _overrides = ClassicProps.MatchingIfResolution(element, screenWidth, screenHeight);
    }

    public IEnumerable<string> Notes => _used.Select(u => GuiTexts.ClassicFromIfResolution(u.Key, u.Value));

    public string? Scalar(string name)
    {
        for (var i = _overrides.Count - 1; i >= 0; i--)
        {
            if (ClassicProps.Scalar(_overrides[i].Block, name) is { } value)
            {
                _used.TryAdd(name, _overrides[i].Condition);
                return value;
            }
        }

        return ClassicProps.Scalar(_element, name);
    }

    public GuiNode? Block(string name)
    {
        for (var i = _overrides.Count - 1; i >= 0; i--)
        {
            if (ClassicProps.Block(_overrides[i].Block, name) is { } block)
            {
                _used.TryAdd(name, _overrides[i].Condition);
                return block;
            }
        }

        return ClassicProps.Block(_element, name);
    }
}
