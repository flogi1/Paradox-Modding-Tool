using System.Text;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class SaveIndexerTests
{
    private const string DatabaseProfile = """{ "objects": [ "*/database/{id}", "database/{id}" ], "nodeDepth": 4 }""";

    private static DesyncProfile Profile(string json = DatabaseProfile) => DesyncProfile.Parse(json);

    private static SaveIndex Index(string text, DesyncProfile? profile = null) =>
        Index(Encoding.UTF8.GetBytes(text), profile);

    private static SaveIndex Index(byte[] bytes, DesyncProfile? profile = null) =>
        SaveIndexer.Read(new MemoryStream(bytes), profile ?? Profile(), null, CancellationToken.None);

    private static SaveNode Node(SaveIndex index, string path)
    {
        for (var id = 0; id < index.Nodes.Count; id++)
        {
            if (index.PathOf(id) == path)
            {
                return index.Nodes[id];
            }
        }

        throw new Xunit.Sdk.XunitException("Kein Knoten mit Pfad " + path);
    }

    private static bool HasNode(SaveIndex index, string path) =>
        Enumerable.Range(0, index.Nodes.Count).Any(id => index.PathOf(id) == path);

    private static string TextOf(string text, SaveNode node)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return Encoding.UTF8.GetString(bytes, (int)node.Start, (int)(node.End - node.Start));
    }

    [Fact]
    public void Units_below_database_containers_get_slot_and_generation()
    {
        const string text = """
            countries={
                database={
                    1795162163={ name="a" }
                }
            }
            """;
        var index = Index(text);

        var unit = Node(index, "countries/database/1795162163");
        Assert.Equal(NodeKind.Unit, unit.Kind);
        Assert.Equal(51, unit.Slot);
        Assert.Equal(107, unit.Generation);
        Assert.Equal("51", unit.Name);
        Assert.Equal("""1795162163={ name="a" }""", TextOf(text, unit));
        Assert.Equal(NodeKind.Section, Node(index, "countries").Kind);
        Assert.Equal(NodeKind.Block, Node(index, "countries/database").Kind);
        Assert.Equal(NodeKind.Root, index.Nodes[0].Kind);
        Assert.Equal(SaveReadState.Complete, index.State);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), index.Length);
        Assert.All(index.Nodes.Select((node, id) => (node, id)), pair => Assert.Equal(pair.id, pair.node.Id));
    }

    [Fact]
    public void Equal_text_gives_equal_full_hashes_and_a_changed_field_changes_unit_and_ancestors_only()
    {
        const string template = """
            random_seed=1
            countries={
                database={
                    1={ name="a" gold=VALUE }
                    2={ name="b" gold=3 }
                }
            }
            war_manager={ x=1 }
            """;
        var first = Index(template.Replace("VALUE", "5"));
        var same = Index(template.Replace("VALUE", "5"));
        var changed = Index(template.Replace("VALUE", "6"));

        Assert.Equal(first.Nodes.Select(node => node.FullHash), same.Nodes.Select(node => node.FullHash));
        Assert.Equal(first.Nodes.Select(node => node.OwnHash), same.Nodes.Select(node => node.OwnHash));

        foreach (var path in new[] { "countries/database/1", "countries/database", "countries" })
        {
            Assert.NotEqual(Node(first, path).FullHash, Node(changed, path).FullHash);
        }

        Assert.NotEqual(first.Nodes[0].FullHash, changed.Nodes[0].FullHash);
        Assert.Equal(Node(first, "countries/database/2").FullHash, Node(changed, "countries/database/2").FullHash);
        Assert.Equal(Node(first, "war_manager").FullHash, Node(changed, "war_manager").FullHash);
        Assert.Equal(Node(first, "countries/database").OwnHash, Node(changed, "countries/database").OwnHash);
        Assert.Equal(first.Nodes[0].OwnHash, changed.Nodes[0].OwnHash);
    }

    [Fact]
    public void A_node_range_starts_at_its_key_and_the_key_bytes_hash_into_the_child()
    {
        const string host = "database={ 637537109={ a=1 } }";
        const string client = "database={ 671091541={ a=1 } }";
        var hostIndex = Index(host);
        var clientIndex = Index(client);

        var hostDatabase = Node(hostIndex, "database");
        var clientDatabase = Node(clientIndex, "database");
        Assert.Equal(hostDatabase.OwnHash, clientDatabase.OwnHash);
        Assert.NotEqual(hostDatabase.FullHash, clientDatabase.FullHash);

        var unit = Node(hostIndex, "database/637537109");
        Assert.Equal((byte)'6', Encoding.UTF8.GetBytes(host)[unit.Start]);
        Assert.Equal("637537109={ a=1 }", TextOf(host, unit));
        Assert.Equal(host, TextOf(host, hostDatabase));
        Assert.Equal(Node(clientIndex, "database/671091541").Slot, unit.Slot);
        Assert.NotEqual(Node(clientIndex, "database/671091541").Generation, unit.Generation);
    }

    [Fact]
    public void Scalar_entries_in_object_containers_are_scalar_units_with_slot_and_generation()
    {
        const string text = """
            construction_manager={
                database={
                    3003121664=none
                    1979712231={ type=CIVIL }
                    5="quoted value"
                }
            }
            """;
        var index = Index(text);

        var none = Node(index, "construction_manager/database/3003121664");
        Assert.Equal(NodeKind.ScalarUnit, none.Kind);
        Assert.Equal(0, none.Slot);
        Assert.Equal(179, none.Generation);
        Assert.Equal("0", none.Name);
        Assert.Equal("3003121664=none", TextOf(text, none));
        Assert.Equal("5=\"quoted value\"", TextOf(text, Node(index, "construction_manager/database/5")));
        Assert.Equal(NodeKind.Unit, Node(index, "construction_manager/database/1979712231").Kind);

        var database = Node(index, "construction_manager/database");
        Assert.Equal(3, index.ChildrenOf(database.Id).Count);
    }

    [Fact]
    public void Own_hash_ignores_child_nodes()
    {
        const string template = "section={ own=1 child={ value=VALUE } other=2 }";
        var first = Index(template.Replace("VALUE", "1"));
        var second = Index(template.Replace("VALUE", "2"));

        Assert.Equal(Node(first, "section").OwnHash, Node(second, "section").OwnHash);
        Assert.NotEqual(Node(first, "section").FullHash, Node(second, "section").FullHash);
        Assert.NotEqual(Node(first, "section/child").OwnHash, Node(second, "section/child").OwnHash);

        var ownChanged = Index("section={ own=3 child={ value=1 } other=2 }");
        Assert.NotEqual(Node(first, "section").OwnHash, Node(ownChanged, "section").OwnHash);
        Assert.Equal(Node(first, "section/child").FullHash, Node(ownChanged, "section/child").FullHash);
    }

    [Fact]
    public void Braces_and_escaped_quotes_inside_strings_do_not_change_depth()
    {
        const string text = """
            metadata={
                flag="POR={
            	pattern=\"pattern_solid.dds\"
            	colored_emblem={ texture=\"ce_border.dds\" }
            }"
                date=1425.2.4
            }
            random_count=5
            """;
        var index = Index(text);

        Assert.Equal(SaveReadState.Complete, index.State);
        Assert.Equal("5", index.Scalars["random_count"]);
        Assert.Equal("1425.2.4", index.Scalars["metadata/date"]);
        Assert.StartsWith("POR={", index.Scalars["metadata/flag"]);
        Assert.Contains("pattern=\"pattern_solid.dds\"", index.Scalars["metadata/flag"]);
        Assert.Equal(new[] { "metadata" }, index.ChildrenOf(0).Select(id => index.PathOf(id)));
    }

    [Fact]
    public void Indentation_is_ignored_nested_content_at_column_zero_is_still_nested()
    {
        const string text = """
            countries={
            database={
            1384={
            name="a"
            }
            }
            }
            after=1
            """;
        var index = Index(text);

        Assert.Equal(NodeKind.Unit, Node(index, "countries/database/1384").Kind);
        Assert.Equal("1", index.Scalars["after"]);
        Assert.False(index.Scalars.ContainsKey("name"));
        Assert.Equal(SaveReadState.Complete, index.State);
    }

    [Fact]
    public void Repeated_keys_get_occurrence_names_and_anonymous_blocks_get_positions_or_their_id_field()
    {
        const string text = """
            played_country={ name=a }
            played_country={ name=b }
            outbreaks={
                sub_units={ { sub_unit=7 x=1 } { x=1 } { x=2 sub_unit=7 } }
            }
            """;
        var profile = Profile("""{ "objects": [ "*/database/{id}" ], "anonymousIds": { "**/sub_units": "sub_unit" }, "nodeDepth": 4 }""");
        var index = Index(text, profile);

        Assert.Equal(new[] { "played_country", "played_country#2", "outbreaks" }, index.ChildrenOf(0).Select(id => index.Nodes[id].Name));
        var subUnits = Node(index, "outbreaks/sub_units");
        Assert.Equal(new[] { "sub_unit=7", "[1]", "sub_unit=7#2" }, index.ChildrenOf(subUnits.Id).Select(id => index.Nodes[id].Name));
        Assert.Equal("outbreaks/sub_units/[1]", index.PathOf(index.ChildrenOf(subUnits.Id)[1]));
        Assert.All(index.ChildrenOf(subUnits.Id), id => Assert.Equal(NodeKind.Block, index.Nodes[id].Kind));
    }

    [Fact]
    public void Blocks_deeper_than_node_depth_are_not_nodes()
    {
        const string template = "a={ b={ c={ x=VALUE } y=1 } }";
        var profile = Profile("""{ "objects": [], "nodeDepth": 2 }""");
        var first = Index(template.Replace("VALUE", "1"), profile);
        var second = Index(template.Replace("VALUE", "2"), profile);

        Assert.True(HasNode(first, "a/b"));
        Assert.False(HasNode(first, "a/b/c"));
        Assert.Empty(first.ChildrenOf(Node(first, "a/b").Id));
        Assert.NotEqual(Node(first, "a/b").OwnHash, Node(second, "a/b").OwnHash);
        Assert.Equal(Node(first, "a").OwnHash, Node(second, "a").OwnHash);
    }

    [Fact]
    public void Nothing_inside_a_unit_becomes_a_node()
    {
        var index = Index("countries={ database={ 7={ inner={ deeper={ x=1 } } } } }");

        var unit = Node(index, "countries/database/7");
        Assert.Empty(index.ChildrenOf(unit.Id));
        Assert.Equal(4, index.Nodes.Count);
    }

    [Fact]
    public void Country_tags_and_location_keys_become_name_tables()
    {
        const string text = """
            metadata={ compatibility={ version=1 locations={stockholm uppsala} } }
            countries={ tags={ 1=PIR 1384=BLG } database={ 1384={ x=1 } } }
            """;
        var profile = Profile("""
            {
              "objects": [ "*/database/{id}", "locations/locations/{id}" ],
              "nodeDepth": 2,
              "names": {
                "countries/database/{id}": { "tagList": "countries/tags" },
                "locations/locations/{id}": { "keyList": "metadata/compatibility/locations", "keyListBase": 1 },
                "units/database/{id}": { "tagList": "units/names" }
              }
            }
            """);
        var index = Index(text, profile);

        Assert.Equal("BLG", index.NameTables["countries/database/{id}"]["1384"]);
        Assert.Equal("PIR", index.NameTables["countries/database/{id}"]["1"]);
        Assert.Equal("stockholm", index.NameTables["locations/locations/{id}"]["1"]);
        Assert.Equal("uppsala", index.NameTables["locations/locations/{id}"]["2"]);
        Assert.Empty(index.NameTables["units/database/{id}"]);
        Assert.False(HasNode(index, "metadata/compatibility/locations"));
    }

    [Fact]
    public void Enabled_dlcs_are_read_from_metadata()
    {
        const string text = """
            metadata={
                enabled_dlcs={ "Ancient Monument Pack" "Shared DLC Data" }
                version="1.0.3"
            }
            """;
        var index = Index(text);

        Assert.Equal(new[] { "Ancient Monument Pack", "Shared DLC Data" }, index.EnabledDlcs);
        Assert.Equal("1.0.3", index.Scalars["metadata/version"]);
    }

    [Fact]
    public void A_cut_off_save_is_incomplete()
    {
        const string text = "random_seed=1\ncountries={ database={ 1={ x=1 }\n2={ y=";
        var index = Index(text);

        Assert.Equal(SaveReadState.Incomplete, index.State);
        Assert.Equal("1", index.Scalars["random_seed"]);
        Assert.All(index.Nodes, node => Assert.NotNull(node));
        Assert.Equal(text.Length, Node(index, "countries").End);
        Assert.Equal("1={ x=1 }", TextOf(text, Node(index, "countries/database/1")));
    }

    [Fact]
    public void A_binary_save_is_detected_without_reading_it_all()
    {
        var header = Encoding.ASCII.GetBytes("SAV0103a1b2c3d4000000000000\n");
        var bytes = new byte[10 * 1024 * 1024];
        header.CopyTo(bytes, 0);
        for (var i = header.Length; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i % 3 == 0 ? 0x01 : 'a');
        }

        using var stream = new MemoryStream(bytes);
        var index = SaveIndexer.Read(stream, Profile(), null, CancellationToken.None);

        Assert.Equal(SaveReadState.Binary, index.State);
        Assert.True(stream.Position < bytes.Length);
        Assert.Single(index.Nodes);
        Assert.Empty(index.ChildrenOf(0));
    }

    [Fact]
    public void A_text_save_with_a_sav_header_line_is_indexed_after_the_header()
    {
        const string text = "SAV01030000000000\nrandom_seed=4\ncountries={ database={ 1={ x=1 } } }";
        var index = Index(text);

        Assert.Equal(SaveReadState.Complete, index.State);
        Assert.Equal("4", index.Scalars["random_seed"]);
        Assert.Equal("1={ x=1 }", TextOf(text, Node(index, "countries/database/1")));
    }

    [Fact]
    public void Cancel_stops_reading()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            SaveIndexer.Read(new MemoryStream(Encoding.UTF8.GetBytes("a={ b=1 }")), Profile(), null, cancellation.Token));
    }

    [Fact]
    public void Progress_reports_the_bytes_read()
    {
        var reported = new List<long>();
        var bytes = Encoding.UTF8.GetBytes("a={ b=1 }");

        SaveIndexer.Read(new MemoryStream(bytes), Profile(), new SyncProgress(reported.Add), CancellationToken.None);

        Assert.Equal(bytes.Length, reported[^1]);
    }

    [Fact]
    public void Hashes_and_names_do_not_depend_on_where_the_read_buffer_ends()
    {
        // Die Puffergrenze liegt bei 1 MiB; jedes Byte des Datenbanktexts wird einmal genau dorthin geschoben.
        const string database = """database={ 637537109={ s="a\"b" t={ u=1 } } 16777217="n\"o" 33554433=none }""";
        const int boundary = 1 << 20;
        const string prefix = "pad=\"";
        const string afterPad = "\"\n";
        var reference = Index(Save(10));

        for (var offset = 0; offset <= database.Length; offset++)
        {
            var padLength = boundary - prefix.Length - afterPad.Length - offset;
            var shifted = Index(Save(padLength));

            Assert.Equal(SaveReadState.Complete, shifted.State);
            foreach (var path in new[] { "database", "database/637537109", "database/16777217", "database/33554433" })
            {
                var expected = Node(reference, path);
                var actual = Node(shifted, path);
                Assert.Equal(expected.FullHash, actual.FullHash);
                Assert.Equal(expected.OwnHash, actual.OwnHash);
                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.Kind, actual.Kind);
                Assert.Equal(expected.End - expected.Start, actual.End - actual.Start);
            }

            Assert.Equal(prefix.Length + padLength + afterPad.Length, Node(shifted, "database").Start);
        }

        static byte[] Save(int padLength) =>
            Encoding.UTF8.GetBytes(prefix + new string('x', padLength) + afterPad + database);
    }

    [Fact]
    public void Own_hash_ignores_whitespace_outside_strings_but_full_hash_does_not()
    {
        const string compact = """section={ own=1 note="a  b" child={ x=1 } other=2 }""";
        const string spread = "section={\r\n\town=1\r\n\tnote=\"a  b\"\r\n\tchild={\r\n\t\tx=1\r\n\t}\r\n\n\tother=2\r\n}";
        var first = Index(compact);
        var second = Index(spread);

        Assert.Equal(Node(first, "section").OwnHash, Node(second, "section").OwnHash);
        Assert.NotEqual(Node(first, "section").FullHash, Node(second, "section").FullHash);

        var ownChanged = Index(spread.Replace("own=1", "own=2"));
        Assert.NotEqual(Node(second, "section").OwnHash, Node(ownChanged, "section").OwnHash);

        var stringChanged = Index(spread.Replace("a  b", "a b"));
        Assert.NotEqual(Node(second, "section").OwnHash, Node(stringChanged, "section").OwnHash);

        var escapedQuote = Index("section={ note=\"a\\\" b\" }");
        var escapedQuoteChanged = Index("section={ note=\"a\\\"b\" }");
        Assert.NotEqual(Node(escapedQuote, "section").OwnHash, Node(escapedQuoteChanged, "section").OwnHash);
    }

    [Fact]
    public void Own_hash_keeps_the_boundary_between_words_separated_by_whitespace()
    {
        Assert.NotEqual(OwnHashOf("section={ a=1 2 }"), OwnHashOf("section={ a=12 }"));
        Assert.NotEqual(OwnHashOf("section={ a=1\n\t2 }"), OwnHashOf("section={ a=12 }"));
        Assert.Equal(OwnHashOf("section={ a=1 2 }"), OwnHashOf("section={\n\ta=1\n\t2\n}"));
        Assert.NotEqual(OwnHashOf("section={ ab }"), OwnHashOf("section={ a b }"));

        // Eine Liste in einem Block unterhalb der Knotentiefe geht in den Eigen-Hash des nächsten Knotens ein.
        var shallow = Profile("""{ "objects": [], "nodeDepth": 2 }""");
        Assert.NotEqual(
            Node(Index("section={ box={ path={ 1 2 } } }", shallow), "section/box").OwnHash,
            Node(Index("section={ box={ path={ 12 } } }", shallow), "section/box").OwnHash);
    }

    [Fact]
    public void Own_hash_needs_no_separator_next_to_operators_braces_and_strings()
    {
        Assert.Equal(OwnHashOf("section={ x = { a } }"), OwnHashOf("section={ x={a} }"));
        Assert.Equal(OwnHashOf("section={ x = \"a b\" y >= 1 }"), OwnHashOf("section={ x=\"a b\" y>=1 }"));
        Assert.Equal(OwnHashOf("section={ a = 1 }"), OwnHashOf("section={ a=1 }"));
    }

    [Fact]
    public void Own_hash_treats_whitespace_interrupted_by_a_child_as_one_run()
    {
        var withChild = OwnHashOf("section={\n\ta=1\n\tchild={\n\t\tv=1\n\t}\n\tb=2\n}");
        var without = OwnHashOf("section={\n\ta=1\n\tb=2\n}");

        Assert.Equal(without, withChild);
        Assert.NotEqual(OwnHashOf("section={\n\ta=1\n\tb=3\n}"), withChild);
    }

    private static ulong OwnHashOf(string text) => Node(Index(text), "section").OwnHash;

    [Fact]
    public void Own_hash_without_whitespace_does_not_depend_on_where_the_read_buffer_ends()
    {
        const string database = "database={\n\t637537109={ s=\"a  \\\" b\" t={ 1 22  3 } }\n\t16777217=\"n  o\"\n\t33554433=none\n}";
        const int boundary = 1 << 20;
        const string prefix = "pad=\"";
        const string afterPad = "\"\n";
        var reference = Index(Save(10));

        for (var offset = 0; offset <= database.Length; offset++)
        {
            var shifted = Index(Save(boundary - prefix.Length - afterPad.Length - offset));

            foreach (var path in new[] { "database", "database/637537109", "database/16777217", "database/33554433" })
            {
                Assert.Equal(Node(reference, path).OwnHash, Node(shifted, path).OwnHash);
            }
        }

        static byte[] Save(int padLength) =>
            Encoding.UTF8.GetBytes(prefix + new string('x', padLength) + afterPad + database);
    }

    private sealed class SyncProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }
}
