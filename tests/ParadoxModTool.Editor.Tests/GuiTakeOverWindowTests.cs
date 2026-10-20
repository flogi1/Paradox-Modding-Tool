using System.Windows;
using System.Windows.Controls;
using ParadoxModTool.Editor.Gui;
using ParadoxModTool.GuiDesign;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.Editor.Tests;

/// <summary>Übersicht "In Mod übernehmen" für klassische Fenster: Spalte mit der Regel, Hinweise, Umzüge und "Ganze Datei kopieren…".</summary>
public sealed class GuiTakeOverWindowTests
{
    private const string ModRoot = @"C:\test\Mod";

    private const string Overrides = @"C:\test\Mod\interface\zz_mod_overrides.gui";

    private static GuiPlanEntry Override(string name) =>
        new GuiPlanEntry(GuiPlanEntryKind.Override, name, GuiDefinitionKind.NamedWidget, Overrides, 0) { Rule = ClassicTargetRule.RootLastWins };

    private static GuiOverridePlan Plan(IReadOnlyList<GuiPlanEntry> entries, IReadOnlyList<GuiPlanWarning>? warnings = null, GuiCopyReason? reason = null) =>
        new(entries, new[] { new GuiPlanFile(Overrides, null, "guiTypes = {\n}\n") }, Array.Empty<GuiPlanBlocker>(), reason)
        {
            Warnings = warnings ?? Array.Empty<GuiPlanWarning>(),
        };

