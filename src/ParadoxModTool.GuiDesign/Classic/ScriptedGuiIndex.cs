using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Classic;

/// <summary>
/// Ein Schlüssel eines Scripted GUI, der ein Element meint. <paramref name="Section"/> ist <c>effects</c>, <c>triggers</c>, <c>properties</c> oder
/// <c>dynamic_lists</c>; <paramref name="ElementName"/> ist der Name des Elements (in der Schreibweise der .gui-Datei), <see langword="null"/>, wenn kein
/// bekannter Name passt. <paramref name="Line"/> ist die Zeile des Schlüssels in der Skriptdatei.
/// </summary>
public sealed record ScriptedGuiBinding(string Key, string? ElementName, string Section, int Line);

/// <summary>
/// Ein Eintrag unter <c>scripted_gui = { … }</c> (HOI IV). <paramref name="Line"/> ist die Zeile des Eintragsnamens. <paramref name="EntryContainers"/> sind
/// die Werte von <c>entry_container</c> und <c>*_scope_entry_container</c> der <c>dynamic_lists</c>, wie sie im Skript stehen (ohne Anführungszeichen); ein
/// Wert mit <c>[…]</c> wird erst im Spiel ausgewertet. <paramref name="IsMod"/>: der Eintrag kommt aus der Mod selbst.
/// </summary>
public sealed record ScriptedGuiEntry(
    string Name, string ContextType, string? WindowName, string? ParentToken, string FilePath, int Line,
    IReadOnlyList<ScriptedGuiBinding> Bindings, IReadOnlyList<string> EntryContainers, bool IsMod)
{
    /// <summary>Ein Einstiegsfenster wird erst im Spiel bestimmt (<c>[…]</c>): Elemente darin kennt PMT nicht, unbekannte Namen sind dann kein Befund.</summary>
    public bool HasDynamicContainer => EntryContainers.Any(ScriptedGuiIndex.IsDynamic);
}

/// <summary>Eine Entscheidungskategorie, die mit <c>scripted_gui = &lt;Eintrag&gt;</c> ein Scripted GUI einbindet: ihr Name und ihre Stelle.</summary>
public sealed record ScriptedGuiCategory(string Name, string FilePath, int Line);

/// <summary>
/// Lesesicht auf die obersten klassischen Fenster und die Namen ihrer Nachfahren, gebaut aus dem vorhandenen Index (<see cref="ClassicWindows"/>). Je Name
/// zählt das Fenster, das nach der Regel des Spiels gilt. Groß-/Kleinschreibung zählt nicht. Die Namen eines Fensters werden beim ersten Zugriff gesammelt.
/// </summary>
public sealed class ClassicWindowIndexView
{
    private readonly Dictionary<string, GuiNode> _windows;
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<(GuiSourceFile File, GuiDocument Document)> _documents;
    private readonly Lazy<(Dictionary<string, HashSet<string>> Types, HashSet<string> Scripted)> _elements;

    private ClassicWindowIndexView(Dictionary<string, GuiNode> windows, IReadOnlyList<(GuiSourceFile File, GuiDocument Document)> documents)
    {
        _windows = windows;
        _documents = documents;
        _elements = new Lazy<(Dictionary<string, HashSet<string>> Types, HashSet<string> Scripted)>(CollectElements);
    }

    public static ClassicWindowIndexView Empty { get; } = new(
        new Dictionary<string, GuiNode>(StringComparer.OrdinalIgnoreCase), Array.Empty<(GuiSourceFile, GuiDocument)>());

    /// <summary>Die geltenden obersten Fenster des Index (Ladereihenfolge nach <see cref="GuiIndex.Layout"/>).</summary>
    public static ClassicWindowIndexView From(GuiIndex index)
    {
        var windows = new Dictionary<string, GuiNode>(StringComparer.OrdinalIgnoreCase);
        var groups = index.Definitions
            .Where(d => d.Kind == GuiDefinitionKind.NamedWidget && d.IsTopLevel && ClassicWindows.IsWindow(d.Node))
            .GroupBy(d => ClassicWindows.NameOf(d.Node), StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            if (ClassicWindows.Pick(index.Layout, group) is { } winner)
            {
                windows[group.Key] = winner.Node;
            }
        }

        return new ClassicWindowIndexView(windows, index.Files.Select(f => (f, index.DocumentOf(f))).ToList());
    }

