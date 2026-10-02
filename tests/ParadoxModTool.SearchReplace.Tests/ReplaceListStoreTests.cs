using ParadoxModTool.SearchReplace.ReplaceLists;
using Xunit;

namespace ParadoxModTool.SearchReplace.Tests;

/// <summary>Ersetzungslisten pro Spiel und Mod: Speichern mit Zusammenführen, kaputte Datei, Export und Import.</summary>
public sealed class ReplaceListStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pmt-replacestore-" + Guid.NewGuid().ToString("N"));

    private string File1 => Path.Combine(_folder, "replace-lists", "mod.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static ReplaceList List(string name, params ReplaceRule[] rules) =>
        new() { Name = name, TargetFolder = "history/provinces", Rules = rules.ToList() };

    private static ReplaceListSet Add(ReplaceListSet set, ReplaceList list)
    {
        set.Lists.Add(list);
        return set;
    }

    [Fact]
    public void File_lies_below_the_game_directory()
    {
        Assert.Equal(Path.Combine(@"C:\g", "replace-lists", "local-X.json"), ReplaceListStore.FileFor(@"C:\g", "local-X"));
    }

    [Fact]
    public void Missing_file_loads_empty()
    {
        Assert.Empty(new ReplaceListStore(File1).Load().Lists);
    }

    [Fact]
    public void Lists_survive_save_and_load_in_order()
    {
        var store = new ReplaceListStore(File1);
        store.Update(set => Add(set, List("B", new ReplaceRule { Search = "a", Replace = "#a" }, new ReplaceRule { Search = "b", Replace = "", Enabled = false }, new ReplaceRule { Search = "c", Replace = "d e" })));
        store.Update(set => Add(set, List("A")));

        var loaded = store.Load();

        Assert.Equal(new[] { "B", "A" }, loaded.Lists.Select(l => l.Name));
        Assert.Equal("history/provinces", loaded.Lists[0].TargetFolder);
        Assert.Equal(new[] { "a", "b", "c" }, loaded.Lists[0].Rules.Select(r => r.Search));
        Assert.Equal(new[] { "#a", "", "d e" }, loaded.Lists[0].Rules.Select(r => r.Replace));
        Assert.Equal(new[] { true, false, true }, loaded.Lists[0].Rules.Select(r => r.Enabled));
    }

    [Fact]
    public void Two_updates_from_two_stores_keep_both_lists()
    {
        var first = new ReplaceListStore(File1);
        var second = new ReplaceListStore(File1);
        first.Load();
        second.Load();

        first.Update(set => Add(set, List("one")));
        second.Update(set => Add(set, List("two")));

        Assert.Equal(new[] { "one", "two" }, first.Load().Lists.Select(l => l.Name));
    }

    [Fact]
    public void Broken_file_is_kept_and_loading_starts_empty()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File1)!);
        File.WriteAllText(File1, "{ not json");

        var set = new ReplaceListStore(File1).Load();

        Assert.Empty(set.Lists);
        Assert.Equal("{ not json", File.ReadAllText(Path.Combine(_folder, "replace-lists", "mod.broken.json")));
        Assert.False(File.Exists(File1));
    }

    [Fact]
    public void Other_version_counts_as_broken()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File1)!);
        File.WriteAllText(File1, "{ \"version\": 2, \"lists\": [] }");

        Assert.Empty(new ReplaceListStore(File1).Load().Lists);
        Assert.True(File.Exists(Path.Combine(_folder, "replace-lists", "mod.broken.json")));
    }

    private ReplaceListSet LoadRaw(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File1)!);
        File.WriteAllText(File1, json);
        return new ReplaceListStore(File1).Load();
    }

    [Fact]
    public void Null_entries_are_dropped_and_null_texts_become_empty()
    {
        var set = LoadRaw("{\"version\":1,\"lists\":[null,{\"name\":null,\"targetFolder\":null,\"rules\":[null,{\"search\":null,\"replace\":null}]}]}");

        var list = Assert.Single(set.Lists);
        Assert.Equal("-", list.Name);
        Assert.Equal(string.Empty, list.TargetFolder);
        var rule = Assert.Single(list.Rules);
        Assert.Equal((string.Empty, string.Empty), (rule.Search, rule.Replace));
    }

    [Theory]
    [InlineData("/", "")]
    [InlineData("../../x", "")]
    [InlineData("C:\\\\x", "")]
    [InlineData("history\\\\provinces", "history/provinces")]
    [InlineData("a/../../b", "")]
    public void Target_folder_outside_the_mod_becomes_the_mod_root(string stored, string expected)
    {
        var set = LoadRaw("{\"version\":1,\"lists\":[{\"name\":\"a\",\"targetFolder\":\"" + stored + "\",\"rules\":[]}]}");

        Assert.Equal(expected, Assert.Single(set.Lists).TargetFolder);
    }

    [Fact]
    public void Foreign_file_without_version_counts_as_broken()
    {
        Assert.Empty(LoadRaw("{\"foo\":2}").Lists);
        Assert.True(File.Exists(Path.Combine(_folder, "replace-lists", "mod.broken.json")));
    }

    [Fact]
    public void Pascal_case_file_loads()
    {
        var set = LoadRaw("{\"Version\":1,\"Lists\":[{\"Name\":\"a\",\"TargetFolder\":\"history\",\"Rules\":[{\"Search\":\"x\",\"Replace\":\"#x\"}]}]}");

        var list = Assert.Single(set.Lists);
        Assert.Equal(("a", "history", "x"), (list.Name, list.TargetFolder, list.Rules.Single().Search));
    }

    [Fact]
    public void Import_cleans_the_list_like_loading()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "import.json");
        File.WriteAllText(path, "{\"version\":1,\"list\":{\"name\":null,\"targetFolder\":\"../x\",\"rules\":[null,{\"search\":null,\"replace\":\"y\"}]}}");

        var list = ReplaceListStore.Import(path);

        Assert.Equal(string.Empty, list.Name);
        Assert.Equal(string.Empty, list.TargetFolder);
        Assert.Equal((string.Empty, "y"), (list.Rules.Single().Search, list.Rules.Single().Replace));
    }

    [Fact]
    public void Export_and_import_round_trip()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "export.json");

        ReplaceListStore.Export(List("Startgebäude", new ReplaceRule { Search = "x", Replace = "#x", Enabled = false }), path);
        var imported = ReplaceListStore.Import(path);

        Assert.Equal("Startgebäude", imported.Name);
        Assert.Equal("history/provinces", imported.TargetFolder);
        var rule = Assert.Single(imported.Rules);
        Assert.Equal(("x", "#x", false), (rule.Search, rule.Replace, rule.Enabled));
    }

    [Fact]
    public void Import_of_a_file_without_list_fails()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "other.json");
        File.WriteAllText(path, "{ \"version\": 1 }");

        Assert.Throws<InvalidDataException>(() => ReplaceListStore.Import(path));
    }

    [Fact]
    public void Unique_name_adds_a_number()
    {
        var set = new ReplaceListSet { Lists = { List("A"), List("A (2)") } };

        Assert.Equal("a (3)", ReplaceListStore.UniqueName(set, "a"));
        Assert.Equal("B", ReplaceListStore.UniqueName(set, "B"));
    }

    [Fact]
    public void Name_rules()
    {
        var set = new ReplaceListSet { Lists = { List("A"), List("B") } };

        Assert.False(ReplaceListStore.IsValidName(set, "  ", null));
        Assert.False(ReplaceListStore.IsValidName(set, "b", "A"));
        Assert.True(ReplaceListStore.IsValidName(set, "A", "A"));
        Assert.True(ReplaceListStore.IsValidName(set, "C", null));
    }

    [Fact]
    public void Rule_kind_and_safe_comment_survive_save_and_load_as_text()
    {
        var store = new ReplaceListStore(File1);
        store.Update(set => Add(set, List("T", new ReplaceRule { Search = "a = *", Replace = "#a = $1", Mode = RuleMode.Pattern, SafeComment = true }, new ReplaceRule { Search = "w", Replace = "x" })));

        var rules = store.Load().Lists.Single().Rules;

        Assert.Equal(RuleMode.Pattern, rules[0].Mode);
        Assert.True(rules[0].SafeComment);
        Assert.Equal(RuleMode.Word, rules[1].Mode);
        Assert.False(rules[1].SafeComment);
        Assert.Contains("\"mode\": \"Pattern\"", File.ReadAllText(File1));
    }

    [Fact]
    public void A_file_from_before_the_rule_kinds_loads_every_rule_as_a_word_rule()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File1)!);
        File.WriteAllText(File1, "{\"version\":1,\"lists\":[{\"name\":\"Alt\",\"targetFolder\":\"x\",\"rules\":[{\"search\":\"a\",\"replace\":\"#a\",\"enabled\":true}]}]}");

        var rule = new ReplaceListStore(File1).Load().Lists.Single().Rules.Single();

        Assert.Equal(RuleMode.Word, rule.Mode);
        Assert.False(rule.SafeComment);
    }
}
