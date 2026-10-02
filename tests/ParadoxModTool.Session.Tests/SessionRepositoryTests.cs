using System.Reflection;
using System.Text.Json.Nodes;
using Xunit;

namespace ParadoxModTool.Session.Tests;

public sealed class SessionRepositoryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pmt-repo-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    private SessionRepository Repo() => new(_root);

    private static DocumentSessionState Doc(string path) => new() { FilePath = path };

    [Fact]
    public void Partition_AssignsEveryPropertyToExactlyOneSide()
    {
        var all = typeof(AppSessionState).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToHashSet();
        var global = SessionPartition.GlobalNames.ToList();
        var game = SessionPartition.GameNames.ToList();

        Assert.Empty(global.Intersect(game));
        Assert.Equal(all, global.Concat(game).ToHashSet());
        Assert.Equal(global.Count + game.Count, all.Count);
    }

    [Fact]
    public void MergeGlobal_KeepsDiskValueForPropertiesThisProcessDidNotChange()
    {
        var baseline = new JsonObject { ["ThemeId"] = "light", ["MultiRowTabs"] = false };
        var current = new JsonObject { ["ThemeId"] = "light", ["MultiRowTabs"] = true };
        var disk = new JsonObject { ["ThemeId"] = "dark", ["MultiRowTabs"] = false };

        var merged = SessionPartition.MergeGlobal(disk, baseline, current);

        Assert.Equal("dark", merged["ThemeId"]!.GetValue<string>());
        Assert.True(merged["MultiRowTabs"]!.GetValue<bool>());
    }

    [Fact]
    public void MergeGlobal_SamePropertyChangedByBoth_ThisProcessWins()
    {
        var baseline = new JsonObject { ["ThemeId"] = "light" };
        var current = new JsonObject { ["ThemeId"] = "builtin-dark" };
        var disk = new JsonObject { ["ThemeId"] = "custom-1" };

        var merged = SessionPartition.MergeGlobal(disk, baseline, current);

        Assert.Equal("builtin-dark", merged["ThemeId"]!.GetValue<string>());
    }

    [Fact]
    public void SaveAndLoad_SplitsGlobalAndGameStateIntoSeparateFiles()
    {
        var repo = Repo();
        var loaded = repo.Load("eu5");
        loaded.State.ThemeId = "builtin-dark";
        loaded.State.GameExecutablePath = @"C:\Games\eu5.exe";
        loaded.State.LeftPane.Documents.Add(Doc(@"C:\mod\a.txt"));

        repo.Save("eu5", loaded);

        var settings = JsonNode.Parse(File.ReadAllText(repo.SettingsFilePath))!.AsObject();
        var game = JsonNode.Parse(File.ReadAllText(repo.GameSessionFilePath("eu5")))!.AsObject();
        Assert.Equal("builtin-dark", settings["ThemeId"]!.GetValue<string>());
        Assert.False(settings.ContainsKey("GameExecutablePath"));
        Assert.False(settings.ContainsKey("LeftPane"));
        Assert.Equal(@"C:\Games\eu5.exe", game["GameExecutablePath"]!.GetValue<string>());
        Assert.False(game.ContainsKey("ThemeId"));

        var again = repo.Load("eu5").State;
        Assert.Equal("builtin-dark", again.ThemeId);
        Assert.Equal(@"C:\Games\eu5.exe", again.GameExecutablePath);
        Assert.Equal(@"C:\mod\a.txt", again.LeftPane.Documents.Single().FilePath);
    }

    [Fact]
    public void CompatchLastStart_IsKeptPerGame()
    {
        var repo = Repo();
        var eu = repo.Load("eu4");
        eu.State.CompatchLastStart = new CompatchStartMemory { FolderA = @"C:\a", FolderB = @"C:\b", TargetMode = "existing", TargetModId = "abc", NewName = "X + Y Kompatch", BLoadsAfterA = false, Cautious = true, SkipLocked = true, HiddenKinds = new List<string> { "Audio" }, MainReplacesVanilla = true, FillMissingFromMain = true };
        repo.Save("eu4", eu);

        var again = repo.Load("eu4").State.CompatchLastStart!;

        Assert.Equal(@"C:\a", again.FolderA);
        Assert.Equal("existing", again.TargetMode);
        Assert.True(again.MainReplacesVanilla);
        Assert.True(again.FillMissingFromMain);
        Assert.Equal("abc", again.TargetModId);
        Assert.False(again.BLoadsAfterA);
        Assert.True(again.Cautious);
        Assert.True(again.SkipLocked);
        Assert.Equal(new[] { "Audio" }, again.HiddenKinds);
        Assert.Null(repo.Load("eu5").State.CompatchLastStart);
    }

    [Fact]
    public void FolderFilters_AreKeptPerGame()
    {
        var repo = Repo();
        var eu = repo.Load("eu4");
        eu.State.FolderFilters = new List<ParadoxModTool.Categories.FolderFilterDefinition>
        {
            new() { Id = "f1", Name = "Idea Variation Hauptmod", FolderPath = @"C:\mod\Idea Variation", ShowInBar = false },
        };
        repo.Save("eu4", eu);

        var again = repo.Load("eu4").State.FolderFilters.Single();

        Assert.Equal("Idea Variation Hauptmod", again.Name);
        Assert.Equal(@"C:\mod\Idea Variation", again.FolderPath);
        Assert.False(again.ShowInBar);
        Assert.Empty(repo.Load("eu5").State.FolderFilters);
    }

    [Fact]
    public void Games_KeepTheirStateSeparateButShareGlobalSettings()
    {
        var repo = Repo();
        var eu = repo.Load("eu5");
        eu.State.ThemeId = "builtin-dark";
        eu.State.LeftPane.Documents.Add(Doc(@"C:\eu5\a.txt"));
        eu.State.ExplorerRoots.Add(new ExplorerRootState { Path = @"C:\eu5", Kind = "ModFolder" });
        repo.Save("eu5", eu);

        var ck = repo.Load("ck3");

        Assert.Equal("builtin-dark", ck.State.ThemeId);
        Assert.Empty(ck.State.LeftPane.Documents);
        Assert.Empty(ck.State.ExplorerRoots);
    }

    [Fact]
    public void CompatchDefaultView_IsSharedByAllGames()
    {
        var repo = Repo();
        var eu = repo.Load("eu5");
        Assert.Equal("A", eu.State.CompatchDefaultView);

        eu.State.CompatchDefaultView = "C";
        repo.Save("eu5", eu);

        Assert.Equal("C", repo.Load("eu4").State.CompatchDefaultView);
    }

    [Fact]
    public void CompatchCardsFolderPane_IsGlobalAndOffByDefault()
    {
        var repo = Repo();
        var eu = repo.Load("eu5");
        Assert.False(eu.State.CompatchCardsFolderPane);

        eu.State.CompatchCardsFolderPane = true;
        repo.Save("eu5", eu);

        Assert.True(repo.Load("eu4").State.CompatchCardsFolderPane);
    }

    [Fact]
    public void CompatchHideComments_IsGlobalAndOnByDefault()
    {
        var repo = Repo();
        var eu = repo.Load("eu5");
        Assert.True(eu.State.CompatchHideComments);

        eu.State.CompatchHideComments = false;
        repo.Save("eu5", eu);

        Assert.False(repo.Load("eu4").State.CompatchHideComments);
    }

    [Fact]
    public void TwoRunningInstances_DoNotOverwriteEachOthersGlobalChanges()
    {
        var repo = Repo();
        var a = repo.Load("eu5");
        var b = repo.Load("ck3");

        a.State.HiddenTabIcons.Add("Vanilla");
        b.State.ThemeId = "builtin-dark";
        repo.Save("ck3", b);
        repo.Save("eu5", a);

        var result = repo.Load("eu5").State;
        Assert.Equal("builtin-dark", result.ThemeId);
        Assert.Equal(new[] { "Vanilla" }, result.HiddenTabIcons);
    }

    [Fact]
    public void Save_UpdatesBaseline_SoLaterSaveDoesNotResendOldChanges()
    {
        var repo = Repo();
        var a = repo.Load("eu5");
        a.State.ThemeId = "builtin-dark";
        repo.Save("eu5", a);

        var other = repo.Load("ck3");
        other.State.ThemeId = "builtin-light";
        repo.Save("ck3", other);

        repo.Save("eu5", a);

        Assert.Equal("builtin-light", repo.Load("eu5").State.ThemeId);
    }

    [Fact]
    public void Window_RoundTripsPerGame()
    {
        var repo = Repo();
        var loaded = repo.Load("eu5");
        loaded.State.Window = new WindowBoundsState { Left = 10, Top = 20, Width = 800, Height = 600, IsMaximized = true };
        repo.Save("eu5", loaded);

        Assert.Equal(800, repo.Load("eu5").State.Window!.Width);
        Assert.True(repo.Load("eu5").State.Window!.IsMaximized);
        Assert.Null(repo.Load("ck3").State.Window);
    }

    [Fact]
    public void Load_FreshRoot_GivesDefaultState()
    {
        var state = Repo().Load("eu5").State;

        Assert.Equal("system", state.ThemeId);
        Assert.Empty(state.LeftPane.Documents);
    }

    [Fact]
    public void Load_CorruptFiles_GiveDefaultStateInsteadOfThrowing()
    {
        var repo = Repo();
        Directory.CreateDirectory(repo.GameDirectory("eu5"));
        File.WriteAllText(repo.SettingsFilePath, "{ nicht json");
        File.WriteAllText(repo.GameSessionFilePath("eu5"), "[1, 2");

        var state = repo.Load("eu5").State;

        Assert.Equal("system", state.ThemeId);
    }

    [Fact]
    public void LastGame_IsNullOnFreshRootAndRemembered()
    {
        var repo = Repo();
        Assert.Null(repo.ReadLastGameId());

        repo.RecordLastGame("ck3");

        Assert.Equal("ck3", repo.ReadLastGameId());
        Assert.Equal("ck3", repo.Load("ck3").State.LastGameProfileId);
    }

    [Fact]
    public void RecordLastGame_KeepsOtherGlobalSettings()
    {
        var repo = Repo();
        var loaded = repo.Load("eu5");
        loaded.State.ThemeId = "builtin-dark";
        repo.Save("eu5", loaded);

        repo.RecordLastGame("ck3");

        Assert.Equal("builtin-dark", repo.Load("ck3").State.ThemeId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a b")]
    public void GameDirectory_RejectsInvalidIds(string id)
    {
        Assert.Throws<ArgumentException>(() => Repo().GameDirectory(id));
    }

    [Fact]
    public void Lock_SecondAcquireFailsUntilFirstIsDisposed()
    {
        var repo = Repo();

        using (var first = repo.TryLockGame("eu5"))
        {
            Assert.NotNull(first);
            Assert.Null(repo.TryLockGame("eu5"));
            Assert.True(repo.IsGameOpen("eu5"));
            using var other = repo.TryLockGame("ck3");
            Assert.NotNull(other);
        }

        Assert.False(repo.IsGameOpen("eu5"));
        using var again = repo.TryLockGame("eu5");
        Assert.NotNull(again);
    }

    [Fact]
    public void Migration_SplitsLegacySessionAndMovesRecoveryFiles()
    {
        var repo = Repo();
        var legacy = new AppSessionState
        {
            ActiveGameProfileId = "eu5",
            ThemeId = "builtin-dark",
            GameExecutablePath = @"C:\Games\eu5.exe",
        };
        legacy.LeftPane.Documents.Add(Doc(@"C:\mod\a.txt"));
        SessionStore.Save(legacy, repo.LegacySessionFilePath);
        Directory.CreateDirectory(repo.LegacyRecoveryDirectory);
        File.WriteAllText(Path.Combine(repo.LegacyRecoveryDirectory, "abc.json"), "{}");

        var state = repo.Load("eu5").State;

        Assert.Equal("builtin-dark", state.ThemeId);
        Assert.Equal(@"C:\Games\eu5.exe", state.GameExecutablePath);
        Assert.Equal(@"C:\mod\a.txt", state.LeftPane.Documents.Single().FilePath);
        Assert.Equal("eu5", repo.ReadLastGameId());
        Assert.False(File.Exists(repo.LegacySessionFilePath));
        Assert.True(File.Exists(repo.LegacySessionFilePath + ".migrated"));
        Assert.True(File.Exists(Path.Combine(repo.GameRecoveryDirectory("eu5"), "abc.json")));
        Assert.False(File.Exists(Path.Combine(repo.LegacyRecoveryDirectory, "abc.json")));
    }

    [Fact]
    public void Migration_UsesActiveProfileIdOfLegacySession_AndFallsBackToEu5()
    {
        var repo = Repo();
        SessionStore.Save(new AppSessionState { ActiveGameProfileId = "ck3" }, repo.LegacySessionFilePath);
        repo.EnsureMigrated();
        Assert.True(File.Exists(repo.GameSessionFilePath("ck3")));

        var other = new SessionRepository(Directory.CreateTempSubdirectory("pmt-repo2-").FullName);
        try
        {
            SessionStore.Save(new AppSessionState(), other.LegacySessionFilePath);
            other.EnsureMigrated();
            Assert.True(File.Exists(other.GameSessionFilePath("eu5")));
        }
        finally
        {
            Directory.Delete(other.Root, recursive: true);
        }
    }

    [Fact]
    public void Migration_RunsOnlyOnce()
    {
        var repo = Repo();
        SessionStore.Save(new AppSessionState { ThemeId = "builtin-dark" }, repo.LegacySessionFilePath);
        repo.EnsureMigrated();
        var loaded = repo.Load("eu5");
        loaded.State.ThemeId = "builtin-light";
        repo.Save("eu5", loaded);

        // Eine später wieder auftauchende alte Datei (z. B. von einer älteren Programmversion) darf nichts überschreiben.
        SessionStore.Save(new AppSessionState { ThemeId = "system" }, repo.LegacySessionFilePath);
        repo.EnsureMigrated();

        Assert.Equal("builtin-light", repo.Load("eu5").State.ThemeId);
    }
}
