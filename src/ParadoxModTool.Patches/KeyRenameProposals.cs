using System.Text.RegularExpressions;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Patches;

public enum RenameStatus
{
    Ok,
    NoProposal,
    Ambiguous,
    TargetDefined,
    TargetInVanilla,
    TargetTwice,
    Invalid,
}

/// <summary>Vorschlag für einen alten Mod-Schlüssel: neuer Schlüssel (oder null), Status und Erläuterung (Fundstelle, Kandidaten).</summary>
public sealed record RenameProposal(string OldKey, string File, string? NewKey, RenameStatus Status, string? Detail);

/// <summary>
/// "Bestehende Mod-Gebäude übernehmen" (Spec 2026-09-30-patch-recipes, Etappe 5): sucht die Schlüssel, die nur die Mod in einem Ordner definiert,
/// schlägt aus der Lokalisierung der Mod den Vanilla-Schlüssel vor (<c>building_type_&lt;alt&gt;: "$building_type_&lt;vanilla&gt;$"</c>) und prüft,
/// ob ein neuer Schlüssel frei ist.
/// </summary>
public sealed class KeyRenameProposals
{
    private static readonly Regex ValidKey = new("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, (string File, int Line)> _modDefinitions;
    private readonly Dictionary<string, string> _vanillaDefinitions;
    private readonly PatchWorkspace _workspace;

    private KeyRenameProposals(PatchWorkspace workspace, Dictionary<string, (string File, int Line)> modDefinitions, Dictionary<string, string> vanillaDefinitions)
    {
        _workspace = workspace;
        _modDefinitions = modDefinitions;
        _vanillaDefinitions = vanillaDefinitions;
    }

    public IReadOnlyList<RenameProposal> Proposals { get; private set; } = Array.Empty<RenameProposal>();

    /// <summary>Anzahl alter Stufen, die nur noch in der Lokalisierung vorkommen (gleiche Schlüssel-Familie, andere Endnummer, nirgends definiert).</summary>
    public int OrphanLocKeys { get; private set; }

    /// <summary>Alle Schlüssel der obersten Ebene im Ordner, in Mod und Vanilla (für <see cref="KeyRenamer.RenameTokens"/>).</summary>
    public IReadOnlySet<string> DefinedKeys => _modDefinitions.Keys.Concat(_vanillaDefinitions.Keys).ToHashSet(StringComparer.Ordinal);

    public static KeyRenameProposals Build(PatchWorkspace workspace, string folder)
    {
        var glob = folder.TrimEnd('/') + "/*.txt";
        var mod = new Dictionary<string, (string File, int Line)>(StringComparer.Ordinal);
        var oldKeys = new List<(string Key, string File)>();
        var vanilla = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rel in workspace.VanillaFiles(glob))
        {
            foreach (var (key, _) in TopLevel(workspace.ReadVanilla(rel)))
            {
                vanilla.TryAdd(key, rel);
            }
        }

        foreach (var rel in workspace.ModFiles(glob))
        {
            foreach (var (key, line) in TopLevel(workspace.Get(rel)))
            {
                if (mod.TryAdd(key, (rel, line)) && !vanilla.ContainsKey(key))
                {
                    oldKeys.Add((key, rel));
                }
            }
        }

        var result = new KeyRenameProposals(workspace, mod, vanilla);
        var locLines = workspace.ModFiles("localization/**/*.yml").SelectMany(rel => LocalizationLines.All(workspace.Get(rel) ?? string.Empty)).ToList();
        var candidates = oldKeys.Select(o => (o.Key, o.File, Found: Candidates(o.Key, locLines, vanilla))).ToList();
        var accepted = candidates.Where(c => c.Found.Count == 1).Select(c => new KeyRename(c.Key, c.Found[0])).ToList();
        result.Proposals = candidates.Select(c =>
        {
            if (c.Found.Count == 0)
            {
                return new RenameProposal(c.Key, c.File, null, RenameStatus.NoProposal, null);
            }

            if (c.Found.Count > 1)
            {
                return new RenameProposal(c.Key, c.File, null, RenameStatus.Ambiguous, string.Join(", ", c.Found));
            }

            var status = result.Check(c.Key, c.Found[0], accepted, out var detail);
            return new RenameProposal(c.Key, c.File, c.Found[0], status, detail);
        }).ToList();
        // Nur Familien mit Vorschlag: Reste anderer eigener Gebäude betrifft die Umbenennung nicht.
        result.OrphanLocKeys = CountOrphans(candidates.Where(c => c.Found.Count > 0).Select(c => c.Key), locLines, key => mod.ContainsKey(key) || vanilla.ContainsKey(key));
        return result;
    }

