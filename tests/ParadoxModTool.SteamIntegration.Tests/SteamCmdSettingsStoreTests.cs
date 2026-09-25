using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class SteamCmdSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-steam-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void UploadMethod_WirdGespeichertUndGeladen_DateiversionBleibtEins()
    {
        var store = new SteamCmdSettingsStore(_dir);

        store.Save(new SteamCmdSettings { UserName = "flogi", UploadMethod = "client" });
        var loaded = store.Load();

        Assert.Equal("client", loaded.UploadMethod);
        Assert.Equal("flogi", loaded.UserName);
        Assert.Contains("\"version\": 1", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void AlteDateiOhneUploadMethod_LaedtMitNull()
    {
        var store = new SteamCmdSettingsStore(_dir);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        File.WriteAllText(store.FilePath, "{ \"version\": 1, \"userName\": \"flogi\" }");

        Assert.Null(store.Load().UploadMethod);
    }

    [Fact]
    public void Load_OhneDatei_LiefertLeereEinstellungen_UndLegtNichtsAn()
    {
        var store = new SteamCmdSettingsStore(_dir);

        var settings = store.Load();

        Assert.Null(settings.SteamCmdPath);
        Assert.Null(settings.UserName);
        Assert.Null(store.LoadWarning);
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Save_UndLoad_Roundtrip_OhnePasswortFelder()
    {
        var store = new SteamCmdSettingsStore(_dir);

        store.Save(new SteamCmdSettings { SteamCmdPath = @"C:\tools\steamcmd.exe", UserName = "flogi" });

        var loaded = new SteamCmdSettingsStore(_dir).Load();
        Assert.Equal(@"C:\tools\steamcmd.exe", loaded.SteamCmdPath);
        Assert.Equal("flogi", loaded.UserName);
        Assert.DoesNotContain("password", File.ReadAllText(store.FilePath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_KaputteDatei_SichertKopie_UndWarnt()
    {
        Directory.CreateDirectory(_dir);
        var store = new SteamCmdSettingsStore(_dir);
        File.WriteAllText(store.FilePath, "{ kaputt");

        var settings = store.Load();

        Assert.Null(settings.UserName);
        Assert.NotNull(store.LoadWarning);
        Assert.Equal("{ kaputt", File.ReadAllText(store.FilePath + ".bad"));
    }
}
