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

/// <summary>
/// Warum eine klassische Überschreibung (HOI IV, EU IV) in ihrer Datei gewinnt; gezeigt in der Übersicht. Gemessen 2026-10-17: in <c>interface/</c> laden
/// die Dateien eines Ordners nach Namen (Vanilla und Mods gemischt), danach seine Unterordner.
/// </summary>
public enum ClassicTargetRule
{
    /// <summary>HOI IV: lädt als letzte Datei direkt in <c>interface/</c>; die zuletzt geladene Fassung gewinnt.</summary>
    RootLastWins,

    /// <summary>HOI IV: lädt als letzte Datei im Unterordner des Vanilla-Fensters; die zuletzt geladene Fassung gewinnt.</summary>
    SubfolderLastWins,

    /// <summary>EU IV: lädt vor allen Vanilla-Dateien direkt in <c>interface/</c> (und damit vor allen Unterordnern); die zuerst geladene Fassung gewinnt.</summary>
    RootFirstWins,

    /// <summary>Der Dateiname ist in den Mod-Einstellungen gesetzt und wird so verwendet; ob er gewinnt, prüft der Planer.</summary>
    Configured,

    /// <summary>Die Mod-Datei hat das Fenster schon und gewinnt nach der Ladereihenfolge (vom Planer geprüft); es wird dort ersetzt.</summary>
    ExistingWinner,
}

/// <summary>
/// Eintrag der Übersicht. <c>Line</c>: Zeile der Definition in der Zieldatei nach dem Schreiben (0 = ohne). <c>Rule</c>: klassisch, warum diese Datei
/// (bei <c>Override</c>, <c>Replace</c> und <c>NewDefinition</c>).
/// </summary>
public sealed record GuiPlanEntry(GuiPlanEntryKind Kind, string Name, GuiDefinitionKind? DefinitionKind, string TargetPath, int Usages, string? Detail = null)
{
    public int Line { get; init; }

    public ClassicTargetRule? Rule { get; init; }

    /// <summary>
    /// Klassischer Umzug (<see cref="GuiPlanEntryKind.RemoveFromMod"/> neben einem Override): Etikett der Abhängigkeits-Mod, gegen die das Fenster in seiner
    /// Mod-Datei verliert; null, wenn Vanilla gewinnt.
    /// </summary>
    public string? LosesToDependency { get; init; }
}

/// <summary>
/// Zu schreibende Datei; <c>OldText</c> null = neue Datei. <c>EncodingName</c>: <c>windows-1252</c> erzwingt diese Kodierung für eine neue Datei
/// (sie übernimmt Nicht-ASCII-Text aus einer Windows-1252-Quelle); null = die App entscheidet.
/// </summary>
public sealed record GuiPlanFile(string FullPath, string? OldText, string NewText)
{
    public bool IsNew => OldText is null;

    public string? EncodingName { get; init; }
}

public enum GuiPlanBlockerKind
{
    /// <summary>Der Entwurf hat neue Syntaxfehler.</summary>
    DraftErrors,

    /// <summary>Die Mod definiert den Namen mehrfach; <c>Places</c> nennt die Fundstellen.</summary>
    DuplicateInMod,

    /// <summary>
    /// Die Mod-Definition lädt nach einer anderen und wirkt im Spiel nicht (Victoria 3: Vanilla und Mods in einer Ladereihenfolge); <c>Places</c>
    /// nennt die Mod-Fundstelle und die wirksame Definition.
    /// </summary>
    ModLoadsAfterVanilla,

    /// <summary>Eine neue Definition hat einen Namen, den es in einer anderen Datei schon gibt.</summary>
    NameTaken,

    /// <summary>Die ganze Datei müsste kopiert werden, stammt aber nicht aus dem Ordner <c>game</c> des Spiels.</summary>
    CopyNotPossible,

    /// <summary>Die Mod hat die Definition geändert, seit der Entwurf begonnen hat; der Entwurf würde das überschreiben.</summary>
    ModChangedSince,

    /// <summary>
    /// Klassisch: Ein geschriebenes Fenster wirkte nach dem Schreiben nicht wie geplant; was genau, sagt <see cref="GuiPlanBlocker.TargetProblem"/>
    /// (gewinnt nicht, ersetzte eine ganze Datei, fehlt in der Zieldatei, unzulässiger Dateiname).
    /// </summary>
    TargetLoses,
}

/// <summary>
/// Ein Hindernis. Nur bei <see cref="GuiPlanBlockerKind.TargetLoses"/>: <c>TargetProblem</c> sagt, was mit der Zieldatei nicht stimmt, <c>ConfiguredTarget</c>,
/// ob ihr Name aus den Mod-Einstellungen stammt (sonst hat PMT ihn gewählt).
/// </summary>
public sealed record GuiPlanBlocker(GuiPlanBlockerKind Kind, string Name, IReadOnlyList<string> Places)
{
    public GuiTargetProblem? TargetProblem { get; init; }

