using ParadoxModTool.Mods;

namespace ParadoxModTool.ModCompat;

public enum CompatSeverity
{
    Info,
    Hint,
    Conflict,
}

public enum CompatKind
{
    FileOverride,
    FileIdentical,
    KeyIdentical,
    KeyCombinable,
    KeyLoss,
    KeyConflict,
    RedundantOverride,
    TooCoarse,
    AdditiveOnly,
    ReplaceNeeded,
    Unreadable,
    MissingFolder,
}

public enum CompatSuggestion
{
    None,
    NeedsCompatch,
    ReviewBothSides,
    UseInject,
    UseReplace,
    ProvideOnlyChanged,
    RemoveRedundant,
}

/// <summary>Ein Ergebnis der Prüfung. Hält alle Beiträge mit Fundstelle, damit später ein Kompatch-Erzeuger (Stufe C) beide Seiten kennt.</summary>
public sealed class CompatFinding
{
    public required CompatSeverity Severity { get; init; }

    public required CompatKind Kind { get; init; }

    public required string RelativePath { get; init; }

    /// <summary>Der Schlüssel; leer bei Fund auf Dateiebene.</summary>
    public string Key { get; init; } = string.Empty;

    public IReadOnlyList<CompatContribution> Contributions { get; init; } = Array.Empty<CompatContribution>();

    public CompatParticipant? Winner { get; init; }

    public CompatSuggestion Suggestion { get; init; }

    public required string Message { get; init; }

    /// <summary>Die Regel oder Empfehlung ist nicht durch das Wiki gesichert.</summary>
    public bool Uncertain { get; init; }

    public string? Note { get; init; }

    internal CompatFinding With(CompatSeverity severity, string? note) => new()
    {
        Severity = severity,
        Kind = Kind,
        RelativePath = RelativePath,
        Key = Key,
        Contributions = Contributions,
        Winner = Winner,
        Suggestion = Suggestion,
        Message = Message,
        Uncertain = Uncertain,
        Note = note ?? Note,
    };
}

/// <summary>Eingabe einer Prüfung. Die Teilnehmer nach Rang, Vanilla mit Rang 0 (<see cref="CompatParticipant.IsVanilla"/>).</summary>
public sealed class CompatRequest
{
    public required IReadOnlyList<CompatParticipant> Participants { get; init; }

    public required OverrideRules Rules { get; init; }

    /// <summary>"Mod 1": jeder Fund muss sie enthalten. <see langword="null"/> = alle gegen alle.</summary>
    public string? FocusId { get; init; }

    /// <summary>Die Gegenseite (IDs); <see langword="null"/> = alle anderen Mods.</summary>
    public IReadOnlyCollection<string>? CounterpartIds { get; init; }

    public ParseCache Cache { get; init; } = new();

    /// <summary>Beziehung zweier Mods im Register (Dev-Branch, Submod); <see langword="null"/> = alle unabhängig.</summary>
    public Func<CompatParticipant, CompatParticipant, ModRelationKind>? Relation { get; init; }

    /// <summary>Ist der erste Teilnehmer ein Kompatch für den zweiten? <see langword="null"/> = keine Kompatchs bekannt.</summary>
    public Func<CompatParticipant, CompatParticipant, bool>? IsCompatchFor { get; init; }
}

public sealed class CompatResult
{
    public CompatResult(IReadOnlyList<CompatFinding> findings)
    {
        Findings = findings;
    }

    public IReadOnlyList<CompatFinding> Findings { get; }
}
