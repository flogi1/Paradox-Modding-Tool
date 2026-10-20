using System.Text;

namespace ParadoxModTool.SaveGames;

public enum SaveEntryKind
{
    Variable,
    ListItem,
}

/// <summary>
/// Ein Eintrag eines <c>variables</c>-Blocks. <see cref="OwnerPath"/>: Blockschlüssel vor <c>variables</c>, mit <c>/</c> verbunden, anonyme
/// Blöcke als <c>#</c>, global leer. <see cref="Name"/>: <c>flag</c> (Variable) bzw. <c>name</c> (Liste). <see cref="ValueField"/> und
/// <see cref="RawValue"/>: erstes skalares Feld außer <c>type</c> im <c>data</c>- bzw. <c>item</c>-Block, leer wenn keines.
/// </summary>
public sealed record SaveVariableEntry(string OwnerPath, string Name, SaveEntryKind Kind, string Type, string ValueField, string RawValue);

public enum SaveReadStatus
{
    Complete,

    /// <summary>Die Datei endet, während noch Blöcke offen sind.</summary>
    Incomplete,

    /// <summary>Die Datei wurde während des Lesens länger oder kürzer (das Spiel schreibt noch).</summary>
    Changed,
}

/// <summary>Ein Objektpfad-Muster (<c>*</c> = genau ein Segment) und das skalare Feld, das bei passenden Objekten erfasst wird.</summary>
public sealed record SaveFieldCapture(string Pattern, string Field);

/// <summary>
/// Was der Leser außer den Variablen erfasst: direkte skalare Kinder der Blöcke in <see cref="Tables"/> (Schlüssel <c>Tabelle/Kind</c>) und
/// die Felder aus <see cref="Fields"/> (Schlüssel <c>Objektpfad/Feld</c>).
/// </summary>
public sealed record SaveCaptureRequest(IReadOnlyList<string> Tables, IReadOnlyList<SaveFieldCapture> Fields)
{
    public static SaveCaptureRequest None { get; } = new(Array.Empty<string>(), Array.Empty<SaveFieldCapture>());
}

public sealed record SaveReadResult(IReadOnlyList<SaveVariableEntry> Entries, IReadOnlyDictionary<string, string> Captured, SaveReadStatus Status);

/// <summary>
/// Streamender Leser für Text-Spielstände: geht byteweise nach Klammern (die Einrückung der Spielstände ist unzuverlässig), baut keinen
/// Syntaxbaum und merkt sich nur Variablen, Variablenlisten und angeforderte Namensfelder. Die erste Zeile (Kopfzeile) wird übersprungen.
/// </summary>
public static class SaveVariableReader
{
    private const int BufferSize = 1 << 20;

    /// <param name="compressed">
    /// Komprimierter Text-Stand (<see cref="SaveFileInfo.Compressed"/>): gelesen wird der ZIP-Eintrag <c>gamestate</c> (<see cref="SaveGamestateStream"/>),
    /// seine erste Zeile gilt nur als Kopfzeile, wenn sie mit <c>SAV</c> beginnt; der Fortschritt folgt den gelesenen komprimierten Bytes. Fehler im
    /// Archiv werfen <see cref="InvalidDataException"/> mit <see cref="SaveExplorerTexts.CompressedUnreadable"/>.
    /// </param>
    public static SaveReadResult ReadFile(string path, SaveCaptureRequest request, IProgress<double>? fraction, CancellationToken cancellationToken, bool compressed = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
        var lengthBefore = stream.Length;
        SaveReadResult result;
        if (compressed)
        {
            using var gamestate = SaveGamestateStream.Open(stream);
            var bytes = fraction is null ? null : new Progress(_ => fraction.Report(lengthBefore == 0 ? 1 : Math.Min(1, (double)stream.Position / lengthBefore)));
            try
            {
                result = Read(gamestate, request, bytes, cancellationToken, headerOnlyIfSav: true);
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or IOException)
            {
                throw SaveGamestateStream.Unreadable();
            }

            fraction?.Report(1);
        }
        else
        {
            var bytes = fraction is null ? null : new Progress(read => fraction.Report(lengthBefore == 0 ? 1 : Math.Min(1, (double)read / lengthBefore)));
            result = Read(stream, request, bytes, cancellationToken);
        }

        var lengthAfter = new FileInfo(path).Length;
        return lengthAfter != lengthBefore ? result with { Status = SaveReadStatus.Changed } : result;
    }

