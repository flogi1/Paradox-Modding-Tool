using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Legt für alle Adressen die Aktionen samt Stufe und Vorauswahl an.</summary>
public static class CompatchMerger
{
    public static List<MergeAction> Decide(ResolvedEntries resolved, CompatchInput input)
    {
        var actions = new List<MergeAction>();
        foreach (var info in resolved.Addresses.Values.OrderBy(i => i.Address.Id, StringComparer.Ordinal))
        {
            if (info.Address.Kind == AddressKind.WholeFile)
            {
                if (WholeFileAction(info, input) is { } fileAction)
                {
                    actions.Add(fileAction);
                }

                continue;
            }

            var noBase = WithoutOldVersion(info, input);
            MergeWalker.Merge(MergeBasis.Entries(info, input), info.A.Entries, info.B.Entries, input.LogicFieldKeys, point =>
            {
                if (noBase && point.Origin == MergeOrigin.Both)
                {
                    // Update-Modus ohne alte Fassung: gleiche Stellen sind "neu auf beiden Seiten", aber es gibt nichts zu entscheiden.
                    return MergeChoice.Entries(point, DecisionKind.TakeA);
                }

                var action = ActionFor(info, point, input);
                actions.Add(action);
                return MergeChoice.Entries(point, action.DefaultDecision);
            });
        }

        return actions;
    }

    public static MergeAction ActionFor(AddressInfo info, MergePoint point, CompatchInput input)
    {
        var inLogic = point.InLogicBlock || LogicFields.IsLogicScope(info.Address.Scope);
        var origin = point.Origin;
        var defaultDecision = DefaultFor(point.Origin, info);
        MergeStage stage;
        var notes = new List<string>();
        var fillsMissing = false;
        if (point.Ambiguous)
        {
            notes.Add(CompatchTexts.NoteAmbiguous);
        }

        if (FillsFromMain(point, input) is { } main)
        {
            // Option 2 (Spec "Verhalten"): Das Ziel hat hier gar nichts, die Hauptmod schon → übernehmen. Ein ganzer fehlender Block ist
            // automatisch; ein Teil innerhalb eines Logikblocks, den beide haben, wird vorgelegt.
            origin = main == MergeSide.A ? MergeOrigin.A : MergeOrigin.B;
            defaultDecision = main == MergeSide.A ? DecisionKind.TakeA : DecisionKind.TakeB;
            var insideLogic = point.InsideLogicBlock || (LogicFields.IsLogicScope(info.Address.Scope) && point.Path.Count > 1);
            stage = insideLogic || point.Ambiguous ? MergeStage.Review : MergeStage.Automatic;
            notes.Insert(0, CompatchTexts.NoteMissingInTarget);
            fillsMissing = true;
            if (insideLogic)
            {
                notes.Add(CompatchTexts.NoteLogicBlock);
            }
        }
        else
        {
            stage = origin is null
                ? MergeStage.Conflict
                : inLogic || point.Ambiguous ? MergeStage.Review : MergeStage.Automatic;
            if (inLogic && origin is not null)
            {
                notes.Add(CompatchTexts.NoteLogicBlock);
            }
        }

        if (origin is not MergeOrigin.Both && WithoutOldVersion(info, input))
        {
            // Update-Modus ohne alte Vanilla-Fassung: Wer was geändert hat, ist unbekannt. Nichts automatisch, die Mod ist vorausgewählt.
            stage = stage == MergeStage.Automatic ? MergeStage.Review : stage;
            defaultDecision = DecisionKind.TakeB;
            notes.Insert(0, CompatchTexts.NoteNoOldVersion);
        }

        return new MergeAction
        {
            Id = info.Address.Id + "::" + point.Id,
            Address = info.Address,
            File = FileOf(info),
            Path = point.Path,
            DisplayPath = point.DisplayPath,
            Origin = origin,
            Vanilla = MergeChoice.Emit(point.Base),
            A = MergeChoice.Emit(point.A),
            B = MergeChoice.Emit(point.B),
            Stage = stage,
            DefaultDecision = defaultDecision,
            Uncertain = info.Uncertain,
            Note = notes.Count == 0 ? null : string.Join(", ", notes),
            ValueHash = SiblingMatcher.Hash(Canonical(point.A) + "|" + Canonical(point.B), 16),
            FillsMissing = fillsMissing,
        };
    }

