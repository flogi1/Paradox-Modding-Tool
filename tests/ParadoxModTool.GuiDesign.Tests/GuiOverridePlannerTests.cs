using ParadoxModTool.GuiDesign.Designer;
using ParadoxModTool.GuiDesign.Edit;
using ParadoxModTool.GuiDesign.Index;
using ParadoxModTool.GuiDesign.Syntax;
using Xunit;

namespace ParadoxModTool.GuiDesign.Tests;

public class GuiOverridePlannerTests
{
    internal const string Vanilla =
        "@w = 10\n@h = @w\ntypes Buttons {\n\ttype button_a = button {\n\t\tsize = { @w @h }\n\t}\n\ttype button_b = button {\n\t\tsize = { 20 20 }\n\t}\n}\n" +
        "template tpl_a {\n\talpha = 1\n}\nwindow = {\n\tname = \"w\"\n\tbutton_a = { }\n\tbutton_b = { }\n\tusing = tpl_a\n}\n";

    internal const string ModOverrides = "types MyModOverrides {\n    type button_b = button {\n        size = { 25 25 }\n    }\n}\n";

    internal const string OverridesPath = @"C:\test\Mod\in_game\gui\mymod_overrides.gui";

    internal const string TypesPath = @"C:\test\Mod\in_game\gui\mymod_types.gui";

    internal static readonly GuiModTargets Targets = GuiModTargets.For(@"C:\test\Mod", "mymod_overrides.gui", "mymod_types.gui") with { ContainerBase = "MyMod" };

    /// <summary>Vanilla-Datei aus dem Ordner <paramref name="root"/> des Spiels (game, jomini, clausewitz).</summary>
    internal static GuiSourceFile V(string relativePath, string root = "game") =>
        new($@"C:\test\Vanilla\{relativePath.Replace('/', '\\')}", relativePath, GuiLayer.Vanilla, root, 0, 0);

    internal static (GuiOverrideRequest Request, GuiIndex Index) Setup(string draft, string vanillaRoot = "game", params (string Relative, string Text)[] mod)
    {
        var source = V("in_game/gui/shared/buttons.gui", vanillaRoot);
        var files = new List<(GuiSourceFile, string)> { (source, Vanilla) };
        files.AddRange(mod.Select(m => (GuiIndexTests.F(m.Relative, GuiLayer.Mod), m.Text)));
        var index = GuiIndexTests.Build(files.ToArray());
        var texts = files.Where(f => f.Item1.Layer == GuiLayer.Mod).ToDictionary(f => f.Item1.FullPath, f => f.Item2, StringComparer.OrdinalIgnoreCase);
        return (new GuiOverrideRequest(source, Vanilla, draft, index, Targets, path => texts.TryGetValue(path, out var text) ? text : null), index);
    }

    private static string Draft(GuiIndex index, string? from = null, string? to = null)
    {
        var text = GuiDraft.Start(index.Files[0], Vanilla, index).Text;
        return from is null ? text : text.Replace(from, to!);
    }

    [Fact]
    public void UnchangedDraftGivesAnEmptyPlan()
    {
        var (_, index) = Setup(string.Empty, "game", ("in_game/gui/mymod_overrides.gui", ModOverrides));
        var (request, _) = Setup(Draft(index), "game", ("in_game/gui/mymod_overrides.gui", ModOverrides));

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Empty(plan.Entries);
        Assert.Empty(plan.Files);
        Assert.Empty(plan.Blockers);
        Assert.False(plan.CanApply);
    }

    [Fact]
    public void ChangedTypeGoesIntoANewOverrideFileWithItsConstants()
    {
        var (request, _) = Setup(Vanilla.Replace("size = { @w @h }", "size = { @w 5 }"));

        var plan = GuiOverridePlanner.Plan(request);

        var file = Assert.Single(plan.Files);
        Assert.True(file.IsNew);
        Assert.Equal(OverridesPath, file.FullPath);
        Assert.Equal("@w = 10\ntypes MyModOverrides {\n\ttype button_a = button {\n\t\tsize = { @w 5 }\n\t}\n}\n", file.NewText);
        Assert.Equal(new[] { GuiPlanEntryKind.CopyConstant, GuiPlanEntryKind.Override }, plan.Entries.Select(e => e.Kind));
        var entry = plan.Entries[1];
        Assert.Equal("button_a", entry.Name);
        Assert.Equal(1, entry.Usages);
        Assert.Equal(3, entry.Line);
        Assert.True(plan.CanApply);
        Assert.Null(plan.CopyReason);
    }

