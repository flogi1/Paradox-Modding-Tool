using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Explorer.Tests;

/// <summary>Favoriten je Spiel: Zuordnung alter Favoriten zu Spielen und einmalige Übernahme aus der gemeinsamen Datei.</summary>
public class ExplorerFavoritesGameTests : IDisposable
{
    // Werte wie in profiles/*.json.
    private static readonly IReadOnlyList<GameProfile> Profiles = new[]
    {
        Profile("ck3", "Crusader Kings III", "1158310"),
        Profile("eu4", "Europa Universalis IV", "236850"),
        Profile("eu5", "Europa Universalis V", "3450310"),
        Profile("vic3", "Victoria 3", "529340"),
    };

    private readonly string _baseFolder = Path.Combine(Path.GetTempPath(), "pmt-explorer-favorites-games-" + Guid.NewGuid());

    public void Dispose()
    {
        try
        {
            Directory.Delete(_baseFolder, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    private static GameProfile Profile(string id, string displayName, string appId) => new()
    {
        Id = id,
        DisplayName = displayName,
        SteamAppId = appId,
        DefaultModRootTemplate = $"%USERPROFILE%/Documents/Paradox Interactive/{displayName}/mod",
    };

    [Fact]
    public void GamesForPath_Eu5Installationsordner_NurEu5()
    {
        var games = ExplorerFavoritesStore.GamesForPath(@"C:\Program Files (x86)\Steam\steamapps\common\Europa Universalis V\game\in_game", Profiles);

        Assert.Equal(new[] { "eu5" }, games);
    }

    [Fact]
    public void GamesForPath_Ck3Dokumentenordner_NurCk3()
    {
        var games = ExplorerFavoritesStore.GamesForPath(@"C:\Users\x\Documents\Paradox Interactive\Crusader Kings III\mod\foo", Profiles);

        Assert.Equal(new[] { "ck3" }, games);
    }

    [Fact]
    public void GamesForPath_Eu4Pfad_NurEu4NichtEu5()
    {
        var games = ExplorerFavoritesStore.GamesForPath(@"D:\SteamLibrary\steamapps\common\Europa Universalis IV\common\ideas", Profiles);

        Assert.Equal(new[] { "eu4" }, games);
    }

    [Fact]
    public void GamesForPath_WorkshopOrdner_UeberSteamAppId()
    {
        var games = ExplorerFavoritesStore.GamesForPath(@"C:\Program Files (x86)\Steam\steamapps\workshop\content\236850\123", Profiles);

        Assert.Equal(new[] { "eu4" }, games);
    }

    [Fact]
    public void GamesForPath_SchraegstricheUndGrossKlein_WerdenIgnoriert()
    {
        var games = ExplorerFavoritesStore.GamesForPath("c:/users/x/documents/paradox interactive/victoria 3/mod", Profiles);

        Assert.Equal(new[] { "vic3" }, games);
    }

    [Fact]
    public void GamesForPath_ZahlNurAlsOrdnernameOhneWorkshop_KeinTreffer()
    {
        var games = ExplorerFavoritesStore.GamesForPath(@"D:\Backups\236850\notes", Profiles);

        Assert.Equal(Profiles.Select(p => p.Id), games);
    }

    [Fact]
    public void GamesForPath_FremderPfad_GehoertZuAllenSpielen()
    {
        var games = ExplorerFavoritesStore.GamesForPath(@"D:\Stuff\notes", Profiles);

        Assert.Equal(Profiles.Select(p => p.Id), games);
    }

    private string LegacyPath => Path.Combine(_baseFolder, "explorer-favorites.json");

    private string GamePath(string gameId) => ExplorerFavoritesStore.GetFavoritesFilePath(gameId, _baseFolder);

    [Fact]
    public void GetFavoritesFilePath_LiegtImSpielordner()
    {
        Assert.Equal(Path.Combine(_baseFolder, "games", "eu5", "explorer-favorites.json"), GamePath("eu5"));
    }

    [Fact]
    public void LoadForGame_OhneSpielDatei_UebernimmtPassendeAlteFavoritenUndSpeichertSie()
    {
        var eu5 = @"C:\Steam\steamapps\common\Europa Universalis V\game\in_game";
        var ck3 = @"C:\Users\x\Documents\Paradox Interactive\Crusader Kings III\mod\foo";
        var neutral = @"D:\Stuff\notes";
        ExplorerFavoritesStore.Save(new[] { eu5, ck3, neutral }, LegacyPath);

        var loaded = ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder);

        Assert.Equal(new[] { eu5, neutral }, loaded);
        Assert.Equal(new[] { eu5, neutral }, ExplorerFavoritesStore.TryLoad(GamePath("eu5")));
    }

    [Fact]
    public void LoadForGame_LeereSpielDatei_UebernimmtNichts()
    {
        ExplorerFavoritesStore.Save(new[] { @"D:\Stuff\notes" }, LegacyPath);
        ExplorerFavoritesStore.Save(Array.Empty<string>(), GamePath("eu5"));

        var loaded = ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder);

        Assert.Empty(loaded);
        Assert.Empty(ExplorerFavoritesStore.TryLoad(GamePath("eu5")));
    }

    [Fact]
    public void LoadForGame_VorhandeneSpielDatei_WirdGeladen()
    {
        ExplorerFavoritesStore.Save(new[] { @"D:\Stuff\notes" }, LegacyPath);
        ExplorerFavoritesStore.Save(new[] { @"C:\Mods\eigener" }, GamePath("eu5"));

        var loaded = ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder);

        Assert.Equal(new[] { @"C:\Mods\eigener" }, loaded);
    }