    /// <summary>Hauptmod-Seite, wenn Option 2 an dieser Stelle greift (Ziel hat keine Einheit, Hauptmod schon); sonst <see langword="null"/>.</summary>
    private static MergeSide? FillsFromMain(MergePoint point, CompatchInput input)
    {
        if (!input.FillMissingFromMain || input.MainSide is not { } main || main == MergeSide.Vanilla)
        {
            return null;
        }

        var (mainUnit, targetUnit) = main == MergeSide.A ? (point.A, point.B) : (point.B, point.A);
        return mainUnit is not null && targetUnit is null ? main : null;
    }

    private static MergeAction? WholeFileAction(AddressInfo info, CompatchInput input)
    {
        if (info.A.FileHash == info.B.FileHash)
        {
            return null;
        }

        var baseHash = MergeBasis.FileHash(info, input);
        MergeOrigin? origin = info.A.FileHash == baseHash
            ? MergeOrigin.B
            : info.B.FileHash == baseHash ? MergeOrigin.A : null;
        var noBase = WithoutOldVersion(info, input);
        return new MergeAction
        {
            Id = info.Address.Id,
            Address = info.Address,
            File = FileOf(info),
            Path = Array.Empty<string>(),
            DisplayPath = info.Address.Scope,
            Origin = origin,
            Stage = origin is null ? MergeStage.Conflict : noBase ? MergeStage.Review : MergeStage.Automatic,
            DefaultDecision = noBase ? DecisionKind.TakeB : DefaultFor(origin, info),
            Uncertain = info.Uncertain,
            Note = noBase ? string.Join(", ", new[] { CompatchTexts.NoteNoOldVersion, info.Note }.Where(n => !string.IsNullOrEmpty(n))) : info.Note,
            ValueHash = SiblingMatcher.Hash(info.A.FileHash + "|" + info.B.FileHash, 16),
        };
    }

    /// <summary>Update-Modus: liegt die Datei der Adresse ohne alte Vanilla-Fassung vor (<see cref="CompatchInput.NoBasePaths"/>)?</summary>
    private static bool WithoutOldVersion(AddressInfo info, CompatchInput input) =>
        input.NoBasePaths.Count > 0 && input.NoBasePaths.Any(p => string.Equals(EntryAddress.Norm(p), FileOf(info), StringComparison.OrdinalIgnoreCase));

    /// <summary>Datei einer Adresse: bei Schlüsseln die Gewinner-Datei ohne Patch, sonst A, B, Vanilla, zuletzt der Bereich; Dateirest und ganze Datei: ihr Pfad.</summary>
    public static string FileOf(AddressInfo info) => info.Address.Kind == AddressKind.Key
        ? EntryAddress.Norm(FileSource(info).RelativePath ?? info.Address.Scope)
        : info.Address.Scope;

    /// <summary>Seite, aus der <see cref="FileOf"/> die Datei nimmt (gleiche Rückfallkette); <see langword="null"/> ohne Datei oder bei Dateirest/ganzer Datei.</summary>
    public static MergeSide? FileSideOf(AddressInfo info) => info.Address.Kind == AddressKind.Key && FileSource(info).RelativePath is not null
        ? FileSource(info) == info.NoPatch ? info.NoPatch.Side : FileSource(info) == info.A ? MergeSide.A : FileSource(info) == info.B ? MergeSide.B : MergeSide.Vanilla
        : info.NoPatch.Side;

    private static Effective FileSource(AddressInfo info) =>
        info.NoPatch.RelativePath is not null ? info.NoPatch
        : info.A.RelativePath is not null ? info.A
        : info.B.RelativePath is not null ? info.B
        : info.Vanilla;

    private static DecisionKind DefaultFor(MergeOrigin? origin, AddressInfo info) => origin switch
    {
        MergeOrigin.A or MergeOrigin.Both => DecisionKind.TakeA,
        MergeOrigin.B => DecisionKind.TakeB,
        _ => info.NoPatch.Side == MergeSide.A ? DecisionKind.TakeA : DecisionKind.TakeB,
    };

    private static string Canonical(EntryUnit? unit) =>
        unit is null ? string.Empty : string.Join(" ", MergeChoice.Emit(unit).Select(SiblingMatcher.Canonical));
}
