namespace ParadoxModTool.LaunchControl;

public enum PlaysetStatus
{
    Ok,
    GameRunning,
    LauncherRunning,
    FileMissing,
    InvalidFile,
    InvalidName,
    DuplicateName,
    ModMissing,
    DuplicateMod,
    PlaysetMissing,
    WriteFailed,
    LoadFileFailed,
    BackupMissing,
}

/// <summary>Ergebnis einer Playset-Änderung. <c>Detail</c> = Pfad, Name oder Fehlermeldung; die Texte liefert <see cref="PlaysetTexts.Describe"/>.</summary>
public sealed record PlaysetResult(PlaysetStatus Status, string? Detail = null)
{
    public bool Success => Status == PlaysetStatus.Ok;

    public static PlaysetResult Ok { get; } = new(PlaysetStatus.Ok);
}
