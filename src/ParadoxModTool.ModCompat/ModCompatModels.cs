using ParadoxModTool.VanillaDiff;

namespace ParadoxModTool.ModCompat;

/// <summary>
/// Eine Datei, die sowohl von Mod A als auch von Mod B gegenüber Vanilla verändert wird
/// (die "Übersicht" aus Abschnitt 8, Punkt 1).
/// </summary>
public sealed class CommonlyModifiedFile
{
    /// <summary>Pfad relativ zur Spielwurzel, identisch für Vanilla/Mod A/Mod B.</summary>
    public required string RelativePath { get; init; }

    public required string VanillaFullPath { get; init; }
    public required string ModAFullPath { get; init; }
    public required string ModBFullPath { get; init; }
}

/// <summary>
/// Der "Parallele Diff" aus Abschnitt 8, Punkt 2: zeigt für eine gemeinsam betroffene Datei,
/// wie stark jeder der beiden Mods von Vanilla abweicht — als zwei unabhängige, nebeneinander
/// darstellbare Diff-Bäume (siehe <see cref="ScriptDiffer"/>).
/// </summary>
public sealed class ParallelDiffResult
{
    public required IReadOnlyList<EntryDiff> ModADiff { get; init; }
    public required IReadOnlyList<EntryDiff> ModBDiff { get; init; }
}

/// <summary>
/// Eine Stelle, an der Mod A und Mod B dieselbe Einstellung unterschiedlich setzen
/// (Abschnitt 8, Punkt 3: "inklusive Erkennung, wenn beide Mods für dieselbe Einstellung
/// unterschiedliche Werte setzen"). Wird dem Nutzer zur manuellen Entscheidung vorgelegt —
/// im Gegensatz zu <see cref="ParadoxModTool.VanillaDiff.MergeConflict"/> werden hier bewusst
/// BEIDE Werte mitgeliefert, da keine Seite automatisch "gewinnen" soll.
/// </summary>
public sealed class ModCompatConflict
{
    /// <summary>Punktgetrennter Pfad zum betroffenen Schlüssel, z. B. "some_idea.modifier.stability_cost".</summary>
    public required string Path { get; init; }

    public required string Description { get; init; }

    /// <summary>Wert, den Mod A für diesen Schlüssel setzt (null, wenn Mod A den Schlüssel entfernt hat).</summary>
    public ScriptValue? ModAValue { get; init; }

    /// <summary>Wert, den Mod B für diesen Schlüssel setzt (null, wenn Mod B den Schlüssel entfernt hat).</summary>
    public ScriptValue? ModBValue { get; init; }
}

/// <summary>
/// Ergebnis der automatischen Zusammenführung aus Abschnitt 8, Stufe 1 (MVP): "automatisches
/// Zusammenführen bei eindeutig nicht überlappenden Änderungen; überlappende Stellen werden
/// markiert und dem Nutzer zur manuellen Entscheidung vorgelegt". <see cref="MergedRoot"/>
/// enthält an überlappenden Stellen provisorisch die Fassung von Mod A — das eigentliche
/// Ergebnis für solche Stellen sind die <see cref="Conflicts"/>, die dem Nutzer beide
/// Fassungen zur Wahl vorlegen; <see cref="MergedRoot"/> darf an diesen Stellen nicht
/// ungeprüft übernommen werden.
/// </summary>
public sealed class ModCompatMergeResult
{
    public required ScriptBlock MergedRoot { get; init; }
    public required IReadOnlyList<ModCompatConflict> Conflicts { get; init; }
    public bool HasConflicts => Conflicts.Count > 0;
}
