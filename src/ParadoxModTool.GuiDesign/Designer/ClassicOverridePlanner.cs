using System.Text;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Designer;

/// <summary>
/// "In Mod übernehmen" im klassischen Format (HOI IV, EU IV). Derselbe Rahmen wie <see cref="GuiOverridePlanner"/> (Syntaxprüfung, Grundlage, Mod seither
/// geändert, ganze Datei kopieren), aber die Einheit ist das oberste Fenster (<see cref="ClassicWindows"/>), weil ein Spiel ein Fenster nur ganz ersetzt.
/// Geschrieben werden nur die geänderten und neuen Fenster, in eine Datei, die nach der gemessenen Ladereihenfolge gewinnt
/// (<see cref="ClassicOverrideTargets"/>). Danach prüft der Planer an einem nachgebildeten Index, ob jedes geschriebene Fenster wirklich gewinnt.
/// Schreibt nichts; die Dateien stehen fertig im Plan.
/// </summary>
public static class ClassicOverridePlanner
{
    public static GuiOverridePlan Plan(GuiOverrideRequest request)
    {
        // Ersetzt eine Abhängigkeits-Mod die ganze Datei, ist ihre Fassung der Grundtext (wie im Jomini-Planer).
        var baseText = GuiDraft.BaseText(request.Source, request.VanillaText, request.Index);
        if (!new GuiLiveDocument(baseText).Update(request.DraftText))
        {
            return new GuiOverridePlan(
                Array.Empty<GuiPlanEntry>(),
                Array.Empty<GuiPlanFile>(),
                new[] { new GuiPlanBlocker(GuiPlanBlockerKind.DraftErrors, request.Source.RelativePath, Array.Empty<string>()) },
                null);
        }

        // Grundlage ist der Stand bei "Für Mod bearbeiten"; hat die Mod ein Fenster seither geändert, darf der Entwurf es nicht überschreiben.
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
        GuiCopyReason? reason = !RestOf(baseline).SequenceEqual(RestOf(draft)) ? GuiCopyReason.TopLevelChanged
            : before.Names.Any(name => !after.Contains(name)) ? GuiCopyReason.DefinitionDeleted
            : request.ForceCopyFile ? GuiCopyReason.Requested
            : null;

        var builder = new Builder(request, draft);
        if (reason is { } copyReason)
        {
            // Die Kopie nimmt jedes Fenster mit: keines darf die Mod seit dem Beginn geändert haben.
            builder.ModChangedSince(after.Where(pair => changedSince.Contains(pair.Key)).Select(pair => pair.Value));
            builder.CopyFile(copyReason, baseline, before, after);
        }
        else
        {
            foreach (var (name, node) in after)
            {
                if (!before.TryGetValue(name, out var old))
                {
                    builder.Added(node);
                }
                else if (baseline.TextOf(old) != draft.TextOf(node))
                {
                    if (changedSince.Contains(name))
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
    /// Fenster, die der Plan umziehen lässt: Eine Mod-Datei hat das Fenster, verliert damit aber nach der Ladereihenfolge; der Plan entfernt es dort
    /// (<see cref="GuiPlanEntryKind.RemoveFromMod"/>) und schreibt es in die Zieldatei (<see cref="GuiPlanEntryKind.Override"/>). Das Entfernen beim
    /// Kopieren der ganzen Datei ist kein Umzug. Pfade relativ zu <paramref name="modRoot"/> mit "/"; <c>Dependency</c> ist das Etikett der
    /// Abhängigkeits-Mod, gegen die das Fenster verliert, null bei Vanilla.
    /// </summary>
    public static IReadOnlyList<(string Window, string FromFile, string ToFile, string? Dependency)> Moves(GuiOverridePlan plan, string modRoot)
    {
        var targets = plan.Entries.Where(e => e.Kind == GuiPlanEntryKind.Override).ToList();
        return plan.Entries
            .Where(e => e.Kind == GuiPlanEntryKind.RemoveFromMod)
            .SelectMany(removed => targets.Where(t => t.Name == removed.Name).Take(1)
                .Select(t => (removed.Name, Relative(modRoot, removed.TargetPath), Relative(modRoot, t.TargetPath), removed.LosesToDependency)))
            .ToList();
    }

    /// <summary>
    /// Kodierung beim Schreiben einer Datei des Plans (Global Constraints "Encoding"): bestehende Dateien nach <see cref="ModFileEncoding.ForWrite"/>
    /// (Windows-1252 bleibt Windows-1252), neue mit Nicht-ASCII-Text aus einer Windows-1252-Quelle als Windows-1252 (<see cref="GuiPlanFile.EncodingName"/>),
    /// sonst nach dem Spielprofil (<see cref="ModFileEncoding.NewFile"/>). Die App fragt vor dem ersten Schreiben, weil <c>ForWrite</c> die Datei liest.
    /// </summary>
    public static Encoding WriteEncoding(GuiPlanFile file, GameProfile? profile)
    {
        if (!file.IsNew)
        {
            return ModFileEncoding.ForWrite(profile, file.FullPath);
        }

        if (file.EncodingName == GuiTextReader.Windows1252)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }

        return ModFileEncoding.NewFile(profile, System.IO.Path.GetExtension(file.FullPath));
    }

    private static string Relative(string modRoot, string path)
    {
        var relative = System.IO.Path.GetRelativePath(modRoot, path);
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative.Replace('\\', '/');
    }

    /// <summary>
    /// Der Index, wie er nach dem Schreiben wäre: jede geplante Datei (neu oder geändert, auch eine, aus der ein Fenster entfernt wird) mit ihrem neuen
    /// Text auf der Ebene Mod, <c>RelativePath</c> relativ zum Mod-Ordner.
    /// </summary>
    internal static GuiIndex Simulate(GuiIndex index, IReadOnlyList<GuiPlanFile> files, string modRoot)
    {
        var label = Path.GetFileName(modRoot.TrimEnd('\\', '/'));
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(modRoot, file.FullPath).Replace('\\', '/');
            index = index.WithFile(new GuiSourceFile(file.FullPath, relative, GuiLayer.Mod, label, 0, 0), file.NewText);
        }

        return index;
    }

    /// <summary>
    /// Ohne gemerkte Grundlage (wiederhergestellter Entwurf) kann ein Fenster noch in der Vanilla-Fassung im Entwurf stehen, obwohl die Mod es
    /// überschreibt. Niemand hat es geändert: Es bekommt die Mod-Fassung, statt die Mod auf Vanilla zurückzusetzen (wie im Jomini-Planer).
    /// </summary>
    private static string KeepModVersions(string vanillaText, string current, string draftText)
    {
        var vanilla = GuiParser.Parse(vanillaText);
        var start = GuiParser.Parse(current);
        var draft = GuiParser.Parse(draftText);
        var vanillaWindows = Keyed(vanilla);
        var startWindows = Keyed(start);
        var changes = new List<TextEdit>();
        foreach (var (name, node) in Keyed(draft))
        {
            if (vanillaWindows.TryGetValue(name, out var original)
                && startWindows.TryGetValue(name, out var modVersion)
                && draft.TextOf(node) == vanilla.TextOf(original)
                && start.TextOf(modVersion) != vanilla.TextOf(original))
            {
                changes.Add(new TextEdit(node.Span.Start, node.Span.Length, start.TextOf(modVersion)));
            }
        }

        return changes.Count == 0 ? draftText : TextEdit.Apply(draftText, changes);
    }

    /// <summary>Fenster, deren Text in der Grundlage und im aktuellen Mod-Stand verschieden ist.</summary>
    private static HashSet<string> ChangedSince(GuiDocument baseline, OrderedWindows before, GuiDocument now)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (ReferenceEquals(baseline, now))
        {
            return result;
        }

        var current = Keyed(now);
        foreach (var (name, node) in before)
        {
            if (current.TryGetValue(name, out var other) && baseline.TextOf(node) != now.TextOf(other))
            {
                result.Add(name);
            }
        }

        return result;
    }

    /// <summary>Die obersten Fenster je Name (das erste bei doppeltem Namen in einer Datei), in Textreihenfolge.</summary>
    private static OrderedWindows Keyed(GuiDocument document)
    {
        var result = new OrderedWindows();
        foreach (var window in ClassicWindows.Of(document))
        {
            result.TryAdd(ClassicWindows.NameOf(window), window);
        }

        return result;
    }

    /// <summary>
    /// Alles außerhalb der Fenster und Kommentare, als Texte: oberste Ebene ohne <c>guiTypes</c>-Blöcke (jede Schreibweise), dazu die übrigen Kinder
    /// dieser Blöcke (unbenannte Blöcke, Werte) und jedes weitere Fenster eines Namens, den es in der Datei schon gab (es wäre sonst keiner Einheit zugeordnet).
    /// </summary>
    private static IReadOnlyList<string> RestOf(GuiDocument document)
    {
        var rest = new List<string>();
        var windows = ClassicWindows.Of(document);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var first = windows.Where(w => seen.Add(ClassicWindows.NameOf(w))).ToHashSet();
        foreach (var node in document.Root.Children)
        {
            if (node.Kind == GuiNodeKind.Comment)
            {
                continue;
            }

            if (IsContainer(node))
            {
                rest.AddRange(node.Children.Where(c => c.Kind != GuiNodeKind.Comment && !first.Contains(c)).Select(document.TextOf));
                continue;
            }

            rest.Add(document.TextOf(node));
        }

        return rest;
    }

    private static bool IsContainer(GuiNode node) =>
        node.Kind == GuiNodeKind.KeyedBlock && string.Equals(node.Name, "guiTypes", StringComparison.OrdinalIgnoreCase);

    private static GuiNode? FindWindow(GuiDocument document, string name) =>
        ClassicWindows.Of(document).FirstOrDefault(w => ClassicWindows.NameOf(w) == name);

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static string Place(GuiDefinition definition) => $"{definition.File.RelativePath}:{definition.Line}";

    private sealed class OrderedWindows : List<KeyValuePair<string, GuiNode>>
    {
        private readonly Dictionary<string, GuiNode> _lookup = new(StringComparer.Ordinal);

        public IEnumerable<string> Names => this.Select(pair => pair.Key);

        public void TryAdd(string name, GuiNode node)
        {
            if (_lookup.TryAdd(name, node))
            {
                Add(new KeyValuePair<string, GuiNode>(name, node));
            }
        }

        public bool Contains(string name) => _lookup.ContainsKey(name);

        public bool TryGetValue(string name, out GuiNode node) => _lookup.TryGetValue(name, out node!);
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
        private readonly string _stem;
        private readonly List<TargetFile> _files = new();
        private readonly List<GuiPlanEntry> _entries = new();
        private readonly List<GuiPlanBlocker> _blockers = new();
        private readonly List<GuiPlanWarning> _warnings = new();

        /// <summary>Geschriebene Fenster (Name, Datei), die nach dem Schreiben gewinnen müssen.</summary>
        private readonly List<(string Name, string Path)> _written = new();

        private ClassicTarget? _target;

        public Builder(GuiOverrideRequest request, GuiDocument draft)
        {
            _request = request;
            _draft = draft;
            _draftEdits = new GuiTextEdits(draft);
            _stem = GuiModTargets.BaseName(System.IO.Path.GetFileNameWithoutExtension(request.Source.RelativePath));
        }

        private GuiIndex Index => _request.Index;

        private string ModRoot => _request.Targets.ModRoot;

        /// <summary>
        /// Die Zieldatei für Überschreibungen und neue Fenster. Ist der Name der Standardname (<see cref="GuiModTargets.For"/> mit <c>classic</c>), wird er bei
        /// Bedarf verlängert; jeder andere Name ist in den Mod-Einstellungen gesetzt und gilt wie angegeben.
        /// </summary>
        private ClassicTarget Target
        {
            get
            {
                if (_target is null)
                {
                    var name = _request.Targets.OverrideFileName;
                    var configured = string.Equals(name, ClassicOverrideTargets.DefaultFileName(ModRoot, Index.Layout), StringComparison.OrdinalIgnoreCase) ? null : name;
                    _target = ClassicOverrideTargets.For(_request.Source, Index, ModRoot, configured);
                }

                return _target;
            }
        }

        public void Changed(GuiNode node)
        {
            var name = ClassicWindows.NameOf(node);
            var mods = ClassicWindows.FindAll(Index, name).Where(d => d.File.Layer == GuiLayer.Mod).ToList();
            if (mods.Count > 1)
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.DuplicateInMod, name, mods.Select(Place).ToList()));
                return;
            }

