using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class TargetIndexTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private string PatchDir => Path.Combine(Dir, "patch");

    private void PutPatch(string relative, string content)
    {
        var path = Path.Combine(PatchDir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Foreign_keys_and_own_files_are_indexed_separately()
    {
        PutPatch(Buildings + "my_fix.txt", "REPLACE:farm = { cost = 3 }\nmine = { x = 1 }\n");
        PutPatch(Buildings + "zz_pmt_compatch.txt", "INJECT:farm = { y = 1 }\n");
        PutPatch(".metadata/metadata.json", "{}");
        PutPatch("thumbnail.png", "x");

        var index = TargetIndex.Build(PatchDir, Input(), readForeignKeys: true);

        Assert.Equal(new[] { Buildings + "zz_pmt_compatch.txt" }, index.OwnFiles);
        var farm = Assert.Single(index.Find(EntryAddress.ForKey("in_game/common/building_types", "farm")));
        Assert.Equal(Buildings + "my_fix.txt", farm.RelativePath);
        Assert.Single(index.FindById(EntryAddress.ForKey("in_game/common/building_types", "mine").Id));
        Assert.True(index.ForeignFiles.ContainsKey(Buildings + "MY_FIX.txt"));
        Assert.DoesNotContain(index.ForeignFiles.Keys, k => k.Contains("metadata") || k.Contains("thumbnail"));
    }

    [Fact]
    public void Key_in_two_foreign_files_has_two_locations()
    {
        PutPatch(Buildings + "a.txt", "farm = { cost = 3 }\n");
        PutPatch(Buildings + "b.txt", "farm = { cost = 4 }\n");

        var index = TargetIndex.Build(PatchDir, Input(), readForeignKeys: true);

        Assert.Equal(2, index.Find(EntryAddress.ForKey("in_game/common/building_types", "farm")).Count);
    }

    [Fact]
    public void Without_foreign_keys_only_own_files_are_listed()
    {
        PutPatch(Buildings + "a.txt", "farm = { cost = 3 }\n");
        PutPatch(Buildings + "zz_pmt_compatch.txt", "farm = { cost = 4 }\n");

        var index = TargetIndex.Build(PatchDir, Input(), readForeignKeys: false);

        Assert.Single(index.OwnFiles);
        Assert.Empty(index.ForeignFiles);
        Assert.Empty(index.Find(EntryAddress.ForKey("in_game/common/building_types", "farm")));
    }

    [Fact]
    public void Missing_folder_gives_empty_index_and_broken_file_is_reported()
    {
        Assert.Empty(TargetIndex.Build(Path.Combine(Dir, "missing"), Input(), true).OwnFiles);

        PutPatch(Buildings + "broken.txt", "farm = { cost = 3\n");
        var index = TargetIndex.Build(PatchDir, Input(), true);
        Assert.Single(index.Unreadable);
    }
}
