using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.Compatch;

public enum MergeStage
{
    Conflict,
    Review,
    Automatic,

    /// <summary>Plan 2: Entscheidung stammt aus einem Patch ohne Vergleichsbasis.</summary>
    FromExistingPatch,

    /// <summary>Plan 2: frühere Entscheidung, A und B unverändert.</summary>
    Decided,

    /// <summary>Plan 2: frühere Entscheidung, A oder B hat sich geändert.</summary>
    Outdated,

    /// <summary>Wirkt im Spiel ohnehin so, kein Patch-Eintrag nötig.</summary>
    NotNeeded,
}

public enum MergeOrigin
{
    A,
    B,

    /// <summary>A und B haben dieselbe Änderung.</summary>
    Both,
}

public enum DecisionKind
{
    TakeA,
    TakeB,
    TakeVanilla,

    /// <summary>Eigener Text (<see cref="MergeDecision.ManualText"/>), ersetzt die Einheit an dieser Stelle.</summary>
    Manual,
}

public sealed record MergeDecision(DecisionKind Kind, string? ManualText = null);

/// <summary>Eine Stelle, an der A und B vom Vanilla-Stand abweichen. Siehe Spec, Abschnitt "Kern", Schritt 2.</summary>
public sealed class MergeAction
{
    /// <summary>Adresse + "::" + Pfad aus Zuordnungsschlüsseln. Stabil über Läufe, solange sich an dieser Stelle nichts ändert.</summary>
    public required string Id { get; init; }

    public required EntryAddress Address { get; init; }

    /// <summary>Datei, die im Spiel ohne Patch gewinnt (Spec Nachtrag 2, Entscheidung 1); klein, mit <c>/</c>. Leer bei von Hand gebauten Aktionen.</summary>
    public string File { get; init; } = string.Empty;

    public required IReadOnlyList<string> Path { get; init; }

    public required string DisplayPath { get; init; }

    /// <summary><see langword="null"/> = Konflikt.</summary>
    public MergeOrigin? Origin { get; init; }

    public IReadOnlyList<ScriptEntry> Vanilla { get; init; } = Array.Empty<ScriptEntry>();

    public IReadOnlyList<ScriptEntry> A { get; init; } = Array.Empty<ScriptEntry>();

    public IReadOnlyList<ScriptEntry> B { get; init; } = Array.Empty<ScriptEntry>();

    public required MergeStage Stage { get; set; }

    public required DecisionKind DefaultDecision { get; init; }

    public bool Uncertain { get; init; }

    public string? Note { get; init; }

    /// <summary>Stelle aus "Fehlende Blöcke der Hauptmod übernehmen" (Option 2): die Hauptmod hat sie, das Ziel gar nicht.</summary>
    public bool FillsMissing { get; init; }

    /// <summary>Prüfsumme der A- und B-Werte (Plan 2 bindet Entscheidungen daran).</summary>
    public string ValueHash { get; init; } = string.Empty;
}

/// <summary>
/// Eine Abweichungsstelle, die der <see cref="MergeWalker"/> meldet. <see cref="InLogicBlock"/>: die Stelle ist ein Logikfeld oder liegt in einem;
/// <see cref="InsideLogicBlock"/>: der umgebende Block ist Logik (die Stelle ist nur ein Teil davon).
/// </summary>
public sealed record MergePoint(
    string Id,
    IReadOnlyList<string> Path,
    string DisplayPath,
    EntryUnit? Base,
    EntryUnit? A,
    EntryUnit? B,
    MergeOrigin? Origin,
    bool InLogicBlock,
    bool Ambiguous,
    bool InsideLogicBlock = false);

/// <summary>Liefert die Einträge, die an einer Abweichungsstelle ins Ergebnis kommen (leer = entfernt).</summary>
public delegate IReadOnlyList<ScriptEntry> MergePointResolver(MergePoint point);
