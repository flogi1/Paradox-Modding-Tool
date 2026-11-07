using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.ReferenceTree;

/// <summary>Art eines Knotens im Verweisbaum.</summary>
public enum RefNodeKind
{
    Root,
    Branch,
    Group,
    Site,
}

/// <summary>Up = Aufrufer-Richtung, Down = Gerufene-Richtung.</summary>
public enum RefDirection
{
    Up,
    Down,
}

/// <summary>Set/Read/Remove: Äste einer Flag-Wurzel. UsedBy/Uses: Äste einer Baustein-Wurzel. Sets/Reads: Unteräste unter Uses bzw. unter einem Down-Knoten.</summary>
public enum RefBranch
{
    Set,
    Read,
    Remove,
    UsedBy,
    Uses,
    Sets,
    Reads,
}

/// <summary>Zugeklappte Herkunftsgruppe unter einem Ast oder Down-Knoten.</summary>
public enum RefGroup
{
    OtherMods,
    Vanilla,
}

/// <summary>Ein Knoten des Verweisbaums.</summary>
/// <param name="NodeKind">Wurzel, Ast, Herkunftsgruppe oder Stelle.</param>
/// <param name="Name">Root: gesuchter Name; Site Up: Besitzer (leer ohne Besitzer); Site Down: Ziel; Flag-Stelle unter Sets/Reads: Flag-Name; Branch/Group: leer.</param>
/// <param name="CheckKind">Art bei Bausteinen/Events.</param>
/// <param name="FlagKind">Art bei Variablen/Flags.</param>
/// <param name="Role">Nur Flag-Stellen.</param>
/// <param name="Location">Nur Site.</param>
/// <param name="Origin">Herkunft (bei Stellen über <see cref="ReferenceSources.OriginOf"/>, bei Flag-Stellen aus der Fundstelle).</param>
/// <param name="OriginName">Anzeigename der Herkunft.</param>
/// <param name="Direction">Up = Aufrufer-Richtung, Down = Gerufene-Richtung.</param>
/// <param name="Branch">Nur Branch.</param>
/// <param name="Group">Nur Group.</param>
/// <param name="Count">Branch/Group: Zahl der Stellen darin.</param>
/// <param name="Expandable">Der Knoten hat (möglicherweise) Kinder.</param>
/// <param name="Partial">„Nur Event-Aufrufe bekannt“: die Aufrufer des Namens sind unvollständig, weil er nicht in der gewählten Mod definiert ist.</param>
/// <param name="Cycle">Der Name steht schon weiter oben im Pfad (↻); dann nicht aufklappbar.</param>
/// <param name="Path">Schlüssel der Namen von der Wurzel bis hier (für Zirkel), Events über <see cref="EventIds.Key"/>.</param>
public sealed record RefNode(
    RefNodeKind NodeKind,
    string Name,
    CheckKind? CheckKind,
    FlagKind? FlagKind,
    FlagRole? Role,
    SiteLocation? Location,
    FlagOrigin Origin,
    string OriginName,
    RefDirection Direction,
    RefBranch? Branch,
    RefGroup? Group,
    int Count,
    bool Expandable,
    bool Partial,
    bool Cycle,
    IReadOnlyList<string> Path);