    /// <summary>Gibt es ein oberstes Fenster dieses Namens?</summary>
    public bool HasWindow(string name) => _windows.ContainsKey(name);

    /// <summary>
    /// (EU IV, Custom GUI) Die obersten Fenster aller <c>.gui</c>-Dateien mit diesem Dateinamen (ohne Ordner, ohne Rücksicht auf Groß-/Kleinschreibung), aus
    /// jeder Ebene (mit <paramref name="vanillaOnly"/> nur aus Vanilla), in Dateireihenfolge; ein Name, der schon vorkam, zählt nicht noch einmal.
    /// </summary>
    public IReadOnlyList<GuiNode> TopLevelInFile(string fileName, bool vanillaOnly = false)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return _documents
            .Where(d => !vanillaOnly || d.File.Layer == GuiLayer.Vanilla)
            .Where(d => string.Equals(Path.GetFileName(d.File.RelativePath), fileName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(d => ClassicWindows.Of(d.Document))
            .Where(w => seen.Add(ClassicWindows.NameOf(w)))
            .ToList();
    }

    /// <summary>
    /// (EU IV, Custom GUI) Die Typen (z. B. <c>guiButtonType</c>) aller benannten Blöcke dieses Namens in irgendeiner <c>.gui</c>-Datei, jede Tiefe, jede
    /// Ebene; leer, wenn kein Element so heißt. Beim ersten Zugriff einmal gesammelt.
    /// </summary>
    public IReadOnlyCollection<string> ElementTypes(string name) =>
        _elements.Value.Types.TryGetValue(name, out var types) ? types : Array.Empty<string>();

    /// <summary>(EU IV, Custom GUI) Hat irgendein Element dieses Namens (jede Datei, jede Tiefe, jede Ebene) <c>scripted = yes</c>?</summary>
    public bool HasScriptedElement(string name) => _elements.Value.Scripted.Contains(name);

    private (Dictionary<string, HashSet<string>> Types, HashSet<string> Scripted) CollectElements()
    {
        var elements = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var scripted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, document) in _documents)
        {
            foreach (var node in document.Root.Descendants().Where(n => n.Kind == GuiNodeKind.KeyedBlock))
            {
                var name = ClassicWindows.NameOf(node);
                if (name.Length == 0)
                {
                    continue;
                }

                if (!elements.TryGetValue(name, out var types))
                {
                    elements[name] = types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                types.Add(node.Name);
                if (node.Children.Any(c => c.Kind == GuiNodeKind.Property
                    && string.Equals(c.Name, "scripted", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(c.Value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase)))
                {
                    scripted.Add(name);
                }
            }
        }

        return (elements, scripted);
    }

    /// <summary>Die Namen aller Elemente im Fenster (jede Tiefe, ohne das Fenster selbst); leer, wenn es das Fenster nicht gibt.</summary>
    public IReadOnlyList<string> NamesUnder(string windowName) =>
        _windows.TryGetValue(windowName, out var window)
            ? _names.GetOrAdd(windowName, _ => window.Descendants()
                .Where(n => n.Kind == GuiNodeKind.KeyedBlock)
                .Select(ClassicWindows.NameOf)
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList())
            : Array.Empty<string>();
}

/// <summary>
/// Scripted GUIs von HOI IV aus <c>common/scripted_guis/*.txt</c> (Vanilla, Abhängigkeiten, Mod) und die Entscheidungskategorien, die sie einbinden
/// (<c>common/decisions/categories/*.txt</c>, Schlüssel <c>scripted_gui</c>). Gelesen mit dem fehlertoleranten <see cref="GuiParser"/> (Zeilennummern,
/// Operatoren, halb bearbeitete Dateien). Eine Datei mit demselben relativen Pfad in einer späteren Wurzel ersetzt die frühere ganz; danach gewinnt bei
/// gleichem Eintragsnamen die zuletzt geladene Definition. Elementnamen werden nach den bekannten Endungen aufgelöst (längster Name zuerst, ohne Rücksicht
/// auf Groß-/Kleinschreibung), gegen die Namen im Fenster und in allen benannten Einstiegsfenstern; ein Schlüssel wird nie an <c>_</c> zerlegt.
/// Unveränderlich; <see cref="WithWindows"/> löst die schon gelesenen Einträge gegen neue Fenster auf.
/// </summary>
public sealed class ScriptedGuiIndex
{
    public const string ScriptFolder = "common/scripted_guis";

