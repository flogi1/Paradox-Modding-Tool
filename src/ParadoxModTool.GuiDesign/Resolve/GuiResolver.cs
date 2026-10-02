using System.Globalization;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Resolve;

/// <summary>
/// Löst Widgets auf: Typkette bis zum Engine-Widget, <c>using</c>-Templates, <c>block</c>/<c>blockoverride</c> und Konstanten.
/// Definitionen im Overlay-Dokument (die gerade bearbeitete Datei) gewinnen über den Index. Vorrang der Blocküberschreibungen:
/// der äußerste gewinnt (Plan-Ruling 5). Strukturierte Blöcke sammeln sich, Skalare und Wertblöcke überschreiben (Ruling 6).
/// </summary>
public sealed class GuiResolver
{
    private const int MaxDepth = 64;

    private readonly GuiIndex _index;
    private readonly GuiDocument? _overlay;
    private readonly string? _overlayPath;
    private readonly Dictionary<string, GuiNode> _overlayTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GuiNode> _overlayTemplates = new(StringComparer.Ordinal);

    public GuiResolver(GuiIndex index, GuiRules rules, GuiDocument? overlay = null, string? overlayPath = null)
    {
        _index = index;
        Rules = rules;
        _overlay = overlay;
        _overlayPath = overlayPath;
        if (overlay is null)
        {
            return;
        }

        foreach (var node in overlay.Root.Descendants())
        {
            if (node.Kind == GuiNodeKind.TypeDefinition)
            {
                _overlayTypes[node.Name] = node;
            }
            else if (node.Kind == GuiNodeKind.TemplateDefinition)
            {
                _overlayTemplates[node.Name] = node;
            }
        }
    }

    public GuiRules Rules { get; }

    public GuiNode? FindType(string name) =>
        _overlayTypes.TryGetValue(name, out var node) ? node : _index.Find(name, GuiDefinitionKind.Type)?.Node;

    public GuiNode? FindTemplate(string name) =>
        _overlayTemplates.TryGetValue(name, out var node) ? node : _index.Find(name, GuiDefinitionKind.Template)?.Node;

    public bool IsType(string name) => FindType(name) is not null;

    public GuiRole Classify(GuiNode node) => Rules.Classify(node, IsType);

    public IReadOnlyList<string> TypeChain(string key) => TypeChain(key, null, null);

    public string BaseWidgetOf(string key) => TypeChain(key)[^1];

    public string? FilePathOf(GuiNode node) =>
        IsOverlay(node) ? _overlayPath : _index.FileOf(node)?.FullPath;

    public string? TextOf(GuiNode node) => DocumentOf(node)?.TextOf(node);

