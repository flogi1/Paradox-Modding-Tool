using System.Diagnostics;
using System.Runtime.CompilerServices;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Desync;

/// <summary>Abschnitt eines Vergleichslaufs, für die Fortschrittsanzeige.</summary>
public enum DesyncPhase
{
    /// <summary>Erster Durchgang über den Referenz-Spielstand.</summary>
    IndexReference,

    /// <summary>Erster Durchgang über den abweichenden Spielstand.</summary>
    IndexDesynced,

    /// <summary>Zweiter Durchgang: Text der abweichenden Einheiten aus beiden Spielständen holen.</summary>
    Collect,

    /// <summary>Feldvergleich der geholten Einheiten.</summary>
    Diff,
}

/// <param name="Fraction">Anteil des Abschnitts zwischen 0 und 1 (bei den Durchgängen nach gelesenen Bytes).</param>
public sealed record DesyncProgress(DesyncPhase Phase, double Fraction);

/// <summary>
/// Ungewichtetes Ergebnis eines Zustandsvergleichs (wird so zwischengespeichert; die Gewichtung läuft beim Laden).
/// <see cref="Units"/> ist leer, wenn einer der Spielstände nicht <see cref="SaveReadState.Complete"/> ist.
/// </summary>
/// <param name="FormatVersion">Stand des Formats, siehe <see cref="DesyncComparison.FormatVersion"/>.</param>
/// <param name="Names">Namenstabellen beider Spielstände, die Referenz gewinnt.</param>
/// <param name="Duration">Dauer des Laufs.</param>
/// <param name="ReferenceStoppedAt">Gelesene Bytes, wenn der Referenz-Spielstand <see cref="SaveReadState.Incomplete"/> ist, sonst null.</param>
/// <param name="DesyncedStoppedAt">Gelesene Bytes, wenn der abweichende Spielstand <see cref="SaveReadState.Incomplete"/> ist, sonst null.</param>
public sealed record DesyncDiffResult(
    int FormatVersion,
    string OosId16,
    int ReferenceMachine,
    int DesyncedMachine,
    SaveReadState ReferenceState,
    SaveReadState DesyncedState,
    IReadOnlyList<UnitDiffDetail> Units,
    SaveNameTables Names,
    TimeSpan Duration,
    long? ReferenceStoppedAt = null,
    long? DesyncedStoppedAt = null);

/// <summary>
/// Vergleicht die OOS-Spielstände zweier Dumps: der erste Durchgang (Index) liest erst die Referenz, dann die abweichende Seite
/// (zwei gleichzeitige Indizes sprengten den Speicherrahmen), der zweite Durchgang (Text der abweichenden Einheiten) liest beide Seiten
/// parallel; danach werden die Einheiten feldweise verglichen. Jede Seite liest nur aus ihrem eigenen <see cref="OosDump"/> und immer nur
/// einen Stream zugleich (Zip-Archive sind nicht threadsicher).
/// </summary>
public static class DesyncComparison
{
    /// <summary>
    /// Stand des Ergebnisformats; ein anderer Stand macht zwischengespeicherte Ergebnisse ungültig. 2: Beschriftungen nur für Objekte,
    /// Behälter mit doppeltem Slot werden über die volle Id zugeordnet.
    /// </summary>
    public const int FormatVersion = 2;

    /// <summary>Fortschritt im zweiten Durchgang höchstens je so vielen gelesenen Bytes einer Seite.</summary>
    private const long CollectReportBytes = 1 << 20;

