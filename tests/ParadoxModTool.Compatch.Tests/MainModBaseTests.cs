using Xunit;

namespace ParadoxModTool.Compatch.Tests;

/// <summary>
/// Ziel "Basis-Mod" mit Hauptmod: Option 1 (nur gegen die Hauptmod vergleichen) und Option 2 (fehlende Blöcke der Hauptmod übernehmen).
/// EU-IV-Regeln, eine Gebäudedatei mit gleichem Pfad in Hauptmod und Ziel.
/// </summary>
public class MainModBaseTests : WriterTestBase
{
    private const string Buildings = "common/buildings/00_buildings.txt";
    private const string VanillaText = "temple = {\n\tcost = 100\n\tmodifier = {\n\t\tlocal_tax = 1\n\t}\n}\n";
    private const string MainText = "temple = {\n\tcost = 150\n\tmodifier = {\n\t\tlocal_tax = 2\n\t\tlocal_unrest = -1\n\t}\n\ton_built = {\n\t\tadd_prestige = 1\n\t}\n}\nelven_hall = {\n\tcost = 300\n}\n";
    private const string AddonText = "temple = {\n\tcost = 120\n\tmodifier = {\n\t\tlocal_tax = 2\n\t}\n}\n";

    /// <summary>Hauptmod A, Ziel B (bzw. gespiegelt). Das Ziel lädt nach der Hauptmod.</summary>
    private CompatchInput BaseInput(bool mainAsBase, bool fill, MergeSide main = MergeSide.A) => new()
    {
        VanillaFolder = VanillaDir,
        ModAFolder = ADir,
        ModBFolder = BDir,
        BLoadsAfterA = main == MergeSide.A,
        Rules = EuIVRules(),
        KeyOperators = Array.Empty<string>(),
        ExcludeOwnPatchFilesOf = main == MergeSide.A ? MergeSide.B : MergeSide.A,
        MainSide = main,
        MainAsBase = mainAsBase,
        FillMissingFromMain = fill,
    };

    private void AddonCase()
    {
        PutVanilla(Buildings, VanillaText);
        PutA(Buildings, MainText);
        PutB(Buildings, AddonText);
    }

    private static string Result(CompatchRun run, string key) =>
        ActionText.Render(run.Applied.Desired.Single(d => d.Key.Key == key).Value.Entries);

    private static MergeAction At(CompatchRun run, string key, string part) =>
        run.Actions.Single(a => a.Address.Key == key && a.DisplayPath.Contains(part, StringComparison.Ordinal));

    [Fact]
    public void Main_as_base_takes_every_target_change_without_conflicts()
    {
        AddonCase();

        var run = CompatchEngine.Run(BaseInput(mainAsBase: true, fill: false), PatchTarget.BaseModB);

        Assert.DoesNotContain(run.Actions, a => a.Stage == MergeStage.Conflict);
        var cost = At(run, "temple", "cost");
        Assert.Equal(MergeOrigin.B, cost.Origin);
        Assert.True(cost.Stage is MergeStage.Automatic or MergeStage.NotNeeded);
        Assert.Contains("cost = 120", Result(run, "temple"));
    }

    [Fact]
    public void Missing_block_is_dropped_without_fill_and_taken_with_fill_and_undo_drops_it()
    {
        AddonCase();
        Assert.DoesNotContain("on_built", Result(CompatchEngine.Run(BaseInput(true, false), PatchTarget.BaseModB), "temple"));

        var session = new CompatchSession(BaseInput(true, true), new CompatchState { Target = PatchTarget.BaseModB, TargetFolder = BDir }, null);
        session.Load();
        var fill = At(session.Run, "temple", "on_built");

        Assert.Equal(MergeOrigin.A, fill.Origin);
        Assert.Equal(MergeStage.Automatic, fill.Stage);
        Assert.Equal(CompatchTexts.NoteMissingInTarget, fill.Note);
        Assert.Contains("add_prestige = 1", Result(session.Run, "temple"));
        Assert.Contains("local_unrest = -1", Result(session.Run, "temple"));
        Assert.Contains("cost = 120", Result(session.Run, "temple"));

        session.Undo(fill.Id);
        Assert.DoesNotContain("on_built", Result(session.Run, "temple"));
    }

    [Fact]
    public void Missing_line_inside_a_shared_logic_block_is_review()
    {
        PutVanilla(Buildings, VanillaText);
        PutA(Buildings, "temple = {\n\tcost = 100\n\ton_built = {\n\t\tadd_prestige = 1\n\t\tadd_legitimacy = 1\n\t}\n}\n");
        PutB(Buildings, "temple = {\n\tcost = 100\n\ton_built = {\n\t\tadd_prestige = 1\n\t}\n}\n");

        var run = CompatchEngine.Run(BaseInput(true, true), PatchTarget.BaseModB);

        var fill = At(run, "temple", "add_legitimacy");
        Assert.Equal(MergeStage.Review, fill.Stage);
        Assert.Equal(MergeOrigin.A, fill.Origin);
        Assert.Contains(CompatchTexts.NoteMissingInTarget, fill.Note);
    }

