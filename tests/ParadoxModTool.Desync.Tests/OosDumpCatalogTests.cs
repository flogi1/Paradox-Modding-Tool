using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class OosDumpCatalogTests
{
    private const string Id11c0 = "11c0de175691b2bb";
    private const string Id9591 = "95910000aaaaaaaa";
    private const string Id2e55 = "2e55000000bbbbbb";
    private const string CommitA = "f2c15314d528e6065a86af708151248f23732b29";
    private const string CommitB = "0123456789abcdef0123456789abcdef01234567";

    private static string Meta(string id, int local, int oos, string commit = CommitA, string player = "p") => $"""
        hotjoin_vs_preparation:preparation
        local_num_times_desynchronized:0
        game_commit:{commit}
        oos_player_name:{player}{local}
        oos_folder_name:{id}_0
        local_machine_id:{local}
        time_utc: 21:47:15 0125-10-11
        oos_machine_id:{oos}
        all_checksums:RANDOM_COUNT
        oos_checksums:
        0:RANDOM_COUNT
        """;

    private static string DebugLog(string time) =>
        $"[{time}][virtualfilesystem_physfs.cpp:596]: Mounted Data: C:/game\n[{time}][x.cpp:1]: more";

    private static string ServerLogMentioning(string fullId) =>
        $"[22:47:12][a.cpp:1]: Host_1 Tick: 1425.2.4.8\n[22:47:13][jominiapplication.cpp:571]: OOS Detected! OOS ID: {fullId}\n";

    private static string FolderDump(TempTree tree, string parent, string id, int machine, int oos,
        string commit = CommitA, params (DumpFileKind Kind, string Content)[] files) =>
        Dumps.Folder(tree, $"{parent}/{id}_0", machine, Meta(id, machine, oos, commit), files);

    private static OosDumpInfo Info(string id, int machine, int oos, string commit = CommitA,
        string? sessionStart = null, params string[] serverIds) =>
        new($@"C:\dumps\{id}_{machine}", id, machine, oos, null, commit, sessionStart, serverIds, null);

    [Fact]
    public void Host_and_client_dumps_with_the_same_id_form_one_pair()
    {
        using var tree = new TempTree();
        FolderDump(tree, "a", Id11c0, 1, 2);
        FolderDump(tree, "b", Id11c0, 2, 2);

        var pairs = OosDumpCatalog.Pair(OosDumpCatalog.Scan([tree.PathOf("a"), tree.PathOf("b")], []));

        var pair = Assert.Single(pairs);
        Assert.Equal(Id11c0, pair.OosId16);
        Assert.Equal(2, pair.OosMachineId);
        Assert.Equal(1, pair.Reference!.MachineId);
        Assert.Equal(2, pair.Desynced!.MachineId);
        Assert.Empty(pair.MissingMachineIds);
    }

    [Fact]
    public void A_folder_holding_the_files_of_both_machines_forms_one_complete_pair()
    {
        using var tree = new TempTree();
        FolderDump(tree, "oos", Id11c0, 1, 2);
        var folder = FolderDump(tree, "oos", Id11c0, 2, 2);

        var dumps = OosDumpCatalog.Scan([tree.PathOf("oos")], []);

        Assert.Equal([1, 2], dumps.Select(d => d.MachineId).Order());
        Assert.All(dumps, d => Assert.Equal(folder, d.Path));
        Assert.Equal(["p1", "p2"], dumps.Select(d => d.PlayerName!).Order());
        AssertCompletePair(dumps);
    }

    [Theory]
    [InlineData("11c0de175691b2bb_0", "11c0de175691b2bb_0")]
    [InlineData("11c0de175691b2bb_0", "11c0de175691b2bb_0 client/11c0de175691b2bb_0")]
    public void A_zip_holding_the_dumps_of_both_machines_forms_one_complete_pair(string hostFolder, string clientFolder)
    {
        using var tree = new TempTree();
        Directory.CreateDirectory(tree.PathOf("oos"));
        var zip = tree.PathOf("oos/both.zip");
        Dumps.Zip(zip, hostFolder, 1, Meta(Id11c0, 1, 2));
        Dumps.Zip(zip, clientFolder, 2, Meta(Id11c0, 2, 2));

        var dumps = OosDumpCatalog.Scan([tree.PathOf("oos")], []);

        Assert.All(dumps, d => Assert.Equal(zip, d.Path));
        AssertCompletePair(dumps);
        using var client = dumps.Single(d => d.MachineId == 2).Open();
        Assert.Equal(2, client.MachineId);
        Assert.Equal("p2", client.Metadata.PlayerName);
    }

    private static void AssertCompletePair(IReadOnlyList<OosDumpInfo> dumps)
    {
        var pair = Assert.Single(OosDumpCatalog.Pair(dumps));
        Assert.Equal(1, pair.Reference!.MachineId);
        Assert.Equal(2, pair.Desynced!.MachineId);
        Assert.Empty(pair.MissingMachineIds);
    }

    [Fact]
    public void A_broken_second_machine_in_a_shared_folder_does_not_hide_the_first()
    {
        using var tree = new TempTree();
        FolderDump(tree, "oos", Id11c0, 1, 2);
        // Metadaten der zweiten Maschine ohne oos_folder_name: dieser Eintrag ist fehlerhaft, die erste Maschine bleibt.
        Dumps.Folder(tree, $"oos/{Id11c0}_0", 2, "local_machine_id:2\n");

        var dumps = OosDumpCatalog.Scan([tree.PathOf("oos")], []);

        Assert.Contains(dumps, d => d.Error is null && d.MachineId == 1);
        Assert.Contains(dumps, d => d.Error is not null);
    }

    [Fact]
    public void A_lone_host_dump_reports_the_missing_machine()
    {
        var pair = Assert.Single(OosDumpCatalog.Pair([Info(Id11c0, 1, 2)]));

        Assert.Equal([2], pair.MissingMachineIds);
        Assert.Null(pair.Desynced);
        Assert.Equal(1, pair.Reference!.MachineId);
    }

    [Fact]
    public void The_host_can_be_the_desynced_machine()
    {
        var pair = Assert.Single(OosDumpCatalog.Pair([Info(Id11c0, 2, 1), Info(Id11c0, 1, 1)]));

        Assert.Equal(1, pair.OosMachineId);
        Assert.Equal(2, pair.Reference!.MachineId);
        Assert.Equal(1, pair.Desynced!.MachineId);
        Assert.Empty(pair.MissingMachineIds);
    }

    [Fact]
    public void With_three_machines_the_lowest_other_machine_is_the_reference()
    {
        var pair = Assert.Single(OosDumpCatalog.Pair(
            [Info(Id11c0, 3, 2), Info(Id11c0, 2, 2), Info(Id11c0, 1, 2)]));

        Assert.Equal(1, pair.Reference!.MachineId);
        Assert.Equal(2, pair.Desynced!.MachineId);
        Assert.Equal([1, 2, 3], pair.Dumps.Select(d => d.MachineId));
    }

    [Fact]
    public void A_missing_oos_machine_line_does_not_invent_a_missing_machine_zero()
    {
        var pair = Assert.Single(OosDumpCatalog.Pair([Info(Id11c0, 1, 0)]));

        Assert.Empty(pair.MissingMachineIds);
    }

    [Fact]
    public void Dumps_with_an_error_are_not_paired()
    {
        var broken = new OosDumpInfo(@"C:\dumps\x.zip", "", 0, 0, null, null, null, [], "kaputt");

        Assert.Empty(OosDumpCatalog.Pair([broken]));
    }

    [Fact]
    public void Pairs_whose_server_log_names_the_other_oos_id_share_a_session()
    {
        // 9591 ist der spätere OOS: sein Server-Log enthält auch die Zeile des früheren 2e55.
        var earlier = OosDumpCatalog.Pair([Info(Id2e55, 1, 2)]);
        var later = OosDumpCatalog.Pair([Info(Id9591, 1, 2, serverIds: [Id9591, Id2e55])]);

        var sessions = OosDumpCatalog.Group([.. earlier, .. later]);

        var session = Assert.Single(sessions);
        Assert.Equal(Id2e55.CompareTo(Id9591) < 0 ? Id2e55 : Id9591, session.Key);
        Assert.Equal(2, session.Pairs.Count);
    }

    [Fact]
    public void Pairs_with_different_game_commits_never_share_a_session()
    {
        var a = OosDumpCatalog.Pair([Info(Id2e55, 1, 2, CommitA)]);
        var b = OosDumpCatalog.Pair([Info(Id9591, 1, 2, CommitB, serverIds: [Id2e55])]);

        Assert.Equal(2, OosDumpCatalog.Group([.. a, .. b]).Count);
    }

    [Fact]
    public void The_same_game_commit_alone_never_joins_sessions()
    {
        var a = OosDumpCatalog.Pair([Info(Id2e55, 1, 2, sessionStart: "20:00:00")]);
        var b = OosDumpCatalog.Pair([Info(Id9591, 1, 2, sessionStart: "21:07:00")]);

        Assert.Equal(2, OosDumpCatalog.Group([.. a, .. b]).Count);
    }

    [Fact]
    public void Same_commit_and_same_session_start_join_sessions()
    {
        var a = OosDumpCatalog.Pair([Info(Id2e55, 1, 2, sessionStart: "21:07:00")]);
        var b = OosDumpCatalog.Pair([Info(Id9591, 1, 2, sessionStart: "21:07:00")]);

        var session = Assert.Single(OosDumpCatalog.Group([.. a, .. b]));

        Assert.Equal(2, session.Pairs.Count);
    }

    [Fact]
    public void A_missing_session_start_never_joins_sessions()
    {
        var a = OosDumpCatalog.Pair([Info(Id2e55, 1, 2)]);
        var b = OosDumpCatalog.Pair([Info(Id9591, 1, 2)]);

        Assert.Equal(2, OosDumpCatalog.Group([.. a, .. b]).Count);
    }

    [Fact]
    public void Sessions_are_joined_transitively()
    {
        var a = OosDumpCatalog.Pair([Info(Id2e55, 1, 2, serverIds: [Id2e55])]);
        var b = OosDumpCatalog.Pair([Info(Id9591, 1, 2, serverIds: [Id2e55, Id9591])]);
        var c = OosDumpCatalog.Pair([Info(Id11c0, 1, 2, serverIds: [Id9591, Id11c0])]);

        var session = Assert.Single(OosDumpCatalog.Group([.. c, .. b, .. a]));

        Assert.Equal(3, session.Pairs.Count);
        Assert.Equal(Id11c0, session.Key);
    }

    [Fact]
    public void Sessions_are_ordered_newest_first_by_the_reference_metadata_write_time()
    {
        using var tree = new TempTree();
        var older = FolderDump(tree, "oos", Id2e55, 1, 2);
        var newer = FolderDump(tree, "oos", Id9591, 1, 2);
        File.SetLastWriteTimeUtc(Path.Combine(older, "oos_metadata_1.txt"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path.Combine(newer, "oos_metadata_1.txt"), new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var sessions = OosDumpCatalog.Group(OosDumpCatalog.Pair(OosDumpCatalog.Scan([tree.PathOf("oos")], [])));

        Assert.Equal([Id9591, Id2e55], sessions.Select(s => s.Key));
    }

    [Fact]
    public void Scan_reads_session_start_and_oos_ids_of_the_server_log()
    {
        using var tree = new TempTree();
        FolderDump(tree, "oos", Id9591, 1, 2, CommitA,
            (DumpFileKind.Debug, DebugLog("21:07:00")),
            (DumpFileKind.DedicatedServer, ServerLogMentioning(Id2e55 + "cccccccccccccccc")
                + ServerLogMentioning(Id2e55 + "cccccccccccccccc")
                + ServerLogMentioning(Id9591 + "dddddddddddddddd")));

        var info = Assert.Single(OosDumpCatalog.Scan([tree.PathOf("oos")], []));

        Assert.Equal("21:07:00", info.SessionStart);
        Assert.Equal([Id2e55, Id9591], info.OosIdsInServerLog);
        Assert.Equal(CommitA, info.GameCommit);
        Assert.Equal("p1", info.PlayerName);
        Assert.Null(info.Error);
    }

    [Fact]
    public void Scan_leaves_session_start_and_ids_empty_without_logs()
    {
        using var tree = new TempTree();
        FolderDump(tree, "oos", Id9591, 1, 2);

        var info = Assert.Single(OosDumpCatalog.Scan([tree.PathOf("oos")], []));

        Assert.Null(info.SessionStart);
        Assert.Empty(info.OosIdsInServerLog);
    }

    [Fact]
    public void Scan_lists_folders_and_zips_in_a_root_and_takes_added_paths_and_parent_folders()
    {
        using var tree = new TempTree();
        FolderDump(tree, "oos", Id11c0, 1, 2);
        Directory.CreateDirectory(tree.PathOf("oos/not-a-dump"));
        File.WriteAllText(tree.PathOf("oos/notes.txt"), "x");
        Dumps.Zip(tree.PathOf("oos/client.zip"), Id11c0 + "_0", 2, Meta(Id11c0, 2, 2));
        // Hinzugefügter Elternordner mit zwei Dumps.
        Dumps.Folder(tree, "extra/host", 1, Meta(Id9591, 1, 2));
        Dumps.Folder(tree, "extra/client", 2, Meta(Id9591, 2, 2));
        // Hinzugefügter Dump-Ordner direkt.
        FolderDump(tree, "single", Id2e55, 1, 2);

        var infos = OosDumpCatalog.Scan([tree.PathOf("oos")], [tree.PathOf("extra"), tree.PathOf("single/" + Id2e55 + "_0")]);

        Assert.Equal(5, infos.Count);
        Assert.Equal(
            new HashSet<(string, int)> { (Id11c0, 1), (Id11c0, 2), (Id9591, 1), (Id9591, 2), (Id2e55, 1) },
            infos.Select(i => (i.OosId16, i.MachineId)).ToHashSet());
    }

    [Fact]
    public void A_broken_zip_is_listed_with_its_error()
    {
        using var tree = new TempTree();
        Directory.CreateDirectory(tree.PathOf("oos"));
        File.WriteAllBytes(tree.PathOf("oos/broken.zip"), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);

        var info = Assert.Single(OosDumpCatalog.Scan([tree.PathOf("oos")], []));

        Assert.Equal(tree.PathOf("oos/broken.zip"), info.Path);
        Assert.NotNull(info.Error);
        Assert.Equal("", info.OosId16);
        Assert.Empty(OosDumpCatalog.Pair([info]));
    }

    [Fact]
    public void A_zip_with_a_corrupt_metadata_entry_is_listed_with_its_error()
    {
        using var tree = new TempTree();
        Directory.CreateDirectory(tree.PathOf("oos"));
        var zip = tree.PathOf("oos/11c0de175691b2bb_0.zip");
        Dumps.Zip(zip, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        Dumps.CorruptEntry(zip, "oos_metadata_1.txt");

        var info = Assert.Single(OosDumpCatalog.Scan([tree.PathOf("oos")], []));

        Assert.Equal(zip, info.Path);
        Assert.NotNull(info.Error);
        Assert.Empty(OosDumpCatalog.Pair([info]));
    }

    [Fact]
    public void The_same_dump_added_twice_is_listed_once()
    {
        using var tree = new TempTree();
        var folder = FolderDump(tree, "oos", Id11c0, 1, 2);

        var infos = OosDumpCatalog.Scan([tree.PathOf("oos"), tree.PathOf("oos")], [folder, folder + "\\"]);

        Assert.Single(infos);
    }

    [Fact]
    public void A_folder_wins_over_a_zip_of_the_same_dump()
    {
        using var tree = new TempTree();
        Directory.CreateDirectory(tree.PathOf("oos"));
        Dumps.Zip(tree.PathOf("oos/a.zip"), "", 1, Meta(Id11c0, 1, 2));
        var folder = FolderDump(tree, "other", Id11c0, 1, 2);

        // Zip zuerst, Ordner später: der Ordner ersetzt die Zip.
        var info = Assert.Single(OosDumpCatalog.Scan([tree.PathOf("oos")], [folder]));

        Assert.Equal(folder, info.Path);
    }

    [Fact]
    public void Missing_roots_and_added_paths_are_ignored()
    {
        using var tree = new TempTree();

        Assert.Empty(OosDumpCatalog.Scan([tree.PathOf("nope"), ""], [tree.PathOf("also-nope")]));
    }

    [Fact]
    public void Folder_for_is_the_oos_folder_next_to_the_mod_folder()
    {
        Assert.Equal(
            Path.Combine(@"C:\Users\x\Documents\Paradox Interactive\Europa Universalis V", "oos"),
            OosLocations.FolderFor(@"C:\Users\x\Documents\Paradox Interactive\Europa Universalis V\mod\"));
    }

    [Fact]
    public void Folder_for_expands_environment_variables()
    {
        Environment.SetEnvironmentVariable("PMT_DESYNC_TEST_DOCS", @"D:\Docs");
        try
        {
            Assert.Equal(
                Path.Combine(@"D:\Docs\EU5", "oos"),
                OosLocations.FolderFor(@"%PMT_DESYNC_TEST_DOCS%\EU5\mod"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PMT_DESYNC_TEST_DOCS", null);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\")]
    public void Folder_for_is_null_without_a_usable_template(string? template)
    {
        Assert.Null(OosLocations.FolderFor(template));
    }
}
