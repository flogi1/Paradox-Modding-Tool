using System.ComponentModel;
using System.Diagnostics;
using Xunit;

namespace ParadoxModTool.LaunchControl.Tests;

public class GameLauncherTests : IDisposable
{
    private readonly string _fakeExePath;

    public GameLauncherTests()
    {
        // Ein echter Prozessstart lässt sich in dieser Umgebung nicht sinnvoll testen - eine
        // leere temporäre Datei reicht, um "Datei existiert" von "Datei existiert nicht" zu
        // unterscheiden, ohne dass sie tatsächlich ausführbar sein muss (Launch prüft nur
        // File.Exists, der eigentliche Start läuft über den injizierten Fake-Starter).
        _fakeExePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_fakeExePath);
        }
        catch
        {
            // Best-effort Aufräumen.
        }
    }

    [Fact]
    public void Launch_KeinPfadEingestellt_LiefertFehlerOhneStarterAufzurufen()
    {
        var starterCalled = false;

        var result = GameLauncher.Launch(null, "-debug_mode", _ =>
        {
            starterCalled = true;
            return null;
        });

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.False(starterCalled);
    }

    [Fact]
    public void Launch_LeererPfad_LiefertFehler()
    {
        var result = GameLauncher.Launch("   ", "", _ => null);

        Assert.False(result.Success);
    }

    [Fact]
    public void Launch_DateiExistiertNicht_LiefertFehlerOhneStarterAufzurufen()
    {
        var starterCalled = false;
        var missingPath = Path.Combine(Path.GetTempPath(), "pmt-nicht-vorhanden-" + Guid.NewGuid() + ".exe");

        var result = GameLauncher.Launch(missingPath, "", _ =>
        {
            starterCalled = true;
            return null;
        });

        Assert.False(result.Success);
        Assert.Contains(missingPath, result.ErrorMessage);
        Assert.False(starterCalled);
    }

    [Fact]
    public void Launch_GueltigeDatei_RuftStarterMitKorrektenArgumentenAufUndLiefertErfolg()
    {
        ProcessStartInfo? captured = null;

        var result = GameLauncher.Launch(_fakeExePath, "-debug_mode", info =>
        {
            captured = info;
            return null;
        });

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(captured);
        Assert.Equal(_fakeExePath, captured!.FileName);
        Assert.Equal("-debug_mode", captured.Arguments);
        Assert.True(captured.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_SetztArbeitsverzeichnisAufDenOrdnerDerExe()
    {
        var info = GameLauncher.BuildStartInfo(_fakeExePath, "");

        Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(_fakeExePath)), info.WorkingDirectory);
    }

    [Fact]
    public void Launch_StarterWirftWin32Exception_LiefertFehlerStattAusnahme()
    {
        var result = GameLauncher.Launch(_fakeExePath, "", _ => throw new Win32Exception("Zugriff verweigert (simuliert)"));

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }
}
