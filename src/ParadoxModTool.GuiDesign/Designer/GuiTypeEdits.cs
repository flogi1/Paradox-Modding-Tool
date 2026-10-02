using System.Text.RegularExpressions;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Resolve;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>Element für "Als neuen Typ in Mod übernehmen": der Knoten im gespeicherten Stand seiner Vanilla- oder Abhängigkeits-Datei.</summary>
public sealed record GuiCopySource(GuiSourceFile File, GuiDocument Document, GuiNode Node);

/// <summary>
/// Eigene Typen (Plan 3c): "Neuer Typ", "Als neuen Typ herausziehen" und "Widget-Schlüssel umstellen" als Ersetzungen gegen den aktuellen Text,
/// dazu Namensprüfung und -vorschlag. Neue Typen kommen in einen types-Container derselben Datei (Ruling 2); in einem Vanilla-Entwurf schreibt
/// "In Mod übernehmen" sie in die Typdatei der Mod.
/// </summary>
public static class GuiTypeEdits
{
    private static readonly Regex NamePattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public static bool IsValidName(string name) => NamePattern.IsMatch(name);

    /// <summary>
    /// Der Name ist vergeben (Ruling 10): gleichartige Definition im Index oder im aktuellen, womöglich ungespeicherten Text; bei Typen auch ein
    /// bekanntes Engine-Widget.
    /// </summary>
    public static bool IsTaken(string name, GuiDefinitionKind kind, GuiIndex index, GuiDocument? document, Func<string, bool> isEngineWidget) =>
        index.FindAll(name, kind).Count > 0
        || (document is not null && GuiDraft.Definitions(document).Any(d => GuiDraft.KindOf(d) == kind && d.Name == name))
        || (kind == GuiDefinitionKind.Type && isEngineWidget(name));

    /// <summary>Bereinigter Ordnername der Mod (wie bei den Zieldateien); ohne Mod "mod".</summary>
    public static string ModBase(string? modRoot) =>
        GuiModTargets.BaseName(Path.GetFileName((modRoot ?? "mod").TrimEnd('\\', '/')));

    /// <summary>Name eines neu angelegten types-Containers: derselbe wie in der Typdatei der Mod.</summary>
    public static string TypesContainerFor(string? modRoot) => GuiModTargets.For(modRoot ?? "mod").TypesContainer;

    /// <summary>Vorschlag <c>&lt;mod&gt;_&lt;stamm&gt;</c> (ohne doppelten Mod-Namen), bei Bedarf mit <c>_2</c>, <c>_3</c> und so weiter.</summary>
    public static string SuggestName(string modBase, string stem, Func<string, bool> taken)
    {
        var name = stem.StartsWith(modBase + "_", StringComparison.Ordinal) ? stem : modBase + "_" + stem;
        if (!taken(name))
        {
            return name;
        }

        for (var n = 2; ; n++)
        {
            var numbered = $"{name}_{n}";
            if (!taken(numbered))
            {
                return numbered;
            }
        }
    }

    /// <summary>Ruling 5: nur Widgets mit Block (Typ-Instanz oder Engine-Widget), nicht Halter, Eigenschaftsblöcke, Blöcke oder Definitionen.</summary>
    public static bool CanRetype(GuiNode node, GuiResolver resolver) =>
        node.Kind == GuiNodeKind.KeyedBlock && node.HasBody && resolver.Classify(node) is GuiRole.TypeInstance or GuiRole.EngineWidget;

    /// <summary>Weitere Stellen derselben Datei mit demselben Schlüssel, in Textreihenfolge (Ruling 3).</summary>
    public static IReadOnlyList<GuiNode> SameKeySpots(GuiDocument document, GuiNode instance, GuiResolver resolver) =>
        document.Root.Descendants()
            .Where(n => !ReferenceEquals(n, instance) && n.Kind == GuiNodeKind.KeyedBlock && n.Name == instance.Name && CanRetype(n, resolver))
            .ToList();

