using Xunit;

namespace ParadoxModTool.Compatch.Tests;

public class CompatchEngineTests : CompatchTestBase
{
    private const string Buildings = "in_game/common/building_types/";

    [Fact]
    public void Automatic_actions_without_patch_need_are_marked_not_needed()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");

        var run = CompatchEngine.Run(Input(), PatchTarget.Compatch);

        Assert.Equal(2, run.Actions.Count);
        Assert.All(run.Actions, a => Assert.Equal(MergeStage.NotNeeded, a.Stage));
        Assert.Empty(run.Plan.Entries);
    }

    [Fact]
    public void Replan_after_undo_turns_not_needed_back_to_automatic()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "INJECT:farm = { a = 1 }\n");
        PutB(Buildings + "b.txt", "INJECT:farm = { b = 1 }\n");
        var input = Input();
        var run = CompatchEngine.Run(input, PatchTarget.Compatch);
        var fromA = run.Actions.Single(a => a.Origin == MergeOrigin.A);

        CompatchEngine.Replan(run, input, PatchTarget.Compatch, new Dictionary<string, MergeDecision> { [fromA.Id] = new(DecisionKind.TakeVanilla) }, null);

        Assert.All(run.Actions, a => Assert.Equal(MergeStage.Automatic, a.Stage));
        Assert.Single(run.Plan.Entries);
    }

    [Fact]
    public void Conflicts_keep_their_stage_even_when_default_equals_no_patch()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 }\n");

        var run = CompatchEngine.Run(Input(), PatchTarget.Compatch);

        Assert.Equal(MergeStage.Conflict, Assert.Single(run.Actions).Stage);
    }

    [Fact]
    public void Skipped_files_are_summarised_per_kind()
    {
        PutA("in_game/gfx/x.dds", "a");
        PutB("in_game/gfx/x.dds", "b");
        PutB("in_game/gfx/y.dds", "b");
        PutA(".metadata/metadata.json", "{}");

        var run = CompatchEngine.Run(Input(), PatchTarget.Compatch);

        Assert.Contains(new SkipSummary(SkipReason.HiddenKind, "Grafik", 2), run.Skipped);
        Assert.Contains(new SkipSummary(SkipReason.Metadata, "Metadaten", 1), run.Skipped);
    }

    [Fact]
    public void Run_never_writes_into_mod_folders()
    {
        PutVanilla(Buildings + "00_x.txt", "farm = { cost = 1 }\n");
        PutA(Buildings + "a.txt", "REPLACE:farm = { cost = 2 }\n");
        PutB(Buildings + "b.txt", "REPLACE:farm = { cost = 3 }\n");
        var before = Snapshot();

        CompatchEngine.Run(Input(), PatchTarget.Compatch);

        Assert.Equal(before, Snapshot());
    }

    private string Snapshot() => string.Join("|", Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories)
        .OrderBy(f => f, StringComparer.Ordinal)
        .Select(f => f + ":" + File.GetLastWriteTimeUtc(f).Ticks + ":" + new FileInfo(f).Length));
}
