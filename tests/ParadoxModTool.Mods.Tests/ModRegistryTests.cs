using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Mods.Tests;

public class ModRegistryTests : TempDirTests
{
    private string GameDir => Path.Combine(Dir, "game");

    private ModRegistry Fresh() => ModRegistry.Load(GameDir);

    [Fact]
    public void A_missing_file_gives_an_empty_registry_without_warning()
    {
        var registry = Fresh();

        Assert.Empty(registry.Mods);
        Assert.Null(registry.LoadWarning);
        Assert.False(File.Exists(registry.FilePath));
    }

    [Fact]
    public void Add_saves_and_a_reload_finds_the_mod()
    {
        var folder = Make("mods", "idea_variation");

        var added = Fresh().Add("  Idea Variation  ", folder);
        var reloaded = Fresh();

        Assert.Equal(32, added.Id.Length);
        var mod = Assert.Single(reloaded.Mods);
        Assert.Equal(added.Id, mod.Id);
        Assert.Equal("Idea Variation", mod.Name);
        Assert.Equal(new[] { folder }, mod.Paths);
    }

    [Fact]
    public void The_file_uses_the_documented_layout()
    {
        var registry = Fresh();
        registry.Add("A", Make("mods", "a"));

        using var document = JsonDocument.Parse(File.ReadAllText(registry.FilePath));

        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("A", document.RootElement.GetProperty("mods")[0].GetProperty("name").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("ignoredPaths").GetArrayLength());
    }

    [Fact]
    public void Add_rejects_an_empty_name()
    {
        Assert.Throws<ArgumentException>(() => Fresh().Add("  ", Make("mods", "a")));
    }

    [Fact]
    public void Add_rejects_a_path_that_already_belongs_to_a_mod_in_any_spelling()
    {
        var registry = Fresh();
        var folder = Make("mods", "a");
        registry.Add("A", folder);

        Assert.Throws<InvalidOperationException>(() => registry.Add("B", folder.ToUpperInvariant() + Path.DirectorySeparatorChar));
        Assert.Single(registry.Mods);
    }

