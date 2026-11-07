using ParadoxModTool.ScriptCheck;
using Xunit;
using Xunit.Abstractions;

namespace ParadoxModTool.GfxAudit.Tests;

/// <summary>
/// Kalibrierung der GFX-Prüfung: die Installation als gewählte Mod, ihre DLC-Ordner als Vanilla. Die Spieltests laufen nur mit
/// <c>PMT_FLAGS_&lt;SPIEL&gt;_GAME</c>; jeder Error-Schlüssel muss in <c>Data/calibration/&lt;id&gt;-errors.txt</c> stehen, und die ungenutzten Bilder
/// dürfen höchstens den Anteil je Spiel ausmachen (Begründung in <c>docs/superpowers/notes/2026-11-06-gfx-audit-calibration.md</c>).
/// Die Hilfstests laufen immer auf einer kleinen Temp-Installation.
/// </summary>
public sealed class CalibrationTests : IDisposable
{
    private const double MaxUnusedShare = 0.15;

    private readonly ITestOutputHelper _output;
    private readonly GfxTestRoots _roots = new();

    public CalibrationTests(ITestOutputHelper output) => _output = output;

    public void Dispose() => _roots.Dispose();

    [Fact]
    public void Calibration_Ck3() => Calibrate("ck3", "CK3");

    [Fact]
    public void Calibration_Vic3() => Calibrate("vic3", "VIC3");

    [Fact]
    public void Calibration_Eu4() => Calibrate("eu4", "EU4");

    [Fact]
    public void Calibration_Eu5() => Calibrate("eu5", "EU5");

    [Fact]
    public void Calibration_Hoi4() => Calibrate("hoi4", "HOI4");

    [Fact]
    public void Calibration_Stellaris() => Calibrate("stellaris", "STELLARIS");

    [Fact]
    public void ReportListsErrorKeysAndSkipsDlcFolders()
    {
        WriteFixture();

        var result = CalibrationReport.Run("eu4", _roots.Vanilla, Path.Combine(_roots.Temp, "out"));

        Assert.Equal(new[] { "MissingTexture|gfx/interface/gone.dds" }, result.ErrorKeys);
        Assert.Equal(1, result.Counts[(GfxFindingKind.MissingTexture, FindingSeverity.Error)]);
        Assert.Equal(2, result.ImageCount);
        Assert.Equal(1, result.ExcludedImages);
        Assert.Equal(1, result.UnusedImages);
        Assert.Equal(0.5, result.UnusedImageShare);
        Assert.True(result.ExcludedFindings >= 1);
        Assert.DoesNotContain(result.Findings, f => f.Location.RelativePath.StartsWith("dlc/", StringComparison.Ordinal));
        Assert.Equal(Path.Combine(_roots.Temp, "out", "eu4-report.txt"), result.ReportPath);
        var report = File.ReadAllText(result.ReportPath);
        Assert.Contains("MissingTexture|gfx/interface/gone.dds\tinterface/a.gfx:4\tGFX_gone", report);
        Assert.Contains("UnusedImage-Anteil: 1 / 2", report);
        var candidate = File.ReadAllText(Path.Combine(_roots.Temp, "out", "eu4-errors.candidate.txt"));
        Assert.Contains("MissingTexture|gfx/interface/gone.dds\tVanilla: Textur des Sprites GFX_gone fehlt in der Installation", candidate);
    }

