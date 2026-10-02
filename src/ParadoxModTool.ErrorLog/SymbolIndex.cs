using System.Text.RegularExpressions;

namespace ParadoxModTool.ErrorLog;

public enum SymbolRole
{
    Definition,
    Set,
    Reference,
    Localization,

    /// <summary>Vorkommen in einer <c>.gui</c>-Datei (z.B. <c>GetVariable('x')</c>); das Spiel zählt es wie Lokalisierung nicht mit.</summary>
    Gui,
}

public sealed record SymbolOccurrence(string Name, SymbolRole Role, string FilePath, int Line, ModSource? Mod);

/// <summary>Wurzel für den Index. <c>Mod</c> null = Vanilla. Ohne <c>IndexReferences</c> werden nur Definitionen aus common-Ordnern gesammelt.</summary>
public sealed record SymbolRoot(string Directory, ModSource? Mod, bool IndexReferences);

/// <summary>Fortschritt des Index-Aufbaus: gelesene und alle Dateien. <c>Total = 0</c>: Die Dateien werden noch gesucht.</summary>
public sealed record IndexProgress(int Done, int Total);

/// <summary>
/// Namen in Skript- (.txt) und Lokalisierungsdateien (.yml): Definitionen (oberste Schlüssel mit Block in common-Ordnern), gesetzte Stellen
/// (Zeile enthält vorher set_/add_/change_/clamp_…, Plan-Ruling 5), sonstige Verweise und Vorkommen in Lokalisierung. Kommentare zählen nicht.
/// </summary>
public sealed class SymbolIndex
{
    /// <summary>Nach so vielen gelesenen Dateien meldet <see cref="Build"/> Fortschritt.</summary>
    public const int ProgressStep = 50;

