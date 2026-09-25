using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

public enum GuiPlanEntryKind
{
    /// <summary>Geänderte Definition kommt neu in die Überschreibdatei.</summary>
    Override,

    /// <summary>Geänderte Definition ersetzt ihre Fassung in einer Mod-Datei.</summary>
    Replace,

    /// <summary>Neue Definition kommt in die Typdatei.</summary>
    NewDefinition,

    /// <summary>Die ganze Datei kommt unter gleichem relativem Pfad in die Mod.</summary>
    CopyFile,

    /// <summary>Definition, die die Dateikopie mitnimmt (wichtig für Kompatibilität und spätere Vanilla-Updates).</summary>
    CopiedDefinition,

    /// <summary>Definition wird aus einer Mod-Datei entfernt, weil die Dateikopie sie enthält.</summary>
    RemoveFromMod,

    /// <summary>@-Konstante wird in die Zieldatei mitkopiert.</summary>
    CopyConstant,

    /// <summary>@-Konstante wird mitkopiert und umbenannt (in der Zieldatei gibt es den Namen mit anderem Wert); <c>Detail</c> ist der neue Name.</summary>
    RenameConstant,
}

/// <summary>Eintrag der Übersicht. <c>Line</c>: Zeile der Definition in der Zieldatei nach dem Schreiben (0 = ohne).</summary>
public sealed record GuiPlanEntry(GuiPlanEntryKind Kind, string Name, GuiDefinitionKind? DefinitionKind, string TargetPath, int Usages, string? Detail = null)
{
    public int Line { get; init; }
}

/// <summary>Zu schreibende Datei; <c>OldText</c> null = neue Datei.</summary>
public sealed record GuiPlanFile(string FullPath, string? OldText, string NewText)
{
    public bool IsNew => OldText is null;
}

public enum GuiPlanBlockerKind
{
    /// <summary>Der Entwurf hat neue Syntaxfehler.</summary>
    DraftErrors,

    /// <summary>Die Mod definiert den Namen mehrfach; <c>Places</c> nennt die Fundstellen.</summary>
    DuplicateInMod,

    /// <summary>Eine neue Definition hat einen Namen, den es in einer anderen Datei schon gibt.</summary>
    NameTaken,

    /// <summary>Die ganze Datei müsste kopiert werden, stammt aber nicht aus dem Ordner <c>game</c> des Spiels.</summary>
    CopyNotPossible,

    /// <summary>Die Mod hat die Definition geändert, seit der Entwurf begonnen hat; der Entwurf würde das überschreiben.</summary>
    ModChangedSince,
}

public sealed record GuiPlanBlocker(GuiPlanBlockerKind Kind, string Name, IReadOnlyList<string> Places);

/// <summary>Warum die ganze Datei kopiert wird.</summary>
public enum GuiCopyReason
{
    /// <summary>Inhalt außerhalb von type- und template-Definitionen geändert (Widgets auf oberster Ebene, Konstanten).</summary>
    TopLevelChanged,

    /// <summary>Eine Definition wurde gelöscht; das geht nur mit einer Kopie ohne sie.</summary>
    DefinitionDeleted,
}

public sealed record GuiOverridePlan(
    IReadOnlyList<GuiPlanEntry> Entries,
    IReadOnlyList<GuiPlanFile> Files,
    IReadOnlyList<GuiPlanBlocker> Blockers,
    GuiCopyReason? CopyReason)
{
    public bool CanApply => Blockers.Count == 0 && Files.Count > 0;
}

/// <param name="Source">Die Vanilla-Datei des Entwurfs (aus dem Index).</param>
/// <param name="ReadFile">Liest eine Mod-Datei (voller Pfad); null, wenn es sie nicht gibt.</param>
/// <param name="BaselineText">Text des Entwurfs bei "Für Mod bearbeiten" (<see cref="GuiDraft.Start"/> von damals); null = aus dem aktuellen Index neu rechnen (z.B. nach Wiederherstellung).</param>
public sealed record GuiOverrideRequest(
    GuiSourceFile Source,
    string VanillaText,
    string DraftText,
    GuiIndex Index,
    GuiModTargets Targets,
    Func<string, string?> ReadFile,
    string? BaselineText = null);

