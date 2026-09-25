using System.IO.Compression;
using System.Security.Cryptography;
using Xunit;

namespace ParadoxModTool.GitIntegration.Tests;

public class GhSetupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-gh-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string Touch(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(_dir, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "exe");
        return path;
    }

    private static byte[] Zip(params (string Name, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public void Settings_Roundtrip_OhneToken_UndKaputteDateiWirdGesichert()
    {
        var store = new GhSettingsStore(_dir);
        Assert.Null(store.Load().GhPath);
        Assert.False(Directory.Exists(_dir));

        store.Save(new GhSettings { GhPath = @"C:\tools\gh.exe" });

        Assert.Equal(@"C:\tools\gh.exe", new GhSettingsStore(_dir).Load().GhPath);
        Assert.DoesNotContain("token", File.ReadAllText(store.FilePath), StringComparison.OrdinalIgnoreCase);

        File.WriteAllText(store.FilePath, "{ kaputt");
        var broken = new GhSettingsStore(_dir);
        Assert.Null(broken.Load().GhPath);
        Assert.NotNull(broken.LoadWarning);
        Assert.True(File.Exists(store.FilePath + ".bad"));
    }

    [Fact]
    public void Locator_Reihenfolge_GespeichertPathProgrammeProgrammordner()
    {
        var saved = Touch("saved/gh.exe");
        var onPath = Touch("bin1/gh.exe");
        var programFiles = Touch("pf/GitHub CLI/gh.exe");
        var own = Touch("own/gh.exe");
        var pathVariable = Path.Combine(_dir, "leer") + Path.PathSeparator + Path.GetDirectoryName(onPath);

        Assert.Equal(saved, GhLocator.Find(new GhSettings { GhPath = saved }, pathVariable, Path.Combine(_dir, "pf"), own));
        Assert.Equal(onPath, GhLocator.Find(new GhSettings { GhPath = Path.Combine(_dir, "weg.exe") }, pathVariable, Path.Combine(_dir, "pf"), own));
        Assert.Equal(programFiles, GhLocator.Find(new GhSettings(), string.Empty, Path.Combine(_dir, "pf"), own));
        Assert.Equal(own, GhLocator.Find(new GhSettings(), string.Empty, Path.Combine(_dir, "nichts"), own));
        Assert.Null(GhLocator.Find(new GhSettings(), string.Empty, Path.Combine(_dir, "nichts"), Path.Combine(_dir, "auch-nichts.exe")));
    }

    [Fact]
    public void SelectDownload_FindetZipUndPruefsummenDatei()
    {
        var json = """
            { "tag_name": "v2.60.0", "assets": [
              { "name": "gh_2.60.0_linux_amd64.tar.gz", "browser_download_url": "https://x/linux" },
              { "name": "gh_2.60.0_windows_amd64.zip", "browser_download_url": "https://x/win.zip" },
              { "name": "gh_2.60.0_checksums.txt", "browser_download_url": "https://x/sums.txt" } ] }
            """;

        var download = GhInstaller.SelectDownload(json);

        Assert.Equal("2.60.0", download.Version);
        Assert.Equal("gh_2.60.0_windows_amd64.zip", download.ZipName);
        Assert.Equal("https://x/win.zip", download.ZipUrl);
        Assert.Equal("https://x/sums.txt", download.ChecksumsUrl);
    }

    [Theory]
    [InlineData("{ \"tag_name\": \"v1\", \"assets\": [] }")]
    [InlineData("kein json")]
    public void SelectDownload_OhnePaket_WirftInvalidData(string json) =>
        Assert.Throws<InvalidDataException>(() => GhInstaller.SelectDownload(json));

    [Fact]
    public void ExpectedSha256_LiestDieZeileDerDatei()
    {
        var text = "aaa111  gh_2.60.0_linux_amd64.tar.gz\nBBB222 *gh_2.60.0_windows_amd64.zip\n";

        Assert.Equal("bbb222", GhInstaller.ExpectedSha256(text, "gh_2.60.0_windows_amd64.zip"));
        Assert.Throws<InvalidDataException>(() => GhInstaller.ExpectedSha256(text, "fehlt.zip"));
    }

    [Fact]
    public void Install_PruefsummeRichtig_EntpacktUndLiefertDenPfad()
    {
        var zip = Zip(("bin/gh.exe", "exe"), ("LICENSE", "lizenz"));
        var folder = Path.Combine(_dir, "tools", "gh");

        var exe = GhInstaller.Install(zip, Sha(zip), folder);

        Assert.Equal(Path.Combine(folder, "bin", "gh.exe"), exe);
        Assert.True(File.Exists(exe));
        Assert.True(File.Exists(Path.Combine(folder, "LICENSE")));
    }

    [Fact]
    public void Install_PruefsummeFalsch_InstalliertNichts()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "tools"));
        var zip = Zip(("bin/gh.exe", "exe"));
        var folder = Path.Combine(_dir, "tools", "gh");

        Assert.Throws<InvalidDataException>(() => GhInstaller.Install(zip, new string('0', 64), folder));

        Assert.False(Directory.Exists(folder));
        Assert.Empty(Directory.GetDirectories(Path.Combine(_dir, "tools")));
    }

    [Fact]
    public void Install_OhneGhExeOderKaputtesArchiv_WirftUndLaesstNichtsZurueck()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "tools"));
        var withoutExe = Zip(("LICENSE", "x"));
        var folder = Path.Combine(_dir, "tools", "gh");

        Assert.Throws<InvalidDataException>(() => GhInstaller.Install(withoutExe, Sha(withoutExe), folder));
        var garbage = new byte[] { 1, 2, 3 };
        Assert.Throws<InvalidDataException>(() => GhInstaller.Install(garbage, Sha(garbage), folder));

        Assert.False(Directory.Exists(folder));
        Assert.Empty(Directory.GetDirectories(Path.Combine(_dir, "tools")));
    }
}
