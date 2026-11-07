using ParadoxModTool.FlagTracker;

namespace ParadoxModTool.ScriptCheck;

/// <summary>Welche Namen der Builder in allen Dateien als Erwähnung sammelt.</summary>
public enum ScriptCheckNames
{
    /// <summary>Nur Namen, die die gewählte Mod definiert (Prüfung der eigenen Mod).</summary>
    Mod,

    /// <summary>
    /// Jeder Effekt, Trigger und Script-Wert jeder Datei (Prüfung von Vanilla und fremden Mods). Der Index taugt für Leistungsfunde
    /// (<see cref="PerformanceAnalyzer"/>) auf fremden Zielen; andere Fundarten aus diesem Index sind nicht aussagekräftig
    /// (sie sind auf <see cref="Mod"/> ausgelegt und mit diesem Modus nicht geprüft).
    /// </summary>
    All,
}

/// <summary>
/// Baut den <see cref="ScriptCheckIndex"/> eines Bereichs in drei Durchgängen: Dateien lesen (Definitionen, Event-Aufrufe), Erwähnungen der Namen
/// der gewählten Mod in allen Dateien sammeln, Aufrufformen in den <c>.txt</c>-Dateien der gewählten Mod sammeln.
/// </summary>
public sealed class ScriptCheckBuilder
{
    /// <summary>
    /// Version des Lesers (<see cref="ScriptCheckReader"/>). Erhöhen, sobald sich sein Ergebnis für denselben Text ändert:
    /// Gespeicherte Leseergebnisse einer anderen Version werden dann nicht wiederverwendet.
    /// </summary>
    public const int ReaderVersion = 20; // 16: CheckDefinition.EndLine. 17: Iteratoren, geprüfte Blöcke, On-Action-Aufrufe, Polled. 18: Polled ohne is_triggered_only, Hülle *, NameField. 19: ExcludeFolders. 20: CheckScan.CheckedCalls, NameField benennt Blöcke oberster Ebene

    /// <summary>Alle so viele bearbeiteten Dateien wird Fortschritt gemeldet.</summary>
    private const int ProgressStep = 50;

    private static readonly string[] ScriptExtensions = { ".txt", ".gui", ".gfx" };

    private readonly FlagSourceSet _sources;
    private readonly ScriptCheckRules _rules;
    private readonly ScriptCheckNames _names;
    private readonly ScriptCheckReader _reader;

    private readonly Func<string, string> _readText;
    private readonly string[] _extensions;

    public ScriptCheckBuilder(FlagSourceSet sources, ScriptCheckRules rules)
        : this(sources, rules, FlagIndexBuilder.ReadText)
    {
    }

    /// <summary>Für Tests: <paramref name="readText"/> liest den Text einer Datei (voller Pfad).</summary>
    internal ScriptCheckBuilder(FlagSourceSet sources, ScriptCheckRules rules, Func<string, string> readText)
        : this(sources, rules, ScriptCheckNames.Mod, readText)
    {
    }

    /// <summary>Wie der Grundkonstruktor; mit <see cref="ScriptCheckNames.All"/> werden die Erwähnungen aller Effekt-, Trigger- und Wertnamen gesammelt.</summary>
    public ScriptCheckBuilder(FlagSourceSet sources, ScriptCheckRules rules, ScriptCheckNames names)
        : this(sources, rules, names, FlagIndexBuilder.ReadText)
    {
    }

    internal ScriptCheckBuilder(FlagSourceSet sources, ScriptCheckRules rules, ScriptCheckNames names, Func<string, string> readText)
    {
        _names = names;
        _sources = sources;
        _rules = rules;
        _reader = new ScriptCheckReader(rules);
        _readText = readText;
        // Lokalisierung (.yml, wie im Flag-Tracker: englische, sonst alle Sprachen einer Wurzel ohne Englisch) wird nur gelesen, wenn das Spiel Funktionen kennt, deren Aufruf einen Namen verwendet.
        _extensions = rules.LocCallFunctions.Count > 0 ? ScriptExtensions.Append(".yml").ToArray() : ScriptExtensions;
    }

