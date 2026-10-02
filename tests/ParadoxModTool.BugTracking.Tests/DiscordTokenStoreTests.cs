using Xunit;

namespace ParadoxModTool.BugTracking.Tests;

public class DiscordTokenStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pmt-token-").FullName;

    private string TokenPath => Path.Combine(_dir, DiscordTokenStore.FileName);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void SpeichernUndLaden_Rundlauf_OhneKlartextInDerDatei()
    {
        DiscordTokenStore.Save(TokenPath, "  MTIz.geheim.token  ");

        Assert.Equal("MTIz.geheim.token", DiscordTokenStore.Load(TokenPath));
        Assert.DoesNotContain("geheim", File.ReadAllText(TokenPath));
    }

    [Fact]
    public void Load_OhneDatei_KeinToken()
    {
        Assert.Null(DiscordTokenStore.Load(TokenPath));
    }

    [Fact]
    public void Load_KaputteDatei_KeinToken()
    {
        File.WriteAllText(TokenPath, "kein base64 !!");
        Assert.Null(DiscordTokenStore.Load(TokenPath));

        File.WriteAllText(TokenPath, Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        Assert.Null(DiscordTokenStore.Load(TokenPath));
    }

    [Fact]
    public void Delete_EntferntDieDatei()
    {
        DiscordTokenStore.Save(TokenPath, "x");

        DiscordTokenStore.Delete(TokenPath);
        DiscordTokenStore.Delete(TokenPath);

        Assert.False(File.Exists(TokenPath));
    }
}
