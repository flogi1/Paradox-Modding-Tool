using System.Buffers;
using System.Globalization;
using System.Text;

namespace ParadoxModTool.Desync;

/// <summary>
/// Erster Durchgang über einen Text-Spielstand: ein Byte-Zustandsautomat im Stil von <c>SaveVariableReader</c>, der nur Klammern,
/// Zeichenketten und Schlüssel verfolgt (die Einrückung ist unzuverlässig) und je Knoten Bereich, Slot und zwei FNV-Hashes speichert.
/// Ein Knoten hasht jedes Byte, das in ihm und nicht in einem Kindknoten liegt (der Eigen-Hash lässt Leerraum außerhalb von
/// Zeichenketten aus, der volle Hash nicht); schließt ein Kind, geht dessen voller Hash in den
/// vollen Hash des Elternknotens ein (Merkle-Baum). Die Bytes eines Schlüssels samt <c>=</c> gehören dem Kindknoten, den er öffnet.
/// </summary>
public static class SaveIndexer
{
    private const int BufferSize = 1 << 20;
    private const int BinaryProbeBytes = 4096;

    public static SaveIndex Read(Stream stream, DesyncProfile profile, IProgress<long>? bytesRead, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var parser = new Parser(profile);
        var buffer = new byte[BufferSize];
        long total = 0;
        var first = true;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();

            // Volle Puffer: die Grenzen liegen damit immer auf Vielfachen von 1 MiB.
            var count = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            if (count <= 0)
            {
                break;
            }

            var start = 0;
            if (first)
            {
                first = false;
                start = HeaderLength(buffer, count);
                var probe = buffer.AsSpan(start, Math.Min(count - start, BinaryProbeBytes));
                if (probe.IndexOfAnyInRange((byte)0, (byte)8) >= 0)
                {
                    total += count;
                    bytesRead?.Report(total);
                    return parser.Binary(total);
                }
            }

            parser.Feed(buffer, start, count, total);
            total += count;
            bytesRead?.Report(total);
        }