    /// <summary>
    /// Liest jede Datei (<c>.txt</c>, <c>.gui</c>, <c>.gfx</c>) parallel, sammelt die Erwähnungen in jeder Datei, deren Text einen Namen der
    /// gewählten Mod enthalten kann, und die Aufrufformen der Mod. Fehler einer Datei landen in UnreadFiles (voller Pfad). Schreibt nichts auf die Platte.
    /// </summary>
    /// <remarks>
    /// Fortschritt je <see cref="BuildPhase"/> der Reihe nach: (Phase, 0, Total), dann alle 50 Dateien, dann (Phase, Total, Total); bei Total 0 nur
    /// (Phase, 0, 0). Reading zählt die gelesenen Dateien (jede neue Datei wird genau einmal gelesen; außerhalb der Mod gleich mit der Suche nach
    /// Erwähnungen), Calls die danach durchsuchten (neu gelesene Mod-Dateien, bei anderen Namen auch alle unveränderten), Resolving die
    /// <c>.txt</c>-Dateien der Mod.
    /// Gemeldet wird aus Arbeits-Threads, nacheinander; Aufrufer sollten <see cref="Progress{T}"/> übergeben.
    /// </remarks>
    public ScriptCheckIndex Build(CancellationToken ct = default, IProgress<BuildProgress>? progress = null) => Run(null, ct, progress);

    /// <summary>
    /// Wie <see cref="Build"/>, nutzt aber die gespeicherten Leseergebnisse des Bereichs (<see cref="FlagSourceSet.ScopeKey"/>): Gelesen werden nur
    /// neue Dateien und solche mit anderer Länge oder Änderungszeit; verschwundene entfallen. Sind die Namen der Mod samt Art (als Menge) dieselben wie
    /// gespeichert, werden die Erwähnungen unveränderter Dateien übernommen und nur neu gelesene durchsucht, sonst alle. Die Aufrufformen der Mod
    /// werden immer neu gesammelt. Danach wird gespeichert (Speicherfehler werden geschluckt). Anderer <see cref="ReaderVersion"/> oder anderer
    /// Regel-Fingerabdruck = voller Aufbau.
    /// </summary>
    public ScriptCheckIndex Refresh(ScriptCheckStore store, CancellationToken ct = default, IProgress<BuildProgress>? progress = null) =>
        Run(store, ct, progress);

    private ScriptCheckIndex Run(ScriptCheckStore? store, CancellationToken ct, IProgress<BuildProgress>? progress)
    {
        ct.ThrowIfCancellationRequested();
        var reporter = new Reporter(progress);
        var options = new ParallelOptions { CancellationToken = ct };
        var modRoot = _sources.Roots.FirstOrDefault(r => r.Origin == FlagOrigin.ActiveMod);
        var all = _names == ScriptCheckNames.All;
        var fingerprint = all ? _rules.Fingerprint + "|all" : _rules.Fingerprint;

        var entries = _sources.EnumerateFiles(_extensions).Where(e => !_rules.IsUnder(e.RelativePath, _rules.IgnoredFolders, true)).ToList();
        var stored = store?.Load(_sources.ScopeKey);
        if (stored is not null && (stored.ReaderVersion != ReaderVersion || stored.RulesFingerprint != fingerprint))
        {
            // Mit anderem Leser oder anderen Regeln gelesen: wie kein Speicher behandeln.
            stored = null;
        }

        var storedByKey = new Dictionary<string, (FileStamp Stamp, CheckScan Scan)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in stored?.Files ?? Array.Empty<(FileStamp, CheckScan)>())
        {
            storedByKey.TryAdd(KeyOf(entry.Stamp), entry);
        }

        var count = entries.Count;
        var slots = new CheckFile?[count];
        var stamps = new FileStamp?[count];
        var texts = new string?[count];
        var isFresh = new bool[count];
        var needsRead = new bool[count];
        var unread = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Durchgang 1, Reading: Stand jeder Datei prüfen und unveränderte übernehmen; neue und geänderte Dateien werden genau einmal gelesen,
        // zuerst die der Mod (sie liefern die Namen), dann die übrigen, die dabei gleich nach Erwähnungen durchsucht werden.
        Parallel.For(0, count, options, i =>
        {
            var (root, relative) = entries[i];
            var location = new SiteLocation(root.Directory, relative, 0);
            FileStamp stamp;
            try
            {
                var info = new FileInfo(location.FullPath);
                stamp = new FileStamp(root.Directory, relative, info.Length, info.LastWriteTimeUtc.Ticks);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AddUnread(unread, location.FullPath);
                return;
            }

            stamps[i] = stamp;
            if (storedByKey.TryGetValue(KeyOf(stamp), out var old) && SameStamp(old.Stamp, stamp))
            {
                slots[i] = new CheckFile(location, root.Origin, root.Name, old.Scan);
            }
            else
            {
                needsRead[i] = true;
            }
        });

