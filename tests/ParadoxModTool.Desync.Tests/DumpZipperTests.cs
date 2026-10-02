using System.IO.Compression;
using Xunit;

namespace ParadoxModTool.Desync.Tests;

public class DumpZipperTests
{
    [Fact]
    public void The_zip_holds_the_folder_with_all_files_and_opens_as_a_dump()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0,
            (DumpFileKind.Error, "[20:07:00] boom"));
        var zip = tree.PathOf("out/dump.zip");
        Directory.CreateDirectory(tree.PathOf("out"));

        DumpZipper.SaveAsZip(folder, zip);

        using (var archive = ZipFile.OpenRead(zip))
        {
            var names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.Equal(["11c0de175691b2bb_0/error_1.log", "11c0de175691b2bb_0/oos_metadata_1.txt"], names);
        }

        using var dump = OosDump.Open(zip);
        Assert.Equal(1, dump.MachineId);
        Assert.Equal("11c0de175691b2bb", dump.Metadata.OosId16);
        Assert.True(dump.Has(DumpFileKind.Error));
        Assert.Contains("boom", string.Join("\n", dump.ReadLines(DumpFileKind.Error)));
        Assert.Empty(Directory.GetFiles(tree.PathOf("out"), "*.tmp"));
    }

    [Fact]
    public void An_existing_target_is_replaced()
    {
        using var tree = new TempTree(("out/dump.zip", "old"));
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        var zip = tree.PathOf("out/dump.zip");

        DumpZipper.SaveAsZip(folder, zip);

        using var dump = OosDump.Open(zip);
        Assert.Equal(1, dump.MachineId);
        Assert.Single(Directory.GetFiles(tree.PathOf("out")));
    }

    [Fact]
    public void A_failed_move_leaves_no_temp_file_and_keeps_the_locked_target()
    {
        using var tree = new TempTree(("out/dump.zip", "old"));
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        var zip = tree.PathOf("out/dump.zip");

        using (new FileStream(zip, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Windows meldet ein gesperrtes Ziel beim Ersetzen als Zugriff verweigert, andere Systeme als IOException.
            var failure = Record.Exception(() => DumpZipper.SaveAsZip(folder, zip));
            Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString() ?? "keine Ausnahme");
        }

        Assert.Equal([zip], Directory.GetFiles(tree.PathOf("out")));
        Assert.Equal("old", File.ReadAllText(zip));
    }

    [Fact]
    public void A_failed_write_into_a_missing_directory_creates_nothing()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        var zip = tree.PathOf("missing/dump.zip");

        Assert.ThrowsAny<IOException>(() => DumpZipper.SaveAsZip(folder, zip));

        Assert.False(Directory.Exists(tree.PathOf("missing")));
        Assert.Equal([folder], Directory.GetDirectories(tree.Root));
        Assert.Empty(Directory.GetFiles(tree.Root));
    }

    [Theory]
    [InlineData("11c0de175691b2bb_0/dump.zip")]
    [InlineData("11c0de175691b2bb_0/sub/dump.zip")]
    [InlineData("11C0DE175691B2BB_0/dump.zip")]
    public void A_target_inside_the_dump_folder_is_refused(string relativeTarget)
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        var target = tree.PathOf(relativeTarget);

        Assert.Throws<ArgumentException>(() => DumpZipper.SaveAsZip(folder, target));

        Assert.False(File.Exists(target));
        Assert.Empty(Directory.GetFiles(folder, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void A_target_next_to_a_folder_with_the_same_name_prefix_is_allowed()
    {
        using var tree = new TempTree();
        var folder = Dumps.Folder(tree, "11c0de175691b2bb_0", 1, Dumps.HostMetadata11c0);
        var target = tree.PathOf("11c0de175691b2bb_0.zip");

        DumpZipper.SaveAsZip(folder, target);

        Assert.True(File.Exists(target));
    }
}
