using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Catalog;

public static class GuiCatalogLearner
{
    private const int MaxWordsPerProperty = 60;
    private const int MaxBindingsPerContext = 200;

    public static GuiPropertyCatalog Learn(GuiIndex index, GuiRules rules, string fingerprint)
    {
        var resolver = new GuiResolver(index, rules);
        var catalog = new GuiPropertyCatalog { Fingerprint = fingerprint };
        var baseCache = new Dictionary<string, string>(StringComparer.Ordinal);

        string BaseOf(string key)
        {
            if (!baseCache.TryGetValue(key, out var baseWidget))
            {
                baseWidget = resolver.BaseWidgetOf(key);
                baseCache[key] = baseWidget;
            }

            return baseWidget;
        }

        foreach (var file in index.Files.Where(f => f.Layer == GuiLayer.Vanilla))
        {
            foreach (var node in index.DocumentOf(file).Root.Descendants())
            {
                if (node.Kind == GuiNodeKind.TypeDefinition)
                {
                    Record(catalog, BaseOf(node.Name), node, resolver);
                }
                else if (node.Kind == GuiNodeKind.KeyedBlock && resolver.Classify(node) is GuiRole.TypeInstance or GuiRole.EngineWidget)
                {
                    Record(catalog, BaseOf(node.Name), node, resolver);
                }
                else if (node is { Kind: GuiNodeKind.Property, IsQuoted: true } && node.Name != "datacontext" && node.Value?.Contains('[') == true)
                {
                    RecordBindings(catalog, node);
                }
            }
        }

        foreach (var context in catalog.Bindings.Keys.ToList())
        {
            var expressions = catalog.Bindings[context];
            if (expressions.Count > MaxBindingsPerContext)
            {
                catalog.Bindings[context] = expressions
                    .OrderByDescending(p => p.Value)
                    .ThenBy(p => p.Key, StringComparer.Ordinal)
                    .Take(MaxBindingsPerContext)
                    .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            }
        }

        return catalog;
    }

    private static void Record(GuiPropertyCatalog catalog, string widget, GuiNode container, GuiResolver resolver)
    {
        if (!catalog.Widgets.TryGetValue(widget, out var stats))
        {
            stats = new GuiWidgetStats { Name = widget };
            catalog.Widgets[widget] = stats;
        }

        stats.Count++;
        foreach (var child in container.Children)
        {
            GuiValueForm form;
            string? word = null;
            switch (child.Kind)
            {
                case GuiNodeKind.Property:
                    form = GuiValueClassifier.ClassifyScalar(child.Value ?? string.Empty, child.IsQuoted);
                    if (form == GuiValueForm.Word)
                    {
                        word = child.Value;
                    }

                    break;

                case GuiNodeKind.KeyedBlock:
                    var role = resolver.Classify(child);
                    if (role == GuiRole.ValueBlock)
                    {
                        form = GuiValueClassifier.ClassifyItems(child.Children.Where(c => c.Kind == GuiNodeKind.ListItem).Select(c => c.Value ?? string.Empty).ToList());
                    }
                    else if (role == GuiRole.StructuredBlock)
                    {
                        form = GuiValueForm.Block;
                    }
                    else
                    {
                        continue;
                    }

                    break;

                default:
                    continue;
            }

            if (!stats.Properties.TryGetValue(child.Name, out var property))
            {
                property = new GuiPropertyStats { Name = child.Name };
                stats.Properties[child.Name] = property;
            }

            property.Count++;
            property.Forms[form] = property.Forms.GetValueOrDefault(form) + 1;
            if (word is not null && (property.Words.ContainsKey(word) || property.Words.Count < MaxWordsPerProperty))
            {
                property.Words[word] = property.Words.GetValueOrDefault(word) + 1;
            }
        }
    }

    private static void RecordBindings(GuiPropertyCatalog catalog, GuiNode property)
    {
        var context = DataContextOf(property.Parent) ?? string.Empty;
        if (!catalog.Bindings.TryGetValue(context, out var expressions))
        {
            expressions = new Dictionary<string, int>(StringComparer.Ordinal);
            catalog.Bindings[context] = expressions;
        }

        foreach (var expression in BracketExpressions(property.Value!))
        {
            expressions[expression] = expressions.GetValueOrDefault(expression) + 1;
        }
    }

    /// <summary>Wert des nächsten <c>datacontext</c> am Container oder einem seiner Vorfahren.</summary>
    private static string? DataContextOf(GuiNode? container)
    {
        for (var node = container; node is not null; node = node.Parent)
        {
            var dataContext = node.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name == "datacontext" && c.Value is not null);
            if (dataContext is not null)
            {
                return dataContext.Value;
            }
        }

        return null;
    }

    /// <summary>Äußere <c>[ … ]</c>-Ausdrücke eines Texts.</summary>
    private static IEnumerable<string> BracketExpressions(string text)
    {
        var depth = 0;
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '[')
            {
                if (depth == 0)
                {
                    start = i;
                }

                depth++;
            }
            else if (text[i] == ']' && depth > 0)
            {
                depth--;
                if (depth == 0)
                {
                    yield return text[start..(i + 1)];
                }
            }
        }
    }
}