    /// <summary>
    /// Vergleicht <paramref name="reference"/> (Referenzmaschine) mit <paramref name="desynced"/> (abgewichene Maschine).
    /// Zwischen den Durchgängen löst der Lauf einmal <see cref="GC.Collect()"/> aus, damit der Speicher der beiden Indizes frei ist,
    /// bevor der zweite Durchgang Texte sammelt.
    /// </summary>
    /// <exception cref="OperationCanceledException">Abgebrochen; der Lauf prüft je MiB und je Einheit.</exception>
    /// <exception cref="OosDumpException">Ein Spielstand fehlt oder ist nicht lesbar.</exception>
    public static DesyncDiffResult Run(OosDump reference, OosDump desynced, DesyncProfile profile, IProgress<DesyncProgress>? progress,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var watch = Stopwatch.StartNew();
        var referenceSize = reference.StampOf(DumpFileKind.OosSave).Size;
        var desyncedSize = desynced.StampOf(DumpFileKind.OosSave).Size;

        var first = IndexAndCompare(reference, desynced, profile, referenceSize, desyncedSize, progress, cancellation);
        DesyncDiffResult Result(IReadOnlyList<UnitDiffDetail> units) => new(
            FormatVersion, reference.Metadata.OosId16, reference.MachineId, desynced.MachineId,
            first.ReferenceState, first.DesyncedState, units, first.Names, watch.Elapsed,
            first.ReferenceState == SaveReadState.Incomplete ? first.ReferenceLength : null,
            first.DesyncedState == SaveReadState.Incomplete ? first.DesyncedLength : null);

        // Ein abgeschnittener oder binärer Spielstand ergibt keinen halben Vergleich, nur seinen Zustand.
        if (first.Plan is not { } plan)
        {
            return Result(Array.Empty<UnitDiffDetail>());
        }

        // Die Indizes beider Seiten (mit nodeDepth 5 zusammen gut 210 MB) sind jetzt unerreichbar. Ohne Sammlung an dieser Stelle läuft
        // der zweite Durchgang auf diesem Müll weiter (gemessen mit nodeDepth 5: Spitze 370-405 MB statt 364-378 MB).
        GC.Collect();
        cancellation.ThrowIfCancellationRequested();

        // Zweiter Durchgang auf frisch geöffneten Streams, wieder beide Seiten zugleich.
        var collected = new SharedBytesProgress(progress, referenceSize + desyncedSize);
        var (referenceTexts, desyncedTexts) = BothSides(
            token => Collect(reference, plan.ReferenceRequests, collected, token),
            token => Collect(desynced, plan.DesyncedRequests, collected, token),
            cancellation);
        progress?.Report(new DesyncProgress(DesyncPhase.Collect, 1.0));

        var diffs = first.Diffs;
        var units = new List<UnitDiffDetail>(diffs.Count);
        for (var i = 0; i < diffs.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var unit = Detail(diffs[i], Take(referenceTexts, plan.ReferenceSlot[i]), Take(desyncedTexts, plan.DesyncedSlot[i]),
                plan.OwnTooLarge[i], profile);
            if (unit is not null)
            {
                units.Add(unit);
            }

            if ((i & 255) == 255)
            {
                progress?.Report(new DesyncProgress(DesyncPhase.Diff, (double)(i + 1) / diffs.Count));
            }
        }

        progress?.Report(new DesyncProgress(DesyncPhase.Diff, 1.0));
        return Result(units);
    }

    /// <summary>Ergebnis des ersten Durchgangs ohne die Indizes; <see cref="Plan"/> ist null, wenn ein Spielstand nicht vollständig ist.</summary>
    private sealed record FirstPass(SaveReadState ReferenceState, SaveReadState DesyncedState, long ReferenceLength, long DesyncedLength,
        SaveNameTables Names, IReadOnlyList<UnitDiff> Diffs, CollectPlan? Plan);

