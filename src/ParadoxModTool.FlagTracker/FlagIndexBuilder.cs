using System.Text;

namespace ParadoxModTool.FlagTracker;

/// <summary>Baut den <see cref="FlagIndex"/> eines Bereichs: Dateien lesen, Aufrufe der Scripted Effects/Trigger sammeln, <c>$PARAM$</c> auflösen.</summary>
public sealed class FlagIndexBuilder
{
    /// <summary>Alle so viele gelesenen Dateien wird Fortschritt gemeldet.</summary>
    private const int ProgressStep = 50;

    /// <summary>Bis zu so vielen neu gelesenen Dateien wird ihr Text behalten (spart das zweite Lesen für die Aufrufe); darüber nur der von Definitionsdateien.</summary>
    private const int KeepAllTextsLimit = 256;

    /// <summary>
    /// Version der Leser (<see cref="ScriptReader"/>, <see cref="DataFunctionReader"/>). Erhöhen, sobald sich ihr Ergebnis für denselben Text ändert:
    /// Gespeicherte Leseergebnisse einer anderen Version werden dann nicht wiederverwendet.
    /// </summary>
    public const int ReaderVersion = 4;

    private readonly FlagSourceSet _sources;
    private readonly FlagCommandTable _table;
    private readonly ScriptReader _reader;

    /// <summary>Messwert für den Messlauf: so viele Kandidaten (<see cref="RawSite.IsCandidate"/>) hat der letzte Aufbau verworfen.</summary>
    internal int LastDroppedCandidates { get; private set; }

    public FlagIndexBuilder(FlagSourceSet sources, FlagCommandTable table)
    {
        _sources = sources;
        _table = table;
        _reader = new ScriptReader(table);
    }

    /// <summary>
    /// Liest jede Datei von <see cref="FlagSourceSet.EnumerateFiles"/> parallel, sammelt dann die Aufrufe (siehe <see cref="CalleeClosure"/>)
    /// in jeder <c>.txt</c>, die einen der Namen enthält, und löst auf. Fehler einer Datei landen in UnreadFiles (voller Pfad).
    /// Schreibt nichts auf die Platte.
    /// </summary>
    /// <remarks>
    /// Fortschritt je <see cref="BuildPhase"/> der Reihe nach: (Phase, 0, Total), dann alle 50 Dateien, dann (Phase, Total, Total);
    /// bei Total 0 nur die eine Meldung (Phase, 0, 0). Reading zählt die gelesenen Dateien, Calls die Skriptdateien, deren Aufrufe
    /// gesammelt werden, Resolving hat Total 1. Die letzte Meldung ist immer (Resolving, 1, 1). Vor der ersten Meldung werden die
    /// Dateien aufgezählt und ihr Stand geprüft. Gemeldet wird aus Arbeits-Threads, nacheinander und ohne die Zählsperre zu halten;
    /// Aufrufer sollten <see cref="Progress{T}"/> übergeben (meldet auf dem Thread der Oberfläche und hält den Aufbau nicht auf).
    /// </remarks>
    public FlagIndex Build(CancellationToken ct = default, IProgress<BuildProgress>? progress = null) => Run(null, ct, progress);

    /// <summary>
    /// Wie <see cref="Build"/>, nutzt aber die gespeicherten Leseergebnisse des Bereichs (<see cref="FlagSourceSet.ScopeKey"/>) und liest nur Dateien neu,
    /// die neu sind oder deren Länge oder Änderungszeit sich geändert hat. Verschwundene Dateien entfallen. Die Aufrufe werden für alle Dateien neu
    /// gesammelt, wenn sich die Namen aus <see cref="CalleeClosure"/> gegenüber dem Gespeicherten geändert haben, sonst nur für neu gelesene.
    /// Fortschritt wie bei <see cref="Build"/>; Reading zählt nur die neu gelesenen Dateien.
    /// Danach wird gespeichert. Fehlt der Speicher, ist er unbrauchbar oder stammt er von einer anderen Befehlstabelle oder <see cref="ReaderVersion"/>, ist es ein voller Aufbau.
    /// </summary>
    public FlagIndex Refresh(FlagIndexStore store, CancellationToken ct = default, IProgress<BuildProgress>? progress = null) =>
        Run(store, ct, progress);

