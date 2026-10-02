using System.Text;
using Xunit;

namespace ParadoxModTool.Patches.Tests;

public sealed class PatchWorkspaceTests
{
    [Fact]
    public void Read_keeps_bom_and_crlf_information()
    {
        using var mod = new TempMod();
        mod.WriteMod("a.txt", "a = 1\r\nb = 2\r\n");

        var content = TextFile.Read(mod.ModPath("a.txt"));

        Assert.True(content.HasBom);
        Assert.Equal("\r\n", content.NewLine);
        Assert.StartsWith("a = 1", content.Text);
    }

    [Fact]
    public void Read_without_bom_and_with_lf()
    {
        using var mod = new TempMod();
        mod.WriteMod("a.txt", "a = 1\nb = 2\n", bom: false);

        var content = TextFile.Read(mod.ModPath("a.txt"));

        Assert.False(content.HasBom);
        Assert.Equal("\n", content.NewLine);
    }

    [Fact]
    public void Glob_matches_star_and_double_star()
    {
        Assert.True(GlobMatcher.IsMatch("history/provinces/*.txt", "history/provinces/k_x.txt"));
        Assert.False(GlobMatcher.IsMatch("history/provinces/*.txt", "history/provinces/sub/k_x.txt"));
        Assert.True(GlobMatcher.IsMatch("common/**/*.txt", "common/a.txt"));
        Assert.True(GlobMatcher.IsMatch("common/**/*.txt", "common/x/y/a.txt"));
        Assert.True(GlobMatcher.IsMatch("Common/Buildings/00_special_buildings.txt", "common/buildings/00_special_buildings.txt"));
        Assert.False(GlobMatcher.IsMatch("common/*.txt", "common/a.yml"));
    }

    [Fact]
    public void Enumerate_returns_sorted_relative_paths_and_skips_dot_folders()
    {
        using var mod = new TempMod();
        mod.WriteMod("history/provinces/k_b.txt", "x");
        mod.WriteMod("history/provinces/k_a.txt", "x");
        mod.WriteMod(".pmt-patches/k_c.txt", "x");

        Assert.Equal(new[] { "history/provinces/k_a.txt", "history/provinces/k_b.txt" }, GlobMatcher.Enumerate(mod.ModRoot, "**/*.txt").ToArray());
    }

    [Fact]
    public void Enumerate_starts_at_the_fixed_folder_and_respects_depth()
    {
        using var mod = new TempMod();
        mod.WriteMod("history/provinces/k_a.txt", "x");
        mod.WriteMod("history/provinces/sub/k_b.txt", "x");
        mod.WriteMod("history/titles/k_c.txt", "x");
        mod.WriteMod("common/buildings/00_special_buildings.txt", "x");

        Assert.Equal(new[] { "history/provinces/k_a.txt" }, GlobMatcher.Enumerate(mod.ModRoot, "history/provinces/*.txt").ToArray());
        Assert.Equal(new[] { "history/provinces/k_a.txt", "history/provinces/sub/k_b.txt" }, GlobMatcher.Enumerate(mod.ModRoot, "history/provinces/**/*.txt").ToArray());
        Assert.Equal(new[] { "common/buildings/00_special_buildings.txt" }, GlobMatcher.Enumerate(mod.ModRoot, "common/buildings/00_special_buildings.txt").ToArray());
        Assert.Empty(GlobMatcher.Enumerate(mod.ModRoot, "common/missing/*.txt"));
        Assert.Empty(GlobMatcher.Enumerate(mod.ModRoot, "common/missing.txt"));
    }

    [Fact]
    public void Workspace_loads_lazily_and_tracks_created_copies()
    {
        using var mod = new TempMod();
        mod.WriteVanilla("common/holdings/00_holdings.txt", "castle_holding = { buildings = { } }\r\n");
        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);

        Assert.Null(workspace.Get("common/holdings/00_holdings.txt"));
        Assert.True(workspace.CreateFromVanilla("common/holdings/00_holdings.txt"));

        Assert.Equal("castle_holding = { buildings = { } }\r\n", workspace.Get("common/holdings/00_holdings.txt"));
        Assert.True(workspace.IsCreatedFromVanilla("common/holdings/00_holdings.txt"));
        Assert.Null(workspace.Original("common/holdings/00_holdings.txt"));
        Assert.Equal("\r\n", workspace.NewLineOf("common/holdings/00_holdings.txt"));
        Assert.Contains("common/holdings/00_holdings.txt", workspace.Touched);
        Assert.False(workspace.CreateFromVanilla("common/missing.txt"));
    }

    [Fact]
    public void Workspace_set_keeps_original_and_lists_in_memory_files()
    {
        using var mod = new TempMod();
        mod.WriteMod("common/a.txt", "a = 1\n", bom: false);
        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);

        workspace.Set("common/a.txt", "a = 2\n");
        workspace.Set("common/new.txt", "b = 1");

        Assert.Equal("a = 1\n", workspace.Original("common/a.txt"));
        Assert.Equal("a = 2\n", workspace.Get("common/a.txt"));
        Assert.False(workspace.HasBomOf("common/a.txt"));
        Assert.Equal("\n", workspace.NewLineOf("common/a.txt"));
        Assert.True(workspace.HasBomOf("common/new.txt"));
        Assert.Equal("\r\n", workspace.NewLineOf("common/new.txt"));
        Assert.Equal(new[] { "common/a.txt", "common/new.txt" }, workspace.ModFiles("common/*.txt").ToArray());
    }

    [Fact]
    public void Workspace_collects_notes_and_conflicts_per_file()
    {
        using var mod = new TempMod();
        var workspace = new PatchWorkspace(mod.ModRoot, mod.VanillaRoot);

        workspace.AddNote("a.txt", "n1");
        workspace.AddConflict("a.txt", "c1");

        Assert.Equal(new[] { "n1" }, workspace.NotesOf("a.txt"));
        Assert.Equal(new[] { "c1" }, workspace.ConflictsOf("a.txt"));
        Assert.Empty(workspace.NotesOf("b.txt"));
    }
}