    [Fact]
    public void LoadForGame_OhneAlteUndOhneSpielDatei_LeerUndSchreibtNichts()
    {
        var loaded = ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder);

        Assert.Empty(loaded);
        Assert.False(File.Exists(GamePath("eu5")));
    }

    [Fact]
    public void LoadForGame_LaesstAlteDateiUnveraendert()
    {
        ExplorerFavoritesStore.Save(new[] { @"C:\Users\x\Documents\Paradox Interactive\Crusader Kings III\mod\foo", @"D:\Stuff\notes" }, LegacyPath);
        var before = File.ReadAllBytes(LegacyPath);

        ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder);
        ExplorerFavoritesStore.LoadForGame("ck3", Profiles, _baseFolder);

        Assert.Equal(before, File.ReadAllBytes(LegacyPath));
    }

    [Fact]
    public void LoadForGame_KaputteAlteDatei_GiltAlsLeer_SpielDateiWirdAngelegt()
    {
        Directory.CreateDirectory(_baseFolder);
        File.WriteAllText(LegacyPath, "[ \"D:\\\\Stuff\\\\notes\", ");

        Assert.Empty(ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder));
        Assert.True(File.Exists(GamePath("eu5")));
    }

    [Fact]
    public void LoadForGame_GesperrteAlteDatei_WirftUndSchreibtNichts()
    {
        ExplorerFavoritesStore.Save(new[] { @"D:\Stuff\notes" }, LegacyPath);

        using (new FileStream(LegacyPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder));
        }

        Assert.False(File.Exists(GamePath("eu5")));
    }

    [Fact]
    public void LoadForGame_SchreibenDerSpielDateiScheitert_WirftStattUebernahmeZuVerlieren()
    {
        ExplorerFavoritesStore.Save(new[] { @"D:\Stuff\notes" }, LegacyPath);
        // Eine Datei an der Stelle des Spielordners: die Spieldatei lässt sich nicht anlegen.
        var blocker = Path.GetDirectoryName(GamePath("eu5"))!;
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
        File.WriteAllText(blocker, "x");

        Assert.ThrowsAny<IOException>(() => ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder));

        // Beim nächsten Start (Hindernis weg) wird übernommen.
        File.Delete(blocker);
        Assert.Equal(new[] { @"D:\Stuff\notes" }, ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder));
    }

    [Fact]
    public void LoadForGame_DanachSaveMerged_BehaeltUebernommeneFavoriten()
    {
        var eu5 = @"C:\Steam\steamapps\common\Europa Universalis V\game\in_game";
        var neutral = @"D:\Stuff\notes";
        ExplorerFavoritesStore.Save(new[] { eu5, neutral }, LegacyPath);
        var baseline = ExplorerFavoritesStore.LoadForGame("eu5", Profiles, _baseFolder);

        var current = baseline.Append(@"C:\Mods\neu").ToList();
        var merged = ExplorerFavoritesStore.SaveMerged(baseline, current, GamePath("eu5"));

        Assert.Equal(new[] { eu5, neutral, @"C:\Mods\neu" }, merged);
    }
}