    public bool ConfiguredTarget { get; init; }
}

/// <summary>Was mit der Zieldatei einer klassischen Übernahme nicht stimmt (<see cref="GuiPlanBlockerKind.TargetLoses"/>).</summary>
public enum GuiTargetProblem
{
    /// <summary>Ein anderes Fenster gewönne weiter; <c>Places</c> = geplante Fundstelle und Gewinner.</summary>
    Loses,

    /// <summary>Die Zieldatei hätte den Pfad einer Vanilla- oder Abhängigkeits-Datei und ersetzte sie ganz; <c>Places</c> = zweimal dieser Pfad.</summary>
    ReplacesFile,

    /// <summary>Das Fenster fehlte nach dem Schreiben in der Zieldatei; <c>Places</c> = die Zieldatei.</summary>
    NotFound,

    /// <summary>Der gesetzte Dateiname enthält einen Ordner oder unzulässige Zeichen; <c>Places</c> = der Name, wie gesetzt.</summary>
    InvalidName,
}

public enum GuiPlanWarningKind
{
    /// <summary>Eine Abhängigkeits-Mod lädt danach und gewinnt im Spiel; <c>Places</c> nennt ihre Fundstelle.</summary>
    DependencyWins,

    /// <summary>Die Ladereihenfolge hängt an einer DLC-Datei und ist nicht gemessen (unsicher); <c>Places</c> nennt die Zieldatei.</summary>
    UncertainOrder,
}

/// <summary>Hinweis der Übersicht, der das Übernehmen nicht verhindert.</summary>
public sealed record GuiPlanWarning(GuiPlanWarningKind Kind, string Name, IReadOnlyList<string> Places);

/// <summary>Warum die ganze Datei kopiert wird.</summary>
public enum GuiCopyReason
{
    /// <summary>Inhalt außerhalb von type- und template-Definitionen geändert (Widgets auf oberster Ebene, Konstanten).</summary>
    TopLevelChanged,

    /// <summary>Eine Definition wurde gelöscht; das geht nur mit einer Kopie ohne sie.</summary>
    DefinitionDeleted,

    /// <summary>In der Übersicht wurde "Ganze Datei kopieren" gewählt.</summary>
    Requested,
}

public sealed record GuiOverridePlan(
    IReadOnlyList<GuiPlanEntry> Entries,
    IReadOnlyList<GuiPlanFile> Files,
    IReadOnlyList<GuiPlanBlocker> Blockers,
    GuiCopyReason? CopyReason)
{
    public bool CanApply => Blockers.Count == 0 && Files.Count > 0;

    public IReadOnlyList<GuiPlanWarning> Warnings { get; init; } = Array.Empty<GuiPlanWarning>();
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
    string? BaselineText = null)
{
    /// <summary>Klassisch: In der Übersicht wurde "Ganze Datei kopieren" gewählt.</summary>
    public bool ForceCopyFile { get; init; }

    /// <summary>Kodierung der Vanilla-Datei, wie <see cref="GuiTextReader"/> sie erkannt hat (<c>utf-8</c>, <c>utf-8-bom</c>, <c>windows-1252</c>); null = unbekannt.</summary>
    public string? SourceEncoding { get; init; }
}

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
        if (request.Index.Layout.IsClassic)
        {
            return ClassicOverridePlanner.Plan(request);
        }

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
        if (request.Index.Layout.IsClassic)
        {
            throw new InvalidOperationException("Das klassische Format (HOI IV, EU IV) hat keine Typen.");
        }

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
            _context = GuiContexts.Of(request.Source.RelativePath, request.Index.Layout) ?? GuiContexts.DefaultOf(request.Index.Layout);
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

            // Victoria 3: Lädt die Mod-Definition nach Vanilla (oder einer anderen Mod), wirkt sie nicht; eine Änderung dort bliebe ohne Wirkung.
            if (mods.Count == 1 && Index.Layout is { FirstWins: true, ModsBeforeVanilla: false }
                && Index.Find(node.Name, kind) is { } effective && !ReferenceEquals(effective, mods[0]))
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.ModLoadsAfterVanilla, node.Name, new[] { Place(mods[0]), Place(effective) }));
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

        /// <summary>Kopiert die Konstanten der Definition auf die oberste Ebene der Zieldatei (siehe <see cref="GuiConstantCopy"/>).</summary>
        private Dictionary<string, string> CopyConstants(GuiNode node, TargetFile file)
        {
            var text = file.Text;
            var renames = GuiConstantCopy.Copy(node, Home(node), ref text, _stem, _entries, file.Path);
            file.Text = text;
            return renames;
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
