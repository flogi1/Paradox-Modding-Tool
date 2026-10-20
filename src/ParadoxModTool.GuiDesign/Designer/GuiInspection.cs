using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Was der Inspektor zeigen soll: ein Element (Typ- oder Template-Definition oder Widget) in seinem Dokument.</summary>
public sealed record GuiInspectTarget(GuiNode Node, GuiDocument Document, string? FilePath);

public enum GuiInspectKind
{
    Type,
    Template,
    Widget,
}

/// <summary>Verweis im geschriebenen Code: ein Typ als Widget-Schlüssel oder ein Template über <c>using</c>.</summary>
public sealed record GuiReference(string Name, GuiDefinitionKind Kind, GuiNode At);

/// <summary>Ein angebotener Block und die Stellen im Spiel, die ihn überschreiben.</summary>
public sealed record GuiOfferedBlock(string Name, IReadOnlyList<GuiUsage> OverriddenAt);

/// <summary>
/// Alles, was der GUI-Inspektor zu einem Element zeigt: aufgelöstes Element, Originaltext, aufgelöster Code, Verweise und Beziehungen
/// (Typkette, abgeleitete Typen, angebotene Blöcke mit ihren Überschreibungen, Verwendungen im Spiel). Beziehungen kommen aus dem Index,
/// also aus dem gespeicherten Stand (Plan-Ruling 11). Rechnet im Hintergrund; nichts davon ändert Text.
/// </summary>
public sealed class GuiInspection
{
    public required GuiInspectTarget Target { get; init; }

    public required GuiInspectKind Kind { get; init; }

    public required string Title { get; init; }

    /// <summary>Typ bzw. Template, dessen Beziehungen gezeigt werden (bei einer Typ-Instanz ihr Typ); null bei Engine-Widgets und Haltern.</summary>
    public string? Subject { get; init; }

    public GuiDefinitionKind SubjectKind { get; init; } = GuiDefinitionKind.Type;

    public required string WrittenText { get; init; }

    /// <summary>Aufgelöstes Element; null bei Templates (sie sind Eigenschaftssammlungen ohne eigenes Widget).</summary>
    public ResolvedElement? Element { get; init; }

    public IReadOnlyList<string> TypeChain { get; init; } = Array.Empty<string>();

    public IReadOnlyList<GuiDefinition> DerivedTypes { get; init; } = Array.Empty<GuiDefinition>();

    public IReadOnlyList<GuiOfferedBlock> OfferedBlocks { get; init; } = Array.Empty<GuiOfferedBlock>();

    public IReadOnlyList<GuiUsage> Usages { get; init; } = Array.Empty<GuiUsage>();

    public IReadOnlyList<GuiReference> References { get; init; } = Array.Empty<GuiReference>();

    public string? ResolvedCode { get; init; }

    public static GuiInspection Create(GuiResolver resolver, GuiIndex index, GuiInspectTarget target, CancellationToken cancellation = default)
    {
        var node = target.Node;
        var kind = node.Kind switch
        {
            GuiNodeKind.TypeDefinition => GuiInspectKind.Type,
            GuiNodeKind.TemplateDefinition => GuiInspectKind.Template,
            _ => GuiInspectKind.Widget,
        };
        var element = kind switch
        {
            GuiInspectKind.Type => resolver.ResolveType(node.Name),
            GuiInspectKind.Template => null,
            _ => resolver.Resolve(node),
        };
        cancellation.ThrowIfCancellationRequested();

        var subject = kind != GuiInspectKind.Widget || resolver.Classify(node) == GuiRole.TypeInstance ? node.Name : null;
        var subjectKind = kind == GuiInspectKind.Template ? GuiDefinitionKind.Template : GuiDefinitionKind.Type;
        var chain = kind == GuiInspectKind.Template ? Array.Empty<string>() : element?.TypeChain ?? resolver.TypeChain(node.Name);
        var usages = subject is null ? Array.Empty<GuiUsage>() : UsagesOf(index, subject, subjectKind);
        cancellation.ThrowIfCancellationRequested();

        return new GuiInspection
        {
            Target = target,
            Kind = kind,
            Title = GuiElementTree.LabelOf(node),
            Subject = subject,
            SubjectKind = subjectKind,
            WrittenText = target.Document.TextOf(node),
            Element = element,
            TypeChain = chain,
            DerivedTypes = subject is null || subjectKind != GuiDefinitionKind.Type ? Array.Empty<GuiDefinition>() : DerivedOf(index, subject),
            OfferedBlocks = subject is null
                ? Array.Empty<GuiOfferedBlock>()
                : BlocksOf(resolver, subject, subjectKind, chain).Select(block => new GuiOfferedBlock(block, OverridesOf(usages, block))).ToList(),
            Usages = usages,
            References = ReferencesIn(node, resolver),
            ResolvedCode = element is null ? null : GuiResolvedCode.Write(element),
        };
    }