    /// <param name="headerOnlyIfSav">Die erste Zeile nur überspringen, wenn sie mit <c>SAV</c> beginnt (Inhalt eines komprimierten Stands).</param>
    public static SaveReadResult Read(Stream stream, SaveCaptureRequest request, IProgress<long>? bytesRead, CancellationToken cancellationToken, bool headerOnlyIfSav = false)
    {
        var parser = new Parser(request);
        var buffer = new byte[BufferSize];
        var headerSkipped = false;
        long total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = stream.Read(buffer, 0, buffer.Length);
            if (count <= 0)
            {
                break;
            }

            total += count;
            var start = 0;
            if (!headerSkipped && headerOnlyIfSav && !(count >= 3 && buffer[0] == (byte)'S' && buffer[1] == (byte)'A' && buffer[2] == (byte)'V'))
            {
                headerSkipped = true;
            }

            if (!headerSkipped)
            {
                var newline = Array.IndexOf(buffer, (byte)'\n', 0, count);
                if (newline < 0)
                {
                    bytesRead?.Report(total);
                    continue;
                }

                headerSkipped = true;
                start = newline + 1;
            }

            parser.Feed(buffer, start, count);
            bytesRead?.Report(total);
        }

        parser.Finish();
        return new SaveReadResult(parser.Entries, parser.Captured, parser.Depth > 0 ? SaveReadStatus.Incomplete : SaveReadStatus.Complete);
    }

    /// <summary>Meldet synchron (<see cref="Progress{T}"/> würde über den SynchronizationContext verzögern).</summary>
    private sealed class Progress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }

    private sealed class Parser
    {
        private readonly List<string> _stack = new();
        private readonly Dictionary<string, string> _interned = new(StringComparer.Ordinal);
        private readonly string[][] _tables;
        private readonly (string[] Pattern, string Field)[] _fields;

        // Tokenizer
        private byte[] _token = new byte[256];
        private int _tokenLength;
        private bool _inString;
        private bool _escape;

        // Grammatik
        private string? _pendingKey;
        private string? _eqKey;

        // Variablen- bzw. Listeneintrag, der gerade gelesen wird
        private int _itemDepth = -1;
        private int _dataDepth = -1;
        private SaveEntryKind _itemKind;
        private string? _name;
        private string? _type;
        private string? _valueField;
        private string? _value;

        public Parser(SaveCaptureRequest request)
        {
            _tables = request.Tables.Select(t => t.Split('/')).ToArray();
            _fields = request.Fields.Select(f => (f.Pattern.Split('/'), f.Field)).ToArray();
        }

        public List<SaveVariableEntry> Entries { get; } = new();

        public Dictionary<string, string> Captured { get; } = new(StringComparer.Ordinal);

        public int Depth => _stack.Count;

        public void Feed(byte[] buffer, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                var b = buffer[i];
                if (_inString)
                {
                    if (_escape)
                    {
                        Append(b);
                        _escape = false;
                    }
                    else if (b == (byte)'\\')
                    {
                        _escape = true;
                    }
                    else if (b == (byte)'"')
                    {
                        _inString = false;
                        OnWord(TakeToken());
                    }
                    else
                    {
                        Append(b);
                    }

                    continue;
                }

                switch (b)
                {
                    case (byte)' ':
                    case (byte)'\t':
                    case (byte)'\r':
                    case (byte)'\n':
                        FlushWord();
                        break;
                    case (byte)'"':
                        FlushWord();
                        _inString = true;
                        break;
                    case (byte)'=':
                        FlushWord();
                        OnEquals();
                        break;
                    case (byte)'{':
                        FlushWord();
                        OnOpen();
                        break;
                    case (byte)'}':
                        FlushWord();
                        OnClose();
                        break;
                    default:
                        Append(b);
                        break;
                }
            }
        }

        public void Finish()
        {
            if (!_inString)
            {
                FlushWord();
            }
        }

        private void Append(byte b)
        {
            if (_tokenLength == _token.Length)
            {
                Array.Resize(ref _token, _token.Length * 2);
            }

            _token[_tokenLength++] = b;
        }

        private string TakeToken()
        {
            var text = Encoding.UTF8.GetString(_token, 0, _tokenLength);
            _tokenLength = 0;
            return text;
        }

        private void FlushWord()
        {
            if (_tokenLength > 0)
            {
                OnWord(TakeToken());
            }
        }

        private void OnWord(string word)
        {
            if (_eqKey is not null)
            {
                OnScalar(_eqKey, word);
                _eqKey = null;
                _pendingKey = null;
            }
            else
            {
                _pendingKey = word;
            }
        }

        private void OnEquals()
        {
            if (_pendingKey is not null)
            {
                _eqKey = _pendingKey;
                _pendingKey = null;
            }
        }

        private void OnOpen()
        {
            var key = _eqKey ?? "#";
            _eqKey = null;
            _pendingKey = null;
            _stack.Add(key);
            var n = _stack.Count;
            if (n >= 3 && key == "#" && _stack[n - 3] == "variables" && _itemDepth < 0)
            {
                var container = _stack[n - 2];
                if (container is "data" or "list")
                {
                    _itemDepth = n;
                    _itemKind = container == "data" ? SaveEntryKind.Variable : SaveEntryKind.ListItem;
                    _name = _type = _valueField = _value = null;
                }
            }
            else if (_itemDepth == n - 1 && _dataDepth < 0 && key == (_itemKind == SaveEntryKind.Variable ? "data" : "item"))
            {
                _dataDepth = n;
            }
        }

        private void OnClose()
        {
            _eqKey = null;
            _pendingKey = null;
            var n = _stack.Count;
            if (n == 0)
            {
                return;
            }

            if (n == _dataDepth)
            {
                _dataDepth = -1;
            }
            else if (n == _itemDepth)
            {
                if (_name is not null)
                {
                    var owner = Intern(string.Join('/', _stack.Take(n - 3)));
                    Entries.Add(new SaveVariableEntry(owner, Intern(_name), _itemKind, Intern(_type ?? string.Empty), Intern(_valueField ?? string.Empty), _value ?? string.Empty));
                }

                _itemDepth = -1;
                _dataDepth = -1;
            }

            _stack.RemoveAt(n - 1);
        }

        private void OnScalar(string key, string value)
        {
            var n = _stack.Count;
            if (n >= 1 && _itemDepth < 0 && _stack[n - 1] == "variables")
            {
                // Hearts of Iron IV: Variablen stehen flach als name=wert im Block, Listen als name^0, name^1 … mit name^num als Länge.
                // Jomini-Spiele haben hier nur die Blöcke data und list, nie einen Wert.
                var owner = Intern(string.Join('/', _stack.Take(n - 1)));
                var caret = key.IndexOf('^');
                if (caret < 0)
                {
                    Entries.Add(new SaveVariableEntry(owner, Intern(key), SaveEntryKind.Variable, string.Empty, "value", value));
                }
                else if (key[(caret + 1)..] != "num")
                {
                    Entries.Add(new SaveVariableEntry(owner, Intern(key[..caret]), SaveEntryKind.ListItem, string.Empty, "value", value));
                }
            }

            if (n == _itemDepth)
            {
                if (key == (_itemKind == SaveEntryKind.Variable ? "flag" : "name"))
                {
                    _name = value;
                }
            }
            else if (n == _dataDepth)
            {
                if (key == "type")
                {
                    _type = value;
                }
                else if (_valueField is null)
                {
                    _valueField = key;
                    _value = value;
                }
            }

            foreach (var table in _tables)
            {
                if (Matches(table))
                {
                    Captured[string.Join('/', _stack) + "/" + key] = value;
                }
            }

            foreach (var (pattern, field) in _fields)
            {
                if (field == key && Matches(pattern))
                {
                    Captured[string.Join('/', _stack) + "/" + field] = value;
                }
            }
        }

        private bool Matches(string[] pattern)
        {
            if (pattern.Length != _stack.Count)
            {
                return false;
            }

            for (var i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] != "*" && pattern[i] != _stack[i])
                {
                    return false;
                }
            }

            return true;
        }

        private string Intern(string value)
        {
            if (_interned.TryGetValue(value, out var existing))
            {
                return existing;
            }

            _interned[value] = value;
            return value;
        }
    }
}
