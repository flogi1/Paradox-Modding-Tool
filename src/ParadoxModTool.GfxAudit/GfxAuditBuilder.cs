using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.GfxAudit;

/// <summary>
/// Baut den <see cref="GfxAuditIndex"/> eines Bereichs: Textdateien lesen (Sprites, Bild- und Sprite-Verweise), Erwähnungen der Namen der gewählten
/// Mod (ihre Bilder und Sprites) in allen Textdateien sammeln und die wirksamen Bilder aller Wurzeln auflisten. Aufbau wie <see cref="ScriptCheckBuilder"/>.
/// </summary>
public sealed class GfxAuditBuilder
{
    /// <summary>
    /// Version des Lesers (<see cref="GfxFileReader"/>). Erhöhen, sobald sich sein Ergebnis für denselben Text ändert:
    /// Gespeicherte Leseergebnisse einer anderen Version werden dann nicht wiederverwendet.
    /// </summary>
    public const int ReaderVersion = 5;

    /// <summary>Alle so viele bearbeiteten Dateien wird Fortschritt gemeldet.</summary>
    private const int ProgressStep = 50;

    private static readonly string[] ImageExtensions = { ".dds", ".png", ".tga" };

    private const string ImageKind = "img:";
    private const string SpriteKind = "spr:";

    private readonly FlagSourceSet _sources;
    private readonly GfxAuditRules _rules;
    private readonly GfxFileReader _reader;
    private readonly Func<string, string> _readText;

    public GfxAuditBuilder(FlagSourceSet sources, GfxAuditRules rules)
        : this(sources, rules, FlagIndexBuilder.ReadText)
    {
    }

    /// <summary>Für Tests: <paramref name="readText"/> liest den Text einer Datei (voller Pfad).</summary>
    internal GfxAuditBuilder(FlagSourceSet sources, GfxAuditRules rules, Func<string, string> readText)
    {
        _sources = sources;
        _rules = rules;
        _reader = new GfxFileReader(rules);
        _readText = readText;
    }

    /// <summary>
    /// Liest jede Textdatei (<see cref="GfxAuditRules.TextExtensions"/>) parallel, sammelt die Erwähnungen in jeder Datei, deren Text einen Namen der
    /// gewählten Mod enthalten kann, und listet die wirksamen Bilder auf. Fehler einer Datei landen in UnreadFiles (voller Pfad). Schreibt nichts auf die Platte.
    /// </summary>
    /// <remarks>
    /// Fortschritt je <see cref="BuildPhase"/> der Reihe nach: (Phase, 0, Total), dann alle 50 Einträge, dann (Phase, Total, Total); bei Total 0 nur
    /// (Phase, 0, 0). Reading zählt die gelesenen Dateien (jede neue Datei wird genau einmal gelesen; außerhalb der Mod gleich mit der Suche nach
    /// Erwähnungen), Calls die danach durchsuchten (neu gelesene Mod-Dateien, bei anderen Namen auch alle unveränderten), Resolving die Bilder.
    /// Gemeldet wird aus Arbeits-Threads, nacheinander; Aufrufer sollten <see cref="Progress{T}"/> übergeben.
    /// </remarks>
    public GfxAuditIndex Build(CancellationToken ct = default, IProgress<BuildProgress>? progress = null) => Run(null, ct, progress);

    /// <summary>
    /// Wie <see cref="Build"/>, nutzt aber die gespeicherten Leseergebnisse des Bereichs (<see cref="FlagSourceSet.ScopeKey"/>): Gelesen werden nur
    /// neue Dateien und solche mit anderer Länge oder Änderungszeit; verschwundene entfallen. Sind die Namen der Mod samt Art (als Menge) dieselben wie
    /// gespeichert, werden die Erwähnungen unveränderter Dateien übernommen und nur neu gelesene durchsucht, sonst alle. Danach wird gespeichert
    /// (Speicherfehler werden geschluckt). Anderer <see cref="ReaderVersion"/> oder anderer Regel-Fingerabdruck = voller Aufbau.
    /// </summary>
    public GfxAuditIndex Refresh(GfxAuditStore store, CancellationToken ct = default, IProgress<BuildProgress>? progress = null) =>
        Run(store, ct, progress);

