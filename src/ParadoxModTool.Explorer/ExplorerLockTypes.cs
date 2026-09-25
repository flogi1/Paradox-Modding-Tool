namespace ParadoxModTool.Explorer;

public enum ExplorerLockKind
{
    Unlocked,

    /// <summary>Der Pfad selbst hat einen Sperr-Eintrag.</summary>
    Locked,

    /// <summary>Ein Ordner darüber ist gesperrt (Pfad in <see cref="ExplorerLockState.ByPath"/>).</summary>
    LockedByParent,
}

/// <summary>Sperrzustand eines Pfads in einer Mod. Der Explorer kennt keine Mods: das Hauptfenster liefert den Zustand (siehe <see cref="ExplorerPanel.PathLockStateProvider"/>).</summary>
public sealed record ExplorerLockState(ExplorerLockKind Kind, string? ByPath);

public sealed class ExplorerPathLockEventArgs : EventArgs
{
    public required string Path { get; init; }

    public required bool IsDirectory { get; init; }

    /// <summary><see langword="true"/> = sperren, <see langword="false"/> = entsperren.</summary>
    public required bool Lock { get; init; }
}
