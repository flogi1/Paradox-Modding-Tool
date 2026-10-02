using System.Text.Json;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class DesyncSourcesStoreTests
{
    [Fact]
    public void A_missing_file_reads_as_empty()
    {
        using var tree = new TempTree();
        var store = new DesyncSourcesStore(tree.PathOf("desync/sources.json"));

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Added_paths_come_back_from_a_new_store_in_the_order_they_were_added()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("desync/sources.json");

        new DesyncSourcesStore(path).Add(@"C:\dumps\11c0de175691b2bb_0.zip");
        new DesyncSourcesStore(path).Add(@"D:\from_flogi");

        Assert.Equal(new[] { @"C:\dumps\11c0de175691b2bb_0.zip", @"D:\from_flogi" }, new DesyncSourcesStore(path).Load());
    }

    [Fact]
    public void A_removed_path_is_gone_after_reloading()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("desync/sources.json");
        var store = new DesyncSourcesStore(path);
        store.Add(@"C:\dumps\a.zip");
        store.Add(@"C:\dumps\b.zip");

        store.Remove(@"C:\dumps\a.zip");

        Assert.Equal(new[] { @"C:\dumps\b.zip" }, new DesyncSourcesStore(path).Load());
    }

    [Fact]
    public void The_same_path_is_added_once_regardless_of_case()
    {
        using var tree = new TempTree();
        var store = new DesyncSourcesStore(tree.PathOf("desync/sources.json"));

        store.Add(@"C:\Dumps\A.zip");
        store.Add(@"c:\dumps\a.ZIP");

        Assert.Equal(new[] { @"C:\Dumps\A.zip" }, store.Load());
    }

    [Fact]
    public void Removing_ignores_case()
    {
        using var tree = new TempTree();
        var store = new DesyncSourcesStore(tree.PathOf("desync/sources.json"));
        store.Add(@"C:\Dumps\A.zip");

        store.Remove(@"c:\dumps\a.zip");

        Assert.Empty(store.Load());
    }

    [Fact]
    public void The_file_has_version_1_and_a_paths_list_and_no_temporary_file_stays_behind()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("desync/sources.json");

        new DesyncSourcesStore(path).Add(@"C:\dumps\a.zip");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(@"C:\dumps\a.zip", Assert.Single(document.RootElement.GetProperty("paths").EnumerateArray()).GetString());
        Assert.Equal(new[] { path }, Directory.GetFiles(tree.PathOf("desync")));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"version\": 1, \"paths\": null }")]
    [InlineData("{ \"version\": 1, \"paths\": [ 3, \"C:\\\\x\" ] }")]
    public void A_broken_file_reads_as_empty(string content)
    {
        using var tree = new TempTree(("desync/sources.json", content));
        var store = new DesyncSourcesStore(tree.PathOf("desync/sources.json"));

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Adding_to_a_broken_file_replaces_it_with_a_valid_one()
    {
        using var tree = new TempTree(("desync/sources.json", "{ not json"));
        var store = new DesyncSourcesStore(tree.PathOf("desync/sources.json"));

        store.Add(@"C:\dumps\a.zip");

        Assert.Equal(new[] { @"C:\dumps\a.zip" }, store.Load());
    }

    [Fact]
    public void Blank_entries_in_the_file_are_skipped()
    {
        using var tree = new TempTree(("desync/sources.json", "{ \"version\": 1, \"paths\": [ \"\", \"  \", \"C:\\\\x\" ] }"));
        var store = new DesyncSourcesStore(tree.PathOf("desync/sources.json"));

        Assert.Equal(new[] { @"C:\x" }, store.Load());
    }

    [Fact]
    public void Adding_or_removing_while_the_file_cannot_be_read_throws_and_keeps_the_file()
    {
        using var tree = new TempTree();
        var path = tree.PathOf("desync/sources.json");
        var store = new DesyncSourcesStore(path);
        store.Add("D:/dumps/a.zip");
        var before = File.ReadAllText(path);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => store.Add("D:/dumps/b.zip"));
            Assert.ThrowsAny<IOException>(() => store.Remove("D:/dumps/a.zip"));
            Assert.Empty(store.Load());
        }

        Assert.Equal(before, File.ReadAllText(path));
    }
}
