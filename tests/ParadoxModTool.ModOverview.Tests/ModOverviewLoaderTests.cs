using System.Text.Json;
using ParadoxModTool.GameProfiles;
using ParadoxModTool.Mods;
using ParadoxModTool.SteamIntegration;
using Xunit;

namespace ParadoxModTool.ModOverview.Tests;

public class ModOverviewLoaderTests : TempDirTests
{
    private static readonly GameProfile Eu4 = new() { Id = "eu4", DisplayName = "Europa Universalis IV", MetadataFile = ModMetadata.ClassicSiblingMetadataFile, SteamAppId = "236850" };

    private static readonly GameProfile Eu5 = new() { Id = "eu5", DisplayName = "Europa Universalis V", MetadataFile = ".metadata/metadata.json", SteamAppId = "3450310" };

    private string Root => Path.Combine(Dir, "appdata");

    private ModRegistry Registry(string gameId) => ModRegistry.Load(Path.Combine(Root, "games", gameId));

    /// <summary>Klassische EU-IV-Mod: Ordner plus Geschwisterdatei <c>&lt;Name&gt;.mod</c>.</summary>
    private string ClassicMod(string name, string supported = "1.37.*", string extra = "")
    {
        var folder = Make("mods4", name);
        File.WriteAllText(Path.Combine(Dir, "mods4", name + ".mod"), $"name=\"{name}\"\nversion=\"1.0\"\nsupported_version=\"{supported}\"\n{extra}");
        return folder;
    }

    /// <summary>EU-V-Mod mit <c>.metadata/metadata.json</c>.</summary>
    private string JsonMod(string folderName, string name, string id, string relationships = "[]")
    {
        var folder = Make("mods5", folderName);
        WriteFile(Path.Combine("mods5", folderName, ".metadata", "metadata.json"),
            $"{{ \"name\": \"{name}\", \"id\": \"{id}\", \"version\": \"1.0\", \"supported_game_version\": \"1.2.*\", \"relationships\": {relationships} }}");
        return folder;
    }

    private OverviewSnapshot Load(params GameProfile[] profiles) =>
        ModOverviewLoader.Load(Root, profiles.Length == 0 ? new[] { Eu4, Eu5 } : profiles, Array.Empty<string>());

    [Fact]
    public void Load_TwoGames_AllModsWithGameIds()
    {
        Registry("eu4").Add("Idea Variation", ClassicMod("Idea Variation"));
        Registry("eu4").Add("BT Addon", ClassicMod("BT Addon"));
        Registry("eu5").Add("IV2", JsonMod("iv2", "The Idea Variation 2", "Idea_Variation_2"));

        var snapshot = Load();

        Assert.Equal(3, snapshot.Mods.Count);
        Assert.Equal(2, snapshot.Mods.Count(m => m.GameId == "eu4"));
        Assert.Equal("eu5", snapshot.Mods.Single(m => m.Mod.Name == "IV2").GameId);
        Assert.Equal(new[] { "eu4", "eu5" }, snapshot.Games.Select(g => g.Id));
        Assert.Empty(snapshot.GameErrors);
    }

    [Fact]
    public void Load_MissingRegistry_NoModsNoError()
    {
        var snapshot = Load();

        Assert.Empty(snapshot.Mods);
        Assert.Empty(snapshot.GameErrors);
    }

