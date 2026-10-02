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
        foreach (var group in index.KeyGroups.Values.OrderBy(g => g.Scope, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsRelevant(request, group.Contributions.Select(c => c.Participant)))
            {
                continue;
            }

            if (KeyClassifier.Classify(group) is { } finding && ApplyRelation(request, finding) is { } kept)
            {
                yield return kept;
            }
        }
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
