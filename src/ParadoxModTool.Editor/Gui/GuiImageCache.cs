using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ParadoxModTool.GuiDesign.Imaging;

namespace ParadoxModTool.Editor.Gui;

/// <summary>Ergebnis von <see cref="GuiImageCache.TryGet"/>.</summary>
public enum GuiImageState
{
    /// <summary>Das Bild ist da.</summary>
    Ready,

    /// <summary>Das Bild steht in der Warteschlange oder wird gerade dekodiert.</summary>
    Pending,

    /// <summary>Die Datei fehlt, ist kaputt oder hat ein unbekanntes Format.</summary>
    Unreadable,

    /// <summary>Das Bild wurde in diesem Lauf schon dekodiert und wieder verdrängt; es wird bis <see cref="GuiImageCache.NewRun"/> nicht erneut dekodiert.</summary>
    OverBudget,
}

/// <summary>
/// Dekodierte Texturen der Vorschau als eingefrorene BGRA-Bitmaps (von jedem Thread nutzbar). Das Format bestimmen die ersten Byte: PNG und JPG dekodiert
/// WPF, alles andere (<c>.dds</c>, <c>.tga</c>) <see cref="GuiImageDecoder"/>. Begrenzt nach Pixelbytes (zuletzt benutzte bleiben); das jüngste Bild bleibt
/// auch über der Grenze. Unlesbare Dateien merkt sich der Puffer, damit nicht jedes Neuzeichnen die Datei erneut liest. Parallel aufrufbar.
/// <para>
/// Zwei Wege: <see cref="Get"/> (klassische Vorschau) dekodiert auf dem aufrufenden Thread. <see cref="TryGet"/> und <see cref="Prefetch"/> dekodieren nie
/// dort, sondern in zwei Hintergrund-Threads, und melden Fertiges mit <see cref="ImageReady"/>. Beide teilen die Einträge für <see cref="GuiTint.White"/>.
/// </para>
/// <para>
/// Bilder und Fehlschläge gelten, solange Schreibzeitpunkt und Länge der Datei gleich sind. Geprüft wird das einmal je Vorschaulauf (<see cref="NewRun"/>,
/// jede neue Szene beginnt einen), nie beim Neuzeichnen zwischen zwei Läufen (Verschieben und Zoomen fragen jedes Bild bei jedem Bild ab). So zeigt die
/// nächste Vorschau eine geänderte oder nachträglich angelegte Textur. Je Lauf wird ein Bild höchstens einmal dekodiert, auch wenn es verdrängt wird.
/// <see cref="Get"/> prüft auf dem aufrufenden Thread. <see cref="TryGet"/> und <see cref="Prefetch"/> liefern bis zur Prüfung den letzten bekannten
/// Stand (altes Bild bzw. unlesbar) und lassen einen Hintergrund-Thread prüfen; hat sich die Datei geändert, meldet <see cref="ImageReady"/> das neue Bild.
/// </para>
/// </summary>
public sealed class GuiImageCache
{
    /// <summary>Gemeinsamer Puffer aller Vorschauen (256 MB Pixeldaten).</summary>
    public static GuiImageCache Shared { get; } = new(256L * 1024 * 1024);

    /// <summary>Höchstzahl gemerkter Fehlschläge; darüber wird die Liste geleert.</summary>
    private const int MaxFailures = 4096;

    /// <summary>Anzahl der Hintergrund-Threads, die dekodieren.</summary>
    private const int WorkerCount = 2;

    /// <summary>Kleinster Abstand zweier <see cref="ImageReady"/>-Ereignisse, solange noch Bilder in der Warteschlange sind.</summary>
    private const long RaiseIntervalMs = 100;

    /// <summary>So viele Dekodier-Threads merkt sich <see cref="DecodeThreadIds"/> höchstens (die jüngsten).</summary>
    internal const int MaxRecordedDecodeThreads = 256;

    private static readonly byte[] PngSignature = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly long _maxBytes;
    private readonly Dictionary<Key, LinkedListNode<Entry>> _entries = new(KeyComparer.Instance);
    private readonly LinkedList<Entry> _recent = new();
    private readonly Dictionary<string, Failure> _failed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Schlüssel, die in diesem Lauf schon gespeichert wurden (auch wenn sie inzwischen verdrängt sind).</summary>
    private readonly HashSet<Key> _decodedThisRun = new(KeyComparer.Instance);