/// <summary>Inspektor "Als neuen Typ in Mod übernehmen": Quelle, neuer Name und das Übliche des Planers.</summary>
public sealed record GuiNewTypeCopyRequest(GuiCopySource Source, string Name, GuiIndex Index, GuiModTargets Targets, Func<string, string?> ReadFile);

/// <summary>
/// "In Mod übernehmen" (Spec "GuiOverridePlanner"): vergleicht den Entwurf mit seiner Grundlage (<see cref="GuiDraft.Start"/>) auf Ebene der
/// obersten type- und template-Definitionen und plant die Mod-Dateien. Geänderte Definition: ersetzt ihre Fassung in der Mod oder kommt in die
/// Überschreibdatei; neue: in die Typdatei; geänderter Inhalt außerhalb von Definitionen oder eine gelöschte Definition: ganze Datei kopieren
/// (nur aus <c>game</c>, Plan-Ruling 6). Schreibt nichts; die Dateien stehen fertig im Plan.
/// </summary>
public static class GuiOverridePlanner
{
    public const string CopyableRoot = "game";

    public static GuiOverridePlan Plan(GuiOverrideRequest request)
    {
        // Ersetzt eine Abhängigkeits-Mod die ganze Datei, ist ihre Fassung der Grundtext (Plan 3c Ruling 8).
        var baseText = GuiDraft.BaseText(request.Source, request.VanillaText, request.Index);
        if (!new GuiLiveDocument(baseText).Update(request.DraftText))
        {
            return new GuiOverridePlan(
                Array.Empty<GuiPlanEntry>(),
                Array.Empty<GuiPlanFile>(),
                new[] { new GuiPlanBlocker(GuiPlanBlockerKind.DraftErrors, request.Source.RelativePath, Array.Empty<string>()) },
                null);
        }

        // Grundlage ist der Stand bei "Für Mod bearbeiten". Hat die Mod eine Definition seither geändert, darf der Entwurf sie nicht mit seiner
        // älteren Fassung überschreiben (Plan-Ruling 5).
        var current = GuiDraft.Start(request.Source, request.VanillaText, request.Index).Text;
        if (request.BaselineText is null)
        {
            request = request with { DraftText = KeepModVersions(baseText, current, request.DraftText) };
        }

        var baseline = GuiParser.Parse(request.BaselineText ?? current);
        var now = request.BaselineText is null || request.BaselineText == current ? baseline : GuiParser.Parse(current);
        var draft = GuiParser.Parse(request.DraftText);
        var before = Keyed(baseline);
        var after = Keyed(draft);
        var changedSince = ChangedSince(baseline, before, now);
        GuiCopyReason? reason = !RestOf(baseline).SequenceEqual(RestOf(draft))
            ? GuiCopyReason.TopLevelChanged
            : before.Keys.Any(key => !after.ContainsKey(key)) ? GuiCopyReason.DefinitionDeleted : null;

        var builder = new Builder(request, draft);
        if (reason is { } copyReason)
        {
            // Die Kopie nimmt jede Definition mit: keine darf die Mod seit dem Beginn geändert haben.
            builder.ModChangedSince(after.Where(pair => changedSince.Contains(pair.Key)).Select(pair => pair.Value));
            builder.CopyFile(copyReason, before, after);
        }
        else
        {
            foreach (var (key, node) in after)
            {
                if (!before.TryGetValue(key, out var old))
                {
                    builder.Added(node);
                }
                else if (baseline.TextOf(old) != draft.TextOf(node))
                {
                    if (changedSince.Contains(key))
                    {
                        builder.ModChangedSince(new[] { node });
                    }
                    else
                    {
                        builder.Changed(node);
                    }
                }
            }
        }

        return builder.Build(reason);
    }

