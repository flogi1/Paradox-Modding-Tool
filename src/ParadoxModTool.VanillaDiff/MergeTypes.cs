namespace ParadoxModTool.VanillaDiff;

/// <summary>Wie ein <see cref="MergeConflict"/> aufgelöst wird, wenn der Nutzer die Mod-Fassung wählt.</summary>
public enum MergeConflictKind
{
    /// <summary>Beide Seiten existieren: die Spiel-Update-Fassung wird durch die Mod-Fassung ersetzt.</summary>
    ReplaceWithMod,

    /// <summary>Der Mod hat die Stelle entfernt, das Spiel-Update sie verändert: Mod-Wahl entfernt sie.</summary>
    RemoveForMod,

    /// <summary>Das Spiel-Update hat die Stelle entfernt, der Mod sie verändert: Mod-Wahl fügt sie wieder ein.</summary>
    InsertForMod
}

/// <summary>Eine Stelle, an der Mod und Spiel-Update dieselbe Struktur unterschiedlich verändert haben.</summary>
public sealed class MergeConflict
{
    /// <summary>Punktgetrennter Anzeigepfad zum betroffenen Schlüssel, z. B. "some_idea.modifier.stability_cost".</summary>
    public required string Path { get; init; }

    /// <summary>Interne, eindeutige Pfadsegmente für <see cref="ConflictResolution"/> (unabhängig von Punkten in Namen).</summary>
    public IReadOnlyList<string> PathSegments { get; init; } = Array.Empty<string>();

    public required string Description { get; init; }

    public MergeConflictKind Kind { get; init; } = MergeConflictKind.ReplaceWithMod;

    /// <summary>
    /// Der Wert auf der Basis-/"gewinnenden"-Seite (zweiter Parameter von <see cref="MergeEngine.Merge"/>),
    /// sofern der Schlüssel dort vorhanden ist. Zusammen mit <see cref="RightValue"/> für eine
    /// UI gedacht, die dem Nutzer beide Fassungen zur manuellen Entscheidung nebeneinander anzeigt
    /// (z. B. Abschnitt 8, Mod-Kompatibilitäts-Checker: "Erkennung, wenn beide Mods für dieselbe
    /// Einstellung unterschiedliche Werte setzen").
    /// </summary>
    public ScriptValue? LeftValue { get; init; }

    /// <summary>Der Wert auf der überlagernden Seite (dritter Parameter von <see cref="MergeEngine.Merge"/>), sofern vorhanden.</summary>
    public ScriptValue? RightValue { get; init; }

    /// <summary>
    /// Der vollständige Mod-Eintrag (samt Original-Quell-Span, siehe <see cref="ScriptParser"/>),
    /// sofern der Mod an dieser Stelle etwas besitzt - für eine Konflikt-UI, die bei "Mod
    /// übernehmen" nicht nur den nackten Wert, sondern den kompletten Original-Eintrag (inkl.
    /// eigener Formatierung) in den Baum einsetzen will (siehe <see cref="ConflictResolution"/>).
    /// </summary>
    public ScriptEntry? ModEntry { get; init; }

    /// <summary>Kopfzeilen-Einträge des Mod-Eintrags (z. B. "template name" vor einem Rumpf-Block), sonst leer.</summary>
    public IReadOnlyList<ScriptEntry> ModHeader { get; init; } = Array.Empty<ScriptEntry>();

    /// <summary>Schlüssel der Einheit, hinter der ein wieder einzufügender Mod-Eintrag stehen soll (nur <see cref="MergeConflictKind.InsertForMod"/>).</summary>
    public string? AnchorKey { get; init; }

    /// <summary>Wie <see cref="ModEntry"/>, aber nur wenn es eine Schlüssel/Wert-Anweisung ist.</summary>
    public ScriptStatement? ModStatement => ModEntry as ScriptStatement;

    /// <summary>Kopie mit anderen Pfadsegmenten und anderem Anker (siehe <see cref="MergeEngine"/>, Übersetzung auf Positionsschlüssel).</summary>
    public MergeConflict WithPath(IReadOnlyList<string> pathSegments, string? anchorKey) => new()
    {
        Path = Path,
        PathSegments = pathSegments,
        Description = Description,
        Kind = Kind,
        LeftValue = LeftValue,
        RightValue = RightValue,
        ModEntry = ModEntry,
        ModHeader = ModHeader,
        AnchorKey = anchorKey,
    };
}

public sealed class MergeResult
{
    public required ScriptBlock MergedRoot { get; init; }
    public required IReadOnlyList<MergeConflict> Conflicts { get; init; }
    public bool HasConflicts => Conflicts.Count > 0;
}