    /// <summary>
    /// Erster Durchgang erst über die Referenz, dann über die abweichende Seite; danach Paarung und Anfragen des zweiten Durchgangs.
    /// Nacheinander, weil der Müll zweier gleichzeitig laufender Indexläufe den Arbeitsspeicher über 400 MB trieb. Eine eigene Methode,
    /// damit die Indizes (der größte Speicherposten) mit ihrem Ende unerreichbar werden, bevor der zweite Durchgang Texte sammelt.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static FirstPass IndexAndCompare(OosDump reference, OosDump desynced, DesyncProfile profile, long referenceSize, long desyncedSize,
        IProgress<DesyncProgress>? progress, CancellationToken cancellation)
    {
        var referenceIndex = Index(reference, profile, new BytesProgress(progress, DesyncPhase.IndexReference, referenceSize), cancellation);
        var desyncedIndex = Index(desynced, profile, new BytesProgress(progress, DesyncPhase.IndexDesynced, desyncedSize), cancellation);

        var names = SaveNameTables.Merge(referenceIndex.NameTables, desyncedIndex.NameTables);
        if (referenceIndex.State != SaveReadState.Complete || desyncedIndex.State != SaveReadState.Complete)
        {
            return new FirstPass(referenceIndex.State, desyncedIndex.State, referenceIndex.Length, desyncedIndex.Length, names,
                Array.Empty<UnitDiff>(), null);
        }

        var diffs = SaveIndexComparer.Compare(referenceIndex, desyncedIndex, profile);
        var plan = CollectPlan.Build(diffs, referenceIndex, desyncedIndex, profile.SizeCapBytes);
        return new FirstPass(referenceIndex.State, desyncedIndex.State, referenceIndex.Length, desyncedIndex.Length, names, diffs, plan);
    }

    /// <summary>Gibt den Text am Platz <paramref name="slot"/> zurück und löst ihn aus dem Feld, damit er nach dem Vergleich frei wird.</summary>
    private static string? Take(string[] texts, int slot)
    {
        if (slot < 0)
        {
            return null;
        }

        var text = texts[slot];
        texts[slot] = null!;
        return text;
    }

    private static SaveIndex Index(OosDump dump, DesyncProfile profile, IProgress<long> bytesRead, CancellationToken cancellation)
    {
        using var stream = dump.OpenRead(DumpFileKind.OosSave);
        return SaveIndexer.Read(stream, profile, bytesRead, cancellation);
    }

    private static string[] Collect(OosDump dump, IReadOnlyList<CollectRequest> requests, SharedBytesProgress progress,
        CancellationToken cancellation)
    {
        if (requests.Count == 0)
        {
            return Array.Empty<string>();
        }

        using var stream = new CountingStream(dump.OpenRead(DumpFileKind.OosSave), progress);
        return SaveTextCollector.Collect(stream, requests, cancellation).ToArray();
    }

    /// <summary>
    /// Führt beide Seiten parallel aus (zweiter Durchgang). Scheitert eine Seite, wird die andere abgebrochen und der Fehler der gescheiterten weitergegeben;
    /// ein Abbruch durch den Aufrufer endet immer als <see cref="OperationCanceledException"/>. Kehrt erst zurück, wenn beide Seiten
    /// fertig sind, damit kein Stream eines Dumps über den Lauf hinaus liest.
    /// </summary>
    private static (T Reference, T Desynced) BothSides<T>(Func<CancellationToken, T> reference, Func<CancellationToken, T> desynced,
        CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var token = linked.Token;
        Task<T> Start(Func<CancellationToken, T> side) => Task.Run(() =>
        {
            try
            {
                return side(token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                linked.Cancel();
                throw;
            }
        }, token);

        var referenceTask = Start(reference);
        var desyncedTask = Start(desynced);
        try
        {
            Task.WhenAll(referenceTask, desyncedTask).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            cancellation.ThrowIfCancellationRequested();
            throw;
        }

        return (referenceTask.Result, desyncedTask.Result);
    }

    /// <summary>Die Einheit mit Beschriftungen und Feldern; null für eine Eigenänderung, deren Feldvergleich nichts findet.</summary>
    private static UnitDiffDetail? Detail(UnitDiff diff, string? referenceText, string? desyncedText, bool ownTooLarge, DesyncProfile profile)
    {
        IReadOnlyList<FieldDiff> fields = Array.Empty<FieldDiff>();
        var truncated = false;
        var lineFallback = false;
        string? referenceLabel = null;
        string? desyncedLabel = null;

        if (diff.Change == UnitChange.OwnScalarsChanged)
        {
            if (ownTooLarge)
            {
                truncated = true;
            }
            else
            {
                var result = UnitFieldDiffer.Diff(referenceText ?? string.Empty, desyncedText ?? string.Empty);
                if (result.Fields.Count == 0 && !result.Truncated)
                {
                    return null;
                }

                (fields, truncated, lineFallback) = (result.Fields, result.Truncated, result.LineFallback);
            }
        }
        else
        {
            // Beschriftungen gibt es nur für Objekte; ein anderer Block (etwa ein ganzer Abschnitt) hat keinen Namen.
            referenceLabel = IsObject(diff.Reference) ? LabelOf(referenceText, profile.LabelFields) : null;
            desyncedLabel = IsObject(diff.Desynced) ? LabelOf(desyncedText, profile.LabelFields) : null;
            if (diff.Change == UnitChange.Changed)
            {
                var result = UnitFieldDiffer.Diff(referenceText ?? string.Empty, desyncedText ?? string.Empty);
                (fields, truncated, lineFallback) = (result.Fields, result.Truncated, result.LineFallback);
            }
        }

        return new UnitDiffDetail(
            diff.Path,
            diff.Change,
            referenceLabel,
            desyncedLabel,
            diff.Reference?.Generation,
            diff.Desynced?.Generation,
            SizeOf(diff.Reference),
            SizeOf(diff.Desynced),
            fields,
            truncated,
            lineFallback);
    }

    private static long SizeOf(SaveNode? node) => node is null ? 0 : node.End - node.Start;

    private static bool IsObject(SaveNode? node) => node?.Kind is NodeKind.Unit or NodeKind.ScalarUnit;

    /// <summary>
    /// Beschriftung einer Einheit: der Wert des ersten Felds aus <paramref name="labelFields"/> (in deren Reihenfolge), das als direkter
    /// Skalar in der Einheit steht. Null ohne Text, ohne Block (Skalar-Einheit) oder wenn sich der Text nicht parsen lässt. Nur für Objekte
    /// aufgerufen.
    /// </summary>
    private static string? LabelOf(string? text, IReadOnlyList<string> labelFields)
    {
        if (string.IsNullOrEmpty(text) || labelFields.Count == 0)
        {
            return null;
        }

        ScriptBlock root;
        try
        {
            root = ScriptDocument.Parse(text);
        }
        catch (ScriptParseException)
        {
            return null;
        }

        var entries = root.Entries.Where(entry => entry is not ScriptCommentEntry).ToList();
        if (entries.Count != 1 || entries[0] is not ScriptStatement { Value: ScriptBlock unit })
        {
            return null;
        }

        foreach (var field in labelFields)
        {
            foreach (var entry in unit.Entries)
            {
                if (entry is ScriptStatement { Value: ScriptScalar scalar } statement
                    && string.Equals(statement.Key, field, StringComparison.Ordinal))
                {
                    return scalar.Text;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Anfragen des zweiten Durchgangs je Seite und je Abweichung der Platz ihres Texts (-1: kein Text auf dieser Seite).
    /// </summary>
    private sealed class CollectPlan
    {
        public List<CollectRequest> ReferenceRequests { get; } = new();

        public List<CollectRequest> DesyncedRequests { get; } = new();

        public int[] ReferenceSlot { get; private init; } = Array.Empty<int>();

        public int[] DesyncedSlot { get; private init; } = Array.Empty<int>();

        /// <summary>Eigener Text einer Eigenänderung über der Größengrenze: er wird weder geholt noch verglichen.</summary>
        public bool[] OwnTooLarge { get; private init; } = Array.Empty<bool>();

        public static CollectPlan Build(IReadOnlyList<UnitDiff> diffs, SaveIndex reference, SaveIndex desynced, long sizeCap)
        {
            var plan = new CollectPlan
            {
                ReferenceSlot = new int[diffs.Count],
                DesyncedSlot = new int[diffs.Count],
                OwnTooLarge = new bool[diffs.Count],
            };
            Array.Fill(plan.ReferenceSlot, -1);
            Array.Fill(plan.DesyncedSlot, -1);

            for (var i = 0; i < diffs.Count; i++)
            {
                var diff = diffs[i];
                switch (diff.Change)
                {
                    case UnitChange.TooLarge:
                        break;
                    case UnitChange.OwnScalarsChanged:
                        var referenceOwn = OwnRequest(reference, diff.Reference!);
                        var desyncedOwn = OwnRequest(desynced, diff.Desynced!);
                        if (OwnSize(referenceOwn) > sizeCap || OwnSize(desyncedOwn) > sizeCap)
                        {
                            plan.OwnTooLarge[i] = true;
                            break;
                        }

                        plan.ReferenceSlot[i] = Add(plan.ReferenceRequests, referenceOwn);
                        plan.DesyncedSlot[i] = Add(plan.DesyncedRequests, desyncedOwn);
                        break;
                    default:
                        // Changed: der ganze Knoten für den Feldvergleich (ein Blatt-Block über der Grenze ist schon TooLarge). SlotReused und
                        // einseitige Einträge brauchen den Text nur für die Beschriftung, und die gibt es nur für Objekte: ein anderer Knoten
                        // (etwa ein ganzer fehlender Abschnitt, beliebig groß) wird nie geholt und bleibt ein Eintrag ohne Text.
                        var forFields = diff.Change == UnitChange.Changed;
                        if (diff.Reference is { } referenceNode && (forFields || IsObject(referenceNode)))
                        {
                            plan.ReferenceSlot[i] = Add(plan.ReferenceRequests, Whole(referenceNode));
                        }

                        if (diff.Desynced is { } desyncedNode && (forFields || IsObject(desyncedNode)))
                        {
                            plan.DesyncedSlot[i] = Add(plan.DesyncedRequests, Whole(desyncedNode));
                        }

                        break;
                }
            }

            return plan;
        }

        private static int Add(List<CollectRequest> requests, CollectRequest request)
        {
            requests.Add(request);
            return requests.Count - 1;
        }

        private static CollectRequest Whole(SaveNode node) => new(node.Start, node.End, Array.Empty<(long, long)>());

        /// <summary>Der Knotenbereich ohne die Bereiche seiner Kindknoten.</summary>
        private static CollectRequest OwnRequest(SaveIndex index, SaveNode node)
        {
            var children = index.ChildrenOf(node.Id);
            var exclude = new (long Start, long End)[children.Count];
            for (var i = 0; i < children.Count; i++)
            {
                var child = index.Nodes[children[i]];
                exclude[i] = (child.Start, child.End);
            }

            return new CollectRequest(node.Start, node.End, exclude);
        }

        /// <summary>Größe des eigenen Texts; Kindbereiche liegen im Knoten und überlappen sich nicht.</summary>
        private static long OwnSize(CollectRequest request) =>
            request.End - request.Start - request.Exclude.Sum(range => range.End - range.Start);
    }

    /// <summary>Meldet die gelesenen Bytes eines Durchgangs einer Seite als Anteil an deren Größe.</summary>
    private sealed class BytesProgress(IProgress<DesyncProgress>? progress, DesyncPhase phase, long total) : IProgress<long>
    {
        public void Report(long value) =>
            progress?.Report(new DesyncProgress(phase, total <= 0 ? 1.0 : Math.Min(1.0, (double)value / total)));
    }

    /// <summary>Gelesene Bytes beider Seiten im zweiten Durchgang zusammen; wird aus beiden Threads gemeldet.</summary>
    private sealed class SharedBytesProgress(IProgress<DesyncProgress>? progress, long total)
    {
        private long _read;

        public void Add(long bytes)
        {
            var read = Interlocked.Add(ref _read, bytes);
            progress?.Report(new DesyncProgress(DesyncPhase.Collect, total <= 0 ? 1.0 : Math.Min(1.0, (double)read / total)));
        }
    }

    /// <summary>Lese-Stream, der die gelesenen Bytes gebündelt (je <see cref="CollectReportBytes"/>) an den Fortschritt meldet.</summary>
    private sealed class CountingStream(Stream inner, SharedBytesProgress progress) : Stream
    {
        private long _pending;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Count(int read)
        {
            _pending += read;
            if (_pending >= CollectReportBytes || (read == 0 && _pending > 0))
            {
                progress.Add(_pending);
                _pending = 0;
            }

            return read;
        }
    }
}
