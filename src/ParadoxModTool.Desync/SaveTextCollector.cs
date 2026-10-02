using System.Text;

namespace ParadoxModTool.Desync;

/// <summary>
/// Eine Textanfrage an <see cref="SaveTextCollector"/>: Byte-Bereich <c>[Start, End)</c> im Stream (ab dem Schlüssel des Knotens),
/// abzüglich der <paramref name="Exclude"/>-Bereiche (z. B. die Bereiche der Kindknoten, wenn nur der eigene Text gebraucht wird).
/// </summary>
public sealed record CollectRequest(long Start, long End, IReadOnlyList<(long Start, long End)> Exclude);

/// <summary>
/// Zweiter Durchgang über einen Text-Spielstand: holt in einem einzigen Vorwärtslauf den Text der angefragten Bereiche.
/// Der Stream wird nur einmal und vorwärts gelesen (Zip-Einträge), mit einem wiederverwendeten 1-MiB-Puffer wie <see cref="SaveIndexer"/>.
/// Bereiche dürfen sich überlappen oder verschachteln; jede aktive Anfrage bekommt ihre eigenen Bytes ohne ihre Ausschlüsse.
/// </summary>
public static class SaveTextCollector
{
    private const int BufferSize = 1 << 20;

    /// <summary>Der Text je Anfrage in Anfragereihenfolge, als UTF-8 dekodiert. Bereiche hinter dem Stream-Ende werden gekürzt.</summary>
    public static IReadOnlyList<string> Collect(Stream stream, IReadOnlyList<CollectRequest> requests, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var results = new string[requests.Count];
        Array.Fill(results, string.Empty);

        // Nach Start sortiert; leere Bereiche bleiben leer und brauchen keinen Puffer.
        var order = Enumerable.Range(0, requests.Count)
            .Where(i => requests[i].End > requests[i].Start)
            .OrderBy(i => requests[i].Start)
            .ToArray();
        var next = 0;
        var active = new List<Active>();
        var buffer = new byte[BufferSize];
        long position = 0;

        while (next < order.Length || active.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();

            // Volle Puffer: die Grenzen liegen damit immer auf Vielfachen von 1 MiB.
            var count = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            if (count <= 0)
            {
                break;
            }

            var chunkEnd = position + count;
            while (next < order.Length && requests[order[next]].Start < chunkEnd)
            {
                active.Add(new Active(order[next], requests[order[next]]));
                next++;
            }

            for (var i = active.Count - 1; i >= 0; i--)
            {
                var item = active[i];
                item.Feed(buffer, position, chunkEnd);
                if (item.Request.End <= chunkEnd)
                {
                    results[item.Index] = item.Decode();
                    active.RemoveAt(i);
                }
            }

            position = chunkEnd;
        }

        // Stream zu Ende: noch offene Anfragen reichen über das Ende hinaus und werden gekürzt.
        foreach (var item in active)
        {
            results[item.Index] = item.Decode();
        }

        return results;
    }

    /// <summary>Eine laufende Anfrage: gesammelte Bytes und die nach Start sortierten Ausschlüsse mit Lesezeiger.</summary>
    private sealed class Active
    {
        private readonly MemoryStream _bytes = new();
        private readonly (long Start, long End)[] _exclude;
        private int _excludeNext;

        public Active(int index, CollectRequest request)
        {
            Index = index;
            Request = request;
            _exclude = request.Exclude.OrderBy(e => e.Start).ToArray();
        }

        public int Index { get; }

        public CollectRequest Request { get; }

        /// <summary>Kopiert den Teil des Puffers <c>[position, chunkEnd)</c>, der im Bereich liegt und in keinem Ausschluss.</summary>
        public void Feed(byte[] buffer, long position, long chunkEnd)
        {
            var cursor = Math.Max(Request.Start, position);
            var limit = Math.Min(Request.End, chunkEnd);
            while (cursor < limit)
            {
                while (_excludeNext < _exclude.Length && _exclude[_excludeNext].End <= cursor)
                {
                    _excludeNext++;
                }

                if (_excludeNext >= _exclude.Length || _exclude[_excludeNext].Start >= limit)
                {
                    Write(buffer, position, cursor, limit);
                    return;
                }

                var exclude = _exclude[_excludeNext];
                if (exclude.Start > cursor)
                {
                    Write(buffer, position, cursor, exclude.Start);
                }

                // Der Ausschluss kann hinter diesem Puffer enden; der Zeiger bleibt dann stehen und greift im nächsten.
                cursor = Math.Max(cursor, exclude.End);
            }
        }

        public string Decode() => Encoding.UTF8.GetString(_bytes.GetBuffer(), 0, (int)_bytes.Length);

        private void Write(byte[] buffer, long position, long from, long to) =>
            _bytes.Write(buffer, (int)(from - position), (int)(to - from));
    }
}