        bool IsMod(int i) => entries[i].Root.Origin == FlagOrigin.ActiveMod;
        var toRead = Enumerable.Range(0, count).Where(i => needsRead[i]).ToList();
        var modFresh = toRead.Where(IsMod).ToList();
        var otherFresh = toRead.Where(i => !IsMod(i)).ToList();
        string ReadSlot(int i)
        {
            var (root, relative) = entries[i];
            var location = new SiteLocation(root.Directory, relative, 0);
            var text = _readText(location.FullPath);
            slots[i] = new CheckFile(location, root.Origin, root.Name, _reader.Read(relative, text));
            isFresh[i] = true;
            return text;
        }

        // Die Texte der Mod bleiben für die Erwähnungen und die Aufrufformen (Durchgang 3) im Speicher; die Mod ist klein.
        ForEachReported(BuildPhase.Reading, modFresh, options, reporter, unread, i => PathOf(entries[i]), i => texts[i] = ReadSlot(i), 0, toRead.Count);

        // Mit All liefern auch die übrigen Dateien Namen: erst alle lesen (ohne Erwähnungen), die werden in Durchgang 2 gesucht.
        if (all)
        {
            ForEachReported(BuildPhase.Reading, otherFresh, options, reporter, unread, i => PathOf(entries[i]), i => ReadSlot(i), modFresh.Count, toRead.Count);
        }

        // Namen der gewählten Mod: jede Definition in ihren Dateien; bei zwei Arten zählt die zuerst gesehene.
        var names = new Dictionary<string, CheckKind>(StringComparer.Ordinal);
        // Mit All: jeder Effekt, Trigger und Wert aller Dateien (Events und reine Mod-Namen kommen nicht dazu).
        foreach (var i in Enumerable.Range(0, count).Where(i => slots[i] is not null && (all || IsMod(i))))
        {
            foreach (var definition in slots[i]!.Scan.Definitions.Where(d => all
                ? d.Kind is CheckKind.Effect or CheckKind.Trigger or CheckKind.Value
                : d.Kind != CheckKind.Named))
            {
                names.TryAdd(definition.Name, definition.Kind);
            }
        }

        var nameSet = new HashSet<string>(names.Keys, StringComparer.Ordinal);
        var nameKeys = names.Select(n => KindName(n.Value, n.Key)).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<CheckRef> MentionsIn(int i, string text) =>
            !MentionWords.MayMention(text, nameSet)
                ? Array.Empty<CheckRef>()
                : ScriptCheckReader.IsLocalisation(slots[i]!.File.RelativePath)
                    ? _reader.ReadLocMentions(slots[i]!.File.RelativePath, text, names)
                    : _reader.ReadMentions(slots[i]!.File.RelativePath, text, names);

        // Gleiche Namen samt Art wie gespeichert: Erwähnungen unveränderter Dateien übernehmen.
        var mentions = new IReadOnlyList<CheckRef>?[count];
        var reuseMentions = stored?.MentionNames is { } storedNames && stored.Mentions is not null && nameKeys.SetEquals(storedNames);
        if (reuseMentions)
        {
            var storedMentions = stored!.Mentions!.GroupBy(m => KeyOf(m.Stamp), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var i in Enumerable.Range(0, count).Where(i => slots[i] is not null && !isFresh[i]))
            {
                mentions[i] = storedMentions.TryGetValue(KeyOf(stamps[i]!), out var list)
                    ? list.Where(m => SameStamp(m.Stamp, stamps[i]!)).Select(m => m.Ref).ToList()
                    : Array.Empty<CheckRef>();
            }
        }

