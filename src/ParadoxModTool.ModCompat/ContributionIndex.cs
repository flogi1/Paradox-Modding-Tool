using ParadoxModTool.Mods;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Ein Beitrag einer Mod zu einem Schlüssel (oder, mit leerem <see cref="Key"/>, zu einer Datei).</summary>
public sealed record CompatContribution(
    CompatParticipant Participant,
    string RelativePath,
    string FullPath,
    OverrideOperator Operator,
    string Key,
    string? Text,
    int Line,
    ScriptStatement? Statement,
    OverrideBehavior Behavior,
    bool Certain);

public sealed record UnreadableFile(CompatParticipant Participant, string RelativePath, string Error);

/// <summary>Alle Beiträge zu einem Schlüssel innerhalb eines Bereichs (<see cref="Scope"/>), nach Rang geordnet.</summary>
public sealed class KeyGroup
{
    public required string Scope { get; init; }

    public required string Key { get; init; }

    public required OverrideBehavior Behavior { get; init; }

    public required bool Certain { get; init; }

    public List<CompatContribution> Contributions { get; } = new();
}

/// <summary>Liest die Schlüsselbeiträge einer einzelnen Datei je nach Verhalten des Ordners.</summary>
public static class ContributionExtractor
{
    private static readonly string[] ScriptExtensions = { ".txt", ".gui", ".gfx" };

