namespace ParadoxModTool.App;

/// <summary>Die Angaben des Spiels dieses Fensters, die das Veröffentlichen auf Steam braucht: der Ordner der Spieldaten (<c>steam.json</c> liegt dort), die Steam-AppID und der Anzeigename.</summary>
/// <param name="SteamAppId">Aus dem Spielprofil; <see langword="null"/>, wenn das Profil keine hat (dann ist das Veröffentlichen gesperrt).</param>
public sealed record SteamPublishHost(string GameDirectory, string? SteamAppId, string GameName);