    /// <summary>Schlüssel, die in diesem Lauf schon zur Prüfung (Eintrag oder Fehlschlag aus einem früheren Lauf) eingestellt wurden.</summary>
    private readonly HashSet<Key> _checkedThisRun = new(KeyComparer.Instance);

    private readonly Queue<Key> _queue = new();

    /// <summary>Schlüssel in der Warteschlange oder in Arbeit.</summary>
    private readonly HashSet<Key> _queued = new(KeyComparer.Instance);

    /// <summary>Pfade, die ein Hintergrund-Thread gerade bearbeitet: dieselbe Datei wird nie von zwei Threads zugleich dekodiert.</summary>
    private readonly HashSet<string> _busyPaths = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<int> _decodeThreads = new();
    private readonly object _gate = new();
    private int _generation;
    private int _workers;
    private int _active;
    private long _lastRaise;
    private int _decodeCount;

    public GuiImageCache(long maxBytes) => _maxBytes = Math.Max(0, maxBytes);

    /// <summary>
    /// Ein Hintergrund-Thread hat ein Bild gespeichert (oder als unlesbar gemerkt). Kommt auf einem Hintergrund-Thread, höchstens alle 100 ms, solange
    /// die Warteschlange noch läuft, und einmal, wenn sie leer ist.
    /// </summary>
    public event EventHandler? ImageReady;

    /// <summary>Summe der Pixelbytes aller gemerkten Bilder.</summary>
    public long CachedBytes { get; private set; }

    /// <summary>Wie oft dieser Puffer eine Datei dekodiert hat (für Tests; der Zähler von <see cref="GuiImageDecoder"/> gilt für den ganzen Prozess).</summary>
    internal int DecodeCount => Volatile.Read(ref _decodeCount);

    /// <summary>Bilder in der Warteschlange oder in Arbeit (für Tests).</summary>
    internal int QueuedCount
    {
        get
        {
            lock (_gate)
            {
                return _queued.Count;
            }
        }
    }

    /// <summary>
    /// Das Bild (oder sein Fehlschlag) ist in diesem Lauf geprüft und nicht mehr in der Warteschlange (für Tests: <see cref="TryGet"/> liefert bis zur
    /// Prüfung den letzten bekannten Stand, das ist kein Zeichen, dass der Hintergrund fertig ist).
    /// </summary>
    internal bool IsSettled(string fullPath, GuiTint tint)
    {
        var key = new Key(fullPath, tint);
        lock (_gate)
        {
            if (_queued.Contains(key))
            {
                return false;
            }

            if (_entries.TryGetValue(key, out var node))
            {
                return node.Value.Generation == _generation;
            }

            return (_failed.TryGetValue(fullPath, out var failure) && failure.Generation == _generation) || _decodedThisRun.Contains(key);
        }
    }

    /// <summary>Die Threads, auf denen dieser Puffer dekodiert hat (für Tests).</summary>
    internal int[] DecodeThreadIds
    {
        get
        {
            lock (_gate)
            {
                return _decodeThreads.ToArray();
            }
        }
    }

    /// <summary>Beginnt einen neuen Vorschaulauf: jedes Bild und jeder Fehlschlag wird beim nächsten Abruf einmal gegen die Datei geprüft.</summary>
    public void NewRun()
    {
        lock (_gate)
        {
            _generation++;
            _decodedThisRun.Clear();
            _checkedThisRun.Clear();
        }
    }

