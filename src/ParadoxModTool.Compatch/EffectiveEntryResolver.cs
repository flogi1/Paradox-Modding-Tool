using System.Security.Cryptography;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.ModCompat;
using ParadoxModTool.Mods;
using Files = System.Collections.Generic.Dictionary<string, ParadoxModTool.Compatch.FileParts>;

namespace ParadoxModTool.Compatch;

/// <summary>Liest A, B und die betroffenen Vanilla-Bereiche und berechnet je Adresse die wirksamen Fassungen (Rulings 5 und 6 im Plan).</summary>
public static class EffectiveEntryResolver
{
    private const int ProgressStep = 20;

    private sealed record Placed(FileParts File, int Rank);

    public static ResolvedEntries Resolve(CompatchInput input, IProgress<CompatProgress>? progress = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = new ResolvedEntries { BLoadsAfterA = input.BLoadsAfterA };
        var partsA = ReadMod(MergeSide.A, input.ModAFolder, input, result, progress, ct);
        var partsB = ReadMod(MergeSide.B, input.ModBFolder, input, result, progress, ct);

        var samePaths = partsA.Keys.Where(partsB.ContainsKey).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var keysA = partsA.Values.SelectMany(f => f.Keys).Select(k => k.Address).ToHashSet();
        var keysB = partsB.Values.SelectMany(f => f.Keys).Select(k => k.Address).ToHashSet();
        var relevant = keysB.Where(keysA.Contains).ToHashSet();

        // Ersetzt eine Mod allein eine Vanilla-Datei, fehlen darin vielleicht Schlüssel, die die andere Mod verändert (entfernt vs. geändert).
        void AddKeysOfReplacedVanillaFiles(Files replacing, Files other, HashSet<EntryAddress> otherKeys)
        {
            foreach (var (path, file) in replacing)
            {
                var full = Path.Combine(input.VanillaFolder, path.Replace('/', Path.DirectorySeparatorChar));
                if (other.ContainsKey(path) || !file.IsScript || file.Error is not null || !File.Exists(full) || ReplacePaths.Hides(input.HiddenVanillaFolders, path, input.ReplacePathRoots))
                {
                    continue;
                }

                var vanilla = KeyPartReader.Read(MergeSide.Vanilla, path, full, input.Rules.Match(path), input.KeyOperators, input.Cache);
                relevant.UnionWith(vanilla.Keys.Select(k => k.Address).Where(otherKeys.Contains));
            }
        }

        AddKeysOfReplacedVanillaFiles(partsA, partsB, keysB);
        AddKeysOfReplacedVanillaFiles(partsB, partsA, keysA);
        var notes = new Dictionary<EntryAddress, string>();
        foreach (var path in samePaths)
        {
            var fa = partsA[path];
            var fb = partsB[path];
            if (!fa.IsScript || !fb.IsScript || fa.Error is not null || fb.Error is not null)
            {
                var address = EntryAddress.ForWholeFile(path);
                relevant.Add(address);
                notes[address] = fa.Error is not null || fb.Error is not null ? CompatchTexts.NoteUnreadable : CompatchTexts.NoteNotScript;
                continue;
            }

            relevant.UnionWith(fa.Keys.Concat(fb.Keys).Select(k => k.Address));
            relevant.Add(EntryAddress.ForFileRest(path));
        }

        var partsV = ReadVanilla(input, relevant, samePaths, result, progress, ct);
        foreach (var path in samePaths)
        {
            if (partsV.TryGetValue(path, out var vanillaFile) && relevant.Contains(EntryAddress.ForFileRest(path)))
            {
                relevant.UnionWith(vanillaFile.Keys.Select(k => k.Address));
            }
        }

        progress?.Report(new CompatProgress(CompatchTexts.ProgressCompare, 0, 0));
        var rankA = input.BLoadsAfterA ? 1 : 2;
        var rankB = input.BLoadsAfterA ? 2 : 1;
        var viewV = View(partsV, null, 0, null, 0);
        var viewA = View(partsV, partsA, rankA, null, 0);
        var viewB = View(partsV, null, 0, partsB, rankB);
        var viewN = View(partsV, partsA, rankA, partsB, rankB);
        foreach (var placed in viewN.Values)
        {
            result.NoPatchFiles[placed.File.RelativePath] = (placed.File.Side, placed.File.FullPath);
        }

        var rules = new Dictionary<EntryAddress, RuleMatch>();
        var indexV = KeyIndex(viewV, relevant, rules);
        var indexA = KeyIndex(viewA, relevant, rules);
        var indexB = KeyIndex(viewB, relevant, rules);
        var indexN = KeyIndex(viewN, relevant, rules);

        foreach (var address in relevant.OrderBy(a => a.Id, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if (address.Kind == AddressKind.Key)
            {
                var rule = rules[address];
                result.Addresses[address] = new AddressInfo
                {
                    Address = address,
                    Behavior = rule.Behavior,
                    Certain = rule.Certain,
                    Operators = rule.Operators,
                    Vanilla = KeyEffective(address, indexV, rule, input.Rules.InjectAppends, input.Rules.FirstWinsInLoadOrder, input.Rules.LastWinsInLoadOrder, input.Rules.LoadOrder),
                    A = KeyEffective(address, indexA, rule, input.Rules.InjectAppends, input.Rules.FirstWinsInLoadOrder, input.Rules.LastWinsInLoadOrder, input.Rules.LoadOrder),
                    B = KeyEffective(address, indexB, rule, input.Rules.InjectAppends, input.Rules.FirstWinsInLoadOrder, input.Rules.LastWinsInLoadOrder, input.Rules.LoadOrder),
                    NoPatch = KeyEffective(address, indexN, rule, input.Rules.InjectAppends, input.Rules.FirstWinsInLoadOrder, input.Rules.LastWinsInLoadOrder, input.Rules.LoadOrder),
                };
                continue;
            }

            var fileRule = input.Rules.Match(address.Scope);
            result.Addresses[address] = new AddressInfo
            {
                Address = address,
                Behavior = fileRule.Behavior,
                Certain = fileRule.Certain,
                Operators = fileRule.Operators,
                Note = notes.GetValueOrDefault(address),
                Vanilla = FileEffective(address, viewV),
                A = FileEffective(address, viewA),
                B = FileEffective(address, viewB),
                NoPatch = FileEffective(address, viewN),
            };
        }

        return result;
    }

    private static Files ReadMod(MergeSide side, string folder, CompatchInput input, ResolvedEntries result, IProgress<CompatProgress>? progress, CancellationToken ct)
    {
        var text = CompatchTexts.ProgressReadMod(side.ToString());
        progress?.Report(new CompatProgress(text, 0, 0));
        ModFileIndex index;
        try
        {
            index = ModFileIndex.Build(folder, null, ct);
        }
        catch (DirectoryNotFoundException)
        {
            throw new CompatchInputException(CompatchTexts.ModFolderMissing(side.ToString(), folder));
        }

        var files = new Files(StringComparer.OrdinalIgnoreCase);
        var paths = index.Entries.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        progress?.Report(new CompatProgress(text, 0, paths.Count));
        var done = 0;
        foreach (var relative in paths)
        {
            ct.ThrowIfCancellationRequested();
            done++;
            if (done % ProgressStep == 0 || done == paths.Count)
            {
                progress?.Report(new CompatProgress(text, done, paths.Count));
            }

            // Mit replace_path der später ladenden anderen Mod ausgeblendet: Das Spiel lädt die Datei nicht.
            if (ReplacePaths.Hides(side == MergeSide.A ? input.HiddenFoldersOfA : input.HiddenFoldersOfB, relative, input.ReplacePathRoots))
            {
                continue;
            }

            if (input.ExcludeOwnPatchFilesOf == side && OwnPatchFiles.IsOwn(relative))
            {
                result.Skipped.Add(new SkippedFile(relative, SkipReason.OwnPatch, "PMT"));
                continue;
            }

            if (input.Filter.Check(relative) is { } skipped)
            {
                result.Skipped.Add(skipped);
                continue;
            }

            var parts = KeyPartReader.Read(side, relative, index.FullPath(relative), input.Rules.Match(relative), input.KeyOperators, input.Cache);
            if (parts.Error is not null)
            {
                result.Unreadable.Add(new CompatchUnreadable(side, relative, parts.Error));
            }

            files[relative] = parts;
        }

        return files;
    }

    private static Files ReadVanilla(CompatchInput input, HashSet<EntryAddress> relevant, IReadOnlyList<string> samePaths, ResolvedEntries result, IProgress<CompatProgress>? progress, CancellationToken ct)
    {
        var files = new Files(StringComparer.OrdinalIgnoreCase);
        var keyAddresses = relevant.Where(a => a.Kind == AddressKind.Key).ToList();
        var languages = keyAddresses.Where(a => a.Key.Contains('/')).Select(a => a.Key[..a.Key.IndexOf('/')].ToLowerInvariant()).ToHashSet();

        void Add(string relative, string full, RuleMatch rule)
        {
            // Mit replace_path ausgeblendete Vanilla-Dateien lädt das Spiel nicht.
            if (ReplacePaths.Hides(input.HiddenVanillaFolders, relative, input.ReplacePathRoots))
            {
                return;
            }

            var parts = KeyPartReader.Read(MergeSide.Vanilla, relative, full, rule, input.KeyOperators, input.Cache);
            if (parts.Error is not null)
            {
                result.Unreadable.Add(new CompatchUnreadable(MergeSide.Vanilla, relative, parts.Error));
            }

            files[relative] = parts;
        }

        var scopes = keyAddresses.Select(a => a.Scope).Distinct().ToList();
        var vanillaText = CompatchTexts.ProgressReadVanilla;
        progress?.Report(new CompatProgress(vanillaText, 0, scopes.Count));
        var scopesDone = 0;
        foreach (var scope in scopes)
        {
            progress?.Report(new CompatProgress(vanillaText, scopesDone++, scopes.Count));
            var dir = Path.Combine(input.VanillaFolder, scope.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir))
            {
                continue;
            }

            foreach (var full in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(input.VanillaFolder, full).Replace('\\', '/');
                var lower = relative.ToLowerInvariant();
                var rule = input.Rules.Match(relative);
                if (input.Filter.IsHiddenKind(relative) || !string.Equals(rule.Scope, scope, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rule.Behavior == OverrideBehavior.LocalizationKeys && !languages.Any(l => lower.Contains("/" + l + "/") || lower.EndsWith("_l_" + l + ".yml")))
                {
                    continue;
                }

                Add(relative, full, rule);
            }
        }

        foreach (var path in samePaths)
        {
            var full = Path.Combine(input.VanillaFolder, path.Replace('/', Path.DirectorySeparatorChar));
            if (!files.ContainsKey(path) && File.Exists(full))
            {
                Add(path, full, input.Rules.Match(path));
            }
        }

        return files;
    }

    /// <summary>Welche Datei je Pfad in dieser Sicht geladen wird: höherer Rang ersetzt gleichen Pfad.</summary>
    private static Dictionary<string, Placed> View(Files vanilla, Files? a, int rankA, Files? b, int rankB)
    {
        var winners = new Dictionary<string, Placed>(StringComparer.OrdinalIgnoreCase);
        void Add(Files? files, int rank)
        {
            if (files is null)
            {
                return;
            }

            foreach (var (path, file) in files)
            {
                if (!winners.TryGetValue(path, out var current) || current.Rank <= rank)
                {
                    winners[path] = new Placed(file, rank);
                }
            }
        }

        Add(vanilla, 0);
        foreach (var (files, rank) in new[] { (a, rankA), (b, rankB) }.OrderBy(x => x.Item2))
        {
            Add(files, rank);
        }

        return winners;
    }

    private static Dictionary<EntryAddress, List<(KeyPart Part, int Rank)>> KeyIndex(Dictionary<string, Placed> view, HashSet<EntryAddress> relevant, Dictionary<EntryAddress, RuleMatch> rules)
    {
        var index = new Dictionary<EntryAddress, List<(KeyPart Part, int Rank)>>();
        foreach (var placed in view.Values)
        {
            foreach (var part in placed.File.Keys.Where(k => relevant.Contains(k.Address)))
            {
                if (!index.TryGetValue(part.Address, out var list))
                {
                    list = new List<(KeyPart Part, int Rank)>();
                    index[part.Address] = list;
                }

                list.Add((part, placed.Rank));
                rules.TryAdd(part.Address, placed.File.Rule);
            }
        }

        return index;
    }

    private static Effective KeyEffective(EntryAddress address, Dictionary<EntryAddress, List<(KeyPart Part, int Rank)>> index, RuleMatch rule, bool injectAppends, bool firstWinsInLoadOrder,
        bool lastWinsInLoadOrder = false, GameLoadOrder? loadOrder = null)
    {
        if (!index.TryGetValue(address, out var list))
        {
            return Effective.Absent;
        }

        // Victoria 3: Bei "zuerst geladen gewinnt" zählt die Ladereihenfolge der Pfade über Vanilla und Mods, nicht der Playset-Rang (gemessen
        // 2026-10-11); gleicher Pfad = die später geladene Mod ersetzt die Datei. Stellaris (Wiki): bei "zuletzt geladen gewinnt" und einzelnen Defines ebenso, die zuletzt
        // geladene Datei gilt. GUI-Typen/Templates (LastWins, EU V): laut Wiki zählt nur die Mod-Ladereihenfolge, Dateinamen nicht. Sonst zuerst der Dateiname.
        var ordered = (rule.Behavior == OverrideBehavior.FirstWins && firstWinsInLoadOrder
                ? list.OrderBy(x => x.Part.RelativePath, loadOrder ?? GameLoadOrder.Instance).ThenByDescending(x => x.Rank)
                : (lastWinsInLoadOrder && (rule.Behavior is OverrideBehavior.LastWins or OverrideBehavior.PartialKeys))
                ? list.OrderBy(x => x.Part.RelativePath, loadOrder ?? GameLoadOrder.Instance).ThenBy(x => x.Rank)
                : rule.Behavior == OverrideBehavior.LastWins
                ? list.OrderBy(x => x.Rank).ThenBy(x => Path.GetFileName(x.Part.RelativePath), StringComparer.Ordinal)
                : list.OrderBy(x => Path.GetFileName(x.Part.RelativePath), StringComparer.Ordinal).ThenBy(x => x.Rank))
            .Select(x => x.Part)
            .ToList();
        return EffectiveCombiner.Combine(ordered, rule.Behavior, rule.Certain, injectAppends);
    }

    private static Effective FileEffective(EntryAddress address, Dictionary<string, Placed> view)
    {
        if (!view.TryGetValue(address.Scope, out var placed))
        {
            return Effective.Absent;
        }

        var file = placed.File;
        if (address.Kind == AddressKind.FileRest)
        {
            return new Effective { Entries = file.Rest, RelativePath = file.RelativePath, FullPath = file.FullPath, Side = file.Side };
        }

        using var stream = File.OpenRead(file.FullPath);
        return new Effective
        {
            FileHash = Convert.ToHexString(SHA256.HashData(stream)),
            RelativePath = file.RelativePath,
            FullPath = file.FullPath,
            Side = file.Side,
        };
    }
}
