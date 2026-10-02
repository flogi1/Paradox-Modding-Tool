using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class KeyRenameProposalsTests
{
    private const string Folder = "common/buildings";

    private static TempMod Fixture(string loc, string vanilla = "holy_01 = {\n\ttype = special\n}\nx_special_01 = {\n\ttype = special\n}\n")
    {
        var mod = new TempMod();
        mod.WriteVanilla("common/buildings/00_special_buildings.txt", vanilla);
        mod.WriteMod("common/buildings/00_special_buildings.txt", "# leer\n");
        mod.WriteMod("common/buildings/01_mod.txt", "m_1_0 = {\n\tcost_gold = 1\n}\nm_2_0 = {\n\tcost_gold = 2\n}\n");
        mod.WriteMod("localization/english/m_l_english.yml", "l_english:\n" + loc);
        return mod;
    }

    private static KeyRenameProposals Build(TempMod mod) => KeyRenameProposals.Build(new PatchWorkspace(mod.ModRoot, mod.VanillaRoot), Folder);

    [Fact]
    public void Proposes_vanilla_key_from_localization()
    {
        using var mod = Fixture(" building_type_m_1_0:0 \"$building_type_holy_01$\"\n building_m_2_0:0 \"$building_special_x_special_01$\" # Kommentar\n");

        var proposals = Build(mod).Proposals;

        Assert.Equal(new[] { "m_1_0", "m_2_0" }, proposals.Select(p => p.OldKey).ToArray());
        Assert.Equal(("holy_01", RenameStatus.Ok), (proposals[0].NewKey, proposals[0].Status));
        Assert.Equal(("x_special_01", RenameStatus.Ok), (proposals[1].NewKey, proposals[1].Status));
        Assert.Equal("common/buildings/01_mod.txt", proposals[0].File);
    }

    [Fact]
    public void Longest_rest_wins()
    {
        using var mod = Fixture(" building_type_m_1_0:0 \"$building_type_holy_temple_01$\"\n", "holy_temple_01 = { }\ntemple_01 = { }\n");

        var first = Build(mod).Proposals[0];

        Assert.Equal(("holy_temple_01", RenameStatus.Ok), (first.NewKey, first.Status));
    }

    [Fact]
    public void Unknown_reference_gives_no_proposal()
    {
        using var mod = Fixture(" building_type_m_1_0:0 \"$building_type_nothing_01$\"\n");

        var proposals = Build(mod).Proposals;

        Assert.Equal((null, RenameStatus.NoProposal), (proposals[0].NewKey, proposals[0].Status));
        Assert.Equal(RenameStatus.NoProposal, proposals[1].Status);
    }

    [Fact]
    public void Two_targets_are_ambiguous()
    {
        using var mod = Fixture(" building_type_m_1_0:0 \"$building_type_holy_01$\"\n building_m_1_0:0 \"$building_x_special_01$\"\n");

        var first = Build(mod).Proposals[0];

        Assert.Equal(RenameStatus.Ambiguous, first.Status);
        Assert.Null(first.NewKey);
        Assert.Contains("holy_01", first.Detail, StringComparison.Ordinal);
        Assert.Contains("x_special_01", first.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Vanilla_definitions_and_redefinitions_are_not_old_keys()
    {
        using var mod = Fixture(string.Empty);
        mod.WriteMod("common/buildings/02_other.txt", "x_special_01 = {\n\tcost_gold = 5\n}\n");

        Assert.Equal(new[] { "m_1_0", "m_2_0" }, Build(mod).Proposals.Select(p => p.OldKey).ToArray());
    }

    [Fact]
    public void Target_defined_in_mod_is_blocked()
    {
        using var mod = Fixture(" building_type_m_1_0:0 \"$building_type_holy_01$\"\n");
        mod.WriteMod("common/buildings/02_other.txt", "holy_01 = {\n\tcost_gold = 5\n}\n");

        var first = Build(mod).Proposals[0];

        Assert.Equal(("holy_01", RenameStatus.TargetDefined), (first.NewKey, first.Status));
        Assert.Equal("common/buildings/02_other.txt:1", first.Detail);
    }

    [Fact]
    public void Target_in_unhidden_vanilla_file_is_blocked()
    {
        using var mod = Fixture(" building_type_m_1_0:0 \"$building_type_holy_01$\"\n");
        mod.WriteVanilla("common/buildings/00_more.txt", "more_01 = { }\n");
        var proposals = Build(mod);

        Assert.Equal(RenameStatus.TargetInVanilla, proposals.Check("m_2_0", "more_01", Array.Empty<KeyRename>(), out var detail));
        Assert.Equal("common/buildings/00_more.txt", detail);
        Assert.Equal(RenameStatus.Ok, proposals.Check("m_1_0", "holy_01", Array.Empty<KeyRename>(), out _));

        mod.WriteMod("common/buildings/00_more.txt", "# leer\n");
        Assert.Equal(RenameStatus.Ok, Build(mod).Check("m_2_0", "more_01", Array.Empty<KeyRename>(), out _));
    }

    [Fact]
    public void Same_target_twice_is_blocked()
    {
        using var mod = Fixture(string.Empty);
        var all = new[] { new KeyRename("m_1_0", "holy_01"), new KeyRename("m_2_0", "holy_01") };

        Assert.Equal(RenameStatus.TargetTwice, Build(mod).Check("m_1_0", "holy_01", all, out _));
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_target_is_blocked(string? target)
    {
        using var mod = Fixture(string.Empty);

        Assert.Equal(RenameStatus.Invalid, Build(mod).Check("m_1_0", target, Array.Empty<KeyRename>(), out _));
    }

    [Fact]
    public void Counts_orphan_loc_keys_of_the_same_family()
    {
        // m_2_0 hat keinen Vorschlag: Reste seiner Familie (m_2_1) zählen nicht, die Umbenennung betrifft sie nicht.
        using var mod = Fixture(" building_m_1_1:0 \"x\"\n building_m_1_1_desc:0 \"y\"\n building_type_m_1_0:0 \"$building_type_holy_01$\"\n building_other_9:0 \"z\"\n building_m_2_1:0 \"q\"\n");

        Assert.Equal(1, Build(mod).OrphanLocKeys);
    }
}
