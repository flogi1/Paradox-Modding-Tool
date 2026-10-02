using Xunit;
using static ParadoxModTool.ErrorLog.Tests.ErrorLogSessionTests;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogSessionActionsTests
{
    private static readonly DateTime FutureRun = DateTime.UtcNow.AddDays(1);

    private const string UnknownEffect = "[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:2";
    private const string Engine = "[17:00:05][game_gui_types.cpp:1540]: couldn't convert parameter value to something we recognise";
    private const string Optimization = "[16:59:01][database_object_reflection.h:694]: Optimization warning: Database static size differs from the actual number of elements, might be wasteful or cause slowdowns! Type: class CBuildingType, Num: 519, Expected: 525";
    private const string Unmatched = "[10:00:00][x.cpp:1]: Error building from string ( TYPE = NCoat )";

    [Fact]
    public void SuppressionOptionsOfferKindNameModAndFile()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect, Unmatched), FutureRun);
        var effect = session.Entries[0];

        var options = session.SuppressionOptions(effect);

        Assert.Equal(
            new[]
            {
                new Suppression(SuppressionScope.Code, "EFF-001"),
                new Suppression(SuppressionScope.CodeAndName, "EFF-001", "cmf_suppress"),
                new Suppression(SuppressionScope.Mod, session.Mods[0].Directory),
                new Suppression(SuppressionScope.File, tree.PathOf("own/in_game/common/scripted_effects/purger.txt")),
            },
            options);
        Assert.Empty(session.SuppressionOptions(session.Entries[1]));
    }

    [Fact]
    public void SuppressMovesTheGroupToSuppressedAndSavesIt()
    {
        using var tree = OwnModTree();
        var path = tree.PathOf("state/errorlog/suppressions.json");
        var session = NewSession(tree, path);
        session.LoadRun("error.log", Lines(UnknownEffect, Engine), FutureRun);
        var changes = 0;
        session.Changed += (_, _) => changes++;

        session.Suppress(new Suppression(SuppressionScope.CodeAndName, "EFF-001", "cmf_suppress"));

        var sections = session.Sections(ErrorFilter.All);
        Assert.Equal(new[] { ErrorSectionKind.Engine, ErrorSectionKind.Suppressed }, sections.Select(s => s.Kind));
        Assert.Equal("EFF-001", sections[^1].Groups.Single().Code);
        Assert.Equal(1, changes);
        Assert.Single(SuppressionSet.Load(path).Items);
    }

    [Fact]
    public void RestoreRemovesEverySuppressionThatHitsTheEntry()
    {
        using var tree = OwnModTree();
        var path = tree.PathOf("state/errorlog/suppressions.json");
        var session = NewSession(tree, path);
        session.LoadRun("error.log", Lines(UnknownEffect, Engine), FutureRun);
        var effect = session.Entries[0];
        session.Suppress(new Suppression(SuppressionScope.Code, "EFF-001"));
        session.Suppress(new Suppression(SuppressionScope.File, tree.PathOf("own/in_game/common/scripted_effects/purger.txt")));
        session.Suppress(new Suppression(SuppressionScope.Code, "PRM-001"));

        var removed = session.Restore(effect);

        Assert.Equal(2, removed);
        Assert.False(session.IsSuppressed(effect));
        Assert.Equal(new[] { new Suppression(SuppressionScope.Code, "PRM-001") }, SuppressionSet.Load(path).Items);
    }

    [Fact]
    public void DefaultSuppressedEntriesCannotBeRestored()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(Optimization), FutureRun);
        var entry = session.Entries.Single();

        var section = Assert.Single(session.Sections(ErrorFilter.All));

        Assert.Equal(ErrorSectionKind.Suppressed, section.Kind);
        Assert.True(ErrorLogSession.IsDefaultSuppressed(entry));
        Assert.Equal(0, session.Restore(entry));
        Assert.True(session.IsSuppressed(entry));
    }

    [Fact]
    public void JumpTargetKeepsTheLoggedLineWhenTheFileIsUnchanged()
    {
        using var tree = OwnModTree();
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect, Engine), FutureRun);

        var target = session.JumpTargetOf(session.Entries[0]);

        Assert.Equal(new JumpTarget(tree.PathOf("own/in_game/common/scripted_effects/purger.txt"), 2, false), target);
        Assert.Null(session.JumpTargetOf(session.Entries[1]));
    }

    [Fact]
    public void JumpTargetUsesNearestLineWhenFileChangedSinceRun()
    {
        using var tree = OwnModTree();
        var file = tree.PathOf("own/in_game/common/scripted_effects/purger.txt");
        File.WriteAllText(file, "purger = {\n\t# neu\n\t# neu\n\t# neu\n\tcmf_suppress = { v = iv2_x }\n}\n");
        var session = NewSession(tree);
        session.LoadRun("error.log", Lines(UnknownEffect), DateTime.UtcNow.AddHours(-1));

        var target = session.JumpTargetOf(session.Entries[0]);

        Assert.Equal(new JumpTarget(file, 5, true), target);
    }
}