        if (!all)
        {
            ForEachReported(BuildPhase.Reading, otherFresh, options, reporter, unread, i => PathOf(entries[i]), i =>
            {
                var text = ReadSlot(i);
                mentions[i] = MentionsIn(i, text);
            }, modFresh.Count, toRead.Count);
        }

        var present = Enumerable.Range(0, count).Where(i => slots[i] is not null).ToList();
        string TextOf(int i) => texts[i] ?? _readText(slots[i]!.File.FullPath);

        // Durchgang 2, Calls: neu gelesene Mod-Dateien; bei anderen Namen auch alle unveränderten Dateien (die werden dafür erneut gelesen).
        ct.ThrowIfCancellationRequested();
        var toMention = names.Count == 0 ? new List<int>() : present.Where(i => isFresh[i] ? (IsMod(i) || all) : !reuseMentions).ToList();
        ForEachReported(BuildPhase.Calls, toMention, options, reporter, unread, i => slots[i]!.File.FullPath, i =>
        {
            var text = TextOf(i);
            if (IsMod(i))
            {
                texts[i] = text;
            }

            mentions[i] = MentionsIn(i, text);
        });

        // Durchgang 3: Aufrufformen in den .txt-Dateien der Mod, immer neu.
        ct.ThrowIfCancellationRequested();
        var blockCalls = new IReadOnlyList<CheckRef>?[count];
        var toBlock = present
            .Where(i => slots[i]!.Origin == FlagOrigin.ActiveMod && slots[i]!.File.RelativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .ToList();
        ForEachReported(BuildPhase.Resolving, toBlock, options, reporter, unread, i => slots[i]!.File.FullPath, i =>
        {
            blockCalls[i] = _reader.ReadBlockCalls(slots[i]!.File.RelativePath, TextOf(i));
        });

        var replaced = ReadReplacedDefinitions(present.Where(IsMod).Select(i => slots[i]!.File.RelativePath), options);

        var index = new ScriptCheckIndex(
            _sources,
            modRoot,
            present.Select(i => slots[i]!).ToList(),
            Locate(present, slots, i => slots[i]!.Scan.EventCalls),
            Locate(present, slots, i => mentions[i]),
            Locate(present, slots, i => blockCalls[i]),
            unread.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            DateTime.UtcNow,
            replaced);

        if (store is not null)
        {
            Save(store, index, present.Where(i => !unread.Contains(slots[i]!.File.FullPath)).ToList(), stamps, slots, mentions, nameKeys, fingerprint);
        }

        return index;
    }