    /// <summary>
    /// Inspektor "Als neuen Typ in Mod übernehmen" (Plan 3c Ruling 7): eine unabhängige Kopie des Elements unter neuem Namen in die Typdatei der Mod,
    /// samt der @-Konstanten, die sie braucht. Gerechnet wie ein Entwurf der Quelldatei, der nur diese neue Definition hinzufügt. Ein vergebener
    /// Name blockiert.
    /// </summary>
    public static GuiOverridePlan PlanNewTypeCopy(GuiNewTypeCopyRequest request)
    {
        var source = request.Source;
        if (GuiTypeEdits.CopySnippet(source.Document, source.Node, request.Name) is not { } copy)
        {
            throw new ArgumentException("Nur Typen, Templates und Widgets lassen sich als neuer Typ übernehmen.", nameof(request));
        }

        var existing = request.Index.FindAll(request.Name, copy.Kind);
        if (existing.Count > 0)
        {
            return new GuiOverridePlan(
                Array.Empty<GuiPlanEntry>(),
                Array.Empty<GuiPlanFile>(),
                new[] { new GuiPlanBlocker(GuiPlanBlockerKind.NameTaken, request.Name, existing.Select(d => $"{d.File.RelativePath}:{d.Line}").ToList()) },
                null);
        }

        var start = GuiDraft.Start(source.File, source.Document.Text, request.Index).Text;
        var startDocument = GuiParser.Parse(start);
        var insertion = copy.Kind == GuiDefinitionKind.Type
            ? GuiTypeEdits.InsertType(startDocument, copy.Snippet, request.Targets.TypesContainer)
            : GuiTypeEdits.InsertTemplate(startDocument, copy.Snippet);
        var draft = TextEdit.Apply(start, insertion);
        return Plan(new GuiOverrideRequest(source.File, source.Document.Text, draft, request.Index, request.Targets, request.ReadFile, start));
    }

    /// <summary>
    /// Ohne gemerkte Grundlage (wiederhergestellter Entwurf, oder eine Vanilla-Datei, die direkt bearbeitet wurde, bevor sie schreibgeschützt war)
    /// kann der Text Definitionen noch in der Vanilla-Fassung enthalten, obwohl die Mod sie überschreibt. Solche Definitionen hat niemand geändert:
    /// Sie bekommen die Mod-Fassung, statt die Mod auf Vanilla zurückzusetzen. <c>vanillaText</c> ist der Grundtext (Vanilla oder die
    /// Abhängigkeits-Kopie, siehe <see cref="GuiDraft.BaseText"/>).
    /// </summary>
    private static string KeepModVersions(string vanillaText, string current, string draftText)
    {
        var vanilla = GuiParser.Parse(vanillaText);
        var start = GuiParser.Parse(current);
        var draft = GuiParser.Parse(draftText);
        var vanillaDefinitions = Keyed(vanilla);
        var startDefinitions = Keyed(start);
        var changes = new List<TextEdit>();
        foreach (var (key, node) in Keyed(draft))
        {
            if (vanillaDefinitions.TryGetValue(key, out var original)
                && startDefinitions.TryGetValue(key, out var modVersion)
                && draft.TextOf(node) == vanilla.TextOf(original)
                && start.TextOf(modVersion) != vanilla.TextOf(original))
            {
                changes.Add(new TextEdit(node.Span.Start, node.Span.Length, start.TextOf(modVersion)));
            }
        }

        return changes.Count == 0 ? draftText : TextEdit.Apply(draftText, changes);
    }

