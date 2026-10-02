namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Zugriff auf die echten EU-V-Dateien dieses Rechners. Fehlt das Spiel, liefern alle Helfer leere Ergebnisse und die Tests kehren sofort zurück.</summary>
internal static class VanillaGui
{
    public const string InstallDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis V";

    public static bool Exists => Directory.Exists(InstallDirectory);

    public static string GameDirectory => Path.Combine(InstallDirectory, "game");

    /// <summary>Gibt es <paramref name="relativePath"/> direkt unter game/ oder unter einem der Unterordner in_game, main_menu, loading_screen, jomini?</summary>
    public static bool GameFileExists(string relativePath)
    {
        var path = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return new[] { "", "in_game", "main_menu", "loading_screen", "jomini" }
            .Any(folder => File.Exists(Path.Combine(GameDirectory, folder, path)));
    }

    public static IReadOnlyList<string> Files() =>
        Exists
            ? Directory.EnumerateFiles(InstallDirectory, "*.gui", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
            : Array.Empty<string>();

    /// <summary>Die echte Installation eines Spiels für Prüfungen der mitgelieferten Bausteine ("eu5" oder "ck3").</summary>
    public static VanillaGame For(string game) => game switch
    {
        "eu5" => new VanillaGame(InstallDirectory, new[] { "", "in_game", "main_menu", "loading_screen", "jomini" }),
        "ck3" => new VanillaGame(@"D:\SteamLibrary\steamapps\common\Crusader Kings III", new[] { "" }),
        _ => throw new ArgumentOutOfRangeException(nameof(game)),
    };
}

/// <summary>Installation eines Spiels: Ordner und die Unterordner von game/, in denen Dateien wie Texturen liegen können.</summary>
internal sealed record VanillaGame(string InstallDirectory, IReadOnlyList<string> GameSubfolders)
{
    public bool Exists => Directory.Exists(InstallDirectory);

    public string GameDirectory => Path.Combine(InstallDirectory, "game");

    public bool GameFileExists(string relativePath)
    {
        var path = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return GameSubfolders.Any(folder => File.Exists(Path.Combine(GameDirectory, folder, path)));
    }

    public IReadOnlyList<string> Files() =>
        Exists
            ? Directory.EnumerateFiles(InstallDirectory, "*.gui", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList()
            : Array.Empty<string>();
}