    [Fact]
    public void ChangedTypeReplacesTheVersionInTheMod()
    {
        var mod = ("in_game/gui/mymod_overrides.gui", ModOverrides);
        var (_, index) = Setup(string.Empty, "game", mod);
        var (request, _) = Setup(Draft(index, "size = { 25 25 }", "size = { 30 30 }"), "game", mod);

        var plan = GuiOverridePlanner.Plan(request);

        var file = Assert.Single(plan.Files);
        Assert.False(file.IsNew);
        Assert.Equal("types MyModOverrides {\n    type button_b = button {\n        size = { 30 30 }\n    }\n}\n", file.NewText);
        var entry = Assert.Single(plan.Entries);
        Assert.Equal(GuiPlanEntryKind.Replace, entry.Kind);
        Assert.Equal(2, entry.Line);
    }

    [Fact]
    public void ModChangesSinceTheDraftStartedAreKeptOrBlock()
    {
        var mod = ("in_game/gui/mymod_overrides.gui", ModOverrides);
        var (_, index) = Setup(string.Empty, "game", mod);
        var start = Draft(index);
        var later = ("in_game/gui/mymod_overrides.gui", ModOverrides.Replace("25 25", "26 26"));

        var (unchanged, _) = Setup(start, "game", later);
        Assert.Empty(GuiOverridePlanner.Plan(unchanged with { BaselineText = start }).Entries);

        var (same, _) = Setup(start.Replace("25 25", "30 30"), "game", later);
        var blocked = GuiOverridePlanner.Plan(same with { BaselineText = start });
        Assert.Equal(GuiPlanBlockerKind.ModChangedSince, Assert.Single(blocked.Blockers).Kind);

        var (other, _) = Setup(start.Replace("alpha = 1", "alpha = 0.5"), "game", later);
        var plan = GuiOverridePlanner.Plan(other with { BaselineText = start });
        Assert.Empty(plan.Blockers);
        Assert.Equal("tpl_a", Assert.Single(plan.Entries).Name);
    }

    [Fact]
    public void TemplatesStayOnTopLevel()
    {
        var (request, _) = Setup(Vanilla.Replace("alpha = 1", "alpha = 0.5"));

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Equal("template tpl_a {\n\talpha = 0.5\n}\n", Assert.Single(plan.Files).NewText);
        Assert.Equal(GuiDefinitionKind.Template, Assert.Single(plan.Entries).DefinitionKind);
    }

    [Fact]
    public void NewTypesGoIntoTheTypesFileAndNeedAFreeName()
    {
        var added = Vanilla.Replace("\t\tsize = { 20 20 }\n\t}\n", "\t\tsize = { 20 20 }\n\t}\n\ttype button_c = button { }\n");
        var (request, _) = Setup(added);

        var plan = GuiOverridePlanner.Plan(request);

        var file = Assert.Single(plan.Files);
        Assert.Equal(TypesPath, file.FullPath);
        Assert.Equal("types MyModTypes {\n\ttype button_c = button { }\n}\n", file.NewText);
        Assert.Equal(GuiPlanEntryKind.NewDefinition, Assert.Single(plan.Entries).Kind);

        var (taken, _) = Setup(added, "game", ("in_game/gui/other.gui", "types O { type button_c = button { } }"));
        var blocker = Assert.Single(GuiOverridePlanner.Plan(taken).Blockers);
        Assert.Equal(GuiPlanBlockerKind.NameTaken, blocker.Kind);
        Assert.Equal("in_game/gui/other.gui:1", Assert.Single(blocker.Places));
    }

    [Fact]
    public void RenamesConstantsThatExistWithAnotherValue()
    {
        var mod = ("in_game/gui/mymod_overrides.gui", "@w = 12\n");
        var (request, _) = Setup(Vanilla.Replace("size = { @w @h }", "size = { @w @h }\n\t\talpha = @[w * 2]"), "game", mod);

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Equal(
            "@w = 12\n@w_buttons = 10\n@h = @w_buttons\ntypes MyModOverrides {\n\ttype button_a = button {\n\t\tsize = { @w_buttons @h }\n\t\talpha = @[w_buttons * 2]\n\t}\n}\n",
            Assert.Single(plan.Files).NewText);
        Assert.Equal(
            new[] { GuiPlanEntryKind.RenameConstant, GuiPlanEntryKind.CopyConstant, GuiPlanEntryKind.Override },
            plan.Entries.Select(e => e.Kind));
        Assert.Equal("w_buttons", plan.Entries[0].Detail);
    }

