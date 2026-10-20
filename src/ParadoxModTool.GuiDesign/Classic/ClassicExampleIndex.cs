using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Ein Vanilla-Beispiel: das benannte Element (<see cref="ElementName"/>, <see cref="WidgetKey"/> wie <c>iconType</c>) in <see cref="File"/>.
/// <see cref="Line"/> ist die Zeile der Eigenschaft bei Beispielen zu einer Eigenschaft, die Zeile des Elements bei Beispielen zu einer Elementart.
/// </summary>
public sealed record ClassicExample(string ElementName, string WidgetKey, GuiSourceFile File, int Line);

/// <summary>
/// Beispiele aus den Vanilla-Fenstern eines klassischen Spiels für die Erklärung im Designer: je Eigenschaft und Wert, je Eigenschaft und je Elementart
/// bis zu drei. Ein Durchgang über alle Vanilla-Dateien in Indexreihenfolge, also stabil. Unveränderlich.
/// </summary>
public sealed class ClassicExampleIndex
{
    private const int MaxExamples = 3;

    private static readonly (string A, string B)[] PairKeys = { ("x", "y"), ("width", "height") };

    private static readonly IReadOnlyList<ClassicExample> None = Array.Empty<ClassicExample>();

    private readonly Dictionary<(string Property, string Value), List<ClassicExample>> _byValue = new();
    private readonly Dictionary<string, List<ClassicExample>> _byProperty = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ClassicExample>> _byElement = new(StringComparer.Ordinal);

    private ClassicExampleIndex()
    {
    }

    /// <summary>Nur Dateien der Ebene Vanilla zählen; Mod und Abhängigkeiten sind keine Vorbilder.</summary>
    public static ClassicExampleIndex Build(GuiIndex index)
    {
        var result = new ClassicExampleIndex();
        foreach (var file in index.Files)
        {
            if (file.Layer != GuiLayer.Vanilla)
            {
                continue;
            }

            foreach (var node in index.DocumentOf(file).Root.Descendants())
            {
                if (node.Kind == GuiNodeKind.KeyedBlock && IsWindow(node))
                {
                    result.AddElementTree(node, file);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Beispiele zu einer Eigenschaft: gibt es welche mit genau diesem (bereinigten) Wert, nur diese, sonst die ersten der Eigenschaft.
    /// <paramref name="value"/> <see langword="null"/>: die ersten der Eigenschaft.
    /// </summary>
    public IReadOnlyList<ClassicExample> For(string property, string? value)
    {
        var name = property.ToLowerInvariant();
        if (value is not null && _byValue.TryGetValue((name, ClassicAnchors.Clean(value).ToLowerInvariant()), out var exact))
        {
            return exact;
        }

        return _byProperty.GetValueOrDefault(name) ?? None;
    }

    /// <summary>Beispiele zu einer Elementart (<c>iconType</c>, ohne Rücksicht auf Groß-/Kleinschreibung).</summary>
    public IReadOnlyList<ClassicExample> ForElement(string key) => _byElement.GetValueOrDefault(key.ToLowerInvariant()) ?? None;

    // Fenster: Block direkt in einem guiTypes auf oberster Dateiebene (wie im Index).
    private static bool IsWindow(GuiNode node) =>
        node.Parent is { Kind: GuiNodeKind.KeyedBlock, Parent.Kind: GuiNodeKind.Document } container
        && string.Equals(container.Name, "guiTypes", StringComparison.OrdinalIgnoreCase);

    private void AddElementTree(GuiNode window, GuiSourceFile file)
    {
        var stack = new Stack<GuiNode>();
        stack.Push(window);
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            var name = NameOf(element);
            if (string.IsNullOrEmpty(name))
            {
                // Ein Block ohne name ist kein Element (z.B. if_resolution) und wird von seinem Element als Eigenschaft gezählt.
                continue;
            }

            Add(_byElement, element.Name.ToLowerInvariant(), new ClassicExample(name, element.Name, file, element.Line));
            var nested = new List<GuiNode>();
            foreach (var child in element.Children)
            {
                if (child.Kind == GuiNodeKind.Property && !string.IsNullOrEmpty(child.Value))
                {
                    AddProperty(child.Name, ClassicAnchors.Clean(child.Value), new ClassicExample(name, element.Name, file, child.Line));
                }
                else if (child.Kind == GuiNodeKind.KeyedBlock)
                {
                    if (NameOf(child) is not null)
                    {
                        nested.Add(child);
                    }
                    else
                    {
                        AddProperty(child.Name, PairValue(child), new ClassicExample(name, element.Name, file, child.Line));
                    }
                }
            }

            // Rückwärts auf den Stapel, damit die Kinder in Textreihenfolge folgen (stabile Beispiele).
            for (var i = nested.Count - 1; i >= 0; i--)
            {
                stack.Push(nested[i]);
            }
        }
    }

    private void AddProperty(string property, string? value, ClassicExample example)
    {
        var name = property.ToLowerInvariant();
        Add(_byProperty, name, example);
        if (value is not null)
        {
            if (!_byValue.TryGetValue((name, value.ToLowerInvariant()), out var list))
            {
                _byValue[(name, value.ToLowerInvariant())] = list = new List<ClassicExample>();
            }

            if (list.Count < MaxExamples)
            {
                list.Add(example);
            }
        }
    }

    private static void Add(Dictionary<string, List<ClassicExample>> map, string key, ClassicExample example)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = new List<ClassicExample>();
        }

        if (list.Count < MaxExamples)
        {
            list.Add(example);
        }
    }

    /// <summary>Paarblock (<c>position = { x = 1 y = 2 }</c>) als <c>"x,y"</c> der Rohwerte; andere Blöcke haben keinen Wert.</summary>
    private static string? PairValue(GuiNode block)
    {
        foreach (var (a, b) in PairKeys)
        {
            if (Raw(block, a) is { } first && Raw(block, b) is { } second)
            {
                return first + "," + second;
            }
        }

        return null;
    }

    private static string? Raw(GuiNode block, string key) =>
        block.Children.LastOrDefault(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase))?.Value is { } value
            ? ClassicAnchors.Clean(value)
            : null;

    private static string? NameOf(GuiNode element) =>
        element.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, "name", StringComparison.OrdinalIgnoreCase))?.Value is { Length: > 0 } name
            ? name
            : null;
}
