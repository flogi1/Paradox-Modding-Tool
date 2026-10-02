using System.Text;
using Xunit;

namespace ParadoxModTool.ErrorLog.Tests;

public class SetEffectEditsTests
{
    private static readonly string[] SetVariable = { "set_variable" };

    private static ModSource Mod(TempTree tree) => new("Own", tree.PathOf("mod"), IsOwn: true, LoadOrder: 0);

    private static SetEffectScan Scan(TempTree tree, string name = "iv2_x") =>
        SetEffectEdits.Scan(Mod(tree), name, SetVariable, Array.Empty<string>());

    [Fact]
    public void MultiLineBlockIsFoundAndCommentedKeepingIndentation()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("mod/common/scripted_effects/a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("e = {\r\n\tset_variable = {\r\n\t\tname = iv2_x\r\n\t\tvalue = 5\r\n\t}\r\n}\r\n")).ToArray());

        var scan = Scan(tree);

        Assert.Equal(new SetStatement(path, 2, 5), Assert.Single(scan.Statements));
        var file = Assert.Single(scan.Plan.Files);
        Assert.Equal("e = {\r\n\t# set_variable = {\r\n\t\t# name = iv2_x\r\n\t\t# value = 5\r\n\t# }\r\n}\r\n", file.NewText);
        Assert.True(file.HasBom);
        Assert.Equal(4, file.Lines.Count);
        Assert.Equal(("\tset_variable = {", "\t# set_variable = {"), (file.Lines[0].Before, file.Lines[0].After));
        Assert.Equal(1, file.Count);
    }

    [Fact]
    public void ShortFormAndQuotedNameAreFound()
    {
        using var tree = new TempTree(("mod/common/a.txt", "e = {\n\tset_variable = iv2_x\n\tset_variable = { name = \"iv2_x\" value = 1 }\n}\n"));

        var scan = Scan(tree);

        Assert.Equal(new[] { 2, 3 }, scan.Statements.Select(s => s.StartLine));
        Assert.Equal("e = {\n\t# set_variable = iv2_x\n\t# set_variable = { name = \"iv2_x\" value = 1 }\n}\n", Assert.Single(scan.Plan.Files).NewText);
    }

    [Fact]
    public void OtherVariablesAndOtherEffectsAreIgnored()
    {
        using var tree = new TempTree(("mod/common/a.txt", "set_variable = { name = iv2_y }\nchange_variable = { name = iv2_x add = 1 }\nname = iv2_x\nset_variable = iv2_xy\n"));

        var scan = Scan(tree);

        Assert.Empty(scan.Statements);
        Assert.Empty(scan.Skipped);
        Assert.Empty(scan.Plan.Files);
    }

    [Fact]
    public void StatementSharingALineWithOtherCodeIsSkipped()
    {
        using var tree = new TempTree(("mod/common/a.txt", "if = { limit = { always = yes } set_variable = { name = iv2_x value = 1 } }\nset_variable = { name = iv2_x } add_gold = 1\n"));

        var scan = Scan(tree);

        Assert.Empty(scan.Statements);
        Assert.Equal(new[] { 1, 2 }, scan.Skipped.Select(s => s.Line));
        Assert.Empty(scan.Plan.Files);
    }

    [Fact]
    public void BracesAndKeywordsInStringsAndCommentsDoNotCount()
    {
        using var tree = new TempTree(("mod/common/a.txt",
            "set_variable = { name = iv2_x value = 1 } # }\n" +
            "custom_tooltip = \"set_variable = { name = iv2_x }\"\n" +
            "# set_variable = { name = iv2_x }\n" +
            "set_variable = {\n\tname = iv2_x\n\ttext = \"}\"\n}\n"));

        var scan = Scan(tree);

        Assert.Equal(new[] { (1, 1), (4, 7) }, scan.Statements.Select(s => (s.StartLine, s.EndLine)));
        Assert.Empty(scan.Skipped);
    }

    [Fact]
    public void ReadsOutsideIgnoreTheNameLineOfAFoundBlock()
    {
        using var tree = new TempTree(
            ("mod/common/scripted_effects/a.txt", "e = {\n\tset_variable = {\n\t\tname = iv2_x\n\t}\n}\n"),
            ("mod/events/b.txt", "t = { has_variable = iv2_x }\n"));
        var mod = Mod(tree);
        var index = SymbolIndex.Build(new[] { new SymbolRoot(mod.Directory, mod, true) });
        var scan = SetEffectEdits.Scan(mod, "iv2_x", SetVariable, Array.Empty<string>());

        var reads = SetEffectEdits.ReadsOutside(index, "iv2_x", new[] { scan });

        Assert.EndsWith("b.txt", Assert.Single(reads).FilePath);

        File.Delete(tree.PathOf("mod/events/b.txt"));
        var clean = SymbolIndex.Build(new[] { new SymbolRoot(mod.Directory, mod, true) });
        Assert.Empty(SetEffectEdits.ReadsOutside(clean, "iv2_x", new[] { scan }));
    }

    [Fact]
    public void UnreadableFilesAreReported()
    {
        using var tree = new TempTree();
        var broken = tree.PathOf("mod/common/broken.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(broken)!);
        File.WriteAllBytes(broken, Encoding.ASCII.GetBytes("set_variable = iv2_x ").Concat(new byte[] { 0xC3, 0x28 }).ToArray());

        var scan = Scan(tree);

        Assert.Equal(new[] { broken }, scan.Unreadable);
        Assert.Empty(scan.Plan.Files);
    }
}
