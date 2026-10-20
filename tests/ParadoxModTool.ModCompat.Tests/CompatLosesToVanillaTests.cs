using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// Stellaris (Wiki "Modding"): Dateien laden nach ihrem Namen über Vanilla und alle Mods hinweg. Definiert eine einzelne Mod einen Schlüssel, den
/// Vanilla in einer Datei definiert, die nach Ladereihenfolge gewinnt, wirkt die Definition der Mod nicht; die Prüfung meldet das auch mit nur einer Mod.
/// </summary>
public class CompatLosesToVanillaTests : CompatTestBase
{
    private static OverrideRules Stellaris(bool lastWinsInLoadOrder = true, bool firstWinsInLoadOrder = true) =>
        new(new[]
        {
            new OverrideRule("common/buildings", OverrideBehavior.LastWins),
            new OverrideRule("common/governments", OverrideBehavior.FirstWins),
        }) { LastWinsInLoadOrder = lastWinsInLoadOrder, FirstWinsInLoadOrder = firstWinsInLoadOrder };

    private static IEnumerable<CompatFinding> Losses(IReadOnlyList<CompatParticipant> participants, OverrideRules rules, string? focusId = null) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = participants, Rules = rules, FocusId = focusId }).Findings
            .Where(f => f.Kind == CompatKind.LosesToVanilla);

    [Fact]
    public void Last_wins_single_mod_loses_to_a_vanilla_file_that_loads_later()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\n");
        Put(a, "common/buildings/00_a.txt", "farm = { cost = 1 }\n");

        var finding = Assert.Single(Losses(new[] { vanilla, a }, Stellaris()));

        Assert.Equal("farm", finding.Key);
        Assert.Equal(CompatSeverity.Conflict, finding.Severity);
        Assert.Equal("common/buildings/00_a.txt", finding.RelativePath);
        Assert.True(finding.Winner!.IsVanilla);
        Assert.All(finding.Contributions, c => Assert.Equal("A", c.Participant.Name));
        Assert.Contains("common/buildings/zz_v.txt", finding.Message);
        // "zz_00_a.txt" sortiert vor "zz_v.txt" ('0' vor 'v'), erst "zzz_" lädt danach.
        Assert.Contains("common/buildings/zzz_00_a.txt", finding.Message);
    }

    [Fact]
    public void Last_wins_mod_file_that_loads_after_vanilla_gives_no_finding()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\n");
        Put(a, "common/buildings/zzz_a.txt", "farm = { cost = 1 }\n");

        Assert.Empty(Losses(new[] { vanilla, a }, Stellaris()));
    }

    [Fact]
    public void The_mod_file_that_loads_last_counts()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\n");
        Put(a, "common/buildings/00_a.txt", "farm = { cost = 1 }\n");
        Put(a, "common/buildings/zzz_a.txt", "farm = { cost = 2 }\n");

        Assert.Empty(Losses(new[] { vanilla, a }, Stellaris()));
    }

    [Fact]
    public void Without_the_flags_there_is_no_finding()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\n");
        Put(a, "common/buildings/00_a.txt", "farm = { cost = 1 }\n");

        Assert.Empty(Losses(new[] { vanilla, a }, Stellaris(lastWinsInLoadOrder: false, firstWinsInLoadOrder: false)));
    }

    [Fact]
    public void A_mod_file_with_the_vanilla_name_replaces_the_vanilla_file()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\n");
        Put(a, "common/buildings/zz_v.txt", "farm = { cost = 1 }\n");

        Assert.Empty(Losses(new[] { vanilla, a }, Stellaris()));
    }

    [Fact]
    public void A_vanilla_file_replaced_by_name_does_not_count_even_if_the_mod_moves_the_key()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\nbarn = { cost = 3 }\n");
        Put(a, "common/buildings/zz_v.txt", "barn = { cost = 3 }\n");
        Put(a, "common/buildings/00_a.txt", "farm = { cost = 1 }\n");

        Assert.Empty(Losses(new[] { vanilla, a }, Stellaris()));
    }

    [Fact]
    public void A_definition_equal_to_the_winning_vanilla_one_gives_no_finding()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\n");
        Put(a, "common/buildings/00_a.txt", "farm = { cost = 10 }\n");

        Assert.Empty(Losses(new[] { vanilla, a }, Stellaris()));
    }

    [Fact]
    public void First_wins_single_mod_loses_to_a_vanilla_file_that_loads_earlier()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/governments/00_v.txt", "gov = { a = 1 }\n");
        Put(a, "common/governments/01_a.txt", "gov = { a = 2 }\n");

        var finding = Assert.Single(Losses(new[] { vanilla, a }, Stellaris()));

        Assert.Equal("gov", finding.Key);
        Assert.True(finding.Winner!.IsVanilla);
        Assert.Contains("common/governments/00_v.txt", finding.Message);
        Assert.Contains("common/governments/!!_01_a.txt", finding.Message);
    }

    [Fact]
    public void First_wins_mod_file_that_loads_before_vanilla_gives_no_finding()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/governments/00_v.txt", "gov = { a = 1 }\n");
        Put(a, "common/governments/!!_a.txt", "gov = { a = 2 }\n");

        Assert.Empty(Losses(new[] { vanilla, a }, Stellaris()));
    }

    [Fact]
    public void Only_the_focus_mod_gets_findings()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\nbarn = { cost = 3 }\n");
        Put(a, "common/buildings/00_a.txt", "farm = { cost = 1 }\n");
        Put(b, "common/buildings/00_b.txt", "barn = { cost = 1 }\n");

        var finding = Assert.Single(Losses(new[] { vanilla, a, b }, Stellaris(), focusId: a.Id));

        Assert.Equal("farm", finding.Key);
    }

    [Fact]
    public void Two_mods_on_one_key_keep_the_conflict_finding_with_vanilla_as_winner()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(vanilla, "common/buildings/zz_v.txt", "farm = { cost = 10 }\n");
        Put(a, "common/buildings/00_a.txt", "farm = { cost = 1 }\n");
        Put(b, "common/buildings/01_b.txt", "farm = { cost = 2 }\n");

        Assert.Empty(Losses(new[] { vanilla, a, b }, Stellaris()));
    }

    [Fact]
    public void Victoria_3_first_wins_without_last_wins_flag_stays_unchanged()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        Put(vanilla, "common/governments/00_v.txt", "gov = { a = 1 }\n");
        Put(a, "common/governments/01_a.txt", "gov = { a = 2 }\n");

        Assert.Empty(Losses(new[] { vanilla, a }, Stellaris(lastWinsInLoadOrder: false, firstWinsInLoadOrder: true)));
    }
}