            // Steht das Fenster noch so in der Mod-Datei (Index und Platte können sich unterscheiden), wird es dort ersetzt, wenn es gewinnt. Verliert es,
            // bliebe eine Änderung dort ohne Wirkung (und EU IV meldete die spätere Kopie als "Already Defined"): Es zieht in die Zieldatei um.
            if (mods.Count == 1 && File(mods[0].File.FullPath) is var file && FindWindow(GuiParser.Parse(file.Text), name) is not null)
            {
                if (ReferenceEquals(ClassicWindows.Winner(Index, name), mods[0]))
                {
                    var renames = CopyConstants(node, file);
                    Replace(file, name, Snippet(node, renames));
                    _entries.Add(Entry(GuiPlanEntryKind.Replace, node, file) with { Rule = ClassicTargetRule.ExistingWinner });
                    _written.Add((name, file.Path));
                    return;
                }

                if (Remove(file, name))
                {
                    // Gegen wen die Mod-Datei verliert, nennt die Rückfrage vor dem Umzug (Vanilla oder eine Abhängigkeits-Mod).
                    var winner = ClassicWindows.Winner(Index, name);
                    _entries.Add(new GuiPlanEntry(GuiPlanEntryKind.RemoveFromMod, name, GuiDefinitionKind.NamedWidget, file.Path, 0)
                    {
                        LosesToDependency = winner?.File.Layer == GuiLayer.Dependency ? winner.File.Label : null,
                    });
                }
            }