    private static readonly System.Text.RegularExpressions.Regex LanguageSuffix =
        new("_l_([a-z_]+)\\.yml$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string LanguageFromFileName(string fileName)
    {
        var match = LanguageSuffix.Match(fileName);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : "?";
    }

    public static IReadOnlyList<CompatContribution> Extract(CompatParticipant p, string relativePath, string fullPath, RuleMatch rule, ParseCache cache, out string? error)
    {
        error = null;
        var result = new List<CompatContribution>();
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        if (rule.Behavior == OverrideBehavior.WholeFile)
        {
            return result;
        }

        if (rule.Behavior == OverrideBehavior.LocalizationKeys)
        {
            if (extension == ".yml")
            {
                ExtractLocalization(p, relativePath, fullPath, rule, result, ref error);
            }

            return result;
        }

        if (!ScriptExtensions.Contains(extension))
        {
            return result;
        }

        var parsed = cache.Get(fullPath);
        if (parsed.Root is null)
        {
            error = parsed.Error ?? ModCompatTexts.Unreadable;
            return result;
        }

        if (GuiUnits.Applies(rule, relativePath))
        {
            // Je GUI-Typ und Template ein Beitrag; oberste Fenster und Widgets sind keine Schlüssel (zwei eigene Fenster widersprechen sich nicht).
            foreach (var gui in GuiUnits.Read(parsed.Root).Keys)
            {
                var entries = gui.Unit.Header.Append(gui.Unit.Entry).ToList();
                var statement = new ScriptStatement
                {
                    Key = gui.Key,
                    Value = new ScriptBlock { Entries = entries },
                    SourceText = gui.Unit.Entry.SourceText,
                    SourceStart = entries.Select(e => e.SourceStart).FirstOrDefault(s => s is not null),
                    SourceEnd = gui.Unit.Entry.SourceEnd,
                };
                result.Add(Make(p, relativePath, fullPath, rule, new OverrideKey(OverrideOperator.None, gui.Key), statement, parsed.Text));
            }

            return result;
        }

        if (RuleKeys.Applies(rule))
        {
            foreach (var keyed in RuleKeys.Read(parsed.Root, rule))
            {
                result.Add(Make(p, relativePath, fullPath, rule, new OverrideKey(OverrideOperator.None, keyed.Key), keyed.Statement, parsed.Text));
            }

            return result;
        }

        foreach (var statement in parsed.Root.Entries.OfType<ScriptStatement>())
        {
            if (statement.Key.StartsWith('@') || (rule.Behavior == OverrideBehavior.FirstWins && statement.Key == "namespace"))
            {
                continue;
            }

            if (rule.Behavior == OverrideBehavior.PartialKeys)
            {
                if (statement.Value is ScriptBlock block)
                {
                    foreach (var child in block.Entries.OfType<ScriptStatement>().Where(c => c.Value is ScriptScalar))
                    {
                        var key = new OverrideKey(OverrideOperator.None, statement.Key + "." + child.Key);
                        result.Add(Make(p, relativePath, fullPath, rule, key, child, parsed.Text));
                    }
                }

                continue;
            }

            result.Add(Make(p, relativePath, fullPath, rule, OverrideKey.Parse(statement.Key), statement, parsed.Text));
        }

        return result;
    }

    private static CompatContribution Make(CompatParticipant p, string relativePath, string fullPath, RuleMatch rule, OverrideKey key, ScriptStatement statement, string fileText) =>
        new(p, relativePath, fullPath, key.Operator, key.Key, TextOf(statement, fileText), LineOf(statement, fileText), statement, rule.Behavior, rule.Certain);

    private static string? TextOf(ScriptStatement statement, string fileText)
    {
        var source = statement.SourceText ?? fileText;
        if (statement.SourceStart is { } start && statement.SourceEnd is { } end && start >= 0 && end <= source.Length && end >= start)
        {
            return source[start..end];
        }

        return null;
    }

    private static int LineOf(ScriptStatement statement, string fileText)
    {
        var source = statement.SourceText ?? fileText;
        if (statement.SourceStart is not { } start || start < 0 || start > source.Length)
        {
            return 0;
        }

        var line = 1;
        for (var i = 0; i < start; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static void ExtractLocalization(CompatParticipant p, string relativePath, string fullPath, RuleMatch rule, List<CompatContribution> result, ref string? error)
    {
        string text;
        try
        {
            text = File.ReadAllText(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return;
        }

        var segments = relativePath.Split('/');
        var at = Array.FindIndex(segments, s =>
            string.Equals(s, "localization", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "localisation", StringComparison.OrdinalIgnoreCase));
        if (at < 0 || at >= segments.Length - 1)
        {
            return;
        }

        // EU V: localization/<Sprache>/[replace/]x.yml. EU IV: localisation/[replace/]x_l_<sprache>.yml (Sprache im Dateinamen).
        var folders = segments.Skip(at + 1).Take(segments.Length - at - 2).ToList();
        var language = folders.FirstOrDefault(f => !string.Equals(f, "replace", StringComparison.OrdinalIgnoreCase)) ?? LanguageFromFileName(segments[^1]);
        var isReplace = folders.Any(s => string.Equals(s, "replace", StringComparison.OrdinalIgnoreCase));
        foreach (var (key, line, lineText) in LocalizationKeyReader.Read(text))
        {
            result.Add(new CompatContribution(p, relativePath, fullPath, isReplace ? OverrideOperator.Replace : OverrideOperator.None, language + "/" + key, lineText, line, null, rule.Behavior, rule.Certain));
        }
    }
}

/// <summary>
/// Der Beitragsindex einer Prüfung: alle Dateien der beteiligten Mods (<see cref="ModFileIndex"/>), Pfadgruppen, ersetzte Dateien und
/// die Schlüsselgruppen. Dateien, die eine höhere Mod am selben Pfad komplett ersetzt, liefern keine Schlüssel (sie werden nie geladen).
/// </summary>
public sealed class ContributionIndex
{
    private readonly Dictionary<string, ModFileIndex> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<CompatParticipant>> _pathGroups = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string, string)> _overridden = new();
    private readonly Dictionary<string, KeyGroup> _keyGroups = new(StringComparer.Ordinal);
    private readonly List<UnreadableFile> _unreadable = new();
    private readonly List<CompatParticipant> _missing = new();

    private ContributionIndex()
    {
    }

    public IReadOnlyDictionary<string, ModFileIndex> Files => _files;

    public IReadOnlyDictionary<string, List<CompatParticipant>> PathGroups => _pathGroups;

    public ISet<(string ParticipantId, string RelativePath)> Overridden => _overridden;

    public IReadOnlyDictionary<string, KeyGroup> KeyGroups => _keyGroups;

    public IReadOnlyList<UnreadableFile> Unreadable => _unreadable;

    public IReadOnlyList<CompatParticipant> MissingFolders => _missing;

    /// <summary>Baut den Index für <paramref name="mods"/> (ohne Vanilla, nach Rang geordnet). Wirft <see cref="OperationCanceledException"/> bei Abbruch.</summary>
    public static ContributionIndex Build(IReadOnlyList<CompatParticipant> mods, OverrideRules rules, ParseCache cache, IProgress<CompatProgress>? progress, CancellationToken ct, Func<CompatParticipant, string, bool>? isHidden = null)
    {
        var index = new ContributionIndex();
        var ordered = mods.OrderBy(m => m.Rank).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new CompatProgress(ModCompatTexts.ProgressReadFiles(ordered[i].Name), i, ordered.Count));
            try
            {
                index._files[ordered[i].Id] = ModFileIndex.Build(ordered[i].Folder, null, ct);
            }
            catch (DirectoryNotFoundException)
            {
                index._missing.Add(ordered[i]);
            }
        }

        var present = ordered.Where(m => index._files.ContainsKey(m.Id)).ToList();
        foreach (var mod in present)
        {
            foreach (var relative in index._files[mod.Id].Entries.Keys)
            {
                // Mit replace_path einer späteren Mod ausgeblendet oder Beschreibung der Mod (.metadata/, descriptor.mod): Das Spiel lädt die Datei nicht.
                if (isHidden?.Invoke(mod, relative) == true || ModMetadata.IsDescriptionFile(relative))
                {
                    continue;
                }

                if (!index._pathGroups.TryGetValue(relative, out var list))
                {
                    list = new List<CompatParticipant>();
                    index._pathGroups[relative] = list;
                }

                list.Add(mod);
            }
        }

        foreach (var (relative, list) in index._pathGroups)
        {
            for (var i = 0; i < list.Count - 1; i++)
            {
                index._overridden.Add((list[i].Id, relative));
            }
        }

        var done = 0;
        var total = present.Sum(m => index._files[m.Id].Entries.Count);
        foreach (var mod in present)
        {
            foreach (var relative in index._files[mod.Id].Entries.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                if (++done % 200 == 0)
                {
                    progress?.Report(new CompatProgress(ModCompatTexts.ProgressCheckFiles(mod.Name), done, total));
                }

                if (index._overridden.Contains((mod.Id, relative)) || isHidden?.Invoke(mod, relative) == true || ModMetadata.IsDescriptionFile(relative))
                {
                    continue;
                }

                var rule = rules.Match(relative);
                var contributions = ContributionExtractor.Extract(mod, relative, index._files[mod.Id].FullPath(relative), rule, cache, out var error);
                if (error is not null)
                {
                    index._unreadable.Add(new UnreadableFile(mod, relative, error));
                    continue;
                }

                foreach (var contribution in contributions)
                {
                    var id = rule.Scope.ToLowerInvariant() + "|" + contribution.Key;
                    if (!index._keyGroups.TryGetValue(id, out var group))
                    {
                        group = new KeyGroup { Scope = rule.Scope, Key = contribution.Key, Behavior = rule.Behavior, Certain = rule.Certain };
                        index._keyGroups[id] = group;
                    }

                    group.Contributions.Add(contribution);
                }
            }
        }

        return index;
    }
}
