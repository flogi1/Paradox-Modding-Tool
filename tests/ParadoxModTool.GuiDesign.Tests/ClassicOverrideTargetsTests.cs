using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>Zieldatei klassischer Überschreibungen: Ordner und Name nach der gemessenen Ladereihenfolge (HOI IV zuletzt, EU IV zuerst).</summary>
public class ClassicOverrideTargetsTests
{
    private const string ModRoot = @"C:\test\My Mod!";

    private const string Window = "guiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"w\"\n\t}\n}\n";

    /// <summary>Vanilla-Datei; <paramref name="label"/> ist "vanilla" für die Installation selbst, sonst der DLC-Ordner.</summary>
    internal static GuiSourceFile V(string relativePath, string label = "vanilla") =>
        new($@"C:\test\Vanilla\{label}\{relativePath.Replace('/', '\\')}", relativePath, GuiLayer.Vanilla, label, 0, 0);

    private static GuiSourceFile Dependency(string relativePath) =>
        new($@"C:\test\Dep\{relativePath.Replace('/', '\\')}", relativePath, GuiLayer.Dependency, "Dep", 0, 0);

    private static GuiIndex Index(string game, params GuiSourceFile[] files) =>
        ClassicWindowsTests.Build(ClassicWindowsTests.LayoutOf(game), files.Select(f => (f, Window)).ToArray());

    [Fact]
    public void Hoi4_root_window_targets_zz_file()
    {
        var source = V("interface/topbar.gui");

        var target = ClassicOverrideTargets.For(source, Index("hoi4", source), ModRoot, null);

        Assert.Equal("interface/zz_my_mod_overrides.gui", target.RelativePath);
        Assert.Equal(@"C:\test\My Mod!\interface\zz_my_mod_overrides.gui", target.FullPath);
        Assert.Equal(ClassicTargetRule.RootLastWins, target.Rule);
        Assert.False(target.Uncertain);
    }

    [Fact]
    public void Hoi4_subfolder_window_targets_same_subfolder()
    {
        var source = V("interface/equipmentdesigner/x.gui");

        var target = ClassicOverrideTargets.For(source, Index("hoi4", source, V("interface/topbar.gui")), ModRoot, null);

        Assert.Equal("interface/equipmentdesigner/zz_my_mod_overrides.gui", target.RelativePath);
        Assert.Equal(ClassicTargetRule.SubfolderLastWins, target.Rule);
    }

    [Fact]
    public void Eu4_targets_root_00_file_even_for_subfolder_source()
    {
        var source = V("interface/sub/x.gui");

        var target = ClassicOverrideTargets.For(source, Index("eu4", source, V("interface/topbar.gui")), ModRoot, null);

        Assert.Equal("interface/00_my_mod_overrides.gui", target.RelativePath);
        Assert.Equal(@"C:\test\My Mod!\interface\00_my_mod_overrides.gui", target.FullPath);
        Assert.Equal(ClassicTargetRule.RootFirstWins, target.Rule);
    }

    [Fact]
    public void Prefix_grows_when_vanilla_sorts_after()
    {
        var source = V("interface/topbar.gui");

        // zz_my… sortiert vor zzz_zzz.gui ('_' vor 'z'), zzz_my… auch ('m' vor 'z'); erst zzzz_my… lädt danach.
        var target = ClassicOverrideTargets.For(source, Index("hoi4", source, V("interface/zzz_zzz.gui")), ModRoot, null);

        Assert.Equal("interface/zzzz_my_mod_overrides.gui", target.RelativePath);

        // zzz_my… lädt schon nach zzz_last.gui ('m' nach 'l'): eine Stufe genügt.
        var shorter = ClassicOverrideTargets.For(source, Index("hoi4", source, V("interface/zzz_last.gui")), ModRoot, null);

        Assert.Equal("interface/zzz_my_mod_overrides.gui", shorter.RelativePath);
    }

