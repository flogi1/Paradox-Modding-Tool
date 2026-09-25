using System.IO.Compression;
using System.Text;
using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class SteamCmdInstallerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-install-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static MemoryStream Zip(params (string Name, string Content)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Install_EntpacktUndLiefertDenPfadDerExe_UndLegtDenElternordnerAn()
    {
        var folder = Path.Combine(_dir, "tools", "steamcmd");

        var exe = SteamCmdInstaller.Install(Zip(("steamcmd.exe", "MZ")), folder);

        Assert.Equal(Path.Combine(folder, "steamcmd.exe"), exe);
        Assert.Equal("MZ", File.ReadAllText(exe));
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(folder)!, "*.partial-*"));
    }

    [Fact]
    public void Install_BestehenderOrdner_BehaeltAndereDateien_UndErneuertNurDieExe()
    {
        var folder = Path.Combine(_dir, "steamcmd");
        Directory.CreateDirectory(Path.Combine(folder, "config"));
        File.WriteAllText(Path.Combine(folder, "config", "config.vdf"), "login");
        File.WriteAllText(Path.Combine(folder, "steamcmd.exe"), "alt");

        SteamCmdInstaller.Install(Zip(("steamcmd.exe", "neu")), folder);

        Assert.Equal("neu", File.ReadAllText(Path.Combine(folder, "steamcmd.exe")));
        Assert.Equal("login", File.ReadAllText(Path.Combine(folder, "config", "config.vdf")));
    }

    [Fact]
    public void Install_ZipOhneExe_Wirft_UndHinterlaesstNichts()
    {
        var folder = Path.Combine(_dir, "steamcmd");

        Assert.Throws<InvalidDataException>(() => SteamCmdInstaller.Install(Zip(("readme.txt", "x")), folder));

        Assert.False(Directory.Exists(folder));
        Assert.Empty(Directory.GetDirectories(_dir));
    }

    [Fact]
    public void Install_KaputtesArchiv_WirftInvalidDataException()
    {
        var folder = Path.Combine(_dir, "steamcmd");

        Assert.Throws<InvalidDataException>(() => SteamCmdInstaller.Install(new MemoryStream(new byte[] { 1, 2, 3, 4 }), folder));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void Locator_NimmtDenGespeichertenPfad_SonstDenStandardordner_SonstNull()
    {
        var saved = Path.Combine(_dir, "eigene", "steamcmd.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
        File.WriteAllText(saved, "x");
        var defaultFolder = Path.Combine(_dir, "default");
        Directory.CreateDirectory(defaultFolder);
        File.WriteAllText(Path.Combine(defaultFolder, "steamcmd.exe"), "x");

        Assert.Equal(saved, SteamCmdLocator.Find(new SteamCmdSettings { SteamCmdPath = saved }, defaultFolder));
        Assert.Equal(
            Path.Combine(defaultFolder, "steamcmd.exe"),
            SteamCmdLocator.Find(new SteamCmdSettings { SteamCmdPath = Path.Combine(_dir, "weg.exe") }, defaultFolder));
        Assert.Null(SteamCmdLocator.Find(new SteamCmdSettings(), Path.Combine(_dir, "leer")));
    }

    [Fact]
    public void Konstanten_ZeigenAufValvesAdresse()
    {
        Assert.Equal("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip", SteamCmdInstaller.DownloadUrl);
        Assert.EndsWith(Path.Combine("ParadoxModTool", "tools", "steamcmd"), SteamCmdInstaller.DefaultFolder);
    }
}