    public const string CategoryFolder = "common/decisions/categories";

    public const string DecisionCategory = "decision_category";

    private static readonly string[] Sections = { "effects", "triggers", "properties", "dynamic_lists" };

    /// <summary>Endungen hinter einem Elementnamen in <c>effects</c> und <c>triggers</c> (Doku und Vanilla).</summary>
    private static readonly Regex Suffix = new(
        @"^(?:(?:_(?:right|alt|control|shift))*_click|_click_enabled|_visible)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<RawEntry> _raw;

    private ScriptedGuiIndex(
        IReadOnlyList<RawEntry> raw, IReadOnlyDictionary<string, ScriptedGuiCategory> categories, IReadOnlySet<string> categoryIds, ClassicWindowIndexView windows)
    {
        _raw = raw;
        Categories = categories;
        CategoryIds = categoryIds;
        Windows = windows;
        Entries = raw.Select(r => Resolve(r, windows)).ToList();
    }

    public static ScriptedGuiIndex Empty { get; } = new(
        Array.Empty<RawEntry>(), new Dictionary<string, ScriptedGuiCategory>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase), ClassicWindowIndexView.Empty);

    /// <summary>Alle geltenden Einträge in Ladereihenfolge.</summary>
    public IReadOnlyList<ScriptedGuiEntry> Entries { get; }

    /// <summary>Kategorien nach dem Eintragsnamen, den sie mit <c>scripted_gui</c> nennen (nicht nach <c>window_name</c>); bei mehreren die zuerst geladene.</summary>
    public IReadOnlyDictionary<string, ScriptedGuiCategory> Categories { get; }

    /// <summary>Die Namen aller Entscheidungskategorien (oberste Blöcke in <c>common/decisions/categories</c>, jede Wurzel), ohne Rücksicht auf Groß-/Kleinschreibung.</summary>
    public IReadOnlySet<string> CategoryIds { get; }

    /// <summary>Die Fenster, gegen die die Namen aufgelöst sind.</summary>
    public ClassicWindowIndexView Windows { get; }

    /// <summary>
    /// <paramref name="rootsInLoadOrder"/> wie <see cref="GuiSourceSet.ClassicRootsFor"/> (Vanilla mit DLC-Ordnern, Abhängigkeiten, Mod); die Ebene einer
    /// Wurzel bestimmt <see cref="ScriptedGuiEntry.IsMod"/>. <paramref name="caseSensitive"/>: Dateinamen der Ladereihenfolge mit Groß-/Kleinschreibung
    /// (HOI IV, <c>loadOrderCaseSensitive</c>).
    /// </summary>
    public static ScriptedGuiIndex Build(IReadOnlyList<GuiSourceRoot> rootsInLoadOrder, ClassicWindowIndexView windows, bool caseSensitive = false)
    {
        var raw = new Dictionary<string, RawEntry>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var file in FilesOf(rootsInLoadOrder, ScriptFolder, caseSensitive))
        {
            foreach (var entry in ReadEntries(file))
            {
                if (raw.ContainsKey(entry.Name))
                {
                    order.RemoveAll(n => string.Equals(n, entry.Name, StringComparison.OrdinalIgnoreCase));
                }

                raw[entry.Name] = entry;
                order.Add(entry.Name);
            }
        }