    public ResolvedElement Resolve(GuiNode widgetNode) =>
        ResolveElement(widgetNode, new Dictionary<string, OverrideEntry>(StringComparer.Ordinal), null, 0, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>
    /// Löst eine leere Instanz eines Typs auf (Vorschau einer Typdefinition). Der dafür erzeugte Knoten trägt Spanne und Zeile der
    /// Definition und hat deren Elternknoten als <c>Parent</c> (für Konstanten und Dateizuordnung), steht aber nicht in dessen Kindern.
    /// Null, wenn der Typ unbekannt ist.
    /// </summary>
    public ResolvedElement? ResolveType(string typeName)
    {
        if (FindType(typeName) is not { } definition)
        {
            return null;
        }

        var instance = new GuiNode(GuiNodeKind.KeyedBlock)
        {
            Name = typeName,
            Span = definition.Span,
            NameSpan = definition.NameSpan,
            OpenBrace = definition.OpenBrace,
            CloseBrace = definition.CloseBrace,
            Line = definition.Line,
            Parent = definition.Parent,
        };
        return Resolve(instance);
    }

    private bool IsOverlay(GuiNode node) => _overlay is not null && ReferenceEquals(node.Root, _overlay.Root);

    private GuiDocument? DocumentOf(GuiNode node) => IsOverlay(node) ? _overlay : _index.DocumentOf(node);

    private IReadOnlyList<string> TypeChain(string key, List<GuiResolveIssue>? issues, GuiNode? at)
    {
        var chain = new List<string> { key };
        var seen = new HashSet<string>(StringComparer.Ordinal) { key };
        var current = key;
        while (FindType(current) is { } definition)
        {
            var next = definition.BaseName ?? string.Empty;
            if (next.Length == 0)
            {
                break;
            }

            // "type icon = icon { … }" setzt Standardwerte des Engine-Widgets gleichen Namens (Vanilla, z.B. shared/defaults.gui):
            // die Kette endet dort am Engine-Widget, kein Zyklus.
            if (next == current)
            {
                chain.Add(next);
                break;
            }

            if (!seen.Add(next))
            {
                if (issues is not null && at is not null)
                {
                    issues.Add(new GuiResolveIssue(GuiResolveIssueKind.TypeCycle, string.Join(" → ", chain.Append(next)), at));
                }

                break;
            }

            chain.Add(next);
            current = next;
        }

        return chain;
    }

    /// <param name="expanding">Typnamen, die auf dem Weg von der Wurzel bis hier gerade aufgelöst werden. Enthält ein Typ sich selbst
    /// (auch über Zwischentypen), endet die Auflösung dort sofort mit Hinweis, statt sich exponentiell zu verzweigen.</param>
    private ResolvedElement ResolveElement(GuiNode node, IReadOnlyDictionary<string, OverrideEntry> outer, GuiOrigin? inherited, int depth, IReadOnlySet<string> expanding)
    {
        var role = Classify(node);
        var issues = new List<GuiResolveIssue>();
        var chain = role == GuiRole.TypeInstance ? TypeChain(node.Name, issues, node) : new[] { node.Name };
        var element = new ResolvedElement
        {
            Key = node.Name,
            Role = role,
            BaseWidget = chain[^1],
            TypeChain = chain,
            Origin = OriginAt(inherited, node),
        };
        element.Issues.AddRange(issues);

        if (depth > MaxDepth)
        {
            element.Issues.Add(new GuiResolveIssue(GuiResolveIssueKind.DepthLimit, node.Name, node));
            return element;
        }

        // Was direkt in der Datei steht (z. B. vbox in vbox), ist endlicher Text und keine Schleife. Nur Inhalt aus Typen, Templates und
        // blockoverrides kann sich selbst wieder einsetzen.
        if (role == GuiRole.TypeInstance && element.Origin.Kind != GuiOriginKind.Here && expanding.Contains(node.Name))
        {
            element.Issues.Add(new GuiResolveIssue(GuiResolveIssueKind.TypeCycle, $"{node.Name} enthält sich selbst", node));
            return element;
        }

        var innerExpanding = new HashSet<string>(expanding, StringComparer.Ordinal);
        if (role == GuiRole.TypeInstance)
        {
            innerExpanding.Add(node.Name);
        }

        var bodies = new List<(GuiNode Body, GuiOrigin Origin)>();
        for (var j = chain.Count - 2; j >= 0; j--)
        {
            if (FindType(chain[j]) is { } definition)
            {
                bodies.Add((definition, Origin(GuiOriginKind.Type, chain[j], definition)));
            }
        }

        bodies.Add((node, element.Origin));

        var overrides = new Dictionary<string, OverrideEntry>(outer, StringComparer.Ordinal);
        for (var k = bodies.Count - 1; k >= 0; k--)
        {
            foreach (var child in bodies[k].Body.Children)
            {
                if (child.Kind == GuiNodeKind.BlockOverride && !overrides.ContainsKey(child.Name))
                {
                    overrides[child.Name] = new OverrideEntry(child, Origin(GuiOriginKind.BlockOverride, child.Name, child));
                }
            }
        }

        foreach (var (body, origin) in bodies)
        {
            Apply(element, body.Children, origin, overrides, depth, new HashSet<string>(StringComparer.Ordinal), innerExpanding);
        }

        return element;
    }

    private void Apply(ResolvedElement element, IEnumerable<GuiNode> entries, GuiOrigin origin, IReadOnlyDictionary<string, OverrideEntry> overrides, int depth, HashSet<string> templateStack, IReadOnlySet<string> expanding)
    {
        foreach (var entry in entries)
        {
            switch (Classify(entry))
            {
                case GuiRole.Property:
                    SetProperty(element, entry, GuiRole.Property, origin);
                    break;

                case GuiRole.ValueBlock:
                    SetProperty(element, entry, GuiRole.ValueBlock, origin);
                    break;

                case GuiRole.StructuredBlock:
                    element.Properties.Add(new ResolvedProperty
                    {
                        Name = entry.Name,
                        Role = GuiRole.StructuredBlock,
                        Value = DocumentOf(entry)?.TextOf(entry.BodySpan).Trim() ?? string.Empty,
                        Origin = OriginAt(origin, entry),
                    });
                    break;

                case GuiRole.Using:
                    var templateName = entry.Value ?? string.Empty;
                    if (!templateStack.Add(templateName))
                    {
                        element.Issues.Add(new GuiResolveIssue(GuiResolveIssueKind.TemplateCycle, templateName, entry));
                        break;
                    }

                    if (FindTemplate(templateName) is { } template)
                    {
                        Apply(element, template.Children, Origin(GuiOriginKind.Template, templateName, template), overrides, depth, templateStack, expanding);
                    }
                    else
                    {
                        element.Issues.Add(new GuiResolveIssue(GuiResolveIssueKind.MissingTemplate, templateName, entry));
                    }

                    templateStack.Remove(templateName);
                    break;

                case GuiRole.Block:
                    if (!element.OfferedBlocks.Contains(entry.Name))
                    {
                        element.OfferedBlocks.Add(entry.Name);
                    }

                    // Ein blockoverride darf in seinem Inhalt denselben block erneut anbieten; dort gilt dann dessen Standardinhalt,
                    // sonst würde sich die Ersetzung endlos selbst einsetzen. Der Schlüssel teilt sich den Stapel mit den Templates.
                    var blockKey = "\u0001block:" + entry.Name;
                    if (overrides.TryGetValue(entry.Name, out var replacement) && templateStack.Add(blockKey))
                    {
                        Apply(element, replacement.Node.Children, replacement.Origin, overrides, depth, templateStack, expanding);
                        templateStack.Remove(blockKey);
                    }
                    else
                    {
                        Apply(element, entry.Children, origin, overrides, depth, templateStack, expanding);
                    }

                    break;

                case GuiRole.TypeInstance or GuiRole.EngineWidget or GuiRole.WidgetHolder:
                    element.Children.Add(ResolveElement(entry, overrides, origin.Kind == GuiOriginKind.Here ? null : origin, depth + 1, expanding));
                    break;
            }
        }
    }

    private void SetProperty(ResolvedElement element, GuiNode entry, GuiRole role, GuiOrigin origin)
    {
        var property = new ResolvedProperty
        {
            Name = entry.Name,
            Role = role,
            IsQuoted = entry.IsQuoted,
            Origin = OriginAt(origin, entry),
        };

        if (role == GuiRole.Property)
        {
            property.Value = entry.IsQuoted ? entry.Value ?? string.Empty : Substitute(entry.Value ?? string.Empty, entry, property, element);
        }
        else
        {
            property.Items = entry.Children
                .Where(c => c.Kind == GuiNodeKind.ListItem)
                .Select(c => c.IsQuoted ? c.Value ?? string.Empty : Substitute(c.Value ?? string.Empty, c, property, element))
                .ToList();
            property.Value = string.Join(" ", property.Items);
        }

        var existing = element.Properties.FindIndex(p => p.Name == entry.Name && p.Role != GuiRole.StructuredBlock);
        if (existing >= 0)
        {
            var previous = element.Properties[existing];
            property.Overridden.AddRange(previous.Overridden);
            property.Overridden.Add(previous.Origin);
            element.Properties[existing] = property;
        }
        else
        {
            element.Properties.Add(property);
        }
    }

    private string Substitute(string value, GuiNode at, ResolvedProperty property, ResolvedElement element)
    {
        if (value.StartsWith("@[", StringComparison.Ordinal) && value.EndsWith(']'))
        {
            if (GuiExpression.TryEvaluate(value[2..^1], name => ParseNumber(LookupConstant(name, at, 0)), out var result))
            {
                return GuiExpression.Format(result);
            }

            property.Unresolved = true;
            element.Issues.Add(new GuiResolveIssue(GuiResolveIssueKind.UnevaluatedExpression, value, at));
            return value;
        }

        if (value.Length > 1 && value[0] == '@')
        {
            if (LookupConstant(value[1..], at, 0) is { } constant)
            {
                return constant;
            }

            property.Unresolved = true;
            element.Issues.Add(new GuiResolveIssue(GuiResolveIssueKind.MissingConstant, value[1..], at));
        }

        return value;
    }

    /// <summary>Konstante im nächsten umschließenden Gültigkeitsbereich derselben Datei (Konstanten sind dateilokal).</summary>
    private static string? LookupConstant(string name, GuiNode at, int depth)
    {
        if (depth > 16)
        {
            return null;
        }

        foreach (var scope in at.Ancestors())
        {
            foreach (var child in scope.Children)
            {
                if (child.Kind != GuiNodeKind.ConstantDefinition || child.Name != name)
                {
                    continue;
                }

                var raw = child.Value ?? string.Empty;
                if (raw.StartsWith("@[", StringComparison.Ordinal) && raw.EndsWith(']'))
                {
                    return GuiExpression.TryEvaluate(raw[2..^1], n => ParseNumber(LookupConstant(n, child, depth + 1)), out var result)
                        ? GuiExpression.Format(result)
                        : null;
                }

                return raw.Length > 1 && raw[0] == '@' ? LookupConstant(raw[1..], child, depth + 1) : raw;
            }
        }

        return null;
    }

    private static double? ParseNumber(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private GuiOrigin Origin(GuiOriginKind kind, string definition, GuiNode node) =>
        new(kind, definition, FilePathOf(node), node.Line, node);

    /// <summary>Herkunft eines Eintrags: "hier" bleibt "hier" mit dem Eintrag selbst; sonst Art und Name der umschließenden Definition mit Datei und Zeile des Eintrags.</summary>
    private GuiOrigin OriginAt(GuiOrigin? context, GuiNode node) =>
        context is null || context.Kind == GuiOriginKind.Here
            ? Origin(GuiOriginKind.Here, string.Empty, node)
            : Origin(context.Kind, context.Definition, node);

    private sealed record OverrideEntry(GuiNode Node, GuiOrigin Origin);
}
