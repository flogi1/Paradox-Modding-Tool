namespace ParadoxModTool.ModOverview;

/// <summary>
/// Die globalen Dateien der Übersicht "Meine Mods" unter <c>&lt;AppData&gt;\ParadoxModTool\overview\</c>. Sie gehören keinem Spiel; jeder Prozess
/// schreibt sie nur unter der Sperre der Datei.
/// </summary>
/// <param name="appDataRoot">Der Datenordner des Programms (<c>SessionRepository.Root</c>).</param>
public sealed class OverviewPaths(string appDataRoot)
{
    public string Directory { get; } = Path.Combine(appDataRoot, "overview");

    public string LinksFile => Path.Combine(Directory, "links.json");

    public string SteamFile => Path.Combine(Directory, "steam.json");

    public string ThumbsDirectory => Path.Combine(Directory, "thumbs");
}
