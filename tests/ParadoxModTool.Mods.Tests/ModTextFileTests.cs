using System.Text;
using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModTextFileTests : TempDirTests
{
    private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

    [Fact]
    public void Read_ErkenntBomUndLiefertTextOhneSie()
    {
        var path = Path.Combine(Dir, "c.txt");
        File.WriteAllBytes(path, Bom.Concat(Encoding.UTF8.GetBytes("Grüße\r\n")).ToArray());

        var document = ModTextFile.Read(path);

        Assert.True(document.HasBom);
        Assert.Equal("Grüße\r\n", document.Text);
    }

    [Fact]
    public void Read_OhneBom()
    {
        var path = Path.Combine(Dir, "c.txt");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes("abc"));

        var document = ModTextFile.Read(path);

        Assert.False(document.HasBom);
        Assert.Equal("abc", document.Text);
    }

    [Fact]
    public void Read_KeinUtf8_WirftInvalidDataException_StattDenTextZuVerfaelschen()
    {
        var path = Path.Combine(Dir, "c.txt");
        File.WriteAllBytes(path, new byte[] { 0x41, 0xFC, 0x42 });

        Assert.Throws<InvalidDataException>(() => ModTextFile.Read(path));
    }

    [Fact]
    public void Write_BehaeltDasBom_SichertDieAlteDateiImBackupOrdner_UndLaesstKeineTempDateiLiegen()
    {
        var folder = Make("mod");
        var backups = Path.Combine(Dir, "backups");
        var path = Path.Combine(folder, "Changelog.txt");
        File.WriteAllBytes(path, Bom.Concat(Encoding.UTF8.GetBytes("alt")).ToArray());

        ModTextFile.Write(path, new ModTextDocument("neu", HasBom: true), backups);

        Assert.Equal(Bom.Concat(Encoding.UTF8.GetBytes("neu")).ToArray(), File.ReadAllBytes(path));
        var backup = Assert.Single(Directory.GetFiles(backups));
        Assert.Equal(Bom.Concat(Encoding.UTF8.GetBytes("alt")).ToArray(), File.ReadAllBytes(backup));
        Assert.EndsWith(".txt", backup);
        Assert.Equal(new[] { path }, Directory.GetFiles(folder));
    }

    [Fact]
    public void Write_OhneBom_SchreibtOhneBom()
    {
        var path = Path.Combine(Dir, "c.txt");

        ModTextFile.Write(path, new ModTextDocument("x", HasBom: false), Path.Combine(Dir, "b"));

        Assert.Equal(Encoding.UTF8.GetBytes("x"), File.ReadAllBytes(path));
    }

    [Fact]
    public void Write_NeueDatei_LegtKeinBackupAn()
    {
        var backups = Path.Combine(Dir, "backups");

        ModTextFile.Write(Path.Combine(Dir, "neu.txt"), new ModTextDocument("x", false), backups);

        Assert.False(Directory.Exists(backups) && Directory.GetFiles(backups).Length > 0);
    }

    [Fact]
    public void Write_ZweiSchreibvorgaengeInDerselbenSekunde_UeberschreibenKeinBackup()
    {
        var path = Path.Combine(Dir, "c.txt");
        var backups = Path.Combine(Dir, "backups");
        File.WriteAllText(path, "0");

        ModTextFile.Write(path, new ModTextDocument("1", false), backups);
        ModTextFile.Write(path, new ModTextDocument("2", false), backups);

        Assert.Equal(2, Directory.GetFiles(backups).Length);
    }
}
