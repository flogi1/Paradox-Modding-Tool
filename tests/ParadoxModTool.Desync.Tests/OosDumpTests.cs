using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class OosDumpTests
{
    [Fact]
    public void Opens_a_folder_dump_and_finds_files_by_machine_suffix()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 2, Dumps.ClientMetadata11c0,
            (DumpFileKind.Error, "[20:07:00] boom"));

        using var dump = OosDump.Open(folder);

        Assert.Equal(folder, dump.SourcePath);
        Assert.Equal(2, dump.MachineId);
        Assert.True(dump.Has(DumpFileKind.Metadata));
        Assert.True(dump.Has(DumpFileKind.Error));
        Assert.False(dump.Has(DumpFileKind.Debug));
        Assert.Equal("11c0de175691b2bb", dump.Metadata.OosId16);
    }

    [Fact]
    public void Ignores_files_of_other_machines_in_the_same_folder()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        File.WriteAllText(Path.Combine(folder, "error_2.log"), "[20:07:00] other machine");

        using var dump = OosDump.Open(folder);

        Assert.Equal(1, dump.MachineId);
        Assert.False(dump.Has(DumpFileKind.Error));
        Assert.Equal([1], dump.AvailableMachineIds);
    }

    [Fact]
    public void Opens_the_requested_machine_of_a_folder_holding_both_machines_and_ignores_the_other()
    {
        using var tree = new TempTree();
        Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0, (DumpFileKind.Error, "[20:07:00] host"));
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 2, Dumps.ClientMetadata11c0, (DumpFileKind.Debug, "[20:07:00] client"));

        using var first = OosDump.Open(folder);
        using var host = OosDump.Open(folder, 1);
        using var client = OosDump.Open(folder, 2);

        Assert.Equal(1, first.MachineId);
        Assert.Equal([1, 2], first.AvailableMachineIds);
        Assert.Equal(1, host.MachineId);
        Assert.True(host.Has(DumpFileKind.Error));
        Assert.False(host.Has(DumpFileKind.Debug));
        Assert.Equal(2, client.MachineId);
        Assert.Equal(2, client.Metadata.LocalMachineId);
        Assert.False(client.Has(DumpFileKind.Error));
        Assert.Contains("client", string.Join("\n", client.ReadLines(DumpFileKind.Debug)));
    }

    [Fact]
    public void Opening_a_machine_the_dump_does_not_hold_fails()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        var zip = tree.PathOf("dump.zip");
        Dumps.Zip(zip, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);

        Assert.Throws<OosDumpException>(() => OosDump.Open(folder, 2));
        Assert.Throws<OosDumpException>(() => OosDump.Open(zip, 2));
    }

    [Fact]
    public void Opens_either_machine_of_a_zip_holding_both_dumps_under_different_folders()
    {
        using var tree = new TempTree();
        var zip = tree.PathOf("both.zip");
        Dumps.Zip(zip, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0, (DumpFileKind.Error, "[20:07:00] host"));
        Dumps.Zip(zip, "11c0de175691b2bb_0 client/11c0de175691b2bb_0", 2, Dumps.ClientMetadata11c0, (DumpFileKind.Error, "[20:07:00] client"));

        using var host = OosDump.Open(zip, 1);
        using var client = OosDump.Open(zip, 2);

        Assert.Equal([1, 2], host.AvailableMachineIds);
        Assert.Contains("host", string.Join("\n", host.ReadLines(DumpFileKind.Error)));
        Assert.Contains("client", string.Join("\n", client.ReadLines(DumpFileKind.Error)));
    }

    [Fact]
    public void Opens_a_renamed_zip_with_the_dump_in_a_subfolder()
    {
        using var tree = new TempTree();
        var zip = tree.PathOf("11c0de175691b2bb_0 client.zip");
        Dumps.Zip(zip, "11c0de175691b2bb_0", 2, Dumps.ClientMetadata11c0,
            (DumpFileKind.Error, "[20:07:00] boom\nsecond line"));

        using var dump = OosDump.Open(zip);

        Assert.Equal(2, dump.MachineId);
        Assert.Equal("11c0de175691b2bb", dump.Metadata.OosId16);
        Assert.Contains("[20:07:00]", string.Join("\n", dump.ReadLines(DumpFileKind.Error)));
    }

    [Fact]
    public void Opens_a_zip_with_files_at_the_root()
    {
        using var tree = new TempTree();
        var zip = tree.PathOf("dump.zip");
        Dumps.Zip(zip, "", 1, Dumps.HostMetadata11c0, (DumpFileKind.Game, "line1\nline2\n"));

        using var dump = OosDump.Open(zip);

        Assert.Equal(1, dump.MachineId);
        Assert.True(dump.Has(DumpFileKind.Game));
        Assert.Equal(new[] { "line1", "line2" }, dump.ReadLines(DumpFileKind.Game));
    }

    [Fact]
    public void Ignored_due_to_checksums_files_have_no_content()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.Debug, Dumps.IgnoredDueToChecksums),
            (DumpFileKind.Error, "real content"),
            (DumpFileKind.Callstacks, ""));

        using var dump = OosDump.Open(folder);

        Assert.True(dump.Has(DumpFileKind.Debug));
        Assert.False(dump.HasContent(DumpFileKind.Debug));
        Assert.True(dump.HasContent(DumpFileKind.Error));
        Assert.True(dump.Has(DumpFileKind.Callstacks));
        Assert.False(dump.HasContent(DumpFileKind.Callstacks));
        Assert.False(dump.HasContent(DumpFileKind.RandomLog));
    }

    [Fact]
    public void Ignored_due_to_checksums_files_inside_a_zip_have_no_content()
    {
        using var tree = new TempTree();
        var zip = tree.PathOf("dump.zip");
        Dumps.Zip(zip, "11c0de175691b2bb_0", 2, Dumps.ClientMetadata11c0,
            (DumpFileKind.Debug, Dumps.IgnoredDueToChecksums),
            (DumpFileKind.Error, "real content"));

        using var dump = OosDump.Open(zip);

        Assert.False(dump.HasContent(DumpFileKind.Debug));
        Assert.True(dump.HasContent(DumpFileKind.Error));
    }

    [Fact]
    public void Stamp_of_a_file_reports_its_size_for_folders_and_zips()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.Error, "12345"));
        var zip = tree.PathOf("dump.zip");
        Dumps.Zip(zip, "", 1, Dumps.HostMetadata11c0, (DumpFileKind.Error, "12345"));

        using var folderDump = OosDump.Open(folder);
        using var zipDump = OosDump.Open(zip);

        Assert.Equal(5, folderDump.StampOf(DumpFileKind.Error).Size);
        Assert.Equal(5, zipDump.StampOf(DumpFileKind.Error).Size);
    }

    [Fact]
    public void A_folder_without_metadata_throws_OosDumpException()
    {
        using var tree = new TempTree(("empty/error_1.log", "x"));

        Assert.Throws<OosDumpException>(() => OosDump.Open(tree.PathOf("empty")));
    }

    [Fact]
    public void A_zip_with_a_corrupt_metadata_entry_throws_OosDumpException_and_releases_the_file()
    {
        using var tree = new TempTree();
        var zip = tree.PathOf("11c0de175691b2bb_0.zip");
        Dumps.Zip(zip, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        Dumps.CorruptEntry(zip, "oos_metadata_1.txt");

        Assert.Throws<OosDumpException>(() => OosDump.Open(zip));

        // Das Archiv ist geschlossen: die Datei lässt sich exklusiv öffnen.
        using var exclusive = new FileStream(zip, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Reading_a_corrupt_log_entry_throws_OosDumpException()
    {
        using var tree = new TempTree();
        var zip = tree.PathOf("11c0de175691b2bb_0.zip");
        Dumps.Zip(zip, "", 1, Dumps.HostMetadata11c0, (DumpFileKind.Error, string.Concat(Enumerable.Repeat("[12:00:00][e.cpp:1]: Fehler\n", 50))));
        Dumps.CorruptEntry(zip, "error_1.log");

        using var dump = OosDump.Open(zip);

        Assert.Throws<OosDumpException>(() => dump.ReadLines(DumpFileKind.Error));
    }

    [Fact]
    public void A_missing_path_throws_OosDumpException()
    {
        using var tree = new TempTree();

        Assert.Throws<OosDumpException>(() => OosDump.Open(tree.PathOf("nope")));
    }

    [Fact]
    public void Reading_a_missing_file_throws_OosDumpException()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);

        using var dump = OosDump.Open(folder);

        Assert.Throws<OosDumpException>(() => dump.OpenRead(DumpFileKind.Error));
    }

    [Fact]
    public void Reading_a_zip_never_creates_files_next_to_it()
    {
        using var tree = new TempTree();
        var zip = tree.PathOf("dump.zip");
        Dumps.Zip(zip, "11c0de175691b2bb_0", 2, Dumps.ClientMetadata11c0,
            (DumpFileKind.Error, "[20:07:00] boom"));
        var before = Listing(tree.Root);

        using (var dump = OosDump.Open(zip))
        {
            _ = dump.ReadLines(DumpFileKind.Error);
            _ = dump.HasContent(DumpFileKind.Error);
            _ = dump.StampOf(DumpFileKind.Error);
        }

        Assert.Equal(before, Listing(tree.Root));
    }

    private static string[] Listing(string root) =>
        Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories).Order().ToArray();
}
