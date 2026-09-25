namespace ParadoxModTool.Templates;

/// <summary>Ergebnis des Lernens für einen Ordner: Zahl der Objekte mit Lokalisierung und die Muster, die sie gemeinsam haben.</summary>
public sealed record LocalizationObjectLearning(int ObjectCount, IReadOnlyList<LocalizationObjectPattern> Patterns);

/// <summary>
/// Lernt, wie die Lokalisierungsschlüssel der Objekte eines Ordners aus dem Objektnamen gebildet werden. Für jedes Objekt werden alle Schlüssel gesucht, die den
/// Namen enthalten (Präfix und Endung bis <see cref="MaxAffixLength"/> Zeichen); der Anteil eines Paars (Präfix, Endung) ist die Zahl der Objekte mit dem Paar
/// geteilt durch die Zahl der Objekte, für die überhaupt ein Schlüssel gefunden wurde.
/// </summary>
public static class LocalizationObjectLearner
{
    /// <summary>Kürzere Namen (bis drei Zeichen) sind zu unscharf und werden nicht gesucht.</summary>
    public const int MinNameLength = 4;

    public const int MaxAffixLength = 30;

    /// <summary>Ein Ordner braucht so viele Objekte mit Lokalisierung, und ein Paar so viele Objekte, um gespeichert zu werden.</summary>
    public const int MinObjects = 5;

    /// <summary>Ab diesem Anteil wird ein Paar als Angebot gespeichert.</summary>
    public const double OfferShare = 0.10;

    /// <summary>Ab diesem Anteil ist ein Paar vorgewählt.</summary>
    public const double PreselectShare = 0.90;

    public static IReadOnlyDictionary<string, LocalizationObjectLearning> Learn(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> namesByFolder,
        IEnumerable<string> keys,
        CancellationToken cancellation = default)
    {
        var trie = new NameTrie();
        var objectsPerFolder = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (folder, names) in namesByFolder)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names.Where(n => n.Length >= MinNameLength))
            {
                if (set.Add(name))
                {
                    trie.Add(name, folder);
                }
            }

            objectsPerFolder[folder] = set;
        }

        var found = new Dictionary<(string Folder, string Name), HashSet<(string Prefix, string Suffix)>>();
        var counter = 0;
        foreach (var key in keys)
        {
            if ((++counter & 0x3FFF) == 0)
            {
                cancellation.ThrowIfCancellationRequested();
            }

            var lastStart = Math.Min(MaxAffixLength, key.Length - 1);
            for (var start = 0; start <= lastStart; start++)
            {
                var from = start;
                trie.Walk(key, from, (length, owners) =>
                {
                    if (key.Length - from - length > MaxAffixLength)
                    {
                        return;
                    }

                    var pair = (key[..from], key[(from + length)..]);
                    foreach (var owner in owners)
                    {
                        if (!found.TryGetValue(owner, out var pairs))
                        {
                            found[owner] = pairs = new HashSet<(string Prefix, string Suffix)>();
                        }

                        pairs.Add(pair);
                    }
                });
            }
        }

        cancellation.ThrowIfCancellationRequested();

        var result = new Dictionary<string, LocalizationObjectLearning>(StringComparer.Ordinal);
        foreach (var (folder, names) in objectsPerFolder)
        {
            var withLoc = 0;
            var counts = new Dictionary<(string Prefix, string Suffix), int>();
            foreach (var name in names)
            {
                if (!found.TryGetValue((folder, name), out var pairs))
                {
                    continue;
                }

                withLoc++;
                foreach (var pair in pairs)
                {
                    counts[pair] = counts.GetValueOrDefault(pair) + 1;
                }
            }

            if (withLoc < MinObjects)
            {
                continue;
            }

            var patterns = counts
                .Where(c => c.Value >= MinObjects && (double)c.Value / withLoc >= OfferShare)
                .Select(c =>
                {
                    var share = (double)c.Value / withLoc;
                    return new LocalizationObjectPattern
                    {
                        Prefix = c.Key.Prefix,
                        Suffix = c.Key.Suffix,
                        Share = Math.Round(share, 3),
                        Preselected = share >= PreselectShare,
                    };
                })
                .OrderByDescending(p => p.Share)
                .ThenBy(p => p.Prefix.Length)
                .ThenBy(p => p.Prefix, StringComparer.Ordinal)
                .ThenBy(p => p.Suffix, StringComparer.Ordinal)
                .ToList();

            if (patterns.Count > 0)
            {
                result[folder] = new LocalizationObjectLearning(withLoc, patterns);
            }
        }

        return result;
    }

    /// <summary>Ein Präfixbaum über alle Objektnamen: findet in einem Schlüssel ab einer Stelle alle Namen, die dort beginnen, ohne Teilstrings zu erzeugen.</summary>
    private sealed class NameTrie
    {
        private readonly Dictionary<(int Node, char Char), int> _next = new();
        private readonly Dictionary<int, List<(string Folder, string Name)>> _terminal = new();
        private int _nodes = 1;

        public void Add(string name, string folder)
        {
            var node = 0;
            foreach (var c in name)
            {
                if (!_next.TryGetValue((node, c), out var next))
                {
                    next = _nodes++;
                    _next[(node, c)] = next;
                }

                node = next;
            }

            if (!_terminal.TryGetValue(node, out var owners))
            {
                _terminal[node] = owners = new List<(string Folder, string Name)>();
            }

            owners.Add((folder, name));
        }

        /// <summary>Ruft <paramref name="found"/> für jeden Namen, der in <paramref name="text"/> ab <paramref name="start"/> steht, mit seiner Länge und den Ordnern, in denen er vorkommt.</summary>
        public void Walk(string text, int start, Action<int, List<(string Folder, string Name)>> found)
        {
            var node = 0;
            for (var i = start; i < text.Length; i++)
            {
                if (!_next.TryGetValue((node, text[i]), out node))
                {
                    return;
                }

                if (_terminal.TryGetValue(node, out var owners))
                {
                    found(i - start + 1, owners);
                }
            }
        }
    }
}