    [Fact]
    public void TopLevelChangeCopiesTheWholeFileAndTakesOverModDefinitions()
    {
        var mod = ("in_game/gui/mymod_overrides.gui", ModOverrides);
        var (_, index) = Setup(string.Empty, "game", mod);
        var draft = Draft(index, "name = \"w\"", "name = \"w2\"");
        var (request, _) = Setup(draft, "game", mod);

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Equal(GuiCopyReason.TopLevelChanged, plan.CopyReason);
        Assert.Equal(2, plan.Files.Count);
        var copy = plan.Files.Single(f => f.FullPath == @"C:\test\Mod\in_game\gui\shared\buttons.gui");
        Assert.True(copy.IsNew);
        Assert.Equal(draft, copy.NewText);
        Assert.Equal("types MyModOverrides {\n}\n", plan.Files.Single(f => f.FullPath == OverridesPath).NewText);
        Assert.Equal(
            new[] { GuiPlanEntryKind.CopyFile, GuiPlanEntryKind.CopiedDefinition, GuiPlanEntryKind.CopiedDefinition, GuiPlanEntryKind.RemoveFromMod, GuiPlanEntryKind.CopiedDefinition },
            plan.Entries.Select(e => e.Kind));
        Assert.Equal(new[] { "in_game/gui/shared/buttons.gui", "button_a", "button_b", "button_b", "tpl_a" }, plan.Entries.Select(e => e.Name));
    }

    [Fact]
    public void DeletingADefinitionCopiesTheFile()
    {
        var (request, _) = Setup(Vanilla.Replace("\ttype button_b = button {\n\t\tsize = { 20 20 }\n\t}\n", string.Empty));

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Equal(GuiCopyReason.DefinitionDeleted, plan.CopyReason);
        Assert.Equal(GuiPlanEntryKind.CopyFile, plan.Entries[0].Kind);
    }

    [Fact]
    public void OnlyFilesFromTheGameFolderCanBeCopied()
    {
        var (request, _) = Setup(Vanilla.Replace("name = \"w\"", "name = \"w2\""), "jomini");

        var plan = GuiOverridePlanner.Plan(request);

        var blocker = Assert.Single(plan.Blockers);
        Assert.Equal(GuiPlanBlockerKind.CopyNotPossible, blocker.Kind);
        Assert.False(plan.CanApply);
    }

