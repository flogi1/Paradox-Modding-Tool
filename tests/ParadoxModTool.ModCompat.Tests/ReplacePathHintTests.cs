using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// Hinweise auf replace_path-Einträge, die nicht wirken (Europa Universalis V: Eintrag beginnt mit <c>in_game/</c> usw.) oder das Spiel beim Start
/// abstürzen lassen (Europa Universalis IV: Unterordner von <c>interface/</c> mit .gui-Dateien), gemessen 2026-09-30.
/// </summary>
public class ReplacePathHintTests : CompatTestBase
{
    private static readonly string[] Eu5Roots = { "in_game", "main_menu", "loading_screen" };
    private static readonly string[] Eu4CrashRoots = { "interface" };

    private static CompatResult Run(
        IReadOnlyList<CompatParticipant> all,
        Dictionary<string, IReadOnlyList<string>> entries,
        IReadOnlyCollection<string>? roots = null,
        IReadOnlyCollection<string>? crashRoots = null,
        string? focus = null) =>
        CompatAnalyzer.Analyze(new CompatRequest
        {
            Participants = all,
            Rules = Rules(),
            FocusId = focus,
            ReplacePathEntries = entries,
            ReplacePathRoots = roots ?? Array.Empty<string>(),
            ReplacePathGuiCrashRoots = crashRoots ?? Array.Empty<string>(),
        });

    [Fact]
    public void Entry_starting_with_a_root_gives_a_no_effect_hint()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);

        var result = Run(new[] { a, b }, new() { ["A"] = new[] { "in_game/common/trait_flavor", "common/x" } }, Eu5Roots);

        var finding = Assert.Single(result.Findings, f => f.Kind == CompatKind.ReplacePathNoEffect);
        Assert.Equal(CompatSeverity.Hint, finding.Severity);
        Assert.Equal("in_game/common/trait_flavor", finding.RelativePath);
        Assert.Contains("common/trait_flavor", finding.Message.Replace("in_game/common/trait_flavor", string.Empty));
        Assert.Contains("\"A\"", finding.Message);
    }

    [Fact]
    public void Root_check_ignores_case_and_backslashes()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);

        var result = Run(new[] { a, b }, new() { ["B"] = new[] { "Main_Menu\\gui\\pmt_rp1" } }, Eu5Roots);

        var finding = Assert.Single(result.Findings, f => f.Kind == CompatKind.ReplacePathNoEffect);
        Assert.Equal("Main_Menu/gui/pmt_rp1", finding.RelativePath);
    }

    [Fact]
    public void Without_roots_in_the_profile_there_is_no_hint()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);

        var result = Run(new[] { a, b }, new() { ["A"] = new[] { "in_game/common/trait_flavor" } });

        Assert.DoesNotContain(result.Findings, f => f.Kind == CompatKind.ReplacePathNoEffect);
    }

    [Fact]
    public void Interface_subfolder_with_gui_files_gives_a_crash_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "interface/pmt_rp/pmt.gui", "guiTypes = { }\n");

        var result = Run(new[] { a, b }, new() { ["A"] = new[] { "interface/pmt_rp" } }, crashRoots: Eu4CrashRoots);

        var finding = Assert.Single(result.Findings, f => f.Kind == CompatKind.ReplacePathGuiCrash);
        Assert.Equal(CompatSeverity.Conflict, finding.Severity);
        Assert.Equal("interface/pmt_rp", finding.RelativePath);
        Assert.Contains("\"A\"", finding.Message);
    }

    [Fact]
    public void Gui_files_of_another_mod_in_the_folder_also_give_a_crash_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(b, "interface/pmt_rp/other.gui", "guiTypes = { }\n");

        var result = Run(new[] { a, b }, new() { ["A"] = new[] { "interface/pmt_rp" } }, crashRoots: Eu4CrashRoots);

        var finding = Assert.Single(result.Findings, f => f.Kind == CompatKind.ReplacePathGuiCrash);
        Assert.Contains("\"B\"", finding.Message);
    }

    [Fact]
    public void Interface_subfolder_without_gui_files_gives_no_crash_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "interface/pmt_rp/readme.txt", "x\n");
        Put(a, "interface/pmt_rp/deeper/pmt.gui", "guiTypes = { }\n");

        var result = Run(new[] { a, b }, new() { ["A"] = new[] { "interface/pmt_rp" } }, crashRoots: Eu4CrashRoots);

        Assert.DoesNotContain(result.Findings, f => f.Kind == CompatKind.ReplacePathGuiCrash);
    }

    [Fact]
    public void Interface_itself_is_no_subfolder_and_gives_no_crash_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "interface/pmt.gui", "guiTypes = { }\n");

        var result = Run(new[] { a, b }, new() { ["A"] = new[] { "interface" } }, crashRoots: Eu4CrashRoots);

        Assert.DoesNotContain(result.Findings, f => f.Kind == CompatKind.ReplacePathGuiCrash);
    }

    [Fact]
    public void Without_crash_roots_in_the_profile_there_is_no_crash_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "interface/pmt_rp/pmt.gui", "guiTypes = { }\n");

        var result = Run(new[] { a, b }, new() { ["A"] = new[] { "interface/pmt_rp" } });

        Assert.DoesNotContain(result.Findings, f => f.Kind == CompatKind.ReplacePathGuiCrash);
    }

    [Fact]
    public void Mods_outside_the_run_give_no_hint()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        var c = Mod("C", 3);

        var result = CompatAnalyzer.Analyze(new CompatRequest
        {
            Participants = new[] { a, b, c },
            Rules = Rules(),
            FocusId = "A",
            CounterpartIds = new[] { "B" },
            ReplacePathEntries = new Dictionary<string, IReadOnlyList<string>> { ["C"] = new[] { "in_game/common/x" } },
            ReplacePathRoots = Eu5Roots,
        });

        Assert.DoesNotContain(result.Findings, f => f.Kind == CompatKind.ReplacePathNoEffect);
    }
}
