using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>
/// Eine Kompatch-Sitzung: löst einmal auf (<see cref="Load"/>), setzt Stufen aus Arbeitsstand und Vergleichsbasis (Rulings 15, 16) und hält
/// Entscheidungen, Formwahl und "gesehen" im <see cref="State"/>. Jede Änderung plant sofort neu (<see cref="CompatchEngine.Replan"/>). Schreibt nie selbst.
/// </summary>
public sealed class CompatchSession
{
    private readonly Dictionary<string, MergeStage> _computed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _explicit = new(StringComparer.Ordinal);
    private readonly Dictionary<EntryAddress, IReadOnlyList<ScriptEntry>> _overrides = new();
    private readonly Dictionary<EntryAddress, IReadOnlyList<ScriptEntry>> _existing = new();

    public CompatchSession(CompatchInput input, CompatchState state, CompatchBase? baseline)
    {
        Input = input;
        State = state;
        Baseline = baseline;
    }

    public CompatchInput Input { get; private set; }

    public CompatchState State { get; }

    public CompatchBase? Baseline { get; private set; }

    public PatchTarget Target => State.Target;

    public CompatchRun Run { get; private set; } = null!;

    public IReadOnlyList<MergeAction> Actions => Run.Actions;

    public void Load(IProgress<CompatProgress>? progress = null, CancellationToken ct = default)
    {
        Run = CompatchEngine.Run(Input, Target, null, null, progress, ct);
        foreach (var action in Run.Actions)
        {
            _computed[action.Id] = action.Stage;
        }

        if (Target == PatchTarget.Compatch && Baseline is null)
        {
            CollectExistingPatch();
        }

        AssignStages();
        Replan();
    }

    /// <summary>
    /// Plan 4, Ruling 7: Optionen der Hauptmod geändert. Legt die Aktionen mit den schon gelesenen Daten neu an (kein Einlesen), ordnet die
    /// Stufen aus Arbeitsstand und Vergleichsbasis neu zu und plant neu. Gespeicherte Entscheidungen bleiben (gebunden an Aktions-ID und A/B-Prüfsumme).
    /// </summary>
    public void Remerge(CompatchInput input)
    {
        Input = input;
        Run = new CompatchRun { Resolved = Run.Resolved, Actions = CompatchMerger.Decide(Run.Resolved, input) };
        _computed.Clear();
        _overrides.Clear();
        _existing.Clear();
        var ids = Run.Actions.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        _explicit.RemoveWhere(id => !ids.Contains(id));
        foreach (var action in Run.Actions)
        {
            _computed[action.Id] = action.Stage;
        }

        if (Target == PatchTarget.Compatch && Baseline is null)
        {
            CollectExistingPatch();
        }

        AssignStages();
        Replan();
    }

    public bool HasDecision(string id) => State.Decisions.ContainsKey(id);

    public bool IsExplicit(string id) => _explicit.Contains(id);

    public MergeDecision EffectiveDecision(MergeAction action) =>
        State.Decisions.TryGetValue(action.Id, out var stored) ? new MergeDecision(stored.Kind, stored.ManualText) : new MergeDecision(action.DefaultDecision);

    public void Decide(string id, MergeDecision decision)
    {
        Store(Find(id), decision);
        Replan();
    }

    /// <summary>Gleiche Stellen: gleiche A- und B-Werte (<see cref="MergeAction.ValueHash"/>) und gleiche Herkunft, auch in anderen Dateien.</summary>
    public IReadOnlyList<MergeAction> EqualTo(string id)
    {
        var action = Find(id);
        return Run.Actions.Where(a => a.Id != id && a.ValueHash == action.ValueHash && a.Origin == action.Origin).ToList();
    }

    /// <summary>Gleiche Stellen, die "Für alle gleichen übernehmen" ändert: die ohne eigene Entscheidung.</summary>
    public IReadOnlyList<MergeAction> EqualPending(string id) => EqualTo(id).Where(a => !HasDecision(a.Id)).ToList();

    /// <summary>
    /// "Für alle gleichen übernehmen": die angezeigte Entscheidung dieser Stelle (auch eigener Text) auf alle gleichen Stellen ohne eigene
    /// Entscheidung übertragen. Die Stelle selbst wird dabei ausdrücklich entschieden. Plant einmal neu.
    /// </summary>
    public void ApplyToEqual(string id)
    {
        var source = Find(id);
        var decision = EffectiveDecision(source);
        var targets = EqualPending(id);
        Store(source, decision);
        foreach (var target in targets)
        {
            Store(target, decision);
        }

        Replan();
    }