    /// <summary>Definitionen, deren Text in der Grundlage und im aktuellen Mod-Stand verschieden ist.</summary>
    private static HashSet<(GuiDefinitionKind Kind, string Name)> ChangedSince(GuiDocument baseline, OrderedDefinitions before, GuiDocument now)
    {
        var result = new HashSet<(GuiDefinitionKind Kind, string Name)>();
        if (ReferenceEquals(baseline, now))
        {
            return result;
        }

        var current = Keyed(now);
        foreach (var (key, node) in before)
        {
            if (current.TryGetValue(key, out var other) && baseline.TextOf(node) != now.TextOf(other))
            {
                result.Add(key);
            }
        }

        return result;
    }

    /// <summary>Definitionen je Art und Name (die erste bei doppeltem Namen in einer Datei), in Textreihenfolge.</summary>
    private static OrderedDefinitions Keyed(GuiDocument document)
    {
        var result = new OrderedDefinitions();
        foreach (var node in GuiDraft.Definitions(document))
        {
            result.TryAdd((GuiDraft.KindOf(node), node.Name), node);
        }

        return result;
    }

    /// <summary>Alles außerhalb von Definitionen und Kommentaren (auf oberster Ebene und direkt in types-Containern), als Texte.</summary>
    private static IReadOnlyList<string> RestOf(GuiDocument document)
    {
        var rest = new List<string>();
        foreach (var node in document.Root.Children)
        {
            switch (node.Kind)
            {
                case GuiNodeKind.Comment or GuiNodeKind.TemplateDefinition or GuiNodeKind.TypeDefinition:
                    break;
                case GuiNodeKind.TypesContainer:
                    rest.AddRange(node.Children
                        .Where(c => c.Kind is not (GuiNodeKind.Comment or GuiNodeKind.TypeDefinition or GuiNodeKind.TemplateDefinition))
                        .Select(document.TextOf));
                    break;
                default:
                    rest.Add(document.TextOf(node));
                    break;
            }
        }

        return rest;
    }

    private sealed class OrderedDefinitions : List<KeyValuePair<(GuiDefinitionKind Kind, string Name), GuiNode>>
    {
        private readonly Dictionary<(GuiDefinitionKind, string), GuiNode> _lookup = new();

        public IEnumerable<(GuiDefinitionKind Kind, string Name)> Keys => this.Select(pair => pair.Key);

        public void TryAdd((GuiDefinitionKind Kind, string Name) key, GuiNode node)
        {
            if (_lookup.TryAdd(key, node))
            {
                Add(new KeyValuePair<(GuiDefinitionKind Kind, string Name), GuiNode>(key, node));
            }
        }

        public bool ContainsKey((GuiDefinitionKind Kind, string Name) key) => _lookup.ContainsKey(key);

        public bool TryGetValue((GuiDefinitionKind Kind, string Name) key, out GuiNode node) => _lookup.TryGetValue(key, out node!);
    }

    private sealed class TargetFile
    {
        public TargetFile(string path, string? old)
        {
            Path = path;
            Old = old;
            Text = old ?? string.Empty;
        }

        public string Path { get; }

        public string? Old { get; }

        public string Text { get; set; }
    }

    private sealed class Builder
    {
        private readonly GuiOverrideRequest _request;
        private readonly GuiDocument _draft;
        private readonly GuiTextEdits _draftEdits;
        private readonly string _context;
        private readonly string _stem;
        private readonly List<TargetFile> _files = new();
        private readonly List<GuiPlanEntry> _entries = new();
        private readonly List<GuiPlanBlocker> _blockers = new();

        public Builder(GuiOverrideRequest request, GuiDocument draft)
        {
            _request = request;
            _draft = draft;
            _draftEdits = new GuiTextEdits(draft);
            _context = GuiContexts.Of(request.Source.RelativePath) ?? GuiContexts.Default;
            _stem = GuiModTargets.BaseName(System.IO.Path.GetFileNameWithoutExtension(request.Source.RelativePath));
        }

        private GuiIndex Index => _request.Index;