    [Fact]
    public void Whole_key_only_in_main_of_a_replaced_file_is_taken()
    {
        AddonCase();

        var run = CompatchEngine.Run(BaseInput(true, true), PatchTarget.BaseModB);

        var hall = run.Actions.Single(a => a.Address.Key == "elven_hall");
        Assert.Equal(MergeOrigin.A, hall.Origin);
        Assert.Equal(MergeStage.Automatic, hall.Stage);
        Assert.Contains("cost = 300", Result(run, "elven_hall"));
    }

    [Fact]
    public void Fill_without_main_as_base_turns_conflict_into_automatic_take()
    {
        PutVanilla(Buildings, VanillaText);
        PutA(Buildings, "temple = {\n\tcost = 100\n\tmodifier = {\n\t\tlocal_tax = 2\n\t}\n}\n");
        PutB(Buildings, "temple = {\n\tcost = 100\n}\n");

        var plain = CompatchEngine.Run(BaseInput(false, false), PatchTarget.BaseModB);
        var filled = CompatchEngine.Run(BaseInput(false, true), PatchTarget.BaseModB);

        Assert.Equal(MergeStage.Conflict, At(plain, "temple", "modifier").Stage);
        var modifier = At(filled, "temple", "modifier");
        Assert.Equal(MergeOrigin.A, modifier.Origin);
        Assert.Equal(MergeStage.Automatic, modifier.Stage);
        Assert.Contains("local_tax = 2", Result(filled, "temple"));
    }

    [Fact]
    public void Mirror_target_a_takes_missing_blocks_from_b()
    {
        PutVanilla(Buildings, VanillaText);
        PutA(Buildings, AddonText);
        PutB(Buildings, MainText);

        var run = CompatchEngine.Run(BaseInput(true, true, main: MergeSide.B), PatchTarget.BaseModA);

        Assert.DoesNotContain(run.Actions, a => a.Stage == MergeStage.Conflict);
        Assert.Equal(MergeOrigin.B, At(run, "temple", "on_built").Origin);
        Assert.Contains("add_prestige = 1", Result(run, "temple"));
        Assert.Contains("cost = 120", Result(run, "temple"));
    }

    [Fact]
    public void Options_are_ignored_without_main_side()
    {
        AddonCase();
        var input = new CompatchInput
        {
            VanillaFolder = VanillaDir,
            ModAFolder = ADir,
            ModBFolder = BDir,
            Rules = EuIVRules(),
            KeyOperators = Array.Empty<string>(),
            MainAsBase = true,
            FillMissingFromMain = true,
        };

        var run = CompatchEngine.Run(input, PatchTarget.Compatch);

        Assert.Contains(run.Actions, a => a.Stage == MergeStage.Conflict);
        Assert.DoesNotContain(run.Actions, a => a.Note == CompatchTexts.NoteMissingInTarget);
    }

    [Fact]
    public void Remerge_switches_options_without_reading_and_keeps_decisions()
    {
        AddonCase();
        var session = new CompatchSession(BaseInput(false, false), new CompatchState { Target = PatchTarget.BaseModB, TargetFolder = BDir }, null);
        session.Load();
        var conflict = session.Actions.First(a => a.Stage == MergeStage.Conflict);
        session.Decide(conflict.Id, new MergeDecision(DecisionKind.TakeA));
        var resolved = session.Run.Resolved;

        session.Remerge(BaseInput(true, true));

        Assert.Same(resolved, session.Run.Resolved);
        Assert.True(session.Input.MainAsBase);
        Assert.DoesNotContain(session.Actions, a => a.Stage == MergeStage.Conflict);
        Assert.Contains(session.Actions, a => a.Note == CompatchTexts.NoteMissingInTarget);
        var again = session.Actions.Single(a => a.Id == conflict.Id);
        Assert.Equal(DecisionKind.TakeA, session.EffectiveDecision(again).Kind);
        Assert.Equal(MergeStage.Decided, again.Stage);
    }

    [Fact]
    public void Main_mod_options_round_trip_and_default_to_off()
    {
        var dir = Path.Combine(Dir, "state");
        var store = new CompatchStateStore(dir);
        store.Save(new CompatchState { MainReplacesVanilla = true, FillMissingFromMain = true });

        var back = new CompatchStateStore(dir).Load()!;
        Assert.True(back.MainReplacesVanilla);
        Assert.True(back.FillMissingFromMain);

        File.WriteAllText(Path.Combine(dir, "state.json"), "{ \"version\": 1, \"modAFolder\": \"C:\\\\a\" }");
        var old = new CompatchStateStore(dir).Load()!;
        Assert.False(old.MainReplacesVanilla);
        Assert.False(old.FillMissingFromMain);
    }

    [Fact]
    public void Writing_into_the_addon_keeps_its_values_and_adds_missing_blocks()
    {
        AddonCase();
        var input = BaseInput(true, true);
        var run = CompatchEngine.Run(input, PatchTarget.BaseModB);

        var prepared = PatchWriter.Prepare(Request(input, run, PatchTarget.BaseModB, BDir));

        var written = string.Join("\n", prepared.Changes.Where(c => c.Kind != FileChangeKind.Unchanged).Select(c => c.PreviewText));
        Assert.Contains("cost = 120", written);
        Assert.Contains("add_prestige = 1", written);
        Assert.Contains("local_unrest = -1", written);
        Assert.Contains("elven_hall", written);
    }
}
