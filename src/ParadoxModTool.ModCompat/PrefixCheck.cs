using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Wie ein Präfix in seinem Ordner wirkt, wenn es nicht so wirkt, wie der Modder es wohl meint.</summary>
public enum PrefixIssueKind
{
    /// <summary>Der Ordner kennt das Präfix nicht; das Spiel liest es als Teil des Schlüssels.</summary>
    NoEffect,

    /// <summary>INJECT in einem Ordner, der nur REPLACE kennt: ersetzt den ganzen Eintrag (Victoria 3, Wiki).</summary>
    ActsAsReplace,
}

/// <summary>Ein Präfix mit Fundstelle: <see cref="Start"/> und <see cref="Length"/> umfassen nur das Präfix samt Doppelpunkt, <see cref="Line"/> zählt ab 1.</summary>
public sealed record PrefixIssue(int Start, int Length, int Line, PrefixIssueKind Kind, string Key, string Message);

/// <summary>
/// Prüft Präfixe (<c>INJECT:</c>, <c>REPLACE:</c> und Varianten) gegen die Präfixe des Ordners aus der Ordnerregel (<see cref="RuleMatch.Operators"/>).
/// Geprüft wird nur, wo das Profil eigene Präfixe für den Ordner nennt (Victoria 3); Spiele ohne solche Regeln bleiben unberührt.
/// Gemeinsame Grundlage für die Wellenlinie im Editor und die Befunde der Mod-Kompatibilität.
/// </summary>
public static class PrefixCheck
{
    /// <summary>Die Art des Problems für einen Schlüssel der obersten Ebene; <see langword="null"/>, wenn das Präfix passt oder es keins gibt.</summary>
    public static PrefixIssueKind? Classify(string rawKey, RuleMatch rule)
    {
        if (rule.Operators is not { } folderOperators)
        {
            return null;
        }

        var parsed = OverrideKey.Parse(rawKey);
        if (parsed.Operator == OverrideOperator.None)
        {
            return null;
        }

        var name = PrefixOf(rawKey).TrimEnd(':');
        if (folderOperators.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return parsed.IsInject && folderOperators.Contains("REPLACE", StringComparer.OrdinalIgnoreCase)
            ? PrefixIssueKind.ActsAsReplace
            : PrefixIssueKind.NoEffect;
    }

    /// <summary>Die Probleme der Schlüssel der obersten Ebene eines Dateitextes; <paramref name="relativePath"/> ist relativ zum Wurzelordner der Mod.</summary>
    public static IReadOnlyList<PrefixIssue> Find(string text, string relativePath, OverrideRules rules)
    {
        var rule = rules.Match(relativePath);
        if (rule.Operators is null)
        {
            return Array.Empty<PrefixIssue>();
        }

        ScriptBlock root;
        try
        {
            root = ScriptDocument.Parse(text);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Array.Empty<PrefixIssue>();
        }

        var result = new List<PrefixIssue>();
        foreach (var statement in root.Entries.OfType<ScriptStatement>())
        {
            if (Classify(statement.Key, rule) is not { } kind || statement.SourceStart is not { } start || start < 0 || start > text.Length)
            {
                continue;
            }

            result.Add(new PrefixIssue(start, PrefixOf(statement.Key).Length, LineAt(text, start), kind, statement.Key, Message(kind, statement.Key, relativePath, rule)));
        }

        return result;
    }

    /// <summary>
    /// Wie <see cref="Find"/> für eine Datei mit vollem Pfad: Der relative Pfad ergibt sich aus der ersten Wurzel (Mod- oder Spielordner), unter der die
    /// Datei liegt; unter einem Sammelordner aller Mods (<paramref name="modCollections"/>, z. B. <c>Documents/…/mod</c>) zählt der Ordner der Mod
    /// darunter als Wurzel. Liegt sie unter keiner, gibt es keine Prüfung.
    /// </summary>
    public static IReadOnlyList<PrefixIssue> FindForFile(string fullPath, string text, OverrideRules rules, IEnumerable<string> roots, IEnumerable<string>? modCollections = null)
    {
        var path = fullPath.Replace('\\', '/');
        if (After(path, roots) is { } relative)
        {
            return Find(text, relative, rules);
        }

        if (After(path, modCollections ?? Array.Empty<string>()) is { } inCollection && inCollection.IndexOf('/') is var slash and > 0)
        {
            return Find(text, inCollection[(slash + 1)..], rules);
        }

        return Array.Empty<PrefixIssue>();
    }

    private static string? After(string path, IEnumerable<string> roots)
    {
        // Die längste passende Wurzel gilt (eine Mod in einem Unterordner eines anderen Wurzelordners).
        return roots.Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Replace('\\', '/').TrimEnd('/') + "/")
            .Where(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(prefix => prefix.Length)
            .Select(prefix => path[prefix.Length..])
            .FirstOrDefault();
    }

    /// <summary>Der Befundtext: was das Präfix hier tut, und was stattdessen hilft (je nach Ordnerregel).</summary>
    public static string Message(PrefixIssueKind kind, string rawKey, string relativePath, RuleMatch rule)
    {
        var prefix = PrefixOf(rawKey);
        var slash = relativePath.Replace('\\', '/').LastIndexOf('/');
        var folder = slash > 0 ? relativePath.Replace('\\', '/')[..slash] : relativePath;
        if (kind == PrefixIssueKind.ActsAsReplace)
        {
            return ModCompatTexts.PrefixActsAsReplace(prefix, folder, OverrideKey.Parse(rawKey).Operator switch
            {
                OverrideOperator.TryInject => "TRY_REPLACE:",
                OverrideOperator.InjectOrCreate => "REPLACE_OR_CREATE:",
                _ => "REPLACE:",
            });
        }

        var tip = rule.Behavior switch
        {
            OverrideBehavior.LastWins => ModCompatTexts.PrefixTipLater,
            OverrideBehavior.FirstWins => ModCompatTexts.PrefixTipEarlier,
            _ => ModCompatTexts.PrefixTipPlain,
        };
        return ModCompatTexts.PrefixNoEffect(prefix, folder, rawKey) + " " + tip;
    }

    /// <summary>Befunde der Mod-Kompatibilität: je Präfix-Problem in den Beiträgen der Mods (Vanilla zählt nicht) ein Befund mit Fundstelle.</summary>
    internal static IEnumerable<CompatFinding> Check(CompatRequest request, ContributionIndex index)
    {
        // Spiele ohne Präfix-Regeln je Ordner (alle außer Victoria 3) haben nichts zu prüfen.
        if (!request.Rules.Rules.Any(r => r.Operators is not null))
        {
            yield break;
        }

        foreach (var contribution in index.KeyGroups.Values.SelectMany(g => g.Contributions)
                     .Where(c => !c.Participant.IsVanilla && c.Statement is not null))
        {
            var rule = request.Rules.Match(contribution.RelativePath);
            if (Classify(contribution.Statement!.Key, rule) is not { } kind)
            {
                continue;
            }

            var noEffect = kind == PrefixIssueKind.NoEffect;
            yield return new CompatFinding
            {
                Severity = noEffect ? CompatSeverity.Conflict : CompatSeverity.Hint,
                Kind = noEffect ? CompatKind.PrefixNoEffect : CompatKind.PrefixActsAsReplace,
                RelativePath = contribution.RelativePath,
                Key = contribution.Statement.Key,
                Contributions = new[] { contribution },
                Winner = contribution.Participant,
                Message = ModCompatTexts.FindingPrefix(contribution.Participant.Name, Message(kind, contribution.Statement.Key, contribution.RelativePath, rule)),
            };
        }
    }

    private static string PrefixOf(string rawKey)
    {
        var colon = rawKey.IndexOf(':');
        return colon < 0 ? string.Empty : rawKey[..(colon + 1)];
    }

    private static int LineAt(string text, int offset)
    {
        var line = 1;
        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
