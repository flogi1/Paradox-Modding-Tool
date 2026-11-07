using ParadoxModTool.GameProfiles;
using Xunit;

namespace ParadoxModTool.Performance.Tests;

public sealed class MeasurementPlanTests
{
    private static PerformanceSettings Eu5() => RepoFiles.Profile("eu5").Performance!;

    private static MeasurementInput Input(string? save, string? title, bool running = false, bool debug = false) =>
        new(Eu5(), "-debug_mode", save, title, running, debug);

    [Fact]
    public void BuildAppendsContinueArgsOnlyWhenTitleMatchesChosenSave()
    {
        var match = MeasurementPlan.Build(Input("perf_1337", "perf_1337"));
        Assert.Equal("-debug_mode -continuelastsave", match.Arguments);
        Assert.True(match.LoadsSaveItself);
        Assert.True(match.Steps[1].ByPmt);
        Assert.DoesNotContain(match.Steps, s => s.Text == PerformanceTexts.StepAutosaveHint);

        var autosave = MeasurementPlan.Build(Input("autosave_1", "autosave_1"));
        Assert.Equal("-debug_mode", autosave.Arguments);
        Assert.False(autosave.LoadsSaveItself);
        Assert.False(autosave.Steps[1].ByPmt);
        Assert.Contains(autosave.Steps, s => s.Text == PerformanceTexts.StepAutosaveHint);

        var other = MeasurementPlan.Build(Input("perf_1337", "perf_other"));
        Assert.Equal("-debug_mode", other.Arguments);
        Assert.False(other.LoadsSaveItself);
        Assert.False(other.Steps[1].ByPmt);
        Assert.Contains("perf_1337", other.Steps[1].Text);
        Assert.DoesNotContain(other.Steps, s => s.Text == PerformanceTexts.StepAutosaveHint);
    }

    [Fact]
    public void BuildSetsTheContinueTitleWhenPmtMayWriteIt()
    {
        var other = MeasurementPlan.Build(new MeasurementInput(Eu5(), "-debug_mode", "perf_1337", "autosave_1", false, false, CanSetContinueTitle: true));
        Assert.Equal("-debug_mode -continuelastsave", other.Arguments);
        Assert.True(other.LoadsSaveItself);
        Assert.True(other.SetsContinueTitle);
        Assert.Equal(new MeasurementStep(PerformanceTexts.StepSetContinue("perf_1337"), true), other.Steps[1]);
        Assert.DoesNotContain(other.Steps, s => s.Text == PerformanceTexts.StepAutosaveHint);

        var match = MeasurementPlan.Build(new MeasurementInput(Eu5(), "-debug_mode", "perf_1337", "perf_1337", false, false, CanSetContinueTitle: true));
        Assert.True(match.LoadsSaveItself);
        Assert.False(match.SetsContinueTitle);
        Assert.Equal(PerformanceTexts.StepLoadSaveByPmt("perf_1337"), match.Steps[1].Text);

        var noSave = MeasurementPlan.Build(new MeasurementInput(Eu5(), "-debug_mode", null, "autosave_1", false, false, CanSetContinueTitle: true));
        Assert.False(noSave.SetsContinueTitle);
        Assert.False(noSave.LoadsSaveItself);
    }

    [Fact]
    public void BuildMatchesTitleIgnoringCase()
    {
        Assert.True(MeasurementPlan.Build(Input("Perf_1337", "perf_1337")).LoadsSaveItself);
    }

    [Fact]
    public void BuildDoesNotLoadSaveItselfWithoutSaveNameOrContinueArgs()
    {
        var noSave = MeasurementPlan.Build(Input(null, "perf_1337"));
        Assert.False(noSave.LoadsSaveItself);
        Assert.Equal(new MeasurementStep(PerformanceTexts.StepLoadSaveUnknown, false), noSave.Steps[1]);
        var noArgs = Eu5();
        noArgs.ContinueSaveArgs = null;
        var result = MeasurementPlan.Build(new MeasurementInput(noArgs, "-debug_mode", "perf_1337", "perf_1337", false, false));
        Assert.False(result.LoadsSaveItself);
        Assert.Equal("-debug_mode", result.Arguments);
    }

    [Fact]
    public void BuildBlocksWhenGameRunsWithoutDebug()
    {
        var plan = MeasurementPlan.Build(Input("perf_1337", "perf_1337", running: true, debug: false));
        Assert.Equal(PerformanceTexts.GameRunningNoDebug, plan.Blocker);
    }

    [Fact]
    public void BuildHasNoBlockerOtherwise()
    {
        Assert.Null(MeasurementPlan.Build(Input("a", "a")).Blocker);
        Assert.Null(MeasurementPlan.Build(Input("a", "a", running: true, debug: true)).Blocker);
    }

    [Fact]
    public void BuildSkipsStartWhenGameRunsInDebug()
    {
        var plan = MeasurementPlan.Build(Input("a", "a", running: true, debug: true));
        Assert.Equal(PerformanceTexts.GameRunningInDebug, plan.Steps[0].Text);
        Assert.False(plan.Steps[0].ByPmt);
        var fresh = MeasurementPlan.Build(Input("a", "a"));
        Assert.Equal(PerformanceTexts.StepStartGame, fresh.Steps[0].Text);
        Assert.True(fresh.Steps[0].ByPmt);
    }

    [Fact]
    public void StepsNameTheProfileCommands()
    {
        var plan = MeasurementPlan.Build(Input("a", "b"));
        Assert.Contains(plan.Steps, s => !s.ByPmt && s.Text.Contains("Script.Profiling.Restart"));
        Assert.Contains(plan.Steps, s => !s.ByPmt && s.Text.Contains("Script.Profiling.DumpAll"));
    }

    [Fact]
    public void StepsEndWithRestartPlayDumpPickup()
    {
        var plan = MeasurementPlan.Build(new MeasurementInput(Eu5(), "-debug_mode", "a", "b", false, false, 24));
        var texts = plan.Steps.Select(s => s.Text).ToList();
        Assert.Equal(PerformanceTexts.StepRestart("Script.Profiling.Restart"), texts[^5]);
        Assert.Equal(PerformanceTexts.StepPlay(24), texts[^4]);
        Assert.Equal(PerformanceTexts.StepStop("Script.Profiling.Stop"), texts[^3]);
        Assert.Equal(PerformanceTexts.StepDump("Script.Profiling.DumpAll"), texts[^2]);
        Assert.Equal(PerformanceTexts.StepPickup, texts[^1]);
        Assert.True(plan.Steps[^1].ByPmt);
        Assert.Contains("24", texts[^4]);
        Assert.Equal("Script.Profiling.Restart", plan.Steps[^5].Command);
        Assert.Null(plan.Steps[^4].Command);
        Assert.Equal("Script.Profiling.Stop", plan.Steps[^3].Command);
        Assert.Equal("Script.Profiling.DumpAll", plan.Steps[^2].Command);
    }

    [Fact]
    public void StopStepIsSkippedWithoutStopCommand()
    {
        var settings = Eu5();
        settings.StopCommand = null;
        var plan = MeasurementPlan.Build(new MeasurementInput(settings, "-debug_mode", "a", "b", false, false));
        var texts = plan.Steps.Select(s => s.Text).ToList();
        Assert.Equal(PerformanceTexts.StepPlay(12), texts[^3]);
        Assert.Equal(PerformanceTexts.StepDump("Script.Profiling.DumpAll"), texts[^2]);
        Assert.DoesNotContain(plan.Steps, s => s.Text.Contains("Stop", StringComparison.Ordinal));
    }
}
