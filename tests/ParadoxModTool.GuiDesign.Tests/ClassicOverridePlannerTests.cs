using ParadoxModTool.GuiDesign.Classic;
using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Index;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

/// <summary>"In Mod übernehmen" für HOI IV und EU IV: nur die geänderten Fenster, in eine Datei, die nach der gemessenen Ladereihenfolge gewinnt.</summary>
public class ClassicOverridePlannerTests
{
    private const string ModRoot = @"C:\test\Mod";

    private const string OverridesPath = @"C:\test\Mod\interface\zz_mod_overrides.gui";

    private const string Topbar = "\tcontainerWindowType = {\n\t\tname = \"topbar\"\n\t\tposition = { x = 0 y = 0 }\n\t}\n";

    private const string Other = "\tcontainerWindowType = {\n\t\tname = \"other\"\n\t\tposition = { x = 5 y = 5 }\n\t}\n";

    private const string VanillaText = "guiTypes = {\n" + Topbar + Other + "}\n";

    private static GuiSourceFile Source(string relativePath = "interface/topbar.gui", string label = "vanilla") =>
        ClassicOverrideTargetsTests.V(relativePath, label);

    private static (GuiSourceFile, string) Mod(string relativePath, string text) => (GuiIndexTests.F(relativePath, GuiLayer.Mod), text);

    private static string Wrap(string windows) => "guiTypes = {\n" + windows + "}\n";

    /// <summary>Anfrage wie "In Mod übernehmen": Grundlage ist der Start des Entwurfs, <paramref name="edit"/> ändert ihn.</summary>
    private static GuiOverrideRequest Setup(
        string game,
        Func<string, string> edit,
        GuiSourceFile? source = null,
        string vanillaText = VanillaText,
        string? configured = null,
        params (GuiSourceFile File, string Text)[] extra)
    {
        source ??= Source();
        var layout = ClassicWindowsTests.LayoutOf(game);
        var files = extra.Prepend((source, vanillaText)).ToArray();
        var index = ClassicWindowsTests.Build(layout, files);
        var texts = files.Where(f => f.Item1.Layer == GuiLayer.Mod).ToDictionary(f => f.Item1.FullPath, f => f.Item2, StringComparer.OrdinalIgnoreCase);
        var targets = GuiModTargets.For(ModRoot, configured, firstWins: layout.FirstWins, classic: true);
        var start = GuiDraft.Start(source, vanillaText, index).Text;
        return new GuiOverrideRequest(source, vanillaText, edit(start), index, targets, path => texts.TryGetValue(path, out var text) ? text : null, start);
    }

    private static string MoveTopbar(string text) => text.Replace("x = 0 y = 0", "x = 10 y = 0");