    /// <summary>Speichert eine Entscheidung ohne neu zu planen (<see cref="Decide"/>, <see cref="ApplyToEqual"/>).</summary>
    private void Store(MergeAction action, MergeDecision decision)
    {
        var id = action.Id;
        State.Decisions[id] = new StoredDecision(decision.Kind, decision.ManualText, action.ValueHash);
        _explicit.Add(id);
        _overrides.Remove(action.Address);
        if (action.Stage is MergeStage.Outdated or MergeStage.FromExistingPatch)
        {
            // Die Vorgabe aus dem bestehenden Patch gilt nur für die Adresse als Ganzes. Nach einer Einzelentscheidung fallen die übrigen
            // Aktionen der Adresse auf ihre Vorauswahl zurück; damit Werte aus dem handgeschriebenen Patch nicht unbemerkt verloren gehen,
            // werden sie "prüfen" (zählen als offen, "Vorsichtig" sperrt).
            foreach (var sibling in Run.Actions.Where(a => a.Address == action.Address && a.Id != id && a.Stage == MergeStage.FromExistingPatch))
            {
                sibling.Stage = MergeStage.Review;
            }

            action.Stage = action.Stage == MergeStage.Outdated ? MergeStage.Decided : _computed[id];
        }
    }

    /// <summary>"Passt": die angezeigte Entscheidung ausdrücklich bestätigen.</summary>
    public void Confirm(string id) => Decide(id, EffectiveDecision(Find(id)));

    /// <summary>↶: eine automatische Übernahme rückgängig machen (andere Seite nehmen); bei einem Konflikt die Entscheidung zurücksetzen.</summary>
    public void Undo(string id)
    {
        var action = Find(id);
        switch (action.Origin)
        {
            case MergeOrigin.A:
                Decide(id, new MergeDecision(DecisionKind.TakeB));
                break;
            case MergeOrigin.B:
                Decide(id, new MergeDecision(DecisionKind.TakeA));
                break;
            case MergeOrigin.Both:
                Decide(id, new MergeDecision(DecisionKind.TakeVanilla));
                break;
            default:
                ResetDecision(id);
                break;
        }
    }

    /// <summary>"Andere Seite" (Karten, Ansicht B): angezeigte Entscheidung A ↔ B; bei Vanilla oder eigenem Text die Seite gegenüber der Vorauswahl.</summary>
    public void TakeOtherSide(string id)
    {
        var action = Find(id);
        var other = EffectiveDecision(action).Kind switch
        {
            DecisionKind.TakeA => DecisionKind.TakeB,
            DecisionKind.TakeB => DecisionKind.TakeA,
            _ => action.DefaultDecision == DecisionKind.TakeA ? DecisionKind.TakeB : DecisionKind.TakeA,
        };
        Decide(id, new MergeDecision(other));
    }

    public void ResetDecision(string id)
    {
        State.Decisions.Remove(id);
        _explicit.Remove(id);
        var action = Find(id);
        if (action.Stage is MergeStage.Decided or MergeStage.Outdated)
        {
            action.Stage = _computed[id];
        }

        Replan();
    }

    public void SetSeen(string id, bool seen)
    {
        if (seen)
        {
            State.Seen.Add(id);
        }
        else
        {
            State.Seen.Remove(id);
        }
    }

    public bool IsSeen(string id) => State.Seen.Contains(id);

    public int SeenCount => Run.Actions.Count(a => State.Seen.Contains(a.Id));

    public void SetForm(EntryAddress address, PatchForm form)
    {
        State.Forms[address.Id] = form;
        Replan();
    }

    public PatchForm? FormOf(EntryAddress address) => State.Forms.TryGetValue(address.Id, out var form) ? form : null;

    /// <summary>"Alles neu prüfen": entschiedene, veraltete und aus dem Patch übernommene Aktionen wieder vorlegen. Entscheidungen bleiben Vorauswahl.</summary>
    public void RecheckAll()
    {
        foreach (var action in Run.Actions.Where(a => a.Stage is MergeStage.Decided or MergeStage.Outdated or MergeStage.FromExistingPatch))
        {
            action.Stage = _computed[action.Id];
        }

        _overrides.Clear();
        _explicit.Clear();
        Replan();
    }

    public IReadOnlyDictionary<MergeStage, int> Counts =>
        Run.Actions.GroupBy(a => a.Stage).ToDictionary(g => g.Key, g => g.Count());

    public int OpenCount => Run.Actions.Count(a => a.Stage is MergeStage.Conflict or MergeStage.Review or MergeStage.Outdated && !_explicit.Contains(a.Id));

    /// <summary>Grund, warum "Patch schreiben" gesperrt ist, oder <see langword="null"/> (Ruling 17).</summary>
    public string? WriteBlockerFor(bool cautious) => cautious && OpenCount > 0 ? CompatchTexts.CautiousBlocked(OpenCount) : null;

