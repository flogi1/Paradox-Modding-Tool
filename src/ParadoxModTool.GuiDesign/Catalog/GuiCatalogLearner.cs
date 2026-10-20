using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Catalog;

public static class GuiCatalogLearner
{
    private const int MaxWordsPerProperty = 60;
    private const int MaxBindingsPerContext = 200;
    private const int MaxExamplesPerKey = 5;

    /// <summary>Anteil der Ausdrücke in den Einträgen einer Liste, die mit demselben Typ beginnen müssen, damit er als Elementtyp gilt.</summary>
    private const double ItemTypeShare = 0.6;

    public static GuiPropertyCatalog Learn(GuiIndex index, GuiRules rules, string fingerprint)
    {
        var resolver = new GuiResolver(index, rules);
        var classic = rules.Layout.IsClassic;
        var catalog = new GuiPropertyCatalog { Fingerprint = fingerprint, Classic = classic };
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

        var examples = new Dictionary<string, Dictionary<string, (int Count, string File)>>(StringComparer.Ordinal);
        var itemTypes = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var file in index.Files.Where(f => f.Layer == GuiLayer.Vanilla))
        {
            foreach (var node in index.DocumentOf(file).Root.Descendants())
            {
                if (node.Kind == GuiNodeKind.TypeDefinition)
                {
                    Record(catalog, BaseOf(node.Name), node, resolver, classic);
                }
                else if (node.Kind == GuiNodeKind.KeyedBlock && resolver.Classify(node) is GuiRole.TypeInstance or GuiRole.EngineWidget)
                {
                    // Klassisch sind guiTypes und spriteTypes nur Behälter; als Element gilt ein bekannter Schlüssel oder ein Block mit name.
                    if (!classic || IsClassicElement(node))
                    {
                        Record(catalog, BaseOf(node.Name), node, resolver, classic);
                    }
                }
                else if (node is { Kind: GuiNodeKind.Property, IsQuoted: true } && node.Value?.Contains('[') == true)
                {
                    if (node.Name != "datacontext")
                    {
                        RecordBindings(catalog, node);
                    }

                    RecordExamples(examples, node.Value!, file.RelativePath);
                    if (node.Name == "datamodel")
                    {
                        RecordItemType(itemTypes, node);
                    }
                }
            }
        }

        foreach (var (key, found) in examples)
        {
            catalog.Examples[key] = found
                .OrderByDescending(p => p.Value.Count)
                .ThenBy(p => p.Key, StringComparer.Ordinal)
                .Take(MaxExamplesPerKey)
                .Select(p => new GuiExample(p.Key, p.Value.File))
                .ToList();
        }