    /// <summary>Prüft einen neuen Schlüssel: gültige Zeichen, nicht schon in der Mod oder in einer nicht überdeckten Vanilla-Datei definiert, nicht doppelt vergeben.</summary>
    public RenameStatus Check(string oldKey, string? newKey, IEnumerable<KeyRename> all, out string? detail)
    {
        detail = null;
        if (string.IsNullOrEmpty(newKey) || !ValidKey.IsMatch(newKey))
        {
            return RenameStatus.Invalid;
        }

        if (_modDefinitions.TryGetValue(newKey, out var hit) && newKey != oldKey)
        {
            detail = hit.File + ":" + hit.Line;
            return RenameStatus.TargetDefined;
        }

        if (_vanillaDefinitions.TryGetValue(newKey, out var vanillaFile) && _workspace.Get(vanillaFile) is null)
        {
            detail = vanillaFile;
            return RenameStatus.TargetInVanilla;
        }

        if (all.Any(r => r.NewKey == newKey && r.OldKey != oldKey))
        {
            return RenameStatus.TargetTwice;
        }

        return RenameStatus.Ok;
    }

    /// <summary>
    /// Vanilla-Kandidaten aus Zeilen <c>&lt;präfix&gt;_&lt;alt&gt;</c>: jeder Verweis wird von links an <c>_</c> gekürzt, der erste (längste) Rest,
    /// den Vanilla definiert, zählt (bei <c>special_&lt;V&gt;</c> auch <c>V</c>).
    /// </summary>
    private static List<string> Candidates(string oldKey, List<LocalizationLine> lines, Dictionary<string, string> vanilla)
    {
        var found = new List<string>();
        foreach (var line in lines.Where(l => l.Key.Length > oldKey.Length + 1 && l.Key.EndsWith("_" + oldKey, StringComparison.Ordinal)))
        {
            foreach (var reference in LocalizationLines.References(line.Value))
            {
                if (Candidate(reference, vanilla) is { } candidate && !found.Contains(candidate))
                {
                    found.Add(candidate);
                }
            }
        }

        return found;
    }

    private static string? Candidate(string reference, Dictionary<string, string> vanilla)
    {
        for (var cut = reference.IndexOf('_'); cut >= 0 && cut + 1 < reference.Length; cut = reference.IndexOf('_', cut + 1))
        {
            var rest = reference[(cut + 1)..];
            if (vanilla.ContainsKey(rest))
            {
                return rest;
            }

            if (rest.StartsWith("special_", StringComparison.Ordinal) && vanilla.ContainsKey(rest["special_".Length..]))
            {
                return rest["special_".Length..];
            }
        }

        return null;
    }

    /// <summary>Gezählt werden Namen X aus Schlüsseln <c>&lt;präfix&gt;_X</c> oder <c>&lt;präfix&gt;_X_desc</c>, die wie ein alter Schlüssel mit anderer Endnummer aussehen.</summary>
    private static int CountOrphans(IEnumerable<string> oldKeys, List<LocalizationLine> lines, Func<string, bool> isDefined)
    {
        var stems = oldKeys.Select(k => Regex.Match(k, @"^(?<stem>.*?)\d+$", RegexOptions.CultureInvariant))
            .Where(m => m.Success && m.Groups["stem"].Value.Length > 0)
            .Select(m => Regex.Escape(m.Groups["stem"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (stems.Count == 0)
        {
            return 0;
        }

        var family = new Regex("^(?:" + string.Join("|", stems) + @")\d+$", RegexOptions.CultureInvariant);
        var orphans = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in lines.Select(l => l.Key).Distinct(StringComparer.Ordinal))
        {
            var bare = key.EndsWith("_desc", StringComparison.Ordinal) ? key[..^"_desc".Length] : key;
            foreach (var name in new[] { key, bare }.Distinct(StringComparer.Ordinal))
            {
                for (var cut = name.IndexOf('_'); cut >= 0 && cut + 1 < name.Length; cut = name.IndexOf('_', cut + 1))
                {
                    var rest = name[(cut + 1)..];
                    if (family.IsMatch(rest) && !isDefined(rest))
                    {
                        orphans.Add(rest);
                    }
                }
            }
        }

        return orphans.Count;
    }

    /// <summary>Schlüssel der obersten Ebene mit Zeilennummer (1-basiert); kaputte Dateien liefern nichts.</summary>
    private static IEnumerable<(string Key, int Line)> TopLevel(string? text)
    {
        if (text is null)
        {
            yield break;
        }

        ScriptBlock root;
        try
        {
            root = ScriptDocument.Parse(text);
        }
        catch (ScriptParseException)
        {
            yield break;
        }

        var line = 1;
        var position = 0;
        foreach (var statement in root.Entries.OfType<ScriptStatement>())
        {
            if (statement.SourceStart is not { } start)
            {
                continue;
            }

            line += text.AsSpan(position, start - position).Count('\n');
            position = start;
            yield return (statement.Key, line);
        }
    }
}