    private GfxAuditIndex Run(GfxAuditStore? store, CancellationToken ct, IProgress<BuildProgress>? progress)
    {
        ct.ThrowIfCancellationRequested();
        var reporter = new Reporter(progress);
        var options = new ParallelOptions { CancellationToken = ct };
        var modRoot = _sources.Roots.FirstOrDefault(r => r.Origin == FlagOrigin.ActiveMod);
        var fingerprint = _rules.Fingerprint;

        var entries = _sources.EnumerateFiles(_rules.TextExtensions).ToList();
        // Bilder werden nur aufgelistet, nie gelesen; ihre logischen Pfade sind Namen der Mod und werden vor den Erwähnungen gebraucht.
        var imageEntries = _sources.EnumerateFiles(ImageExtensions).ToList();
        var stored = store?.Load(_sources.ScopeKey);
        if (stored is not null && (stored.ReaderVersion != ReaderVersion || stored.RulesFingerprint != fingerprint))
        {
            // Mit anderem Leser oder anderen Regeln gelesen: wie kein Speicher behandeln.
            stored = null;
        }

        var storedByKey = new Dictionary<string, StoredGfxFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in stored?.Files ?? Array.Empty<StoredGfxFile>())
        {
            storedByKey.TryAdd(KeyOf(entry.Stamp), entry);
        }