    /// <summary>Verwendungen des Schlüssels als Widget in anderen Dateien (gespeicherter Stand im Index).</summary>
    public static int UsagesElsewhere(GuiIndex index, string key, string? filePath) =>
        index.UsagesOf(key).Count(u => u.Kind == GuiUsageKind.Widget && !IsShownFile(u.File.FullPath, index, filePath));

    /// <summary>
    /// Gehört <paramref name="path"/> zum angezeigten Text von <paramref name="filePath"/>? Die Datei selbst und, bei einem Vanilla-Entwurf auf
    /// Abhängigkeits-Basis, deren Kopie in der Abhängigkeit: der Entwurf geht von ihr aus, ihre Stellen stehen schon im Text (3c-Review).
    /// </summary>
    public static bool IsShownFile(string path, GuiIndex index, string? filePath)
    {
        if (filePath is null)
        {
            return false;
        }

        if (SamePath(path, filePath))
        {
            return true;
        }

        return index.Files.FirstOrDefault(f => SamePath(f.FullPath, filePath)) is { } shown
            && GuiDraft.DependencyCopyOf(shown, index) is { } copy
            && SamePath(path, copy.FullPath);
    }

    /// <summary>blockoverride-Namen der Stelle, die kein Typ der Kette des neuen Schlüssels als <c>block</c> anbietet.</summary>
    public static IReadOnlyList<string> LostBlockOverrides(GuiNode instance, string newKey, GuiResolver resolver)
    {
        var offered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var typeName in resolver.TypeChain(newKey))
        {
            if (resolver.FindType(typeName) is { } definition)
            {
                offered.UnionWith(definition.Descendants().Where(n => n.Kind == GuiNodeKind.Block).Select(n => n.Name));
            }
        }