    private static readonly Regex Identifier = new(@"(?<![A-Za-z0-9_])[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Setter = new(@"(?<![A-Za-z0-9_])(?:set|add|change|clamp)_[A-Za-z0-9_]*\s*=", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BlockAssignment = new(@"^\s*=\s*(?:\{|$)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly Dictionary<string, List<SymbolOccurrence>> _byName;

    private SymbolIndex(Dictionary<string, List<SymbolOccurrence>> byName) => _byName = byName;

    public static SymbolIndex Empty { get; } = new(new Dictionary<string, List<SymbolOccurrence>>(StringComparer.Ordinal));

    /// <summary>
    /// Baut den Index über alle Wurzeln. <paramref name="progress"/> erhält zuerst (0, 0), nach der Dateisuche (0, Gesamt), beim Lesen alle
    /// <see cref="ProgressStep"/> Dateien und am Ende (Gesamt, Gesamt); die Meldungen können aus mehreren Threads kommen.
    /// </summary>
    public static SymbolIndex Build(IEnumerable<SymbolRoot> roots, CancellationToken cancellation = default, IProgress<IndexProgress>? progress = null)
    {
        progress?.Report(new IndexProgress(0, 0));
        var work = new List<(string Path, string Relative, SymbolRoot Root)>();
        foreach (var root in roots)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!Directory.Exists(root.Directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(root.Directory, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(path);
                var isScript = extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
                var isLocalization = extension.Equals(".yml", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".gui", StringComparison.OrdinalIgnoreCase);
                if (!isScript && !isLocalization)
                {
                    continue;
                }

                var relative = Path.GetRelativePath(root.Directory, path).Replace('\\', '/');
                var first = relative.Split('/')[0];
                if (first.StartsWith('.') || (root.Mod is not null && string.Equals(first, "snapshots", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!root.IndexReferences && (isLocalization || !IsCommon(relative)))
                {
                    continue;
                }

                work.Add((path, relative, root));
            }
        }

        progress?.Report(new IndexProgress(0, work.Count));
        var done = 0;
        var results = new List<SymbolOccurrence>[work.Count];
        Parallel.For(0, work.Count, new ParallelOptions { CancellationToken = cancellation }, i =>
        {
            var (path, relative, root) = work[i];
            var text = File.ReadAllText(path);
            results[i] = path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ? ScanWords(path, text, root, SymbolRole.Localization)
                : path.EndsWith(".gui", StringComparison.OrdinalIgnoreCase) ? ScanWords(path, text, root, SymbolRole.Gui)
                : ScanScript(path, text, root, IsCommon(relative));
            var count = Interlocked.Increment(ref done);
            if (count % ProgressStep == 0 && count < work.Count)
            {
                progress?.Report(new IndexProgress(count, work.Count));
            }
        });

        progress?.Report(new IndexProgress(work.Count, work.Count));

        var byName = new Dictionary<string, List<SymbolOccurrence>>(StringComparer.Ordinal);
        foreach (var list in results)
        {
            foreach (var occurrence in list)
            {
                if (!byName.TryGetValue(occurrence.Name, out var bucket))
                {
                    bucket = new List<SymbolOccurrence>();
                    byName[occurrence.Name] = bucket;
                }

                bucket.Add(occurrence);
            }
        }

        return new SymbolIndex(byName);
    }

    public IReadOnlyList<SymbolOccurrence> Find(string name) =>
        _byName.TryGetValue(name, out var list) ? list : Array.Empty<SymbolOccurrence>();

    /// <summary>Namen mit Editierabstand höchstens <paramref name="maxDistance"/>, von denen mindestens ein Vorkommen den Filter erfüllt; nächste zuerst.</summary>
    public IReadOnlyList<string> SimilarNames(string name, Func<SymbolOccurrence, bool> filter, int maxDistance = 2, int max = 10) =>
        _byName
            .Where(pair => pair.Key != name && Math.Abs(pair.Key.Length - name.Length) <= maxDistance)
            .Select(pair => (Name: pair.Key, Distance: Distance(pair.Key, name, maxDistance), Occurrences: pair.Value))
            .Where(x => x.Distance <= maxDistance && x.Occurrences.Any(filter))
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Take(max)
            .Select(x => x.Name)
            .ToList();

    /// <summary>Levenshtein-Abstand; bricht ab und liefert <paramref name="max"/> + 1, sobald er sicher größer als <paramref name="max"/> ist.</summary>
    public static int Distance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max)
        {
            return max + 1;
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMinimum = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowMinimum = Math.Min(rowMinimum, current[j]);
            }

            if (rowMinimum > max)
            {
                return max + 1;
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    private static bool IsCommon(string relative) =>
        relative.StartsWith("common/", StringComparison.OrdinalIgnoreCase) || relative.Contains("/common/", StringComparison.OrdinalIgnoreCase);

    private static List<SymbolOccurrence> ScanScript(string path, string text, SymbolRoot root, bool isCommon)
    {
        var result = new List<SymbolOccurrence>();
        var depth = 0;
        var lines = text.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = StripComment(lines[lineIndex]);
            var depthAt = DepthBeforeEachChar(line, ref depth);
            var setter = Setter.Match(line);
            foreach (Match token in Identifier.Matches(line))
            {
                var tokenEnd = token.Index + token.Length;
                if (depthAt[token.Index] == 0 && isCommon && BlockAssignment.IsMatch(line[tokenEnd..]))
                {
                    result.Add(new SymbolOccurrence(token.Value, SymbolRole.Definition, path, lineIndex + 1, root.Mod));
                    continue;
                }

                if (!root.IndexReferences)
                {
                    continue;
                }

                var isSet = setter.Success && token.Index >= setter.Index + setter.Length;
                result.Add(new SymbolOccurrence(token.Value, isSet ? SymbolRole.Set : SymbolRole.Reference, path, lineIndex + 1, root.Mod));
            }
        }

        return result;
    }

    /// <summary>Lokalisierung und GUI: jeder Bezeichner außerhalb von Kommentaren ist ein Vorkommen mit der Rolle <paramref name="role"/>.</summary>
    private static List<SymbolOccurrence> ScanWords(string path, string text, SymbolRoot root, SymbolRole role)
    {
        var result = new List<SymbolOccurrence>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]);
            foreach (Match token in Identifier.Matches(line))
            {
                result.Add(new SymbolOccurrence(token.Value, role, path, i + 1, root.Mod));
            }
        }

        return result;
    }

    /// <summary>
    /// Klammertiefe vor jedem Zeichen der Zeile (Index = Zeichenposition, letzter Eintrag = Zeilenende). Die Zeile wird in einem Durchgang
    /// gelesen, damit der Zustand "in Anführungszeichen" über die ganze Zeile gilt; Klammern in Zeichenketten zählen nicht.
    /// <paramref name="depth"/> steht danach auf der Tiefe am Zeilenende (nie unter 0).
    /// </summary>
    private static int[] DepthBeforeEachChar(string line, ref int depth)
    {
        var result = new int[line.Length + 1];
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            result[i] = depth;
            var c = line[i];
            if (c == '"')
            {
                inString = !inString;
            }
            else if (!inString && c == '{')
            {
                depth++;
            }
            else if (!inString && c == '}' && depth > 0)
            {
                depth--;
            }
        }

        result[line.Length] = depth;
        return result;
    }

    /// <summary>Entfernt "# …" außerhalb von Anführungszeichen.</summary>
    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inString = !inString;
            }
            else if (line[i] == '#' && !inString)
            {
                return line[..i];
            }
        }

        return line.TrimEnd('\r');
    }
}