    [Fact]
    public void Two_mods_may_share_a_name()
    {
        var registry = Fresh();

        var first = registry.Add("Same", Make("mods", "a"));
        var second = registry.Add("Same", Make("mods", "b"));

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Resolve_takes_the_longest_matching_path()
    {
        var registry = Fresh();
        var outer = Make("mods", "outer");
        var inner = Make("mods", "outer", "inner");
        var outerMod = registry.Add("Outer", outer);
        var innerMod = registry.Add("Inner", inner);

        Assert.Equal(innerMod.Id, registry.Resolve(Path.Combine(inner, "common", "a.txt"))!.Id);
        Assert.Equal(outerMod.Id, registry.Resolve(Path.Combine(outer, "b.txt"))!.Id);
    }

    [Fact]
    public void Resolve_respects_the_directory_border()
    {
        var registry = Fresh();
        registry.Add("A", Make("mods", "mod"));

        Assert.Null(registry.Resolve(Path.Combine(Dir, "mods", "mod2", "x.txt")));
    }

    [Fact]
    public void Resolve_ignores_case_and_slash_style()
    {
        var registry = Fresh();
        var folder = Make("mods", "Mod");
        var mod = registry.Add("A", folder);

        var forward = folder.Replace('\\', '/').ToLowerInvariant() + "/common/../common/a.txt";

        Assert.Equal(mod.Id, registry.Resolve(forward)!.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a\0b")]
    public void Resolve_returns_null_for_invalid_paths(string? path)
    {
        var registry = Fresh();
        registry.Add("A", Make("mods", "a"));

        Assert.Null(registry.Resolve(path));
    }

    [Fact]
    public void Get_finds_a_mod_by_id()
    {
        var registry = Fresh();
        var mod = registry.Add("A", Make("mods", "a"));

        Assert.Same(mod, registry.Get(mod.Id));
        Assert.Null(registry.Get("unbekannt"));
    }

    [Fact]
    public void Corrupt_file_is_copied_and_registry_starts_empty()
    {
        Directory.CreateDirectory(GameDir);
        var path = Path.Combine(GameDir, ModRegistry.FileName);
        File.WriteAllText(path, "{ das ist kein json");

        var registry = ModRegistry.Load(GameDir);

        Assert.Empty(registry.Mods);
        Assert.NotNull(registry.LoadWarning);
        Assert.Equal("{ das ist kein json", File.ReadAllText(path + ".bad"));
    }

    [Fact]
    public void Entries_without_id_or_with_null_lists_are_repaired_on_load()
    {
        Directory.CreateDirectory(GameDir);
        File.WriteAllText(
            Path.Combine(GameDir, ModRegistry.FileName),
            "{ \"version\": 1, \"mods\": [ { \"name\": \"Ohne ID\", \"paths\": null }, null ], \"ignoredPaths\": null }");

        var registry = ModRegistry.Load(GameDir);

        var mod = Assert.Single(registry.Mods);
        Assert.Equal(32, mod.Id.Length);
        Assert.Empty(mod.Paths);
        Assert.Null(registry.LoadWarning);
    }

    [Fact]
    public void Rename_changes_the_name_and_keeps_the_id()
    {
        var registry = Fresh();
        var mod = registry.Add("Alt", Make("mods", "a"));

        registry.Rename(mod.Id, " Neu ");

        Assert.Equal("Neu", Fresh().Get(mod.Id)!.Name);
        Assert.Throws<ArgumentException>(() => registry.Rename(mod.Id, ""));
        Assert.Throws<InvalidOperationException>(() => registry.Rename("unbekannt", "X"));
    }

    [Fact]
    public void AddPath_and_RemovePath_change_the_paths_of_one_mod()
    {
        var registry = Fresh();
        var mod = registry.Add("A", Make("mods", "a"));
        var second = Make("workshop", "a");

        registry.AddPath(mod.Id, second);

        Assert.Equal(2, Fresh().Get(mod.Id)!.Paths.Count);
        Assert.Same(mod, registry.Resolve(Path.Combine(second, "x.txt")));

        registry.RemovePath(mod.Id, second + Path.DirectorySeparatorChar);

        Assert.Single(Fresh().Get(mod.Id)!.Paths);
        Assert.Throws<InvalidOperationException>(() => registry.RemovePath(mod.Id, second));
    }

    [Fact]
    public void AddPath_rejects_a_path_of_another_mod_and_a_repeated_path()
    {
        var registry = Fresh();
        var folderA = Make("mods", "a");
        var a = registry.Add("A", folderA);
        var b = registry.Add("B", Make("mods", "b"));

        Assert.Throws<InvalidOperationException>(() => registry.AddPath(b.Id, folderA));
        Assert.Throws<InvalidOperationException>(() => registry.AddPath(a.Id, folderA));
    }

    [Fact]
    public void Relink_replaces_the_path_and_keeps_the_id()
    {
        var registry = Fresh();
        var oldFolder = Make("mods", "old");
        var newFolder = Make("mods", "new");
        var mod = registry.Add("A", oldFolder);

        registry.Relink(mod.Id, oldFolder, newFolder);

        var reloaded = Fresh();
        Assert.Equal(new[] { newFolder }, reloaded.Get(mod.Id)!.Paths);
        Assert.Equal(mod.Id, reloaded.Resolve(Path.Combine(newFolder, "x"))!.Id);
        Assert.Null(reloaded.Resolve(Path.Combine(oldFolder, "x")));
    }

    [Fact]
    public void Relink_rejects_an_unknown_old_path_and_a_path_of_another_mod()
    {
        var registry = Fresh();
        var folderA = Make("mods", "a");
        var folderB = Make("mods", "b");
        var a = registry.Add("A", folderA);
        registry.Add("B", folderB);

        Assert.Throws<InvalidOperationException>(() => registry.Relink(a.Id, Make("mods", "fremd"), Make("mods", "neu")));
        Assert.Throws<InvalidOperationException>(() => registry.Relink(a.Id, folderA, folderB));
        Assert.Equal(new[] { folderA }, registry.Get(a.Id)!.Paths);
    }

    [Fact]
    public void Remove_drops_the_mod_from_the_registry()
    {
        var registry = Fresh();
        var mod = registry.Add("A", Make("mods", "a"));

        registry.Remove(mod.Id);

        Assert.Empty(Fresh().Mods);
        Assert.Throws<InvalidOperationException>(() => registry.Remove(mod.Id));
    }

    [Fact]
    public void Ignore_is_persisted_and_cleared_by_add()
    {
        var registry = Fresh();
        var folder = Make("mods", "alt");

        registry.Ignore(folder);

        Assert.True(Fresh().IsIgnored(folder.ToUpperInvariant()));

        registry.Add("Doch eine Mod", folder);

        Assert.False(Fresh().IsIgnored(folder));
    }

    [Fact]
    public void IsIgnored_is_false_for_invalid_paths()
    {
        Assert.False(Fresh().IsIgnored("a\0b"));
        Assert.False(Fresh().IsIgnored(""));
    }

    [Fact]
    public void DataDirectory_lies_in_the_game_folder_under_the_id()
    {
        var registry = Fresh();
        var mod = registry.Add("A", Make("mods", "a"));

        Assert.Equal(Path.Combine(GameDir, "mods", mod.Id), registry.DataDirectory(mod.Id));
        Assert.Throws<InvalidOperationException>(() => registry.DataDirectory("unbekannt"));
    }

    [Fact]
    public void ToRelative_uses_forward_slashes_and_the_longest_matching_path()
    {
        var registry = Fresh();
        var folder = Make("mods", "a");
        var mod = registry.Add("A", folder);

        Assert.Equal("common/ideas/a.txt", registry.ToRelative(mod.Id, Path.Combine(folder, "common", "ideas", "a.txt")));
        Assert.Equal(string.Empty, registry.ToRelative(mod.Id, folder));
        Assert.Null(registry.ToRelative(mod.Id, Path.Combine(Dir, "mods", "b", "x.txt")));
        Assert.Null(registry.ToRelative("unbekannt", folder));
        Assert.Null(registry.ToRelative(mod.Id, "a\0b"));
    }

    [Fact]
    public void SuggestName_reads_the_name_from_the_metadata_file()
    {
        var folder = Make("mods", "ordnername");
        Directory.CreateDirectory(Path.Combine(folder, ".metadata"));
        File.WriteAllText(Path.Combine(folder, ".metadata", "metadata.json"), "﻿{ \"Name\": \" Idea Variation 2 \", \"version\": \"2.0.0\" }");

        Assert.Equal("Idea Variation 2", ModRegistry.SuggestName(folder, ".metadata/metadata.json"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ kaputt")]
    [InlineData("{ \"name\": 5 }")]
    [InlineData("{ \"name\": \"  \" }")]
    [InlineData("[1, 2]")]
    public void SuggestName_falls_back_to_the_folder_name(string? metadata)
    {
        var folder = Make("mods", "ordnername");
        if (metadata is not null)
        {
            Directory.CreateDirectory(Path.Combine(folder, ".metadata"));
            File.WriteAllText(Path.Combine(folder, ".metadata", "metadata.json"), metadata);
        }

        Assert.Equal("ordnername", ModRegistry.SuggestName(folder + Path.DirectorySeparatorChar, ".metadata/metadata.json"));
    }

    [Fact]
    public void SuggestName_for_a_drive_root_is_just_Mod()
    {
        Assert.Equal("Mod", ModRegistry.SuggestName(Path.GetPathRoot(Dir)!, ".metadata/metadata.json"));
    }

    [Fact]
    public void SuggestName_reads_the_name_from_a_classic_sibling_mod_file()
    {
        var folder = Make("mods", "ordnername");
        File.WriteAllText(Path.Combine(Dir, "mods", "ordnername.mod"), "name = \"Idea Variation 2\"\n");

        Assert.Equal("Idea Variation 2", ModRegistry.SuggestName(folder, ModMetadata.ClassicSiblingMetadataFile));
    }

    [Fact]
    public void Concurrent_writers_never_leave_a_broken_file()
    {
        Directory.CreateDirectory(GameDir);
        var folders = Enumerable.Range(0, 8).Select(i => Make("mods", "m" + i)).ToList();

        Parallel.For(0, folders.Count, i =>
        {
            var registry = ModRegistry.Load(GameDir);
            var mod = registry.Add("Mod " + i, folders[i]);
            registry.Rename(mod.Id, "Mod " + i + " neu");
        });

        var reloaded = ModRegistry.Load(GameDir);
        Assert.Null(reloaded.LoadWarning);
        Assert.False(File.Exists(reloaded.FilePath + ".bad"));
        Assert.NotEmpty(reloaded.Mods);
    }
}
