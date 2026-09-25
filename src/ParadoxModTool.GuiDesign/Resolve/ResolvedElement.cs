using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Resolve;

public enum GuiOriginKind
{
    Here,
    Type,
    Template,
    BlockOverride,
}

/// <summary>Herkunft eines Werts oder Elements. <c>Definition</c> ist der Name von Typ, Template bzw. Block ("" bei <see cref="GuiOriginKind.Here"/>).</summary>
public sealed record GuiOrigin(GuiOriginKind Kind, string Definition, string? FilePath, int Line, GuiNode Node);

public sealed class ResolvedProperty
{
    public required string Name { get; init; }

    /// <summary><see cref="GuiRole.Property"/>, <see cref="GuiRole.ValueBlock"/> oder <see cref="GuiRole.StructuredBlock"/>.</summary>
    public required GuiRole Role { get; init; }

    /// <summary>Skalarwert (Konstanten ersetzt), bei Wertblöcken die Einträge mit Leerzeichen verbunden, bei strukturierten Blöcken der Rohtext des Inhalts.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Einträge eines Wertblocks, Konstanten und <c>@[ … ]</c> soweit möglich ersetzt.</summary>
    public IReadOnlyList<string> Items { get; set; } = Array.Empty<string>();

    public bool IsQuoted { get; init; }

    public required GuiOrigin Origin { get; init; }

    /// <summary>Frühere, jetzt überschriebene Stellen, älteste zuerst.</summary>
    public List<GuiOrigin> Overridden { get; } = new();

    /// <summary>Eine Konstante fehlt oder ein Ausdruck ließ sich nicht auswerten.</summary>
    public bool Unresolved { get; set; }
}

public enum GuiResolveIssueKind
{
    TypeCycle,
    TemplateCycle,
    MissingTemplate,
    MissingConstant,
    UnevaluatedExpression,
    DepthLimit,
}

public sealed record GuiResolveIssue(GuiResolveIssueKind Kind, string Detail, GuiNode Node);

/// <summary>Ein Widget nach Auflösung von Typkette, Templates, Blöcken und Konstanten.</summary>
public sealed class ResolvedElement
{
    /// <summary>Schlüssel wie geschrieben, z.B. <c>button_standard</c> oder <c>hbox</c>.</summary>
    public required string Key { get; init; }

    /// <summary><see cref="GuiRole.TypeInstance"/>, <see cref="GuiRole.EngineWidget"/> oder <see cref="GuiRole.WidgetHolder"/>.</summary>
    public required GuiRole Role { get; init; }

    /// <summary>Engine-Widget am Ende der Typkette (bei Haltern der Halter-Name).</summary>
    public required string BaseWidget { get; init; }

    /// <summary>Schlüssel, Basistypen, Engine-Widget.</summary>
    public required IReadOnlyList<string> TypeChain { get; init; }

    public required GuiOrigin Origin { get; init; }

    public List<ResolvedProperty> Properties { get; } = new();

    public List<ResolvedElement> Children { get; } = new();

    /// <summary>Blöcke, die dieses Element selbst (nicht seine Kinder) anbietet.</summary>
    public List<string> OfferedBlocks { get; } = new();

    public List<GuiResolveIssue> Issues { get; } = new();

    public string? Name => Property("name")?.Value;

    /// <summary>Skalare Eigenschaft oder Wertblock mit diesem Namen (der letzte gewinnt).</summary>
    public ResolvedProperty? Property(string name) =>
        Properties.FirstOrDefault(p => p.Name == name && p.Role != GuiRole.StructuredBlock);

    public IEnumerable<ResolvedProperty> StructuredBlocks(string name) =>
        Properties.Where(p => p.Name == name && p.Role == GuiRole.StructuredBlock);

    /// <summary>Angebotene Blöcke dieses Elements und aller Kinder, ohne Doppelte.</summary>
    public IReadOnlyList<string> AllOfferedBlocks()
    {
        var result = new List<string>();
        var stack = new Stack<ResolvedElement>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            foreach (var block in element.OfferedBlocks)
            {
                if (!result.Contains(block))
                {
                    result.Add(block);
                }
            }

            foreach (var child in element.Children)
            {
                stack.Push(child);
            }
        }

        return result;
    }
}