        var count = entries.Count;
        var slots = new GfxFile?[count];
        var stamps = new FileStamp?[count];
        var texts = new string?[count];
        var isFresh = new bool[count];
        var needsRead = new bool[count];
        var unread = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Durchgang 1, Reading: Stand jeder Datei prüfen und unveränderte übernehmen; neue und geänderte Dateien werden genau einmal gelesen,
        // zuerst die der Mod (sie liefern die Sprite-Namen), dann die übrigen, die dabei gleich nach Erwähnungen durchsucht werden.
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
                slots[i] = new GfxFile(location, root.Origin, root.Name, old.Scan);
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
            slots[i] = new GfxFile(location, root.Origin, root.Name, _reader.Read(relative, text));
            isFresh[i] = true;
            return text;
        }

        // Die Texte der Mod bleiben für die Erwähnungen im Speicher; die Mod ist klein.
        ForEachReported(BuildPhase.Reading, modFresh, options, reporter, unread, i => PathOf(entries[i]), i => texts[i] = ReadSlot(i), 0, toRead.Count);

        var modSprites = Enumerable.Range(0, count).Where(i => slots[i] is not null && IsMod(i)).SelectMany(i => slots[i]!.Scan.Sprites.Select(s => s.Name));
        var names = MentionNames.Of(imageEntries.Where(e => e.Root.Origin == FlagOrigin.ActiveMod).Select(e => e.RelativePath), modSprites, _rules);
        IReadOnlyList<GfxMention> MentionsIn(int i, string text) =>
            names.MayMention(text) ? _reader.ReadMentions(slots[i]!.File.RelativePath, text, names.ByCandidate) : Array.Empty<GfxMention>();

        // Gleiche Namen samt Art wie gespeichert: Erwähnungen unveränderter Dateien übernehmen.
        var mentions = new IReadOnlyList<GfxMention>?[count];
        var reuseMentions = stored?.MentionNames is { } storedNames && names.Keys.SetEquals(storedNames);
        if (reuseMentions)
        {
            var storedMentions = stored!.Mentions.GroupBy(m => KeyOf(m.Stamp), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var i in Enumerable.Range(0, count).Where(i => slots[i] is not null && !isFresh[i]))
            {
                mentions[i] = storedMentions.TryGetValue(KeyOf(stamps[i]!), out var list)
                    ? list.Where(m => SameStamp(m.Stamp, stamps[i]!)).Select(m => m.Mention).ToList()
                    : Array.Empty<GfxMention>();
            }
        }

        ForEachReported(BuildPhase.Reading, otherFresh, options, reporter, unread, i => PathOf(entries[i]), i =>
        {
            var text = ReadSlot(i);
            mentions[i] = MentionsIn(i, text);
        }, modFresh.Count, toRead.Count);

        var present = Enumerable.Range(0, count).Where(i => slots[i] is not null).ToList();

        // Durchgang 2, Calls: neu gelesene Mod-Dateien; bei anderen Namen auch alle unveränderten Dateien (die werden dafür erneut gelesen).
        ct.ThrowIfCancellationRequested();
        var toMention = names.IsEmpty ? new List<int>() : present.Where(i => isFresh[i] ? IsMod(i) : !reuseMentions).ToList();
        ForEachReported(BuildPhase.Calls, toMention, options, reporter, unread, i => slots[i]!.File.FullPath, i =>
        {
            mentions[i] = MentionsIn(i, texts[i] ?? _readText(slots[i]!.File.FullPath));
        });

        // Durchgang 3, Resolving: die wirksamen Bilder. Ein Bild der Mod ersetzt ein vorhandenes, wenn eine niedriger gerankte Wurzel denselben
        // relativen Pfad hat (die Datei ist dann überschrieben und fehlt in der Liste) oder ein wirksames Bild mit demselben logischen Pfad
        // (EU V: in_game/gfx/x.dds der Mod gegen main_menu/gfx/x.dds von Vanilla).
        ct.ThrowIfCancellationRequested();
        var lower = modRoot is null ? new List<FlagRoot>() : _sources.Roots.Where(r => r.Rank < modRoot.Rank).ToList();
        var lowerLogical = modRoot is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : imageEntries.Where(e => e.Root.Rank < modRoot.Rank).Select(e => _rules.LogicalPath(e.RelativePath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var images = new GfxImage?[imageEntries.Count];
        ForEachReported(BuildPhase.Resolving, Enumerable.Range(0, imageEntries.Count).ToList(), options, reporter, unread,
            i => PathOf(imageEntries[i]), i =>
            {
                var (root, relative) = imageEntries[i];
                var replaces = root.Origin == FlagOrigin.ActiveMod
                    && (lowerLogical.Contains(_rules.LogicalPath(relative))
                        || lower.Any(r => File.Exists(new SiteLocation(r.Directory, relative, 0).FullPath)));
                images[i] = new GfxImage(new SiteLocation(root.Directory, relative, 0), root.Origin, _rules.LogicalPath(relative), replaces);
            });

        var index = new GfxAuditIndex(
            _sources,
            modRoot,
            present.Select(i => slots[i]!).ToList(),
            images.Where(i => i is not null).Select(i => i!).ToList(),
            present.SelectMany(i => (mentions[i] ?? Array.Empty<GfxMention>())
                .Select(m => new GfxMentionSite(m.Name, slots[i]!.File with { Line = m.Line }))).ToList(),
            unread.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(),
            DateTime.UtcNow);

        if (store is not null)
        {
            Save(store, present.Where(i => !unread.Contains(slots[i]!.File.FullPath)).ToList(), stamps, slots, mentions, names.Keys, fingerprint);
        }

        return index;
    }

    /// <summary>Speichert Dateien und Erwähnungen der lesbaren Dateien; ein Speicherfehler kostet nur den Zeitgewinn beim nächsten Mal.</summary>
    private void Save(
        GfxAuditStore store, IReadOnlyList<int> saved, FileStamp?[] stamps, GfxFile?[] slots, IReadOnlyList<GfxMention>?[] mentions,
        IReadOnlySet<string> names, string fingerprint)
    {
        var files = saved.Select(i => new StoredGfxFile(stamps[i]!, slots[i]!.Scan)).ToList();
        var storedMentions = saved.SelectMany(i => (mentions[i] ?? Array.Empty<GfxMention>()).Select(m => new StoredGfxMention(stamps[i]!, m))).ToList();
        try
        {
            store.Save(_sources.ScopeKey, new StoredGfx(
                ReaderVersion, fingerprint, names.OrderBy(n => n, StringComparer.Ordinal).ToList(), files, storedMentions));
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

    private static void AddUnread(HashSet<string> unread, string path)
    {
        lock (unread)
        {
            unread.Add(path);
        }
    }

    private static string PathOf((FlagRoot Root, string RelativePath) entry) => new SiteLocation(entry.Root.Directory, entry.RelativePath, 0).FullPath;

    private static string KeyOf(FileStamp stamp) => stamp.RootDirectory + "|" + stamp.RelativePath;

    private static bool SameStamp(FileStamp a, FileStamp b) => a.Length == b.Length && a.LastWriteUtcTicks == b.LastWriteUtcTicks;

    /// <summary>
    /// Die Namen der gewählten Mod mit ihren Kandidaten und dem Vorfilter. Bilder: logischer Pfad als Name, Kandidaten
    /// <see cref="GfxNames.Candidates"/> des Dateinamens ohne Endung sowie der logische Pfad mit und ohne Endung. Sprites: der Name mit seinen Kandidaten.
    /// </summary>
    private sealed class MentionNames
    {
        private readonly HashSet<string> _words;
        private readonly List<string> _substrings;

        private MentionNames(
            IReadOnlyDictionary<string, IReadOnlyList<string>> byCandidate, HashSet<string> keys, HashSet<string> words, List<string> substrings)
        {
            ByCandidate = byCandidate;
            Keys = keys;
            _words = words;
            _substrings = substrings;
        }

        /// <summary>Kandidat → Mod-Namen (<see cref="StringComparer.OrdinalIgnoreCase"/>), wie <see cref="GfxFileReader.ReadMentions"/> es braucht.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> ByCandidate { get; }

        /// <summary>Die Namen samt Art (<c>img:gfx/a.dds</c>, <c>spr:GFX_a</c>) für den Vergleich mit dem Speicher (als Menge, ordinal).</summary>
        public HashSet<string> Keys { get; }

        public bool IsEmpty => Keys.Count == 0;

        public static MentionNames Of(IEnumerable<string> modImages, IEnumerable<string> modSprites, GfxAuditRules rules)
        {
            var byCandidate = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var substrings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string candidate, string name)
            {
                if (!byCandidate.TryGetValue(candidate, out var list))
                {
                    byCandidate[candidate] = list = new List<string>();
                }

                if (!list.Contains(name, StringComparer.Ordinal))
                {
                    list.Add(name);
                }
            }

            // Vorfilter: Wortkandidaten über MentionWords.MayMention, alle anderen als Teilstring ohne Rücksicht auf Groß-/Kleinschreibung.
            void Filter(string candidate)
            {
                if (candidate.All(IsWordChar))
                {
                    words.Add(candidate);
                }
                else
                {
                    substrings.Add(candidate);
                }
            }

            foreach (var relative in modImages)
            {
                var logical = rules.LogicalPath(relative);
                if (!keys.Add(ImageKind + logical))
                {
                    continue;
                }

                var stem = GfxNames.Stem(relative);
                foreach (var candidate in GfxNames.Candidates(stem, rules))
                {
                    Add(candidate, logical);
                    Filter(candidate);
                }

                // Die Pfadkandidaten selbst braucht der Vorfilter nicht: Wer den Pfad nennt (gleich mit welchen Trennzeichen), nennt den Dateinamen.
                // Ein Wortlauf trifft den Dateinamen ohne Endung aber nur, wenn er keinen Punkt hat (MentionWords zerlegt "a.b.dds" in a, b, dds).
                Add(logical, logical);
                Add(GfxNames.WithoutExtension(logical), logical);
                if (stem.Contains('.'))
                {
                    substrings.Add(stem);
                }
            }

            foreach (var sprite in modSprites)
            {
                if (!keys.Add(SpriteKind + sprite))
                {
                    continue;
                }

                foreach (var candidate in GfxNames.Candidates(sprite, rules))
                {
                    Add(candidate, sprite);
                    Filter(candidate);
                }
            }

            var dictionary = byCandidate.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.OrdinalIgnoreCase);
            return new MentionNames(dictionary, keys, words, substrings.ToList());
        }

        /// <summary>
        /// Schneller Vorfilter über den Rohtext: Falsch-Positive erlaubt, Falsch-Negative nicht. Wortkandidaten über <see cref="MentionWords.MayMention"/>
        /// (Menge ohne Groß-/Kleinschreibung), Kandidaten mit anderen Zeichen und Dateinamen ohne Endung mit Punkt als Teilstring.
        /// </summary>
        /// <remarks>
        /// Bewusst abweichend vom Plan (vom Controller abgenommen): Die Pfadkandidaten (logischer Pfad mit und ohne Endung) stehen nicht im Vorfilter.
        /// Jeder Pfad im Text enthält den Dateinamen, und dessen Stamm ist selbst ein Kandidat; ein Teilstring-Test des ganzen Pfads würde dagegen
        /// Schreibweisen mit <c>\</c> verfehlen und kostete je Mod-Bild einen Durchlauf über jeden Text.
        /// </remarks>
        public bool MayMention(string text)
        {
            if (IsEmpty || string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var s in _substrings)
            {
                if (text.Contains(s, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return MentionWords.MayMention(text, _words);
        }

        /// <summary>Wie <see cref="MentionWords"/>: Buchstaben, Ziffern, <c>_</c> und <c>.</c> bilden Wörter.</summary>
        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.';
    }

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
