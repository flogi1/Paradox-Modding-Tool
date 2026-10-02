using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Index;

public enum GuiDefinitionKind
{
    Type,
    Template,
    NamedWidget,
}

/// <summary>
/// Eine Definition im Index. <c>Blocks</c> sind die Namen aller <c>block "…"</c> im Inhalt (nur bei Typen und Templates).
/// <c>IsTopLevel</c>: Die Definition steht direkt auf oberster Dateiebene (Widgets dort lassen sich nur mit der ganzen Datei ersetzen).
/// </summary>
public sealed record GuiDefinition(
    string Name,
    GuiDefinitionKind Kind,
    GuiSourceFile File,
    GuiNode Node,
    string? BaseName,
    string? WidgetKey,
    bool IsTopLevel,
    string? TypesContainer,
    IReadOnlyList<string> Blocks)
{
    public int Line => Node.Line;
}

public enum GuiUsageKind
{
    Widget,
    Using,
}

public sealed record GuiUsage(string Name, GuiUsageKind Kind, GuiSourceFile File, GuiNode Node);

public sealed record GuiFileDiagnostic(GuiSourceFile File, GuiDiagnostic Diagnostic);

/// <summary>
/// Alle geparsten .gui-Dateien aus Vanilla, Abhängigkeits-Mods und aktueller Mod mit ihren Definitionen und Verwendungen.
/// Unveränderlich; <see cref="WithFile"/> liefert einen neuen Index.
/// </summary>
public sealed class GuiIndex
{
    private readonly Dictionary<GuiSourceFile, GuiDocument> _documents = new();
    private readonly Dictionary<GuiNode, GuiSourceFile> _fileByRoot = new();
    private readonly Dictionary<(GuiDefinitionKind Kind, string Name), List<GuiDefinition>> _definitions = new();
    private readonly Dictionary<string, List<GuiUsage>> _usages = new(StringComparer.Ordinal);
    private readonly List<GuiFileDiagnostic> _diagnostics = new();

    private GuiIndex(IReadOnlyList<GuiSourceFile> files, IReadOnlyList<GuiDocument> documents, GuiLayout? layout = null)
    {
        Files = files;
        Layout = layout ?? GuiLayout.Default;
        for (var i = 0; i < files.Count; i++)
        {
            _documents[files[i]] = documents[i];
            _fileByRoot[documents[i].Root] = files[i];
            foreach (var diagnostic in documents[i].Diagnostics)
            {
                _diagnostics.Add(new GuiFileDiagnostic(files[i], diagnostic));
            }

            Collect(files[i], documents[i]);
        }
    }

    public static GuiIndex Empty { get; } = new(Array.Empty<GuiSourceFile>(), Array.Empty<GuiDocument>());

    /// <summary>Parst alle Dateien parallel. <paramref name="readText"/> ersetzt das Lesen von der Platte (Tests).</summary>
    public static GuiIndex Build(IReadOnlyList<GuiSourceFile> files, Func<GuiSourceFile, string>? readText = null, GuiLayout? layout = null)
    {
        readText ??= file => File.ReadAllText(file.FullPath);
        var documents = new GuiDocument[files.Count];
        Parallel.For(0, files.Count, i => documents[i] = GuiParser.Parse(readText(files[i])));
        return new GuiIndex(files, documents, layout);
    }

    /// <summary>Index aus bereits geparsten Dokumenten, z.B. Vanilla aus einem früheren Aufbau plus neu geparste Mod-Dateien.</summary>
    public static GuiIndex FromDocuments(IReadOnlyList<GuiSourceFile> files, IReadOnlyList<GuiDocument> documents, GuiLayout? layout = null)
    {
        if (files.Count != documents.Count)
        {
            throw new ArgumentException("Gleich viele Dateien und Dokumente erwartet.", nameof(documents));
        }

        return new GuiIndex(files, documents, layout);
    }

    public IReadOnlyList<GuiSourceFile> Files { get; }

    public IReadOnlyList<GuiFileDiagnostic> Diagnostics => _diagnostics;

    public IEnumerable<GuiDefinition> Definitions => _definitions.Values.SelectMany(list => list);

    public GuiDocument DocumentOf(GuiSourceFile file) => _documents[file];

    public GuiDocument? DocumentOf(GuiNode node) => FileOf(node) is { } file ? _documents[file] : null;

    public GuiSourceFile? FileOf(GuiNode node) => _fileByRoot.TryGetValue(node.Root, out var file) ? file : null;

    /// <summary>Kontextordner und Überschreib-Reihenfolge des Spiels, zu dem die Dateien gehören.</summary>
    public GuiLayout Layout { get; }

    /// <summary>
    /// Wirksame Definition: höchste Ebene (Mod vor Abhängigkeit vor Vanilla), innerhalb einer Ebene die zuletzt geladene, bei
    /// <see cref="GuiLayout.FirstWins"/> (Crusader Kings III) die zuerst geladene. Bei FirstWins bilden eigene Mod und Abhängigkeits-Mods eine
    /// gemeinsame Ebene, sortiert nach relativem Pfad (gemessen 2026-09-30: die Playset-Reihenfolge zählt nicht); gleicher Pfad in zwei Mods = die
    /// eigene Mod, deren Datei die andere ersetzt.
    /// </summary>
    public GuiDefinition? Find(string name, GuiDefinitionKind kind) =>
        !_definitions.TryGetValue((kind, name), out var list) ? null
        : Layout.FirstWins ? FirstLoaded(list)
        : list.OrderBy(d => d.File.Layer).Last();

