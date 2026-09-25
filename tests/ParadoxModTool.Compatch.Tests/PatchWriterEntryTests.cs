using System.Text;
using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class PatchWriterEntryTests : WriterTestBase
{
    private const string Buildings = "in_game/common/building_types/";
    private const string OwnFile = "in_game/common/building_types/zz_pmt_compatch.txt";

    /// <summary>A und B ersetzen farm unterschiedlich in Teilschlüsseln: das Ergebnis ist ein REPLACE im Kompatch.</summary>
    private (CompatchInput Input, CompatchRun Run) FarmCase()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 time = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 time = 1 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 1 time = 2 }\n");
        var input = Input();
        return (input, CompatchEngine.Run(input, PatchTarget.Compatch));
    }

    [Fact]
    public void New_entries_go_into_a_regenerated_own_file_with_header()
    {
        var (input, run) = FarmCase();

        var prepared = PatchWriter.Prepare(Request(input, run));

        var change = Assert.Single(prepared.Changes);
        Assert.Equal(OwnFile, change.RelativePath);
        Assert.Equal(FileChangeKind.Create, change.Kind);
        Assert.StartsWith("# " + CompatchTexts.OwnFileHeader + "\n", change.PreviewText);
        Assert.Contains("REPLACE:farm", change.PreviewText);
        Assert.Contains("cost = 2", change.PreviewText);
        Assert.Contains("time = 2", change.PreviewText);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, change.Content!.Take(3));
    }

    [Fact]
    public void Existing_key_in_one_foreign_file_is_replaced_in_place()
    {
        var (input, run) = FarmCase();
        PutPatch(Buildings + "my_fix.txt", "# eigener Patch\r\nREPLACE:farm = { cost = 9 }\r\nmine = { x = 1 }\r\n");

        var prepared = PatchWriter.Prepare(Request(input, run));

        var change = Assert.Single(prepared.Changes, c => c.RelativePath.EndsWith("my_fix.txt"));
        Assert.Equal(FileChangeKind.Modify, change.Kind);
        Assert.StartsWith("# eigener Patch\r\nREPLACE:farm", change.PreviewText);
        Assert.EndsWith("\r\nmine = { x = 1 }\r\n", change.PreviewText);
        Assert.DoesNotContain("cost = 9", change.PreviewText);
        Assert.DoesNotContain(prepared.Changes, c => c.RelativePath == OwnFile);
        Assert.Contains(prepared.ForeignEntries, e => e.RelativePath.EndsWith("my_fix.txt") && e.AddressId.EndsWith("|farm"));
    }

    [Fact]
    public void Key_twice_in_foreign_files_is_reported_and_left_alone()
    {
        var (input, run) = FarmCase();
        PutPatch(Buildings + "a_fix.txt", "REPLACE:farm = { cost = 9 }\n");
        PutPatch(Buildings + "b_fix.txt", "REPLACE:farm = { cost = 8 }\n");

        var prepared = PatchWriter.Prepare(Request(input, run));

        Assert.Contains(prepared.Issues, i => i.Kind == WriteIssueKind.DuplicateInPatch);
        Assert.Empty(prepared.Changes);
    }

    [Fact]
    public void Stale_own_files_are_deleted_and_empty_plan_deletes_own_file()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");
        PutPatch(Buildings + "zzz_pmt_compatch.txt", "INJECT:farm = { old = 1 }\n");
        var input = Input();
        var run = CompatchEngine.Run(input, PatchTarget.Compatch);

        var prepared = PatchWriter.Prepare(Request(input, run));

        var change = Assert.Single(prepared.Changes);
        Assert.Equal(FileChangeKind.Delete, change.Kind);
        Assert.EndsWith("zzz_pmt_compatch.txt", change.RelativePath);
    }

    [Fact]
    public void Previously_written_foreign_entry_that_is_no_longer_needed_is_removed()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");
        PutPatch(Buildings + "my_fix.txt", "keep = { x = 1 }\nREPLACE:farm = { cost = 9 }\nlast = { y = 1 }\n");
        var input = Input();
        var run = CompatchEngine.Run(input, PatchTarget.Compatch);
        var farmId = EntryAddress.ForKey("in_game/common/building_types", "farm").Id;

        var prepared = PatchWriter.Prepare(Request(input, run, previous: new[] { new WrittenEntry(Buildings + "my_fix.txt", farmId) }));

        Assert.Equal("keep = { x = 1 }\nlast = { y = 1 }\n", Preview(prepared, Buildings + "my_fix.txt"));
    }

    [Fact]
    public void Foreign_entry_not_written_by_pmt_is_never_removed()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");
        PutPatch(Buildings + "my_fix.txt", "REPLACE:farm = { cost = 9 }\n");
        var input = Input();

        var prepared = PatchWriter.Prepare(Request(input, CompatchEngine.Run(input, PatchTarget.Compatch)));

        Assert.Empty(prepared.Changes);
    }

    [Fact]
    public void Localization_own_file_gets_header_line_and_bom()
    {
        const string Loc = "in_game/localization/english/";
        PutVanilla(Loc + "v_l_english.yml", "l_english:\n farm: \"Farm\"\n");
        PutA(Loc + "replace/a_l_english.yml", "l_english:\n farm: \"Farm A\"\n");
        PutB(Loc + "replace/b_l_english.yml", "l_english:\n farm: \"Farm B\"\n");
        var input = Input();
        var run = CompatchEngine.Run(input, PatchTarget.Compatch);
        var conflict = Assert.Single(run.Actions);
        CompatchEngine.Replan(run, input, PatchTarget.Compatch, new Dictionary<string, MergeDecision> { [conflict.Id] = new(DecisionKind.TakeA) }, null);

        var prepared = PatchWriter.Prepare(Request(input, run));

        var change = Assert.Single(prepared.Changes);
        Assert.EndsWith("replace/zz_pmt_compatch_l_english.yml", change.RelativePath);
        Assert.StartsWith("l_english:\n", change.PreviewText);
        Assert.Contains("farm: \"Farm A\"", change.PreviewText);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, change.Content!.Take(3));
    }

    [Fact]
    public void Events_own_file_declares_namespaces()
    {
        const string Events = "in_game/events/";
        PutA(Events + "a.txt", "namespace = my_ns\nmy_ns.1 = { type = country_event title = a }\n");
        PutB(Events + "b.txt", "namespace = my_ns\nmy_ns.1 = { type = country_event title = b }\n");
        var input = Input();
        var run = CompatchEngine.Run(input, PatchTarget.Compatch);
        var conflict = run.Actions.First(a => a.Stage == MergeStage.Conflict);
        CompatchEngine.Replan(run, input, PatchTarget.Compatch, new Dictionary<string, MergeDecision> { [conflict.Id] = new(DecisionKind.TakeB) }, null);

        var prepared = PatchWriter.Prepare(Request(input, run));

        var own = Assert.Single(prepared.Changes, c => OwnPatchFiles.IsOwn(c.RelativePath));
        Assert.Contains("\nnamespace = my_ns\n", own.PreviewText);
        Assert.True(own.PreviewText.IndexOf("namespace", StringComparison.Ordinal) < own.PreviewText.IndexOf("my_ns.1", StringComparison.Ordinal));
    }

    [Fact]
    public void Windows_1252_encoding_is_used_when_the_profile_says_so()
    {
        var (input, run) = FarmCase();
        var request = Request(input, run);
        var withEncoding = new WriteRequest
        {
            Run = request.Run, Input = request.Input, Target = request.Target, TargetFolder = request.TargetFolder,
            EncodingFor = _ => "windows-1252",
        };

        var prepared = PatchWriter.Prepare(withEncoding);

        var header = Encoding.Latin1.GetString(prepared.Changes.Single().Content!);
        Assert.StartsWith("# ", header);
    }
}
