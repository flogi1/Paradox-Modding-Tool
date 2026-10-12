using Xunit;

namespace ParadoxModTool.ModCompat.Tests;

/// <summary>
/// GUI-Dateien in der Mod-Kompatibilität: Schlüssel sind <c>type name</c> in <c>types X { }</c> und <c>template name</c> (wie im Kompatch-Merge).
/// Oberste Fenster und Widgets sind keine Schlüssel; zwei Mods mit je einem eigenen Fenster widersprechen sich nicht.
/// </summary>
public class CompatGuiTypesTests : CompatTestBase
{
    private const string EuVGui = "in_game/gui/";

    private static CompatResult Run(IReadOnlyList<CompatParticipant> mods, OverrideRules? rules = null) =>
        CompatAnalyzer.Analyze(new CompatRequest { Participants = mods, Rules = rules ?? Rules() });

    private static OverrideRules Ck3Rules() => new(new[] { new OverrideRule("gui", OverrideBehavior.FirstWins) });

    /// <summary>Victoria 3 (gemessen 2026-10-11): Die Playset-Reihenfolge zählt nicht, die zuerst geladene Datei gewinnt (Dateien vor Unterordnern).</summary>
    [Fact]
    public void Vic3_first_loaded_file_wins_regardless_of_playset_order()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "gui/shared/zz_a.gui", "types A {\n\ttype hud_button = button { size = { 10 10 } }\n}\n");
        Put(b, "gui/00_b.gui", "types B {\n\ttype hud_button = button { size = { 20 20 } }\n}\n");
        var rules = new OverrideRules(new[] { new OverrideRule("gui", OverrideBehavior.FirstWins) }) { FirstWinsInLoadOrder = true };

        var finding = Assert.Single(Run(new[] { a, b }, rules).Findings, f => f.Key == "type hud_button");

        Assert.Equal("B", finding.Winner!.Name);
    }

    [Fact]
    public void Vic3_vanilla_that_loads_first_is_the_winner()
    {
        var vanilla = Vanilla();
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(vanilla, "gui/shared/button_icons.gui", "types V {\n\ttype hud_button = button { size = { 5 5 } }\n}\n");
        Put(a, "gui/shared/zz_a.gui", "types A {\n\ttype hud_button = button { size = { 10 10 } }\n}\n");
        Put(b, "gui/shared/zzz_b.gui", "types B {\n\ttype hud_button = button { size = { 20 20 } }\n}\n");
        var rules = new OverrideRules(new[] { new OverrideRule("gui", OverrideBehavior.FirstWins) }) { FirstWinsInLoadOrder = true };

        var finding = Assert.Single(Run(new[] { vanilla, a, b }, rules).Findings, f => f.Key == "type hud_button");

        Assert.True(finding.Winner!.IsVanilla);
        Assert.Contains("Vanilla", finding.Message);
    }

    [Fact]
    public void Same_type_changed_differently_is_a_conflict()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, EuVGui + "a.gui", "types A {\n\ttype hud_button = button { size = { 10 10 } }\n}\n");
        Put(b, EuVGui + "b.gui", "types B {\n\ttype hud_button = button { size = { 20 20 } }\n}\n");

        var finding = Assert.Single(Run(new[] { a, b }).Findings, f => f.Key == "type hud_button");

        Assert.Equal(CompatKind.KeyConflict, finding.Kind);
    }

    [Fact]
    public void Same_template_with_same_content_is_identical()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, EuVGui + "a.gui", "template big_font { fontsize = 20 }\n");
        Put(b, EuVGui + "b.gui", "template big_font {\n\t# gleich\n\tfontsize = 20\n}\n");

        Assert.Equal(CompatKind.KeyIdentical, Assert.Single(Run(new[] { a, b }).Findings, f => f.Key == "template big_font").Kind);
    }

    [Fact]
    public void Own_windows_in_different_files_are_no_finding()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, EuVGui + "a.gui", "window = { name = \"a_window\" }\n");
        Put(b, EuVGui + "b.gui", "window = { name = \"b_window\" }\n");

        Assert.DoesNotContain(Run(new[] { a, b }).Findings, f => f.Key == "window");
    }

    [Fact]
    public void First_wins_rule_gives_the_same_keys()
    {
        var a = Mod("A", 1);
        var b = Mod("B", 2);
        Put(a, "gui/a.gui", "widget = { name = hud }\ntypes HUD\n{\n\ttype hud_button = button { size = { 10 10 } }\n}\n");
        Put(b, "gui/b.gui", "types Mine\n{\n\ttype hud_button = button { size = { 20 20 } }\n}\n");

        var findings = Run(new[] { a, b }, Ck3Rules()).Findings;

        Assert.Contains(findings, f => f.Key == "type hud_button" && f.Kind == CompatKind.KeyConflict);
        Assert.DoesNotContain(findings, f => f.Key == "widget");
    }

    [Fact]
    public void Contribution_points_at_the_type_line()
    {
        var a = Mod("A", 1);
        var path = Put(a, EuVGui + "a.gui", "window = { name = w }\ntypes A {\n\ttype hud_button = button { size = { 10 10 } }\n}\n");

        var contribution = Assert.Single(ContributionExtractor.Extract(a, EuVGui + "a.gui", path, Rules().Match(EuVGui + "a.gui"), new ParseCache(), out var error));

        Assert.Null(error);
        Assert.Equal("type hud_button", contribution.Key);
        Assert.Equal(3, contribution.Line);
        Assert.StartsWith("type hud_button", contribution.Text);
    }
}
