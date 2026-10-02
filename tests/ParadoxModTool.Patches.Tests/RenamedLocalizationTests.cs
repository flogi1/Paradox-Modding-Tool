using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class RenamedLocalizationTests
{
    private static readonly IReadOnlySet<string> NoKeys = new HashSet<string>();

    private static readonly Dictionary<string, string> Map = new() { ["m_1_0"] = "holy_01" };

    private static TempMod Fixture(string modLoc, string? vanillaEnglish = null)
    {
        var mod = new TempMod();
        mod.WriteVanilla("localization/english/buildings_l_english.yml", "l_english:\n" + (vanillaEnglish ??
            " building_type_holy_01:0 \"Holy\"\n building_type_holy_01_desc:0 \"Vanilla desc\"\n building_holy_01:0 \"Holy B\"\n"));
        mod.WriteVanilla("localization/russian/buildings_l_russian.yml", "l_russian:\n building_holy_01:0 \"Ru\"\n");
        mod.WriteMod("localization/english/m_l_english.yml", "l_english:\n" + modLoc);
        return mod;
    }

    private static (LocCleanupResult Result, PatchWorkspace Workspace) Run(TempMod mod)
    {
        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);
        var result = RenamedLocalization.Apply(workspace, Map, NoKeys, lang => LocalizationIndex.LoadVanilla(mod.VanillaRoot, lang));
        return (result, workspace);
    }

    [Fact]
    public void Pure_self_reference_is_removed()
    {
        using var mod = Fixture(" building_type_m_1_0:0 \"$building_type_holy_01$\"\n other:0 \"x\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal("l_english:\n other:0 \"x\"\n", workspace.Get("localization/english/m_l_english.yml"));
        Assert.Equal(1, result.SelfRemoved);
        Assert.Empty(result.Warnings);
        Assert.Null(workspace.Get("localization/english/replace/pmt_renamed_l_english.yml"));
    }

    [Fact]
    public void Self_reference_with_prefix_gets_vanilla_text()
    {
        using var mod = Fixture(" building_type_m_1_0_desc:0 \"$cost$$building_type_holy_01_desc$\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal(1, result.Inlined);
        Assert.Equal(1, result.Moved);
        Assert.Equal("l_english:\n", workspace.Get("localization/english/m_l_english.yml"));
        Assert.Contains(" building_type_holy_01_desc:0 \"$cost$Vanilla desc\"", workspace.Get("localization/english/replace/pmt_renamed_l_english.yml"), StringComparison.Ordinal);
    }

    [Fact]
    public void Self_reference_without_vanilla_text_is_kept_with_warning()
    {
        using var mod = Fixture(string.Empty);
        mod.WriteMod("localization/russian/m_l_russian.yml", "l_russian:\n building_type_m_1_0:0 \"$building_type_holy_01$\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal("l_russian:\n building_type_holy_01:0 \"$building_type_holy_01$\"\n", workspace.Get("localization/russian/m_l_russian.yml"));
        Assert.Single(result.Warnings);
        Assert.Equal(0, result.SelfRemoved);
    }

    [Fact]
    public void Duplicate_with_same_value_is_removed()
    {
        using var mod = Fixture(" flogi_is_special_holy_01: \"yes\"\n flogi_is_special_m_1_0: \"yes\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal("l_english:\n flogi_is_special_holy_01: \"yes\"\n", workspace.Get("localization/english/m_l_english.yml"));
        Assert.Equal(1, result.DuplicatesRemoved);
    }

    [Fact]
    public void Duplicate_with_other_value_warns()
    {
        using var mod = Fixture(" flogi_is_special_holy_01: \"yes\"\n flogi_is_special_m_1_0: \"no\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal("l_english:\n flogi_is_special_holy_01: \"yes\"\n flogi_is_special_holy_01: \"no\"\n", workspace.Get("localization/english/m_l_english.yml"));
        Assert.Contains(result.Warnings, w => w.Contains("flogi_is_special_holy_01", StringComparison.Ordinal));
        Assert.Equal(0, result.DuplicatesRemoved);
    }

    [Fact]
    public void Key_vanilla_defines_moves_to_replace()
    {
        using var mod = Fixture(" building_m_1_0:0 \"Mein Name\"\n building_type_m_1_0:0 \"Mein Typ\"\n keep:0 \"k\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal(2, result.Moved);
        Assert.Equal("l_english:\n keep:0 \"k\"\n", workspace.Get("localization/english/m_l_english.yml"));
        var replace = workspace.Get("localization/english/replace/pmt_renamed_l_english.yml")!;
        Assert.StartsWith("l_english:", replace, StringComparison.Ordinal);
        Assert.Contains(" building_holy_01:0 \"Mein Name\"", replace, StringComparison.Ordinal);
        Assert.Contains(" building_type_holy_01:0 \"Mein Typ\"", replace, StringComparison.Ordinal);
        Assert.True(workspace.HasBomOf("localization/english/replace/pmt_renamed_l_english.yml"));
    }

    [Fact]
    public void Moved_lines_are_appended_to_an_existing_replace_file()
    {
        using var mod = Fixture(" building_m_1_0:0 \"Mein Name\"\n");
        mod.WriteMod("localization/english/replace/pmt_renamed_l_english.yml", "l_english:\r\n alt:0 \"a\"\r\n");

        var (_, workspace) = Run(mod);

        Assert.Equal("l_english:\r\n alt:0 \"a\"\r\n building_holy_01:0 \"Mein Name\"\r\n", workspace.Get("localization/english/replace/pmt_renamed_l_english.yml"));
    }

    [Fact]
    public void Line_in_replace_folder_stays()
    {
        using var mod = Fixture(string.Empty);
        mod.WriteMod("localization/english/replace/own_l_english.yml", "l_english:\n building_m_1_0:0 \"Mein Name\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal("l_english:\n building_holy_01:0 \"Mein Name\"\n", workspace.Get("localization/english/replace/own_l_english.yml"));
        Assert.Equal(0, result.Moved);
    }

    [Fact]
    public void Value_only_rename_stays_in_place()
    {
        using var mod = Fixture(" my_text:0 \"Siehe $building_m_1_0$\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal("l_english:\n my_text:0 \"Siehe $building_holy_01$\"\n", workspace.Get("localization/english/m_l_english.yml"));
        Assert.Equal(0, result.Moved);
    }

    [Fact]
    public void German_and_english_are_handled_per_language()
    {
        using var mod = Fixture(" building_m_1_0:0 \"Mein Name\"\n");
        mod.WriteMod("localization/german/m_l_german.yml", "l_german:\n building_m_1_0:0 \"Mein Name DE\"\n");

        var (result, workspace) = Run(mod);

        Assert.Equal(1, result.Moved);
        Assert.Equal("l_german:\n building_holy_01:0 \"Mein Name DE\"\n", workspace.Get("localization/german/m_l_german.yml"));
        Assert.Null(workspace.Get("localization/german/replace/pmt_renamed_l_german.yml"));
    }

    [Fact]
    public void Crlf_lines_are_removed_with_their_newline()
    {
        using var mod = Fixture(string.Empty);
        mod.WriteMod("localization/english/m_l_english.yml", "l_english:\r\n building_type_m_1_0:0 \"$building_type_holy_01$\"\r\n other:0 \"x\"\r\n");

        var (_, workspace) = Run(mod);

        Assert.Equal("l_english:\r\n other:0 \"x\"\r\n", workspace.Get("localization/english/m_l_english.yml"));
    }
}
