using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class DesyncCacheTests
{
    private static DesyncDiffResult Result(int formatVersion = DesyncComparison.FormatVersion)
    {
        var units = new List<UnitDiffDetail>
        {
            new("countries/database/1384", UnitChange.Changed, "Bulgaria", "Bulgaria", 0, 0, 120, 118,
                new[] { new FieldDiff("government/parliament_issue_support", "0.47488", "0.2453", FieldChangeKind.Changed) },
                false, false),
            new("subunit_manager/database/637537109", UnitChange.SlotReused, "arran", "suffolk_province", 0x26, 0x28, 40, 50,
                Array.Empty<FieldDiff>(), false, false),
            new("", UnitChange.OwnScalarsChanged, null, null, null, null, 300000, 300001, Array.Empty<FieldDiff>(), true, false),
        };
        var names = new SaveNameTables(new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["countries/database/{id}"] = new Dictionary<string, string> { ["1384"] = "BLG" },
        });
        return new DesyncDiffResult(formatVersion, "11c0de175691b2bb", 1, 2, SaveReadState.Complete, SaveReadState.Incomplete,
            units, names, TimeSpan.FromSeconds(12.5), null, 4_194_304);
    }

    [Fact]
    public void A_saved_result_loads_again_with_the_same_stamp()
    {
        using var tree = new TempTree();
        var cache = new DesyncCache(tree.PathOf("desync/cache"));

        cache.Save(Result(), "stamp-1");
        var loaded = cache.TryLoad("11c0de175691b2bb", "stamp-1");

        Assert.NotNull(loaded);
        Assert.True(File.Exists(tree.PathOf("desync/cache/11c0de175691b2bb.json")));
        Assert.Single(Directory.GetFiles(tree.PathOf("desync/cache")));
        Assert.Equal(DesyncComparison.FormatVersion, loaded!.FormatVersion);
        Assert.Equal("11c0de175691b2bb", loaded.OosId16);
        Assert.Equal(1, loaded.ReferenceMachine);
        Assert.Equal(2, loaded.DesyncedMachine);
        Assert.Equal(SaveReadState.Complete, loaded.ReferenceState);
        Assert.Equal(SaveReadState.Incomplete, loaded.DesyncedState);
        Assert.Equal(TimeSpan.FromSeconds(12.5), loaded.Duration);
        Assert.Null(loaded.ReferenceStoppedAt);
        Assert.Equal(4_194_304, loaded.DesyncedStoppedAt);
        Assert.Equal("BLG", loaded.Names.Tables["countries/database/{id}"]["1384"]);

        Assert.Equal(3, loaded.Units.Count);
        var changed = loaded.Units[0];
        Assert.Equal(Result().Units[0] with { Fields = changed.Fields }, changed);
        Assert.Equal(Result().Units[0].Fields, changed.Fields);
        var reused = loaded.Units[1];
        Assert.Equal(UnitChange.SlotReused, reused.Change);
        Assert.Equal(0x28, reused.DesyncedGeneration);
        Assert.Equal("suffolk_province", reused.DesyncedLabel);
        var own = loaded.Units[2];
        Assert.Equal("", own.Path);
        Assert.Null(own.ReferenceGeneration);
        Assert.True(own.FieldsTruncated);
    }

    [Fact]
    public void Saving_again_replaces_the_file()
    {
        using var tree = new TempTree();
        var cache = new DesyncCache(tree.PathOf("cache"));

        cache.Save(Result(), "stamp-1");
        cache.Save(Result() with { ReferenceMachine = 3 }, "stamp-2");

        Assert.Null(cache.TryLoad("11c0de175691b2bb", "stamp-1"));
        Assert.Equal(3, cache.TryLoad("11c0de175691b2bb", "stamp-2")!.ReferenceMachine);
        Assert.Single(Directory.GetFiles(tree.PathOf("cache")));
    }

    [Fact]
    public void A_different_stamp_or_format_version_misses()
    {
        using var tree = new TempTree();
        var cache = new DesyncCache(tree.PathOf("cache"));

        cache.Save(Result(), "stamp-1");
        Assert.Null(cache.TryLoad("11c0de175691b2bb", "stamp-2"));
        Assert.Null(cache.TryLoad("2e5541e097da87ce", "stamp-1"));

        cache.Save(Result(DesyncComparison.FormatVersion + 1), "stamp-1");
        Assert.Null(cache.TryLoad("11c0de175691b2bb", "stamp-1"));
    }

    [Fact]
    public void A_broken_cache_file_is_a_miss_not_an_exception()
    {
        using var tree = new TempTree(("cache/11c0de175691b2bb.json", "{ \"stamp\": \"stamp-1\", \"result\": { \"units\": [ 1, "));
        var cache = new DesyncCache(tree.PathOf("cache"));

        Assert.Null(cache.TryLoad("11c0de175691b2bb", "stamp-1"));
        Assert.Null(cache.TryLoad("11c0de175691b2bb", "stamp-1"));
        Assert.Null(new DesyncCache(tree.PathOf("missing")).TryLoad("11c0de175691b2bb", "stamp-1"));

        File.WriteAllText(tree.PathOf("cache/11c0de175691b2bb.json"), "null");
        Assert.Null(cache.TryLoad("11c0de175691b2bb", "stamp-1"));

        // Ein gültiges Ergebnis mit einer leeren Einheit gilt ebenfalls als nicht vorhanden.
        cache.Save(Result(), "stamp-1");
        var path = tree.PathOf("cache/11c0de175691b2bb.json");
        var text = File.ReadAllText(path);
        var units = text.IndexOf("\"units\":[", StringComparison.Ordinal);
        Assert.True(units >= 0);
        File.WriteAllText(path, text.Insert(units + "\"units\":[".Length, "null,"));
        Assert.Null(cache.TryLoad("11c0de175691b2bb", "stamp-1"));
    }

    [Fact]
    public void A_result_saved_with_another_profile_misses()
    {
        using var tree = new TempTree();
        Dumps.Zip(tree.PathOf("a.zip"), "", 1, Dumps.HostMetadata11c0, (DumpFileKind.OosSave, "a=1"));
        Dumps.Zip(tree.PathOf("b.zip"), "", 2, Dumps.ClientMetadata11c0, (DumpFileKind.OosSave, "a=2"));
        using var a = OosDump.Open(tree.PathOf("a.zip"));
        using var b = OosDump.Open(tree.PathOf("b.zip"));
        var cache = new DesyncCache(tree.PathOf("cache"));
        var before = DesyncProfile.Parse("""{ "objects": [ "*/database/{id}" ], "nodeDepth": 4 }""");
        var after = DesyncProfile.Parse("""{ "objects": [ "*/database/{id}" ], "nodeDepth": 5 }""");

        cache.Save(Result(), DesyncCache.StampOf(a, b, before));

        Assert.NotNull(cache.TryLoad("11c0de175691b2bb", DesyncCache.StampOf(a, b, before)));
        Assert.Null(cache.TryLoad("11c0de175691b2bb", DesyncCache.StampOf(a, b, after)));
    }

    [Fact]
    public void The_stamp_changes_with_the_saves_the_profile_and_the_format_version()
    {
        using var tree = new TempTree();
        Dumps.Zip(tree.PathOf("a.zip"), "", 1, Dumps.HostMetadata11c0, (DumpFileKind.OosSave, "a=1"));
        Dumps.Zip(tree.PathOf("b.zip"), "", 2, Dumps.ClientMetadata11c0, (DumpFileKind.OosSave, "a=2"));
        Dumps.Zip(tree.PathOf("c.zip"), "", 2, Dumps.ClientMetadata11c0, (DumpFileKind.OosSave, "a=22"));
        using var a = OosDump.Open(tree.PathOf("a.zip"));
        using var b = OosDump.Open(tree.PathOf("b.zip"));
        using var c = OosDump.Open(tree.PathOf("c.zip"));

        var profile = DesyncProfile.Parse("""{ "objects": [ "*/database/{id}" ], "nodeDepth": 4 }""");
        var deeper = DesyncProfile.Parse("""{ "objects": [ "*/database/{id}" ], "nodeDepth": 5 }""");

        var stamp = DesyncCache.StampOf(a, b, profile);

        Assert.Equal(stamp, DesyncCache.StampOf(a, b, DesyncProfile.Parse("""{ "objects": [ "*/database/{id}" ], "nodeDepth": 4 }""")));
        Assert.NotEqual(stamp, DesyncCache.StampOf(a, c, profile));
        Assert.NotEqual(stamp, DesyncCache.StampOf(a, b, deeper));
        Assert.StartsWith(DesyncComparison.FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|", stamp);
    }
}