    [Fact]
    public void DlcRootsAreOnlyFolders()
    {
        GfxTestRoots.Write(_roots.Vanilla, "dlc/dlc001_a/gfx/a.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "dlc/dlc002_b.zip", "zip");
        GfxTestRoots.Write(_roots.Vanilla, "integrated_dlc/i1/gfx/b.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "builtin_dlc/b1/gfx/c.dds", "dds");

        var roots = CalibrationReport.DlcRoots(_roots.Vanilla);

        Assert.Equal(
            new[] { Path.Combine(_roots.Vanilla, "dlc", "dlc001_a"), Path.Combine(_roots.Vanilla, "integrated_dlc", "i1"), Path.Combine(_roots.Vanilla, "builtin_dlc", "b1") },
            roots);
        Assert.True(CalibrationReport.IsDlcPath("builtin_dlc/b1/gfx/c.dds"));
        Assert.False(CalibrationReport.IsDlcPath("gfx/dlc/c.dds"));
    }

    [Fact]
    public void EngineRootsBesideTheInstallResolveTextures()
    {
        GfxTestRoots.Write(_roots.Vanilla, "gui/a.gui", "texture = \"gfx/editor_gui/editor_button.dds\"\ntexture = \"gfx/editor_gui/gone.dds\"\n");
        GfxTestRoots.Write(Path.Combine(_roots.Temp, "clausewitz"), "gfx/editor_gui/editor_button.dds", "dds");

        var roots = CalibrationReport.EngineRoots(_roots.Vanilla);
        var result = CalibrationReport.Run("ck3", _roots.Vanilla, Path.Combine(_roots.Temp, "out"));

        Assert.Equal(new[] { Path.Combine(_roots.Temp, "clausewitz") }, roots);
        Assert.Equal(new[] { "MissingTexture|gfx/editor_gui/gone.dds" }, result.ErrorKeys);
    }

    [Fact]
    public void UnlistedErrorFails()
    {
        var residual = new Dictionary<string, string>(StringComparer.Ordinal) { ["MissingTexture|gfx/a.dds"] = "Vanilla" };

        var (unlisted, vanished) = CalibrationReport.Compare(new[] { "MissingTexture|gfx/a.dds", "MissingSprite|GFX_b" }, residual);

        Assert.Equal(new[] { "MissingSprite|GFX_b" }, unlisted);
        Assert.Empty(vanished);
    }

    [Fact]
    public void VanishedResidualKeyOnlyReported()
    {
        var path = Path.Combine(_roots.Temp, "x-errors.txt");
        File.WriteAllText(path, "# Kopf\n\nMissingTexture|gfx/a.dds\tVanilla: Textur fehlt\nMissingSprite|GFX_b\n");

        var residual = CalibrationReport.ReadResidual(path);
        var (unlisted, vanished) = CalibrationReport.Compare(new[] { "MissingTexture|gfx/a.dds" }, residual);

        Assert.Equal(2, residual.Count);
        Assert.Equal("Vanilla: Textur fehlt", residual["MissingTexture|gfx/a.dds"]);
        Assert.Equal(string.Empty, residual["MissingSprite|GFX_b"]);
        Assert.Empty(unlisted);
        Assert.Equal(new[] { "MissingSprite|GFX_b" }, vanished);
    }

    [Fact]
    public void ResidualFilesExistForAllGames()
    {
        foreach (var id in new[] { "ck3", "vic3", "eu4", "eu5", "hoi4", "stellaris" })
        {
            Assert.True(File.Exists(RepoFiles.Data("calibration/" + id + "-errors.txt")), id);
        }
    }

    /// <summary>
    /// Kleine Installation: Sprite <c>GFX_used</c> (genannt) auf ein vorhandenes Bild, Sprite <c>GFX_gone</c> auf ein fehlendes, ein ungenutztes Bild
    /// und ein ungenutztes Bild im DLC-Ordner (zählt nicht).
    /// </summary>
    private void WriteFixture()
    {
        GfxTestRoots.Write(
            _roots.Vanilla,
            "interface/a.gfx",
            "spriteTypes = {\n\tspriteType = {\n\t\tname = \"GFX_gone\"\n\t\ttexturefile = \"gfx/interface/gone.dds\"\n\t}\n"
                + "\tspriteType = {\n\t\tname = \"GFX_used\"\n\t\ttexturefile = \"gfx/interface/used.dds\"\n\t}\n}\n");
        GfxTestRoots.Write(_roots.Vanilla, "interface/b.gui", "iconType = { spriteType = \"GFX_used\" }\niconType = { spriteType = \"GFX_gone\" }\n");
        GfxTestRoots.Write(_roots.Vanilla, "gfx/interface/used.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "gfx/interface/old_thing.dds", "dds");
        GfxTestRoots.Write(_roots.Vanilla, "dlc/dlc001_x/gfx/interface/dlc_thing.dds", "dds");
    }

    /// <summary>Ein Spiel kalibrieren: ohne Umgebungsvariable sofort fertig; sonst Restliste und Anteil ungenutzter Bilder prüfen.</summary>
    private void Calibrate(string gameId, string game, double maxShare = MaxUnusedShare)
    {
        var install = Environment.GetEnvironmentVariable("PMT_FLAGS_" + game + "_GAME");
        if (string.IsNullOrEmpty(install))
        {
            return;
        }

        if (!Directory.Exists(install))
        {
            _output.WriteLine($"PMT_FLAGS_{game}_GAME zeigt auf einen fehlenden Ordner, Kalibrierung {gameId} übersprungen: {install}");
            return;
        }

        var result = CalibrationReport.Run(gameId, install, Path.Combine(Path.GetTempPath(), "pmt-gfxaudit-calibration"));
        Assert.True(result.FileCount > 0, "keine Dateien gelesen");

        _output.WriteLine(
            $"{gameId}: Textdateien {result.FileCount}, Bilder {result.ImageCount} (+{result.ExcludedImages} DLC), Aufbau {result.BuildSeconds:F1} s, "
                + $"Refresh {result.RefreshSeconds:F1} s, Refresh warm {result.WarmRefreshSeconds:F1} s, UnusedImage {result.UnusedImages} = {result.UnusedImageShare:P1}, nicht gewertet (DLC) {result.ExcludedFindings}");
        foreach (var count in result.Counts.OrderBy(c => c.Key.Severity).ThenBy(c => c.Key.Kind))
        {
            _output.WriteLine($"  {count.Key.Kind} {count.Key.Severity}: {count.Value}");
        }

        _output.WriteLine("Bericht: " + result.ReportPath);

        var residual = CalibrationReport.ReadResidual(RepoFiles.Data("calibration/" + gameId + "-errors.txt"));
        var (unlisted, vanished) = CalibrationReport.Compare(result.ErrorKeys, residual);
        foreach (var key in vanished)
        {
            _output.WriteLine("Nicht mehr aufgetreten (Restliste): " + key);
        }

        Assert.True(
            unlisted.Count == 0,
            $"{unlisted.Count} Error-Schlüssel fehlen in der Restliste {gameId}-errors.txt (alle im Bericht {result.ReportPath}):\n"
                + string.Join("\n", unlisted.Take(30)));
        Assert.True(
            result.UnusedImageShare <= maxShare,
            $"UnusedImage-Anteil {result.UnusedImageShare:P1} über {maxShare:P1} (Bericht {result.ReportPath})");
    }
}
