using System.Security.Cryptography;
using System.Text;

namespace ParadoxModTool.Scopes;

/// <summary>Eine Skriptdatei der Vanilla-Dateien, aus der gelernt wird.</summary>
public sealed record ScopeSourceFile(string Folder, string Path, long Size, long WriteTicks);

public sealed record LearnResult(ScopeRules Rules, TimeSpan Duration);

/// <summary>
/// Lernt aus den Skriptdateien eines Spiels, in welchem Scope die Felder ihrer Objekte stehen. Jeder Trigger, Effect oder Modifier, der direkt in einem
/// Feld steht (<c>bureaucracies/*/potential</c>), stimmt für die Scopes, in denen er gilt; der Scope mit der höchsten Zustimmung (mindestens 90 %, eindeutig)
/// wird der Start-Scope des Feldes. Hängt der Scope vom Wert eines Geschwisterfeldes ab (<c>type = character</c>), wird das als Unterscheider gelernt.
/// </summary>
public static class ScopeRuleLearner
{
    /// <summary>Felder mit weniger Statements werden nicht bewertet.</summary>
    public const int MinStatements = 5;

    private const int MaxFieldDepth = 3;
    private const double DiscriminatorThreshold = 0.95;

    // Ein Unterscheider muss die Zustimmung des Feldes spürbar heben; schon ein Prozent Ausnahmen wären als Warnungen im Editor sichtbar.
    private const double MinDiscriminatorGain = 0.005;