        var categories = new Dictionary<string, ScriptedGuiCategory>(StringComparer.OrdinalIgnoreCase);
        var categoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in FilesOf(rootsInLoadOrder, CategoryFolder, caseSensitive))
        {
            foreach (var category in Parse(file.FullPath).Root.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock))
            {
                if (category.Name.Length > 0)
                {
                    categoryIds.Add(category.Name);
                }

                foreach (var reference in category.Children.Where(c => IsProperty(c, "scripted_gui")))
                {
                    var name = (reference.Value ?? string.Empty).Trim();
                    if (name.Length > 0 && !categories.ContainsKey(name))
                    {
                        categories[name] = new ScriptedGuiCategory(category.Name, file.FullPath, category.Line);
                    }
                }
            }
        }

        return new ScriptedGuiIndex(order.Select(n => raw[n]).ToList(), categories, categoryIds, windows);
    }

    /// <summary>Dieselben Einträge, gegen <paramref name="windows"/> aufgelöst (nach dem Speichern einer .gui-Datei); liest keine Datei neu.</summary>
    public ScriptedGuiIndex WithWindows(ClassicWindowIndexView windows) => new(_raw, Categories, CategoryIds, windows);

    /// <summary>Einträge mit diesem <c>window_name</c> oder mit diesem Fenster als Einstiegsfenster einer <c>dynamic_lists</c>-Liste.</summary>
    public IReadOnlyList<ScriptedGuiEntry> ForWindow(string windowName) =>
        Entries.Where(e => string.Equals(e.WindowName, windowName, StringComparison.OrdinalIgnoreCase)
                || e.EntryContainers.Any(c => string.Equals(c, windowName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>Die Schlüssel, die das Element <paramref name="elementName"/> im Fenster <paramref name="windowName"/> meinen, mit ihrem Eintrag.</summary>
    public IReadOnlyList<(ScriptedGuiEntry Entry, ScriptedGuiBinding Binding)> BindingsFor(string windowName, string elementName) =>
        ForWindow(windowName)
            .SelectMany(e => e.Bindings
                .Where(b => string.Equals(b.ElementName, elementName, StringComparison.OrdinalIgnoreCase))
                .Select(b => (e, b)))
            .ToList();

    /// <summary>Ein Wert, den erst das Spiel auswertet (<c>[…]</c>).</summary>
    internal static bool IsDynamic(string value) => value.Contains('[');

    private static ScriptedGuiEntry Resolve(RawEntry raw, ClassicWindowIndexView windows)
    {
        var names = (raw.WindowName is { } window ? windows.NamesUnder(window) : Array.Empty<string>())
            .Concat(raw.Containers.Where(c => !IsDynamic(c)).SelectMany(windows.NamesUnder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .ToList();
        var bindings = raw.Keys.Select(k => new ScriptedGuiBinding(k.Key, Match(k.Key, k.Section, names), k.Section, k.Line)).ToList();
        return new ScriptedGuiEntry(raw.Name, raw.ContextType, raw.WindowName, raw.ParentToken, raw.FilePath, raw.Line, bindings, raw.Containers, raw.IsMod);
    }

    /// <summary>
    /// Der Elementname zum Schlüssel: unter <c>properties</c>/<c>dynamic_lists</c> der Name selbst, unter <c>effects</c>/<c>triggers</c> Name plus bekannte
    /// Endung. <paramref name="names"/> ist nach Länge absteigend sortiert, damit <c>a_b_click</c> zu <c>a_b</c> gehört und nicht zu <c>a</c>.
    /// </summary>
    private static string? Match(string key, string section, IReadOnlyList<string> names)
    {
        var bare = section is "properties" or "dynamic_lists";
        foreach (var name in names)
        {
            if (bare)
            {
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }

                continue;
            }

            if (key.Length > name.Length
                && key.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                && Suffix.IsMatch(key[name.Length..]))
            {
                return name;
            }
        }

        return null;
    }

    private static IEnumerable<RawEntry> ReadEntries(GuiSourceFile file)
    {
        var isMod = file.Layer == GuiLayer.Mod;
        foreach (var container in Parse(file.FullPath).Root.Children.Where(IsContainerBlock))
        {
            foreach (var node in container.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock && c.Name.Length > 0))
            {
                var keys = new List<RawKey>();
                var containers = new List<string>();
                foreach (var section in node.Children.Where(c => c.Kind == GuiNodeKind.KeyedBlock && Sections.Contains(c.Name, StringComparer.OrdinalIgnoreCase)))
                {
                    var sectionName = section.Name.ToLowerInvariant();
                    foreach (var key in section.Children.Where(c => c.Kind is GuiNodeKind.KeyedBlock or GuiNodeKind.Property && c.Name.Length > 0))
                    {
                        keys.Add(new RawKey(key.Name, sectionName, key.Line));
                        if (sectionName == "dynamic_lists")
                        {
                            containers.AddRange(key.Children
                                .Where(c => c.Kind == GuiNodeKind.Property && IsContainerKey(c.Name) && !string.IsNullOrWhiteSpace(c.Value))
                                .Select(c => c.Value!.Trim()));
                        }
                    }
                }

                yield return new RawEntry(
                    node.Name,
                    ValueOf(node, "context_type") ?? string.Empty,
                    ValueOf(node, "window_name"),
                    ValueOf(node, "parent_window_token"),
                    file.FullPath,
                    node.Line,
                    keys,
                    containers,
                    isMod);
            }
        }
    }

    private static bool IsContainerKey(string key) =>
        string.Equals(key, "entry_container", StringComparison.OrdinalIgnoreCase)
        || key.EndsWith("_scope_entry_container", StringComparison.OrdinalIgnoreCase);

    private static string? ValueOf(GuiNode node, string key) =>
        node.Children.LastOrDefault(c => IsProperty(c, key))?.Value?.Trim() is { Length: > 0 } value ? value : null;

    private static bool IsProperty(GuiNode node, string key) =>
        node.Kind == GuiNodeKind.Property && string.Equals(node.Name, key, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Der äußere Block <c>scripted_gui = { … }</c> einer Skriptdatei, ohne Rücksicht auf Groß-/Kleinschreibung; dieselbe Prüfung für Index und
    /// <see cref="ScriptedGuiDraft"/>, damit der Entwurf keinen Eintrag anlegt, den der Index nicht sieht (und umgekehrt).
    /// </summary>
    internal static bool IsContainerBlock(GuiNode node) =>
        node.Kind == GuiNodeKind.KeyedBlock && node.HasBody && string.Equals(node.Name, ContainerKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>Schlüssel des äußeren Blocks in <c>common/scripted_guis</c>.</summary>
    internal const string ContainerKey = "scripted_gui";

    /// <summary>
    /// Die <c>.txt</c>-Dateien direkt in <paramref name="folder"/> aller Wurzeln; eine Datei mit demselben relativen Pfad in einer späteren Wurzel ersetzt die
    /// frühere. Das Ergebnis ist nach Dateinamen in der Ladereihenfolge des Spiels sortiert, über alle Wurzeln hinweg (wie <see cref="CustomGuiIndex"/>):
    /// eine Vanilla-Datei <c>zz.txt</c> lädt nach einer Mod-Datei <c>aa.txt</c> und gewinnt bei gleichem Eintrag.
    /// </summary>
    private static IReadOnlyList<GuiSourceFile> FilesOf(IReadOnlyList<GuiSourceRoot> roots, string folder, bool caseSensitive)
    {
        var files = new Dictionary<string, GuiSourceFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            var directory = Path.Combine(root.Directory, folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory))
            {
                continue;
            }

            string[] paths;
            try
            {
                paths = Directory.GetFiles(directory, "*.txt", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var path in paths.Where(p => p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
            {
                var relative = folder + "/" + Path.GetFileName(path);
                files[relative] = new GuiSourceFile(path, relative, root.Layer, root.Label, 0, 0);
            }
        }

        return files.Values.OrderBy(f => f.RelativePath, GameLoadOrder.For(caseSensitive)).ToList();
    }

    private static GuiDocument Parse(string path)
    {
        try
        {
            return GuiParser.Parse(GuiTextReader.Read(path).Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return GuiParser.Parse(string.Empty);
        }
    }

    private sealed record RawKey(string Key, string Section, int Line);

    private sealed record RawEntry(
        string Name, string ContextType, string? WindowName, string? ParentToken, string FilePath, int Line,
        IReadOnlyList<RawKey> Keys, IReadOnlyList<string> Containers, bool IsMod);
}
