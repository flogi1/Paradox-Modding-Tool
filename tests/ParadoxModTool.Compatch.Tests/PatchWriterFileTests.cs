using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class PatchWriterFileTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    private (CompatchInput Input, CompatchRun Run) FarmCase()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 1 time = 2 }\n");
        var input = Input();
        return (input, CompatchEngine.Run(input, PatchTarget.Compatch));
    }

    /// <summary>EU-IV-Fall: beide ändern dieselbe Datei, nur "ganze Datei" ist möglich.</summary>
    private (CompatchInput Input, CompatchRun Run) WholeFileCase()
    {
        PutVanilla("common/buildings/00_x.txt", "farm = { cost = 1 }\nmine = { x = 1 }\n");
        PutA("common/buildings/00_x.txt", "farm = { cost = 2 }\nmine = { x = 1 }\n");
        PutB("common/buildings/00_x.txt", "farm = { cost = 1 }\nmine = { x = 2 }\n");
        var input = Input(euV: false);
        return (input, CompatchEngine.Run(input, PatchTarget.Compatch));
    }

    [Fact]
    public void Whole_file_is_written_under_its_path()
    {
        var (input, run) = WholeFileCase();

        var prepared = PatchWriter.Prepare(Request(input, run));

        var change = Assert.Single(prepared.Changes);
        Assert.Equal("common/buildings/00_x.txt", change.RelativePath.ToLowerInvariant());
        Assert.Contains("cost = 2", change.PreviewText);
        Assert.Contains("x = 2", change.PreviewText);
        Assert.Single(prepared.WholeFiles);
    }

    [Fact]
    public void Second_write_without_changes_changes_nothing()
    {
        var (input, run) = FarmCase();
        PatchWriter.Commit(PatchWriter.Prepare(Request(input, run)), PatchDir, null);

        var again = PatchWriter.Prepare(Request(input, CompatchEngine.Run(input, PatchTarget.Compatch)));

        Assert.All(again.Changes, c => Assert.Equal(FileChangeKind.Unchanged, c.Kind));
        Assert.Equal(0, PatchWriter.Commit(again, PatchDir, () => throw new InvalidOperationException("kein Snapshot ohne Änderung")).Written);
    }

    [Fact]
    public void Base_mod_rewrite_keeps_needed_entries()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 1 time = 2 }\n");
        var input = new CompatchInput
        {
            VanillaFolder = VanillaDir, ModAFolder = ADir, ModBFolder = BDir, Rules = EuVRules(), KeyOperators = EuVOps,
            ExcludeOwnPatchFilesOf = MergeSide.A,
        };
        var first = PatchWriter.Prepare(Request(input, CompatchEngine.Run(input, PatchTarget.BaseModA), PatchTarget.BaseModA, ADir));
        PatchWriter.Commit(first, ADir, null);
        Assert.True(File.Exists(Path.Combine(ADir, "in_game", "common", "building_types", "zz_pmt_compatch.txt")));

        var second = PatchWriter.Prepare(Request(input, CompatchEngine.Run(input, PatchTarget.BaseModA), PatchTarget.BaseModA, ADir));

        Assert.All(second.Changes, c => Assert.Equal(FileChangeKind.Unchanged, c.Kind));
        Assert.Equal("REPLACE:farm = { cost = 2 time = 1 }\n", File.ReadAllText(Path.Combine(ADir, "in_game", "common", "building_types", "a.txt")));
    }

    [Fact]
    public void Locked_target_paths_are_skipped_and_reported()
    {
        var (input, run) = FarmCase();

        var prepared = PatchWriter.Prepare(Request(input, run, locked: new[] { "in_game/common" }));

        Assert.Empty(prepared.Changes);
        Assert.Contains(prepared.Issues, i => i.Kind == WriteIssueKind.Locked);
    }

    [Fact]
    public void Copy_onto_itself_for_base_mod_is_reported_not_written()
    {
        // .csv ist keine Skriptdatei: eine Adresse "ganze Datei", bei Wahl A zeigt CopyFrom auf die Datei in A selbst.
        PutVanilla("common/buildings/data.csv", "v\n");
        PutA("common/buildings/data.csv", "a\n");
        PutB("common/buildings/data.csv", "b\n");
        var input = new CompatchInput { VanillaFolder = VanillaDir, ModAFolder = ADir, ModBFolder = BDir, Rules = EuIVRules(), ExcludeOwnPatchFilesOf = MergeSide.A };
        var run = CompatchEngine.Run(input, PatchTarget.BaseModA);
        var conflict = Assert.Single(run.Actions);
        CompatchEngine.Replan(run, input, PatchTarget.BaseModA, new Dictionary<string, MergeDecision> { [conflict.Id] = new(DecisionKind.TakeA) }, null);

        var prepared = PatchWriter.Prepare(Request(input, run, PatchTarget.BaseModA, ADir));

        Assert.Contains(prepared.Issues, i => i.Kind is WriteIssueKind.SameFile);
        Assert.DoesNotContain(prepared.Changes, c => c.Kind is FileChangeKind.Modify or FileChangeKind.Create);
    }

    [Fact]
    public void Previously_written_whole_file_no_longer_needed_is_deleted_only_if_unchanged()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");
        PutPatch("common/old.txt", "old = 1\n");
        PutPatch("common/edited.txt", "edited = 2\n");
        var input = Input();
        var previous = new Dictionary<string, string>
        {
            ["common/old.txt"] = TextEncodings.Sha256(File.ReadAllBytes(Path.Combine(PatchDir, "common", "old.txt"))),
            ["common/edited.txt"] = "0000",
        };

        var prepared = PatchWriter.Prepare(Request(input, CompatchEngine.Run(input, PatchTarget.Compatch), previousFiles: previous));

        Assert.Contains(prepared.Changes, c => c.RelativePath == "common/old.txt" && c.Kind == FileChangeKind.Delete);
        Assert.Contains(prepared.Issues, i => i.Kind == WriteIssueKind.KeptChangedFile && i.RelativePath == "common/edited.txt");
    }

    [Fact]
    public void Commit_writes_all_files_after_snapshot_and_deletes()
    {
        var (input, run) = FarmCase();
        PutPatch("in_game/common/building_types/zzz_pmt_compatch.txt", "old\n");
        var prepared = PatchWriter.Prepare(Request(input, run));
        var snapshotCalls = 0;

        var result = PatchWriter.Commit(prepared, PatchDir, () =>
        {
            snapshotCalls++;
            Assert.True(File.Exists(Path.Combine(PatchDir, "in_game", "common", "building_types", "zzz_pmt_compatch.txt")));
            return "snap.zip";
        });

        Assert.Equal(1, snapshotCalls);
        Assert.Equal("snap.zip", result.SnapshotPath);
        Assert.Equal(1, result.Written);
        Assert.Equal(1, result.Deleted);
        Assert.Contains("REPLACE:farm", ReadPatch("in_game/common/building_types/zz_pmt_compatch.txt"));
        Assert.Empty(Directory.GetFiles(PatchDir, "*.pmt-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Commit_failure_names_the_snapshot_and_leaves_no_temp_files()
    {
        var (input, run) = FarmCase();
        var prepared = PatchWriter.Prepare(Request(input, run));
        // Ein Ordner an der Stelle der Zieldatei lässt das Ersetzen scheitern.
        Directory.CreateDirectory(Path.Combine(PatchDir, "in_game", "common", "building_types", "zz_pmt_compatch.txt"));

        var error = Assert.Throws<CompatchWriteException>(() => PatchWriter.Commit(prepared, PatchDir, () => "snap.zip"));

        Assert.Contains("snap.zip", error.Message);
        Assert.Empty(Directory.GetFiles(PatchDir, "*.pmt-tmp", SearchOption.AllDirectories));
    }
}
