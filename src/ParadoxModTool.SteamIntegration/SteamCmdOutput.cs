namespace ParadoxModTool.SteamIntegration;

public enum SteamCmdOutcomeKind
{
    Success,
    LoginRequired,
    GuardRequired,
    Timeout,
    Failed,
}

public sealed record SteamCmdOutcome(SteamCmdOutcomeKind Kind, string Message);

/// <summary>
/// Wertet einen SteamCMD-Lauf aus: Exit-Code und Ausgabezeilen. Die Erkennungstexte sind bewusst tolerant (Groß-/Kleinschreibung, Fragmente); der genaue Wortlaut von SteamCMD
/// ist erst am echten Lauf bestätigt. Deshalb zeigt das Fenster die volle Ausgabe immer an.
/// </summary>
public static class SteamCmdOutput
{
    private static readonly string[] GuardMarkers = { "steam guard", "two-factor", "two factor", "twofactor" };
    private static readonly string[] LoginMarkers = { "login failure", "invalid password", "password:", "no cached credentials", "cached credentials", "not logged on" };
    private static readonly string[] ErrorMarkers = { "error!", "error:", "failed", "failure" };

    public static SteamCmdOutcome Interpret(SteamCmdRun run)
    {
        if (run.TimedOut)
        {
            return new SteamCmdOutcome(SteamCmdOutcomeKind.Timeout, SteamTexts.CmdTimeout);
        }

        if (run.Lines.Any(line => Contains(line, GuardMarkers)))
        {
            return new SteamCmdOutcome(
                SteamCmdOutcomeKind.GuardRequired,
                SteamTexts.CmdGuardRequired);
        }

        if (run.Lines.Any(line => Contains(line, LoginMarkers)))
        {
            return new SteamCmdOutcome(
                SteamCmdOutcomeKind.LoginRequired,
                SteamTexts.CmdLoginRequired);
        }

        var errorLine = run.Lines.FirstOrDefault(line => Contains(line, ErrorMarkers));
        if (errorLine is not null)
        {
            return new SteamCmdOutcome(SteamCmdOutcomeKind.Failed, errorLine.Trim());
        }

        if (run.ExitCode != 0)
        {
            return new SteamCmdOutcome(SteamCmdOutcomeKind.Failed, SteamTexts.CmdExitCode(run.ExitCode));
        }

        return new SteamCmdOutcome(SteamCmdOutcomeKind.Success, SteamTexts.CmdNoError);
    }

    private static bool Contains(string line, string[] markers) =>
        markers.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