    private FlagIndex Run(FlagIndexStore? store, CancellationToken ct, IProgress<BuildProgress>? progress)
    {
        ct.ThrowIfCancellationRequested();
        var reporter = new Reporter(progress);

        var entries = _sources.EnumerateFiles();
        var stored = store?.Load(_sources.ScopeKey);
        if (stored is not null && (stored.ReaderVersion != ReaderVersion || stored.TableFingerprint != _table.Fingerprint))
        {
            // Mit anderer Befehlstabelle oder anderem Leser gelesen: wie kein Speicher behandeln.
            stored = null;
        }

        var storedByKey = new Dictionary<string, (FileStamp Stamp, FileScan Scan)>(StringComparer.OrdinalIgnoreCase);
        if (stored is not null)
        {
            foreach (var entry in stored.Files)
            {
                storedByKey.TryAdd(KeyOf(entry.Stamp.RootDirectory, entry.Stamp.RelativePath), entry);
            }
        }

        var unread = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = entries.Count;
        var slots = new FlagScannedFile?[count];
        var stamps = new FileStamp?[count];
        var texts = new string?[count];
        var isFresh = new bool[count];
        var toScan = new List<int>();
        var reused = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var needsScan = new bool[count];
        var options = new ParallelOptions { CancellationToken = ct };

        Parallel.For(0, count, options, i =>
        {
            var (root, relative) = entries[i];
            FileStamp stamp;
            try
            {
                var info = new FileInfo(FullPath(root, relative));
                stamp = new FileStamp(root.Directory, relative, info.Length, info.LastWriteTimeUtc.Ticks);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (unread)
                {
                    unread.Add(FullPath(root, relative));
                }

                return;
            }

            stamps[i] = stamp;
            if (storedByKey.TryGetValue(KeyOf(root.Directory, relative), out var old)
                && old.Stamp.Length == stamp.Length && old.Stamp.LastWriteUtcTicks == stamp.LastWriteUtcTicks)
            {
                slots[i] = new FlagScannedFile(new SiteLocation(root.Directory, relative, 0), root.Origin, root.Name, old.Scan);
            }
            else
            {
                needsScan[i] = true;
            }
        });

        for (var i = 0; i < count; i++)
        {
            if (needsScan[i])
            {
                toScan.Add(i);
            }
            else if (slots[i] is not null)
            {
                reused.Add(KeyOf(entries[i].Root.Directory, entries[i].RelativePath));
            }
        }

        var total = toScan.Count;
        var keepAll = total <= KeepAllTextsLimit;
        var done = 0;
        reporter.Report(new BuildProgress(BuildPhase.Reading, 0, total));
        Parallel.For(0, total, options, n =>
        {
            var i = toScan[n];
            var (root, relative) = entries[i];
            try
            {
                var (file, text) = ScanFile(root, relative);
                slots[i] = file;
                isFresh[i] = true;
                // Bei vielen Dateien behalten nur Definitionsdateien ihren Text (für die Hülle der Aufgerufenen); der Rest wird später neu gelesen.
                if (keepAll || file.Scan.Definitions.Count > 0)
                {
                    texts[i] = text;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (unread)
                {
                    unread.Add(FullPath(root, relative));
                }
            }

            var now = Interlocked.Increment(ref done);
            if (now % ProgressStep == 0 && now < total)
            {
                reporter.Report(new BuildProgress(BuildPhase.Reading, now, total));
            }
        });
        reporter.Report(new BuildProgress(BuildPhase.Reading, total, total));

        var files = new List<FlagScannedFile>(count);
        var fileStamps = new List<FileStamp>(count);
        var fresh = new HashSet<FlagScannedFile>(ReferenceEqualityComparer.Instance);
        var knownTexts = new Dictionary<FlagScannedFile, string>(ReferenceEqualityComparer.Instance);
        var definitionsChanged = false;
        for (var i = 0; i < count; i++)
        {
            if (slots[i] is not { } file)
            {
                continue;
            }

            files.Add(file);
            fileStamps.Add(stamps[i]!);
            if (texts[i] is { } text)
            {
                knownTexts[file] = text;
            }

            if (isFresh[i])
            {
                fresh.Add(file);
                definitionsChanged |= file.Scan.Definitions.Count > 0;
            }
        }

        // Geänderte oder verschwundene Definitionsdateien ändern evtl. die Namen der Aufgerufenen.
        definitionsChanged |= storedByKey.Any(e => !reused.Contains(e.Key) && e.Value.Scan.Definitions.Count > 0);

        ct.ThrowIfCancellationRequested();
        var storedCallees = stored?.Callees;
        IReadOnlySet<string> callees;
        bool allCalls;
        if (storedCallees is null || definitionsChanged)
        {
            callees = CalleeClosure(files, f =>
            {
                if (knownTexts.TryGetValue(f, out var known))
                {
                    return known;
                }

                try
                {
                    var read = ReadText(f.File.FullPath);
                    knownTexts[f] = read;
                    return read;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    unread.Add(f.File.FullPath);
                    return string.Empty;
                }
            });
            allCalls = storedCallees is null || !callees.SetEquals(storedCallees);
        }
        else
        {
            callees = new HashSet<string>(storedCallees, CalleeNameComparer.Instance);
            allCalls = false;
        }

        var callFiles = files.Where(f => IsScript(f.File.RelativePath) && (allCalls || fresh.Contains(f))).ToList();
        var callsDone = 0;
        reporter.Report(new BuildProgress(BuildPhase.Calls, 0, callFiles.Count));
        Parallel.ForEach(callFiles, options, file =>
        {
            try
            {
                var text = callees.Count == 0 ? string.Empty : knownTexts.TryGetValue(file, out var known) ? known : ReadText(file.File.FullPath);
                SetCalls(file, text, callees);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (unread)
                {
                    unread.Add(file.File.FullPath);
                }
            }

            var now = Interlocked.Increment(ref callsDone);
            if (now % ProgressStep == 0 && now < callFiles.Count)
            {
                reporter.Report(new BuildProgress(BuildPhase.Calls, now, callFiles.Count));
            }
        });
        reporter.Report(new BuildProgress(BuildPhase.Calls, callFiles.Count, callFiles.Count));

        ct.ThrowIfCancellationRequested();
        reporter.Report(new BuildProgress(BuildPhase.Resolving, 0, 1));
        var index = BuildFrom(files, unread.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList());
        if (store is not null)
        {
            // Dateien mit Lesefehler beim Sammeln der Aufrufe nicht speichern: Sie sollen beim nächsten Mal wieder gelesen werden.
            var saved = new List<(FileStamp, FileScan)>(files.Count);
            for (var i = 0; i < files.Count; i++)
            {
                if (!unread.Contains(files[i].File.FullPath))
                {
                    saved.Add((fileStamps[i], files[i].Scan));
                }
            }

            try
            {
                store.Save(_sources.ScopeKey, new StoredScans(
                    FlagIndexStore.FormatVersion, index.BuiltUtc, saved, callees.OrderBy(c => c, StringComparer.Ordinal).ToList(), ReaderVersion, _table.Fingerprint));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
            {
                // Der Index ist fertig; ein Speicherfehler kostet nur den Zeitgewinn beim nächsten Mal.
            }
        }

        reporter.Report(new BuildProgress(BuildPhase.Resolving, 1, 1));
        return index;
    }

    /// <summary>
    /// Meldet Fortschritt nacheinander (eigene Sperre, nicht die der Zählung) und verwirft veraltete Meldungen: eine frühere Phase
    /// oder ein Done, das in derselben Phase nicht größer ist als das zuletzt gemeldete. So bleibt Done je Phase steigend und
    /// (Phase, 0, 0) kommt nur einmal.
    /// </summary>
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

    private static string KeyOf(string rootDirectory, string relativePath) => rootDirectory + "|" + relativePath;

    /// <summary>
    /// Liest eine Datei: <c>.txt</c> mit <see cref="ScriptReader"/>, <c>.gui</c> und <c>.yml</c> mit <see cref="DataFunctionReader"/>.
    /// Herkunft und Name aus der Wurzel; die Aufrufe bleiben leer (siehe <see cref="SetCalls"/>). Wirft bei Lesefehlern.
    /// </summary>
    internal (FlagScannedFile File, string Text) ScanFile(FlagRoot root, string relativePath)
    {
        var location = new SiteLocation(root.Directory, relativePath, 0);
        var text = ReadText(location.FullPath);
        var scan = IsScript(relativePath) ? _reader.Read(relativePath, text) : DataFunctionReader.Read(text, relativePath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase), _table.LocalisationStyle);
        return (new FlagScannedFile(location, root.Origin, root.Name, scan), text);
    }

    /// <summary>
    /// Namen, deren Aufrufe gesammelt werden: S0 = Definitionen mit Vorlagen (<c>$PARAM$</c>); dann wiederholt über die Definitionsdateien
    /// jede umgebende Definition eines Aufrufs aus S, der einen <c>$X$</c>-Wert weiterreicht, bis sich nichts mehr ändert.
    /// So landen auch äußere Durchreich-Aufrufe in Dateien, die den inneren Effekt nie nennen. <paramref name="textOf"/> liefert den Text
    /// einer Datei mit Definitionen.
    /// </summary>
    internal IReadOnlySet<string> CalleeClosure(IReadOnlyList<FlagScannedFile> files, Func<FlagScannedFile, string> textOf)
    {
        var names = new HashSet<string>(
            files.SelectMany(f => f.Scan.Templates).Select(t => t.DefinitionName).OfType<string>(), CalleeNameComparer.Instance);
        var definitionFiles = files.Where(f => f.Scan.Definitions.Count > 0).ToList();
        while (names.Count > 0)
        {
            var added = new HashSet<string>(CalleeNameComparer.Instance);
            foreach (var file in definitionFiles)
            {
                var text = textOf(file);
                if (!MayCall(text, names))
                {
                    continue;
                }

                foreach (var call in _reader.ReadCalls(file.File.RelativePath, text, names))
                {
                    // Dynamische Inline-Skript-Aufrufe ($ im Pfad) werden nicht durchgereicht, sie machen nur Muster.
                    if (call.CallerDefinition is { } caller && !names.Contains(caller) && names.Contains(call.Callee)
                        && call.Params is not null && call.Params.Values.Any(v => v is not null && v.Contains('$')))
                    {
                        added.Add(caller);
                    }
                }
            }

            if (added.Count == 0)
            {
                break;
            }

            names.UnionWith(added);
        }

        return names;
    }

    /// <summary>Ersetzt die Aufrufe einer <c>.txt</c>-Datei durch die Aufrufe der Namen aus <paramref name="callees"/> (leer, wenn der Text keinen nennt).</summary>
    internal void SetCalls(FlagScannedFile file, string text, IReadOnlySet<string> callees)
    {
        file.Scan.Calls.Clear();
        if (callees.Count > 0 && MayCall(text, callees))
        {
            file.Scan.Calls.AddRange(_reader.ReadCalls(file.File.RelativePath, text, callees));
        }
    }

    /// <summary>
    /// Index aus gelesenen Dateien (Aufrufe schon gesetzt): direkte Fundstellen mit Ort und Herkunft plus aufgelöste <c>$PARAM$</c>-Stellen
    /// mit den wirksamen Definitionen des Bereichs. Sortiert nach Wurzel, Pfad, Zeile. Kandidaten (<see cref="RawSite.IsCandidate"/>) zählen nur,
    /// wenn eine konkrete Nicht-Kandidaten-Stelle (direkt oder aufgelöst, kein Muster) denselben Namen (Vergleich wie im Index) mit derselben Art setzt.
    /// Ausnahme: Ein Event-Target-Kandidat, dessen Name nur als globales Event-Target gesetzt wird, zählt als gelesenes globales Event-Target.
    /// </summary>
    internal FlagIndex BuildFrom(IReadOnlyList<FlagScannedFile> files, IReadOnlyList<string> unreadFiles)
    {
        var resolved = ParamResolver.Resolve(files, _sources.EffectiveDefinitions(files));
        if (_table.ScopedVariableNames)
        {
            resolved = NormalizeScopedVariables(resolved);
        }

        // Gesetzte Namen je Art; der Namensvergleich folgt der Tabelle.
        var setKeys = new HashSet<(FlagKind Kind, string Name)>(new KindNameComparer(_table.NamesIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal));
        foreach (var raw in files.SelectMany(f => f.Scan.Sites).Where(r => !r.IsCandidate && !r.IsPattern && r.Role == FlagRole.Set))
        {
            setKeys.Add((raw.Kind, raw.Name));
        }

        foreach (var site in resolved.Where(s => !s.IsPattern && s.Role == FlagRole.Set))
        {
            setKeys.Add((site.Kind, site.Name));
        }

        var sites = new List<FlagSite>();
        var dropped = 0;
        foreach (var file in files)
        {
            foreach (var raw in file.Scan.Sites)
            {
                var kind = raw.Kind;
                if (raw.IsCandidate && !setKeys.Contains((kind, raw.Name)))
                {
                    // [X.GetName] kann lokal und global nicht unterscheiden: nur global gesetzt, dann ein globales Event-Target.
                    if (kind != FlagKind.EventTarget || !setKeys.Contains((FlagKind.GlobalEventTarget, raw.Name)))
                    {
                        dropped++;
                        continue;
                    }

                    kind = FlagKind.GlobalEventTarget;
                }

                sites.Add(new FlagSite(
                    raw.Name, kind, raw.Family, raw.Role, file.File with { Line = raw.Line }, file.Origin, file.OriginName, IsPattern: raw.IsPattern));
            }
        }

        LastDroppedCandidates = dropped;
        sites.AddRange(resolved);
        sites.Sort(CompareSites);
        return new FlagIndex(sites, unreadFiles, DateTime.UtcNow, files.Count, _table.NamesIgnoreCase);
    }

    /// <summary>
    /// <see cref="FlagCommandTable.ScopedVariableNames"/> auch nach dem Einsetzen: Eine aufgelöste Variable wie <c>prev.x</c> heißt <c>x</c>; ist sie
    /// danach kein Name (z. B. <c>value = $VALUE_IF_CREATED$</c> mit Wert <c>0</c>), entfällt sie.
    /// </summary>
    private static IReadOnlyList<FlagSite> NormalizeScopedVariables(IReadOnlyList<FlagSite> sites)
    {
        var result = new List<FlagSite>(sites.Count);
        foreach (var site in sites)
        {
            if (site.Kind != FlagKind.Variable)
            {
                result.Add(site);
            }
            else if (ScriptReader.ScopedVariableName(site.Name) is { } name && name.IndexOf(':') < 0)
            {
                result.Add(name == site.Name ? site : site with { Name = name });
            }
        }

        return result;
    }

    /// <summary>Vergleicht (Art, Name) mit genauer Art und dem Namensvergleich der Tabelle.</summary>
    private sealed class KindNameComparer(StringComparer names) : IEqualityComparer<(FlagKind Kind, string Name)>
    {
        public bool Equals((FlagKind Kind, string Name) x, (FlagKind Kind, string Name) y) => x.Kind == y.Kind && names.Equals(x.Name, y.Name);

        public int GetHashCode((FlagKind Kind, string Name) obj) => HashCode.Combine(obj.Kind, names.GetHashCode(obj.Name));
    }

    private static int CompareSites(FlagSite a, FlagSite b)
    {
        var cmp = string.Compare(a.Location.RootDirectory, b.Location.RootDirectory, StringComparison.OrdinalIgnoreCase);
        if (cmp == 0)
        {
            cmp = string.Compare(a.Location.RelativePath, b.Location.RelativePath, StringComparison.OrdinalIgnoreCase);
        }

        if (cmp == 0)
        {
            cmp = a.Location.Line.CompareTo(b.Location.Line);
        }

        if (cmp == 0)
        {
            cmp = string.CompareOrdinal(a.Name, b.Name);
        }

        if (cmp == 0)
        {
            cmp = a.Role.CompareTo(b.Role);
        }

        return cmp;
    }

    /// <summary>
    /// Vorfilter für Aufrufe: <see cref="MentionsAny"/>, bei <see cref="FlagCommandTable.InlineScripts"/> auch jeder Text mit <c>inline_script</c>
    /// (die Aufgerufenen heißen dort <c>inline_script:pfad</c>, im Text steht nur der Schlüssel).
    /// </summary>
    private bool MayCall(string text, IReadOnlySet<string> names) =>
        MentionsAny(text, names) || (_table.InlineScripts && text.Contains("inline_script", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Schneller Vorfilter: true, wenn ein Wort des Textes (Folge ohne Leerraum und ohne <c>={}#"&lt;&gt;!?</c>) in <paramref name="names"/> steht.
    /// Aufrufe sind Schlüssel, also solche Wörter; ein Treffer heißt nur „vielleicht“.
    /// </summary>
    private static bool MentionsAny(string text, IReadOnlySet<string> names)
    {
        // Nur Wörter mit einer Länge, die ein Name hat, werden als Zeichenkette nachgeschlagen.
        var lengths = new HashSet<int>(names.Select(n => n.Length));
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var separator = i == text.Length || IsSeparator(text[i]);
            if (!separator)
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0 && lengths.Contains(i - start) && names.Contains(text.Substring(start, i - start)))
            {
                return true;
            }

            start = -1;
        }

        return false;
    }

    private static bool IsSeparator(char c) =>
        char.IsWhiteSpace(c) || c is '=' or '{' or '}' or '#' or '"' or '<' or '>' or '!' or '?';

    private static bool IsScript(string relativePath) => relativePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    /// <summary>Strenges UTF-8: wirft bei fehlerhaften Bytes, damit <see cref="ReadText"/> auf Latin-1 ausweichen kann.</summary>
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>Liest den Text als UTF-8 (Byte-Order-Mark wird erkannt); ist er kein gültiges UTF-8, als Latin-1 (EU IV hat viele solche Dateien).</summary>
    public static string ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path, StrictUtf8);
        }
        catch (DecoderFallbackException)
        {
            return File.ReadAllText(path, Encoding.Latin1);
        }
    }

    private static string FullPath(FlagRoot root, string relativePath) => new SiteLocation(root.Directory, relativePath, 0).FullPath;
}
