using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Syntax;

namespace ParadoxModTool.Editor.Gui;

public enum GuiDesignerActionKind
{
    Insert,
    Delete,
    Duplicate,
    MoveUp,
    MoveDown,
    Move,
    WrapIn,
    OverrideBlock,
    SetProperty,
    SetValue,
    RemoveProperty,
    DeriveType,
    ExtractType,
    Retype,
}

/// <summary>
/// Designer-Aktion, wie Baum, Palette und Eigenschaftenfeld sie melden. Die Knoten stammen aus dem angezeigten, womöglich älteren Parse; der
/// Controller holt sie vor dem Schreiben per Pfad in den aktuellen Text (Plan-Ruling 11).
/// </summary>
public sealed record GuiDesignerAction(GuiDesignerActionKind Kind, GuiNode? Node)
{
    /// <summary>Insert und Move: wohin, bezogen auf <see cref="Target"/>.</summary>
    public GuiPlacement Placement { get; init; } = GuiPlacement.AtSelection;

    /// <summary>Insert und Move: Zielknoten (Auswahl bzw. Eintrag unter der Maus); null = Dateiebene.</summary>
    public GuiNode? Target { get; init; }

    /// <summary>Ablage: senkrechte Lage der Maus im Zieleintrag, 0 oben bis 1 unten.</summary>
    public double DropY { get; init; } = 0.5;

    /// <summary>Insert: Eintrag der Palette.</summary>
    public GuiPaletteEntry? Entry { get; init; }

    /// <summary>Insert: fertiger Code (Baustein aus dem Fenster "Bausteine"); geht vor dem Schnipsel des Palette-Eintrags.</summary>
    public string? Code { get; init; }

    /// <summary>Eigenschaftsname (SetProperty), Box-Schlüssel (WrapIn), Blockname (OverrideBlock), neuer Typname (DeriveType, ExtractType) oder neuer Schlüssel (Retype).</summary>
    public string? Name { get; init; }

    /// <summary>SetProperty und SetValue: der Wert als Code; DeriveType und ExtractType: Name eines neu anzulegenden types-Containers.</summary>
    public string? Value { get; init; }

    /// <summary>
    /// Die Aktion gilt dem gewählten Element (Tasten im Baum, Einfügen aus der Palette an der Auswahl). Stammt der Knoten noch aus dem Stand
    /// vor der letzten Aktion (Baum noch nicht neu aufgebaut), nimmt der Controller das Element am Cursor; dort steht das Ergebnis der letzten
    /// Aktion (Plan-Ruling 11).
    /// </summary>
    public bool FromSelection { get; init; }

    /// <summary>OverrideBlock: den bisher wirksamen Inhalt übernehmen (sonst wird der Block geleert; Plan-Ruling 14).</summary>
    public bool KeepContent { get; init; }

    /// <summary>OverrideBlock: Knoten, dessen Inhalt übernommen wird (block bzw. blockoverride in einer Typdefinition); geht vor <see cref="KeepContent"/>.</summary>
    public GuiNode? ContentFrom { get; init; }

    /// <summary>OverrideBlock: der Cursor kommt danach auf die erste Zeile mit diesem Schlüssel (das angepasste Element).</summary>
    public string? FocusKey { get; init; }

    /// <summary>DeriveType: weitere Stellen derselben Datei, die ebenfalls auf den neuen Typ umgestellt werden (Plan 3c Ruling 3).</summary>
    public IReadOnlyList<GuiNode> Others { get; init; } = Array.Empty<GuiNode>();
}
