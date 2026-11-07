using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.GuiDesign.Checks;

/// <summary>Schwere eines Befunds; die Reihenfolge ist die Sortierung der Liste (Fehler zuerst).</summary>
public enum GuiFindingSeverity
{
    Error,
    Warning,
    Hint,
}

/// <summary>Art eines Befunds der klassischen Prüfungen (Plan 3). Neue Prüfungen ergänzen hier ihre Art.</summary>
public enum GuiFindingKind
{
    SpriteMissing,
    TextureMissing,
    FontMissing,
    FrameOutOfRange,
    AnchorUnknown,
    FormatUnknown,
    Overflow,
    LosesToOther,
    LocalisationMissing,
    ReplacePathSubfolder,
    ScriptWindowMissing,
    ScriptElementUnknown,
    ScriptButtonWithoutEffect,
    CategoryMissing,
    CustomEntryMissing,
    CustomElementMissing,
    CustomKindMismatch,
    CustomOutsideSupportedWindow,

    /// <summary>Eine Prüfung ist mit einem Fehler abgebrochen (Hinweis; ihre Befunde fehlen).</summary>
    CheckFailed,

    /// <summary>Ein Eintrag der Mod in <c>common/custom_gui</c>, dessen Element es gibt, aber ohne <c>scripted = yes</c> (Warnung).</summary>
    CustomNotScripted,

    /// <summary>Jomini: ein Bild, das sich nicht lesen lässt (nicht unterstütztes Format).</summary>
    ImageUnreadable,

    /// <summary>Jomini: ein Text-Icon, das es nicht gibt.</summary>
    TextIconMissing,

    /// <summary>Jomini: das Spiel meldet an dieser Stelle einen Fehler (Anker, Rand oder Position an der falschen Stelle).</summary>
    GameError,

    /// <summary>Jomini: ein Hinweis der Vorschau ohne eigene Art (Annäherung, nicht gemessen); nur ein Hinweis.</summary>
    PreviewNote,
}

/// <summary>
/// Ein Befund: <paramref name="Node"/> ist das Element in der geöffneten Datei (Klick wählt es aus), <paramref name="FilePath"/>/<paramref name="Line"/>
/// seine Stelle. <paramref name="JumpPath"/>/<paramref name="JumpLine"/> zeigen auf eine Skriptdatei; dann öffnet ein Klick diese Datei an der Zeile.
/// Befunde ändern nie Dateien.
/// </summary>
public sealed record GuiFinding(
    GuiFindingKind Kind,
    GuiFindingSeverity Severity,
    string Message,
    GuiNode? Node,
    string? FilePath,
    int Line,
    string? JumpPath = null,
    int JumpLine = 0);

/// <summary>Eine Prüfung der klassischen GUI. Läuft im Hintergrund; sie soll <paramref name="cancellation"/> zwischen größeren Schritten beachten.</summary>
public interface IClassicCheck
{
    IEnumerable<GuiFinding> Run(ClassicCheckContext context, CancellationToken cancellation);
}

/// <summary>
/// Eingaben eines Prüflaufs: Arbeitsbereich, angezeigter Stand der Datei samt Pfad, die Szene der Vorschau (null ohne Vorschau), ob die Datei zur Mod
/// gehört (sonst gibt es keine Befunde) und ob "Überstand markieren" an ist (nur dann Überstand-Hinweise).
/// </summary>
public sealed record ClassicCheckContext(
    GuiWorkspace Workspace,
    GuiDocument Document,
    string DocumentPath,
    GuiSchematicScene? Scene,
    bool IsModFile,
    bool OverflowEnabled);
