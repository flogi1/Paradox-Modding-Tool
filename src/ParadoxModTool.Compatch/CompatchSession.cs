using ParadoxModTool.ModCompat;
using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

/// <summary>Gesicherter Entscheidungsstand einiger Blöcke (<see cref="CompatchSession.Capture"/>), für Rückgängig im Merge-Fenster.</summary>
public sealed class DecisionSnapshot
{
    internal Dictionary<EntryAddress, (StoredBlockText? Text, IReadOnlyList<ScriptEntry>? Entries, Dictionary<string, (MergeStage Stage, bool Explicit)>? Before, IReadOnlyList<ScriptEntry>? Override)> Blocks { get; } = new();

    internal Dictionary<string, (StoredDecision? Decision, bool Explicit, MergeStage Stage)> Actions { get; } = new(StringComparer.Ordinal);
}

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
    private readonly Dictionary<EntryAddress, IReadOnlyList<ScriptEntry>> _blockTexts = new();
    private readonly Dictionary<EntryAddress, Dictionary<string, (MergeStage Stage, bool Explicit)>> _beforeBlockText = new();
    private CompatchRun? _indexedRun;
    private Dictionary<string, MergeAction>? _byId;
    private ILookup<EntryAddress, MergeAction>? _byAddress;

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

        ForgetChangedFiles();
        AssignStages();
        Replan();
    }

    /// <summary>
    /// Update-Modus: Dateien, die beim Laden nicht mehr den zuletzt geschriebenen Inhalt hatten (z. B. von Hand zurückkopiert); ihre
    /// Entscheidungen und ihre Vergleichsbasis wurden verworfen. Der Aufrufer speichert dann Arbeitsstand und Basis, sonst geschähe es bei
    /// jedem Öffnen erneut.
    /// </summary>
    public IReadOnlyList<string> ForgottenFiles { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Update-Modus: Die Mod-Datei ist zugleich das Ziel. Stimmt ihr Inhalt nicht mehr mit dem geschriebenen überein, gelten gespeicherte
    /// Entscheidungen und die Prüfsummen der Basis nicht mehr: Nach einem Zurückkopieren passten die Prüfsummen wieder zum Stand vor dem
    /// Schreiben, und alle Stellen galten als erledigt (Nutzertest 2026-10-01, hud.gui). Entfernt wird nach Adress-ID, damit auch Entscheidungen
    /// zu Stellen früherer Läufe verschwinden.
    /// </summary>
    private void ForgetChangedFiles()
    {
        ForgottenFiles = Array.Empty<string>();
        if (Target != PatchTarget.UpdateMod || Baseline is not { UpdateFiles.Count: > 0 } baseline)
        {
            return;
        }

        var changed = baseline.UpdateFiles
            .Where(f => PatchWriter.FullPath(State.TargetFolder, f.Key) is var full && (!File.Exists(full) || TextEncodings.Sha256(File.ReadAllBytes(full)) != f.Value))
            .Select(f => f.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (changed.Count == 0)
        {
            return;
        }

        var addresses = Run.Actions.Where(a => changed.Contains(PatchWriter.Norm(a.File))).Select(a => a.Address.Id).ToHashSet(StringComparer.Ordinal);
        bool OfChanged(string id) => addresses.Contains(id) || (id.IndexOf("::", StringComparison.Ordinal) is var cut and >= 0 && addresses.Contains(id[..cut]));
        foreach (var id in State.Decisions.Keys.Where(OfChanged).ToList())
        {
            State.Decisions.Remove(id);
        }

        State.Seen.RemoveWhere(OfChanged);
        foreach (var id in State.BlockTexts.Keys.Concat(State.Forms.Keys).Concat(baseline.AddressHashes.Keys).Where(addresses.Contains).ToList())
        {
            State.BlockTexts.Remove(id);
            State.Forms.Remove(id);
            baseline.AddressHashes.Remove(id);
        }

        foreach (var file in changed)
        {
            baseline.UpdateFiles.Remove(file);
        }

        ForgottenFiles = changed.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
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
        _blockTexts.Clear();
        _beforeBlockText.Clear();
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
        var action = Find(id);
        Store(action, decision);
        Replan(new[] { action.Address });
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

        Replan(targets.Select(t => t.Address).Append(source.Address));
    }

    /// <summary>
    /// Sammelentscheidung (ganzer Block, ganze Datei, ganzer Ordner): jede Stelle bekommt <paramref name="decisionFor"/> und gilt als ausdrücklich
    /// entschieden. Plant einmal neu; ohne Stellen passiert nichts.
    /// </summary>
    public void DecideMany(IEnumerable<string> ids, Func<MergeAction, MergeDecision> decisionFor)
    {
        var actions = ids.Distinct(StringComparer.Ordinal).Select(Find).ToList();
        if (actions.Count == 0)
        {
            return;
        }

        foreach (var action in actions)
        {
            Store(action, decisionFor(action));
        }

        Replan(actions.Select(a => a.Address));
    }

    /// <summary>"Passt" für viele Stellen: die angezeigte Entscheidung jeder Stelle bestätigen ("als gelöst markieren").</summary>
    /// <remarks>Stellen in Blöcken mit eigenem Blocktext sind schon entschieden; sie werden ausgelassen, sonst höbe das Bestätigen den Blocktext auf.</remarks>
    public void ConfirmMany(IEnumerable<string> ids) =>
        DecideMany(ids.Where(id => !_blockTexts.ContainsKey(Find(id).Address)), EffectiveDecision);

    /// <summary>
    /// Stellen aus <paramref name="ids"/>, deren schon getroffene Entscheidung <paramref name="decisionFor"/> ändern würde: eine gespeicherte
    /// Entscheidung mit anderer Wahl oder ein Blocktext im Block der Stelle (Nachfrage vor dem Überschreiben).
    /// </summary>
    public IReadOnlyList<string> Overwritten(IEnumerable<string> ids, Func<MergeAction, MergeDecision> decisionFor)
    {
        var result = new List<string>();
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            var action = Find(id);
            var wanted = decisionFor(action);
            if (_blockTexts.ContainsKey(action.Address)
                || (State.Decisions.TryGetValue(id, out var stored) && (stored.Kind != wanted.Kind || stored.ManualText != wanted.ManualText)))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// Stellen aus <paramref name="ids"/>, die offene Konflikte sind (kein Entscheid, kein Blocktext). "Alle als gelöst markieren" würde bei ihnen
    /// stillschweigend den Vorschlag übernehmen; das Merge-Fenster fragt deshalb vorher nach.
    /// </summary>
    public IReadOnlyList<string> OpenConflicts(IEnumerable<string> ids) =>
        ids.Distinct(StringComparer.Ordinal)
            .Select(Find)
            .Where(a => a.Stage == MergeStage.Conflict && !HasDecision(a.Id) && !_blockTexts.ContainsKey(a.Address))
            .Select(a => a.Id)
            .ToList();

    /// <summary>Sichert den Entscheidungsstand der Blöcke <paramref name="addresses"/> (für Rückgängig, <see cref="Restore"/>).</summary>
    public DecisionSnapshot Capture(IEnumerable<EntryAddress> addresses)
    {
        var snapshot = new DecisionSnapshot();
        foreach (var address in addresses.Distinct())
        {
            snapshot.Blocks[address] = (
                State.BlockTexts.TryGetValue(address.Id, out var text) ? text : null,
                _blockTexts.TryGetValue(address, out var entries) ? entries : null,
                _beforeBlockText.TryGetValue(address, out var before) ? new Dictionary<string, (MergeStage, bool)>(before, StringComparer.Ordinal) : null,
                _overrides.TryGetValue(address, out var patched) ? patched : null);
            foreach (var action in ByAddress[address])
            {
                snapshot.Actions[action.Id] = (State.Decisions.TryGetValue(action.Id, out var stored) ? stored : null, _explicit.Contains(action.Id), action.Stage);
            }
        }

        return snapshot;
    }

    /// <summary>Stellt einen mit <see cref="Capture"/> gesicherten Stand wieder her (Entscheidungen, Stufen, Blocktexte) und plant diese Blöcke neu.</summary>
    public void Restore(DecisionSnapshot snapshot)
    {
        foreach (var (address, block) in snapshot.Blocks)
        {
            Set(State.BlockTexts, address.Id, block.Text);
            Set(_blockTexts, address, block.Entries);
            Set(_beforeBlockText, address, block.Before);
            Set(_overrides, address, block.Override);
        }

        Index();
        foreach (var (id, saved) in snapshot.Actions)
        {
            if (!_byId!.TryGetValue(id, out var action))
            {
                continue;
            }

            Set(State.Decisions, id, saved.Decision);
            if (saved.Explicit)
            {
                _explicit.Add(id);
            }
            else
            {
                _explicit.Remove(id);
            }

            action.Stage = saved.Stage;
        }

        Replan(snapshot.Blocks.Keys);

        static void Set<TKey, TValue>(IDictionary<TKey, TValue> map, TKey key, TValue? value)
            where TValue : class
        {
            if (value is null)
            {
                map.Remove(key);
            }
            else
            {
                map[key] = value;
            }
        }
    }

    /// <summary>Hat der Block einen eigenen Text (markierte Zeilen übernommen)?</summary>
    public bool HasBlockText(EntryAddress address) => _blockTexts.ContainsKey(address);

    /// <summary>
    /// Eigener Text für den ganzen Block (Nutzerwunsch: markierte Zeilen im Vergleichsfenster übernehmen). Gewinnt über die Einzelentscheidungen,
    /// alle Stellen des Blocks gelten als entschieden. Liefert die Fehlermeldung, wenn der Text nicht lesbar ist; dann ändert sich nichts.
    /// Der Text bekommt die Formatierung der Quellen zurück (<see cref="SourceFormat"/>), vorrangig die der Spalte <paramref name="from"/>.
    /// </summary>
    public string? SetBlockText(EntryAddress address, string text, CompatchColumn? from = null)
    {
        IReadOnlyList<ScriptEntry> entries;
        try
        {
            text = SourceFormat.Text(text, FormatSources(address, from));
            entries = ScriptDocument.Parse(text).Entries;
        }
        catch (ScriptParseException ex)
        {
            return ex.Message;
        }

        var actions = ByAddress[address].ToList();
        if (!_beforeBlockText.ContainsKey(address))
        {
            _beforeBlockText[address] = actions.ToDictionary(a => a.Id, a => (a.Stage, _explicit.Contains(a.Id)), StringComparer.Ordinal);
        }

        State.BlockTexts[address.Id] = new StoredBlockText(text, AddressHash(Run.Resolved.Addresses[address]));
        _blockTexts[address] = entries;
        foreach (var action in actions)
        {
            action.Stage = MergeStage.Decided;
            _explicit.Add(action.Id);
        }

        Replan(new[] { address });
        return null;
    }

    /// <summary>
    /// Markierte Zeilen eines Blocks übernehmen (Taste U): Jede geänderte Stelle, die einer Aktion entspricht, wird einzeln entschieden (A, B,
    /// Vanilla oder eigener Text); die übrigen Stellen bleiben, wie sie sind. Nur wenn eine Änderung keiner Stelle entspricht oder der Block schon
    /// einen eigenen Text hat, wird der ganze Text zum Blocktext (<see cref="SetBlockText"/>). Nutzerbefund 2026-09-30: eine Zeile erledigte
    /// sonst alle Stellen des Blocks, und eine spätere Einzelentscheidung verwarf die Übernahme wieder.
    /// </summary>
    public string? TakeBlockLines(EntryAddress address, string text, CompatchColumn? from = null)
    {
        IReadOnlyList<ScriptEntry> entries;
        try
        {
            entries = ScriptDocument.Parse(text).Entries;
        }
        catch (ScriptParseException ex)
        {
            return ex.Message;
        }

        if (HasBlockText(address) || !Run.Applied.Desired.TryGetValue(address, out var current))
        {
            return SetBlockText(address, text, from);
        }

        // Eigener Text einer Stelle behält so die Formatierung der Quelle wie ein Blocktext.
        entries = SourceFormat.Adopt(entries, FormatSources(address, from));

        var changed = new List<MergePoint>();
        MergeWalker.Merge(current.Entries, entries, current.Entries, Input.LogicFieldKeys, point =>
        {
            changed.Add(point);
            return MergeChoice.Entries(point, DecisionKind.TakeA);
        });
        var byId = ByAddress[address].ToDictionary(a => a.Id, StringComparer.Ordinal);
        var decisions = new Dictionary<string, MergeDecision>(StringComparer.Ordinal);
        foreach (var point in changed.Where(p => p.Origin != MergeOrigin.Both))
        {
            if (!byId.TryGetValue(address.Id + "::" + point.Id, out var action))
            {
                return SetBlockText(address, text, from);
            }

            var wanted = MergeChoice.Emit(point.A);
            decisions[action.Id] = StructuralEquality.EntriesEqual(wanted, action.A) ? new MergeDecision(DecisionKind.TakeA)
                : StructuralEquality.EntriesEqual(wanted, action.B) ? new MergeDecision(DecisionKind.TakeB)
                : StructuralEquality.EntriesEqual(wanted, action.Vanilla) ? new MergeDecision(DecisionKind.TakeVanilla)
                : new MergeDecision(DecisionKind.Manual, SourceFormat.Render(wanted));
        }

        if (decisions.Count > 0)
        {
            DecideMany(decisions.Keys, a => decisions[a.Id]);
        }

        return null;
    }

    /// <summary>
    /// Die Quellen eines Blocks, deren Formatierung übernommener Text zurückbekommt: die Spalte <paramref name="column"/> zuerst, dann die übrigen
    /// Dateien. Das Ergebnis zählt nicht; es ist zusammengeführt oder selbst ein Blocktext und damit schon einheitlich formatiert.
    /// </summary>
    private IReadOnlyList<IReadOnlyList<ScriptEntry>> FormatSources(EntryAddress address, CompatchColumn? column)
    {
        if (!Run.Resolved.Addresses.TryGetValue(address, out var info))
        {
            return Array.Empty<IReadOnlyList<ScriptEntry>>();
        }

        var basis = MergeBasis.Entries(info, Input);
        IReadOnlyList<ScriptEntry>[] order = column switch
        {
            CompatchColumn.B => new[] { info.B.Entries, info.A.Entries, basis, info.Vanilla.Entries, info.NoPatch.Entries },
            CompatchColumn.Vanilla => new[] { basis, info.Vanilla.Entries, info.A.Entries, info.B.Entries, info.NoPatch.Entries },
            _ => new[] { info.A.Entries, info.B.Entries, basis, info.Vanilla.Entries, info.NoPatch.Entries },
        };
        return order.Where(entries => entries.Count > 0).Distinct<IReadOnlyList<ScriptEntry>>(ReferenceEqualityComparer.Instance).ToList();
    }

    /// <summary>Hebt den eigenen Blocktext auf; die Stellen bekommen ihren Stand von vorher zurück (Rückgängig im Vergleichsfenster).</summary>
    public void RemoveBlockText(EntryAddress address)
    {
        if (DropBlockText(address))
        {
            Replan(new[] { address });
        }
    }

    private bool DropBlockText(EntryAddress address)
    {
        var removed = State.BlockTexts.Remove(address.Id) | _blockTexts.Remove(address);
        if (!removed)
        {
            return false;
        }

        _beforeBlockText.Remove(address, out var before);
        foreach (var action in ByAddress[address])
        {
            if (before is not null && before.TryGetValue(action.Id, out var old))
            {
                action.Stage = old.Stage;
                if (!old.Explicit)
                {
                    _explicit.Remove(action.Id);
                }
            }
            else
            {
                action.Stage = State.Decisions.ContainsKey(action.Id) ? MergeStage.Decided : _computed[action.Id];
                if (!State.Decisions.ContainsKey(action.Id))
                {
                    _explicit.Remove(action.Id);
                }
            }
        }

        return true;
    }

    /// <summary>Speichert eine Entscheidung ohne neu zu planen (<see cref="Decide"/>, <see cref="ApplyToEqual"/>).</summary>
    private void Store(MergeAction action, MergeDecision decision)
    {
        // Eine Einzelentscheidung im Block hebt einen eigenen Blocktext auf (sonst wirkte sie nicht).
        DropBlockText(action.Address);
        var id = action.Id;
        State.Decisions[id] = new StoredDecision(decision.Kind, decision.ManualText, action.ValueHash);
        _explicit.Add(id);
        _overrides.Remove(action.Address);
        if (action.Stage is MergeStage.Outdated or MergeStage.FromExistingPatch)
        {
            // Die Vorgabe aus dem bestehenden Patch gilt nur für die Adresse als Ganzes. Nach einer Einzelentscheidung fallen die übrigen
            // Aktionen der Adresse auf ihre Vorauswahl zurück; damit Werte aus dem handgeschriebenen Patch nicht unbemerkt verloren gehen,
            // werden sie "prüfen" (zählen als offen, "Vorsichtig" sperrt).
            foreach (var sibling in ByAddress[action.Address].Where(a => a.Id != id && a.Stage == MergeStage.FromExistingPatch))
            {
                sibling.Stage = MergeStage.Review;
            }

            action.Stage = action.Stage == MergeStage.Outdated ? MergeStage.Decided : _computed[id];
        }
    }

    /// <summary>"Passt": die angezeigte Entscheidung ausdrücklich bestätigen.</summary>
    public void Confirm(string id)
    {
        // Im Block mit eigenem Blocktext ist die Stelle schon entschieden; ein erneutes Bestätigen höbe den Blocktext auf.
        if (!_blockTexts.ContainsKey(Find(id).Address))
        {
            Decide(id, EffectiveDecision(Find(id)));
        }
    }

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
        DropBlockText(Find(id).Address);
        State.Decisions.Remove(id);
        _explicit.Remove(id);
        var action = Find(id);
        if (action.Stage is MergeStage.Decided or MergeStage.Outdated)
        {
            action.Stage = _computed[id];
        }

        Replan(new[] { action.Address });
    }

    /// <summary>
    /// Sammel-Zurücksetzen (Block, Datei, Ordner; Nutzerwunsch 2026-10-01 als Ausweg für eine ganze Datei): Entscheidungen und Blocktexte der
    /// Stellen fallen weg, jede Stelle zeigt wieder ihren Vorschlag. Plant einmal neu statt je Stelle (hud.gui hat hunderte Stellen).
    /// </summary>
    public void ResetMany(IEnumerable<string> ids)
    {
        var actions = ids.Distinct(StringComparer.Ordinal).Select(Find).ToList();
        if (actions.Count == 0)
        {
            return;
        }

        foreach (var address in actions.Select(a => a.Address).Distinct())
        {
            DropBlockText(address);
        }

        foreach (var action in actions)
        {
            State.Decisions.Remove(action.Id);
            _explicit.Remove(action.Id);
            if (action.Stage is MergeStage.Decided or MergeStage.Outdated)
            {
                action.Stage = _computed[action.Id];
            }
        }

        Replan(actions.Select(a => a.Address).Distinct());
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
        Replan(Array.Empty<EntryAddress>());
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
        if (Target == PatchTarget.UpdateMod)
        {
            foreach (var change in written.Changes.Where(c => c.Content is not null))
            {
                baseline.UpdateFiles[PatchWriter.Norm(change.RelativePath)] = TextEncodings.Sha256(change.Content!);
            }
        }

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

    public void Replan() => Replan(null);

    /// <summary>
    /// Plant neu. Mit <paramref name="changed"/> werden nur diese Blöcke neu zusammengeführt (eine Entscheidung ändert nur ihren Block; bei großen
    /// Mods mit zehntausenden Stellen spart das den Großteil der Zeit), der Patch-Plan entsteht trotzdem ganz neu.
    /// </summary>
    private void Replan(IEnumerable<EntryAddress>? changed)
    {
        var decisions = State.Decisions.ToDictionary(d => d.Key, d => new MergeDecision(d.Value.Kind, d.Value.ManualText), StringComparer.Ordinal);
        var byId = Run.Resolved.Addresses.Keys.ToDictionary(a => a.Id, StringComparer.Ordinal);
        var forms = State.Forms.Where(f => byId.ContainsKey(f.Key)).ToDictionary(f => byId[f.Key], f => f.Value);
        var overrides = new Dictionary<EntryAddress, IReadOnlyList<ScriptEntry>>(_overrides);
        foreach (var (address, entries) in _blockTexts)
        {
            // Ein Blocktext gewinnt: ohne Einzelentscheidungen der Adresse greift die Vorgabe im DecisionApplier.
            overrides[address] = entries;
            foreach (var action in ByAddress[address])
            {
                decisions.Remove(action.Id);
            }
        }

        CompatchEngine.Replan(Run, Input, Target, decisions, forms, overrides, changed?.ToHashSet());
    }

    private MergeAction Find(string id)
    {
        Index();
        return _byId![id];
    }

    /// <summary>Aktionen je Adresse (Nachschlagen statt Durchsuchen aller Stellen).</summary>
    private ILookup<EntryAddress, MergeAction> ByAddress
    {
        get
        {
            Index();
            return _byAddress!;
        }
    }

    /// <summary>Baut die Nachschlagetabellen neu, sobald der Lauf wechselt (<see cref="Load"/>, <see cref="Remerge"/>).</summary>
    private void Index()
    {
        if (_indexedRun != Run)
        {
            _indexedRun = Run;
            _byId = Run.Actions.ToDictionary(a => a.Id, StringComparer.Ordinal);
            _byAddress = Run.Actions.ToLookup(a => a.Address);
        }
    }

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

            // Gespeicherter Blocktext: entschieden, solange A und B dieses Blocks gleich geblieben sind, sonst veraltet.
            if (State.BlockTexts.TryGetValue(group.Key.Id, out var blockText))
            {
                try
                {
                    // Blocktexte aus älteren Läufen stehen noch einheitlich formatiert im Arbeitsstand: Formatierung der Quellen zurückgeben.
                    var text = SourceFormat.Text(blockText.Text, FormatSources(group.Key, null));
                    _blockTexts[group.Key] = ScriptDocument.Parse(text).Entries;
                    if (text != blockText.Text)
                    {
                        State.BlockTexts[group.Key.Id] = blockText with { Text = text };
                    }
                }
                catch (ScriptParseException)
                {
                    continue;
                }

                var current = blockText.AddressHash == AddressHash(Run.Resolved.Addresses[group.Key]);
                foreach (var action in group)
                {
                    action.Stage = current ? MergeStage.Decided : MergeStage.Outdated;
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