    /// <summary>
    /// Die Definitionen der Dateien anderer Wurzeln, die denselben relativen Pfad wie eine Datei der Mod haben (siehe <see cref="ScriptCheckIndex.ReplacedDefinitions"/>).
    /// Wenige Dateien, darum jedes Mal neu gelesen; eine unlesbare Datei liefert nichts, ist aber auch kein Lesefehler der Prüfung (sie gilt ja nicht).
    /// </summary>
    private IReadOnlyList<CheckDefinition> ReadReplacedDefinitions(IEnumerable<string> modFiles, ParallelOptions options)
    {
        var others = _sources.Roots.Where(r => r.Origin != FlagOrigin.ActiveMod).ToList();
        var result = new List<CheckDefinition>();
        // Nur .txt trägt Definitionen; .gui und .gfx würden umsonst gelesen und geparst.
        Parallel.ForEach(modFiles.Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)), options, relative =>
        {
            foreach (var root in others)
            {
                var path = new SiteLocation(root.Directory, relative, 0).FullPath;
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    var definitions = _reader.Read(relative, _readText(path)).Definitions;
                    lock (result)
                    {
                        result.AddRange(definitions);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                }
            }
        });
        return result;
    }

    /// <summary>Speichert Dateien und Erwähnungen der lesbaren Dateien; ein Speicherfehler kostet nur den Zeitgewinn beim nächsten Mal.</summary>
    private void Save(
        ScriptCheckStore store, ScriptCheckIndex index, IReadOnlyList<int> saved, FileStamp?[] stamps, CheckFile?[] slots,
        IReadOnlyList<CheckRef>?[] mentions, IReadOnlySet<string> names, string fingerprint)
    {
        var files = saved.Select(i => (stamps[i]!, slots[i]!.Scan)).ToList();
        var storedMentions = saved.SelectMany(i => (mentions[i] ?? Array.Empty<CheckRef>()).Select(r => new StoredMention(stamps[i]!, r))).ToList();
        try
        {
            store.Save(_sources.ScopeKey, new StoredCheck(
                ScriptCheckStore.FormatVersion, index.BuiltUtc, ReaderVersion, fingerprint, files,
                names.OrderBy(n => n, StringComparer.Ordinal).ToList(), storedMentions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
        {
        }
    }

    /// <summary>
    /// Bearbeitet die Einträge parallel und meldet den Fortschritt der Phase (siehe <see cref="Build"/>). Wirft <paramref name="work"/>
    /// (außer bei Abbruch), landet der Pfad des Eintrags in <paramref name="unread"/>. Läuft eine Phase in mehreren Teilen, zählt
    /// <paramref name="doneBefore"/> die Einträge früherer Teile und <paramref name="phaseTotal"/> alle Einträge der Phase.
    /// </summary>
    private static void ForEachReported(
        BuildPhase phase, IReadOnlyList<int> items, ParallelOptions options, Reporter reporter, HashSet<string> unread,
        Func<int, string> pathOf, Action<int> work, int doneBefore = 0, int? phaseTotal = null)
    {
        var total = phaseTotal ?? items.Count;
        var done = doneBefore;
        var end = doneBefore + items.Count;
        reporter.Report(new BuildProgress(phase, doneBefore, total));
        Parallel.For(0, items.Count, options, n =>
        {
            try
            {
                work(items[n]);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AddUnread(unread, pathOf(items[n]));
            }

            var now = Interlocked.Increment(ref done);
            if (now % ProgressStep == 0 && now < end)
            {
                reporter.Report(new BuildProgress(phase, now, total));
            }
        });
        reporter.Report(new BuildProgress(phase, end, total));
    }

    /// <summary>Verweise der Dateien in Dateireihenfolge, jeweils mit dem Ort der Datei und der Zeile des Verweises.</summary>
    private static List<LocatedRef> Locate(IReadOnlyList<int> present, CheckFile?[] slots, Func<int, IReadOnlyList<CheckRef>?> refsOf)
    {
        var result = new List<LocatedRef>();
        foreach (var i in present)
        {
            var file = slots[i]!.File;
            foreach (var r in refsOf(i) ?? Array.Empty<CheckRef>())
            {
                result.Add(new LocatedRef(file with { Line = r.Line }, r));
            }
        }

        return result;
    }

    private static void AddUnread(HashSet<string> unread, string path)
    {
        lock (unread)
        {
            unread.Add(path);
        }
    }

    private static string PathOf((FlagRoot Root, string RelativePath) entry) => new SiteLocation(entry.Root.Directory, entry.RelativePath, 0).FullPath;

    private static string KeyOf(FileStamp stamp) => stamp.RootDirectory + "|" + stamp.RelativePath;

    /// <summary>Gespeicherter Schlüssel eines Namens samt Art (<c>Effect:my_fx</c>): Wechselt ein Name die Art, werden die Erwähnungen neu gesammelt.</summary>
    private static string KindName(CheckKind kind, string name) => kind + ":" + name;

    private static bool SameStamp(FileStamp a, FileStamp b) => a.Length == b.Length && a.LastWriteUtcTicks == b.LastWriteUtcTicks;

    /// <summary>Wie im Flag-Tracker: meldet nacheinander und verwirft veraltete Meldungen (frühere Phase oder nicht steigendes Done).</summary>
    private sealed class Reporter
    {
        private readonly IProgress<BuildProgress>? _progress;
        private readonly object _gate = new();
        private BuildProgress? _last;

        public Reporter(IProgress<BuildProgress>? progress) => _progress = progress;

        public void Report(BuildProgress value)
        {
            if (_progress is null)
            {
                return;
            }

            lock (_gate)
            {
                if (_last is { } last && (value.Phase < last.Phase || (value.Phase == last.Phase && value.Done <= last.Done)))
                {
                    return;
                }

                _last = value;
                _progress.Report(value);
            }
        }
    }
}
