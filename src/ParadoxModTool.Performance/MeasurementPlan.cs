using ParadoxModTool.GameProfiles;

namespace ParadoxModTool.Performance;

/// <summary>
/// Eingaben für den Messplan. <paramref name="Months"/> ist die geplante Dauer und steht nur im Text des Spielschritts.
/// <paramref name="CanSetContinueTitle"/>: PMT darf den Titel in continue_game.json auf den gewählten Spielstand setzen (Datei vorhanden).
/// </summary>
public sealed record MeasurementInput(PerformanceSettings Settings, string DebugArguments, string? SaveName, string? ContinueTitle, bool GameRunning, bool GameRunningInDebug, int Months = 12, bool CanSetContinueTitle = false);

/// <summary>
/// Ein Schritt der Checkliste; <paramref name="ByPmt"/> = PMT erledigt ihn, sonst der Modder. <paramref name="Command"/> ist der Konsolenbefehl des
/// Schritts (zum Kopieren), null bei Schritten ohne Befehl.
/// </summary>
public sealed record MeasurementStep(string Text, bool ByPmt, string? Command = null);

/// <param name="Arguments">Startargumente des Spiels.</param>
/// <param name="LoadsSaveItself">Das Spiel lädt den gewählten Spielstand selbst (Startargument).</param>
/// <param name="Blocker">Grund, warum nicht gestartet werden darf; null = frei.</param>
/// <param name="SetsContinueTitle">PMT trägt vor dem Start den gewählten Spielstand in continue_game.json ein.</param>
public sealed record MeasurementPlanResult(string Arguments, bool LoadsSaveItself, IReadOnlyList<MeasurementStep> Steps, string? Blocker, bool SetsContinueTitle = false);

/// <summary>Berechnet Startargumente und Checkliste für eine Messung; reine Logik, schreibt nichts.</summary>
public static class MeasurementPlan
{
    public static MeasurementPlanResult Build(MeasurementInput input)
    {
        var settings = input.Settings;
        var canContinue = !string.IsNullOrEmpty(settings.ContinueSaveArgs) && !string.IsNullOrEmpty(input.SaveName);
        var titleMatches = canContinue
            && string.Equals(input.ContinueTitle, input.SaveName, StringComparison.OrdinalIgnoreCase)
            && !ContinueGameFile.IsAutosave(input.ContinueTitle);
        var setsContinueTitle = canContinue && !titleMatches && input.CanSetContinueTitle;
        var loadsSaveItself = titleMatches || setsContinueTitle;
        var arguments = loadsSaveItself ? input.DebugArguments + " " + settings.ContinueSaveArgs : input.DebugArguments;
        var blocker = input.GameRunning && !input.GameRunningInDebug ? PerformanceTexts.GameRunningNoDebug : null;

        var steps = new List<MeasurementStep>
        {
            input.GameRunningInDebug
                ? new MeasurementStep(PerformanceTexts.GameRunningInDebug, false)
                : new MeasurementStep(PerformanceTexts.StepStartGame, true),
        };

        steps.Add(string.IsNullOrEmpty(input.SaveName)
            ? new MeasurementStep(PerformanceTexts.StepLoadSaveUnknown, false)
            : setsContinueTitle
                ? new MeasurementStep(PerformanceTexts.StepSetContinue(input.SaveName), true)
                : loadsSaveItself
                    ? new MeasurementStep(PerformanceTexts.StepLoadSaveByPmt(input.SaveName), true)
                    : new MeasurementStep(PerformanceTexts.StepLoadSave(input.SaveName), false));
        if (!loadsSaveItself && ContinueGameFile.IsAutosave(input.ContinueTitle))
        {
            steps.Add(new MeasurementStep(PerformanceTexts.StepAutosaveHint, false));
        }

        steps.Add(new MeasurementStep(PerformanceTexts.StepRestart(settings.RestartCommand), false, settings.RestartCommand));
        steps.Add(new MeasurementStep(PerformanceTexts.StepPlay(input.Months), false));
        if (!string.IsNullOrEmpty(settings.StopCommand))
        {
            // EU V (Probelauf 2026-10-24): der Dump schreibt erst nach Stop etwas.
            steps.Add(new MeasurementStep(PerformanceTexts.StepStop(settings.StopCommand), false, settings.StopCommand));
        }

        steps.Add(new MeasurementStep(PerformanceTexts.StepDump(settings.DumpCommand), false, settings.DumpCommand));
        steps.Add(new MeasurementStep(PerformanceTexts.StepPickup, true));
        return new MeasurementPlanResult(arguments, loadsSaveItself, steps, blocker, setsContinueTitle);
    }
}
