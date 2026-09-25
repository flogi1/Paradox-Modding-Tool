namespace ParadoxModTool.Explorer;

/// <summary>Was der Nutzer im Explorer-Kontextmenü mit einer Datei oder einem Ordner eines Mod-Projekts tun will.</summary>
public enum ExplorerBranchAction
{
    Copy,
    Move,
    Compare,
}

/// <summary>Eine Ziel-Mod im Untermenü (ID und Anzeigename).</summary>
public sealed record ExplorerBranchTarget(string ModId, string Name);

/// <summary>Die Ziele je Aktion. Der Explorer kennt keine Mods: das Hauptfenster liefert die Listen (siehe <see cref="ExplorerPanel.BranchTargetsProvider"/>).</summary>
public sealed record ExplorerBranchTargets(
    IReadOnlyList<ExplorerBranchTarget> Copy,
    IReadOnlyList<ExplorerBranchTarget> Move,
    IReadOnlyList<ExplorerBranchTarget> Compare);

public sealed class ExplorerBranchActionEventArgs : EventArgs
{
    public required string Path { get; init; }

    public required bool IsDirectory { get; init; }

    public required ExplorerBranchAction Action { get; init; }

    public required string TargetModId { get; init; }
}