            IntoTarget(GuiPlanEntryKind.Override, node);
        }

        public void Added(GuiNode node)
        {
            var name = ClassicWindows.NameOf(node);
            var existing = ClassicWindows.FindAll(Index, name);
            if (existing.Count > 0)
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.NameTaken, name, existing.Select(Place).ToList()));
                return;
            }

            IntoTarget(GuiPlanEntryKind.NewDefinition, node);
        }

        public void ModChangedSince(IEnumerable<GuiNode> nodes)
        {
            foreach (var node in nodes)
            {
                var name = ClassicWindows.NameOf(node);
                var places = ClassicWindows.FindAll(Index, name)
                    .Where(d => d.File.Layer is GuiLayer.Mod or GuiLayer.Dependency)
                    .Select(Place)
                    .ToList();
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.ModChangedSince, name, places));
            }
        }

        /// <summary>
        /// Die ganze Datei kommt unter gleichem relativem Pfad in die Mod und ersetzt die Vanilla-Datei. Nur aus der Installation selbst: Ob eine Mod-Datei
        /// eine Datei eines DLC-Ordners ersetzt, ist nicht gemessen.
        /// </summary>
        public void CopyFile(GuiCopyReason reason, GuiDocument baseline, OrderedWindows before, OrderedWindows after)
        {
            var source = _request.Source;
            if (!ClassicOverrideTargets.IsInstall(source))
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.CopyNotPossible, source.RelativePath, new[] { source.Label }));
                return;
            }

            var path = ClassicOverrideTargets.FullPathOf(ModRoot, source.RelativePath);
            var copy = File(path);
            copy.Text = _request.DraftText;
            _entries.Add(new GuiPlanEntry(GuiPlanEntryKind.CopyFile, source.RelativePath, null, path, 0, reason.ToString()));
            foreach (var (name, node) in after)
            {
                var existing = ClassicWindows.FindAll(Index, name);
                if (!before.TryGetValue(name, out var old) && existing.Count > 0)
                {
                    _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.NameTaken, name, existing.Select(Place).ToList()));
                    continue;
                }

                _entries.Add(Entry(GuiPlanEntryKind.CopiedDefinition, node, copy));
                if (Home(node) is not null)
                {
                    // Die Mod-Fassung braucht die Konstanten ihrer Mod-Datei, nicht die der Vanilla-Datei.
                    var renames = CopyConstants(node, copy);
                    if (renames.Count > 0)
                    {
                        Replace(copy, name, Snippet(node, renames));
                    }
                }

                // Vor dem Entfernen: Gewinnt heute eine Mod-Fassung, muss die Kopie danach ebenso gewinnen.
                var modWins = ClassicWindows.Winner(Index, name)?.File.Layer == GuiLayer.Mod;
                var moved = RemoveFromMod(name, path);

                // Geprüft werden Fenster, die der Entwurf geändert oder neu angelegt hat, und Mod-Fenster, die mit der Kopie umziehen (sie könnten danach
                // gegen eine Abhängigkeit oder Vanilla verlieren). Unberührte Vanilla-Fenster bleiben, wie sie im Spiel sind.
                if (old is null || baseline.TextOf(old) != _draft.TextOf(node) || moved || modWins)
                {
                    _written.Add((name, path));
                }
            }

            // Ein gelöschtes Fenster darf auch in den übrigen Mod-Dateien nicht stehen bleiben.
            foreach (var name in before.Names.Where(name => !after.Contains(name)))
            {
                RemoveFromMod(name, path);
            }
        }

        public GuiOverridePlan Build(GuiCopyReason? reason)
        {
            var newline = _request.DraftText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var files = new List<GuiPlanFile>();
            var missing = new List<GuiPlanFile>();
            foreach (var file in _files)
            {
                // Eine Datei, die der Index kennt, die es auf der Platte aber nicht gibt (veralteter Index), und in die nichts kam: keine leere neue Datei
                // planen. Für die Nachprüfung gilt sie als leer, so wie sie auf der Platte ist.
                if (file.Old is null && file.Text.Length == 0)
                {
                    missing.Add(new GuiPlanFile(file.Path, null, string.Empty));
                    continue;
                }

                var text = file.Old is null && newline == "\r\n" ? file.Text.Replace("\r\n", "\n").Replace("\n", "\r\n") : file.Text;
                if (text != file.Old)
                {
                    files.Add(new GuiPlanFile(file.Path, file.Old, text) { EncodingName = EncodingFor(file.Old, text) });
                }
            }

            CheckWinners(files.Concat(missing).ToList());
            var entries = _entries.Select(entry => entry with { Line = LineOf(entry, files) }).ToList();
            return new GuiOverridePlan(entries, files, _blockers, reason) { Warnings = _warnings };
        }

        /// <summary>Überschreibung oder neues Fenster: am Ende des ersten <c>guiTypes</c>-Blocks der Zieldatei.</summary>
        private void IntoTarget(GuiPlanEntryKind kind, GuiNode node)
        {
            var name = ClassicWindows.NameOf(node);
            var target = Target;
            var configured = target.Rule == ClassicTargetRule.Configured;

            // Ein gesetzter Name ist nur ein Dateiname: Mit Ordner landete die Datei woanders (EU IV nie in einem Unterordner von interface/).
            if (configured && !ClassicOverrideTargets.IsPlainFileName(_request.Targets.OverrideFileName))
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, name, new[] { _request.Targets.OverrideFileName })
                {
                    TargetProblem = GuiTargetProblem.InvalidName,
                    ConfiguredTarget = true,
                });
                return;
            }

            // Ein Name mit dem Pfad einer Vanilla- oder Abhängigkeits-Datei ersetzte diese ganz (Global Constraints): blockieren.
            if (ClassicOverrideTargets.Colliding(Index, target.RelativePath) is { } replaced)
            {
                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, name, new[] { target.RelativePath, replaced.RelativePath })
                {
                    TargetProblem = GuiTargetProblem.ReplacesFile,
                    ConfiguredTarget = configured,
                });
                return;
            }

            var file = File(target.FullPath);
            var renames = CopyConstants(node, file);
            Insert(file, Snippet(node, renames));
            _entries.Add(Entry(kind, node, file) with { Rule = target.Rule });
            _written.Add((name, file.Path));
            // Unsicher nur, wenn der Gewinner an einer DLC-Datei hängen kann: Quelle aus einem DLC-Ordner oder eine DLC-Datei, die dieses Fenster definiert.
            if (target.Uncertain || ClassicWindows.FindAll(Index, name).Any(d => d.File.Layer == GuiLayer.Vanilla && !ClassicOverrideTargets.IsInstall(d.File)))
            {
                _warnings.Add(new GuiPlanWarning(GuiPlanWarningKind.UncertainOrder, name, new[] { target.RelativePath }));
            }
        }

        /// <summary>
        /// Nachprüfung: Im Index nach dem Schreiben muss jedes geschriebene Fenster aus der geplanten Datei gewinnen. Gewinnt eine Abhängigkeits-Mod, ist
        /// das ein Hinweis (sie lädt nach der Mod oder vor ihr, je nach Spiel, und das will der Nutzer womöglich so); sonst blockiert der Plan.
        /// </summary>
        private void CheckWinners(IReadOnlyList<GuiPlanFile> files)
        {
            if (_written.Count == 0)
            {
                return;
            }

            var simulated = Simulate(Index, files, ModRoot);
            foreach (var (name, path) in _written.Distinct())
            {
                var configured = _target is { Rule: ClassicTargetRule.Configured } target && SamePath(target.FullPath, path);
                var planned = ClassicWindows.FindAll(simulated, name).FirstOrDefault(d => SamePath(d.File.FullPath, path));
                if (planned is null)
                {
                    // Das Fenster steht nach dem Schreiben nicht als oberstes Fenster in der Datei: nicht still durchwinken.
                    _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, name, new[] { RelativeOf(path) })
                    {
                        TargetProblem = GuiTargetProblem.NotFound,
                        ConfiguredTarget = configured,
                    });
                    continue;
                }

                var winner = ClassicWindows.Winner(simulated, name)!;
                if (SamePath(winner.File.FullPath, path))
                {
                    continue;
                }

                if (winner.File.Layer == GuiLayer.Dependency)
                {
                    _warnings.Add(new GuiPlanWarning(GuiPlanWarningKind.DependencyWins, name, new[] { Place(winner) }));
                    continue;
                }

                _blockers.Add(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, name, new[] { Place(planned), Place(winner) })
                {
                    TargetProblem = GuiTargetProblem.Loses,
                    ConfiguredTarget = configured,
                });
            }
        }

        /// <summary>
        /// Eine neue Datei mit Nicht-ASCII-Text aus einer Windows-1252-Quelle bleibt Windows-1252 (wie die Vanilla-Datei), aber nur, wenn jedes Zeichen
        /// dort darstellbar ist; sonst UTF-8, damit nichts zu "?" wird.
        /// </summary>
        private string? EncodingFor(string? old, string text) =>
            old is null && _request.SourceEncoding == GuiTextReader.Windows1252 && text.Any(c => c > '\u007f') && FitsWindows1252(text)
                ? GuiTextReader.Windows1252
                : null;

        private static bool FitsWindows1252(string text)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            try
            {
                encoding.GetByteCount(text);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        /// <returns>Ob eine Mod-Datei das Fenster verliert.</returns>
        private bool RemoveFromMod(string name, string copyPath)
        {
            var removed = false;
            foreach (var mod in ClassicWindows.FindAll(Index, name).Where(d => d.File.Layer == GuiLayer.Mod && !SamePath(d.File.FullPath, copyPath)))
            {
                var file = File(mod.File.FullPath);
                if (Remove(file, name))
                {
                    _entries.Add(new GuiPlanEntry(GuiPlanEntryKind.RemoveFromMod, name, GuiDefinitionKind.NamedWidget, file.Path, 0));
                    removed = true;
                }
            }

            return removed;
        }

        /// <summary>Das Fenster in der Mod- oder Abhängigkeits-Datei, aus der der Entwurf seinen Text hat; null bei Vanilla-Fenstern.</summary>
        private GuiNode? Home(GuiNode node) => GuiDraft.ModDefinition(Index, node)?.Node;

        /// <summary>Fenster als Schnipsel (Einrückung als Tabs) mit umbenannten Konstanten.</summary>
        private string Snippet(GuiNode node, IReadOnlyDictionary<string, string> renames) =>
            GuiConstants.Rename(_draftEdits.Dedent(node), renames);

        /// <summary>Kopiert die Konstanten des Fensters auf die oberste Ebene der Zieldatei (siehe <see cref="GuiConstantCopy"/>).</summary>
        private Dictionary<string, string> CopyConstants(GuiNode node, TargetFile file)
        {
            var text = file.Text;
            var renames = GuiConstantCopy.Copy(node, Home(node), ref text, _stem, _entries, file.Path);
            file.Text = text;
            return renames;
        }

        /// <summary>Fügt das Fenster am Ende des ersten obersten <c>guiTypes</c>-Blocks ein (jede Schreibweise); fehlt er, kommt <c>guiTypes = { }</c> ans Ende.</summary>
        private static void Insert(TargetFile file, string snippet)
        {
            var document = GuiParser.Parse(file.Text);
            if (ClassicWindows.ContainerOf(document) is not { } container)
            {
                file.Text = TextEdit.Apply(file.Text, new GuiTextEdits(document).InsertChild(document.Root, "guiTypes = {\n}", InsertPosition.LastChild));
                document = GuiParser.Parse(file.Text);
                container = ClassicWindows.ContainerOf(document)!;
            }

            file.Text = TextEdit.Apply(file.Text, new GuiTextEdits(document).InsertChild(container, snippet, InsertPosition.LastChild));
        }

        private static bool Replace(TargetFile file, string name, string snippet)
        {
            var document = GuiParser.Parse(file.Text);
            if (FindWindow(document, name) is not { } node)
            {
                return false;
            }

            var replacement = new GuiTextEdits(document).ReplacementFor(node, snippet);
            file.Text = TextEdit.Apply(file.Text, new[] { new TextEdit(node.Span.Start, node.Span.Length, replacement) });
            return true;
        }

        private static bool Remove(TargetFile file, string name)
        {
            var document = GuiParser.Parse(file.Text);
            if (FindWindow(document, name) is not { } node)
            {
                return false;
            }

            file.Text = TextEdit.Apply(file.Text, new GuiTextEdits(document).RemoveNode(node));
            return true;
        }

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

        /// <summary>Klassische Fenster haben keine Verwendungen wie Typen (sie werden nicht über ihren Schlüssel eingesetzt): <c>Usages</c> bleibt 0.</summary>
        private static GuiPlanEntry Entry(GuiPlanEntryKind kind, GuiNode node, TargetFile file) =>
            new(kind, ClassicWindows.NameOf(node), GuiDefinitionKind.NamedWidget, file.Path, 0);

        private static int LineOf(GuiPlanEntry entry, IReadOnlyList<GuiPlanFile> files)
        {
            if (entry.DefinitionKind is null
                || entry.Kind is GuiPlanEntryKind.RemoveFromMod
                || files.FirstOrDefault(f => SamePath(f.FullPath, entry.TargetPath)) is not { } file)
            {
                return 0;
            }

            return FindWindow(GuiParser.Parse(file.NewText), entry.Name)?.Line ?? 0;
        }

        private string RelativeOf(string fullPath) => System.IO.Path.GetRelativePath(ModRoot, fullPath).Replace('\\', '/');
    }
}
