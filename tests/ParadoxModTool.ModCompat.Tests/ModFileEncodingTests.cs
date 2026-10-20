using System.Text;
using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// Kodierung beim Schreiben von Mod-Dateien: neue Dateien nach dem Spielprofil, bestehende behalten ihr BOM. Stellaris liest Skript-, .gui- und
/// .gfx-Dateien mit BOM falsch ("Unexpected token: &lt;BOM&gt;", gemessen 2026-10-16); HOI IV schreibt Skriptdateien, EU IV .gui und .gfx ohne BOM
/// (wie Vanilla; Probe-Mods ohne BOM luden). Lokalisierung braucht in allen Spielen ein BOM.
/// </summary>
public sealed class ModFileEncodingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pmt-modfileencoding-" + Guid.NewGuid().ToString("N"));

    public ModFileEncodingTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static GameProfile Shipped(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ParadoxModTool.slnx")))
        {
            dir = dir.Parent;
        }

        return GameProfileLoader.LoadFromFile(Path.Combine(dir!.FullName, "profiles", file));
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".gui")]
    [InlineData(".gfx")]
    public void StellarisWritesNewScriptFilesWithoutBom(string extension)
    {
        var profile = Shipped("stellaris.json");

        Assert.False(ModFileEncoding.NewFileHasBom(profile, extension));
        Assert.Empty(ModFileEncoding.NewFile(profile, extension).GetPreamble());
    }

    [Theory]
    [InlineData("stellaris.json")]
    [InlineData("ck3.json")]
    [InlineData("eu4.json")]
    [InlineData("eu5.json")]
    [InlineData("vic3.json")]
    public void LocalisationAlwaysGetsBom(string file)
    {
        Assert.True(ModFileEncoding.NewFileHasBom(Shipped(file), ".yml"));
        Assert.True(ModFileEncoding.NewFileHasBom(Shipped(file), ".YML"));
    }

    [Theory]
    [InlineData("ck3.json")]
    [InlineData("eu5.json")]
    [InlineData("vic3.json")]
    public void OtherGamesKeepBomForNewScriptFiles(string file)
    {
        var profile = Shipped(file);
        foreach (var extension in new[] { ".txt", ".gui", ".gfx" })
        {
            Assert.True(ModFileEncoding.NewFileHasBom(profile, extension));
            Assert.Equal(3, ModFileEncoding.NewFile(profile, extension).GetPreamble().Length);
        }
    }

    [Fact]
    public void Eu4WritesNewGuiAndGfxWithoutBomButKeepsBomForScripts()
    {
        // EU IV: Vanilla-.gui/.gfx haben kein BOM, die Probe-Mods ohne BOM luden (2026-10-17); Skriptdateien bleiben wie bisher mit BOM.
        var profile = Shipped("eu4.json");

        Assert.True(ModFileEncoding.NewFileHasBom(profile, ".txt"));
        Assert.Equal(3, ModFileEncoding.NewFile(profile, ".txt").GetPreamble().Length);
        foreach (var extension in new[] { ".gui", ".gfx" })
        {
            Assert.False(ModFileEncoding.NewFileHasBom(profile, extension));
            Assert.Empty(ModFileEncoding.NewFile(profile, extension).GetPreamble());
        }
    }

    [Fact]
    public void WithoutProfileOrForUnknownExtensionsNewFilesGetBom()
    {
        Assert.True(ModFileEncoding.NewFileHasBom(null, ".txt"));
        Assert.True(ModFileEncoding.NewFileHasBom(Shipped("stellaris.json"), ".lua"));
        Assert.Equal("utf-8-bom", ModFileEncoding.NewFileEncodingName(null, ".gui"));
        Assert.Equal("utf-8", ModFileEncoding.NewFileEncodingName(Shipped("stellaris.json"), ".GUI"));
    }

    [Fact]
    public void ForWriteUsesProfileForNewFiles()
    {
        var stellaris = Shipped("stellaris.json");

        Assert.Empty(ModFileEncoding.ForWrite(stellaris, Path.Combine(_dir, "new.txt")).GetPreamble());
        Assert.Equal(3, ModFileEncoding.ForWrite(stellaris, Path.Combine(_dir, "new_l_english.yml")).GetPreamble().Length);
        Assert.Equal(3, ModFileEncoding.ForWrite(Shipped("ck3.json"), Path.Combine(_dir, "new.txt")).GetPreamble().Length);
    }

    [Fact]
    public void ForWriteKeepsBomStateOfExistingFiles()
    {
        var withBom = Path.Combine(_dir, "bom.txt");
        var withoutBom = Path.Combine(_dir, "plain.txt");
        File.WriteAllText(withBom, "a = b", new UTF8Encoding(true));
        File.WriteAllText(withoutBom, "a = ö", new UTF8Encoding(false));

        Assert.Equal(3, ModFileEncoding.ForWrite(Shipped("stellaris.json"), withBom).GetPreamble().Length);
        Assert.Empty(ModFileEncoding.ForWrite(Shipped("ck3.json"), withoutBom).GetPreamble());
    }

    [Fact]
    public void ForWriteGivesExistingLocalisationWithoutBomOne()
    {
        var path = Path.Combine(_dir, "x_l_english.yml");
        File.WriteAllText(path, "l_english:\n", new UTF8Encoding(false));

        Assert.Equal(3, ModFileEncoding.ForWrite(Shipped("stellaris.json"), path).GetPreamble().Length);
    }

    [Fact]
    public void ForWriteTreatsNonUtf8ExistingFilesLikeNewOnes()
    {
        // Windows-1252-Dateien (EU IV) wurden bisher als UTF-8 mit BOM gespeichert; das bleibt so
        var path = Path.Combine(_dir, "legacy.txt");
        File.WriteAllBytes(path, new byte[] { (byte)'a', (byte)'=', 0xF6 });

        Assert.Equal(3, ModFileEncoding.ForWrite(Shipped("eu4.json"), path).GetPreamble().Length);
    }

    [Theory]
    [InlineData(".gui")]
    [InlineData(".gfx")]
    public void ForWriteKeepsWindows1252ForExistingGuiFiles(string extension)
    {
        // Der Editor liest .gui/.gfx bei ungültigem UTF-8 als Windows-1252 (EU IV end_of_combat.gui); sie müssen so zurückgeschrieben werden
        var path = Path.Combine(_dir, "legacy" + extension);
        File.WriteAllBytes(path, new byte[] { (byte)'a', (byte)'=', 0xE9 });

        var encoding = ModFileEncoding.ForWrite(Shipped("eu4.json"), path);

        Assert.Empty(encoding.GetPreamble());
        Assert.Equal(new byte[] { 0xE9 }, encoding.GetBytes("é"));
    }
}