    /// <summary>Wirksame Definition bei FirstWins: zuerst die Mods (eigene und Abhängigkeiten zusammen, nach Pfad), sonst Vanilla in Ladereihenfolge.</summary>
    public static GuiDefinition? FirstLoaded(IEnumerable<GuiDefinition> definitions)
    {
        var all = definitions.ToList();
        return all
            .Where(d => d.File.Layer != GuiLayer.Vanilla)
            .OrderBy(d => d.File.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(d => d.File.Layer)
            .FirstOrDefault()
            ?? all.FirstOrDefault(d => d.File.Layer == GuiLayer.Vanilla);
    }

    public IReadOnlyList<GuiDefinition> FindAll(string name, GuiDefinitionKind kind) =>
        _definitions.TryGetValue((kind, name), out var list) ? list : Array.Empty<GuiDefinition>();

    public IReadOnlyList<GuiUsage> UsagesOf(string name) =>
        _usages.TryGetValue(name, out var list) ? list : Array.Empty<GuiUsage>();

    /// <summary>Typen und Templates, die in der aktuellen Mod mehr als einmal definiert sind (je Gruppe gleicher Art und gleichen Namens).</summary>
    public IReadOnlyList<IReadOnlyList<GuiDefinition>> ModDuplicates =>
        _definitions
            .Where(pair => pair.Key.Kind != GuiDefinitionKind.NamedWidget)
            .Select(pair => (IReadOnlyList<GuiDefinition>)pair.Value.Where(d => d.File.Layer == GuiLayer.Mod).ToList())
            .Where(group => group.Count > 1)
            .ToList();

    /// <summary>Neuer Index, in dem die Datei (gleicher voller Pfad) durch den Text ersetzt bzw. am Ende ergänzt ist.</summary>
    public GuiIndex WithFile(GuiSourceFile file, string text)
    {
        var files = new List<GuiSourceFile>();
        var documents = new List<GuiDocument>();
        var replaced = false;
        foreach (var existing in Files)
        {
            if (string.Equals(existing.FullPath, file.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                files.Add(file);
                documents.Add(GuiParser.Parse(text));
                replaced = true;
            }
            else
            {
                files.Add(existing);
                documents.Add(_documents[existing]);
            }
        }

        if (!replaced)
        {
            files.Add(file);
            documents.Add(GuiParser.Parse(text));
        }

        return new GuiIndex(files, documents, Layout);
    }

    private void Collect(GuiSourceFile file, GuiDocument document)
    {
        foreach (var node in document.Root.Descendants())
        {
            switch (node.Kind)
            {
                case GuiNodeKind.TypeDefinition:
                    Add(new GuiDefinition(
                        node.Name, GuiDefinitionKind.Type, file, node, node.BaseName, null, false,
                        node.Parent is { Kind: GuiNodeKind.TypesContainer } container ? container.Name : null,
                        BlocksIn(node)));
                    break;

                case GuiNodeKind.TemplateDefinition:
                    Add(new GuiDefinition(
                        node.Name, GuiDefinitionKind.Template, file, node, null, null,
                        node.Parent?.Kind == GuiNodeKind.Document, null, BlocksIn(node)));
                    break;

                case GuiNodeKind.KeyedBlock when node.Name.Length > 0:
                    AddUsage(new GuiUsage(node.Name, GuiUsageKind.Widget, file, node));
                    var name = node.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.Property && c.Name == "name");
                    if (name is { Value.Length: > 0 })
                    {
                        Add(new GuiDefinition(
                            name.Value, GuiDefinitionKind.NamedWidget, file, node, null, node.Name,
                            node.Parent?.Kind == GuiNodeKind.Document, null, Array.Empty<string>()));
                    }

                    break;

                case GuiNodeKind.Using when !string.IsNullOrEmpty(node.Value):
                    AddUsage(new GuiUsage(node.Value, GuiUsageKind.Using, file, node));
                    break;
            }
        }
    }

    private void Add(GuiDefinition definition)
    {
        var key = (definition.Kind, definition.Name);
        if (!_definitions.TryGetValue(key, out var list))
        {
            list = new List<GuiDefinition>();
            _definitions[key] = list;
        }

        list.Add(definition);
    }

    private void AddUsage(GuiUsage usage)
    {
        if (!_usages.TryGetValue(usage.Name, out var list))
        {
            list = new List<GuiUsage>();
            _usages[usage.Name] = list;
        }

        list.Add(usage);
    }

    private static IReadOnlyList<string> BlocksIn(GuiNode definition) =>
        definition.Descendants()
            .Where(n => n.Kind == GuiNodeKind.Block)
            .Select(n => n.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
