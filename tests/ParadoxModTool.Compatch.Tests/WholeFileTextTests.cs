using System.Text;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class WholeFileTextTests : CompatchTestBase
{
    private Effective File(string root, string name, byte[] content)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, content);
        return new Effective { RelativePath = name, FullPath = path, FileHash = TextEncodings.Sha256(content) };
    }

    private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    private AddressInfo Info(Effective a, Effective b, Effective? vanilla = null) => new()
    {
        Address = EntryAddress.ForWholeFile(a.RelativePath ?? b.RelativePath!),
        Behavior = ParadoxModTool.ModCompat.OverrideBehavior.WholeFile,
        Certain = true,
        A = a,
        B = b,
        Vanilla = vanilla ?? Effective.Absent,
    };

    [Fact]
    public void Text_file_is_read_as_text_also_in_windows_1252()
    {
        var utf8 = File(ADir, "history/provinces/151 - Constantinople.txt", Utf8("owner = TUR\nculture = turkish\n"));
        var ansi = File(BDir, "history/provinces/1 - Stockholm.txt", new byte[] { 0x6E, 0x61, 0x6D, 0x65, 0x20, 0x3D, 0x20, 0x22, 0x47, 0xF6, 0x74, 0x65, 0x22 });

        Assert.Equal("owner = TUR\nculture = turkish\n", WholeFileText.Of(utf8));
        Assert.Equal("name = \"Göte\"", WholeFileText.Of(ansi));
    }

    [Fact]
    public void Missing_side_is_empty_and_binary_or_large_files_are_described()
    {
        var binary = File(ADir, "gfx/flags/TUR.tga", new byte[] { 0, 2, 0, 0, 1, 2, 3 });
        var large = File(BDir, "map/big.txt", Utf8(new string('x', (int)WholeFileText.MaxBytes + 1)));

        Assert.Equal(string.Empty, WholeFileText.Of(Effective.Absent));
        Assert.StartsWith("(", WholeFileText.Of(binary));
        Assert.Contains("7 B", WholeFileText.Of(binary));
        Assert.StartsWith("(", WholeFileText.Of(large));
        Assert.DoesNotContain("xxx", WholeFileText.Of(large));
    }

    [Fact]
    public void Resolved_whole_file_address_shows_the_contents_of_all_three_sides()
    {
        const string path = "in_game/common/building_types/x.txt";
        PutVanilla(path, "farm = { cost = 1 }\n");
        PutA(path, "farm = { cost = 2\n");
        PutB(path, "farm = { cost = 3 }\n");

        var info = EffectiveEntryResolver.Resolve(Input()).Addresses[EntryAddress.ForWholeFile(path)];
        var rows = ActionText.WholeFileRows(info, new MergeDecision(DecisionKind.TakeB), withVanilla: true);

        Assert.Contains(rows, r => r.A == "farm = { cost = 2");
        Assert.Contains(rows, r => r.B == "farm = { cost = 3 }" && r.Result == "farm = { cost = 3 }");
        Assert.Contains(rows, r => r.Vanilla == "farm = { cost = 1 }");
    }

    [Fact]
    public void Key_with_whole_file_form_shows_the_files_and_the_planned_patch_file()
    {
        const string path = "in_game/common/building_types/x.txt";
        PutVanilla(path, "farm = { cost = 1 }\nmine = { cost = 5 }\n");
        PutA(path, "farm = { cost = 2 }\nmine = { cost = 5 }\n");
        PutB(path, "farm = { cost = 3 }\nmine = { cost = 6 }\n");
        var info = EffectiveEntryResolver.Resolve(Input()).Addresses[EntryAddress.ForKey("in_game/common/building_types", "farm")];
        var prepared = new PreparedWrite();
        prepared.Changes.Add(new FileChange(path, FileChangeKind.Create, null, "farm = { cost = 2 }\nmine = { cost = 6 }\n"));

        var rows = ActionText.KeyFileRows(info, prepared, withVanilla: true);
        var withoutPreview = ActionText.KeyFileRows(info, null, withVanilla: false);

        Assert.Contains(rows, r => r.A == "farm = { cost = 2 }" && r.Result == "farm = { cost = 2 }");
        Assert.Contains(rows, r => r.B == "mine = { cost = 6 }" && r.Result == "mine = { cost = 6 }");
        Assert.Contains(rows, r => r.Vanilla == "farm = { cost = 1 }");
        Assert.Contains(withoutPreview, r => r.Result == "farm = { cost = 3 }");
    }

    [Fact]
    public void Rows_show_the_file_contents_with_the_chosen_side_as_result()
    {
        const string name = "history/provinces/151 - Constantinople.txt";
        var a = File(ADir, name, Utf8("owner = TUR\nbase_tax = 12\nhre = no\n"));
        var b = File(BDir, name, Utf8("owner = TUR\nbase_tax = 15\nhre = no\nadd_building = bt_grand_bazaar\n"));
        var vanilla = File(VanillaDir, name, Utf8("owner = TUR\nbase_tax = 11\nhre = no\n"));
        var info = Info(a, b, vanilla);

        var rows = ActionText.WholeFileRows(info, new MergeDecision(DecisionKind.TakeB), withVanilla: true);

        Assert.Contains(rows, r => r.A == "owner = TUR" && r.Result == "owner = TUR" && r.B == "owner = TUR" && r.Vanilla == "owner = TUR");
        Assert.Contains(rows, r => r.Result == "add_building = bt_grand_bazaar" && r.B == "add_building = bt_grand_bazaar" && r.A is null);
        Assert.Contains(rows, r => r.A == "base_tax = 12");
        Assert.Contains(rows, r => r.Vanilla == "base_tax = 11");
        Assert.DoesNotContain(rows, r => r.Result == "base_tax = 12");

        var takeA = ActionText.WholeFileRows(info, new MergeDecision(DecisionKind.TakeA), withVanilla: false);
        Assert.Contains(takeA, r => r.Result == "base_tax = 12" && r.A == "base_tax = 12");
        Assert.All(takeA, r => Assert.Null(r.Vanilla));
    }
}
