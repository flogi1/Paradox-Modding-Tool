using ParadoxModTool.SaveGames;

namespace ParadoxModTool.ErrorLog;

public enum EngineCommandKind
{
    Effect,
    Trigger,
}

public enum HintKind
{
    EngineCommand,
    WrongCommandKind,
    DefinedInPlayset,
    DefinedOutsidePlayset,
    NotDefined,
    Usages,
    NotFoundInFiles,
    UsedInLocalization,
    UsedInGui,

    /// <summary>"used but never set", aber im Spielstand gesetzt: entsteht zur Laufzeit.</summary>
    SaveSetAtRuntime,

    /// <summary>Im Spielstand fehlt der Name, ein ähnlicher ist gesetzt.</summary>
    SaveLikelyTypo,

    /// <summary>Im Spielstand weder der Name noch ein ähnlicher.</summary>
    SaveNowhere,
}

public sealed record ErrorHint(HintKind Kind, string Name, IReadOnlyList<SymbolOccurrence> Occurrences, IReadOnlyList<string> Candidates, EngineCommandKind? CommandKind = null, SaveEvidence? Save = null)
{
    public ModSource? Mod => Occurrences.FirstOrDefault()?.Mod;
}

/// <summary>
/// Leitet aus den Katalog-Namen eines Fehlers (<see cref="ErrorKind.Fixes"/>) Hinweise ab ("findDefinition", "showUsages", "locCheck")
/// und wählt die anwendbaren Fix-Bausteine. Fixes, die Dateien ändern, gibt es nur für eigene Mods (Spec, Abschnitt Fix-Bausteine).
/// </summary>
public sealed class FixAdvisor
{
    private const int MaxCandidates = 10;

    private readonly SymbolIndex _index;
    private readonly Func<string, EngineCommandKind?> _engineCommandKind;

    public FixAdvisor(SymbolIndex index, Func<string, EngineCommandKind?> engineCommandKind)
    {
        _index = index;
        _engineCommandKind = engineCommandKind;
    }

    /// <summary>Hinweise zum Eintrag; mit <paramref name="save"/> auch die Spielstand-Regeln (Katalog <c>saveCheck</c>, nur bei Schwere Fehler).</summary>
    public IReadOnlyList<ErrorHint> HintsFor(ErrorEntry entry, SaveSnapshot? save = null)
    {
        var hints = new List<ErrorHint>();
        var names = entry.Kind?.Fixes ?? Array.Empty<string>();
        if (entry.Name is not { } name)
        {
            return hints;
        }

        if (names.Contains("findDefinition"))
        {
            hints.Add(DefinitionHint(entry, name));
        }

        if (names.Contains("showUsages"))
        {
            var usages = _index.Find(name).Where(o => o.Role is SymbolRole.Set or SymbolRole.Reference).ToList();
            hints.Add(usages.Count > 0
                ? new ErrorHint(HintKind.Usages, name, usages, Array.Empty<string>())
                : new ErrorHint(HintKind.NotFoundInFiles, name, Array.Empty<SymbolOccurrence>(), CandidatesFor(entry)));
        }

        if (names.Contains("locCheck"))
        {
            var localization = _index.Find(name).Where(o => o.Role == SymbolRole.Localization).ToList();
            if (localization.Count > 0)
            {
                hints.Add(new ErrorHint(HintKind.UsedInLocalization, name, localization, Array.Empty<string>()));
            }

            var gui = _index.Find(name).Where(o => o.Role == SymbolRole.Gui).ToList();
            if (gui.Count > 0)
            {
                hints.Add(new ErrorHint(HintKind.UsedInGui, name, gui, Array.Empty<string>()));
            }
        }

        // "used but never set" (Schwere Fehler): Der Spielstand zeigt, ob der Name zur Laufzeit entsteht oder ein Tippfehler ist.
        if (entry.Severity == ErrorSeverity.Error && EvidenceFor(entry, save) is { } evidence)
        {
            var none = Array.Empty<SymbolOccurrence>();
            hints.Add(evidence.ObjectCount > 0 ? new ErrorHint(HintKind.SaveSetAtRuntime, name, none, Array.Empty<string>(), Save: evidence)
                : evidence.SimilarName is { } similar ? new ErrorHint(HintKind.SaveLikelyTypo, name, none, new[] { similar }, Save: evidence)
                : new ErrorHint(HintKind.SaveNowhere, name, none, Array.Empty<string>(), Save: evidence));
        }

        return hints;
    }

