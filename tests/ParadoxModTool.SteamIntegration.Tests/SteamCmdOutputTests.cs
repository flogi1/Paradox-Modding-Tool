using Xunit;

namespace ParadoxModTool.SteamIntegration.Tests;

public class SteamCmdOutputTests
{
    private static SteamCmdRun Run(int exit, params string[] lines) => new(exit, lines, false);

    [Fact]
    public void ErfolgreicherLauf_IstSuccess()
    {
        var outcome = SteamCmdOutput.Interpret(Run(
            0,
            "Loading Steam API...OK",
            "Logging in user 'flogi' to Steam Public...OK",
            "Waiting for user info...OK",
            "Uploading content...",
            "Success."));

        Assert.Equal(SteamCmdOutcomeKind.Success, outcome.Kind);
    }

    [Fact]
    public void Fehlerzeile_TrotzExitCodeNull_IstFailed_MitDerZeileAlsMeldung()
    {
        var outcome = SteamCmdOutput.Interpret(Run(0, "Logging in user 'flogi' to Steam Public...OK", "ERROR! Failed to update workshop item (File not found)."));

        Assert.Equal(SteamCmdOutcomeKind.Failed, outcome.Kind);
        Assert.Contains("Failed to update workshop item", outcome.Message);
    }

    [Fact]
    public void ExitCodeUngleichNull_TrotzSuccessText_IstFailed()
    {
        var outcome = SteamCmdOutput.Interpret(Run(1, "Success."));

        Assert.Equal(SteamCmdOutcomeKind.Failed, outcome.Kind);
        Assert.Contains("1", outcome.Message);
    }

    [Theory]
    [InlineData("password:")]
    [InlineData("Logging in user 'x' to Steam Public...FAILED (Invalid Password)")]
    [InlineData("Login Failure: Account Logon Denied")]
    [InlineData("FAILED (No cached credentials)")]
    public void Anmeldung_verlangt_oder_abgelaufen(string line)
    {
        var outcome = SteamCmdOutput.Interpret(Run(5, "Logging in user 'x' to Steam Public...", line));

        Assert.Equal(SteamCmdOutcomeKind.LoginRequired, outcome.Kind);
        Assert.Contains("Steam-Anmeldung einrichten", outcome.Message);
    }

    [Theory]
    [InlineData("Steam Guard code:")]
    [InlineData("Two-factor code:")]
    [InlineData("This computer has not been authenticated for your account using Steam Guard.")]
    public void SteamGuard_verlangt(string line)
    {
        var outcome = SteamCmdOutput.Interpret(Run(5, line));

        Assert.Equal(SteamCmdOutcomeKind.GuardRequired, outcome.Kind);
    }

    [Fact]
    public void SteamGuard_hatVorrangVorPasswort()
    {
        Assert.Equal(SteamCmdOutcomeKind.GuardRequired, SteamCmdOutput.Interpret(Run(5, "password:", "Steam Guard code:")).Kind);
    }

    [Fact]
    public void Zeitueberschreitung()
    {
        var outcome = SteamCmdOutput.Interpret(new SteamCmdRun(-1, new[] { "Success." }, TimedOut: true));

        Assert.Equal(SteamCmdOutcomeKind.Timeout, outcome.Kind);
    }
}