    [Fact]
    public void Load_BrokenRegistry_SkipsGameAndReportsIt()
    {
        Registry("eu4").Add("Idea Variation", ClassicMod("Idea Variation"));
        WriteFile(Path.Combine("appdata", "games", "eu5", "mods.json"), "{broken");

        var snapshot = Load();

        Assert.Single(snapshot.Mods);
        var error = Assert.Single(snapshot.GameErrors);
        Assert.Equal("eu5", error.GameId);
        Assert.Equal("Europa Universalis V", error.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public void Load_ModWithoutPaths_HasNoDescriptor()
    {
        WriteFile(Path.Combine("appdata", "games", "eu4", "mods.json"), "{ \"version\": 1, \"mods\": [ { \"id\": \"x\", \"name\": \"Empty\", \"paths\": [] } ] }");

        var mod = Assert.Single(Load().Mods);

        Assert.Null(mod.Metadata);
        Assert.Empty(mod.MissingPaths);
        Assert.Null(mod.Thumbnail);
    }

    [Fact]
    public void Load_MissingFolder_Listed()
    {
        var registry = Registry("eu4");
        var mod = registry.Add("Idea Variation", ClassicMod("Idea Variation"));
        var gone = Make("copies", "gone");
        registry.AddPath(mod.Id, gone);
        Directory.Delete(gone);

        var loaded = Assert.Single(Load().Mods);

        Assert.Equal(new[] { ModPaths.Normalize(gone) }, loaded.MissingPaths);
        Assert.NotNull(loaded.Metadata);
    }

    [Fact]
    public void Load_LastRelease()
    {
        var registry = Registry("eu4");
        var mod = registry.Add("Idea Variation", ClassicMod("Idea Variation"));
        var log = ReleaseLog.Load(registry.DataDirectory(mod.Id));
        log.Add("1.0", "2026-08-01");
        log.Add("1.1", "2026-09-12");
        log.SetSteam("1.1", new SteamPublication { PublishedAt = "2026-09-12", WorkshopId = "111" });
        log.SetGitHub("1.1", new[] { new GitHubPublication { Repo = "flogi/iv", Tag = "v1.1" } });

        var loaded = Assert.Single(Load().Mods);

        Assert.Equal(new OverviewRelease("1.1", "2026-09-12", true, true), loaded.LastRelease);
        Assert.True(loaded.HasGitHubRelease);
        Assert.NotNull(loaded.ReleaseStatus);
    }

    [Fact]
    public void Load_ResolvesDependencies()
    {
        var eu5 = Registry("eu5");
        eu5.Add("IV2", JsonMod("iv2", "The Idea Variation 2", "Idea_Variation_2"));
        eu5.Add("Demo", JsonMod("demo", "Demo", "Demo",
            "[ { \"rel_type\": \"dependency\", \"id\": \"Idea_Variation_2\", \"display_name\": \"Something else\" } ]"));
        var eu4 = Registry("eu4");
        eu4.Add("Idea Variation", ClassicMod("Idea Variation"));
        eu4.Add("Addon", ClassicMod("Addon", extra: "dependencies={ \"idea variation\" \"Beyond Typus\" \"The Idea Variation 2\" }\n"));

        var snapshot = Load();

        var demo = snapshot.Mods.Single(m => m.Mod.Name == "Demo");
        Assert.Equal("IV2", Assert.Single(demo.ResolvedDependencies).Target!.Mod.Name);

        var addon = snapshot.Mods.Single(m => m.Mod.Name == "Addon");
        Assert.Equal(3, addon.ResolvedDependencies.Count);
        Assert.Equal("Idea Variation", addon.ResolvedDependencies[0].Target!.Mod.Name);
        Assert.Null(addon.ResolvedDependencies[1].Target);
        Assert.Null(addon.ResolvedDependencies[2].Target);
    }

    [Fact]
    public void Load_ProjectRoot()
    {
        var registry = Registry("eu4");
        var main = registry.Add("Idea Variation", ClassicMod("Idea Variation"));
        var sub = registry.Add("Addon", ClassicMod("Addon"));
        registry.SetProject(sub.Id, main.Id, ModRole.Submod);
        registry.Add("Single", ClassicMod("Single"));

        var snapshot = Load();

        Assert.Equal("Idea Variation", snapshot.Mods.Single(m => m.Mod.Name == "Addon").ProjectRoot.Mod.Name);
        var single = snapshot.Mods.Single(m => m.Mod.Name == "Single");
        Assert.Same(single, single.ProjectRoot);
    }

    [Fact]
    public void Load_IsPublished()
    {
        var registry = Registry("eu4");
        var steam = registry.Add("Steam", ClassicMod("Steam"));
        registry.SetSteamWorkshopId(steam.Id, "111");
        var hidden = registry.Add("Hidden", ClassicMod("Hidden"));
        registry.SetSteamWorkshopId(hidden.Id, "222");
        var github = registry.Add("GitHub", ClassicMod("GitHub"));
        var log = ReleaseLog.Load(registry.DataDirectory(github.Id));
        log.Add("1.0", "2026-08-01");
        log.SetGitHub("1.0", new[] { new GitHubPublication { Repo = "flogi/x", Tag = "v1.0" } });
        registry.Add("Local", ClassicMod("Local"));
        SteamCacheStore.Apply(new OverviewPaths(Root).SteamFile, new[]
        {
            new WorkshopDetails("111", WorkshopPageState.Ok, "Steam", null, 10, null, null, null),
            new WorkshopDetails("222", WorkshopPageState.Hidden, "Hidden", null, 1, null, null, null),
        }, "A", DateTime.UtcNow);

        var mods = Load().Mods.ToDictionary(m => m.Mod.Name);

        Assert.True(mods["Steam"].IsPublished);
        Assert.False(mods["Hidden"].IsPublished);
        Assert.True(mods["GitHub"].IsPublished);
        Assert.False(mods["Local"].IsPublished);
    }

    [Fact]
    public void Load_WorkshopIdFromDescriptorWhenRegistryHasNone()
    {
        var registry = Registry("eu4");
        registry.Add("Launcher Upload", ClassicMod("Launcher Upload", extra: "remote_file_id=\"333\"\n"));
        var own = registry.Add("Own Id", ClassicMod("Own Id", extra: "remote_file_id=\"444\"\n"));
        registry.SetSteamWorkshopId(own.Id, "555");
        SteamCacheStore.Apply(new OverviewPaths(Root).SteamFile, new[]
        {
            new WorkshopDetails("333", WorkshopPageState.Ok, "Launcher Upload", null, 7, null, null, null),
        }, "A", DateTime.UtcNow);

        var mods = Load().Mods.ToDictionary(m => m.Mod.Name);

        Assert.Equal("333", mods["Launcher Upload"].WorkshopId);
        Assert.True(mods["Launcher Upload"].IsPublished);
        Assert.Equal(7, mods["Launcher Upload"].Steam!.Subscriptions);
        Assert.Equal("https://steamcommunity.com/sharedfiles/filedetails/?id=333", mods["Launcher Upload"].Links.Workshop);
        Assert.Equal("555", mods["Own Id"].WorkshopId);
    }

    [Fact]
    public void Load_WorkshopIdFromTheInnerDescriptorWhenTheSiblingFileHasNone()
    {
        var folder = ClassicMod("Inner Only");
        File.WriteAllText(Path.Combine(folder, "descriptor.mod"), "name=\"Inner Only\"\nversion=\"1.0\"\nremote_file_id=\"666\"\n");
        Registry("eu4").Add("Inner Only", folder);

        var mod = Load().Mods.Single(m => m.Mod.Name == "Inner Only");

        Assert.Equal("666", mod.WorkshopId);
    }

    [Fact]
    public void Load_WorkshopIdFromOverviewLinksAsLastResort()
    {
        var mod = Registry("eu5").Add("IV2", JsonMod("iv2", "The Idea Variation 2", "Idea_Variation_2"));
        ModLinkStore.SetWorkshopId(new OverviewPaths(Root).LinksFile, "eu5", mod.Id, "3599737490");

        var loaded = Assert.Single(Load().Mods);

        Assert.Equal("3599737490", loaded.WorkshopId);
        Assert.True(loaded.IsPublished);
    }

    [Fact]
    public void Load_VersionSupport()
    {
        var install = Make("EU4");
        Make("EU4", "common");
        File.WriteAllText(Path.Combine(install, "launcher-settings.json"), "{ \"rawVersion\": \"v1.37.5.0\" }");
        WriteFile(Path.Combine("appdata", "games", "eu4", "session.json"), JsonSerializer.Serialize(new { GameExecutablePath = Path.Combine(install, "eu4.exe") }));
        Registry("eu4").Add("Current", ClassicMod("Current", "1.37.*"));
        Registry("eu4").Add("Old", ClassicMod("Old", "1.36.*"));
        Registry("eu5").Add("IV2", JsonMod("iv2", "The Idea Variation 2", "Idea_Variation_2"));

        var snapshot = Load();
        var mods = snapshot.Mods.ToDictionary(m => m.Mod.Name);

        Assert.Equal(VersionSupport.Supported, mods["Current"].VersionSupport);
        Assert.Equal(VersionSupport.Outdated, mods["Old"].VersionSupport);
        Assert.Equal(VersionSupport.Unknown, mods["IV2"].VersionSupport);
        Assert.Equal("v1.37.5.0", snapshot.Games.Single(g => g.Id == "eu4").InstalledVersion);
        Assert.Null(snapshot.Games.Single(g => g.Id == "eu5").InstalledVersion);
    }

    [Fact]
    public void Load_LinksAndSteamFromCaches()
    {
        var registry = Registry("eu4");
        var mod = registry.Add("Idea Variation", ClassicMod("Idea Variation"));
        registry.SetSteamWorkshopId(mod.Id, "111");
        var paths = new OverviewPaths(Root);
        ModLinkStore.Set(paths.LinksFile, "eu4", mod.Id, "https://forum.paradoxplaza.com/x", null, null);
        var fetched = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        SteamCacheStore.Apply(paths.SteamFile, new[] { new WorkshopDetails("111", WorkshopPageState.Ok, "IV", null, 48213, 2104, 391550, null) }, "A", fetched);

        var snapshot = Load();
        var loaded = Assert.Single(snapshot.Mods);

        Assert.Equal("https://forum.paradoxplaza.com/x", loaded.Links.Forum);
        Assert.Equal("https://steamcommunity.com/sharedfiles/filedetails/?id=111", loaded.Links.Workshop);
        Assert.Equal(48213, loaded.Steam!.Subscriptions);
        Assert.Equal(fetched, snapshot.SteamFetchedUtc);
    }
}
