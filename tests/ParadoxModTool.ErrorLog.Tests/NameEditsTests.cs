using System.Text;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class NameEditsTests
{
    [Fact]
    public void FindInLineMatchesWholeIdentifiersOnly()
    {
        Assert.Equal(45, NameEdits.FindInLine("\tset_variable = { name = iv2_x_count value = iv2_x }", "iv2_x"));
        Assert.Null(NameEdits.FindInLine("my_iv2_x = 1", "iv2_x"));
        Assert.Equal(6, NameEdits.FindInLine("scope:iv2_x = yes", "iv2_x"));
        Assert.Equal(1, NameEdits.FindInLine("$iv2_x$", "iv2_x"));
        Assert.Null(NameEdits.FindInLine(string.Empty, "iv2_x"));
    }

    [Fact]
    public void PlanRenamesWholeIdentifiersAndListsChangedLines()
    {
        using var tree = new TempTree(("mod/in_game/common/scripted_effects/a.txt", "a = {\n\tset_variable = { name = iv2_x }\n\tb = iv2_x_count\n\tc = scope:iv2_x\n}\n"));

        var file = Assert.Single(NameEdits.Apply(NameEdits.Scan(tree.PathOf("mod"), "iv2_x", Array.Empty<string>()), "iv2_x", "iv2_y"));

        Assert.Equal(2, file.Count);
        Assert.Equal(new[] { 2, 4 }, file.Lines.Select(l => l.Number));
        Assert.Equal("\tset_variable = { name = iv2_x }", file.Lines[0].Before);
        Assert.Equal("\tset_variable = { name = iv2_y }", file.Lines[0].After);
        Assert.Contains("iv2_x_count", file.NewText);
        Assert.Contains("scope:iv2_y", file.NewText);
        Assert.False(file.HasBom);
    }

    [Fact]
    public void PlanSkipsExcludedDotFoldersAndOtherExtensions()
    {
        using var tree = new TempTree(
            ("mod/in_game/gui/a.gui", "x = iv2_x\n"),
            ("mod/in_game/localization/english/l_english.yml", "l_english:\n iv2_x:0 \"X\"\n"),
            ("mod/in_game/gfx/a.gfx", "name = iv2_x\n"),
            ("mod/in_game/common/other.txt", "nichts\n"),
            ("mod/.git/b.txt", "iv2_x\n"),
            ("mod/.metadata/metadata.json", "iv2_x\n"),
            ("mod/readme.md", "iv2_x\n"),
            ("mod/Mod Snapshots/2026/in_game/common/c.txt", "iv2_x\n"));

        var scan = NameEdits.Scan(tree.PathOf("mod"), "iv2_x", new[] { tree.PathOf("mod/Mod Snapshots") });

        Assert.Equal(
            new[] { "a.gfx", "a.gui", "l_english.yml" },
            scan.Files.Select(f => Path.GetFileName(f.Path)).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Empty(scan.Unreadable);
    }

    [Fact]
    public void PlanKeepsCrlfAndReportsBom()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("mod/a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "a = iv2_x\r\nb = 1\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var file = Assert.Single(NameEdits.Apply(NameEdits.Scan(tree.PathOf("mod"), "iv2_x", Array.Empty<string>()), "iv2_x", "iv2_y"));

        Assert.True(file.HasBom);
        Assert.Equal("a = iv2_y\r\nb = 1\r\n", file.NewText);
        Assert.Equal(new RenameLine(1, "a = iv2_x", "a = iv2_y"), Assert.Single(file.Lines));
    }

    [Fact]
    public void InvalidUtf8FilesWithTheNameAreReportedNotPlanned()
    {
        using var tree = new TempTree();
        Directory.CreateDirectory(tree.PathOf("mod"));
        var broken = tree.PathOf("mod/alt.txt");
        File.WriteAllBytes(broken, Encoding.ASCII.GetBytes("a = iv2_x # K").Concat(new byte[] { 0xE4 }).Concat(Encoding.ASCII.GetBytes("se\n")).ToArray());
        File.WriteAllBytes(tree.PathOf("mod/fremd.txt"), new byte[] { 0x62, 0x20, 0x3D, 0x20, 0xE4, 0x0A });

        var scan = NameEdits.Scan(tree.PathOf("mod"), "iv2_x", Array.Empty<string>());

        Assert.Empty(scan.Files);
        Assert.Equal(new[] { broken }, scan.Unreadable);
    }

    [Theory]
    [InlineData("iv2_y", true)]
    [InlineData("_x1", true)]
    [InlineData("1x", false)]
    [InlineData("a b", false)]
    [InlineData("", false)]
    [InlineData("a.b", false)]
    public void IsValidNameAllowsOnlyIdentifiers(string name, bool valid)
    {
        Assert.Equal(valid, NameEdits.IsValidName(name));
    }
}
