using Xunit;
using static ParadoxModTool.ErrorLog.Tests.ErrorLogSessionTests;

namespace ParadoxModTool.ErrorLog.Tests;

public class ErrorLogMarksTests
{
    private static readonly DateTime FutureRun = DateTime.UtcNow.AddDays(1);

    private const string EffectLine2 = "[19:49:44][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:2";
    private const string EffectLine3 = "[19:49:45][jomini_effect.cpp:556]: Unknown effect cmf_suppress at common/scripted_effects/purger.txt:3";
    private const string GuiInCommonX = "[10:00:00][pdx_gui_factory.cpp:1]: common_x/a.gui:1 - something new";
    private const string VanillaGui = "[10:00:00][pdx_gui_factory.cpp:1]: gui/v.gui:1 - something new";

    [Fact]
    public void FilesAndFoldersOfOwnModsCarryCountAndWorstSeverity()
    {
        using var tree = MarksTree();
        var session = LoadedSession(tree);

        Assert.Equal(new ErrorFileMark(2, ErrorSeverity.Error), session.MarkForFile(tree.PathOf("own/in_game/common/scripted_effects/purger.txt")));
        Assert.Equal(new ErrorFileMark(2, ErrorSeverity.Error), session.MarkForFolder(tree.PathOf("own/in_game/common")));
        Assert.Equal(new ErrorFileMark(1, ErrorSeverity.Warning), session.MarkForFolder(tree.PathOf("own/in_game/common_x")));
        Assert.Equal(new ErrorFileMark(3, ErrorSeverity.Error), session.MarkForFolder(tree.PathOf("own") + Path.DirectorySeparatorChar));
        Assert.Null(session.MarkForFile(tree.PathOf("install/game/in_game/gui/v.gui")));
        Assert.Null(session.MarkForFolder(tree.PathOf("install")));
    }

    [Fact]
    public void FolderMarksStopAtSeparators()
    {
        using var tree = MarksTree();
        var session = LoadedSession(tree);

        Assert.Equal(2, session.MarkForFolder(tree.PathOf("own/in_game/common"))!.Count);
        Assert.Null(session.MarkForFolder(tree.PathOf("own/in_game/comm")));
        Assert.Equal(new[] { "GUI-000" }, Codes(session.Sections(ErrorFilter.All with { Path = tree.PathOf("own/in_game/common_x") })));
        Assert.Equal(new[] { "EFF-001" }, Codes(session.Sections(ErrorFilter.All with { Path = tree.PathOf("own/in_game/common") })));
        Assert.Equal(new[] { "GUI-000" }, Codes(session.Sections(ErrorFilter.All with { Path = tree.PathOf("own/in_game/common_x/a.gui") })));
    }

    [Fact]
    public void SuppressedErrorsLeaveNoMark()
    {
        using var tree = MarksTree();
        var session = LoadedSession(tree);
        var file = tree.PathOf("own/in_game/common/scripted_effects/purger.txt");
        Assert.NotNull(session.MarkForFile(file));

        session.Suppress(new Suppression(SuppressionScope.CodeAndName, "EFF-001", "cmf_suppress"));

        Assert.Null(session.MarkForFile(file));
        Assert.Equal(new ErrorFileMark(1, ErrorSeverity.Warning), session.MarkForFolder(tree.PathOf("own")));
        Assert.Empty(session.EntriesForFile(file));
    }

    [Fact]
    public void EntriesForFileListsEveryAttributionIncludingVanilla()
    {
        using var tree = MarksTree();
        var session = LoadedSession(tree);

        var vanilla = session.EntriesForFile(tree.PathOf("install/game/in_game/gui/v.gui").ToUpperInvariant());
        var own = session.EntriesForFile(tree.PathOf("own/in_game/common/scripted_effects/purger.txt"));

        Assert.Equal("GUI-000", Assert.Single(vanilla).Code);
        Assert.Equal(2, own.Count);
    }

    private static TempTree MarksTree() => new(
        ("own/in_game/common/scripted_effects/purger.txt", "purger = {\n\tcmf_suppress = { v = iv2_x }\n\tcmf_suppress = { v = iv2_x }\n}\n"),
        ("own/in_game/common_x/a.gui", "window = { }\n"),
        ("install/game/in_game/gui/v.gui", "window = { }\n"));

    private static ErrorLogSession LoadedSession(TempTree tree)
    {
        var session = new ErrorLogSession(Catalogs.Eu5, new SuppressionSet(), null, "de");
        var own = new ModSource("Own", tree.PathOf("own"), IsOwn: true, LoadOrder: 0);
        session.SetSources(new[] { own }, SourceResolver.VanillaRootsFor(tree.PathOf("install")).Where(Directory.Exists).ToList());
        session.LoadRun("error.log", Lines(EffectLine2, EffectLine3, GuiInCommonX, VanillaGui), FutureRun);
        return session;
    }

    private static string[] Codes(IReadOnlyList<ErrorSection> sections) =>
        sections.Where(s => s.Kind is not (ErrorSectionKind.Fixed or ErrorSectionKind.Suppressed)).SelectMany(s => s.Groups).Select(g => g.Code).ToArray();
}