        foreach (var (list, counts) in itemTypes)
        {
            var total = counts.Values.Sum();
            var best = counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First();
            if (best.Value >= ItemTypeShare * total)
            {
                catalog.ListItemTypes[list] = best.Key;
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

    private static bool IsClassicElement(GuiNode node) =>
        Classic.ClassicLayoutEngine.IsKnownElement(node.Name)
        || node.Children.Any(c => c.Kind == GuiNodeKind.Property && string.Equals(c.Name, "name", StringComparison.OrdinalIgnoreCase));

    /// <summary>Klassisch liegen Widgets und Eigenschaften unter dem kleingeschriebenen Schlüssel; die Schreibweisen zählt <c>Spellings</c> mit.</summary>
    private static void Record(GuiPropertyCatalog catalog, string widget, GuiNode container, GuiResolver resolver, bool classic)
    {
        var widgetKey = classic ? GuiPropertyCatalog.ClassicKey(widget) : widget;
        if (!catalog.Widgets.TryGetValue(widgetKey, out var stats))
        {
            stats = new GuiWidgetStats { Name = widgetKey };
            catalog.Widgets[widgetKey] = stats;
        }

        stats.Count++;
        stats.Spellings[widget] = stats.Spellings.GetValueOrDefault(widget) + 1;
        foreach (var child in container.Children)
        {
            GuiValueForm form;
            string? word = null;
            string? pairKey = null;
            var quoted = false;
            switch (child.Kind)
            {
                case GuiNodeKind.Property:
                    form = GuiValueClassifier.ClassifyScalar(child.Value ?? string.Empty, child.IsQuoted);
                    quoted = child.IsQuoted;
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
                        pairKey = PairKeyOf(child);
                    }
                    else
                    {
                        continue;
                    }

                    break;

                default:
                    continue;
            }

            var propertyKey = classic ? GuiPropertyCatalog.ClassicKey(child.Name) : child.Name;
            if (!stats.Properties.TryGetValue(propertyKey, out var property))
            {
                property = new GuiPropertyStats { Name = propertyKey };
                stats.Properties[propertyKey] = property;
            }

            property.Count++;
            property.Spellings[child.Name] = property.Spellings.GetValueOrDefault(child.Name) + 1;
            if (quoted)
            {
                property.Quoted++;
            }

            if (pairKey is not null)
            {
                property.PairKeys[pairKey] = property.PairKeys.GetValueOrDefault(pairKey) + 1;
            }

            property.Forms[form] = property.Forms.GetValueOrDefault(form) + 1;
            if (word is not null && (property.Words.ContainsKey(word) || property.Words.Count < MaxWordsPerProperty))
            {
                property.Words[word] = property.Words.GetValueOrDefault(word) + 1;
            }
        }
    }

    /// <summary>Schlüssel eines Blocks mit genau zwei Skalaren, klein und mit Komma verbunden (<c>width,height</c>); sonst null. Kommentare zählen nicht.</summary>
    private static string? PairKeyOf(GuiNode block)
    {
        var children = block.Children.Where(c => c.Kind != GuiNodeKind.Comment).ToList();
        return children.Count == 2 && children.All(c => c.Kind == GuiNodeKind.Property)
            ? string.Join(',', children.Select(c => c.Name.ToLowerInvariant()))
            : null;
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

    /// <summary>
    /// Merkt jeden <c>[…]</c>-Ausdruck als Beispiel unter den Schlüsseln seiner Ketten: erster Bezeichner, die ersten beiden (<c>Country.GetCapital</c>) und
    /// Namen globaler Funktionen (<c>Not</c>). Tiefere Glieder nicht: ihr Besitzer-Typ ist ohne Datentypen unbekannt.
    /// </summary>
    private static void RecordExamples(Dictionary<string, Dictionary<string, (int Count, string File)>> examples, string value, string file)
    {
        foreach (var expression in BracketExpressions(value))
        {
            foreach (var key in ExampleKeys(expression).Distinct(StringComparer.Ordinal))
            {
                if (!examples.TryGetValue(key, out var found))
                {
                    examples[key] = found = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
                }

                found[expression] = found.TryGetValue(expression, out var known) ? (known.Count + 1, known.File) : (1, file);
            }
        }
    }

    private static IEnumerable<string> ExampleKeys(string expression)
    {
        foreach (var chain in DataTypes.DataExpression.Chains(expression))
        {
            yield return chain.Parts[0];
            if (chain.Parts.Count > 1)
            {
                yield return chain.Parts[0] + "." + chain.Parts[1];
            }
        }
    }

    /// <summary>Zählt, mit welchem Typ die Ausdrücke im <c>item</c>-Block neben einem <c>datamodel</c> beginnen.</summary>
    private static void RecordItemType(Dictionary<string, Dictionary<string, int>> itemTypes, GuiNode datamodel)
    {
        var list = DataTypes.DataExpression.Strip(datamodel.Value!);
        if (datamodel.Parent?.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name == "item") is not { } item)
        {
            return;
        }

        if (!itemTypes.TryGetValue(list, out var counts))
        {
            itemTypes[list] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        foreach (var property in ItemExpressions(item))
        {
            foreach (var expression in BracketExpressions(property.Value!))
            {
                // Der Kontext-Typ ist der Anfang der ersten Kette, die kein Aufruf einer globalen Funktion ist (Not(Location.HasOwner) → Location).
                // Das Widget selbst (PdxGuiWidget.GetIndexInDataModel) und globale Promotes (GetPlayer, Player) zählen nicht.
                if (DataTypes.DataExpression.Chains(expression)
                        .FirstOrDefault(c => !(c.IsCall && c.Parts.Count == 1) && !IsContextFreeStart(c.Parts[0])) is { } chain
                    && char.IsUpper(chain.Parts[0][0]))
                {
                    counts[chain.Parts[0]] = counts.GetValueOrDefault(chain.Parts[0]) + 1;
                }
            }
        }

        if (counts.Count == 0)
        {
            itemTypes.Remove(list);
        }
    }

    /// <summary>Datenausdrücke in einem Eintrag; Unterelemente mit eigenem <c>datacontext</c> gehören zu einem anderen Kontext und bleiben außen vor.</summary>
    private static IEnumerable<GuiNode> ItemExpressions(GuiNode container)
    {
        foreach (var child in container.Children)
        {
            if (child is { Kind: GuiNodeKind.Property, IsQuoted: true } && child.Name != "datacontext" && child.Value?.Contains('[') == true)
            {
                yield return child;
            }
            else if (child.Children.Count > 0 && !child.Children.Any(c => c.Kind == GuiNodeKind.Property && c.Name == "datacontext"))
            {
                foreach (var nested in ItemExpressions(child))
                {
                    yield return nested;
                }
            }
        }
    }

    /// <summary>Anfänge, die nicht den Eintrag meinen: das Widget selbst (<c>PdxGuiWidget</c>) und globale Promotes (<c>GetPlayer</c>, <c>Player</c>).</summary>
    private static bool IsContextFreeStart(string identifier) =>
        identifier.StartsWith("Pdx", StringComparison.Ordinal)
        || identifier.StartsWith("Get", StringComparison.Ordinal)
        || identifier is "Player" or "GuiScope" or "JominiPlayer" or "EmptyScope";

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