    /// <summary>Das Bild der Datei oder <see langword="null"/> (fehlt, unlesbar, Format unbekannt). Dekodiert auf dem aufrufenden Thread.</summary>
    public BitmapSource? Get(string fullPath)
    {
        var key = new Key(fullPath, GuiTint.White);
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node) && node.Value.Generation == _generation)
            {
                Touch(node);
                return node.Value.Bitmap;
            }

            if (node is null && _failed.TryGetValue(fullPath, out var failure) && failure.Generation == _generation)
            {
                return null;
            }
        }

        // Aus einem früheren Lauf (oder neu): Datei einmal prüfen, außerhalb der Sperre.
        var stamp = StampOf(fullPath);
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                if (node.Value.Stamp == stamp)
                {
                    node.Value.Generation = _generation;
                    Touch(node);
                    return node.Value.Bitmap;
                }

                Discard(node);
            }
            else if (_failed.TryGetValue(fullPath, out var failure) && failure.Stamp == stamp)
            {
                _failed[fullPath] = failure with { Generation = _generation };
                return null;
            }
        }

        // Außerhalb der Sperre dekodieren; dekodieren zwei Aufrufer gleichzeitig, gilt das zuerst gespeicherte Ergebnis.
        var bitmap = LoadCounted(fullPath);
        lock (_gate)
        {
            if (bitmap is null)
            {
                if (_entries.TryGetValue(key, out var existing) && existing.Value.Stamp == stamp)
                {
                    return existing.Value.Bitmap;
                }

                RecordFailureLocked(fullPath, stamp);
                return null;
            }

            return StoreLocked(key, bitmap, stamp);
        }
    }

    /// <summary>
    /// Fragt das Bild ab, ohne je zu dekodieren. Fehlt es noch, kommt es in die Warteschlange der Hintergrund-Threads (<see cref="GuiImageState.Pending"/>)
    /// und <see cref="ImageReady"/> meldet es später. Die Färbung wird aus dem weißen Bild gemacht und zählt als eigener Eintrag im Puffer.
    /// </summary>
    public GuiImageState TryGet(string fullPath, GuiTint tint, out BitmapSource? bitmap)
    {
        var key = new Key(fullPath, tint);
        bitmap = null;
        lock (_gate)
        {
            // Bekanntes Bild, auch aus einem früheren Lauf: sofort zeigen (kein Rückfall auf den schematischen Kasten bei jeder neuen Szene). Ob die
            // Datei sich geändert hat, prüft ein Hintergrund-Thread; dann kommt das neue Bild mit ImageReady. Nie ein Dateizugriff auf diesem Thread.
            if (_entries.TryGetValue(key, out var node))
            {
                Touch(node);
                bitmap = node.Value.Bitmap;
                if (node.Value.Generation != _generation)
                {
                    RevalidateLocked(key);
                }

                return GuiImageState.Ready;
            }

            // Bekannter Fehlschlag: ebenso sofort unlesbar (schraffiert) und im Hintergrund geprüft.
            if (_failed.TryGetValue(fullPath, out var failure))
            {
                if (failure.Generation != _generation)
                {
                    RevalidateLocked(key);
                }

                return GuiImageState.Unreadable;
            }

            if (_decodedThisRun.Contains(key))
            {
                return GuiImageState.OverBudget;
            }

            EnqueueLocked(key);
            return GuiImageState.Pending;
        }
    }

    /// <summary>
    /// Stellt Bilder in der angegebenen Reihenfolge in die Warteschlange; bekannte (fertig, unlesbar, verdrängt, schon wartend) werden übergangen. Bilder
    /// und Fehlschläge aus einem früheren Lauf kommen einmal je Lauf zur Prüfung in die Warteschlange.
    /// </summary>
    public void Prefetch(IEnumerable<(string Path, GuiTint Tint)> items)
    {
        // Erst auslesen, dann sperren: eine träge Aufzählung läuft nicht unter der Sperre.
        var list = items.ToList();
        lock (_gate)
        {
            foreach (var (path, tint) in list)
            {
                var key = new Key(path, tint);
                if (_queued.Contains(key) || _decodedThisRun.Contains(key))
                {
                    continue;
                }

                if (_entries.TryGetValue(key, out var node))
                {
                    if (node.Value.Generation != _generation)
                    {
                        RevalidateLocked(key);
                    }

                    continue;
                }

                if (_failed.TryGetValue(path, out var failure))
                {
                    if (failure.Generation != _generation)
                    {
                        RevalidateLocked(key);
                    }

                    continue;
                }

                EnqueueLocked(key);
            }
        }
    }

    /// <summary>
    /// Stellt einen Eintrag oder Fehlschlag aus einem früheren Lauf zur Prüfung in die Warteschlange, höchstens einmal je Lauf: so stellt ein Neuzeichnen
    /// nach <see cref="ImageReady"/> nichts erneut ein, auch wenn der Hintergrund-Thread den Eintrag nicht auf den neuen Lauf heben konnte. Unter der Sperre.
    /// </summary>
    private void RevalidateLocked(Key key)
    {
        if (_checkedThisRun.Add(key))
        {
            EnqueueLocked(key);
        }
    }

    /// <summary>Das weiße Bild ist gerade gemerkt (für Tests der Verdrängung).</summary>
    internal bool Contains(string fullPath) => Contains(fullPath, GuiTint.White);

    /// <summary>Die Färbung des Bildes ist gerade gemerkt.</summary>
    internal bool Contains(string fullPath, GuiTint tint)
    {
        lock (_gate)
        {
            return _entries.ContainsKey(new Key(fullPath, tint));
        }
    }

    private void EnqueueLocked(Key key)
    {
        if (!_queued.Add(key))
        {
            return;
        }

        _queue.Enqueue(key);

        // Die Threads starten erst mit dem ersten Eintrag (Tests legen viele Puffer an) und enden, wenn die Warteschlange leer ist.
        if (_workers < WorkerCount)
        {
            _workers++;
            new Thread(WorkerLoop) { IsBackground = true, Name = "GuiImageCache", Priority = ThreadPriority.BelowNormal }.Start();
        }
    }

    private void WorkerLoop()
    {
        while (true)
        {
            Key key;
            lock (_gate)
            {
                if (!_queue.TryDequeue(out key))
                {
                    _workers--;
                    return;
                }

                // Dieselbe Datei nie zweimal zugleich: der zweite Thread wartet und findet das Ergebnis dann im Puffer.
                while (_busyPaths.Contains(key.Path))
                {
                    Monitor.Wait(_gate);
                }

                _busyPaths.Add(key.Path);
                _active++;
            }

            try
            {
                Process(key);
            }
            catch (Exception)
            {
                // Ein Bild, bei dem das Dekodieren oder Färben wirft, gilt als unlesbar; der Thread läuft weiter.
                var stamp = StampOf(key.Path);
                lock (_gate)
                {
                    FailLocked(key, stamp);
                }
            }

            bool raise;
            lock (_gate)
            {
                _queued.Remove(key);
                _busyPaths.Remove(key.Path);
                _active--;
                Monitor.PulseAll(_gate);

                // Bei laufender Warteschlange höchstens alle 100 ms, bei leerer immer: so sieht der Aufrufer das Ende.
                var now = Environment.TickCount64;
                raise = (_queue.Count == 0 && _active == 0) || now - _lastRaise >= RaiseIntervalMs;
                if (raise)
                {
                    _lastRaise = now;
                }
            }

            if (raise)
            {
                RaiseImageReady();
            }
        }
    }

    private void RaiseImageReady()
    {
        try
        {
            ImageReady?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // Ein fehlerhafter Beobachter darf den Hintergrund-Thread nicht beenden.
        }
    }

    /// <summary>Macht ein Bild fertig (dekodieren, färben, speichern) oder merkt es als unlesbar. Läuft auf einem Hintergrund-Thread.</summary>
    private void Process(Key key)
    {
        var path = key.Path;
        var stamp = StampOf(path);
        var whiteKey = key with { Tint = GuiTint.White };
        BitmapSource? white = null;
        lock (_gate)
        {
            // Bild aus einem früheren Lauf (Prüfung) oder zwischen Abruf und Start schon gespeichert (etwa von Get): bei gleicher Datei nur bestätigen.
            if (_entries.TryGetValue(key, out var node) && node.Value.Stamp == stamp)
            {
                node.Value.Generation = _generation;
                Touch(node);
                return;
            }

            // Ab hier ist ein vorhandener Eintrag veraltet. Er bleibt sichtbar, bis das neue Bild ihn ersetzt (StoreLocked); jeder Fehlschlag verwirft ihn.
            if (stamp.Length < 0)
            {
                FailLocked(key, stamp);
                return;
            }

            if (_failed.TryGetValue(path, out var failure) && failure.Stamp == stamp)
            {
                FailLocked(key, stamp);
                return;
            }

            if (!key.Tint.IsWhite)
            {
                if (_entries.TryGetValue(whiteKey, out var whiteNode))
                {
                    if (whiteNode.Value.Stamp == stamp)
                    {
                        whiteNode.Value.Generation = _generation;
                        Touch(whiteNode);
                        white = whiteNode.Value.Bitmap;
                    }

                    // Ein veraltetes weißes Bild ersetzt StoreLocked nach dem Dekodieren.
                }
                else if (_decodedThisRun.Contains(whiteKey))
                {
                    // Das weiße Bild wurde in diesem Lauf schon dekodiert und verdrängt: nicht noch einmal dekodieren. Eine veraltete Färbung fällt weg.
                    if (node is not null)
                    {
                        Discard(node);
                    }

                    _decodedThisRun.Add(key);
                    return;
                }
            }
        }

        var decoded = false;
        if (white is null)
        {
            white = LoadCounted(path);
            decoded = true;
            if (white is null)
            {
                lock (_gate)
                {
                    FailLocked(key, stamp);
                }

                return;
            }
        }

        var result = key.Tint.IsWhite ? white : Tinted(white, key.Tint);
        lock (_gate)
        {
            if (decoded && !key.Tint.IsWhite)
            {
                // Das weiße Bild kommt zuerst in den Puffer, die Färbung danach: bei Platzmangel fällt zuerst das ältere.
                StoreLocked(whiteKey, white, stamp);
            }

            StoreLocked(key, result, stamp);
        }
    }

    /// <summary>Speichert das Bild und verdrängt die ältesten; gibt es schon einen gleich alten Eintrag, gilt dieser. Unter der Sperre.</summary>
    private BitmapSource StoreLocked(Key key, BitmapSource bitmap, (DateTime WriteTime, long Length) stamp)
    {
        if (_entries.TryGetValue(key, out var existing))
        {
            if (existing.Value.Stamp == stamp)
            {
                existing.Value.Generation = _generation;
                Touch(existing);
                return existing.Value.Bitmap;
            }

            Discard(existing);
        }

        _failed.Remove(key.Path);
        var bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
        _entries[key] = _recent.AddFirst(new Entry(key, bitmap, bytes, stamp) { Generation = _generation });
        _decodedThisRun.Add(key);
        CachedBytes += bytes;
        while (CachedBytes > _maxBytes && _recent.Count > 1)
        {
            Remove(_recent.Last!);
        }

        return bitmap;
    }

    /// <summary>
    /// Merkt die Datei als unlesbar (für diesen Lauf) und verwirft veraltete Bilder des Schlüssels und das weiße Bild derselben Datei, sonst zeigte ein
    /// Abruf weiter das alte Bild einer inzwischen kaputten Datei. Unter der Sperre.
    /// </summary>
    private void FailLocked(Key key, (DateTime WriteTime, long Length) stamp)
    {
        if (_entries.TryGetValue(key, out var node) && node.Value.Stamp != stamp)
        {
            Discard(node);
        }

        var whiteKey = key with { Tint = GuiTint.White };
        if (!key.Tint.IsWhite && _entries.TryGetValue(whiteKey, out var whiteNode) && whiteNode.Value.Stamp != stamp)
        {
            Discard(whiteNode);
        }

        RecordFailureLocked(key.Path, stamp);
    }

    private void RecordFailureLocked(string path, (DateTime WriteTime, long Length) stamp)
    {
        if (_failed.Count >= MaxFailures)
        {
            _failed.Clear();
        }

        _failed[path] = new Failure(stamp, _generation);
    }

    private void Touch(LinkedListNode<Entry> node)
    {
        _recent.Remove(node);
        _recent.AddFirst(node);
    }

    /// <summary>Verdrängt den Eintrag. Er bleibt in <c>_decodedThisRun</c>, damit er in diesem Lauf nicht noch einmal dekodiert wird.</summary>
    private void Remove(LinkedListNode<Entry> node)
    {
        _recent.Remove(node);
        _entries.Remove(node.Value.Key);
        CachedBytes -= node.Value.Bytes;
    }

    /// <summary>Wirft einen veralteten Eintrag weg (die Datei hat sich geändert): er darf neu dekodiert werden.</summary>
    private void Discard(LinkedListNode<Entry> node)
    {
        Remove(node);
        _decodedThisRun.Remove(node.Value.Key);
    }

    /// <summary>Schreibzeitpunkt und Länge; eine fehlende Datei hat (default, -1).</summary>
    private static (DateTime WriteTime, long Length) StampOf(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : (default, -1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return (default, -1);
        }
    }

    /// <summary>Wie <see cref="Load"/>, zählt aber den Aufruf; jede andere Ausnahme gilt als unlesbar.</summary>
    private BitmapSource? LoadCounted(string fullPath)
    {
        Interlocked.Increment(ref _decodeCount);
        lock (_gate)
        {
            // Nur die letzten Einträge behalten: der gemeinsame Puffer läuft so lange wie die Anwendung.
            if (_decodeThreads.Count >= MaxRecordedDecodeThreads)
            {
                _decodeThreads.RemoveAt(0);
            }

            _decodeThreads.Add(Environment.CurrentManagedThreadId);
        }

        try
        {
            return Load(fullPath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static BitmapSource? Load(string fullPath)
    {
        // Das Format bestimmen die ersten Byte, nicht die Endung: CK III hat ein PNG mit der Endung .dds.
        if (IsPngOrJpg(fullPath))
        {
            return LoadWithWpf(fullPath);
        }

        if (GuiImageDecoder.TryDecode(fullPath) is not { } image)
        {
            return null;
        }

        // GuiImage hat gerade Alpha, das entspricht Bgra32 (nicht Pbgra32).
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Bgra, image.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>PNG-Signatur oder <c>FF D8</c> am Anfang der Datei.</summary>
    private static bool IsPngOrJpg(string fullPath)
    {
        try
        {
            using var stream = File.OpenRead(fullPath);
            Span<byte> head = stackalloc byte[8];
            var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            var data = head[..read];
            return data.StartsWith(PngSignature) || (read >= 2 && data[0] == 0xFF && data[1] == 0xD8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static BitmapSource? LoadWithWpf(string fullPath)
    {
        try
        {
            using var stream = File.OpenRead(fullPath);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                return null;
            }

            BitmapSource frame = decoder.Frames[0];
            if (frame.Format != PixelFormats.Bgra32)
            {
                frame = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            }

            // In einen eigenen Puffer kopieren: 96 dpi wie bei DDS/TGA, damit Ausschnitte in Pixeln und geräteunabhängigen Einheiten gleich sind.
            var stride = frame.PixelWidth * 4;
            var pixels = new byte[stride * frame.PixelHeight];
            frame.CopyPixels(pixels, stride, 0);
            var bitmap = BitmapSource.Create(frame.PixelWidth, frame.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or FileFormatException or InvalidOperationException or OverflowException or OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Kopie des weißen Bildes mit den Farbfaktoren multipliziert.</summary>
    private static BitmapSource Tinted(BitmapSource white, GuiTint tint)
    {
        var stride = white.PixelWidth * 4;
        var pixels = new byte[stride * white.PixelHeight];
        white.CopyPixels(pixels, stride, 0);
        GuiImageOps.MultiplyInPlace(pixels, tint);
        var bitmap = BitmapSource.Create(white.PixelWidth, white.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Pfad (ohne Beachtung der Groß-/Kleinschreibung) und Färbung.</summary>
    private readonly record struct Key(string Path, GuiTint Tint);

    private sealed class KeyComparer : IEqualityComparer<Key>
    {
        public static readonly KeyComparer Instance = new();

        public bool Equals(Key x, Key y) => x.Tint.Equals(y.Tint) && string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(Key key) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Path), key.Tint);
    }

    private sealed record Entry(Key Key, BitmapSource Bitmap, long Bytes, (DateTime WriteTime, long Length) Stamp)
    {
        /// <summary>Lauf, in dem die Datei zuletzt geprüft wurde.</summary>
        public int Generation { get; set; }
    }

    private sealed record Failure((DateTime WriteTime, long Length) Stamp, int Generation);
}
