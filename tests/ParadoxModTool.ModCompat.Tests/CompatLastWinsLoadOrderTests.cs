using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// Stellaris (Wiki "Modding", Abschnitt "Overwriting specific elements"): Dateien laden nach ihrem Namen über Vanilla und alle Mods hinweg; bei
/// "zuletzt geladen gewinnt" (LIOS) gilt die Definition aus der Datei, die zuletzt lädt, nicht die der Mod, die im Playset zuletzt steht.
/// </summary>
public class CompatLastWinsLoadOrderTests : CompatTestBase
{
    private static OverrideRules Stellaris(bool lastWinsInLoadOrder = true) =>
        new(new[] { new OverrideRule("common/buildings", OverrideBehavior.LastWins) }) { LastWinsInLoadOrder = lastWinsInLoadOrder };

    private static CompatResult Run(IReadOnlyList<CompatParticipant> participants, OverrideRules rules) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = participants, Rules = rules });

    [Fact]
    public void Last_loaded_file_wins_regardless_of_playset_order()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/buildings/zz_a.txt", "farm = { cost = 1 }\n");
        Put(b, "common/buildings/00_b.txt", "farm = { cost = 2 }\n");

        var finding = Assert.Single(Run(new[] { a, b }, Stellaris()).Findings, f => f.Key == "farm");

        Assert.Equal("A", finding.Winner!.Name);
    }

    [Fact]
    public void Without_the_flag_the_last_mod_in_the_playset_wins_as_before()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/buildings/zz_a.txt", "farm = { cost = 1 }\n");
        Put(b, "common/buildings/00_b.txt", "farm = { cost = 2 }\n");

        var finding = Assert.Single(Run(new[] { a, b }, Stellaris(lastWinsInLoadOrder: false)).Findings, f => f.Key == "farm");

        Assert.Equal("B", finding.Winner!.Name);
    }

    [Fact]
    public void Vanilla_in_a_later_file_beats_two_mods()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(vanilla, "common/buildings/zz_vanilla.txt", "farm = { cost = 10 }\n");
        Put(a, "common/buildings/00_a.txt", "farm = { cost = 1 }\n");
        Put(b, "common/buildings/01_b.txt", "farm = { cost = 2 }\n");

        var finding = Assert.Single(Run(new[] { vanilla, a, b }, Stellaris()).Findings, f => f.Key == "farm");

        Assert.True(finding.Winner!.IsVanilla);
    }

    [Fact]
    public void Case_sensitive_order_sorts_upper_case_before_underscore()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/buildings/_a.txt", "farm = { cost = 1 }\n");
        Put(b, "common/buildings/Z_b.txt", "farm = { cost = 2 }\n");
        var rules = new OverrideRules(new[] { new OverrideRule("common/buildings", OverrideBehavior.LastWins) }) { LastWinsInLoadOrder = true, LoadOrderCaseSensitive = true };

        // Gemessen 2026-10-16 (PMT Order Probe, C3): "Z_…" lädt vor "_…", also gilt A; in Kleinschreibung wäre es umgekehrt.
        Assert.Equal("A", Assert.Single(Run(new[] { a, b }, rules).Findings, f => f.Key == "farm").Winner!.Name);
        Assert.Equal("B", Assert.Single(Run(new[] { a, b }, Stellaris()).Findings, f => f.Key == "farm").Winner!.Name);
    }

    [Fact]
    public void Same_file_name_the_later_mod_in_the_playset_wins()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "common/buildings/shared.txt", "farm = { cost = 1 }\n");
        Put(b, "common/buildings/shared.txt", "farm = { cost = 2 }\n");

        // Gleicher Pfad: die Datei der später geladenen Mod ersetzt die ganze Datei (Befund auf Dateiebene).
        var finding = Assert.Single(Run(new[] { a, b }, Stellaris()).Findings, f => f.Kind == CompatKind.FileOverride);

        Assert.Equal("B", finding.Winner!.Name);
    }
}
