using System.Security.Cryptography;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>Sinnhaftigkeitscheck für die Fokus-Mod: Wo überschreibt sie eine tiefere Fassung, obwohl weniger genügt?</summary>
internal static class SenseChecker
{
    public static IEnumerable<CompatFinding> Check(CompatRequest request, ContributionIndex index, CancellationToken ct)
    {
        if (request.FocusId is null)
        {
            yield break;
        }

        var focus = request.Participants.FirstOrDefault(p => p.Id == request.FocusId && !p.IsVanilla);
        if (focus is null || !index.Files.TryGetValue(focus.Id, out var files))
        {
            yield break;
        }

        var lower = request.Participants.Where(p => p.Rank < focus.Rank).OrderByDescending(p => p.Rank).ToList();
        foreach (var relative in files.Entries.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var basePart = lower.FirstOrDefault(p => File.Exists(PathOf(p, relative)));
            if (basePart is null)
            {
                continue;
            }

            foreach (var finding in CheckFile(request, focus, files.FullPath(relative), relative, basePart, PathOf(basePart, relative)))
            {
                yield return finding;
            }
        }
    }

    private static string PathOf(CompatParticipant p, string relative) => Path.Combine(p.Folder, relative.Replace('/', Path.DirectorySeparatorChar));

    private static List<CompatFinding> CheckFile(CompatRequest request, CompatParticipant focus, string modPath, string relative, CompatParticipant basePart, string basePath)
    {
        var result = new List<CompatFinding>();
        var rule = request.Rules.Match(relative);

        CompatContribution Own(string key, ScriptStatement? statement) =>
            new(focus, relative, modPath, OverrideOperator.None, key, null, 0, statement, rule.Behavior, rule.Certain);
        CompatContribution Base(string key, ScriptStatement? statement) =>
            new(basePart, relative, basePath, OverrideOperator.None, key, null, 0, statement, rule.Behavior, rule.Certain);
        CompatFinding Make(CompatKind kind, CompatSeverity severity, string key, string message, CompatSuggestion suggestion, ScriptStatement? own, ScriptStatement? baseStatement) => new()
        {
            Severity = severity,
            Kind = kind,
            RelativePath = relative,
            Key = key,
            Contributions = new[] { Own(key, own), Base(key, baseStatement) },
            Winner = focus,
            Suggestion = suggestion,
            Message = message,
            Uncertain = !rule.Certain,
        };

        if (SameBytes(modPath, basePath))
        {
            result.Add(Make(CompatKind.RedundantOverride, CompatSeverity.Hint, string.Empty, $"Die Datei {relative} in \"{focus.Name}\" ist eine unveränderte Kopie der Fassung aus \"{basePart.Name}\". Sie lässt sich löschen.", CompatSuggestion.RemoveRedundant, null, null));
            return result;
        }

        if (rule.Behavior != OverrideBehavior.PerKey || !string.Equals(Path.GetExtension(relative), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var mod = request.Cache.Get(modPath);
        var baseFile = request.Cache.Get(basePath);
        if (mod.Root is null || baseFile.Root is null)
        {
            return result;
        }

        var baseByKey = baseFile.Root.Entries.OfType<ScriptStatement>().GroupBy(s => s.Key).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var plain = mod.Root.Entries.OfType<ScriptStatement>()
            .Where(s => !s.Key.StartsWith('@') && OverrideKey.Parse(s.Key).Operator == OverrideOperator.None)
            .ToList();
        if (plain.Count == 0)
        {
            return result;
        }

        var unchanged = 0;
        var added = 0;
        var changedKeys = new List<string>();
        foreach (var statement in plain)
        {
            if (!baseByKey.TryGetValue(statement.Key, out var original))
            {
                added++;
                continue;
            }

            if (ScriptStructuralComparer.ValueEquals(original.Value, statement.Value))
            {
                unchanged++;
                continue;
            }

            changedKeys.Add(statement.Key);
            var additive = original.Value is ScriptBlock baseBlock
                && statement.Value is ScriptBlock modBlock
                && IsAdditiveOnly(baseBlock, modBlock);
            result.Add(additive
                ? Make(CompatKind.AdditiveOnly, CompatSeverity.Hint, statement.Key, $"\"{statement.Key}\" fügt gegenüber \"{basePart.Name}\" nur Teilschlüssel hinzu. Statt den ganzen Eintrag zu ersetzen, genügt INJECT:{statement.Key}.", CompatSuggestion.UseInject, statement, original)
                : Make(CompatKind.ReplaceNeeded, CompatSeverity.Info, statement.Key, $"\"{statement.Key}\" ändert oder entfernt vorhandene Werte aus \"{basePart.Name}\". Dafür ist REPLACE:{statement.Key} nötig (ohne die restliche Datei).", CompatSuggestion.UseReplace, statement, original));
        }

        if (unchanged == plain.Count)
        {
            result.Insert(0, Make(CompatKind.RedundantOverride, CompatSeverity.Hint, string.Empty, $"Alle Einträge der Datei {relative} in \"{focus.Name}\" entsprechen der Fassung aus \"{basePart.Name}\" (nur Formatierung). Sie lässt sich löschen.", CompatSuggestion.RemoveRedundant, null, null));
        }
        else if (unchanged >= 1 && changedKeys.Count + added >= 1)
        {
            var needed = changedKeys.Count + added;
            result.Insert(0, Make(CompatKind.TooCoarse, CompatSeverity.Hint, string.Empty, $"{unchanged} von {plain.Count} Einträgen der Datei {relative} sind unverändert aus \"{basePart.Name}\" kopiert. Nötig sind nur {needed}. Als REPLACE:/INJECT: bereitgestellt kollidiert die Mod seltener mit anderen.", CompatSuggestion.ProvideOnlyChanged, null, null));
        }

        return result;
    }

    private static bool IsAdditiveOnly(ScriptBlock baseBlock, ScriptBlock modBlock)
    {
        var diffs = ScriptDiffer.Diff(baseBlock, modBlock).Where(d => d.Kind != DiffKind.Unchanged).ToList();
        return diffs.Count > 0 && diffs.All(d => d.Kind == DiffKind.Added);
    }

    private static bool SameBytes(string a, string b)
    {
        try
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length)
            {
                return false;
            }

            using var left = File.OpenRead(a);
            using var right = File.OpenRead(b);
            return SHA256.HashData(left).AsSpan().SequenceEqual(SHA256.HashData(right));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