    /// <summary>Alle Skriptordner unter den Wurzeln: <c>common\&lt;Ordner&gt;</c> und <c>events</c>, jeweils auch unter <c>in_game</c>.</summary>
    public static IReadOnlyList<(string Folder, string Directory)> FindScriptFolders(IEnumerable<string> roots)
    {
        var result = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string folder, string directory)
        {
            if (seen.Add(Path.GetFullPath(directory)))
            {
                result.Add((folder, directory));
            }
        }

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var baseDir in new[] { root, Path.Combine(root, "in_game") })
            {
                var common = Path.Combine(baseDir, "common");
                if (Directory.Exists(common))
                {
                    foreach (var directory in Directory.GetDirectories(common))
                    {
                        Add(Path.GetFileName(directory), directory);
                    }
                }

                var events = Path.Combine(baseDir, "events");
                if (Directory.Exists(events))
                {
                    Add("events", events);
                }
            }
        }

        return result;
    }

    /// <summary>Alle Skriptdateien (<c>.txt</c>, ohne <c>readme</c>) samt Größe und Änderungszeit; ein Aufruf, der nur Dateiinformationen liest.</summary>
    public static IReadOnlyList<ScopeSourceFile> EnumerateFiles(IEnumerable<string> roots)
    {
        var files = new List<ScopeSourceFile>();
        foreach (var (folder, directory) in FindScriptFolders(roots))
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            foreach (var path in Directory.EnumerateFiles(directory, "*.txt", options))
            {
                if (Path.GetFileName(path).StartsWith("readme", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var info = new FileInfo(path);
                files.Add(new ScopeSourceFile(folder, path, info.Length, info.LastWriteTimeUtc.Ticks));
            }
        }

        return files;
    }

    /// <summary>Fingerabdruck der Dateiliste: ändert sich eine Datei (Größe, Zeit) oder kommt eine hinzu, ändert sich auch er.</summary>
    public static string ComputeFingerprint(IReadOnlyList<ScopeSourceFile> files)
    {
        var lines = files
            .Select(f => $"{f.Path.Replace('\\', '/')}|{f.Size}|{f.WriteTicks}")
            .OrderBy(l => l, StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    public static LearnResult Learn(
        IEnumerable<string> roots,
        ScopeDb db,
        int docsImportId,
        DateTime nowUtc,
        CancellationToken cancellation = default,
        IProgress<(int Done, int Total)>? progress = null,
        string? defaultEventType = null,
        IReadOnlyDictionary<string, string>? eventBlockScopes = null,
        bool numericKeysChangeScope = false)
    {
        var started = DateTime.UtcNow;
        var files = EnumerateFiles(roots);
        var tallies = new Dictionary<string, PathTally>(StringComparer.Ordinal);
        var savedTallies = new Dictionary<(string Field, string Name), Tally>();
        var savedByName = new Dictionary<string, Tally>(StringComparer.Ordinal);

        var done = 0;
        foreach (var file in files)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report((++done, files.Count));

            string text;
            try
            {
                text = File.ReadAllText(file.Path, Encoding.UTF8);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var obj in ScriptTree.Parse(text))
            {
                if (!obj.IsBlock || obj.Key is null)
                {
                    continue;
                }

                var siblings = obj.Children!
                    .Where(c => c.Key is not null && c.Scalar is { Length: > 0 and <= 40 })
                    .GroupBy(c => c.Key!)
                    .ToDictionary(g => g.Key, g => g.First().Scalar!);

                // Ereignisse heißen nach ihrer Kennung; ihre Art steht im Feld type (country_event …), im Blocknamen (Stellaris) oder ist der Standard
                // des Spiels und bildet den Pfad. Ein Event mit eigenem Root-Scope (scope = artifact) stimmt nicht mit ab, ein Event-Block ohne
                // Root-Scope (Stellaris: event) auch nicht.
                string path;
                if (file.Folder == "events")
                {
                    var blockType = ScopeEngine.BlockEventType(obj.Key, eventBlockScopes);
                    var eventType = siblings.GetValueOrDefault("type") ?? blockType ?? ScopeEngine.DefaultTypeFor(obj.Key, defaultEventType);
                    if (eventType is null || siblings.ContainsKey("scope") || (blockType is not null && eventBlockScopes![blockType] != ScopeRules.LearnedRootScope && !db.IsScopeType(eventBlockScopes[blockType])))
                    {
                        continue;
                    }

                    path = $"events/{eventType}";
                }
                else
                {
                    path = $"{file.Folder}/*";
                }

                WalkField(db, obj.Children!, path, 1, siblings, tallies, numericKeysChangeScope);

                foreach (var field in obj.Children!.Where(c => c.Key is not null && c.IsBlock))
                {
                    VisitSavedScopes(db, field.Children!, $"{path}/{field.Key}", savedTallies, savedByName, numericKeysChangeScope);
                }
            }
        }

        var rules = new ScopeRules
        {
            LearnedUtc = nowUtc,
            Fingerprint = ComputeFingerprint(files),
            DocsImportId = docsImportId,
            FilesScanned = files.Count,
            DefaultEventType = defaultEventType,
            EventBlockScopes = eventBlockScopes is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(eventBlockScopes, StringComparer.Ordinal),
            NumericKeysChangeScope = numericKeysChangeScope,
            ScriptValues = ScriptValueNames.FromFiles(files.Where(f => f.Folder == ScriptValueNames.Folder).Select(f => f.Path)).ToList(),
        };

        foreach (var (path, tally) in tallies)
        {
            if (tally.Total < MinStatements)
            {
                continue;
            }

            var best = tally.Votes.Values.Max();
            var tops = tally.Votes.Where(v => v.Value == best).Select(v => v.Key).ToList();
            var support = (double)best / tally.Total;
            rules.Fields[path] = new FieldRule
            {
                Scope = tops.Count == 1 && support >= ScopeRules.ConfidentSupport ? tops[0] : null,
                Support = Math.Round(support, 4),
                Total = tally.Total,
            };

            if (support < 0.9995 && FindDiscriminator(tally, support) is { } discriminator)
            {
                rules.Discriminators[path] = discriminator;
            }
        }

        foreach (var ((field, name), tally) in savedTallies)
        {
            if (ConfidentTop(tally) is not { } scope)
            {
                continue;
            }

            if (!rules.SavedFields.TryGetValue(field, out var names))
            {
                rules.SavedFields[field] = names = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            names[name] = scope;
        }

        foreach (var (name, tally) in savedByName)
        {
            if (ConfidentTop(tally) is { } scope)
            {
                rules.SavedNames[name] = scope;
            }
        }

        return new LearnResult(rules, DateTime.UtcNow - started);
    }

    /// <summary>Der Scope, den mindestens 90 % der (mindestens 5) Statements zulassen und der eindeutig ist; sonst <see langword="null"/>.</summary>
    private static string? ConfidentTop(Tally tally)
    {
        if (tally.Total < MinStatements || tally.Votes.Count == 0)
        {
            return null;
        }

        var best = tally.Votes.Values.Max();
        var tops = tally.Votes.Where(v => v.Value == best).Select(v => v.Key).ToList();
        return tops.Count == 1 && (double)best / tally.Total >= ScopeRules.ConfidentSupport ? tops[0] : null;
    }

    /// <summary>
    /// Sucht in einem Feld alle Blöcke <c>scope:name = { … }</c> und wertet aus, in welchem Scope die Anweisungen darin gelten: das ist der Typ des
    /// gespeicherten Scopes. Gezählt wird je Feld und Name sowie je Name allein.
    /// </summary>
    private static void VisitSavedScopes(
        ScopeDb db,
        List<ScriptNode> nodes,
        string field,
        Dictionary<(string Field, string Name), Tally> byField,
        Dictionary<string, Tally> byName,
        bool numeric)
    {
        foreach (var node in nodes)
        {
            if (!node.IsBlock)
            {
                continue;
            }

            if (node.Key is { } key && SavedScopeName(key) is { } name)
            {
                if (!byField.TryGetValue((field, name), out var fieldTally))
                {
                    byField[(field, name)] = fieldTally = new Tally();
                }

                if (!byName.TryGetValue(name, out var nameTally))
                {
                    byName[name] = nameTally = new Tally();
                }

                foreach (var direct in DirectKeys(node.Children!, db, numeric, node.Key))
                {
                    if (db.VoteScopes(direct) is { } scopes)
                    {
                        fieldTally.Add(scopes);
                        nameTally.Add(scopes);
                    }
                }
            }

            VisitSavedScopes(db, node.Children!, field, byField, byName, numeric);
        }
    }

    /// <summary>Der Name in <c>scope:name</c>, wenn der Schlüssel genau dieser einfachen Form entspricht (ohne Kette wie <c>scope:name.owner</c>).</summary>
    internal static string? SavedScopeName(string key)
    {
        if (!key.StartsWith("scope:", StringComparison.Ordinal) || key.Length == "scope:".Length)
        {
            return null;
        }

        var name = key["scope:".Length..];
        return name.All(c => char.IsLetterOrDigit(c) || c == '_') ? name : null;
    }

    private sealed class Tally
    {
        public int Total;
        public readonly Dictionary<string, int> Votes = new();

        public void Add(IReadOnlySet<string> scopes)
        {
            Total++;
            foreach (var scope in scopes)
            {
                Votes[scope] = Votes.GetValueOrDefault(scope) + 1;
            }
        }
    }

    private sealed class SiblingTally
    {
        public int Count;
        public readonly Dictionary<string, int> Votes = new();
    }

    private sealed class PathTally
    {
        public int Total;
        public readonly Dictionary<string, int> Votes = new();
        public readonly Dictionary<string, Dictionary<string, SiblingTally>> Siblings = new();
    }

    /// <summary>Direkt im Block stehende Schlüssel; transparente Blöcke (limit, OR, if …) werden aufgelöst, Scope-Wechsler zählen nicht.</summary>
    private static IEnumerable<string> DirectKeys(IEnumerable<ScriptNode> nodes, ScopeDb db, bool numeric, string? parentKey)
    {
        foreach (var node in nodes)
        {
            if (node.Key is null)
            {
                continue;
            }

            var kind = KeyClassifier.Classify(node.Key, db, KeyClassifier.NumericChangesScope(numeric, parentKey));
            if (kind == KeyKind.Transparent && node.IsBlock)
            {
                foreach (var key in DirectKeys(node.Children!, db, numeric, node.Key))
                {
                    yield return key;
                }
            }
            else if (!KeyClassifier.ChangesScope(kind))
            {
                yield return node.Key;
            }
        }
    }

    private static void WalkField(
        ScopeDb db,
        List<ScriptNode> nodes,
        string path,
        int depth,
        IReadOnlyDictionary<string, string> siblings,
        Dictionary<string, PathTally> tallies,
        bool numeric)
    {
        foreach (var key in DirectKeys(nodes, db, numeric, null))
        {
            var scopes = db.VoteScopes(key);
            if (scopes is null)
            {
                continue;
            }

            if (!tallies.TryGetValue(path, out var tally))
            {
                tallies[path] = tally = new PathTally();
            }

            tally.Total++;
            foreach (var scope in scopes)
            {
                tally.Votes[scope] = tally.Votes.GetValueOrDefault(scope) + 1;
            }

            foreach (var (siblingKey, siblingValue) in siblings)
            {
                if (!tally.Siblings.TryGetValue(siblingKey, out var values))
                {
                    tally.Siblings[siblingKey] = values = new Dictionary<string, SiblingTally>();
                }

                if (!values.TryGetValue(siblingValue, out var sibling))
                {
                    values[siblingValue] = sibling = new SiblingTally();
                }

                sibling.Count++;
                foreach (var scope in scopes)
                {
                    sibling.Votes[scope] = sibling.Votes.GetValueOrDefault(scope) + 1;
                }
            }
        }

        if (depth >= MaxFieldDepth)
        {
            return;
        }

        foreach (var node in nodes)
        {
            if (node.Key is null || !node.IsBlock || KeyClassifier.Classify(node.Key, db) != KeyKind.Structural)
            {
                continue;
            }

            WalkField(db, node.Children!, path + "/" + node.Key, depth + 1, siblings, tallies, numeric);
        }
    }

    /// <summary>
    /// Sucht ein Geschwisterfeld, dessen Wert den Scope des Feldes bestimmt. Es muss fast alle Statements abdecken, mindestens zwei Werte haben, die
    /// mehrfach vorkommen und je zu einem Scope führen, und die Zustimmung deutlich über die des Feldes ohne Unterscheider heben.
    /// </summary>
    private static DiscriminatorRule? FindDiscriminator(PathTally tally, double baseSupport)
    {
        DiscriminatorRule? best = null;
        var bestOverall = 0.0;

        foreach (var (siblingKey, values) in tally.Siblings)
        {
            var covered = values.Values.Sum(v => v.Count);
            if (covered < 0.9 * tally.Total || values.Count < 2)
            {
                continue;
            }

            var map = new Dictionary<string, string>();
            var correct = 0;
            var mapped = 0;
            foreach (var (value, sibling) in values)
            {
                var top = sibling.Votes.OrderByDescending(v => v.Value).ThenBy(v => v.Key, StringComparer.Ordinal).First();
                correct += top.Value;
                if (sibling.Count >= 2 && (double)top.Value / sibling.Count >= ScopeRules.ConfidentSupport)
                {
                    map[value] = top.Key;
                    mapped += sibling.Count;
                }
            }

            var overall = (double)correct / covered;
            if (map.Count >= 2 && mapped >= 0.8 * tally.Total && overall >= DiscriminatorThreshold && overall > baseSupport + MinDiscriminatorGain && overall > bestOverall)
            {
                best = new DiscriminatorRule { Key = siblingKey, Map = map };
                bestOverall = overall;
            }
        }

        return best;
    }
}