    [Fact]
    public void EveryVanillaFilePlansNothingUnchangedAndOneOverridePerChangedType()
    {
        if (!VanillaGui.Exists)
        {
            return;
        }

        var index = GuiRulesVanillaTests.Vanilla;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var changed = 0;
        foreach (var file in index.Files.Where(f => f.Label == "game"))
        {
            var text = File.ReadAllText(file.FullPath);
            var request = new GuiOverrideRequest(file, text, text, index, Targets, _ => null);
            var unchanged = GuiOverridePlanner.Plan(request);
            Assert.True(unchanged.Entries.Count == 0 && unchanged.Blockers.Count == 0, file.RelativePath);

            var document = GuiParser.Parse(text);
            if (GuiDraft.Definitions(document).FirstOrDefault(n => n.Kind == GuiNodeKind.TypeDefinition && n.HasBody) is not { } type)
            {
                continue;
            }

            var draft = TextEdit.Apply(text, new GuiTextEdits(document).SetProperty(type, "alpha", "0.5"));
            var plan = GuiOverridePlanner.Plan(request with { DraftText = draft });
            Assert.True(plan.CanApply, file.RelativePath);
            Assert.True(plan.Entries.Count(e => e.Kind == GuiPlanEntryKind.Override && e.Name == type.Name) == 1, file.RelativePath + " " + type.Name + ": " + string.Join(",", plan.Entries.Select(e => e.Kind + " " + e.Name + " " + e.Detail)));
            var written = Assert.Single(plan.Files);
            Assert.Empty(GuiParser.Parse(written.NewText).Diagnostics);
            changed++;
        }

        Assert.True(changed > 50, $"nur {changed} Dateien mit Typen");
        Console.WriteLine($"{changed} Dateien in {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void DuplicateModDefinitionsAndSyntaxErrorsBlock()
    {
        var (duplicate, _) = Setup(
            Vanilla.Replace("size = { 20 20 }", "size = { 21 21 }"),
            "game",
            ("in_game/gui/a.gui", "types A { type button_b = button { } }"),
            ("in_game/gui/b.gui", "types B { type button_b = button { } }"));
        var blocker = Assert.Single(GuiOverridePlanner.Plan(duplicate).Blockers);
        Assert.Equal(GuiPlanBlockerKind.DuplicateInMod, blocker.Kind);
        Assert.Equal(2, blocker.Places.Count);

        var (broken, _) = Setup(Vanilla.Replace("alpha = 1\n}", "alpha = 1\n"));
        Assert.Equal(GuiPlanBlockerKind.DraftErrors, Assert.Single(GuiOverridePlanner.Plan(broken).Blockers).Kind);
    }

    [Fact]
    public void ModVersionKeepsTheConstantsOfItsModFile()
    {
        // Konstanten gelten nur in ihrer Datei: @w der Mod-Fassung ist das @w der Mod-Datei (50), nicht das der Vanilla-Datei (10).
        var mod = ("in_game/gui/mymod_overrides.gui", "@w = 50\ntypes MyModOverrides {\n    type button_a = button {\n        size = { @w @w }\n    }\n}\n");
        var (_, index) = Setup(string.Empty, "game", mod);
        var (request, _) = Setup(Draft(index, "size = { @w @w }", "size = { @w @w }\n\t\talpha = 0.5"), "game", mod);

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Equal(
            "@w = 50\ntypes MyModOverrides {\n    type button_a = button {\n        size = { @w @w }\n        alpha = 0.5\n    }\n}\n",
            Assert.Single(plan.Files).NewText);
        Assert.Equal(GuiPlanEntryKind.Replace, Assert.Single(plan.Entries).Kind);
    }

    [Fact]
    public void FileCopyTakesTheConstantsOfModVersions()
    {
        var mod = ("in_game/gui/mymod_overrides.gui", "@w = 50\n@mw = 30\ntypes MyModOverrides {\n    type button_b = button {\n        size = { @mw @w }\n    }\n}\n");
        var (_, index) = Setup(string.Empty, "game", mod);
        var draft = Draft(index, "name = \"w\"", "name = \"w2\"");
        var (request, _) = Setup(draft, "game", mod);

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Empty(plan.Blockers);
        var copy = plan.Files.Single(f => f.FullPath == @"C:\test\Mod\in_game\gui\shared\buttons.gui");
        Assert.Equal(
            draft.Replace("@h = @w\n", "@h = @w\n@mw = 30\n@w_buttons = 50\n").Replace("size = { @mw @w }", "size = { @mw @w_buttons }"),
            copy.NewText);
    }

    [Fact]
    public void SubmodStartsFromTheDependencyVersionAndOverridesIt()
    {
        var source = V("in_game/gui/shared/buttons.gui");
        var parent = GuiIndexTests.F("in_game/gui/parent.gui", GuiLayer.Dependency);
        var index = GuiIndexTests.Build(
            (source, Vanilla),
            (parent, "@pw = 99\ntypes Parent {\n\ttype button_a = button {\n\t\tsize = { @pw @pw }\n\t}\n}\n"));

        var start = GuiDraft.Start(source, Vanilla, index);
        Assert.Equal("button_a", Assert.Single(start.Substituted));

        var draft = start.Text.Replace("size = { @pw @pw }", "size = { @pw @pw }\n\t\talpha = 0.5");
        var plan = GuiOverridePlanner.Plan(new GuiOverrideRequest(source, Vanilla, draft, index, Targets, _ => null));

        var file = Assert.Single(plan.Files);
        Assert.Equal(OverridesPath, file.FullPath);
        Assert.Equal("@pw = 99\ntypes MyModOverrides {\n\ttype button_a = button {\n\t\tsize = { @pw @pw }\n\t\talpha = 0.5\n\t}\n}\n", file.NewText);
        Assert.Equal(new[] { GuiPlanEntryKind.CopyConstant, GuiPlanEntryKind.Override }, plan.Entries.Select(e => e.Kind));
    }

    [Fact]
    public void DraftWithoutBaselineKeepsModVersionsItNeverContained()
    {
        // Wiederhergestellter Text einer direkt bearbeiteten Vanilla-Datei: button_b steht darin in der Vanilla-Fassung, die Mod hat ihre eigene.
        var mod = ("in_game/gui/mymod_overrides.gui", ModOverrides);
        var (changed, _) = Setup(Vanilla.Replace("size = { @w @h }", "size = { @w 5 }"), "game", mod);

        var plan = GuiOverridePlanner.Plan(changed);

        Assert.DoesNotContain(plan.Entries, e => e.Kind == GuiPlanEntryKind.Replace);
        Assert.Contains("size = { 25 25 }", Assert.Single(plan.Files).NewText);

        var (topLevel, _) = Setup(Vanilla.Replace("name = \"w\"", "name = \"w2\""), "game", mod);
        var copy = GuiOverridePlanner.Plan(topLevel).Files.Single(f => f.FullPath == @"C:\test\Mod\in_game\gui\shared\buttons.gui");
        Assert.Contains("size = { 25 25 }", copy.NewText);
    }

    [Fact]
    public void DeletedDefinitionIsRemovedFromTheMod()
    {
        var mod = ("in_game/gui/mymod_overrides.gui", ModOverrides);
        var (_, index) = Setup(string.Empty, "game", mod);
        var (request, _) = Setup(Draft(index, "\ttype button_b = button {\n\t\tsize = { 25 25 }\n\t}\n", string.Empty), "game", mod);

        var plan = GuiOverridePlanner.Plan(request);

        Assert.Equal(GuiCopyReason.DefinitionDeleted, plan.CopyReason);
        Assert.Contains(plan.Entries, e => e.Kind == GuiPlanEntryKind.RemoveFromMod && e.Name == "button_b");
        Assert.Equal("types MyModOverrides {\n}\n", plan.Files.Single(f => f.FullPath == OverridesPath).NewText);
    }

    /// <summary>Fassung einer Abhängigkeits-Mod, die die ganze Vanilla-Datei ersetzt: ein Widget auf oberster Ebene ist anders.</summary>
    internal static readonly string ParentCopy = Vanilla.Replace("\tusing = tpl_a\n", "\tusing = tpl_a\n\talpha = 0.8\n");

    /// <summary>Index aus der Vanilla-Datei, einer Abhängigkeits-Kopie derselben Datei und optional Mod-Dateien.</summary>
    internal static (GuiOverrideRequest Request, GuiIndex Index) WithParentCopy(string? draft = null, params (string Relative, string Text)[] mod)
    {
        var source = V("in_game/gui/shared/buttons.gui");
        var parent = GuiIndexTests.F("in_game/gui/shared/buttons.gui", GuiLayer.Dependency);
        var files = new List<(GuiSourceFile, string)> { (source, Vanilla), (parent, ParentCopy) };
        files.AddRange(mod.Select(m => (GuiIndexTests.F(m.Relative, GuiLayer.Mod), m.Text)));
        var index = GuiIndexTests.Build(files.ToArray());
        var texts = mod.ToDictionary(m => GuiIndexTests.F(m.Relative, GuiLayer.Mod).FullPath, m => m.Text, StringComparer.OrdinalIgnoreCase);
        return (new GuiOverrideRequest(source, Vanilla, draft ?? string.Empty, index, Targets, path => texts.TryGetValue(path, out var text) ? text : null), index);
    }

    [Fact]
    public void UnchangedDraftOnADependencyBaseGivesAnEmptyPlan()
    {
        var (probe, index) = WithParentCopy();
        var start = GuiDraft.Start(probe.Source, Vanilla, index).Text;
        var (request, _) = WithParentCopy(start);

        Assert.Empty(GuiOverridePlanner.Plan(request with { BaselineText = start }).Files);

        // Wiederhergestellter Entwurf: ohne gemerkte Grundlage.
        var recovered = GuiOverridePlanner.Plan(request);
        Assert.Empty(recovered.Files);
        Assert.Empty(recovered.Blockers);
    }

    [Fact]
    public void TopLevelEditOnADependencyBaseCopiesTheDependencyVersion()
    {
        var (probe, index) = WithParentCopy();
        var start = GuiDraft.Start(probe.Source, Vanilla, index).Text;
        var draft = start.Replace("alpha = 0.8", "alpha = 0.7");
        var (request, _) = WithParentCopy(draft);

        foreach (var plan in new[] { GuiOverridePlanner.Plan(request with { BaselineText = start }), GuiOverridePlanner.Plan(request) })
        {
            Assert.Empty(plan.Blockers);
            Assert.Equal(GuiCopyReason.TopLevelChanged, plan.CopyReason);
            var file = Assert.Single(plan.Files);
            Assert.Equal(@"C:\test\Mod\in_game\gui\shared\buttons.gui", file.FullPath);
            Assert.Equal(draft, file.NewText);
        }
    }
}
