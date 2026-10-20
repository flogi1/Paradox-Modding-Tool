using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.ModCompat;

/// <summary>Prüft die Mods eines Sets gegeneinander. Liest nur, schreibt nie.</summary>
public static class CompatAnalyzer
{
    public static CompatResult Analyze(CompatRequest request, IProgress<CompatProgress>? progress = null, CancellationToken ct = default)
    {
        var mods = ActiveMods(request);
        var index = ContributionIndex.Build(mods, request.Rules, request.Cache, progress, ct, request.IsHidden);
        var findings = new List<CompatFinding>();

        foreach (var missing in index.MissingFolders)
        {
            findings.Add(new CompatFinding
            {
                Severity = CompatSeverity.Info,
                Kind = CompatKind.MissingFolder,
                RelativePath = string.Empty,
                Message = ModCompatTexts.FindingMissingFolder(missing.Name, missing.Folder),
            });
        }

        foreach (var file in index.Unreadable)
        {
            findings.Add(new CompatFinding
            {
                Severity = CompatSeverity.Info,
                Kind = CompatKind.Unreadable,
                RelativePath = file.RelativePath,
                Message = ModCompatTexts.FindingUnreadable(file.RelativePath, file.Participant.Name, file.Error),
            });
        }

        findings.AddRange(ReplacePathChecker.Check(request, mods));
        findings.AddRange(PrefixCheck.Check(request, index));

        progress?.Report(new CompatProgress(ModCompatTexts.ProgressCompareFiles, 0, 0));
        findings.AddRange(FileFindings(request, index, ct));

        progress?.Report(new CompatProgress(ModCompatTexts.ProgressCompareKeys, 0, 0));
        findings.AddRange(KeyFindings(request, index, ct));

        progress?.Report(new CompatProgress(ModCompatTexts.ProgressSenseCheck, 0, 0));
        findings.AddRange(SenseChecker.Check(request, index, ct));

        return new CompatResult(findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Key, StringComparer.Ordinal)
            .ToList());
    }

    /// <summary>Die Mods des Laufs nach Rang: Fokus plus Gegenseite (oder alle). Vanilla gehört nicht dazu.</summary>
    internal static List<CompatParticipant> ActiveMods(CompatRequest request)
    {
        var mods = request.Participants.Where(p => !p.IsVanilla).OrderBy(p => p.Rank).ToList();
        if (request.FocusId is null)
        {
            return mods;
        }

        if (mods.All(p => p.Id != request.FocusId))
        {
            throw new ArgumentException(ModCompatTexts.FocusNotInSet, nameof(request));
        }

        return mods.Where(p => p.Id == request.FocusId || request.CounterpartIds is null || request.CounterpartIds.Contains(p.Id)).ToList();
    }

    /// <summary>Betrifft der Fund mindestens zwei Mods und, falls gesetzt, die Fokus-Mod?</summary>
    internal static bool IsRelevant(CompatRequest request, IEnumerable<CompatParticipant> group)
    {
        var ids = group.Select(p => p.Id).Distinct().ToList();
        return ids.Count >= 2 && (request.FocusId is null || ids.Contains(request.FocusId));
    }

    /// <summary>Wendet die Projektbeziehung an: <see langword="null"/> = unterdrücken, sonst der Fund (bei Submod nur Info).</summary>
    internal static CompatFinding? ApplyRelation(CompatRequest request, CompatFinding finding)
    {
        switch (RelationVerdict.Judge(finding.Contributions.Select(c => c.Participant).ToList(), request.Relation, request.IsCompatchFor, out var compatch))
        {
            case Verdict.Suppress:
                return null;
            case Verdict.Compatch:
                return finding.With(CompatSeverity.Info, ModCompatTexts.NoteCompatchPresent(compatch!.Name));
            case Verdict.InfoOnly:
                return finding.With(CompatSeverity.Info, ModCompatTexts.NoteSameProject);
            default:
                return finding;
        }
    }

    private static IEnumerable<CompatFinding> FileFindings(CompatRequest request, ContributionIndex index, CancellationToken ct)
    {
        foreach (var (relative, group) in index.PathGroups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            if (group.Count < 2 || !IsRelevant(request, group))
            {
                continue;
            }

            var rule = request.Rules.Match(relative);
            var contributions = group
                .Select(p => new CompatContribution(p, relative, index.Files[p.Id].FullPath(relative), OverrideOperator.None, string.Empty, null, 0, null, rule.Behavior, rule.Certain))
                .ToList();
            var winner = group.OrderBy(p => p.Rank).Last();
            var names = string.Join(", ", group.Select(p => "\"" + p.Name + "\""));
            var identical = AllIdentical(group, index, relative);

            var finding = identical
                ? new CompatFinding
                {
                    Severity = CompatSeverity.Info,
                    Kind = CompatKind.FileIdentical,
                    RelativePath = relative,
                    Contributions = contributions,
                    Message = ModCompatTexts.FindingFileIdentical(relative, names),
                }
                : new CompatFinding
                {
                    Severity = CompatSeverity.Conflict,
                    Kind = CompatKind.FileOverride,
                    RelativePath = relative,
                    Contributions = contributions,
                    Winner = winner,
                    Suggestion = CompatSuggestion.NeedsCompatch,
                    Uncertain = !rule.Certain,
                    Message = ModCompatTexts.FindingFileOverride(relative, names, winner.Name),
                };

            if (ApplyRelation(request, finding) is { } kept)
            {
                yield return kept;
            }
        }
    }

    private static IEnumerable<CompatFinding> KeyFindings(CompatRequest request, ContributionIndex index, CancellationToken ct)
    {
        var vanilla = request.Participants.FirstOrDefault(p => p.IsVanilla);
        var vanillaByScope = new Dictionary<string, Dictionary<string, CompatContribution>>(StringComparer.OrdinalIgnoreCase);
        var vanillaLastByScope = new Dictionary<string, Dictionary<string, CompatContribution>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in index.KeyGroups.Values.OrderBy(g => g.Scope, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if (request.Rules.LastWinsInLoadOrder && vanilla is not null && group.Contributions.Select(c => c.Participant.Id).Distinct().Count() == 1)
            {
                if (LosesToVanilla(request, index, group, vanilla, vanillaByScope, vanillaLastByScope) is { } loss)
                {
                    yield return loss;
                }

                continue;
            }

            if (!IsRelevant(request, group.Contributions.Select(c => c.Participant)))
            {
                continue;
            }

            // Victoria 3: Vanilla zählt in derselben Ladereihenfolge mit; lädt seine Fassung zuerst, gewinnt sie (gemessen 2026-10-11).
            var vanillaFirst = request.Rules.FirstWinsInLoadOrder && group.Behavior == OverrideBehavior.FirstWins && vanilla is not null
                ? VanillaEdge(request, index, vanilla, group.Scope, vanillaByScope, last: false).GetValueOrDefault(group.Key)
                : null;
            // Stellaris (Wiki): bei "zuletzt geladen gewinnt" ebenso; lädt die Vanilla-Fassung nach allen Mods, gewinnt sie.
            var vanillaLast = request.Rules.LastWinsInLoadOrder && (group.Behavior is OverrideBehavior.LastWins or OverrideBehavior.PartialKeys) && vanilla is not null
                ? VanillaEdge(request, index, vanilla, group.Scope, vanillaLastByScope, last: true).GetValueOrDefault(group.Key)
                : null;
            if (KeyClassifier.Classify(group, request.Rules.FirstWinsInLoadOrder, request.Rules.InjectAppends, vanillaFirst, request.Rules.LastWinsInLoadOrder, vanillaLast, request.Rules.LoadOrder) is { } finding
                && ApplyRelation(request, finding) is { } kept)
            {
                yield return kept;
            }
        }
    }

    /// <summary>
    /// Stellaris (Wiki "Modding"): Dateien laden nach ihrem Namen über Vanilla und alle Mods hinweg. Definiert nur eine Mod den Schlüssel und lädt die
    /// gewinnende Vanilla-Definition nach ihr ("zuletzt geladen gewinnt", einzelne Schlüssel) bzw. vor ihr ("zuerst geladen gewinnt"), wirkt die Mod
    /// nicht. Mit gesetztem Fokus zählt nur die Fokus-Mod; eine Fassung gleich der von Vanilla ändert nichts und bleibt ohne Befund.
    /// </summary>
    private static CompatFinding? LosesToVanilla(CompatRequest request, ContributionIndex index, KeyGroup group, CompatParticipant vanilla,
        Dictionary<string, Dictionary<string, CompatContribution>> firstCache, Dictionary<string, Dictionary<string, CompatContribution>> lastCache)
    {
        var mod = group.Contributions[0].Participant;
        if (request.FocusId is not null && mod.Id != request.FocusId)
        {
            return null;
        }

        bool last;
        if (group.Behavior is OverrideBehavior.LastWins or OverrideBehavior.PartialKeys)
        {
            last = true;
        }
        else if (group.Behavior == OverrideBehavior.FirstWins && request.Rules.FirstWinsInLoadOrder)
        {
            last = false;
        }
        else
        {
            return null;
        }

        var ordered = group.Contributions.OrderBy(c => c.RelativePath, request.Rules.LoadOrder).ToList();
        var own = last ? ordered[^1] : ordered[0];
        if (VanillaEdge(request, index, vanilla, group.Scope, last ? lastCache : firstCache, last).GetValueOrDefault(group.Key) is not { } edge)
        {
            return null;
        }

        var compare = request.Rules.LoadOrder.Compare(edge.RelativePath, own.RelativePath);
        if ((last ? compare <= 0 : compare >= 0) || KeyClassifier.SameContent(own, edge))
        {
            return null;
        }

        var example = RenameExample(own.RelativePath, edge.RelativePath, last, request.Rules.LoadOrder);
        return new CompatFinding
        {
            Severity = CompatSeverity.Conflict,
            Kind = CompatKind.LosesToVanilla,
            RelativePath = own.RelativePath,
            Key = group.Key,
            Contributions = ordered,
            Winner = vanilla,
            Uncertain = !group.Certain,
            Message = last
                ? ModCompatTexts.FindingLosesToVanillaLater(mod.Name, group.Key, own.RelativePath, edge.RelativePath, example)
                : ModCompatTexts.FindingLosesToVanillaEarlier(mod.Name, group.Key, own.RelativePath, edge.RelativePath, example),
        };
    }

    /// <summary>
    /// Ein Dateiname, der nach (<paramref name="last"/>) bzw. vor der Vanilla-Datei lädt: der Name der Mod-Datei mit zz_ … bzw. !!_ … davor, zuerst in
    /// ihrem Ordner, sonst im Ordner der Vanilla-Datei (Dateien eines Ordners laden vor seinen Unterordnern, siehe <see cref="GameLoadOrder"/>).
    /// </summary>
    private static string RenameExample(string own, string vanillaPath, bool last, GameLoadOrder order)
    {
        static string Folder(string path) => path.LastIndexOf('/') is var slash and > 0 ? path[..slash] : string.Empty;

        var name = own[(own.LastIndexOf('/') + 1)..];
        foreach (var folder in new[] { Folder(own), Folder(vanillaPath) })
        {
            for (var count = 2; count <= 10; count++)
            {
                var candidate = folder + "/" + new string(last ? 'z' : '!', count) + "_" + name;
                var compare = order.Compare(candidate, vanillaPath);
                if (last ? compare > 0 : compare < 0)
                {
                    return candidate;
                }
            }
        }

        return Folder(vanillaPath) + "/" + (last ? "zz_" : "!!_") + name;
    }

    /// <summary>
    /// Je Schlüssel die zuerst (<paramref name="last"/> = false, Regel "zuerst geladen gewinnt") bzw. zuletzt geladene Vanilla-Definition (Regeln "zuletzt
    /// geladen gewinnt" und einzelne Schlüssel) im Ordner <paramref name="scope"/> samt Unterordnern; je Ordner einmal gelesen. Stellaris: Vanilla-Dateien,
    /// die eine Mod des Laufs mit gleichem Pfad ersetzt, lädt das Spiel nicht und zählen nicht (Victoria 3 noch nicht, siehe Backlog).
    /// </summary>
    private static Dictionary<string, CompatContribution> VanillaEdge(CompatRequest request, ContributionIndex index, CompatParticipant vanilla, string scope, Dictionary<string, Dictionary<string, CompatContribution>> cache, bool last)
    {
        if (cache.TryGetValue(scope, out var known))
        {
            return known;
        }

        var result = new Dictionary<string, CompatContribution>(StringComparer.Ordinal);
        cache[scope] = result;
        var folder = Path.Combine(vanilla.Folder, scope.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(folder))
        {
            return result;
        }

        foreach (var full in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(vanilla.Folder, full).Replace('\\', '/');
            var rule = request.Rules.Match(relative);
            var wanted = last ? (rule.Behavior is OverrideBehavior.LastWins or OverrideBehavior.PartialKeys) : rule.Behavior == OverrideBehavior.FirstWins;
            if (!wanted || request.IsHidden(vanilla, relative) || (request.Rules.LastWinsInLoadOrder && index.PathGroups.ContainsKey(relative)))
            {
                continue;
            }

            foreach (var contribution in ContributionExtractor.Extract(vanilla, relative, full, rule, request.Cache, out _))
            {
                // Gleiche Ladeposition (eine Datei mit dem Schlüssel zweimal): bei "zuletzt" gilt die spätere Stelle.
                if (!result.TryGetValue(contribution.Key, out var seen) || (last ? request.Rules.LoadOrder.Compare(relative, seen.RelativePath) >= 0 : request.Rules.LoadOrder.Compare(relative, seen.RelativePath) < 0))
                {
                    result[contribution.Key] = contribution;
                }
            }
        }

        return result;
    }

    private static bool AllIdentical(IReadOnlyList<CompatParticipant> group, ContributionIndex index, string relative)
    {
        try
        {
            var first = index.Files[group[0].Id].HashOf(relative);
            return group.Skip(1).All(p => index.Files[p.Id].HashOf(relative) == first);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
