using ParadoxModTool.FlagTracker;
using ParadoxModTool.ScriptCheck;

namespace ParadoxModTool.GfxAudit;

/// <summary>Art eines Fundes der GFX-Prüfung.</summary>
public enum GfxFindingKind
{
    UnusedImage,
    UnusedSprite,
    MissingTexture,
    MissingSprite,
    DuplicateSprite,
}

/// <summary>Ein Fund in der gewählten Mod.</summary>
/// <param name="Name">
/// Unbenutztes Bild: logischer Pfad; unbenutzter, fehlender oder doppelter Sprite: Sprite-Name; fehlende Textur: der normalisierte Pfad
/// (<see cref="GfxNames.NormalizeSpriteTexture"/> für Sprite-Texturen, sonst <see cref="GfxNames.NormalizePath"/>).
/// </param>
/// <param name="Location">Ort in der gewählten Mod: das Bild (Zeile 0), die Sprite-Definition, der Texturschlüssel bzw. der Verweis.</param>
/// <param name="Related">
/// Unbenutztes Bild: die unbenutzten Sprites der Mod, die darauf zeigen; fehlende Textur aus einem Sprite: dessen Name; doppelter Sprite: alle
/// Definitionen als <c>pfad:zeile (Herkunft)</c>, die gewinnende zuerst; sonst leer.
/// </param>
/// <param name="Key">Schlüssel für die Ausblendliste: <c>Art|Name</c>.</param>
public sealed record GfxFinding(
    GfxFindingKind Kind,
    FindingSeverity Severity,
    string Name,
    SiteLocation Location,
    IReadOnlyList<string> Related,
    string Key)
{
    /// <summary>Fund mit <see cref="Key"/> = <c>Art|Name</c>.</summary>
    public static GfxFinding Create(GfxFindingKind kind, FindingSeverity severity, string name, SiteLocation location, IReadOnlyList<string> related) =>
        new(kind, severity, name, location, related, kind + "|" + name);
}

/// <summary>Ergebnis der Auswertung.</summary>
/// <param name="Findings">Funde nach Schwere, relativem Pfad und Zeile sortiert.</param>
public sealed record GfxAuditResult(IReadOnlyList<GfxFinding> Findings);
