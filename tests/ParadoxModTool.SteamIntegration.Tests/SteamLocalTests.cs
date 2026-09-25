using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class VdfParserTests
{
    [Fact]
    public void Parse_LiestVerschachtelteObjekteUndWerte()
    {
        var root = VdfNode.Parse("\"users\"\n{\n\t\"765\"\n\t{\n\t\t\"AccountName\"\t\t\"ninpoukaton\"\n\t\t\"Timestamp\"\t\t\"12\"\n\t}\n}");

        var user = root["users"]!["765"]!;
        Assert.Equal("ninpoukaton", user["AccountName"]!.Value);
        Assert.Equal("12", user["Timestamp"]!.Value);
        Assert.Equal(new[] { "765" }, root["users"]!.Keys);
    }

    [Fact]
    public void Parse_SchluesselOhneGrossKleinschreibung_KommentareUndEscapes()
    {
        var root = VdfNode.Parse("// Kommentar\n\"Path\" \"C:\\\\Program Files\\\\Steam\"\n\"Text\" \"a\\\"b\\nc\"");

        Assert.Equal(@"C:\Program Files\Steam", root["path"]!.Value);
        Assert.Equal("a\"b\nc", root["TEXT"]!.Value);
    }

    [Fact]
    public void Parse_UnvollstaendigerText_WirftNicht_UndLiefertWasDaIst()
    {
        var root = VdfNode.Parse("\"a\" \"1\"\n\"b\"\n{\n\"c\" ");

        Assert.Equal("1", root["a"]!.Value);
        Assert.NotNull(root["b"]);
        Assert.Null(VdfNode.Parse("")["x"]);
    }

    [Fact]
    public void Parse_UnbekannterSchluessel_IstNull()
    {
        Assert.Null(VdfNode.Parse("\"a\" \"1\"")["b"]);
    }
}

public class SteamLoginUsersTests
{
    private const string Sample =
        "\"users\"\n{\n\t\"76561198006673200\"\n\t{\n\t\t\"AccountName\"\t\t\"ninpoukaton\"\n\t\t\"PersonaName\"\t\t\"flogi\"\n\t\t\"RememberPassword\"\t\t\"1\"\n\t\t\"Timestamp\"\t\t\"1789888149\"\n\t}\n" +
        "\t\"76561198000000001\"\n\t{\n\t\t\"AccountName\"\t\t\"zweitkonto\"\n\t\t\"PersonaName\"\t\t\"Zweit\"\n\t\t\"MostRecent\"\t\t\"1\"\n\t\t\"Timestamp\"\t\t\"1700000000\"\n\t}\n}";

    [Fact]
    public void Parse_LiestKontenMitSteamIdKontonameAnzeigenameUndZeit()
    {
        var users = SteamLoginUsers.Parse(Sample);

        Assert.Equal(2, users.Count);
        Assert.Equal("76561198006673200", users[0].SteamId64);
        Assert.Equal("ninpoukaton", users[0].AccountName);
        Assert.Equal("flogi", users[0].PersonaName);
        Assert.Equal(1789888149, users[0].Timestamp);
        Assert.False(users[0].MostRecent);
        Assert.True(users[1].MostRecent);
    }

    [Fact]
    public void Pick_NimmtDasKontoMitDemKontonamen_SonstMostRecent_SonstDasNeueste()
    {
        var users = SteamLoginUsers.Parse(Sample);

        Assert.Equal("ninpoukaton", SteamLoginUsers.Pick(users, "NINPOUKATON")!.AccountName);
        Assert.Equal("zweitkonto", SteamLoginUsers.Pick(users, null)!.AccountName);
        Assert.Equal("zweitkonto", SteamLoginUsers.Pick(users, "unbekannt")!.AccountName);

        var withoutFlag = SteamLoginUsers.Parse(Sample.Replace("\"MostRecent\"\t\t\"1\"", "\"MostRecent\"\t\t\"0\""));
        Assert.Equal("ninpoukaton", SteamLoginUsers.Pick(withoutFlag, null)!.AccountName);
        Assert.Null(SteamLoginUsers.Pick(Array.Empty<SteamLoginUser>(), "x"));
    }

    [Fact]
    public void Parse_IgnoriertEintraegeOhneNumerischeIdOderKontonamen_UndLeerenText()
    {
        Assert.Empty(SteamLoginUsers.Parse(""));
        Assert.Empty(SteamLoginUsers.Parse("\"users\" { \"abc\" { \"AccountName\" \"x\" } \"123\" { \"PersonaName\" \"y\" } }"));
    }

    [Fact]
    public void Parse_EnthaeltKeinPasswortFeld()
    {
        Assert.DoesNotContain(typeof(SteamLoginUser).GetProperties(), p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }
}

public class SteamFoldersTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-steamdir-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void LibraryFolders_DerSteamOrdnerSelbstUndDieBibliothekenAusLibraryfolders_NurExistierende_OhneDoppelte()
    {
        var steam = Path.Combine(_dir, "Steam");
        var second = Path.Combine(_dir, "Second");
        var missing = Path.Combine(_dir, "Missing");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        Directory.CreateDirectory(second);
        File.WriteAllText(
            Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\"0\"\n{\n\"path\" \"" + steam.Replace("\\", "\\\\") + "\"\n}\n\"1\"\n{\n\"path\" \"" + second.Replace("\\", "\\\\")
            + "\"\n}\n\"2\"\n{\n\"path\" \"" + missing.Replace("\\", "\\\\") + "\"\n}\n}");

        var folders = SteamFolders.LibraryFolders(steam);

        Assert.Equal(new[] { steam, second }, folders);
    }

    [Fact]
    public void LibraryFolders_OhneDatei_NurDerSteamOrdner()
    {
        Directory.CreateDirectory(_dir);

        Assert.Equal(new[] { _dir }, SteamFolders.LibraryFolders(_dir));
    }

    [Fact]
    public void LoginUsersFile_LiegtImConfigOrdner()
    {
        Assert.Equal(Path.Combine(@"C:\Steam", "config", "loginusers.vdf"), SteamFolders.LoginUsersFile(@"C:\Steam"));
    }
}