    /// <summary>
    /// Ziel für einen Typ- oder Template-Namen. Steht das gezeigte Element in einer Datei außerhalb des Index (ungespeichert oder fremd),
    /// wird der Name zuerst dort gesucht; sonst gilt die wirksame Definition im Index. Null, wenn der Name nirgends definiert ist.
    /// </summary>
    public static GuiInspectTarget? TargetFor(GuiIndex index, string name, GuiDefinitionKind kind, GuiInspectTarget? context = null)
    {
        if (context is not null && index.FileOf(context.Node) is null)
        {
            var nodeKind = kind == GuiDefinitionKind.Template ? GuiNodeKind.TemplateDefinition : GuiNodeKind.TypeDefinition;
            var local = context.Document.Root.Descendants().FirstOrDefault(n => n.Kind == nodeKind && n.Name == name);
            if (local is not null)
            {
                return new GuiInspectTarget(local, context.Document, context.FilePath);
            }
        }

        return index.Find(name, kind) is { } definition ? TargetFor(index, definition) : null;
    }

    public static GuiInspectTarget TargetFor(GuiIndex index, GuiDefinition definition) =>
        new(definition.Node, index.DocumentOf(definition.File), definition.File.FullPath);

    /// <summary>Ziel für eine Verwendung: das Widget selbst bzw. bei <c>using</c> das Element, das das Template einbindet.</summary>
    public static GuiInspectTarget TargetFor(GuiIndex index, GuiUsage usage) =>
        new(usage.Kind == GuiUsageKind.Using && usage.Node.Parent is { } host ? host : usage.Node, index.DocumentOf(usage.File), usage.File.FullPath);

    /// <summary>Kurzbeschreibung der Umgebung einer Stelle: die umschließende Definition bzw. das Widget auf oberster Dateiebene.</summary>
    public static string ContextOf(GuiNode node) => ContextOf(node, GuiLayout.Default);

    /// <summary>Wie <see cref="ContextOf(GuiNode)"/>; klassisch das Fenster unter <c>guiTypes</c> (siehe <see cref="GuiPreviewBuilder.PreviewTarget(GuiNode, GuiLayout)"/>).</summary>
    public static string ContextOf(GuiNode node, GuiLayout layout) => GuiElementTree.LabelOf(GuiPreviewBuilder.PreviewTarget(node, layout) ?? node);

    private static IReadOnlyList<GuiUsage> UsagesOf(GuiIndex index, string subject, GuiDefinitionKind kind) =>
        index.UsagesOf(subject)
            .Where(u => kind == GuiDefinitionKind.Template ? u.Kind == GuiUsageKind.Using : u.Kind == GuiUsageKind.Widget)
            .OrderBy(u => u.File.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(u => u.Node.Line)
            .ToList();

    /// <summary>Typen, deren Basis direkt dieser Typ ist; je Name die wirksame Definition (Mod vor Vanilla), ohne Doppelte.</summary>
    private static IReadOnlyList<GuiDefinition> DerivedOf(GuiIndex index, string subject) =>
        index.Definitions
            .Where(d => d.Kind == GuiDefinitionKind.Type && d.BaseName == subject && d.Name != subject)
            .Select(d => d.Name)
            .Distinct(StringComparer.Ordinal)
            .Select(name => index.Find(name, GuiDefinitionKind.Type)!)
            .Where(d => d.BaseName == subject)
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Namen aller <c>block</c> im Template bzw. in den Definitionen der Typkette, ohne Doppelte, in Textreihenfolge.</summary>
    private static IReadOnlyList<string> BlocksOf(GuiResolver resolver, string subject, GuiDefinitionKind kind, IReadOnlyList<string> chain)
    {
        var definitions = kind == GuiDefinitionKind.Template
            ? new[] { resolver.FindTemplate(subject) }
            : chain.Select(resolver.FindType).ToArray();
        return definitions
            .OfType<GuiNode>()
            .Distinct()
            .SelectMany(d => d.Descendants())
            .Where(n => n.Kind == GuiNodeKind.Block)
            .Select(n => n.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Verwendungen, die den Block überschreiben: <c>blockoverride</c> in der Typ-Instanz bzw. neben dem <c>using</c>.</summary>
    private static IReadOnlyList<GuiUsage> OverridesOf(IReadOnlyList<GuiUsage> usages, string block) =>
        usages
            .Where(u => (u.Kind == GuiUsageKind.Widget ? u.Node : u.Node.Parent) is { } host
                && host.Children.Any(c => c.Kind == GuiNodeKind.BlockOverride && c.Name == block))
            .ToList();

    private static IReadOnlyList<GuiReference> ReferencesIn(GuiNode node, GuiResolver resolver)
    {
        var references = new List<GuiReference>();
        var seen = new HashSet<(string, GuiDefinitionKind)>();
        foreach (var descendant in node.Descendants())
        {
            if (descendant.Kind == GuiNodeKind.KeyedBlock
                && resolver.Classify(descendant) == GuiRole.TypeInstance
                && seen.Add((descendant.Name, GuiDefinitionKind.Type)))
            {
                references.Add(new GuiReference(descendant.Name, GuiDefinitionKind.Type, descendant));
            }
            else if (descendant.Kind == GuiNodeKind.Using
                && descendant.Value is { Length: > 0 } template
                && seen.Add((template, GuiDefinitionKind.Template)))
            {
                references.Add(new GuiReference(template, GuiDefinitionKind.Template, descendant));
            }
        }

        return references;
    }
}
