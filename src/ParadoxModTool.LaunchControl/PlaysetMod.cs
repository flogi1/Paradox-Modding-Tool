namespace ParadoxModTool.LaunchControl;

/// <summary>Ein Eintrag aus <c>orderedListMods</c> eines Playsets: Ordner der Mod und ob sie aktiviert ist. Nur zum Lesen.</summary>
public sealed record PlaysetMod(string Path, bool IsEnabled);