        return parser.Finish(total);
    }

    /// <summary>Länge einer Kopfzeile, die mit <c>SAV</c> beginnt (samt Zeilenumbruch); 0 ohne Kopfzeile.</summary>
    private static int HeaderLength(byte[] buffer, int count)
    {
        if (count < 3 || buffer[0] != (byte)'S' || buffer[1] != (byte)'A' || buffer[2] != (byte)'V')
        {
            return 0;
        }

        var newline = Array.IndexOf(buffer, (byte)'\n', 0, count);
        return newline < 0 ? count : newline + 1;
    }

    /// <summary>Was ein Pfad laut Profil ist; je normalisiertem Pfad einmal ausgewertet.</summary>
    private sealed record PathRules(string Path, bool IsContainer, int SlotBits, string? ListIdField);

    /// <summary>Stelle im Baum der erfassten Pfade (Kopfwerte, DLCs, Namenslisten).</summary>
    private sealed class CaptureNode
    {
        public Dictionary<string, CaptureNode> Children { get; } = new(StringComparer.Ordinal);

        /// <summary>Direkte Skalare kommen mit diesem Präfix nach <see cref="SaveIndex.Scalars"/>.</summary>
        public string? ScalarPrefix { get; set; }

        public bool Dlcs { get; set; }

        public List<Dictionary<string, string>> TagTables { get; } = new();

        public List<(Dictionary<string, string> Table, int Base)> KeyLists { get; } = new();

        public bool WantsValues => ScalarPrefix is not null || TagTables.Count > 0;
    }

    private sealed class CaptureFrame(CaptureNode node, int depth)
    {
        public CaptureNode Node { get; } = node;

        public int Depth { get; } = depth;

        public int Items { get; set; }
    }

    /// <summary>Offener Knoten; die Objekte werden wiederverwendet, die Hasher sind Felder, damit Append sie direkt ändert.</summary>
    private sealed class Frame
    {
        public Fnv64 Own;
        public Fnv64 Full;

        /// <summary>Eigen-Hash: Leerraum seit dem letzten gehashten Byte gesehen / letztes gehashtes Byte war ein Wortbyte.</summary>
        public bool OwnGap;
        public bool OwnWord;
        public int Id;
        public int Parent;
        public int Depth;
        public NodeKind Kind;
        public long Start;
        public string Name = string.Empty;
        public int? Slot;
        public int? Generation;
        public PathRules? Rules;
        public bool Anonymous;
        public int AnonymousIndex;
        public int AnonymousCount;
        public string? ItemIdField;
        public byte[]? ItemIdFieldBytes;
        public string? ItemId;
        public Dictionary<string, int>? Seen;
    }

    /// <summary>Wachsender Bytepuffer für Token-Text.</summary>
    private sealed class Bytes
    {
        private byte[] _data = new byte[256];

        public int Length { get; private set; }

        public ReadOnlySpan<byte> Span => _data.AsSpan(0, Length);

        public void Clear() => Length = 0;

        public void Append(ReadOnlySpan<byte> bytes)
        {
            if (Length + bytes.Length > _data.Length)
            {
                Array.Resize(ref _data, Math.Max(_data.Length * 2, Length + bytes.Length));
            }

            bytes.CopyTo(_data.AsSpan(Length));
            Length += bytes.Length;
        }

        public override string ToString() => Encoding.UTF8.GetString(Span);
    }

    private enum TokenRole
    {
        /// <summary>Token ohne Bedeutung (schnelles Lesen in Objekten und tiefen Blöcken).</summary>
        Ignored,

        /// <summary>Möglicher Schlüssel; ohne folgendes <c>=</c> ein Listeneintrag.</summary>
        Candidate,

        /// <summary>Wert nach <c>key=</c>.</summary>
        Value,
    }

    private sealed class Parser
    {
        private static readonly SearchValues<byte> Whitespace = SearchValues.Create(" \t\r\n"u8);
        private static readonly SearchValues<byte> WordEnd = SearchValues.Create(" \t\r\n={}\""u8);
        private static readonly SearchValues<byte> Structure = SearchValues.Create("{}\""u8);
        private static readonly SearchValues<byte> StringStops = SearchValues.Create("\"\\"u8);
        private static readonly SearchValues<byte> NonWord = SearchValues.Create("{}=\"<>!?"u8);
        private static readonly byte[] Separator = [(byte)' '];
        private static readonly SearchValues<byte> OutsideStringStops = SearchValues.Create(" \t\r\n\""u8);

        private readonly DesyncProfile _profile;
        private readonly int _nodeDepth;

        // Ergebnis
        private readonly List<SaveNode> _nodes = new();
        private readonly List<ulong> _ids = new();
        private readonly Dictionary<string, string> _scalars = new(StringComparer.Ordinal);
        private readonly List<string> _dlcs = new();
        private readonly Dictionary<string, Dictionary<string, string>> _tables = new(StringComparer.Ordinal);

        // Zwischenspeicher
        private readonly Dictionary<string, PathRules> _rules = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _interned = new(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _slotNames = new();

        // Offene Knoten und erfasste Pfade
        private readonly List<Frame> _frames = new();
        private readonly List<CaptureFrame> _captures = new();
        private int _open;

        // Puffer: Bytes ab _hashFrom sind noch keinem Knoten zugeschlagen; _carry hält zurückgehaltene Bytes früherer Puffer.
        private byte[] _buffer = Array.Empty<byte>();
        private long _base;
        private int _hashFrom;
        private int _bufferEnd;
        private readonly Bytes _carry = new();

        // Zeichenkettenzustand der Bytes, die in den Eigen-Hash laufen (unabhängig vom Tokenizer, aber nach denselben Regeln).
        private bool _ownInString;
        private bool _ownEscape;

        // Tokenizer
        private bool _inWord;
        private bool _inString;
        private bool _escape;
        private TokenRole _role;
        private bool _copy;
        private readonly Bytes _key = new();
        private readonly Bytes _value = new();

        // Grammatik
        private int _depth;
        private bool _hasCandidate;
        private bool _afterEquals;
        private bool _holding;
        private long _holdStart;
        private bool _slow;
        private bool _nodeCanOpen;

        public Parser(DesyncProfile profile)
        {
            _profile = profile;
            _nodeDepth = profile.NodeDepth;

            var captureRoot = new CaptureNode { ScalarPrefix = string.Empty };
            CaptureAt(captureRoot, "metadata").ScalarPrefix = "metadata/";
            CaptureAt(captureRoot, "metadata/enabled_dlcs").Dlcs = true;
            foreach (var (pattern, source) in profile.Names)
            {
                var table = new Dictionary<string, string>(StringComparer.Ordinal);
                _tables[pattern] = table;
                if (!string.IsNullOrEmpty(source.TagList))
                {
                    CaptureAt(captureRoot, source.TagList).TagTables.Add(table);
                }

                if (!string.IsNullOrEmpty(source.KeyList))
                {
                    CaptureAt(captureRoot, source.KeyList).KeyLists.Add((table, source.KeyListBase));
                }
            }

            _captures.Add(new CaptureFrame(captureRoot, 0));

            var root = Rent();
            root.Id = Add(0);
            root.Parent = -1;
            root.Kind = NodeKind.Root;
            root.Rules = RulesFor(string.Empty);
            UpdateMode();
        }

        private Frame Top => _frames[_open - 1];

        private CaptureFrame Capture => _captures[^1];

        public void Feed(byte[] buffer, int start, int end, long offset)
        {
            _buffer = buffer;
            _base = offset;
            _hashFrom = start;
            _bufferEnd = end;
            var i = start;
            while (i < end)
            {
                if (_inString)
                {
                    i = ScanString(i, end);
                    continue;
                }

                if (_inWord)
                {
                    i = ScanWord(i, end);
                    continue;
                }

                // Schnell: nur Klammern und Zeichenketten zählen; langsam: jedes Token, weil hier Knoten entstehen oder Werte erfasst werden.
                var skip = _slow
                    ? buffer.AsSpan(i, end - i).IndexOfAnyExcept(Whitespace)
                    : buffer.AsSpan(i, end - i).IndexOfAny(Structure);
                if (skip < 0)
                {
                    break;
                }

                i += skip;
                switch (buffer[i])
                {
                    case (byte)'"':
                        BeginToken(i);
                        _inString = true;
                        i++;
                        break;
                    case (byte)'{':
                        OnOpen(i);
                        i++;
                        break;
                    case (byte)'}':
                        OnClose(i);
                        i++;
                        break;
                    case (byte)'=':
                        OnEquals();
                        i++;
                        break;
                    default:
                        BeginToken(i);
                        _inWord = true;
                        break;
                }
            }

            if (_holding)
            {
                _carry.Append(buffer.AsSpan(_hashFrom, end - _hashFrom));
                _hashFrom = end;
            }
            else
            {
                FlushPending(end);
            }
        }

        public SaveIndex Finish(long total)
        {
            var cutInString = _inString;
            if (_inWord)
            {
                _inWord = false;
                EndToken(_bufferEnd);
            }

            _inString = false;
            if (_hasCandidate)
            {
                OnItem();
            }

            _afterEquals = false;
            _holding = false;
            FlushPending(_bufferEnd);
            while (_open > 0)
            {
                CloseTop(total);
            }

            var state = _depth > 0 || cutInString ? SaveReadState.Incomplete : SaveReadState.Complete;
            return Result(state, total);
        }

        public SaveIndex Binary(long total)
        {
            while (_open > 0)
            {
                CloseTop(total);
            }

            return Result(SaveReadState.Binary, total);
        }

        private SaveIndex Result(SaveReadState state, long total)
        {
            _nodes.TrimExcess();
            _ids.TrimExcess();
            return new SaveIndex(
                _nodes,
                _ids,
                state,
                total,
                _scalars,
                _dlcs,
                _tables.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, string>)pair.Value, StringComparer.Ordinal));
        }

        private int ScanWord(int i, int end)
        {
            var found = _buffer.AsSpan(i, end - i).IndexOfAny(WordEnd);
            var stop = found < 0 ? end : i + found;
            if (_copy)
            {
                Token.Append(_buffer.AsSpan(i, stop - i));
            }

            if (found >= 0)
            {
                _inWord = false;
                EndToken(stop);
            }

            return stop;
        }

        private int ScanString(int i, int end)
        {
            if (_escape)
            {
                // Das Zeichen nach \ gehört ohne Bedeutung zur Zeichenkette.
                _escape = false;
                if (_copy)
                {
                    Token.Append(_buffer.AsSpan(i, 1));
                }

                return i + 1;
            }

            var found = _buffer.AsSpan(i, end - i).IndexOfAny(StringStops);
            var stop = found < 0 ? end : i + found;
            if (_copy)
            {
                Token.Append(_buffer.AsSpan(i, stop - i));
            }

            if (found < 0)
            {
                return end;
            }

            if (_buffer[stop] == (byte)'\\')
            {
                _escape = true;
                return stop + 1;
            }

            _inString = false;
            EndToken(stop + 1);
            return stop + 1;
        }

        private Bytes Token => _role == TokenRole.Value ? _value : _key;

        private void BeginToken(int index)
        {
            if (!_slow)
            {
                _role = TokenRole.Ignored;
                _copy = false;
                return;
            }

            if (_afterEquals)
            {
                _afterEquals = false;
                _role = TokenRole.Value;
                _value.Clear();
                _copy = ValueNeeded();
                if (_holding)
                {
                    _holding = false;
                    if (TryUnitId(Top, out var id, out var slot, out var generation))
                    {
                        // Die zurückgehaltenen Schlüsselbytes und der Wert gehören dem Objekt; es liegt wie ein Kind eine Ebene tiefer
                        // und schließt am Ende des Werts.
                        Push(NodeKind.ScalarUnit, _holdStart, id, slot, generation).Depth = _depth + 1;
                    }
                }

                return;
            }

            if (_hasCandidate)
            {
                OnItem();
            }

            _role = TokenRole.Candidate;
            _key.Clear();
            _copy = true;
            if (_nodeCanOpen)
            {
                FlushPending(index);
                _holding = true;
                _holdStart = _base + index;
            }
        }

        private void EndToken(int end)
        {
            var role = _role;
            _role = TokenRole.Ignored;
            if (role == TokenRole.Candidate)
            {
                _hasCandidate = true;
            }
            else if (role == TokenRole.Value)
            {
                OnValue(end);
            }

            _copy = false;
        }

        private void OnEquals()
        {
            if (_hasCandidate)
            {
                _hasCandidate = false;
                _afterEquals = true;
            }
        }

        /// <summary>Ein Token ohne folgendes <c>=</c>: Eintrag einer Liste.</summary>
        private void OnItem()
        {
            _hasCandidate = false;
            _holding = false;
            var capture = Capture;
            if (capture.Depth != _depth || (!capture.Node.Dlcs && capture.Node.KeyLists.Count == 0))
            {
                return;
            }

            var item = _key.ToString();
            if (capture.Node.Dlcs)
            {
                _dlcs.Add(item);
            }

            foreach (var (table, first) in capture.Node.KeyLists)
            {
                table[(capture.Items + first).ToString(CultureInfo.InvariantCulture)] = item;
            }

            capture.Items++;
        }

        private void OnValue(int end)
        {
            if (_open > 0 && Top.Kind == NodeKind.ScalarUnit)
            {
                FlushPending(end);
                CloseTop(_base + end);
            }

            if (!_copy)
            {
                return;
            }

            var capture = Capture;
            if (capture.Depth == _depth && capture.Node.WantsValues)
            {
                var key = Intern(_key.Span);
                var value = _value.ToString();
                if (capture.Node.ScalarPrefix is { } prefix)
                {
                    _scalars.TryAdd(prefix.Length == 0 ? key : prefix + key, value);
                }

                foreach (var table in capture.Node.TagTables)
                {
                    table[key] = value;
                }
            }

            var top = Top;
            if (WantsItemId(top))
            {
                top.ItemId = _value.ToString();
            }
        }

        private bool ValueNeeded() =>
            (Capture.Depth == _depth && Capture.Node.WantsValues) || WantsItemId(Top);

        /// <summary>Ob der Wert das Kennfeld eines namenlosen Listeneintrags ist (erstes direktes Vorkommen).</summary>
        private bool WantsItemId(Frame top) =>
            top.Depth == _depth && top.ItemIdFieldBytes is not null && top.ItemId is null && _key.Span.SequenceEqual(top.ItemIdFieldBytes);

        private void OnOpen(int index)
        {
            var keyed = _afterEquals;
            _afterEquals = false;
            if (_hasCandidate)
            {
                OnItem();
            }

            var parent = Top;
            var nodeCanOpen = _nodeCanOpen;
            var parentCapture = Capture.Depth == _depth ? Capture : null;
            _depth++;

            if (nodeCanOpen)
            {
                if (keyed && TryUnitId(parent, out var id, out var slot, out var generation))
                {
                    Push(NodeKind.Unit, _holdStart, id, slot, generation);
                }
                else if (_depth <= _nodeDepth)
                {
                    long start;
                    if (keyed)
                    {
                        start = _holdStart;
                    }
                    else
                    {
                        FlushPending(index);
                        start = _base + index;
                    }

                    var frame = Push(_depth == 1 ? NodeKind.Section : NodeKind.Block, start, 0, null, null);
                    var key = keyed ? Intern(_key.Span) : null;
                    if (key is not null)
                    {
                        frame.Name = Occurrence(parent, key);
                    }
                    else
                    {
                        frame.Anonymous = true;
                        frame.AnonymousIndex = parent.AnonymousCount++;
                        frame.ItemIdField = parent.Rules!.ListIdField;
                        frame.ItemIdFieldBytes = frame.ItemIdField is null ? null : Encoding.UTF8.GetBytes(frame.ItemIdField);
                    }

                    var segment = key ?? "[]";
                    var parentPath = parent.Rules!.Path;
                    frame.Rules = RulesFor(parentPath.Length == 0 ? segment : parentPath + "/" + segment);
                }
            }

            _holding = false;

            if (keyed && parentCapture is not null && parentCapture.Node.Children.Count > 0
                && parentCapture.Node.Children.TryGetValue(Intern(_key.Span), out var child))
            {
                _captures.Add(new CaptureFrame(child, _depth));
            }

            UpdateMode();
        }

        private void OnClose(int index)
        {
            _afterEquals = false;
            if (_hasCandidate)
            {
                OnItem();
            }

            _holding = false;
            if (_depth == 0)
            {
                // Überzählige schließende Klammer: bleibt Text der Wurzel.
                return;
            }

            if (Top.Depth == _depth)
            {
                FlushPending(index + 1);
                CloseTop(_base + index + 1);
            }

            if (Capture.Depth == _depth)
            {
                _captures.RemoveAt(_captures.Count - 1);
            }

            _depth--;
            UpdateMode();
        }

        private void UpdateMode()
        {
            var top = Top;
            var atNode = top.Depth == _depth && top.Kind != NodeKind.Unit;
            _nodeCanOpen = atNode && (_depth + 1 <= _nodeDepth || top.Rules!.IsContainer);
            _slow = _nodeCanOpen || Capture.Depth == _depth || (atNode && top.ItemIdFieldBytes is not null);
        }

        /// <summary>Id aus dem Schlüssel, wenn der Knoten ein Objektbehälter ist und der Schlüssel nur aus Ziffern besteht.</summary>
        private bool TryUnitId(Frame container, out ulong id, out int slot, out int generation)
        {
            id = 0;
            slot = generation = 0;
            if (container.Kind == NodeKind.Unit || container.Rules is not { IsContainer: true } rules)
            {
                return false;
            }

            var key = _key.Span;
            if (key.Length == 0 || key.Length > 20)
            {
                return false;
            }

            foreach (var b in key)
            {
                var digit = (uint)(b - '0');
                if (digit > 9 || id > (ulong.MaxValue - digit) / 10)
                {
                    return false;
                }

                id = id * 10 + digit;
            }

            var mask = (1UL << rules.SlotBits) - 1;
            var slotValue = id & mask;
            var generationValue = id >> rules.SlotBits;
            if (slotValue > int.MaxValue || generationValue > int.MaxValue)
            {
                return false;
            }

            slot = (int)slotValue;
            generation = (int)generationValue;
            return true;
        }

        private Frame Push(NodeKind kind, long start, ulong id, int? slot, int? generation)
        {
            var parent = Top;
            var frame = Rent();
            frame.Id = Add(id);
            frame.Parent = parent.Id;
            frame.Depth = _depth;
            frame.Kind = kind;
            frame.Start = start;
            frame.Slot = slot;
            frame.Generation = generation;
            if (slot is { } value)
            {
                frame.Name = SlotName(value);
            }

            return frame;
        }

        private Frame Rent()
        {
            if (_open == _frames.Count)
            {
                _frames.Add(new Frame());
            }

            var frame = _frames[_open++];
            frame.Own = Fnv64.Create();
            frame.Full = Fnv64.Create();
            frame.OwnGap = false;
            frame.OwnWord = false;
            frame.Name = string.Empty;
            frame.Slot = null;
            frame.Generation = null;
            frame.Rules = null;
            frame.Anonymous = false;
            frame.AnonymousIndex = 0;
            frame.AnonymousCount = 0;
            frame.ItemIdField = null;
            frame.ItemIdFieldBytes = null;
            frame.ItemId = null;
            frame.Seen = null;
            frame.Start = 0;
            frame.Depth = 0;
            return frame;
        }

        private int Add(ulong id)
        {
            _nodes.Add(null!);
            _ids.Add(id);
            return _nodes.Count - 1;
        }

        private void CloseTop(long end)
        {
            var frame = Top;
            _open--;
            var name = frame.Name;
            if (frame.Anonymous)
            {
                var parent = Top;
                name = frame.ItemId is not null
                    ? Occurrence(parent, frame.ItemIdField + "=" + frame.ItemId)
                    : Intern("[" + frame.AnonymousIndex.ToString(CultureInfo.InvariantCulture) + "]");
            }

            _nodes[frame.Id] = new SaveNode(frame.Id, frame.Parent, frame.Kind, name, frame.Start, end, frame.Full.Value, frame.Own.Value, frame.Slot, frame.Generation);
            if (_open > 0)
            {
                Top.Full.Append(frame.Full.Value);
            }

            frame.Seen = null;
        }

        /// <summary>Schlägt die offenen Bytes bis <paramref name="upTo"/> (Pufferindex) dem innersten offenen Knoten zu.</summary>
        private void FlushPending(int upTo)
        {
            if (_open == 0)
            {
                return;
            }

            var frame = Top;
            if (_carry.Length > 0)
            {
                AppendOwn(frame, _carry.Span);
                frame.Full.Append(_carry.Span);
                _carry.Clear();
            }

            if (upTo > _hashFrom)
            {
                var span = _buffer.AsSpan(_hashFrom, upTo - _hashFrom);
                AppendOwn(frame, span);
                frame.Full.Append(span);
                _hashFrom = upTo;
            }
        }

        /// <summary>
        /// Hasht für den Eigen-Hash nur Bytes, die keine Leerraumzeichen außerhalb von Zeichenketten sind, behält aber die Grenze
        /// zwischen Wörtern: Liegt Leerraum zwischen zwei Wortbytes, geht ein Trennbyte ein (<c>a=1 2</c> ist nicht <c>a=12</c>).
        /// Leerraum, den ein Kindknoten unterbricht, zählt als ein Lauf, denn Kindbytes berühren den Zustand des Elternknotens nicht.
        /// Die Spannen kommen in Textreihenfolge, daher trägt <see cref="_ownInString"/> den Zeichenkettenzustand über Knoten- und
        /// Puffergrenzen hinweg.
        /// </summary>
        private void AppendOwn(Frame frame, ReadOnlySpan<byte> span)
        {
            while (span.Length > 0)
            {
                if (_ownEscape)
                {
                    // Das Zeichen nach \ gehört ohne Bedeutung zur Zeichenkette.
                    _ownEscape = false;
                    frame.Own.Append(span[..1]);
                    span = span[1..];
                    continue;
                }

                if (_ownInString)
                {
                    var stop = span.IndexOfAny(StringStops);
                    var take = stop < 0 ? span.Length : stop + 1;
                    frame.Own.Append(span[..take]);
                    if (stop >= 0)
                    {
                        if (span[stop] == (byte)'\\')
                        {
                            _ownEscape = true;
                        }
                        else
                        {
                            _ownInString = false;
                        }
                    }

                    span = span[take..];
                    continue;
                }

                var skip = span.IndexOfAnyExcept(Whitespace);
                if (skip < 0)
                {
                    frame.OwnGap = true;
                    return;
                }

                if (skip > 0)
                {
                    frame.OwnGap = true;
                    span = span[skip..];
                }

                if (span[0] == (byte)'"')
                {
                    // Anführungszeichen sind keine Wortbytes; vor ihnen und nach ihnen braucht es kein Trennbyte.
                    frame.Own.Append(span[..1]);
                    frame.OwnGap = false;
                    frame.OwnWord = false;
                    _ownInString = true;
                    span = span[1..];
                    continue;
                }

                var end = span.IndexOfAny(OutsideStringStops);
                var run = end < 0 ? span.Length : end;
                if (frame.OwnGap && frame.OwnWord && IsWordByte(span[0]))
                {
                    frame.Own.Append(Separator);
                }

                frame.Own.Append(span[..run]);
                frame.OwnGap = false;
                frame.OwnWord = IsWordByte(span[run - 1]);
                span = span[run..];
            }
        }

        /// <summary>Wortbyte: weder Klammer, <c>=</c>, Anführungszeichen noch ein anderes Operatorzeichen.</summary>
        private static bool IsWordByte(byte value) => !NonWord.Contains(value);

        /// <summary>Name unter den Geschwistern: beim ersten Vorkommen der Schlüssel, danach <c>key#2</c>, <c>key#3</c> …</summary>
        private string Occurrence(Frame parent, string key)
        {
            parent.Seen ??= new Dictionary<string, int>(StringComparer.Ordinal);
            var count = parent.Seen.GetValueOrDefault(key) + 1;
            parent.Seen[key] = count;
            return count == 1 ? key : Intern(key + "#" + count.ToString(CultureInfo.InvariantCulture));
        }

        private PathRules RulesFor(string path)
        {
            if (!_rules.TryGetValue(path, out var rules))
            {
                rules = new PathRules(path, _profile.IsObjectContainer(path), _profile.SlotBitsFor(path), _profile.AnonymousIdFor(path));
                _rules[path] = rules;
            }

            return rules;
        }

        private string SlotName(int slot)
        {
            if (!_slotNames.TryGetValue(slot, out var name))
            {
                name = slot.ToString(CultureInfo.InvariantCulture);
                _slotNames[slot] = name;
            }

            return name;
        }

        private string Intern(ReadOnlySpan<byte> bytes) => Intern(Encoding.UTF8.GetString(bytes));

        private string Intern(string value)
        {
            if (_interned.TryGetValue(value, out var existing))
            {
                return existing;
            }

            _interned[value] = value;
            return value;
        }

        private static CaptureNode CaptureAt(CaptureNode root, string path)
        {
            var node = root;
            foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!node.Children.TryGetValue(segment, out var child))
                {
                    child = new CaptureNode();
                    node.Children[segment] = child;
                }

                node = child;
            }

            return node;
        }
    }
}