        return instance.Children
            .Where(c => c.Kind == GuiNodeKind.BlockOverride && !offered.Contains(c.Name))
            .Select(c => c.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Fügt eine type-Definition (Schnipsel, Einrückung als Tabs) als letztes Kind des letzten types-Containers auf oberster Ebene ein; ohne
    /// Container entsteht am Dateiende <c>types &lt;container&gt; { … }</c>.
    /// </summary>
    public static IReadOnlyList<TextEdit> InsertType(GuiDocument document, string snippet, string containerName)
    {
        var edits = new GuiTextEdits(document);
        var container = document.Root.Children.LastOrDefault(c => c.Kind == GuiNodeKind.TypesContainer && c.HasBody);
        if (container is not null)
        {
            return edits.InsertChild(container, snippet, InsertPosition.LastChild);
        }

        var indented = string.Join("\n", snippet.Replace("\r\n", "\n").Split('\n').Select(line => line.Length == 0 ? line : "\t" + line));
        return edits.InsertChild(document.Root, $"types {containerName} {{\n{indented}\n}}", InsertPosition.LastChild);
    }

    /// <summary>Fügt eine template-Definition am Dateiende auf oberster Ebene ein.</summary>
    public static IReadOnlyList<TextEdit> InsertTemplate(GuiDocument document, string snippet) =>
        new GuiTextEdits(document).InsertChild(document.Root, snippet, InsertPosition.LastChild);

    /// <summary>
    /// "Neuer Typ" (Ruling 1): legt <c>type name = schlüssel { }</c> an und stellt die Stelle und <paramref name="alsoRetype"/> darauf um. Der
    /// Cursor kommt auf die neue Definition; weitere Änderungen landen so im Typ.
    /// </summary>
    public static GuiDesignEdit Derive(GuiDocument document, GuiNode instance, string name, IReadOnlyList<GuiNode> alsoRetype, string containerName)
    {
        RequireWidget(instance);
        var textEdits = new GuiTextEdits(document);
        var all = new List<TextEdit>(InsertType(document, $"type {name} = {instance.Name} {{\n}}", containerName));
        foreach (var node in new[] { instance }.Concat(alsoRetype).Distinct())
        {
            RequireWidget(node);
            all.AddRange(textEdits.RenameKey(node, name));
        }

        return new GuiDesignEdit(all, TextEdit.PositionInResult(all, 0, all[0].Text.IndexOf("type " + name + " ", StringComparison.Ordinal)));
    }

    /// <summary>
    /// "Als neuen Typ herausziehen" (Ruling 4): <c>type name = schlüssel { … }</c> mit allem Inhalt der Stelle außer <c>name</c>; die Stelle wird
    /// zu <c>name = { name = "…" }</c> bzw. <c>name = { }</c>. Der Cursor kommt auf die Stelle.
    /// </summary>
    public static GuiDesignEdit Extract(GuiDocument document, GuiNode instance, string name, string containerName)
    {
        RequireWidget(instance);
        var textEdits = new GuiTextEdits(document);
        var nameProperty = instance.Children.FirstOrDefault(IsNameProperty);
        var moved = EnclosingConstants(instance).Concat(instance.Children.Where(c => !ReferenceEquals(c, nameProperty)));
        var snippet = $"type {name} = {instance.Name} {{{BodyOf(textEdits, moved)}\n}}";
        var stays = nameProperty is null ? "{ }" : "{ " + document.TextOf(nameProperty) + " }";
        var all = new List<TextEdit> { new(instance.Span.Start, instance.Span.Length, $"{name} = {stays}") };
        all.AddRange(InsertType(document, snippet, containerName));
        return new GuiDesignEdit(all, TextEdit.PositionInResult(all, 0, 0));
    }

    /// <summary>"Widget-Schlüssel umstellen" (Ruling 5): nur der Schlüssel ändert sich; derselbe Schlüssel ändert nichts.</summary>
    public static GuiDesignEdit Retype(GuiDocument document, GuiNode instance, string newKey)
    {
        RequireWidget(instance);
        if (instance.Name == newKey)
        {
            return GuiDesignEdit.None;
        }

        var edits = new GuiTextEdits(document).RenameKey(instance, newKey);
        return new GuiDesignEdit(edits, TextEdit.MapOffset(instance.Span.Start, edits));
    }

    /// <summary>
    /// Quelle für "Als neuen Typ in Mod übernehmen" (Ruling 7): ein Typ, Template oder Widget in einer Vanilla- oder Abhängigkeits-Datei des Index.
    /// Kommt das Element aus einem offenen Tab, gilt derselbe Knoten im gespeicherten Stand, sofern sein Text gleich ist. Ersetzt eine Abhängigkeits-Mod
    /// die ganze Vanilla-Datei, gilt dasselbe Element in ihrer Fassung. Halter und Eigenschaftsblöcke gehen nicht. Null sonst.
    /// </summary>
    public static GuiCopySource? CopySource(GuiIndex index, GuiInspectTarget target, GuiRules rules)
    {
        var file = index.FileOf(target.Node)
            ?? (target.FilePath is { } path ? index.Files.FirstOrDefault(f => SamePath(f.FullPath, path)) : null);
        if (file is not { Layer: GuiLayer.Vanilla or GuiLayer.Dependency })
        {
            return null;
        }

        var document = index.DocumentOf(file);
        var node = ReferenceEquals(target.Node.Root, document.Root) ? target.Node : GuiDesignerEdits.Rebind(target.Node, target.Document, document);
        if (node is null)
        {
            return null;
        }

        // Ersetzt eine Abhängigkeits-Mod die ganze Vanilla-Datei, gilt im Spiel ihre Fassung (Endprüfung 3c): dasselbe Element dort, sonst nichts.
        if (GuiDraft.DependencyCopyOf(file, index) is { } parent)
        {
            var parentDocument = index.DocumentOf(parent);
            var definition = node;
            node = GuiNodePath.Of(node).FindExact(parentDocument)
                ?? (definition.Kind is GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition
                    ? GuiDraft.Definitions(parentDocument).FirstOrDefault(d => d.Kind == definition.Kind && d.Name == definition.Name)
                    : null);
            if (node is null)
            {
                return null;
            }

            (file, document) = (parent, parentDocument);
        }

        // Nur Widgets: ein Halter wie tooltipwidget ergäbe einen ungültigen Typ (Endprüfung 3c).
        if (node.Kind == GuiNodeKind.KeyedBlock
            && rules.Classify(node, name => index.FindAll(name, GuiDefinitionKind.Type).Count > 0) is not (GuiRole.TypeInstance or GuiRole.EngineWidget))
        {
            return null;
        }

        return CopySnippet(document, node, "x") is not null ? new GuiCopySource(file, document, node) : null;
    }

    /// <summary>Stamm für den Namensvorschlag: bei einem Widget sein <c>name</c>, sonst der Name bzw. Schlüssel.</summary>
    public static string CopyStem(GuiNode node) =>
        node.Kind == GuiNodeKind.KeyedBlock && node.Children.FirstOrDefault(IsNameProperty)?.Value is { Length: > 0 } name ? name : node.Name;

    /// <summary>
    /// Kopie eines Elements als neue Definition (Schnipsel, Einrückung als Tabs; Ruling 7): Typ → <c>type name = basis { Inhalt }</c>, Template →
    /// <c>template name { Inhalt }</c>, Widget → <c>type name = schlüssel { Inhalt ohne name }</c>. Null bei anderen Knoten.
    /// </summary>
    public static (string Snippet, GuiDefinitionKind Kind)? CopySnippet(GuiDocument document, GuiNode node, string name)
    {
        var edits = new GuiTextEdits(document);
        return node.Kind switch
        {
            GuiNodeKind.TypeDefinition when node.BaseName is { } baseName =>
                ($"type {name} = {baseName} {{{BodyOf(edits, EnclosingConstants(node).Concat(node.Children))}\n}}", GuiDefinitionKind.Type),
            GuiNodeKind.TemplateDefinition =>
                ($"template {name} {{{BodyOf(edits, EnclosingConstants(node).Concat(node.Children))}\n}}", GuiDefinitionKind.Template),
            GuiNodeKind.KeyedBlock when node.HasBody =>
                ($"type {name} = {node.Name} {{{BodyOf(edits, EnclosingConstants(node).Concat(node.Children.Where(c => !IsNameProperty(c))))}\n}}", GuiDefinitionKind.Type),
            _ => null,
        };
    }

    /// <summary>
    /// Konstanten aus umschließenden Blöcken (nicht von der Dateiebene), die das Element braucht; Abhängigkeiten zuerst. Am neuen Ort gelten
    /// sie nicht mehr, deshalb kommen sie an den Anfang des neuen Typs (Endprüfung 3c). Konstanten der Dateiebene nimmt der Planer mit.
    /// </summary>
    internal static IReadOnlyList<GuiNode> EnclosingConstants(GuiNode node) =>
        GuiConstants.RequiredBy(node).Where(c => c.Parent is { Kind: not GuiNodeKind.Document }).ToList();

    /// <summary>Inhalt für einen neuen Definitionsblock: jedes Kind auf eigener Zeile, eine Ebene eingerückt (Tabs), mit führendem Zeilenumbruch.</summary>
    internal static string BodyOf(GuiTextEdits edits, IEnumerable<GuiNode> children) =>
        string.Concat(children.Select(child => "\n\t" + edits.Dedent(child).Replace("\n", "\n\t")));

    internal static bool IsNameProperty(GuiNode node) => node.Kind == GuiNodeKind.Property && node.Name == "name";

    internal static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static void RequireWidget(GuiNode node)
    {
        if (node.Kind != GuiNodeKind.KeyedBlock || !node.HasBody)
        {
            throw new ArgumentException($"\"{node.Name}\" ist kein Widget mit Block.", nameof(node));
        }
    }
}