    [Fact]
    public void Changed_window_goes_to_override_file()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar));

        Assert.Empty(plan.Blockers);
        Assert.Empty(plan.Warnings);
        var entry = Assert.Single(plan.Entries);
        Assert.Equal(GuiPlanEntryKind.Override, entry.Kind);
        Assert.Equal("topbar", entry.Name);
        Assert.Equal(GuiDefinitionKind.NamedWidget, entry.DefinitionKind);
        Assert.Equal(ClassicTargetRule.RootLastWins, entry.Rule);
        Assert.Equal(OverridesPath, entry.TargetPath);
        Assert.Equal(2, entry.Line);
        var file = Assert.Single(plan.Files);
        Assert.True(file.IsNew);
        Assert.Equal(OverridesPath, file.FullPath);
        Assert.Equal(Wrap(MoveTopbar(Topbar)), file.NewText);
        Assert.DoesNotContain("other", file.NewText);
        Assert.Null(file.EncodingName);
        Assert.Null(plan.CopyReason);
        Assert.True(plan.CanApply);
    }

    [Fact]
    public void Replace_in_winning_mod_file()
    {
        var mod = Mod("interface/zz_mod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50")));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace("x = 50", "x = 60"), extra: mod));

        Assert.Empty(plan.Blockers);
        var entry = Assert.Single(plan.Entries);
        Assert.Equal(GuiPlanEntryKind.Replace, entry.Kind);
        Assert.Equal(@"C:\test\Mod\interface\zz_mod_topbar.gui", entry.TargetPath);
        Assert.Equal(ClassicTargetRule.ExistingWinner, entry.Rule);
        var file = Assert.Single(plan.Files);
        Assert.False(file.IsNew);
        Assert.Equal(Wrap(Topbar.Replace("x = 0", "x = 60")), file.NewText);
    }

    [Fact]
    public void Losing_mod_copy_moves_to_override_file()
    {
        // mymod_topbar.gui lädt vor topbar.gui und verliert in HOI IV; dort zu ersetzen bliebe ohne Wirkung.
        var keep = "\tcontainerWindowType = {\n\t\tname = \"keep\"\n\t}\n";
        var mod = Mod("interface/mymod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50") + keep));

        var request = Setup("hoi4", text => text.Replace("x = 50", "x = 60"), extra: mod);

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Empty(plan.Blockers);
        Assert.Equal(new[] { GuiPlanEntryKind.RemoveFromMod, GuiPlanEntryKind.Override }, plan.Entries.Select(e => e.Kind));
        Assert.Equal(@"C:\test\Mod\interface\mymod_topbar.gui", plan.Entries[0].TargetPath);
        Assert.Equal(OverridesPath, plan.Entries[1].TargetPath);
        Assert.Equal(2, plan.Files.Count);
        Assert.Equal(Wrap(keep), plan.Files.Single(f => !f.IsNew).NewText);
        Assert.Equal(Wrap(Topbar.Replace("x = 0", "x = 60")), plan.Files.Single(f => f.IsNew).NewText);

        var simulated = ClassicOverridePlanner.Simulate(request.Index, plan.Files, ModRoot);

        Assert.DoesNotContain(ClassicWindows.FindAll(simulated, "topbar"), d => d.File.RelativePath == "interface/mymod_topbar.gui");
        Assert.Single(simulated.Files, f => f.RelativePath == "interface/mymod_topbar.gui");
        var winner = ClassicWindows.Winner(simulated, "topbar")!;
        Assert.Equal("interface/zz_mod_overrides.gui", winner.File.RelativePath);
        Assert.Equal(GuiLayer.Mod, winner.File.Layer);
    }

    [Fact]
    public void Moves_lists_losing_copy()
    {
        var keep = "\tcontainerWindowType = {\n\t\tname = \"keep\"\n\t}\n";
        var mod = Mod("interface/mymod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50") + keep));
        var moving = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace("x = 50", "x = 60"), extra: mod));

        var move = Assert.Single(ClassicOverridePlanner.Moves(moving, ModRoot));

        // Gewinner ist Vanilla: keine Abhängigkeits-Mod im Umzug.
        Assert.Equal(("topbar", "interface/mymod_topbar.gui", "interface/zz_mod_overrides.gui", (string?)null), move);
        Assert.Empty(ClassicOverridePlanner.Moves(GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar)), ModRoot));
    }

    [Fact]
    public void Moves_name_the_winning_dependency()
    {
        // mymod_topbar.gui verliert gegen zzzz_dep.gui der Abhängigkeits-Mod, nicht gegen Vanilla.
        var mod = Mod("interface/mymod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50")));
        var dependency = (new GuiSourceFile(@"C:\test\Dep\interface\zzzz_dep.gui", "interface/zzzz_dep.gui", GuiLayer.Dependency, "Dep", 0, 0), Wrap(Topbar.Replace("x = 0", "x = 70")));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace("x = 50", "x = 60"), extra: new[] { mod, dependency }));

        Assert.Equal(new[] { ("topbar", "interface/mymod_topbar.gui", "interface/zz_mod_overrides.gui", (string?)"Dep") }, ClassicOverridePlanner.Moves(plan, ModRoot));
    }

    [Fact]
    public void Whole_file_copy_has_no_moves()
    {
        // Die Kopie nimmt topbar aus der verlierenden Mod-Datei mit (RemoveFromMod), aber ohne Override: das ist kein Umzug, nach dem gefragt wird.
        var mod = Mod("interface/mymod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50")));
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace("x = 50", "x = 60"), extra: mod) with { ForceCopyFile = true });

        Assert.Equal(GuiCopyReason.Requested, plan.CopyReason);
        Assert.Contains(plan.Entries, e => e.Kind == GuiPlanEntryKind.RemoveFromMod && e.Name == "topbar");
        Assert.Empty(ClassicOverridePlanner.Moves(plan, ModRoot));
    }

    [Fact]
    public void Move_wording_follows_rule_and_winner()
    {
        var hoi4 = GuiTexts.ClassicMoveWarning("topbar", "interface/a.gui", "interface/zz_mod_overrides.gui", firstWins: false, dependency: null);
        var eu4 = GuiTexts.ClassicMoveWarning("topbar", "interface/a.gui", "interface/00_mod_overrides.gui", firstWins: true, dependency: null);
        var hoi4Dependency = GuiTexts.ClassicMoveWarning("topbar", "interface/a.gui", "interface/zz_mod_overrides.gui", firstWins: false, dependency: "Dep");
        var eu4Dependency = GuiTexts.ClassicMoveWarning("topbar", "interface/a.gui", "interface/00_mod_overrides.gui", firstWins: true, dependency: "Dep");

        Assert.All(new[] { hoi4, eu4, hoi4Dependency, eu4Dependency }, text => Assert.Contains("interface/a.gui", text));
        Assert.Contains("interface/00_mod_overrides.gui", eu4);
        Assert.Contains("Dep", hoi4Dependency);
        Assert.Contains("Dep", eu4Dependency);
        Assert.DoesNotContain("Dep", hoi4);

        // Die vier Fälle (HOI IV früher / EU IV später geladen, Vanilla / Abhängigkeits-Mod) haben eigene Texte.
        var templates = new[] { hoi4, eu4.Replace("00_", "zz_"), hoi4Dependency, eu4Dependency.Replace("00_", "zz_") };
        Assert.Equal(4, templates.Distinct().Count());
        var confirm = GuiTexts.ClassicMoveConfirm(new[] { ("topbar", "interface/a.gui", "interface/zz_mod_overrides.gui", (string?)null) }, firstWins: false);
        Assert.StartsWith(hoi4, confirm);
    }

    [Fact]
    public void Eu4_changed_subfolder_window_goes_to_root_00_file()
    {
        var source = Source("interface/sub/x.gui");

        var plan = GuiOverridePlanner.Plan(Setup("eu4", MoveTopbar, source));

        Assert.Empty(plan.Blockers);
        var entry = Assert.Single(plan.Entries);
        Assert.Equal(GuiPlanEntryKind.Override, entry.Kind);
        Assert.Equal(ClassicTargetRule.RootFirstWins, entry.Rule);
        var file = Assert.Single(plan.Files);
        Assert.Equal(@"C:\test\Mod\interface\00_mod_overrides.gui", file.FullPath);
        Assert.Equal(Wrap(MoveTopbar(Topbar)), file.NewText);
    }

    [Fact]
    public void Constants_come_along()
    {
        var vanilla = "@warn_x = 280\n" + VanillaText.Replace("x = 0 y = 0", "x = @warn_x y = 0");
        static string Edit(string text) => text.Replace("x = @warn_x y = 0", "x = @warn_x y = 7");

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", Edit, vanillaText: vanilla));

        Assert.Equal(new[] { GuiPlanEntryKind.CopyConstant, GuiPlanEntryKind.Override }, plan.Entries.Select(e => e.Kind));
        Assert.Equal("@warn_x = 280\n" + Wrap(Topbar.Replace("x = 0 y = 0", "x = @warn_x y = 7")), Assert.Single(plan.Files).NewText);

        var existing = Mod("interface/zz_mod_overrides.gui", "@warn_x = 99\nguiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"mine\"\n\t}\n}\n");
        var renamed = GuiOverridePlanner.Plan(Setup("hoi4", Edit, vanillaText: vanilla, extra: existing));

        Assert.Empty(renamed.Blockers);
        Assert.Equal(new[] { GuiPlanEntryKind.RenameConstant, GuiPlanEntryKind.Override }, renamed.Entries.Select(e => e.Kind));
        Assert.Equal("warn_x_topbar", renamed.Entries[0].Detail);
        var file = Assert.Single(renamed.Files);
        Assert.False(file.IsNew);
        Assert.Equal(
            "@warn_x = 99\n@warn_x_topbar = 280\nguiTypes = {\n\tcontainerWindowType = {\n\t\tname = \"mine\"\n\t}\n"
            + Topbar.Replace("x = 0 y = 0", "x = @warn_x_topbar y = 7") + "}\n",
            file.NewText);
    }

    [Fact]
    public void Override_goes_into_existing_guiTypes_of_any_case_or_a_new_one()
    {
        var lower = Mod("interface/zz_mod_overrides.gui", "guitypes = {\n}\n");
        var none = Mod("interface/zz_mod_overrides.gui", "# nur ein Kommentar\n");

        var intoLower = Assert.Single(GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, extra: lower)).Files);
        var intoNew = Assert.Single(GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, extra: none)).Files);

        Assert.Equal("guitypes = {\n" + MoveTopbar(Topbar) + "}\n", intoLower.NewText);
        Assert.Equal("# nur ein Kommentar\n" + Wrap(MoveTopbar(Topbar)), intoNew.NewText);
    }

    [Fact]
    public void New_window_goes_to_override_file()
    {
        var fresh = "\tcontainerWindowType = {\n\t\tname = \"fresh\"\n\t}\n";

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace(Other, Other + fresh)));

        Assert.Empty(plan.Blockers);
        var entry = Assert.Single(plan.Entries);
        Assert.Equal(GuiPlanEntryKind.NewDefinition, entry.Kind);
        Assert.Equal("fresh", entry.Name);
        Assert.Equal(ClassicTargetRule.RootLastWins, entry.Rule);
        Assert.Equal(Wrap(fresh), Assert.Single(plan.Files).NewText);
    }

    [Fact]
    public void New_window_with_taken_name_blocks()
    {
        var fresh = "\tcontainerWindowType = {\n\t\tname = \"fresh\"\n\t}\n";
        var taken = (Source("interface/other.gui"), Wrap(fresh));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace(Other, Other + fresh), extra: taken));

        var blocker = Assert.Single(plan.Blockers);
        Assert.Equal(GuiPlanBlockerKind.NameTaken, blocker.Kind);
        Assert.Equal(new[] { "interface/other.gui:2" }, blocker.Places);
        Assert.False(plan.CanApply);
    }

    [Fact]
    public void Nested_duplicate_name_does_not_block()
    {
        var vanilla = Wrap("\tcontainerWindowType = {\n\t\tname = \"a\"\n\t\ticonType = {\n\t\t\tname = \"close\"\n\t\t}\n\t\tposition = { x = 0 y = 0 }\n\t}\n");
        var mod = Mod(
            "interface/zz_x.gui",
            Wrap("\tcontainerWindowType = {\n\t\tname = \"close\"\n\t}\n\tcontainerWindowType = {\n\t\tname = \"b\"\n\t\ticonType = {\n\t\t\tname = \"a\"\n\t\t}\n\t}\n"));
        var other = Mod("interface/zz_y.gui", Wrap("\tcontainerWindowType = {\n\t\tname = \"c\"\n\t\ticonType = {\n\t\t\tname = \"a\"\n\t\t}\n\t}\n"));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, vanillaText: vanilla, extra: new[] { mod, other }));

        Assert.Empty(plan.Blockers);
        Assert.Equal(GuiPlanEntryKind.Override, Assert.Single(plan.Entries).Kind);
    }

    [Fact]
    public void Deleted_window_copies_file()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace(Other, string.Empty)));

        Assert.Empty(plan.Blockers);
        Assert.Equal(GuiCopyReason.DefinitionDeleted, plan.CopyReason);
        Assert.Equal(GuiPlanEntryKind.CopyFile, plan.Entries[0].Kind);
        var file = Assert.Single(plan.Files);
        Assert.Equal(@"C:\test\Mod\interface\topbar.gui", file.FullPath);
        Assert.Equal(Wrap(Topbar), file.NewText);
    }

    [Fact]
    public void Changed_text_outside_windows_copies_file()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => "@new = 1\n" + text));

        Assert.Equal(GuiCopyReason.TopLevelChanged, plan.CopyReason);
        Assert.Equal(@"C:\test\Mod\interface\topbar.gui", Assert.Single(plan.Files).FullPath);
    }

    [Fact]
    public void Change_in_a_second_window_of_the_same_name_copies_file()
    {
        // Die Einheit ist das erste Fenster eines Namens; eine Änderung am zweiten ginge sonst verloren.
        var vanilla = VanillaText.Replace(Other, Other + Other.Replace("x = 5", "x = 8"));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace("x = 8", "x = 9"), vanillaText: vanilla));

        Assert.Equal(GuiCopyReason.TopLevelChanged, plan.CopyReason);
    }

    [Fact]
    public void Copy_from_dlc_root_is_blocked()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace(Other, string.Empty), Source(label: "integrated_dlc_a")));

        var blocker = Assert.Single(plan.Blockers);
        Assert.Equal(GuiPlanBlockerKind.CopyNotPossible, blocker.Kind);
        Assert.Equal(new[] { "integrated_dlc_a" }, blocker.Places);
        Assert.Empty(plan.Files);
    }

    [Fact]
    public void Forced_copy_has_reason_requested()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text) with { ForceCopyFile = true });

        Assert.Empty(plan.Blockers);
        Assert.Equal(GuiCopyReason.Requested, plan.CopyReason);
        Assert.Equal(GuiPlanEntryKind.CopyFile, plan.Entries[0].Kind);
        Assert.Equal(VanillaText, Assert.Single(plan.Files).NewText);
    }

    [Fact]
    public void Configured_name_that_loses_blocks()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, configured: "aaa_overrides.gui"));

        Assert.Equal(ClassicTargetRule.Configured, Assert.Single(plan.Entries).Rule);
        var blocker = Assert.Single(plan.Blockers);
        Assert.Equal(GuiPlanBlockerKind.TargetLoses, blocker.Kind);
        Assert.Equal(GuiTargetProblem.Loses, blocker.TargetProblem);
        Assert.True(blocker.ConfiguredTarget);
        Assert.Equal("topbar", blocker.Name);
        Assert.Equal(new[] { "interface/aaa_overrides.gui:2", "interface/topbar.gui:2" }, blocker.Places);
        Assert.False(plan.CanApply);
    }

    [Fact]
    public void Configured_name_equal_to_vanilla_path_blocks()
    {
        // Die Datei hätte den Pfad von interface/topbar.gui und ersetzte die ganze Vanilla-Datei.
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, configured: "topbar.gui"));

        var blocker = Assert.Single(plan.Blockers);
        Assert.Equal(GuiPlanBlockerKind.TargetLoses, blocker.Kind);
        Assert.Equal(GuiTargetProblem.ReplacesFile, blocker.TargetProblem);
        Assert.Equal(new[] { "interface/topbar.gui", "interface/topbar.gui" }, blocker.Places);
    }

    [Fact]
    public void Configured_name_with_a_folder_blocks()
    {
        // EU IV darf nie in einen Unterordner von interface/ schreiben; ein gesetzter Name ist nur ein Dateiname.
        foreach (var name in new[] { "sub/x.gui", @"sub\x.gui", @"..\x.gui" })
        {
            var plan = GuiOverridePlanner.Plan(Setup("eu4", MoveTopbar, configured: name));

            var blocker = Assert.Single(plan.Blockers);
            Assert.Equal(GuiPlanBlockerKind.TargetLoses, blocker.Kind);
            Assert.Equal(GuiTargetProblem.InvalidName, blocker.TargetProblem);
            Assert.Equal(new[] { name }, blocker.Places);
            Assert.Empty(plan.Files);
        }
    }

    [Fact]
    public void Default_name_that_loses_blocks_without_blaming_the_settings()
    {
        // Ein Unterordner lädt nach interface/ (gemessen 2026-10-17): Dort gewinnt Vanillas zweite Fassung, kein Name in interface/ kommt dagegen an.
        var later = (Source("interface/sub/later.gui"), Wrap(Topbar));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, extra: later));

        var blocker = Assert.Single(plan.Blockers);
        Assert.Equal(GuiTargetProblem.Loses, blocker.TargetProblem);
        Assert.False(blocker.ConfiguredTarget);
        Assert.Equal(new[] { "interface/zz_mod_overrides.gui:2", "interface/sub/later.gui:2" }, blocker.Places);
    }

    [Fact]
    public void Stale_index_plans_no_empty_file()
    {
        // Der Index kennt zz_mod_topbar.gui mit "topbar", auf der Platte fehlt die Datei: kein leerer Plan-Eintrag, das Fenster kommt in die Zieldatei.
        var mod = Mod("interface/zz_mod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50")));
        var request = Setup("hoi4", text => text.Replace("x = 50", "x = 60"), extra: mod) with { ReadFile = _ => null };

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Empty(plan.Blockers);
        var file = Assert.Single(plan.Files);
        Assert.Equal(OverridesPath, file.FullPath);
        Assert.Equal(Wrap(Topbar.Replace("x = 0", "x = 60")), file.NewText);
        Assert.Equal(GuiPlanEntryKind.Override, Assert.Single(plan.Entries).Kind);
    }

    [Fact]
    public void Copy_with_changed_window_and_deleted_other_has_no_blocker()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => MoveTopbar(text).Replace(Other, string.Empty)));

        Assert.Empty(plan.Blockers);
        Assert.Empty(plan.Warnings);
        Assert.Equal(GuiCopyReason.DefinitionDeleted, plan.CopyReason);
        Assert.Equal(Wrap(MoveTopbar(Topbar)), Assert.Single(plan.Files).NewText);
    }

    [Fact]
    public void Copy_that_moves_a_winning_mod_window_is_checked()
    {
        // topbar der Mod in zz_mod_overrides.gui gewinnt gegen zz_dep.gui; die Kopie nimmt es nach interface/topbar.gui mit, das vor zz_dep.gui lädt.
        var mod = Mod("interface/zz_mod_overrides.gui", Wrap(Topbar.Replace("x = 0", "x = 50")));
        var dependency = (new GuiSourceFile(@"C:\test\Dep\interface\zz_dep.gui", "interface/zz_dep.gui", GuiLayer.Dependency, "Dep", 0, 0), Wrap(Topbar.Replace("x = 0", "x = 70")));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text.Replace(Other, string.Empty), extra: new[] { mod, dependency }));

        Assert.Empty(plan.Blockers);
        Assert.Contains(plan.Entries, e => e.Kind == GuiPlanEntryKind.RemoveFromMod && e.Name == "topbar");
        var warning = Assert.Single(plan.Warnings);
        Assert.Equal(GuiPlanWarningKind.DependencyWins, warning.Kind);
        Assert.Equal("topbar", warning.Name);
        Assert.Equal(new[] { "interface/zz_dep.gui:2" }, warning.Places);
    }

    [Fact]
    public void Configured_name_that_wins_is_written()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, configured: "zz_mine.gui"));

        Assert.Empty(plan.Blockers);
        Assert.Equal(@"C:\test\Mod\interface\zz_mine.gui", Assert.Single(plan.Files).FullPath);
    }

    [Fact]
    public void Dependency_that_wins_warns()
    {
        var dependency = (new GuiSourceFile(@"C:\test\Dep\interface\zzzz_dep.gui", "interface/zzzz_dep.gui", GuiLayer.Dependency, "Dep", 0, 0), Wrap(Topbar));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, extra: dependency));

        Assert.Empty(plan.Blockers);
        var warning = Assert.Single(plan.Warnings);
        Assert.Equal(GuiPlanWarningKind.DependencyWins, warning.Kind);
        Assert.Equal("topbar", warning.Name);
        Assert.Equal(new[] { "interface/zzzz_dep.gui:2" }, warning.Places);
        Assert.True(plan.CanApply);
    }

    [Fact]
    public void Dlc_source_warns_uncertain_order()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, Source(label: "integrated_dlc_a")));

        Assert.Empty(plan.Blockers);
        var warning = Assert.Single(plan.Warnings);
        Assert.Equal(GuiPlanWarningKind.UncertainOrder, warning.Kind);
        Assert.Equal("topbar", warning.Name);
    }

    [Fact]
    public void Dlc_file_without_the_window_gives_no_warning()
    {
        // Eine DLC-Datei im selben Ordner, die topbar nicht definiert, kann nicht ändern, wer gewinnt.
        var dlc = (ClassicOverrideTargetsTests.V("interface/dlc_window.gui", "integrated_dlc_a"), Wrap(Other.Replace("\"other\"", "\"dlc_only\"")));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, extra: dlc));

        Assert.Empty(plan.Blockers);
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Dlc_file_defining_the_window_warns_uncertain_order()
    {
        var dlc = (ClassicOverrideTargetsTests.V("interface/dlc_window.gui", "integrated_dlc_a"), Wrap(Topbar));

        var plan = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, extra: dlc));

        Assert.Empty(plan.Blockers);
        Assert.Contains(plan.Warnings, w => w.Kind == GuiPlanWarningKind.UncertainOrder && w.Name == "topbar");
    }

    [Fact]
    public void New_file_keeps_windows1252_for_non_ascii()
    {
        var vanilla = VanillaText.Replace("name = \"topbar\"", "name = \"topbar\"\n\t\ttext = \"café\"");

        var latin = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, vanillaText: vanilla) with { SourceEncoding = GuiTextReader.Windows1252 });
        var ascii = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar) with { SourceEncoding = GuiTextReader.Windows1252 });
        var utf8 = GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, vanillaText: vanilla) with { SourceEncoding = GuiTextReader.Utf8 });

        Assert.Equal("windows-1252", Assert.Single(latin.Files).EncodingName);
        Assert.Null(Assert.Single(ascii.Files).EncodingName);
        Assert.Null(Assert.Single(utf8.Files).EncodingName);
    }

    [Fact]
    public void Windows1252_only_when_every_character_fits()
    {
        GuiOverridePlan PlanWith(string text) =>
            GuiOverridePlanner.Plan(Setup("hoi4", MoveTopbar, vanillaText: VanillaText.Replace("name = \"topbar\"", "name = \"topbar\"\n\t\ttext = \"" + text + "\"")) with
            {
                SourceEncoding = GuiTextReader.Windows1252,
            });

        Assert.Equal("windows-1252", Assert.Single(PlanWith("5 €").Files).EncodingName);
        Assert.Null(Assert.Single(PlanWith("café ✓").Files).EncodingName);
    }

    [Fact]
    public void Unchanged_draft_gives_empty_plan()
    {
        var mod = Mod("interface/zz_mod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50")));
        var request = Setup("hoi4", text => text, extra: mod);

        foreach (var plan in new[] { GuiOverridePlanner.Plan(request), GuiOverridePlanner.Plan(request with { BaselineText = null }) })
        {
            Assert.Empty(plan.Entries);
            Assert.Empty(plan.Files);
            Assert.Empty(plan.Blockers);
            Assert.Empty(plan.Warnings);
            Assert.False(plan.CanApply);
        }
    }

    [Fact]
    public void Restored_draft_keeps_the_mod_window()
    {
        // Ohne gemerkte Grundlage steht "topbar" noch in der Vanilla-Fassung im Entwurf; die Mod-Fassung darf nicht auf Vanilla zurückgesetzt werden.
        var mod = Mod("interface/zz_mod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50")));
        var request = Setup("hoi4", text => text, extra: mod) with { DraftText = VanillaText.Replace("x = 5 y = 5", "x = 6 y = 5"), BaselineText = null };

        var plan = GuiOverridePlanner.Plan(request);

        var entry = Assert.Single(plan.Entries);
        Assert.Equal("other", entry.Name);
        Assert.Equal(GuiPlanEntryKind.Override, entry.Kind);
    }

    [Fact]
    public void Mod_change_since_the_draft_started_blocks()
    {
        var mod = Mod("interface/zz_mod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 50")));
        var later = Mod("interface/zz_mod_topbar.gui", Wrap(Topbar.Replace("x = 0", "x = 51")));
        var start = Setup("hoi4", text => text, extra: mod).DraftText;

        var request = Setup("hoi4", text => text, extra: later) with { BaselineText = start, DraftText = start.Replace("x = 50", "x = 60") };
        var plan = GuiOverridePlanner.Plan(request);

        var blocker = Assert.Single(plan.Blockers);
        Assert.Equal(GuiPlanBlockerKind.ModChangedSince, blocker.Kind);
        Assert.Equal(new[] { "interface/zz_mod_topbar.gui:2" }, blocker.Places);
    }

    [Fact]
    public void Draft_with_syntax_errors_blocks()
    {
        var plan = GuiOverridePlanner.Plan(Setup("hoi4", text => text + "guiTypes = {\n"));

        Assert.Equal(GuiPlanBlockerKind.DraftErrors, Assert.Single(plan.Blockers).Kind);
    }

    [Fact]
    public void Texts_name_windows_rules_and_new_kinds()
    {
        Assert.NotEqual(GuiTexts.TakeOverKind(GuiPlanEntryKind.Override, null), GuiTexts.TakeOverKind(GuiPlanEntryKind.Override, null, GuiDefinitionKind.NamedWidget));
        Assert.Equal(GuiTexts.TakeOverKind(GuiPlanEntryKind.Override, null), GuiTexts.TakeOverKind(GuiPlanEntryKind.Override, null, GuiDefinitionKind.Type));
        Assert.Equal(GuiTexts.TakeOverKind(GuiPlanEntryKind.CopyConstant, null), GuiTexts.TakeOverKind(GuiPlanEntryKind.CopyConstant, null, GuiDefinitionKind.NamedWidget));

        Assert.Equal(GuiTexts.TakeOverReason(GuiCopyReason.Requested), GuiTexts.TakeOverReason(GuiCopyReason.Requested, true));
        Assert.NotEqual(GuiTexts.TakeOverReason(GuiCopyReason.TopLevelChanged), GuiTexts.TakeOverReason(GuiCopyReason.TopLevelChanged, true));
        Assert.NotEqual(GuiTexts.TakeOverReason(GuiCopyReason.DefinitionDeleted), GuiTexts.TakeOverReason(GuiCopyReason.DefinitionDeleted, true));
        Assert.Equal(GuiTexts.TakeOverReason(GuiCopyReason.DefinitionDeleted), GuiTexts.TakeOverReason(GuiCopyReason.DefinitionDeleted, false));

        var loses = GuiTexts.TakeOverBlocker(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, "topbar", new[] { "interface/aaa.gui:2", "interface/topbar.gui:2" }));
        var replaces = GuiTexts.TakeOverBlocker(
            new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, "topbar", new[] { "interface/topbar.gui", "interface/topbar.gui" }) { TargetProblem = GuiTargetProblem.ReplacesFile });
        Assert.Contains("interface/aaa.gui:2", loses);
        Assert.Contains("interface/topbar.gui:2", loses);
        Assert.Contains("interface/topbar.gui", replaces);
        Assert.NotEqual(loses, replaces);
        var copy = new GuiPlanBlocker(GuiPlanBlockerKind.CopyNotPossible, "interface/topbar.gui", new[] { "integrated_dlc_a" });
        Assert.NotEqual(GuiTexts.TakeOverBlocker(copy), GuiTexts.TakeOverBlocker(copy, classic: true));
        Assert.Equal(loses, GuiTexts.TakeOverBlocker(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, "topbar", new[] { "interface/aaa.gui:2", "interface/topbar.gui:2" }), classic: true));

        Assert.Contains("interface/zzzz_dep.gui:2", GuiTexts.TakeOverWarning(new GuiPlanWarning(GuiPlanWarningKind.DependencyWins, "topbar", new[] { "interface/zzzz_dep.gui:2" })));
        Assert.Contains("interface/zz_mod_overrides.gui", GuiTexts.TakeOverWarning(new GuiPlanWarning(GuiPlanWarningKind.UncertainOrder, "topbar", new[] { "interface/zz_mod_overrides.gui" })));
        Assert.Equal(5, Enum.GetValues<ClassicTargetRule>().Select(GuiTexts.TakeOverRule).Distinct().Count());

        var places = new[] { "interface/aaa.gui:2", "interface/topbar.gui:2" };
        var configured = GuiTexts.TakeOverBlocker(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, "topbar", places) { TargetProblem = GuiTargetProblem.Loses, ConfiguredTarget = true });
        var standard = GuiTexts.TakeOverBlocker(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, "topbar", places) { TargetProblem = GuiTargetProblem.Loses });
        var notFound = GuiTexts.TakeOverBlocker(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, "topbar", new[] { "interface/x.gui" }) { TargetProblem = GuiTargetProblem.NotFound });
        var invalid = GuiTexts.TakeOverBlocker(new GuiPlanBlocker(GuiPlanBlockerKind.TargetLoses, "topbar", new[] { "sub/x.gui" }) { TargetProblem = GuiTargetProblem.InvalidName });
        Assert.Equal(5, new[] { configured, standard, notFound, invalid, replaces }.Distinct().Count());
        Assert.Contains("interface/x.gui", notFound);
        Assert.Contains("sub/x.gui", invalid);
    }

    [Fact]
    public void New_type_copy_is_not_classic()
    {
        var request = Setup("hoi4", text => text);
        var document = request.Index.DocumentOf(request.Source);
        var copy = new GuiNewTypeCopyRequest(new GuiCopySource(request.Source, document, ClassicWindows.Of(document)[0]), "x", request.Index, request.Targets, request.ReadFile);

        Assert.Throws<InvalidOperationException>(() => GuiOverridePlanner.PlanNewTypeCopy(copy));
    }
}
