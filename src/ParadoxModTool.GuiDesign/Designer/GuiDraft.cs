using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// Beginn eines Vanilla-Entwurfs: der Text des Entwurfs und die Namen der Definitionen, deren Mod-Fassung eingesetzt wurde. Ersetzt die Mod die
/// ganze Datei schon (<c>ModCopy</c>), gibt es keinen Entwurf; bearbeitet wird dann die Mod-Datei. <c>DependencyCopy</c>: Eine Abhängigkeits-Mod
/// ersetzt die ganze Datei; der Entwurf beginnt mit ihrer Fassung (Plan 3c Ruling 8).
/// </summary>
public sealed record GuiDraftStart(string Text, GuiSourceFile? ModCopy, IReadOnlyList<string> Substituted, GuiSourceFile? DependencyCopy = null);

/// <summary>
/// "Für Mod bearbeiten" (Plan-Ruling 4): Ein Entwurf beginnt mit dem Vanilla-Text, in dem jede type- und template-Definition, die die Mod schon
/// überschreibt, durch die Mod-Fassung ersetzt ist (eingerückt wie die Vanilla-Definition). Derselbe Text ist die Grundlage, gegen die
/// <see cref="GuiOverridePlanner"/> den Entwurf vergleicht; ein unveränderter Entwurf ergibt so einen leeren Plan.
/// </summary>
public static class GuiDraft
{
    public static GuiDraftStart Start(GuiSourceFile vanilla, string vanillaText, GuiIndex index)
    {
        if (ModCopyOf(vanilla, index) is { } copy)
        {
            return new GuiDraftStart(vanillaText, copy, Array.Empty<string>());
        }

        var dependencyCopy = DependencyCopyOf(vanilla, index);
        var baseText = dependencyCopy is null ? vanillaText : index.DocumentOf(dependencyCopy).Text;
        var document = GuiParser.Parse(baseText);
        var edits = new GuiTextEdits(document);
        var changes = new List<TextEdit>();
        var names = new List<string>();
        foreach (var node in Definitions(document))
        {
            if (ModDefinition(index, node) is not { } mod)
            {
                continue;
            }

            var replacement = edits.ReplacementFor(node, new GuiTextEdits(index.DocumentOf(mod.File)).Dedent(mod.Node));
            if (replacement != document.TextOf(node))
            {
                changes.Add(new TextEdit(node.Span.Start, node.Span.Length, replacement));
                names.Add(node.Name);
            }
        }

        return new GuiDraftStart(TextEdit.Apply(baseText, changes), null, names, dependencyCopy);
    }

    /// <summary>Mod-Datei mit demselben relativen Pfad: Sie ersetzt die ganze Vanilla-Datei.</summary>
    public static GuiSourceFile? ModCopyOf(GuiSourceFile vanilla, GuiIndex index) =>
        index.Files.FirstOrDefault(f => f.Layer == GuiLayer.Mod && string.Equals(f.RelativePath, vanilla.RelativePath, StringComparison.OrdinalIgnoreCase));

    /// <summary>Datei der zuletzt geladenen Abhängigkeits-Mod mit demselben relativen Pfad: Sie ersetzt die ganze Vanilla-Datei. Nur für Vanilla-Quellen.</summary>
    public static GuiSourceFile? DependencyCopyOf(GuiSourceFile vanilla, GuiIndex index) =>
        vanilla.Layer != GuiLayer.Vanilla
            ? null
            : index.Files.LastOrDefault(f => f.Layer == GuiLayer.Dependency && string.Equals(f.RelativePath, vanilla.RelativePath, StringComparison.OrdinalIgnoreCase));

    /// <summary>Text, von dem ein Entwurf ausgeht (vor dem Einsetzen der Mod-Fassungen): die Abhängigkeits-Kopie, sonst der Vanilla-Text.</summary>
    public static string BaseText(GuiSourceFile vanilla, string vanillaText, GuiIndex index) =>
        DependencyCopyOf(vanilla, index) is { } copy ? index.DocumentOf(copy).Text : vanillaText;

    /// <summary>
    /// Zeile in einer Mod-Kopie, die der Stelle <paramref name="offset"/> im Vanilla-Text entspricht (3b-Nacharbeit): dasselbe Element über seinen
    /// Pfad, sonst die gleichnamige äußerste Definition, sonst 1.
    /// </summary>
    public static int LineInCopy(GuiDocument vanilla, int offset, GuiDocument copy)
    {
        if (vanilla.Text.Length == 0 || vanilla.FindDeepestAt(Math.Clamp(offset, 0, vanilla.Text.Length - 1)) is not { } node)
        {
            return 1;
        }

        if (GuiNodePath.Of(node).FindExact(copy) is { } same)
        {
            return same.Line;
        }

        var definition = new[] { node }.Concat(node.Ancestors())
            .LastOrDefault(n => n.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition);
        return definition is not null && Definitions(copy).FirstOrDefault(d => d.Kind == definition.Kind && d.Name == definition.Name) is { } match
            ? match.Line
            : 1;
    }

    /// <summary>type- und template-Definitionen der Datei, die nicht in einer anderen Definition stehen, in Textreihenfolge.</summary>
    public static IReadOnlyList<GuiNode> Definitions(GuiDocument document) =>
        document.Root.Descendants()
            .Where(n => n.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition
                && !n.Ancestors().Any(a => a.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition))
            .ToList();

    public static GuiDefinitionKind KindOf(GuiNode definition) =>
        definition.Kind == GuiNodeKind.TypeDefinition ? GuiDefinitionKind.Type : GuiDefinitionKind.Template;

    /// <summary>
    /// Wirksame Mod-Definition gleicher Art und gleichen Namens: in der Mod die zuletzt geladene, sonst in einer Abhängigkeits-Mod (Spec: Mod vor
    /// Abhängigkeit vor Vanilla); bei <see cref="GuiLayout.FirstWins"/> die erste nach Pfad über Mod und Abhängigkeiten zusammen. Null, wenn keine Mod sie hat.
    /// </summary>
    public static GuiDefinition? ModDefinition(GuiIndex index, GuiNode definition)
    {
        var all = index.FindAll(definition.Name, KindOf(definition));
        if (index.Layout.FirstWins)
        {
            // Eigene Mod und Abhängigkeiten zählen gemeinsam nach Pfad (siehe GuiIndex.FirstLoaded).
            return GuiIndex.FirstLoaded(all.Where(d => d.File.Layer != GuiLayer.Vanilla));
        }

        return all.LastOrDefault(d => d.File.Layer == GuiLayer.Mod) ?? all.LastOrDefault(d => d.File.Layer == GuiLayer.Dependency);
    }
}