    /// <summary>Befund aus dem Spielstand; null ohne Stand, ohne Namen oder bei Fehlerarten ohne <c>saveCheck</c>.</summary>
    public SaveEvidence? EvidenceFor(ErrorEntry entry, SaveSnapshot? save) =>
        save is null || entry.Name is not { } name || entry.Kind is not { } kind || !kind.Fixes.Contains("saveCheck")
            ? null
            : SaveEvidence.For(save, name, kind.SaveNames ?? "variable");

    /// <summary>
    /// Kandidaten zum Ersetzen: ein ähnlicher, im Spielstand gesetzter Name zuerst (nur wenn der Name selbst dort fehlt), dann "Possible
    /// match"-Kandidaten aus dem Log, dann ähnliche Namen aus dem Index (ohne den Namen selbst, ohne Doppelte).
    /// </summary>
    public IReadOnlyList<string> CandidatesFor(ErrorEntry entry, SaveSnapshot? save)
    {
        var candidates = CandidatesFor(entry);
        return EvidenceFor(entry, save) is { ObjectCount: 0, SimilarName: { } similar }
            ? new[] { similar }.Concat(candidates).Distinct(StringComparer.Ordinal).Take(MaxCandidates).ToList()
            : candidates;
    }

    /// <summary>"Possible match"-Kandidaten aus dem Log zuerst, dann ähnliche Namen aus dem Index (ohne den Namen selbst, ohne Doppelte).</summary>
    public IReadOnlyList<string> CandidatesFor(ErrorEntry entry)
    {
        if (entry.Name is not { } name)
        {
            return entry.Candidates;
        }

        var definitionsOnly = entry.Kind?.Fixes.Contains("findDefinition") == true;
        var similar = _index.SimilarNames(
            name,
            o => definitionsOnly ? o.Role == SymbolRole.Definition : o.Role is SymbolRole.Set or SymbolRole.Reference,
            max: MaxCandidates);
        return entry.Candidates.Concat(similar).Where(c => c != name).Distinct(StringComparer.Ordinal).Take(MaxCandidates).ToList();
    }

    public IReadOnlyList<string> FixesFor(ErrorEntry entry, IReadOnlyList<ErrorHint> hints, ErrorAttribution attribution)
    {
        var result = new List<string>();
        var own = attribution.Bucket == ErrorBucket.OwnMod;
        foreach (var fix in entry.Kind?.Fixes ?? Array.Empty<string>())
        {
            var applies = fix switch
            {
                "jump" => entry.HasFileReference && attribution.Source is not null,
                "replaceWithCandidate" => own && (CandidatesFor(entry).Count > 0 || hints.Any(h => h.Kind == HintKind.SaveLikelyTypo)),
                "renameEverywhere" => own && entry.Name is not null && hints.Any(h => h.Kind == HintKind.Usages && h.Occurrences.Any(o => o.Mod is { IsOwn: true })),
                "addModToPlayset" => hints.Any(h => h.Kind == HintKind.DefinedOutsidePlayset),
                "commentOutSet" => own && entry.Name is not null && entry.Kind is { SetEffects.Count: > 0 }
                    && !hints.Any(h => h.Kind is HintKind.UsedInLocalization or HintKind.UsedInGui),
                "traceScope" => own && entry.HasFileReference && attribution.Source is not null,
                "suppress" => true,
                _ => false,
            };

            if (applies)
            {
                result.Add(fix);
            }
        }

        return result;
    }

    private ErrorHint DefinitionHint(ErrorEntry entry, string name)
    {
        if (_engineCommandKind(name) is { } commandKind)
        {
            // Das Spiel meldet "Unknown effect X" nur, wenn X kein Engine-Effekt ist. Ist X ein Engine-Befehl, dann meist der anderen Art.
            EngineCommandKind? expected = entry.Code.StartsWith("EFF-", StringComparison.Ordinal) ? EngineCommandKind.Effect
                : entry.Code.StartsWith("TRG-", StringComparison.Ordinal) ? EngineCommandKind.Trigger
                : null;
            var kind = expected is null || expected == commandKind ? HintKind.EngineCommand : HintKind.WrongCommandKind;
            return new ErrorHint(kind, name, Array.Empty<SymbolOccurrence>(), Array.Empty<string>(), commandKind);
        }

        var definitions = _index.Find(name).Where(o => o.Role == SymbolRole.Definition).ToList();
        var inPlayset = definitions.Where(o => o.Mod is null || o.Mod.InPlayset).ToList();
        if (inPlayset.Count > 0)
        {
            return new ErrorHint(HintKind.DefinedInPlayset, name, inPlayset, Array.Empty<string>());
        }

        return definitions.Count > 0
            ? new ErrorHint(HintKind.DefinedOutsidePlayset, name, definitions, Array.Empty<string>())
            : new ErrorHint(HintKind.NotDefined, name, Array.Empty<SymbolOccurrence>(), CandidatesFor(entry));
    }
}