    public PreparedWrite PrepareWrite(IReadOnlyCollection<string> lockedPaths, Func<string, string> encodingFor) =>
        PatchWriter.Prepare(new WriteRequest
        {
            Run = Run,
            Input = Input,
            Target = Target,
            TargetFolder = State.TargetFolder,
            LockedPaths = lockedPaths,
            PreviousForeignEntries = Baseline?.ForeignEntries ?? new List<WrittenEntry>(),
            PreviousWholeFiles = Baseline?.WrittenFiles ?? new Dictionary<string, string>(),
            EncodingFor = encodingFor,
        });

    /// <summary>Die Vergleichsbasis nach einem erfolgreichen Schreiben. Setzt auch <see cref="Baseline"/>.</summary>
    public CompatchBase CaptureBase(PreparedWrite written, string? versionA, string? versionB)
    {
        var baseline = new CompatchBase
        {
            WrittenUtc = DateTime.UtcNow,
            ModAVersion = versionA,
            ModBVersion = versionB,
            ForeignEntries = written.ForeignEntries.ToList(),
            WrittenFiles = new Dictionary<string, string>(written.WholeFiles, StringComparer.OrdinalIgnoreCase),
        };
        foreach (var address in Run.Actions.Select(a => a.Address).Distinct())
        {
            baseline.AddressHashes[address.Id] = AddressHash(Run.Resolved.Addresses[address]);
        }

        Baseline = baseline;
        return baseline;
    }

    public static string AddressHash(AddressInfo info) => info.Address.Kind == AddressKind.WholeFile
        ? SiblingMatcher.Hash(info.A.FileHash + "|" + info.B.FileHash, 16)
        : SiblingMatcher.Hash(string.Join(" ", info.A.Entries.Select(SiblingMatcher.Canonical)) + "|" + string.Join(" ", info.B.Entries.Select(SiblingMatcher.Canonical)), 16);

    public IReadOnlyList<ScriptEntry> ExistingPatchEntries(EntryAddress address) =>
        _existing.TryGetValue(address, out var entries) ? entries : Array.Empty<ScriptEntry>();

    public void Replan()
    {
        var decisions = State.Decisions.ToDictionary(d => d.Key, d => new MergeDecision(d.Value.Kind, d.Value.ManualText), StringComparer.Ordinal);
        var byId = Run.Resolved.Addresses.Keys.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var forms = State.Forms.Where(f => byId.ContainsKey(f.Key)).ToDictionary(f => byId[f.Key], f => f.Value);
        CompatchEngine.Replan(Run, Input, Target, decisions, forms, _overrides);
    }

    private MergeAction Find(string id) => Run.Actions.First(a => a.Id == id);

    private void AssignStages()
    {
        foreach (var action in Run.Actions)
        {
            if (State.Decisions.TryGetValue(action.Id, out var stored))
            {
                action.Stage = stored.ValueHash == action.ValueHash ? MergeStage.Decided : MergeStage.Outdated;
                continue;
            }

            if (Baseline is not null && action.Stage is MergeStage.Automatic or MergeStage.Review
                && Baseline.AddressHashes.TryGetValue(action.Address.Id, out var hash)
                && hash == AddressHash(Run.Resolved.Addresses[action.Address]))
            {
                action.Stage = MergeStage.Decided;
            }
        }

        foreach (var group in Run.Actions.GroupBy(a => a.Address))
        {
            if (_existing.TryGetValue(group.Key, out var patched) && !group.Any(a => State.Decisions.ContainsKey(a.Id)))
            {
                _overrides[group.Key] = patched;
                foreach (var action in group)
                {
                    action.Stage = MergeStage.FromExistingPatch;
                }
            }
        }
    }

    /// <summary>Ruling 16: Schlüssel, die der Zielordner schon genau einmal hat, ergeben den Zustand "ohne Patch + Patch-Eintrag".</summary>
    private void CollectExistingPatch()
    {
        var index = TargetIndex.Build(State.TargetFolder, Input, readForeignKeys: true);
        foreach (var address in Run.Actions.Select(a => a.Address).Distinct().Where(a => a.Kind == AddressKind.Key))
        {
            var found = index.Find(address);
            if (found.Count != 1)
            {
                continue;
            }

            var info = Run.Resolved.Addresses[address];
            var parts = new List<KeyPart>();
            if (info.NoPatch.Exists)
            {
                parts.Add(new KeyPart(MergeSide.A, info.NoPatch.RelativePath ?? address.Scope, info.NoPatch.FullPath ?? string.Empty, OverrideOperator.None, address, info.NoPatch.Entries));
            }

            if (info.Behavior == OverrideBehavior.FirstWins)
            {
                parts.Insert(0, found[0].Part);
            }
            else
            {
                parts.Add(found[0].Part);
            }

            var combined = EffectiveCombiner.Combine(parts, info.Behavior, info.Certain).Entries;
            if (combined.Count > 0)
            {
                _existing[address] = combined;
            }
        }
    }
}