    private static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var inner in Logical<T>(child))
            {
                yield return inner;
            }
        }
    }

    private static List<string> Texts(Window window) => Logical<TextBlock>(window).Select(t => t.Text).ToList();

    private static GridView GridOf(Window window) => (GridView)Logical<ListView>(window).Single().View;

    [Fact]
    public void Shows_rule_column_and_warnings() => Sta.Run(() =>
    {
        var warning = new GuiPlanWarning(GuiPlanWarningKind.DependencyWins, "topbar", new[] { "interface/dep.gui:3" });
        var window = new GuiTakeOverWindow(Plan(new[] { Override("topbar") }, new[] { warning }), ModRoot, Array.Empty<string>(), classic: true);

        Assert.Contains(GridOf(window).Columns, c => Equals(c.Header, GuiTexts.TakeOverColumnRule));
        var row = Assert.IsType<GuiTakeOverWindow.Row>(Assert.Single(Logical<ListView>(window).Single().Items.Cast<object>()));
        Assert.Equal(GuiTexts.TakeOverRule(ClassicTargetRule.RootLastWins), row.Rule);
        Assert.Contains(Texts(window), t => t.Contains(GuiTexts.TakeOverWarning(warning), StringComparison.Ordinal));

        // Ohne Regel (Jomini) keine Spalte und keine Hinweisbox.
        var jomini = new GuiPlanEntry(GuiPlanEntryKind.Override, "x", GuiDefinitionKind.Type, @"C:\test\Mod\gui\x.gui", 0);
        var plain = new GuiTakeOverWindow(Plan(new[] { jomini }), ModRoot, Array.Empty<string>());
        Assert.DoesNotContain(GridOf(plain).Columns, c => Equals(c.Header, GuiTexts.TakeOverColumnRule));
        Assert.DoesNotContain(Texts(plain), t => t == GuiTexts.TakeOverWarnings);
    });

    [Fact]
    public void Copy_button_only_for_classic() => Sta.Run(() =>
    {
        var plan = Plan(new[] { Override("topbar") });

        var classic = new GuiTakeOverWindow(plan, ModRoot, Array.Empty<string>(), classic: true);
        var jomini = new GuiTakeOverWindow(plan, ModRoot, Array.Empty<string>());
        var copying = new GuiTakeOverWindow(Plan(new[] { Override("topbar") }, reason: GuiCopyReason.Requested), ModRoot, Array.Empty<string>(), classic: true);

        Assert.Contains(Logical<Button>(classic), b => Equals(b.Content, GuiTexts.TakeOverCopyFile));
        Assert.False(classic.CopyFileRequested);
        Assert.DoesNotContain(Logical<Button>(jomini), b => Equals(b.Content, GuiTexts.TakeOverCopyFile));

        // Kopiert der Plan die Datei schon, gibt es nichts mehr zu wählen.
        Assert.DoesNotContain(Logical<Button>(copying), b => Equals(b.Content, GuiTexts.TakeOverCopyFile));
    });

    [Fact]
    public void Copy_button_not_offered_without_changes_or_with_draft_errors() => Sta.Run(() =>
    {
        // Ohne Einträge kopierte "Ganze Datei kopieren…" eine unveränderte Vanilla-Datei in die Mod; mit Syntaxfehlern ginge die Kopie ohnehin nicht.
        var empty = new GuiTakeOverWindow(
            new GuiOverridePlan(Array.Empty<GuiPlanEntry>(), Array.Empty<GuiPlanFile>(), Array.Empty<GuiPlanBlocker>(), null), ModRoot, Array.Empty<string>(), classic: true);
        var errors = new GuiTakeOverWindow(
            new GuiOverridePlan(
                Array.Empty<GuiPlanEntry>(),
                Array.Empty<GuiPlanFile>(),
                new[] { new GuiPlanBlocker(GuiPlanBlockerKind.DraftErrors, "interface/topbar.gui", Array.Empty<string>()) },
                null),
            ModRoot,
            Array.Empty<string>(),
            classic: true);

        Assert.DoesNotContain(Logical<Button>(empty), b => Equals(b.Content, GuiTexts.TakeOverCopyFile));
        Assert.DoesNotContain(Logical<Button>(errors), b => Equals(b.Content, GuiTexts.TakeOverCopyFile));
    });

    private static GuiOverridePlan MovingPlan(string? dependency = null) => Plan(new[]
    {
        new GuiPlanEntry(GuiPlanEntryKind.RemoveFromMod, "topbar", GuiDefinitionKind.NamedWidget, @"C:\test\Mod\interface\mymod_topbar.gui", 0) { LosesToDependency = dependency },
        Override("topbar"),
    });

    [Fact]
    public void Lists_moves_in_warning_box() => Sta.Run(() =>
    {
        var window = new GuiTakeOverWindow(MovingPlan(), ModRoot, Array.Empty<string>(), classic: true);

        Assert.Equal(new[] { ("topbar", "interface/mymod_topbar.gui", "interface/zz_mod_overrides.gui", (string?)null) }, window.Moves);
        Assert.Contains(Texts(window), t => t == GuiTexts.TakeOverWarnings);
        Assert.Contains(
            Texts(window),
            t => t.Contains(GuiTexts.ClassicMoveWarning("topbar", "interface/mymod_topbar.gui", "interface/zz_mod_overrides.gui", firstWins: false, dependency: null), StringComparison.Ordinal));
    });

    [Fact]
    public void Move_wording_for_eu4_and_dependency() => Sta.Run(() =>
    {
        var eu4 = new GuiTakeOverWindow(MovingPlan(), ModRoot, Array.Empty<string>(), classic: true, firstWins: true);
        var dependency = new GuiTakeOverWindow(MovingPlan("Dep"), ModRoot, Array.Empty<string>(), classic: true);

        var eu4Text = GuiTexts.ClassicMoveWarning("topbar", "interface/mymod_topbar.gui", "interface/zz_mod_overrides.gui", firstWins: true, dependency: null);
        Assert.NotEqual(eu4Text, GuiTexts.ClassicMoveWarning("topbar", "interface/mymod_topbar.gui", "interface/zz_mod_overrides.gui", firstWins: false, dependency: null));
        Assert.Contains(Texts(eu4), t => t.Contains(eu4Text, StringComparison.Ordinal));
        Assert.Contains(
            Texts(dependency),
            t => t.Contains(GuiTexts.ClassicMoveWarning("topbar", "interface/mymod_topbar.gui", "interface/zz_mod_overrides.gui", firstWins: false, dependency: "Dep"), StringComparison.Ordinal));
    });

    private static Button ApplyButton(Window window) => Logical<Button>(window).Single(b => Equals(b.Content, GuiTexts.TakeOverApply));

    /// <summary>Zeigt die Übersicht außerhalb des Bildschirms als Dialog, klickt "Übernehmen" und schließt sie, falls sie danach noch offen ist.</summary>
    private static (bool? Result, bool StillOpen) ClickApply(GuiTakeOverWindow window)
    {
        window.Left = -3000;
        window.Top = -3000;
        window.ShowActivated = false;
        var stillOpen = false;
        window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(() =>
        {
            ApplyButton(window).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            stillOpen = window.IsVisible;
            if (stillOpen)
            {
                window.Close();
            }
        });
        var result = window.ShowDialog();
        return (result, stillOpen);
    }

    [Fact]
    public void Apply_asks_before_moving_and_no_keeps_the_overview() => Sta.Run(() =>
    {
        IReadOnlyList<(string, string, string, string?)>? seen = null;
        Window? ownerSeen = null;
        var window = new GuiTakeOverWindow(MovingPlan(), ModRoot, Array.Empty<string>(), classic: true)
        {
            ConfirmMoves = (owner, moves) =>
            {
                ownerSeen = owner;
                seen = moves;
                return false;
            },
        };

        var (result, stillOpen) = ClickApply(window);

        Assert.Same(window.Moves, seen);
        Assert.Same(window, ownerSeen);
        Assert.True(stillOpen);
        Assert.NotEqual(true, result);
    });

    [Fact]
    public void Apply_after_yes_closes_with_true() => Sta.Run(() =>
    {
        var asked = 0;
        var window = new GuiTakeOverWindow(MovingPlan(), ModRoot, Array.Empty<string>(), classic: true)
        {
            ConfirmMoves = (_, _) =>
            {
                asked++;
                return true;
            },
        };

        var (result, stillOpen) = ClickApply(window);

        Assert.Equal(1, asked);
        Assert.False(stillOpen);
        Assert.True(result);
    });
}
