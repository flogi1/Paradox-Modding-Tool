using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

public class CompatSetTests : TempDirTests
{
    private static CompatSetMember Member(string folder, string name = "M") => new() { Folder = folder, Name = name };

    [Fact]
    public void Add_rejects_same_folder_in_other_spelling()
    {
        var set = new CompatSet { Name = "S" };
        var folder = Make("modA");
        Assert.True(set.Add(Member(folder)));
        Assert.False(set.Add(Member(folder.ToUpperInvariant() + Path.DirectorySeparatorChar)));
        Assert.Single(set.Members);
    }

    [Fact]
    public void Add_normalizes_slashes()
    {
        var set = new CompatSet { Name = "S" };
        var folder = Make("modA");
        set.Add(Member(folder.Replace('\\', '/') + "/"));
        Assert.Equal(folder, set.Members[0].Folder);
    }

    [Fact]
    public void Move_changes_order_and_clamps()
    {
        var set = new CompatSet { Name = "S" };
        var a = Make("a");
        var b = Make("b");
        var c = Make("c");
        set.Add(Member(a, "A"));
        set.Add(Member(b, "B"));
        set.Add(Member(c, "C"));
        Assert.True(set.Move(c, -1));
        Assert.Equal(new[] { "A", "C", "B" }, set.Members.Select(m => m.Name));
        Assert.False(set.Move(a, -1));
        Assert.False(set.Move(b, 1));
        Assert.False(set.Move(Path.Combine(Dir, "fehlt"), 1));
    }

    [Fact]
    public void Remove_returns_false_for_unknown_folder()
    {
        var set = new CompatSet { Name = "S" };
        var a = Make("a");
        set.Add(Member(a));
        Assert.True(set.Remove(a));
        Assert.False(set.Remove(a));
    }

    [Fact]
    public void Store_roundtrip_list_delete()
    {
        var store = new CompatSetStore(Path.Combine(Dir, "compat", "sets"));
        Assert.Empty(store.List());
        var set = new CompatSet { Name = "Mein Set" };
        set.Add(new CompatSetMember { Folder = Make("a"), Name = "A", Enabled = false, Origin = CompatOrigin.Workshop, ModId = "x1" });
        store.Save(set);
        Assert.Equal(new[] { "Mein Set" }, store.List());
        var loaded = store.Load("Mein Set")!;
        Assert.Equal("Mein Set", loaded.Name);
        Assert.Equal(CompatOrigin.Workshop, loaded.Members[0].Origin);
        Assert.False(loaded.Members[0].Enabled);
        Assert.Equal("x1", loaded.Members[0].ModId);
        store.Delete("Mein Set");
        Assert.Empty(store.List());
        Assert.Null(store.Load("Mein Set"));
    }

    [Fact]
    public void Store_roundtrip_keeps_description()
    {
        var store = new CompatSetStore(Path.Combine(Dir, "compat", "sets"));
        var set = new CompatSet { Name = "Mit Beschreibung", Description = "Erklärt, wofür das Set da ist." };
        store.Save(set);
        var loaded = store.Load("Mit Beschreibung")!;
        Assert.Equal("Erklärt, wofür das Set da ist.", loaded.Description);
    }

    [Fact]
    public void Store_load_of_broken_file_is_null()
    {
        var dir = Make("sets");
        File.WriteAllText(Path.Combine(dir, "kaputt.json"), "{ nope");
        Assert.Null(new CompatSetStore(dir).Load("kaputt"));
    }

    [Fact]
    public void Store_handles_names_with_invalid_characters()
    {
        var store = new CompatSetStore(Make("sets"));
        store.Save(new CompatSet { Name = "a/b:c" });
        Assert.NotNull(store.Load("a/b:c"));
    }

    [Fact]
    public void FromFolder_takes_name_from_metadata_else_folder_name()
    {
        var withMeta = Make("m1");
        Write("m1/.metadata/metadata.json", "{ \"name\": \"Schöne Mod\" }");
        var without = Make("plain_mod");
        Assert.Equal("Schöne Mod", CompatMembers.FromFolder(withMeta, CompatOrigin.Folder, null, ".metadata/metadata.json").Name);
        Assert.Equal("plain_mod", CompatMembers.FromFolder(without + Path.DirectorySeparatorChar, CompatOrigin.Folder, null, ".metadata/metadata.json").Name);
    }

    [Fact]
    public void FindWorkshopFolders_lists_app_folders_of_all_libraries()
    {
        Make("lib1", "steamapps", "workshop", "content", "3450310", "111");
        Make("lib1", "steamapps", "workshop", "content", "3450310", "222");
        Make("lib2", "steamapps", "workshop", "content", "999", "333");
        Make("lib2", "steamapps", "workshop", "content", "3450310", "444");
        var found = CompatMembers.FindWorkshopFolders("3450310", new[] { Path.Combine(Dir, "lib1"), Path.Combine(Dir, "lib2"), Path.Combine(Dir, "fehlt") });
        Assert.Equal(new[] { "111", "222", "444" }, found.Select(Path.GetFileName));
    }
}