    [Fact]
    public void Prefix_check_uses_only_files_directly_in_the_folder()
    {
        var source = V("interface/topbar.gui");

        var target = ClassicOverrideTargets.For(source, Index("hoi4", source, V("interface/sub/zzz_zzz.gui")), ModRoot, null);

        Assert.Equal("interface/zz_my_mod_overrides.gui", target.RelativePath);
    }

    [Fact]
    public void Eu4_prefix_grows_when_vanilla_sorts_before()
    {
        var source = V("interface/topbar.gui");

        var target = ClassicOverrideTargets.For(source, Index("eu4", source, V("interface/000_first.gui")), ModRoot, null);

        Assert.Equal("interface/0000_my_mod_overrides.gui", target.RelativePath);
    }

    [Fact]
    public void Path_equal_to_dependency_file_is_avoided()
    {
        var source = V("interface/topbar.gui");

        var hoi4 = ClassicOverrideTargets.For(source, Index("hoi4", source, Dependency("interface/zz_my_mod_overrides.gui")), ModRoot, null);
        var eu4 = ClassicOverrideTargets.For(source, Index("eu4", source, Dependency("interface/00_my_mod_overrides.gui")), ModRoot, null);

        Assert.Equal("interface/zzz_my_mod_overrides.gui", hoi4.RelativePath);
        Assert.Equal("interface/000_my_mod_overrides.gui", eu4.RelativePath);
    }

    [Fact]
    public void Dlc_source_is_uncertain()
    {
        var source = V("interface/topbar.gui", "integrated_dlc_a");

        var target = ClassicOverrideTargets.For(source, Index("hoi4", source), ModRoot, null);

        Assert.True(target.Uncertain);
        Assert.Equal("interface/zz_my_mod_overrides.gui", target.RelativePath);
    }

    [Fact]
    public void Dlc_file_in_the_same_folder_does_not_mark_the_target_uncertain()
    {
        // Ob eine DLC-Datei im selben Ordner zählt, hängt am Fenster (definiert sie es?); das entscheidet der Planer, nicht das Ziel.
        var source = V("interface/topbar.gui");

        var same = ClassicOverrideTargets.For(source, Index("hoi4", source, V("interface/dlc_window.gui", "integrated_dlc_a")), ModRoot, null);
        var other = ClassicOverrideTargets.For(source, Index("hoi4", source, V("interface/sub/dlc_window.gui", "integrated_dlc_a")), ModRoot, null);

        Assert.False(same.Uncertain);
        Assert.False(other.Uncertain);
    }

    [Fact]
    public void Configured_name_is_used_as_given()
    {
        var source = V("interface/topbar.gui");

        var target = ClassicOverrideTargets.For(source, Index("hoi4", source, V("interface/zzz_zzz.gui")), ModRoot, "aaa_overrides.gui");

        Assert.Equal("interface/aaa_overrides.gui", target.RelativePath);
        Assert.Equal(ClassicTargetRule.Configured, target.Rule);
    }

    [Fact]
    public void Default_file_name_follows_the_window_rule()
    {
        Assert.Equal("zz_my_mod_overrides.gui", ClassicOverrideTargets.DefaultFileName(ModRoot, ClassicWindowsTests.LayoutOf("hoi4")));
        Assert.Equal("00_my_mod_overrides.gui", ClassicOverrideTargets.DefaultFileName(ModRoot, ClassicWindowsTests.LayoutOf("eu4")));
    }

    [Fact]
    public void Mod_targets_use_the_classic_default_name()
    {
        Assert.Equal("zz_my_mod_overrides.gui", GuiModTargets.For(ModRoot, classic: true).OverrideFileName);
        Assert.Equal("00_my_mod_overrides.gui", GuiModTargets.For(ModRoot, firstWins: true, classic: true).OverrideFileName);
        Assert.Equal("mine.gui", GuiModTargets.For(ModRoot, "mine.gui", classic: true).OverrideFileName);
        Assert.Equal("my_mod_overrides.gui", GuiModTargets.For(ModRoot).OverrideFileName);
    }
}