        public void Changed(GuiNode node)
        {
            var kind = GuiDraft.KindOf(node);
            var mods = Index.FindAll(node.Name, kind).Where(d => d.File.Layer == GuiLayer.Mod).ToList();
            if (mods.Count > 1)
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.DuplicateInMod, node.Name, mods.Select(Place).ToList()));
                return;
            }

            // Steht die Definition noch so in der Mod-Datei (Index und Platte können sich unterscheiden), wird sie dort ersetzt.
            if (mods.Count == 1 && File(mods[0].File.FullPath) is var file && Find(GuiParser.Parse(file.Text), kind, node.Name) is not null)
            {
                var renames = CopyConstants(node, file);
                Replace(file, kind, node.Name, Snippet(node, renames));
                _entries.Add(Entry(GuiPlanEntryKind.Replace, node, file));
                return;
            }

            var target = File(_request.Targets.OverridePath(_context));
            var copied = CopyConstants(node, target);
            Insert(target, kind == GuiDefinitionKind.Type ? _request.Targets.OverrideContainer : null, Snippet(node, copied));
            _entries.Add(Entry(GuiPlanEntryKind.Override, node, target));
        }

        public void ModChangedSince(IEnumerable<GuiNode> nodes)
        {
            foreach (var node in nodes)
            {
                var places = Index.FindAll(node.Name, GuiDraft.KindOf(node))
                    .Where(d => d.File.Layer is GuiLayer.Mod or GuiLayer.Dependency)
                    .Select(Place)
                    .ToList();
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.ModChangedSince, node.Name, places));
            }
        }

        public void Added(GuiNode node)
        {
            var kind = GuiDraft.KindOf(node);
            var existing = Index.FindAll(node.Name, kind);
            if (existing.Count > 0)
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.NameTaken, node.Name, existing.Select(Place).ToList()));
                return;
            }

            var target = File(_request.Targets.TypesPath(_context));
            var renames = CopyConstants(node, target);
            Insert(target, kind == GuiDefinitionKind.Type ? _request.Targets.TypesContainer : null, Snippet(node, renames));
            _entries.Add(Entry(GuiPlanEntryKind.NewDefinition, node, target));
        }

        public void CopyFile(GuiCopyReason reason, OrderedDefinitions before, OrderedDefinitions after)
        {
            var source = _request.Source;
            if (!string.Equals(source.Label, CopyableRoot, StringComparison.OrdinalIgnoreCase))
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.CopyNotPossible, source.RelativePath, new[] { source.Label }));
                return;
            }

            var path = System.IO.Path.Combine(_request.Targets.ModRoot, source.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var copy = File(path);
            copy.Text = _request.DraftText;
            _entries.Add(new GuiPlanEntry(GuiPlanEntryKind.CopyFile, source.RelativePath, null, path, 0, reason.ToString()));
            foreach (var (key, node) in after)
            {
                var existing = Index.FindAll(node.Name, key.Kind);
                if (!before.ContainsKey(key) && existing.Count > 0)
                {
                    _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.NameTaken, node.Name, existing.Select(Place).ToList()));
                    continue;
                }

                _entries.Add(Entry(GuiPlanEntryKind.CopiedDefinition, node, copy));
                if (Home(node) is not null)
                {
                    // Die Mod-Fassung braucht die Konstanten ihrer Mod-Datei, nicht die der Vanilla-Datei.
                    var renames = CopyConstants(node, copy);
                    if (renames.Count > 0)
                    {
                        Replace(copy, key.Kind, node.Name, Snippet(node, renames));
                    }
                }

                RemoveFromMod(key, path);
            }

            // Eine gelöschte Definition darf auch in den übrigen Mod-Dateien nicht stehen bleiben.
            foreach (var key in before.Keys.Where(key => !after.ContainsKey(key)))
            {
                RemoveFromMod(key, path);
            }
        }

        private void RemoveFromMod((GuiDefinitionKind Kind, string Name) key, string copyPath)
        {
            foreach (var mod in Index.FindAll(key.Name, key.Kind).Where(d => d.File.Layer == GuiLayer.Mod && !SamePath(d.File.FullPath, copyPath)))
            {
                var file = File(mod.File.FullPath);
                if (Remove(file, key.Kind, key.Name))
                {
                    _entries.Add(new GuiPlanEntry(GuiPlanEntryKind.RemoveFromMod, key.Name, key.Kind, file.Path, Usages(key.Name, key.Kind)));
                }
            }
        }

        /// <summary>Die Definition in der Mod- oder Abhängigkeits-Datei, aus der der Entwurf ihren Text hat; null bei Vanilla-Definitionen.</summary>
        private GuiNode? Home(GuiNode node) => GuiDraft.ModDefinition(Index, node)?.Node;

        public GuiOverridePlan Build(GuiCopyReason? reason)
        {
            var newline = _request.DraftText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var files = new List<GuiPlanFile>();
            foreach (var file in _files)
            {
                var text = file.Old is null && newline == "\r\n" ? file.Text.Replace("\r\n", "\n").Replace("\n", "\r\n") : file.Text;
                if (text != file.Old)
                {
                    files.Add(new GuiPlanFile(file.Path, file.Old, text));
                }
            }

            var entries = _entries.Select(entry => entry with { Line = LineOf(entry, files) }).ToList();
            return new GuiOverridePlan(entries, files, _blockers, reason);
        }

        /// <summary>Definition als Schnipsel (Einrückung als Tabs) mit umbenannten Konstanten.</summary>
        private string Snippet(GuiNode node, IReadOnlyDictionary<string, string> renames) =>
            GuiConstants.Rename(_draftEdits.Dedent(node), renames);

        /// <summary>
        /// Kopiert die Konstanten, die die Definition braucht, auf die oberste Ebene der Zieldatei. Gibt es dort denselben Namen mit anderem Wert,
        /// bekommt die Kopie den Namen <c>name_&lt;quelldatei&gt;</c>; das Ergebnis sind diese Umbenennungen.
        /// </summary>
        private Dictionary<string, string> CopyConstants(GuiNode node, TargetFile file)
        {
            var renames = new Dictionary<string, string>(StringComparer.Ordinal);
            var existing = GuiParser.Parse(file.Text).Root.Children
                .Where(c => c.Kind == GuiNodeKind.ConstantDefinition)
                .GroupBy(c => c.Name)
                .ToDictionary(g => g.Key, g => g.First().Value ?? string.Empty, StringComparer.Ordinal);
            foreach (var constant in GuiConstants.RequiredBy(node, Home(node)))
            {
                var value = GuiConstants.Rename(constant.Value ?? string.Empty, renames);
                if (existing.TryGetValue(constant.Name, out var present))
                {
                    if (present == value)
                    {
                        continue;
                    }

                    var name = constant.Name + "_" + _stem;
                    for (var n = 2; existing.TryGetValue(name, out var taken) && taken != value; n++)
                    {
                        name = $"{constant.Name}_{_stem}_{n}";
                    }

                    renames[constant.Name] = name;
                    if (!existing.ContainsKey(name))
                    {
                        InsertConstant(file, name, value);
                        existing[name] = value;
                    }

                    _entries.Add(new GuiPlanEntry(GuiPlanEntryKind.RenameConstant, constant.Name, null, file.Path, 0, name));
                    continue;
                }

                InsertConstant(file, constant.Name, value);
                existing[constant.Name] = value;
                _entries.Add(new GuiPlanEntry(GuiPlanEntryKind.CopyConstant, constant.Name, null, file.Path, 0));
            }

            return renames;
        }

        private static void InsertConstant(TargetFile file, string name, string value)
        {
            var document = GuiParser.Parse(file.Text);
            var edits = new GuiTextEdits(document);
            var root = document.Root;
            var line = $"@{name} = {value}";
            var lastConstant = root.Children.LastOrDefault(c => c.Kind == GuiNodeKind.ConstantDefinition);
            var first = root.Children.FirstOrDefault(c => c.Kind != GuiNodeKind.Comment);
            var change = lastConstant is not null ? edits.InsertChild(root, line, InsertPosition.After, lastConstant)
                : first is not null ? edits.InsertChild(root, line, InsertPosition.Before, first)
                : edits.InsertChild(root, line, InsertPosition.LastChild);
            file.Text = TextEdit.Apply(file.Text, change);
        }

        /// <summary>Fügt die Definition am Ende ein: Typen in den types-Container (bei Bedarf neu angelegt), Templates auf oberster Ebene.</summary>
        private static void Insert(TargetFile file, string? container, string snippet)
        {
            var document = GuiParser.Parse(file.Text);
            var parent = document.Root;
            if (container is not null)
            {
                parent = FindContainer(document, container)!;
                if (parent is null)
                {
                    file.Text = TextEdit.Apply(file.Text, new GuiTextEdits(document).InsertChild(document.Root, $"types {container} {{\n}}", InsertPosition.LastChild));
                    document = GuiParser.Parse(file.Text);
                    parent = FindContainer(document, container)!;
                }
            }

            file.Text = TextEdit.Apply(file.Text, new GuiTextEdits(document).InsertChild(parent, snippet, InsertPosition.LastChild));
        }

        private static GuiNode? FindContainer(GuiDocument document, string name) =>
            document.Root.Children.FirstOrDefault(c => c.Kind == GuiNodeKind.TypesContainer && c.Name == name);

        private static bool Replace(TargetFile file, GuiDefinitionKind kind, string name, string snippet)
        {
            var document = GuiParser.Parse(file.Text);
            if (Find(document, kind, name) is not { } node)
            {
                return false;
            }

            var replacement = new GuiTextEdits(document).ReplacementFor(node, snippet);
            file.Text = TextEdit.Apply(file.Text, new[] { new TextEdit(node.Span.Start, node.Span.Length, replacement) });
            return true;
        }

        private static bool Remove(TargetFile file, GuiDefinitionKind kind, string name)
        {
            var document = GuiParser.Parse(file.Text);
            if (Find(document, kind, name) is not { } node)
            {
                return false;
            }

            file.Text = TextEdit.Apply(file.Text, new GuiTextEdits(document).RemoveNode(node));
            return true;
        }

        private static GuiNode? Find(GuiDocument document, GuiDefinitionKind kind, string name) =>
            GuiDraft.Definitions(document).FirstOrDefault(n => GuiDraft.KindOf(n) == kind && n.Name == name);

        private TargetFile File(string path)
        {
            if (_files.FirstOrDefault(f => SamePath(f.Path, path)) is { } known)
            {
                return known;
            }

            var file = new TargetFile(path, _request.ReadFile(path));
            _files.Add(file);
            return file;
        }

        private GuiPlanEntry Entry(GuiPlanEntryKind kind, GuiNode node, TargetFile file)
        {
            var definitionKind = GuiDraft.KindOf(node);
            return new GuiPlanEntry(kind, node.Name, definitionKind, file.Path, Usages(node.Name, definitionKind));
        }

        private int Usages(string name, GuiDefinitionKind kind)
        {
            var usageKind = kind == GuiDefinitionKind.Type ? GuiUsageKind.Widget : GuiUsageKind.Using;
            return Index.UsagesOf(name).Count(u => u.Kind == usageKind);
        }

        private static int LineOf(GuiPlanEntry entry, IReadOnlyList<GuiPlanFile> files)
        {
            if (entry.DefinitionKind is not { } kind
                || entry.Kind is GuiPlanEntryKind.RemoveFromMod
                || files.FirstOrDefault(f => SamePath(f.FullPath, entry.TargetPath)) is not { } file)
            {
                return 0;
            }

            return Find(GuiParser.Parse(file.NewText), kind, entry.Name)?.Line ?? 0;
        }

        private static string Place(GuiDefinition definition) => $"{definition.File.RelativePath}:{definition.Line}";

        private static bool SamePath(string a, string b) =>
            string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }
}
