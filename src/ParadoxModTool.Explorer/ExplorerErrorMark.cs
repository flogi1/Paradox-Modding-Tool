namespace ParadoxModTool.Explorer;

/// <summary>Fehlermarkierung eines Explorer-Eintrags (Fehlerprotokoll Plan 2b): kurzer Text wie "⚠3" und Tooltip. Der Explorer kennt das Fehlerprotokoll nicht; das Hauptfenster liefert sie.</summary>
public sealed record ExplorerErrorMark(string Badge, string Tooltip);
